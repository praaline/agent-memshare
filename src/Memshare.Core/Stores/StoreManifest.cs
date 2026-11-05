using System.Text.Json;

using Memshare.Configuration;

namespace Memshare.Stores;

/// <summary>A store's <c>memshare.json</c>: the version of the store's layout (ADR-0004).</summary>
public sealed record StoreManifest
{
    /// <summary>The highest store format this version of memshare understands.</summary>
    public const int SupportedFormat = 1;

    /// <summary>The file's name at the root of a store.</summary>
    public const string FileName = "memshare.json";

    /// <summary>The store format.</summary>
    public int Format { get; init; } = SupportedFormat;

    /// <summary>The store's format, or null when it has no (readable) <c>memshare.json</c>.</summary>
    public static int? ReadFormat(Store store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var path = Path.Combine(store.Directory, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), MemshareJsonContext.Default.StoreManifest)?.Format;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The manifest as written in a new store.</summary>
    public static string Serialize(StoreManifest manifest) =>
        JsonSerializer.Serialize(manifest, MemshareJsonContext.Default.StoreManifest) + "\n";
}
