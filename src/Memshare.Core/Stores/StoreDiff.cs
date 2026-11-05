using Memshare.Git;

namespace Memshare.Stores;

/// <summary><c>memshare diff</c>: what publishing a store would send.</summary>
public static class StoreDiff
{
    /// <summary>
    /// The unpublished commits, then the uncommitted changes, new files included (they are marked
    /// "intent to add" for that, without staging their content).
    /// </summary>
    public static async Task<string> DescribeAsync(GitRunner git, Store store, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(store);
        await git.RunAsync(store.Directory, ["add", "--all", "--intent-to-add"], GitRunner.DefaultTimeout, cancellationToken).ConfigureAwait(false);

        var parts = new List<string>();
        var commits = await git.RunAsync(store.Directory, ["log", "--oneline", "--no-color", "@{u}..HEAD"], GitRunner.DefaultTimeout, cancellationToken)
            .ConfigureAwait(false);
        if (commits.Succeeded && commits.Output.Length > 0)
        {
            parts.Add("Committed, not published:\n" + commits.Output);
        }

        var changes = await git.RunAsync(
            store.Directory,
            ["-c", "core.quotepath=false", "diff", "--no-color", "--no-ext-diff", "--stat", "--patch", "HEAD"],
            GitRunner.DefaultTimeout,
            cancellationToken).ConfigureAwait(false);
        if (!changes.Succeeded)
        {
            // No commit yet: compare with nothing.
            changes = await git.RunAsync(store.Directory, ["-c", "core.quotepath=false", "diff", "--no-color", "--stat", "--patch"], GitRunner.DefaultTimeout, cancellationToken)
                .ConfigureAwait(false);
        }

        if (changes.Output.Length > 0)
        {
            parts.Add(changes.Output);
        }

        return parts.Count == 0 ? $"Store {store.Name}: nothing unpublished." : string.Join("\n\n", parts);
    }
}
