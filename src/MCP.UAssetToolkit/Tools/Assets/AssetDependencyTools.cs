using System.ComponentModel;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class AssetDependencyTools
{
    [McpServerTool, Description("List resolved package dependencies from asset headers, dependency maps, export serialization order lists, and package references.")]
    public static Task<string> GetAssetDependenciesAsync(
        [Description("Full path to the .uasset file")] string filePath,
        [Description("Optional 1-based export index; when supplied, returns only dependencies owned by that export")] int? exportIndex = null,
        [Description("Zero-based offset into dependency entries")] int offset = 0,
        [Description("Maximum results to return (1-1000)")] int limit = 100)
    {
        return ToolHelpers.ExecuteAsync(() =>
        {
            var session = AssetSessionService.GetOrLoadSession(filePath);
            var page = AssetDependencyService.GetDependencies(session, exportIndex, offset, limit);
            return ToolHelpers.FormatSuccess(new
            {
                count = page.Dependencies.Count,
                totalCount = page.TotalCount,
                offset = page.Offset,
                limit = page.Limit,
                hasMore = page.HasMore,
                dependencies = page.Dependencies,
                isDirty = session.IsDirty
            });
        });
    }
}
