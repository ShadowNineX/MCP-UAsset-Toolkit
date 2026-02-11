namespace MCP_UAsset_Toolkit.Source.Models;

public class AssetInfo
{
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string EngineVersion { get; set; } = string.Empty;
    public string PackageFlags { get; set; } = string.Empty;
    public int ExportCount { get; set; }
    public int ImportCount { get; set; }
    public string PackageGuid { get; set; } = string.Empty;
}
