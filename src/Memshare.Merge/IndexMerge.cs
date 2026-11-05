namespace Memshare.Merge;

/// <summary>
/// Three-way merge of an index (ADR-0005). Sections are matched by heading and lines by key (entries
/// by link target):
/// <list type="bullet">
/// <item>added on either side: kept;</item>
/// <item>removed on one side and unchanged on the other: removed;</item>
/// <item>changed on one side: the change is kept, also against a removal on the other side;</item>
/// <item>changed differently on both sides: both versions are kept (the session-start hook then
/// reports the duplicate entry for the user to resolve).</item>
/// </list>
/// The merge always succeeds and never writes conflict markers. Additions from the other side are
/// placed after the line they followed there, after our own additions at that place.
/// </summary>
public static class IndexMerge
{
    /// <summary>Merges two versions of an index that share a common ancestor.</summary>
    public static string Merge(string ancestor, string ours, string theirs)
    {
        ArgumentNullException.ThrowIfNull(ancestor);
        ArgumentNullException.ThrowIfNull(ours);
        ArgumentNullException.ThrowIfNull(theirs);
        var a = IndexDocument.Parse(ancestor);
        var o = IndexDocument.Parse(ours);
        var t = IndexDocument.Parse(theirs);
        var sections = MergeKeyed(a.Sections, o.Sections, t.Sections, s => s.Key, ResolveSection);
        return new IndexDocument(sections).Render();
    }

    private static IEnumerable<IndexSection> ResolveSection(IndexSection? ancestor, IndexSection? ours, IndexSection? theirs)
    {
        if (ours is null && theirs is null)
        {
            yield break;
        }

        if (ours is null || theirs is null)
        {
            var present = (ours ?? theirs)!;
            // Removed on one side: gone if the other side left it as it was, kept if it changed it.
            if (ancestor is null || !SameContent(ancestor, present))
            {
                yield return present;
            }

            yield break;
        }

        var items = MergeKeyed(ancestor?.Items ?? [], ours.Items, theirs.Items, i => i.Key, ResolveItem);
        yield return ours with { Items = items };
    }

    private static IEnumerable<IndexItem> ResolveItem(IndexItem? ancestor, IndexItem? ours, IndexItem? theirs)
    {
        IndexItem? single;
        if (Same(ours, theirs))
        {
            single = ours;
        }
        else if (Same(ancestor, ours))
        {
            single = theirs;
        }
        else if (Same(ancestor, theirs))
        {
            single = ours;
        }
        else if (ours is null || theirs is null)
        {
            single = ours ?? theirs; // changed on one side, removed on the other: keep the change
        }
        else
        {
            // Changed differently on both sides: keep both.
            yield return ours;
            yield return theirs;
            yield break;
        }

        if (single is not null)
        {
            yield return single;
        }
    }

    /// <summary>
    /// Merges ordered lists of keyed elements: our order first, then elements only the other side has,
    /// each placed after the element it followed there.
    /// </summary>
    private static List<T> MergeKeyed<T>(
        IReadOnlyList<T> ancestor,
        IReadOnlyList<T> ours,
        IReadOnlyList<T> theirs,
        Func<T, string> key,
        Func<T?, T?, T?, IEnumerable<T>> resolve)
        where T : class
    {
        var a = ancestor.ToDictionary(key, StringComparer.Ordinal);
        var o = ours.ToDictionary(key, StringComparer.Ordinal);
        var t = theirs.ToDictionary(key, StringComparer.Ordinal);

        var order = ours.Select(key).ToList();
        string? previous = null;
        foreach (var k in theirs.Select(key))
        {
            if (!o.ContainsKey(k))
            {
                // After the element it followed on their side, and after our own additions there.
                var at = previous is null ? 0 : order.IndexOf(previous) + 1;
                while (at < order.Count && !t.ContainsKey(order[at]) && !a.ContainsKey(order[at]))
                {
                    at++;
                }

                order.Insert(at, k);
            }

            previous = k;
        }

        var result = new List<T>();
        foreach (var k in order)
        {
            result.AddRange(resolve(a.GetValueOrDefault(k), o.GetValueOrDefault(k), t.GetValueOrDefault(k)));
        }

        return result;
    }

    private static bool Same(IndexItem? x, IndexItem? y) => x is null ? y is null : y is not null && x.Text == y.Text;

    private static bool SameContent(IndexSection x, IndexSection y) =>
        x.Heading == y.Heading && x.Items.Select(i => i.Text).SequenceEqual(y.Items.Select(i => i.Text), StringComparer.Ordinal);
}
