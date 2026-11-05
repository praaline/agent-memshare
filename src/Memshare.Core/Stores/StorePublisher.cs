using Memshare.Git;

namespace Memshare.Stores;

/// <summary>
/// <c>memshare publish</c>: commits a store's changes, merges them with the remote and pushes them. The
/// only operation that sends anything (ADR-0002).
/// </summary>
/// <param name="git">The user's git.</param>
/// <param name="time">The clock, for the names of conflict copies.</param>
/// <param name="lockWait">How long to wait for another operation on the store; <see cref="LockWait"/> by default.</param>
public sealed class StorePublisher(GitRunner git, TimeProvider time, TimeSpan? lockWait = null)
{
    /// <summary>How long pushing may take.</summary>
    public static readonly TimeSpan PushTimeout = TimeSpan.FromSeconds(30);

    /// <summary>How long publish waits for another operation on the store to finish.</summary>
    public static readonly TimeSpan LockWait = TimeSpan.FromSeconds(10);

    private readonly GitRunner _git = StoreGit.For(git);

    /// <summary>Publishes a store.</summary>
    /// <exception cref="MemshareException">
    /// The store is busy, a file contains conflict markers or what looks like a secret, or the remote could not be reached (the
    /// changes are then committed locally and the next publish sends them).
    /// </exception>
    /// <param name="store">The store.</param>
    /// <param name="message">What the change is.</param>
    /// <param name="allowSecrets">Publish even values that look like secrets (ADR-0006).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public async Task<PublishResult> PublishAsync(Store store, string message, bool allowSecrets, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        using var storeLock = await StoreLock.AcquireAsync(store, lockWait ?? LockWait, cancellationToken).ConfigureAwait(false);

        await Run(store, cancellationToken, "add", "--all").ConfigureAwait(false);
        var staged = !(await _git.RunAsync(store.Directory, ["diff", "--cached", "--quiet"], GitRunner.DefaultTimeout, cancellationToken)
            .ConfigureAwait(false)).Succeeded;

        var summary = string.Empty;
        if (staged)
        {
            var added = await StagedChanges.AddedLinesAsync(_git, store, cancellationToken).ConfigureAwait(false);
            var conflicted = ConflictMarkers.FilesWithMarkers(added);
            if (conflicted.Count > 0)
            {
                throw new MemshareException(
                    $"Not published: conflict markers in {string.Join(", ", conflicted)}. Merge the versions, remove the markers, then publish again.");
            }

            var secrets = allowSecrets ? [] : Secrets.SecretScanner.Scan(added);
            if (secrets.Count > 0)
            {
                var list = string.Join("\n", secrets.Select(f => $"  {f.Path}:{f.LineNumber}: {f.Kind} ({f.Masked})"));
                throw new MemshareException(
                    $"Not published: what looks like a secret:\n{list}\nRemove it. If it is not a secret, mark the line with `{Secrets.SecretScanner.AllowMarker}`, or publish with --allow-secrets.");
            }

            summary = (await _git.RunAsync(store.Directory, ["diff", "--cached", "--stat", "--no-color"], GitRunner.DefaultTimeout, cancellationToken)
                .ConfigureAwait(false)).Output;
            await Run(store, cancellationToken, "commit", "--quiet", "--message", message).ConfigureAwait(false);
        }

        var sync = new StoreSync(git, time);
        var pull = await sync.PullAsync(store, cancellationToken).ConfigureAwait(false);
        if (!pull.Succeeded)
        {
            throw new MemshareException(
                $"Could not update store {store.Name} from its remote ({pull.Error}). The changes are committed on this machine: publish again later.");
        }

        var pushed = false;
        if (await NeedsPushAsync(store, sync, cancellationToken).ConfigureAwait(false))
        {
            var push = await _git.RunAsync(store.Directory, ["push", "--quiet", "--set-upstream", "origin", "HEAD"], PushTimeout, cancellationToken)
                .ConfigureAwait(false);
            if (!push.Succeeded)
            {
                var why = push.TimedOut ? "the remote did not answer in time" : StoreSync.FirstLine(push.StandardError);
                throw new MemshareException(
                    $"Could not send store {store.Name} to its remote ({why}). The changes are committed on this machine: publish again later.");
            }

            pushed = true;
        }

        var head = (await _git.RunAsync(store.Directory, ["rev-parse", "--short", "HEAD"], GitRunner.DefaultTimeout, cancellationToken)
            .ConfigureAwait(false)).Output;
        var copies = (await _git.RunAsync(store.Directory, ["-c", "core.quotepath=false", "ls-files", $"*{ConflictCopies.Marker}*"], GitRunner.DefaultTimeout, cancellationToken)
            .ConfigureAwait(false)).Lines;
        return new PublishResult(staged, summary, pushed, head, copies);
    }

    private async Task<bool> NeedsPushAsync(Store store, StoreSync sync, CancellationToken cancellationToken)
    {
        if (!await sync.HasUpstreamAsync(store, cancellationToken).ConfigureAwait(false))
        {
            // Never pushed: push if there is anything at all.
            return (await _git.RunAsync(store.Directory, ["rev-parse", "--quiet", "--verify", "HEAD"], GitRunner.DefaultTimeout, cancellationToken)
                .ConfigureAwait(false)).Succeeded;
        }

        var ahead = await _git.RunAsync(store.Directory, ["rev-list", "--count", "@{u}..HEAD"], GitRunner.DefaultTimeout, cancellationToken)
            .ConfigureAwait(false);
        return !ahead.Succeeded || ahead.Output != "0";
    }

    private async Task Run(Store store, CancellationToken cancellationToken, params string[] arguments)
    {
        var result = await _git.RunAsync(store.Directory, arguments, GitRunner.DefaultTimeout, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new MemshareException($"git {arguments[0]} failed in store {store.Name}: {StoreSync.FirstLine(result.StandardError)}");
        }
    }
}
