using System.CommandLine;
using System.Globalization;

using Memshare.Status;

namespace Memshare.Cli.Commands;

/// <summary><c>memshare status</c>: this machine, its stores, and the current project's link.</summary>
internal static class StatusCommand
{
    public static Command Create()
    {
        var command = new Command("status", "Show this machine, its stores, unpublished changes and the current project's link.");
        command.SetAction(async (_, cancellationToken) =>
        {
            var report = await StatusReport.GatherAsync(CommandContext.Environment, CommandContext.Git, cancellationToken)
                .ConfigureAwait(false);
            await Console.Out.WriteAsync(Format(report)).ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    internal static string Format(StatusReport report)
    {
        var text = new System.Text.StringBuilder();
        var invariant = CultureInfo.InvariantCulture;
        text.AppendLine(invariant, $"machine {report.Machine} ({report.OperatingSystem}), stores in {report.StoresRoot}");
        if (report.Stores.Count == 0)
        {
            text.AppendLine("  no stores");
        }

        foreach (var store in report.Stores)
        {
            text.AppendLine(invariant, $"  {store.Store.Name,-24} {store.Unpublished} unpublished, origin {store.Origin ?? "(none)"}");
        }

        text.AppendLine(report.Link is { } link
            ? $"this project: linked to {link.StoreName}/projects/{link.Slug}"
            : $"this project: not linked ({report.ProjectRoot})");
        return text.ToString();
    }
}
