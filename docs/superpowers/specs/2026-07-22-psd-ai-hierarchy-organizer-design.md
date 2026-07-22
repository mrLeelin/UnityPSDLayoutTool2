# PSD AI Hierarchy Organizer Design

## Goal

Add an AI-assisted, preview-first hierarchy organizer to PSD Layout Tool 2. The organizer must produce semantic Prefab structure without allowing the model to write Unity assets directly, and it must remain stable across incremental PSD imports.

## User Entry Point

The PSD custom Inspector adds `AI 整理层级（预览）` directly below the primary `生成预制体` action. The action resolves the generated Prefab through the same PSD output directory and `PrefabMode` rules used by import. It never selects a same-name Prefab by search order or modification time.

The current `7日任务拆分.psd` setting resolves to:

`Assets/PSDLayoutTool2/TestData/7日任务拆分/7日任务拆分.prefab`

The alternative same-name candidate is:

`Assets/PSDLayoutTool2/TestData/7日任务拆分.prefab`

Path-resolution tests must assert that `InsideOutputFolder` selects only the first path, `SiblingToOutputFolder` selects only the second path, and applying a plan never modifies the non-configured candidate.

If the configured target does not exist, the organizer reports the calculated path and asks the user to generate it first. It does not silently fall back to the alternative Prefab output mode.

## Architecture

The model is a planner only. Unity owns all validation, mutation, rollback, and verification.

```text
PSD Inspector
  -> resolve exact Prefab
  -> export hierarchy request
  -> local AI runner produces strict plan JSON
  -> Unity validates plan
  -> preview current/proposed trees
  -> user applies
  -> deterministic C# hierarchy applier
  -> verify geometry, ordering, and references
  -> save Profile and Prefab
```

The first implementation includes a local AI invocation behind `IPsdHierarchyAiRunner`. Its default implementation invokes Codex CLI asynchronously with a fixed read-only planning prompt and accepts only the strict plan JSON result. The process has a configurable timeout, supports cancellation when the window closes, captures standard output/error, and never receives permission to modify Unity assets. When Codex CLI is unavailable, offline, times out, or returns invalid output, the editor preserves the request package and offers manual plan import without weakening validation.

Strict parsing rejects unknown root fields, unknown fields within groups or renames, duplicate JSON keys, non-finite numbers, unsupported schema versions, trailing non-JSON output, and any command/code/deletion field. `JsonUtility`'s normal unknown-field tolerance is not sufficient for this trust boundary.

## Incremental Update Contract

The accepted plan is persisted as a `PsdHierarchyProfile` ScriptableObject under `Assets/PSDLayoutTool2Settings/HierarchyProfiles/`, keyed by the source PSD GUID. Plan membership refers to stable PSD `layerId` values, never generated names.

The Profile stores three independent fingerprints:

- `contentFingerprint`: image/text/style content only; changes never invalidate hierarchy membership.
- `structureFingerprint`: stable ID, source parent ID, sibling order, and node kind; membership or parent changes trigger incremental reconciliation.
- `geometryFingerprint`: stable ID plus bounds; geometry changes rerun spatial/order validation and trigger focused replanning only when an existing group is no longer valid.

- An unchanged or content-only PSD update reuses the Profile without calling the model.
- A geometry-only update reuses the Profile after deterministic group-contiguity and protected-boundary validation; only invalidated groups are sent for focused replanning.
- Layer renames preserve membership when `layerId` is unchanged.
- New layer IDs are not guessed into existing semantic groups. They appear in `Unsorted_New` and are reported for focused replanning.
- Removed layer IDs remain in the Profile as missing entries until the user confirms cleanup; they do not cause business objects to be silently deleted.
- Generated group keys are stable and reused, so repeated imports cannot create `Header_2`, `TaskList_2`, or duplicate wrappers.
- Project-owned objects without PSD identity are never moved or deleted.
- The Profile schema and source fingerprint support stale-plan detection and migration.

The Profile is configuration data, not hardcoded per-screen logic. The general implementation must not contain `7日任务拆分`, `组 1`, `组 19`, or `ui_daily_*` matching rules.

## Existing Prefab Merge and Business-Node Preservation

Generation is transactional and does not overwrite the target Prefab in place. Before regeneration, Unity loads the existing Prefab contents and the previous Profile. The Profile records each generated stable ID's Prefab local file ID and last known transform path after a successful save. Generated group keys receive the same identity record.

Nodes not present in the generated-identity map are project-owned. Project-owned subtrees are detached into an in-memory preservation set together with their nearest generated parent stable ID, sibling relationship, components, serialized references, and world rectangle. The importer then builds and organizes a candidate Prefab under a temporary asset path. Project-owned subtrees are cloned back beneath the corresponding generated parent in the candidate; if that parent is missing or ambiguous, apply stops and reports the subtree instead of dropping it.

The candidate and Profile are validated before the configured target changes. After validation, the target Prefab file is replaced transactionally while its `.meta`/GUID remains unchanged, then re-imported and verified again. A backup is retained until verification succeeds. Only after the target passes does Unity update the Profile's fingerprints, local file IDs, paths, and missing/new ID state. Any failure restores the previous Prefab bytes and leaves the previous Profile asset unchanged.

First-time adoption of an existing generated Prefab uses the deterministic source tree, sibling order, generated naming rules, and exact resource references to build the initial generated-identity map. Ambiguous nodes are reported and block apply; they are never classified as project-owned or generated by guess alone.

## AI Request and Plan Contracts

The request includes stable ID, original name, node kind, parent ID, sibling index, rectangle, component/boundary flags, current Prefab hierarchy, and optional PSD/Prefab preview images. It excludes texture bytes and does not grant write access.

The plan may contain only:

- semantic generated groups with stable keys and parent keys;
- member stable IDs;
- semantic rename suggestions;
- evidence and confidence;
- source fingerprint and schema version.

The plan cannot contain C#, filesystem commands, deletion instructions, material changes, or arbitrary Unity property writes.

## Validation and Apply Safety

Before preview or apply, Unity rejects plans with unknown or duplicate IDs, cycles, multiple parents, invalid group keys, protected-boundary crossings, non-contiguous render-order moves, or incompatible structure/geometry fingerprints. Content-only fingerprint changes do not make a hierarchy plan stale.

The applier runs after `ExportTree(tree)` and before `PrefabUtility.SaveAsPrefabAsset`. It creates only empty `RectTransform` grouping nodes, inserts each group at the minimum original sibling index, preserves original child order, and reconstructs child local rectangles from captured world corners. It must not cross Canvas, Mask, Button, Animator, nested Prefab, or common asset boundaries.

Apply verification compares every moved leaf before and after:

- four world corners within 0.01 pixels;
- anchor minimum/maximum, pivot, anchored position, size delta, rotation, and local scale;
- active state;
- Sprite reference, `Image.type`, `Image.material`, color, fill settings, raycast target, and preserve-aspect state;
- TMP text, font, and shared material;
- nine-slice Sprite reference, border, `Image.type`, fill settings, preserve-aspect state, and material;
- component type/order and serialized references for preserved project-owned subtrees;
- sibling draw ordering among visual leaves.

Any validation or verification failure aborts the save and leaves the existing Prefab and Profile unchanged.

## Preview and First-Version Scope

`PsdHierarchyOrganizerWindow` displays the exact target Prefab path, current tree, proposed tree, validation warnings, evidence, and confidence. The only mutating action is `应用并重新生成 Prefab`.

The first version supports:

- exact Prefab resolution;
- asynchronous local Codex planning, timeout/cancellation, offline request export, and manual plan import fallback;
- strict plan validation;
- preview;
- semantic group creation and renaming;
- incremental Profile reuse;
- safe application during Prefab generation;
- focused handling of new and missing layer IDs.

The first version does not delete nodes, extract nested item Prefabs, change business components, bind scripts, or change textures, materials, text values, or nine-slice settings.

## Verification

Automated tests cover exact Prefab path resolution for both output modes, plan validation, stable group reuse, rename persistence by layer ID, new/missing layer handling, render-order guards, and world-rectangle preservation.

The `7日任务拆分` fixture is an acceptance sample for general rules, not a source of hardcoded conditions. The acceptance run must show that repeated apply/import produces the same hierarchy, visual leaf geometry and references remain unchanged, and the configured inside-folder Prefab is the only modified Prefab.
