# Configurable Common Asset Prefixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Common Prefab and Common Texture naming prefixes configurable through project-wide settings without breaking existing projects.

**Architecture:** Ship a default ScriptableObject template in the UPM package and copy it into the consuming project's `Assets` folder on first use. Store normalized prefixes beside the existing project font settings in that project-owned asset, expose it through a custom Inspector, and pass immutable snapshots into import code. Prefix changes invalidate the generated catalog.

**Tech Stack:** Unity 6 Editor, C#, ScriptableSingleton project settings, NUnit Editor tests.

---

### Task 1: Lock the naming contract with tests

**Files:**
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdLayoutProjectSettingsTests.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdCommonAssetTests.cs`

- [ ] Add tests for defaults, underscore normalization, empty fallback, duplicate rejection, and custom PSD/asset parsing.
- [ ] Run the focused Editor tests and confirm they fail because the naming settings API does not exist.

### Task 2: Add project-wide naming settings

**Files:**
- Modify: `Assets/PSDLayoutTool2/Editor/Settings/PsdLayoutProjectSettings.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/Settings/PsdLayoutProjectSettingsGUI.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/Settings/PsdLayoutProjectSettingsProvider.cs`

- [ ] Add serialized naming data, normalized snapshots, duplicate validation, persistence, comments, examples, and search keywords.
- [ ] Mark the Common Asset Catalog stale after a successful prefix change.

### Task 3: Create the UPM-safe project settings asset lifecycle

**Files:**
- Create: `Assets/PSDLayoutTool2/Editor/Settings/Defaults/PsdLayoutProjectSettings.asset`
- Create: `Assets/PSDLayoutTool2/Editor/Settings/PsdLayoutProjectSettingsAsset.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Settings/PsdLayoutProjectSettingsEditor.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdInspector.cs`
- Delete: `Assets/PSDLayoutTool2/Editor/Settings/PsdLayoutProjectSettingsGUI.cs`
- Delete: `Assets/PSDLayoutTool2/Editor/Settings/PsdLayoutProjectSettingsProvider.cs`

- [ ] Copy the package template to `Assets/PSDLayoutTool2Settings/PsdLayoutProjectSettings.asset` on first use.
- [ ] Preserve an existing project copy across package updates.
- [ ] Migrate the legacy ProjectSettings values during first creation.
- [ ] Select and ping the project asset from the PSD Inspector button.
- [ ] Draw all global fields only in the settings asset custom Inspector.

### Task 4: Route all Common parsing through configuration

**Files:**
- Modify: `Assets/PSDLayoutTool2/Editor/CommonLibrary/PsdCommonAssetNameParser.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/CommonLibrary/PsdCommonAssetCatalog.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/CommonLibrary/PsdCommonAssetCatalogSettingsProvider.cs`

- [ ] Replace hard-coded parser constants with the global naming snapshot while retaining explicit snapshot overloads for tests.
- [ ] Display current prefix examples in the catalog settings page.
- [ ] Run focused tests, Unity compilation, and `git diff --check`.
