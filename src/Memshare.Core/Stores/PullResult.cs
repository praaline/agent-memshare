namespace Memshare.Stores;

/// <summary>The outcome of updating a store from its remote.</summary>
/// <param name="Succeeded">Whether the store is now up to date with its remote.</param>
/// <param name="Error">Why not, when it failed.</param>
/// <param name="ConflictCopies">Conflict copies made while merging, relative to the store.</param>
public sealed record PullResult(bool Succeeded, string? Error, IReadOnlyList<string> ConflictCopies)
{
    public static PullResult Ok(IReadOnlyList<string>? copies = null) => new(true, null, copies ?? []);

    public static PullResult Failed(string error) => new(false, error, []);
}
