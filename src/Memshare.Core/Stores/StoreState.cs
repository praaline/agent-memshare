using Memshare.Git;

namespace Memshare.Stores;

/// <summary>What git says about a store's checkout.</summary>
public static class StoreState
{
    /// <summary>
    /// How many changes on this machine are not published: changed or new files, plus commits not
    /// pushed yet (all of them when the store has never been pushed).
    /// </summary>
    public static async Task<int> CountUnpublishedAsync(GitRunner git, Store store, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(store);

        var status = await git.RunAsync(store.Directory, ["status", "--porcelain"], GitRunner.DefaultTimeout, cancellationToken)
            .ConfigureAwait(false);
        var changedFiles = status.Succeeded ? status.Lines.Count : 0;

        var ahead = await git.RunAsync(store.Directory, ["rev-list", "--count", "@{u}..HEAD"], GitRunner.DefaultTimeout, cancellationToken)
            .ConfigureAwait(false);
        if (!ahead.Succeeded)
        {
            // No upstream yet: every local commit is unpublished (none in an empty repository).
            ahead = await git.RunAsync(store.Directory, ["rev-list", "--count", "HEAD"], GitRunner.DefaultTimeout, cancellationToken)
                .ConfigureAwait(false);
        }

        var commits = ahead.Succeeded && int.TryParse(ahead.Output, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0;
        return changedFiles + commits;
    }

    /// <summary>The URL of the store's remote <c>origin</c>, if it has one.</summary>
    public static async Task<string?> OriginAsync(GitRunner git, Store store, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(store);
        var result = await git.RunAsync(store.Directory, ["remote", "get-url", "origin"], GitRunner.DefaultTimeout, cancellationToken)
            .ConfigureAwait(false);
        return result.Succeeded ? result.Output : null;
    }
}
