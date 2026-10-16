# MCP UAsset Toolkit - Agent Guidelines

## Project overview

A .NET 10 MCP server for Unreal Engine assets. UAssetAPI owns asset mutation and saving; CUE4Parse owns archive browsing and extraction. Retoc is an optional external executable for IoStore operations.

## Build and test

Run commands from the repository root. `global.json` selects the SDK and Microsoft.Testing.Platform runner.

```powershell
dotnet restore MCP.UAssetToolkit.slnx
dotnet build MCP.UAssetToolkit.slnx --configuration Debug --no-restore
dotnet test --solution MCP.UAssetToolkit.slnx --configuration Debug --no-build
dotnet run --project src/MCP.UAssetToolkit/MCP.UAssetToolkit.csproj
dotnet publish src/MCP.UAssetToolkit/MCP.UAssetToolkit.csproj --configuration Release --runtime win-x64 --self-contained true
```

`./eng/Verify.ps1` restores, builds, and tests Debug and Release. `-Publish` also publishes a self-contained Windows executable to `artifacts/publish/Release`.

Avoid simultaneous builds: UAssetAPI creates and deletes `git_commit.txt` during its build. Keep the upstream submodule pristine. The toolkit's tests are under `Tests/MCP.UAssetToolkit.Tests`; UAssetAPI's separate upstream suite is not included in the root solution.

## Code ownership

- `src/MCP.UAssetToolkit/Program.cs`: HTTP hosting and MCP registration.
- `src/MCP.UAssetToolkit/Core/`: operations and models, grouped into Assets, Archives, Engine, Mappings, and Retoc.
- `src/MCP.UAssetToolkit/Tools/`: MCP definitions grouped by feature, with execution and response helpers under Common.
- `Tests/MCP.UAssetToolkit.Tests/`: asset, archive, MCP schema, and retoc tests grouped by area.
- `eng/Toolkit.props`: C# defaults shared by toolkit projects only; project-specific settings stay in each project.

See [architecture](docs/ARCHITECTURE.md), [capabilities](docs/CAPABILITIES.md), and the complete [tool catalog](docs/TOOLS.md). Update the catalog when adding or removing tools; a schema test checks it against discovery.

## Adding MCP tools

Follow the adjacent tool class. Keep public tool methods static, annotated with `[McpServerTool]` and descriptions, inside a `[McpServerToolType]` class. Resource classes also require `[McpServerResourceType]`.

Use `ToolHelpers.ExecuteAsync` for synchronous asset operations: it catches exceptions and holds the asset-session operation lock for the complete operation. Retoc tools use their own asynchronous wrapper so cancellation and process timeouts remain effective.

Load assets through `AssetSessionService.GetOrLoadSession`, which reuses unsaved in-memory state and session load options. Do not construct a fresh asset in a tool or reject an already registered session just because its file does not exist. Engine versions may be set globally, supplied per load, or detected by UAssetAPI.

Return JSON through `ToolHelpers.FormatSuccess` and `ToolHelpers.FormatError`. Preserve existing field names, descriptions, default values, and exception details. Mark sessions dirty after edits; dirty close, clear, and reload operations require explicit discard.

Use `AssetDocumentService.PatchBatch` for atomic ordered edits, including preview and test guards. Bulk operations report each asset's result independently and never save implicitly. New DataTable mutations clone and validate before replacing session state. Preserve type-specific enum and FText metadata when changing values.

Retoc `manifest` writes a fixed `pakstore.json` in its working directory. Run it in an isolated temporary working directory; never in a source, project, or game directory.

## Compatibility and style

- Preserve the existing `MCP UAsset Toolkit` assembly/executable name and `MCP_UAsset_Toolkit.Source.*` namespaces when moving files.
- Keep existing static services, public models, tool names, and JSON contracts.
- User-facing import and export indices are 1-based; internal collections are 0-based. Name-map indices are 0-based.
- Keep archive path, symlink, size, and overwrite checks. Retoc conversion must preserve staging, rollback, cancellation, timeout, and key-redaction behavior.
- Use the existing C# style. Make focused changes and preserve unrelated work.
