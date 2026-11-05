namespace Memshare.Diagnostics;

/// <summary>How serious a finding of <c>memshare doctor</c> is.</summary>
public enum Severity
{
    /// <summary>As it should be.</summary>
    Ok,

    /// <summary>Works, but deserves attention.</summary>
    Warning,

    /// <summary>Doesn't work as it should.</summary>
    Problem,
}

/// <summary>One check of <c>memshare doctor</c>.</summary>
/// <param name="Severity">How serious it is.</param>
/// <param name="Area">What was checked (git, a store, the agent, …).</param>
/// <param name="Message">What was found, and what to do when it isn't ok.</param>
public sealed record Finding(Severity Severity, string Area, string Message)
{
    public static Finding Ok(string area, string message) => new(Severity.Ok, area, message);

    public static Finding Warning(string area, string message) => new(Severity.Warning, area, message);

    public static Finding Problem(string area, string message) => new(Severity.Problem, area, message);
}
