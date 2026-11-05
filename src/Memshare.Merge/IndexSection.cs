namespace Memshare.Merge;

/// <summary>A part of an index: the text before the first <c>##</c> heading, or one <c>##</c> section.</summary>
/// <param name="Key">What identifies the section across versions (its heading).</param>
/// <param name="Heading">The heading line; null for the text before the first heading.</param>
/// <param name="Items">The section's lines.</param>
public sealed record IndexSection(string Key, string? Heading, IReadOnlyList<IndexItem> Items);
