---
name: prefab-hierarchy-cleanup
description: Safely organize an existing Unity Prefab into a complete semantic hierarchy while preserving its visual result and serialized behavior. Use only when explicitly invoked as $prefab-hierarchy-cleanup to inspect, plan, review, and optionally apply cleanup to a Unity .prefab; especially when the existing hierarchy is flat, PSD-generated, or hard to maintain. Also use for an explicitly approved in-place cleanup that semantically renames the Prefab's private Texture and SpriteAtlas assets to PrefabName_SemanticName, or extracts verified repeated UI units into a shared nested component Prefab while preserving instance overrides. Do not use for Figma cleanup, PSD import, Figma-to-Prefab generation, or runtime UI redesign.
---

# Prefab Hierarchy Cleanup

Organize existing Unity Prefabs by transferring the *discipline* of Figma hierarchy cleanup, not Figma's node model or tooling. Treat Unity components, serialized bindings, RectTransforms, asset references, prefab overrides, and sibling order as source-of-truth data.

## Bundled Tools

Use the bundled scripts for every execution. Do not write one-off `.tmp` C# payloads for a cleanup operation.

1. Inspect first through `scripts/snapshot_prefab_hierarchy.ps1`; it is read-only and emits the complete tree, RectTransform state, UI components, Sprite/Texture paths, TMP state, nested Prefab boundaries, and counts.
2. For component extraction, pass the snapshot `Result` text to `scripts/find_prefab_component_candidates.py`. It identifies repeated sibling units with the same recursive component signature and reports only advisory candidates.
3. Create a JSON plan from [references/plan-format.md](references/plan-format.md). Start from [examples/sample-plan.json](examples/sample-plan.json).
4. Validate and execute through `scripts/run_prefab_hierarchy_cleanup.ps1`.
5. Use `-ApplyConfirmed` only after the user has reviewed and explicitly confirmed the complete tree, output mode, `PrefabName`, and every Texture/Atlas rename.
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
- Start read-only. Inspect the complete hierarchy, components, RectTransforms, active state, asset references, and nested Prefab instances before proposing changes.
- Infer semantic groups from geometry, component/type patterns, visible hierarchy, repeated structures, and existing names. Names are a hint, never sole membership evidence.
- Produce one complete final-tree proposal before applying. Include nested repeated units such as `[Item_*]`, cards, progress segments, map markers, tabs, or rows; do not stop at coarse region wrappers.
- Assign every moved source node exactly once. Rename every exported object to a concise English semantic name; retain brackets only for structural groups such as `[ContentCard_1]`. Never delete, duplicate, merge, replace components, or change bindings merely to make the tree look cleaner.
- Treat ambiguous membership, a likely serialized binding, nested Prefab boundary, or unfamiliar custom component as a blocker. State the ambiguity and leave that portion unchanged rather than guessing.
- Require explicit user confirmation of the reviewed plan before writing. A request to inspect or organize does not authorize an immediate asset mutation.
- For a `PrefabName_SemanticName` rename, infer an English PascalCase `PrefabName` from the UI's actual function, require it to end in `View`, and ask the user to confirm it before mutation.
- Rename only Texture and SpriteAtlas assets that are proven private to the named Prefab. Use `AssetDatabase.RenameAsset`; never use file-system moves or create replacement `.meta` files.
- Extract a shared component Prefab only from an explicitly reviewed `componentExtractions` plan. Require at least two structurally matching units, no nested Prefab boundary inside a source unit, and no external serialized reference to that unit. Do not extract merely because items look similar.
- Treat the candidate scanner as a discovery aid, not a safety proof. Its result must still pass the Unity-side nested-boundary, external-reference, and structural-signature checks during apply.

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
5. When extracting repeated units, run `find_prefab_component_candidates.py` on the snapshot. Review every candidate's parent, direct children, recursive signature, and excluded nested Prefab state before adding it to a plan.

### 2. Infer a Complete Tree

Build the final hierarchy from all current direct and nested children before writing anything.

Use these evidence rules:

- Cluster repeated units from comparable component signatures, size, alignment, spacing, and internal structure.
- Form each repeated unit as its own `[Item_*]`, `[TabItem_*]`, `[Marker_*]`, or similarly neutral wrapper. Do not place every background and icon directly under a broad region such as `[MiniMap]`.
- Treat repeated text labels, badges, counters, and interaction targets as members of the nearest repeated visual unit when geometry and cardinality align. A label at the same position as one of four map markers belongs inside that marker, not in a broad navigation/text group. Keep only truly global text, such as a timer, in a shared group.
- Keep a region-scale background outside a repeated foreground unit. For example, a map-sector background belongs in `[MapSectors]`; `[MapMarker_*]` contains only the marker's own background/frame and icon. Do not use proximity alone to put a large area panel into a foreground marker/card/item.
- Use generic structural names only when the role is supported by evidence: `[Background]`, `[Header]`, `[Content]`, `[ListRoot]`, `[ScrollView]`, `[Viewport]`, `[TabBar]`, `[ProgressSection]`, `[Navigation]`, `[BottomHUD]`.
- Preserve the existing relative sibling order inside every inferred group unless visual/order evidence supports a different order.
- Identify a component family only when repeated units have the same recursive component/child signature and their differences are legitimate instance data such as Sprite, text, color, visibility, or RectTransform values. Treat a non-matching signature, nested Prefab, or an external reference as a blocker.
- Include inactive and hidden nodes in analysis. Do not remove them because they are currently invisible.
- Do not introduce `ScrollView > Viewport > Content` based only on a vertical layout; require an existing `ScrollRect` or clear project-specific structural evidence.
- Do not cross a nested Prefab instance boundary or alter source Prefab structure without separately identifying the owning asset and obtaining explicit approval for that asset.

Before review, validate the plan locally:

- every affected node has one and only one destination;
- all direct and nested repeated groups are represented;
- no operation deletes nodes, changes components/references/active state/Prefab ownership, or moves any existing visual node in world space;
- every structural wrapper has a centered `RectTransform` whose bounds exactly enclose its direct child `RectTransform` bounds; do not use full-stretch containers for a semantic group;
- every repeated unit has an explicit direct-child membership contract in `verify.directChildren`; validate names and sibling order after saving rather than relying on a child count alone;
- every proposed component family has an explicit template, output asset path, and complete instance list; it runs in a separate plan after hierarchy cleanup so source paths and external-reference checks remain unambiguous;
- every proposed wrapper has at least two evidence-backed members, unless it is a single semantic region required to contain a verified set of child groups;
- ambiguous nodes remain in place and are reported.

### 3. Review Before Apply

Present a compact, complete proposed tree. Identify:

- target Prefab and whether output will be a copy or in-place;
- new wrappers and each member path;
- each component family: template instance, all member instances, shared nested Prefab path, and the instance-level properties expected to vary;
- every scanner candidate that was rejected, when a similar-looking unit was left unextracted;
- unchanged/ambiguous nodes and why they were left untouched;
- expected invariants and verification checks.

Default to a sibling output named `<original>.cleaned.prefab`. Use `output.mode: in_place` only when the user explicitly requests source-Prefab cleanup after reviewing the plan.

For an in-place semantic asset rename, review the exact mapping, including the PrefabName and each `PrefabName_SemanticName` target. Include the sibling SpriteAtlas only when it is private to the same Prefab and has been explicitly listed in the plan.

Stop here until the user explicitly confirms the plan.

### 4. Apply Through Unity

After confirmation, run `scripts/run_prefab_hierarchy_cleanup.ps1 -ApplyConfirmed`. The generated Unity operation uses Editor APIs only:

- Load with `PrefabUtility.LoadPrefabContents`.
- Create only the approved wrapper `GameObject`s, transfer the approved transforms under them, preserve sibling order, then tighten each wrapper to its direct-child bounds while preserving every existing child's world corners.
- For a standalone `componentExtractions` plan, save a named shared component Prefab from the approved template, replace every approved source unit with a nested instance, and copy every source value into the instance as an override. The operation rejects a structural mismatch, nested source Prefab, external reference, or existing output asset.
- Save the copied output with `PrefabUtility.SaveAsPrefabAsset`, or save the original only with explicit in-place authorization.
- Unload Prefab contents in a `finally` path.

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
- each extracted unit is a nested Prefab instance sourced from the approved shared component asset, with every affected RectTransform world corner preserved within `0.01`;
- `Missing Component = 0`;
- visual comparison in Prefab Stage when available.

Run `uloop compile` only when this work changed C# source. Asset-only cleanup does not need a compilation claim; report whether Unity successfully loaded and saved the Prefab instead.

If any invariant fails, preserve the original, do not describe the cleanup as complete, and use the snapshot to narrow the failed operation before attempting a corrected copy.

## Explicit Non-Goals

- Do not access Figma, use Figma MCP, or treat Figma node/component semantics as Unity data.
- Do not generate a Prefab from a PSD or Figma design.
- Do not infer or attach runtime scripts, serialized bindings, Animator transitions, interaction semantics, or asset replacements.
- Do not make a coarse tree appear complete through cosmetic names alone.

## Invocation Examples

- `Use $prefab-hierarchy-cleanup to inspect Assets/UI/RewardPanel.prefab and propose a complete hierarchy. Do not modify it yet.`
- `Use $prefab-hierarchy-cleanup on Assets/PSDLayoutTool2/TestData/Example.prefab. Create a separate .cleaned.prefab only after I confirm the proposed tree.`
- `Use $prefab-hierarchy-cleanup to organize this UI in place and rename its private textures to RewardPanelView_SemanticName. Infer the View name and ask me to confirm the rename plan first.`
