using Memshare.Git;

namespace Memshare.Stores;

/// <summary>An added line of a staged change.</summary>
/// <param name="Path">The file, relative to the store.</param>
/// <param name="LineNumber">The line's number in the new version of the file.</param>
/// <param name="Text">The line.</param>
public sealed record AddedLine(string Path, int LineNumber, string Text);

/// <summary>The lines a commit of the staged changes would add, read from <c>git diff --cached</c>.</summary>
public static class StagedChanges
{
    /// <summary>Reads the added lines of the staged changes of a store.</summary>
    public static async Task<IReadOnlyList<AddedLine>> AddedLinesAsync(GitRunner git, Store store, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(store);
        var diff = await git.RunAsync(
            store.Directory,
            ["-c", "core.quotepath=false", "diff", "--cached", "--unified=0", "--no-color", "--no-ext-diff", "--text"],
            GitRunner.DefaultTimeout,
            cancellationToken).ConfigureAwait(false);
        if (!diff.Succeeded)
        {
            throw new MemshareException($"Could not read the changes of store {store.Name}: {diff.StandardError.Trim()}");
        }

        return Parse(diff.StandardOutput);
    }

    /// <summary>Parses the added lines out of a unified diff with no context lines.</summary>
    internal static IReadOnlyList<AddedLine> Parse(string diff)
    {
        var lines = new List<AddedLine>();
        string? path = null;
        var lineNumber = 0;
        foreach (var raw in diff.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                path = line == "+++ /dev/null" ? null : line[4..].StartsWith("b/", StringComparison.Ordinal) ? line[6..] : line[4..];
            }
            else if (line.StartsWith("@@ ", StringComparison.Ordinal))
            {
                // @@ -a,b +c,d @@: the new lines start at c.
                var plus = line.IndexOf(" +", StringComparison.Ordinal);
                var end = line.IndexOfAny([',', ' '], plus + 2);
                lineNumber = int.Parse(line.AsSpan(plus + 2, end - plus - 2), System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (path is not null && line.StartsWith('+'))
            {
                lines.Add(new AddedLine(path, lineNumber++, line[1..]));
            }
        }

        return lines;
    }
}
