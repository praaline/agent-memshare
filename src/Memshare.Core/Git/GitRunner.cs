using Memshare.Platform;

namespace Memshare.Git;

/// <summary>
/// Runs the user's <c>git</c> executable (ADR-0003), so that their SSH configuration, credential
/// helpers and each repository's <c>core.sshCommand</c> apply. git never prompts: a command that
/// would need input fails instead of waiting.
/// </summary>
/// <param name="environment">
/// Variables to set for git (a null value removes the variable). Tests use it to isolate git from the
/// user's own configuration.
/// </param>
public sealed class GitRunner(IReadOnlyDictionary<string, string?>? environment = null)
{
    /// <summary>How long a command may take when the caller gives no limit.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    private readonly IReadOnlyDictionary<string, string?> _environment =
        environment ?? new Dictionary<string, string?>();

    /// <summary>The variables this runner sets, for other programs that must see the same environment.</summary>
    public IReadOnlyDictionary<string, string?> Environment => _environment;

    /// <summary>A runner with more variables set (or removed, with a null value) for git.</summary>
    public GitRunner WithEnvironment(IReadOnlyDictionary<string, string?> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        var merged = new Dictionary<string, string?>(_environment);
        foreach (var (name, value) in variables)
        {
            merged[name] = value;
        }

        return new GitRunner(merged);
    }

    /// <summary>Runs <c>git</c> with the given arguments in a directory.</summary>
    /// <exception cref="MemshareException">git is not installed or cannot be started.</exception>
    public Task<GitResult> RunAsync(string workingDirectory, params IEnumerable<string> arguments) =>
        RunAsync(workingDirectory, arguments, DefaultTimeout, CancellationToken.None);

    /// <summary>Runs <c>git</c> with the given arguments in a directory, within a time limit.</summary>
    /// <exception cref="MemshareException">git is not installed or cannot be started.</exception>
    public Task<GitResult> RunAsync(
        string workingDirectory,
        IEnumerable<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var variables = new Dictionary<string, string?>(_environment)
        {
            ["GIT_TERMINAL_PROMPT"] = "0",
            ["GCM_INTERACTIVE"] = "never",
        };
        return ToolRunner.RunAsync("git", workingDirectory, arguments, variables, timeout, cancellationToken);
    }
}
