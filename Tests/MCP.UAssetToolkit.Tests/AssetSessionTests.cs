using MCP_UAsset_Toolkit.Source.Models;
using System.Text.Json;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using Xunit;

namespace MCP.UAssetToolkit.Tests;

[CollectionDefinition("Asset sessions", DisableParallelization = true)]
public class AssetSessionCollection;

[Collection("Asset sessions")]
public sealed class AssetSessionTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "UAssetToolkitTests", Guid.NewGuid().ToString("N"));
    private string AssetPath => Path.Combine(directory, "TestSoundClass.uasset");

    public AssetSessionTests()
    {
        Directory.CreateDirectory(directory);
        AssetSessionService.ClearAllSessions(discardChanges: true);
        EngineVersionService.ClearVersion();
    }

    private AssetSession LoadFixture()
    {
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestSoundClass.uasset"), AssetPath);
        return AssetSessionService.GetOrLoadSession(AssetPath, EngineVersion.VER_UE4_27);
    }

    [Fact]
    public void RegisteredAssetCanBeUsedBeforeItsFileExists()
    {
        var session = AssetSessionService.RegisterSession(AssetPath, new UAsset(), isDirty: true);

        Assert.False(File.Exists(AssetPath));
        Assert.Same(session, AssetSessionService.GetOrLoadSession(AssetPath));
        Assert.Same(session, AssetSessionService.GetOrLoadSession(Path.Combine(directory, ".", "TestSoundClass.uasset")));
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void CloseRequiresExplicitDiscardForDirtySession()
    {
        var session = AssetSessionService.RegisterSession(AssetPath, new UAsset(), isDirty: true);

        Assert.Throws<InvalidOperationException>(() => AssetSessionService.CloseSession(AssetPath));
        Assert.Same(session, AssetSessionService.GetSession(AssetPath));
        Assert.True(AssetSessionService.CloseSession(AssetPath, discardChanges: true));
        Assert.Null(AssetSessionService.GetSession(AssetPath));
        Assert.False(AssetSessionService.CloseSession(AssetPath));
    }

    [Fact]
    public void ClearRefusesEntireOperationIfAnySessionIsDirty()
    {
        AssetSessionService.RegisterSession(AssetPath, new UAsset());
        AssetSessionService.RegisterSession(Path.Combine(directory, "Dirty.uasset"), new UAsset(), isDirty: true);

        Assert.Throws<InvalidOperationException>(() => AssetSessionService.ClearAllSessions());
        Assert.Equal(2, AssetSessionService.GetAllSessions().Count);
        AssetSessionService.ClearAllSessions(discardChanges: true);
        Assert.Empty(AssetSessionService.GetAllSessions());
    }

    [Fact]
    public void ReloadDiscardsEditsOnlyWhenRequestedAndPreservesEngineVersion()
    {
        var session = LoadFixture();
        var originalFolder = session.Asset.FolderName.Value;
        session.Asset.FolderName = new FString("Unsaved");
        AssetSessionService.MarkDirty(AssetPath);
        EngineVersionService.SetVersion(EngineVersion.VER_UE5_3, "5.3");

        Assert.Throws<InvalidOperationException>(() => AssetSessionService.ReloadSession(AssetPath));
        Assert.Same(session, AssetSessionService.GetSession(AssetPath));
        var reloaded = AssetSessionService.ReloadSession(AssetPath, discardChanges: true);

        Assert.NotSame(session, reloaded);
        Assert.False(reloaded.IsDirty);
        Assert.Equal(EngineVersion.VER_UE4_27, reloaded.EngineVersion);
        Assert.Equal(originalFolder, reloaded.Asset.FolderName.Value);
    }

    [Fact]
    public void FailedReloadKeepsOriginalDirtySession()
    {
        var session = LoadFixture();
        AssetSessionService.MarkDirty(AssetPath);
        File.WriteAllText(AssetPath, "invalid asset");

        Assert.ThrowsAny<Exception>(() => AssetSessionService.ReloadSession(AssetPath, discardChanges: true));
        Assert.Same(session, AssetSessionService.GetSession(AssetPath));
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void SaveAsKeepsSourceDirtyAndSaveClearsIt()
    {
        var session = LoadFixture();
        session.Asset.FolderName = new FString("ChangedFolder");
        AssetSessionService.MarkDirty(AssetPath);
        var copyPath = Path.Combine(directory, "copy", "Saved.uasset");

        var copy = AssetSessionService.SaveSession(AssetPath, copyPath);
        Assert.True(copy.success);
        Assert.True(copy.fileSize > 0);
        Assert.True(session.IsDirty);
        Assert.Equal("ChangedFolder", new UAsset(copyPath, EngineVersion.VER_UE4_27).FolderName.Value);

        AssetSessionService.SaveSession(AssetPath);
        Assert.False(session.IsDirty);
        Assert.Equal("ChangedFolder", new UAsset(AssetPath, EngineVersion.VER_UE4_27).FolderName.Value);
    }

    [Fact]
    public async Task JsonImportWithoutSavingCanBeEditedAndSavedToNewPath()
    {
        var source = LoadFixture();
        var target = Path.Combine(directory, "new", "Imported.uasset");
        using var response = JsonDocument.Parse(await AssetJsonTools.ImportAssetFromJsonAsync(source.Asset.SerializeJson(), target, saveToDisk: false));

        Assert.True(response.RootElement.GetProperty("success").GetBoolean());
        Assert.False(File.Exists(target));
        var imported = AssetSessionService.GetOrLoadSession(target);
        Assert.True(imported.IsDirty);
        imported.Asset.FolderName = new FString("ImportedFolder");

        AssetSessionService.SaveSession(target);
        Assert.False(imported.IsDirty);
        Assert.Equal("ImportedFolder", new UAsset(target, EngineVersion.VER_UE4_27).FolderName.Value);
    }

    [Fact]
    public async Task SessionToolsReturnStructuredErrorsWithoutLosingEdits()
    {
        var session = AssetSessionService.RegisterSession(AssetPath, new UAsset(), isDirty: true);
        using var close = JsonDocument.Parse(await SessionTools.CloseSessionAsync(AssetPath));
        using var reload = JsonDocument.Parse(await SessionTools.ReloadSessionAsync(AssetPath));
        using var clear = JsonDocument.Parse(await SessionTools.ClearAllSessionsAsync());

        Assert.False(close.RootElement.GetProperty("success").GetBoolean());
        Assert.False(reload.RootElement.GetProperty("success").GetBoolean());
        Assert.False(clear.RootElement.GetProperty("success").GetBoolean());
        Assert.Same(session, AssetSessionService.GetSession(AssetPath));
    }

    public void Dispose()
    {
        AssetSessionService.ClearAllSessions(discardChanges: true);
        EngineVersionService.ClearVersion();
        Directory.Delete(directory, recursive: true);
    }
}
