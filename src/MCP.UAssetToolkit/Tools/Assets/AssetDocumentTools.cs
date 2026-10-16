using System.ComponentModel;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class AssetDocumentTools
{
    [McpServerTool, Description("List exact UAssetAPI $type strings, including the required ', UAssetAPI' qualifier, that may be introduced in asset document JSON.")]
    public static Task<string> GetAssetDocumentSchemaAsync()
    {
        return ToolHelpers.ExecuteAsync(() =>
        {
            var schema = AssetDocumentService.GetSchema();
            return ToolHelpers.FormatSuccess(new { propertyDataTypes = schema.PropertyDataTypes, exportTypes = schema.ExportTypes });
        });
    }

    [McpServerTool, Description("Read any UAssetAPI JSON subtree using an RFC 6901 JSON pointer. Use an empty pointer for the whole asset.")]
    public static Task<string> ReadAssetDocumentAsync(
        [Description("Full path to the .uasset file")] string filePath,
        [Description("RFC 6901 JSON pointer; empty reads the whole asset")] string pointer = "",
        [Description("Maximum JSON characters to return (1-1000000, default 65536)")] int maxJsonChars = AssetDocumentService.DefaultMaxJsonChars)
    {
        return ToolHelpers.ExecuteAsync(() =>
        {
            var result = AssetDocumentService.Read(filePath, pointer, maxJsonChars);
            return ToolHelpers.FormatSuccess(new { filePath = result.FilePath, pointer = result.Pointer, jsonLength = result.JsonLength, value = result.Value });
        });
    }

    [McpServerTool, Description("Atomically replace, add, or remove an UAssetAPI JSON member or array entry using an RFC 6901 JSON pointer. valueJson is required except for remove.")]
    public static Task<string> PatchAssetDocumentAsync(
        [Description("Full path to the .uasset file")] string filePath,
        [Description("RFC 6901 JSON pointer to patch")] string pointer,
        [Description("Patch operation: replace, add, or remove")] string operation,
        [Description("A complete JSON value for replace/add; omit for remove")] string? valueJson = null)
    {
        return ToolHelpers.ExecuteAsync(() =>
        {
            var result = AssetDocumentService.Patch(filePath, pointer, operation, valueJson);
            return ToolHelpers.FormatSuccess(new { filePath = result.FilePath, pointer = result.Pointer, operation = result.Operation, jsonLength = result.JsonLength }, "Asset document patched successfully");
        });
    }
}
