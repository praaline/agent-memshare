namespace Memshare.Stores;

/// <summary>The outcome of publishing a store.</summary>
/// <param name="Committed">Whether changes were committed by this publish.</param>
/// <param name="Summary">What was committed (<c>git diff --stat</c>), empty when nothing was.</param>
/// <param name="Pushed">Whether commits were sent to the remote.</param>
/// <param name="Head">The store's commit after publishing (short hash).</param>
/// <param name="ConflictCopies">Conflict copies in the store, to be merged.</param>
public sealed record PublishResult(bool Committed, string Summary, bool Pushed, string Head, IReadOnlyList<string> ConflictCopies);
