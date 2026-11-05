using System.CommandLine;

using Memshare.Hooks;

namespace Memshare.Cli.Commands;

/// <summary><c>memshare hook &lt;event&gt;</c>: called by the agent, never by hand.</summary>
internal static class HookCommands
{
    /// <summary>The whole hook must finish well within the agent's own limit for it.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(25);

    public static Command Create()
    {
        var sessionStart = new Command("session-start", "Update the stores and print the session's shared-memory context (reads only).");
        sessionStart.SetAction(async (_, cancellationToken) =>
        {
            await DrainInputAsync().ConfigureAwait(false);
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(Budget);
            try
            {
                var context = await new SessionStartContext(CommandContext.Environment, CommandContext.Git, CommandContext.Time)
                    .BuildAsync(budget.Token).ConfigureAwait(false);
                await Console.Out.WriteAsync(context).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // The hook must never stop a session from starting: any failure becomes a note.
            catch (Exception e)
#pragma warning restore CA1031
            {
                await Console.Out.WriteLineAsync($"Shared memory (memshare) is unavailable in this session: {e.Message}").ConfigureAwait(false);
            }

            return 0;
        });

        return new Command("hook", "Entry points for the coding agent's hooks.") { sessionStart };
    }

    /// <summary>The agent sends the event as JSON on standard input; it isn't needed, but is read so the agent's write completes.</summary>
    private static async Task DrainInputAsync()
    {
        if (!Console.IsInputRedirected)
        {
            return;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await Console.In.ReadToEndAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Nothing more is coming.
        }
    }
}
