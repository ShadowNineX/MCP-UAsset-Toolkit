using System.ComponentModel;
using System.Text.Json;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;
using UAssetAPI.UnrealTypes;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType, McpServerResourceType]
public static class VersionTools
{
    [McpServerTool, Description("Set the default Unreal Engine version for asset parsing")]
    public static Task<string> SetEngineVersionAsync(
        [Description("Engine version (e.g., '4.27', '5.0', '5.1', '5.5', '5.7', '5.0EA')")]
        string version)
    {
        return ToolHelpers.ExecuteAsync(() =>
        {
            try
            {
                var engineVersion = ToolHelpers.ParseEngineVersion(version);
                EngineVersionService.SetVersion(engineVersion, version);

                return ToolHelpers.FormatSuccess(new
                {
                    version,
                    engineVersion = engineVersion.ToString()
                }, $"Engine version set to {version}");
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError($"Failed to set engine version: {ex.Message}", ex);
            }
        });
    }

    [McpServerTool, Description("Get list of all supported Unreal Engine versions")]
    public static Task<string> GetSupportedEngineVersionsAsync()
    {
        return ToolHelpers.ExecuteAsync(() =>
        {
            var versions = Enum.GetNames<EngineVersion>()
                .Where(v => v.StartsWith("VER_UE"))
                .Select(v => new
                {
                    enumName = v,
                    displayVersion = v.Replace("VER_UE", "").Replace("_", ".").Replace(".0EA", " 5.0 EA")
                })
                .ToList();

            return ToolHelpers.FormatSuccess(new
            {
                count = versions.Count,
                versions
            });
        });
    }

    [McpServerResource(UriTemplate = "engine://version", Title = "Current Unreal Engine version")]
    public static Task<string> GetEngineVersionResourceAsync()
    {
        return ToolHelpers.ExecuteAsync(() =>
        {
            if (!EngineVersionService.IsVersionSet)
            {
                return JsonSerializer.Serialize(new
                {
                    isSet = false,
                    message = ToolHelpers.VersionNotSetError
                });
            }

            return JsonSerializer.Serialize(new
            {
                isSet = true,
                version = EngineVersionService.VersionString,
                engineVersion = EngineVersionService.CurrentVersion.ToString()
            });
        });
    }
}
