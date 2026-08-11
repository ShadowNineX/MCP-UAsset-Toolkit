using System.ComponentModel;
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
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;

                return ToolHelpers.FormatSuccess(new
                {
                    filePath = session.FilePath,
                    engineVersion = session.EngineVersion.ToString(),
                    objectVersion = asset.ObjectVersion.ToString(),
                    packageFlags = asset.PackageFlags.ToString(),
                    exportCount = asset.Exports.Count,
                    importCount = asset.Imports.Count,
                    isDirty = session.IsDirty
                });
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
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
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;

                return ToolHelpers.FormatSuccess(new
                {
                    legacyFileVersion = asset.LegacyFileVersion,
                    isUnversioned = asset.IsUnversioned,
                    fileVersionLicenseeUE = asset.FileVersionLicenseeUE,
                    packageGuid = asset.PackageGuid.ToString(),
                    packageFlags = asset.PackageFlags.ToString(),
                    packageSource = asset.PackageSource,
                    folderName = asset.FolderName?.ToString() ?? "None",
                    isDirty = session.IsDirty
                });
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
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
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;

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
                    exportType = export.GetType().Name,
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

                return ToolHelpers.FormatSuccess(new
                {
                    count = exports.Count,
                    exports
                });
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
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
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;

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

                return ToolHelpers.FormatSuccess(new
                {
                    count = imports.Count,
                    imports
                });
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
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
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;

                if (asset.CustomVersionContainer == null || asset.CustomVersionContainer.Count == 0)
                {
                    return ToolHelpers.FormatSuccess(new
                    {
                        count = 0,
                        customVersions = Array.Empty<object>()
                    });
                }

                var customVersions = asset.CustomVersionContainer.Select(cv => new
                {
                    friendlyName = cv.FriendlyName ?? CustomVersion.GetCustomVersionFriendlyNameFromGuid(cv.Key),
                    version = cv.Version,
                    key = cv.Key.ToString(),
                    name = cv.Name?.ToString()
                }).ToList();

                return ToolHelpers.FormatSuccess(new
                {
                    count = customVersions.Count,
                    customVersions
                });
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
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
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;
                var nameMap = asset.GetNameMapIndexList();

                var nameEntries = nameMap.Select((name, index) => new
                {
                    index,
                    name = name?.Value ?? "null",
                    encoding = name?.Encoding?.WebName ?? "unknown"
                }).ToList();

                return ToolHelpers.FormatSuccess(new
                {
                    count = nameEntries.Count,
                    names = nameEntries
                });
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
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
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;

                if (exportIndex < 1 || exportIndex > asset.Exports.Count)
                {
                    return ToolHelpers.FormatError($"Export index {exportIndex} out of range (1-{asset.Exports.Count})");
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
                            value = ToolHelpers.ConvertPropertyToValue(prop)
                        }).ToList()
                        : null
                };

                return ToolHelpers.FormatSuccess(exportData);
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }

    [McpServerTool, Description("Search the name map for entries containing a substring")]
    public static Task<string> SearchNameMapAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Search query string")]
        string query)
    {
        return Task.Run(() =>
        {
            try
            {
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var nameMap = session.Asset.GetNameMapIndexList();

                var matches = nameMap
                    .Select((name, index) => new { index, value = name?.Value ?? string.Empty })
                    .Where(x => x.value.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return ToolHelpers.FormatSuccess(new { count = matches.Count, matches, query });
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }

    [McpServerTool, Description("Search imports by object name, class name, or class package")]
    public static Task<string> SearchImportsAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Search query string")]
        string query)
    {
        return Task.Run(() =>
        {
            try
            {
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;

                var matches = asset.Imports
                    .Select((imp, index) => new
                    {
                        index = index + 1,
                        objectName = imp.ObjectName.ToString(),
                        className = imp.ClassName.ToString(),
                        classPackage = imp.ClassPackage.ToString(),
                        outerIndex = imp.OuterIndex.Index
                    })
                    .Where(x => x.objectName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                x.className.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                x.classPackage.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return ToolHelpers.FormatSuccess(new { count = matches.Count, matches, query });
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }

    [McpServerTool, Description("Search exports by object name or class name")]
    public static Task<string> SearchExportsAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Search query string")]
        string query)
    {
        return Task.Run(() =>
        {
            try
            {
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;

                var matches = asset.Exports
                    .Select((exp, index) => new
                    {
                        index = index + 1,
                        objectName = exp.ObjectName.ToString(),
                        className = exp.ClassIndex.IsImport()
                            ? exp.ClassIndex.ToImport(asset).ObjectName.ToString()
                            : "Class",
                        exportType = exp.GetType().Name
                    })
                    .Where(x => x.objectName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                x.className.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return ToolHelpers.FormatSuccess(new { count = matches.Count, matches, query });
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }
}
