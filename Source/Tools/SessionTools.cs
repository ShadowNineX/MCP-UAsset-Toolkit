using System.ComponentModel;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class SessionTools
{
    [McpServerTool, Description("List all currently active in-memory asset sessions and dirty states")]
    public static Task<string> ListActiveSessionsAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                var sessions = AssetSessionService.GetAllSessions().Select(s => new
                {
                    filePath = s.FilePath,
                    isDirty = s.IsDirty,
                    engineVersion = s.EngineVersion.ToString(),
                    exportCount = s.Asset.Exports.Count,
                    importCount = s.Asset.Imports.Count,
                    nameCount = s.Asset.GetNameMapIndexList().Count,
                    mappingsPath = s.MappingsPath,
                    createdAt = s.CreatedAt,
                    lastAccessedAt = s.LastAccessedAt
                }).ToList();

                return ToolHelpers.FormatSuccess(new
                {
                    count = sessions.Count,
                    sessions
                });
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }

    [McpServerTool, Description("Close and unload an active asset session from memory")]
    public static Task<string> CloseSessionAsync(
        [Description("Full path to the .uasset file")]
        string filePath)
    {
        return Task.Run(() =>
        {
            try
            {
                var closed = AssetSessionService.CloseSession(filePath);
                return ToolHelpers.FormatSuccess(new { closed, filePath }, closed ? "Session closed" : "Session not found");
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }

    [McpServerTool, Description("Unload all active asset sessions from memory")]
    public static Task<string> ClearAllSessionsAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                AssetSessionService.ClearAllSessions();
                return ToolHelpers.FormatSuccess(null, "All active asset sessions cleared");
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }
}
