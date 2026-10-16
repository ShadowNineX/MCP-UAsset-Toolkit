using System.Text.Json;
using MCP_UAsset_Toolkit.Source.Models;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using Xunit;

namespace MCP.UAssetToolkit.Tests;

[Collection("Asset sessions")]
public sealed class DataTableTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "UAssetToolkitDataTableTests", Guid.NewGuid().ToString("N"));
    private string AssetPath => Path.Combine(directory, "DataTable.uasset");

    public DataTableTests()
    {
        Directory.CreateDirectory(directory);
        AssetSessionService.ClearAllSessions(discardChanges: true);
        EngineVersionService.ClearVersion();
    }

    private AssetSession LoadFixture()
    {
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "DataTable.uasset"), AssetPath);
        return AssetSessionService.GetOrLoadSession(AssetPath, EngineVersion.VER_UE4_18);
    }

    private static DataTableExport Table(AssetSession session) => Assert.IsType<DataTableExport>(session.Asset.Exports[0]);
    private static StructPropertyData FirstRow(AssetSession session)
    {
        var rows = Table(session).Table.Data;
        Assert.NotEmpty(rows);
        return rows[0];
    }
    private static string RowName(StructPropertyData row)
    {
        Assert.NotNull(row.Name);
        return row.Name.ToString();
    }
    private static BoolPropertyData FirstBoolean(StructPropertyData row) => row.Value!.OfType<BoolPropertyData>().First();

    [Fact]
    public async Task ListAndReadRowsFilterPageAndReturnBoundedTypedJson()
    {
        var session = LoadFixture();
        var firstRow = FirstRow(session);
        var name = RowName(firstRow);

        var listed = DataTableService.ListRows(AssetPath, 1, name[..Math.Min(4, name.Length)], 0, 1);
        using var listJson = JsonDocument.Parse(await DataTableTools.ListDataTableRowsAsync(AssetPath, 1, name[..Math.Min(4, name.Length)], 0, 1));
        using var readJson = JsonDocument.Parse(await DataTableTools.GetDataTableRowAsync(AssetPath, 1, name, DataTableService.MaximumMaxJsonCharacters));

        Assert.NotNull(listed);
        Assert.True(listJson.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(1, listJson.RootElement.GetProperty("data").GetProperty("count").GetInt32());
        Assert.True(readJson.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("$type", readJson.RootElement.GetProperty("data").GetProperty("rawJson").GetString());
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void CopyRowDeepClonesSourceWithoutSharingProperties()
    {
        var session = LoadFixture();
        var source = FirstRow(session);
        var sourceName = RowName(source);
        var copyName = sourceName + "_Copy";

        DataTableService.CopyRow(AssetPath, 1, sourceName, copyName);

        var currentSource = Assert.Single(Table(session).Table.Data, row => RowName(row) == sourceName);
        var copied = Assert.Single(Table(session).Table.Data, row => RowName(row) == copyName);
        var sourceBoolean = FirstBoolean(currentSource);
        var copiedBoolean = FirstBoolean(copied);
        var originalValue = sourceBoolean.Value;
        copiedBoolean.Value = !copiedBoolean.Value;

        Assert.NotSame(currentSource, copied);
        Assert.NotSame(currentSource.Value, copied.Value);
        Assert.NotSame(sourceBoolean, copiedBoolean);
        Assert.Equal(originalValue, sourceBoolean.Value);
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void UpdateAndRemoveRowsPersistAfterSaveAndReload()
    {
        var session = LoadFixture();
        var edited = FirstRow(session);
        var editedName = RowName(edited);
        var boolean = FirstBoolean(edited);
        Assert.NotNull(boolean.Name);
        var propertyName = boolean.Name.ToString();
        var targetValue = !boolean.Value;
        var removedName = RowName(Table(session).Table.Data[1]);

        DataTableService.SetRowProperty(AssetPath, 1, editedName, propertyName, targetValue ? "true" : "false");
        DataTableService.RemoveRow(AssetPath, 1, removedName);
        AssetSessionService.SaveSession(AssetPath);
        var reloaded = AssetSessionService.ReloadSession(AssetPath, discardChanges: true);
        var reloadedEdited = Assert.Single(Table(reloaded).Table.Data, row => RowName(row) == editedName);

        Assert.Equal(targetValue, FirstBoolean(reloadedEdited).Value);
        Assert.DoesNotContain(Table(reloaded).Table.Data, row => RowName(row) == removedName);
        Assert.False(reloaded.IsDirty);
    }

    [Fact]
    public void UpdatePreservesEnumAndTextMetadataAfterSaveAndReload()
    {
        var session = LoadFixture();
        var row = FirstRow(session);
        const string enumName = "SyntheticEnum";
        const string baseTextName = "SyntheticBaseText";
        const string rawTextName = "SyntheticRawText";
        const string enumType = "SyntheticEnumType";
        const string enumInnerType = "ByteProperty";
        const string textNamespace = "SyntheticNamespace";
        const string textKey = "SyntheticKey";
        var enumProperty = new EnumPropertyData(FName.FromString(session.Asset, enumName))
        {
            EnumType = FName.FromString(session.Asset, enumType),
            InnerType = FName.FromString(session.Asset, enumInnerType),
            Value = FName.FromString(session.Asset, "SyntheticEnumValue")
        };
        var baseText = new TextPropertyData(FName.FromString(session.Asset, baseTextName))
        {
            Flags = ETextFlag.CultureInvariant | ETextFlag.Immutable,
            HistoryType = TextHistoryType.Base,
            Namespace = FString.FromString(textNamespace),
            Value = FString.FromString(textKey),
            CultureInvariantString = FString.FromString("Original base text")
        };
        var rawText = new TextPropertyData(FName.FromString(session.Asset, rawTextName))
        {
            Flags = ETextFlag.Transient,
            HistoryType = TextHistoryType.RawText,
            Value = FString.FromString("Original raw text")
        };
        row.Value!.Add(enumProperty);
        row.Value.Add(baseText);
        row.Value.Add(rawText);

        DataTableService.SetRowProperty(AssetPath, 1, RowName(row), enumName, "\"SyntheticEnumValue_2\"");
        DataTableService.SetRowProperty(AssetPath, 1, RowName(row), baseTextName, "\"Updated base text\"");
        DataTableService.SetRowProperty(AssetPath, 1, RowName(row), rawTextName, "\"Updated raw text\"");
        var updatedRow = Assert.Single(Table(session).Table.Data, candidate => RowName(candidate) == RowName(row));
        var updatedEnum = Assert.IsType<EnumPropertyData>(Assert.Single(updatedRow.Value!, property => property.Name?.ToString() == enumName));
        Assert.Equal(enumInnerType, updatedEnum.InnerType.ToString());
        AssetSessionService.SaveSession(AssetPath);
        var reloaded = AssetSessionService.ReloadSession(AssetPath, discardChanges: true);
        var reloadedRow = Assert.Single(Table(reloaded).Table.Data, candidate => RowName(candidate) == RowName(row));
        var reloadedEnum = Assert.IsType<EnumPropertyData>(Assert.Single(reloadedRow.Value!, property => property.Name?.ToString() == enumName));
        var reloadedBaseText = Assert.IsType<TextPropertyData>(Assert.Single(reloadedRow.Value!, property => property.Name?.ToString() == baseTextName));
        var reloadedRawText = Assert.IsType<TextPropertyData>(Assert.Single(reloadedRow.Value!, property => property.Name?.ToString() == rawTextName));

        Assert.Equal(enumType, reloadedEnum.EnumType.ToString());
        // InnerType is mapping context for unversioned properties, not a field in this versioned package.
        Assert.Null(reloadedEnum.InnerType);
        Assert.Equal("SyntheticEnumValue_2", reloadedEnum.Value.ToString());
        Assert.Equal(TextHistoryType.Base, reloadedBaseText.HistoryType);
        Assert.Equal(ETextFlag.CultureInvariant | ETextFlag.Immutable, reloadedBaseText.Flags);
        Assert.Equal(textNamespace, reloadedBaseText.Namespace.ToString());
        Assert.Equal(textKey, reloadedBaseText.Value.ToString());
        Assert.Equal("Updated base text", reloadedBaseText.CultureInvariantString.ToString());
        Assert.Equal(TextHistoryType.RawText, reloadedRawText.HistoryType);
        Assert.Equal(ETextFlag.Transient, reloadedRawText.Flags);
        Assert.Equal("Updated raw text", reloadedRawText.Value.ToString());
    }

    [Fact]
    public async Task RejectedRequestsKeepOriginalSessionsAndDirtyState()
    {
        var session = LoadFixture();
        var originalAsset = session.Asset;
        var rowName = RowName(FirstRow(session));

        var secondRowName = RowName(Table(session).Table.Data[1]);
        Assert.Throws<InvalidOperationException>(() => DataTableService.CopyRow(AssetPath, 1, secondRowName, rowName));
        Assert.Throws<KeyNotFoundException>(() => DataTableService.RemoveRow(AssetPath, 1, "MissingRow"));
        Assert.ThrowsAny<JsonException>(() => DataTableService.SetRowProperty(AssetPath, 1, rowName, "MissingProperty", "not json"));
        Assert.Throws<KeyNotFoundException>(() => DataTableService.SetRowProperty(AssetPath, 1, rowName, "MissingProperty", "true"));
        Assert.Throws<ArgumentOutOfRangeException>(() => DataTableService.SetRowProperty(AssetPath, 1, rowName, "MissingProperty", "\"" + new string('x', DataTableService.MaximumMaxJsonCharacters) + "\""));
        Assert.Throws<ArgumentOutOfRangeException>(() => DataTableService.ListRows(AssetPath, 1, offset: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => DataTableService.GetRow(AssetPath, 1, rowName, 0));

        var nonTablePath = Path.Combine(directory, "TestSoundClass.uasset");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestSoundClass.uasset"), nonTablePath);
        var nonTableSession = AssetSessionService.GetOrLoadSession(nonTablePath, EngineVersion.VER_UE4_27);
        using var wrongExport = JsonDocument.Parse(await DataTableTools.GetDataTableRowAsync(nonTablePath, 1, rowName));

        Assert.False(wrongExport.RootElement.GetProperty("success").GetBoolean());
        Assert.Same(originalAsset, session.Asset);
        Assert.False(session.IsDirty);
        Assert.False(nonTableSession.IsDirty);
    }

    public void Dispose()
    {
        AssetSessionService.ClearAllSessions(discardChanges: true);
        EngineVersionService.ClearVersion();
        Directory.Delete(directory, recursive: true);
    }
}
