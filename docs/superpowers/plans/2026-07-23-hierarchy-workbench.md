# PSD Hierarchy Workbench Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the technical hierarchy preview with a reviewable three-pane workbench with explicit Prefab Stage Ping, accepted groups, and scoped AI refinement.

**Architecture:** Keep planning and acceptance state in `PsdHierarchyOrganizerPreviewModel`. Extract exact Prefab Stage selection into an editor-only helper and keep IMGUI animation presentation-only; only the existing Apply action may mutate assets.

**Tech Stack:** Unity Editor IMGUI, `UnityEditor.AnimatedValues.AnimBool`, `PrefabStageUtility`, NUnit EditMode tests, existing hierarchy AI runner.

---

### Task 1: Resolve Ping inside the active Prefab Stage

**Files:**
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyPrefabStageSelection.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyPrefabStageSelectionTests.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyOrganizerWindow.cs`

- [ ] **Step 1: Write the failing pure mapping test.**

```csharp
[Test]
public void ResolveStageTargets_MapsPersistentLocalIdsToStageObjects()
{
    CollectionAssert.AreEqual(new[] { "Reward", "DayOne" },
        PsdHierarchyPrefabStageSelection.ResolveStageTargets(
            new[] { 1L, 10L, 20L }, new[] { "Root", "DayOne", "Reward" }, new[] { 20L, 10L }));
}
```

- [ ] **Step 2: Run `uloop run-tests --project-path E:\\Project\\Demo\\monsterhunter --test-filter PsdHierarchyPrefabStageSelectionTests`; confirm it fails because the helper is absent.**
- [ ] **Step 3: Implement `ResolveStageTargets<T>`: reject unequal persistent/stage lengths, iterate in traversal order, and return only values whose parallel persistent local ID is requested.**
- [ ] **Step 4: Change `SelectPrefabMembers` to open the asset, wait through `EditorApplication.delayCall`, get `PrefabStageUtility.GetCurrentPrefabStage()`, build parallel persistent/stage transform arrays, and select stage objects returned by the helper. Do not select persistent Prefab children.**
- [ ] **Step 5: Run the focused test, then verify `Selection.activeGameObject.transform.IsChildOf(stage.prefabContentsRoot.transform)` after a Ping on fixture ID `594`.**
- [ ] **Step 6: Commit only Task 1 files with `git commit -m "Resolve hierarchy pings in Prefab Stage"`.**

### Task 2: Add accepted groups and scoped refinement

**Files:**
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyOrganizerWindow.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyAiRunnerTests.cs`

- [ ] **Step 1: Add a failing test.**

```csharp
[Test]
public async Task RefineGroupAsync_ExcludesAcceptedGroupMembers()
{
    model.AcceptGroup("day_1_card");
    await model.RefineGroupAsync("future_day_card", CancellationToken.None);
    CollectionAssert.DoesNotContain(fake.requests.Single().modifiableStableIds, "594");
}
```

- [ ] **Step 2: Run `uloop run-tests --project-path E:\\Project\\Demo\\monsterhunter --test-filter PsdHierarchyAiRunnerTests`; confirm missing `AcceptGroup` and `RefineGroupAsync` APIs.**
- [ ] **Step 3: Add transient `HashSet<string> acceptedGroupKeys`, `AcceptGroup`, and `RefineGroupAsync`. Build the refinement scope from the selected unlocked group, subtract all accepted-group members, and preserve accepted groups in `baselineGroups`; merge only the validated selected-group diff.**
- [ ] **Step 4: Reset transient acceptance state in `ReplaceContext` and `ClearContext`; do not serialize it to Profile assets.**
- [ ] **Step 5: Run focused AI runner tests and commit only Task 2 files with `git commit -m "Scope hierarchy refinement to accepted review state"`.**

### Task 3: Replace the list with a three-pane review workbench

**Files:**
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyOrganizerWindow.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyOrganizerEntryTests.cs`

- [ ] **Step 1: Add a failing context-reset test that selects and accepts a group, calls `ReplaceContext`, and asserts selected/accepted transient state is empty.**
- [ ] **Step 2: Run `uloop run-tests --project-path E:\\Project\\Demo\\monsterhunter --test-filter PsdHierarchyOrganizerEntryTests`; confirm the test-only state accessors are missing.**
- [ ] **Step 3: Implement panes: left current hierarchy/search, center compact group cards (name, count, confidence, accepted marker), right selected-group detail (members, evidence, `Ping`, `Ping All`, `Accept`, `Refine with AI`). Keep labels non-clickable and every action explicitly labelled.**
- [ ] **Step 4: Create one `AnimBool` per card (`speed = 6f`), repaint on `valueChanged`, use `BeginFadeGroup` for expansion, and clear all animation state after context replacement/domain reload. Do not animate asset writes or block input.**
- [ ] **Step 5: Run focused entry tests; capture fixture states for collapsed, expanded, and accepted card; commit only Task 3 files with `git commit -m "Present hierarchy planning as a review workbench"`.**

### Task 4: Verify the incremental review workflow

**Files:**
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyAiRunnerTests.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyOrganizerEntryTests.cs`

- [ ] **Step 1: Add an end-to-end test that accepts `day_1_card`, refines `future_day_card`, and asserts `day_1_card` still has its original members.**
- [ ] **Step 2: Run `uloop run-tests --project-path E:\\Project\\Demo\\monsterhunter --test-filter PsdHierarchy`. If an unsaved user scene blocks the runner, do not save/discard it; record the block and run compile plus the dynamic Prefab Stage assertion.**
- [ ] **Step 3: Run `uloop compile --project-path E:\\Project\\Demo\\monsterhunter`, open `7日任务拆分`, Ping one member, accept one group, refine another, discard its diff, and verify Prefab/Profile timestamps remain unchanged before Apply.**
- [ ] **Step 4: Commit verification tests with `git commit -m "Verify scoped hierarchy review workflow"`.**
