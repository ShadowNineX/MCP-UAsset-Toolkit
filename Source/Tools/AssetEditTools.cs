using System.ComponentModel;
using System.Text.Json;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;
using UAssetAPI;
using UAssetAPI.UnrealTypes;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class AssetEditTools
{
    [McpServerTool, Description("Save a modified .uasset file")]
    public static Task<string> SaveAssetAsync(
        [Description("Full path to the .uasset file to save")]
        string filePath,
        [Description("Optional output path (defaults to overwrite original)")]
        string? outputPath = null)
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
                var savePath = outputPath ?? filePath;

                // Create directory if it doesn't exist
                var directory = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                asset.Write(savePath);

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = $"Asset saved successfully",
                    savedPath = savePath,
                    fileSize = new FileInfo(savePath).Length
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

    [McpServerTool, Description("Edit a name map entry at a specific index")]
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
                var nameMap = asset.GetNameMapIndexList();

                if (index < 0 || index >= nameMap.Count)
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = $"Index {index} is out of range (0-{nameMap.Count - 1})"
                    });
                }

                var oldName = nameMap[index]?.Value ?? "null";
                asset.SetNameReference(index, FString.FromString(newName));

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = $"Name map entry {index} updated",
                    oldValue = oldName,
                    newValue = newName,
                    note = "Remember to call save_asset to persist changes"
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

    [McpServerTool, Description("Add a new entry to the name map")]
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
                var newIndex = asset.AddNameReference(FString.FromString(name));

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = $"Added name to map at index {newIndex}",
                    index = newIndex,
                    value = name,
                    note = "Remember to call save_asset to persist changes"
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

    [McpServerTool, Description("Edit import data fields")]
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

                if (importIndex < 1 || importIndex > asset.Imports.Count)
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = $"Import index {importIndex} is out of range (1-{asset.Imports.Count})"
                    });
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

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = $"Import {importIndex} updated",
                    changes,
                    note = "Remember to call save_asset to persist changes"
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

    [McpServerTool, Description("Edit export information fields")]
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

                if (exportIndex < 1 || exportIndex > asset.Exports.Count)
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = $"Export index {exportIndex} is out of range (1-{asset.Exports.Count})"
                    });
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

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = $"Export {exportIndex} information updated",
                    changes,
                    note = "Remember to call save_asset to persist changes"
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
}
