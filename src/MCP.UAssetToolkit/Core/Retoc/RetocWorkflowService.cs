using System.Text.Json.Serialization;
using MCP_UAsset_Toolkit.Source.Models;

namespace MCP_UAsset_Toolkit.Source.Services;

/// <summary>Typed, safety-preserving workflows for retoc commands that are not conversions.</summary>
public static class RetocWorkflowService
{
    private const int ManifestOutputLimit = 64 * 1024;

    public static async Task<RetocManifestResult> ExtractManifestAsync(string utocPath, RetocOptions? options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RetocService.ValidateForOperation("manifest", options);
        var input = RequireFile(utocPath, ".utoc", nameof(utocPath));
        var stage = CreateStage(Path.Combine(Path.GetTempPath(), "manifest.json"));
        try
        {
            var result = await RetocService.RunInDirectoryAsync(options?.RetocPath, RetocService.BuildArguments(options, "manifest", input), options?.AesKey, RetocService.ValidateTimeout(options), cancellationToken, stage);
            cancellationToken.ThrowIfCancellationRequested();
            EnsureSuccess(result, "manifest", stage);
            var manifestPath = Path.Combine(stage, "pakstore.json");
            EnsureExpectedFiles([manifestPath]);
            var (manifestJson, truncated) = await ReadCappedAsync(manifestPath, cancellationToken);
            TryDeleteStage(stage);
            return new RetocManifestResult(result, manifestJson, truncated);
        }
        catch (OperationCanceledException)
        {
            TryDeleteStage(stage);
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"{ex.Message} Recovery files, if any, remain in '{stage}'.", ex);
        }
    }

    public static Task<RetocCommandResult> InspectAssetRegistryAsync(string assetRegistryPath, RetocOptions? options, CancellationToken cancellationToken = default) =>
        RunReadOnlyAsync("asset-registry", RequireExistingFile(assetRegistryPath, nameof(assetRegistryPath)), options, cancellationToken);

    public static Task<RetocCommandResult> PrintScriptObjectsAsync(string utocPath, RetocOptions? options, CancellationToken cancellationToken = default) =>
        RunReadOnlyAsync("print-script-objects", RequireFile(utocPath, ".utoc", nameof(utocPath)), options, cancellationToken);

    public static Task<RetocWorkflowResult> UnpackAsync(string utocPath, string outputDirectory, RetocOptions? options, CancellationToken cancellationToken = default) =>
        UnpackCoreAsync("unpack", utocPath, outputDirectory, options, cancellationToken);

    public static Task<RetocWorkflowResult> UnpackRawAsync(string utocPath, string outputDirectory, RetocOptions? options, CancellationToken cancellationToken = default) =>
        UnpackCoreAsync("unpack-raw", utocPath, outputDirectory, options, cancellationToken);

    public static async Task<RetocWorkflowResult> PackRawAsync(string inputDirectory, string outputUtocPath, RetocOptions? options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RetocService.ValidateForOperation("pack-raw", options);
        var input = RequireDirectory(inputDirectory, nameof(inputDirectory));
        var outputUtoc = RequireNewFile(outputUtocPath, ".utoc", nameof(outputUtocPath));
        var outputUcas = Path.ChangeExtension(outputUtoc, ".ucas");
        EnsureMissing(outputUcas);
        EnsureOutsideInput(input, outputUtoc);
        return await RunStagedAsync("pack-raw", [input], outputUtoc, [outputUtoc, outputUcas], options, cancellationToken);
    }

    public static async Task<RetocWorkflowResult> GetChunkAsync(string inputPath, string chunkId, string outputPath, RetocOptions? options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RetocService.ValidateForOperation("get", options);
        var input = RequireUtocOrDirectory(inputPath, nameof(inputPath));
        var normalizedChunkId = RequireChunkId(chunkId);
        var output = RequireNewPath(outputPath, nameof(outputPath));
        EnsureOutsideInput(input, output);
        return await RunStagedAsync("get", [input, normalizedChunkId], output, [output], options, cancellationToken);
    }

    public static async Task<RetocWorkflowResult> GenerateScriptObjectsAsync(string jmapPath, string outputUtocPath, string engineVersion, RetocOptions? options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RetocService.ValidateForOperation("gen-script-objects", options);
        var input = RequireFile(jmapPath, ".jmap", nameof(jmapPath));
        var outputUtoc = RequireNewFile(outputUtocPath, ".utoc", nameof(outputUtocPath));
        var version = RetocService.RequireEngineVersion(engineVersion);
        var outputUcas = Path.ChangeExtension(outputUtoc, ".ucas");
        EnsureMissing(outputUcas);
        EnsureOutsideInput(input, outputUtoc);
        return await RunStagedAsync("gen-script-objects", ["--version", version, input], outputUtoc, [outputUtoc, outputUcas], options, cancellationToken);
    }

    private static async Task<RetocWorkflowResult> UnpackCoreAsync(string action, string utocPath, string outputDirectory, RetocOptions? options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RetocService.ValidateForOperation(action, options);
        var input = RequireFile(utocPath, ".utoc", nameof(utocPath));
        var output = RequireNewDirectory(outputDirectory, nameof(outputDirectory));
        EnsureOutsideInput(input, output);
        return await RunStagedAsync(action, [input], output, [output], options, cancellationToken, outputIsDirectory: true);
    }

    private static async Task<RetocCommandResult> RunReadOnlyAsync(string action, string input, RetocOptions? options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RetocService.ValidateForOperation(action, options);
        var arguments = RetocService.BuildArguments(options, action, input);
        return await RetocService.RunAsync(options?.RetocPath, arguments, options?.AesKey, RetocService.ValidateTimeout(options), cancellationToken);
    }

    private static async Task<RetocWorkflowResult> RunStagedAsync(string action, IReadOnlyList<string> commandInputs, string primaryOutput, IReadOnlyList<string> finalOutputs, RetocOptions? options, CancellationToken cancellationToken, bool outputIsDirectory = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stage = CreateStage(primaryOutput);
        var stagedPrimary = Path.Combine(stage, Path.GetFileName(primaryOutput));
        var stagedOutputs = finalOutputs.Select(output => Path.Combine(stage, Path.GetFileName(output))).ToArray();
        try
        {
            var commandPaths = commandInputs.Concat([stagedPrimary]).ToArray();
            var arguments = RetocService.BuildArguments(options, action, commandPaths);
            var result = await RetocService.RunAsync(options?.RetocPath, arguments, options?.AesKey, RetocService.ValidateTimeout(options), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            EnsureSuccess(result, action, stage);
            if (outputIsDirectory) EnsureExpectedDirectory(stagedPrimary); else EnsureExpectedFiles(stagedOutputs);
            cancellationToken.ThrowIfCancellationRequested();
            Publish(stagedOutputs.Zip(finalOutputs, static (source, destination) => new OutputTransfer(source, destination)));
            TryDeleteStage(stage);
            return new RetocWorkflowResult(result, finalOutputs);
        }
        catch (OperationCanceledException)
        {
            TryDeleteStage(stage);
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"{ex.Message} Recovery files, if any, remain in '{stage}'.", ex);
        }
    }

    private static string RequireChunkId(string? chunkId)
    {
        var value = chunkId?.Trim() ?? string.Empty;
        if (value.Length != 24 || !value.All(Uri.IsHexDigit)) throw new ArgumentException("chunkId must be a 24-character hexadecimal IoStore chunk ID.", nameof(chunkId));
        return value;
    }

    private static string RequireFile(string? path, string extension, string parameterName)
    {
        var fullPath = RequireExistingFile(path, parameterName);
        if (!fullPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"{parameterName} must end with '{extension}'.", parameterName);
        return fullPath;
    }

    private static string RequireExistingFile(string? path, string parameterName)
    {
        var fullPath = Path.GetFullPath(path ?? throw new ArgumentNullException(parameterName));
        if (!File.Exists(fullPath)) throw new FileNotFoundException($"Input file not found: {fullPath}", fullPath);
        return fullPath;
    }

    private static string RequireDirectory(string? path, string parameterName)
    {
        var fullPath = Path.GetFullPath(path ?? throw new ArgumentNullException(parameterName));
        if (!Directory.Exists(fullPath)) throw new DirectoryNotFoundException($"Input directory not found: {fullPath}");
        return fullPath;
    }

    private static string RequireUtocOrDirectory(string? path, string parameterName)
    {
        var fullPath = Path.GetFullPath(path ?? throw new ArgumentNullException(parameterName));
        if (Directory.Exists(fullPath)) return fullPath;
        if (!File.Exists(fullPath)) throw new FileNotFoundException($"Input file or directory not found: {fullPath}", fullPath);
        if (!fullPath.EndsWith(".utoc", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"{parameterName} must be a .utoc file or a directory containing .utoc files.", parameterName);
        return fullPath;
    }

    private static string RequireNewFile(string? path, string extension, string parameterName)
    {
        var fullPath = RequireNewPath(path, parameterName);
        if (!fullPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"{parameterName} must end with '{extension}'.", parameterName);
        return fullPath;
    }

    private static string RequireNewPath(string? path, string parameterName)
    {
        var fullPath = Path.GetFullPath(path ?? throw new ArgumentNullException(parameterName));
        EnsureMissing(fullPath);
        return fullPath;
    }

    private static string RequireNewDirectory(string? path, string parameterName) => RequireNewPath(path, parameterName);

    private static void EnsureMissing(string path)
    {
        if (File.Exists(path) || Directory.Exists(path)) throw new IOException($"Output already exists and will not be overwritten: {path}");
    }

    private static void EnsureOutsideInput(string input, string output)
    {
        if (File.Exists(input) && string.Equals(input, output, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Output path must differ from the input path.");
        if (!Directory.Exists(input)) return;
        var relativeOutput = Path.GetRelativePath(input, output);
        if (!Path.IsPathRooted(relativeOutput) && relativeOutput != ".." && !relativeOutput.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !relativeOutput.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Output path must not be inside the input directory.");
    }

    private static string CreateStage(string output)
    {
        var stage = Path.Combine(Path.GetDirectoryName(output)!, $".{Path.GetFileNameWithoutExtension(output)}.retoc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stage);
        return stage;
    }

    private static void EnsureExpectedFiles(IEnumerable<string> files)
    {
        var missing = files.Where(path => !File.Exists(path) || new FileInfo(path).Length == 0).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException($"retoc completed without the expected output files: {string.Join(", ", missing)}");
    }

    private static void EnsureExpectedDirectory(string path)
    {
        if (!Directory.Exists(path)) throw new InvalidOperationException($"retoc completed without the expected output directory: {path}");
    }

    private static void EnsureSuccess(RetocCommandResult result, string action, string stage)
    {
        if (result.LaunchFailed) throw new InvalidOperationException($"retoc could not start. Download it from {RetocService.ReleasesUrl}. {result.StandardError}");
        if (result.TimedOut) throw new TimeoutException($"retoc {action} exceeded its configured timeout. {result.StandardError} Partial outputs were retained in '{stage}'.");
        if (result.ExitCode != 0) throw new InvalidOperationException($"retoc {action} failed with exit code {result.ExitCode}. Partial outputs were retained in '{stage}'. {result.StandardError}");
    }

    private static void Publish(IEnumerable<OutputTransfer> transfers)
    {
        var moved = new List<OutputTransfer>();
        try
        {
            foreach (var transfer in transfers)
            {
                if (Directory.Exists(transfer.Source)) Directory.Move(transfer.Source, transfer.Destination); else File.Move(transfer.Source, transfer.Destination);
                moved.Add(transfer);
            }
        }
        catch
        {
            foreach (var transfer in moved.AsEnumerable().Reverse())
            {
                if (Directory.Exists(transfer.Destination)) Directory.Move(transfer.Destination, transfer.Source); else if (File.Exists(transfer.Destination)) File.Move(transfer.Destination, transfer.Source);
            }
            throw;
        }
    }

    private static void TryDeleteStage(string stage)
    {
        try { Directory.Delete(stage, recursive: true); }
        catch { /* Staging cleanup after a successful publish is best effort. */ }
    }

    private sealed record OutputTransfer(string Source, string Destination);

    private static async Task<(string Text, bool Truncated)> ReadCappedAsync(string path, CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var output = new System.Text.StringBuilder();
        using var reader = File.OpenText(path);
        while (output.Length < ManifestOutputLimit)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, ManifestOutputLimit - output.Length)), cancellationToken);
            if (read == 0) return (output.ToString(), false);
            output.Append(buffer, 0, read);
        }
        return (output.ToString(), await reader.ReadAsync(buffer.AsMemory(0, 1), cancellationToken) != 0);
    }
}

public sealed record RetocManifestResult(
    [property: JsonPropertyName("command")] RetocCommandResult Command,
    [property: JsonPropertyName("manifestJson")] string ManifestJson,
    [property: JsonPropertyName("truncated")] bool Truncated);

public sealed record RetocWorkflowResult(
    [property: JsonPropertyName("command")] RetocCommandResult Command,
    [property: JsonPropertyName("outputPaths")] IReadOnlyList<string> OutputPaths);
