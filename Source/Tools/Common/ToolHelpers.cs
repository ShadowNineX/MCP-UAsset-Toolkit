using System.Text.Json;
using System.Text.Json.Nodes;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace MCP_UAsset_Toolkit.Source.Tools.Common;

public static class ToolHelpers
{
    public const string FileNotFoundError = "File not found or invalid path";
    public const string VersionNotSetError = "Engine version not set. Use set_engine_version tool first.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IgnoreReadOnlyProperties = false
    };

    public static EngineVersion ParseEngineVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Engine version string cannot be empty.");

        var normalized = version.Trim();

        // Check exact match on enum name
        if (Enum.TryParse<EngineVersion>(normalized, true, out var exactMatch))
        {
            return exactMatch;
        }

        var lower = normalized.ToLowerInvariant();

        if (lower is "5.0ea" or "5_0ea" or "5.0_ea")
            return EngineVersion.VER_UE5_0EA;

        // Parse version parts (e.g., "4.27", "5.5", "5_7")
        var parts = lower.Replace("_", ".").Split('.');
        if (parts.Length >= 2 && int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor))
        {
            var enumName = $"VER_UE{major}_{minor}";
            if (Enum.TryParse<EngineVersion>(enumName, true, out var result))
            {
                return result;
            }
        }

        throw new ArgumentException($"Unsupported engine version: '{version}'. Supported range: 4.0 - 4.27, 5.0EA, 5.0 - 5.7");
    }

    public static string FormatSuccess(object? data = null, string? message = null, object? extra = null)
    {
        var dict = new Dictionary<string, object?>
        {
            ["success"] = true
        };

        if (message != null) dict["message"] = message;
        if (data != null) dict["data"] = data;

        if (extra != null)
        {
            var extraJson = JsonSerializer.SerializeToNode(extra);
            if (extraJson is JsonObject obj)
            {
                foreach (var kvp in obj)
                {
                    dict[kvp.Key] = kvp.Value;
                }
            }
        }

        return JsonSerializer.Serialize(dict, JsonOptions);
    }

    public static string FormatError(string error, Exception? ex = null)
    {
        var dict = new Dictionary<string, object?>
        {
            ["success"] = false,
            ["error"] = error
        };

        if (ex != null)
        {
            dict["exceptionType"] = ex.GetType().Name;
            dict["details"] = ex.Message;
        }

        return JsonSerializer.Serialize(dict, JsonOptions);
    }

    public static object? ConvertPropertyToValue(PropertyData? prop)
    {
        if (prop == null) return null;

        return prop switch
        {
            BoolPropertyData boolProp => boolProp.Value,
            BytePropertyData byteProp => byteProp.ByteType == BytePropertyType.Byte ? byteProp.Value : byteProp.EnumValue?.ToString(),
            IntPropertyData intProp => intProp.Value,
            Int8PropertyData int8Prop => int8Prop.Value,
            Int16PropertyData int16Prop => int16Prop.Value,
            Int64PropertyData int64Prop => int64Prop.Value,
            UInt16PropertyData uInt16Prop => uInt16Prop.Value,
            UInt32PropertyData uInt32Prop => uInt32Prop.Value,
            UInt64PropertyData uInt64Prop => uInt64Prop.Value,
            FloatPropertyData floatProp => floatProp.Value,
            DoublePropertyData doubleProp => doubleProp.Value,
            StrPropertyData strProp => strProp.Value?.ToString(),
            Utf8StrPropertyData utf8StrProp => utf8StrProp.Value?.ToString(),
            NamePropertyData nameProp => nameProp.Value?.ToString(),
            TextPropertyData textProp => textProp.CultureInvariantString?.ToString() ?? textProp.Value?.ToString(),
            ObjectPropertyData objProp => new { index = objProp.Value.Index, isImport = objProp.Value.IsImport(), isExport = objProp.Value.IsExport() },
            SetPropertyData setProp => setProp.Value?.Select(ConvertPropertyToValue).ToList(),
            ArrayPropertyData arrayProp => arrayProp.Value?.Select(ConvertPropertyToValue).ToList(),
            StructPropertyData structProp => structProp.Value?.Select(p => new
            {
                name = p.Name?.ToString(),
                type = p.PropertyType?.ToString(),
                value = ConvertPropertyToValue(p)
            }).ToList(),
            MapPropertyData mapProp => mapProp.Value?.Select(kvp => new
            {
                key = ConvertPropertyToValue(kvp.Key),
                value = ConvertPropertyToValue(kvp.Value)
            }).ToList(),
            _ => prop.ToString()
        };
    }

    public static PropertyData CreatePropertyData(
        string propertyType,
        FName name,
        JsonElement value,
        UAsset asset)
    {
        var typeName = propertyType.Trim();
        if (!typeName.EndsWith("PropertyData", StringComparison.OrdinalIgnoreCase))
        {
            typeName += "PropertyData";
        }

        switch (typeName.ToLowerInvariant())
        {
            case "strpropertydata":
            case "strproperty":
                var strProp = new StrPropertyData(name);
                strProp.Value = FString.FromString(value.GetString());
                return strProp;

            case "utf8strpropertydata":
            case "utf8strproperty":
                var utf8Prop = new Utf8StrPropertyData(name);
                utf8Prop.Value = FString.FromString(value.GetString());
                return utf8Prop;

            case "namepropertydata":
            case "nameproperty":
                var nameProp = new NamePropertyData(name);
                nameProp.Value = new FName(asset, value.GetString());
                return nameProp;

            case "textpropertydata":
            case "textproperty":
                var textProp = new TextPropertyData(name);
                textProp.CultureInvariantString = FString.FromString(value.GetString());
                return textProp;

            case "boolpropertydata":
            case "boolproperty":
                var boolProp = new BoolPropertyData(name);
                boolProp.Value = value.GetBoolean();
                return boolProp;

            case "intpropertydata":
            case "intproperty":
                var intProp = new IntPropertyData(name);
                intProp.Value = value.GetInt32();
                return intProp;

            case "int64propertydata":
            case "int64property":
                var int64Prop = new Int64PropertyData(name);
                int64Prop.Value = value.GetInt64();
                return int64Prop;

            case "floatpropertydata":
            case "floatproperty":
                var floatProp = new FloatPropertyData(name);
                floatProp.Value = value.GetSingle();
                return floatProp;

            case "doublepropertydata":
            case "doubleproperty":
                var doubleProp = new DoublePropertyData(name);
                doubleProp.Value = value.GetDouble();
                return doubleProp;

            case "bytepropertydata":
            case "byteproperty":
                var byteProp = new BytePropertyData(name);
                if (value.ValueKind == JsonValueKind.Number)
                {
                    byteProp.ByteType = BytePropertyType.Byte;
                    byteProp.Value = value.GetByte();
                }
                else
                {
                    byteProp.ByteType = BytePropertyType.FName;
                    byteProp.EnumValue = new FName(asset, value.GetString());
                }
                return byteProp;

            case "enumpropertydata":
            case "enumproperty":
                var enumProp = new EnumPropertyData(name);
                enumProp.Value = new FName(asset, value.GetString());
                return enumProp;

            case "objectpropertydata":
            case "objectproperty":
                var objProp = new ObjectPropertyData(name);
                objProp.Value = new FPackageIndex(value.GetInt32());
                return objProp;

            default:
                throw new NotSupportedException($"Property type '{propertyType}' is not supported for dynamic creation directly. Consider JSON import or edit.");
        }
    }
}
