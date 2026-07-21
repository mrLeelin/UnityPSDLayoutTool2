# PSD Common Library Design

## Goal

Resolve `Common_Prefab_<Key>` and `Common_Texture_<Key>` PSD layers from a configured Unity common library instead of exporting duplicate PSD pixels.

## Rules

- `Common_Prefab_<Key>` resolves an exact prefab key and creates a nested prefab instance. PSD owns its parent, position, size, and sorting; the common prefab owns internal hierarchy, scripts, animation, and styling.
- `Common_Texture_<Key>` resolves an exact Sprite/Texture key and creates a normal image/sprite renderer using the PSD layout.
- Missing configuration, missing key, or duplicate key is an import error. The importer never silently exports the PSD layer as a fallback image.
- Common layers do not export PNG files. A common prefab folder consumes its subtree, so children are not emitted a second time.
- The library index is built only from configured folder roots. It caches exact key-to-GUID mappings and is invalidated by asset changes under those roots.

## Persistence

- `Assets/PSDLayoutTool2Settings/PsdCommonAssetLibrary.asset` stores prefab and texture root folders and is versioned in Git.
- Unity GUIDs are stored and resolved by the AssetDatabase; moving a resource keeps existing nested prefab and Sprite references valid.
- PSD `lyid` continues to control normal incremental import identity. No generated JSON mapping is added.

## Configuration

`Project Settings > PSD Layout Tool > Common Asset Library` selects or creates the settings asset. The default library folders are `Assets/UI/Common/Prefabs` and `Assets/UI/Common/Textures`.

## Validation

- Pure tests cover exact Common naming parsing and ordinary names.
- Resolver validation rejects duplicate keys and wrong asset types.
- Unity runtime/import verification remains user-owned: generate a prefab containing both common prefixes after configuration.
