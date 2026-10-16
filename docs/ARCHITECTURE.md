# Project layout

```text
MCP.UAssetToolkit.slnx
src/MCP.UAssetToolkit/
  Program.cs                 HTTP host and MCP registration
  appsettings.json           Default endpoint and logging configuration
  Core/
    Assets/                  Asset sessions, editing, inspection, and asset models
    Archives/                CUE4Parse sessions, extraction, and pak creation
    Engine/                  Default Unreal Engine version
    Mappings/                UAssetAPI mapping cache and defaults
    Retoc/                   Optional process integration and its options
  Tools/
    Assets/                  Asset inspection and editing tools
    Archives/                Archive tools
    Engine/                  Version tools and engine://version resource
    Mappings/                Mapping tools
    Retoc/                   IoStore inspection and conversion tools
    Sessions/                Asset session lifecycle tools
    Common/                  MCP execution, JSON responses, and search helpers
Tests/MCP.UAssetToolkit.Tests/
  Assets/                    Asset, search, editing, and mapping tests
  Archives/                  Archive and filesystem safety tests
  Protocol/                  MCP discovery and input schema tests
  Retoc/                     Optional process integration tests
eng/
  Toolkit.props              Shared C# defaults
  Verify.ps1                 Restore, build, test, and optional publish
UAssetAPI/                    Pinned upstream submodule
```

## Responsibilities

`Program.cs` hosts the server. `Tools/` owns MCP method metadata, user-facing validation, and JSON responses. `Core/` owns reusable asset, archive, mapping, and process operations, together with their state and input models. Add code beside the feature it belongs to.

UAssetAPI remains the asset mutation and save implementation. CUE4Parse owns archive discovery, browsing, and extraction. Retoc runs as an optional external executable for IoStore operations.

Asset tools run through `ToolHelpers.ExecuteAsync`, which holds the existing session operation lock across reads and edits. Asset sessions retain unsaved changes; closing, clearing, or reloading a dirty session requires explicit discard. Retoc's asynchronous process runner keeps its separate cancellation and timeout handling.

## Build and compatibility

The application remains one assembly. Its existing `MCP UAsset Toolkit` assembly/executable name, `MCP_UAsset_Toolkit.Source.*` namespaces, public types, MCP names, and JSON contracts are retained. Moving files into feature directories does not rename these contracts.

The root solution groups the application, tests, and upstream dependency. `Directory.Build.props` imports `eng/Toolkit.props` only for the two toolkit projects, preserving their nullable and implicit-using settings. Each project keeps its target framework and project-specific options. The existing `Directory.Build.targets` compression-package override still applies to UAssetAPI without editing the submodule.

`eng/Verify.ps1` runs from the repository root so `global.json` selects the SDK and Microsoft.Testing.Platform runner. CI uses the same solution and project paths. Publish output goes under the ignored `artifacts/publish/` directory; it retains the executable and endpoint configuration.

Run the server with `dotnet run --project src/MCP.UAssetToolkit/MCP.UAssetToolkit.csproj`. Configure the endpoint in that project's `appsettings.json`, or in the copy alongside the published executable.
