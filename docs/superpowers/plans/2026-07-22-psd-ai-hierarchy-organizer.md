# PSD AI Hierarchy Organizer Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add preview-first AI hierarchy planning that resolves the exact generated Prefab, persists stable layer-ID organization, and safely reapplies it across incremental PSD imports.

**Architecture:** Unity exports a strict hierarchy request and invokes Codex CLI only as a read-only planner. A validated `PsdHierarchyProfile` drives deterministic C# grouping between `ExportTree` and Prefab save; source content changes reuse the Profile, structural changes reconcile stable IDs, and target replacement preserves project-owned nodes transactionally.

**Tech Stack:** Unity 6 editor C#, Newtonsoft.Json, NUnit EditMode tests, Codex CLI `exec --sandbox read-only --output-schema`, uloop compile/test verification

---

**Exact targeted test command:**

`uloop run-tests --test-mode EditMode --filter-type regex --filter-value "PsdLayoutTool2.Tests.Psd(Hierarchy|PrefabIncremental).*Tests" --save-before-run false --project-path E:\Project\Demo\monsterhunter`

Expected: `Success=true`, every selected test passes, and no unrelated scene is saved. If Unity Test Runner refuses because an unrelated scene is dirty, preserve that scene, report the verification blocker, and use only non-mutating compile/static checks until the test runner can execute safely.

## Chunk 1: Stable Identity, Exact Paths, and Incremental Profile

### Task 1: Resolve the configured Prefab without same-name fallback

**Files:**
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdGeneratedPrefabPathResolver.cs`
- Create: corresponding `.meta`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdImporter.cs`
- Test: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyPathResolverTests.cs`

- [x] Write failing tests for sibling and inside-folder modes, including both existing same-name candidates and a missing configured target.
- [x] Run the exact targeted test command above and verify the resolver type/method is missing.
- [x] Implement a pure resolver taking PSD asset path, output settings, and Prefab mode; expose `PsdImporter.TryResolveGeneratedPrefabPath` without relying on mutable `PsdName` state.
- [x] Verify both tests pass and the inside-folder result is exactly `Assets/PSDLayoutTool2/TestData/7日任务拆分/7日任务拆分.prefab`.

### Task 2: Define shared stable IDs, fingerprints, and Profile reconciliation

**Files:**
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdStableLayerIdUtility.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyFingerprints.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyProfile.cs`
- Create: corresponding `.meta` files
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/PsdPrefabModelBuilder.cs`
- Test: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyProfileTests.cs`

- [x] Write failing tests proving layer rename reuse, content-only reuse, geometry-only validation state, new IDs in `Unsorted_New`, missing IDs retained, and stable generated group keys.
- [x] Add reconciliation-state tests proving content-only changes require no replan, geometry-invalidated/new-ID changes identify only focused invalidated scopes, unaffected plan bytes remain unchanged, and missing-ID cleanup remains pending until explicitly confirmed.
- [x] Add tests proving `layer.Id == 0` nodes are marked `FallbackUnstable`, cannot enter persisted groups/renames, and remain `Unsorted_Unstable` after rename/reorder.
- [x] Run the exact targeted test command and verify failure is caused by missing Profile/fingerprint APIs.
- [x] Implement content/structure/geometry fingerprints and Profile reconciliation keyed only by stable layer ID.
- [x] Replace the model builder's private fallback ID implementation with the shared utility.
- [x] Verify repeated reconciliation is idempotent and does not create duplicate groups.

## Chunk 2: Strict AI Planning and Preview

### Task 3: Export a bounded hierarchy request and parse a strict plan

**Files:**
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyContracts.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyContextBuilder.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyPlanJson.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyPlanValidator.cs`
- Create: corresponding `.meta` files
- Test: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyPlanTests.cs`

- [x] Write failing tests for valid plans and rejection of unknown fields, duplicate keys, unsupported schema, unknown/duplicate member IDs, cycles, multiple parents, protected-boundary crossings, non-contiguous sibling moves, commands, code, and deletion fields.
- [x] Run the exact targeted test command and verify the strict parser/validator APIs are missing.
- [x] Implement strict Newtonsoft parsing with explicit allowed-property sets and non-finite number rejection.
- [x] Implement the context builder from the normalized PSD tree and current Prefab metadata without texture bytes.
- [x] Verify all trust-boundary tests pass.

### Task 4: Invoke Codex read-only and display a non-mutating preview

**Files:**
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/IPsdHierarchyAiRunner.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/CodexCliHierarchyRunner.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyOrganizerWindow.cs`
- Create: corresponding `.meta` files
- Test: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyAiRunnerTests.cs`

- [x] Write failing tests for argument construction, read-only sandbox, output schema, timeout, cancellation, non-zero exit, malformed output, and offline request-package fallback using an injected process adapter.
- [x] Add focused-replanning orchestration tests using a fake runner: zero calls for content-only updates, one bounded call per invalidated/new-ID scope, no calls for unaffected groups, complete-plan validation after partial merge, and confirmed missing-ID cleanup in the preview model.
- [x] Run the exact targeted test command and verify missing runner APIs.
- [x] Implement asynchronous Codex CLI execution with no Unity write permission and a request package under `Temp/PSDLayoutTool2/Hierarchy/<operationId>`.
- [x] Implement current/proposed tree preview, exact target path display, validation errors, retry, manual plan import, cancel, and a disabled Apply action until validation succeeds.
- [ ] Verify closing the window cancels the runner and never modifies the Prefab/Profile. The async test compiles, but native TestRunner execution remains blocked by project-level prebuild/package behavior.

## Chunk 3: Deterministic Apply and Incremental Preservation

### Task 5: Apply groups while preserving geometry, render order, and references

**Files:**
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyApplier.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyApplyVerifier.cs`
- Create: corresponding `.meta` files
- Modify: `Assets/PSDLayoutTool2/Editor/PsdImporter.cs`
- Test: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyApplierTests.cs`

- [x] Write failing tests for stable group reuse, nested groups, rename application, world-corner preservation, anchorMin/anchorMax, pivot, anchoredPosition, sizeDelta, rotation, localScale, component order, serialized references, original visual sibling order, and refusal to cross Canvas/Mask/Button/Animator/nested-Prefab boundaries.
- [x] Run the targeted tests and verify missing applier behavior.
- [x] Add an import-session stable-ID to `RectTransform` registry populated from normalized layer metadata during UI generation.
- [x] Apply the Profile after `ExportTree(tree)` and before Prefab save, using captured world corners and deterministic group keys.
- [x] Verify Image/TMP/material/nine-slice/active/reference snapshots remain equal.

### Task 6: Preserve project-owned subtrees and replace target transactionally

**Files:**
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdPrefabIncrementalMerge.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdPrefabTransactionalSave.cs`
- Create: corresponding `.meta` files
- Modify: `Assets/PSDLayoutTool2/Editor/PsdImporter.cs`
- Test: `Assets/PSDLayoutTool2/Editor/Tests/PsdPrefabIncrementalMergeTests.cs`

- [x] Write failing tests for generated local-ID/path tracking, in-place retention of generated local file IDs, project-owned child preservation beneath generated parents, missing-parent blocking, serialized-reference preservation, external references and Prefab-instance overrides, unchanged target/Profile on every injected failure point, preserved target GUID, and non-target same-name Prefab immutability.
- [x] Add injected failures after in-place Prefab save, during reimport verification, during Profile copy/save, and during final verification; assert both Prefab/Profile backups restore and every temporary asset plus `.meta` is removed.
- [x] Run the targeted tests and verify the merge/transaction APIs are missing.
- [x] Implement first-adoption ambiguity blocking and subsequent generated-vs-project-owned classification from the previous Profile.
- [x] Build a temporary candidate as the source of PSD-owned values, then update matched objects in loaded existing Prefab contents in place so retained local file IDs, business children, external references, and overrides survive.
- [x] Save Prefab and cloned next Profile in two phases with backup/restore around every failure point.
- [x] Verify rollback restores previous Prefab bytes and leaves Profile untouched.

## Chunk 4: Inspector Integration and Acceptance

### Task 7: Add the PSD Inspector entry point

**Files:**
- Modify: `Assets/PSDLayoutTool2/Editor/PsdInspector.cs`
- Test: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyOrganizerEntryTests.cs`

- [x] Write failing tests for configured target resolution, missing-target messaging, and organizer input creation without Prefab mutation.
- [x] Add a test proving the action is disabled with an explanation when `UseUnityUI` is false.
- [x] Run the targeted tests and verify the entry API is missing.
- [x] Add `AI 整理层级（预览）` below the primary Generate Prefab action and open `PsdHierarchyOrganizerWindow` with the selected PSD and exact target Prefab; disable it in non-Unity-UI mode.
- [x] Keep Apply as a separate explicit action inside the preview window.

### Task 8: Run end-to-end incremental acceptance

**Files:**
- Test fixture: `Assets/PSDLayoutTool2/TestData/7日任务拆分.psd`
- Target only: `Assets/PSDLayoutTool2/TestData/7日任务拆分/7日任务拆分.prefab`

- [x] Run all synchronous PSD EditMode tests in an isolated Unity project: 223/223 passed. The live editor's queue was intentionally not used because it has an unsaved user scene and is unresponsive.
- [x] Compile in the isolated Unity project and require zero C# errors.
- [ ] Generate/import a validated fixture plan, apply twice, and prove the second apply creates no duplicate groups.
- [ ] Compare before/after visual leaf geometry and references; prove the sibling Prefab `Assets/PSDLayoutTool2/TestData/7日任务拆分.prefab` is byte-identical.
- [x] Confirm new IDs are reported without disturbing existing group membership and missing IDs remain pending rather than silently deleting nodes.
- [x] Run `git diff --check`, inspect Chinese encoding, and report any unrelated pre-existing working-tree state separately.
