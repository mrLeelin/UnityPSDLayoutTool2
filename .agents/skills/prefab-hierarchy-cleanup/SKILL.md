---
name: prefab-hierarchy-cleanup
description: Safely organize one existing Unity Prefab in place into a complete semantic hierarchy while preserving its visual result and serialized behavior. Use only when explicitly invoked as $prefab-hierarchy-cleanup to inspect, plan, review, and optionally apply cleanup to a Unity .prefab; especially when the existing hierarchy is flat, PSD-generated, or hard to maintain. Use for one approved in-place plan that adds semantic containers, replaces PSD/export node names with semantic English names, renames proven-private Texture or SpriteAtlas assets to PrefabName_SemanticName, and extracts approved shared nested components. Never create, copy, or replace the target Prefab. Do not use for Figma cleanup, PSD import, Figma-to-Prefab generation, or runtime UI redesign.
---

# Prefab Hierarchy Cleanup

Organize existing Unity Prefabs by transferring the *discipline* of Figma hierarchy cleanup, not Figma's node model or tooling. Treat Unity components, serialized bindings, RectTransforms, asset references, prefab overrides, and sibling order as source-of-truth data.

When the AI hierarchy setting `organizeAnchors` is enabled, also read and follow [recttransform-anchor-cleanup](../recttransform-anchor-cleanup/SKILL.md). It is a companion review skill: it supplies the evidence and visual-preservation rules for RectTransform anchors, while this skill remains authoritative for the Prefab plan and apply workflow. When the setting is disabled, do not load or invoke that companion skill and preserve existing RectTransform layout values.

## Already-organized Prefabs

When the Unity terminal task declares `INCREMENTAL ADJUSTMENT`, the existing saved Prefab and its fresh authoritative snapshot are the baseline. The first response asks the user what to adjust and waits for the answer. Discuss unclear scope before planning. The review covers only the requested change and its necessary dependencies; the v2 JSON document remains complete, but its executable arrays contain only this round's operations. Record the user's request in `operationScope.requestedChange`, set `operationScope.kind` to `incremental_adjustment`, and list affected current nodes in `selectionNodeIds`. Preserve unrelated hierarchy, names, assets, bindings and child Prefabs. Existing unrelated `requiresExtraction` candidates do not expand the request. After a successful Apply, refresh the snapshot in a new AI整理 terminal session before another adjustment. This incremental session obeys the same mandatory single-agent rule as a full cleanup: the primary agent performs every stage itself and never spawns, resumes or delegates to a subagent, team or external advisor agent, including for the read-only review.

> This project's engine has behaviors that have already caused real failures (no automatic
> tightening, extraction-stage counts, a rename-locked Prefab root). Read
> **Project-verified engine realities** at the end of this file before authoring a plan.

> **Current capability (2026-09-17, ADR 0001/0002) — authoritative over the rest of this file.**
> The only formal write path is a version 2 `node:<id>` plan that a human approves in the terminal,
> after which the AI writes `<session>.apply`; Unity claims it (rename to `<session>.applying`),
> applies it in the Editor and writes `<session>.apply-result.json`. The current Unity executor
> performs `wrappers`, `moves`, `renames`, `tightBounds`, `emptyContainerRemovals`,
> `componentExtractions` (a matching `componentFamilyDecisions` entry uses mode `component`; the template node must
> also appear in `instances`; every instance must share the template's recursive component
> structure; `assetPath` must be a new PascalCase `.prefab` under `Assets/`) and
> `stateComponentExtractions` (mutually exclusive direct-sibling roots in one visual slot;
> `template` must be one of `states[].source`; those sources must not be referenced from outside)
> and `variantComponentExtractions` (rows visible at different list positions; every state
> representative must also appear once in `instances`; each instance's structure must match its
> selected state source), `statefulComponentExtractions` (repeated items with real shared content
> plus a few states; `[States]` is created before `[Common]`; every direct child of an instance
> source must be mapped exactly once by `commonSourceNames` + `stateSourceNames`), and
> `textureRenames` / `spriteAtlasRenames` (`toName` without extension; every Texture `toName` must
> start with `<prefabName>_`; every SpriteAtlas `toName` must equal `prefabName`; `prefabName` is
> the prefix you choose, independent of the Prefab file name; each `from` must be a private asset
> of the target Prefab; the target path must not exist yet; the rename keeps the asset identity so
> Prefab references stay valid).
> `postGroupingExtractionIntents` is executable too, but only as an automatic **second stage**:
> Unity saves and re-verifies the hierarchy stage, refreshes the authoritative snapshot, rebuilds
> the extraction plan from the reviewed intents (paths, modes, instances, states and Common
> members) and applies it without asking again.
> Every other operation array — `containmentResolutions`, `flatSiblingResolutions`,
> `selectedPrefabExtractions`, `crossParentPrefabExtractions` —
> **must be empty**: a non-empty one is refused before any write, and Unity never normalizes,
> repairs, force-extracts or converts the reviewed plan. `verify` accepts only `nodes`,
> `hierarchy`, `absentPaths`, `directChildren` and `tightBounds`; any other key is refused. The
> Prefab root (snapshot `isPrefabRoot` / `renameLocked`) must never appear in `renames`.
> Report containment or flat-sibling work in the review text only. Receipt statuses are `applied`,
> `rejected`, `partial`, `uncertain`. A `rejected` request wrote nothing, but any corrected plan is
> still a new request that must be completely reviewed and explicitly approved before its own
> `.apply` is written. `partial`/`uncertain` additionally require verification of the on-disk Prefab
> before any new plan.

## Efficiency rules that preserve accuracy

These rules reduce repeated work without weakening evidence requirements:

- **Single-agent execution is mandatory.** The primary agent owns evidence collection, the complete review/plan, validation and approval handling. Do not spawn, resume or delegate to subagents, teams or external advisor agents, even for read-only review. General workspace permission to delegate does not apply to this workflow. This covers every entry point — a full cleanup, an `INCREMENTAL ADJUSTMENT` session on an already-organized Prefab, a locked-selection local repair, the Unity chat window, and any terminal or external session — and it covers every stage of this skill, including the read-only review and the post-apply verification.
- Read the complete task, skill, plan format and authoritative snapshot once per unchanged version. Prevent output truncation with bounded non-overlapping chunks; recover only the missing section, not the whole document. Re-read an input only when it changes or a specific evidence gap requires it.
- After required hierarchy, image/render and ownership evidence is collected, write the complete draft JSON and review before preparing validation runners. Keep tables complete but concise; prioritize the executable plan over repeated explanations. Use the existing lint/simulation/publish helpers and documented Unity validation entry points. Read executor source only for a specific validation error or missing capability; do not build speculative alternative validation infrastructure.
- Follow the artifact sequence: evidence -> complete draft plan and review -> lint -> simulation -> required Unity preflight and visual checks -> publish exact validated bytes -> explicit approval -> apply/result. A failed validation stops dependent steps. Approval and visual/preservation gates remain mandatory.
- Every five minutes report elapsed time, actual written artifact paths, any specific blocker and the next concrete action. If no complete draft exists, finish it from sufficient evidence or report exactly which evidence is missing. Do not spend another interval on speculative tooling or polling for another agent's output. This checkpoint is not permission to skip checks or claim completion.

- The authoritative hierarchy snapshot is always read in full. Image files are risk-ranked, never skipped solely because of a name or size. A semantic name, narrow geometry, or screen-sized background may suppress an isolated image read only when snapshot data, components, sibling geometry, TMP alignment, and asset path agree. Any conflict, overlapping candidate, state branch, transparency/content-background question, or incomplete visual-unit closure escalates to image or render evidence. Record the skip reason and keep the final before/after visual audit mandatory.
- After `validate_plan_locally.py` and `simulate_and_verify_plan.py` both pass against the same snapshot, publish a draft by byte-for-byte file copy and verify its SHA-256. Never regenerate equivalent JSON or Markdown from parsed content after validation. Unity preflight remains authoritative.
- Run offline checks with short-circuit ordering: lint first, simulation second. The repository helper `scripts/publish_validated_plan.py` performs both checks in one Python process and copies only unchanged validated bytes.
- Read the complete skill and plan-format rules by default. Caching may avoid repeated disk reads, but a summary is allowed only after strict classification as a simple hierarchy-only task with no extraction, state, variant, asset-rename, nested-Prefab, binding, or unresolved-evidence branch. If classification is unavailable or changes, fall back to the complete files.

## AI Chat Single-Confirmation Contract

When this skill is supplied to the Unity AI hierarchy chat window, use exactly this interaction:

1. The Unity window first generates an authoritative node snapshot through Unity Editor APIs. The first confirmable AI reply presents the complete review in Markdown tables and one complete UTF-8 JSON plan in a `json` code block. The tables must cover grouping and naming, child Prefab extraction, preserved or ambiguous content, and verification. The child-Prefab table must disclose every output path, extraction mode, ordered instance, ordered Common member, state ID/name/member list, and default or per-instance state. The root object must use `"version": 2`, copy the snapshot `fingerprint` into `snapshotFingerprint`, reference every existing node as `node:<id>`, include every required operation array from `references/plan-format.md`, and record extraction work that becomes addressable only after grouping in `postGroupingExtractionIntents`.
2. The user may correct the tables before approval. The eventual explicit `确认`, `满意`, or equivalent apply intent is the only confirmation. It authorizes the complete reviewed workflow: first-stage hierarchy apply, authoritative resnapshot, exact manifest-matching child-Prefab extraction, save, and final verification.
3. After that confirmation, the Unity chat window rechecks the Prefab fingerprint, applies the first stage, refreshes the snapshot, rebuilds and validates the second stage from `postGroupingExtractionIntents`, and applies it without asking again. The second stage keeps the confirmed IDs, modes, output paths, ordered instance names and states, ordered Common members, state IDs, names and members, and default states; it may only add the `componentFamilyDecisions` entries the refreshed snapshot requires, and it may not include hierarchy, rename, containment, flat-sibling, or asset-rename operations.

The one confirmation is sufficient authorization for every stage that exactly matches the displayed tables and machine-readable manifest. Do not ask the user to choose an output mode, repeat confirmation, approve the refreshed snapshot, approve child Prefabs separately, or manually run a script. A revision requested before confirmation replaces the review and still leads to one eventual confirmation. After execution starts, evidence that conflicts with the confirmed manifest is a blocking failure: stop, report the exact mismatch and current saved state, and never guess, broaden scope, or obtain a second confirmation as a bypass. Before enabling confirmation, the chat window must reject a plan whose `version`, required fields, `snapshotFingerprint`, `prefabAssetPath`, `output.assetPath`, assets, node references, Prefab-root renames, or post-grouping intent shape are invalid, then run the Unity executor's in-memory preflight on the reviewed plan itself. That validation simulates wrapper creation, moves, renames, tight bounds, and ordered empty-container removals against an unsaved Prefab instance, then resnapshots that simulated tree and proves every `verify.hierarchy`, `verify.directChildren`, and `verify.tightBounds` entry. It must also inspect the simulated tree's complete `componentFamilyCandidates`, `containmentFindings`, and `flatSiblingFindings` before confirmation. An existing-node reference must be copied from the Unity-generated snapshot; it must never be inferred from displayed text, a Sprite name, a visual label, or a guessed hierarchy path.

`postGroupingExtractionIntents: []` is allowed only when the simulated post-grouping snapshot has no required component family and no repeated family that the reviewed scope requires or clearly implies extracting. If grouping creates a required family, the first confirmable response must disclose and freeze its complete extraction intent. Every mandatory (`requiresExtraction: true`) candidate gets exactly one `componentFamilyDecisions` entry: `parent` and `sources` exactly match the snapshot candidate; `recommendedMode` is advisory only; `mode` is `component`, `state`, `variant`, or `stateful` and matches the actual extraction list or `postGroupingExtractionIntents` entry named by `extractionId`; and that extraction's source roots completely cover the candidate sources. When the family only becomes extractable after grouping, Unity repeats the exact candidate-coverage check against the refreshed snapshot before executing it. If the simulated snapshot introduces an undisclosed candidate, unresolved cluster, changed extraction path, or changed member/state mapping, reject the plan before confirmation and report the exact difference without changing the real Prefab.

Asset GUIDs are Unity-owned data. Use an empty `expectedGuid` for Texture and SpriteAtlas renames so Unity captures the current `AssetDatabase` GUID, or paste an exact GUID to pin it. Unity rejects a missing asset with the exact array index and path, and enforces the captured GUID before and after the rename so an identity change between validation and apply remains blocking.

`prefabName` is the private-asset prefix Unity enforces verbatim: every `textureRenames[].toName` must start with `<prefabName>_` and every `spriteAtlasRenames[].toName` must equal `prefabName`. It is independent of the Prefab asset file name and of the root name. Choose the English PascalCase name the assets should carry (ending with `View`, for example `MainScreenView`) and make every `toName` agree with it. A prefix-mismatch rejection is fixed by making `prefabName` and every `toName` consistent; it never means the prefix is hard-coded to the Prefab file name.

Every AI-specific prompt must supply the canonical plan format and authoritative node snapshot before the first response. The snapshot is authoritative for hierarchy, node identity, geometry, components, active state, and references. Read only a targeted Prefab section when the snapshot explicitly lacks evidence or conflicts with serialized data; do not repeatedly read the complete Prefab and snapshot. Do not infer JSON names, node IDs, or paths from prose or use a legacy schema. The complete workflow has a small, bounded automatic JSON-repair budget, and each repair message lists every problem Unity found. Keep that repair in the same AI session and request one complete replacement JSON code block with no prose. The automatic second stage has no additional repair budget: any invalid or manifest-mismatching result stops immediately. The repair may use only IDs present in the original snapshot; it must remove an operation whose intended source cannot be proven instead of inventing another ID. If the replacement still fails, leave the Prefab unchanged, disable confirmation, and report the final validation failure.

## Workflow Tools

Execute everything through Unity Editor APIs. Do not hand-edit serialized Unity files or write one-off cleanup payloads.

When the task depends on the user's current Unity Editor GameObject selection, use only NativeUnity: run `scripts/read_unity_selection.py --mode selection`, which invokes `unity command eval_file` and reads `UnityEditor.Selection` in the target Editor. Python is only the command wrapper and result parser. Do not read the live selection through uLoop, any other third-party MCP/plugin, cached files, screenshots, or an inferred Prefab path. If NativeUnity fails, report that the current selection is unverified; never switch backends automatically.

1. Read the authoritative JSON snapshot that Unity provides for the session (the chat window supplies it; a terminal session names its absolute path in the task file). It carries every node as `node:<id>` with path, name, components, geometry, Sprite/Texture paths, TMP state, nested Prefab boundaries, `componentFamilyCandidates`, `containmentFindings` and `flatSiblingFindings`. Its root node is marked `isPrefabRoot: true`, `renameLocked: true` and `requiredName`. Copy `fingerprint` into `snapshotFingerprint`.
2. Review every `componentFamilyCandidates` entry; `scripts/find_prefab_component_candidates.py` is an optional extra discovery aid. Record every candidate in `componentFamilyDecisions`; a candidate marked `requiresExtraction: true` must be extracted and cannot be skipped. A candidate marked `requiresExtraction: false` is advisory and may be skipped with concrete recursive-structure evidence. A family whose members are not all structurally identical also reports `numbered_structure_subset` candidates: the members that do share one recursive structure. Prefer the family-level extraction when it is marked required, and use a subset when the family only fits a variant or when a narrower boundary is cleaner. A single-member subset is a report that the member has no peer, not an instruction to extract it alone.
3. Author a version 2 `node:<id>` plan from [references/plan-format.md](references/plan-format.md). Optionally lint it offline with `scripts/validate_plan_locally.py` and `scripts/simulate_and_verify_plan.py` against the same JSON snapshot.
4. Execute only through the Unity core: the human approves, the AI writes `<session>.apply`, and Unity validates, applies and reports `<session>.apply-result.json`.
5. Never write `.apply` before explicit human approval, and never write it twice for the same request.
   After approval, create the JSON sentinel with `scripts/create_apply_approval.py --plan <exact plan> --apply <exact apply>`;
   do not hand-compose `planPath`. The helper resolves the existing plan to a platform-native absolute path
   (Windows drive path with `\\`; macOS/Linux path with `/`), derives the current SHA-256, snapshot fingerprint,
   target Prefab and review version from that exact plan, and refuses an existing `.apply` or result file.
6. If the receipt reports `rejected`, report the complete error and stop. A correction must be emitted as a complete new review and plan under a new request, then receive explicit human approval before its own `.apply` is written.
7. If the receipt reports `partial` or `uncertain`, do not apply again: verify the saved state and require a new review.
8. After every successful apply the Prefab file, and therefore the snapshot fingerprint, has changed. Read the current snapshot again before authoring any further plan; never reuse an old `snapshotFingerprint` or old node IDs.

## Operating Contract

- Work only on the named `.prefab` asset. Do not require, query, or modify Figma.
- Always organize the named target Prefab in place. Its plan must use `output.mode: in_place` and the same `output.assetPath` as `prefabAssetPath`; never create, offer, or ask the user to choose a `.cleaned.prefab`, duplicate, or replacement target Prefab. Preserve the root object's asset-compatible name: never rename the root to a name different from `Path.GetFileNameWithoutExtension(prefabAssetPath)` or put a different root name into post-apply verification paths.
- Start read-only. Inspect the complete hierarchy, components, RectTransforms, active state, asset references, and nested Prefab instances before proposing changes.
- Infer semantic groups from geometry, component/type patterns, visible hierarchy, repeated structures, and existing names. Names are a hint, never sole membership evidence.
- Produce one complete final-tree proposal before applying. Include nested repeated units such as `[Item_*]`, cards, progress segments, map markers, tabs, or rows; do not stop at coarse region wrappers.
- Assign every moved source node exactly once. Rename every exported object to a concise English semantic name; retain brackets only for structural groups such as `[ContentCard_1]`. A text value (`20`, `+`, `150k`, `login 1 day`), PSD/export token (for example a configured `ui_*` or `img_v*` prefix), UUID-like export name, punctuation-only name, or duplicate ambiguous sibling name is not semantic. Name the role, such as `ActivityAmount`, `PlusLabel`, `MissionIcon`, or `MissionDescription`. Before confirmation the Unity chat window's naming gate computes every node's final name and lists every one that is still not semantic, so rename all of them in the same plan. Never delete, duplicate, merge, replace components, or change bindings merely to make the tree look cleaner. The sole structural exception is `emptyContainerRemovals`: list a pre-existing container only when every one of its current direct children is moved out or is itself removed earlier in the same removal list. Nested removals must use child-before-parent order. The resulting container must be empty, have no components beyond its `Transform`, and have no external serialized references.
- Every plan-owned ID, including `wrappers[].id`, extraction `id`, and state `id`, must be lower snake_case matching `^[a-z][a-z0-9_]*$`. Use IDs such as `screen_root`, `day_markers`, or `task_in_progress`; never use PascalCase, kebab-case, spaces, brackets, or an `@` prefix. The `@` prefix is reserved only for a later reference to an earlier wrapper, such as `@screen_root`.
- Treat ambiguous membership, a likely serialized binding, nested Prefab boundary, or unfamiliar custom component as a blocker. State the ambiguity and leave that portion unchanged rather than guessing.
- Require explicit user confirmation of the reviewed plan before writing. A request to inspect or organize does not authorize an immediate asset mutation.
- For a `PrefabName_SemanticName` rename, infer an English PascalCase `PrefabName` from the UI's actual function, require it to end in `View`, and ask the user to confirm it before mutation.
- Rename only Texture and SpriteAtlas assets that are proven private to the named Prefab. When a private Texture directory is in scope, list and rename every Texture in it to `PrefabName_SemanticName`; do not leave a partial prefix migration. Use `AssetDatabase.RenameAsset`; never use file-system moves or create replacement `.meta` files.
- When the user explicitly asks for reusable Prefabs with visual states, include every approved family in the same explicitly reviewed in-place plan. `output.mode: in_place` prohibits a replacement screen Prefab, not a shared child asset under the target Prefab's sibling `Common` directory. Require either at least two structurally matching units, or at least three numbered semantic units under one parent with matching anchors and pivot; no source may cross a nested Prefab boundary or retain an external serialized reference.
- Treat the candidate scanner as a discovery aid, not a safety proof. It reports both strictly matching structures and high-confidence numbered families; the latter may vary in `sizeDelta` or state structure, which must be represented as reviewed instance overrides or a stateful mapping. Every result still passes the Unity-side nested-boundary, external-reference, and structural checks during apply.
- Treat visually overlapping alternatives as states, not repeated instances. A `stateComponentExtractions` plan replaces all approved sibling state sources with one nested component containing a `[States]` container. It requires an explicit semantic state mapping and a single default state; never infer those names from layer order alone.
- Treat simultaneously visible list rows that share one logical component but display at least two distinct observed visual states as a `variantComponentExtractions` family. Create exactly one shared Prefab under `Prefab/Common/`; its root must contain direct `[Common]` and `[States]` children. Use one observed representative row in `states[].source` for each unique state, then list every approved row exactly once in `instances`; multiple instances may select the same state. If all visible rows have one observed state, use `componentExtractions` instead and do not invent a state branch. Replace every approved source row with an instance of that same Prefab, preserve the row's list position and instance name, and activate exactly the mapped state in each instance. `[Common]` may be empty only when no element can be proven common to every state without changing the rendering.
- Treat a repeated unit with both stable members and a finite set of visual variants as a `statefulComponentExtractions` family. Create exactly one shared Prefab under `Prefab/Common/`, with direct `[States]` followed by `[Common]` children so shared labels still render above state backgrounds. Every source unit must map every direct member exactly once into `[Common]` or its selected state; `Common` must contain every member proven common. A named all-common state may have an empty `members` list only when its instances map every direct child through `commonSourceNames` and use an empty `stateSourceNames` list; never use an empty state to hide an unmapped child. Every instance must list both complete mappings explicitly; Unity never rebuilds or completes a missing Common or selected-state list. Reuse one semantic state branch across all matching instances and apply only the approved instance overrides, such as a counter value. Do not create a distinct state solely because an instance label differs.
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

- Use `PrefabUtility`, `GameObject`, `Transform`, `AssetDatabase`, and the Unity shared cleanup core.
- Each stage gets one Apply attempt. A timeout or indeterminate result permits exactly one read-only verification of the saved state, then stops.
- Never automatically rerun the same plan, restore a backup, reimport the PSD, generate a third plan, or continue to another AI stage after an Apply failure or contract warning.
- A failed Apply may have partially saved the Prefab. Report the observed saved state; never assume either the original or intended state.

### 4. Apply Through Unity

After the single confirmation, Unity applies the plan through its shared cleanup core. If the confirmed hierarchy stage creates the repeated-unit roots needed for extraction, Unity itself refreshes the snapshot, rebuilds the second stage from `postGroupingExtractionIntents` (adding the `componentFamilyDecisions` the refreshed snapshot requires), preflights it, and applies it through the same shared core without asking the user. The generated Unity operations use Editor APIs only:

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

Never edit `.prefab` YAML text, reconstruct the Prefab, reimport PSD assets, regenerate textures, manually move files, or create replacement `.meta` files as part of hierarchy cleanup.

### 5. Verify the Saved Asset

Reopen the saved Prefab and compare it with the before snapshot. Report evidence for:

- complete final tree, exact wrapper membership, and preserved affected-node sibling order;
- unique assignment of all moved nodes;
- existing RectTransform world corners preserved within `0.01` where reparenting changes hierarchy, and every structural container's bounds exactly match its direct children;
- all object names are English semantic names; no PSD/export, Chinese, or punctuation-heavy source names remain;
- preserved active states, component counts/types, object references, Sprite/TMP/font/material assignments, and nested Prefab boundaries;
- missing Sprite references on the target Prefab itself are target-owned verification issues and must be surfaced, never silently classified as inherited nested-Prefab issues. A missing Sprite inside an unchanged nested Prefab instance is reported separately by path; fix that source asset through its own owner rather than crossing the nested boundary;
- after a save, a `partial` result means a verification contract failed: surface it and stop before another stage. A hierarchy, membership, path, state, reference, layout, or manifest mismatch is blocking and is never completion proof. The pre-confirmation simulation must treat the same mismatch as a rejection, not a warning that may reach user confirmation;
- each separately extracted unit is a nested Prefab instance sourced from the approved shared component asset, with every affected RectTransform world corner preserved within `0.01`;
- each separately extracted state component is a nested Prefab instance with one direct `[States]` container, all approved state branch names in order, exactly one active default branch, and every state branch's world corners preserved within `0.01`;
- each separately extracted variant component is a nested instance of the one reviewed `Prefab/Common` asset, has direct `[Common]` and `[States]` containers, contains every approved state branch in order, and has exactly its mapped branch active;
- each separately extracted stateful component is a nested instance of the one reviewed `Prefab/Common` asset, has non-empty `[Common]` and `[States]` containers, contains each approved state branch once, maps every source direct member exactly once, and has exactly its mapped branch active;
- all private Texture files in scope use the exact `<prefabName>_` filename prefix, and no residual PSD/export or text-value node name remains in scope;
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
- For a direct iterative maintenance session with explicit end-to-end apply authorization, record each revised in-scope plan before running it, then preflight, apply once, and verify. The AI chat bridge's frozen-manifest confirmation contract remains unchanged. A read-only preflight may be corrected and retested before approval; once an approved request is submitted, any corrected plan requires a complete new review and explicit confirmation. An uncertain Apply must be verified before any new mutation.
- Establish a reproducible offscreen render of the exact target before editing. Compare after hierarchy changes and after extraction, including overlapping layers and inactive state branches. World-corner preservation alone does not prove draw-order preservation. A visual mismatch prevents completion and requires an evidence-backed correction, not a relaxed tolerance.
- Audit image ownership using current AssetDatabase references and retain GUID identity with RenameAsset. A directory can mix private and shared images; rename all proven-private images, but list externally owned/shared exceptions instead of treating the entire directory as private. Do not silently rename a global catalog's assets to a screen-specific prefix.
- When requested, perform hierarchy and private image naming first; extract common Prefabs last. Preserve the approved boundaries and exact per-instance state/member mapping across stages.

## Invocation Examples

- `Use $prefab-hierarchy-cleanup to inspect Assets/UI/RewardPanel.prefab and propose a complete hierarchy. Do not modify it yet.`
- `Use $prefab-hierarchy-cleanup on Assets/PSDLayoutTool2/TestData/Example.prefab. Propose the complete in-place hierarchy cleanup before applying it.`
- `Use $prefab-hierarchy-cleanup to organize this UI in place and rename its private textures to RewardPanelView_SemanticName. Infer the View name and ask me to confirm the rename plan first.`

## Project-verified engine realities (read this before authoring a plan)

Measured in this project with the Unity shared cleanup core. They have already produced real
damage, so they are rules, not tips.

- **R1 — New containers are not tightened automatically.** A wrapper created by a plan keeps
  `sizeDelta = 0` unless the same plan lists it in `tightBounds`. Always emit `tightBounds` for
  **every** new wrapper (inner -> outer). A later stage that tightens a *parent* computes the union
  of its children, so a parent tightened while its children are still 0 x 0 also collapses to 0 x 0.
  `validate_plan_locally.py` fails this rule as a warning before the Unity preflight.
- **R2 — Extraction stages expand the tree.** `componentExtractions`, `stateComponentExtractions`,
  `variantComponentExtractions` and `statefulComponentExtractions` replace source units with nested
  instances, so `verify.nodes` must be the **post-expansion** expectation. Passing the pre-apply
  snapshot count fails verification (`partial`) *after* the asset is already saved. Prefer omitting those counts in an extraction stage, or take a fresh
  snapshot first.
- **R3 — The Prefab root is rename-locked.** The snapshot root carries `isPrefabRoot`,
  `renameLocked` and `requiredName` (the asset file name). Never put it in `renames`, even when the
  name is Chinese; Unity rejects the plan. `prefabName` is not the root name. A different root name
  requires renaming the Prefab asset file, which is outside this skill.
- **R4 — The snapshot expires on every successful apply.** `fingerprint` is the Prefab file hash.
  Read the current snapshot again before the next plan; an old `snapshotFingerprint` or old node IDs
  are rejected.
- **R5 — `eval_file` payloads are top-level statements only.** No `using` directives (they fail to
  compile with "Identifier expected"); `UnityEngine`, `UnityEditor`, `System` and
  `System.Collections.Generic` are implicitly available; qualify `UnityEngine.UI.*` explicitly, and
  `return` a string to get it back in the CLI result.
- **R6 — Terminal session files.** External sessions write
  `Library/PsdHierarchyTerminal/<session>.plan.json` (version 2, `node:<id>` references) and the
  review file; Unity applies only after a human-approved `<session>.apply` sentinel and reports
  `<session>.apply-result.json`. Never run any script to apply a plan yourself.
- **R7 — What the plan language cannot express** (verified: no `UnpackPrefab` anywhere in the project
  tooling, and the executor deletes assets only in rollback/replay paths): moving nodes across a
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
  Test fixtures are the
  only plan files that belong to the skill, and they live in `scripts/tests/fixtures/`. The former
  `plans/` directory at the skill root was removed on 2026-09-16 precisely because runtime output
  kept landing there untracked.

### Read-only toolbox added by this project

| script | purpose |
|---|---|
| `scripts/parse_hierarchy_snapshot.py` | normalise the Unity JSON snapshot, print metrics + tree |
| `scripts/validate_plan_locally.py` | offline linter for version 2 `node:<id>` plans: refs, root rename, uniqueness, removals, naming gate, directChildren, plus R1/R2 warnings |
| `scripts/simulate_and_verify_plan.py` | rebuild the final tree offline and prove `verify.hierarchy/directChildren/absentPaths` (caught a real double-wrapper defect) |
| `scripts/check_extraction_result.py` | prove a finished extraction: instance names/order, `[Common]`/`[States]`, exactly one active mapped state, and (stateful) the declared Common + state member names - covers `component`, `state`, `variant` and `stateful`; run as `--plan <plan> --before-snapshot <snapshot the plan was written against> --snapshot <fresh after-apply snapshot>` |
| `scripts/audit_prefab_preservation.py` | **before/after preservation audit**: prove that every pre-existing node survived with the same components, sprite, TMP text and reparent-invariant RectTransform values, and that each extracted unit's `[Common]` + active branch reproduces the unit it replaced (`--mode capture` needs Unity, `--mode compare` is pure offline) |
| `scripts/read_unity_selection.py` | read the user's live Editor selection / Prefab Stage / nested-instance links (read-only, `--mode selection|instance-links|prefab-stage`) |
| `scripts/find_prefab_component_candidates.py` | optional extra discovery of repeated component families from the JSON snapshot (advisory only) |
| `scripts/prefab_visual_audit.py` | render-capture payloads plus a strict RGBA comparison for before/after visual proof |
| `scripts/payloads/read_selection.cs` | the eval_file payload behind it (template for R5) |
| `scripts/payloads/audit_prefab_dump.cs` | the read-only tree+fingerprint dump behind the preservation audit (marker `DUMP_BEGIN`/`DUMP_END`) |

All read-only diagnostics accept only the Unity JSON snapshot and version 2 `node:<id>` plans; none of
them writes to the Prefab.

Typical session order: read the current JSON snapshot -> semantics -> plan -> `validate_plan_locally.py` ->
`simulate_and_verify_plan.py` -> `audit_prefab_preservation.py --mode capture` (BEFORE, per stage) ->
user approval -> one `.apply` (Unity preflights and applies) ->
read-only verification (`check_extraction_result.py`, `audit_prefab_preservation.py --mode compare`,
`read_unity_selection.py --mode instance-links`) -> record evidence in the review file -> read a fresh
snapshot before any further plan.
