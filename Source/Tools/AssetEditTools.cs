using MCP_UAsset_Toolkit.Source.Models;
using System.ComponentModel;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;
using UAssetAPI.UnrealTypes;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class AssetEditTools
{
    [McpServerTool, Description("Save a modified .uasset file in active session to disk")]
    public static Task<string> SaveAssetAsync(
        [Description("Full path to the .uasset file in session")]
        string filePath,
        [Description("Optional output path (defaults to overwrite original)")]
        string? outputPath = null)
    {
        return ToolHelpers.ExecuteAsync(() => {
            var result = AssetSessionService.SaveSession(filePath, outputPath);
            return ToolHelpers.FormatSuccess(new
            {
                savedPath = result.savedPath,
                fileSize = result.fileSize
            }, "Asset saved successfully");
        });
    }

    [McpServerTool, Description("Edit a name map entry at a specific index in memory")]
    public static Task<string> EditNameMapEntryAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Index of the name to edit (0-based)")]
        int index,
        [Description("New name value")]
        string newName)
    {
        return ToolHelpers.ExecuteAsync(() => {
            var session = AssetSessionService.GetOrLoadSession(filePath);
            var asset = session.Asset;
            var nameMap = asset.GetNameMapIndexList();

            if (index < 0 || index >= nameMap.Count)
            {
                return ToolHelpers.FormatError($"Index {index} is out of range (0-{nameMap.Count - 1})");
            }

            var oldName = nameMap[index]?.Value ?? "null";
            asset.SetNameReference(index, FString.FromString(newName));
            AssetSessionService.MarkDirty(filePath);

            return ToolHelpers.FormatSuccess(new
            {
                index,
                oldValue = oldName,
                newValue = newName
            }, $"Name map entry {index} updated. Call save_asset to persist to disk.");
        });
    }

    [McpServerTool, Description("Add a new entry to the name map in memory")]
    public static Task<string> AddNameMapEntryAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Name value to add")]
        string name)
    {
        return ToolHelpers.ExecuteAsync(() => {
            var session = AssetSessionService.GetOrLoadSession(filePath);
            var asset = session.Asset;
            var newIndex = asset.AddNameReference(FString.FromString(name));
            AssetSessionService.MarkDirty(filePath);

            return ToolHelpers.FormatSuccess(new
            {
                index = newIndex,
                value = name
            }, $"Added name to map at index {newIndex}. Call save_asset to persist to disk.");
        });
    }

    [McpServerTool, Description("Edit import data fields in memory")]
    public static Task<string> EditImportAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Import index (1-based)")]
        int importIndex,
        [Description("Object name (optional)")]
        string? objectName = null,
        [Description("Class package (optional)")]
        string? classPackage = null,
        [Description("Class name (optional)")]
        string? className = null,
        [Description("Outer index (optional)")]
        int? outerIndex = null)
    {
        return ToolHelpers.ExecuteAsync(() => {
            var session = AssetSessionService.GetOrLoadSession(filePath);
            var asset = session.Asset;

            if (importIndex < 1 || importIndex > asset.Imports.Count)
            {
                return ToolHelpers.FormatError($"Import index {importIndex} is out of range (1-{asset.Imports.Count})");
            }

            var import = asset.Imports[importIndex - 1];
            var changes = new List<string>();

            if (objectName != null)
            {
                import.ObjectName = new FName(asset, objectName);
                changes.Add($"ObjectName: {objectName}");
            }

            if (classPackage != null)
            {
                import.ClassPackage = new FName(asset, classPackage);
                changes.Add($"ClassPackage: {classPackage}");
            }

            if (className != null)
            {
                import.ClassName = new FName(asset, className);
                changes.Add($"ClassName: {className}");
            }

            if (outerIndex.HasValue)
            {
                import.OuterIndex = new FPackageIndex(outerIndex.Value);
                changes.Add($"OuterIndex: {outerIndex.Value}");
            }

            AssetSessionService.MarkDirty(filePath);

            return ToolHelpers.FormatSuccess(new
            {
                importIndex,
                changes
            }, $"Import {importIndex} updated. Call save_asset to persist to disk.");
        });
    }

    [McpServerTool, Description("Remove an import by index (1-based)")]
    public static Task<string> RemoveImportAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Import index (1-based)")]
        int importIndex)
    {
        return ToolHelpers.ExecuteAsync(() => {
            var session = AssetSessionService.GetOrLoadSession(filePath);
            var asset = session.Asset;

            if (importIndex < 1 || importIndex > asset.Imports.Count)
            {
                return ToolHelpers.FormatError($"Import index {importIndex} is out of range (1-{asset.Imports.Count})");
            }

            asset.Imports.RemoveAt(importIndex - 1);
            AssetSessionService.MarkDirty(filePath);

            return ToolHelpers.FormatSuccess(new { importIndex, remainingCount = asset.Imports.Count }, $"Import {importIndex} removed");
        });
    }

    [McpServerTool, Description("Edit export header fields using an update object. Omitted fields retain their current values.")]
    public static Task<string> EditExportInfoAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Export index (1-based)")]
        int exportIndex,
        [Description("Fields to update, for example {\"objectName\":\"Hero\",\"bIsAsset\":true}")]
        ExportInfoUpdate update)
    {
        return ToolHelpers.ExecuteAsync(() =>
        {
            var session = AssetSessionService.GetOrLoadSession(filePath);
            var changes = ExportEditingService.Update(session.Asset, exportIndex, update);
            if (changes.Count > 0) AssetSessionService.MarkDirty(filePath);
            return ToolHelpers.FormatSuccess(new { exportIndex, changes }, "Export updated. Call save_asset to persist to disk.");
        });
    }
    [McpServerTool, Description("Remove an export by index (1-based)")]
    public static Task<string> RemoveExportAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Export index (1-based)")]
        int exportIndex)
    {
        return ToolHelpers.ExecuteAsync(() => {
            var session = AssetSessionService.GetOrLoadSession(filePath);
            var asset = session.Asset;

            if (exportIndex < 1 || exportIndex > asset.Exports.Count)
            {
                return ToolHelpers.FormatError($"Export index {exportIndex} is out of range (1-{asset.Exports.Count})");
            }

            asset.Exports.RemoveAt(exportIndex - 1);
            AssetSessionService.MarkDirty(filePath);

            return ToolHelpers.FormatSuccess(new { exportIndex, remainingCount = asset.Exports.Count }, $"Export {exportIndex} removed");
        });
    }
}
