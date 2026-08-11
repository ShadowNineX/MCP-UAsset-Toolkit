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
        return Task.Run(() =>
        {
            try
            {
                var result = AssetSessionService.SaveSession(filePath, outputPath);
                return ToolHelpers.FormatSuccess(new
                {
                    savedPath = result.savedPath,
                    fileSize = result.fileSize
                }, "Asset saved successfully");
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
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
        return Task.Run(() =>
        {
            try
            {
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
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }

    [McpServerTool, Description("Add a new entry to the name map in memory")]
    public static Task<string> AddNameMapEntryAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Name value to add")]
        string name)
    {
        return Task.Run(() =>
        {
            try
            {
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;
                var newIndex = asset.AddNameReference(FString.FromString(name));
                AssetSessionService.MarkDirty(filePath);

                return ToolHelpers.FormatSuccess(new
                {
                    index = newIndex,
                    value = name
                }, $"Added name to map at index {newIndex}. Call save_asset to persist to disk.");
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
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
        return Task.Run(() =>
        {
            try
            {
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
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }

    [McpServerTool, Description("Remove an import by index (1-based)")]
    public static Task<string> RemoveImportAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Import index (1-based)")]
        int importIndex)
    {
        return Task.Run(() =>
        {
            try
            {
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;

                if (importIndex < 1 || importIndex > asset.Imports.Count)
                {
                    return ToolHelpers.FormatError($"Import index {importIndex} is out of range (1-{asset.Imports.Count})");
                }

                asset.Imports.RemoveAt(importIndex - 1);
                AssetSessionService.MarkDirty(filePath);

                return ToolHelpers.FormatSuccess(new { importIndex, remainingCount = asset.Imports.Count }, $"Import {importIndex} removed");
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }

    [McpServerTool, Description("Edit export information fields in memory")]
    public static Task<string> EditExportInfoAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Export index (1-based)")]
        int exportIndex,
        [Description("Object name (optional)")]
        string? objectName = null,
        [Description("Outer index (optional)")]
        int? outerIndex = null,
        [Description("Class index (optional)")]
        int? classIndex = null,
        [Description("Super index (optional)")]
        int? superIndex = null,
        [Description("Template index (optional)")]
        int? templateIndex = null,
        [Description("bForcedExport flag (optional)")]
        bool? bForcedExport = null,
        [Description("bNotForClient flag (optional)")]
        bool? bNotForClient = null,
        [Description("bNotForServer flag (optional)")]
        bool? bNotForServer = null,
        [Description("bIsAsset flag (optional)")]
        bool? bIsAsset = null)
    {
        return Task.Run(() =>
        {
            try
            {
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;

                if (exportIndex < 1 || exportIndex > asset.Exports.Count)
                {
                    return ToolHelpers.FormatError($"Export index {exportIndex} is out of range (1-{asset.Exports.Count})");
                }

                var export = asset.Exports[exportIndex - 1];
                var changes = new List<string>();

                if (objectName != null)
                {
                    export.ObjectName = new FName(asset, objectName);
                    changes.Add($"ObjectName: {objectName}");
                }

                if (outerIndex.HasValue)
                {
                    export.OuterIndex = new FPackageIndex(outerIndex.Value);
                    changes.Add($"OuterIndex: {outerIndex.Value}");
                }

                if (classIndex.HasValue)
                {
                    export.ClassIndex = new FPackageIndex(classIndex.Value);
                    changes.Add($"ClassIndex: {classIndex.Value}");
                }

                if (superIndex.HasValue)
                {
                    export.SuperIndex = new FPackageIndex(superIndex.Value);
                    changes.Add($"SuperIndex: {superIndex.Value}");
                }

                if (templateIndex.HasValue)
                {
                    export.TemplateIndex = new FPackageIndex(templateIndex.Value);
                    changes.Add($"TemplateIndex: {templateIndex.Value}");
                }

                if (bForcedExport.HasValue)
                {
                    export.bForcedExport = bForcedExport.Value;
                    changes.Add($"bForcedExport: {bForcedExport.Value}");
                }

                if (bNotForClient.HasValue)
                {
                    export.bNotForClient = bNotForClient.Value;
                    changes.Add($"bNotForClient: {bNotForClient.Value}");
                }

                if (bNotForServer.HasValue)
                {
                    export.bNotForServer = bNotForServer.Value;
                    changes.Add($"bNotForServer: {bNotForServer.Value}");
                }

                if (bIsAsset.HasValue)
                {
                    export.bIsAsset = bIsAsset.Value;
                    changes.Add($"bIsAsset: {bIsAsset.Value}");
                }

                AssetSessionService.MarkDirty(filePath);

                return ToolHelpers.FormatSuccess(new
                {
                    exportIndex,
                    changes
                }, $"Export {exportIndex} updated. Call save_asset to persist to disk.");
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }

    [McpServerTool, Description("Remove an export by index (1-based)")]
    public static Task<string> RemoveExportAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Export index (1-based)")]
        int exportIndex)
    {
        return Task.Run(() =>
        {
            try
            {
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;

                if (exportIndex < 1 || exportIndex > asset.Exports.Count)
                {
                    return ToolHelpers.FormatError($"Export index {exportIndex} is out of range (1-{asset.Exports.Count})");
                }

                asset.Exports.RemoveAt(exportIndex - 1);
                AssetSessionService.MarkDirty(filePath);

                return ToolHelpers.FormatSuccess(new { exportIndex, remainingCount = asset.Exports.Count }, $"Export {exportIndex} removed");
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }
}
