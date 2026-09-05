using System.Reflection;
using UAssetAPI;
using UAssetAPI.UnrealTypes;

namespace MCP_UAsset_Toolkit.Source.Services;

/// <summary>Preserves parser context omitted by UAssetAPI's public JSON format.</summary>
public static class UAssetContext
{
    // Empty header sections can retain nonzero offsets. UAssetAPI does not include
    // these internal values in JSON or always recalculate them when writing.
    private static readonly FieldInfo[] HeaderFields = typeof(UAsset)
        .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
        .Where(field => field.IsAssembly && (field.FieldType == typeof(int) || field.FieldType == typeof(long))
            && (field.Name.EndsWith("Offset", StringComparison.Ordinal) || field.Name.EndsWith("Count", StringComparison.Ordinal)))
        .ToArray();

    public static string Serialize(UAsset asset)
    {
        var info = asset.Info;
        try { return asset.SerializeJson(); }
        finally { asset.Info = info; }
    }

    public static UAsset Clone(UAsset original)
    {
        var clone = UAsset.DeserializeJson(Serialize(original));
        Copy(original, clone);
        return clone;
    }

    public static T WithNameRepair<T>(UAsset asset, Func<T> serialize)
    {
        var repaired = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            try { return serialize(); }
            catch (NameMapOutOfRangeException ex)
            {
                // Match UAssetGUI's save repair, with a bound and repeat detection.
                if (ex.RequiredName == null || repaired.Count >= 4096 || !repaired.Add(ex.RequiredName.Value)) throw;
                asset.AddNameReference(ex.RequiredName);
            }
        }
    }

    public static UAsset PrepareForWrite(UAsset candidate)
    {
        AssetNamePreparation.Prepare(candidate);
        var repaired = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            try
            {
                using var binary = WithNameRepair(candidate, candidate.WriteData);
                if (binary.Length == 0) throw new InvalidOperationException("Asset produced an empty binary package.");
                return candidate;
            }
            catch (FName.DummyFNameSerializationException ex)
            {
                if (ex.Dummy == null || repaired.Count >= 4096 || !repaired.Add(ex.Dummy.Value)) throw;
                // FromString understands Unreal's numeric name suffixes. Rebinding
                // via JSON then replaces dummy FNames with real name-map entries.
                FName.FromString(candidate, ex.Dummy.Value);
                candidate = Clone(candidate);
            }
        }
    }

    public static void Copy(UAsset original, UAsset candidate)
    {
        candidate.Info = original.Info;
        candidate.FilePath = original.FilePath;
        candidate.Mappings = original.Mappings;
        candidate.CustomSerializationFlags = original.CustomSerializationFlags;
        candidate.GameSpecificOverride = original.GameSpecificOverride;
        candidate.UseSeparateBulkDataFiles = original.UseSeparateBulkDataFiles;
        candidate.MapStructTypeOverride = new(original.MapStructTypeOverride);
        candidate.ArrayStructTypeOverride = new(original.ArrayStructTypeOverride);
        candidate.OverrideNameMapHashes = original.OverrideNameMapHashes == null ? null : new(original.OverrideNameMapHashes);
        foreach (var field in HeaderFields) field.SetValue(candidate, field.GetValue(original));
    }
}
