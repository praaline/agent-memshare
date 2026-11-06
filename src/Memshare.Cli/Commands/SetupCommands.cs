using System.CommandLine;

using Memshare.Diagnostics;
using Memshare.Platform;

namespace Memshare.Cli.Commands;

/// <summary><c>memshare install</c>, <c>uninstall</c> and <c>doctor</c>.</summary>
internal static class SetupCommands
{
    public static Command Install()
    {
        var machine = new Option<string?>("--machine") { Description = "This machine's name (lower-case letters, digits, hyphens); kept from the last install when left out." };
        var root = new Option<string?>("--stores-root") { Description = "Where stores are checked out; default ~/agent-memshare." };
        var command = new Command("install", "Set up this machine: the executable, the configuration, and the coding agent's hook, approval rule and instructions.") { machine, root };
        command.SetAction(async (parse, cancellationToken) =>
        {
            var source = Environment.ProcessPath ?? throw new MemshareException("Cannot tell where this executable is.");
            var result = await Integrations.Installer().InstallAsync(parse.GetValue(machine), parse.GetValue(root), source, cancellationToken).ConfigureAwait(false);
            foreach (var change in result.Changes)
            {
                await Console.Out.WriteLineAsync($"  {change}").ConfigureAwait(false);
            }

            foreach (var warning in result.Warnings)
            {
                await Console.Out.WriteLineAsync($"note: {warning}").ConfigureAwait(false);
            }

            await Console.Out.WriteLineAsync(result.Changes.Count == 0 ? "already installed, nothing changed." : $"installed {DisplayPath.Of(result.ExecutablePath)}.")
                .ConfigureAwait(false);
            if (result.StoreCount == 0)
            {
                await Console.Out.WriteLineAsync("Next: memshare store add <url>, then memshare link <slug> in a project.").ConfigureAwait(false);
            }

            return 0;
        });
        return command;
    }

    public static Command Uninstall()
    {
        var command = new Command("uninstall", "Remove memshare from the coding agent's settings. Stores, keys, configuration and the executable stay.");
        command.SetAction(async (_, _) =>
        {
            var changes = Integrations.Installer().Uninstall();
            foreach (var change in changes)
            {
                await Console.Out.WriteLineAsync($"  {change}").ConfigureAwait(false);
            }

            await Console.Out.WriteLineAsync(changes.Count == 0 ? "nothing to remove." : "uninstalled from the agent's settings.").ConfigureAwait(false);
            await Console.Out.WriteLineAsync(
                $"Left in place: the stores, deploy keys (~/.ssh/memshare-*), {DisplayPath.Of(CommandContext.Environment.ConfigDirectory)} and {DisplayPath.Of(Integrations.InstalledExecutable)}.")
                .ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    public static Command Doctor()
    {
        var offline = new Option<bool>("--offline") { Description = "Don't contact the stores' remotes." };
        var command = new Command("doctor", "Check this machine's setup and say how to fix what's wrong.") { offline };
        command.SetAction(async (parse, cancellationToken) =>
        {
            var findings = await new Doctor(CommandContext.Environment, CommandContext.Git, Integrations.All, Integrations.InstalledExecutable, CommandContext.Time)
                .CheckAsync(!parse.GetValue(offline), cancellationToken).ConfigureAwait(false);
            foreach (var finding in findings)
            {
                var mark = finding.Severity switch { Severity.Ok => "ok  ", Severity.Warning => "warn", _ => "FAIL" };
                await Console.Out.WriteLineAsync($"{mark}  {finding.Area}: {finding.Message}").ConfigureAwait(false);
            }

            return findings.Any(f => f.Severity == Severity.Problem) ? 1 : 0;
        });
        return command;
    }
}
