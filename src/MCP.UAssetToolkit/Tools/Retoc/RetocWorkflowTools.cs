using System.ComponentModel;
using MCP_UAsset_Toolkit.Source.Models;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class RetocWorkflowTools
{
    [McpServerTool(Name = "extract_iostore_manifest"), Description("Extract and return the manifest inventory from a .utoc IoStore container. This is read-only.")]
    public static Task<string> ExtractIoStoreManifestAsync(
        [Description("Full path to the input .utoc file")] string utocPath,
        RetocOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => FormatManifest(await RetocWorkflowService.ExtractManifestAsync(utocPath, options, cancellationToken)));

    [McpServerTool(Name = "inspect_asset_registry"), Description("Parse and return an Unreal AssetRegistry.bin file through retoc. This is read-only.")]
    public static Task<string> InspectAssetRegistryAsync(
        [Description("Full path to the input AssetRegistry.bin file")] string assetRegistryPath,
        RetocOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => FormatCommand(await RetocWorkflowService.InspectAssetRegistryAsync(assetRegistryPath, options, cancellationToken), "Asset registry inspected"));

    [McpServerTool(Name = "print_script_objects"), Description("Print script objects from a .utoc IoStore container. This is read-only.")]
    public static Task<string> PrintScriptObjectsAsync(
        [Description("Full path to the input .utoc file containing script objects")] string utocPath,
        RetocOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => FormatCommand(await RetocWorkflowService.PrintScriptObjectsAsync(utocPath, options, cancellationToken), "Script objects printed"));

    [McpServerTool(Name = "unpack_iostore"), Description("Extract IoStore files from a .utoc container to a new output directory. Existing directories are refused and output is staged before publication.")]
    public static Task<string> UnpackIoStoreAsync(
        [Description("Full path to the input .utoc file")] string utocPath,
        [Description("New output directory path")] string outputDirectory,
        RetocOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => ToolHelpers.FormatSuccess(await RetocWorkflowService.UnpackAsync(utocPath, outputDirectory, options, cancellationToken), "IoStore files unpacked"));

    [McpServerTool(Name = "unpack_iostore_raw"), Description("Extract raw IoStore chunks from a .utoc container to a new output directory. Existing directories are refused and output is staged before publication.")]
    public static Task<string> UnpackIoStoreRawAsync(
        [Description("Full path to the input .utoc file")] string utocPath,
        [Description("New output directory path")] string outputDirectory,
        RetocOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => ToolHelpers.FormatSuccess(await RetocWorkflowService.UnpackRawAsync(utocPath, outputDirectory, options, cancellationToken), "Raw IoStore chunks unpacked"));

    [McpServerTool(Name = "pack_iostore_raw"), Description("Pack a directory of raw IoStore chunks into new .utoc and .ucas files. Existing outputs are refused and output is staged before publication.")]
    public static Task<string> PackIoStoreRawAsync(
        [Description("Full path to the input directory of raw chunks")] string inputDirectory,
        [Description("New output .utoc path; retoc also creates the matching .ucas file")] string outputUtocPath,
        RetocOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => ToolHelpers.FormatSuccess(await RetocWorkflowService.PackRawAsync(inputDirectory, outputUtocPath, options, cancellationToken), "Raw IoStore container created"));

    [McpServerTool(Name = "get_iostore_chunk"), Description("Extract one 24-hex-character IoStore chunk ID to a new output file. Existing output files are refused and output is staged before publication.")]
    public static Task<string> GetIoStoreChunkAsync(
        [Description("Full path to an input .utoc file or a directory containing .utoc files")] string inputPath,
        [Description("24-character hexadecimal IoStore chunk ID")] string chunkId,
        [Description("New output file path")] string outputPath,
        RetocOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => ToolHelpers.FormatSuccess(await RetocWorkflowService.GetChunkAsync(inputPath, chunkId, outputPath, options, cancellationToken), "IoStore chunk extracted"));

    [McpServerTool(Name = "generate_script_objects"), Description("Generate a new script-objects .utoc and matching .ucas from a .jmap reflection dump. Existing outputs are refused and output is staged before publication.")]
    public static Task<string> GenerateScriptObjectsAsync(
        [Description("Full path to the input .jmap reflection data dump")] string jmapPath,
        [Description("New output .utoc path; retoc also creates the matching .ucas file")] string outputUtocPath,
        [Description("Required retoc engine version, such as UE5_4")] string engineVersion,
        RetocOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => ToolHelpers.FormatSuccess(await RetocWorkflowService.GenerateScriptObjectsAsync(jmapPath, outputUtocPath, engineVersion, options, cancellationToken), "Script objects container created"));

    private static async Task<string> ExecuteAsync(Func<Task<string>> operation)
    {
        try { return await operation(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return ToolHelpers.FormatError(ex.Message, ex); }
    }

    private static string FormatCommand(RetocCommandResult result, string successMessage)
    {
        if (result.LaunchFailed)
            return ToolHelpers.FormatError($"retoc is unavailable. Download it from {RetocService.ReleasesUrl} or set options.retocPath. {result.StandardError}");
        if (result.TimedOut)
            return ToolHelpers.FormatError("retoc timed out. The process tree was stopped.");
        if (result.ExitCode != 0)
            return ToolHelpers.FormatError($"retoc failed with exit code {result.ExitCode}. {result.StandardError}");
        return ToolHelpers.FormatSuccess(result, successMessage);
    }

    private static string FormatManifest(RetocManifestResult result)
    {
        if (result.Command.LaunchFailed)
            return ToolHelpers.FormatError($"retoc is unavailable. Download it from {RetocService.ReleasesUrl} or set options.retocPath. {result.Command.StandardError}");
        if (result.Command.TimedOut)
            return ToolHelpers.FormatError("retoc timed out. The process tree was stopped.");
        if (result.Command.ExitCode != 0)
            return ToolHelpers.FormatError($"retoc failed with exit code {result.Command.ExitCode}. {result.Command.StandardError}");
        return ToolHelpers.FormatSuccess(result, result.Truncated ? "IoStore manifest extracted (truncated to 65536 characters)" : "IoStore manifest extracted");
    }
}
