using UAssetAPI;
using UAssetAPI.UnrealTypes;

namespace MCP_UAsset_Toolkit.Source.Models;

public class AssetSession
{
    public string FilePath { get; set; }
    public UAsset Asset { get; set; }
    public EngineVersion EngineVersion { get; set; }
    public bool IsDirty { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public string? MappingsPath { get; set; }

    public AssetSession(string filePath, UAsset asset, EngineVersion engineVersion, string? mappingsPath = null)
    {
        FilePath = filePath;
        Asset = asset;
        EngineVersion = engineVersion;
        MappingsPath = mappingsPath;
    }
}
