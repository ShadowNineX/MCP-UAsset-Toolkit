using System.ComponentModel;
using System.Text.Json;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;
using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class AssetPropertyTools
{
    [McpServerTool, Description("Get all property data for a specific export (1-based) as structured JSON")]
    public static Task<string> GetExportPropertiesAsync(
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
                return ToolHelpers.FormatError($"Export index {exportIndex} out of range (1-{asset.Exports.Count})");
            }

            var export = asset.Exports[exportIndex - 1];

            if (export is not NormalExport normalExport)
            {
                return ToolHelpers.FormatError($"Export {exportIndex} is of type '{export.GetType().Name}', not NormalExport. Properties are only available on NormalExport.");
            }

            var properties = normalExport.Data?.Select((prop, idx) => new
            {
                index = idx,
                name = prop.Name.ToString(),
                propertyType = prop.PropertyType.ToString(),
                rawType = prop.GetType().Name,
                value = ToolHelpers.ConvertPropertyToValue(prop)
            }).ToList() ?? [];

            return ToolHelpers.FormatSuccess(new
            {
                exportIndex,
                objectName = export.ObjectName.ToString(),
                count = properties.Count,
                properties
            });
        });
    }

    [McpServerTool, Description("Set, add, or update a property value on a NormalExport by property name")]
    public static Task<string> SetExportPropertyAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Export index (1-based)")]
        int exportIndex,
        [Description("Property name (e.g., 'Health', 'CharacterName', 'bIsEnabled')")]
        string propertyName,
        [Description("Property type (e.g., 'IntPropertyData', 'FloatPropertyData', 'StrPropertyData', 'BoolPropertyData', 'NamePropertyData', 'BytePropertyData', 'ObjectPropertyData', 'Utf8StrPropertyData')")]
        string propertyType,
        [Description("JSON representation of the value (e.g. '100', '\"Hero\"', 'true', '{\"index\":-1}')")]
        string valueJson)
    {
        return ToolHelpers.ExecuteAsync(() => {
            var session = AssetSessionService.GetOrLoadSession(filePath);
            var asset = session.Asset;

            if (exportIndex < 1 || exportIndex > asset.Exports.Count)
            {
                return ToolHelpers.FormatError($"Export index {exportIndex} out of range (1-{asset.Exports.Count})");
            }

            var export = asset.Exports[exportIndex - 1];
            if (export is not NormalExport normalExport)
            {
                return ToolHelpers.FormatError($"Export {exportIndex} is of type '{export.GetType().Name}', not NormalExport");
            }

            normalExport.Data ??= [];

            using var doc = JsonDocument.Parse(valueJson);
            var element = doc.RootElement;

            var fName = new FName(asset, propertyName);
            var newProp = ToolHelpers.CreatePropertyData(propertyType, fName, element, asset);

            var existingIdx = normalExport.Data.FindIndex(p => string.Equals(p.Name?.ToString(), propertyName, StringComparison.OrdinalIgnoreCase));
            if (existingIdx >= 0)
            {
                normalExport.Data[existingIdx] = newProp;
            }
            else
            {
                normalExport.Data.Add(newProp);
            }

            AssetSessionService.MarkDirty(filePath);

            return ToolHelpers.FormatSuccess(new
            {
                exportIndex,
                propertyName,
                propertyType,
                newValue = ToolHelpers.ConvertPropertyToValue(newProp)
            }, $"Property '{propertyName}' updated on export {exportIndex}. Call save_asset to persist.");
        });
    }

    [McpServerTool, Description("Remove a property from a NormalExport by property name")]
    public static Task<string> RemoveExportPropertyAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Export index (1-based)")]
        int exportIndex,
        [Description("Property name to remove")]
        string propertyName)
    {
        return ToolHelpers.ExecuteAsync(() => {
            var session = AssetSessionService.GetOrLoadSession(filePath);
            var asset = session.Asset;

            if (exportIndex < 1 || exportIndex > asset.Exports.Count)
            {
                return ToolHelpers.FormatError($"Export index {exportIndex} out of range (1-{asset.Exports.Count})");
            }

            var export = asset.Exports[exportIndex - 1];
            if (export is not NormalExport normalExport || normalExport.Data == null)
            {
                return ToolHelpers.FormatError($"Export {exportIndex} has no property data");
            }

            var removedCount = normalExport.Data.RemoveAll(p => string.Equals(p.Name?.ToString(), propertyName, StringComparison.OrdinalIgnoreCase));
            if (removedCount > 0)
            {
                AssetSessionService.MarkDirty(filePath);
                return ToolHelpers.FormatSuccess(new { exportIndex, propertyName, removedCount }, $"Removed property '{propertyName}' from export {exportIndex}");
            }

            return ToolHelpers.FormatError($"Property '{propertyName}' not found in export {exportIndex}");
        });
    }
}
