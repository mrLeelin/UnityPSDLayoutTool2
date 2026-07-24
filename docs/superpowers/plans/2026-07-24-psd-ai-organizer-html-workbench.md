# PSD AI Organizer HTML Workbench Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Clicking “AI 整理” in the PSD Inspector opens a local HTML workbench that displays the complete PSD, lets the user inspect and adjust AI groups visually, applies approved naming and hierarchy changes, and creates approved common Prefabs in a separate confirmation phase.

**Architecture:** Unity remains the only authority for PSD parsing, hierarchy planning, validation, asset mutation, Prefab creation, Undo, and recovery. A loopback-only `TcpListener` serves static HTML/CSS/JavaScript and small JSON APIs from an authenticated per-PSD session. The browser renders the full PSD composite plus Unity-produced node/group geometry, while all Unity API work is marshalled back to the Editor main thread.

**Tech Stack:** Unity 6 Editor C#, NUnit EditMode tests, `System.Net.Sockets.TcpListener`, Newtonsoft.Json, Unity `Texture2D.EncodeToPNG`, plain HTML/CSS/JavaScript, existing `PsdHierarchyOrganizerPreviewModel`, existing hierarchy apply pipeline, existing Prefab candidate analyzer.

---

## Execution Preconditions

- Work from `E:\Project\Demo\monsterhunter`.
- Preserve every unrelated modified or untracked file in the current checkout.
- The current checkout contains hierarchy-organizer work that is not all committed. Before implementation, identify which of those files are required by this feature and preserve their provenance.
- Create a dedicated worktree only after required current changes are safely committed or otherwise preserved. Do not base implementation on a clean worktree that silently omits the current Prefab candidate and organizer changes.
- Keep Editor-only code below `Assets/PSDLayoutTool2/Editor/`.
- Use namespace `PsdLayoutTool2.Editor` for every new C# file unless the neighboring source proves a more specific existing namespace is required.
- Never write generated browser session data below `Assets/`. Use `Library/PSDLayoutTool2/HierarchyWebSessions/`.
- Do not add Node, npm, React, WebView, a remote CDN, or a new package dependency.
- Run each task’s narrow test before its commit. Run the complete filtered suite only after all tasks are integrated.

## Public Contract

The implementation must expose these authenticated loopback routes:

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/session/{sessionId}` | Load the current session summary and operation state |
| `GET` | `/session/{sessionId}/composite.png` | Load the full PSD composite preview |
| `GET` | `/session/{sessionId}/snapshot` | Load nodes, bounds, groups, locks, warnings, and Prefab candidates |
| `POST` | `/session/{sessionId}/analyze` | Start or restart full AI organization |
| `POST` | `/session/{sessionId}/refine` | Reorganize selected groups or stable IDs with an optional instruction |
| `POST` | `/session/{sessionId}/accept` | Accept or unlock selected groups without mutating assets |
| `POST` | `/session/{sessionId}/apply` | Apply the validated naming and hierarchy plan |
| `GET` | `/session/{sessionId}/prefab-candidates` | Load common Prefab candidates after hierarchy apply |
| `POST` | `/session/{sessionId}/create-prefabs` | Create only explicitly selected Prefab candidates |
| `GET` | `/session/{sessionId}/status` | Poll the current asynchronous operation |

Every request except static assets must carry the session token in `X-PSD-Session-Token`. The server must bind to `127.0.0.1` on a random available port, reject invalid `Host` headers, reject missing or incorrect tokens, and never expose a filesystem path supplied by the request.

## Task 1: Add Serializable Web Contracts

**Files:**

- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebContracts.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyWebContractsTests.cs`

- [ ] Write a failing round-trip test for the snapshot and refine request.

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using PsdLayoutTool2.Editor;

namespace PsdLayoutTool2.Tests
{
    public sealed class PsdHierarchyWebContractsTests
    {
        [Test]
        public void RefineRequest_RoundTripsStableIdsAndInstruction()
        {
            var source = new PsdHierarchyWebRefineRequest
            {
                stableIds = new List<string> { "layer:17", "layer:18" },
                instruction = "这两个任务属于同一个列表项"
            };

            var json = JsonConvert.SerializeObject(source);
            var result = JsonConvert.DeserializeObject<PsdHierarchyWebRefineRequest>(json);

            CollectionAssert.AreEqual(source.stableIds, result.stableIds);
            Assert.AreEqual(source.instruction, result.instruction);
        }

        [Test]
        public void OperationState_DefaultsToIdle()
        {
            var state = new PsdHierarchyWebOperationState();

            Assert.AreEqual(PsdHierarchyWebOperationKind.None, state.kind);
            Assert.AreEqual(PsdHierarchyWebOperationStatus.Idle, state.status);
            Assert.IsEmpty(state.message);
        }
    }
}
```

- [ ] Run the narrow test and confirm it fails because the contract types do not exist.

```powershell
& 'E:\UnityEngine\Engine\Installs_location\6000.3.6f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'E:\Project\Demo\monsterhunter' `
  -runTests -testPlatform EditMode `
  -testFilter 'PsdLayoutTool2.Tests.PsdHierarchyWebContractsTests' `
  -testResults 'E:\Project\Demo\monsterhunter\Temp\PsdHierarchyWebContractsTests.xml' `
  -logFile -
```

Expected: non-zero exit or failed compilation naming the missing web contract types.

- [ ] Implement plain JSON DTOs with public fields and deterministic defaults:

  - `PsdHierarchyWebSessionDto`
  - `PsdHierarchyWebSnapshotDto`
  - `PsdHierarchyWebNodeDto`
  - `PsdHierarchyWebGroupDto`
  - `PsdHierarchyWebBoundsDto`
  - `PsdHierarchyWebWarningDto`
  - `PsdHierarchyWebPrefabCandidateDto`
  - `PsdHierarchyWebRefineRequest`
  - `PsdHierarchyWebAcceptRequest`
  - `PsdHierarchyWebApplyRequest`
  - `PsdHierarchyWebCreatePrefabsRequest`
  - `PsdHierarchyWebOperationState`
  - `PsdHierarchyWebOperationKind`
  - `PsdHierarchyWebOperationStatus`

```csharp
namespace PsdLayoutTool2.Editor
{
    internal enum PsdHierarchyWebOperationStatus
    {
        Idle,
        Running,
        Succeeded,
        Failed
    }

    internal sealed class PsdHierarchyWebRefineRequest
    {
        public List<string> stableIds = new List<string>();
        public string instruction = string.Empty;
    }

    internal sealed class PsdHierarchyWebOperationState
    {
        public string operationId = string.Empty;
        public PsdHierarchyWebOperationKind kind = PsdHierarchyWebOperationKind.None;
        public PsdHierarchyWebOperationStatus status = PsdHierarchyWebOperationStatus.Idle;
        public string message = string.Empty;
    }
}
```

- [ ] Keep JSON field names stable and lower camel case. Do not serialize Unity objects, delegates, `PsdFile`, or the preview model.
- [ ] Re-run the narrow test and confirm exit code `0`.
- [ ] Commit only the two new files.

```powershell
git add -- `
  'Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebContracts.cs' `
  'Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyWebContractsTests.cs'
git commit -m "Define a stable browser contract for PSD organization" `
  -m "Constraint: Browser DTOs must not expose Unity object references or writable filesystem paths.
Rejected: Serializing the existing preview model directly | it couples the UI protocol to mutable Editor internals.
Confidence: high
Scope-risk: narrow
Directive: Keep web JSON fields backward compatible once the Inspector entry ships.
Tested: PsdHierarchyWebContractsTests
Not-tested: Browser rendering"
```

## Task 2: Build the Real PSD Snapshot and Composite Preview

**Files:**

- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebSnapshotBuilder.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyCompositePreviewWriter.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyWebSnapshotTests.cs`
- Test fixture: `Assets/PSDLayoutTool2/TestData/7日任务拆分.psd`

- [ ] Write a failing fixture test that loads the real PSD, builds the organizer input, and verifies full-canvas geometry.

```csharp
[Test]
public void Build_RealSevenDayTaskPsd_ContainsEveryImportedNodeInsideCanvas()
{
    const string psdPath = "Assets/PSDLayoutTool2/TestData/7日任务拆分.psd";
    var input = PsdHierarchyOrganizerEntry.BuildInputForTests(psdPath);

    var snapshot = PsdHierarchyWebSnapshotBuilder.Build(input.previewModel);

    Assert.AreEqual(1080, snapshot.canvas.width);
    Assert.AreEqual(2340, snapshot.canvas.height);
    Assert.AreEqual(113, snapshot.nodes.Count);
    Assert.That(snapshot.nodes, Has.All.Matches<PsdHierarchyWebNodeDto>(
        node => node.bounds.x >= 0 &&
                node.bounds.y >= 0 &&
                node.bounds.x + node.bounds.width <= snapshot.canvas.width &&
                node.bounds.y + node.bounds.height <= snapshot.canvas.height));
}
```

- [ ] Add a failing preview test that verifies the PNG signature and exact PSD dimensions after decoding.
- [ ] Run `PsdHierarchyWebSnapshotTests`; expect failure because the builder and writer do not exist.
- [ ] Implement `PsdHierarchyWebSnapshotBuilder.Build` using the existing organizer request and proposed plan:

  - include every imported node, including locked or ignored nodes;
  - retain stable IDs as the only selection identity;
  - map PSD coordinates to a single top-left canvas coordinate system;
  - include current name, proposed name, source group, proposed group, accepted/locked state, and warning state;
  - include Prefab candidate IDs but do not create Prefabs.

- [ ] Implement `PsdHierarchyCompositePreviewWriter.Write`:

  - load `PhotoshopFile.PsdFile`;
  - use the already-decoded merged `ImageData`;
  - convert RGB plus optional alpha into RGBA;
  - correct the PSD row direction once;
  - encode with `Texture2D.EncodeToPNG`;
  - destroy the temporary `Texture2D` in `finally`;
  - write only to the supplied session directory under `Library`.

```csharp
internal static string Write(string psdAssetPath, string sessionDirectory)
{
    var absolutePsdPath = Path.GetFullPath(psdAssetPath);
    var outputPath = Path.Combine(sessionDirectory, "composite.png");
    var psd = new PsdFile(absolutePsdPath, Encoding.Default);
    Texture2D texture = null;
    try
    {
        texture = BuildCompositeTexture(psd);
        File.WriteAllBytes(outputPath, texture.EncodeToPNG());
        return outputPath;
    }
    finally
    {
        if (texture != null)
            UnityEngine.Object.DestroyImmediate(texture);
    }
}
```

- [ ] Re-run `PsdHierarchyWebSnapshotTests`; expect exit code `0` and a valid 1080 × 2340 PNG.
- [ ] Reopen the two C# files and confirm Chinese strings are intact, no `\uXXXX` literals were introduced, and the namespace matches neighboring Editor code.
- [ ] Commit only this task’s files.

## Task 3: Add Recoverable Per-PSD Sessions

**Files:**

- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebSession.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebSessionRegistry.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyWebSessionTests.cs`

- [ ] Write failing tests for:

  - one writable session per PSD GUID;
  - reopening a PSD returns the existing session;
  - two different PSDs get different tokens and directories;
  - `Dispose` cancels the active operation;
  - stale session directories older than seven days are removed only under the exact session root.

```csharp
[Test]
public void GetOrCreate_SamePsdGuid_ReusesWritableSession()
{
    using var registry = CreateRegistry();

    var first = registry.GetOrCreate("guid-a", "Assets/A.psd", CreatePreviewModel());
    var second = registry.GetOrCreate("guid-a", "Assets/A.psd", CreatePreviewModel());

    Assert.AreSame(first, second);
    Assert.AreEqual(first.token, second.token);
}
```

- [ ] Run `PsdHierarchyWebSessionTests`; expect missing-type failures.
- [ ] Implement `PsdHierarchyWebSession` with:

  - immutable `sessionId`, `token`, `sourcePsdGuid`, `sourcePsdPath`, `directory`;
  - current `PsdHierarchyOrganizerPreviewModel`;
  - current `PsdHierarchyWebOperationState`;
  - `CancellationTokenSource` owned by the current operation;
  - thread-safe start, complete, fail, cancel, and snapshot replacement methods.

- [ ] Implement `PsdHierarchyWebSessionRegistry` with an injected clock and root directory so cleanup can be tested without touching the real Library tree.
- [ ] Generate session IDs and tokens with `RandomNumberGenerator.Fill`; encode them as lowercase hexadecimal.
- [ ] Canonicalize the session root and verify every cleanup target remains below it before deleting recursively.
- [ ] Re-run `PsdHierarchyWebSessionTests`; expect exit code `0`.
- [ ] Commit only this task’s files.

## Task 4: Add the Authenticated Loopback Server

**Files:**

- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebServer.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebRouter.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebResponse.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyWebServerTests.cs`

- [ ] Write socket-level failing tests for:

  - binding exclusively to `IPAddress.Loopback`;
  - selecting an available port;
  - returning `401` when the token is missing or wrong;
  - returning `400` for a non-loopback `Host`;
  - returning `404` for an unknown session;
  - returning JSON with `application/json; charset=utf-8`;
  - returning PNG with `image/png`;
  - rejecting request bodies larger than 1 MiB.

```csharp
[Test]
public async Task Snapshot_WithoutSessionToken_ReturnsUnauthorized()
{
    using var fixture = await PsdHierarchyWebServerFixture.StartAsync();

    var response = await fixture.SendRawAsync(
        $"GET /session/{fixture.SessionId}/snapshot HTTP/1.1\r\n" +
        $"Host: 127.0.0.1:{fixture.Port}\r\n" +
        "Connection: close\r\n\r\n");

    StringAssert.StartsWith("HTTP/1.1 401 Unauthorized", response);
}
```

- [ ] Run `PsdHierarchyWebServerTests`; expect missing-type failures.
- [ ] Implement a small HTTP/1.1 parser with strict limits:

  - request line at most 4 KiB;
  - total headers at most 32 KiB;
  - body at most 1 MiB;
  - only `GET` and `POST`;
  - close the connection after each response;
  - never support directory traversal or arbitrary file paths.

- [ ] Run the accept loop on a background task, but route all Unity work through the main-thread bridge added later.
- [ ] Dispose the listener on assembly reload and Editor shutdown.
- [ ] Re-run `PsdHierarchyWebServerTests`; expect exit code `0`.
- [ ] Commit only this task’s files.

## Task 5: Serve the Formal HTML Workbench

**Files:**

- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/Static/index.html`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/Static/organizer.css`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/Static/organizer.js`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebStaticAssets.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyWebStaticAssetsTests.cs`

- [ ] Write failing tests that resolve the three static files through `PackageInfo.FindForAssembly`, verify their content types, and assert that the HTML contains the required landmarks:

```csharp
[TestCase("/", "text/html; charset=utf-8", "data-role=\"psd-canvas\"")]
[TestCase("/organizer.css", "text/css; charset=utf-8", ".group-overlay")]
[TestCase("/organizer.js", "text/javascript; charset=utf-8", "requestAnimationFrame")]
public void Resolve_ReturnsBundledWorkbenchAsset(
    string route,
    string expectedContentType,
    string expectedText)
{
    var asset = PsdHierarchyWebStaticAssets.Resolve(route);

    Assert.AreEqual(expectedContentType, asset.contentType);
    StringAssert.Contains(expectedText, Encoding.UTF8.GetString(asset.bytes));
}
```

- [ ] Run `PsdHierarchyWebStaticAssetsTests`; expect missing-file or missing-type failures.
- [ ] Implement the HTML as one application shell with:

  - header: PSD filename, connection state, AI state, “重新分析”;
  - left tool rail: select, hand/pan, fit, actual size;
  - center viewport: full PSD composite, SVG group overlays, marquee selection, minimap;
  - right inspector: selected scope, current hierarchy, proposed hierarchy, naming changes, warnings, instruction field;
  - footer actions: “重新整理选中区域”, “接受选中分组”, “应用命名与层级”;
  - post-apply Prefab phase: candidate list, instance count, representative preview, explicit checkboxes, “创建选中的 Prefab”.

- [ ] Implement interactions in plain JavaScript:

  - wheel zoom centered on the pointer;
  - Space or middle mouse drag to pan;
  - click one group to select;
  - Shift-click to add/remove groups;
  - empty-canvas drag to create a custom rectangle;
  - Escape clears selection;
  - F fits selection or the full canvas;
  - minimap viewport drag;
  - polling `/status` every 500 ms only while an operation is running;
  - disable mutation buttons while disconnected or busy;
  - preserve accepted locks after refreshing the snapshot.

- [ ] Render all user-originated values with `textContent`, never `innerHTML`.
- [ ] Use SVG rectangles and labels for overlays so all 113 real nodes remain visible at any zoom without generating 113 bitmap previews.
- [ ] Use CSS variables for Unity-like dark colors, but prioritize large readable labels and clear selection outlines over imitating every Unity control.
- [ ] Re-run `PsdHierarchyWebStaticAssetsTests`; expect exit code `0`.
- [ ] Commit the static workbench and resolver together.

## Task 6: Add Main-Thread Dispatch and Web Controller

**Files:**

- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebMainThread.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebController.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyWebControllerTests.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyOrganizerWindow.cs`

- [ ] Write failing controller tests using a fake dispatcher and fake AI runner:

  - analyze starts once and reports `Running`;
  - a second mutation request returns conflict while busy;
  - refine sends exactly the selected stable IDs and instruction;
  - accepted unselected groups remain locked;
  - failure changes status to `Failed` without losing the last good snapshot.

```csharp
[Test]
public async Task Refine_UsesSelectedStableIdsAndPreservesOtherAcceptedGroups()
{
    var fixture = CreateControllerFixture();
    fixture.Model.AcceptGroup("daily-list");

    await fixture.Controller.RefineAsync(
        fixture.Session,
        new PsdHierarchyWebRefineRequest
        {
            stableIds = new List<string> { "layer:41", "layer:42" },
            instruction = "合并为同一个奖励卡片"
        });

    CollectionAssert.AreEquivalent(
        new[] { "layer:41", "layer:42" },
        fixture.Runner.LastRequest.focusStableIds);
    CollectionAssert.Contains(fixture.Model.acceptedGroupKeys, "daily-list");
}
```

- [ ] Run `PsdHierarchyWebControllerTests`; expect failure because selection-based refine does not exist.
- [ ] Extract UI-independent functionality from `PsdHierarchyOrganizerPreviewModel` without changing existing window behavior:

```csharp
public Task RefineSelectionAsync(
    IReadOnlyCollection<string> stableIds,
    string instruction,
    CancellationToken cancellationToken)
{
    return RunFocusedPlanAsync(
        stableIds,
        instruction ?? string.Empty,
        cancellationToken);
}

public Task RefineGroupAsync(string groupKey, CancellationToken cancellationToken)
{
    return RefineSelectionAsync(
        ResolveStableIdsForGroup(groupKey),
        string.Empty,
        cancellationToken);
}
```

- [ ] If the existing AI request has no instruction field, add one optional string field at the existing request boundary and verify older callers still produce identical requests.
- [ ] Implement `PsdHierarchyWebMainThread.InvokeAsync` using `EditorApplication.delayCall` and `TaskCompletionSource`; execute immediately when already on the captured Editor main thread.
- [ ] Implement controller methods for session, snapshot, analyze, refine, accept, and status. The controller updates DTO snapshots only after a successful model operation.
- [ ] Re-run both `PsdHierarchyWebControllerTests` and the existing organizer preview model tests; expect exit code `0`.
- [ ] Reopen the modified organizer file and verify no existing Chinese labels are corrupted.
- [ ] Commit only the controller, dispatcher, tests, and focused preview-model change.

## Task 7: Apply Naming and Hierarchy Through the Existing Validated Pipeline

**Files:**

- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebController.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebContracts.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyWebApplyTests.cs`
- Modify only if required to expose the existing handler safely: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyOrganizerEntry.cs`

- [ ] Write failing tests proving:

  - apply is rejected when validation errors exist;
  - apply is rejected until every non-ignored group is accepted;
  - the existing apply delegate receives exactly the result of `TryCreateValidatedApplyPlan`;
  - apply runs on the Editor main thread;
  - success records the resulting Prefab path and transitions to the Prefab review phase;
  - failure leaves the session open and returns a readable error.

- [ ] Run `PsdHierarchyWebApplyTests`; expect failure because the endpoint is not connected.
- [ ] Pass the existing `Action<PsdHierarchyPlan>` apply handler into the web session or controller. Do not duplicate `PsdImporter.GeneratePrefabWithHierarchyPlan`.
- [ ] Use this order:

  1. reject if another operation is active;
  2. call `TryCreateValidatedApplyPlan`;
  3. record an operation ID and `Running`;
  4. marshal the apply delegate to the Editor main thread;
  5. refresh the snapshot from the resulting asset state;
  6. record `Succeeded` or `Failed`.

- [ ] Ensure the browser never sends a serialized authoritative plan. It sends confirmation only; Unity constructs and validates the plan it applies.
- [ ] Re-run `PsdHierarchyWebApplyTests` and existing apply verifier tests; expect exit code `0`.
- [ ] Commit only this task’s files.

## Task 8: Add the Separate Common-Prefab Confirmation Phase

**Files:**

- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebController.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebSnapshotBuilder.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyWebPrefabTests.cs`
- Reuse: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyPrefabCandidateAnalyzer.cs`
- Reuse: existing Prefab creation and incremental merge services discovered during implementation

- [ ] Write failing tests with candidates modeled after the real five daily task items:

  - identical visual structures produce one candidate with instance count five;
  - no Prefab is created during analyze or hierarchy apply;
  - only checked candidate IDs are sent to the creation service;
  - unknown or stale candidate IDs are rejected;
  - a failed candidate does not silently mark the whole batch successful.

- [ ] Run `PsdHierarchyWebPrefabTests`; expect failure because candidate creation is not routed.
- [ ] Build candidate DTOs from `PsdHierarchyPrefabCandidateAnalyzer.Analyze`, including:

  - stable candidate ID;
  - proposed Prefab name;
  - representative node stable ID;
  - instance stable IDs;
  - instance count;
  - shared width and height;
  - state differences that must remain instance-controlled.

- [ ] On create, recompute candidates from the current hierarchy and match requested IDs against that fresh set before invoking the existing creation service.
- [ ] Run Prefab creation on the Editor main thread and keep Undo/asset refresh behavior in the existing service.
- [ ] Return per-candidate success or error results so the HTML can show partial failure clearly.
- [ ] Re-run `PsdHierarchyWebPrefabTests` and existing candidate analyzer tests; expect exit code `0`.
- [ ] Commit only this task’s files.

## Task 9: Switch the PSD Inspector Entry to the Browser

**Files:**

- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebEntry.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/PsdInspector.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyOrganizerEntry.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyWebEntryTests.cs`

- [ ] Write failing entry tests proving:

  - clicking the entry builds the same real organizer input as the old window;
  - a loopback server starts lazily;
  - the URL contains a session ID but not the secret token in its query string;
  - the token is delivered through the first same-origin bootstrap exchange and then stored only in page memory;
  - reopening the same PSD focuses/reopens its existing session;
  - unavailable PSDs retain the current explanatory Editor error.

- [ ] Run `PsdHierarchyWebEntryTests`; expect missing-type failures.
- [ ] Implement `PsdHierarchyWebEntry.Open(sourcePsdPath)`:

  1. call the existing availability and input-building path;
  2. start or reuse the loopback server;
  3. create or reuse the per-PSD session;
  4. generate the composite preview if absent or stale;
  5. open the system browser with `Application.OpenURL`;
  6. display an Editor notification containing the local URL and recovery action.

- [ ] Change only the PSD Inspector button target from the old window entry to `PsdHierarchyWebEntry.Open`.
- [ ] Keep the old EditorWindow reachable through a diagnostic menu item named `PSD Layout Tool 2/Diagnostics/AI Organizer Window`; do not leave two primary “AI 整理” actions.
- [ ] Add bootstrap token handling without placing the token in the URL, browser history, HTML file, or Unity Console. A valid design is:

  - open `/open/{sessionId}/{oneTimeNonce}`;
  - server validates and consumes the nonce once;
  - response sets an `HttpOnly; SameSite=Strict` loopback cookie;
  - subsequent APIs authenticate the cookie;
  - `X-PSD-Session-Token` remains available for test and non-browser clients.

- [ ] Re-run `PsdHierarchyWebEntryTests` and existing `PsdInspector`/organizer entry tests; expect exit code `0`.
- [ ] Commit only this task’s files.

## Task 10: Add Reload Recovery and Lifecycle Cleanup

**Files:**

- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebServer.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebSessionRegistry.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebEntry.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyWebRecoveryTests.cs`

- [ ] Write failing tests for:

  - assembly reload stops the listener cleanly;
  - a browser polling an expired server receives a deterministic disconnected response;
  - reopening from Unity recreates the server and session snapshot;
  - a session manifest contains only safe recovery metadata, never the token;
  - composite PNG and manifest are removed by explicit session close or stale cleanup;
  - cleanup cannot escape the configured Library root.

- [ ] Run `PsdHierarchyWebRecoveryTests`; expect failures.
- [ ] Register lifecycle hooks with `[InitializeOnLoad]`, `AssemblyReloadEvents.beforeAssemblyReload`, and `EditorApplication.quitting`.
- [ ] Persist a minimal session manifest containing PSD GUID, asset path, last access time, and session phase. Do not persist accepted secret tokens or cancellation objects.
- [ ] Add a “重新打开 AI 整理页面” recovery action in Unity that creates a fresh token and URL from the current PSD state.
- [ ] Make the HTML disconnected state explicit:

  - retain the last rendered snapshot;
  - show “Unity 连接已断开”;
  - disable all mutations;
  - tell the user to return to Unity and reopen the page.

- [ ] Re-run `PsdHierarchyWebRecoveryTests`; expect exit code `0`.
- [ ] Commit only this task’s files.

## Task 11: Integrate the Real Seven-Day Task PSD

**Files:**

- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyWebRealFixtureTests.cs`
- Modify: `Assets/PSDLayoutTool2/README.md`
- Update if implementation behavior changed: `docs/superpowers/specs/2026-07-24-psd-ai-organizer-html-workbench-design.md`

- [ ] Add an EditMode integration test against `Assets/PSDLayoutTool2/TestData/7日任务拆分.psd` that verifies:

  - canvas is 1080 × 2340;
  - all 113 imported objects appear in the snapshot;
  - the daily task region exposes five same-sized items;
  - the full image route returns a decodable PNG;
  - selecting two task items produces exactly those stable IDs in a refine request;
  - accepted groups remain locked across a second refine;
  - hierarchy apply and Prefab creation remain separate operations.

- [ ] Run the real fixture test:

```powershell
& 'E:\UnityEngine\Engine\Installs_location\6000.3.6f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'E:\Project\Demo\monsterhunter' `
  -runTests -testPlatform EditMode `
  -testFilter 'PsdLayoutTool2.Tests.PsdHierarchyWebRealFixtureTests' `
  -testResults 'E:\Project\Demo\monsterhunter\Temp\PsdHierarchyWebRealFixtureTests.xml' `
  -logFile -
```

Expected: exit code `0`; the XML reports all real-fixture tests passed.

- [ ] Document:

  - how “AI 整理” opens the system browser;
  - how to pan, zoom, multi-select, and rectangle-select;
  - the difference between “重新整理选中区域” and full analysis;
  - that “应用命名与层级” mutates the Prefab;
  - that common Prefab creation is a later explicit confirmation;
  - how to recover after a Unity domain reload;
  - where temporary session files are stored and when they are removed.

- [ ] Reopen the README and spec to verify Chinese text encoding and terminology:

  - use “重新整理选中区域”;
  - do not use “二次 AI 修复”;
  - distinguish “候选” from “已创建”;
  - distinguish “预览” from “已应用”.

- [ ] Commit the fixture and documentation.

## Task 12: Final Verification

**Files:**

- Verify all files changed by Tasks 1–11.

- [ ] Run the complete web-workbench EditMode suite:

```powershell
& 'E:\UnityEngine\Engine\Installs_location\6000.3.6f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'E:\Project\Demo\monsterhunter' `
  -runTests -testPlatform EditMode `
  -testFilter 'PsdLayoutTool2.Tests.PsdHierarchyWeb' `
  -testResults 'E:\Project\Demo\monsterhunter\Temp\PsdHierarchyWebTests.xml' `
  -logFile -
```

Expected: exit code `0`; every filtered EditMode test passes.

- [ ] Run the existing hierarchy organizer, plan validator, apply verifier, and Prefab candidate tests to detect regressions.
- [ ] Open Unity normally and wait for compilation to finish. Confirm Console has no new compiler errors.
- [ ] Perform the end-user acceptance flow with `7日任务拆分.psd`:

  1. select the PSD and click “AI 整理”;
  2. confirm the browser opens without a manually started server;
  3. confirm the complete 1080 × 2340 PSD is visible;
  4. zoom, pan, use the minimap, click one group, Shift-click several groups, and drag a custom rectangle;
  5. enter a Chinese instruction and run “重新整理选中区域”;
  6. accept a group and verify a later refine does not change it;
  7. apply naming and hierarchy, then verify the generated Prefab in Unity;
  8. review common Prefab candidates and create only one checked candidate;
  9. verify unselected candidates are not created;
  10. trigger a domain reload and verify the page shows a disconnected state and can be reopened from Unity.

- [ ] Inspect `Library/PSDLayoutTool2/HierarchyWebSessions/` and confirm no generated file was imported into `Assets/`.
- [ ] Inspect the listener and confirm it is bound only to `127.0.0.1`.
- [ ] Inspect `git status --short`; stage only this feature’s intended files and their Unity `.meta` files.
- [ ] Run a final diff review for:

  - unrelated user changes accidentally included;
  - secret/token logging;
  - arbitrary path handling;
  - Unity API calls from background threads;
  - direct Prefab YAML edits;
  - mojibake or escaped Chinese UI text;
  - recursive hierarchy `FindChild` helpers;
  - duplicate primary AI organizer entry points.

- [ ] Create the final implementation commit using the Lore protocol only if integration required a final cohesive commit:

```powershell
git commit -m "Make PSD organization visually inspectable before asset mutation" `
  -m "Constraint: Unity remains authoritative for parsing, validation, mutation, Undo, and Prefab creation.
Rejected: Embedding a WebView or moving PSD mutation into JavaScript | both increase platform and data-integrity risk.
Confidence: high
Scope-risk: moderate
Directive: Keep the loopback protocol authenticated and keep generated session data outside Assets.
Tested: PSD hierarchy web EditMode suite, existing organizer regressions, real 7日任务拆分.psd acceptance flow
Not-tested: Non-Windows system-browser behavior"
```

## Completion Criteria

- Clicking the PSD Inspector’s only primary “AI 整理” action opens the system browser automatically.
- The page renders the full real PSD and all imported objects, not only group names and counts.
- The user can pan, zoom, use a minimap, select one or multiple groups, and drag a custom region.
- The user can provide an instruction and reorganize only the selected scope.
- Accepted unselected groups stay locked.
- Unity validates and applies naming and hierarchy; JavaScript never mutates Unity assets directly.
- Common Prefab candidates are understandable and are created only after a separate explicit selection.
- Domain reload, disconnect, cancellation, and stale-session cleanup have deterministic behavior.
- The loopback server is authenticated, bound only to `127.0.0.1`, and exposes no arbitrary filesystem access.
- Existing organizer, apply, validation, and Prefab candidate tests still pass.
- No unrelated dirty worktree changes are staged or reverted.
