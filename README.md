# MCP UAsset Toolkit

An [MCP (Model Context Protocol)](https://modelcontextprotocol.io) server that exposes Unreal Engine .uasset file operations via the [UAssetAPI](https://github.com/atenfyr/UAssetAPI) library. This tool enables AI assistants and other MCP clients to read, analyze, and modify Unreal Engine asset files programmatically.

## Features

### Asset Information
- **Read Asset Structure** - Parse .uasset files and access metadata
- **General Information** - View package details, flags, GUIDs, folder names
- **Name Map** - List and edit the string table used by the asset
- **Custom Versions** - View all custom version information

### Import/Export Management
- **List Imports** - View all external dependencies with full details (class package, class name, outer index, etc.)
- **List Exports** - View all objects contained in the asset with comprehensive metadata
- **Export Data** - Access detailed property data for specific exports

### Editing Capabilities
- **Name Map Editing** - Add or modify entries in the name table
- **Import Editing** - Update import references (object names, class packages, class names, outer indices)
- **Export Editing** - Modify export information (object names, indices, flags)
- **Save Changes** - Write modified assets back to disk

## Installation

### Prerequisites
- .NET 10.0 SDK or later
- Windows x64 (currently configured for win-x64)

### Build from Source

```bash
# Clone the repository
git clone <repository-url>
cd "MCP UAsset Toolkit"

# Build the project
dotnet build

# Run the MCP server
dotnet run
```

### Publish Standalone Executable

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

The executable will be located in `bin/Release/net10.0/win-x64/publish/`

## Configuration

### Server URL

The default server URL is configured in `appsettings.json`:

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://127.0.0.1:13338"
      }
    }
  }
}
```

You can override this via:
- **Environment variable**: `ASPNETCORE_URLS=http://localhost:5000`
- **Command line**: `dotnet run --urls "http://localhost:5000"`

### MCP Client Configuration

Add to your MCP client configuration (e.g., Claude Desktop):

```json
{
  "mcpServers": {
    "uasset-toolkit": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/MCP UAsset Toolkit"],
      "env": {}
    }
  }
}
```

## Usage

### 1. Set Engine Version

Before working with any assets, set the Unreal Engine version:

```
set_engine_version("4.27")
```

Supported formats: `"4.27"`, `"5.3"`, `"5.5"`, etc.

### 2. Read Asset Information

```
read_asset("path/to/asset.uasset")
list_exports("path/to/asset.uasset")
list_imports("path/to/asset.uasset")
get_name_map("path/to/asset.uasset")
```

### 3. Edit Asset

```
# Edit a name in the name map
edit_name_map_entry("path/to/asset.uasset", index: 5, newName: "NewName")

# Edit an import
edit_import("path/to/asset.uasset", importIndex: 1, objectName: "NewObjectName")

# Edit export information
edit_export_info("path/to/asset.uasset", exportIndex: 1, bIsAsset: true)

# Save changes
save_asset("path/to/asset.uasset")
```

## Available MCP Tools

### Version Management
- `set_engine_version` - Set the Unreal Engine version for parsing

### Asset Reading
- `read_asset` - Parse and read basic asset structure
- `get_general_info` - Get general package information
- `get_full_asset_info` - Get comprehensive metadata (similar to UAssetGUI Info tab)
- `list_exports` - List all exports with detailed information
- `list_imports` - List all imports with detailed information  
- `get_export_data` - Get detailed data for a specific export
- `get_name_map` - Get the name map (string table)
- `list_custom_versions` - List all custom versions

### Asset Editing
- `edit_name_map_entry` - Edit an entry in the name map
- `add_name_map_entry` - Add a new name to the name map
- `edit_import` - Edit import fields
- `edit_export_info` - Edit export information fields
- `save_asset` - Save modified asset to disk

## Architecture

```
MCP UAsset Toolkit/
├── Source/
│   ├── Program.cs              # ASP.NET Core host and MCP server setup
│   ├── Models/                 # Data transfer objects
│   ├── Services/               # Global state management (EngineVersionService)
│   └── Tools/                  # MCP tool implementations
│       ├── VersionTools.cs     # Engine version management
│       ├── AssetInfoTools.cs   # Asset information retrieval
│       ├── AssetDataTools.cs   # Import/export/name map listing
│       └── AssetEditTools.cs   # Asset editing operations
├── UAssetAPI/                  # Submodule - Unreal asset parsing library
└── appsettings.json            # Server configuration
```

## Transport

The MCP server uses **SSE (Server-Sent Events)** transport at the `/sse` endpoint:
- Default URL: `http://127.0.0.1:13338/sse`
- Protocol: [Model Context Protocol](https://modelcontextprotocol.io)

## Dependencies

- **[UAssetAPI](https://github.com/atenfyr/UAssetAPI)** - Core Unreal asset parsing and manipulation
- **[CUE4Parse](https://github.com/FabianFG/CUE4Parse)** v1.2.2 - Unreal Engine parsing utilities
- **[ModelContextProtocol.AspNetCore](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore)** v0.8.0-preview.1 - MCP server framework

## Important Notes

⚠️ **Backup Your Assets** - Always back up original .uasset files before editing

⚠️ **Version Compatibility** - Ensure you set the correct engine version before working with assets

⚠️ **Save Required** - Edit operations modify assets in memory. You must call `save_asset` to persist changes

## Development

### Building

```bash
dotnet build
```

### Running Tests

```bash
# UAssetAPI has separate test suite
cd UAssetAPI
dotnet test UAssetAPI.Tests/UAssetAPI.Tests.csproj
```

### Code Style

- Methods end with `Async`
- Tool classes are `public static`
- All tools return JSON strings with `{ success: bool, ... }` format
- Validate engine version and file existence in all tools

## License

This project uses UAssetAPI. See the [UAssetAPI LICENSE](UAssetAPI/LICENSE) for details.

## Contributing

Contributions are welcome! Please ensure:
1. All MCP tools follow the established patterns
2. Proper error handling with standard error messages
3. Engine version validation before asset operations
4. Build succeeds without errors

## Acknowledgments

- [atenfyr](https://github.com/atenfyr) for [UAssetAPI](https://github.com/atenfyr/UAssetAPI)
- [Anthropic](https://www.anthropic.com) for the [Model Context Protocol](https://modelcontextprotocol.io)
