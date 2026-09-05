using System.ComponentModel;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;
using UAssetAPI.Unversioned;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class MappingInspectionTools
{
    [McpServerTool, Description("Search loaded mapping schema names with pagination. Supply mappingsPath or use the global mappings.")]
    public static Task<string> ListMappingSchemasAsync(string query = "", string? mappingsPath = null, int offset = 0, int limit = 100)
    {
        return ToolHelpers.ExecuteAsync(() =>
        {
            var mappings = GetMappings(mappingsPath);
            var result = AssetSearch.Search(mappings.Schemas.Keys.Order(StringComparer.Ordinal), query, name => [name], offset, limit);
            return ToolHelpers.FormatSuccess(new { count = result.Matches.Count, totalCount = result.TotalCount, offset, limit, hasMore = result.HasMore, schemas = result.Matches });
        });
    }

    [McpServerTool, Description("Inspect a mapping schema and its inherited property layout, including type details for arrays, maps, structs and enums.")]
    public static Task<string> GetMappingSchemaAsync(string schemaName, string? mappingsPath = null, string? modulePath = null, int offset = 0, int limit = 100)
    {
        return ToolHelpers.ExecuteAsync(() =>
        {
            var mappings = GetMappings(mappingsPath);
            var schema = mappings.GetSchemaFromName(schemaName, modulePath: modulePath);
            var properties = mappings.GetAllProperties(schemaName, modulePath).Select(property => new
            {
                name = property.Name, schemaIndex = property.SchemaIndex, arrayIndex = property.ArrayIndex,
                arraySize = property.ArraySize, flags = property.PropertyFlags.ToString(),
                type = property.PropertyData?.Type.ToString(),
                typeDetails = Newtonsoft.Json.Linq.JToken.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(property.PropertyData)).ToString()
            });
            var page = AssetSearch.Search(properties, "", property => [property.name], offset, limit);
            return ToolHelpers.FormatSuccess(new { name = schema.Name, superType = schema.SuperType, modulePath = schema.ModulePath,
                count = page.Matches.Count, totalCount = page.TotalCount, offset, limit, hasMore = page.HasMore, properties = page.Matches });
        });
    }

    [McpServerTool, Description("Export mappings to diagnostic JSON. This is a lossy visualization, not reloadable .jmap/.usmap. Existing output is preserved unless overwrite=true.")]
    public static Task<string> ExportMappingsJsonAsync(string outputPath, string? mappingsPath = null, bool overwrite = false)
    {
        return ToolHelpers.ExecuteAsync(() =>
        {
            var mappings = GetMappings(mappingsPath);
            var path = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = new FileStream(path, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write);
            using var writer = new StreamWriter(stream);
            writer.Write(mappings.SerializeJSON(Newtonsoft.Json.Formatting.Indented));
            return ToolHelpers.FormatSuccess(new { outputPath = path });
        });
    }

    [McpServerTool, Description("Copy a .usmap and patch the copy with an Unreal engine version. The source is preserved; output must be a new file.")]
    public static Task<string> PatchMappingsVersionAsync(string mappingsPath, string outputPath, string engineVersion)
    {
        return ToolHelpers.ExecuteAsync(() =>
        {
            var version = ToolHelpers.ParseEngineVersion(engineVersion);
            if (version == UAssetAPI.UnrealTypes.EngineVersion.UNKNOWN) throw new ArgumentException("An explicit engine version is required.");
            var source = Path.GetFullPath(mappingsPath);
            var target = Path.GetFullPath(outputPath);
            if (!File.Exists(source)) throw new FileNotFoundException("Mappings file not found.", source);
            if (File.Exists(target)) throw new IOException("Output already exists; choose a new path.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(source, temporary, overwrite: false);
                Usmap.PatchUsmapWithVersion(temporary, version);
                File.Move(temporary, target, overwrite: false);
                return ToolHelpers.FormatSuccess(new { outputPath = target, engineVersion = version.ToString() });
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        });
    }

    private static Usmap GetMappings(string? path) => string.IsNullOrWhiteSpace(path)
        ? MappingService.GetGlobalMappings() ?? throw new InvalidOperationException("No mappings are loaded. Supply mappingsPath or call load_mappings first.")
        : MappingService.GetOrLoadMappings(path);
}
