using System.Text.Json;
using System.Text.Json.Nodes;
using MCP_UAsset_Toolkit.Source.Models;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using Xunit;

namespace MCP.UAssetToolkit.Tests;

[Collection("Asset sessions")]
public sealed class AssetDocumentTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "UAssetToolkitDocumentTests", Guid.NewGuid().ToString("N"));
    private string AssetPath => Path.Combine(directory, "TestSoundClass.uasset");

    public AssetDocumentTests()
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
    public void ReadsRootAndEnforcesOutputCap()
    {
        var session = LoadFixture();

        var root = AssetDocumentService.Read(AssetPath);
        Assert.IsType<JsonObject>(root.Value);
        Assert.True(root.JsonLength > 0);
        Assert.Throws<InvalidOperationException>(() => AssetDocumentService.Read(AssetPath, "", 1));
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void ReplacesPackageFieldAtomicallyAndPreservesContext()
    {
        var session = LoadFixture();
        var mappings = session.Asset.Mappings;
        var flags = session.Asset.CustomSerializationFlags;
        var gameOverride = session.Asset.GameSpecificOverride;
        var mapOverrides = session.Asset.MapStructTypeOverride;
        var arrayOverrides = session.Asset.ArrayStructTypeOverride;
        var nameMapHashes = session.Asset.OverrideNameMapHashes;
        session.Asset.UseSeparateBulkDataFiles = true;

        AssetDocumentService.Patch(AssetPath, "/FolderName", "replace", "\"DocumentPatched\"");

        Assert.True(session.IsDirty);
        Assert.Equal("DocumentPatched", session.Asset.FolderName.Value);
        Assert.Equal(AssetPath, session.Asset.FilePath);
        Assert.Same(mappings, session.Asset.Mappings);
        Assert.Equal(flags, session.Asset.CustomSerializationFlags);
        Assert.Equal(gameOverride, session.Asset.GameSpecificOverride);
        Assert.Equivalent(mapOverrides, session.Asset.MapStructTypeOverride);
        Assert.Equivalent(arrayOverrides, session.Asset.ArrayStructTypeOverride);
        Assert.True(session.Asset.UseSeparateBulkDataFiles);
        Assert.Equivalent(nameMapHashes, session.Asset.OverrideNameMapHashes);
    }

    [Fact]
    public void FailedPatchLeavesLiveAssetAndDirtyStateUntouched()
    {
        var session = LoadFixture();
        var original = session.Asset;
        var folder = session.Asset.FolderName.Value;

        Assert.Throws<ArgumentException>(() => AssetDocumentService.Patch(AssetPath, "/FolderName", "replace", "not json"));
        Assert.Same(original, session.Asset);
        Assert.Equal(folder, session.Asset.FolderName.Value);
        Assert.False(session.IsDirty);

        Assert.Throws<InvalidOperationException>(() => AssetDocumentService.Patch(AssetPath, "/missing", "replace", "1"));
        Assert.Same(original, session.Asset);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void ProtectsTypeAndSessionPathMetadata()
    {
        var session = LoadFixture();
        var original = session.Asset;

        Assert.Throws<InvalidOperationException>(() => AssetDocumentService.Patch(AssetPath, "/$type", "replace", "\"System.String, System.Private.CoreLib\""));
        Assert.Throws<InvalidOperationException>(() => AssetDocumentService.Patch(AssetPath, "/FilePath", "replace", "\"C:/elsewhere.uasset\""));

        Assert.Same(original, session.Asset);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void RejectedCanonicalOrInvalidPatchesKeepSessionUntouched()
    {
        var session = LoadFixture();
        var original = session.Asset;

        Assert.Throws<InvalidOperationException>(() => AssetDocumentService.Patch(AssetPath, "/Typo", "add", "1"));
        Assert.ThrowsAny<Exception>(() => AssetDocumentService.Patch(AssetPath, "/Exports", "replace", "null"));
        Assert.Throws<InvalidOperationException>(() => AssetDocumentService.Patch(AssetPath, "/Typo", "add", "{\"$type\":1}"));
        Assert.Throws<ArgumentException>(() => AssetDocumentService.Read(AssetPath, "/Exports/~2"));
        Assert.Throws<InvalidOperationException>(() => AssetDocumentService.Patch(AssetPath, "/Exports/+1", "replace", "null"));

        Assert.Same(original, session.Asset);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void AllowsWhitelistedNewPropertyTypeButRejectsForeignType()
    {
        var session = LoadFixture();
        const string boolType = "UAssetAPI.PropertyTypes.Objects.BoolPropertyData, UAssetAPI";
        var propertyJson = "{\"$type\":\"UAssetAPI.PropertyTypes.Objects.BoolPropertyData, UAssetAPI\",\"Name\":\"DocumentBool\",\"Value\":true}";

        AssetDocumentService.Patch(AssetPath, "/Exports/0/Data/-", "add", propertyJson);

        var data = AssetDocumentService.Read(AssetPath, "/Exports/0/Data").Value as JsonArray;
        Assert.NotNull(data);
        var last = Assert.IsType<JsonObject>(data[data.Count - 1]);
        Assert.Equal(boolType, last["$type"]!.GetValue<string>());
        Assert.Equal("DocumentBool", last["Name"]!.GetValue<string>());
        Assert.True(session.IsDirty);

        var accepted = session.Asset;
        Assert.Throws<InvalidOperationException>(() => AssetDocumentService.Patch(AssetPath, "/Exports/0/Data/-", "add", "{\"$type\":\"System.String, System.Private.CoreLib\",\"Value\":\"nope\"}"));
        Assert.Same(accepted, session.Asset);
    }

    [Fact]
    public async Task ToolsReturnStructuredJson()
    {
        LoadFixture();

        using var read = JsonDocument.Parse(await AssetDocumentTools.ReadAssetDocumentAsync(AssetPath, "/FolderName"));
        Assert.True(read.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("/FolderName", read.RootElement.GetProperty("data").GetProperty("pointer").GetString());

        using var patch = JsonDocument.Parse(await AssetDocumentTools.PatchAssetDocumentAsync(AssetPath, "/FolderName", "replace", "\"ToolPatched\""));
        Assert.True(patch.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("ToolPatched", AssetSessionService.GetSession(AssetPath)!.Asset.FolderName.Value);
    }

    public void Dispose()
    {
        AssetSessionService.ClearAllSessions(discardChanges: true);
        EngineVersionService.ClearVersion();
        Directory.Delete(directory, recursive: true);
    }
}
