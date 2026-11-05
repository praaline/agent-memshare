namespace Memshare.Stores;

/// <summary>
/// Conflict markers in changes about to be published (ADR-0005). A file counts as conflicted when its
/// added lines hold both an opening (<c>&lt;&lt;&lt;&lt;&lt;&lt;&lt; </c>) and a closing
/// (<c>&gt;&gt;&gt;&gt;&gt;&gt;&gt; </c>) marker; a lone <c>=======</c> is a Markdown heading underline.
/// </summary>
public static class ConflictMarkers
{
    /// <summary>The files whose added lines contain conflict markers.</summary>
    public static IReadOnlyList<string> FilesWithMarkers(IEnumerable<AddedLine> addedLines)
    {
        ArgumentNullException.ThrowIfNull(addedLines);
        return addedLines
            .GroupBy(l => l.Path, StringComparer.Ordinal)
            .Where(file =>
                file.Any(l => l.Text.StartsWith("<<<<<<< ", StringComparison.Ordinal) || l.Text == "<<<<<<<")
                && file.Any(l => l.Text.StartsWith(">>>>>>> ", StringComparison.Ordinal) || l.Text == ">>>>>>>"))
            .Select(file => file.Key)
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}
