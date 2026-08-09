# AI Visual Local Organizer Stability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the mock local-organizer visual analysis with a real Codex CLI image workflow that reliably produces, previews, validates, and transactionally applies multiple non-overlapping child Prefabs from one locked local scope.

**Architecture:** Unity deterministically owns candidate membership, captures paired PSD/Unity evidence, validates schema-constrained AI semantics, and compiles those semantics into the existing v2 hierarchy plan. Codex CLI is mandatory for semantic decisions, while Unity owns scope enforcement, no-write preflight, asset paths, apply, rollback, and Replay ordering. Every run is fingerprinted and retained under `Library`, and no asset write is permitted before explicit confirmation.

**Tech Stack:** Unity Editor C#, UI Toolkit, Newtonsoft.Json/JObject, PhotoshopFile `PsdFile` and `ImageDecoder`, Unity Preview Scene APIs, PrefabUtility/AssetDatabase, Codex CLI (`exec`, `exec resume`, `--image`, `--output-schema`, `--json`), NUnit EditMode tests, uloop.

---

## Execution Guardrails

- Work from the Unity project root `E:\Project\Demo\monsterhunter` when running Unity or uloop commands. Source paths below are relative to that root.
- Preserve unrelated staged and unstaged changes. Stage and commit only the files named by the current task.
- Do not replace the real Codex path with deterministic decisions, mock scores, structure-only fallbacks, or a second AI provider.
- Do not mutate the open Prefab Stage during capture. All Unity image capture occurs in an isolated Preview Scene.
- Do not write Prefabs, Profiles, Replay records, or `.meta` files during analysis or preflight.
- Keep the existing manual `Confirm and Update` gate. AI produces evidence and semantic decisions; it never bypasses Unity validation or confirmation.
- The target PSD smoke fixture is `Assets/PSDLayoutTool2/TestData/跑酷 新ui-导出版本.psd`.
- After every C# write, reopen changed files and check Chinese text for `\\uXXXX`, `???`, replacement characters, BOM drift, or mojibake.
- New hierarchy organizer C# files use namespace `PsdLayoutTool2`; new test files use `PsdLayoutTool2.Tests`. Do not place Editor-only code outside `Editor/`.
- If uloop reports an unsaved Scene or Prefab Stage, preserve the user's state and report the blocked runtime test. Never save or discard it automatically.

## Verification Commands

Use these command shapes throughout the plan:

```powershell
uloop run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyChatCleanupExecutionTests --save-before-run false
uloop compile --project-path E:\Project\Demo\monsterhunter --force-recompile false --wait-for-domain-reload true
git diff --check -- Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy Assets/PSDLayoutTool2/Editor/Tests
```

Expected passing test output: the selected EditMode class reports zero failed tests. Expected compile output: zero new errors and zero new warnings from this feature. If the existing external error at `Assets/Frame/Framework/LFramework/Scripts/Hotfix/Entry.cs(20)` reappears because `LSystemApplication.ClearReflectionCache()` is inaccessible, record it separately and do not attribute it to this feature.

## Task 1: Restore a Green v2 Planning Baseline

**Files:**

- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyChatCleanupExecutionTests.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyChatClientTests.cs`
- Test: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyChatCleanupExecutionTests.cs`

- [ ] **Step 1: Add one canonical v2 plan fixture helper**

Add a helper that always includes every required top-level array, especially `containmentResolutions` and `flatSiblingResolutions`:

```csharp
private static JObject CreateEmptyV2Plan(string prefabPath)
{
    return new JObject
    {
        ["version"] = 2,
        ["prefabPath"] = prefabPath,
        ["wrappers"] = new JArray(),
        ["moves"] = new JArray(),
        ["renames"] = new JArray(),
        ["emptyContainerRemovals"] = new JArray(),
        ["tightBounds"] = new JArray(),
        ["textureRenames"] = new JArray(),
        ["spriteAtlasRenames"] = new JArray(),
        ["containmentResolutions"] = new JArray(),
        ["flatSiblingResolutions"] = new JArray(),
        ["componentFamilyDecisions"] = new JArray(),
        ["componentExtractions"] = new JArray(),
        ["stateComponentExtractions"] = new JArray(),
        ["variantComponentExtractions"] = new JArray(),
        ["statefulComponentExtractions"] = new JArray()
    };
}
```

- [ ] **Step 2: Replace incomplete inline fixtures with the canonical helper**

Keep each test's intended operations, but build from `CreateEmptyV2Plan` so old tests fail only for the behavior they assert.

- [ ] **Step 3: Run the previously failing test class**

```powershell
uloop run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyChatCleanupExecutionTests --save-before-run false
```

Expected: zero failures; no failure should be caused by missing required v2 arrays.

- [ ] **Step 4: Run the chat client tests**

```powershell
uloop run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyChatClientTests --save-before-run false
```

Expected: zero failures.

- [ ] **Step 5: Commit the baseline repair**

```text
Keep visual organizer tests anchored to the executable v2 contract

Constraint: All hierarchy plans require explicit containment and flat-sibling resolution arrays.
Rejected: Relax production validation for legacy test fixtures | It would hide malformed AI plans.
Confidence: high
Scope-risk: narrow
Directive: Build new plan fixtures through the canonical v2 helper.
Tested: PsdHierarchyChatCleanupExecutionTests; PsdHierarchyChatClientTests
```

## Task 2: Define the Visual Evidence Contract and JSON Schema

**Files:**

- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualContracts.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualContracts.cs.meta`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualEvidenceValidator.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualEvidenceValidator.cs.meta`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Schemas.meta`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Schemas/visual-evidence.schema.json`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Schemas/visual-evidence.schema.json.meta`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyVisualEvidenceValidatorTests.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyVisualEvidenceValidatorTests.cs.meta`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualScore.cs`

- [ ] **Step 1: Write failing contract tests**

Cover exact candidate coverage, duplicate IDs, fingerprint mismatch, score range, confidence gates, PSD/Unity disagreement, template membership, variant state count, stateful evidence, and unknown candidate IDs.

```csharp
[Test]
public void ValidateRejectsMissingCandidateResult()
{
    PsdHierarchyVisualEvidenceBatch batch = PsdHierarchyVisualEvidenceFixture.Batch(
        "scope-a", "snapshot-a", "candidate_001", "candidate_002");
    JObject response = PsdHierarchyVisualEvidenceFixture.Response(
        "scope-a", "snapshot-a", "candidate_001");

    PsdHierarchyVisualEvidenceValidationResult result =
        PsdHierarchyVisualEvidenceValidator.Validate(batch, response);

    Assert.That(result.isValid, Is.False);
    StringAssert.Contains("candidate_002", result.error);
}
```

- [ ] **Step 2: Run the new tests and confirm failure**

Expected: compile failure because the visual evidence types and validator do not exist yet.

- [ ] **Step 3: Add strongly typed runtime contracts**

Define serializable contracts with fixed enum strings and no executable hierarchy fields:

```csharp
internal enum PsdHierarchyVisualDecision
{
    Extract,
    GroupOnly,
    LeaveUnchanged,
    NeedsReview
}

[Serializable]
internal sealed class PsdHierarchyVisualCandidateResult
{
    public string candidateId;
    public string decision;
    public string componentKind;
    public string suggestedName;
    public int similarityScore;
    public int confidence;
    public string psdUnityConsistency;
    public string templateCandidateId;
    public string[] instanceCandidateIds;
    public PsdHierarchyVisualStateAssignment[] states;
    public string evidence;
}
```

Also define batch fingerprints, manifest references, validation diagnostics, retry metadata, and merged evidence. Keep fields internal and JSON-focused.

- [ ] **Step 4: Add the strict output schema**

The schema must set `additionalProperties: false`, require both fingerprints, require `results`, constrain enum strings, constrain both scores to `0..100`, and forbid hierarchy paths/asset paths by omission.

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "required": ["scopeFingerprint", "snapshotFingerprint", "results"],
  "properties": {
    "scopeFingerprint": { "type": "string", "minLength": 16 },
    "snapshotFingerprint": { "type": "string", "minLength": 16 },
    "results": {
      "type": "array",
      "items": { "$ref": "#/$defs/result" }
    }
  }
}
```

- [ ] **Step 5: Implement semantic validation and confidence gates**

`PsdHierarchyVisualEvidenceValidator` must compare exact ID sets and fingerprints before evaluating semantics. Any `psdUnityConsistency != "consistent"` forces `needs_review`, even when confidence is above 80. Scores 60-79 remain disabled by default; scores below 60 cannot compile into extraction.

- [ ] **Step 6: Run validator tests**

```powershell
uloop run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyVisualEvidenceValidatorTests --save-before-run false
```

Expected: zero failures.

- [ ] **Step 7: Commit the contract**

```text
Make visual AI output reject ambiguity before planning

Constraint: AI may describe semantics but may not emit executable hierarchy operations.
Rejected: Parse free-form Markdown as the primary visual contract | It cannot guarantee candidate coverage.
Confidence: high
Scope-risk: narrow
Directive: Keep the JSON Schema and Unity semantic validator in lockstep.
Tested: PsdHierarchyVisualEvidenceValidatorTests
```

## Task 3: Create Deterministic Evidence Packages and Run Records

**Files:**

- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualEvidencePackage.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualEvidencePackage.cs.meta`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualRunRecord.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualRunRecord.cs.meta`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyVisualEvidencePackageTests.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyVisualEvidencePackageTests.cs.meta`

- [ ] **Step 1: Write failing package tests**

Test deterministic candidate ordering, run ID isolation, SHA-256 hashes, manifest round-trip, Unicode/space paths, `Library`-only output, and retention of five successful plus ten failed runs.

- [ ] **Step 2: Run and confirm the tests fail**

Expected: compile failure because the evidence package types do not exist.

- [ ] **Step 3: Implement package paths and atomic writes**

```csharp
internal static string GetCandidateDirectory(string projectRoot, string runId, int ordinal)
{
    return Path.Combine(
        projectRoot,
        "Library",
        "PSDLayoutTool2",
        "VisualAnalysis",
        runId,
        "candidate_" + ordinal.ToString("000", CultureInfo.InvariantCulture));
}
```

Write files through a temporary sibling path and `File.Move` into place. Hash the final PNG bytes, not `Texture2D` object state. Reject any resolved path outside the project `Library/PSDLayoutTool2` root.

- [ ] **Step 4: Implement the diagnostic run record**

Store raw/validated responses, retry history, compiled plan, preflight/apply reports, and normalization records under `Library/PSDLayoutTool2/VisualRuns/<runId>/`. This code must never call `AssetDatabase` or Replay APIs.

- [ ] **Step 5: Add retention pruning tests and implementation**

Sort by recorded completion UTC, retain the latest five successes and ten failures, and never delete an active run.

- [ ] **Step 6: Run package tests**

```powershell
uloop run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyVisualEvidencePackageTests --save-before-run false
```

Expected: zero failures and no new files under `Assets/`.

- [ ] **Step 7: Commit evidence persistence**

```text
Preserve reproducible visual evidence without importing assets

Constraint: Analysis artifacts must stay under Library and remain outside Git and AssetDatabase.
Rejected: Store thumbnails under Assets | It would trigger imports and create metadata before confirmation.
Confidence: high
Scope-risk: narrow
Directive: Hash final bytes and keep run-record retention deterministic.
Tested: PsdHierarchyVisualEvidencePackageTests
```

## Task 4: Capture the PSD Side from Stable Layer IDs

**Files:**

- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyPsdVisualCapture.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyPsdVisualCapture.cs.meta`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyPsdVisualCaptureTests.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyPsdVisualCaptureTests.cs.meta`
- Reuse: `Assets/PSDLayoutTool2/Editor/PsdFile/ImageDecoder.cs`

- [ ] **Step 1: Write failing stable-ID capture tests**

Cover direct art layers, folder descendants, original layer order, union bounds, proportional transparent padding, opacity/masks exposed by `ImageDecoder`, duplicate display names with distinct `lyid`, missing IDs, ambiguous IDs, and pixel-hash repeatability.

- [ ] **Step 2: Run and confirm failure**

Expected: compile failure because `PsdHierarchyPsdVisualCapture` does not exist.

- [ ] **Step 3: Implement stable layer indexing**

Load the PSD with the existing PhotoshopFile integration and build a unique `lyid` index. Never fall back to display names.

```csharp
PsdFile psd = new PsdFile(sourcePsdFullPath);
IReadOnlyDictionary<string, Layer> layersByStableId = BuildStableLayerIndex(psd.Layers);
IReadOnlyList<Layer> orderedLayers = ResolveExactLayers(
    candidate.stableLayerIds,
    layersByStableId);
return CompositeCandidate(orderedLayers, candidate.sourceBounds, paddingRatio: 0.06f);
```

The bundled `PhotoshopFile.PsdFile` constructor closes its `FileStream` after loading and does not implement `IDisposable`. Follow that existing lifetime model; do not add reflection or a second PSD parser.

- [ ] **Step 4: Composite actual PSD pixels**

Use `ImageDecoder.DecodeImage(layer)` for resolved layers and `ImageDecoder.DecodeMergedImageCrop(layer)` only for the same unsupported merged-content cases already handled by the importer. Destroy temporary textures in `finally`. Preserve alpha and aspect ratio; do not resize to square.

- [ ] **Step 5: Reject incomplete mapping and validate bytes**

Return a structured capture error containing candidate ID, missing/duplicate stable ID, and source PSD asset path. Encode/decode the final PNG once and compare dimensions and nontransparent pixel ratio.

- [ ] **Step 6: Run capture tests**

```powershell
uloop run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyPsdVisualCaptureTests --save-before-run false
```

Expected: zero failures; repeated capture produces identical PNG SHA-256.

- [ ] **Step 7: Commit PSD capture**

```text
Ground visual decisions in stable PSD layer pixels

Constraint: PSD evidence must map through persisted layer IDs and preserve original ordering.
Rejected: Resolve PSD layers by display name | Duplicate and renamed layers would make evidence non-authoritative.
Confidence: high
Scope-risk: moderate
Directive: Reuse PhotoshopFile and ImageDecoder; do not add another PSD parser.
Tested: PsdHierarchyPsdVisualCaptureTests
```

## Task 5: Replace Prefab Stage Capture with an Isolated Preview Scene

**Files:**

- Replace implementation: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualAnalyzer.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyUnityVisualCapture.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyUnityVisualCapture.cs.meta`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyUnityVisualCaptureTests.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyUnityVisualCaptureTests.cs.meta`

- [ ] **Step 1: Write failing isolation and pixel tests**

Use fixtures containing Sprite, TMP, Mask, custom material, alpha, inactive nodes, duplicate names, and nested RectTransforms. Assert the original Prefab Stage active states and serialized values are byte-for-byte unchanged after capture.

- [ ] **Step 2: Add nonblank and cleanup tests**

Assert positive dimensions, PNG round-trip, minimum nontransparent ratio, rejection of all-transparent/all-black/single-color placeholder frames, repeated pixel-hash consistency, and zero leaked Preview Scenes, cameras, Canvases, RenderTextures, or clones.

- [ ] **Step 3: Run and confirm the tests fail against the temporary-camera implementation**

Expected: isolation tests fail because the current implementation captures against the live stage and can toggle active state.

- [ ] **Step 4: Implement isolated capture**

```csharp
Scene previewScene = EditorSceneManager.NewPreviewScene();
GameObject clone = null;
RenderTexture target = null;
try
{
    clone = UnityEngine.Object.Instantiate(source.gameObject);
    SceneManager.MoveGameObjectToScene(clone, previewScene);
    ConfigurePreviewCanvasAndCamera(clone, sourceBounds, out Camera camera, out target);
    return RenderAndReadback(camera, target, sourceBounds);
}
finally
{
    if (target != null) target.Release();
    if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
    EditorSceneManager.ClosePreviewScene(previewScene);
}
```

Record observed active state in the manifest, then activate only the isolated clone when required for rendering. Never call `SetActive` on a source Stage object.

- [ ] **Step 5: Preserve equivalent PSD/Unity framing**

Render to the same content aspect ratio and source bounds used by PSD capture. Add transparent padding without stretching. Treat Unity rendering as authoritative for executable structure but record visible PSD/Unity conflict.

- [ ] **Step 6: Remove mock thumbnail persistence from the analyzer**

Keep `PsdHierarchyVisualAnalyzer` as the paired-capture coordinator. It delegates PSD capture to `PsdHierarchyPsdVisualCapture` and isolated Unity capture to `PsdHierarchyUnityVisualCapture`. Delete its live-stage camera path and every `mock_analysis_v1` assumption.

- [ ] **Step 7: Run capture tests**

```powershell
uloop run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyUnityVisualCaptureTests --save-before-run false
```

Expected: zero failures and unchanged Prefab Stage state.

- [ ] **Step 8: Commit isolated rendering**

```text
Make Unity visual evidence independent of the open Prefab Stage

Constraint: Capturing an inactive or masked candidate must not mutate the user's editing state.
Rejected: Toggle source nodes for a temporary camera | It can dirty or corrupt the open Prefab Stage.
Confidence: high
Scope-risk: moderate
Directive: All capture objects must live and die inside one Preview Scene.
Tested: PsdHierarchyUnityVisualCaptureTests
```

## Task 6: Add Image and Output-Schema Support to the Codex CLI Transport

**Files:**

- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyChatClient.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyCliVisualRequest.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyCliVisualRequest.cs.meta`
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyChatClientTests.cs`

- [ ] **Step 1: Write failing invocation tests**

Test initial and resume calls, exact paired attachment order, one `--image` per path, `--output-schema`, `--json`, Unicode/space paths, read-only sandbox on initial calls, and no image omission on correction.

```csharp
[Test]
public void CreateCodexResumeInvocationReattachesImagesAndSchema()
{
    PsdHierarchyCliInvocation invocation = CreateInvocation(
        sessionId: "session-1",
        images: new[] { @"E:\Evidence 路径\psd.png", @"E:\Evidence 路径\unity.png" });

    CollectionAssert.IsSubsetOf(
        new[] { "exec", "resume", "--json", "--image", "--output-schema" },
        invocation.argumentList);
}
```

- [ ] **Step 2: Run and confirm failures**

Expected: tests fail because the transport accepts only messages and session ID.

- [ ] **Step 3: Extend the transport request without changing nonvisual callers**

Add an optional request object and change `PsdHierarchyCliInvocation` to retain an argument list until the final process-launch boundary:

```csharp
internal sealed class PsdHierarchyCliVisualRequest
{
    internal IReadOnlyList<string> imageFullPaths = Array.Empty<string>();
    internal string outputSchemaFullPath = string.Empty;
    internal IReadOnlyList<PsdHierarchyVisualAttachment> attachments =
        Array.Empty<PsdHierarchyVisualAttachment>();
}

internal readonly struct PsdHierarchyCliInvocation
{
    internal readonly string executablePath;
    internal readonly IReadOnlyList<string> argumentList;
    internal readonly string workingDirectory;
    internal readonly bool writePromptToStandardInput;
}
```

Extend `IPsdHierarchyCliChatTransport.SendAsync` and `CreateCliInvocation` with a nullable visual request. Existing text-only callers must generate byte-for-byte equivalent arguments.

- [ ] **Step 4: Build arguments as an array, not a shell string**

Append `--image <absolute-path>` in manifest order and `--output-schema <absolute-path>` for both `codex exec` and `codex exec resume`. Keep `--json`. Validate every file exists before process launch and quote only at the `ProcessStartInfo` boundary.

- [ ] **Step 5: Map attachment order in the prompt**

The prompt must state `attachment 1 = candidate_001 PSD`, `attachment 2 = candidate_001 Unity`, and so on, matching the argument array exactly.

- [ ] **Step 6: Run transport tests**

```powershell
uloop run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyChatClientTests --save-before-run false
```

Expected: zero failures; existing text-only CLI tests remain unchanged.

- [ ] **Step 7: Commit CLI image transport**

```text
Send paired visual evidence through the real Codex session

Constraint: Initial, resume, and correction calls must all preserve images and the output schema.
Rejected: Embed image paths only in prompt text | Codex would not receive image attachments.
Confidence: high
Scope-risk: moderate
Directive: Keep attachment order explicit and identical in arguments and prompt text.
Tested: PsdHierarchyChatClientTests
```

## Task 7: Implement the Visual Analysis State Machine, Batching, and Retry

**Files:**

- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualSession.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualSession.cs.meta`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyChatCleanupExecution.cs`
- Replace implementation: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualChatPrompt.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyVisualSessionTests.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyVisualSessionTests.cs.meta`

- [ ] **Step 1: Write failing state-transition tests**

Cover `Idle -> SelectionLocked -> CapturingImages -> VisualAnalyzing -> VisualValidated -> Planning -> PlanValidated -> AwaitingConfirmation`, failure from each active stage, cancellation before apply, and rejection of concurrent runs.

- [ ] **Step 2: Write failing batching tests**

Assert at most eight candidates/sixteen images per request, deterministic parent/region ordering, first batch as primary session, later batch ingestion through `resume`, exact candidate coverage, and rejection of cross-batch duplicate/overlapping sources.

- [ ] **Step 3: Write retry tests with a scripted transport**

Prove this exact sequence:

1. initial batch request;
2. one same-session correction with all batch images/schema/manifest/error;
3. clear session ID;
4. one fresh request with full context and all batch images;
5. fail closed while preserving both responses and parse/validation error.

Add the equivalent planning-stage test: correction in primary session, then fresh primary session that replays every visual batch before requesting the complete plan.

- [ ] **Step 4: Run and confirm failures**

Expected: tests fail because `PerformVisualAnalysisAsync` currently returns mock scores after `Task.Delay(100)`.

- [ ] **Step 5: Implement deterministic batching and state transitions**

Make state mutation single-threaded on the Editor main thread. Store the current run ID, scope/snapshot fingerprints, primary session ID, batch session IDs, attempts, cancellation token, and diagnostic paths.

- [ ] **Step 6: Replace mock analysis with the real CLI sequence**

Remove `mock_analysis_v1` and every hardcoded template/instance score. `PerformVisualAnalysisAsync` must capture, validate, send, parse, and semantically validate each batch before planning can begin.

- [ ] **Step 7: Preserve actionable diagnostics**

Store first response, correction response, fresh response, parser error, semantic validation error, CLI exit/timeout details, and the evidence-package path. The UI-facing result must expose these separately rather than collapse them into “AI did not generate a plan.”

- [ ] **Step 8: Run session tests and cleanup execution tests**

```powershell
uloop run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyVisualSessionTests --save-before-run false
uloop run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyChatCleanupExecutionTests --save-before-run false
```

Expected: zero failures and no remaining mock response path.

- [ ] **Step 9: Commit orchestration**

```text
Bound visual AI retries while preserving the evidence needed to recover

Constraint: A fresh session must receive complete current context, not only the final error.
Rejected: Retry indefinitely in one stale CLI session | Lost context and malformed output would recur invisibly.
Confidence: high
Scope-risk: moderate
Directive: Keep retry counts finite and persist every attempt before returning failure.
Tested: PsdHierarchyVisualSessionTests; PsdHierarchyChatCleanupExecutionTests
```

## Task 8: Compile Visual Semantics into Strict Local Multi-Prefab Plans

**Files:**

- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualPlanCompiler.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualPlanCompiler.cs.meta`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyLocalRepairScope.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyChatCleanupExecution.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyVisualPlanCompilerTests.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyVisualPlanCompilerTests.cs.meta`
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyLocalRepairScopeTests.cs`

- [ ] **Step 1: Write failing compiler tests**

Cover three independent component extractions in one plan, deterministic PascalCase names, collision suffixing, unique `Common/*.prefab` paths, disabled review items, confidence gates, exact source membership, overlap rejection, and no out-of-scope node references.

- [ ] **Step 2: Add variant/stateful sibling tests**

Assert template, instances, and state representatives are direct siblings. A variant requires at least two observed states. A stateful result must map both common and state-specific sources. No compiler normalization may invent a source node.

- [ ] **Step 3: Run and confirm failure**

Expected: tests fail because local scope currently forbids or empties extraction arrays and there is no visual decision compiler.

- [ ] **Step 4: Implement deterministic plan compilation**

For every eligible AI result, look up the sealed Unity candidate by ID and derive all executable fields from that candidate. Move the existing private `ToPascalCase` and unique `Common` path logic from `PsdHierarchyChatCleanupExecution` into internal compiler helpers so normalization has one implementation:

```csharp
string assetPath = GetUniqueCommonPrefabPath(
    context,
    ToPascalCase(result.suggestedName),
    reservedAssetPaths);
```

Do not accept AI-provided node paths, asset paths, wrapper IDs, moves, or deletes.

- [ ] **Step 5: Extend strict local scope for sealed extraction items**

Replace the blanket “extraction arrays must be empty” rule with exact source-set validation against locked candidate IDs. All template/instance sources must be editable and inside the fingerprinted scope; output parents must be approved direct parents.

- [ ] **Step 6: Normalize conflicts deterministically**

Sort candidates by ordinal candidate ID. Exclude lower-confidence overlaps and record the exclusion. If confidence ties, use stable candidate ID order. Never silently shrink a candidate's source membership.

- [ ] **Step 7: Run compiler and scope tests**

```powershell
uloop run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyVisualPlanCompilerTests --save-before-run false
uloop run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyLocalRepairScopeTests --save-before-run false
```

Expected: zero failures; the three-extraction fixture yields exactly three distinct `Common/*.prefab` paths.

- [ ] **Step 8: Commit compiler and scope enforcement**

```text
Turn AI semantics into deterministic local multi-Prefab plans

Constraint: Unity owns source membership, destinations, and asset paths inside the locked scope.
Rejected: Let AI write executable moves and paths | It would make local scope and rollback unverifiable.
Confidence: high
Scope-risk: moderate
Directive: Never change a sealed candidate source set during normalization.
Tested: PsdHierarchyVisualPlanCompilerTests; PsdHierarchyLocalRepairScopeTests
```

## Task 9: Add No-Write Multi-Extraction Preflight

**Files:**

- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyNativeCleanupExecutor.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyPreflightReport.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyPreflightReport.cs.meta`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyNativeCleanupExecutorPreflightTests.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyNativeCleanupExecutorPreflightTests.cs.meta`

- [ ] **Step 1: Write failing no-write tests**

Capture hashes and directory listings for the main Prefab, Profile, Replay, `Common`, and relevant `.meta` files before `Validate`. Assert they are identical afterward, including for a valid three-extraction plan.

- [ ] **Step 2: Write failing structural preflight tests**

Cover non-overlap, direct siblings, variant/state mappings, empty removal targets, unique paths, expected node count, expected child-Prefab count, and expected instance-reference count.

- [ ] **Step 3: Run and confirm failures**

Expected: at least the multi-extraction report/count assertions fail because the current validation path does not expose the complete report.

- [ ] **Step 4: Split simulation from persistence**

Load the main Prefab through `PrefabUtility.LoadPrefabContents`, simulate all operations against the isolated contents, and model child-Prefab outputs in memory. Do not call `SaveAsPrefabAsset`, `CreateAsset`, `DeleteAsset`, or write files in preflight.

- [ ] **Step 5: Return an explicit report**

```csharp
internal sealed class PsdHierarchyPreflightReport
{
    internal int originalNodeCount;
    internal int expectedNodeCount;
    internal int childPrefabCount;
    internal int instanceReferenceCount;
    internal string[] outputAssetPaths;
    internal string[] normalizations;
    internal string[] exclusions;
}
```

Include the report in the run record and UI state.

- [ ] **Step 6: Run executor preflight tests**

```powershell
uloop run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyNativeCleanupExecutorPreflightTests --save-before-run false
```

Expected: zero failures and no filesystem or AssetDatabase changes.

- [ ] **Step 7: Commit no-write preflight**

```text
Prove multi-Prefab plans without touching project assets

Constraint: Analysis and preflight must create no Prefab, Profile, Replay, or metadata files.
Rejected: Validate by writing temporary assets under Assets | It violates the confirmation boundary.
Confidence: high
Scope-risk: moderate
Directive: Keep all preflight child assets as in-memory models until confirmation.
Tested: PsdHierarchyNativeCleanupExecutorPreflightTests
```

## Task 10: Make Multi-Asset Apply Transactional and Verify Rollback

**Files:**

- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyMultiAssetTransaction.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyMultiAssetTransaction.cs.meta`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyNativeCleanupExecutor.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdPrefabTransactionalSave.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyCleanupReplayCoordinator.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyMultiAssetTransactionTests.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyMultiAssetTransactionTests.cs.meta`

- [ ] **Step 1: Write fault-injection rollback tests**

Inject failure after each phase: backup, first/second/third child save, source replacement, main Prefab save, reload verification, Profile update, and Replay update. Assert restoration of asset and `.meta` hashes, deletion of newly created assets, and preservation of unrelated files.

- [ ] **Step 2: Write the successful three-child-Prefab test**

Assert exactly three nonempty child Prefabs, distinct asset paths, expected instance references in the main Prefab, no duplicate assets, no empty shells, and no remaining required candidate.

- [ ] **Step 3: Run and confirm failure**

Expected: current single-main-Prefab backup and ad hoc child deletion do not satisfy every rollback phase.

- [ ] **Step 4: Generalize asset backup and write lock**

Move the existing private `AssetBackup` behavior into `PsdHierarchyMultiAssetTransaction` as an internal generalized backup entry. Refactor `PsdPrefabTransactionalSave` to use that transaction for its existing two-asset path. The transaction captures the main Prefab, Profile, Replay, all existing output assets, and every `.meta`; it records non-existent paths for deletion and stores backup copies under `Library/PSDLayoutTool2/Transactions/<transactionId>/`.

- [ ] **Step 5: Apply in deterministic order**

After a fresh fingerprint check and target write lock, create child Prefabs by candidate ID, replace sources with instances, save the main Prefab, refresh/reload, and verify links/counts/geometry/order.

- [ ] **Step 6: Update Profile and Replay last**

Only after all Prefab verification succeeds may the transaction update Profile and Replay. A failure in either update rolls back every touched asset.

- [ ] **Step 7: Verify rollback hashes**

After restoration, refresh `AssetDatabase` and compare restored bytes and `.meta` bytes to the recorded hashes. Surface a high-priority recovery error and backup path if rollback verification fails.

- [ ] **Step 8: Run transaction tests**

```powershell
uloop run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyMultiAssetTransactionTests --save-before-run false
```

Expected: zero failures across every fault point and the successful three-Prefab path.

- [ ] **Step 9: Commit transaction support**

```text
Keep multi-Prefab apply atomic across assets and Replay state

Constraint: A partial child-Prefab write must never survive a failed confirmation.
Rejected: Delete only newly created child assets on error | Existing assets, metadata, Profile, and Replay can also be modified.
Confidence: high
Scope-risk: broad
Directive: Verify rollback hashes before releasing the target write lock.
Tested: PsdHierarchyMultiAssetTransactionTests
```

## Task 11: Integrate the Dedicated Local Organizer Review UI

**Files:**

- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyLocalRepairWindow.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyOrganizerEntry.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyChatWindow.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyLocalRepairWindowTests.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyChatWindowTests.cs`

- [ ] **Step 1: Write failing UI state tests**

Cover lock selection, stale fingerprint reset, paired thumbnail rows, confidence/status labels, conflict warning, disabled review items, output path display, normalization/exclusion lists, preflight counts, first/correction/fresh diagnostics, cancellation, and confirmation enablement only in `AwaitingConfirmation`.

- [ ] **Step 2: Run and confirm failures**

Expected: current window cannot display or drive the complete visual session state.

- [ ] **Step 3: Bind the session state to the dedicated window**

Show one repeated review row per semantic decision with fixed-aspect PSD and Unity thumbnails, AI decision/kind/reason, confidence, template/instance/state counts, output asset path, and review toggle. Do not nest cards or allow editing source membership.

- [ ] **Step 4: Enforce state-dependent actions**

Disable concurrent analysis. Allow cancellation before `Applying`. Selection or authoritative snapshot changes invalidate evidence and return to `SelectionLocked`. `Confirm and Update` is enabled only after valid evidence, compiled plan, and successful no-write preflight.

- [ ] **Step 5: Preserve complete failure information**

Display the first response summary, last correction/fresh response summary, exact parse/validation/CLI error, and run-record path. Keep detailed diagnostics collapsible; do not replace them with a generic “no executable plan” message.

- [ ] **Step 6: Run UI tests**

```powershell
uloop run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyLocalRepairWindowTests --save-before-run false
uloop run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyChatWindowTests --save-before-run false
```

Expected: zero failures.

- [ ] **Step 7: Commit UI integration**

```text
Expose evidence and preflight truth before local organizer confirmation

Constraint: Users must review paired images and complete failure diagnostics before any write.
Rejected: Hide visual analysis behind a single success or failure label | It prevents diagnosis and informed confirmation.
Confidence: high
Scope-risk: moderate
Directive: Source membership changes always require a new locked selection and AI run.
Tested: PsdHierarchyLocalRepairWindowTests; PsdHierarchyChatWindowTests
```

## Task 12: Add Real AI Smoke and 20-Run Stability Verification

**Files:**

- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualStabilityRunner.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualStabilityRunner.cs.meta`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyVisualSmokeTests.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyVisualSmokeTests.cs.meta`
- Create: `docs/verification.meta`
- Create: `docs/verification/ai-visual-local-organizer-stability.md`
- Create: `docs/verification/ai-visual-local-organizer-stability.md.meta`

- [ ] **Step 1: Add an explicit real-AI smoke category**

The smoke test must be opt-in and must never run in the ordinary unit suite without a configured Codex CLI. It must use the real transport, real source PSD, current authoritative Prefab snapshot, and a locked local selection containing at least three valid non-overlapping candidates.

- [ ] **Step 2: Implement the no-apply stability runner**

Run the complete capture, visual analysis, merge, planning, compile, and preflight flow 20 times. Record per run:

- CLI session IDs and retry counts;
- candidate IDs and source memberships;
- visual response hash;
- compiled plan hash;
- child-Prefab count/names/paths;
- elapsed time and failure stage;
- run-record directory.

The runner must not click confirmation or modify assets.

- [ ] **Step 3: Define the acceptance thresholds**

The report passes only when:

- 20 of 20 runs produce schema-valid visual evidence;
- at least 19 of 20 runs produce a Unity-valid executable plan;
- every executable plan contains at least three non-overlapping child-Prefab extractions for the approved locked fixture;
- all executable runs agree on candidate source membership and output path set after deterministic normalization;
- no run writes assets or Replay during no-apply mode;
- every failure, if any, includes its concrete stage, response summaries, and error.

- [ ] **Step 4: Run targeted unit and compile verification first**

```powershell
uloop run-tests --project-path E:\Project\Demo\monsterhunter --filter-type assembly --filter-value PsdLayoutTool2.Editor.Tests --save-before-run false
uloop compile --project-path E:\Project\Demo\monsterhunter --force-recompile false --wait-for-domain-reload true
```

Expected: zero feature test failures and no new compile errors. Record any unrelated existing compile blocker separately.

- [ ] **Step 5: Run the real AI smoke once**

Use the actual local organizer entry and real Codex CLI with:

```text
PSD: Assets/PSDLayoutTool2/TestData/跑酷 新ui-导出版本.psd
Mode: strict local
Apply: false
Expected child Prefabs: at least 3
```

Expected: paired evidence for every candidate, validated AI semantics, executable preflight, zero asset writes.

- [ ] **Step 6: Run the 20-attempt stability loop**

Keep all `Library/PSDLayoutTool2/VisualRuns/<runId>` records until the report is written. Do not silently rerun failed attempts to improve the denominator.

- [ ] **Step 7: Review and apply one successful run through the real UI**

Open the dedicated panel, lock the same selection, click AI organize, review at least three extraction rows, then use the existing `Confirm and Update` flow. Do not create a replacement main Prefab.

- [ ] **Step 8: Verify post-apply assets**

Reload the target Prefab and `Common` directory and assert:

- target Prefab saved at its existing path;
- expected number of nonempty child Prefabs exists;
- main Prefab instances reference those exact assets;
- no duplicates, empty shells, or unhandled required candidates remain;
- Profile and Replay changed only after successful asset verification;
- the PSD source file remains unchanged.

- [ ] **Step 9: Write the verification report**

Include exact date/time, Unity version, Codex CLI version, source PSD GUID, target Prefab GUID, locked node IDs, 20-run table, success percentage, retry distribution, output path set, apply verification, compile/test output, and any unrelated blocker.

- [ ] **Step 10: Commit the smoke runner and evidence report**

```text
Measure real AI organizer stability before treating it as production-ready

Constraint: Stability claims require repeated real Codex image runs and one confirmed transactional apply.
Rejected: Infer reliability from scripted transport tests alone | They do not measure model output or image attachment behavior.
Confidence: high
Scope-risk: moderate
Directive: Preserve the raw run records behind every reported success rate.
Tested: full EditMode suite; Unity compile; real AI smoke; 20-run stability; one confirmed apply
```

## Final Completion Checklist

- [ ] `mock_analysis_v1`, hardcoded visual scores, and live Prefab Stage capture paths are absent.
- [ ] Initial, resume, correction, and fresh Codex calls all attach the required images and output schema.
- [ ] Visual responses have exact candidate coverage and survive both schema and Unity semantic validation.
- [ ] The planning AI has directly received every image across bounded batches.
- [ ] A valid local run compiles at least three non-overlapping extractions with unique sibling `Common/*.prefab` paths.
- [ ] Preflight writes nothing and reports expected node, asset, and instance counts.
- [ ] Transaction fault injection restores all assets and `.meta` files at every phase.
- [ ] Replay updates only after verified asset persistence.
- [ ] The dedicated window shows paired evidence, confidence, normalizations, exclusions, preflight counts, and actionable diagnostics.
- [ ] Targeted tests, full EditMode tests, compile, one real smoke run, 20 no-apply runs, and one confirmed apply have fresh recorded evidence.
- [ ] `git diff --check` passes for all implementation changes.
- [ ] No unrelated staged or unstaged files are included in any task commit.
