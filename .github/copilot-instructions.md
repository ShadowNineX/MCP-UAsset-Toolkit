# MCP UAsset Toolkit - Agent Guidelines

## Project Overview
An MCP (Model Context Protocol) server exposing Unreal Engine .uasset file operations via the UAssetAPI library. Server runs on `http://127.0.0.1:13338` with SSE transport.

## Build and Test
```bash
dotnet build                                    # Build project
dotnet run                                       # Run MCP server
dotnet publish -c Release -r win-x64 --self-contained  # Publish executable
```
No test project exists. UAssetAPI has separate test suite in `UAssetAPI/UAssetAPI.Tests/`.

## Adding New MCP Tools

**1. Tool Class Structure** - See [AssetDataTools.cs](Source/Tools/AssetDataTools.cs#L11-L17):
```csharp
[McpServerToolType]
public static class MyTools
{
    [McpServerTool, Description("Tool description for AI")]
    public static Task<string> MyToolAsync(
        [Description("Param description")]
        string paramName)
    {
        return Task.Run(() => { /* implementation */ });
    }
}
```

**2. Required Validation Pattern** - Every tool MUST follow this exact sequence:
```csharp
// 1. Check engine version set (required for all asset operations)
if (!EngineVersionService.IsVersionSet)
    return JsonSerializer.Serialize(new { success = false, error = ToolHelpers.VersionNotSetError });

// 2. Validate file exists
if (!File.Exists(filePath))
    return JsonSerializer.Serialize(new { success = false, error = ToolHelpers.FileNotFoundError });

// 3. Load asset with global version
var asset = new UAsset(filePath, EngineVersionService.GetVersionOrThrow());
```

**3. Response Format** - All tools return JSON strings with this structure:
```csharp
// Success: { "success": true, /* data fields */ }
// Error: { "success": false, "error": "message" }
```

## Architecture Patterns

**Version Management** - [EngineVersionService.cs](Source/Services/EngineVersionService.cs#L9-L28) uses static singleton:
- Users MUST call `SetEngineVersionAsync` before any asset operations
- Check `EngineVersionService.IsVersionSet` before UAsset instantiation
- Version parsing via `ToolHelpers.ParseEngineVersion()` supports "4.27", "5.5" format

**UAssetAPI Integration** - [AssetDataTools.cs](Source/Tools/AssetDataTools.cs#L87-L94):
- Access exports: `asset.Exports[index]` (0-based internal)  
- Check import references: `export.ToImport(asset).IsImport()`
- Get names: `export.ObjectName.ToString()`
- Type-cast exports: `if (export is NormalExport normalExport)`

**Error Handling** - Use shared constants from [ToolHelpers.cs](Source/Tools/Common/ToolHelpers.cs#L7-L8):
- Standard messages prevent inconsistent error text
- Always wrap operations in try-catch returning `{ success: false, error: ex.Message }`

**Indexing Convention** - [AssetDataTools.cs](Source/Tools/AssetDataTools.cs#L89-L93):
- Internal: 0-based arrays (`asset.Exports[0]`)
- User-facing: 1-based in descriptions and messages ("Export 1 of 10")

## Code Style

**Naming**: Methods end with `Async`, classes end with `Tools`/`Service`. Use `camelCase` parameters.

**Static Classes**: All tool classes are `public static` - no instances created.

**File Organization**: `Tools/` (MCP definitions), `Services/` (global state), `Models/` (DTOs), `Tools/Common/` (shared utilities).

**Dependencies**: 
- `ModelContextProtocol.AspNetCore` v0.8.0-preview.1 (MCP server framework)
- `CUE4Parse` v1.2.2 (Unreal parsing)
- Local `UAssetAPI` project reference (asset manipulation)

## File Handling Warnings

⚠️ **No path sanitization** - Tools trust all file paths from input  
⚠️ **Overwrites allowed** - Edit operations can overwrite originals without backup  
⚠️ **Exception disclosure** - Full exception messages returned to clients (may leak paths)
