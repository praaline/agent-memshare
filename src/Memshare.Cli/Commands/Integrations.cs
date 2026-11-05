using Memshare.Agents;
using Memshare.Agents.ClaudeCode;
using Memshare.Installation;

namespace Memshare.Cli.Commands;

/// <summary>The agents memshare sets up, and the installed executable's path.</summary>
internal static class Integrations
{
    public static IReadOnlyList<IAgentIntegration> All { get; } = [new ClaudeCodeIntegration()];

    public static Installer Installer() => new(CommandContext.Environment, CommandContext.Git, All, CommandContext.Time);

    public static string InstalledExecutable => Installer().InstalledExecutable;
}
