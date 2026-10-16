using System.Text.Json;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using Xunit;

namespace MCP.UAssetToolkit.Tests;

[Collection("Asset sessions")]
public sealed class AssetSearchTests
{
    private const string AlphaQuery = "alpha";
    [Fact]
    public void Search_PaginatesMatchedCandidatesAndReportsMetadata()
    {
        var result = AssetSearch.Search(
            new[] { AlphaQuery, "beta", "alphabet", "gamma" },
            AlphaQuery,
            static value => new[] { value },
            offset: 1,
            limit: 1);

        Assert.Equal(new[] { "alphabet" }, result.Matches);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Offset);
        Assert.Equal(1, result.Limit);
        Assert.False(result.HasMore);
    }

    [Fact]
    public void Search_AtEndOfResults_ReturnsEmptyPage()
    {
        var result = AssetSearch.Search(
            new[] { "Alpha", "ALPHA" },
            AlphaQuery,
            static value => new[] { value },
            offset: 2,
            limit: 100);

        Assert.Empty(result.Matches);
        Assert.Equal(2, result.TotalCount);
        Assert.False(result.HasMore);
    }

    [Fact]
    public void Search_IsCaseInsensitiveAndSupportsRegularExpressions()
    {
        var result = AssetSearch.Search(
            new[] { "alpha1", "ALPHA2", "alphaX", "beta3" },
            "^alpha\\d$",
            static value => new[] { value },
            useRegex: true);

        Assert.Equal(new[] { "alpha1", "ALPHA2" }, result.Matches);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(0, 1001)]
    public void Search_RejectsInvalidPagination(int offset, int limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AssetSearch.Search(
            new[] { AlphaQuery },
            AlphaQuery,
            static value => new[] { value },
            offset,
            limit));
    }

    [Fact]
    public void Search_RejectsInvalidRegularExpressions()
    {
        Assert.ThrowsAny<ArgumentException>(() => AssetSearch.Search(
            new[] { AlphaQuery },
            "[",
            static value => new[] { value },
            useRegex: true));
    }

    [Fact]
    public async Task SearchNameMapAsync_PaginatesAndPreservesMatchesResponseKey()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"asset-search-{Guid.NewGuid():N}.uasset");
        await File.WriteAllBytesAsync(filePath, [], TestContext.Current.CancellationToken);

        try
        {
            var asset = new UAsset();
            asset.ClearNameIndexList();
            asset.AddNameReference(new FString("NeedleOne"));
            asset.AddNameReference(new FString("NeedleTwo"));
            asset.AddNameReference(new FString("Other"));
            AssetSessionService.RegisterSession(filePath, asset);

            var response = await AssetDataTools.SearchNameMapAsync(filePath, "needle", offset: 1, limit: 1);
            using var document = JsonDocument.Parse(response);
            var data = document.RootElement.GetProperty("data");

            Assert.True(document.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(1, data.GetProperty("count").GetInt32());
            Assert.Equal(2, data.GetProperty("totalCount").GetInt32());
            Assert.Equal(1, data.GetProperty("offset").GetInt32());
            Assert.Equal(1, data.GetProperty("limit").GetInt32());
            Assert.False(data.GetProperty("hasMore").GetBoolean());
            Assert.Equal(1, data.GetProperty("matches").GetArrayLength());
            Assert.Equal(1, data.GetProperty("matches")[0].GetProperty("index").GetInt32());
        }
        finally
        {
            AssetSessionService.CloseSession(filePath);
            File.Delete(filePath);
        }
    }
}
