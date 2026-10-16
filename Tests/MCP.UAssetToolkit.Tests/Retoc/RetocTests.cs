using System.Text.Json;
using System.Diagnostics;
using MCP_UAsset_Toolkit.Source.Models;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools;
using Xunit;
using Xunit.Sdk;

namespace MCP.UAssetToolkit.Tests;

public class RetocTests
{
    private const string SuccessProperty = "success";
    private const string TestDirectoryPrefix = "retoc-tests-";
    private const string InputFileContents = "input";
    [Fact]
    public void ArgumentBuilderUsesTypedGlobalOptionsBeforeCommand()
    {
        var inputPath = Path.Combine("test-input", "input.utoc");
        var args = RetocService.BuildArguments(new RetocOptions { AesKey = "secret", ContainerHeaderVersionOverride = "Initial", TocVersionOverride = "DirectoryIndex" }, "info", inputPath);
        Assert.Equal(["--aes-key", "secret", "--override-container-header-version", "Initial", "--override-toc-version", "DirectoryIndex", "info", inputPath], args);
    }

    [Fact]
    public async Task MissingRetocHasDownloadGuidanceWithoutInstallingAnything()
    {
        using var response = JsonDocument.Parse(await RetocTools.RetocStatusAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "retoc.exe"), TestContext.Current.CancellationToken));
        Assert.False(response.RootElement.GetProperty(SuccessProperty).GetBoolean());
        Assert.Contains(RetocService.ReleasesUrl, response.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ConversionRejectsExistingOutputBeforeStartingRetoc()
    {
        var root = Path.Combine(Path.GetTempPath(), TestDirectoryPrefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "legacy.pak");
            var output = Path.Combine(root, "out.utoc");
            await File.WriteAllTextAsync(input, InputFileContents, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(output, "existing", TestContext.Current.CancellationToken);
            using var response = JsonDocument.Parse(await RetocTools.ConvertToZenAsync(input, output, "UE5_4", cancellationToken: TestContext.Current.CancellationToken));
            Assert.False(response.RootElement.GetProperty(SuccessProperty).GetBoolean());
            Assert.Equal("existing", await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task LegacyConversionRejectsInputAsOutputWithoutChangingIt()
    {
        var path = Path.Combine(Path.GetTempPath(), TestDirectoryPrefix + Guid.NewGuid().ToString("N") + ".pak");
        await File.WriteAllTextAsync(path, InputFileContents, TestContext.Current.CancellationToken);
        try
        {
            using var response = JsonDocument.Parse(await RetocTools.ConvertToLegacyAsync(path, path, cancellationToken: TestContext.Current.CancellationToken));
            Assert.False(response.RootElement.GetProperty(SuccessProperty).GetBoolean());
            Assert.Equal(InputFileContents, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task NestedOutputPreflightDoesNotCreateItsMissingParent()
    {
        var root = Path.Combine(Path.GetTempPath(), TestDirectoryPrefix + Guid.NewGuid().ToString("N"));
        var input = Path.Combine(root, "legacy-input");
        var missingOutputParent = Path.Combine(input, "new-output");
        Directory.CreateDirectory(input);
        try
        {
            using var response = JsonDocument.Parse(await RetocTools.ConvertToZenAsync(input, Path.Combine(missingOutputParent, "out.utoc"), "UE5_4", cancellationToken: TestContext.Current.CancellationToken));
            Assert.False(response.RootElement.GetProperty(SuccessProperty).GetBoolean());
            Assert.False(Directory.Exists(missingOutputParent));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task PreCancelledConversionDoesNotCreateAStageDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), TestDirectoryPrefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var input = Path.Combine(root, "input.utoc");
        await File.WriteAllTextAsync(input, InputFileContents, TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RetocService.ConvertToLegacyAsync(input, Path.Combine(root, "out.pak"), null, cancellation.Token));
            Assert.Empty(Directory.EnumerateDirectories(root, ".out.retoc-*"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task InspectRejectsUnsupportedAction()
    {
        using var response = JsonDocument.Parse(await RetocTools.InspectIoStoreAsync("missing.utoc", "unpack", cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(response.RootElement.GetProperty(SuccessProperty).GetBoolean());
        Assert.Contains("info, list, verify", response.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task StatusUsesRetocFromPathWhenInstalled()
    {
        RequireRetocOnPath();
        using var response = JsonDocument.Parse(await RetocTools.RetocStatusAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(response.RootElement.GetProperty(SuccessProperty).GetBoolean());
        Assert.Equal(0, response.RootElement.GetProperty("data").GetProperty("exitCode").GetInt32());
        Assert.Contains("retoc", response.RootElement.GetProperty("data").GetProperty("standardOutput").GetString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InstalledRetocReturnsActionableErrorForInvalidContainer()
    {
        RequireRetocOnPath();
        var path = Path.Combine(Path.GetTempPath(), TestDirectoryPrefix + Guid.NewGuid().ToString("N") + ".utoc");
        await File.WriteAllTextAsync(path, "not an IoStore container", TestContext.Current.CancellationToken);
        try
        {
            var result = await RetocService.InspectAsync(path, "info", new RetocOptions { TimeoutSeconds = 15 }, TestContext.Current.CancellationToken);
            Assert.False(result.LaunchFailed);
            Assert.False(result.TimedOut);
            Assert.NotEqual(0, result.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(result.StandardError));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task TimedOutHelperProcessReturnsWithoutWaitingForTheHelperDuration()
    {
        RequireWindows();
        var root = Path.Combine(Path.GetTempPath(), TestDirectoryPrefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await RunSleepingProcessAsync(1, TestContext.Current.CancellationToken);
            stopwatch.Stop();
            Assert.False(result.LaunchFailed);
            Assert.True(result.TimedOut);
            Assert.NotEqual(0, result.ExitCode);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task CancellationStopsHelperProcess()
    {
        RequireWindows();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        try
        {
            var process = RunSleepingProcessAsync(30, cancellation.Token);
            await Task.Delay(200, TestContext.Current.CancellationToken);
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => process);
        }
        finally { await cancellation.CancelAsync(); }
    }

    private static void RequireRetocOnPath()
    {
        var executable = OperatingSystem.IsWindows() ? "retoc.exe" : "retoc";
        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!pathEntries.Any(entry => File.Exists(Path.Combine(entry, executable))))
            throw SkipException.ForSkip("retoc is optional and was not found on PATH.");
    }

    private static void RequireWindows()
    {
        if (!OperatingSystem.IsWindows()) throw SkipException.ForSkip("The retoc helper process fixture requires Windows command scripts.");
    }

    private static Task<RetocCommandResult> RunSleepingProcessAsync(int timeoutSeconds, CancellationToken cancellationToken)
    {
        var commandPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        return RetocService.RunAsync(commandPath, ["/d", "/c", "ping 127.0.0.1 -n 31 > nul"], null, timeoutSeconds, cancellationToken);
    }
}
