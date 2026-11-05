using System.CommandLine;

using Memshare.Platform;
using Memshare.Projects;

namespace Memshare.Cli.Commands;

/// <summary><c>memshare link</c> and <c>unlink</c>.</summary>
internal static class LinkCommands
{
    public static Command Link()
    {
        var slug = new Argument<string?>("slug")
        {
            Description = "The project's name in the store: lower-case letters, digits and hyphens. Left out, one is suggested from the Git remote.",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var store = new Option<string?>("--store") { Description = "The store; may be left out when only one is installed." };
        var command = new Command("link", "Link the current project, on this machine, to its shared notes in a store.") { slug, store };
        command.SetAction(async (parse, cancellationToken) =>
        {
            var chosen = parse.GetValue(slug) ?? await SuggestAsync(cancellationToken).ConfigureAwait(false);
            if (chosen is null)
            {
                return 1;
            }

            var link = await ProjectLinker.LinkAsync(CommandContext.Environment, CommandContext.Git, chosen, parse.GetValue(store), cancellationToken)
                .ConfigureAwait(false);
            await Console.Out.WriteLineAsync(
                $"linked {DisplayPath.Of(link.ProjectRoot)} to {link.StoreName}/projects/{link.Slug} (on this machine; nothing written to the store)")
                .ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    /// <summary>A slug from the Git remote or the directory, confirmed by the user.</summary>
    private static async Task<string?> SuggestAsync(CancellationToken cancellationToken)
    {
        var environment = CommandContext.Environment;
        var root = await ProjectLink.FindProjectRootAsync(CommandContext.Git, environment.ProjectDirectory ?? environment.CurrentDirectory, cancellationToken)
            .ConfigureAwait(false);
        var suggestion = await SlugSuggestion.SuggestAsync(CommandContext.Git, root, cancellationToken).ConfigureAwait(false)
            ?? throw new MemshareException("No name to suggest for this project: memshare link <slug>.");
        var existing = CommandContext.Stores().Stores.Any(s => Directory.Exists(s.ProjectDirectory(suggestion)));
        var note = existing ? "shared notes exist under that name" : "no shared notes under that name yet";
        if (Console.IsInputRedirected)
        {
            throw new MemshareException($"Suggested name: {suggestion} ({note}). Run: memshare link {suggestion}");
        }

        await Console.Out.WriteAsync($"Link this project as '{suggestion}' ({note})? [Y/n] ").ConfigureAwait(false);
        var answer = (await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false))?.Trim();
        return string.IsNullOrEmpty(answer) || answer.Equals("y", StringComparison.OrdinalIgnoreCase) || answer.Equals("yes", StringComparison.OrdinalIgnoreCase)
            ? suggestion
            : null;
    }

    public static Command Unlink()
    {
        var command = new Command("unlink", "Remove the current project's link. Its shared notes stay in the store.");
        command.SetAction(async (_, cancellationToken) =>
        {
            var link = await ProjectLinker.UnlinkAsync(CommandContext.Environment, CommandContext.Git, cancellationToken).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"unlinked (the shared notes stay in {link.StoreName}/projects/{link.Slug})").ConfigureAwait(false);
            return 0;
        });
        return command;
    }
}
