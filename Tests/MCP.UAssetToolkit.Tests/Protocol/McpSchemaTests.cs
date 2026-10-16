using System.Reflection;
using System.Text.RegularExpressions;
using MCP_UAsset_Toolkit.Source.Tools;
using ModelContextProtocol.Server;
using Xunit;

namespace MCP.UAssetToolkit.Tests;

public class McpSchemaTests
{
    private static readonly TimeSpan CatalogRegexTimeout = TimeSpan.FromSeconds(1);
    [Fact]
    public void CatalogIncludesEveryDiscoveredToolExactlyOnce()
    {
        var discovered = typeof(SessionTools).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() != null)
            .Select(method => McpServerTool.Create(method, target: null).ProtocolTool.Name)
            .Order(StringComparer.Ordinal).ToArray();
        var catalog = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "TOOLS.md"));
        var documented = Regex.Matches(catalog, @"^\| `(?<name>[a-z][a-z0-9_]*)` \|", RegexOptions.Multiline, CatalogRegexTimeout)
            .Select(match => match.Groups["name"].Value).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(documented.Length, documented.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(discovered, documented);
        Assert.Contains($"**{discovered.Length} tools**", catalog);
    }

    [Fact]
    public void AtomicBatchExposesTypedOrderedEditsAndSafePreviewDefaults()
    {
        var method = typeof(AssetBatchTools).GetMethod(nameof(AssetBatchTools.PatchAssetDocumentBatchAsync))!;
        var schema = McpServerTool.Create(method, target: null).ProtocolTool.InputSchema;
        var operationSchema = schema.GetProperty("properties").GetProperty("operations");
        Assert.Equal("array", operationSchema.GetProperty("type").GetString());
        var itemSchema = operationSchema.GetProperty("items");
        Assert.Equal("object", itemSchema.GetProperty("type").GetString());
        Assert.Equal(["pointer", "operation"], itemSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.True(itemSchema.GetProperty("properties").TryGetProperty("from", out _));
        var bulk = typeof(AssetBatchTools).GetMethod(nameof(AssetBatchTools.BatchPatchAssetsAsync))!;
        Assert.True(Assert.IsType<bool>(bulk.GetParameters().Single(parameter => parameter.Name == "preview").DefaultValue));
    }

    [Fact]
    public void EveryMcpToolGeneratesAUniqueValidInputSchema()
    {
        var methods = typeof(SessionTools).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() != null);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in methods)
        {
            var tool = McpServerTool.Create(method, target: null);
            Assert.True(names.Add(tool.ProtocolTool.Name), $"Duplicate MCP tool: {tool.ProtocolTool.Name}");
            Assert.Equal("object", tool.ProtocolTool.InputSchema.GetProperty("type").GetString());
        }
        Assert.Contains("open_asset", names);
        Assert.Contains("patch_asset_document", names);
        Assert.Contains("open_archive_session", names);
    }

    [Fact]
    public void ExportEditExposesTypedUpdateObject()
    {
        var method = typeof(AssetEditTools).GetMethod(nameof(AssetEditTools.EditExportInfoAsync))!;
        var tool = McpServerTool.Create(method, target: null);
        var properties = tool.ProtocolTool.InputSchema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("update", out _));
        Assert.Equal(3, method.GetParameters().Length);
    }
}
