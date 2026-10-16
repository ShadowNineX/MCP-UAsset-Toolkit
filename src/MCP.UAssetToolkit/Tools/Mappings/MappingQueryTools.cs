using System.ComponentModel;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class MappingQueryTools
{
    [McpServerTool, Description("Search mapping enums by name with pagination. Supply mappingsPath or use the global mappings.")]
    public static Task<string> ListMappingEnumsAsync(string query = "", string? mappingsPath = null, int offset = 0, int limit = 100) =>
        ToolHelpers.ExecuteAsync(() =>
        {
            var page = MappingQueryService.ListEnums(query, mappingsPath, offset, limit);
            return ToolHelpers.FormatSuccess(new { count = page.Enums.Count, totalCount = page.TotalCount, offset = page.Offset, limit = page.Limit, hasMore = page.HasMore, enums = page.Enums });
        });

    [McpServerTool, Description("Inspect a mapping enum and its numeric values with pagination. Supply mappingsPath or use the global mappings.")]
    public static Task<string> GetMappingEnumAsync(string enumName, string? mappingsPath = null, int offset = 0, int limit = 100) =>
        ToolHelpers.ExecuteAsync(() =>
        {
            var result = MappingQueryService.GetEnum(enumName, mappingsPath, offset, limit);
            return ToolHelpers.FormatSuccess(new { name = result.Name, modulePath = result.ModulePath, enumFlags = result.EnumFlags, count = result.Values.Count, totalCount = result.TotalCount, offset = result.Offset, limit = result.Limit, hasMore = result.HasMore, values = result.Values });
        });

    [McpServerTool, Description("Get mapping parser diagnostics, optionally for an explicit mappings file.")]
    public static Task<string> GetMappingDiagnosticsAsync(string? mappingsPath = null) =>
        ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(MappingQueryService.GetDiagnostics(mappingsPath)));
}
