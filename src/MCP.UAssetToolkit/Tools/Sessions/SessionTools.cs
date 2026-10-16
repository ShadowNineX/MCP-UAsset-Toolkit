using System.ComponentModel;
using MCP_UAsset_Toolkit.Source.Models;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class SessionTools
{
    [McpServerTool, Description("Load an asset with per-file engine version, mappings, serialization flags, game override, and map/array struct types. Existing sessions are reused unless forceReload=true.")]
    public static Task<string> OpenAssetAsync(string filePath, AssetLoadOptions? options = null, bool forceReload = false, bool discardChanges = false)
    {
        return ToolHelpers.ExecuteAsync(() =>
        {
            var version = options?.EngineVersion == null ? (UAssetAPI.UnrealTypes.EngineVersion?)null : ToolHelpers.ParseEngineVersion(options.EngineVersion);
            var session = AssetSessionService.GetOrLoadSession(filePath, version, options?.MappingsPath, forceReload, discardChanges, options);
            return ToolHelpers.FormatSuccess(new { filePath = session.FilePath, engineVersion = session.EngineVersion.ToString(), isDirty = session.IsDirty,
                exportCount = session.Asset.Exports.Count, mappingsPath = session.MappingsPath });
        });
    }

    [McpServerTool, Description("List supported advanced UAssetAPI load flags and game-specific overrides.")]
    public static Task<string> GetAssetLoadOptionsAsync() => ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(new
    {
        serializationFlags = Enum.GetNames<UAssetAPI.CustomSerializationFlags>(),
        gameSpecificOverrides = Enum.GetNames<UAssetAPI.UnrealTypes.GameSpecificOverride>()
    }));

    [McpServerTool, Description("List all currently active in-memory asset sessions and dirty states")]
    public static Task<string> ListActiveSessionsAsync()
    {
        return ToolHelpers.ExecuteAsync(() => {
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
        });
    }

    [McpServerTool, Description("Close an asset session. Unsaved changes require explicit discardChanges=true.")]
    public static Task<string> CloseSessionAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Discard unsaved edits instead of requiring a save (default: false)")]
        bool discardChanges = false)
    {
        return ToolHelpers.ExecuteAsync(() => {
            var closed = AssetSessionService.CloseSession(filePath, discardChanges);
            return ToolHelpers.FormatSuccess(new { closed, filePath }, closed ? "Session closed" : "Session not found");
        });
    }

    [McpServerTool, Description("Unload all sessions. If any has unsaved changes, none are unloaded unless discardChanges=true.")]
    public static Task<string> ClearAllSessionsAsync(
        [Description("Discard unsaved edits in all sessions (default: false)")]
        bool discardChanges = false)
    {
        return ToolHelpers.ExecuteAsync(() => {
            AssetSessionService.ClearAllSessions(discardChanges);
            return ToolHelpers.FormatSuccess(null, "All active asset sessions cleared");
        });
    }

    [McpServerTool, Description("Reload an asset from disk using its session engine version and mappings path. Unsaved edits require discardChanges=true.")]
    public static Task<string> ReloadSessionAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Discard unsaved edits and reload from disk (default: false)")]
        bool discardChanges = false)
    {
        return ToolHelpers.ExecuteAsync(() => {
            var session = AssetSessionService.ReloadSession(filePath, discardChanges);
            return ToolHelpers.FormatSuccess(new
            {
                filePath = session.FilePath,
                engineVersion = session.EngineVersion.ToString(),
                isDirty = session.IsDirty,
                exportCount = session.Asset.Exports.Count,
                importCount = session.Asset.Imports.Count
            }, "Session reloaded from disk");
        });
    }
}
