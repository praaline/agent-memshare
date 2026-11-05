using System.Text;

namespace Memshare.Agents.ClaudeCode;

/// <summary>
/// The block memshare manages in Claude Code's user <c>CLAUDE.md</c>: imports of the stores' global
/// instructions, and the machine's name. It sits between two markers; the rest of the file is the
/// user's and is never changed.
/// </summary>
public static class ClaudeInstructions
{
    /// <summary>The line that opens the block.</summary>
    public const string Begin = "<!-- memshare:begin -->";

    /// <summary>The line that closes the block.</summary>
    public const string End = "<!-- memshare:end -->";

    /// <summary>The block for a set of imports.</summary>
    /// <param name="imports">Paths of the stores' <c>global/CLAUDE.md</c>, as Claude Code imports them.</param>
    /// <param name="machine">The machine's name.</param>
    /// <param name="operatingSystem">A description of its OS.</param>
    public static string Render(IReadOnlyList<string> imports, string machine, string operatingSystem)
    {
        ArgumentNullException.ThrowIfNull(imports);
        var text = new StringBuilder();
        text.Append(Begin).Append('\n');
        text.Append("<!-- Managed by memshare: edit the shared instructions in their store, not here. -->\n");
        text.Append("## Shared memory (memshare)\n\n");
        if (imports.Count == 0)
        {
            text.Append("No store has shared instructions (`global/CLAUDE.md`) yet.\n");
        }

        foreach (var import in imports)
        {
            text.Append('@').Append(import).Append('\n');
        }

        text.Append('\n').Append("This machine: `").Append(machine).Append("` (").Append(operatingSystem).Append(").\n");
        text.Append(End).Append('\n');
        return text.ToString();
    }

    /// <summary>The file's content with the block set: replaced where it is, or appended.</summary>
    public static string Apply(string? content, string block)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (string.IsNullOrEmpty(content))
        {
            return block;
        }

        if (Find(content) is var (start, end))
        {
            return content[..start] + block + content[end..];
        }

        var separator = content.EndsWith("\n\n", StringComparison.Ordinal) ? string.Empty : content.EndsWith('\n') ? "\n" : "\n\n";
        return content + separator + block;
    }

    /// <summary>The file's content without the block (and the blank line before it); unchanged when absent.</summary>
    public static string Remove(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (Find(content) is not var (start, end))
        {
            return content;
        }

        var before = content[..start].TrimEnd('\n');
        var after = content[end..].TrimStart('\n');
        return before.Length == 0 ? after : after.Length == 0 ? before + "\n" : before + "\n\n" + after;
    }

    /// <summary>The block in a file's content, if there is one.</summary>
    public static string? Extract(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Find(content) is var (start, end) ? content[start..end] : null;
    }

    /// <summary>Where the block starts, and where it ends (after its last line break).</summary>
    private static (int Start, int End)? Find(string content)
    {
        var start = content.IndexOf(Begin, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        var end = content.IndexOf(End, start, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        end += End.Length;
        if (end < content.Length && content[end] == '\r')
        {
            end++;
        }

        if (end < content.Length && content[end] == '\n')
        {
            end++;
        }

        return (start, end);
    }
}
