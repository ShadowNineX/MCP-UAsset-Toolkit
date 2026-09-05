using System.ComponentModel;

namespace MCP_UAsset_Toolkit.Source.Models;

public sealed class AssetLoadOptions
{
    public string? EngineVersion { get; init; }
    public string? MappingsPath { get; init; }
    [Description("UAssetAPI CustomSerializationFlags enum names; comma-separated for multiple flags.")]
    public string? CustomSerializationFlags { get; init; }
    public string? GameSpecificOverride { get; init; }
    [Description("Map property names to [keyStructType, valueStructType] pairs.")]
    public Dictionary<string, string[]>? MapStructTypes { get; init; }
    [Description("Map array property names to their element struct type.")]
    public Dictionary<string, string>? ArrayStructTypes { get; init; }
}
