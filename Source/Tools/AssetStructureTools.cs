using System.ComponentModel;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.UnrealTypes;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class AssetStructureTools
{
    [McpServerTool, Description("Add a new export (e.g. NormalExport) to the asset")]
    public static Task<string> AddExportAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Object name for the new export")]
        string objectName,
        [Description("Class index (package index, e.g., -1 for import 1, or 0 for null)")]
        int classIndex = 0,
        [Description("Outer index (package index, e.g., 0 for package level)")]
        int outerIndex = 0,
        [Description("Export type (e.g. 'NormalExport', 'RawExport', 'ClassExport')")]
        string exportType = "NormalExport")
    {
        return Task.Run(() =>
        {
            try
            {
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;

                Export newExport = exportType.ToLowerInvariant() switch
                {
                    "rawexport" => new RawExport(new byte[0], asset, new byte[0]),
                    "classexport" => new ClassExport(asset, new byte[0]),
                    _ => new NormalExport(asset, new byte[0])
                };

                newExport.ObjectName = new FName(asset, objectName);
                newExport.ClassIndex = new FPackageIndex(classIndex);
                newExport.OuterIndex = new FPackageIndex(outerIndex);

                asset.Exports.Add(newExport);
                var newIndex = asset.Exports.Count;

                AssetSessionService.MarkDirty(filePath);

                return ToolHelpers.FormatSuccess(new
                {
                    exportIndex = newIndex,
                    objectName,
                    exportType = newExport.GetType().Name
                }, $"Added export {newIndex} ('{objectName}')");
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }

    [McpServerTool, Description("Add a new import reference to the asset")]
    public static Task<string> AddImportAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Class package (e.g., '/Script/Engine')")]
        string classPackage,
        [Description("Class name (e.g., 'Texture2D', 'BlueprintGeneratedClass')")]
        string className,
        [Description("Outer index (0 for none, negative index for outer import)")]
        int outerIndex,
        [Description("Object name (e.g., 'MyTexture')")]
        string objectName,
        [Description("Package name (optional, e.g. '/Game/Textures/MyTexture')")]
        string? packageName = null)
    {
        return Task.Run(() =>
        {
            try
            {
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;

                var import = new Import(
                    classPackage: new FName(asset, classPackage),
                    className: new FName(asset, className),
                    outerIndex: new FPackageIndex(outerIndex),
                    objectName: new FName(asset, objectName),
                    importOptional: false
                );

                if (!string.IsNullOrEmpty(packageName))
                {
                    import.PackageName = new FName(asset, packageName);
                }

                asset.Imports.Add(import);
                var newIndex = asset.Imports.Count;

                AssetSessionService.MarkDirty(filePath);

                return ToolHelpers.FormatSuccess(new
                {
                    importIndex = newIndex,
                    objectName,
                    className,
                    classPackage
                }, $"Added import {newIndex} ('{objectName}')");
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }

    [McpServerTool, Description("Copy/duplicate an existing export within the asset")]
    public static Task<string> CopyExportAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Source export index (1-based)")]
        int sourceExportIndex,
        [Description("New object name for duplicated export")]
        string newObjectName)
    {
        return Task.Run(() =>
        {
            try
            {
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;

                if (sourceExportIndex < 1 || sourceExportIndex > asset.Exports.Count)
                {
                    return ToolHelpers.FormatError($"Export index {sourceExportIndex} is out of range (1-{asset.Exports.Count})");
                }

                var srcExport = asset.Exports[sourceExportIndex - 1];
                var cloned = (Export)srcExport.Clone();

                cloned.ObjectName = new FName(asset, newObjectName);
                asset.Exports.Add(cloned);

                var newIndex = asset.Exports.Count;
                AssetSessionService.MarkDirty(filePath);

                return ToolHelpers.FormatSuccess(new
                {
                    newExportIndex = newIndex,
                    originalIndex = sourceExportIndex,
                    newObjectName
                }, $"Export {sourceExportIndex} cloned to new export {newIndex} ('{newObjectName}')");
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }

    [McpServerTool, Description("Add a soft object path to the asset header")]
    public static Task<string> AddSoftObjectPathAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Asset path name (e.g. '/Game/Maps/MainMap.MainMap')")]
        string assetPathName,
        [Description("Optional sub-path string")]
        string subPathString = "")
    {
        return Task.Run(() =>
        {
            try
            {
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;

                asset.SoftObjectPathList ??= [];

                var sop = new FSoftObjectPath(new FName(asset, assetPathName), new FName(asset, ""), FString.FromString(subPathString));
                asset.SoftObjectPathList.Add(sop);

                var index = asset.SoftObjectPathList.Count - 1;
                AssetSessionService.MarkDirty(filePath);

                return ToolHelpers.FormatSuccess(new { index, assetPathName, subPathString }, "Added soft object path");
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }

    [McpServerTool, Description("Remove a soft object path from the asset header by index")]
    public static Task<string> RemoveSoftObjectPathAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Soft object path index (0-based)")]
        int index)
    {
        return Task.Run(() =>
        {
            try
            {
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;

                if (asset.SoftObjectPathList == null || index < 0 || index >= asset.SoftObjectPathList.Count)
                {
                    return ToolHelpers.FormatError($"Index {index} out of range for soft object path list");
                }

                asset.SoftObjectPathList.RemoveAt(index);
                AssetSessionService.MarkDirty(filePath);

                return ToolHelpers.FormatSuccess(new { index, remainingCount = asset.SoftObjectPathList.Count }, "Removed soft object path");
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }

    [McpServerTool, Description("Add a custom version GUID and version number to the asset header")]
    public static Task<string> AddCustomVersionAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Custom version GUID string (e.g., 'E4B068ED-F494-42E9-A231-DB0B2B000000')")]
        string guidString,
        [Description("Version number (integer)")]
        int version,
        [Description("Optional friendly name")]
        string? friendlyName = null)
    {
        return Task.Run(() =>
        {
            try
            {
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;

                if (!Guid.TryParse(guidString, out var guid))
                {
                    return ToolHelpers.FormatError($"Invalid GUID format: '{guidString}'");
                }

                asset.CustomVersionContainer ??= [];

                var cv = new CustomVersion(guid, version);
                if (!string.IsNullOrEmpty(friendlyName))
                {
                    cv.FriendlyName = friendlyName;
                }

                asset.CustomVersionContainer.Add(cv);
                AssetSessionService.MarkDirty(filePath);

                return ToolHelpers.FormatSuccess(new { key = guid.ToString(), version, friendlyName }, "Added custom version");
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }

    [McpServerTool, Description("Remove a custom version by GUID string")]
    public static Task<string> RemoveCustomVersionAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Custom version GUID string")]
        string guidString)
    {
        return Task.Run(() =>
        {
            try
            {
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;

                if (!Guid.TryParse(guidString, out var guid))
                {
                    return ToolHelpers.FormatError($"Invalid GUID format: '{guidString}'");
                }

                if (asset.CustomVersionContainer == null)
                {
                    return ToolHelpers.FormatError("No custom version container present in asset");
                }

                var removedCount = asset.CustomVersionContainer.RemoveAll(cv => cv.Key == guid);
                if (removedCount > 0)
                {
                    AssetSessionService.MarkDirty(filePath);
                    return ToolHelpers.FormatSuccess(new { guidString, removedCount }, "Removed custom version");
                }

                return ToolHelpers.FormatError($"Custom version GUID '{guidString}' not found");
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }

    [McpServerTool, Description("Set or add a searchable asset registry tag on an asset registry record")]
    public static Task<string> SetAssetRegistryTagAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Asset registry record index (0-based, or -1 to apply to first/create record)")]
        int recordIndex,
        [Description("Tag key name")]
        string tagKey,
        [Description("Tag string value")]
        string tagValue)
    {
        return Task.Run(() =>
        {
            try
            {
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var asset = session.Asset;

                asset.AssetRegistryRecords ??= [];

                if (asset.AssetRegistryRecords.Count == 0 || recordIndex >= asset.AssetRegistryRecords.Count)
                {
                    var newRecord = new FAssetRegistryRecord
                    {
                        Path = Path.GetFileNameWithoutExtension(filePath),
                        ClassName = "Object",
                        TagMap = []
                    };
                    asset.AssetRegistryRecords.Add(newRecord);
                    recordIndex = asset.AssetRegistryRecords.Count - 1;
                }
                else if (recordIndex < 0)
                {
                    recordIndex = 0;
                }

                var record = asset.AssetRegistryRecords[recordIndex];
                record.TagMap ??= [];
                record.TagMap[tagKey] = tagValue;

                AssetSessionService.MarkDirty(filePath);

                return ToolHelpers.FormatSuccess(new { recordIndex, tagKey, tagValue }, "Set asset registry tag");
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }
}
