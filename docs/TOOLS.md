# MCP tool catalog

The server exposes **97 tools** and the `engine://version` resource. This catalog is generated from live MCP discovery; schema tests require every discovered tool to appear exactly once. See [capabilities](CAPABILITIES.md) for workflows, limits, and compatibility.

Required inputs are listed below. Optional inputs, defaults, descriptions, and typed objects are provided by each tool's MCP input schema. Asset edits stay in memory until `save_asset` is called unless a tool explicitly writes a new output file.

| Tool | Required inputs | Purpose |
| --- | --- | --- |
| `add_custom_version` | `filePath`, `guidString`, `version` | Add a custom version GUID and version number to the asset header |
| `add_export` | `filePath`, `objectName` | Add a new export (e.g. NormalExport) to the asset |
| `add_import` | `filePath`, `classPackage`, `className`, `outerIndex`, `objectName` | Add a new import reference to the asset |
| `add_name_map_entry` | `filePath`, `name` | Add a new entry to the name map in memory |
| `add_soft_object_path` | `filePath`, `assetPathName` | Add a soft object path to the asset header |
| `batch_patch_assets` | `filePaths`, `operations` | Preview or apply the same ordered JSON patch transaction to 1-50 explicit asset paths. Preview defaults to true. Each asset commits independently; inspect per-file errors. Never saves to disk. |
| `batch_validate_assets` | `filePaths` | Validate serialization for 1-50 explicit asset paths, returning each success or error. Does not edit or save assets. |
| `clear_all_sessions` | `` | Unload all sessions. If any has unsaved changes, none are unloaded unless discardChanges=true. |
| `clone_asset_session` | `filePath`, `targetFilePath` | Deep-copy an existing loaded asset/template to a new dirty session, preserving parser context. Target must not already exist on disk or in sessions. No disk writes; save_asset persists it. |
| `close_archive_session` | `sessionId` | Close and dispose an archive session. |
| `close_session` | `filePath` | Close an asset session. Unsaved changes require explicit discardChanges=true. |
| `configure_archive_read_options` | `sessionId` | Configure only the supplied CUE4Parse read flags for one archive session. Omitted nullable flags retain the provider's current defaults or settings. |
| `convert_to_legacy` | `inputPath`, `outputPakPath` | Convert a Zen IoStore input (.utoc or Paks directory) to a new legacy .pak. Existing output is refused; the conversion stages beside the output and leaves inputs untouched. |
| `convert_to_zen` | `inputPath`, `outputUtocPath`, `engineVersion` | Convert a legacy .pak or directory to new .utoc, .ucas, and .pak outputs. engineVersion is required in retoc form, such as UE5_4. Existing outputs are refused. |
| `copy_data_table_row` | `filePath`, `exportIndex`, `sourceRowName`, `newRowName` | Deep-clone an existing DataTable row to a new exact row name. The new name must not already exist. |
| `copy_export` | `filePath`, `sourceExportIndex`, `newObjectName` | Copy/duplicate an existing export within the asset |
| `create_pak_from_directory` | `inputDirectory`, `outputPakPath` | Create an unencrypted .pak from a directory for extract-edit-rebuild workflows. PakVersion defaults to V11. Use the dedicated retoc tools for IoStore .utoc/.ucas rebuilds. |
| `edit_export_info` | `filePath`, `exportIndex`, `update` | Edit export header fields using an update object. Omitted fields retain their current values. |
| `edit_import` | `filePath`, `importIndex` | Edit import data fields in memory |
| `edit_name_map_entry` | `filePath`, `index`, `newName` | Edit a name map entry at a specific index in memory |
| `export_asset_to_json` | `filePath` | Export complete .uasset to UAssetAPI JSON format string |
| `export_mappings_json` | `outputPath` | Export mappings to diagnostic JSON. This is a lossy visualization, not reloadable .jmap/.usmap. Existing output is preserved unless overwrite=true. |
| `export_to_json_file` | `filePath`, `jsonOutputPath` | Export .uasset to a .json file on disk |
| `extract_archive_file` | `sessionId`, `virtualPath`, `outputDirectory` | Extract a virtual file or package and its .uexp/.ubulk companions into an explicit output directory. Existing targets are refused unless overwrite=true. |
| `extract_iostore_manifest` | `utocPath` | Extract and return the manifest inventory from a .utoc IoStore container. This is read-only. |
| `generate_script_objects` | `jmapPath`, `outputUtocPath`, `engineVersion` | Generate a new script-objects .utoc and matching .ucas from a .jmap reflection dump. Existing outputs are refused and output is staged before publication. |
| `get_archive_file_info` | `sessionId`, `virtualPath` | Get virtual-file metadata including size, compression, encryption, package state, and source container metadata when available. |
| `get_archive_read_options` | `sessionId` | Get the current CUE4Parse read flags for one archive session. |
| `get_asset_dependencies` | `filePath` | List resolved package dependencies from asset headers, dependency maps, export serialization order lists, and package references. |
| `get_asset_document_schema` | `` | List exact UAssetAPI $type strings, including the required ', UAssetAPI' qualifier, that may be introduced in asset document JSON. |
| `get_asset_info` | `filePath` | Get basic information about a .uasset file |
| `get_asset_load_options` | `` | List supported advanced UAssetAPI load flags and game-specific overrides. |
| `get_asset_registry_records` | `filePath` | Get asset registry records and searchable tags embedded in the asset |
| `get_data_table_row` | `filePath`, `exportIndex`, `rowName` | Read one DataTable row by its exact row name, including a bounded typed UAssetAPI JSON representation and readable property values. |
| `get_export_bytes` | `filePath`, `exportIndex` | Read a bounded slice of export binary data as base64. Section: raw, extras, or script. Export index is 1-based; byte offset is 0-based. |
| `get_export_data` | `filePath`, `exportIndex` | Get detailed data for a specific export by index (1-based) |
| `get_export_properties` | `filePath`, `exportIndex` | Get all property data for a specific export (1-based) as structured JSON |
| `get_full_asset_info` | `filePath` | Get comprehensive metadata about a .uasset file (similar to UAssetGUI Info tab) |
| `get_gatherable_text_data` | `filePath` | Get gatherable text localization data stored in the asset |
| `get_general_info` | `filePath` | Get general information about a .uasset file |
| `get_iostore_chunk` | `inputPath`, `chunkId`, `outputPath` | Extract one 24-hex-character IoStore chunk ID to a new output file. Existing output files are refused and output is staged before publication. |
| `get_mapping_diagnostics` | `` | Get mapping parser diagnostics, optionally for an explicit mappings file. |
| `get_mapping_enum` | `enumName` | Inspect a mapping enum and its numeric values with pagination. Supply mappingsPath or use the global mappings. |
| `get_mapping_schema` | `schemaName` | Inspect a mapping schema and its inherited property layout, including type details for arrays, maps, structs and enums. |
| `get_mappings_info` | `` | Get information about currently loaded property mappings |
| `get_name_map` | `filePath` | Get the name map (string table) from a .uasset file |
| `get_soft_object_paths` | `filePath` | Get list of soft object paths stored in the asset header |
| `get_supported_engine_versions` | `` | Get list of all supported Unreal Engine versions |
| `get_world_tile_info` | `filePath` | Get World composition tile info if present in the asset |
| `import_asset_from_json` | `jsonContent`, `targetFilePath` | Import asset from UAssetAPI JSON string and load into session or save as .uasset |
| `import_from_json_file` | `jsonInputPath`, `targetFilePath` | Import asset from a .json file on disk and compile into .uasset file |
| `inspect_asset_registry` | `assetRegistryPath` | Parse and return an Unreal AssetRegistry.bin file through retoc. This is read-only. |
| `inspect_iostore` | `utocPath` | Run exactly one read-only retoc IoStore inspection action: info, list, or verify. Requires a .utoc input path. |
| `list_active_sessions` | `` | List all currently active in-memory asset sessions and dirty states |
| `list_archive_files` | `sessionId` | List or search mounted virtual files. Results are paginated; limit defaults to 100 and is at most 1000. |
| `list_archive_objects` | `sessionId`, `virtualPath` | List object name and type metadata in one Unreal package. Results are paginated; limit defaults to 100 and is at most 1000. |
| `list_archive_sessions` | `` | List active CUE4Parse archive sessions, including mounted and unmounted archive metadata and required key GUIDs. AES key values are never returned. |
| `list_custom_versions` | `filePath` | List all custom versions in a .uasset file |
| `list_data_table_rows` | `filePath`, `exportIndex` | List DataTable rows by exact export index, with optional case-insensitive name search and pagination. |
| `list_exports` | `filePath` | List all exports in a .uasset file |
| `list_imports` | `filePath` | List all imports in a .uasset file |
| `list_mapping_enums` | `` | Search mapping enums by name with pagination. Supply mappingsPath or use the global mappings. |
| `list_mapping_schemas` | `` | Search loaded mapping schema names with pagination. Supply mappingsPath or use the global mappings. |
| `load_mappings` | `mappingsFilePath` | Load a .usmap or .jmap/.jmap.gz unversioned property mapping file globally |
| `open_archive_session` | `directory`, `game` | Open an Unreal archive or loose-package directory with CUE4Parse. game must be a CUE4Parse EGame value such as GAME_UE4_27. |
| `open_asset` | `filePath` | Load an asset with per-file engine version, mappings, serialization flags, game override, and map/array struct types. Existing sessions are reused unless forceReload=true. |
| `pack_iostore_raw` | `inputDirectory`, `outputUtocPath` | Pack a directory of raw IoStore chunks into new .utoc and .ucas files. Existing outputs are refused and output is staged before publication. |
| `patch_asset_document` | `filePath`, `pointer`, `operation` | Atomically replace, add, or remove an UAssetAPI JSON member or array entry using an RFC 6901 JSON pointer. valueJson is required except for remove. |
| `patch_asset_document_batch` | `filePath`, `operations` | Apply 1-100 ordered JSON edits atomically to one asset, or validate a preview without changing the session. Supports add/replace/remove/copy/move/test; no disk writes. |
| `patch_mappings_version` | `mappingsPath`, `outputPath`, `engineVersion` | Copy a .usmap and patch the copy with an Unreal engine version. The source is preserved; output must be a new file. |
| `print_script_objects` | `utocPath` | Print script objects from a .utoc IoStore container. This is read-only. |
| `read_archive_object` | `sessionId`, `virtualPath`, `objectName` | Read one exactly named object from an Unreal package as bounded JSON. virtualPath and objectName are exact selections; maxJsonCharacters is at most 10000000. |
| `read_asset` | `filePath` | Read and parse a .uasset file to access its structure |
| `read_asset_document` | `filePath` | Read any UAssetAPI JSON subtree using an RFC 6901 JSON pointer. Use an empty pointer for the whole asset. |
| `read_package_exports` | `sessionId`, `virtualPath` | Read parsed package exports as JSON strings. Results are paginated and bounded by maxJsonCharacters (at most 10000000). |
| `reload_session` | `filePath` | Reload an asset from disk using its session engine version and mappings path. Unsaved edits require discardChanges=true. |
| `remove_custom_version` | `filePath`, `guidString` | Remove a custom version by GUID string |
| `remove_data_table_row` | `filePath`, `exportIndex`, `rowName` | Remove a DataTable row by its exact row name. |
| `remove_export` | `filePath`, `exportIndex` | Remove an export by index (1-based) |
| `remove_export_property` | `filePath`, `exportIndex`, `propertyName` | Remove a property from a NormalExport by property name |
| `remove_import` | `filePath`, `importIndex` | Remove an import by index (1-based) |
| `remove_soft_object_path` | `filePath`, `index` | Remove a soft object path from the asset header by index |
| `replace_name_references` | `filePath`, `oldName`, `newName` | Replace all matching name-map strings without shifting indices, updating references that use those entries. |
| `retoc_status` | `` | Check whether the optional retoc CLI is available and report its version. retoc is not downloaded or installed by this tool. |
| `save_asset` | `filePath` | Save a modified .uasset file in active session to disk |
| `search_exports` | `filePath`, `query` | Search exports by object name or class name |
| `search_imports` | `filePath`, `query` | Search imports by object name, class name, or class package |
| `search_name_map` | `filePath`, `query` | Search the name map by substring or regular expression |
| `set_asset_registry_tag` | `filePath`, `recordIndex`, `tagKey`, `tagValue` | Set or add a searchable asset registry tag on an asset registry record |
| `set_data_table_row_property` | `filePath`, `exportIndex`, `rowName`, `propertyName`, `valueJson` | Update an existing primitive property in a DataTable row by exact row and property names. This tool does not insert unknown properties. |
| `set_engine_version` | `version` | Set the default Unreal Engine version for asset parsing |
| `set_export_bytes` | `filePath`, `exportIndex`, `base64` | Replace an export's complete raw, extras, or script byte buffer with base64 data (maximum 16 MiB). Script requires its logical scriptBytecodeSize and clears parsed bytecode. Save explicitly to persist. |
| `set_export_property` | `filePath`, `exportIndex`, `propertyName`, `propertyType`, `valueJson` | Set, add, or update a property value on a NormalExport by property name |
| `submit_archive_key` | `sessionId`, `keyGuid`, `aesKey` | Submit an AES key for a required archive GUID and mount matching encrypted archives. The key is never returned. |
| `unpack_iostore` | `utocPath`, `outputDirectory` | Extract IoStore files from a .utoc container to a new output directory. Existing directories are refused and output is staged before publication. |
| `unpack_iostore_raw` | `utocPath`, `outputDirectory` | Extract raw IoStore chunks from a .utoc container to a new output directory. Existing directories are refused and output is staged before publication. |
| `validate_asset` | `filePath` | Test binary serialization on a copy and compare it with disk, without saving or changing the session. Differences after edits are expected. |
