using System.Text;
using System.Text.RegularExpressions;

namespace Memshare.Merge;

/// <summary>
/// A <c>MEMORY.md</c> index: a preamble and <c>##</c> sections of lines, most of them entries such as
/// <c>- [Title](file.md) — hook</c>.
/// </summary>
/// <param name="Sections">The preamble (key "") and the sections, in order.</param>
public sealed partial record IndexDocument(IReadOnlyList<IndexSection> Sections)
{
    /// <summary>Parses an index. Any text is accepted; line endings are normalised to LF.</summary>
    public static IndexDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1); // the final line break
        }

        var sections = new List<IndexSection>();
        var sectionKeys = new Dictionary<string, int>(StringComparer.Ordinal);
        string key = string.Empty;
        string? heading = null;
        var items = new List<IndexItem>();
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);

        void Close()
        {
            if (heading is not null || items.Count > 0)
            {
                sections.Add(new IndexSection(key, heading, items));
            }
        }

        foreach (var line in lines)
        {
            if (Heading().IsMatch(line))
            {
                Close();
                var baseKey = "## " + line[2..].Trim();
                key = Occurrence(sectionKeys, baseKey);
                heading = line;
                items = [];
                occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
                continue;
            }

            items.Add(ParseLine(line, occurrences));
        }

        Close();
        return new IndexDocument(sections);
    }

    /// <summary>The index as text, with LF line endings and a final line break.</summary>
    public string Render()
    {
        var text = new StringBuilder();
        foreach (var section in Sections)
        {
            if (section.Heading is not null)
            {
                text.Append(section.Heading).Append('\n');
            }

            foreach (var item in section.Items)
            {
                text.Append(item.Text).Append('\n');
            }
        }

        return text.ToString();
    }

    /// <summary>Every entry with a link, in order.</summary>
    public IEnumerable<IndexItem> Links() => Sections.SelectMany(s => s.Items).Where(i => i.LinkTarget is not null);

    private static IndexItem ParseLine(string line, Dictionary<string, int> occurrences)
    {
        if (Entry().IsMatch(line))
        {
            var link = Link().Match(line);
            if (link.Success)
            {
                var target = link.Groups["target"].Value.Trim();
                return new IndexItem(Occurrence(occurrences, "link:" + target), line, target);
            }

            return new IndexItem(Occurrence(occurrences, "entry:" + line.Trim()), line, null);
        }

        return new IndexItem(Occurrence(occurrences, "text:" + line.TrimEnd()), line, null);
    }

    /// <summary>A key made unique within its scope: the second "x" becomes "x#2".</summary>
    private static string Occurrence(Dictionary<string, int> seen, string key)
    {
        var count = seen.TryGetValue(key, out var n) ? n + 1 : 1;
        seen[key] = count;
        return count == 1 ? key : $"{key}#{count}";
    }

    [GeneratedRegex(@"^##(?!#)\s", RegexOptions.CultureInvariant)]
    private static partial Regex Heading();

    [GeneratedRegex(@"^\s*[-*+]\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Entry();

    [GeneratedRegex(@"\]\((?<target>[^)\s]+)(?:\s+""[^""]*"")?\)", RegexOptions.CultureInvariant)]
    private static partial Regex Link();
}
