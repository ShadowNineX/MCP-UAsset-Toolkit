using System.Collections;
using System.Reflection;
using Newtonsoft.Json;
using UAssetAPI;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.UnrealTypes;

namespace MCP_UAsset_Toolkit.Source.Services;

/// <summary>Registers names before the library enters its write-only serialization phase.</summary>
public static class AssetNamePreparation
{
    public static void Prepare(UAsset asset)
    {
        var pending = new Stack<object>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        pending.Push(asset);
        while (pending.TryPop(out var value))
        {
            if (!visited.Add(value) || value is string or byte[] || value.GetType().IsPrimitive) continue;
            if (value is FName name)
            {
                BindName(asset, name);
                continue;
            }
            if (value is PropertyData property && !string.IsNullOrEmpty(property.PropertyType?.Value))
                asset.AddNameReference(property.PropertyType);
            foreach (var child in Children(value).OfType<object>()) pending.Push(child);
        }
    }

    private static void BindName(UAsset asset, FName name)
    {
        if (!name.IsDummy) return;
        var bound = FName.FromString(asset, name.Value.Value);
        name.Asset = asset;
        name.Value = bound.Value;
        name.Number = bound.Number;
    }

    private static IEnumerable<object?> Children(object value)
    {
        if (value is IDictionary dictionary)
            return dictionary.Keys.Cast<object?>().Concat(dictionary.Values.Cast<object?>());
        if (value is IEnumerable enumerable) return enumerable.Cast<object?>();
        var type = value.GetType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            return [type.GetProperty("Key")!.GetValue(value), type.GetProperty("Value")!.GetValue(value)];
        if (type.Assembly != typeof(UAsset).Assembly || value is FString) return [];
        return SerializedMembers(value, type);
    }

    private static IEnumerable<object?> SerializedMembers(object value, Type type)
    {
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Where(field => field.GetCustomAttribute<JsonIgnoreAttribute>() == null).Select(field => field.GetValue(value));
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0 && property.GetCustomAttribute<JsonPropertyAttribute>() != null)
            .Select(property => property.GetValue(value));
        return fields.Concat(properties);
    }
}
