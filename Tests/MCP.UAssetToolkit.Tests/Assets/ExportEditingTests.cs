using MCP_UAsset_Toolkit.Source.Models;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using System.Text.Json;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using Xunit;

namespace MCP.UAssetToolkit.Tests;

public class ExportEditingTests
{
    private static UAsset LoadFixture() => new(Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestSoundClass.uasset"), EngineVersion.VER_UE4_27);

    [Fact]
    public void UpdateCanClearFlagsWithoutChangingOmittedFields()
    {
        var asset = LoadFixture();
        var export = asset.Exports[0];
        var originalName = export.ObjectName.ToString();
        export.bIsAsset = true;
        var changes = ExportEditingService.Update(asset, 1, new ExportInfoUpdate { BIsAsset = false, ObjectFlags = "RF_Public" });

        Assert.False(export.bIsAsset);
        Assert.Equal(EObjectFlags.RF_Public, export.ObjectFlags);
        Assert.Equal(originalName, export.ObjectName.ToString());
        Assert.Equal(2, changes.Count);
    }

    [Fact]
    public void InvalidReferenceDoesNotPartiallyRenameExport()
    {
        var asset = LoadFixture();
        var originalName = asset.Exports[0].ObjectName.ToString();
        var originalNameCount = asset.GetNameMapIndexList().Count;

        Assert.Throws<ArgumentOutOfRangeException>(() => ExportEditingService.Update(asset, 1,
            new ExportInfoUpdate { ObjectName = "MustNotBeAdded", ClassIndex = int.MaxValue }));
        Assert.Equal(originalName, asset.Exports[0].ObjectName.ToString());
        Assert.Equal(originalNameCount, asset.GetNameMapIndexList().Count);
    }

    [Fact]
    public void EmptyUpdateDoesNotReportChanges()
    {
        Assert.Empty(ExportEditingService.Update(LoadFixture(), 1, new ExportInfoUpdate()));
    }

    [Fact]
    public async Task SharedExecutionPreservesSuccessAndFormatsErrors()
    {
        Assert.Equal("response", await ToolHelpers.ExecuteAsync(() => "response"));
        using var result = JsonDocument.Parse(await ToolHelpers.ExecuteAsync(() => throw new InvalidOperationException("Example failure")));
        Assert.False(result.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Example failure", result.RootElement.GetProperty("error").GetString());
        Assert.Equal("InvalidOperationException", result.RootElement.GetProperty("exceptionType").GetString());
    }
}
