# Changelog

All notable changes to this package are documented in this file.

## [0.1.2] - 2026-07-24

### Added

- Added a project-owned PSD Layout Tool settings asset copied from the UPM package template on first use.
- Added configurable Common Prefab and Common Texture naming prefixes.
- Added a dedicated global settings window accessible from the PSD Inspector.

### Changed

- Moved shared output paths, TMP font, TMP material, and Common naming controls out of the PSD Inspector.
- PSD imports and hierarchy tools now resolve shared output rules directly from the project settings asset.
- Existing project settings are preserved when the package is upgraded.

## [0.1.1] - 2026-07-24

### Changed

- Moved the canonical Unity Package Manager manifest to the repository root.
- Git installation no longer requires the `?path=/Assets/PSDLayoutTool2` query.
- Kept the existing source and assembly layout intact to preserve Unity asset GUIDs.

## [0.1.0] - 2026-07-24

### Added

- Added a Unity Package Manager manifest for Git, disk, and tarball installation.
- Split runtime, editor, and editor-test code into dedicated assemblies.
- Declared the uGUI, Newtonsoft Json, and Unity Test Framework package dependencies.
- Added package installation and usage documentation.
