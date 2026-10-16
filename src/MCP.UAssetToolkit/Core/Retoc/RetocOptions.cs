using System.ComponentModel;

namespace MCP_UAsset_Toolkit.Source.Models;

/// <summary>Optional, safe configuration for a retoc invocation.</summary>
public sealed class RetocOptions
{
    [Description("Optional full path to retoc (or retoc.exe). When omitted, retoc is resolved from PATH.")]
    public string? RetocPath { get; init; }

    [Description("Optional AES key for encrypted IoStore containers. It is passed only to retoc and is never returned in tool output.")]
    public string? AesKey { get; init; }

    [Description("Optional retoc EIoContainerHeaderVersion override: PreInitial, Initial, LocalizedPackages, OptionalSegmentPackages, NoExportInfo, SoftPackageReferences, or SoftPackageReferencesOffset.")]
    public string? ContainerHeaderVersionOverride { get; init; }

    [Description("Optional retoc EIoStoreTocVersion override: Invalid, Initial, DirectoryIndex, PartitionSize, PerfectHash, PerfectHashWithOverflow, OnDemandMetaData, RemovedOnDemandMetaData, or ReplaceIoChunkHashWithIoHash.")]
    public string? TocVersionOverride { get; init; }

    [Description("Optional asset filename filters. retoc receives one --filter value for each item.")]
    public IReadOnlyList<string>? Filters { get; init; }

    [Description("For inspect_iostore action=list, include patched chunks as well as unique chunks.")]
    public bool ListAll { get; init; }
    [Description("For inspect_iostore action=list, include chunk hashes.")]
    public bool ListHash { get; init; }
    [Description("For inspect_iostore action=list, include package IDs.")]
    public bool ListPackage { get; init; }
    [Description("For inspect_iostore action=list, include chunk sizes.")]
    public bool ListSize { get; init; }
    [Description("For inspect_iostore action=list, include chunk paths.")]
    public bool ListPath { get; init; }
    [Description("For inspect_iostore action=list, include package-store entries.")]
    public bool ListStore { get; init; }

    [Description("For convert_to_legacy, skip asset conversion.")]
    public bool NoAssets { get; init; }
    [Description("For convert_to_legacy, skip shader-library conversion.")]
    public bool NoShaders { get; init; }
    [Description("For convert_to_legacy, skip script-object extraction.")]
    public bool NoScriptObjects { get; init; }
    [Description("For convert_to_legacy, do not compress shader libraries.")]
    public bool NoCompressShaders { get; init; }
    [Description("For convert_to_legacy, validate conversion without creating final output files.")]
    public bool DryRun { get; init; }
    [Description("Optional retoc engine-version override for convert_to_legacy, such as UE5_4.")]
    public string? LegacyEngineVersion { get; init; }

    [Description("Maximum retoc run time in seconds (1-3600). Defaults to 300.")]
    public int TimeoutSeconds { get; init; } = 300;
}
