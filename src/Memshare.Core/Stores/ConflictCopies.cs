using System.Globalization;

namespace Memshare.Stores;

/// <summary>
/// Copies of files that two machines changed at the same time (ADR-0005): the other machine's version
/// stays in place, this machine's is kept next to it as <c>&lt;name&gt;.conflict-&lt;time&gt;&lt;extension&gt;</c>.
/// </summary>
public static class ConflictCopies
{
    /// <summary>The part of a file name that marks a conflict copy.</summary>
    public const string Marker = ".conflict-";

    /// <summary>The name of the conflict copy of a file, made at a given time.</summary>
    public static string NameFor(string relativePath, DateTimeOffset time)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        var extension = Path.GetExtension(relativePath);
        var withoutExtension = relativePath[..^extension.Length];
        return $"{withoutExtension}{Marker}{time.UtcDateTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}{extension}";
    }

    /// <summary>Whether a file name is a conflict copy.</summary>
    public static bool IsConflictCopy(string fileName) =>
        fileName is not null && Path.GetFileName(fileName).Contains(Marker, StringComparison.Ordinal);

    /// <summary>The conflict copies directly in a directory, by name.</summary>
    public static IReadOnlyList<string> In(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(IsConflictCopy)
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];
    }
}
