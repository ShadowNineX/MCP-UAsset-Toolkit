using System.ComponentModel;

namespace MCP_UAsset_Toolkit.Source.Models;

public sealed record AssetDocumentEdit
{
    [Description("RFC 6901 JSON pointer; array positions are zero-based.")]
    public required string Pointer { get; init; }

    [Description("add, replace, remove, copy, move, or test. Operations execute in order.")]
    public required string Operation { get; init; }

    [Description("Complete JSON value for add/replace/test; omit for remove/copy/move.")]
    public string? ValueJson { get; init; }

    [Description("Source JSON pointer for copy/move; omit for other operations.")]
    public string? From { get; init; }
}
