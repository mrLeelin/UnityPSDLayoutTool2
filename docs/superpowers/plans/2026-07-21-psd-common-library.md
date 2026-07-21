# PSD Common Library Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add deterministic Common_Prefab and Common_Texture resolution to PSD-to-Prefab import.

**Architecture:** Parse common-layer names in a pure module, resolve exact keys from configured folder roots through a GUID cache, and branch before normal PSD PNG generation. The resolver owns lookup and diagnostics; the importer owns Unity object creation and PSD layout.

**Tech Stack:** Unity Editor C#, AssetDatabase, SettingsProvider, PrefabUtility, NUnit edit-mode tests.

---

### Task 1: Common-name parsing

**Files:**
- Create: `Assets/PSDLayoutTool2/Editor/CommonLibrary/PsdCommonAssetModels.cs`
- Create: `Assets/PSDLayoutTool2/Editor/CommonLibrary/PsdCommonAssetNameParser.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdCommonAssetTests.cs`

- [ ] Write failing tests for exact `Common_Prefab_Button_Green`, exact `Common_Texture_Lock`, and rejection of ordinary `Button_Green` names.
- [ ] Implement a case-insensitive parser returning kind plus exact key; reject empty keys.
- [ ] Run the pure C# regression probe and verify it passes.

### Task 2: Settings and exact resolver

**Files:**
- Create: `Assets/PSDLayoutTool2/Editor/CommonLibrary/PsdCommonAssetLibrarySettings.cs`
- Create: `Assets/PSDLayoutTool2/Editor/CommonLibrary/PsdCommonAssetResolver.cs`
- Create: `Assets/PSDLayoutTool2/Editor/CommonLibrary/PsdCommonAssetLibrarySettingsProvider.cs`

- [ ] Create the ScriptableObject that stores prefab and texture root folders at `Assets/PSDLayoutTool2Settings/PsdCommonAssetLibrary.asset`.
- [ ] Implement exact filename-key indexing only below configured roots; fail lookup on zero or multiple matches.
- [ ] Add Project Settings UI with root-folder selection and a default-library creation action.

### Task 3: Importer integration

**Files:**
- Modify: `Assets/PSDLayoutTool2/Editor/PsdImporter.cs`

- [ ] Detect common rules before folder/art export.
- [ ] Instantiate common prefabs with `PrefabUtility.InstantiatePrefab`, parent them, and apply PSD root layout only.
- [ ] Create UI Image or SpriteRenderer from common Sprite references while retaining PSD layout.
- [ ] Do not export PNG or descend into a resolved common prefab subtree; log and stop on lookup failure.

### Task 4: Index invalidation and verification

**Files:**
- Create: `Assets/PSDLayoutTool2/Editor/CommonLibrary/PsdCommonAssetLibraryPostprocessor.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdCommonAssetTests.cs`

- [ ] Invalidate the in-memory resolver index only when assets under configured roots change.
- [ ] Run pure parsing tests, resolver validation tests where possible, and `git diff --check`.
- [ ] Do not launch/restart Unity; hand runtime import verification to the user.
