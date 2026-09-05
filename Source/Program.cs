using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ModelContextProtocol.AspNetCore;

namespace MCP_UAsset_Toolkit.Source;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Clear default console logging providers so "info: Microsoft.Hosting.Lifetime" is not written to stdout
        builder.Logging.ClearProviders();

        builder.Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly()
            .WithResourcesFromAssembly();

        var app = builder.Build();

        app.MapMcp("/sse");

        await app.RunAsync();
    }
}
