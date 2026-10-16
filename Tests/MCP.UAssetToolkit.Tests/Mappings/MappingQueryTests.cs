using System.Text.Json;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools;
using Xunit;
using UAssetAPI.Unversioned;

namespace MCP.UAssetToolkit.Tests;

[Collection("Asset sessions")]
public sealed class MappingQueryTests : IDisposable
{
    private const string SuccessProperty = "success";
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Test.usmap");

    public void Dispose() => MappingService.ClearMappings();

    [Fact]
    public async Task ListMappingEnumsAsync_SearchesRepresentativeFixtureEnumWithPagination()
    {
        var all = MappingQueryService.ListEnums(mappingsPath: FixturePath, limit: 1000);
        var name = Assert.Single(all.Enums.Take(1));
        using var response = JsonDocument.Parse(await MappingQueryTools.ListMappingEnumsAsync(name, FixturePath, offset: 0, limit: 1));
        var data = response.RootElement.GetProperty("data");

        Assert.True(response.RootElement.GetProperty(SuccessProperty).GetBoolean());
        Assert.Equal(1, data.GetProperty("count").GetInt32());
        Assert.Contains(data.GetProperty("enums").EnumerateArray().Select(value => value.GetString()), value => value == name);
        Assert.True(data.GetProperty("totalCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task GetMappingEnumAsync_ReturnsDetailsAndPaginatesValues()
    {
        var enumName = Assert.Single(MappingQueryService.ListEnums(mappingsPath: FixturePath, limit: 1).Enums);
        var details = MappingQueryService.GetEnum(enumName, FixturePath, limit: 1000);
        using var response = JsonDocument.Parse(await MappingQueryTools.GetMappingEnumAsync(enumName, FixturePath, offset: 0, limit: 1));
        var data = response.RootElement.GetProperty("data");

        Assert.True(response.RootElement.GetProperty(SuccessProperty).GetBoolean());
        Assert.Equal(enumName, data.GetProperty("name").GetString());
        Assert.Equal(Math.Min(1, details.TotalCount), data.GetProperty("count").GetInt32());
        Assert.Equal(details.TotalCount, data.GetProperty("totalCount").GetInt32());
        Assert.Equal(details.TotalCount > 1, data.GetProperty("hasMore").GetBoolean());
    }

    [Fact]
    public async Task MappingEnumValuesAndBoundariesMatchThePinnedFixture()
    {
        var fixture = new Usmap(FixturePath);
        var entry = fixture.EnumMap.OrderBy(pair => pair.Key, StringComparer.Ordinal).First(pair => pair.Value.Values.Count >= 3);
        var expected = entry.Value.Values.OrderBy(pair => pair.Key).Skip(1).First();
        using var response = JsonDocument.Parse(await MappingQueryTools.GetMappingEnumAsync(entry.Key, FixturePath, offset: 1, limit: 1));
        var data = response.RootElement.GetProperty("data");
        var value = data.GetProperty("values")[0];
        Assert.Equal(expected.Key, value.GetProperty("value").GetInt64());
        Assert.Equal(expected.Value, value.GetProperty("name").GetString());
        Assert.Equal(entry.Value.Values.Count, data.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, data.GetProperty("offset").GetInt32());
        var end = MappingQueryService.GetEnum(entry.Key, FixturePath, offset: entry.Value.Values.Count);
        Assert.Empty(end.Values);
        Assert.False(end.HasMore);
        Assert.Throws<ArgumentOutOfRangeException>(() => MappingQueryService.GetEnum(entry.Key, FixturePath, offset: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MappingQueryService.GetEnum(entry.Key, FixturePath, limit: 1001));
    }

    [Fact]
    public async Task MappingQueryTools_ReportMissingEnumsAndNoGlobalMappings()
    {
        using var missing = JsonDocument.Parse(await MappingQueryTools.GetMappingEnumAsync("DefinitelyMissing", FixturePath));
        Assert.False(missing.RootElement.GetProperty(SuccessProperty).GetBoolean());

        MappingService.ClearMappings();
        using var noGlobal = JsonDocument.Parse(await MappingQueryTools.ListMappingEnumsAsync());
        Assert.False(noGlobal.RootElement.GetProperty(SuccessProperty).GetBoolean());
    }

    [Fact]
    public async Task GetMappingDiagnosticsAsync_IsDeterministicForFixtureAndNoGlobalState()
    {
        using var fromFixture = JsonDocument.Parse(await MappingQueryTools.GetMappingDiagnosticsAsync(FixturePath));
        var fixture = fromFixture.RootElement.GetProperty("data");
        Assert.True(fixture.GetProperty("isLoaded").GetBoolean());
        Assert.True(fixture.GetProperty("schemaCount").GetInt32() > 0);
        Assert.True(fixture.GetProperty("enumCount").GetInt32() > 0);
        Assert.True(fixture.TryGetProperty("version", out _));
        Assert.True(fixture.TryGetProperty("netCL", out _));
        Assert.True(fixture.TryGetProperty("failedExtensions", out _));
        Assert.True(fixture.TryGetProperty("areFNamesCaseInsensitive", out _));
        Assert.True(fixture.TryGetProperty("moduleCount", out _));
        var source = new Usmap(FixturePath);
        Assert.Equal(source.Version.ToString(), fixture.GetProperty("version").GetString());
        Assert.Equal(source.FileVersionUE4.ToString(), fixture.GetProperty("fileVersionUE4").GetString());
        Assert.Equal(source.FileVersionUE5.ToString(), fixture.GetProperty("fileVersionUE5").GetString());
        Assert.Equal(source.NetCL, fixture.GetProperty("netCL").GetUInt32());
        Assert.Equal(source.AreFNamesCaseInsensitive, fixture.GetProperty("areFNamesCaseInsensitive").GetBoolean());
        Assert.Equal(source.Schemas.Count, fixture.GetProperty("schemaCount").GetInt32());
        Assert.Equal(source.EnumMap.Count, fixture.GetProperty("enumCount").GetInt32());
        var expectedModules = new HashSet<string>(StringComparer.Ordinal);
        expectedModules.UnionWith(source.Schemas.Values.Where(schema => !string.IsNullOrWhiteSpace(schema.ModulePath)).Select(schema => schema.ModulePath));
        expectedModules.UnionWith(source.EnumMap.Values.Where(enumeration => !string.IsNullOrWhiteSpace(enumeration.ModulePath)).Select(enumeration => enumeration.ModulePath));
        Assert.Equal(expectedModules.Count, fixture.GetProperty("moduleCount").GetInt32());

        MappingService.ClearMappings();
        using var noGlobal = JsonDocument.Parse(await MappingQueryTools.GetMappingDiagnosticsAsync());
        Assert.True(noGlobal.RootElement.GetProperty(SuccessProperty).GetBoolean());
        Assert.False(noGlobal.RootElement.GetProperty("data").GetProperty("isLoaded").GetBoolean());
    }
}
