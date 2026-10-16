# Capabilities and compatibility

This map compares the toolkit with the data operations in [UAssetGUI](https://github.com/atenfyr/UAssetGUI), [UAssetAPI](https://github.com/atenfyr/UAssetAPI), [CUE4Parse](https://github.com/FabianFG/CUE4Parse), and the installed retoc CLI. It does not imply compatibility with every game or expose every low-level serializer method.

The complete exposed interface is listed in the [97-tool catalog](TOOLS.md). The [tool audit](TOOL_AUDIT.md) records what was added and the remaining library/runtime boundaries.

## Capability map

| Workflow | MCP support |
| --- | --- |
| Read headers, names, imports, exports, registry tags, world tile data | Existing inspection and search tools |
| Load engine/game-specific cooked assets and unversioned mappings | `open_asset` with typed options; `get_asset_load_options` lists flags/overrides |
| Add/edit primitive properties; copy exports | Existing property/structure tools |
| Edit nested structs, arrays, maps, DataTables, complex properties and export JSON | `read_asset_document`, `patch_asset_document`, `get_asset_document_schema` |
| Query and edit named DataTable rows | `list_data_table_rows`, `get_data_table_row`, `copy_data_table_row`, `set_data_table_row_property`, `remove_data_table_row` |
| Resolved package dependencies and mapping enums | `get_asset_dependencies`, `list_mapping_enums`, `get_mapping_enum`, `get_mapping_diagnostics` |
| Read/edit dependencies, soft package references, package fields and custom versions | Resolved dependency reports plus JSON pointers into the corresponding document members |
| Atomic, guarded, and multi-asset edits | `patch_asset_document_batch`, `batch_patch_assets`, `batch_validate_assets`; `clone_asset_session` starts a new package from a loaded template |
| Raw export bytes, extra bytes, script bytecode | `get_export_bytes`, `set_export_bytes`; script writes require logical `scriptBytecodeSize` |
| Replace names throughout references | `replace_name_references` preserves name-map positions |
| Verify serialization and original binary equality | `validate_asset` checks a copy; edited assets normally differ from disk |
| Missing-name repair on save | Bounded UAssetGUI-style retry using the missing name reported by UAssetAPI |
| Reload/discard/save-as | Session tools; explicit discard for dirty sessions; save-as keeps the source dirty |
| Inspect mappings and patch mapping version metadata | Schema listing/details, diagnostic JSON export, and version-patched `.usmap` copies |
| Browse/mount `.pak` and IoStore containers | CUE4Parse archive sessions; encrypted containers may need user-supplied AES keys and mappings |
| Inspect individual archive objects and provider options | `list_archive_objects`, `read_archive_object`, `get_archive_file_info`, `get_archive_read_options`, `configure_archive_read_options` |
| Extract and repack `.pak` | Extract to a staging directory, edit files, then `create_pak_from_directory` |
| Inspect/verify and convert IoStore | Optional retoc tools with explicit engine-version and override options |
| IoStore raw chunk, manifest, registry, and script-object workflows | Retoc unpack/pack/get, manifest, registry, and script-object tools |
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

## Atomic and bulk edits

`patch_asset_document_batch` accepts 1-100 ordered operations. It supports `add`, `replace`, `remove`, `copy`, `move`, and `test`. `copy`/`move` take a `from` pointer; `add`/`replace`/`test` take `valueJson`. The complete batch is validated on a copy before committing. Invalid pointers, failed tests, protected metadata, serializer-dropped values, or serialization failures leave the original asset and dirty state intact.

```json
{
  "filePath": "C:\\Game\\Content\\Example.uasset",
  "operations": [
    { "pointer": "/FolderName", "operation": "test", "valueJson": "\"Original\"" },
    { "pointer": "/FolderName", "operation": "replace", "valueJson": "\"Edited\"" }
  ],
  "preview": true
}
```

`preview: true` validates without changing the session. `batch_patch_assets` applies the same transaction to 1-50 explicit asset paths and defaults to preview. Each asset succeeds or fails independently; inspect `succeededCount`, `failedCount`, and each result. `batch_validate_assets` checks serialization for the same bounded input shape. These tools never save automatically. Batch input is capped at one million characters; duplicate normalized paths are rejected.

`clone_asset_session` deep-copies a loaded template into a new dirty session, retaining engine/mapping/parser context. It refuses existing disk or session targets and does not write until `save_asset` is called. Names and package contents remain those of the template until explicitly edited.

## DataTables, dependencies, and mappings

DataTable tools use a 1-based export index and exact, case-sensitive row/property names. Row searches use case-insensitive substrings and pages of up to 1000 rows. Reads include typed row JSON with a configurable cap of up to one million characters. Copying a row preserves its schema and creates independent property data; existing names are refused. Property updates change existing primitive values while preserving enum representation, FText history, localization keys, and property metadata. Complex FText histories and nested values remain available through document editing. New properties are not inserted by the primitive row-update tool. All mutations validate a copy and require an explicit save.

`get_asset_dependencies` resolves signed import/export indices from headers, `DependsMap`, export serialization-order lists, and package references. Null and invalid indices are reported directly. This is an inspection report; it does not rewrite package indices or recursively load other assets.

Mapping enum tools provide deterministic name/value pagination. Diagnostics expose version information, changelist, case sensitivity, failed extensions, and schema/module/enum counts without changing the mapping file. Mapping JSON export remains diagnostic and lossy.

## Archive workflow

1. `open_archive_session` with the directory containing `.pak` or `.utoc/.ucas` files and the CUE4Parse game enum, for example `GAME_UE5_4`. Set `recursive` only when needed.
2. Inspect the returned mounted/unmounted archives and required key GUIDs. Use `submit_archive_key` when a key is required. Responses do not include key values.
3. Use `list_archive_files` with `query`, `offset`, and `limit` to find virtual paths.
4. `read_package_exports` reads parsed export JSON. A size-blocked export gives an actionable response; extract it when JSON is too large.
5. `extract_archive_file` writes beneath an explicit output directory and includes available package companions. It refuses existing targets unless `overwrite` is true, and rejects unsafe paths/reparse points.
6. Edit loose assets with UAssetAPI tools and save them. Use `create_pak_from_directory` to build a new `.pak` from the staging directory.

Use `list_archive_objects` for a package's object names/types and `read_archive_object` for one exact object name, avoiding a full JSON export response. `get_archive_file_info` reports virtual-file and available backing-container metadata. Object JSON has a configurable output cap; oversized objects return an actionable blocked response. `configure_archive_read_options` changes only the selected provider's script/shader/Nanite/reference/lazy-read flags; omitted nullable options preserve current values. Configure them before loading packages. `get_archive_read_options` reports their current state.

Extraction's byte limit is an output limit. CUE4Parse may allocate decompressed package/companion buffers before the final total can be checked. Large or malformed archives therefore still require appropriate process memory limits.

## Optional retoc

`retoc_status` checks `retoc.exe` on Windows `PATH`, or an explicit `retocPath`. No automatic download or installation occurs. Missing/unlaunchable retoc responses explain how to install it from [official retoc releases](https://github.com/trumank/retoc/releases). The integration was inspected against **retoc_cli 0.1.5**.

- `inspect_iostore`: `info`, `list`, or `verify` for a `.utoc`. List options include hashes, sizes, paths, package IDs, and store entries.
- `convert_to_legacy`: `.utoc` or Paks directory to a **new `.pak`**; optional filters, engine override, shader/asset skip flags, and dry run.
- `convert_to_zen`: legacy `.pak` or directory to new `.utoc`, `.ucas`, and `.pak` files; requires `engineVersion`, for example `UE5_4`.
- `extract_iostore_manifest`: runs retoc's fixed-filename manifest writer in an isolated temporary working directory and returns up to 64 KiB of manifest text. `truncated` reports an output cap.
- `inspect_asset_registry` and `print_script_objects`: bounded read-only metadata output.
- `unpack_iostore` and `unpack_iostore_raw`: write files or raw chunks to a new output directory.
- `pack_iostore_raw`: builds `.utoc`/`.ucas` from retoc's raw `manifest.json` plus `chunks/<24-hex-id>` format.
- `get_iostore_chunk`: writes one validated 24-hex-character chunk ID to a new file.
- `generate_script_objects`: builds a script-object container from a **trumank/jmap reflection dump**, distinct from UAssetAPI property mapping JSON, and an explicit engine version.

Conversion tools accept an `options` object for `retocPath`, AES key, supported container/header overrides, and timeout. They stage output beside the destination and refuse existing output files. Failed conversions retain staging files for diagnosis/recovery; canceled conversions attempt staging cleanup after stopping the process. The subprocess uses argument lists instead of shell command strings, drains output with a cap, and requests process-tree termination on timeout/cancellation. Termination failures are reported explicitly.

The same options and staging rules apply to new retoc workflows. Options specific to a different command are rejected before creating outputs. Multi-file publication rolls back earlier moves if a later target conflicts. New unpack directories must not exist; `pack_iostore_raw` refuses outputs inside its input tree. Retoc extraction is subprocess-driven: its timeout/output-text cap does not impose a decompressed disk-byte quota. Native archive compatibility and disk sizing remain properties of the selected container and retoc version.

Native IoStore/chunk compatibility is determined by retoc and the game. Game-specific AES values, mappings, Oodle prerequisites, and version overrides may still be needed. The tools report subprocess errors rather than treating an unsuccessful conversion as a completed file operation.

## Development and verification

Use the .NET 10 SDK. `global.json` selects Microsoft.Testing.Platform for the toolkit's xUnit suite:

```powershell
dotnet restore MCP.UAssetToolkit.slnx
dotnet test --solution MCP.UAssetToolkit.slnx
dotnet test --solution MCP.UAssetToolkit.slnx --configuration Release
dotnet publish src/MCP.UAssetToolkit/MCP.UAssetToolkit.csproj --configuration Release --runtime win-x64 --self-contained true
```

Tests link small fixtures from the pinned UAssetAPI submodule and create temporary `.pak` files. Initialize submodules before running them. Tests of optional installed binaries and symlink behavior may skip when their host prerequisites are absent. UAssetAPI's separate upstream test suite is not migrated into this solution; it retains upstream tooling. Avoid simultaneous builds of this checkout because the upstream project creates and deletes `git_commit.txt` during builds.

`./eng/Verify.ps1` runs restore plus explicit builds and tests for Debug and Release. Use `-Configuration Debug` or `-Configuration Release` for one configuration and `-Publish` to add a self-contained Release publish to `artifacts/publish/Release`.

Application code lives in `src/MCP.UAssetToolkit`, with `Core/` and `Tools/` grouped by feature. Tests mirror those areas under `Tests/MCP.UAssetToolkit.Tests`. See [architecture](ARCHITECTURE.md) for the boundaries and shared build configuration.
