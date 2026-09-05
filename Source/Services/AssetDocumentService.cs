using System.Text.Json;
using System.Text.Json.Nodes;
using MCP_UAsset_Toolkit.Source.Models;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;

namespace MCP_UAsset_Toolkit.Source.Services;

/// <summary>
/// Provides an atomic JSON-document view over an asset session. The document format is
/// UAssetAPI's JSON representation, so callers can work with fields UAssetAPI supports
/// without each field needing a dedicated MCP tool.
/// </summary>
public static class AssetDocumentService
{
    public const int DefaultMaxJsonChars = 65_536;
    public const int MaximumMaxJsonChars = 1_000_000;

    // These values carry process/session context rather than package data. They must be
    // restored even if an older UAssetAPI version includes them in its JSON output.
    private static readonly HashSet<string> ProtectedRootMembers = new(StringComparer.Ordinal)
    {
        "$type", "FilePath", "Mappings", "CustomSerializationFlags", "GameSpecificOverride", "MapStructTypeOverride", "ArrayStructTypeOverride",
        "UseSeparateBulkDataFiles", "OverrideNameMapHashes"
    };

    // UAssetAPI's polymorphic reader accepts assembly-qualified names. Newly introduced
    // metadata is restricted to public concrete document types from its own assembly.
    private static readonly HashSet<string> IntroducibleTypeNames = typeof(UAsset).Assembly.GetTypes()
        .Where(type => type.IsPublic && !type.IsAbstract && (typeof(PropertyData).IsAssignableFrom(type) || typeof(Export).IsAssignableFrom(type)))
        .Select(ToMetadataTypeName)
        .ToHashSet(StringComparer.Ordinal);

    public sealed record DocumentRead(string FilePath, string Pointer, JsonNode? Value, int JsonLength);
    public sealed record DocumentPatch(string FilePath, string Pointer, string Operation, int JsonLength);
    public sealed record DocumentSchema(IReadOnlyList<string> PropertyDataTypes, IReadOnlyList<string> ExportTypes);

    /// <summary>Lists exact $type metadata values that can be introduced in the JSON document.</summary>
    public static DocumentSchema GetSchema()
    {
        var types = typeof(UAsset).Assembly.GetTypes();
        return new DocumentSchema(
            types.Where(type => type.IsPublic && !type.IsAbstract && typeof(PropertyData).IsAssignableFrom(type))
                .Select(ToMetadataTypeName).OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            types.Where(type => type.IsPublic && !type.IsAbstract && typeof(Export).IsAssignableFrom(type))
                .Select(ToMetadataTypeName).OrderBy(name => name, StringComparer.Ordinal).ToArray());
    }

    public static DocumentRead Read(string filePath, string pointer = "", int maxJsonChars = DefaultMaxJsonChars)
    {
        ValidateMaxJsonChars(maxJsonChars);
        var session = AssetSessionService.GetOrLoadSession(filePath);
        var root = ParseSerializedAsset(session.Asset);
        var node = Resolve(root, ParsePointer(pointer), pointer);
        var length = node?.ToJsonString().Length ?? 4;
        if (length > maxJsonChars)
        {
            throw new InvalidOperationException($"JSON at pointer '{pointer}' is {length} characters, exceeding maxJsonChars {maxJsonChars}. Use a narrower pointer or export the asset to a JSON file.");
        }

        return new DocumentRead(session.FilePath, pointer, node?.DeepClone(), length);
    }

    public static DocumentPatch Patch(string filePath, string pointer, string operation, string? valueJson = null)
    {
        if (operation is not ("replace" or "add" or "remove"))
        {
            throw new ArgumentException("operation must be 'replace', 'add', or 'remove'.", nameof(operation));
        }

        var tokens = ParsePointer(pointer);
        EnsurePointerIsWritable(tokens, pointer);
        JsonNode? value = null;
        if (operation != "remove")
        {
            if (valueJson is null)
            {
                throw new ArgumentException($"valueJson is required for '{operation}'.", nameof(valueJson));
            }
            value = ParseJsonValue(valueJson);
        }
        else if (valueJson is not null)
        {
            throw new ArgumentException("valueJson must be omitted for 'remove'.", nameof(valueJson));
        }

        var jsonLength = 0;
        var session = AssetSessionService.TransformSessionAsset(filePath, original =>
        {
            var originalRoot = ParseSerializedAsset(original);
            var candidateRoot = Apply(originalRoot.DeepClone(), tokens, pointer, operation, value);
            if (candidateRoot is not JsonObject)
            {
                throw new InvalidOperationException("The document root must remain a UAsset JSON object.");
            }

            RestoreProtectedRootMembers(originalRoot, candidateRoot);
            ValidateTypeMetadata(originalRoot, candidateRoot);

            // Deserialization happens only after all validation is complete. The session
            // service swaps the result only when this callback returns successfully.
            var candidate = UAsset.DeserializeJson(candidateRoot.ToJsonString());
            UAssetContext.Copy(original, candidate);
            candidate = UAssetContext.PrepareForWrite(candidate);
            VerifyCanonicalPatch(candidateRoot, ParseSerializedAsset(candidate), tokens, pointer, operation);
            jsonLength = candidateRoot.ToJsonString().Length;
            return candidate;
        });
        return new DocumentPatch(session.FilePath, pointer, operation, jsonLength);
    }

    private static JsonNode ParseSerializedAsset(UAsset asset)
    {
        return JsonNode.Parse(UAssetContext.Serialize(asset)) ?? throw new InvalidOperationException("UAssetAPI produced an empty JSON document.");
    }

    private static JsonNode? ParseJsonValue(string json)
    {
        try
        {
            // JSON null is a valid patch value and is represented by a null JsonNode.
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Null ? null : JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("valueJson is not valid JSON.", nameof(json), ex);
        }
    }

    private static void ValidateMaxJsonChars(int value)
    {
        if (value < 1 || value > MaximumMaxJsonChars)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"maxJsonChars must be between 1 and {MaximumMaxJsonChars}.");
        }
    }

    private static string[] ParsePointer(string pointer)
    {
        if (pointer is null) throw new ArgumentNullException(nameof(pointer));
        if (pointer.Length == 0) return [];
        if (!pointer.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("JSON pointer must be empty or start with '/'.", nameof(pointer));
        }

        return pointer[1..].Split('/').Select(DecodePointerToken).ToArray();
    }

    private static string DecodePointerToken(string token)
    {
        if (!token.Contains('~')) return token;
        var result = new System.Text.StringBuilder(token.Length);
        var index = 0;
        while (index < token.Length)
        {
            if (token[index] != '~')
            {
                result.Append(token[index++]);
                continue;
            }
            if (++index >= token.Length || (token[index] != '0' && token[index] != '1'))
            {
                throw new ArgumentException("JSON pointer contains an invalid '~' escape.", nameof(token));
            }
            result.Append(token[index++] == '0' ? '~' : '/');
        }
        return result.ToString();
    }

    private static void EnsurePointerIsWritable(IEnumerable<string> tokens, string pointer)
    {
        var tokenArray = tokens.ToArray();
        if (tokenArray.Any(token => string.Equals(token, "$type", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Pointer '{pointer}' addresses protected $type metadata.");
        }
        if (tokenArray.Length > 0 && ProtectedRootMembers.Contains(tokenArray[0]))
        {
            throw new InvalidOperationException($"Pointer '{pointer}' addresses protected session context '{tokenArray[0]}'.");
        }
    }

    private static JsonNode? Resolve(JsonNode root, IReadOnlyList<string> tokens, string pointer)
    {
        JsonNode? current = root;
        foreach (var token in tokens)
        {
            current = current switch
            {
                JsonObject obj when obj.ContainsKey(token) => obj[token],
                JsonArray array => array[ParseArrayIndex(token, array.Count, false, pointer)],
                _ => throw new InvalidOperationException($"JSON pointer '{pointer}' does not exist.")
            };
        }
        return current;
    }

    private static JsonNode Apply(JsonNode root, IReadOnlyList<string> tokens, string pointer, string operation, JsonNode? value)
    {
        if (tokens.Count == 0)
        {
            if (operation == "remove") throw new InvalidOperationException("Removing the document root is not allowed.");
            if (value is not JsonObject replacement) throw new InvalidOperationException("Replacing the document root requires a UAsset JSON object.");
            return replacement.DeepClone();
        }

        var parent = Resolve(root, tokens.Take(tokens.Count - 1).ToArray(), pointer)
            ?? throw new InvalidOperationException($"JSON pointer '{pointer}' has a null parent.");
        var token = tokens[^1];
        if (parent is JsonObject obj)
        {
            ApplyObject(obj, token, pointer, operation, value);
            return root;
        }

        if (parent is JsonArray array)
        {
            ApplyArray(array, token, pointer, operation, value);
            return root;
        }

        throw new InvalidOperationException($"JSON pointer '{pointer}' has a non-container parent.");
    }

    private static void ApplyObject(JsonObject obj, string token, string pointer, string operation, JsonNode? value)
    {
        if (operation == "replace" && !obj.ContainsKey(token)) throw new InvalidOperationException($"JSON pointer '{pointer}' does not exist for replace.");
        if (operation == "remove")
        {
            if (!obj.Remove(token)) throw new InvalidOperationException($"JSON pointer '{pointer}' does not exist for remove.");
        }
        else obj[token] = value?.DeepClone();
    }

    private static void ApplyArray(JsonArray array, string token, string pointer, string operation, JsonNode? value)
    {
        if (operation == "add" && token == "-")
        {
            array.Add(value?.DeepClone());
            return;
        }
        var index = ParseArrayIndex(token, array.Count, operation == "add", pointer);
        if (operation == "add") array.Insert(index, value?.DeepClone());
        else if (operation == "replace") array[index] = value?.DeepClone();
        else array.RemoveAt(index);
    }

    private static int ParseArrayIndex(string token, int count, bool allowEnd, string pointer)
    {
        if (token.Length == 0 || (token.Length > 1 && token[0] == '0') || token.Any(character => character is < '0' or > '9') ||
            !int.TryParse(token, out var index) || index >= count + (allowEnd ? 1 : 0))
        {
            throw new InvalidOperationException($"JSON pointer '{pointer}' contains invalid array index '{token}'.");
        }
        return index;
    }

    private static void ValidateTypeMetadata(JsonNode original, JsonNode candidate)
    {
        var allowed = TypeNames(original);
        allowed.UnionWith(IntroducibleTypeNames);
        if (TypeNames(candidate).Any(typeName => !allowed.Contains(typeName)))
        {
            throw new InvalidOperationException("Patched JSON introduces a $type value outside the original metadata and UAssetAPI document-type allowlist.");
        }
    }

    private static void VerifyCanonicalPatch(JsonNode expectedRoot, JsonNode canonicalRoot, IReadOnlyList<string> tokens, string pointer, string operation)
    {
        if (tokens.Count == 0)
        {
            if (!JsonNode.DeepEquals(expectedRoot, canonicalRoot)) throw new InvalidOperationException("Patched root was not preserved by UAssetAPI's JSON round-trip.");
            return;
        }

        var effectiveTokens = tokens.ToArray();
        if (operation == "add" && effectiveTokens[^1] == "-")
        {
            var expectedParent = Resolve(expectedRoot, effectiveTokens[..^1], pointer) as JsonArray
                ?? throw new InvalidOperationException($"JSON pointer '{pointer}' has a non-array parent.");
            effectiveTokens[^1] = (expectedParent.Count - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (operation is "add" or "replace")
        {
            if (!Contains(expectedRoot, effectiveTokens, pointer) || !Contains(canonicalRoot, effectiveTokens, pointer) ||
                !MatchesRequestedValue(Resolve(expectedRoot, effectiveTokens, pointer), Resolve(canonicalRoot, effectiveTokens, pointer)))
            {
                throw new InvalidOperationException($"Patched value at '{pointer}' was not preserved by UAssetAPI's JSON round-trip.");
            }
            return;
        }

        var expectedParentNode = Resolve(expectedRoot, effectiveTokens[..^1], pointer);
        var canonicalParentNode = Resolve(canonicalRoot, effectiveTokens[..^1], pointer);
        if (expectedParentNode is JsonObject && canonicalParentNode is JsonObject canonicalObject && !canonicalObject.ContainsKey(effectiveTokens[^1])) return;
        if (expectedParentNode is JsonArray expectedArray && canonicalParentNode is JsonArray canonicalArray && expectedArray.Count == canonicalArray.Count) return;
        throw new InvalidOperationException($"Removal at '{pointer}' was not preserved by UAssetAPI's JSON round-trip.");
    }

    private static bool Contains(JsonNode root, IReadOnlyList<string> tokens, string pointer)
    {
        if (tokens.Count == 0) return true;
        var parent = Resolve(root, tokens.Take(tokens.Count - 1).ToArray(), pointer);
        if (parent is JsonObject obj) return obj.ContainsKey(tokens[^1]);
        if (parent is JsonArray array && int.TryParse(tokens[^1], out var index)) return index >= 0 && index < array.Count;
        return false;
    }

    private static bool MatchesRequestedValue(JsonNode? expected, JsonNode? actual)
    {
        if (expected is null || actual is null) return expected is null && actual is null;
        if (expected is JsonObject expectedObject && actual is JsonObject actualObject)
        {
            return expectedObject.All(entry => actualObject.ContainsKey(entry.Key) && MatchesRequestedValue(entry.Value, actualObject[entry.Key]));
        }
        if (expected is JsonArray expectedArray && actual is JsonArray actualArray)
        {
            return expectedArray.Count == actualArray.Count && expectedArray.Zip(actualArray).All(pair => MatchesRequestedValue(pair.First, pair.Second));
        }
        return JsonNode.DeepEquals(expected, actual);
    }

    private static void RestoreProtectedRootMembers(JsonNode original, JsonNode candidate)
    {
        if (original is not JsonObject originalObject || candidate is not JsonObject candidateObject) return;
        foreach (var member in ProtectedRootMembers)
        {
            if (originalObject.ContainsKey(member)) candidateObject[member] = originalObject[member]?.DeepClone();
            else candidateObject.Remove(member);
        }
    }

    private static HashSet<string> TypeNames(JsonNode node)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        CollectTypeNames(node, result);
        return result;
    }

    private static string ToMetadataTypeName(Type type) => $"{type.FullName ?? type.Name}, {type.Assembly.GetName().Name}";

    private static void CollectTypeNames(JsonNode? node, ISet<string> result)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj.ContainsKey("$type"))
                {
                    if (obj["$type"] is not JsonValue typeValue || !typeValue.TryGetValue<string>(out var typeName) || string.IsNullOrWhiteSpace(typeName))
                    {
                        throw new InvalidOperationException("Every $type metadata value must be a non-empty string.");
                    }
                    result.Add(typeName);
                }
                foreach (var child in obj) CollectTypeNames(child.Value, result);
                break;
            case JsonArray array:
                foreach (var child in array) CollectTypeNames(child, result);
                break;
        }
    }

}
