using System.ComponentModel;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class AssetInfoTools
{
    [McpServerTool, Description("Get basic information about a .uasset file")]
    public static Task<string> GetAssetInfoAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Optional engine version override (e.g. '4.27', '5.5')")]
        string? engineVersion = null)
    {
        return ToolHelpers.ExecuteAsync(() => {
            var overrideVersion = string.IsNullOrEmpty(engineVersion) ? null : (UAssetAPI.UnrealTypes.EngineVersion?)ToolHelpers.ParseEngineVersion(engineVersion);
            var session = AssetSessionService.GetOrLoadSession(filePath, overrideVersion);
            var asset = session.Asset;

            var info = new
            {
                fileName = Path.GetFileName(filePath),
                filePath = session.FilePath,
                fileSize = new FileInfo(session.FilePath).Length,
                engineVersion = session.EngineVersion.ToString(),
                objectVersion = asset.ObjectVersion.ToString(),
                packageFlags = asset.PackageFlags.ToString(),
                exportCount = asset.Exports.Count,
                importCount = asset.Imports.Count,
                packageGuid = asset.PackageGuid.ToString(),
                isDirty = session.IsDirty
            };

            return ToolHelpers.FormatSuccess(info);
        });
    }

    [McpServerTool, Description("Get comprehensive metadata about a .uasset file (similar to UAssetGUI Info tab)")]
    public static Task<string> GetFullAssetInfoAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Optional engine version override")]
        string? engineVersion = null)
    {
        return ToolHelpers.ExecuteAsync(() => {
            var overrideVersion = string.IsNullOrEmpty(engineVersion) ? null : (UAssetAPI.UnrealTypes.EngineVersion?)ToolHelpers.ParseEngineVersion(engineVersion);
            var session = AssetSessionService.GetOrLoadSession(filePath, overrideVersion);
            var asset = session.Asset;

            var info = new
            {
                fileName = Path.GetFileName(filePath),
                filePath = session.FilePath,
                fileSize = new FileInfo(session.FilePath).Length,
                isDirty = session.IsDirty,

                // Versions
                engineVersion = session.EngineVersion.ToString(),
                legacyFileVersion = asset.LegacyFileVersion,
                isUnversioned = asset.IsUnversioned,
                fileVersionLicenseeUE = asset.FileVersionLicenseeUE,
                objectVersion = asset.ObjectVersion.ToString(),
                objectVersionUE5 = asset.ObjectVersionUE5.ToString(),

                // Package Header Info
                packageFlags = asset.PackageFlags.ToString(),
                packageGuid = asset.PackageGuid.ToString(),
                packageSource = asset.PackageSource,
                folderName = asset.FolderName?.ToString() ?? "None",
                recordedEngineVersion = $"{asset.RecordedEngineVersion.Major}.{asset.RecordedEngineVersion.Minor}.{asset.RecordedEngineVersion.Patch} (CL {asset.RecordedEngineVersion.Changelist})",
                recordedCompatibleWithEngineVersion = $"{asset.RecordedCompatibleWithEngineVersion.Major}.{asset.RecordedCompatibleWithEngineVersion.Minor}.{asset.RecordedCompatibleWithEngineVersion.Patch} (CL {asset.RecordedCompatibleWithEngineVersion.Changelist})",

                // Tables and Counts
                exportCount = asset.Exports.Count,
                importCount = asset.Imports.Count,
                nameCount = asset.GetNameMapIndexList().Count,
                softObjectPathListCount = asset.SoftObjectPathList?.Count ?? 0,
                gatherableTextDataCount = asset.GatherableTextData?.Count ?? 0,
                assetRegistryRecordCount = asset.AssetRegistryRecords?.Count ?? 0,
                hasWorldTileInfo = asset.WorldTileInfo != null,
                customVersionCount = asset.CustomVersionContainer?.Count ?? 0,
                chunkIDs = asset.ChunkIDs
            };

            return ToolHelpers.FormatSuccess(info);
        });
    }

    [McpServerTool, Description("Get list of soft object paths stored in the asset header")]
    public static Task<string> GetSoftObjectPathsAsync(
        [Description("Full path to the .uasset file")]
        string filePath)
    {
        return ToolHelpers.ExecuteAsync(() => {
            var session = AssetSessionService.GetOrLoadSession(filePath);
            var paths = session.Asset.SoftObjectPathList?.Select((sop, idx) => new
            {
                index = idx,
                packageName = sop.AssetPath.PackageName?.ToString(),
                assetName = sop.AssetPath.AssetName?.ToString(),
                subPathString = sop.SubPathString?.ToString()
            }).ToList() ?? [];

            return ToolHelpers.FormatSuccess(new { count = paths.Count, paths });
        });
    }

    [McpServerTool, Description("Get gatherable text localization data stored in the asset")]
    public static Task<string> GetGatherableTextDataAsync(
        [Description("Full path to the .uasset file")]
        string filePath)
    {
        return ToolHelpers.ExecuteAsync(() => {
            var session = AssetSessionService.GetOrLoadSession(filePath);
            var textData = session.Asset.GatherableTextData?.Select((td, idx) => new
            {
                index = idx,
                namespaceName = td.NamespaceName?.ToString(),
                sourceData = td.SourceData.SourceString?.ToString()
            }).ToList() ?? [];

            return ToolHelpers.FormatSuccess(new { count = textData.Count, textData });
        });
    }

    [McpServerTool, Description("Get asset registry records and searchable tags embedded in the asset")]
    public static Task<string> GetAssetRegistryRecordsAsync(
        [Description("Full path to the .uasset file")]
        string filePath)
    {
        return ToolHelpers.ExecuteAsync(() => {
            var session = AssetSessionService.GetOrLoadSession(filePath);
            var records = session.Asset.AssetRegistryRecords?.Select((rec, idx) => new
            {
                index = idx,
                objectPath = rec.Path,
                objectClassName = rec.ClassName,
                tags = rec.TagMap
            }).ToList() ?? [];

            return ToolHelpers.FormatSuccess(new { count = records.Count, records });
        });
    }

    [McpServerTool, Description("Get World composition tile info if present in the asset")]
    public static Task<string> GetWorldTileInfoAsync(
        [Description("Full path to the .uasset file")]
        string filePath)
    {
        return ToolHelpers.ExecuteAsync(() => {
            var session = AssetSessionService.GetOrLoadSession(filePath);
            var tileInfo = session.Asset.WorldTileInfo;

            if (tileInfo == null)
            {
                return ToolHelpers.FormatSuccess(null, "No WorldTileInfo present in asset");
            }

            return ToolHelpers.FormatSuccess(new
            {
                position = tileInfo.Position,
                bounds = tileInfo.Bounds,
                layer = tileInfo.Layer,
                lodListCount = tileInfo.LODList?.Length ?? 0
            });
        });
    }
}
