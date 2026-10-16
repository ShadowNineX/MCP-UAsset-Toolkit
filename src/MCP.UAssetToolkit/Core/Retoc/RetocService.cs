using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using MCP_UAsset_Toolkit.Source.Models;

namespace MCP_UAsset_Toolkit.Source.Services;

public static class RetocService
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "S1075", Justification = "This canonical upstream documentation link is returned to clients; it is not a configurable service endpoint or a filesystem path.")]
    public const string ReleasesUrl = "https://github.com/trumank/retoc/releases";
    private const string ToLegacyAction = "to-legacy";
    private const string ToZenAction = "to-zen";
    private const int OutputLimit = 64 * 1024;
    private static readonly TimeSpan StopWaitTimeout = TimeSpan.FromSeconds(5);
    private static readonly HashSet<string> InspectActions = new(StringComparer.Ordinal) { "info", "list", "verify" };
    private static readonly HashSet<string> HeaderVersions = new(StringComparer.Ordinal)
    {
        "PreInitial", "Initial", "LocalizedPackages", "OptionalSegmentPackages", "NoExportInfo", "SoftPackageReferences", "SoftPackageReferencesOffset"
    };
    private static readonly HashSet<string> TocVersions = new(StringComparer.Ordinal)
    {
        "Invalid", "Initial", "DirectoryIndex", "PartitionSize", "PerfectHash", "PerfectHashWithOverflow", "OnDemandMetaData", "RemovedOnDemandMetaData", "ReplaceIoChunkHashWithIoHash"
    };
    private static readonly HashSet<string> EngineVersions = new(StringComparer.Ordinal)
    {
        "UE4_25", "UE4_26", "UE4_27", "UE5_0", "UE5_1", "UE5_2", "UE5_3", "UE5_4", "UE5_5", "UE5_6", "UE5_7"
    };

    public static Task<RetocCommandResult> GetStatusAsync(string? retocPath, CancellationToken cancellationToken = default) =>
        RunAsync(retocPath, ["--version"], null, 15, cancellationToken);

    public static Task<RetocCommandResult> InspectAsync(string utocPath, string action, RetocOptions? options, CancellationToken cancellationToken = default)
    {
        var normalizedAction = RequireInspectAction(action);
        ValidateForOperation(normalizedAction, options);
        var input = RequireFile(utocPath, ".utoc");
        var arguments = BuildArguments(options, normalizedAction, input);
        if (normalizedAction == "list") AddListOptions(arguments, options);
        return RunAsync(options?.RetocPath, arguments, options?.AesKey, ValidateTimeout(options), cancellationToken);
    }

    public static async Task<RetocConversionResult> ConvertToLegacyAsync(string inputPath, string outputPakPath, RetocOptions? options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateForOperation(ToLegacyAction, options);
        var input = RequireInput(inputPath, ".utoc");
        var output = RequireOutput(outputPakPath, ".pak", "outputPakPath");
        EnsureDoesNotOverlapInput(input, output);
        cancellationToken.ThrowIfCancellationRequested();
        var stage = CreateStage(output);
        var stagedOutput = Path.Combine(stage, Path.GetFileName(output));

        try
        {
            var arguments = BuildArguments(options, ToLegacyAction, input, stagedOutput);
            AddLegacyOptions(arguments, options);
            var result = await RunAsync(options?.RetocPath, arguments, options?.AesKey, ValidateTimeout(options), cancellationToken);
            EnsureSuccess(result, ToLegacyAction, stage);
            if (options?.DryRun == true)
            {
                TryDeleteStage(stage);
                return new RetocConversionResult(result, []);
            }
            EnsureExpectedFiles([stagedOutput]);
            cancellationToken.ThrowIfCancellationRequested();
            Publish([new FileTransfer(stagedOutput, output)]);
            TryDeleteStage(stage);
            return new RetocConversionResult(result, [output]);
        }
        catch (OperationCanceledException)
        {
            TryDeleteStage(stage);
            throw;
        }
        catch (Exception ex)
        {
            throw ToSafeFailure(ex, stage, options?.AesKey);
        }
    }

    public static async Task<RetocConversionResult> ConvertToZenAsync(string inputPath, string outputUtocPath, string engineVersion, RetocOptions? options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateForOperation(ToZenAction, options);
        var input = RequireInput(inputPath, ".pak");
        var outputUtoc = RequireOutput(outputUtocPath, ".utoc", "outputUtocPath");
        var version = RequireEngineVersion(engineVersion);
        EnsureDoesNotOverlapInput(input, outputUtoc);
        var outputUcas = Path.ChangeExtension(outputUtoc, ".ucas");
        var outputPak = Path.ChangeExtension(outputUtoc, ".pak");
        EnsureMissing(outputUcas);
        EnsureMissing(outputPak);
        cancellationToken.ThrowIfCancellationRequested();
        var stage = CreateStage(outputUtoc);
        var stagedUtoc = Path.Combine(stage, Path.GetFileName(outputUtoc));
        var stagedUcas = Path.ChangeExtension(stagedUtoc, ".ucas");
        var stagedPak = Path.ChangeExtension(stagedUtoc, ".pak");

        try
        {
            var arguments = BuildArguments(options, ToZenAction, input, stagedUtoc);
            AddFilters(arguments, options);
            arguments.Add("--version");
            arguments.Add(version);
            var result = await RunAsync(options?.RetocPath, arguments, options?.AesKey, ValidateTimeout(options), cancellationToken);
            EnsureSuccess(result, ToZenAction, stage);
            EnsureExpectedFiles([stagedUtoc, stagedUcas, stagedPak]);
            cancellationToken.ThrowIfCancellationRequested();
            Publish([new FileTransfer(stagedUtoc, outputUtoc), new FileTransfer(stagedUcas, outputUcas), new FileTransfer(stagedPak, outputPak)]);
            TryDeleteStage(stage);
            return new RetocConversionResult(result, [outputUtoc, outputUcas, outputPak]);
        }
        catch (OperationCanceledException)
        {
            TryDeleteStage(stage);
            throw;
        }
        catch (Exception ex)
        {
            throw ToSafeFailure(ex, stage, options?.AesKey);
        }
    }

    public static List<string> BuildArguments(RetocOptions? options, string action, params string[] paths)
    {
        ValidateOptions(options);
        var arguments = new List<string>();
        if (!string.IsNullOrWhiteSpace(options?.AesKey)) { arguments.Add("--aes-key"); arguments.Add(options.AesKey.Trim()); }
        if (!string.IsNullOrWhiteSpace(options?.ContainerHeaderVersionOverride)) { arguments.Add("--override-container-header-version"); arguments.Add(options.ContainerHeaderVersionOverride.Trim()); }
        if (!string.IsNullOrWhiteSpace(options?.TocVersionOverride)) { arguments.Add("--override-toc-version"); arguments.Add(options.TocVersionOverride.Trim()); }
        arguments.Add(action);
        arguments.AddRange(paths);
        return arguments;
    }

    internal static Task<RetocCommandResult> RunAsync(string? retocPath, IReadOnlyList<string> arguments, string? aesKey, int timeoutSeconds, CancellationToken cancellationToken) =>
        RunCoreAsync(retocPath, arguments, aesKey, timeoutSeconds, cancellationToken, null);

    internal static Task<RetocCommandResult> RunInDirectoryAsync(string? retocPath, IReadOnlyList<string> arguments, string? aesKey, int timeoutSeconds, CancellationToken cancellationToken, string workingDirectory) =>
        RunCoreAsync(retocPath, arguments, aesKey, timeoutSeconds, cancellationToken, workingDirectory);

    private static async Task<RetocCommandResult> RunCoreAsync(string? retocPath, IReadOnlyList<string> arguments, string? aesKey, int timeoutSeconds, CancellationToken cancellationToken, string? workingDirectory)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var executable = ResolveExecutable(retocPath);
        var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (!string.IsNullOrWhiteSpace(workingDirectory)) startInfo.WorkingDirectory = workingDirectory;
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = startInfo };
        cancellationToken.ThrowIfCancellationRequested();
        try { process.Start(); }
        catch (Exception ex) { return RetocCommandResult.LaunchFailure(executable, Redact(ex.Message, aesKey)); }

        // Keep draining after cancellation so a killed process cannot leave a redirected pipe unread.
        var stdoutTask = ReadCappedAsync(process.StandardOutput, CancellationToken.None);
        var stderrTask = ReadCappedAsync(process.StandardError, CancellationToken.None);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var timedOut = false;
        try { await process.WaitForExitAsync(combined.Token); }
        catch (OperationCanceledException)
        {
            timedOut = timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
            var terminationFailure = await StopProcessAsync(process);
            if (terminationFailure is not null)
            {
                Observe(stdoutTask);
                Observe(stderrTask);
                var safeFailure = Redact(terminationFailure, aesKey);
                if (cancellationToken.IsCancellationRequested) throw new RetocTerminationException(safeFailure);
                return new RetocCommandResult(executable, -1, false, string.Empty, safeFailure, false);
            }
        }
        var output = await DrainOutputAsync(stdoutTask, stderrTask);
        if (output.Error is not null)
            return new RetocCommandResult(executable, -1, false, string.Empty, Redact(output.Error, aesKey), false);
        cancellationToken.ThrowIfCancellationRequested();
        return new RetocCommandResult(executable, process.ExitCode, timedOut, Redact(output.StandardOutput!, aesKey), Redact(output.StandardError!, aesKey), false);
    }

    private static async Task<string?> StopProcessAsync(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            if (process.HasExited) return null;
            return "retoc process state could not be queried while stopping it.";
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return $"retoc process could not be stopped: {ex.Message}";
        }
        catch (NotSupportedException ex)
        {
            return $"retoc process could not be stopped: {ex.Message}";
        }

        using var stopWait = new CancellationTokenSource(StopWaitTimeout);
        try
        {
            await process.WaitForExitAsync(stopWait.Token);
            return null;
        }
        catch (OperationCanceledException)
        {
            return $"retoc process did not exit within {StopWaitTimeout.TotalSeconds:0} seconds after cancellation.";
        }
    }

    private static void Observe(Task task) => _ = task.ContinueWith(completed => _ = completed.Exception, TaskContinuationOptions.OnlyOnFaulted);

    private static async Task<ProcessOutput> DrainOutputAsync(Task<string> stdoutTask, Task<string> stderrTask)
    {
        try
        {
            await Task.WhenAll(stdoutTask, stderrTask).WaitAsync(StopWaitTimeout);
            return new ProcessOutput(stdoutTask.Result, stderrTask.Result, null);
        }
        catch (TimeoutException)
        {
            Observe(stdoutTask);
            Observe(stderrTask);
            return new ProcessOutput(null, null, $"retoc output pipes did not close within {StopWaitTimeout.TotalSeconds:0} seconds after process exit.");
        }
    }

    private static async Task<string> ReadCappedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var output = new StringBuilder();
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0) break;
            if (output.Length < OutputLimit) output.Append(buffer, 0, Math.Min(read, OutputLimit - output.Length));
        }
        return output.Length == OutputLimit ? output.Append("\n[output truncated]").ToString() : output.ToString();
    }

    private static string ResolveExecutable(string? retocPath)
    {
        if (string.IsNullOrWhiteSpace(retocPath)) return OperatingSystem.IsWindows() ? "retoc.exe" : "retoc";
        var fullPath = Path.GetFullPath(retocPath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException($"retoc executable was not found at '{fullPath}'. Download a release from {ReleasesUrl} or provide a valid retocPath.", fullPath);
        return fullPath;
    }

    private static string RequireInspectAction(string? action)
    {
        var normalizedAction = action?.Trim();
        if (normalizedAction is null || !InspectActions.Contains(normalizedAction)) throw new ArgumentException("action must be one of: info, list, verify.", nameof(action));
        return normalizedAction;
    }
    private static string RequireFile(string path, string extension)
    {
        var fullPath = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        if (!fullPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"Path must end with '{extension}'.", nameof(path));
        if (!File.Exists(fullPath)) throw new FileNotFoundException($"Input file not found: {fullPath}", fullPath);
        return fullPath;
    }
    private static string RequireInput(string inputPath, string fileExtension)
    {
        var fullPath = Path.GetFullPath(inputPath ?? throw new ArgumentNullException(nameof(inputPath)));
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath)) throw new FileNotFoundException($"Input file or directory not found: {fullPath}", fullPath);
        if (File.Exists(fullPath) && !fullPath.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"Input file must end with '{fileExtension}' or be a directory.", nameof(inputPath));
        return fullPath;
    }
    private static string RequireOutput(string outputPath, string extension, string parameterName)
    {
        var fullPath = Path.GetFullPath(outputPath ?? throw new ArgumentNullException(parameterName));
        if (!fullPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"{parameterName} must end with '{extension}'.", parameterName);
        EnsureMissing(fullPath);
        return fullPath;
    }
    private static void EnsureMissing(string path)
    {
        if (File.Exists(path) || Directory.Exists(path)) throw new IOException($"Output already exists and will not be overwritten: {path}");
    }
    private static void EnsureDoesNotOverlapInput(string input, string output)
    {
        if (File.Exists(input) && string.Equals(input, output, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Output path must differ from the input path.");
        if (!Directory.Exists(input)) return;
        var relativeOutput = Path.GetRelativePath(input, output);
        var outsideInput = Path.IsPathRooted(relativeOutput)
            || relativeOutput == ".."
            || relativeOutput.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relativeOutput.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
        if (!outsideInput) throw new ArgumentException("Output path must not be inside the input directory.");
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
    private static void EnsureSuccess(RetocCommandResult result, string action, string stage)
    {
        if (result.LaunchFailed) throw new InvalidOperationException($"retoc could not start. Download it from {ReleasesUrl}. {result.StandardError}");
        if (result.TimedOut) throw new TimeoutException($"retoc {action} exceeded its configured timeout. {result.StandardError} Partial outputs were retained in '{stage}'.");
        if (result.ExitCode != 0) throw new InvalidOperationException($"retoc {action} failed with exit code {result.ExitCode}. Partial outputs were retained in '{stage}'. {result.StandardError}");
    }
    private static void Publish(IEnumerable<FileTransfer> transfers)
    {
        var moved = new List<FileTransfer>();
        try { foreach (var transfer in transfers) { File.Move(transfer.Source, transfer.Destination); moved.Add(transfer); } }
        catch
        {
            foreach (var transfer in moved.AsEnumerable().Reverse().Where(transfer => File.Exists(transfer.Destination))) File.Move(transfer.Destination, transfer.Source);
            throw;
        }
    }
    private static void TryDeleteStage(string stage)
    {
        try { Directory.Delete(stage, recursive: true); }
        catch { /* Converted outputs were already published; staging cleanup is best effort. */ }
    }
    private static void ValidateOptions(RetocOptions? options)
    {
        if (options is null) return;
        if (options.TimeoutSeconds is < 1 or > 3600) throw new ArgumentOutOfRangeException(nameof(options), options.TimeoutSeconds, "TimeoutSeconds must be between 1 and 3600.");
        if (!string.IsNullOrWhiteSpace(options.ContainerHeaderVersionOverride) && !HeaderVersions.Contains(options.ContainerHeaderVersionOverride.Trim())) throw new ArgumentException("Unsupported container header version override.");
        if (!string.IsNullOrWhiteSpace(options.TocVersionOverride) && !TocVersions.Contains(options.TocVersionOverride.Trim())) throw new ArgumentException("Unsupported TOC version override.");
        if (options.Filters?.Any(string.IsNullOrWhiteSpace) == true) throw new ArgumentException("Filters cannot contain blank values.");
        if (!string.IsNullOrWhiteSpace(options.LegacyEngineVersion)) RequireEngineVersion(options.LegacyEngineVersion);
    }
    internal static void ValidateForOperation(string action, RetocOptions? options)
    {
        ValidateOptions(options);
        if (options is null) return;
        var hasListOptions = options.ListAll || options.ListHash || options.ListPackage || options.ListSize || options.ListPath || options.ListStore;
        if (hasListOptions && action != "list") throw new ArgumentException("List display options are only supported for inspect_iostore action=list.");
        var hasLegacyOnlyOptions = options.NoAssets || options.NoShaders || options.NoScriptObjects || options.NoCompressShaders || options.DryRun || !string.IsNullOrWhiteSpace(options.LegacyEngineVersion);
        if (hasLegacyOnlyOptions && action != ToLegacyAction) throw new ArgumentException("Legacy conversion options are only supported by convert_to_legacy.");
        if (options.Filters?.Count > 0 && action is not ToLegacyAction and not ToZenAction) throw new ArgumentException("Filters are only supported by conversion tools.");
    }
    internal static int ValidateTimeout(RetocOptions? options) { ValidateOptions(options); return options?.TimeoutSeconds ?? 300; }
    internal static string RequireEngineVersion(string engineVersion)
    {
        var value = engineVersion?.Trim() ?? string.Empty;
        if (!EngineVersions.Contains(value)) throw new ArgumentException("engineVersion must be a retoc EngineVersion enum name (UE4_25 through UE4_27 or UE5_0 through UE5_7), such as UE5_4.", nameof(engineVersion));
        return value;
    }
    private static void AddFilters(List<string> arguments, RetocOptions? options)
    {
        foreach (var filter in options?.Filters ?? []) { arguments.Add("--filter"); arguments.Add(filter); }
    }
    private static void AddListOptions(List<string> arguments, RetocOptions? options)
    {
        if (options is null) return;
        if (options.ListAll) arguments.Add("--all");
        if (options.ListHash) arguments.Add("--hash");
        if (options.ListPackage) arguments.Add("--package");
        if (options.ListSize) arguments.Add("--size");
        if (options.ListPath) arguments.Add("--path");
        if (options.ListStore) arguments.Add("--store");
    }
    private static void AddLegacyOptions(List<string> arguments, RetocOptions? options)
    {
        AddFilters(arguments, options);
        if (options is null) return;
        if (options.NoAssets) arguments.Add("--no-assets");
        if (options.NoShaders) arguments.Add("--no-shaders");
        if (options.NoScriptObjects) arguments.Add("--no-script-objects");
        if (options.NoCompressShaders) arguments.Add("--no-compres-shaders");
        if (options.DryRun) arguments.Add("--dry-run");
        if (!string.IsNullOrWhiteSpace(options.LegacyEngineVersion)) { arguments.Add("--version"); arguments.Add(options.LegacyEngineVersion.Trim()); }
    }
    private static Exception ToSafeFailure(Exception exception, string stage, string? aesKey)
    {
        var message = Redact(exception.Message, aesKey);
        return new InvalidOperationException($"{message} Recovery files, if any, remain in '{stage}'.", exception);
    }
    private static string Redact(string text, string? aesKey) => string.IsNullOrWhiteSpace(aesKey) ? text : text.Replace(aesKey.Trim(), "[redacted]", StringComparison.Ordinal);
    private sealed record FileTransfer(string Source, string Destination);
    private sealed record ProcessOutput(string? StandardOutput, string? StandardError, string? Error);
    private sealed class RetocTerminationException(string message) : InvalidOperationException(message);
}

public sealed record RetocCommandResult(
    [property: JsonPropertyName("executable")] string Executable,
    [property: JsonPropertyName("exitCode")] int ExitCode,
    [property: JsonPropertyName("timedOut")] bool TimedOut,
    [property: JsonPropertyName("standardOutput")] string StandardOutput,
    [property: JsonPropertyName("standardError")] string StandardError,
    [property: JsonPropertyName("launchFailed")] bool LaunchFailed)
{
    public static RetocCommandResult LaunchFailure(string executable, string error) => new(executable, -1, false, string.Empty, error, true);
}

public sealed record RetocConversionResult(
    [property: JsonPropertyName("command")] RetocCommandResult Command,
    [property: JsonPropertyName("outputPaths")] IReadOnlyList<string> OutputPaths);
