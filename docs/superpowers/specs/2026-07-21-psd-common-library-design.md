# PSD Common Library Design

## Goal

Resolve `Common_Prefab_<Key>` and `Common_Texture_<Key>` PSD layers from a generated Unity Asset Catalog instead of exporting duplicate PSD pixels.

## Rules

- `Common_Prefab_<Key>` resolves an exact prefab key and creates a nested prefab instance. PSD owns its parent, position, size, and sorting; the common prefab owns internal hierarchy, scripts, animation, and styling.
- `Common_Texture_<Key>` resolves an exact Sprite/Texture key and creates a normal image/sprite renderer using the PSD layout.
- Missing configuration, missing key, or duplicate key is an import error. The importer never silently exports the PSD layer as a fallback image.
- Common layers do not export PNG files. A common prefab folder consumes its subtree, so children are not emitted a second time.
- Catalog refresh scans the project for public asset names beginning with `Common_Prefab_` and `Common_Texture_`, then stores exact key-to-GUID mappings. Daily PSD import reads only the catalog.

## Persistence

- `Assets/PSDLayoutTool2Settings/PsdCommonAssetCatalog.asset` stores direct Prefab and Sprite references and is versioned in Git.
- Unity GUIDs and current paths are stored in the catalog. `AssetPostprocessor` incrementally updates only Common-named added, changed, moved, renamed, or deleted assets; moving a resource keeps existing nested prefab and Sprite references valid. The explicit Generate/Refresh action remains the full-scan repair path.
- PSD `lyid` continues to control normal incremental import identity. No generated JSON mapping is added.

## Configuration

`Project Settings > PSD Layout Tool > Common Asset Catalog` generates or refreshes the catalog from Common-named resources anywhere in the project.

## Validation

- Pure tests cover exact Common naming parsing and ordinary names.
- Resolver validation rejects duplicate keys and wrong asset types.
- Unity runtime/import verification remains user-owned: generate a prefab containing both common prefixes after configuration.
