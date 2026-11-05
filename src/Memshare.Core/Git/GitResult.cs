namespace Memshare.Git;

/// <summary>The outcome of one git command.</summary>
/// <param name="ExitCode">git's exit code; -1 when it was stopped for taking too long.</param>
/// <param name="StandardOutput">What git wrote to standard output.</param>
/// <param name="StandardError">What git wrote to standard error.</param>
/// <param name="TimedOut">Whether the command was stopped for taking too long.</param>
public sealed record GitResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut)
{
    /// <summary>Whether git finished in time and reported success.</summary>
    public bool Succeeded => ExitCode == 0 && !TimedOut;

    /// <summary>Standard output without the trailing line break.</summary>
    public string Output => StandardOutput.TrimEnd('\r', '\n');

    /// <summary>The non-empty lines of standard output.</summary>
    public IReadOnlyList<string> Lines =>
        StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
