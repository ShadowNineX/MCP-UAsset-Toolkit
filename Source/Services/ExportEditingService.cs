using MCP_UAsset_Toolkit.Source.Models;
using UAssetAPI;
using UAssetAPI.UnrealTypes;

namespace MCP_UAsset_Toolkit.Source.Services;

public static class ExportEditingService
{
    public static IReadOnlyList<string> Update(UAsset asset, int exportIndex, ExportInfoUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (exportIndex < 1 || exportIndex > asset.Exports.Count)
            throw new ArgumentOutOfRangeException(nameof(exportIndex), $"Export index must be between 1 and {asset.Exports.Count}.");

        // Validate everything before changing the export or adding names.
        foreach (var index in new[] { update.OuterIndex, update.ClassIndex, update.SuperIndex, update.TemplateIndex })
            ValidatePackageIndex(asset, index);
        EObjectFlags? flags = update.ObjectFlags == null ? null : Enum.Parse<EObjectFlags>(update.ObjectFlags, ignoreCase: true);
        if (update.ObjectName != null)
            ArgumentException.ThrowIfNullOrWhiteSpace(update.ObjectName);

        var export = asset.Exports[exportIndex - 1];
        var changes = new List<string>();
        if (update.ObjectName != null)
        {
            export.ObjectName = new FName(asset, update.ObjectName);
            changes.Add($"ObjectName: {update.ObjectName}");
        }
        Apply(update.OuterIndex, value => export.OuterIndex = new FPackageIndex(value), "OuterIndex", changes);
        Apply(update.ClassIndex, value => export.ClassIndex = new FPackageIndex(value), "ClassIndex", changes);
        Apply(update.SuperIndex, value => export.SuperIndex = new FPackageIndex(value), "SuperIndex", changes);
        Apply(update.TemplateIndex, value => export.TemplateIndex = new FPackageIndex(value), "TemplateIndex", changes);
        Apply(update.BForcedExport, value => export.bForcedExport = value, "bForcedExport", changes);
        Apply(update.BNotForClient, value => export.bNotForClient = value, "bNotForClient", changes);
        Apply(update.BNotForServer, value => export.bNotForServer = value, "bNotForServer", changes);
        Apply(update.BIsAsset, value => export.bIsAsset = value, "bIsAsset", changes);
        Apply(flags, value => export.ObjectFlags = value, "ObjectFlags", changes);
        return changes;
    }

    public static void ValidatePackageIndex(UAsset asset, int? index)
    {
        if (index > asset.Exports.Count || index < -asset.Imports.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Package index does not refer to an existing import or export.");
    }

    private static void Apply<T>(T? value, Action<T> setter, string name, List<string> changes) where T : struct
    {
        if (!value.HasValue) return;
        setter(value.Value);
        changes.Add($"{name}: {value.Value}");
    }
}
