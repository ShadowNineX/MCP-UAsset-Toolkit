using System.Text.Json;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools;
using Xunit;

namespace MCP.UAssetToolkit.Tests;

[Collection("Asset sessions")]
public sealed class ArchiveQueryTests : IDisposable
{
    private const string ObjectsProperty = "objects";
    private const string ReadShaderMapsProperty = "readShaderMaps";
    private const string ReadScriptDataProperty = "readScriptData";
    private const string ReadNaniteDataProperty = "readNaniteData";
    private const string SkipReferencedTexturesProperty = "skipReferencedTextures";
    private readonly string directory = Path.Combine(Path.GetTempPath(), "UAssetToolkitArchiveQueryTests", Guid.NewGuid().ToString("N"));
    private readonly List<string> sessions = [];

    public ArchiveQueryTests()
    {
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestSoundClass.uasset"), Path.Combine(directory, "TestSoundClass.uasset"));
    }

    [Fact]
    public void ReadObject_UsesExactNamedObjectAndRejectsMissingNames()
    {
        var session = Open();
        var virtualPath = PackagePath(session.Id);
        using var list = JsonDocument.Parse(JsonSerializer.Serialize(ArchiveQueryService.ListObjects(session.Id, virtualPath)));
        var objectName = list.RootElement.GetProperty(ObjectsProperty)[0].GetProperty("name").GetString()!;

        var read = ArchiveQueryService.ReadObject(session.Id, virtualPath, objectName);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(read));
        Assert.Equal(objectName, json.RootElement.GetProperty("objectName").GetString());
        Assert.Equal(objectName, json.RootElement.GetProperty("name").GetString());
        Assert.True(json.RootElement.GetProperty("found").GetBoolean());
        Assert.False(json.RootElement.GetProperty("truncated").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("json").GetString()));

        Assert.Throws<KeyNotFoundException>(() => ArchiveQueryService.ReadObject(session.Id, virtualPath, "DefinitelyMissingObject"));
    }

    [Fact]
    public void ListObjects_PaginatesAndEnforcesCaps()
    {
        var session = Open();
        var virtualPath = PackagePath(session.Id);
        using var firstPage = JsonDocument.Parse(JsonSerializer.Serialize(ArchiveQueryService.ListObjects(session.Id, virtualPath, offset: 0, limit: 1)));

        Assert.Equal(1, firstPage.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(1, firstPage.RootElement.GetProperty(ObjectsProperty).GetArrayLength());
        Assert.True(firstPage.RootElement.GetProperty("totalCount").GetInt32() >= 1);
        Assert.True(firstPage.RootElement.GetProperty(ObjectsProperty)[0].TryGetProperty("name", out _));
        Assert.True(firstPage.RootElement.GetProperty(ObjectsProperty)[0].TryGetProperty("className", out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArchiveQueryService.ListObjects(session.Id, virtualPath, limit: 1001));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArchiveQueryService.ListObjects(session.Id, virtualPath, limit: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArchiveQueryService.ListObjects(session.Id, virtualPath, offset: -1));

        var objectName = firstPage.RootElement.GetProperty(ObjectsProperty)[0].GetProperty("name").GetString()!;
        using var bounded = JsonDocument.Parse(JsonSerializer.Serialize(ArchiveQueryService.ReadObject(session.Id, virtualPath, objectName, maxJsonCharacters: 1)));
        Assert.True(bounded.RootElement.GetProperty("truncated").GetBoolean());
        Assert.False(bounded.RootElement.TryGetProperty("json", out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArchiveQueryService.ReadObject(session.Id, virtualPath, objectName, maxJsonCharacters: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArchiveQueryService.ReadObject(session.Id, virtualPath, objectName, maxJsonCharacters: 10_000_001));
        using var maximum = JsonDocument.Parse(JsonSerializer.Serialize(ArchiveQueryService.ReadObject(session.Id, virtualPath, objectName, maxJsonCharacters: 10_000_000)));
        Assert.False(maximum.RootElement.GetProperty("truncated").GetBoolean());
        Assert.Equal(10_000_000, maximum.RootElement.GetProperty("maxJsonCharacters").GetInt32());
    }

    [Fact]
    public void FileInfo_ReturnsMetadataAndPackageQueriesRejectArbitraryFiles()
    {
        var arbitraryPath = Path.Combine(directory, "Payload.bin");
        File.WriteAllBytes(arbitraryPath, [1, 2, 3]);
        var session = Open();
        var virtualPath = session.Provider.Files.Keys.Single(path => path.EndsWith("Payload.bin", StringComparison.OrdinalIgnoreCase));

        using var info = JsonDocument.Parse(JsonSerializer.Serialize(ArchiveQueryService.GetFileInfo(session.Id, virtualPath)));
        Assert.Equal(virtualPath, info.RootElement.GetProperty("virtualPath").GetString());
        Assert.Equal(3, info.RootElement.GetProperty("size").GetInt64());
        Assert.Equal("bin", info.RootElement.GetProperty("extension").GetString());
        Assert.False(info.RootElement.GetProperty("isPackage").GetBoolean());
        Assert.True(info.RootElement.TryGetProperty("compressionMethod", out _));
        var container = info.RootElement.GetProperty("container");
        Assert.True(container.ValueKind is JsonValueKind.Null or JsonValueKind.Object);
        if (container.ValueKind == JsonValueKind.Object) Assert.True(container.TryGetProperty("path", out _));

        Assert.Throws<InvalidOperationException>(() => ArchiveQueryService.ListObjects(session.Id, virtualPath));
        Assert.Throws<InvalidOperationException>(() => ArchiveQueryService.ReadObject(session.Id, virtualPath, "Anything"));
    }

    [Fact]
    public void ReadOptions_PreserveUnsetDefaultsAndDoNotLeakAcrossSessions()
    {
        var first = Open();
        var second = Open();
        using var before = JsonDocument.Parse(JsonSerializer.Serialize(ArchiveQueryService.GetReadOptions(first.Id)));
        var originalShaderMaps = before.RootElement.GetProperty(ReadShaderMapsProperty).GetBoolean();
        using var secondDefaults = JsonDocument.Parse(JsonSerializer.Serialize(ArchiveQueryService.GetReadOptions(second.Id)));

        ArchiveQueryService.ConfigureReadOptions(first.Id, readShaderMaps: !originalShaderMaps);
        using var configured = JsonDocument.Parse(JsonSerializer.Serialize(ArchiveQueryService.GetReadOptions(first.Id)));
        Assert.Equal(!originalShaderMaps, configured.RootElement.GetProperty(ReadShaderMapsProperty).GetBoolean());
        Assert.Equal(before.RootElement.GetProperty(ReadScriptDataProperty).GetBoolean(), configured.RootElement.GetProperty(ReadScriptDataProperty).GetBoolean());
        using var secondCurrent = JsonDocument.Parse(JsonSerializer.Serialize(ArchiveQueryService.GetReadOptions(second.Id)));
        Assert.Equal(secondDefaults.RootElement.GetProperty(ReadShaderMapsProperty).GetBoolean(), secondCurrent.RootElement.GetProperty(ReadShaderMapsProperty).GetBoolean());
        Assert.Equal(secondDefaults.RootElement.GetProperty(ReadScriptDataProperty).GetBoolean(), secondCurrent.RootElement.GetProperty(ReadScriptDataProperty).GetBoolean());
        Assert.Equal(secondDefaults.RootElement.GetProperty(ReadNaniteDataProperty).GetBoolean(), secondCurrent.RootElement.GetProperty(ReadNaniteDataProperty).GetBoolean());
        Assert.Equal(secondDefaults.RootElement.GetProperty(SkipReferencedTexturesProperty).GetBoolean(), secondCurrent.RootElement.GetProperty(SkipReferencedTexturesProperty).GetBoolean());
        Assert.Equal(secondDefaults.RootElement.GetProperty("useLazyPackageSerialization").GetBoolean(), secondCurrent.RootElement.GetProperty("useLazyPackageSerialization").GetBoolean());

        ArchiveQueryService.ConfigureReadOptions(first.Id);
        using var unchanged = JsonDocument.Parse(JsonSerializer.Serialize(ArchiveQueryService.GetReadOptions(first.Id)));
        Assert.Equal(!originalShaderMaps, unchanged.RootElement.GetProperty(ReadShaderMapsProperty).GetBoolean());
        Assert.Throws<KeyNotFoundException>(() => ArchiveQueryService.GetReadOptions("missing"));
    }

    [Fact]
    public void ConfigureReadOptions_ExposesNullableBooleanInputs()
    {
        var parameters = typeof(ArchiveQueryTools)
            .GetMethod(nameof(ArchiveQueryTools.ConfigureArchiveReadOptionsAsync))!
            .GetParameters();

        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.All(parameters.Skip(1), parameter => Assert.Equal(typeof(bool?), parameter.ParameterType));
    }

    [Theory]
    [InlineData(ReadScriptDataProperty)]
    [InlineData(ReadNaniteDataProperty)]
    [InlineData(SkipReferencedTexturesProperty)]
    [InlineData("useLazyPackageSerialization")]
    public void EachAdditionalReadOptionChangesOnlyItsSelectedProvider(string property)
    {
        var first = Open();
        var second = Open();
        using var before = JsonDocument.Parse(JsonSerializer.Serialize(ArchiveQueryService.GetReadOptions(first.Id)));
        using var secondBefore = JsonDocument.Parse(JsonSerializer.Serialize(ArchiveQueryService.GetReadOptions(second.Id)));
        var value = !before.RootElement.GetProperty(property).GetBoolean();
        _ = property switch
        {
            ReadScriptDataProperty => ArchiveQueryService.ConfigureReadOptions(first.Id, readScriptData: value),
            ReadNaniteDataProperty => ArchiveQueryService.ConfigureReadOptions(first.Id, readNaniteData: value),
            SkipReferencedTexturesProperty => ArchiveQueryService.ConfigureReadOptions(first.Id, skipReferencedTextures: value),
            _ => ArchiveQueryService.ConfigureReadOptions(first.Id, useLazyPackageSerialization: value)
        };
        using var after = JsonDocument.Parse(JsonSerializer.Serialize(ArchiveQueryService.GetReadOptions(first.Id)));
        using var secondAfter = JsonDocument.Parse(JsonSerializer.Serialize(ArchiveQueryService.GetReadOptions(second.Id)));
        foreach (var option in new[] { ReadScriptDataProperty, ReadShaderMapsProperty, ReadNaniteDataProperty, SkipReferencedTexturesProperty, "useLazyPackageSerialization" })
        {
            Assert.Equal(option == property ? value : before.RootElement.GetProperty(option).GetBoolean(), after.RootElement.GetProperty(option).GetBoolean());
            Assert.Equal(secondBefore.RootElement.GetProperty(option).GetBoolean(), secondAfter.RootElement.GetProperty(option).GetBoolean());
        }
    }

    private MCP_UAsset_Toolkit.Source.Models.ArchiveSession Open()
    {
        var session = ArchiveService.Open(directory, "GAME_UE4_27");
        sessions.Add(session.Id);
        return session;
    }

    private static string PackagePath(string sessionId) => ArchiveService.Get(sessionId).Provider.Files.Keys.Single(path => path.EndsWith("TestSoundClass.uasset", StringComparison.OrdinalIgnoreCase));

    public void Dispose()
    {
        foreach (var id in sessions) ArchiveService.Close(id);
        Directory.Delete(directory, recursive: true);
    }
}
