using MCP_UAsset_Toolkit.Source.Tools.Common;
using System.Text.Json.Serialization;
using UAssetAPI.Unversioned;

namespace MCP_UAsset_Toolkit.Source.Services;

public sealed record MappingEnumPage(IReadOnlyList<string> Enums, int TotalCount, int Offset, int Limit, bool HasMore);
public sealed record MappingEnumDetails(string Name, string? ModulePath, int EnumFlags, IReadOnlyList<MappingEnumValue> Values, int TotalCount, int Offset, int Limit, bool HasMore);
public sealed record MappingEnumValue([property: JsonPropertyName("value")] long Value, [property: JsonPropertyName("name")] string Name);

public static class MappingQueryService
{
    public static Usmap GetMappings(string? mappingsPath) => string.IsNullOrWhiteSpace(mappingsPath)
        ? MappingService.GetGlobalMappings() ?? throw new InvalidOperationException("No mappings are loaded. Supply mappingsPath or call load_mappings first.")
        : MappingService.GetOrLoadMappings(mappingsPath);

    public static MappingEnumPage ListEnums(string query = "", string? mappingsPath = null, int offset = 0, int limit = 100)
    {
        var mappings = GetMappings(mappingsPath);
        var result = AssetSearch.Search(mappings.EnumMap.Keys.Order(StringComparer.Ordinal), query, static name => [name], offset, limit);
        return new MappingEnumPage(result.Matches, result.TotalCount, result.Offset, result.Limit, result.HasMore);
    }

    public static MappingEnumDetails GetEnum(string enumName, string? mappingsPath = null, int offset = 0, int limit = 100)
    {
        if (string.IsNullOrWhiteSpace(enumName)) throw new ArgumentException("Enum name cannot be empty.", nameof(enumName));
        var mappings = GetMappings(mappingsPath);
        if (!mappings.EnumMap.TryGetValue(enumName, out var entry)) throw new KeyNotFoundException($"Mapping enum '{enumName}' was not found.");
        var values = entry.Values.OrderBy(value => value.Key).Select(value => new MappingEnumValue(value.Key, value.Value)).ToList();
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be zero or greater.");
        if (limit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be between 1 and 1000.");
        var page = values.Skip(offset).Take(limit).ToList();
        return new MappingEnumDetails(entry.Name, entry.ModulePath, entry.EnumFlags, page, values.Count, offset, limit, offset + page.Count < values.Count);
    }

    public static object GetDiagnostics(string? mappingsPath = null)
    {
        if (string.IsNullOrWhiteSpace(mappingsPath) && MappingService.GetGlobalMappings() is null)
            return new { isLoaded = false };

        var mappings = GetMappings(mappingsPath);
        var modules = mappings.Schemas.Values.Select(schema => schema.ModulePath)
            .Concat(mappings.EnumMap.Values.Select(@enum => @enum.ModulePath))
            .Where(module => !string.IsNullOrWhiteSpace(module))
            .Distinct(StringComparer.Ordinal)
            .Count();
        return new
        {
            isLoaded = true,
            filePath = mappings.FilePath,
            version = mappings.Version.ToString(),
            fileVersionUE4 = mappings.FileVersionUE4.ToString(),
            fileVersionUE5 = mappings.FileVersionUE5.ToString(),
            netCL = mappings.NetCL,
            failedExtensions = mappings.FailedExtensions ?? [],
            areFNamesCaseInsensitive = mappings.AreFNamesCaseInsensitive,
            schemaCount = mappings.Schemas.Count,
            moduleCount = modules,
            enumCount = mappings.EnumMap.Count
        };
    }
}
