using System.CommandLine;

using Memshare.Merge;

namespace Memshare.Cli.Commands;

/// <summary><c>memshare merge-driver %O %A %B</c>: called by git to merge an index (ADR-0005).</summary>
internal static class MergeDriverCommand
{
    public static Command Create()
    {
        var ancestor = new Argument<string>("ancestor") { Description = "The common ancestor's version (%O)." };
        var ours = new Argument<string>("ours") { Description = "Our version (%A); replaced by the result." };
        var theirs = new Argument<string>("theirs") { Description = "Their version (%B)." };
        var command = new Command("merge-driver", "Merge two versions of a MEMORY.md index (called by git).") { ancestor, ours, theirs };
        command.Hidden = true;
        command.SetAction(async (parse, cancellationToken) =>
        {
            var oursPath = parse.GetValue(ours)!;
            try
            {
                var merged = IndexMerge.Merge(
                    await File.ReadAllTextAsync(parse.GetValue(ancestor)!, cancellationToken).ConfigureAwait(false),
                    await File.ReadAllTextAsync(oursPath, cancellationToken).ConfigureAwait(false),
                    await File.ReadAllTextAsync(parse.GetValue(theirs)!, cancellationToken).ConfigureAwait(false));
                await File.WriteAllTextAsync(oursPath, merged, cancellationToken).ConfigureAwait(false);
                return 0;
            }
            catch (IOException e)
            {
                // Git then treats the file as conflicted, and memshare keeps both versions.
                await Console.Error.WriteLineAsync($"memshare merge-driver: {e.Message}").ConfigureAwait(false);
                return 1;
            }
        });
        return command;
    }
}
