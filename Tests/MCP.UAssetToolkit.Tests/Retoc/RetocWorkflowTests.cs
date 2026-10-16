using System.Text.Json;
using MCP_UAsset_Toolkit.Source.Models;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools;
using Xunit;
using Xunit.Sdk;

namespace MCP.UAssetToolkit.Tests;

public class RetocWorkflowTests
{
    private const string InitialVersion = "Initial";
    private const string DirectoryIndexVersion = "DirectoryIndex";
    private const string InputFileName = "input.utoc";
    private const string InputFileContents = "input";
    private const string ChunkFileName = "chunk.bin";
    private const string UcasExtension = ".ucas";
    private const string SampleChunkId = "000000000000000000000005";
    private const string SuperSecretKey = "super-secret";
    private static readonly TimeSpan RecoveryPathRegexTimeout = TimeSpan.FromSeconds(1);
    [Fact]
    public void WorkflowArgumentsKeepGlobalOptionsBeforeTheExactRetocCommand()
    {
        var arguments = RetocService.BuildArguments(
            new RetocOptions { AesKey = "secret", ContainerHeaderVersionOverride = InitialVersion, TocVersionOverride = DirectoryIndexVersion },
            "gen-script-objects",
            "--version", "UE5_4", "C:\\input.jmap", "C:\\output.utoc");

        Assert.Equal(
        [
            "--aes-key", "secret",
            "--override-container-header-version", InitialVersion,
            "--override-toc-version", DirectoryIndexVersion,
            "gen-script-objects", "--version", "UE5_4", "C:\\input.jmap", "C:\\output.utoc"
        ], arguments);
    }

    [Fact]
    public async Task UnpackRejectsUnsupportedLegacyOptionsBeforeCreatingOutputOrStage()
    {
        var root = CreateRoot();
        try
        {
            var input = Path.Combine(root, InputFileName);
            var output = Path.Combine(root, "unpacked");
            await File.WriteAllTextAsync(input, InputFileContents, TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<ArgumentException>(() => RetocWorkflowService.UnpackAsync(input, output, new RetocOptions { NoAssets = true }, TestContext.Current.CancellationToken));

            Assert.False(Directory.Exists(output));
            Assert.Empty(Directory.EnumerateDirectories(root, ".unpacked.retoc-*"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task GetChunkRejectsMalformedIdBeforeCreatingOutputOrStage()
    {
        var root = CreateRoot();
        try
        {
            var input = Path.Combine(root, InputFileName);
            var output = Path.Combine(root, ChunkFileName);
            await File.WriteAllTextAsync(input, InputFileContents, TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<ArgumentException>(() => RetocWorkflowService.GetChunkAsync(input, "not-a-chunk-id", output, null, TestContext.Current.CancellationToken));

            Assert.False(File.Exists(output));
            Assert.Empty(Directory.EnumerateDirectories(root, ".chunk.retoc-*"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task PackRawRefusesExistingSidecarBeforeLaunchingRetoc()
    {
        var root = CreateRoot();
        try
        {
            var input = Path.Combine(root, "raw");
            var output = Path.Combine(root, "packed.utoc");
            var sidecar = Path.ChangeExtension(output, UcasExtension);
            Directory.CreateDirectory(input);
            await File.WriteAllTextAsync(sidecar, "existing", TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<IOException>(() => RetocWorkflowService.PackRawAsync(input, output, null, TestContext.Current.CancellationToken));

            Assert.Equal("existing", await File.ReadAllTextAsync(sidecar, TestContext.Current.CancellationToken));
            Assert.False(File.Exists(output));
            Assert.Empty(Directory.EnumerateDirectories(root, ".packed.retoc-*"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ReadOnlyWorkflowReportsMissingRetocWithoutInstallingAnything()
    {
        var root = CreateRoot();
        try
        {
            var input = Path.Combine(root, InputFileName);
            await File.WriteAllTextAsync(input, InputFileContents, TestContext.Current.CancellationToken);
            var missingRetoc = Path.Combine(root, "missing-retoc.exe");

            using var response = JsonDocument.Parse(await RetocWorkflowTools.ExtractIoStoreManifestAsync(input, new RetocOptions { RetocPath = missingRetoc }, TestContext.Current.CancellationToken));

            Assert.False(response.RootElement.GetProperty("success").GetBoolean());
            Assert.Contains(RetocService.ReleasesUrl, response.RootElement.GetProperty("error").GetString());
            Assert.False(File.Exists(missingRetoc));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task UnpackWorkflowInvokesRetocWithExactTypedArgumentsAndPublishesOnlyAfterSuccess()
    {
        RequireWindows();
        var root = CreateRoot();
        try
        {
            var argumentsPath = Path.Combine(root, "arguments.txt");
            var retocPath = CreateFakeRetoc(root, "unpack", argumentsPath);
            var input = Path.Combine(root, InputFileName);
            var output = Path.Combine(root, "out");
            await File.WriteAllTextAsync(input, InputFileContents, TestContext.Current.CancellationToken);
            var options = new RetocOptions { RetocPath = retocPath, AesKey = "test-secret", ContainerHeaderVersionOverride = InitialVersion, TocVersionOverride = DirectoryIndexVersion };

            var result = await RetocWorkflowService.UnpackAsync(input, output, options, TestContext.Current.CancellationToken);

            Assert.True(Directory.Exists(output));
            Assert.Equal("payload", (await File.ReadAllTextAsync(Path.Combine(output, "payload.bin"), TestContext.Current.CancellationToken)).Trim());
            var arguments = (await File.ReadAllTextAsync(argumentsPath, TestContext.Current.CancellationToken)).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("--aes-key", arguments[0]);
            Assert.Equal("test-secret", arguments[1]);
            Assert.Equal("--override-container-header-version", arguments[2]);
            Assert.Equal(InitialVersion, arguments[3]);
            Assert.Equal("--override-toc-version", arguments[4]);
            Assert.Equal(DirectoryIndexVersion, arguments[5]);
            Assert.Equal("unpack", arguments[6]);
            Assert.Equal(input, arguments[7]);
            Assert.StartsWith(Path.Combine(root, ".out.retoc-"), arguments[8], StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("\\out", arguments[8], StringComparison.OrdinalIgnoreCase);
            Assert.Equal([output], result.OutputPaths);
            Assert.Empty(Directory.EnumerateDirectories(root, ".out.retoc-*"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task SuccessfulFakePackAndGetPublishBytesAndSidecars()
    {
        RequireWindows();
        var root = CreateRoot();
        try
        {
            var input = Path.Combine(root, "raw");
            Directory.CreateDirectory(input);
            var packRetoc = CreateFakeRetoc(root, "pack", null);
            var package = Path.Combine(root, "container.utoc");
            var packed = await RetocWorkflowService.PackRawAsync(input, package, new RetocOptions { RetocPath = packRetoc }, TestContext.Current.CancellationToken);
            Assert.Equal("utoc", (await File.ReadAllTextAsync(package, TestContext.Current.CancellationToken)).Trim());
            Assert.Equal("ucas", (await File.ReadAllTextAsync(Path.ChangeExtension(package, UcasExtension), TestContext.Current.CancellationToken)).Trim());
            Assert.Equal([package, Path.ChangeExtension(package, UcasExtension)], packed.OutputPaths);

            var getRetoc = CreateFakeRetoc(root, "get", null);
            var chunk = Path.Combine(root, ChunkFileName);
            await RetocWorkflowService.GetChunkAsync(package, SampleChunkId, chunk, new RetocOptions { RetocPath = getRetoc }, TestContext.Current.CancellationToken);
            Assert.Equal("raw-chunk", await File.ReadAllTextAsync(chunk, TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task PublicationConflictRollsBackEarlierMovesAndPreservesExistingSidecar()
    {
        RequireWindows();
        var root = CreateRoot();
        try
        {
            var input = Path.Combine(root, "raw");
            Directory.CreateDirectory(input);
            var output = Path.Combine(root, "packed.utoc");
            var sidecar = Path.ChangeExtension(output, UcasExtension);
            // Capture creates the conflicting final sidecar after service preflight, inside the subprocess.
            var retocPath = CreateFakeRetoc(root, "pack", sidecar);
            await Assert.ThrowsAsync<InvalidOperationException>(() => RetocWorkflowService.PackRawAsync(input, output, new RetocOptions { RetocPath = retocPath }, TestContext.Current.CancellationToken));
            Assert.False(File.Exists(output));
            Assert.Contains("pack-raw", await File.ReadAllTextAsync(sidecar, TestContext.Current.CancellationToken));
            var stage = Assert.Single(Directory.EnumerateDirectories(root, ".packed.retoc-*"));
            Assert.Equal("utoc", (await File.ReadAllTextAsync(Path.Combine(stage, "packed.utoc"), TestContext.Current.CancellationToken)).Trim());
            Assert.Equal("ucas", (await File.ReadAllTextAsync(Path.Combine(stage, "packed.ucas"), TestContext.Current.CancellationToken)).Trim());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task SuccessfulFakeScriptObjectGenerationPublishesBothContainerFiles()
    {
        RequireWindows();
        var root = CreateRoot();
        try
        {
            var input = Path.Combine(root, "reflection.jmap");
            var output = Path.Combine(root, "script-objects.utoc");
            await File.WriteAllTextAsync(input, "{}", TestContext.Current.CancellationToken);
            var retocPath = CreateFakeRetoc(root, "pack", null);

            var result = await RetocWorkflowService.GenerateScriptObjectsAsync(input, output, "UE5_4", new RetocOptions { RetocPath = retocPath }, TestContext.Current.CancellationToken);

            Assert.Equal([output, Path.ChangeExtension(output, UcasExtension)], result.OutputPaths);
            Assert.Equal("utoc", (await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken)).Trim());
            Assert.Equal("ucas", (await File.ReadAllTextAsync(Path.ChangeExtension(output, UcasExtension), TestContext.Current.CancellationToken)).Trim());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ManifestWorkflowCapturesItsFixedRetocOutputInAStageInsteadOfTheCurrentDirectory()
    {
        RequireWindows();
        var root = CreateRoot();
        try
        {
            var input = Path.Combine(root, InputFileName);
            await File.WriteAllTextAsync(input, InputFileContents, TestContext.Current.CancellationToken);
            var retocPath = CreateFakeRetoc(root, "manifest", null);
            var existingManifest = Path.Combine(root, "pakstore.json");
            await File.WriteAllTextAsync(existingManifest, "existing-manifest", TestContext.Current.CancellationToken);

            var result = await RetocWorkflowService.ExtractManifestAsync(input, new RetocOptions { RetocPath = retocPath }, TestContext.Current.CancellationToken);

            Assert.Equal("{\"oplog\":{\"entries\":[]}}", result.ManifestJson.Trim());
            Assert.False(result.Truncated);
            Assert.Equal("existing-manifest", await File.ReadAllTextAsync(existingManifest, TestContext.Current.CancellationToken));
            Assert.Empty(Directory.EnumerateDirectories(root, ".manifest.retoc-*"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task MissingExpectedOutputRetainsDiagnosticStageWithoutPublishing()
    {
        RequireWindows();
        var root = CreateRoot();
        try
        {
            var input = Path.Combine(root, InputFileName);
            var output = Path.Combine(root, ChunkFileName);
            await File.WriteAllTextAsync(input, InputFileContents, TestContext.Current.CancellationToken);
            var retocPath = CreateFakeRetoc(root, "none", null);

            await Assert.ThrowsAsync<InvalidOperationException>(() => RetocWorkflowService.GetChunkAsync(input, SampleChunkId, output, new RetocOptions { RetocPath = retocPath }, TestContext.Current.CancellationToken));

            Assert.False(File.Exists(output));
            Assert.Single(Directory.EnumerateDirectories(root, ".chunk.retoc-*"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task CancellationCleansWorkflowStageAfterStoppingFakeRetoc()
    {
        RequireWindows();
        var root = CreateRoot();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        try
        {
            var input = Path.Combine(root, InputFileName);
            var output = Path.Combine(root, "out");
            await File.WriteAllTextAsync(input, InputFileContents, TestContext.Current.CancellationToken);
            var retocPath = CreateFakeRetoc(root, "sleep-after-unpack", null);
            var running = RetocWorkflowService.UnpackAsync(input, output, new RetocOptions { RetocPath = retocPath }, cancellation.Token);
            await Task.Delay(200, TestContext.Current.CancellationToken);
            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
            Assert.False(Directory.Exists(output));
            Assert.Empty(Directory.EnumerateDirectories(root, ".out.retoc-*"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task TimedOutWorkflowRedactsTheAesKeyAndRetainsDiagnosticStage()
    {
        RequireWindows();
        var root = CreateRoot();
        try
        {
            var input = Path.Combine(root, InputFileName);
            var output = Path.Combine(root, "out");
            await File.WriteAllTextAsync(input, InputFileContents, TestContext.Current.CancellationToken);
            var retocPath = CreateFakeRetoc(root, "sleep-error", null);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => RetocWorkflowService.UnpackAsync(input, output, new RetocOptions { RetocPath = retocPath, AesKey = SuperSecretKey, TimeoutSeconds = 1 }, TestContext.Current.CancellationToken));

            Assert.DoesNotContain(SuperSecretKey, error.ToString(), StringComparison.Ordinal);
            Assert.Single(Directory.EnumerateDirectories(root, ".out.retoc-*"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task SuccessfulWorkflowRedactsTheAesKeyFromBothRetocOutputStreams()
    {
        RequireWindows();
        var root = CreateRoot();
        try
        {
            var input = Path.Combine(root, InputFileName);
            var output = Path.Combine(root, ChunkFileName);
            await File.WriteAllTextAsync(input, InputFileContents, TestContext.Current.CancellationToken);
            var retocPath = CreateFakeRetoc(root, "get-secret", null);

            var result = await RetocWorkflowService.GetChunkAsync(input, SampleChunkId, output, new RetocOptions { RetocPath = retocPath, AesKey = SuperSecretKey }, TestContext.Current.CancellationToken);

            Assert.DoesNotContain(SuperSecretKey, result.Command.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(SuperSecretKey, result.Command.StandardError, StringComparison.Ordinal);
            Assert.Equal("raw-chunk", await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task InstalledRetocPacksUnpacksAndGetsATinyRawChunkRoundTrip()
    {
        var retocPath = RequireInstalledRetoc();
        var root = CreateRoot();
        try
        {
            const string chunkId = SampleChunkId;
            var bytes = new byte[] { 0, 1, 2, 3, 255 };
            var raw = Path.Combine(root, "raw");
            var chunks = Path.Combine(raw, "chunks");
            Directory.CreateDirectory(chunks);
            await File.WriteAllTextAsync(Path.Combine(raw, "manifest.json"), "{\"chunk_paths\":{},\"version\":\"PerfectHashWithOverflow\",\"mount_point\":\"../../../\"}", TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(chunks, chunkId), bytes, TestContext.Current.CancellationToken);
            var options = new RetocOptions { RetocPath = retocPath, TimeoutSeconds = 15 };
            var utoc = Path.Combine(root, "roundtrip.utoc");
            var unpacked = Path.Combine(root, "unpacked");
            var extracted = Path.Combine(root, ChunkFileName);

            await RetocWorkflowService.PackRawAsync(raw, utoc, options, TestContext.Current.CancellationToken);
            await RetocWorkflowService.UnpackRawAsync(utoc, unpacked, options, TestContext.Current.CancellationToken);
            await RetocWorkflowService.GetChunkAsync(utoc, chunkId, extracted, options, TestContext.Current.CancellationToken);
            var manifestError = await Assert.ThrowsAsync<InvalidOperationException>(() => RetocWorkflowService.ExtractManifestAsync(utoc, options, TestContext.Current.CancellationToken));
            var recovery = System.Text.RegularExpressions.Regex.Match(manifestError.Message, "Recovery files, if any, remain in '([^']+)'", System.Text.RegularExpressions.RegexOptions.None, RecoveryPathRegexTimeout);
            Assert.True(recovery.Success);
            var manifestStage = recovery.Groups[1].Value;
            Assert.StartsWith(Path.Combine(Path.GetTempPath(), ".manifest.retoc-"), manifestStage, StringComparison.OrdinalIgnoreCase);
            Directory.Delete(manifestStage, recursive: true);
            var scriptObjects = await RetocWorkflowService.PrintScriptObjectsAsync(utoc, options, TestContext.Current.CancellationToken);
            var invalidRegistry = Path.Combine(root, "AssetRegistry.bin");
            await File.WriteAllTextAsync(invalidRegistry, "not an asset registry", TestContext.Current.CancellationToken);
            var registry = await RetocWorkflowService.InspectAssetRegistryAsync(invalidRegistry, options, TestContext.Current.CancellationToken);

            Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(unpacked, "chunks", chunkId), TestContext.Current.CancellationToken));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(extracted, TestContext.Current.CancellationToken));
            Assert.True(File.Exists(Path.Combine(unpacked, "manifest.json")));
            Assert.NotEqual(0, scriptObjects.ExitCode);
            Assert.NotEqual(0, registry.ExitCode);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "retoc-workflow-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void RequireWindows()
    {
        if (!OperatingSystem.IsWindows()) throw SkipException.ForSkip("The fake retoc fixture is a Windows command script.");
    }

    private static string RequireInstalledRetoc()
    {
        var configuredPath = Environment.GetEnvironmentVariable("RETOC_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath)) return configuredPath;

        var executableName = OperatingSystem.IsWindows() ? "retoc.exe" : "retoc";
        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var discoveredPath = pathEntries.Select(entry => Path.Combine(entry, executableName)).FirstOrDefault(File.Exists);
        return discoveredPath ?? throw SkipException.ForSkip("retoc is optional and was not found through RETOC_PATH or PATH.");
    }

    private static string CreateFakeRetoc(string root, string mode, string? argumentsPath)
    {
        var scriptPath = Path.Combine(root, $"fake-retoc-{mode}-{Guid.NewGuid():N}.cmd");
        var capture = argumentsPath is null ? string.Empty : $"> \"{argumentsPath}\" echo %*\r\n";
        var body = mode switch
        {
            "unpack" => "mkdir \"%last%\"\r\n> \"%last%\\payload.bin\" echo payload\r\n",
            "pack" => " > \"%last%\" echo utoc\r\nfor %%I in (\"%last%\") do > \"%%~dpnI.ucas\" echo ucas\r\n",
            "get" => "> \"%last%\" <nul set /p \"=raw-chunk\"\r\n",
            "manifest" => "> pakstore.json echo {\"oplog\":{\"entries\":[]}}\r\n",
            "sleep" => "ping 127.0.0.1 -n 31 > nul\r\n",
            "sleep-after-unpack" => "mkdir \"%last%\"\r\n> \"%last%\\payload.bin\" echo payload\r\nping 127.0.0.1 -n 31 > nul\r\n",
            "sleep-error" => "1>&2 echo super-secret\r\nping 127.0.0.1 -n 31 > nul\r\n",
            "get-secret" => "echo super-secret\r\n1>&2 echo super-secret\r\n> \"%last%\" <nul set /p \"=raw-chunk\"\r\n",
            _ => string.Empty
        };
        var script = "@echo off\r\nsetlocal EnableExtensions\r\n" + capture + "set \"last=\"\r\n:next\r\nif \"%~1\"==\"\" goto execute\r\nset \"last=%~1\"\r\nshift\r\ngoto next\r\n:execute\r\n" + body + "exit /b 0\r\n";
        File.WriteAllText(scriptPath, script);
        return scriptPath;
    }
}
