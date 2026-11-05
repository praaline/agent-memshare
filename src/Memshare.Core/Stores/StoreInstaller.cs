using Memshare.Agents;
using Memshare.Configuration;
using Memshare.Git;
using Memshare.Platform;

namespace Memshare.Stores;

/// <summary>The outcome of <c>memshare store add</c>.</summary>
/// <param name="Store">The store, when it is installed.</param>
/// <param name="PublicKey">The deploy key to register on the host, when the host doesn't accept it yet.</param>
/// <param name="IsEmpty">Whether the store has no content yet (then <c>memshare store init</c>).</param>
/// <param name="NeedsInit">Whether the store has content but no <c>memshare.json</c>.</param>
public sealed record StoreAddResult(Store? Store, string? PublicKey, bool IsEmpty, bool NeedsInit);

/// <summary><c>memshare store add</c>, <c>init</c> and <c>remove</c>.</summary>
/// <param name="environment">The machine.</param>
/// <param name="git">The user's git.</param>
/// <param name="integrations">The agents whose imported instructions follow the stores.</param>
/// <param name="executablePath">The installed executable (for the agents' context).</param>
/// <param name="time">The clock.</param>
public sealed class StoreInstaller(
    MemshareEnvironment environment,
    GitRunner git,
    IReadOnlyList<IAgentIntegration> integrations,
    string executablePath,
    TimeProvider time)
{
    /// <summary>Clones a store onto this machine.</summary>
    /// <param name="url">The store's remote.</param>
    /// <param name="name">The store's name; by default the repository's name.</param>
    /// <param name="hostKey">The SSH host key line to pin, instead of reading it from the host.</param>
    /// <param name="trustHostKey">Asked to confirm a host key read from the host (its fingerprint); false refuses it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public async Task<StoreAddResult> AddAsync(
        string url,
        string? name,
        string? hostKey,
        Func<string, bool> trustHostKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trustHostKey);
        var remote = StoreUrl.Parse(url);
        name ??= remote.DefaultName;
        if (!Projects.ProjectLink.IsValidStoreName(name))
        {
            throw new MemshareException($"'{name}' is not a valid store name: use letters, digits, '.', '_' and '-'. Give one with --name.");
        }

        var config = ConfigFile.Load(environment);
        var root = environment.ExpandPath(config.StoresRoot);
        var target = Path.Combine(root, name);
        if (Path.Exists(target))
        {
            throw new MemshareException($"{DisplayPath.Of(target)} exists already: remove it, or choose another name with --name.");
        }

        string? sshCommand = null;
        if (remote.IsSsh)
        {
            var publicKey = await SshAccess.EnsureKeyAsync(environment, name, config.Machine ?? Environment.MachineName.ToLowerInvariant(), cancellationToken)
                .ConfigureAwait(false);
            if (!SshAccess.IsPinned(environment, remote))
            {
                string line;
                if (hostKey is not null)
                {
                    line = hostKey;
                    await SshAccess.FingerprintAsync(environment, line, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var (scanned, fingerprint) = await SshAccess.ScanAsync(environment, remote, cancellationToken).ConfigureAwait(false);
                    if (!trustHostKey(fingerprint))
                    {
                        throw new MemshareException("The host key was not trusted: nothing was installed. Check the fingerprint with the host's administrator, or give the key with --host-key.");
                    }

                    line = scanned;
                }

                SshAccess.Pin(environment, remote, line);
            }

            sshCommand = SshAccess.SshCommand(SshAccess.KeyPath(environment, name), environment.KnownHostsPath);
            var probe = await SshAccess.ProbeAsync(git, environment, remote.Url, sshCommand, cancellationToken).ConfigureAwait(false);
            if (!probe.Succeeded)
            {
                if (probe.TimedOut || probe.StandardError.Contains("Could not resolve", StringComparison.OrdinalIgnoreCase)
                    || probe.StandardError.Contains("timed out", StringComparison.OrdinalIgnoreCase))
                {
                    throw new MemshareException($"Could not reach {remote.KnownHostsName}: {StoreSync.FirstLine(probe.StandardError)}");
                }

                if (probe.StandardError.Contains("Host key verification failed", StringComparison.OrdinalIgnoreCase))
                {
                    throw new MemshareException($"The host key of {remote.KnownHostsName} does not match the pinned one in {DisplayPath.Of(environment.KnownHostsPath)}. If the host's key really changed, remove its line there and run the command again.");
                }

                return new StoreAddResult(null, publicKey, false, false);
            }
        }

        Directory.CreateDirectory(root);
        List<string> clone = sshCommand is null ? ["clone", "--quiet"] : ["-c", $"core.sshCommand={sshCommand}", "clone", "--quiet"];
        var cloned = await git.RunAsync(root, [.. clone, remote.Url, target], GitRunner.DefaultTimeout, cancellationToken).ConfigureAwait(false);
        if (!cloned.Succeeded)
        {
            throw new MemshareException($"Could not clone {remote.Url}: {StoreSync.FirstLine(cloned.StandardError)}");
        }

        var store = new Store(name, target);
        await StoreConfiguration.ConfigureAsync(git, store, executablePath, sshCommand, cancellationToken).ConfigureAwait(false);

        var format = StoreManifest.ReadFormat(store);
        if (format > StoreManifest.SupportedFormat)
        {
            DeleteDirectory(target);
            throw new MemshareException($"Store {name} has format {format}; this version of memshare understands up to {StoreManifest.SupportedFormat}. Update memshare, then add it again.");
        }

        var isEmpty = !(await git.RunAsync(target, ["rev-parse", "--quiet", "--verify", "HEAD"], GitRunner.DefaultTimeout, cancellationToken).ConfigureAwait(false)).Succeeded;
        RefreshAgents();
        return new StoreAddResult(store, null, isEmpty, !isEmpty && format is null);
    }

    /// <summary>
    /// Adds the template's files that a store lacks (never overwriting one), for a new store or one
    /// made before memshare.json existed. The user reviews and publishes them.
    /// </summary>
    public IReadOnlyList<string> Init(string? storeName)
    {
        var store = Catalog().Pick(storeName);
        var added = new List<string>();
        foreach (var (relative, content) in StoreTemplate.Files)
        {
            var path = Path.Combine(store.Directory, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            added.Add(relative);
        }

        RefreshAgents();
        return added;
    }

    /// <summary>Removes a store's checkout from this machine (its remote is untouched).</summary>
    /// <exception cref="MemshareException">The store has unpublished changes and <paramref name="force"/> is false.</exception>
    public async Task<Store> RemoveAsync(string storeName, bool force, CancellationToken cancellationToken)
    {
        var store = Catalog().Pick(storeName);
        var unpublished = await StoreState.CountUnpublishedAsync(git, store, cancellationToken).ConfigureAwait(false);
        if (unpublished > 0 && !force)
        {
            throw new MemshareException($"Store {store.Name} has {unpublished} unpublished change(s): publish them, or remove it anyway with --force.");
        }

        DeleteDirectory(store.Directory);
        RefreshAgents();
        return store;
    }

    private StoreCatalog Catalog() => StoreCatalog.Discover(environment.ExpandPath(ConfigFile.Load(environment).StoresRoot));

    private void RefreshAgents()
    {
        var context = new InstallContext(environment, ConfigFile.Load(environment), Catalog(), executablePath, time);
        foreach (var integration in integrations)
        {
            integration.RefreshInstructions(context);
        }
    }

    internal static void DeleteDirectory(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}
