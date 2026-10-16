using MCP_UAsset_Toolkit.Source.Models;
using System.Text.Json.Serialization;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;

namespace MCP_UAsset_Toolkit.Source.Services;

public sealed record AssetDependency(
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("sourceExportIndex")] int? SourceExportIndex,
    [property: JsonPropertyName("sourceField")] string? SourceField,
    [property: JsonPropertyName("rawIndex")] int? RawIndex,
    [property: JsonPropertyName("resolution")] string Resolution,
    [property: JsonPropertyName("targetIndex")] int? TargetIndex,
    [property: JsonPropertyName("targetName")] string? TargetName,
    [property: JsonPropertyName("packageName")] string? PackageName,
    [property: JsonPropertyName("flags")] uint? Flags = null);

public sealed record AssetDependencyPage(
    IReadOnlyList<AssetDependency> Dependencies,
    int TotalCount,
    int Offset,
    int Limit,
    bool HasMore);

/// <summary>Builds a read-only view of package references stored in an asset's headers.</summary>
public static class AssetDependencyService
{
    private const string ExportHeaderCategory = "exportHeader";

    public static AssetDependencyPage GetDependencies(AssetSession session, int? exportIndex = null, int offset = 0, int limit = 100)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be zero or greater.");
        if (limit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be between 1 and 1000.");

        var asset = session.Asset;
        if (exportIndex is not null && (exportIndex < 1 || exportIndex > asset.Exports.Count))
            throw new ArgumentOutOfRangeException(nameof(exportIndex), exportIndex, $"Export index must be between 1 and {asset.Exports.Count}.");

        var result = new List<AssetDependency>();
        AddExportDependencies(result, asset, exportIndex);
        if (exportIndex is null) AddPackageDependencies(result, asset);

        var totalCount = result.Count;
        var page = result.Skip(offset).Take(limit).ToList();
        return new AssetDependencyPage(page, totalCount, offset, limit, offset + page.Count < totalCount);
    }

    private static void AddExportDependencies(List<AssetDependency> result, UAsset asset, int? exportIndex)
    {
        var exportIndexes = exportIndex is null ? Enumerable.Range(1, asset.Exports.Count) : [exportIndex.Value];
        foreach (var oneBasedIndex in exportIndexes) AddExportDependencies(result, asset, oneBasedIndex);
    }

    private static void AddExportDependencies(List<AssetDependency> result, UAsset asset, int oneBasedIndex)
    {
        var export = asset.Exports[oneBasedIndex - 1];
        AddPackageIndex(result, asset, ExportHeaderCategory, oneBasedIndex, "outerIndex", export.OuterIndex);
        AddPackageIndex(result, asset, ExportHeaderCategory, oneBasedIndex, "classIndex", export.ClassIndex);
        AddPackageIndex(result, asset, ExportHeaderCategory, oneBasedIndex, "superIndex", export.SuperIndex);
        AddPackageIndex(result, asset, ExportHeaderCategory, oneBasedIndex, "templateIndex", export.TemplateIndex);

        AddPackageIndexes(result, asset, "serializationBeforeSerialization", oneBasedIndex, export.SerializationBeforeSerializationDependencies);
        AddPackageIndexes(result, asset, "createBeforeSerialization", oneBasedIndex, export.CreateBeforeSerializationDependencies);
        AddPackageIndexes(result, asset, "serializationBeforeCreate", oneBasedIndex, export.SerializationBeforeCreateDependencies);
        AddPackageIndexes(result, asset, "createBeforeCreate", oneBasedIndex, export.CreateBeforeCreateDependencies);
        AddDependsMapDependencies(result, asset, oneBasedIndex);
    }

    private static void AddDependsMapDependencies(List<AssetDependency> result, UAsset asset, int oneBasedIndex)
    {
        if (asset.DependsMap is null || oneBasedIndex - 1 >= asset.DependsMap.Count) return;
        foreach (var rawIndex in asset.DependsMap[oneBasedIndex - 1] ?? [])
            AddPackageIndex(result, asset, "dependsMap", oneBasedIndex, null, new FPackageIndex(rawIndex));
    }

    private static void AddPackageDependencies(List<AssetDependency> result, UAsset asset)
    {
        foreach (var (import, index) in asset.Imports.Select((value, index) => (value, index + 1)))
            AddPackageIndex(result, asset, "importHeader", null, $"import[{index}].outerIndex", import.OuterIndex);

        foreach (var package in asset.SoftPackageReferenceList ?? [])
            result.Add(new AssetDependency("softPackageReference", null, null, null, "softPackage", null, package?.ToString(), null));

        foreach (var dependency in asset.ExtraPackageDependencies ?? [])
            result.Add(new AssetDependency("extraPackageDependency", null, null, null, "package", null, dependency.Key?.ToString(), null, dependency.Value));
    }

    private static void AddPackageIndexes(List<AssetDependency> result, UAsset asset, string category, int sourceExportIndex, IEnumerable<FPackageIndex>? indexes)
    {
        foreach (var index in indexes ?? []) AddPackageIndex(result, asset, category, sourceExportIndex, null, index);
    }

    private static void AddPackageIndex(List<AssetDependency> result, UAsset asset, string category, int? sourceExportIndex, string? sourceField, FPackageIndex? packageIndex)
    {
        var rawIndex = packageIndex?.Index ?? 0;
        if (rawIndex == 0)
        {
            result.Add(new AssetDependency(category, sourceExportIndex, sourceField, rawIndex, "null", null, null, null));
            return;
        }

        if (rawIndex > 0)
        {
            var index = rawIndex - 1;
            if (index >= asset.Exports.Count)
            {
                result.Add(new AssetDependency(category, sourceExportIndex, sourceField, rawIndex, "invalid", null, null, null));
                return;
            }

            var target = asset.Exports[index];
            result.Add(new AssetDependency(category, sourceExportIndex, sourceField, rawIndex, "export", index + 1, SafeName(target.ObjectName), null));
            return;
        }

        var importIndex = -(long)rawIndex - 1;
        if (importIndex >= asset.Imports.Count)
        {
            result.Add(new AssetDependency(category, sourceExportIndex, sourceField, rawIndex, "invalid", null, null, null));
            return;
        }

        var import = asset.Imports[(int)importIndex];
        result.Add(new AssetDependency(category, sourceExportIndex, sourceField, rawIndex, "import", (int)importIndex + 1, SafeName(import.ObjectName), SafeName(import.PackageName)));
    }

    private static string? SafeName(FName? name)
    {
        try { return name?.ToString(); }
        catch (InvalidOperationException) { return null; }
    }
}
