# Hierarchy Preview Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Present current Prefab and proposed AI hierarchy in a Unity-style two-pane EditorWindow.

**Architecture:** Keep all hierarchy data in `PsdHierarchyOrganizerPreviewModel`; add pure presentation-tree helpers so IMGUI rendering remains small and testable. Preserve all import and preview commands.

**Tech Stack:** Unity Editor IMGUI, C#, NUnit EditMode tests.

---

### Task 1: Presentation-tree tests

**Files:**
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyOrganizerEntryTests.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyOrganizerWindow.cs`

- [ ] Add a failing test that feeds parented current-tree nodes and asserts a deterministic root/child presentation tree.
- [ ] Add a failing test that feeds one proposed group and asserts the group title, member IDs, confidence, and evidence are exposed as display data.
- [ ] Run the focused EditMode tests and observe failure before the helper exists.
- [ ] Add the minimal immutable presentation-node helpers to the window model.
- [ ] Re-run the focused tests and verify pass.

### Task 2: Two-pane Unity-style rendering

**Files:**
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyOrganizerWindow.cs`

- [ ] Add splitter, scroll, foldout, and selected-group state to the window.
- [ ] Render Current Prefab in the left pane using indentation, foldout arrows, and compact object rows.
- [ ] Render Proposed Structure in the right pane using tinted group rows and member leaves.
- [ ] Render selected group confidence/evidence in a right-pane inspector strip.
- [ ] Keep existing command buttons and status messages unchanged.

### Task 3: Verification

**Files:**
- Test: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyOrganizerEntryTests.cs`

- [ ] Run focused hierarchy preview tests.
- [ ] Run `uloop compile` from `E:\Project\Demo\monsterhunter` and require zero errors.
- [ ] Open the PSD Hierarchy Preview window in Unity and verify resizing, scrolling, selection, and unchanged buttons.
