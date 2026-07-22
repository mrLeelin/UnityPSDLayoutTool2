# PSD Text Effect Conversion Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract PSD text outline and face-dilate conversion into one editor-only class whose constants can be adjusted manually without touching material asset logic.

**Architecture:** Add a pure `PsdTextEffectConversion` static class beside the material factory. The material factory will delegate numeric conversion to it while retaining sole ownership of material comparison, reuse, creation, and existing-asset immutability.

**Tech Stack:** Unity 6 editor C#, TextMeshPro, NUnit editor tests, uloop compile verification

---

## Chunk 1: Extract and Verify the Conversion Boundary

### Task 1: Lock the public tuning surface with failing tests

**Files:**
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdTextMaterialValueTests.cs`

- [ ] **Step 1: Replace reflection-based outline conversion coverage with direct calls**

  Call `PsdTextEffectConversion.ConvertOutline(pixelWidth, fontSize)` for the five existing Figma-aligned examples, and add direct coverage for `ConvertFaceDilate(0.25f) == 0.125f`.

- [ ] **Step 2: Compile to verify the tests fail for the intended reason**

  Run: `uloop compile --force-recompile false --wait-for-domain-reload true --project-path E:\Project\Demo\monsterhunter`

  Expected: compilation fails because `PsdTextEffectConversion` does not exist yet; no unrelated new error is accepted.

### Task 2: Add the manually adjustable conversion class

**Files:**
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/PsdTextEffectConversion.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/PsdTextEffectConversion.cs.meta`

- [ ] **Step 1: Add the editor-only static class**

  In namespace `PsdLayoutTool2`, define `internal static class PsdTextEffectConversion`. Keep the approved tuning surface limited to these three documented constants and two pure conversion methods:

  ```csharp
  public const float OutlineScale = 7f / 3f;
  public const int OutlineDecimalPlaces = 2;
  public const float FaceDilateRatio = 0.5f;
  ```

  Implement `ConvertOutline` as `Clamp01(OutlineScale * pixelWidth / fontSize)` followed by rounding to `OutlineDecimalPlaces`; return zero for non-positive width or font size. Implement `ConvertFaceDilate` as `outlineWidth * FaceDilateRatio`.

- [ ] **Step 2: Compile to verify the new class satisfies direct test references**

  Run the same uloop compile command.

  Expected: success with zero errors; the pre-existing obsolete warning may remain.

- [ ] **Step 3: Run the conversion tests before changing the factory**

  Run: `uloop run-tests --test-mode EditMode --filter-type regex --filter-value "PsdLayoutTool2.Tests.PsdTextMaterialValueTests" --save-before-run false --project-path E:\Project\Demo\monsterhunter`

  Expected: all direct conversion tests pass. If Unity Test Runner refuses because another scene has unsaved user changes, preserve that scene and use the existing read-only reflection test harness to execute `PsdTextMaterialValueTests`; do not save or discard unrelated scene changes.

### Task 3: Delegate material property application to the new class

**Files:**
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/PsdPrefabTextMaterialFactory.cs`
- Test: `Assets/PSDLayoutTool2/Editor/Tests/PsdTextMaterialValueTests.cs`

- [ ] **Step 1: Replace inline calculations**

  Replace `ConvertPsdPixelsToOutlineWidth(...)` with `PsdTextEffectConversion.ConvertOutline(...)`, replace `outlineWidth * 0.5f` with `PsdTextEffectConversion.ConvertFaceDilate(...)`, and remove the old private conversion method.

- [ ] **Step 2: Run targeted regression tests**

  Run: `uloop run-tests --test-mode EditMode --filter-type regex --filter-value "PsdLayoutTool2.Tests.PsdTextMaterialValueTests" --save-before-run false --project-path E:\Project\Demo\monsterhunter`

  Expected: all conversion, material equivalence, signature, and existing-dirty-material tests pass. If blocked by the unrelated unsaved scene, use the same read-only reflection test fallback without saving or discarding it.

- [ ] **Step 3: Run final verification**

  Run the uloop compile command and `git diff --check`.

  Expected: Unity compilation succeeds with zero errors and the diff has no whitespace errors. Inspect `Assets/PSDLayoutTool2/TestData/7日任务拆分/7日任务拆分.prefab` and its referenced material read-only to confirm it still uses the generated `0.25` outline material and never the old outline-1 material. Do not reimport, save, or modify the prefab or any material during this extraction.

- [ ] **Step 4: Report commit boundaries honestly**

  The approved design document is already committed separately. Do not commit implementation unless the user explicitly requests another commit; report the remaining working-tree changes and verification evidence.
