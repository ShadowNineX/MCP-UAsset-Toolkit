using MCP_UAsset_Toolkit.Source.Models;

namespace MCP_UAsset_Toolkit.Source.Services;

public static class AssetBatchService
{
    public const int MaximumAssets = 50;
    public sealed record Item(string FilePath, bool Success, object? Result, string? Error, string? ExceptionType);

    public static IReadOnlyList<Item> Validate(IReadOnlyList<string> filePaths) =>
        Execute(filePaths, path => AssetInspectionService.Validate(AssetSessionService.GetOrLoadSession(path)));

    public static IReadOnlyList<Item> Patch(IReadOnlyList<string> filePaths, IReadOnlyList<AssetDocumentEdit> operations, bool preview = true) =>
        Execute(filePaths, path =>
        {
            var result = AssetDocumentService.PatchBatch(path, operations, preview);
            return new { operationCount = result.OperationCount, preview = result.Preview, changed = result.Changed, jsonLength = result.JsonLength };
        });

    public static AssetSession CloneSession(string filePath, string targetFilePath)
    {
        return AssetSessionService.ExecuteOperation(() =>
        {
            var target = Path.GetFullPath(targetFilePath);
            if (File.Exists(target) || Directory.Exists(target) || AssetSessionService.GetSession(target) != null)
                throw new IOException("Target already exists on disk or in the session cache; choose a new path.");
            if (!Path.GetExtension(target).Equals(".uasset", StringComparison.OrdinalIgnoreCase) && !Path.GetExtension(target).Equals(".umap", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Target must have a .uasset or .umap extension.", nameof(targetFilePath));
            var source = AssetSessionService.GetOrLoadSession(filePath);
            var candidate = UAssetContext.PrepareForWrite(UAssetContext.Clone(source.Asset));
            candidate.FilePath = target;
            return AssetSessionService.RegisterSession(target, candidate, source.EngineVersion, source.MappingsPath, isDirty: true);
        });
    }

    private static IReadOnlyList<Item> Execute(IReadOnlyList<string> filePaths, Func<string, object> operation)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        if (filePaths.Count is < 1 or > MaximumAssets) throw new ArgumentOutOfRangeException(nameof(filePaths), "Supply between 1 and 50 explicit asset paths.");
        var paths = filePaths.Select(path =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            return Path.GetFullPath(path);
        }).ToArray();
        if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length) throw new ArgumentException("Duplicate asset paths are not allowed.", nameof(filePaths));
        return AssetSessionService.ExecuteOperation(() => paths.Select(path =>
        {
            try { return new Item(path, true, operation(path), null, null); }
            catch (Exception exception) { return new Item(path, false, null, exception.Message, exception.GetType().Name); }
        }).ToArray());
    }
}
