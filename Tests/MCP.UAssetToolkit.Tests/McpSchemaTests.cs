using System.Reflection;
using MCP_UAsset_Toolkit.Source.Tools;
using ModelContextProtocol.Server;
using Xunit;

namespace MCP.UAssetToolkit.Tests;

public class McpSchemaTests
{
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
