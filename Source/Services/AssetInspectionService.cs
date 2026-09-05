using MCP_UAsset_Toolkit.Source.Models;
using UAssetAPI;
using UAssetAPI.ExportTypes;

namespace MCP_UAsset_Toolkit.Source.Services;

public static class AssetInspectionService
{
    public static void ValidateBytePage(int offset, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (limit is < 1 or > 1048576)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 1048576 bytes.");
    }

    public static object Validate(AssetSession session)
    {
        // Serialization resolves names/ancestry, so inspect a copy rather than changing a live session.
        var copy = UAssetContext.Clone(session.Asset);
        AssetNamePreparation.Prepare(copy);
        using var serialized = UAssetContext.WithNameRepair(copy, copy.WriteData);
        bool? binaryEqualsDisk = null;
        if (File.Exists(session.FilePath))
        {
            using var original = copy.PathToStream(session.FilePath);
            binaryEqualsDisk = serialized.ToArray().AsSpan().SequenceEqual(original.ToArray());
        }
        return new
        {
            canSerialize = true,
            serializedBytes = serialized.Length,
            binaryEqualsDisk,
            isDirty = session.IsDirty,
            rawExportCount = copy.Exports.OfType<RawExport>().Count(),
            note = "Binary differences are expected after edits. Successful serialization does not guarantee game compatibility."
        };
    }

    public static Export GetExport(UAsset asset, int index)
    {
        if (index < 1 || index > asset.Exports.Count)
            throw new ArgumentOutOfRangeException(nameof(index), $"Export index must be between 1 and {asset.Exports.Count}.");
        return asset.Exports[index - 1];
    }

    public static byte[] GetBytes(Export export, string section) => section.ToLowerInvariant() switch
    {
        "raw" when export is RawExport raw => raw.Data ?? [],
        "extras" => export.Extras ?? [],
        "script" when export is StructExport script => script.ScriptBytecodeRaw ?? [],
        _ => throw new ArgumentException("Section must be 'raw' for RawExport, 'extras' for any export, or 'script' for StructExport.")
    };

    public static void SetBytes(Export export, string section, byte[] bytes, int? scriptBytecodeSize = null)
    {
        // Check the export/section combination before any mutation.
        GetBytes(export, section);
        if (section.Equals("script", StringComparison.OrdinalIgnoreCase) && scriptBytecodeSize is not >= 0)
            throw new ArgumentException("Script replacement requires a nonnegative logical scriptBytecodeSize; it can differ from the stored byte count.");
        switch (section.ToLowerInvariant())
        {
            case "raw": ((RawExport)export).Data = bytes; break;
            case "extras": export.Extras = bytes; break;
            case "script":
                var script = (StructExport)export;
                script.ScriptBytecodeRaw = bytes;
                script.ScriptBytecode = null;
                script.ScriptBytecodeSize = scriptBytecodeSize!.Value;
                break;
        }
    }
}
