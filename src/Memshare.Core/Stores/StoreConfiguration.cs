using Memshare.Git;

namespace Memshare.Stores;

/// <summary>
/// The git settings of a store's clone: rebase on pull, the neutral author, the deploy key (SSH), and the
/// merge driver for indexes (ADR-0005). Git never takes drivers from a repository, for safety: each
/// clone registers it.
/// </summary>
public static class StoreConfiguration
{
    /// <summary>The merge driver's name, as <c>.gitattributes</c> refers to it.</summary>
    public const string MergeDriverName = "memshare-index";

    /// <summary>The <c>.gitattributes</c> line that routes indexes to the driver.</summary>
    public const string AttributesLine = "**/MEMORY.md merge=" + MergeDriverName;

    /// <summary>The driver command for an executable: git passes ancestor, ours, theirs; the result replaces ours.</summary>
    public static string MergeDriverCommand(string executablePath)
    {
        ArgumentNullException.ThrowIfNull(executablePath);
        return $"\"{executablePath.Replace('\\', '/')}\" merge-driver %O %A %B";
    }

    /// <summary>Writes the store's git settings.</summary>
    /// <param name="git">The user's git.</param>
    /// <param name="store">The store.</param>
    /// <param name="executablePath">The installed memshare executable (the merge driver).</param>
    /// <param name="sshCommand">The SSH command with the store's deploy key; null to leave it as it is.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public static async Task ConfigureAsync(GitRunner git, Store store, string executablePath, string? sshCommand, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(store);
        List<(string Key, string Value)> settings =
        [
            ("pull.rebase", "true"),
            ("user.name", StoreGit.AuthorName),
            ("user.email", StoreGit.AuthorEmail),
            ($"merge.{MergeDriverName}.name", "memshare: merge MEMORY.md indexes entry by entry"),
            ($"merge.{MergeDriverName}.driver", MergeDriverCommand(executablePath)),
        ];
        if (sshCommand is not null)
        {
            settings.Add(("core.sshCommand", sshCommand));
        }

        foreach (var (key, value) in settings)
        {
            var result = await git.RunAsync(store.Directory, ["config", key, value], GitRunner.DefaultTimeout, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw new MemshareException($"Could not configure store {store.Name} ({key}): {StoreSync.FirstLine(result.StandardError)}");
            }
        }
    }

    /// <summary>The merge driver command configured in a clone, if any.</summary>
    public static async Task<string?> ConfiguredDriverAsync(GitRunner git, Store store, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(store);
        var result = await git.RunAsync(store.Directory, ["config", "--get", $"merge.{MergeDriverName}.driver"], GitRunner.DefaultTimeout, cancellationToken)
            .ConfigureAwait(false);
        return result.Succeeded ? result.Output : null;
    }

    /// <summary>Whether the store's <c>.gitattributes</c> sends indexes to the driver.</summary>
    public static bool UsesMergeDriver(Store store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var path = Path.Combine(store.Directory, ".gitattributes");
        return File.Exists(path) && File.ReadLines(path).Any(l => l.Trim() == AttributesLine);
    }
}
