using System.Text.Json.Serialization;

namespace Memshare.Configuration;

/// <summary>JSON serialisation without reflection, as Native AOT requires (ADR-0003).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(MemshareConfig))]
[JsonSerializable(typeof(Memshare.Stores.StoreManifest))]
internal sealed partial class MemshareJsonContext : JsonSerializerContext;
