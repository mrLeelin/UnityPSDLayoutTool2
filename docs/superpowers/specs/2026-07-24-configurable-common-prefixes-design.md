# Configurable Common Asset Prefixes Design

## Goal

Move the hard-coded `Common_Prefab_` and `Common_Texture_` naming contracts into the existing project-wide PSD Layout Tool settings.

Store those settings in a project-owned ScriptableObject asset that can be selected and edited directly in Unity's Inspector when this tool is installed through UPM.

## Behavior

- Keep `Common_Prefab_` and `Common_Texture_` as backward-compatible defaults.
- Expose separate prefab and texture prefix fields in `Project Settings > PSD Layout Tool > General`.
- Automatically append `_` when a non-empty configured prefix omits it.
- Treat blank values as requests to restore the corresponding default.
- Reject identical prefab and texture prefixes because the asset kind would be ambiguous.
- Use the same normalized prefixes for PSD layer parsing, catalog scanning, and incremental catalog updates.
- Mark an existing Common Asset Catalog stale when either prefix changes; the catalog must be refreshed before import.
- Ship a read-only default settings template inside the UPM package.
- On first use, copy that template to `Assets/PSDLayoutTool2Settings/PsdLayoutProjectSettings.asset` and use only the project copy afterward.
- Never overwrite an existing project copy when the package is updated.
- Migrate values from the legacy `ProjectSettings/PSDLayoutTool2Settings.asset` file during the first copy when that file exists.
- The PSD Inspector shows only an `Open Global Settings` command; font, material, and Common prefix fields are edited exclusively on the settings asset Inspector.

## Documentation

The settings UI and code comments show complete examples such as `UI_Prefab_Button_Green` and `UI_Texture_Lock`, and explain that the suffix after the configured prefix becomes the catalog key.

## Verification

- Editor tests cover defaults, normalization, invalid duplicate prefixes, and custom parsing.
- Unity compilation and the focused Editor test assembly must pass.
