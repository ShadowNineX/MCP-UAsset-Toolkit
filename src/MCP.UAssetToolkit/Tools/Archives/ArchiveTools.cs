using System.ComponentModel;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class ArchiveTools
{
    [McpServerTool, Description("Open an Unreal archive or loose-package directory with CUE4Parse. game must be a CUE4Parse EGame value such as GAME_UE4_27.")]
    public static Task<string> OpenArchiveSessionAsync(string directory, string game, bool recursive = false, string? mappingsPath = null) => ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(ArchiveService.Describe(ArchiveService.Open(directory, game, recursive, mappingsPath)), "Archive session opened"));
    [McpServerTool, Description("List active CUE4Parse archive sessions, including mounted and unmounted archive metadata and required key GUIDs. AES key values are never returned.")]
    public static Task<string> ListArchiveSessionsAsync() => ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(new { count = ArchiveService.GetAll().Count, sessions = ArchiveService.GetAll().Select(ArchiveService.Describe).ToArray() }));
    [McpServerTool, Description("Submit an AES key for a required archive GUID and mount matching encrypted archives. The key is never returned.")]
    public static Task<string> SubmitArchiveKeyAsync(string sessionId, string keyGuid, string aesKey) => ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(new { sessionId, newlyMounted = ArchiveService.SubmitKey(sessionId, keyGuid, aesKey) }, "Key submitted"));
    [McpServerTool, Description("List or search mounted virtual files. Results are paginated; limit defaults to 100 and is at most 1000.")]
    public static Task<string> ListArchiveFilesAsync(string sessionId, string? query = null, int offset = 0, int limit = 100) => ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(ArchiveService.ListFiles(sessionId, query, offset, limit)));
    [McpServerTool, Description("Read parsed package exports as JSON strings. Results are paginated and bounded by maxJsonCharacters (at most 10000000).")]
    public static Task<string> ReadPackageExportsAsync(string sessionId, string virtualPath, int offset = 0, int limit = 100, int maxJsonCharacters = 1000000) => ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(ArchiveService.ReadPackageExports(sessionId, virtualPath, offset, limit, maxJsonCharacters)));
    [McpServerTool, Description("Extract a virtual file or package and its .uexp/.ubulk companions into an explicit output directory. Existing targets are refused unless overwrite=true.")]
    public static Task<string> ExtractArchiveFileAsync(string sessionId, string virtualPath, string outputDirectory, bool overwrite = false, long maxBytes = 536870912) => ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(ArchiveService.Extract(sessionId, virtualPath, outputDirectory, overwrite, maxBytes), "Extraction complete"));
    [McpServerTool, Description("Close and dispose an archive session.")]
    public static Task<string> CloseArchiveSessionAsync(string sessionId) => ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(new { sessionId, closed = ArchiveService.Close(sessionId) }));
    [McpServerTool, Description("Create an unencrypted .pak from a directory for extract-edit-rebuild workflows. PakVersion defaults to V11. Use the dedicated retoc tools for IoStore .utoc/.ucas rebuilds.")]
    public static Task<string> CreatePakFromDirectoryAsync(string inputDirectory, string outputPakPath, string pakVersion = "V11", string mountPoint = "../../../", bool overwrite = false) => ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(ArchiveService.CreatePakFromDirectory(inputDirectory, outputPakPath, pakVersion, mountPoint, overwrite), "Pak created"));
}
