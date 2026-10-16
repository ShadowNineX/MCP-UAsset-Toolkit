# MCP UAsset Toolkit

[![Build](https://github.com/hedgehogform/MCP-UAsset-Toolkit/actions/workflows/build.yml/badge.svg)](https://github.com/hedgehogform/MCP-UAsset-Toolkit/actions/workflows/build.yml)
[![Release](https://github.com/hedgehogform/MCP-UAsset-Toolkit/actions/workflows/release.yml/badge.svg)](https://github.com/hedgehogform/MCP-UAsset-Toolkit/actions/workflows/release.yml)

Give AI assistants tools to inspect, edit, and package Unreal Engine assets through [MCP](https://modelcontextprotocol.io). Built on **.NET 10**, [UAssetAPI](https://github.com/atenfyr/UAssetAPI), and [CUE4Parse](https://github.com/FabianFG/CUE4Parse), with optional [retoc](https://github.com/trumank/retoc) integration for IoStore.

## What it does

- **Inspect assets:** headers, names, imports, exports, properties, mappings, and searchable metadata.
- **Edit in memory:** primitive properties, nested structs/arrays/maps, DataTables, export data, and raw or script bytes.
- **Validate and save:** JSON import/export, serialization checks, binary comparison, save-as, and protected reload/discard.
- **Work with archives:** browse CUE4Parse packages, supply AES keys, extract companion files, and create `.pak` archives.
- **Use IoStore:** inspect, verify, and convert containers through an installed retoc executable.

See the **[capability guide](docs/CAPABILITIES.md)** for examples and compatibility limits, and the **[97-tool catalog](docs/TOOLS.md)** for the complete exposed interface. Named DataTable editing, atomic/bulk document patches, resolved dependencies, mapping enums, archive object queries, and retoc chunk workflows are included. Game support depends on the underlying libraries, engine version, and mappings. Visual media previews and media conversion are not included.

## Quick start

Requires **Windows x64** and the **.NET 10 SDK** to build from source.

```powershell
git clone --recurse-submodules https://github.com/hedgehogform/MCP-UAsset-Toolkit.git
cd MCP-UAsset-Toolkit
dotnet run --project src/MCP.UAssetToolkit/MCP.UAssetToolkit.csproj
```

For an existing checkout, initialize its pinned dependency with `git submodule update --init --recursive`.

Connect your MCP client to **`http://127.0.0.1:13338/sse`** using its HTTP server configuration. The server binds to loopback by default; change the endpoint in `src/MCP.UAssetToolkit/appsettings.json` if needed.

A typical asset workflow is `open_asset` → inspect/search → edit → `validate_asset` → `save_asset`. Edits stay in the session until saved. Dirty sessions require an explicit discard before closing or reloading.

### Optional retoc

Run `retoc_status` through your MCP client to check availability. The toolkit searches `PATH`, or accepts a `retocPath` in tool options. If unavailable, tools return guidance linking to the [official download](https://github.com/trumank/retoc/releases). Conversion writes to new output files and refuses overwrites.

## Develop

```powershell
# .NET 10 / xUnit / Microsoft.Testing.Platform
dotnet test --solution MCP.UAssetToolkit.slnx
dotnet test --solution MCP.UAssetToolkit.slnx --configuration Release

# Self-contained Windows build
dotnet publish src/MCP.UAssetToolkit/MCP.UAssetToolkit.csproj -c Release -r win-x64 --self-contained true
```

Run `./eng/Verify.ps1` to restore, build, and test Debug and Release, or add `-Publish` to produce the Windows executable under `artifacts/publish/Release`.

| Path | Purpose |
| --- | --- |
| `src/MCP.UAssetToolkit/Core/` | Asset, archive, engine, mapping, and retoc operations and models, grouped by feature. |
| `src/MCP.UAssetToolkit/Tools/` | MCP tools and shared response helpers, grouped by feature. |
| `Tests/MCP.UAssetToolkit.Tests/` | Asset roundtrips, atomic edits, archive workflows, tool schemas, and optional retoc execution. |
| `eng/` | Shared build settings and local verification. |
| `UAssetAPI/` | Pinned upstream submodule. |

See the [architecture guide](docs/ARCHITECTURE.md) for code ownership and [development notes](docs/CAPABILITIES.md#development-and-verification) for fixtures and build details.

## Dependencies

UAssetAPI is pinned as a Git submodule; CUE4Parse and the MCP SDK are NuGet dependencies. See [UAssetAPI's license](UAssetAPI/LICENSE) and each upstream project for its licensing terms.
