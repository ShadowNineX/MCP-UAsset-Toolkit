using System.ComponentModel;
using System.Text.Json;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class VersionTools
{
    [McpServerTool, Description("Set the Unreal Engine version for asset parsing")]
    public static Task<string> SetEngineVersionAsync(
        [Description("Engine version (e.g., '4.0', '4.10', '4.27', '5.0', '5.1', '5.5', '5.7')")]
        string version)
    {
        return Task.Run(() =>
        {
            try
            {
                var engineVersion = ToolHelpers.ParseEngineVersion(version);
                EngineVersionService.SetVersion(engineVersion, version);

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = $"Engine version set to {version}",
                    version
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        });
    }

    [McpServerResource(UriTemplate = "engine://version", Title = "Current Unreal Engine version")]
    public static Task<string> GetEngineVersionResourceAsync()
    {
        return Task.Run(() =>
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
