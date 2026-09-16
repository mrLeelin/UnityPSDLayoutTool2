---
name: prefab-hierarchy-cleanup
description: Safely organize one existing Unity Prefab in place into a complete semantic hierarchy while preserving its visual result and serialized behavior. Use only when explicitly invoked as $prefab-hierarchy-cleanup to inspect, plan, review, and optionally apply cleanup to a Unity .prefab; especially when the existing hierarchy is flat, PSD-generated, or hard to maintain. Use for one approved in-place plan that adds semantic containers, replaces PSD/export node names with semantic English names, renames proven-private Texture or SpriteAtlas assets to PrefabName_SemanticName, and extracts approved shared nested components. Never create, copy, or replace the target Prefab. Do not use for Figma cleanup, PSD import, Figma-to-Prefab generation, or runtime UI redesign.
---

# Prefab Hierarchy Cleanup

Organize existing Unity Prefabs by transferring the *discipline* of Figma hierarchy cleanup, not Figma's node model or tooling. Treat Unity components, serialized bindings, RectTransforms, asset references, prefab overrides, and sibling order as source-of-truth data.

> This project's engine deviates from the written plan format in ways that have already caused
> real failures (auto-tightening, extraction-stage counts, text snapshots without node ids). Read
> **Project-verified engine realities** at the end of this file before authoring a plan.

## AI Chat Single-Confirmation Contract

When this skill is supplied to the Unity AI hierarchy chat window, use exactly this interaction:

1. The Unity window first generates an authoritative node snapshot through Unity Editor APIs. The first confirmable AI reply presents the complete review in Markdown tables and one complete UTF-8 JSON plan in a `json` code block. The tables must cover grouping and naming, child Prefab extraction, preserved or ambiguous content, and verification. The child-Prefab table must disclose every output path, extraction mode, ordered instance, ordered Common member, state ID/name/member list, and default or per-instance state. The root object must use `"version": 2`, copy the snapshot `fingerprint` into `snapshotFingerprint`, reference every existing node as `node:<id>`, include every required operation array from `references/plan-format.md`, and record extraction work that becomes addressable only after grouping in `postGroupingExtractionIntents`.
2. The user may correct the tables before approval. The eventual explicit `确认`, `满意`, or equivalent apply intent is the only confirmation. It authorizes the complete reviewed workflow: first-stage hierarchy apply, authoritative resnapshot, exact manifest-matching child-Prefab extraction, save, and final verification.
3. After that confirmation, the Unity chat window rechecks the Prefab fingerprint, applies the first stage, refreshes the snapshot, automatically generates and validates the component-only second stage, and applies it without asking again. The second stage must exactly match the confirmed `postGroupingExtractionIntents` IDs, modes, output paths, ordered instance names and states, ordered Common members, state IDs, names and members, and default states. It may not include hierarchy, rename, containment, flat-sibling, or asset-rename operations.

The one confirmation is sufficient authorization for every stage that exactly matches the displayed tables and machine-readable manifest. Do not ask the user to choose an output mode, repeat confirmation, approve the refreshed snapshot, approve child Prefabs separately, or manually run a script. A revision requested before confirmation replaces the review and still leads to one eventual confirmation. After execution starts, evidence that conflicts with the confirmed manifest is a blocking failure: stop, report the exact mismatch and current saved state, and never guess, broaden scope, or obtain a second confirmation as a bypass. Before enabling confirmation, the chat window must reject a plan whose `version`, required fields, `snapshotFingerprint`, `prefabAssetPath`, `output.assetPath`, assets, node references, or post-grouping intent shape are invalid, then run the same renderer validation used by the runner on the converted internal plan. That validation simulates wrapper creation, moves, renames, tight bounds, and ordered empty-container removals against an unsaved Prefab instance, then resnapshots that simulated tree and proves every `verify.hierarchy`, `verify.directChildren`, and `verify.tightBounds` entry. It must also inspect the simulated tree's complete `componentFamilyCandidates`, `containmentFindings`, and `flatSiblingFindings` before confirmation. An existing-node reference must be copied from the Unity-generated snapshot; it must never be inferred from displayed text, a Sprite name, a visual label, or a guessed hierarchy path.

`postGroupingExtractionIntents: []` is allowed only when the simulated post-grouping snapshot has no required component family and no repeated family that the reviewed scope requires or clearly implies extracting. If grouping creates a required family, the first confirmable response must disclose and freeze its complete extraction intent. If the simulated snapshot introduces an undisclosed candidate, unresolved cluster, changed extraction path, or changed member/state mapping, reject the plan before confirmation and report the exact difference without changing the real Prefab.

Asset GUIDs are Unity-owned data. In a version 2 AI chat plan, use an empty `expectedGuid` for Texture and SpriteAtlas renames. The chat execution bridge resolves each existing `from` path through `AssetDatabase`, rejects a missing asset with the exact array index and path, and injects the current GUID into the internal version 1 runner plan. The runner then keeps enforcing that captured GUID before and after rename so an identity change between validation and apply remains blocking.

Private-asset `prefabName` is also execution-derived in a version 2 AI chat plan. The reviewed fields are the actual `textureRenames[].toName` and `spriteAtlasRenames[].toName` values: every Texture target contributes the prefix before its first underscore, every SpriteAtlas target contributes its complete name, and all candidates must agree on one PascalCase name ending with `View`. The chat bridge writes that candidate into the internal version 1 runner plan. Keep the required version 2 `prefabName` field present for schema stability, but do not treat it as authoritative or try to repair a naming failure by changing only that field. Conflicting or invalid reviewed targets fail before the external runner and report the submitted value, candidates, array indices, and complete `toName` values.

Every AI-specific prompt must supply the canonical plan format and authoritative node snapshot before the first response. The snapshot is authoritative for hierarchy, node identity, geometry, components, active state, and references. Read only a targeted Prefab section when the snapshot explicitly lacks evidence or conflicts with serialized data; do not repeatedly read the complete Prefab and snapshot. Do not infer JSON names, node IDs, or paths from prose or use a legacy schema. The complete workflow has one automatic JSON-repair budget. Keep that repair in the same AI session and request one complete replacement JSON code block with no prose. The automatic second stage has no additional repair budget: any invalid or manifest-mismatching result stops immediately. The repair may use only IDs present in the original snapshot; it must remove an operation whose intended source cannot be proven instead of inventing another ID. If the replacement still fails, leave the Prefab unchanged, disable confirmation, and report the final validation failure.

## Bundled Tools

Use a supported Unity Editor API execution path. When the project setting is `NativeUnity`, keep the entire standard workflow on that backend. Do not invoke uloop merely because a bundled fallback script exists. Do not hand-edit serialized Unity files or write one-off cleanup payloads.

1. Inspect first through `scripts/snapshot_prefab_hierarchy.ps1`; it is read-only and emits the complete tree, RectTransform state, UI components, Sprite/Texture paths, TMP state, nested Prefab boundaries, and counts.
2. Run `scripts/find_prefab_component_candidates.py` whenever the snapshot contains repeated visual units. The Unity AI chat snapshot also emits numbered repeated-family candidates. Record every candidate in `componentFamilyDecisions`; a chat candidate marked `requiresExtraction: true` must be extracted and cannot be skipped. A candidate marked `requiresExtraction: false` is advisory and may be skipped with concrete recursive-structure evidence. A family whose members are not all structurally identical also reports `numbered_structure_subset` candidates: the members that do share one recursive structure. Prefer the family-level extraction when it is marked required, and use a subset when the family only fits a variant or when a narrower boundary is cleaner. A single-member subset is a report that the member has no peer, not an instruction to extract it alone.
3. For a direct script run, create an internal version 1 JSON plan from the corresponding section of [references/plan-format.md](references/plan-format.md), starting from [examples/sample-plan.json](examples/sample-plan.json). The Unity AI chat window instead creates version 2 node-ID plans and performs the conversion itself.
4. Validate and execute through the Unity AI chat window's selected backend. For a repeatable command-line run, use `scripts/run_native_cleanup.py`; it is the canonical NativeUnity runner and never falls back to uloop. The legacy PowerShell runner is fallback-only and may be used only when uloop was explicitly selected as the backend.
5. Use `-ApplyConfirmed` only after the user has reviewed and explicitly confirmed the complete tree, the exact in-place target path, `PrefabName`, and every Texture/Atlas rename.
6. If Unity or the wrapper times out after an apply attempt, do not apply again. Run the same plan with `-VerifyOnly` to determine the actual saved state.

```powershell
& <skill-dir>/scripts/run_prefab_hierarchy_cleanup.ps1 `
  -ProjectPath "E:\\Project\\Demo\\monsterhunter" `
  -PlanPath "C:\\Temp\\reward-panel.plan.json" `
  -AllowUloopFallback `
  -ApplyConfirmed
```

```powershell
& <skill-dir>/scripts/snapshot_prefab_hierarchy.ps1 `
  -ProjectPath "E:\\Project\\Demo\\monsterhunter" `
  -PrefabAssetPath "Assets/UI/RewardPanel.prefab"
```

The snapshot script calls the registered NativeUnity `eval_file` Unity Pipeline command and therefore never invokes uloop. The optional runner is uloop-only, requires `-AllowUloopFallback`, never installs or downloads uloop, and removes its temporary UTF-8 C# payload after the Unity call returns.

Use `-CompileOnly` only after the renderer, runner, or generated C# source changes. An ordinary new extraction plan uses one preflight and one apply; do not add a redundant compile-only pass.

### NativeUnity run ledger and timing contract

Every snapshot, preflight, apply, and verify invocation must go through `scripts/run_native_cleanup.py` (or an equivalent Unity chat bridge that emits the same evidence). The runner creates a unique `Library/PrefabCleanupRuns/<run-id>/` directory containing:

- `events.jsonl`: one flushed JSON object for every phase start, periodic heartbeat, phase end, and workflow error. Each event includes the target, UTC timestamp, phase, status, exit code when available, and elapsed milliseconds from both the phase and run start.
- `summary.json`: final status, total elapsed time, every completed phase, and the slowest phases. A missing summary or non-`passed` status is not completion evidence.
- phase stdout/stderr and the validated phase result, plus a frozen copy of the exact plan used for mutation.

The heartbeat interval must be bounded and visible while Unity is compiling, importing, saving, or waiting. Never infer a hang from silence. If the CLI times out during Apply, the state is indeterminate: do not replay Apply or create a second mutation plan. Run Verify with the same frozen plan, inspect the ledger, and only then decide whether a narrowly corrected plan is safe.

The runner's phase semantics are exact: Snapshot is read-only; Preflight simulates the complete unsaved tree and rejects any contract mismatch; Apply is the only mutating phase and requires the already-reviewed plan plus explicit apply authorization; Verify reopens the saved asset and checks the same manifest, hierarchy, names, references, layout, extraction topology, image ownership, and missing-component invariants. A zero exit code without a validated `PREFLIGHT_OK`/`VERIFY_OK` result is failure.

```powershell
& <skill-dir>/scripts/run_prefab_hierarchy_cleanup.ps1 `
  -ProjectPath "E:\\Project\\Demo\\monsterhunter" `
  -PlanPath "C:\\Temp\\reward-panel-components.plan.json" `
  -AllowUloopFallback `
  -CompileOnly
```

```powershell
$priorOutputEncoding = $OutputEncoding
try {
  $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
  $snapshot = & <skill-dir>/scripts/snapshot_prefab_hierarchy.ps1 `
    -ProjectPath "E:\\Project\\Demo\\monsterhunter" `
    -PrefabAssetPath "Assets/UI/RewardPanel.prefab"
  ($snapshot | ConvertFrom-Json).data.result |
    python <skill-dir>/scripts/find_prefab_component_candidates.py -
}
finally { $OutputEncoding = $priorOutputEncoding }
```

## Operating Contract

- Work only on the named `.prefab` asset. Do not require, query, or modify Figma.
- Always organize the named target Prefab in place. Its plan must use `output.mode: in_place` and the same `output.assetPath` as `prefabAssetPath`; never create, offer, or ask the user to choose a `.cleaned.prefab`, duplicate, or replacement target Prefab. Preserve the root object's asset-compatible name: never rename the root to a name different from `Path.GetFileNameWithoutExtension(prefabAssetPath)` or put a different root name into post-apply verification paths.
- Start read-only. Inspect the complete hierarchy, components, RectTransforms, active state, asset references, and nested Prefab instances before proposing changes.
- Infer semantic groups from geometry, component/type patterns, visible hierarchy, repeated structures, and existing names. Names are a hint, never sole membership evidence.
- Produce one complete final-tree proposal before applying. Include nested repeated units such as `[Item_*]`, cards, progress segments, map markers, tabs, or rows; do not stop at coarse region wrappers.
- Assign every moved source node exactly once. Rename every exported object to a concise English semantic name; retain brackets only for structural groups such as `[ContentCard_1]`. A text value (`20`, `+`, `150k`, `login 1 day`), PSD/export token (for example a configured `ui_*` or `img_v*` prefix), UUID-like export name, punctuation-only name, or duplicate ambiguous sibling name is not semantic. Name the role, such as `ActivityAmount`, `PlusLabel`, `MissionIcon`, or `MissionDescription`. With `verify.requireEnglishNames`, the runner rejects those baseline invalid forms even when a plan omits custom patterns. Never delete, duplicate, merge, replace components, or change bindings merely to make the tree look cleaner. The sole structural exception is `emptyContainerRemovals`: list a pre-existing container only when every one of its current direct children is moved out or is itself removed earlier in the same removal list. Nested removals must use child-before-parent order. The resulting container must be empty, have no components beyond its `Transform`, and have no external serialized references.
- Every plan-owned ID, including `wrappers[].id`, extraction `id`, and state `id`, must be lower snake_case matching `^[a-z][a-z0-9_]*$`. Use IDs such as `screen_root`, `day_markers`, or `task_in_progress`; never use PascalCase, kebab-case, spaces, brackets, or an `@` prefix. The `@` prefix is reserved only for a later reference to an earlier wrapper, such as `@screen_root`.
- Treat ambiguous membership, a likely serialized binding, nested Prefab boundary, or unfamiliar custom component as a blocker. State the ambiguity and leave that portion unchanged rather than guessing.
- Require explicit user confirmation of the reviewed plan before writing. A request to inspect or organize does not authorize an immediate asset mutation.
- For a `PrefabName_SemanticName` rename, infer an English PascalCase `PrefabName` from the UI's actual function, require it to end in `View`, and ask the user to confirm it before mutation.
- Rename only Texture and SpriteAtlas assets that are proven private to the named Prefab. When a private Texture directory is in scope, list and rename every Texture in it to `PrefabName_SemanticName`; do not leave a partial prefix migration. Use `AssetDatabase.RenameAsset`; never use file-system moves or create replacement `.meta` files.
- When the user explicitly asks for reusable Prefabs with visual states, include every approved family in the same explicitly reviewed in-place plan. `output.mode: in_place` prohibits a replacement screen Prefab, not a shared child asset under the target Prefab's sibling `Common` directory. Require either at least two structurally matching units, or at least three numbered semantic units under one parent with matching anchors and pivot; no source may cross a nested Prefab boundary or retain an external serialized reference.
- Treat the candidate scanner as a discovery aid, not a safety proof. It reports both strictly matching structures and high-confidence numbered families; the latter may vary in `sizeDelta` or state structure, which must be represented as reviewed instance overrides or a stateful mapping. Every result still passes the Unity-side nested-boundary, external-reference, and structural checks during apply.
- Treat visually overlapping alternatives as states, not repeated instances. A `stateComponentExtractions` plan replaces all approved sibling state sources with one nested component containing a `[States]` container. It requires an explicit semantic state mapping and a single default state; never infer those names from layer order alone.
- Treat simultaneously visible list rows that share one logical component but display at least two distinct observed visual states as a `variantComponentExtractions` family. Create exactly one shared Prefab under `Prefab/Common/`; its root must contain direct `[Common]` and `[States]` children. Use one observed representative row in `states[].source` for each unique state, then list every approved row exactly once in `instances`; multiple instances may select the same state. If all visible rows have one observed state, use `componentExtractions` instead and do not invent a state branch. Replace every approved source row with an instance of that same Prefab, preserve the row's list position and instance name, and activate exactly the mapped state in each instance. `[Common]` may be empty only when no element can be proven common to every state without changing the rendering.
- Treat a repeated unit with both stable members and a finite set of visual variants as a `statefulComponentExtractions` family. Create exactly one shared Prefab under `Prefab/Common/`, with direct `[States]` followed by `[Common]` children so shared labels still render above state backgrounds. Every source unit must map every direct member exactly once into `[Common]` or its selected state; `Common` must contain every member proven common. A named all-common state may have an empty `members` list only when its instances map every direct child through `commonSourceNames` and use an empty `stateSourceNames` list; never use an empty state to hide an unmapped child. In the Unity chat, an incomplete Common or selected-state list is rebuilt from the authoritative snapshot only when the opposite side or the reviewed source contract proves the partition, and the other side is the exact ordered complement; the direct runner keeps requiring both explicit complete mappings. Reuse one semantic state branch across all matching instances and apply only the approved instance overrides, such as a counter value. Do not create a distinct state solely because an instance label differs.
- State extraction creates hierarchy and the initial active branch only. It does not create or attach a runtime state-switching script, Animator, or binding; use the project's established presentation owner to switch the branches later.

## Workflow

### 1. Preflight and Snapshot

1. Confirm the target is one `.prefab` under `Assets/`, inspect `git status`, and preserve unrelated work.
2. Capture the complete hierarchy, sibling order, active state, RectTransform geometry, component types, serialized references, Sprite/TMP/material assignments, nested Prefab boundaries, overrides, and missing components through Unity Editor APIs.
3. Treat the Unity-generated snapshot as authoritative. Visual review is additional evidence, not a substitute for the serialized snapshot.
4. Review every `componentFamilyCandidates`, `containmentFindings`, and `flatSiblingFindings` entry before proposing a plan.

### 2. Infer and Validate the Complete Tree

- Build the final hierarchy from every current direct and nested child. Include inactive and hidden nodes.
- Infer membership from geometry, component signatures, sibling order, repeated structure, and names together; names alone never prove membership.
- Keep each repeated visual unit together. Do not flatten its background, label, value, badge, lock, status, or icon into type-based containers. An explicit structural group named `[... ]` is already a resolved semantic boundary and its direct leaves must not produce a new `flatSiblingFinding`. Perform a visual-unit closure audit for every remaining wrapper and `flatSiblingFinding`: inspect the complete parent sibling sequence, geometry, component roles, text/icon semantics, and active state. A detector's `members` array is a conservative seed, not proof that no adjacent same-slot foreground or status child belongs to the unit; include every proven member in the reviewed move and `verify.directChildren`, or report the ambiguity and leave it unchanged.
- Keep region-scale backgrounds outside foreground repeated units. Do not introduce `ScrollView > Viewport > Content` without an existing `ScrollRect` or equivalent project evidence.
- Preserve relative sibling order and world-space RectTransform corners. Do not cross nested Prefab boundaries or modify components, references, active states, or asset ownership.
- Assign every affected source exactly once. Every repeated unit must have an explicit ordered `verify.directChildren` contract, not only a child count.
- Resolve every measured containment and flat-sibling finding. Record every component candidate decision; a required candidate cannot be skipped.
- Before review, run one read-only preflight plus one unsaved post-grouping simulation. The simulation must resnapshot the simulated tree, validate every final verification path and direct-child order, and audit projected repeated-family candidates. If it fails, exceeds its timeout, leaves visual-unit closure ambiguous, or produces an extraction candidate not fully disclosed in `postGroupingExtractionIntents`, stop. Do not repair the Prefab, start Apply, or ask AI again.

### 3. Review Before Apply

Present complete Markdown tables for the in-place target, grouping and naming, every child-Prefab extraction, preserved or ambiguous nodes, risks, and verification. Disclose exact node identities, ordered members, output paths, modes, templates, per-instance mappings, Common members, states, and defaults. Include the simulated post-grouping root path, every projected component candidate, and the evidence for each extraction or skip decision. `postGroupingExtractionIntents` must freeze full post-grouping paths and source-member mappings, not leaf names. Stop until the user gives the single explicit confirmation.

### Hard Prohibition: Never Repair Prefabs as Text

Never use Python, Shell, regular expressions, or other text processing to edit `.prefab`, `.asset`, or `.unity` serialized contents. All hierarchy mutations must use Unity Editor APIs.

- Use `PrefabUtility`, `GameObject`, `Transform`, `AssetDatabase`, and the project's supported NativeUnity or runner backend.
- Each stage gets one Apply attempt. A timeout or indeterminate result permits exactly one read-only verification of the saved state, then stops.
- Never automatically rerun the same plan, restore a backup, reimport the PSD, generate a third plan, or continue to another AI stage after an Apply failure or contract warning.
- A failed Apply may have partially saved the Prefab. Report the observed saved state; never assume either the original or intended state.

### 4. Apply Through Unity

After the single confirmation, apply through the backend selected in the Unity AI chat window. With `NativeUnity`, do not call the uloop runner. If the confirmed hierarchy stage creates the repeated-unit roots needed for extraction, immediately resnapshot, generate the component-only plan constrained by `postGroupingExtractionIntents`, preflight it, and apply it through the same selected backend without asking the user. The generated Unity operations use Editor APIs only:

- Load with `PrefabUtility.LoadPrefabContents`.
- Create only the approved wrapper `GameObject`s, transfer the approved transforms under them, preserve sibling order, then tighten each wrapper to its direct-child bounds while preserving every existing child's world corners.
- For `componentExtractions`, save a named shared component Prefab from the approved template, replace every approved source unit with a nested instance, and copy every source value into the instance as an override. The operation rejects a structural mismatch, nested source Prefab, or external reference, and overwrites the approved output asset path with the current extraction.
- For `stateComponentExtractions`, clone the approved sibling state roots under one `[States]` container, activate only the reviewed default state, replace all state roots with one nested instance, reject nested Prefabs, external references, or non-sibling sources, and overwrite the declared output asset path with the current extraction.
- For `variantComponentExtractions`, create one shared component with direct `[Common]` and `[States]` children, clone each approved visual state under `[States]`, then replace every source list row with a nested instance of that component and activate the reviewed state for that row. It rejects nested source Prefabs, external references, overlapping source paths, or incomplete instance coverage, and overwrites the declared output asset path with the current extraction.
- For `statefulComponentExtractions`, create one shared component with direct `[States]` and `[Common]` children, build the reviewed state branches once, replace every approved source with a nested instance, then apply only its reviewed Common and selected-state member overrides. It rejects nested Prefabs, external references, unmapped direct members, overlapping sources, or incomplete member mapping, and overwrites the declared output asset path with the current extraction.
- Save only the exact target Prefab path with `PrefabUtility.SaveAsPrefabAsset`; Unity's API name is retained, but it overwrites the already loaded target asset in place.
- Unload Prefab contents in a `finally` path.

Only the reviewed in-place plan may create a shared nested component asset. Its source-unit validation, output path, state mapping, replacement instances, and `componentFamilyDecisions` coverage must be explicit. An ordinary hierarchy cleanup may leave extraction arrays empty only after recording every advisory candidate as an evidence-backed skip; high-confidence chat candidates may not be skipped.

When the confirmed plan includes private asset renames, it also verifies the original GUID for every Texture after `AssetDatabase.RenameAsset`, then reloads the saved Prefab and checks all `Image` Sprite references.

For this project, use the existing NativeUnity backend when it is selected. The bundled uloop runner is an explicit fallback only and requires `-AllowUloopFallback`; it must never be reached implicitly from a NativeUnity workflow. Never download or install an execution tool during a cleanup run.

Never edit `.prefab` YAML text, reconstruct the Prefab, reimport PSD assets, regenerate textures, manually move files, or create replacement `.meta` files as part of hierarchy cleanup.

### 5. Verify the Saved Asset

Reopen the saved Prefab and compare it with the before snapshot. Report evidence for:

- complete final tree, exact wrapper membership, and preserved affected-node sibling order;
- unique assignment of all moved nodes;
- existing RectTransform world corners preserved within `0.01` where reparenting changes hierarchy, and every structural container's bounds exactly match its direct children;
- all object names are English semantic names; no PSD/export, Chinese, or punctuation-heavy source names remain;
- preserved active states, component counts/types, object references, Sprite/TMP/font/material assignments, and nested Prefab boundaries;
- missing Sprite references on the target Prefab itself are target-owned verification issues and must be surfaced, never silently classified as inherited nested-Prefab issues. A missing Sprite inside an unchanged nested Prefab instance is reported separately by path; fix that source asset through its own owner rather than crossing the nested boundary;
- after a successful save, surface any `VERIFY_WARN issue=...` and stop before another stage. A hierarchy, membership, path, state, reference, layout, or manifest mismatch is blocking and is never completion proof. The pre-confirmation simulation must treat the same mismatch as a rejection, not a warning that may reach user confirmation;
- each separately extracted unit is a nested Prefab instance sourced from the approved shared component asset, with every affected RectTransform world corner preserved within `0.01`;
- each separately extracted state component is a nested Prefab instance with one direct `[States]` container, all approved state branch names in order, exactly one active default branch, and every state branch's world corners preserved within `0.01`;
- each separately extracted variant component is a nested instance of the one reviewed `Prefab/Common` asset, has direct `[Common]` and `[States]` containers, contains every approved state branch in order, and has exactly its mapped branch active;
- each separately extracted stateful component is a nested instance of the one reviewed `Prefab/Common` asset, has non-empty `[Common]` and `[States]` containers, contains each approved state branch once, maps every source direct member exactly once, and has exactly its mapped branch active;
- all private Texture files in scope use the exact `PrefabName_` filename prefix, and `verify.forbiddenObjectNamePatterns` rejects residual PSD/export or text-value node names;
- `Missing Component = 0`;
- every pre-existing node is still present with an unchanged fingerprint (component types, `Image`
  sprite, TMP text, `sizeDelta`/anchors/pivot/`localScale`/`rotationZ`), and every extracted unit's
  `[Common]` + active state branch reproduces the unit it replaced - prove it with
  `audit_prefab_preservation.py --mode compare` against a capture taken **before** the apply;
  compare on reparent-invariant values, never on `anchoredPosition`, and treat world rects as AABBs
  (four corners), because `corner[0]`/`corner[2]` mis-measures a rotated rect by `s*(cos-sin)` vs
  `s*(cos+sin)` and fakes a sub-pixel difference across tools;
- visual comparison in Prefab Stage when available.

The preservation audit is the only check that proves *content* survival, and it needs a capture from
BEFORE the apply - so capture at the start of every mutating stage, not at verification time:

```bash
# once, before the stage's Apply (read-only; ~2-5 s)
python scripts/audit_prefab_preservation.py --project-path <project> --mode capture \
       --prefab-path Assets/.../Target.prefab --out before.json
# after the Apply (pure offline, no Editor needed; needs --plan for extraction stages)
python scripts/audit_prefab_preservation.py --mode compare \
       --before before.json --after after.json --plan stage.plan.json
```

It reports `A` lost/changed nodes, `B` per-unit content loss/duplication (which is also the proof
that per-instance overrides really wrote each instance's own values), and `C` instance links,
the naming gate and target-owned missing Sprites. `capture` must run before the apply: pointing it
at a post-apply tree makes `B` compare an instance's inactive branches against itself and report a
false loss (the tool warns about this).

Run a Unity compilation only when C# source changed. Asset-only cleanup does not need a compilation claim; report whether Unity successfully loaded, saved, and verified the Prefab instead.

If any invariant fails, preserve the original, do not describe the cleanup as complete, and use the snapshot to narrow the failed in-place operation before any retry.

## Explicit Non-Goals

- Do not access Figma, use Figma MCP, or treat Figma node/component semantics as Unity data.
- Do not generate a Prefab from a PSD or Figma design.
- Do not create or copy a replacement screen Prefab. A shared nested component Prefab is allowed through a separately approved component-extraction plan when the user asks for reusable or stateful units; do not silently omit a detected repeated family.
- Do not infer or attach runtime scripts, serialized bindings, Animator transitions, interaction semantics, or asset replacements.
- Do not infer state semantics, default state, or a runtime state-switching mechanism from visual overlap alone.
- Do not make a coarse tree appear complete through cosmetic names alone.

## Business-Completeness Gate

Technical verification is not completion proof. A cleanup must be rejected as incomplete when it only adds outer wrappers or merely preserves node/component counts.

- Identify every complete visible business unit before editing. For task, reward, card, row, marker, or button screens, a unit includes its background, icon, labels, values, progress/status elements, action control, and state overlays when they occupy one logical visual region.
- Repeated business units must be compared recursively. When reusable Prefabs are in scope, extract complete proven families under `Prefab/Common/`, preserving each instance's rendering and serialized values. Shared role names alone are not structural proof. If extraction is skipped, list the actual structural, binding, or draw-order evidence. Do not force a lone icon extraction to satisfy a numerical quota.
- Do not treat `componentFamilyCandidates: []` as evidence that no business-level repeated unit exists. The candidate scanner is only a discovery aid; inspect numbered groups, sibling order, geometry, component roles, and asset patterns recursively.
- Every remaining internal object name in the affected reviewed scope must be an English semantic name. Chinese names, numeric-only names, punctuation-only names, PSD/export tokens, UUID-like names, and raw display values are not acceptable semantic names, including pre-existing group names. The single main root must retain the exact asset filename; this identity exception does not exempt any descendant or nested-component root.
- A plan that only creates containers, leaves Chinese/export names, fails to map Common/State/Instance members, or does not explain repeated-unit extraction is incomplete and must not be applied or reported as finished.
- Final verification must explicitly prove: zero Chinese or raw-value internal node names in scope; every repeated unit has a complete member mapping; the approved common Prefab exists; every approved source was replaced exactly once; and the resulting hierarchy is semantically usable, not merely technically valid.

### Evidence-driven continuation and visuals

- A missing intermediate wrapper is work to plan, not an external blocker. Create an exact source-to-destination map from the current snapshot and simulate it. Never transplant fixture paths, GUIDs, state labels, or expected counts into a production plan by string replacement.
- Distinguish separate date selectors from task rows using geometry and rendered content. A visible label may be baked into a Sprite; do not invent a TMP node or deduce node identity from displayed numbers.
- For a direct iterative maintenance session with explicit end-to-end apply authorization, record each revised in-scope plan before running it, then preflight, apply once, and verify. The AI chat bridge's frozen-manifest confirmation contract remains unchanged. A definitely read-only preflight rejection may be corrected and retested; an uncertain Apply must be verified before any new mutation.
- Establish a reproducible offscreen render of the exact target before editing. Compare after hierarchy changes and after extraction, including overlapping layers and inactive state branches. World-corner preservation alone does not prove draw-order preservation. A visual mismatch prevents completion and requires an evidence-backed correction, not a relaxed tolerance.
- Audit image ownership using current AssetDatabase references and retain GUID identity with RenameAsset. A directory can mix private and shared images; rename all proven-private images, but list externally owned/shared exceptions instead of treating the entire directory as private. Do not silently rename a global catalog's assets to a screen-specific prefix.
- When requested, perform hierarchy and private image naming first; extract common Prefabs last. Preserve the approved boundaries and exact per-instance state/member mapping across stages.

## Invocation Examples

- `Use $prefab-hierarchy-cleanup to inspect Assets/UI/RewardPanel.prefab and propose a complete hierarchy. Do not modify it yet.`
- `Use $prefab-hierarchy-cleanup on Assets/PSDLayoutTool2/TestData/Example.prefab. Propose the complete in-place hierarchy cleanup before applying it.`
- `Use $prefab-hierarchy-cleanup to organize this UI in place and rename its private textures to RewardPanelView_SemanticName. Infer the View name and ask me to confirm the rename plan first.`

## Project-verified engine realities (read this before authoring a plan)

Measured in this project with the NativeUnity backend (`run_native_cleanup.py`, `unity.exe command
eval_file`). Two of them contradict the written plan format and have already produced real damage,
so they are rules, not tips.

- **R1 — New containers are not tightened automatically.** A wrapper created by a plan keeps
  `sizeDelta = 0` unless the same plan lists it in `tightBounds`. Always emit `tightBounds` for
  **every** new wrapper (inner -> outer). A later stage that tightens a *parent* computes the union
  of its children, so a parent tightened while its children are still 0 x 0 also collapses to 0 x 0.
  `validate_plan_locally.py` fails this rule as a warning before the Unity preflight.
- **R2 — Extraction stages expand the tree.** `componentExtractions`, `stateComponentExtractions`,
  `variantComponentExtractions` and `statefulComponentExtractions` replace source units with nested
  instances, so `verify.nodes|components|images` must be the **post-expansion** expectation. Passing
  the pre-apply snapshot numbers gives `VERIFY_WARN issue=nodes expected=.. actual=..` *after* the
  asset is already saved. Prefer omitting those counts in an extraction stage, or take a fresh
  snapshot first.
- **R3 — The CLI text snapshot has no node ids.** `snapshot_prefab_hierarchy.ps1` (and the runner's
  Snapshot phase) emit `SUMMARY`/`NODE` text with paths, rects, active flags and children, but no
  `node:<id>`. Such a snapshot can only author a **version 1 (path) plan**; a version 2 (node-id)
  plan needs the Unity chat window's JSON snapshot. See `parse_hierarchy_snapshot.py`.
- **R4 — A transport timeout is not a contract mismatch.** If the `verify` phase fails with
  `Pipeline server returned 400 Bad Request ... Main thread operation timed out`, the Apply phase has
  already returned `VERIFY_OK`. Do not replay Apply: re-run `--mode verify` once (read-only) or run
  the read-only checkers, and report the ledger paths.
- **R5 — `eval_file` payloads are top-level statements only.** No `using` directives (they fail to
  compile with "Identifier expected"); `UnityEngine`, `UnityEditor`, `System` and
  `System.Collections.Generic` are implicitly available; qualify `UnityEngine.UI.*` explicitly, and
  `return` a string to get it back in the CLI result.
- **R6 — Never name a version 1 plan `*.plan.json`.** The Unity inspector button "应用AI计划" applies
  the *newest* `*.plan.json` under `Library/PsdHierarchyTerminal` and requires version 2, so a v1 file
  with that suffix is picked up and rejected. Use e.g. `<session>.rectfix.v1.json`.
- **R7 — What the plan language cannot express** (verified: no `UnpackPrefab` anywhere in the project
  tooling, and the renderer deletes assets only in rollback/replay paths): moving nodes across a
  nested-Prefab boundary, editing an existing asset's internals, unpacking an instance, and deleting
  an asset. When a request needs one of these (e.g. "these two Prefabs should be one"), a plan cannot
  do it. The reviewed escape hatch is *bake-and-propagate*:
  1. `PrefabUtility.LoadPrefabContents(sourceAsset)` and read the member's components/rect values;
  2. add that member to the target asset's **state branch** (`LoadPrefabContents` on the target,
     `new GameObject(..., typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))`, copy the
     values, `SaveAsPrefabAsset`) — the member then propagates to every existing instance;
  3. in the target Prefab set each instance's member position override so the world corners are kept;
  4. remove the now-redundant source nodes/containers in the target Prefab;
  5. prove no reference remains (walk instance roots and compare
     `GetPrefabAssetPathOfNearestInstanceRoot`, or grep the saved text for the old GUID), only then
     `AssetDatabase.DeleteAsset`.
  Such a step bypasses the plan validator, so it needs explicit user approval, a before/after
  read-only snapshot and a link check; report it as an escape hatch, never as a normal stage.
- **R8 — Text-level checks of `.prefab` files must respect Unity's serialisation.** Names starting
  with `[` are written as `m_Name: '[Name]'` (quote-aware greps, or you get false "missing" results),
  and non-ASCII names are written escaped as `"日..."` — unescape before comparing. Never edit
  these files as text; this is only for read-only evidence.
- **R9 — Authored plan files never live inside this skill directory.** A plan JSON is a per-run
  artifact, not skill knowledge. Write every authored/frozen plan to
  `<project-root>/Library/PrefabCleanupPlans/` (already covered by the `Library/` gitignore rule);
  the runner's own evidence stays in `Library/PrefabCleanupRuns/<run-id>/`. Test fixtures are the
  only plan files that belong to the skill, and they live in `scripts/tests/fixtures/`. The former
  `plans/` directory at the skill root was removed on 2026-09-16 precisely because runtime output
  kept landing there untracked.

### Read-only toolbox added by this project

| script | purpose |
|---|---|
| `scripts/parse_hierarchy_snapshot.py` | normalise a text/JSON snapshot, print metrics + tree, emit a v1 plan skeleton |
| `scripts/validate_plan_locally.py` | offline linter: refs, uniqueness, removals, naming gate, directChildren, plus R1/R2 warnings |
| `scripts/simulate_and_verify_plan.py` | rebuild the final tree offline and prove `verify.hierarchy/directChildren/absentPaths` (caught a real double-wrapper defect) |
| `scripts/check_extraction_result.py` | prove a finished extraction: instance names/order, `[Common]`/`[States]`, exactly one active mapped state, and (stateful) the declared Common + state member names - covers `component`, `state`, `variant` and `stateful` |
| `scripts/audit_prefab_preservation.py` | **before/after preservation audit**: prove that every pre-existing node survived with the same components, sprite, TMP text and reparent-invariant RectTransform values, and that each extracted unit's `[Common]` + active branch reproduces the unit it replaced (`--mode capture` needs Unity, `--mode compare` is pure offline) |
| `scripts/read_unity_selection.py` | read the user's live Editor selection / Prefab Stage / nested-instance links (read-only, `--mode selection|instance-links|prefab-stage`) |
| `scripts/payloads/read_selection.cs` | the eval_file payload behind it (template for R5) |
| `scripts/payloads/audit_prefab_dump.cs` | the read-only tree+fingerprint dump behind the preservation audit (marker `DUMP_BEGIN`/`DUMP_END`) |

Typical session order: snapshot -> semantics -> plan -> `validate_plan_locally.py` ->
`simulate_and_verify_plan.py` -> `audit_prefab_preservation.py --mode capture` (BEFORE, per stage) ->
Unity preflight -> user confirmation -> one Apply ->
read-only verification (`check_extraction_result.py`, `audit_prefab_preservation.py --mode compare`,
container tightness, world-rect preservation,
`--mode instance-links`) -> record evidence in the review file.
