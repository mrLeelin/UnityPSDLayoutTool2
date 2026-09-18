# Changelog

All notable changes to this package are documented in this file.

## [0.2.0] - 2026-09-18

### Added

- Added the terminal apply sentinel flow. After a human approves a version 2 plan in the terminal, the AI writes `<session>.apply`; Unity claims it by renaming it to `.applying`, applies it through the shared native core, and writes `<session>.apply-result.json` with `success`, `status`, and `message`, so the terminal AI can read validation errors and revise the plan itself. The receipt distinguishes `applied`, `rejected`, `partial`, and `uncertain`, and the plan content is hashed at claim time, so a domain reload, crash, or failed rename can never re-run an already accepted request.
- Added version 2 replay binding evidence. Every saved replay stage now records the referenced nodes' observable facts (`path`, `name`, `siblingIndex`, `components`), and a replay must prove a unique correspondence on the regenerated snapshot before it executes. A matching positional `node:<id>` number or fingerprint no longer counts as proof, and a version 1 plan or an evidence-free stage permanently requires re-analysis.
- Added `postGroupingExtractionIntents`. An approved first stage can carry second-stage sub-Prefab extraction intents described by **post-grouping** paths; Unity saves and re-verifies the hierarchy stage, refreshes the authoritative snapshot, rebuilds the extraction plan from those intents, and applies it without asking the AI for a second confirmation.
- Added texture and SpriteAtlas renames (`textureRenames`, `spriteAtlasRenames`) to the native executor. Renames check asset identity, ownership, target conflicts, and the naming convention, and keep the asset GUID so Prefab references stay valid.
- Added per-developer local settings at `UserSettings/PsdLayoutTool2/user-settings.json` (AI CLI configuration, preview port, nine-slice marker visibility), so the shared `PsdLayoutProjectSettings.asset` no longer mixes team pipeline configuration with personal preferences. First use seeds the file and clears those fields from the shared asset.
- Added settings web server auto-resume. The `localhost:9528` page survives a script domain reload through `SessionState` and comes back silently after compilation, without reopening the browser.
- Added the `PsdLayoutTool2.TestSupport` assembly with extraction reference and value probes, plus version 2 EditMode coverage for basic cleanup, component/state/variant/stateful/selected/cross-parent/post-grouping extraction, replay, asset renames, terminal apply end-to-end and its watcher, local user settings, and replay-profile persistence failures.

### Changed

- Made the version 2 `node:<id>` plan the only formal write path (ADR 0001/0002). Cleanup execution is fixed to Native Unity, and historical `cleanupBackend` values are normalized instead of rejected so older settings pages keep saving.
- Moved the extraction and execution implementation out of `PsdHierarchyChatCleanupExecution` into dedicated native modules, so terminal sessions and the in-editor workflow share one core.
- Retired `render_prefab_cleanup.py --mode apply|reapply` and the PowerShell `-ApplyConfirmed` write path; the skill scripts are now read-only verification and diagnostics, and a version 1 path plan is diagnostics only.
- Updated the `prefab-hierarchy-cleanup` skill to state the sentinel flow as the authoritative capability, to enumerate the operations the executor actually supports, and to require every other operation array to be empty.
- Replaced the Inspector "应用AI计划" button with `AI整理` and `AI提示词复制`; the copied prompt and the terminal contract now document the `.apply` sentinel and the `apply-result.json` receipt the AI must poll.
- Made second-stage extraction Unity-owned: the executor re-checks candidate coverage against the refreshed snapshot and only adds the `componentFamilyDecisions` entries that snapshot requires.
- Hardened settings writes so clearing AI settings, toggling nine-slice markers, and changing the preview port report validation or personal-file write errors instead of silently rewriting the shared asset.
- Asset renames now report the renames already completed when a later one fails, instead of rolling back or retrying.

### Removed

- Removed `PsdHierarchyNativePayloadExecutor` and its `Process.Start("python", render_prefab_cleanup.py)` payload path.
- Removed the legacy runner entry points (`RequiresUnityCliRunner`, `TryValidatePlanCapabilities`, `BuildReapplyPreflightPlan`, and the context-free `Validate`/`Apply`); `PsdHierarchyLegacyRemovalTests` locks them out.
- Removed containment and flat-sibling auto-repair. `containmentResolutions`, `flatSiblingResolutions`, `selectedPrefabExtractions`, and `crossParentPrefabExtractions` must now be empty, and a non-empty array is refused before any write.

## [0.1.9] - 2026-09-16

### Fixed

- Fixed PSD group hierarchy construction for full Prefab generation. Art layers that Photoshop marked with the pixel-irrelevant flag (but still carry a non-empty bounds and real image data) are no longer treated as folder starts; those false starts swallowed following siblings, inflated group layout rects, and made the generated hierarchy diverge from the PSD panel and import preview.
- `IsEndGroup` now prefers the `lsct` SectionType=3 bounding marker before falling back to `</Layer set>` / `</Layer group>` name matching, so group closes are reliable even when the end divider keeps a normal folder name.
- Unclosed groups at end-of-file are flushed while preserving parent-child nesting instead of being dumped as flat roots.

### Added

- Full import logs now include an indented layer-tree dump after `BuildLayerTree`, so hierarchy regressions can be compared against the PSD panel directly from `Library/PSDLayoutTool2/Logs`.

## [0.1.8] - 2026-09-16

### Added

- Added a browser-based project settings page (`PsdLayoutProjectSettingsWebServer`) so shared output paths, TMP, Common naming, and AI hierarchy options can be edited outside the IMGUI window. The page listens on `localhost:9528` and keeps all writes on the Unity main thread.
- Added nine-slice exported-border lookup and applier so imported sprites can keep Photoshop/PSD border data instead of relying only on pixel inference.
- Added nine-slice observability diagnostics and source-PSD path resolution, so a sliced Image can report its border and recover the originating PSD from common export-folder conventions.
- Added a persistent hierarchy-plan conflict surface: failed AI analysis is retained as a reviewable workspace with recovery actions, instead of vanishing as a chat-only error.
- Added AI hierarchy organizer provider-secret storage and a web review workflow for organizing generated UI Prefabs.
- Added real UI Prefab preview rendering in the Common Asset Library (Canvas graphics no longer rely on `AssetPreview`).

### Fixed

- Fixed the Common Asset Library copy control on LAN origins: clipboard access now falls back when `navigator.clipboard` is unavailable, the button reports success/failure, and the copied name is the on-disk asset name with the configured Common prefix.
- Stopped the Common Asset Library preview poll from wiping click feedback and rebuilding thumbnails while the catalog is unchanged.
- Routed preview start/stop and other settings-page actions through a main-thread queue so HTTP listener threads never mutate Unity state directly.
- Stopped deterministic flat-sibling grouping from invalidating unrelated AI cleanup plan steps; nested empty-container removals run deepest-first and only conflict-carrying container removals are dropped.
- Kept hierarchy workspace diagnostics outside imported assets so review evidence survives without polluting the asset database or version-control scope.

### Changed

- Improved the nine-slice editor window and auto-processor, including shared image-source handling and exported-border application.
- Expanded AI hierarchy settings and chat-client plumbing for provider configuration, cleanup execution, and web review.
- Hardened project settings validation (enum checks, cached runtime state) before applying server-side updates.

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
