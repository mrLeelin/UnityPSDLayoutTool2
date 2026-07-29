---
name: prefab-hierarchy-cleanup
description: Safely organize one existing Unity Prefab in place into a complete semantic hierarchy while preserving its visual result and serialized behavior. Use only when explicitly invoked as $prefab-hierarchy-cleanup to inspect, plan, review, and optionally apply cleanup to a Unity .prefab; especially when the existing hierarchy is flat, PSD-generated, or hard to maintain. Use for one approved in-place plan that adds semantic containers, replaces PSD/export node names with semantic English names, renames proven-private Texture or SpriteAtlas assets to PrefabName_SemanticName, and extracts approved shared nested components. Never create, copy, or replace the target Prefab. Do not use for Figma cleanup, PSD import, Figma-to-Prefab generation, or runtime UI redesign.
---

# Prefab Hierarchy Cleanup

Organize existing Unity Prefabs by transferring the *discipline* of Figma hierarchy cleanup, not Figma's node model or tooling. Treat Unity components, serialized bindings, RectTransforms, asset references, prefab overrides, and sibling order as source-of-truth data.

## AI Chat Two-Turn Contract

When this skill is supplied to the Unity AI hierarchy chat window, use exactly this interaction:

1. The Unity window first generates an authoritative node snapshot through Unity Editor APIs. The first AI reply presents the complete reviewable plan and one complete UTF-8 JSON plan in a `json` code block. The root object must use `"version": 2`, copy the snapshot `fingerprint` into `snapshotFingerprint`, reference every existing node as `node:<id>`, and include every required operation array from `references/plan-format.md`, using `[]` when an operation is unused. The AI itself does not invoke tools or write files.
2. When the user explicitly confirms that displayed plan, the Unity chat window rechecks the Prefab fingerprint, resolves every node ID to its exact original path, converts the plan to a temporary internal version 1 runner plan, and invokes `scripts/run_prefab_hierarchy_cleanup.ps1 -ApplyConfirmed`. That runner performs the actual Prefab update through Unity Editor APIs and returns its verification result to the same chat.

The confirmation is sufficient authorization for the displayed plan. Do not ask the user to choose an output mode, repeat confirmation, or manually run a script. A revised plan requires a new first-round review before it can be confirmed. Before enabling confirmation, the chat window must reject a plan whose `version`, required fields, `snapshotFingerprint`, `prefabAssetPath`, `output.assetPath`, assets, or node references are invalid, then run the same renderer validation used by the runner on the converted internal plan. That validation simulates wrapper creation, moves, and ordered empty-container removals against an unsaved Prefab instance so an incomplete evacuation fails before user confirmation. An existing-node reference must be copied from the Unity-generated snapshot; it must never be inferred from displayed text, a Sprite name, a visual label, or a guessed hierarchy path.

Every AI-specific prompt must supply the canonical plan format and authoritative node snapshot before the first response. Do not make an agent infer JSON names, node IDs, or paths from prose or use a legacy schema. If the first AI reply is missing the complete JSON block or fails validation, the chat window must automatically send the exact validation failure back to the same AI session and request one complete replacement JSON code block with no prose. When the failure involves a required component family, the repair prompt must replay every mandatory `candidateId`, `parent`, `sources`, and recommended mode from the authoritative snapshot. The replacement must copy those fields exactly, use a non-`skip` mode, and provide one matching concrete extraction per candidate. For a stateful instance, the chat conversion may normalize incomplete, duplicated, or invalid Common/State instance lists from the authoritative direct-child snapshot when either side is already a complete observed mapping or the instance is the reviewed source of that Common/State contract. It derives the other side only as the ordered direct-child complement, and the final counts must exactly match `common.members` plus the selected state's `members`; otherwise validation still fails. The repair may use only IDs present in the original snapshot; it must remove an operation whose intended source cannot be proven instead of inventing another ID. The window retains the first response's review text and pairs it with the corrected JSON only after validation succeeds. This is an internal repair step, not another user-visible turn: do not display an invalid reply as confirmable, do not ask the user to prompt the AI again, and do not create a new AI session. If the replacement still fails, leave the Prefab unchanged, disable confirmation, and report the final validation failure.

## Bundled Tools

Use the bundled scripts for every execution. Do not write one-off `.tmp` C# payloads for a cleanup operation.

1. Inspect first through `scripts/snapshot_prefab_hierarchy.ps1`; it is read-only and emits the complete tree, RectTransform state, UI components, Sprite/Texture paths, TMP state, nested Prefab boundaries, and counts.
2. Run `scripts/find_prefab_component_candidates.py` whenever the snapshot contains repeated visual units. The Unity AI chat snapshot also emits high-confidence numbered repeated-family candidates. Record every candidate in `componentFamilyDecisions`; a chat candidate marked `requiresExtraction: true` must be extracted and cannot be skipped. A family whose members are not all structurally identical also reports `numbered_structure_subset` candidates: the members that do share one recursive structure. Prefer the family-level extraction when it is marked required, and use a subset when the family only fits a variant or when a narrower boundary is cleaner. A single-member subset is a report that the member has no peer, not an instruction to extract it alone.
3. For a direct script run, create an internal version 1 JSON plan from the corresponding section of [references/plan-format.md](references/plan-format.md), starting from [examples/sample-plan.json](examples/sample-plan.json). The Unity AI chat window instead creates version 2 node-ID plans and performs the conversion itself.
4. Validate and execute through `scripts/run_prefab_hierarchy_cleanup.ps1`.
5. Use `-ApplyConfirmed` only after the user has reviewed and explicitly confirmed the complete tree, the exact in-place target path, `PrefabName`, and every Texture/Atlas rename.
6. If Unity or the wrapper times out after an apply attempt, do not apply again. Run the same plan with `-VerifyOnly` to determine the actual saved state.

```powershell
& <skill-dir>/scripts/run_prefab_hierarchy_cleanup.ps1 `
  -ProjectPath "E:\\Project\\Demo\\monsterhunter" `
  -PlanPath "C:\\Temp\\reward-panel.plan.json" `
  -ApplyConfirmed
```

```powershell
& <skill-dir>/scripts/snapshot_prefab_hierarchy.ps1 `
  -ProjectPath "E:\\Project\\Demo\\monsterhunter" `
  -PrefabAssetPath "Assets/UI/RewardPanel.prefab"
```

The runner renders a temporary UTF-8 C# payload, calls `uloop execute-dynamic-code --code-file`, and removes that payload after the Unity call returns. The skill itself contains the reusable PowerShell, Python, plan schema, and C# rendering logic.

Before applying a new extraction plan, run the same runner with `-CompileOnly`. It renders the apply payload but passes `--compile-only true` to Unity, so no asset is written:

```powershell
& <skill-dir>/scripts/run_prefab_hierarchy_cleanup.ps1 `
  -ProjectPath "E:\\Project\\Demo\\monsterhunter" `
  -PlanPath "C:\\Temp\\reward-panel-components.plan.json" `
  -CompileOnly
```

```powershell
$priorOutputEncoding = $OutputEncoding
try {
  $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
  $snapshot = & <skill-dir>/scripts/snapshot_prefab_hierarchy.ps1 `
    -ProjectPath "E:\\Project\\Demo\\monsterhunter" `
    -PrefabAssetPath "Assets/UI/RewardPanel.prefab"
  ($snapshot | ConvertFrom-Json).Result | python <skill-dir>/scripts/find_prefab_component_candidates.py -
}
finally { $OutputEncoding = $priorOutputEncoding }
```

## Operating Contract

- Work only on the named `.prefab` asset. Do not require, query, or modify Figma.
- Always organize the named target Prefab in place. Its plan must use `output.mode: in_place` and the same `output.assetPath` as `prefabAssetPath`; never create, offer, or ask the user to choose a `.cleaned.prefab`, duplicate, or replacement target Prefab.
- Start read-only. Inspect the complete hierarchy, components, RectTransforms, active state, asset references, and nested Prefab instances before proposing changes.
- Infer semantic groups from geometry, component/type patterns, visible hierarchy, repeated structures, and existing names. Names are a hint, never sole membership evidence.
- Produce one complete final-tree proposal before applying. Include nested repeated units such as `[Item_*]`, cards, progress segments, map markers, tabs, or rows; do not stop at coarse region wrappers.
- Assign every moved source node exactly once. Rename every exported object to a concise English semantic name; retain brackets only for structural groups such as `[ContentCard_1]`. A text value (`20`, `+`, `150k`, `login 1 day`), PSD token (`daily_*`, `ui_*`), UUID-like export name, punctuation-only name, or duplicate ambiguous sibling name is not semantic. Name the role, such as `RewardActivityAmount`, `RewardPlusLabel`, `MissionIcon`, or `MissionDescription`. With `verify.requireEnglishNames`, the runner rejects those baseline invalid forms even when a plan omits custom patterns. Never delete, duplicate, merge, replace components, or change bindings merely to make the tree look cleaner. The sole structural exception is `emptyContainerRemovals`: list a pre-existing container only when every one of its current direct children is moved out or is itself removed earlier in the same removal list. Nested removals must use child-before-parent order. The resulting container must be empty, have no components beyond its `Transform`, and have no external serialized references.
- Every plan-owned ID, including `wrappers[].id`, extraction `id`, and state `id`, must be lower snake_case matching `^[a-z][a-z0-9_]*$`. Use IDs such as `screen_root`, `day_markers`, or `task_in_progress`; never use PascalCase, kebab-case, spaces, brackets, or an `@` prefix. The `@` prefix is reserved only for a later reference to an earlier wrapper, such as `@screen_root`.
- Treat ambiguous membership, a likely serialized binding, nested Prefab boundary, or unfamiliar custom component as a blocker. State the ambiguity and leave that portion unchanged rather than guessing.
- Require explicit user confirmation of the reviewed plan before writing. A request to inspect or organize does not authorize an immediate asset mutation.
- For a `PrefabName_SemanticName` rename, infer an English PascalCase `PrefabName` from the UI's actual function, require it to end in `View`, and ask the user to confirm it before mutation.
- Rename only Texture and SpriteAtlas assets that are proven private to the named Prefab. When a private Texture directory is in scope, list and rename every Texture in it to `PrefabName_SemanticName`; do not leave a partial prefix migration. Use `AssetDatabase.RenameAsset`; never use file-system moves or create replacement `.meta` files.
- When the user explicitly asks for reusable Prefabs with visual states, include every approved family in the same explicitly reviewed in-place plan. `output.mode: in_place` prohibits a replacement screen Prefab, not a shared child asset under the target Prefab's sibling `Common` directory. Require either at least two structurally matching units, or at least three numbered semantic units under one parent with matching anchors and pivot; no source may cross a nested Prefab boundary or retain an external serialized reference.
- Treat the candidate scanner as a discovery aid, not a safety proof. It reports both strictly matching structures and high-confidence numbered families; the latter may vary in `sizeDelta` or state structure, which must be represented as reviewed instance overrides or a stateful mapping. Every result still passes the Unity-side nested-boundary, external-reference, and structural checks during apply.
- Treat visually overlapping alternatives as states, not repeated instances. A `stateComponentExtractions` plan replaces all approved sibling state sources with one nested component containing a `[States]` container. It requires an explicit semantic state mapping and a single default state; never infer those names from layer order alone.
- Treat simultaneously visible list rows that share one logical component but display different visual states as a `variantComponentExtractions` family. Create exactly one shared Prefab under `Prefab/Common/`; its root must contain direct `[Common]` and `[States]` children. Replace every approved source row with an instance of that same Prefab, preserve the row's list position and instance name, and activate exactly the mapped state in each instance. `[Common]` may be empty only when no element can be proven common to every state without changing the rendering.
- Treat a repeated unit with both stable members and a finite set of visual variants as a `statefulComponentExtractions` family. Create exactly one shared Prefab under `Prefab/Common/`, with direct `[States]` followed by `[Common]` children so shared labels still render above state backgrounds. Every source unit must map every direct member exactly once into `[Common]` or its selected state; `Common` must contain every member proven common. A named all-common state may have an empty `members` list only when its instances map every direct child through `commonSourceNames` and use an empty `stateSourceNames` list; never use an empty state to hide an unmapped child. In the Unity chat, an incomplete Common or selected-state list is rebuilt from the authoritative snapshot only when the opposite side or the reviewed source contract proves the partition, and the other side is the exact ordered complement; the direct runner keeps requiring both explicit complete mappings. Reuse one semantic state branch across all matching instances and apply only the approved instance overrides, such as a counter value. Do not create a distinct state solely because an instance label differs.
- State extraction creates hierarchy and the initial active branch only. It does not create or attach a runtime state-switching script, Animator, or binding; use the project's established presentation owner to switch the branches later.

## Workflow

### 1. Preflight and Snapshot

1. Confirm the target path is a Unity `.prefab` under the project `Assets/` directory and inspect `git status`; preserve unrelated worktree changes.
2. Use Unity Editor APIs or a supported Unity bridge to load the Prefab. Do not hand-edit YAML.
3. Capture a before snapshot containing:
   - complete transform tree and sibling order;
   - active state, RectTransform geometry, anchors, pivot, scale, and rotation;
   - component types and serialized object-reference fields;
   - Sprite, TMP/font/material, Animator, Button, ScrollRect, CanvasGroup, LayoutGroup, and custom-script presence;
   - nested Prefab instance boundaries, overrides, and missing components.
4. Open a Prefab Stage or otherwise capture a visual reference when the Unity environment permits it. Treat visual review as additional evidence, not a substitute for the serialized snapshot.
5. When extracting repeated units, run `find_prefab_component_candidates.py` on the snapshot. Review every candidate's parent, direct children, recursive signature, and excluded nested Prefab state before adding it to the reviewed plan. The same report also lists `containmentMisgroupings`, where a numbered family sits geometrically inside a different numbered family, and `sparseContainers`, where a container's own area is mostly empty; both usually mean a repeated member was grouped by type instead of by unit, so resolve them before writing the tree.

### 2. Infer a Complete Tree

Build the final hierarchy from all current direct and nested children before writing anything.

Use these evidence rules:

- Cluster repeated units from comparable component signatures, size, alignment, spacing, and internal structure.
- For a same-parent numbered family such as `[TaskItem_1..N]` or `[DayCard_1..N]`, matching anchors and pivot are sufficient to recognize one component family. Do not reject it merely because a visual state changes `sizeDelta`; preserve that value as an instance override and choose `stateful` when the internal structures differ.
- Form each repeated unit as its own `[Item_*]`, `[TabItem_*]`, `[Marker_*]`, or similarly neutral wrapper. Do not place every background and icon directly under a broad region such as `[MiniMap]`.
- Do not flatten a repeated visual unit by component type or render layer. When background, label, amount, badge, lock, or icon share a repeated index and overlay one visual slot, put them together in that unit (for example `[ItemList]/[Item_03]/ItemBackground + ItemLabel + ItemValue + ItemLock`), not into separate `[...Backgrounds]`, `[...Labels]`, and lock groups.
- Treat repeated text labels, badges, counters, and interaction targets as members of the nearest repeated visual unit when geometry and cardinality align. A label at the same position as one of four map markers belongs inside that marker, not in a broad navigation/text group. Keep only truly global text, such as a timer, in a shared group.
- Keep a region-scale background outside a repeated foreground unit. For example, a map-sector background belongs in `[MapSectors]`; `[MapMarker_*]` contains only the marker's own background/frame and icon. Do not use proximity alone to put a large area panel into a foreground marker/card/item.
- When the snapshot carries `containmentFindings`, that is measured geometry, not a suggestion: every listed member's world rectangle lies fully inside the paired repeated unit. Answer each one in `containmentResolutions` — `reparent` it into that unit (the usual answer, since the pairing is 1:1 and cardinality matches), or `keep` it outside with concrete evidence of at least 20 characters, such as a shared layout group, an animation driver, or region-scale coverage. A plan that leaves a finding unanswered is rejected before it runs.
- Use generic structural names only when the role is supported by evidence: `[Background]`, `[Header]`, `[Content]`, `[ListRoot]`, `[ScrollView]`, `[Viewport]`, `[TabBar]`, `[ProgressSection]`, `[Navigation]`, `[BottomHUD]`.
- Preserve the existing relative sibling order inside every inferred group unless visual/order evidence supports a different order.
- Identify a component family only when repeated units have the same recursive component/child signature and their differences are legitimate instance data such as Sprite, text, color, visibility, or RectTransform values. Treat a non-matching signature, nested Prefab, or an external reference as a blocker.
- Identify a state family only when sibling roots occupy the same visual slot and are mutually exclusive. Their internal signatures may differ. Do not turn vertically or horizontally spaced list entries into states merely because their names share a prefix.
- Identify a variant component family when two or more visible list rows occupy different list slots but are the same logical item in different visual states. Do not collapse those rows into a single list instance. Instead, make each row an instance of one stateful component and keep its own selected state.
- Include inactive and hidden nodes in analysis. Do not remove them because they are currently invisible.
- Do not introduce `ScrollView > Viewport > Content` based only on a vertical layout; require an existing `ScrollRect` or clear project-specific structural evidence.
- Do not cross a nested Prefab instance boundary or alter source Prefab structure without separately identifying the owning asset and obtaining explicit approval for that asset.

Before review, validate the plan locally:

- every affected node has one and only one destination;
- in the Unity AI chat plan, an `@` wrapper reference is exactly `@wrapperId` and every existing source-tree node uses `node:<id>` copied from the authoritative snapshot; only the window-generated internal runner plan contains original pre-apply paths;
- all direct and nested repeated groups are represented;
- no operation deletes nodes, changes components/references/active state/Prefab ownership, or moves any existing visual node in world space;
- every structural wrapper has a centered `RectTransform` whose bounds exactly enclose its direct child `RectTransform` bounds; do not use full-stretch containers for a semantic group;
- every repeated unit has an explicit direct-child membership contract in `verify.directChildren`; validate names and sibling order after saving rather than relying on a child count alone;
- if a legacy type/layer container is emptied during regrouping, prove that every direct child is covered by a move or an earlier child-container removal, then list it in both `emptyContainerRemovals` and `verify.absentPaths`; use child-before-parent removal order and do not leave empty former grouping wrappers behind;
- when a read-only snapshot already shows the requested final grouping, report it as complete and do not describe the request as blocked merely because an unrelated nested Prefab has an inherited missing Sprite;
- a repeated unit is complete only when its direct-child contract is met. Check every member that shares the unit's geometry and index, including its optional badge, lock, icon, label, or value; wrappers alone are not completion evidence.
- every repeated-family candidate has one `componentFamilyDecisions` entry. A skip names the exact safety or binding reason; a Unity chat candidate marked `requiresExtraction: true` must instead have an extraction with an explicit template, output asset path, and complete instance list in this plan;
- a `numbered_structure_subset` candidate is judged like any other candidate, and its decision names whether the family-level boundary or the subset boundary was chosen and why. A source may appear in only one decision, so choosing both boundaries for the same member fails validation;
- every `containmentFindings` member has one `containmentResolutions` entry; a `reparent` target is the containing unit or a node inside it, and a `keep` names the layout, animation, or coverage evidence rather than restating the geometry;
- every variant component family has a complete semantic state map, one output `Prefab/Common` asset, every source row exactly once, an explicit per-instance state, and direct `[Common]` + `[States]` verification;
- every stateful component family has a complete Common/State member map, one output `Prefab/Common` asset, one selected state for every visible source, and a direct-member coverage check for every source unit;
- every proposed wrapper has at least two evidence-backed members, unless it is a single semantic region required to contain a verified set of child groups;
- ambiguous nodes remain in place and are reported.

### 3. Review Before Apply

Present a compact, complete proposed tree. Identify:

- exact target Prefab path and confirmation that the saved main Prefab is the same in-place asset;
- new wrappers and each member path;
- each requested component family, its output `Prefab/Common` path, source units, and blockers that excluded look-alikes;
- each requested state, variant, or stateful component family, including its explicit state map and default or per-instance state;
- every scanner candidate that was rejected, when a similar-looking unit was left unextracted;
- unchanged/ambiguous nodes and why they were left untouched;
- expected invariants and verification checks.

Always use `output.mode: in_place`, with `output.assetPath` exactly equal to `prefabAssetPath`. Do not offer `copy`, `<original>.cleaned.prefab`, or an output-mode choice.

For an in-place semantic asset rename, review the exact mapping, including the PrefabName and each `PrefabName_SemanticName` target. Include the sibling SpriteAtlas only when it is private to the same Prefab and has been explicitly listed in the plan.

Stop here until the user explicitly confirms the plan.

### 4. Apply Through Unity

After confirmation, run `scripts/run_prefab_hierarchy_cleanup.ps1 -ApplyConfirmed`. The generated Unity operation uses Editor APIs only:

- Load with `PrefabUtility.LoadPrefabContents`.
- Create only the approved wrapper `GameObject`s, transfer the approved transforms under them, preserve sibling order, then tighten each wrapper to its direct-child bounds while preserving every existing child's world corners.
- For `componentExtractions`, save a named shared component Prefab from the approved template, replace every approved source unit with a nested instance, and copy every source value into the instance as an override. The operation rejects a structural mismatch, nested source Prefab, external reference, or existing output asset.
- For `stateComponentExtractions`, clone the approved sibling state roots under one `[States]` container, activate only the reviewed default state, replace all state roots with one nested instance, and reject nested Prefabs, external references, non-sibling sources, or existing output assets.
- For `variantComponentExtractions`, create one shared component with direct `[Common]` and `[States]` children, clone each approved visual state under `[States]`, then replace every source list row with a nested instance of that component and activate the reviewed state for that row. It rejects nested source Prefabs, external references, overlapping source paths, incomplete instance coverage, or an existing output asset.
- For `statefulComponentExtractions`, create one shared component with direct `[States]` and `[Common]` children, build the reviewed state branches once, replace every approved source with a nested instance, then apply only its reviewed Common and selected-state member overrides. It rejects nested source Prefabs, external references, unmapped direct members, overlapping sources, incomplete mappings, or an existing output asset.
- Save only the exact target Prefab path with `PrefabUtility.SaveAsPrefabAsset`; Unity's API name is retained, but it overwrites the already loaded target asset in place.
- Unload Prefab contents in a `finally` path.

Only the reviewed in-place plan may create a shared nested component asset. Its source-unit validation, output path, state mapping, replacement instances, and `componentFamilyDecisions` coverage must be explicit. An ordinary hierarchy cleanup may leave extraction arrays empty only after recording every advisory candidate as an evidence-backed skip; high-confidence chat candidates may not be skipped.

When the confirmed plan includes private asset renames, it also verifies the original GUID for every Texture after `AssetDatabase.RenameAsset`, then reloads the saved Prefab and checks all `Image` Sprite references.

For this project, prefer `uloop execute-dynamic-code --code-file` for one-off Unity Editor actions. Use a UTF-8 C# file rather than an inline multiline PowerShell payload. Keep editor-only logic in an `Editor/` context.

Never edit `.prefab` YAML text, reconstruct the Prefab, reimport PSD assets, regenerate textures, manually move files, or create replacement `.meta` files as part of hierarchy cleanup.

### 5. Verify the Saved Asset

Reopen the saved Prefab and compare it with the before snapshot. Report evidence for:

- complete final tree, exact wrapper membership, and preserved affected-node sibling order;
- unique assignment of all moved nodes;
- existing RectTransform world corners preserved within `0.01` where reparenting changes hierarchy, and every structural container's bounds exactly match its direct children;
- all object names are English semantic names; no PSD/export, Chinese, or punctuation-heavy source names remain;
- preserved active states, component counts/types, object references, Sprite/TMP/font/material assignments, and nested Prefab boundaries;
- missing Sprite references on the target Prefab itself are target-owned verification issues and must be surfaced, never silently classified as inherited nested-Prefab issues. A missing Sprite inside an unchanged nested Prefab instance is reported separately by path; fix that source asset through its own owner rather than crossing the nested boundary;
- after a successful save, return and surface any verification mismatch as `VERIFY_WARN issue=...` and continue the requested workflow with that warning carried forward. Do not silently discard it or report the phase complete. Target loading, mutation preconditions, hierarchy operations, and saving remain blocking because they leave no trustworthy saved result to build on;
- each separately extracted unit is a nested Prefab instance sourced from the approved shared component asset, with every affected RectTransform world corner preserved within `0.01`;
- each separately extracted state component is a nested Prefab instance with one direct `[States]` container, all approved state branch names in order, exactly one active default branch, and every state branch's world corners preserved within `0.01`;
- each separately extracted variant component is a nested instance of the one reviewed `Prefab/Common` asset, has direct `[Common]` and `[States]` containers, contains every approved state branch in order, and has exactly its mapped branch active;
- each separately extracted stateful component is a nested instance of the one reviewed `Prefab/Common` asset, has non-empty `[Common]` and `[States]` containers, contains each approved state branch once, maps every source direct member exactly once, and has exactly its mapped branch active;
- all private Texture files in scope use the exact `PrefabName_` filename prefix, and `verify.forbiddenObjectNamePatterns` rejects residual PSD/export or text-value node names;
- `Missing Component = 0`;
- visual comparison in Prefab Stage when available.

Run `uloop compile` only when this work changed C# source. Asset-only cleanup does not need a compilation claim; report whether Unity successfully loaded and saved the Prefab instead.

If any invariant fails, preserve the original, do not describe the cleanup as complete, and use the snapshot to narrow the failed in-place operation before any retry.

## Explicit Non-Goals

- Do not access Figma, use Figma MCP, or treat Figma node/component semantics as Unity data.
- Do not generate a Prefab from a PSD or Figma design.
- Do not create or copy a replacement screen Prefab. A shared nested component Prefab is allowed through a separately approved component-extraction plan when the user asks for reusable or stateful units; do not silently omit a detected repeated family.
- Do not infer or attach runtime scripts, serialized bindings, Animator transitions, interaction semantics, or asset replacements.
- Do not infer state semantics, default state, or a runtime state-switching mechanism from visual overlap alone.
- Do not make a coarse tree appear complete through cosmetic names alone.

## Invocation Examples

- `Use $prefab-hierarchy-cleanup to inspect Assets/UI/RewardPanel.prefab and propose a complete hierarchy. Do not modify it yet.`
- `Use $prefab-hierarchy-cleanup on Assets/PSDLayoutTool2/TestData/Example.prefab. Propose the complete in-place hierarchy cleanup before applying it.`
- `Use $prefab-hierarchy-cleanup to organize this UI in place and rename its private textures to RewardPanelView_SemanticName. Infer the View name and ask me to confirm the rename plan first.`
