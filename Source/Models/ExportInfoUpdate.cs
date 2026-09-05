using System.ComponentModel;

namespace MCP_UAsset_Toolkit.Source.Models;

public sealed class ExportInfoUpdate
{
    [Description("New object name; omitted fields retain their current values.")]
    public string? ObjectName { get; init; }
    [Description("Package index: 0=null, positive=export, negative=import.")]
    public int? OuterIndex { get; init; }
    public int? ClassIndex { get; init; }
    public int? SuperIndex { get; init; }
    public int? TemplateIndex { get; init; }
    public bool? BForcedExport { get; init; }
    public bool? BNotForClient { get; init; }
    public bool? BNotForServer { get; init; }
    public bool? BIsAsset { get; init; }
    [Description("Unreal EObjectFlags names, comma-separated for multiple flags.")]
    public string? ObjectFlags { get; init; }
}
