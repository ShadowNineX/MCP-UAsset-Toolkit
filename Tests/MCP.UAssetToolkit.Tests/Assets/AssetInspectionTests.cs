using System.Text.Json;
using MCP_UAsset_Toolkit.Source.Models;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;
using Xunit;

namespace MCP.UAssetToolkit.Tests;

[Collection("Asset sessions")]
public class AssetInspectionTests
{
    [Fact]
    public void ValidationDoesNotChangeAssetAndDetectsEdits()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestSoundClass.uasset");
        var asset = new UAsset(path, EngineVersion.VER_UE4_27);
        var session = new AssetSession(path, asset, EngineVersion.VER_UE4_27);
        var before = asset.SerializeJson();
        using var original = JsonDocument.Parse(JsonSerializer.Serialize(AssetInspectionService.Validate(session)));
        Assert.True(original.RootElement.GetProperty("canSerialize").GetBoolean());
        Assert.True(original.RootElement.GetProperty("binaryEqualsDisk").GetBoolean());
        Assert.Equal(before, asset.SerializeJson());

        asset.Info = "Custom info";
        AssetInspectionService.Validate(session);
        Assert.Equal("Custom info", asset.Info);

        asset.FolderName = new FString("Modified");
        using var edited = JsonDocument.Parse(JsonSerializer.Serialize(AssetInspectionService.Validate(session)));
        Assert.False(edited.RootElement.GetProperty("binaryEqualsDisk").GetBoolean());
    }

    [Fact]
    public async Task RawBytesRoundTripThroughToolsAndInvalidBase64DoesNotMutate()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".uasset");
        var asset = new UAsset { Exports = [new RawExport([1, 2, 3], null!, [])] };
        var session = AssetSessionService.RegisterSession(path, asset);
        try
        {
            using var invalid = JsonDocument.Parse(await AssetInspectionTools.SetExportBytesAsync(path, 1, "not base64!"));
            Assert.False(invalid.RootElement.GetProperty("success").GetBoolean());
            Assert.False(session.IsDirty);
            Assert.Equal(new byte[] { 1, 2, 3 }, ((RawExport)asset.Exports[0]).Data);

            using var update = JsonDocument.Parse(await AssetInspectionTools.SetExportBytesAsync(path, 1, "BAUGBw=="));
            Assert.True(update.RootElement.GetProperty("success").GetBoolean());
            using var read = JsonDocument.Parse(await AssetInspectionTools.GetExportBytesAsync(path, 1, offset: 1, limit: 2));
            Assert.Equal("BQY=", read.RootElement.GetProperty("data").GetProperty("base64").GetString());
            Assert.True(read.RootElement.GetProperty("data").GetProperty("hasMore").GetBoolean());
            Assert.True(session.IsDirty);
        }
        finally { AssetSessionService.CloseSession(path, discardChanges: true); }
    }

    [Fact]
    public async Task ScriptReplacementRequiresLogicalSizeAndClearsParsedBytecode()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".uasset");
        var script = new StructExport { ScriptBytecodeRaw = [1], ScriptBytecodeSize = 10, ScriptBytecode = [] };
        var session = AssetSessionService.RegisterSession(path, new UAsset { Exports = [script] });
        try
        {
            using var invalid = JsonDocument.Parse(await AssetInspectionTools.SetExportBytesAsync(path, 1, "AgM=", "script"));
            Assert.False(invalid.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(new byte[] { 1 }, script.ScriptBytecodeRaw);
            Assert.Equal(10, script.ScriptBytecodeSize);
            Assert.NotNull(script.ScriptBytecode);
            Assert.False(session.IsDirty);

            using var valid = JsonDocument.Parse(await AssetInspectionTools.SetExportBytesAsync(path, 1, "AgM=", "script", 42));
            Assert.True(valid.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(new byte[] { 2, 3 }, script.ScriptBytecodeRaw);
            Assert.Equal(42, script.ScriptBytecodeSize);
            Assert.Null(script.ScriptBytecode);
            Assert.True(session.IsDirty);
        }
        finally { AssetSessionService.CloseSession(path, discardChanges: true); }
    }

    [Fact]
    public async Task OpenOptionsAndReloadPreserveStructOverrides()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestSoundClass.uasset");
        try
        {
            using var response = JsonDocument.Parse(await SessionTools.OpenAssetAsync(path, new AssetLoadOptions
            {
                EngineVersion = "4.27",
                MapStructTypes = new() { ["ExampleMap"] = ["Vector", "Guid"] },
                ArrayStructTypes = new() { ["ExampleArray"] = "Vector" }
            }));
            Assert.True(response.RootElement.GetProperty("success").GetBoolean());
            var reloaded = AssetSessionService.ReloadSession(path);
            Assert.Equal("Vector", reloaded.Asset.MapStructTypeOverride["ExampleMap"].Item1.Value);
            Assert.Equal("Vector", reloaded.Asset.ArrayStructTypeOverride["ExampleArray"].Value);
        }
        finally { AssetSessionService.CloseSession(path, discardChanges: true); }
    }

    [Fact]
    public async Task MappingSearchInspectExportAndPatchPreserveSource()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Test.usmap");
        var directory = Path.Combine(Path.GetTempPath(), "UAssetMappingTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var before = File.ReadAllBytes(source);
            using var list = JsonDocument.Parse(await MappingInspectionTools.ListMappingSchemasAsync(mappingsPath: source, limit: 1));
            Assert.True(list.RootElement.GetProperty("success").GetBoolean());
            var name = list.RootElement.GetProperty("data").GetProperty("schemas")[0].GetString()!;
            using var schema = JsonDocument.Parse(await MappingInspectionTools.GetMappingSchemaAsync(name, source));
            Assert.True(schema.RootElement.GetProperty("success").GetBoolean());

            var jmap = Path.Combine(directory, "export.json");
            using var export = JsonDocument.Parse(await MappingInspectionTools.ExportMappingsJsonAsync(jmap, source));
            Assert.True(export.RootElement.GetProperty("success").GetBoolean(), export.RootElement.ToString());
            using var exportedJson = JsonDocument.Parse(File.ReadAllText(jmap));
            Assert.True(exportedJson.RootElement.GetProperty("Schemas").EnumerateObject().Any());
            using var second = JsonDocument.Parse(await MappingInspectionTools.ExportMappingsJsonAsync(jmap, source));
            Assert.False(second.RootElement.GetProperty("success").GetBoolean());

            var target = Path.Combine(directory, "patched.usmap");
            using var patch = JsonDocument.Parse(await MappingInspectionTools.PatchMappingsVersionAsync(source, target, "5.4"));
            Assert.True(patch.RootElement.GetProperty("success").GetBoolean());
            Assert.True(new UAssetAPI.Unversioned.Usmap(target).Schemas.Count > 0);
            Assert.Equal(before, File.ReadAllBytes(source));
        }
        finally
        {
            MappingService.ClearMappings();
            Directory.Delete(directory, recursive: true);
        }
    }
}
