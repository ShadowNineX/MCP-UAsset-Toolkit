using System.ComponentModel;
using MCP_UAsset_Toolkit.Source.Models;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class AssetBatchTools
{
    [McpServerTool, Description("Apply 1-100 ordered JSON edits atomically to one asset, or validate a preview without changing the session. Supports add/replace/remove/copy/move/test; no disk writes.")]
    public static Task<string> PatchAssetDocumentBatchAsync(string filePath, IReadOnlyList<AssetDocumentEdit> operations, bool preview = false) => ToolHelpers.ExecuteAsync(() =>
    {
        var result = AssetDocumentService.PatchBatch(filePath, operations, preview);
        return ToolHelpers.FormatSuccess(new { filePath = result.FilePath, operationCount = result.OperationCount, preview = result.Preview, changed = result.Changed, jsonLength = result.JsonLength });
    });

    [McpServerTool, Description("Validate serialization for 1-50 explicit asset paths, returning each success or error. Does not edit or save assets.")]
    public static Task<string> BatchValidateAssetsAsync(IReadOnlyList<string> filePaths) => ToolHelpers.ExecuteAsync(() => FormatItems(AssetBatchService.Validate(filePaths)));

    [McpServerTool, Description("Preview or apply the same ordered JSON patch transaction to 1-50 explicit asset paths. Preview defaults to true. Each asset commits independently; inspect per-file errors. Never saves to disk.")]
    public static Task<string> BatchPatchAssetsAsync(IReadOnlyList<string> filePaths, IReadOnlyList<AssetDocumentEdit> operations, bool preview = true) =>
        ToolHelpers.ExecuteAsync(() => FormatItems(AssetBatchService.Patch(filePaths, operations, preview)));

    [McpServerTool, Description("Deep-copy an existing loaded asset/template to a new dirty session, preserving parser context. Target must not already exist on disk or in sessions. No disk writes; save_asset persists it.")]
    public static Task<string> CloneAssetSessionAsync(string filePath, string targetFilePath) => ToolHelpers.ExecuteAsync(() =>
    {
        var session = AssetBatchService.CloneSession(filePath, targetFilePath);
        return ToolHelpers.FormatSuccess(new { filePath = session.FilePath, engineVersion = session.EngineVersion.ToString(), isDirty = session.IsDirty, exportCount = session.Asset.Exports.Count });
    });

    private static string FormatItems(IReadOnlyList<AssetBatchService.Item> items) => ToolHelpers.FormatSuccess(new
    {
        count = items.Count,
        succeededCount = items.Count(item => item.Success),
        failedCount = items.Count(item => !item.Success),
        results = items.Select(item => new { filePath = item.FilePath, success = item.Success, result = item.Result, error = item.Error, exceptionType = item.ExceptionType })
    });
}
