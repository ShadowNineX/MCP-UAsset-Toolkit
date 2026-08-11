# MCP UAsset Toolkit

[![Build](https://github.com/hedgehogform/MCP-UAsset-Toolkit/actions/workflows/build.yml/badge.svg)](https://github.com/hedgehogform/MCP-UAsset-Toolkit/actions/workflows/build.yml)
[![Release](https://github.com/hedgehogform/MCP-UAsset-Toolkit/actions/workflows/release.yml/badge.svg)](https://github.com/hedgehogform/MCP-UAsset-Toolkit/actions/workflows/release.yml)

An [MCP (Model Context Protocol)](https://modelcontextprotocol.io) server that exposes Unreal Engine `.uasset` file operations via the [UAssetAPI](https://github.com/atenfyr/UAssetAPI) library. This tool enables AI assistants and other MCP clients to read, analyze, search, edit, and convert Unreal Engine asset files programmatically.

## Key Features

### In-Memory Asset Session Management
- **Persistent Multi-Step Edits**: Keeps loaded assets in memory (`AssetSessionService`) so edits accumulate across multiple tool calls before `save_asset` is invoked.
- **Session Tracking**: List active sessions, check dirty flags, and unload sessions cleanly.

### Comprehensive Asset Information & Inspection
- **Read & Full Info**: Package flags, object versions (UE4 & UE5), licensee version, recorded engine version, folder names, GUIDs.
- **Header Metadata**: Soft object paths, gatherable localization text, embedded asset registry records & tags, world composition tile info.
- **Tables & Lists**: Exports, imports, string table (name map), custom version container.
- **Search Capabilities**: Substring/regex searching across name map, imports, and exports.

### Property & Export Editing
- **Export Property Manipulation**: Read, set, update, or remove property values (`Int`, `Float`, `Double`, `Bool`, `Str`, `Utf8Str`, `Name`, `Text`, `Byte`, `Enum`, `Object`, etc.) on exports.
- **Structural Editing**: Add new exports (`NormalExport`, `RawExport`, `ClassExport`), duplicate/clone exports, remove exports.
- **Import Editing**: Add, edit, or remove import references.
- **Name Map Editing**: Add, edit, search, or remove string table entries.

### Full JSON Serialization & Roundtripping
- **Export to JSON**: Serialize entire assets to UAssetAPI JSON format strings or `.json` files.
- **Import from JSON**: Compile `.json` files or raw JSON strings back into valid binary `.uasset` files.

### Unversioned Property Mappings (.usmap & .jmap)
- **Mapping Support**: Load `.usmap`, `.jmap`, and `.jmap.gz` schema mappings for cooked unversioned assets.

---

## Installation & Build

### Prerequisites
- **.NET 10.0 SDK** or later
- **Windows x64**

### Build from Source

```bash
# Clone the repository with submodules
git clone --recurse-submodules https://github.com/hedgehogform/MCP-UAsset-Toolkit.git
cd "MCP UAsset Toolkit"

# Build Debug
dotnet build

# Build Release
dotnet build --configuration Release

# Run tests
dotnet test UAssetAPI/UAssetAPI.Tests/UAssetAPI.Tests.csproj

# Run MCP Server
dotnet run
```

---

## Complete MCP Tools Reference

### Version & Mapping Tools
- `set_engine_version` - Set default Unreal Engine version (e.g. `'4.27'`, `'5.0'`, `'5.5'`, `'5.7'`, `'5.0EA'`)
- `get_supported_engine_versions` - List all supported Unreal Engine version strings
- `load_mappings` - Load a `.usmap` or `.jmap` unversioned property mapping file
- `get_mappings_info` - Inspect loaded property mapping schemas

### Session Tools
- `list_active_sessions` - View all active in-memory asset sessions and dirty states
- `close_session` - Unload a specific asset session from memory
- `clear_all_sessions` - Clear all loaded sessions

### Inspection Tools
- `read_asset` - Parse basic structure of a `.uasset` file
- `get_general_info` - View package flags, folder name, and package GUID
- `get_full_asset_info` - Comprehensive metadata (similar to UAssetGUI Info tab)
- `list_exports` - List all exports with index, class, flags, serial size/offset
- `list_imports` - List all external dependency imports
- `get_name_map` - List string table entries with encodings
- `list_custom_versions` - List custom versions (GUID, friendly name, version)
- `get_soft_object_paths` - View soft object paths stored in asset header
- `get_gatherable_text_data` - View gatherable localization text data
- `get_asset_registry_records` - View asset registry records and searchable tags
- `get_world_tile_info` - View world composition tile bounds and settings
- `get_export_data` - Detailed export properties and summary
- `search_name_map` - Search name map entries matching a query string
- `search_imports` - Search imports by object name, class name, or package
- `search_exports` - Search exports by object name or class name

### Property Tools
- `get_export_properties` - Get export properties as structured JSON
- `set_export_property` - Set, add, or update property value on an export
- `remove_export_property` - Remove a property from an export

### Editing & Structure Tools
- `add_name_map_entry` - Add string to name map
- `edit_name_map_entry` - Modify name map entry at index
- `add_import` - Add a new external import reference
- `edit_import` - Edit import fields
- `remove_import` - Remove import at index
- `add_export` - Add a new export (`NormalExport`, `RawExport`, `ClassExport`)
- `edit_export_info` - Modify export header info and flags
- `copy_export` - Clone/duplicate an export
- `remove_export` - Remove export at index
- `add_soft_object_path` - Add a soft object path to asset header
- `remove_soft_object_path` - Remove soft object path by index
- `add_custom_version` - Add custom version GUID & number
- `remove_custom_version` - Remove custom version by GUID
- `set_asset_registry_tag` - Add or update asset registry searchable tag
- `save_asset` - Save in-memory modified asset back to disk

### JSON Roundtripping Tools
- `export_asset_to_json` - Export asset to JSON string
- `export_to_json_file` - Export asset to a `.json` file
- `import_asset_from_json` - Import asset from JSON string
- `import_from_json_file` - Compile `.json` file back into `.uasset` file

---

## Architecture

```
MCP UAsset Toolkit/
├── Source/
│   ├── Program.cs                  # ASP.NET Core host & MCP server setup
│   ├── Models/                     # Data transfer models
│   ├── Services/                   # Core application services
│   │   ├── EngineVersionService.cs # Default engine version management
│   │   ├── AssetSessionService.cs  # In-memory asset session management
│   │   └── MappingService.cs       # .usmap / .jmap property mapping service
│   └── Tools/                      # MCP Server Tool Registries
│       ├── Common/ToolHelpers.cs   # Shared parsing, property, & JSON helpers
│       ├── VersionTools.cs         # Engine version tools
│       ├── SessionTools.cs         # Session management tools
│       ├── AssetInfoTools.cs       # Metadata & header inspection tools
│       ├── AssetDataTools.cs       # Export/import/name map listing & search
│       ├── AssetEditTools.cs       # Export/import/name map editing & saving
│       ├── AssetPropertyTools.cs   # Property read/write/delete tools
│       ├── AssetStructureTools.cs  # Export/import/tag addition & deletion
│       ├── AssetJsonTools.cs       # JSON serialization & roundtripping
│       └── MappingTools.cs         # Mapping loading & inspection tools
├── UAssetAPI/                      # Submodule - UAssetAPI Library (v1.1.0)
└── appsettings.json                # ASP.NET Core configuration
```

## License

This project uses [UAssetAPI](https://github.com/atenfyr/UAssetAPI). See [LICENSE](UAssetAPI/LICENSE) for details.
