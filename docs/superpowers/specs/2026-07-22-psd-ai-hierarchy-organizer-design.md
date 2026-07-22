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

Only native PSD layer IDs are durable identities. A node whose Photoshop layer ID is zero may receive a session-only fallback key for diagnostics, but it is marked `FallbackUnstable`, cannot appear in a persisted AI group/rename, and remains in `Unsorted_Unstable`. The UI explains that the source PSD must provide a real layer ID before persistent incremental organization is safe.

- An unchanged or content-only PSD update reuses the Profile without calling the model.
- A geometry-only update reuses the Profile after deterministic group-contiguity and protected-boundary validation; only invalidated groups are sent for focused replanning.
- Layer renames preserve membership when `layerId` is unchanged.
- New layer IDs are not guessed into existing semantic groups. They appear in `Unsorted_New` and are reported for focused replanning.
- Removed layer IDs remain in the Profile as missing entries until the user confirms cleanup; they do not cause business objects to be silently deleted.
- Generated group keys are stable and reused, so repeated imports cannot create `Header_2`, `TaskList_2`, or duplicate wrappers.
- Project-owned objects without PSD identity are never moved or deleted.
- The Profile schema and source fingerprint support stale-plan detection and migration.

The Profile is configuration data, not hardcoded per-screen logic. The general implementation must not contain `7日任务拆分`, `组 1`, `组 19`, or `ui_daily_*` matching rules.

Focused replanning is orchestrated per invalidated scope. Content-only updates make zero model calls. New IDs and geometry-invalidated groups produce a bounded request containing only the affected nodes, their current group, direct neighbors, protected boundaries, and relevant preview crop. The returned partial plan may modify only the invalidated scopes and new IDs. It is strictly validated, merged into a clone of the previous Profile, and then the complete merged plan is validated again against the complete current tree for cycles, multiple parents, render order, protected boundaries, and stable identity. Unaffected group membership and renames remain byte-for-byte unchanged. Missing IDs remain pending until the preview window exposes and the user confirms an explicit cleanup action.

## Existing Prefab Merge and Business-Node Preservation

Generation is transactional, but retained generated objects are updated in place so their Prefab local file IDs and external references survive. Before regeneration, Unity loads the existing Prefab contents and the previous Profile. The Profile records each generated stable ID's Prefab local file ID and last known transform path after a successful save. Generated group keys receive the same identity record.

Nodes not present in the generated-identity map are project-owned. The importer first builds a temporary candidate tree, then loads the existing target with `PrefabUtility.LoadPrefabContents`, matches retained generated objects by stable ID to their recorded local file ID, and uses the last-known path only as a diagnostic fallback. The existing importer payload synchronizer updates only its explicit PSD-owned allowlist on matched generated objects: active state and source name; `RectTransform` anchors, pivot, position, size, rotation, and scale before regrouping; `Image` sprite, color, type/fill/raycast/preserve-aspect fields; and TMP text/font/style fields. Material selection may assign an existing exact-match material or a newly created material, but neither the synchronizer nor organizer may mutate a material asset. The synchronizer may update Sprite/text/nine-slice values only as part of ordinary PSD content import, never as a hierarchy-plan instruction.

The hierarchy organizer itself owns only generated empty group objects, parent/sibling placement, approved semantic names, and the local-rectangle reconstruction required after regrouping. It never writes Sprite, text, Image type/fill, nine-slice metadata, font/material properties, business components, component order, custom serialized fields, or project-owned objects. Those non-allowlisted values always remain on the existing Prefab object. Because retained existing objects are reorganized in place rather than replaced, their local file IDs, external references, Prefab-instance overrides, and project-owned children remain intact.

New stable IDs create new objects. Missing generated IDs are retained and marked pending until confirmed cleanup. Project-owned subtrees are never copied through a newly generated replacement tree. If a recorded generated parent is missing or ambiguous, apply stops and reports the subtree instead of guessing.

The candidate and cloned next Profile are validated before the configured target changes. The target Prefab and current Profile bytes are backed up, then the loaded existing Prefab is saved in place so retained local file IDs remain stable. After reimport verification succeeds, the cloned Profile is copied into the existing Profile asset and saved as phase two. An injected or real failure during Prefab save, reimport verification, Profile copy/save, or final verification restores both backups. Temporary candidate assets and `.meta` files are deleted in success and failure paths.

First-time adoption of an existing generated Prefab uses the deterministic source tree, sibling order, generated naming rules, and exact resource references to build the initial generated-identity map. Ambiguous nodes are reported and block apply; they are never classified as project-owned or generated by guess alone. Once adopted, all retained generated local file IDs must remain unchanged across apply/import tests.

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

The unique apply flow is: `ExportTree(tree)` builds the temporary candidate; `LoadPrefabContents(target)` loads the existing Prefab; the payload synchronizer copies only allowlisted PSD-owned values onto matched existing generated objects; then the hierarchy applier operates on that loaded existing Prefab, creating/reusing empty `RectTransform` groups and moving existing retained nodes; validation runs; finally `SaveAsPrefabAsset(existing, target)` saves the same loaded object graph. The applier inserts each group at the minimum original sibling index, preserves original child order, and reconstructs child local rectangles from captured world corners. It must not cross Canvas, Mask, Button, Animator, nested Prefab, or common asset boundaries.

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

AI organization is available only when `UseUnityUI` is enabled. In non-Unity-UI/SpriteRenderer mode the Inspector action is disabled with a clear explanation; the first version does not create ordinary `Transform` grouping plans.

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

The hierarchy organizer does not delete nodes, extract nested item Prefabs, change business components, bind scripts, or request changes to textures, materials, text values, or nine-slice settings. Ordinary PSD content import still updates its explicit PSD-owned allowlist; hierarchy application cannot expand that ownership.

## Verification

Automated tests cover exact Prefab path resolution for both output modes, plan validation, stable group reuse, rename persistence by layer ID, new/missing layer handling, render-order guards, and world-rectangle preservation. They also prove retained local file IDs and external references remain unchanged, partial plans cannot modify unaffected groups, zero layer IDs cannot persist, the non-Unity-UI action is disabled, and injected failures at every Prefab/Profile transaction phase restore both original assets.

The `7日任务拆分` fixture is an acceptance sample for general rules, not a source of hardcoded conditions. The acceptance run must show that repeated apply/import produces the same hierarchy, visual leaf geometry and references remain unchanged, and the configured inside-folder Prefab is the only modified Prefab.
