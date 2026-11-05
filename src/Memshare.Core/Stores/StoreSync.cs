using Memshare.Git;

namespace Memshare.Stores;

/// <summary>
/// Brings a store up to date with its remote. Changes on this machine stay as they are: committed ones
/// are rebased on the remote, uncommitted ones are set aside and put back (still unpublished). When
/// both sides changed the same file, the remote's version stays in place and this machine's becomes a
/// conflict copy (ADR-0005): nothing is lost and no conflict marker is written.
/// </summary>
/// <param name="git">The user's git.</param>
/// <param name="time">The clock, for the names of conflict copies.</param>
public sealed class StoreSync(GitRunner git, TimeProvider time)
{
    /// <summary>How long fetching from the remote may take.</summary>
    public static readonly TimeSpan PullTimeout = TimeSpan.FromSeconds(15);

    private const int MaxRebaseSteps = 100;

    private readonly GitRunner _git = StoreGit.For(git);

    /// <summary>Updates a store from its remote. The caller holds the store's lock.</summary>
    public async Task<PullResult> PullAsync(Store store, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (!await HasUpstreamAsync(store, cancellationToken).ConfigureAwait(false))
        {
            return PullResult.Ok(); // A remote nobody has published to yet.
        }

        var pull = await _git.RunAsync(
            store.Directory, ["pull", "--quiet", "--rebase", "--autostash"], PullTimeout, cancellationToken).ConfigureAwait(false);
        if (pull.Succeeded)
        {
            return PullResult.Ok();
        }

        if (IsRebasing(store))
        {
            return await KeepBothAsync(store, cancellationToken).ConfigureAwait(false);
        }

        return PullResult.Failed(pull.TimedOut ? "the remote did not answer in time" : FirstLine(pull.StandardError));
    }

    internal static bool IsRebasing(Store store) =>
        Directory.Exists(Path.Combine(store.Directory, ".git", "rebase-merge"))
        || Directory.Exists(Path.Combine(store.Directory, ".git", "rebase-apply"));

    internal async Task<bool> HasUpstreamAsync(Store store, CancellationToken cancellationToken) =>
        (await _git.RunAsync(store.Directory, ["rev-parse", "--quiet", "--verify", "@{u}"], GitRunner.DefaultTimeout, cancellationToken)
            .ConfigureAwait(false)).Succeeded;

    /// <summary>
    /// Finishes a rebase that stopped on conflicts. During a rebase, stage 2 ("ours") is the remote's
    /// version and stage 3 ("theirs") is this machine's commit being replayed.
    /// </summary>
    private async Task<PullResult> KeepBothAsync(Store store, CancellationToken cancellationToken)
    {
        var copies = new List<string>();
        for (var step = 0; step < MaxRebaseSteps && IsRebasing(store); step++)
        {
            foreach (var file in await UnmergedFilesAsync(store, cancellationToken).ConfigureAwait(false))
            {
                var mine = await _git.RunAsync(store.Directory, ["show", $":3:{file}"], GitRunner.DefaultTimeout, cancellationToken)
                    .ConfigureAwait(false);
                if (mine.Succeeded)
                {
                    var copy = ConflictCopies.NameFor(file, time.GetUtcNow());
                    await File.WriteAllTextAsync(Path.Combine(store.Directory, copy), mine.StandardOutput, cancellationToken).ConfigureAwait(false);
                    await Run(store, cancellationToken, "add", "--", copy).ConfigureAwait(false);
                    copies.Add(copy);
                }

                var remoteHasIt = (await _git.RunAsync(store.Directory, ["cat-file", "-e", $":2:{file}"], GitRunner.DefaultTimeout, cancellationToken)
                    .ConfigureAwait(false)).Succeeded;
                if (remoteHasIt)
                {
                    await Run(store, cancellationToken, "checkout", "--ours", "--", file).ConfigureAwait(false);
                    await Run(store, cancellationToken, "add", "--", file).ConfigureAwait(false);
                }
                else
                {
                    await Run(store, cancellationToken, "rm", "--quiet", "--cached", "--ignore-unmatch", "--", file).ConfigureAwait(false);
                    File.Delete(Path.Combine(store.Directory, file));
                }
            }

            var next = await _git.RunAsync(store.Directory, ["rebase", "--continue"], GitRunner.DefaultTimeout, cancellationToken)
                .ConfigureAwait(false);
            if (!next.Succeeded && IsRebasing(store) && (await UnmergedFilesAsync(store, cancellationToken).ConfigureAwait(false)).Count == 0)
            {
                // The replayed commit is now empty: everything it changed is in the remote already.
                await _git.RunAsync(store.Directory, ["rebase", "--skip"], GitRunner.DefaultTimeout, cancellationToken).ConfigureAwait(false);
            }
        }

        if (IsRebasing(store))
        {
            await _git.RunAsync(store.Directory, ["rebase", "--abort"], GitRunner.DefaultTimeout, cancellationToken).ConfigureAwait(false);
            return PullResult.Failed("the changes could not be merged automatically; the store was left as it was before");
        }

        return PullResult.Ok(copies);
    }

    private async Task<IReadOnlyList<string>> UnmergedFilesAsync(Store store, CancellationToken cancellationToken)
    {
        var result = await _git.RunAsync(store.Directory, ["diff", "--name-only", "-z", "--diff-filter=U"], GitRunner.DefaultTimeout, cancellationToken)
            .ConfigureAwait(false);
        return result.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToList();
    }

    private async Task Run(Store store, CancellationToken cancellationToken, params string[] arguments)
    {
        var result = await _git.RunAsync(store.Directory, arguments, GitRunner.DefaultTimeout, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new MemshareException($"git {string.Join(' ', arguments)} failed in store {store.Name}: {FirstLine(result.StandardError)}");
        }
    }

    internal static string FirstLine(string text)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return string.IsNullOrEmpty(line) ? "git failed without a message" : line;
    }
}
