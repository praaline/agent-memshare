namespace Memshare.Merge;

/// <summary>
/// One line of an index. An entry (a list item) is identified by its link target, so that an entry
/// whose description changed is still the same entry; other lines by their text.
/// </summary>
/// <param name="Key">What identifies the line across versions.</param>
/// <param name="Text">The line, without its line break.</param>
/// <param name="LinkTarget">The entry's link target, if it is an entry with a link.</param>
public sealed record IndexItem(string Key, string Text, string? LinkTarget);
