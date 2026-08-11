using System.ComponentModel;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools.Common;
using ModelContextProtocol.Server;
using UAssetAPI;

namespace MCP_UAsset_Toolkit.Source.Tools;

[McpServerToolType]
public static class AssetJsonTools
{
    [McpServerTool, Description("Export complete .uasset to UAssetAPI JSON format string")]
    public static Task<string> ExportAssetToJsonAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Whether to format/indent the JSON output (default: true)")]
        bool formatted = true)
    {
        return Task.Run(() =>
        {
            try
            {
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var json = session.Asset.SerializeJson(formatted);

                return ToolHelpers.FormatSuccess(new
                {
                    filePath = session.FilePath,
                    jsonLength = json.Length,
                    json
                });
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }

    [McpServerTool, Description("Export .uasset to a .json file on disk")]
    public static Task<string> ExportToJsonFileAsync(
        [Description("Full path to the .uasset file")]
        string filePath,
        [Description("Destination .json file path")]
        string jsonOutputPath,
        [Description("Whether to format/indent the JSON output (default: true)")]
        bool formatted = true)
    {
        return Task.Run(() =>
        {
            try
            {
                var session = AssetSessionService.GetOrLoadSession(filePath);
                var json = session.Asset.SerializeJson(formatted);

                var fullOutPath = Path.GetFullPath(jsonOutputPath);
                var dir = Path.GetDirectoryName(fullOutPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(fullOutPath, json);

                return ToolHelpers.FormatSuccess(new
                {
                    jsonOutputPath = fullOutPath,
                    fileSize = new FileInfo(fullOutPath).Length
                }, "Exported asset to JSON file successfully");
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }

    [McpServerTool, Description("Import asset from UAssetAPI JSON string and load into session or save as .uasset")]
    public static Task<string> ImportAssetFromJsonAsync(
        [Description("JSON string representing the UAsset")]
        string jsonContent,
        [Description("Target .uasset file path to associate with this asset")]
        string targetFilePath,
        [Description("Whether to immediately write the imported asset to disk (default: true)")]
        bool saveToDisk = true)
    {
        return Task.Run(() =>
        {
            try
            {
                var fullPath = Path.GetFullPath(targetFilePath);
                var asset = UAsset.DeserializeJson(jsonContent);

                if (saveToDisk)
                {
                    var dir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    asset.Write(fullPath);
                }

                // Register session
                var session = AssetSessionService.RegisterSession(fullPath, asset);

                return ToolHelpers.FormatSuccess(new
                {
                    targetFilePath = fullPath,
                    exportCount = asset.Exports.Count,
                    importCount = asset.Imports.Count,
                    savedToDisk = saveToDisk
                }, "Asset imported from JSON successfully");
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }

    [McpServerTool, Description("Import asset from a .json file on disk and compile into .uasset file")]
    public static Task<string> ImportFromJsonFileAsync(
        [Description("Source .json file path")]
        string jsonInputPath,
        [Description("Destination .uasset file path")]
        string targetFilePath)
    {
        return Task.Run(() =>
        {
            try
            {
                var jsonFullPath = Path.GetFullPath(jsonInputPath);
                if (!File.Exists(jsonFullPath))
                {
                    return ToolHelpers.FormatError($"JSON file not found at '{jsonInputPath}'");
                }

                var jsonContent = File.ReadAllText(jsonFullPath);
                var fullPath = Path.GetFullPath(targetFilePath);

                var asset = UAsset.DeserializeJson(jsonContent);

                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                asset.Write(fullPath);

                AssetSessionService.RegisterSession(fullPath, asset);

                return ToolHelpers.FormatSuccess(new
                {
                    jsonInputPath = jsonFullPath,
                    targetFilePath = fullPath,
                    fileSize = new FileInfo(fullPath).Length,
                    exportCount = asset.Exports.Count,
                    importCount = asset.Imports.Count
                }, "Compiled .uasset from JSON file successfully");
            }
            catch (Exception ex)
            {
                return ToolHelpers.FormatError(ex.Message, ex);
            }
        });
    }
}
