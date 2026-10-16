using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace MCP_UAsset_Toolkit.Source.Services;

/// <summary>Provides bounded, atomic named-row operations for DataTable exports.</summary>
public static class DataTableService
{
    public const int DefaultLimit = 100;
    public const int MaximumLimit = 1_000;
    public const int DefaultMaxJsonCharacters = 65_536;
    public const int MaximumMaxJsonCharacters = 1_000_000;

    public static object ListRows(string filePath, int exportIndex, string query = "", int offset = 0, int limit = DefaultLimit)
    {
        ValidatePage(offset, limit);
        var session = AssetSessionService.GetOrLoadSession(filePath);
        var table = GetDataTable(session.Asset, exportIndex);
        var rows = table.Table?.Data ?? throw new InvalidOperationException($"DataTable export {exportIndex} has no row data.");
        var matches = rows.Where(row => RowName(row).Contains(query ?? string.Empty, StringComparison.OrdinalIgnoreCase)).ToArray();
        var page = matches.Skip(offset).Take(limit).Select((row, pageIndex) => new
        {
            index = offset + pageIndex,
            name = RowName(row),
            structType = row.StructType?.ToString(),
            propertyCount = row.Value?.Count ?? 0
        }).ToArray();
        return new
        {
            filePath = session.FilePath,
            exportIndex,
            count = page.Length,
            totalCount = matches.Length,
            offset,
            limit,
            hasMore = offset + page.Length < matches.Length,
            rows = page
        };
    }

    public static object GetRow(string filePath, int exportIndex, string rowName, int maxJsonCharacters = DefaultMaxJsonCharacters)
    {
        ValidateRowName(rowName, nameof(rowName));
        ValidateMaxJsonCharacters(maxJsonCharacters);
        var session = AssetSessionService.GetOrLoadSession(filePath);
        var table = GetDataTable(session.Asset, exportIndex);
        var row = FindRow(table, rowName) ?? throw new KeyNotFoundException($"DataTable row '{rowName}' was not found.");
        var rawJson = session.Asset.SerializeJsonObject(row, isFormatted: true);
        if (rawJson.Length > maxJsonCharacters)
        {
            throw new InvalidOperationException($"DataTable row '{rowName}' is {rawJson.Length} characters, exceeding maxJsonCharacters {maxJsonCharacters}.");
        }
        return new
        {
            filePath = session.FilePath,
            exportIndex,
            name = RowName(row),
            structType = row.StructType?.ToString(),
            jsonCharacters = rawJson.Length,
            rawJson,
            values = DescribeProperties(row.Value)
        };
    }

    public static object CopyRow(string filePath, int exportIndex, string sourceRowName, string newRowName)
    {
        ValidateRowName(sourceRowName, nameof(sourceRowName));
        ValidateRowName(newRowName, nameof(newRowName));
        if (string.Equals(sourceRowName, newRowName, StringComparison.Ordinal))
            throw new ArgumentException("newRowName must differ from sourceRowName.", nameof(newRowName));

        var session = AssetSessionService.TransformSessionAsset(filePath, original =>
        {
            var candidate = UAssetContext.Clone(original);
            var table = GetDataTable(candidate, exportIndex);
            var source = FindRow(table, sourceRowName) ?? throw new KeyNotFoundException($"DataTable row '{sourceRowName}' was not found.");
            if (FindRow(table, newRowName) != null) throw new InvalidOperationException($"DataTable row '{newRowName}' already exists.");
            var clone = (StructPropertyData)source.Clone();
            clone.Name = FName.FromString(candidate, newRowName);
            table.Table.Data.Add(clone);
            return UAssetContext.PrepareForWrite(candidate);
        });
        return new { filePath = session.FilePath, exportIndex, sourceRowName, newRowName, isDirty = session.IsDirty };
    }

    public static object SetRowProperty(string filePath, int exportIndex, string rowName, string propertyName, string valueJson)
    {
        ValidateRowName(rowName, nameof(rowName));
        ValidateRowName(propertyName, nameof(propertyName));
        if (string.IsNullOrWhiteSpace(valueJson)) throw new ArgumentException("valueJson cannot be empty.", nameof(valueJson));
        if (valueJson.Length > MaximumMaxJsonCharacters)
            throw new ArgumentOutOfRangeException(nameof(valueJson), $"valueJson must be at most {MaximumMaxJsonCharacters} characters.");

        using var document = System.Text.Json.JsonDocument.Parse(valueJson);
        var requestedValue = document.RootElement.Clone();
        string? propertyType = null;
        object? newValue = null;
        var session = AssetSessionService.TransformSessionAsset(filePath, original =>
        {
            var candidate = UAssetContext.Clone(original);
            var table = GetDataTable(candidate, exportIndex);
            var row = FindRow(table, rowName) ?? throw new KeyNotFoundException($"DataTable row '{rowName}' was not found.");
            var properties = row.Value ?? throw new InvalidOperationException($"DataTable row '{rowName}' has no property data.");
            var propertyIndex = properties.FindIndex(property => string.Equals(PropertyName(property), propertyName, StringComparison.Ordinal));
            if (propertyIndex < 0) throw new KeyNotFoundException($"Property '{propertyName}' was not found in DataTable row '{rowName}'. New row properties are not inserted by this tool.");

            var existing = properties[propertyIndex];
            if (!IsSupportedPrimitive(existing))
                throw new NotSupportedException($"Property '{propertyName}' has unsupported row type '{existing.GetType().Name}'. Only existing primitive row properties can be updated by this tool.");
            propertyType = existing.GetType().Name;
            var replacement = (PropertyData)existing.Clone();
            SetPrimitiveValue(replacement, requestedValue, candidate);
            properties[propertyIndex] = replacement;
            newValue = MCP_UAsset_Toolkit.Source.Tools.Common.ToolHelpers.ConvertPropertyToValue(replacement);
            return UAssetContext.PrepareForWrite(candidate);
        });
        return new { filePath = session.FilePath, exportIndex, rowName, propertyName, propertyType, newValue, isDirty = session.IsDirty };
    }

    public static object RemoveRow(string filePath, int exportIndex, string rowName)
    {
        ValidateRowName(rowName, nameof(rowName));
        var session = AssetSessionService.TransformSessionAsset(filePath, original =>
        {
            var candidate = UAssetContext.Clone(original);
            var table = GetDataTable(candidate, exportIndex);
            var row = FindRow(table, rowName) ?? throw new KeyNotFoundException($"DataTable row '{rowName}' was not found.");
            table.Table.Data.Remove(row);
            return UAssetContext.PrepareForWrite(candidate);
        });
        return new { filePath = session.FilePath, exportIndex, rowName, removed = true, isDirty = session.IsDirty };
    }

    private static DataTableExport GetDataTable(UAsset asset, int exportIndex)
    {
        var export = AssetInspectionService.GetExport(asset, exportIndex);
        return export as DataTableExport
            ?? throw new InvalidOperationException($"Export {exportIndex} is '{export.GetType().Name}', not DataTableExport.");
    }

    private static StructPropertyData? FindRow(DataTableExport table, string rowName) =>
        table.Table?.Data?.FirstOrDefault(row => string.Equals(RowName(row), rowName, StringComparison.Ordinal));

    private static string RowName(StructPropertyData row) => row.Name?.ToString() ?? string.Empty;
    private static string PropertyName(PropertyData property) => property.Name?.ToString() ?? string.Empty;

    private static object[] DescribeProperties(IEnumerable<PropertyData>? properties) => (properties ?? [])
        .Select(property => (object)new
        {
            name = PropertyName(property),
            propertyType = property.PropertyType?.ToString(),
            rawType = property.GetType().Name,
            value = MCP_UAsset_Toolkit.Source.Tools.Common.ToolHelpers.ConvertPropertyToValue(property)
        }).ToArray();

    private static void SetPrimitiveValue(PropertyData property, System.Text.Json.JsonElement value, UAsset asset)
    {
        switch (property)
        {
            case BoolPropertyData boolean: boolean.Value = value.GetBoolean(); break;
            case IntPropertyData integer: integer.Value = value.GetInt32(); break;
            case Int64PropertyData integer64: integer64.Value = value.GetInt64(); break;
            case FloatPropertyData single: single.Value = value.GetSingle(); break;
            case DoublePropertyData doubleValue: doubleValue.Value = value.GetDouble(); break;
            case StrPropertyData text: text.Value = FString.FromString(value.GetString()); break;
            case Utf8StrPropertyData utf8: utf8.Value = FString.FromString(value.GetString()); break;
            case NamePropertyData name: name.Value = FName.FromString(asset, value.GetString()); break;
            case EnumPropertyData enumeration: enumeration.Value = FName.FromString(asset, value.GetString()); break;
            case BytePropertyData byteProperty when byteProperty.ByteType == BytePropertyType.Byte:
                if (value.ValueKind != System.Text.Json.JsonValueKind.Number)
                    throw new ArgumentException("BytePropertyData with Byte representation requires a JSON number; changing its representation is not supported.", nameof(value));
                byteProperty.Value = value.GetByte();
                break;
            case BytePropertyData byteProperty when byteProperty.ByteType == BytePropertyType.FName:
                if (value.ValueKind != System.Text.Json.JsonValueKind.String)
                    throw new ArgumentException("BytePropertyData with FName representation requires a JSON string; changing its representation is not supported.", nameof(value));
                byteProperty.EnumValue = FName.FromString(asset, value.GetString());
                break;
            case TextPropertyData textProperty:
                SetTextValue(textProperty, value);
                break;
            default:
                throw new NotSupportedException($"Property type '{property.GetType().Name}' is not supported for DataTable row updates.");
        }
    }

    private static void SetTextValue(TextPropertyData property, System.Text.Json.JsonElement value)
    {
        var source = FString.FromString(value.GetString());
        switch (property.HistoryType)
        {
            case TextHistoryType.Base:
            case TextHistoryType.None:
                property.CultureInvariantString = source;
                return;
            case TextHistoryType.RawText:
                property.Value = source;
                return;
            default:
                throw new NotSupportedException($"Text history '{property.HistoryType}' is not supported for direct row edits. Use patch_asset_document for complex FText history.");
        }
    }

    private static bool IsSupportedPrimitive(PropertyData property) => property is
        BoolPropertyData or BytePropertyData or IntPropertyData or Int64PropertyData or
        FloatPropertyData or DoublePropertyData or StrPropertyData or Utf8StrPropertyData or
        NamePropertyData or TextPropertyData or EnumPropertyData;

    private static void ValidatePage(int offset, int limit)
    {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "offset must be zero or greater.");
        if (limit is < 1 or > MaximumLimit) throw new ArgumentOutOfRangeException(nameof(limit), $"limit must be between 1 and {MaximumLimit}.");
    }

    private static void ValidateMaxJsonCharacters(int maxJsonCharacters)
    {
        if (maxJsonCharacters is < 1 or > MaximumMaxJsonCharacters)
            throw new ArgumentOutOfRangeException(nameof(maxJsonCharacters), $"maxJsonCharacters must be between 1 and {MaximumMaxJsonCharacters}.");
    }

    private static void ValidateRowName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Name cannot be empty.", parameterName);
    }
}
