using System.CommandLine;
using System.Globalization;

using Memshare.Projects;
using Memshare.Stores;

namespace Memshare.Cli.Commands;

/// <summary>Commands on stores: diff, publish, pull, projects.</summary>
internal static class StoreCommands
{
    public static Command Diff()
    {
        var store = StoreArgument();
        var command = new Command("diff", "Show the unpublished changes of a store.") { store };
        command.SetAction(async (parse, cancellationToken) =>
        {
            var target = CommandContext.Stores().Pick(parse.GetValue(store));
            await Console.Out.WriteLineAsync(await StoreDiff.DescribeAsync(CommandContext.Git, target, cancellationToken).ConfigureAwait(false))
                .ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    public static Command Publish()
    {
        var store = StoreArgument();
        var message = new Option<string>("--message", "-m")
        {
            Description = "What the change is, for the store's history.",
            Required = true,
        };
        var allowSecrets = new Option<bool>("--allow-secrets") { Description = "Publish even values that look like secrets (after checking that they are not)." };
        var command = new Command("publish", "Commit a store's changes, merge them with the remote, and push them.") { store, message, allowSecrets };
        command.SetAction(async (parse, cancellationToken) =>
        {
            var target = CommandContext.Stores().Pick(parse.GetValue(store));
            var result = await new StorePublisher(CommandContext.Git, CommandContext.Time)
                .PublishAsync(target, parse.GetValue(message)!, parse.GetValue(allowSecrets), cancellationToken).ConfigureAwait(false);
            if (result.Committed)
            {
                await Console.Out.WriteLineAsync($"publishing from {target.Name}:\n{result.Summary}").ConfigureAwait(false);
            }

            await Console.Out.WriteLineAsync(result.Pushed
                ? $"published {target.Name} ({result.Head})"
                : $"{target.Name}: nothing to publish ({result.Head})").ConfigureAwait(false);
            if (result.ConflictCopies.Count > 0)
            {
                await Console.Out.WriteLineAsync($"conflict copies to merge: {string.Join(", ", result.ConflictCopies)}").ConfigureAwait(false);
            }

            return 0;
        });
        return command;
    }

    public static Command Pull()
    {
        var command = new Command("pull", "Update every store from its remote now. Unpublished changes stay unpublished.");
        command.SetAction(async (_, cancellationToken) =>
        {
            var exit = 0;
            var sync = new StoreSync(CommandContext.Git, CommandContext.Time);
            foreach (var store in CommandContext.Stores().Stores)
            {
                using var storeLock = await StoreLock.TryAcquireAsync(store, StorePublisher.LockWait, cancellationToken).ConfigureAwait(false);
                if (storeLock is null)
                {
                    await Console.Out.WriteLineAsync($"{store.Name}: busy, try again").ConfigureAwait(false);
                    exit = 1;
                    continue;
                }

                var result = await sync.PullAsync(store, cancellationToken).ConfigureAwait(false);
                await Console.Out.WriteLineAsync(result.Succeeded
                    ? $"{store.Name}: up to date" + (result.ConflictCopies.Count > 0 ? $", conflict copies to merge: {string.Join(", ", result.ConflictCopies)}" : string.Empty)
                    : $"{store.Name}: could not update ({result.Error})").ConfigureAwait(false);
                exit = result.Succeeded ? exit : 1;
            }

            return exit;
        });
        return command;
    }

    public static Command Projects()
    {
        var command = new Command("projects", "List the projects with shared notes, and when they were last published.");
        command.SetAction(async (_, cancellationToken) =>
        {
            var projects = await SharedProjects.ListAsync(CommandContext.Git, CommandContext.Stores(), cancellationToken).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"{"store",-24} {"project",-32} last publish").ConfigureAwait(false);
            foreach (var project in projects)
            {
                var when = project.LastPublished?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "(unpublished)";
                await Console.Out.WriteLineAsync($"{project.Store.Name,-24} {project.Slug,-32} {when}").ConfigureAwait(false);
            }

            return 0;
        });
        return command;
    }

    private static Argument<string?> StoreArgument() => new("store")
    {
        Description = "The store; may be left out when only one is installed.",
        Arity = ArgumentArity.ZeroOrOne,
    };
}
