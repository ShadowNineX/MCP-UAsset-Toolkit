using System.ComponentModel;
using System.Text.Json;
using MCP_UAsset_Toolkit.Source.Models;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;
using UAssetAPI;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class AssetInfoTools
{
    [McpServerTool, Description("Get basic information about a .uasset file")]
    public static Task<string> GetAssetInfoAsync(
        [Description("Full path to the .uasset file")]
        string filePath)
    {
        return Task.Run(() =>
        {
            try
            {
                if (!EngineVersionService.IsVersionSet)
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = ToolHelpers.VersionNotSetError
                    });
                }

                if (!File.Exists(filePath))
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = ToolHelpers.FileNotFoundError
                    });
                }

                var asset = new UAsset(filePath, EngineVersionService.GetVersionOrThrow());

                var info = new AssetInfo
                {
                    FileName = Path.GetFileName(filePath),
                    FileSize = new FileInfo(filePath).Length,
                    EngineVersion = asset.ObjectVersion.ToString(),
                    PackageFlags = asset.PackageFlags.ToString(),
                    ExportCount = asset.Exports.Count,
                    ImportCount = asset.Imports.Count,
                    PackageGuid = asset.PackageGuid.ToString()
                };

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    data = info
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        });
    }

    [McpServerTool, Description("Get comprehensive metadata about a .uasset file (similar to UAssetGUI Info tab)")]
    public static Task<string> GetFullAssetInfoAsync(
        [Description("Full path to the .uasset file")]
        string filePath)
    {
        return Task.Run(() =>
        {
            try
            {
                if (!EngineVersionService.IsVersionSet)
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = ToolHelpers.VersionNotSetError
                    });
                }

                if (!File.Exists(filePath))
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = ToolHelpers.FileNotFoundError
                    });
                }

                var asset = new UAsset(filePath, EngineVersionService.GetVersionOrThrow());

                var info = new
                {
                    fileName = Path.GetFileName(filePath),
                    filePath,
                    fileSize = new FileInfo(filePath).Length,

                    // Version info
                    legacyFileVersion = asset.LegacyFileVersion,
                    isUnversioned = asset.IsUnversioned,
                    fileVersionLicenseeUE = asset.FileVersionLicenseeUE,
                    objectVersion = asset.ObjectVersion.ToString(),
                    objectVersionUE5 = asset.ObjectVersionUE5.ToString(),

                    // Package info
                    packageFlags = asset.PackageFlags.ToString(),
                    packageGuid = asset.PackageGuid.ToString(),
                    packageSource = asset.PackageSource,
                    folderName = asset.FolderName?.ToString() ?? "None",
                    recordedEngineVersion = $"{asset.RecordedEngineVersion.Major}.{asset.RecordedEngineVersion.Minor}.{asset.RecordedEngineVersion.Patch} (CL {asset.RecordedEngineVersion.Changelist})",
                    recordedCompatibleWithEngineVersion = $"{asset.RecordedCompatibleWithEngineVersion.Major}.{asset.RecordedCompatibleWithEngineVersion.Minor}.{asset.RecordedCompatibleWithEngineVersion.Patch} (CL {asset.RecordedCompatibleWithEngineVersion.Changelist})",

                    // Counts
                    exportCount = asset.Exports.Count,
                    importCount = asset.Imports.Count,
                    nameCount = asset.GetNameMapIndexList().Count,

                    // Additional metadata
                    chunkIDs = asset.ChunkIDs,
                    softObjectPathListCount = asset.SoftObjectPathList?.Count ?? 0,
                    gatherableTextDataCount = asset.GatherableTextData?.Count ?? 0,
                    assetRegistryDataSize = asset.AssetRegistryData?.Length ?? 0,
                    hasWorldTileInfo = asset.WorldTileInfo != null,

                    // Custom versions
                    customVersionCount = asset.CustomVersionContainer?.Count ?? 0
                };

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    data = info
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        });
    }
}
