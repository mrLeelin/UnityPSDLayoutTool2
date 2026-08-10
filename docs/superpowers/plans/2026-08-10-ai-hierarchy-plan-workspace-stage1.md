# AI Hierarchy Plan Workspace Stage 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ensure every completed AI analysis produces a reviewable workspace with structured issues instead of collapsing into "no executable plan."

**Architecture:** Add a compatibility `PsdHierarchyPlanWorkspace` around the existing plan preparation pipeline. A validated plan remains one enabled atomic batch; extraction, normalization, runner-preflight, and infrastructure failures become structured issues retained in the workspace. `PsdHierarchyChatWindow` renders a persistent `待处理问题` panel and preserves retry, keep-unchanged, details, locate, and diagnostic-export actions. Stage 1 intentionally keeps the current AI JSON contract and all-or-nothing executor semantics; safe partial apply begins only after the Stage 2 ownership graph and virtual hierarchy exist.

**Tech Stack:** Unity 6 Editor C#, UI Toolkit, Newtonsoft.Json, NUnit/Unity Test Framework, uloop.

---

## Stage 1 Boundaries

- A successful current plan becomes exactly one enabled batch and continues through the existing `ValidatePlanAsync` and `ApplyConfirmedAsync` paths.
- A failed current plan becomes a workspace with zero enabled batches and at least one structured issue. The AI review, final raw reply, validation error, snapshot fingerprint, and retry action remain available.
- `保持原结构` resolves the issue as an explicit no-change decision for this snapshot; it does not fabricate an executable plan.
- `应用安全项` is not implemented in Stage 1. With only one compatibility batch, no failed plan has a provably independent safe subset.
- The executable JSON prompt, v2 normalization rules, runner schema, Native Unity executor, replay behavior, and Prefab transaction semantics remain unchanged.
- Infrastructure failures also use the workspace panel, but they remain visibly distinct from semantic/topology issues.

## Execution Guardrails

- Run Unity and uloop commands from `E:\Project\Demo\monsterhunter`.
- Preserve all unrelated staged and unstaged changes. Stage and commit only files named by the active task.
- Do not save, close, or discard the user's open Scene or Prefab Stage.
- Analysis, workspace construction, issue resolution, details display, and diagnostic export must not write any Prefab or project asset.
- Diagnostic files go only under `Library/PSDLayoutTool2/PlanWorkspaces`; never import them through `AssetDatabase`.
- New hierarchy organizer C# files use namespace `PsdLayoutTool2`; new tests use `PsdLayoutTool2.Tests`.
- Add a Unity `.meta` file for every new file below `Assets/`.
- After every C# write, reopen changed files with UTF-8 and check Chinese text for `\\uXXXX`, `???`, replacement characters, mojibake, and BOM drift.
- Keep `TryExtractApprovedPlan`, `ValidatePlanAsync`, and `ApplyConfirmedAsync` available to existing callers until later migration stages remove the old protocol.

## Verification Commands

Use the smallest targeted test first, then broaden:

```powershell
npx --yes uloop-cli@2.2.0 run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyPlanWorkspaceTests --save-before-run false
npx --yes uloop-cli@2.2.0 run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyPlanWorkspaceStoreTests --save-before-run false
npx --yes uloop-cli@2.2.0 run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyPlanWorkspaceViewTests --save-before-run false
npx --yes uloop-cli@2.2.0 run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyChatWindowTests --save-before-run false
npx --yes uloop-cli@2.2.0 run-tests --project-path E:\Project\Demo\monsterhunter --filter-type class --filter-value PsdLayoutTool2.Tests.PsdHierarchyChatCleanupExecutionTests --save-before-run false
npx --yes uloop-cli@2.2.0 compile --project-path E:\Project\Demo\monsterhunter --force-recompile false --wait-for-domain-reload true
git diff --check -- Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy Assets/PSDLayoutTool2/Editor/Tests
```

If the active Unity instance blocks EditMode tests because the user's Scene is unsaved, do not save it. Use the existing ignored isolated test project under `.tmp/UnityHierarchyTests`, whose Editor source is linked to this package, and record that separation in the verification report.

## Task 1: Define the Compatibility Workspace Contract

**Files:**

- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyPlanWorkspace.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyPlanWorkspace.cs.meta`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyPlanWorkspaceTests.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyPlanWorkspaceTests.cs.meta`

- [ ] **Step 1: Write failing state-transition tests**

Cover these exact contracts before production code exists:

```csharp
[Test]
public void ReadyWorkspaceExposesItsSingleValidatedBatch()
{
    PsdHierarchyPlanWorkspace workspace = PsdHierarchyPlanWorkspace.CreateReady(
        "snapshot-a",
        "review",
        "{\"version\":2}");

    Assert.That(workspace.state, Is.EqualTo(PsdHierarchyPlanWorkspaceState.Ready));
    Assert.That(workspace.batches, Has.Count.EqualTo(1));
    Assert.That(workspace.issues, Is.Empty);
    Assert.That(workspace.TryGetEnabledPlan(out string planJson), Is.True);
    Assert.That(planJson, Is.EqualTo("{\"version\":2}"));
}

[Test]
public void FailedCompatibilityBatchReturnsNoSafeChangesInsteadOfNull()
{
    PsdHierarchyPlanWorkspace workspace = PsdHierarchyPlanWorkspace.CreateBlockedPlan(
        "snapshot-a",
        "review",
        "raw reply",
        PsdHierarchyPlanIssueCategory.PlanPreparation,
        "variant sources must remain direct siblings",
        new[] { "family_002" },
        new[] { "n000059", "n000089" });

    Assert.That(workspace, Is.Not.Null);
    Assert.That(workspace.state, Is.EqualTo(PsdHierarchyPlanWorkspaceState.NoSafeChanges));
    Assert.That(workspace.TryGetEnabledPlan(out _), Is.False);
    Assert.That(workspace.issues.Single().state, Is.EqualTo(PsdHierarchyPlanIssueState.Open));
}

[Test]
public void KeepOriginalStructureRecordsAnExplicitResolution()
{
    PsdHierarchyPlanWorkspace workspace = CreateBlockedWorkspace();

    Assert.That(workspace.TryKeepOriginalStructure(workspace.issues[0].id), Is.True);

    Assert.That(workspace.issues[0].state, Is.EqualTo(PsdHierarchyPlanIssueState.KeptUnchanged));
    Assert.That(workspace.state, Is.EqualTo(PsdHierarchyPlanWorkspaceState.NoSafeChanges));
}
```

Also test stable issue IDs, duplicate-resolution idempotence, unknown issue IDs, counts, raw diagnostic retention, and `InfrastructureBlocked` creation.

- [ ] **Step 2: Run the new test class and confirm the expected compile failure**

Expected: the workspace types do not exist yet. Do not weaken the tests.

- [ ] **Step 3: Add the minimal serializable model**

Implement only the Stage 1 compatibility surface:

```csharp
internal enum PsdHierarchyPlanWorkspaceState
{
    Ready,
    NeedsAttention,
    NoSafeChanges,
    InfrastructureBlocked,
}

internal enum PsdHierarchyPlanIssueSeverity
{
    Info,
    Warning,
    NeedsDecision,
    Blocked,
}

internal enum PsdHierarchyPlanIssueCategory
{
    PlanExtraction,
    PlanPreparation,
    RunnerPreflight,
    Infrastructure,
}

internal enum PsdHierarchyPlanIssueState
{
    Open,
    KeptUnchanged,
}

[Serializable]
internal sealed class PsdHierarchyPlanIssue
{
    public string id;
    public PsdHierarchyPlanIssueSeverity severity;
    public PsdHierarchyPlanIssueCategory category;
    public string[] candidateIds;
    public string[] affectedNodeIds;
    public string summary;
    public string technicalDetails;
    public string recommendedResolution;
    public PsdHierarchyPlanIssueState state;
}

[Serializable]
internal sealed class PsdHierarchyPlanBatch
{
    public string id;
    public bool enabled;
    public string planJson;
}
```

`PsdHierarchyPlanWorkspace` stores `snapshotFingerprint`, `reviewText`, `rawAssistantReply`, `state`, `batches`, and `issues`. Expose factory methods rather than allowing callers to assemble contradictory states. Copy incoming arrays/lists so callers cannot mutate workspace membership accidentally.

Use deterministic IDs derived from category plus ordinal (`issue_plan_preparation_001`, `batch_compatibility_001`); Stage 1 does not need hashing.

- [ ] **Step 4: Run the workspace tests**

Expected: every factory and state-transition test passes.

- [ ] **Step 5: Commit the contract**

```text
Retain failed hierarchy analysis as a reviewable workspace

Constraint: Stage 1 must preserve the current executable JSON and all-or-nothing apply contract.
Rejected: Treat a validation exception as an absent plan | It discards useful analysis and leaves no recovery surface.
Confidence: high
Scope-risk: narrow
Directive: Construct workspaces through factories so invalid state combinations remain unavailable.
Tested: PsdHierarchyPlanWorkspaceTests
```

## Task 2: Classify Existing Failures Without Rewriting the Executor

**Files:**

- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyChatCleanupExecution.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyChatCleanupExecutionTests.cs`

- [ ] **Step 1: Add failing classification tests for the two reported regressions**

Add tests that pass the exact error shapes through a new pure classifier:

```csharp
[TestCase(
    "Deterministic component-family repair failed: candidateId=family_002; asset=组 16; recommendedMode=variant; sources=node:n000059,node:n000064; reason=suggestedAssetName=组 16 cannot produce a bracketed English semantic item name.",
    PsdHierarchyPlanIssueCategory.PlanPreparation,
    "family_002",
    "n000059")]
[TestCase(
    "variantComponentExtractions[1] variant sources must remain direct siblings after planned moves; template=7日签到拆分/[Root]/组 16/[FlatSibling_flat_sibling_001]; source=7日签到拆分/[Root]/组 16/[FlatSibling_flat_sibling_007]",
    PsdHierarchyPlanIssueCategory.PlanPreparation,
    "",
    "")]
public void ClassifyPlanFailurePreservesActionableContext(
    string error,
    PsdHierarchyPlanIssueCategory expectedCategory,
    string expectedCandidateId,
    string expectedNodeId)
{
    PsdHierarchyPlanIssue issue = PsdHierarchyChatCleanupExecution.ClassifyPlanFailure(
        expectedCategory,
        error);

    Assert.That(issue.category, Is.EqualTo(expectedCategory));
    Assert.That(issue.technicalDetails, Is.EqualTo(error));
    Assert.That(issue.candidateIds, Does.Contain(expectedCandidateId).Or.Empty);
    Assert.That(issue.affectedNodeIds, Does.Contain(expectedNodeId).Or.Empty);
}
```

Also cover no JSON code block, fingerprint mismatch, missing target, runner unavailable, and preflight failure. The classifier must never throw on an unknown error string.

- [ ] **Step 2: Run the cleanup execution tests and confirm failure**

Expected: `ClassifyPlanFailure` does not exist.

- [ ] **Step 3: Add a pure error-to-issue classifier**

Add `ClassifyPlanFailure(category, error)` beside the extraction/preparation boundary. It may use compiled regular expressions only to extract optional `candidateId=...` and `node:...` tokens. The complete original error remains `technicalDetails`; classification must not depend on matching one known English sentence.

Use concise user-facing summaries:

- `PlanExtraction`: `AI 返回内容缺少可读取的计划。`
- `PlanPreparation`: `计划包含相互冲突或无法确定的结构操作。`
- `RunnerPreflight`: `计划未通过执行前校验。`
- `Infrastructure`: `分析或校验环境当前不可用。`

The recommended resolution for recoverable plan issues is `保持原结构，或重新分析此项。`; infrastructure issues recommend retry/refresh instead.

- [ ] **Step 4: Add workspace-producing compatibility helpers**

Do not replace `TryExtractApprovedPlan` yet. Add focused helpers used by the window:

```csharp
internal static PsdHierarchyPlanWorkspace CreateReadyWorkspace(
    PsdHierarchyChatContext context,
    string reviewText,
    string rawAssistantReply,
    string validatedPlanJson)

internal static PsdHierarchyPlanWorkspace CreateIssueWorkspace(
    PsdHierarchyChatContext context,
    string reviewText,
    string rawAssistantReply,
    PsdHierarchyPlanIssueCategory category,
    string error)
```

Both helpers return a non-null workspace even when `context` is null. They must not touch disk, Unity objects, or the Prefab.

- [ ] **Step 5: Re-run extraction, preparation, and classification tests**

Expected: existing plan validation behavior remains unchanged; new helpers retain the raw response and structured issue.

- [ ] **Step 6: Commit the compatibility adapter**

```text
Translate hierarchy plan failures into actionable issue data

Constraint: Current validators remain authoritative during the compatibility stage.
Rejected: Teach AI to repair each new mechanical invariant | Interacting rewrites do not provide convergence.
Confidence: high
Scope-risk: narrow
Directive: Preserve the full original diagnostic even when a concise issue summary is available.
Tested: PsdHierarchyChatCleanupExecutionTests
```

## Task 3: Persist Workspace Diagnostics Under Library

**Files:**

- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyPlanWorkspaceStore.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyPlanWorkspaceStore.cs.meta`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyPlanWorkspaceStoreTests.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyPlanWorkspaceStoreTests.cs.meta`

- [ ] **Step 1: Write failing storage tests against a temporary project root**

```csharp
[Test]
public void SaveWritesUtf8JsonBelowLibraryOnly()
{
    string projectRoot = CreateTemporaryDirectory();
    PsdHierarchyPlanWorkspace workspace = CreateBlockedWorkspace("snapshot-a");

    PsdHierarchyPlanWorkspaceStoreResult result =
        PsdHierarchyPlanWorkspaceStore.TrySave(projectRoot, workspace);

    Assert.That(result.success, Is.True);
    Assert.That(result.path, Does.StartWith(Path.Combine(projectRoot, "Library", "PSDLayoutTool2")));
    Assert.That(File.Exists(result.path), Is.True);
    StringAssert.Contains("variant sources must remain direct siblings", File.ReadAllText(result.path));
}
```

Also test filename sanitization, missing fingerprint fallback, two saves not overwriting each other, and I/O failure returning a result instead of throwing into the Editor window.

- [ ] **Step 2: Confirm the storage tests fail because the store is absent**

- [ ] **Step 3: Implement deterministic directory ownership and safe I/O**

Use:

```text
<projectRoot>/Library/PSDLayoutTool2/PlanWorkspaces/<snapshotFingerprint>/<utcTimestamp>-<shortId>.json
```

Serialize with `JsonConvert.SerializeObject(workspace, Formatting.Indented)` and `new UTF8Encoding(false)`. Return a small result containing `success`, `path`, and `error`; only an explicit `导出诊断` action needs to surface an error to the user. Do not call `AssetDatabase.Refresh`.

- [ ] **Step 4: Re-run storage tests and inspect one emitted JSON fixture**

Expected: round-trippable UTF-8 JSON under `Library`, with the exact technical error retained.

- [ ] **Step 5: Commit diagnostic persistence**

```text
Keep hierarchy workspace diagnostics outside imported assets

Constraint: Failure evidence must survive review without creating project asset churn.
Rejected: Store diagnostics beside the Prefab | It would pollute imports and version-control scope.
Confidence: high
Scope-risk: narrow
Directive: Never refresh AssetDatabase for PlanWorkspaces output.
Tested: PsdHierarchyPlanWorkspaceStoreTests
```

## Task 4: Build the Persistent Issue Panel

**Files:**

- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyPlanWorkspaceView.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyPlanWorkspaceView.cs.meta`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyPlanWorkspaceViewTests.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyPlanWorkspaceViewTests.cs.meta`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyChatWindow.uss`

- [ ] **Step 1: Write failing UI Toolkit tests**

Verify the panel is hidden before a workspace is bound, a ready workspace shows the safe-batch summary with no issue rows, and a blocked workspace exposes the exact handling place requested by the user:

```csharp
[Test]
public void BlockedWorkspaceShowsIssueActionsWithoutAnApplyButton()
{
    var view = new PsdHierarchyPlanWorkspaceView();

    view.Bind(CreateBlockedWorkspace(), new PsdHierarchyPlanWorkspaceActions());

    Assert.That(view.Q<Label>(PsdHierarchyPlanWorkspaceView.TitleName).text, Is.EqualTo("待处理问题"));
    Assert.That(view.Q<Button>(PsdHierarchyPlanWorkspaceView.KeepOriginalButtonName), Is.Not.Null);
    Assert.That(view.Q<Button>(PsdHierarchyPlanWorkspaceView.ShowDetailsButtonName), Is.Not.Null);
    Assert.That(view.Q<Button>(PsdHierarchyPlanWorkspaceView.RetryButtonName), Is.Not.Null);
    Assert.That(view.Q<Button>(PsdHierarchyPlanWorkspaceView.ExportButtonName), Is.Not.Null);
    Assert.That(view.Q<Button>(PsdHierarchyPlanWorkspaceView.ApplySafeButtonName), Is.Null);
}
```

Also test that `定位节点` is disabled when no authoritative node IDs were extracted, details are collapsed by default, keep-unchanged changes the row state, and rebinding does not duplicate callbacks.

- [ ] **Step 2: Confirm the view tests fail because the view is absent**

- [ ] **Step 3: Implement a focused UI Toolkit view**

`PsdHierarchyPlanWorkspaceView` owns rendering only. It receives callbacks through a small action object:

```csharp
internal sealed class PsdHierarchyPlanWorkspaceActions
{
    internal Action<string> keepOriginalStructure;
    internal Action<string[]> locateNodes;
    internal Action retryAnalysis;
    internal Action exportDiagnostics;
}
```

Render:

- summary: safe batch count, quarantined batch count, open issue count, affected node count, state;
- one issue row per issue;
- `保持原结构`, `定位节点`, `重新分析`, `查看详情`, and `导出诊断` controls;
- full technical details in a collapsed selectable `TextField` or `Label` so the user can copy them;
- clear `已保留原结构` state after resolution.

Do not place an `应用安全项` button in this Stage 1 view. Continue using the existing window apply button for the sole ready compatibility batch.

- [ ] **Step 4: Add restrained USS styles**

Use an unframed full-width section with a top border, compact issue rows, severity marker, wrapped text, and stable button sizes. Reuse existing chat colors and spacing. Avoid nesting issue cards inside another card.

- [ ] **Step 5: Run the view tests**

Expected: issue actions exist, state changes rerender correctly, and details never force the apply control to appear.

- [ ] **Step 6: Commit the review surface**

```text
Give hierarchy plan conflicts a persistent handling surface

Constraint: Users need recovery actions without editing JSON or losing the AI review.
Rejected: Render validation errors only as chat messages | Messages provide no stateful resolution workflow.
Confidence: high
Scope-risk: moderate
Directive: Keep partial apply absent until batch independence can be proven.
Tested: PsdHierarchyPlanWorkspaceViewTests
```

## Task 5: Route Every Completed Analysis Into the Workspace

**Files:**

- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyChatWindow.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyChatWindowTests.cs`

- [ ] **Step 1: Add failing window integration tests**

Add test seams that bind a prepared workspace without making a network call. Assert:

- the window builds `PsdHierarchyPlanWorkspaceView` below the existing recovery section;
- a ready workspace sets `pendingPlanJson`, enables the existing confirm/apply button, and shows no open issues;
- a failed workspace leaves apply disabled but preserves review text and renders `待处理问题`;
- `保持原结构` resolves the issue without sending AI or writing the Prefab;
- retry calls the existing failed-plan recovery route;
- exporting diagnostics displays the saved path;
- refreshing/reinitializing with a different fingerprint clears the old workspace and its resolutions.

Suggested test seam:

```csharp
internal void SetWorkspaceForTests(PsdHierarchyPlanWorkspace workspace)
{
    SetPlanWorkspace(workspace);
}
```

- [ ] **Step 2: Run the window tests and confirm the new assertions fail**

- [ ] **Step 3: Add workspace state to the window**

Add:

```csharp
private PsdHierarchyPlanWorkspace currentWorkspace;
private PsdHierarchyPlanWorkspaceView workspaceView;
```

Create the view in `RebuildUi()` immediately after `CreateRecoverySection()` and before the message scroll view. Centralize all UI state changes in:

```csharp
private void SetPlanWorkspace(PsdHierarchyPlanWorkspace workspace)
```

This method binds the view and calls `SetPendingPlan` only when `workspace.TryGetEnabledPlan(...)` succeeds.

- [ ] **Step 4: Replace the terminal failure branch in `SendMessage()`**

On successful extraction and `ValidatePlanAsync`, create a ready workspace, bind it, and preserve the existing reviewable reply and explicit confirmation gate.

Track `lastFailureCategory` beside `lastPlanError`. Before calling `TryExtractApprovedPlan`, use `ExtractJsonCodeBlock` only to distinguish a missing plan block (`PlanExtraction`) from a rejected present plan (`PlanPreparation`); do not parse or validate it a second time in the window. A failed `ValidatePlanAsync` is `RunnerPreflight`. A failed send or caught environment exception is `Infrastructure`.

After the bounded automatic repair attempt fails, replace:

```csharp
SetPendingPlan(string.Empty);
ResetFailedPlanConversation();
// terminal failure messages
```

with workspace construction and binding:

```csharp
PsdHierarchyPlanWorkspace workspace =
    PsdHierarchyChatCleanupExecution.CreateIssueWorkspace(
        context,
        initialReviewText,
        lastAssistantReply,
        lastFailureCategory,
        lastPlanError);
SetPlanWorkspace(workspace);
PsdHierarchyPlanWorkspaceStore.TrySave(context.projectRoot, workspace);
```

Keep the AI review visible. Do not call `ResetFailedPlanConversation()` merely because plan preparation failed. Append one concise message: `分析已完成，但当前没有可安全执行的变更。请在“待处理问题”中处理。` Set the status to `存在待处理问题`, not `计划生成失败`.

- [ ] **Step 5: Route transport and unexpected exceptions into infrastructure workspaces**

When the AI request, snapshot refresh, or preflight infrastructure fails after an analysis begins, bind an `InfrastructureBlocked` workspace before returning. The panel offers retry and export. Do not describe infrastructure failure as a semantic issue.

- [ ] **Step 6: Wire issue actions**

- `保持原结构`: call `TryKeepOriginalStructure`, rebind, save diagnostics, and append a concise state message.
- `定位节点`: resolve each authoritative ID through `context.TryGetNodePath`; open/ping the target Prefab through the existing asset-ping path and show the resolved paths. Stage 1 must not add a recursive hierarchy search.
- `重新分析`: call the existing `PrepareFailedPlanRecovery`/regeneration path.
- `查看详情`: handled entirely by the view.
- `导出诊断`: call the store and reveal the output path in Explorer or append a copyable path message.

- [ ] **Step 7: Ensure apply failures retain the ready workspace**

Today `ApplyPendingPlan` clears `pendingPlanJson` before execution. If apply fails, retain the compatibility batch in the workspace but mark it disabled, attach a `RunnerPreflight` or `Infrastructure` issue, and keep diagnostics/retry available. The workspace is `NoSafeChanges` or `InfrastructureBlocked`, so the existing apply button stays disabled. Do not auto-reapply.

- [ ] **Step 8: Run the window and execution regression tests**

Expected: prior success behavior is unchanged; both reported failures produce a visible issue workspace, not a terminal dead end.

- [ ] **Step 9: Commit the window integration**

```text
Keep hierarchy analysis usable when plan validation fails

Constraint: Existing apply semantics and explicit confirmation remain unchanged in Stage 1.
Rejected: Clear the conversation and pending result after the final repair attempt | It removes the only useful recovery context.
Confidence: high
Scope-risk: moderate
Directive: Every completed analysis path must bind either Ready, NoSafeChanges, or InfrastructureBlocked.
Tested: PsdHierarchyChatWindowTests; PsdHierarchyChatCleanupExecutionTests
```

## Task 6: Add the Two Historical Failures as Permanent Regression Fixtures

**Files:**

- Create: `Assets/PSDLayoutTool2/Editor/Tests/Fixtures/HierarchyPlanWorkspaces.meta`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/Fixtures/HierarchyPlanWorkspaces/family-002-invalid-semantic-name.json`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/Fixtures/HierarchyPlanWorkspaces/family-002-invalid-semantic-name.json.meta`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/Fixtures/HierarchyPlanWorkspaces/variant-conflicting-move.json`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/Fixtures/HierarchyPlanWorkspaces/variant-conflicting-move.json.meta`
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyChatCleanupExecutionTests.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyChatWindowTests.cs`

- [ ] **Step 1: Store minimal sanitized inputs and exact expected classification**

Each fixture records only the snapshot fingerprint, raw error, expected category, extracted candidate/node IDs, and expected workspace state. Do not copy machine-local absolute paths or private AI conversation content.

- [ ] **Step 2: Add parameterized regression tests**

For both fixtures, assert:

1. classification never throws;
2. workspace is non-null;
3. state is `NoSafeChanges`;
4. full technical details are retained;
5. `保持原结构` is available;
6. the apply button remains disabled;
7. retry and export remain available.

- [ ] **Step 3: Run both affected test classes twice**

Expected: deterministic issue IDs and byte-stable serialized workspace content, excluding export timestamp/path.

- [ ] **Step 4: Commit the regression fixtures**

```text
Make hierarchy repair dead ends permanent workspace regressions

Constraint: The reported naming and sibling-topology failures must never collapse the UI again.
Rejected: Keep errors only in conversation history | Future validation changes would silently reintroduce the dead end.
Confidence: high
Scope-risk: narrow
Directive: Add each newly observed terminal plan failure as a sanitized workspace fixture.
Tested: PsdHierarchyChatCleanupExecutionTests; PsdHierarchyChatWindowTests
```

## Task 7: Verify Stage 1 End to End

**Files:**

- Test: all files changed in Tasks 1-6
- Do not modify runtime code unless verification reveals a Stage 1 regression

- [ ] **Step 1: Run all new targeted test classes**

Run the five commands listed in `Verification Commands`. Record test counts and failures.

- [ ] **Step 2: Compile the Unity project**

Expected: zero new compiler errors or warnings attributable to this stage.

- [ ] **Step 3: Perform a manual Editor smoke test against the reported Prefab**

Trigger the same analysis that produced `family_002` / `组 16`. Verify:

- analysis completes without modifying the Prefab;
- the review remains visible;
- `待处理问题` shows the exact naming or sibling-topology issue;
- `查看详情` reveals the full original diagnostic;
- `保持原结构` records an explicit no-change resolution;
- `重新分析` remains available;
- `导出诊断` writes under `Library/PSDLayoutTool2/PlanWorkspaces`;
- confirm/apply remains disabled because Stage 1 has no safe batch;
- a normal validated plan still enables the existing explicit apply flow.

- [ ] **Step 4: Inspect file and encoding hygiene**

```powershell
rg -n "\\\\u[0-9a-fA-F]{4}|\?\?\?|�" Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyPlanWorkspace*.cs Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyPlanWorkspace*.cs
git diff --check -- Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy Assets/PSDLayoutTool2/Editor/Tests
```

Confirm all new Unity source files have `.meta` files and namespace declarations.

- [ ] **Step 5: Review the final diff against Stage 1 boundaries**

Reject any implementation that changes the AI prompt to intent-only, introduces node ownership/virtual hierarchy types, offers partial apply, or removes existing validators. Those belong to Stages 2-5.

- [ ] **Step 6: Commit verification-only adjustments if required**

```text
Verify hierarchy workspace recovery without changing apply semantics

Constraint: Stage 1 ends at structured recovery and diagnostic retention.
Rejected: Enable partial apply before ownership and dependency proofs | Quarantined nodes could still be mutated indirectly.
Confidence: high
Scope-risk: narrow
Directive: Begin Stage 2 from the approved intent-compiler design, not by extending error-string repairs.
Tested: targeted EditMode suites; Unity compile; manual reported-Prefab smoke test
```

## Stage 1 Acceptance Criteria

- Every completed analysis binds a non-null workspace in `Ready`, `NoSafeChanges`, or `InfrastructureBlocked`.
- The two reported failures appear in `待处理问题`; neither clears the AI review nor ends with only `计划生成失败`.
- Every plan issue offers `保持原结构` and `查看详情`; retry and diagnostic export remain available.
- Failed compatibility plans cannot enable apply.
- Existing validated plans still use the same explicit confirm/apply path and all-or-nothing transaction.
- No Stage 1 analysis, resolution, or diagnostic action modifies a Prefab.
- The full diagnostic is written under `Library/PSDLayoutTool2/PlanWorkspaces` and remains copyable from the window.
- Existing extraction/preparation/apply tests remain green.
- No intent-only prompt, ownership graph, virtual hierarchy, or partial apply code is introduced early.
