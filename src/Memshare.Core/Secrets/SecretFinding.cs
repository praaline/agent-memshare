namespace Memshare.Secrets;

/// <summary>A value that looks like a secret, in a change about to be published.</summary>
/// <param name="Path">The file, relative to the store.</param>
/// <param name="LineNumber">The line.</param>
/// <param name="Kind">What it looks like (for example "GitHub token").</param>
/// <param name="Masked">The value with all but its first characters hidden.</param>
public sealed record SecretFinding(string Path, int LineNumber, string Kind, string Masked);
