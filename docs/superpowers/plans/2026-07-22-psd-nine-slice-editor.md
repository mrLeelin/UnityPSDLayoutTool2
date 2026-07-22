# PSD Nine-Slice Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a PSD-native editor where artists can browse image layers, manually adjust nine-slice borders, and retain those values across incremental Prefab imports.

**Architecture:** Store only per-layer manual decisions in the PSD asset importer `userData`; load PSD layers and preview textures in a disposable editor session; keep the EditorWindow responsible only for interaction and rendering. The importer reads the store at import start and resolves it before authoring tags or embedded metadata.

**Tech Stack:** Unity Editor IMGUI, existing `PhotoshopFile.PsdFile` and `ImageDecoder`, Unity `AssetImporter.userData`, NUnit-style editor tests.

---

### Task 1: Persistent manual override model

**Files:**
- Create: `Assets/PSDLayoutTool2/Editor/NineSlice/PsdNineSliceOverrideStore.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdNineSliceOverrideStoreTests.cs`

- [ ] **Step 1: Write failing storage tests**

```csharp
[Test]
public void WriteThenReadPreservesOtherUserDataAndBorder()
{
    string data = PsdNineSliceOverrideStore.Write(
        "other-tool=value", 41U, true, new PsdNineSliceBorder(10, 20, 30, 40));
    PsdNineSliceOverride value;
    Assert.IsTrue(PsdNineSliceOverrideStore.TryGet(data, 41U, out value));
    Assert.IsTrue(value.Enabled);
    Assert.AreEqual(10, value.Border.Left);
    Assert.IsTrue(data.Contains("other-tool=value"));
}
```

- [ ] **Step 2: Run the test and confirm compilation fails because the store does not exist.**

Run: `uloop compile --wait-for-domain-reload false`

Expected: a compile error referencing `PsdNineSliceOverrideStore`.

- [ ] **Step 3: Implement a line-based versioned store**

Implement `TryGet`, `Write`, and `Remove`, using one `psd-layout-nine-slice-overrides:v1:` line containing stable layer IDs, enabled state, and four integer borders. Preserve non-owned `userData` lines.

- [ ] **Step 4: Re-run the focused test and compile**

Expected: the storage test passes and Unity reports zero compilation errors.

### Task 2: PSD layer preview session

**Files:**
- Create: `Assets/PSDLayoutTool2/Editor/NineSlice/PsdNineSlicePsdLayerSession.cs`
- Test: `Assets/PSDLayoutTool2/Editor/Tests/PsdNineSliceOverrideStoreTests.cs`

- [ ] **Step 1: Write a failing test for PSD layer filtering**

```csharp
[Test]
public void SessionListsVisibleRasterLeafLayers()
{
    using (var session = PsdNineSlicePsdLayerSession.Open("Assets/PSDLayoutTool2/TestData/7日任务拆分.psd"))
    {
        Assert.Greater(session.Layers.Count, 0);
        Assert.IsTrue(session.Layers.All(layer => layer.IsVisibleRasterLeaf));
    }
}
```

- [ ] **Step 2: Implement PSD loading and lazy preview decode**

Use `PsdFile`, `Layer.Visible`, `Layer.Children`, `Layer.IsTextLayer`, `Layer.Rect`, and `ImageDecoder.DecodeImage`. Dispose cached `Texture2D` objects in `Dispose`.

- [ ] **Step 3: Run the test and verify no decoded texture is retained after disposal.**

### Task 3: PSD editor window

**Files:**
- Modify: `Assets/PSDLayoutTool2/Editor/NineSlice/PsdNineSliceWindow.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdInspector.cs`

- [ ] **Step 1: Preserve PNG mode and add PSD mode**

`Open(path)` detects `.psd`, creates a `PsdNineSlicePsdLayerSession`, and shows a selectable left-hand layer list. Existing PNG menu behavior remains unchanged.

- [ ] **Step 2: Implement selected layer editing**

Draw preview guides at left, top, right, and bottom; hit-test guide drags; clamp borders so the center has at least one pixel. Provide numeric fields, enable toggle, automatic candidate, save current, and clear manual override.

- [ ] **Step 3: Persist through PSD importer `userData`**

Read/write the store via `AssetImporter.GetAtPath(psdPath)`, call `SaveAndReimport` only after saving or clearing, and redraw selection after persistence.

- [ ] **Step 4: Open the window by a Unity dynamic smoke script**

Expected: `PsdNineSliceWindow.Open("Assets/PSDLayoutTool2/TestData/7日任务拆分.psd")` shows visible layer rows and a valid preview for the selected layer.

### Task 4: Importer priority and incremental import

**Files:**
- Modify: `Assets/PSDLayoutTool2/Editor/PsdImporter.cs`
- Test: `Assets/PSDLayoutTool2/Editor/Tests/PsdNineSliceOverrideStoreTests.cs`

- [ ] **Step 1: Write a failing rule-resolution test**

```csharp
[Test]
public void ManualDisabledOverrideSuppressesNameRule()
{
    var overrides = new Dictionary<uint, PsdNineSliceOverride>
    {
        { 41U, PsdNineSliceOverride.Disabled(41U) }
    };
    Assert.IsFalse(PsdNineSliceOverrideResolver.TryResolve(41U, "jiugong_panel", overrides, out _));
}
```

- [ ] **Step 2: Implement override-first rule resolution**

Load PSD importer overrides immediately after `PsdFile` loads. An enabled override supplies a `NineSlice` rule with its border; disabled returns no rule and prevents later name/XMP paths for the same ID.

- [ ] **Step 3: Compile and run dynamic import-resolution smoke test**

Expected: a manual border is selected before a `jiugong` tag, and a manual disabled state produces no nine-slice conversion.

### Task 5: Verification and handoff

**Files:**
- Modify only files from Tasks 1-4 if verification reveals a defect.

- [ ] **Step 1: Run focused editor tests and `uloop compile --wait-for-domain-reload false`.**

- [ ] **Step 2: Open the real PSD editor and capture the list/preview smoke result.**

- [ ] **Step 3: Report exact usage and the layer-ID incremental-update limitation.**
