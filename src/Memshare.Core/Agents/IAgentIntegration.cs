using Memshare.Diagnostics;

namespace Memshare.Agents;

/// <summary>
/// How memshare connects to one coding agent (ADR-0007): the session-start hook, the rule that makes
/// edits of stores need the user's approval, and the instructions that import the stores' global
/// notes. Claude Code is the first; others can be added beside it.
/// </summary>
public interface IAgentIntegration
{
    /// <summary>The agent's name, as shown to the user.</summary>
    string Name { get; }

    /// <summary>Whether the agent is present on this machine.</summary>
    bool IsPresent(InstallContext context);

    /// <summary>Sets up the integration (idempotent) and says what changed.</summary>
    IReadOnlyList<string> Apply(InstallContext context);

    /// <summary>Removes the integration and says what changed.</summary>
    IReadOnlyList<string> Remove(InstallContext context);

    /// <summary>Updates the imported instructions after stores were added or removed.</summary>
    void RefreshInstructions(InstallContext context);

    /// <summary>Checks the integration for <c>memshare doctor</c>.</summary>
    IEnumerable<Finding> Check(InstallContext context);
}
