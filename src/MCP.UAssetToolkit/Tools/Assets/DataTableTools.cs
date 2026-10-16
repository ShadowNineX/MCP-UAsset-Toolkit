using System.ComponentModel;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class DataTableTools
{
    [McpServerTool, Description("List DataTable rows by exact export index, with optional case-insensitive name search and pagination.")]
    public static Task<string> ListDataTableRowsAsync(string filePath, int exportIndex, string query = "", int offset = 0, int limit = DataTableService.DefaultLimit) =>
        ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(DataTableService.ListRows(filePath, exportIndex, query, offset, limit)));

    [McpServerTool, Description("Read one DataTable row by its exact row name, including a bounded typed UAssetAPI JSON representation and readable property values.")]
    public static Task<string> GetDataTableRowAsync(string filePath, int exportIndex, string rowName, int maxJsonCharacters = DataTableService.DefaultMaxJsonCharacters) =>
        ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(DataTableService.GetRow(filePath, exportIndex, rowName, maxJsonCharacters)));

    [McpServerTool, Description("Deep-clone an existing DataTable row to a new exact row name. The new name must not already exist.")]
    public static Task<string> CopyDataTableRowAsync(string filePath, int exportIndex, string sourceRowName, string newRowName) =>
        ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(DataTableService.CopyRow(filePath, exportIndex, sourceRowName, newRowName), "DataTable row copied. Call save_asset to persist."));

    [McpServerTool, Description("Update an existing primitive property in a DataTable row by exact row and property names. This tool does not insert unknown properties.")]
    public static Task<string> SetDataTableRowPropertyAsync(string filePath, int exportIndex, string rowName, string propertyName, string valueJson) =>
        ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(DataTableService.SetRowProperty(filePath, exportIndex, rowName, propertyName, valueJson), "DataTable row property updated. Call save_asset to persist."));

    [McpServerTool, Description("Remove a DataTable row by its exact row name.")]
    public static Task<string> RemoveDataTableRowAsync(string filePath, int exportIndex, string rowName) =>
        ToolHelpers.ExecuteAsync(() => ToolHelpers.FormatSuccess(DataTableService.RemoveRow(filePath, exportIndex, rowName), "DataTable row removed. Call save_asset to persist."));
}
