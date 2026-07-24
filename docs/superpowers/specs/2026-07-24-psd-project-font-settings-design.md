# PSD Project Font Settings Design

## Goal

Provide one project-wide TextMeshPro font configuration for every PSD import. The configuration must be shared through Git, editable from Unity, and independent of the currently selected PSD or developer machine.

## Scope

The first version contains exactly two settings:

- Default `TMP_FontAsset`
- Optional TMP base `Material`

Per-PSD overrides, scene Canvas references, output paths, scaling, anchors, and other importer options are outside this design.

## Storage

Add an editor-only `PsdLayoutProjectSettings` based on `ScriptableSingleton<T>` and persist it at:

`ProjectSettings/PSDLayoutTool2Settings.asset`

Store the selected assets as Unity GUID strings instead of absolute or project-relative paths. Resolve them through `AssetDatabase` when displaying the settings or starting an import. This keeps references valid when assets move inside the project and makes the settings file safe to share through Git.

Saving is explicit: when a user changes either field, update the GUID and call `Save(true)`.

## Editing Surfaces

Expose the same settings through two Unity surfaces:

1. `Project Settings > PSD Layout Tool > General`
2. A `Project Global Settings` section in the PSD custom Inspector

Both surfaces must call one shared GUI/settings adapter so labels, validation, persistence, and warnings cannot drift.

The PSD Inspector fields are project-global controls, not values owned by the selected PSD. The UI must state this clearly.

## Import Behavior

The importer resolves the project settings at the start of every import.

- Valid font GUID: use the referenced `TMP_FontAsset` for generated TMP text.
- Empty font GUID: use the normal TextMeshPro default-font fallback.
- Valid compatible base material GUID: use it as the source for generated text-effect materials.
- Empty, missing, or incompatible base material: use the selected font's material.
- Missing or invalid asset GUID: show a warning and follow the same safe fallback behavior.

The importer must not depend on whether a PSD Inspector is open. `EditorPrefs` font and material paths stop participating in import behavior.

Existing `EditorPrefs` values are left untouched but ignored. They are not automatically copied into project settings because one developer's local choice must not silently modify the shared project configuration.

## Error Handling

The settings UI displays a warning when a stored GUID cannot resolve to the expected asset type.

An invalid global setting must not abort PSD import. The importer logs one actionable warning for the import session and falls back to the default TMP font or selected font material.

## Verification

- A selected font and material survive Unity domain reload and editor restart.
- Moving either asset preserves the reference through its GUID.
- Both settings surfaces display and modify the same values.
- Importing without an open PSD Inspector uses the project settings.
- Empty and invalid GUIDs use the documented fallback behavior.
- An incompatible base material falls back to the font material.
- No font or material value is read from `EditorPrefs`.
- Existing PSD importer tests and Unity compilation complete without new errors.

## Non-Goals

- Per-PSD font selection
- Per-user font overrides
- Automatic migration from `EditorPrefs`
- Runtime settings or player-build inclusion
- Configuration of non-TMP Unity UI fonts
