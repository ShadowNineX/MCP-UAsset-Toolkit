# Useful-tool audit

The audit compared the original 71 MCP tools with the pinned UAssetAPI source, installed CUE4Parse APIs, and installed `retoc_cli 0.1.5` command help/source. The server now exposes **97 tools**. [TOOLS.md](TOOLS.md) lists every tool; schema tests compare that catalog against live assembly discovery.

| Area | Added workflows | Verification |
| --- | --- | --- |
| DataTables | Named row listing/reading/copying, existing primitive-value updates, and row removal | Real UE4.18 DataTable fixture; independent row copies; save/reload; enum/FText metadata; invalid request rollback |
| Asset documents | Ordered atomic add/replace/remove/copy/move/test batches; preview; per-file bulk patch/validation; template-session cloning | Real asset fixture; late-failure rollback; protected metadata/type allowlist; untouched subtree checks; no implicit disk writes |
| Dependencies | Signed-index resolution for headers, dependency maps, serialization ordering, soft and extra package references | Explicit valid/invalid/null references, filtering/pagination, JSON output, clean session state |
| Mappings | Enum search/value queries and parser diagnostics | Pinned `.usmap` fixture, numeric/name pagination, missing mappings/enums, diagnostic fields |
| Archives | Targeted object reads, object metadata pages, file/container information, provider read options | Real loose Unreal package fixture, non-package refusals, JSON caps, option defaults/session isolation |
| Retoc | Manifest, asset-registry, script-object inspection, unpack/raw unpack/raw pack, chunk extraction, script-object generation | Controlled subprocess fixtures for exact arguments, output publication/rollback, timeout, cancellation, and key redaction; installed-retoc raw chunk roundtrip |

Existing tool inputs, defaults, and schemas are retained. Before/after local MCP discovery compared every original schema and representative version/resource responses. The structural migration retains the existing assembly name and public namespaces; UAssetAPI remains pinned and unmodified.

## Already covered without additional wrappers

Headers, names, imports, exports, primitive properties, raw/extras/script bytes, custom versions, soft paths, registry tags, JSON import/export, validation, session lifecycle, mapping schemas, archive extraction/pak creation, and IoStore conversion were already exposed. Nested document operations cover structs, arrays, maps, complex DataTable values, and unusual package fields. Those fields do not each need a separate low-level serializer tool.

## Boundaries

- UAssetAPI's empty constructors create parser contexts, not initialized game packages. New packages can start from validated JSON import or a loaded template clone. A generic blank-package wizard would need game-specific construction rules.
- Texture/audio/mesh rendering and conversion require separate exporters/conversion dependencies and richer native fixtures. The current dependencies expose parsed JSON and original payload extraction. No new media dependency was added.
- Retoc's ordinary useful installed commands are covered. Its internal `dump-test` command has no documented stable input/output contract and is not exposed. Help/version are represented by descriptions and `retoc_status`.
- Archive object/provider reads depend on game versions, mappings, keys, and native compression support. Successful package serialization does not prove target-game compatibility.
- Fake-process tests prove script-object generation and metadata command wiring/publication. The native success test qualifies raw-container pack/unpack/get for a tiny fixture; it does not qualify every game, reflection dump, or registry format. Malformed native metadata inputs are checked as refusal paths.

## Adding another tool

Add the workflow beside its feature in `Core/` and `Tools/`, bound its input/output, preserve existing public contracts, and test its useful success path and failure side effects. Update [TOOLS.md](TOOLS.md) and the relevant capability notes. `CatalogIncludesEveryDiscoveredToolExactlyOnce` prevents undocumented tool additions/removals.
