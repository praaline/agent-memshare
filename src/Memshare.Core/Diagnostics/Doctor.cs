using Memshare.Agents;
using Memshare.Configuration;
using Memshare.Git;
using Memshare.Platform;
using Memshare.Projects;
using Memshare.Stores;

namespace Memshare.Diagnostics;

/// <summary><c>memshare doctor</c>: checks this machine's setup and says how to fix what's wrong.</summary>
/// <param name="environment">The machine.</param>
/// <param name="git">The user's git.</param>
/// <param name="integrations">The agents to check.</param>
/// <param name="executablePath">The installed executable.</param>
/// <param name="time">The clock.</param>
public sealed class Doctor(
    MemshareEnvironment environment,
    GitRunner git,
    IReadOnlyList<IAgentIntegration> integrations,
    string executablePath,
    TimeProvider time)
{
    /// <summary>Runs every check.</summary>
    /// <param name="checkRemotes">Whether to contact each store's remote.</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    public async Task<IReadOnlyList<Finding>> CheckAsync(bool checkRemotes, CancellationToken cancellationToken)
    {
        var findings = new List<Finding>();

        var version = await git.RunAsync(environment.HomeDirectory, ["--version"], TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
        findings.Add(version.Succeeded ? Finding.Ok("git", version.Output) : Finding.Problem("git", "git does not run: install Git."));

        MemshareConfig config;
        try
        {
            config = ConfigFile.Load(environment);
        }
        catch (MemshareException e)
        {
            findings.Add(Finding.Problem("configuration", e.Message));
            return findings;
        }

        findings.Add(config.Machine is null
            ? Finding.Problem("configuration", "no machine name: run `memshare install --machine <name>`.")
            : Finding.Ok("configuration", $"machine {config.Machine}, stores in {config.StoresRoot}"));

        var catalog = StoreCatalog.Discover(environment.ExpandPath(config.StoresRoot));
        if (catalog.Stores.Count == 0)
        {
            findings.Add(Finding.Warning("stores", $"no store in {DisplayPath.Of(catalog.Root)}: add one with `memshare store add <url>`."));
        }

        foreach (var store in catalog.Stores)
        {
            findings.AddRange(await CheckStoreAsync(store, checkRemotes, cancellationToken).ConfigureAwait(false));
        }

        var context = new InstallContext(environment, config, catalog, executablePath, time);
        foreach (var integration in integrations)
        {
            findings.AddRange(integration.Check(context));
        }

        var root = await ProjectLink.FindProjectRootAsync(git, environment.ProjectDirectory ?? environment.CurrentDirectory, cancellationToken).ConfigureAwait(false);
        if (ProjectLink.Read(root) is { } link)
        {
            findings.Add(catalog.Find(link.StoreName) is null
                ? Finding.Problem("this project", $"linked to store {link.StoreName}, which is not installed: `memshare store add <url>` or `memshare link`.")
                : Finding.Ok("this project", $"linked to {link.StoreName}/projects/{link.Slug}"));
        }

        return findings;
    }

    private async Task<IReadOnlyList<Finding>> CheckStoreAsync(Store store, bool checkRemotes, CancellationToken cancellationToken)
    {
        var area = $"store {store.Name}";
        var findings = new List<Finding>();

        var format = StoreManifest.ReadFormat(store);
        findings.Add(format switch
        {
            null => Finding.Warning(area, $"no {StoreManifest.FileName}: run `memshare store init {store.Name}` and publish."),
            > StoreManifest.SupportedFormat => Finding.Problem(area, $"format {format} is newer than this memshare understands: update memshare."),
            _ => Finding.Ok(area, $"format {format}"),
        });

        if (!StoreConfiguration.UsesMergeDriver(store))
        {
            findings.Add(Finding.Warning(area, $".gitattributes does not send indexes to memshare's merge driver: add the line `{StoreConfiguration.AttributesLine}` and publish."));
        }

        var driver = await StoreConfiguration.ConfiguredDriverAsync(git, store, cancellationToken).ConfigureAwait(false);
        findings.Add(driver == StoreConfiguration.MergeDriverCommand(executablePath)
            ? Finding.Ok(area, "merge driver configured")
            : Finding.Problem(area, "the merge driver is not configured for the installed executable: run `memshare install`."));

        var origin = await StoreState.OriginAsync(git, store, cancellationToken).ConfigureAwait(false);
        if (origin is null)
        {
            findings.Add(Finding.Problem(area, "no remote `origin`: the store can't be published."));
            return findings;
        }

        var sshCommand = await git.RunAsync(store.Directory, ["config", "--get", "core.sshCommand"], GitRunner.DefaultTimeout, cancellationToken).ConfigureAwait(false);
        if (StoreUrl.Parse(origin).IsSsh)
        {
            if (!sshCommand.Succeeded)
            {
                findings.Add(Finding.Warning(area, "SSH remote without its own deploy key (core.sshCommand): your personal SSH setup is used."));
            }
            else if (ExtractKey(sshCommand.Output) is var key && (key.Length == 0 || !File.Exists(key)))
            {
                findings.Add(Finding.Problem(area, $"the deploy key named in core.sshCommand does not exist ({sshCommand.Output})."));
            }
            else
            {
                findings.Add(Finding.Ok(area, $"deploy key {key}"));
            }
        }

        if (checkRemotes)
        {
            var probe = await git.RunAsync(store.Directory, ["ls-remote", "--heads", "origin"], TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
            findings.Add(probe.Succeeded
                ? Finding.Ok(area, $"remote reachable ({origin})")
                : Finding.Problem(area, $"remote not reachable ({origin}): {StoreSync.FirstLine(probe.StandardError)}"));
        }

        var unpublished = await StoreState.CountUnpublishedAsync(git, store, cancellationToken).ConfigureAwait(false);
        if (unpublished > 0)
        {
            findings.Add(Finding.Warning(area, $"{unpublished} unpublished change(s): `memshare diff {store.Name}`."));
        }

        var copies = (await git.RunAsync(store.Directory, ["ls-files", $"*{ConflictCopies.Marker}*"], GitRunner.DefaultTimeout, cancellationToken).ConfigureAwait(false)).Lines;
        if (copies.Count > 0)
        {
            findings.Add(Finding.Warning(area, $"conflict copies to merge: {string.Join(", ", copies)}."));
        }

        var lockFile = StoreLock.PathOf(store);
        if (File.Exists(lockFile) && DateTime.UtcNow - File.GetLastWriteTimeUtc(lockFile) > StoreLock.StaleAfter)
        {
            findings.Add(Finding.Warning(area, "an abandoned lock (it will be taken over by the next operation)."));
        }

        return findings;
    }

    /// <summary>The identity file of an SSH command (<c>-i path</c> or <c>-i "path"</c>); empty if none.</summary>
    internal static string ExtractKey(string sshCommand)
    {
        var start = sshCommand.IndexOf("-i ", StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var rest = sshCommand[(start + 3)..].TrimStart();
        if (rest.StartsWith('"') || rest.StartsWith('\''))
        {
            var end = rest.IndexOf(rest[0], 1);
            return end < 0 ? string.Empty : rest[1..end];
        }

        var space = rest.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? rest : rest[..space];
    }
}
