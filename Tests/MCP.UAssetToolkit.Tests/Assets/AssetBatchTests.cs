using System.Text.Json;
using System.Text.Json.Nodes;
using MCP_UAsset_Toolkit.Source.Models;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.UnrealTypes;
using Xunit;

namespace MCP.UAssetToolkit.Tests;

[Collection("Asset sessions")]
public sealed class AssetBatchTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "UAssetToolkitBatchTests", Guid.NewGuid().ToString("N"));
    private string AssetPath => Path.Combine(directory, "Test.uasset");

    public AssetBatchTests()
    {
        Directory.CreateDirectory(directory);
        AssetSessionService.ClearAllSessions(discardChanges: true);
        EngineVersionService.ClearVersion();
    }

    private AssetSession LoadFixture(string? path = null)
    {
        path ??= AssetPath;
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestSoundClass.uasset"), path);
        return AssetSessionService.GetOrLoadSession(path, EngineVersion.VER_UE4_27);
    }

    private static AssetDocumentEdit Edit(string pointer, string operation, string? value = null, string? from = null) =>
        new() { Pointer = pointer, Operation = operation, ValueJson = value, From = from };

    [Fact]
    public void BatchCommitsOrderedEditsAndPreservesContext()
    {
        var session = LoadFixture();
        var original = session.Asset;
        var mappings = original.Mappings;
        original.ArrayStructTypeOverride["BatchArray"] = new FString("Guid");
        var result = AssetDocumentService.PatchBatch(AssetPath,
        [
            Edit("/FolderName", "test", JsonSerializer.Serialize(original.FolderName.Value)),
            Edit("/FolderName", "replace", "\"First\""),
            Edit("/FolderName", "replace", "\"FinalBatchFolder\""),
            Edit("/PackageSource", "replace", "123")
        ]);

        Assert.Equal(4, result.OperationCount);
        Assert.True(result.Changed);
        Assert.False(result.Preview);
        Assert.NotSame(original, session.Asset);
        Assert.Equal("FinalBatchFolder", session.Asset.FolderName.Value);
        Assert.Equal((uint)123, session.Asset.PackageSource);
        Assert.Equal(AssetPath, session.Asset.FilePath);
        Assert.Same(mappings, session.Asset.Mappings);
        Assert.Equal("Guid", session.Asset.ArrayStructTypeOverride["BatchArray"].Value);
        Assert.True(session.IsDirty);
        AssetSessionService.SaveSession(AssetPath);
        Assert.Equal("FinalBatchFolder", new UAsset(AssetPath, EngineVersion.VER_UE4_27).FolderName.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreviewAndTestOnlyBatchesPreserveOriginalState(bool initiallyDirty)
    {
        var session = LoadFixture();
        session.IsDirty = initiallyDirty;
        var original = session.Asset;
        var preview = AssetDocumentService.PatchBatch(AssetPath, [Edit("/FolderName", "replace", "\"PreviewOnly\"")], preview: true);
        Assert.True(preview.Changed);
        Assert.True(preview.Preview);
        Assert.Same(original, session.Asset);
        Assert.Equal(initiallyDirty, session.IsDirty);
        var testOnly = AssetDocumentService.PatchBatch(AssetPath, [Edit("/FolderName", "test", JsonSerializer.Serialize(original.FolderName.Value))]);
        Assert.False(testOnly.Changed);
        Assert.Same(original, session.Asset);
        Assert.Equal(initiallyDirty, session.IsDirty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LateFailureRollsBackWholeBatchAndDirtyState(bool initiallyDirty)
    {
        var session = LoadFixture();
        session.IsDirty = initiallyDirty;
        var original = session.Asset;
        Assert.Throws<InvalidOperationException>(() => AssetDocumentService.PatchBatch(AssetPath,
            [Edit("/FolderName", "replace", "\"DiscardThis\""), Edit("/FolderName", "test", "\"Wrong\"")]));
        Assert.Same(original, session.Asset);
        Assert.Equal(initiallyDirty, session.IsDirty);
        Assert.Throws<InvalidOperationException>(() => AssetDocumentService.PatchBatch(AssetPath,
            [Edit("/FolderName", "replace", "\"DiscardThis\""), Edit("/Missing", "remove")]));
        Assert.Same(original, session.Asset);
        Assert.Equal(initiallyDirty, session.IsDirty);
    }

    [Fact]
    public void BatchSupportsOrderedArrayCopyMoveAndRemove()
    {
        var session = LoadFixture();
        AssetDocumentService.PatchBatch(AssetPath,
        [
            Edit("/ChunkIDs", "replace", "[1,2,3]"),
            Edit("/ChunkIDs/-", "copy", from: "/ChunkIDs/0"),
            Edit("/ChunkIDs/0", "move", from: "/ChunkIDs/1"),
            Edit("/ChunkIDs/2", "remove"),
            Edit("/ChunkIDs", "test", "[2,1,1]")
        ]);
        Assert.Equal([2, 1, 1], session.Asset.ChunkIDs);
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void BatchPreservesUntouchedSubtreesWhenRegisteringNewNames()
    {
        var session = LoadFixture();
        var importsBefore = AssetDocumentService.Read(AssetPath, "/Imports").Value;
        var registryBefore = AssetDocumentService.Read(AssetPath, "/AssetRegistryRecords").Value;
        var oldProperties = Assert.IsAssignableFrom<NormalExport>(session.Asset.Exports[0]).Data.Count;
        AssetDocumentService.PatchBatch(AssetPath,
        [
            Edit("/FolderName", "replace", "\"LongerBatchFolderForOffsetRecalculation\""),
            Edit("/Exports/0/Data/-", "add", "{\"$type\":\"UAssetAPI.PropertyTypes.Objects.BoolPropertyData, UAssetAPI\",\"Name\":\"BatchNovelName_17\",\"Value\":true}")
        ]);
        Assert.True(JsonNode.DeepEquals(importsBefore, AssetDocumentService.Read(AssetPath, "/Imports").Value));
        Assert.True(JsonNode.DeepEquals(registryBefore, AssetDocumentService.Read(AssetPath, "/AssetRegistryRecords").Value));
        var properties = Assert.IsAssignableFrom<NormalExport>(session.Asset.Exports[0]).Data;
        Assert.Equal(oldProperties + 1, properties.Count);
        var added = Assert.IsType<BoolPropertyData>(properties[^1]);
        Assert.Equal("BatchNovelName_17", added.Name.ToString());
        Assert.True(added.Value);
    }

    [Theory]
    [InlineData("/Typo", "add", "1")]
    [InlineData("/FilePath", "replace", "\"C:/other.uasset\"")]
    [InlineData("/$type", "replace", "\"System.String, System.Private.CoreLib\"")]
    [InlineData("/Exports/0/Data/-", "add", "{\"$type\":\"System.String, System.Private.CoreLib\",\"Value\":\"no\"}")]
    [InlineData("/Exports/0/SerialSize", "replace", "0")]
    public void RejectedCanonicalOrProtectedEditsDoNotCommit(string pointer, string operation, string value)
    {
        var session = LoadFixture();
        var original = session.Asset;
        Assert.Throws<InvalidOperationException>(() => AssetDocumentService.PatchBatch(AssetPath,
            [Edit("/FolderName", "replace", "\"DiscardThis\""), Edit(pointer, operation, value)]));
        Assert.Same(original, session.Asset);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void InvalidSyntaxAndBatchLimitsAreRejectedBeforeMutation()
    {
        var session = LoadFixture();
        var original = session.Asset;
        Assert.Throws<ArgumentNullException>(() => AssetDocumentService.PatchBatch(AssetPath, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => AssetDocumentService.PatchBatch(AssetPath, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => AssetDocumentService.PatchBatch(AssetPath, Enumerable.Repeat(Edit("/FolderName", "replace", "null"), 101).ToArray()));
        Assert.Throws<ArgumentNullException>(() => AssetDocumentService.PatchBatch(AssetPath, [null!]));
        Assert.Throws<ArgumentException>(() => AssetDocumentService.PatchBatch(AssetPath, [Edit("/FolderName", "unknown")]));
        Assert.Throws<ArgumentException>(() => AssetDocumentService.PatchBatch(AssetPath, [Edit("/FolderName", "replace")]));
        Assert.Throws<ArgumentException>(() => AssetDocumentService.PatchBatch(AssetPath, [Edit("/FolderName", "remove", "null")]));
        Assert.Throws<ArgumentException>(() => AssetDocumentService.PatchBatch(AssetPath, [Edit("/FolderName", "copy")]));
        Assert.Throws<ArgumentException>(() => AssetDocumentService.PatchBatch(AssetPath, [Edit("/FolderName", "replace", "null", "/ChunkIDs")]));
        Assert.Throws<ArgumentException>(() => AssetDocumentService.PatchBatch(AssetPath, [Edit("/FolderName", "replace", "not-json")]));
        Assert.Throws<ArgumentException>(() => AssetDocumentService.PatchBatch(AssetPath, [Edit("/FolderName", "replace", new string(' ', 1_000_001))]));
        Assert.Throws<ArgumentException>(() => AssetDocumentService.PatchBatch(AssetPath, [Edit("/Exports/~2", "remove")]));
        Assert.Throws<InvalidOperationException>(() => AssetDocumentService.PatchBatch(AssetPath, [Edit("/Exports/0/Data", "move", from: "/Exports")]));
        Assert.Same(original, session.Asset);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void MultiAssetPreviewAndApplyIsolateFailuresAndNeverSave()
    {
        var first = LoadFixture();
        var secondPath = Path.Combine(directory, "Second.uasset");
        var second = LoadFixture(secondPath);
        var missingPath = Path.Combine(directory, "Missing.uasset");
        var diskBefore = File.ReadAllBytes(AssetPath);
        var secondDiskBefore = File.ReadAllBytes(secondPath);
        var edits = new[] { Edit("/FolderName", "replace", "\"BulkEdited\"") };
        var preview = AssetBatchService.Patch([AssetPath, missingPath, secondPath], edits);
        Assert.Equal([true, false, true], preview.Select(item => item.Success));
        Assert.False(first.IsDirty);
        Assert.False(second.IsDirty);
        Assert.NotEqual("BulkEdited", first.Asset.FolderName.Value);
        var applied = AssetBatchService.Patch([AssetPath, missingPath, secondPath], edits, preview: false);
        Assert.Equal([true, false, true], applied.Select(item => item.Success));
        Assert.Contains("not found", applied[1].Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("FileNotFoundException", applied[1].ExceptionType);
        Assert.Null(applied[1].Result);
        Assert.True(first.IsDirty);
        Assert.True(second.IsDirty);
        Assert.Equal("BulkEdited", first.Asset.FolderName.Value);
        Assert.Equal("BulkEdited", second.Asset.FolderName.Value);
        Assert.Equal(diskBefore, File.ReadAllBytes(AssetPath));
        Assert.Equal(secondDiskBefore, File.ReadAllBytes(secondPath));
        Assert.False(File.Exists(missingPath));
    }

    [Fact]
    public void BatchValidationReportsEachFileAndRejectsInvalidPathLists()
    {
        var session = LoadFixture();
        var results = AssetBatchService.Validate([AssetPath, Path.Combine(directory, "Missing.uasset")]);
        Assert.Equal([true, false], results.Select(item => item.Success));
        using var result = JsonDocument.Parse(JsonSerializer.Serialize(results[0].Result));
        Assert.True(result.RootElement.GetProperty("canSerialize").GetBoolean());
        Assert.False(session.IsDirty);
        Assert.Throws<ArgumentOutOfRangeException>(() => AssetBatchService.Validate([]));
        Assert.Throws<ArgumentOutOfRangeException>(() => AssetBatchService.Validate(Enumerable.Repeat(AssetPath, 51).ToArray()));
        Assert.Throws<ArgumentException>(() => AssetBatchService.Validate([AssetPath, Path.Combine(directory, ".", "Test.uasset")]));
        Assert.Throws<ArgumentException>(() => AssetBatchService.Validate([" "]));
    }

    [Fact]
    public void CloneCreatesIndependentDirtyTemplateAndRefusesCollisions()
    {
        var source = LoadFixture();
        source.Asset.ArrayStructTypeOverride["CloneArray"] = new FString("Guid");
        var targetPath = Path.Combine(directory, "Clone.uasset");
        var clone = AssetBatchService.CloneSession(AssetPath, targetPath);
        Assert.False(File.Exists(targetPath));
        Assert.True(clone.IsDirty);
        Assert.False(source.IsDirty);
        Assert.NotSame(source.Asset, clone.Asset);
        Assert.Equal(source.EngineVersion, clone.EngineVersion);
        Assert.Equal(targetPath, clone.Asset.FilePath);
        Assert.Equal("Guid", clone.Asset.ArrayStructTypeOverride["CloneArray"].Value);
        clone.Asset.FolderName = new FString("IndependentClone");
        Assert.NotEqual("IndependentClone", source.Asset.FolderName.Value);
        AssetSessionService.SaveSession(targetPath);
        Assert.Equal("IndependentClone", new UAsset(targetPath, EngineVersion.VER_UE4_27).FolderName.Value);
        Assert.Throws<IOException>(() => AssetBatchService.CloneSession(AssetPath, targetPath));
        Assert.Throws<IOException>(() => AssetBatchService.CloneSession(AssetPath, AssetPath));
        Assert.Throws<ArgumentException>(() => AssetBatchService.CloneSession(AssetPath, Path.Combine(directory, "bad.txt")));
    }

    [Fact]
    public async Task BatchToolsExposePerFileSummaryAndStructuredErrors()
    {
        LoadFixture();
        using var result = JsonDocument.Parse(await AssetBatchTools.BatchValidateAssetsAsync([AssetPath, Path.Combine(directory, "Missing.uasset")]));
        Assert.True(result.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(1, result.RootElement.GetProperty("data").GetProperty("succeededCount").GetInt32());
        Assert.Equal(1, result.RootElement.GetProperty("data").GetProperty("failedCount").GetInt32());
        using var invalid = JsonDocument.Parse(await AssetBatchTools.PatchAssetDocumentBatchAsync(AssetPath, []));
        Assert.False(invalid.RootElement.GetProperty("success").GetBoolean());
    }

    public void Dispose()
    {
        AssetSessionService.ClearAllSessions(discardChanges: true);
        EngineVersionService.ClearVersion();
        Directory.Delete(directory, recursive: true);
    }
}
