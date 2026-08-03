# Changelog

All notable changes to this package are documented in this file.

## [0.1.7] - 2026-08-03

### Changed

- Allowed the configured PSD button component to inherit directly from `MonoBehaviour`, so projects can use custom touch handlers without deriving from `UnityEngine.UI.Button`.
- Kept native Button sprite-state generation for `UnityEngine.UI.Button` configurations while preserving normal layer and child import for custom touch behaviours.

### Added

- Added EditMode coverage for resolving and attaching a custom `MonoBehaviour` button component.

## [0.1.6] - 2026-08-02

### Fixed

- Made full PSD regeneration replay confirmed cleanup stages deterministically, preserving the main Prefab and its Common Prefab references across repeated delete-and-generate runs.
- Recognized generated flat-sibling wrappers as reusable component candidates while excluding nested Prefab contents and duplicate root containers.
- Retried the transient "Unity server is starting" payload-compilation state with bounded backoff instead of leaving a replay pending until a later domain reload.
- Prevented generated empty wrappers from failing tight-bounds cleanup, while retaining the safety check for authored empty containers.

## [0.1.5] - 2026-07-31

### Fixed

- The Common Asset Catalog refresh action now highlights the generated catalog without changing the active PSD Inspector.
- The incremental update action remains visible when unavailable and is disabled until a valid preservation profile exists.

### Removed

- Removed the optional Photoshop UXP metadata exporter; Unity-native PSD import remains the supported workflow.

## [0.1.4] - 2026-07-29

### Fixed

- Resolved AI hierarchy cleanup skills and runner scripts from the installed package location, so renamed, nested, and UPM package installs no longer depend on `Assets/UnityPSDLayoutTool2`.
- Added a path-resolution regression test for packages nested under a renamed Assets directory.

## [0.1.3] - 2026-07-24

### Added

- Added semantic hierarchy grouping, candidate analysis, and safer incremental Prefab adoption.
- Added Photoshop text-transform, shadow, and material conversion coverage.
- Added Common Texture visual-transform matching for public replacement sprites.

### Changed

- Improved hierarchy-plan validation, visual-leaf verification, and generated text-material synchronization.
- Removed generated Superpowers planning and specification artifacts from the package repository.

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
