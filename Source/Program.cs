using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using ModelContextProtocol.AspNetCore;

namespace MCP_UAsset_Toolkit.Source;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        // Server URL configuration is in appsettings.json (Kestrel:Endpoints:Http:Url)
        // Can be overridden by environment variable ASPNETCORE_URLS or command line argument --urls

        builder.Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly();

        var app = builder.Build();

        app.MapMcp("/sse");

        await app.RunAsync();
    }
}
