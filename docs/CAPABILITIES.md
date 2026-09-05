# Capabilities and compatibility

This map compares the toolkit with the data operations in [UAssetGUI](https://github.com/atenfyr/UAssetGUI), [UAssetAPI](https://github.com/atenfyr/UAssetAPI), [CUE4Parse](https://github.com/FabianFG/CUE4Parse), and the installed retoc CLI. It does not imply compatibility with every game or expose every low-level serializer method.

## Capability map

| Workflow | MCP support |
| --- | --- |
| Read headers, names, imports, exports, registry tags, world tile data | Existing inspection and search tools |
| Load engine/game-specific cooked assets and unversioned mappings | `open_asset` with typed options; `get_asset_load_options` lists flags/overrides |
| Add/edit primitive properties; copy exports | Existing property/structure tools |
| Edit nested structs, arrays, maps, DataTables, complex properties and export JSON | `read_asset_document`, `patch_asset_document`, `get_asset_document_schema` |
| Read/edit dependencies, soft package references, package fields and custom versions | JSON pointers into the corresponding document members; dedicated tools remain available for common fields |
| Raw export bytes, extra bytes, script bytecode | `get_export_bytes`, `set_export_bytes`; script writes require logical `scriptBytecodeSize` |
| Replace names throughout references | `replace_name_references` preserves name-map positions |
| Verify serialization and original binary equality | `validate_asset` checks a copy; edited assets normally differ from disk |
| Missing-name repair on save | Bounded UAssetGUI-style retry using the missing name reported by UAssetAPI |
| Reload/discard/save-as | Session tools; explicit discard for dirty sessions; save-as keeps the source dirty |
| Inspect mappings and patch mapping version metadata | Schema listing/details, diagnostic JSON export, and version-patched `.usmap` copies |
| Browse/mount `.pak` and IoStore containers | CUE4Parse archive sessions; encrypted containers may need user-supplied AES keys and mappings |
| Extract and repack `.pak` | Extract to a staging directory, edit files, then `create_pak_from_directory` |
| Inspect/verify and convert IoStore | Optional retoc tools with explicit engine-version and override options |
| GUI tree controls, clipboard UI, visual previews, arbitrary scripts | Data equivalents are exposed where relevant; desktop UI and arbitrary code execution are outside this toolkit |
| Texture/audio/model media conversion | Package JSON and original binary extraction are available; rendered media conversion is not included |

## Loading and editing an asset

Call `open_asset` with parameters such as:

```json
{
  "filePath": "C:\\Game\\Content\\Example.uasset",
  "options": {
    "engineVersion": "5.4",
    "mappingsPath": "C:\\Mappings\\Game.usmap",
    "mapStructTypes": { "MyMap": ["Guid", "Vector"] },
    "arrayStructTypes": { "MyArray": "Vector" }
  }
}
```

An existing session is reused unless `forceReload` is true. Reloading a dirty session also requires `discardChanges: true`. JSON imports with `saveToDisk: false` remain dirty and can be edited/saved before the target file exists.

`edit_export_info` now takes three arguments: `filePath`, `exportIndex`, and an `update` object. This replaces the old flat optional fields:

```json
{
  "filePath": "C:\\Game\\Content\\Example.uasset",
  "exportIndex": 1,
  "update": { "objectName": "NewName", "bIsAsset": true, "objectFlags": "RF_Public" }
}
```

## Nested JSON editing

Read the relevant subtree first. JSON pointers use **zero-based** array positions, unlike the dedicated import/export tools, which use **one-based** indices. Examples include `/Exports/0`, `/Exports/0/Data`, `/DependsMap`, `/SoftPackageReferenceList`, and `/FolderName`. JSON field names are case-sensitive; inspect the actual asset to learn its layout.

```json
{
  "filePath": "C:\\Game\\Content\\Example.uasset",
  "pointer": "/Exports/0/Data/0/Value",
  "operation": "replace",
  "valueJson": "42"
}
```

`replace`, `add`, and `remove` operate on objects or arrays. Use `/-` to append to an array. Escape `/` as `~1` and `~` as `~0` in member names. `valueJson` is a complete JSON value; a string value therefore includes JSON quotes. Omit it for `remove`.

Complex typed nodes can be copied from a document read. `get_asset_document_schema` lists exact allowed UAssetAPI property/export `$type` names for creating new nodes. Arbitrary .NET type metadata is rejected. File paths, parser options, mapping context, and split-file mode are protected. Invalid JSON, unknown fields ignored by the library, invalid pointers, or unserializable changes fail before the session is replaced.

Structural editing is low-level: deleting/inserting top-level imports or exports can change package indices. Neither the original structure tools nor JSON patching automatically rewrite all references. Prefer append/copy where possible, inspect dependencies, validate, and test the result in the target game. Successful serialization cannot prove game compatibility.

## Archive workflow

1. `open_archive_session` with the directory containing `.pak` or `.utoc/.ucas` files and the CUE4Parse game enum, for example `GAME_UE5_4`. Set `recursive` only when needed.
2. Inspect the returned mounted/unmounted archives and required key GUIDs. Use `submit_archive_key` when a key is required. Responses do not include key values.
3. Use `list_archive_files` with `query`, `offset`, and `limit` to find virtual paths.
4. `read_package_exports` reads parsed export JSON. A size-blocked export gives an actionable response; extract it when JSON is too large.
5. `extract_archive_file` writes beneath an explicit output directory and includes available package companions. It refuses existing targets unless `overwrite` is true, and rejects unsafe paths/reparse points.
6. Edit loose assets with UAssetAPI tools and save them. Use `create_pak_from_directory` to build a new `.pak` from the staging directory.

Extraction's byte limit is an output limit. CUE4Parse may allocate decompressed package/companion buffers before the final total can be checked. Large or malformed archives therefore still require appropriate process memory limits.

## Optional retoc

`retoc_status` checks `retoc.exe` on Windows `PATH`, or an explicit `retocPath`. No automatic download or installation occurs. Missing/unlaunchable retoc responses explain how to install it from [official retoc releases](https://github.com/trumank/retoc/releases). The integration was inspected against **retoc_cli 0.1.5**.

- `inspect_iostore`: `info`, `list`, or `verify` for a `.utoc`. List options include hashes, sizes, paths, package IDs, and store entries.
- `convert_to_legacy`: `.utoc` or Paks directory to a **new `.pak`**; optional filters, engine override, shader/asset skip flags, and dry run.
- `convert_to_zen`: legacy `.pak` or directory to new `.utoc`, `.ucas`, and `.pak` files; requires `engineVersion`, for example `UE5_4`.

Conversion tools accept an `options` object for `retocPath`, AES key, supported container/header overrides, and timeout. They stage output beside the destination and refuse existing output files. Failed conversions retain staging files for diagnosis/recovery; canceled conversions attempt staging cleanup after stopping the process. The subprocess uses argument lists instead of shell command strings, drains output with a cap, and requests process-tree termination on timeout/cancellation. Termination failures are reported explicitly.

Native IoStore/chunk compatibility is determined by retoc and the game. Game-specific AES values, mappings, Oodle prerequisites, and version overrides may still be needed. The tools report subprocess errors rather than treating an unsuccessful conversion as a completed file operation.

## Development and verification

Use the .NET 10 SDK. `global.json` selects Microsoft.Testing.Platform for the toolkit's xUnit suite:

```powershell
dotnet restore "MCP UAsset Toolkit.slnx"
dotnet test --solution "MCP UAsset Toolkit.slnx"
dotnet test --solution "MCP UAsset Toolkit.slnx" --configuration Release
dotnet publish "MCP UAsset Toolkit.csproj" --configuration Release --runtime win-x64 --self-contained true
```

Tests link small fixtures from the pinned UAssetAPI submodule and create temporary `.pak` files. Initialize submodules before running them. Tests of optional installed binaries and symlink behavior may skip when their host prerequisites are absent. UAssetAPI's separate upstream test suite is not migrated into this solution; it retains upstream tooling. Avoid simultaneous builds of this checkout because the upstream project creates and deletes `git_commit.txt` during builds.
