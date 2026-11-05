namespace Memshare.Merge;

/// <summary>Problems in an index that the user should know about.</summary>
public static class IndexCheck
{
    /// <summary>Link targets with more than one entry (often the two versions of a merged change).</summary>
    public static IReadOnlyList<string> DuplicateTargets(IndexDocument index)
    {
        ArgumentNullException.ThrowIfNull(index);
        return index.Links()
            .GroupBy(i => i.LinkTarget!, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
    }

    /// <summary>Link targets that point to a file missing next to the index (web links and anchors aside).</summary>
    public static IReadOnlyList<string> MissingTargets(IndexDocument index, string directory)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(directory);
        return index.Links()
            .Select(i => i.LinkTarget!)
            .Where(t => !t.Contains("://", StringComparison.Ordinal) && !t.StartsWith('#') && !t.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            .Select(t => (Target: t, Path: Uri.UnescapeDataString(t.Split('#')[0])))
            .Where(t => t.Path.Length > 0 && !File.Exists(Path.Combine(directory, t.Path)) && !Directory.Exists(Path.Combine(directory, t.Path)))
            .Select(t => t.Target)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
