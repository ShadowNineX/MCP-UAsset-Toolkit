using System.ComponentModel;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class MappingTools
{
    [McpServerTool, Description("Load a .usmap or .jmap/.jmap.gz unversioned property mapping file globally")]
    public static Task<string> LoadMappingsAsync(
        [Description("Full path to the .usmap or .jmap file")]
        string mappingsFilePath)
    {
        return ToolHelpers.ExecuteAsync(() => {
            MappingService.SetGlobalMappings(mappingsFilePath);
            var mappings = MappingService.GetGlobalMappings();

            return ToolHelpers.FormatSuccess(new
            {
                mappingsFilePath = Path.GetFullPath(mappingsFilePath),
                version = mappings?.Version.ToString(),
                fileVersionUE4 = mappings?.FileVersionUE4.ToString(),
                fileVersionUE5 = mappings?.FileVersionUE5.ToString(),
                schemaCount = mappings?.Schemas?.Count ?? 0,
                enumCount = mappings?.EnumMap?.Count ?? 0
            }, "Global property mappings loaded successfully");
        });
    }

    [McpServerTool, Description("Get information about currently loaded property mappings")]
    public static Task<string> GetMappingsInfoAsync()
    {
        return ToolHelpers.ExecuteAsync(() => {
            var mappings = MappingService.GetGlobalMappings();
            if (mappings == null)
            {
                return ToolHelpers.FormatSuccess(new { isLoaded = false }, "No global property mappings currently loaded");
            }

            return ToolHelpers.FormatSuccess(new
            {
                isLoaded = true,
                filePath = mappings.FilePath,
                version = mappings.Version.ToString(),
                fileVersionUE4 = mappings.FileVersionUE4.ToString(),
                fileVersionUE5 = mappings.FileVersionUE5.ToString(),
                schemaCount = mappings.Schemas?.Count ?? 0,
                enumCount = mappings.EnumMap?.Count ?? 0,
                netCL = mappings.NetCL
            });
        });
    }
}
