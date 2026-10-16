using System.ComponentModel;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class ArchiveQueryTools
{
    [McpServerTool, Description("Read one exactly named object from an Unreal package as bounded JSON. virtualPath and objectName are exact selections; maxJsonCharacters is at most 10000000.")]
    public static Task<string> ReadArchiveObjectAsync(string sessionId, string virtualPath, string objectName, int maxJsonCharacters = 1000000) => ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(ArchiveQueryService.ReadObject(sessionId, virtualPath, objectName, maxJsonCharacters)));

    [McpServerTool, Description("List object name and type metadata in one Unreal package. Results are paginated; limit defaults to 100 and is at most 1000.")]
    public static Task<string> ListArchiveObjectsAsync(string sessionId, string virtualPath, int offset = 0, int limit = 100) => ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(ArchiveQueryService.ListObjects(sessionId, virtualPath, offset, limit)));

    [McpServerTool, Description("Get virtual-file metadata including size, compression, encryption, package state, and source container metadata when available.")]
    public static Task<string> GetArchiveFileInfoAsync(string sessionId, string virtualPath) => ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(ArchiveQueryService.GetFileInfo(sessionId, virtualPath)));

    [McpServerTool, Description("Configure only the supplied CUE4Parse read flags for one archive session. Omitted nullable flags retain the provider's current defaults or settings.")]
    public static Task<string> ConfigureArchiveReadOptionsAsync(string sessionId, bool? readScriptData = null, bool? readShaderMaps = null, bool? readNaniteData = null, bool? skipReferencedTextures = null, bool? useLazyPackageSerialization = null) => ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(ArchiveQueryService.ConfigureReadOptions(sessionId, readScriptData, readShaderMaps, readNaniteData, skipReferencedTextures, useLazyPackageSerialization), "Archive read options configured"));

    [McpServerTool, Description("Get the current CUE4Parse read flags for one archive session.")]
    public static Task<string> GetArchiveReadOptionsAsync(string sessionId) => ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(ArchiveQueryService.GetReadOptions(sessionId)));
}
