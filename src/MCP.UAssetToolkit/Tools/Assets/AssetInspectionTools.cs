using System.ComponentModel;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;
using UAssetAPI.UnrealTypes;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class AssetInspectionTools
{
    [McpServerTool, Description("Test binary serialization on a copy and compare it with disk, without saving or changing the session. Differences after edits are expected.")]
    public static Task<string> ValidateAssetAsync(string filePath) => ToolHelpers.ExecuteAsync(() =>
        ToolHelpers.FormatSuccess(AssetInspectionService.Validate(AssetSessionService.GetOrLoadSession(filePath))));

    [McpServerTool, Description("Read a bounded slice of export binary data as base64. Section: raw, extras, or script. Export index is 1-based; byte offset is 0-based.")]
    public static Task<string> GetExportBytesAsync(string filePath, int exportIndex, string section = "raw", int offset = 0, int limit = 65536)
    {
        return ToolHelpers.ExecuteAsync(() =>
        {
            AssetInspectionService.ValidateBytePage(offset, limit);
            var asset = AssetSessionService.GetOrLoadSession(filePath).Asset;
            var bytes = AssetInspectionService.GetBytes(AssetInspectionService.GetExport(asset, exportIndex), section);
            var count = Math.Min(limit, Math.Max(0, bytes.Length - offset));
            return ToolHelpers.FormatSuccess(new { exportIndex, section, offset, count, totalBytes = bytes.Length,
                hasMore = offset + count < bytes.Length, base64 = Convert.ToBase64String(bytes, Math.Min(offset, bytes.Length), count) });
        });
    }

    [McpServerTool, Description("Replace an export's complete raw, extras, or script byte buffer with base64 data (maximum 16 MiB). Script requires its logical scriptBytecodeSize and clears parsed bytecode. Save explicitly to persist.")]
    public static Task<string> SetExportBytesAsync(string filePath, int exportIndex, string base64, string section = "raw", int? scriptBytecodeSize = null)
    {
        return ToolHelpers.ExecuteAsync(() =>
        {
            if (base64.Length > 22369624) throw new ArgumentException("Binary input exceeds 16 MiB.");
            var bytes = Convert.FromBase64String(base64);
            if (bytes.Length > 16777216) throw new ArgumentException("Binary input exceeds 16 MiB.");
            AssetSessionService.TransformSessionAsset(filePath, asset =>
            {
                AssetInspectionService.SetBytes(AssetInspectionService.GetExport(asset, exportIndex), section, bytes, scriptBytecodeSize);
                return asset;
            });
            return ToolHelpers.FormatSuccess(new { exportIndex, section, byteCount = bytes.Length }, "Binary data updated. Call save_asset to persist.");
        });
    }

    [McpServerTool, Description("Replace all matching name-map strings without shifting indices, updating references that use those entries.")]
    public static Task<string> ReplaceNameReferencesAsync(string filePath, string oldName, string newName, bool matchCase = true)
    {
        return ToolHelpers.ExecuteAsync(() =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(oldName);
            ArgumentException.ThrowIfNullOrWhiteSpace(newName);
            var asset = AssetSessionService.GetOrLoadSession(filePath).Asset;
            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var indices = asset.GetNameMapIndexList().Select((name, index) => (name, index))
                .Where(entry => string.Equals(entry.name.Value, oldName, comparison)).Select(entry => entry.index).ToArray();
            foreach (var index in indices) asset.SetNameReference(index, new FString(newName));
            if (indices.Length > 0) AssetSessionService.MarkDirty(filePath);
            return ToolHelpers.FormatSuccess(new { replacedCount = indices.Length, indices });
        });
    }
}
