using System.ComponentModel;
using System.Text.Json;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;
using UAssetAPI;
using UAssetAPI.ExportTypes;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class AssetDataTools
{
    [McpServerTool, Description("Read and parse a .uasset file to access its structure")]
    public static Task<string> ReadAssetAsync(
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

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    filePath,
                    objectVersion = asset.ObjectVersion.ToString(),
                    packageFlags = asset.PackageFlags.ToString(),
                    exportCount = asset.Exports.Count,
                    importCount = asset.Imports.Count
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

    [McpServerTool, Description("Get general information about a .uasset file")]
    public static Task<string> GetGeneralInfoAsync(
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

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    legacyFileVersion = asset.LegacyFileVersion,
                    isUnversioned = asset.IsUnversioned,
                    fileVersionLicenseeUE = asset.FileVersionLicenseeUE,
                    packageGuid = asset.PackageGuid.ToString(),
                    packageFlags = asset.PackageFlags.ToString(),
                    packageSource = asset.PackageSource,
                    folderName = asset.FolderName?.ToString() ?? "None"
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

    [McpServerTool, Description("List all exports in a .uasset file")]
    public static Task<string> ListExportsAsync(
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
                var exports = asset.Exports.Select((export, index) => new
                {
                    index = index + 1,
                    objectName = export.ObjectName.ToString(),
                    outerIndex = export.OuterIndex.Index,
                    classIndex = export.ClassIndex.Index,
                    className = export.ClassIndex.IsImport()
                        ? export.ClassIndex.ToImport(asset).ObjectName.ToString()
                        : "Class",
                    superIndex = export.SuperIndex.Index,
                    templateIndex = export.TemplateIndex.Index,
                    objectFlags = export.ObjectFlags.ToString(),
                    serialSize = export.SerialSize,
                    serialOffset = export.SerialOffset,
                    export.bForcedExport,
                    export.bNotForClient,
                    export.bNotForServer,
                    packageGuid = export.PackageGuid.ToString(),
                    isInheritedInstance = export.IsInheritedInstance,
                    packageFlags = export.PackageFlags.ToString(),
                    export.bNotAlwaysLoadedForEditorGame,
                    export.bIsAsset,
                    generatePublicHash = export.GeneratePublicHash,
                    serializationBeforeSerializationDependencies = export.SerializationBeforeSerializationDependencies.Count,
                    createBeforeSerializationDependencies = export.CreateBeforeSerializationDependencies.Count,
                    serializationBeforeCreateDependencies = export.SerializationBeforeCreateDependencies.Count,
                    createBeforeCreateDependencies = export.CreateBeforeCreateDependencies.Count
                }).ToList();

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    count = exports.Count,
                    exports
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

    [McpServerTool, Description("List all imports in a .uasset file")]
    public static Task<string> ListImportsAsync(
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
                var imports = asset.Imports.Select((import, index) => new
                {
                    index = index + 1,
                    classPackage = import.ClassPackage.ToString(),
                    className = import.ClassName.ToString(),
                    outerIndex = import.OuterIndex.Index,
                    objectName = import.ObjectName.ToString(),
                    packageName = import.PackageName?.ToString(),
                    import.bImportOptional
                }).ToList();

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    count = imports.Count,
                    imports
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

    [McpServerTool, Description("List all custom versions in a .uasset file")]
    public static Task<string> ListCustomVersionsAsync(
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

                if (asset.CustomVersionContainer == null || asset.CustomVersionContainer.Count == 0)
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        count = 0,
                        customVersions = new List<object>()
                    });
                }

                var customVersions = asset.CustomVersionContainer.Select(cv => new
                {
                    friendlyName = cv.FriendlyName ?? CustomVersion.GetCustomVersionFriendlyNameFromGuid(cv.Key),
                    version = cv.Version,
                    key = cv.Key.ToString(),
                    name = cv.Name?.ToString()
                }).ToList();

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    count = customVersions.Count,
                    customVersions
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

    [McpServerTool, Description("Get the name map (string table) from a .uasset file")]
    public static Task<string> GetNameMapAsync(
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
                var nameMap = asset.GetNameMapIndexList();

                var nameEntries = nameMap.Select((name, index) => new
                {
                    index,
                    name = name?.Value ?? "null",
                    encoding = name?.Encoding?.WebName ?? "unknown"
                }).ToList();

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    count = nameEntries.Count,
                    names = nameEntries
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

    [McpServerTool, Description("Get detailed data for a specific export by index (1-based)")]
    public static Task<string> GetExportDataAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Export index (1-based)")]
        int exportIndex)
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
                        error = $"Export index {exportIndex} out of range (1-{asset.Exports.Count})"
                    });
                }

                var export = asset.Exports[exportIndex - 1];

                var exportData = new
                {
                    index = exportIndex,
                    objectName = export.ObjectName.ToString(),
                    className = export.ClassIndex.IsImport()
                        ? export.ClassIndex.ToImport(asset).ObjectName.ToString()
                        : "Class",
                    exportType = export.GetType().Name,
                    serialSize = export.SerialSize,
                    serialOffset = export.SerialOffset,
                    export.bForcedExport,
                    export.bNotForClient,
                    export.bNotForServer,
                    properties = export is NormalExport normalExport
                        ? normalExport.Data?.Select(prop => new
                        {
                            name = prop.Name.ToString(),
                            propertyType = prop.PropertyType.ToString(),
                            value = prop.ToString()
                        }).ToList()
                        : null
                };

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    data = exportData
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

    [McpServerTool, Description("Get custom version container information from a .uasset file")]
    public static Task<string> GetCustomVersionsAsync(
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

                if (asset.CustomVersionContainer == null || asset.CustomVersionContainer.Count == 0)
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        message = "No custom versions found",
                        count = 0,
                        customVersions = Array.Empty<object>()
                    });
                }

                var customVersions = asset.CustomVersionContainer
                    .Select(cv => new
                    {
                        friendlyName = cv.FriendlyName,
                        key = cv.Key.ToString(),
                        version = cv.Version
                    })
                    .ToList();

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    count = customVersions.Count,
                    customVersions
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
