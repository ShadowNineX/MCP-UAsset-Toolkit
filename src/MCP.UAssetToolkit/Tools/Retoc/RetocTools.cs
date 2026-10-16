using System.ComponentModel;
using MCP_UAsset_Toolkit.Source.Models;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class RetocTools
{
    [McpServerTool, Description("Check whether the optional retoc CLI is available and report its version. retoc is not downloaded or installed by this tool.")]
    public static Task<string> RetocStatusAsync(string? retocPath = null, CancellationToken cancellationToken = default) => ExecuteAsync(async () =>
    {
        var result = await RetocService.GetStatusAsync(retocPath, cancellationToken);
        if (result.LaunchFailed)
            return ToolHelpers.FormatError($"retoc is unavailable. Download it from {RetocService.ReleasesUrl} or set retocPath. {result.StandardError}");
        if (result.TimedOut)
            return ToolHelpers.FormatError("retoc timed out while checking its version. The process tree was stopped.");
        if (result.ExitCode != 0)
            return ToolHelpers.FormatError($"retoc was found but --version failed with exit code {result.ExitCode}. {result.StandardError}");
        return ToolHelpers.FormatSuccess(result, "retoc is available");
    });

    [McpServerTool(Name = "inspect_iostore"), Description("Run exactly one read-only retoc IoStore inspection action: info, list, or verify. Requires a .utoc input path.")]
    public static Task<string> InspectIoStoreAsync(string utocPath, [Description("One of: info, list, verify.")] string action = "info", RetocOptions? options = null, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => FormatCommand(await RetocService.InspectAsync(utocPath, action, options, cancellationToken), $"retoc {action} completed"));

    [McpServerTool, Description("Convert a Zen IoStore input (.utoc or Paks directory) to a new legacy .pak. Existing output is refused; the conversion stages beside the output and leaves inputs untouched.")]
    public static Task<string> ConvertToLegacyAsync(string inputPath, string outputPakPath, RetocOptions? options = null, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            var conversion = await RetocService.ConvertToLegacyAsync(inputPath, outputPakPath, options, cancellationToken);
            var message = options is { DryRun: true } ? "Legacy conversion dry run completed" : "Legacy .pak created";
            return ToolHelpers.FormatSuccess(conversion, message);
        });

    [McpServerTool, Description("Convert a legacy .pak or directory to new .utoc, .ucas, and .pak outputs. engineVersion is required in retoc form, such as UE5_4. Existing outputs are refused.")]
    public static Task<string> ConvertToZenAsync(string inputPath, string outputUtocPath, string engineVersion, RetocOptions? options = null, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => ToolHelpers.FormatSuccess(await RetocService.ConvertToZenAsync(inputPath, outputUtocPath, engineVersion, options, cancellationToken), "Zen IoStore outputs created"));

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
}
