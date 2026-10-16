using System.Text.Json;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools;
using Xunit;

namespace MCP.UAssetToolkit.Tests;

[Collection("Asset sessions")]
public sealed class ArchiveTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "UAssetToolkitArchiveTests", Guid.NewGuid().ToString("N"));
    private readonly List<string> sessions = [];

    public ArchiveTests() => Directory.CreateDirectory(directory);

    [Fact]
    public void OpenLooseDirectory_ListsVirtualFilesAndClosesSession()
    {
        var packagePath = Path.Combine(directory, "Loose.uasset");
        File.WriteAllBytes(packagePath, [1, 2, 3]);

        var session = Open();
        var files = ArchiveService.ListFiles(session.Id, "loose", limit: 100);
        var json = JsonSerializer.Serialize(files);

        Assert.Contains("Loose.uasset", json, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(session.Provider.RequiredKeys);
        Assert.True(ArchiveService.Close(session.Id));
        Assert.False(ArchiveService.Close(session.Id));
        sessions.Remove(session.Id);
    }

    [Fact]
    public void CreatePak_ThenMountListAndExtract_UsesRealCUE4ParsePakProvider()
    {
        var input = Path.Combine(directory, "input");
        Directory.CreateDirectory(Path.Combine(input, "Game", "Content"));
        var source = Path.Combine(input, "Game", "Content", "readme.txt");
        File.WriteAllText(source, "pak fixture content");
        var pakPath = Path.Combine(directory, "fixture.pak");

        var created = ArchiveService.CreatePakFromDirectory(input, pakPath);
        Assert.Contains("fixture.pak", JsonSerializer.Serialize(created), StringComparison.OrdinalIgnoreCase);
        var session = Open();
        Assert.Single(session.Provider.MountedVfs);
        var virtualPath = session.Provider.Files.Keys.Single(x => x.EndsWith("readme.txt", StringComparison.OrdinalIgnoreCase));
        var output = Path.Combine(directory, "extracted");

        var extracted = ArchiveService.Extract(session.Id, virtualPath, output);
        Assert.Contains("readme.txt", JsonSerializer.Serialize(extracted), StringComparison.OrdinalIgnoreCase);
        var saved = Directory.EnumerateFiles(output, "readme.txt", SearchOption.AllDirectories).Single();
        Assert.Equal("pak fixture content", File.ReadAllText(saved));
    }

    [Fact]
    public void Open_RejectsUnknownGamesAndMissingDirectories()
    {
        Assert.Throws<ArgumentException>(() => ArchiveService.Open(directory, "definitely-not-a-game"));
        Assert.Throws<DirectoryNotFoundException>(() => ArchiveService.Open(Path.Combine(directory, "missing"), "GAME_UE4_27"));
    }

    [Fact]
    public void Open_NormalizesWhitespaceMappingsPathAndClosedSessionsRejectOperations()
    {
        File.WriteAllBytes(Path.Combine(directory, "Loose.uasset"), [1]);
        var session = ArchiveService.Open(directory, "GAME_UE4_27", mappingsPath: "  ");
        sessions.Add(session.Id);
        Assert.Null(session.MappingsPath);
        Assert.True(ArchiveService.Close(session.Id));
        sessions.Remove(session.Id);
        Assert.Throws<KeyNotFoundException>(() => ArchiveService.ListFiles(session.Id, null));
    }

    [Theory]
    [InlineData("../outside.uasset")]
    [InlineData("C:/outside.uasset")]
    [InlineData("//server/share.uasset")]
    [InlineData("folder/../../outside.uasset")]
    [InlineData("folder/CON.txt")]
    [InlineData("folder/unsafe. ")]
    public void ExtractionPaths_RejectTraversalAndRootedPaths(string path)
    {
        Assert.Throws<ArgumentException>(() => ArchiveService.SafeRelativePath(path));
    }

    [Fact]
    public void Extract_RefusesExistingTargetsWithoutWritingAnything()
    {
        File.WriteAllBytes(Path.Combine(directory, "Payload.uasset"), [4, 5, 6]);
        var output = Path.Combine(directory, "output");
        var session = Open();
        var virtualPath = session.Provider.Files.Keys.Single(x => x.EndsWith("Payload.uasset", StringComparison.OrdinalIgnoreCase));
        var relative = ArchiveService.SafeRelativePath(virtualPath);
        var existing = ArchiveService.EnsureUnderRoot(output, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        File.WriteAllBytes(existing, [99]);

        Assert.Throws<IOException>(() => ArchiveService.Extract(session.Id, virtualPath, output));
        Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(existing));
    }

    [Fact]
    public void Extract_OverwriteRejectsExistingFileSymlink()
    {
        File.WriteAllBytes(Path.Combine(directory, "Payload.uasset"), [4, 5, 6]);
        var output = Path.Combine(directory, "output");
        var outside = Path.Combine(directory, "outside.bin");
        File.WriteAllBytes(outside, [99]);
        var session = Open();
        var virtualPath = session.Provider.Files.Keys.Single(x => x.EndsWith("Payload.uasset", StringComparison.OrdinalIgnoreCase));
        var target = ArchiveService.EnsureUnderRoot(output, ArchiveService.SafeRelativePath(virtualPath));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        try
        {
            File.CreateSymbolicLink(target, outside);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException && (ex.HResult & 0xffff) == 1314)
        {
            Assert.Skip("Symlink creation requires Windows Developer Mode or an elevated test process.");
        }

        Assert.Throws<IOException>(() => ArchiveService.Extract(session.Id, virtualPath, output, overwrite: true));
        Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(outside));
    }

    [Fact]
    public async Task Tools_ReturnStructuredErrorsForMissingSession()
    {
        using var response = JsonDocument.Parse(await ArchiveTools.ListArchiveFilesAsync("missing", "anything"));
        Assert.False(response.RootElement.GetProperty("success").GetBoolean());
    }

    private MCP_UAsset_Toolkit.Source.Models.ArchiveSession Open()
    {
        var session = ArchiveService.Open(directory, "GAME_UE4_27");
        sessions.Add(session.Id);
        return session;
    }

    public void Dispose()
    {
        foreach (var id in sessions) ArchiveService.Close(id);
        Directory.Delete(directory, recursive: true);
    }
}
