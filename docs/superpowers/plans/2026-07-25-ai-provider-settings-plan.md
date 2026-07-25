# AI Provider Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the PSD hierarchy organizer select Claude or Codex from Unity global settings, use either the provider default or a machine-local custom endpoint/key, and show the effective choice plus actionable sanitized failures in the web workbench.

**Architecture:** Persist provider, mode, and custom URLs in `PsdLayoutProjectSettings`; keep keys behind an `IPsdAiSecretStore` using Windows DPAPI-backed local storage. Create provider-specific runners behind `IPsdHierarchyAiRunner`, selected by an immutable session snapshot. Extend the existing web DTO with read-only provider status while keeping Unity validation and explicit apply ownership unchanged.

**Tech Stack:** Unity 6000.3.7f1 Editor C#, ScriptableObject settings, NUnit EditMode tests, `System.Diagnostics.Process`, `System.Security.Cryptography`/Windows DPAPI, Newtonsoft.Json, existing loopback web workbench.

---

### Task 1: Add provider and connection domain types

**Files:**
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyAiProvider.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyAiConnectionSettings.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/Settings/PsdLayoutProjectSettings.cs:276-350`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyAiProviderSettingsTests.cs`

- [ ] **Step 1: Write failing tests for defaults, independent provider values, and validation**

  Add NUnit tests asserting:

  ```csharp
  [Test]
  public void NewSettingsDefaultToCodexDefaultMode()
  {
      var settings = ScriptableObject.CreateInstance<PsdLayoutProjectSettings>();
      try
      {
          Assert.That(settings.ResolveAiSettings().provider, Is.EqualTo(PsdHierarchyAiProvider.Codex));
          Assert.That(settings.ResolveAiSettings().codex.mode, Is.EqualTo(PsdHierarchyAiConnectionMode.Default));
          Assert.That(settings.ResolveAiSettings().claude.mode, Is.EqualTo(PsdHierarchyAiConnectionMode.Default));
      }
      finally { Object.DestroyImmediate(settings); }
  }

  [Test]
  public void ClaudeAndCodexConnectionModesRemainIndependent()
  {
      var settings = ScriptableObject.CreateInstance<PsdLayoutProjectSettings>();
      try
      {
          settings.SetAiProvider(PsdHierarchyAiProvider.Claude);
          settings.SetAiConnectionMode(PsdHierarchyAiProvider.Claude, PsdHierarchyAiConnectionMode.Custom);
          settings.SetAiConnectionMode(PsdHierarchyAiProvider.Codex, PsdHierarchyAiConnectionMode.Default);
          Assert.That(settings.ResolveAiSettings().claude.mode, Is.EqualTo(PsdHierarchyAiConnectionMode.Custom));
          Assert.That(settings.ResolveAiSettings().codex.mode, Is.EqualTo(PsdHierarchyAiConnectionMode.Default));
      }
      finally { Object.DestroyImmediate(settings); }
  }

  [TestCase("https://api.example.com/v1", true)]
  [TestCase("http://127.0.0.1:8080/v1", true)]
  [TestCase("http://api.example.com/v1", false)]
  [TestCase("not a url", false)]
  public void CustomUrlValidationEnforcesHttpsExceptLoopback(string value, bool expected)
  {
      Assert.That(PsdHierarchyAiConnectionSettings.TryValidateBaseUrl(value, out _), Is.EqualTo(expected));
  }
  ```

- [ ] **Step 2: Run the focused Unity EditMode tests and verify the expected missing-type/member failures**

  Run:

  ```powershell
  uloop test --project-path E:\Project\Demo\monsterhunter --test-platform EditMode --test-filter PsdHierarchyAiProviderSettingsTests --wait
  ```

  Expected: compilation/test failure because the provider enums, snapshots, and settings accessors do not exist yet.

- [ ] **Step 3: Implement the minimal serializable settings model**

  Define `PsdHierarchyAiProvider { Codex, Claude }`, `PsdHierarchyAiConnectionMode { Default, Custom }`, a serialized per-provider connection class containing mode and base URL, and an immutable `PsdHierarchyAiSettingsSnapshot` containing the active provider plus both provider snapshots. Add `ResolveAiSettings`, `SetAiProvider`, `SetAiConnectionMode`, and `SetAiBaseUrl` to `PsdLayoutProjectSettings`; use Codex/Default as field initializers so old assets deserialize compatibly. Mark the asset dirty only when values change.

- [ ] **Step 4: Re-run the focused tests and verify green**

  Run the same `uloop test` command. Expected: all provider settings, independent state, and URL validation tests pass with no new compiler errors.

- [ ] **Step 5: Commit the domain slice**

  ```powershell
  git add Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyAiProvider.cs Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyAiConnectionSettings.cs Assets/PSDLayoutTool2/Editor/Settings/PsdLayoutProjectSettings.cs Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyAiProviderSettingsTests.cs
  git commit -m "Add project settings for hierarchy AI providers"
  ```

### Task 2: Implement machine-local protected secrets

**Files:**
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/IPsdAiSecretStore.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdAiSecretStore.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdAiSecretStoreTests.cs`

- [ ] **Step 1: Write failing tests for save/read/clear and isolation**

  Test a fake in-memory store first so behavior is platform-independent:

  ```csharp
  [Test]
  public void SaveReadAndClearAreScopedByProjectAndProvider()
  {
      var store = new InMemorySecretStore();
      store.Save("project-a", PsdHierarchyAiProvider.Claude, "claude-key");
      store.Save("project-a", PsdHierarchyAiProvider.Codex, "codex-key");
      Assert.That(store.TryRead("project-a", PsdHierarchyAiProvider.Claude, out var claude), Is.True);
      Assert.That(claude, Is.EqualTo("claude-key"));
      Assert.That(store.TryRead("project-b", PsdHierarchyAiProvider.Claude, out _), Is.False);
      store.Clear("project-a", PsdHierarchyAiProvider.Claude);
      Assert.That(store.TryRead("project-a", PsdHierarchyAiProvider.Claude, out _), Is.False);
      Assert.That(store.TryRead("project-a", PsdHierarchyAiProvider.Codex, out _), Is.True);
  }
  ```

  Add a Windows implementation contract test that round-trips a key and never exposes the plaintext from its serialized storage value. Keep OS-specific calls behind an injectable protected-store adapter so EditMode tests do not require a real credential.

- [ ] **Step 2: Run the focused tests and confirm the missing secret-store API failure**

  ```powershell
  uloop test --project-path E:\Project\Demo\monsterhunter --test-platform EditMode --test-filter PsdAiSecretStoreTests --wait
  ```

  Expected: failure because `IPsdAiSecretStore`, the fake test store, and production implementation are absent.

- [ ] **Step 3: Implement the secret-store boundary and Windows DPAPI backend**

  Define `TryRead(projectIdentity, provider, out string key)`, `Save(...)`, and `Clear(...)`. Derive a stable secret identifier from the normalized project path plus provider. Protect UTF-8 key bytes with current-user DPAPI before storing ciphertext in a machine-local location; do not put the key or ciphertext in `PsdLayoutProjectSettings`. Return a specific unavailable-store error rather than falling back to plaintext.

- [ ] **Step 4: Re-run secret tests and verify green**

  Run the same `uloop test` command. Expected: all isolation, clear, round-trip, and failure-path tests pass.

- [ ] **Step 5: Commit the secret slice**

  ```powershell
  git add Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/IPsdAiSecretStore.cs Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdAiSecretStore.cs Assets/PSDLayoutTool2/Editor/Tests/PsdAiSecretStoreTests.cs
  git commit -m "Protect hierarchy AI credentials per local user"
  ```

### Task 3: Add the global settings Inspector with conditional visibility

**Files:**
- Modify: `Assets/PSDLayoutTool2/Editor/Settings/PsdLayoutProjectSettingsEditor.cs:16-125`
- Modify: `Assets/PSDLayoutTool2/Editor/Settings/PsdLayoutProjectSettings.cs` to expose save/clear status helpers
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyAiSettingsUiStateTests.cs`

- [ ] **Step 1: Write failing pure-state tests for visibility and validation**

  Test a small UI-state helper with cases:

  ```csharp
  [Test]
  public void DefaultModeHidesCustomFields()
  {
      var state = PsdHierarchyAiSettingsUiState.Resolve(PsdHierarchyAiConnectionMode.Default, false);
      Assert.That(state.showBaseUrl, Is.False);
      Assert.That(state.showApiKey, Is.False);
      Assert.That(state.showTestConnection, Is.False);
  }

  [Test]
  public void CustomModeShowsFieldsOnlyWhenAKeyAndUrlCanBeSaved()
  {
      var state = PsdHierarchyAiSettingsUiState.Resolve(PsdHierarchyAiConnectionMode.Custom, true);
      Assert.That(state.showBaseUrl, Is.True);
      Assert.That(state.showApiKey, Is.True);
      Assert.That(state.canTestConnection, Is.True);
  }
  ```

- [ ] **Step 2: Run the focused UI-state tests and verify red**

  ```powershell
  uloop test --project-path E:\Project\Demo\monsterhunter --test-platform EditMode --test-filter PsdHierarchyAiSettingsUiStateTests --wait
  ```

  Expected: missing helper/type failure.

- [ ] **Step 3: Implement the Inspector section and UI-state helper**

  Draw provider popup first, then the active provider's Default/Custom toolbar. In Custom mode use `EditorGUILayout.PasswordField`, a compact eye-button toggle, delayed URL field, `测试连接`, replace-key, and clear-key actions. Do not assign the password field value into the serialized settings object. Save the URL/mode immediately through existing settings methods and save/clear the key through `IPsdAiSecretStore` keyed by `Application.dataPath`'s project root. Keep field labels and error dialogs Chinese-compatible with the existing Inspector.

- [ ] **Step 4: Re-run focused UI tests and perform a read-only Inspector compile check**

  Run the test command above, then:

  ```powershell
  uloop compile --project-path E:\Project\Demo\monsterhunter --force-recompile false --wait-for-domain-reload true
  ```

  Expected: focused tests pass and Unity reports 0 compiler errors.

- [ ] **Step 5: Commit the settings UI slice**

  ```powershell
  git add Assets/PSDLayoutTool2/Editor/Settings/PsdLayoutProjectSettingsEditor.cs Assets/PSDLayoutTool2/Editor/Settings/PsdLayoutProjectSettings.cs Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyAiSettingsUiStateTests.cs
  git commit -m "Expose hierarchy AI connection settings in Unity"
  ```

### Task 4: Extend process invocation and add provider runners

**Files:**
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/IPsdHierarchyAiRunner.cs:13-108`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/CodexCliHierarchyRunner.cs:89-409`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/ClaudeCliHierarchyRunner.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyAiRunnerFactory.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/CodexCliHierarchyRunner.cs` or a new shared process invocation helper to carry child-only environment values and provider-neutral diagnostics
- Modify: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyAiRunnerTests.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/ClaudeCliHierarchyRunnerTests.cs`

- [ ] **Step 1: Write failing invocation tests**

  Add assertions that:

  ```csharp
  [Test]
  public void DefaultCodexInvocationHasNoModelOrCredentialOverride() { /* inspect invocation arguments/environment */ }

  [Test]
  public void CustomClaudeInvocationInjectsOnlyChildEnvironmentAndNeverArguments() { /* assert base URL/key are environment-only */ }

  [Test]
  public void FactorySelectsRunnerFromImmutableProviderSnapshot() { /* Codex -> Codex runner, Claude -> Claude runner */ }
  ```

  Add fixture outputs for Claude's print-mode JSON envelope and Codex's existing output file; both must normalize to the same `PsdHierarchyPlan`.

- [ ] **Step 2: Run runner tests and verify red**

  ```powershell
  uloop test --project-path E:\Project\Demo\monsterhunter --test-platform EditMode --test-filter "PsdHierarchyAiRunnerTests|ClaudeCliHierarchyRunnerTests" --wait
  ```

  Expected: failure because invocation environment support, Claude runner, factory, and envelope normalization do not exist.

- [ ] **Step 3: Add child-process environment support without changing global environment**

  Add an environment dictionary to `PsdHierarchyProcessInvocation`. Apply it to `ProcessStartInfo.EnvironmentVariables` inside `SystemHierarchyProcessAdapter.CreateStartInfo`; never call `Environment.SetEnvironmentVariable`. Extend the fake process adapter to capture the dictionary for tests. Change hard-coded Codex failure text to include a provider field.

- [ ] **Step 4: Implement Codex settings-aware execution**

  Keep current read-only, `--sandbox read-only`, `--ephemeral`, output-schema, and timeout arguments. In default mode pass no model, URL, or key. In custom mode resolve the Codex secret immediately before launch and inject only the provider-specific endpoint/key environment variables required by the installed CLI. Return a sanitized provider-specific error when the secret or URL is invalid.

- [ ] **Step 5: Implement Claude print-mode structured execution**

  Invoke the installed `claude` CLI non-interactively with print mode, JSON output, JSON Schema validation, no session persistence, no tools, and no model override. Use the same prompt/request/schema files and bounded process adapter. Parse Claude's structured output envelope and normalize the plan before `PsdHierarchyPlanValidator` runs. Keep all provider credentials out of arguments and output packages.

- [ ] **Step 6: Implement the provider factory and run the tests green**

  The factory accepts an immutable provider/connection snapshot, a secret store, and process adapter. Run the focused runner command again; expected: all default/custom argument, environment isolation, factory, timeout, output parsing, and redaction tests pass.

- [ ] **Step 7: Commit the runner slice**

  ```powershell
  git add Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/IPsdHierarchyAiRunner.cs Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/CodexCliHierarchyRunner.cs Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/ClaudeCliHierarchyRunner.cs Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyAiRunnerFactory.cs Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyAiRunnerTests.cs Assets/PSDLayoutTool2/Editor/Tests/ClaudeCliHierarchyRunnerTests.cs
  git commit -m "Run hierarchy planning through the selected AI provider"
  ```

### Task 5: Thread the immutable provider snapshot into organizer sessions

**Files:**
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyOrganizerEntry.cs:125-205`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyOrganizerWindow.cs:17-330`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebSessionRegistry.cs`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebSession.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyAiSessionSnapshotTests.cs`

- [ ] **Step 1: Write a failing session snapshot test**

  Build a session from Codex settings, mutate the global settings to Claude, and assert the already-open preview continues to use Codex until the session is reopened. Also assert the display label reports provider plus Default/Custom mode.

- [ ] **Step 2: Run the session test and verify red**

  ```powershell
  uloop test --project-path E:\Project\Demo\monsterhunter --test-platform EditMode --test-filter PsdHierarchyAiSessionSnapshotTests --wait
  ```

  Expected: failure because organizer entry and web session do not carry provider snapshots.

- [ ] **Step 3: Pass the factory-created runner and status snapshot through entry points**

  Replace direct `new CodexCliHierarchyRunner()` calls in `Open`/`OpenWeb` with a factory built from the current project settings and secret store. Store a provider status snapshot alongside the preview model in the web session. Keep the existing source PSD, target Prefab, apply handler, and read-only input construction unchanged.

- [ ] **Step 4: Run session tests and compile**

  Run the focused test command, then:

  ```powershell
  uloop compile --project-path E:\Project\Demo\monsterhunter --force-recompile false --wait-for-domain-reload true
  ```

  Expected: test pass and 0 compiler errors.

- [ ] **Step 5: Commit the session slice**

  ```powershell
  git add Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyOrganizerEntry.cs Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyOrganizerWindow.cs Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebSessionRegistry.cs Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebSession.cs Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyAiSessionSnapshotTests.cs
  git commit -m "Snapshot the selected AI provider per organizer session"
  ```

### Task 6: Expose read-only provider status and diagnostics in the web workbench

**Files:**
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebContracts.cs:29-48`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebController.cs:22-66,227-270`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/Static/index.html:10-35`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/Static/organizer.js:90-145,630-760`
- Modify: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/Static/organizer.css`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyWebProviderStatusTests.cs`

- [ ] **Step 1: Write failing DTO/controller tests**

  Assert that a session response contains provider (`Claude`/`Codex`) and mode (`Default`/`Custom`) but no key, credential environment, or secret-store value. Assert failure status prefixes the sanitized provider label.

- [ ] **Step 2: Run web contract tests and verify red**

  ```powershell
  uloop test --project-path E:\Project\Demo\monsterhunter --test-platform EditMode --test-filter PsdHierarchyWebProviderStatusTests --wait
  ```

  Expected: failure because provider status fields and rendering do not exist.

- [ ] **Step 3: Add the read-only status strip**

  Add a compact top-bar status element such as `当前 AI：Claude（自定义）`. Render it from the session DTO only; do not add provider dropdowns, URL fields, key fields, or configuration POST endpoints to the web app.

- [ ] **Step 4: Add sanitized operation diagnostics**

  Update operation messages and failed-state rendering to show provider, mode, operation stage, exit code, and bounded sanitized detail. Keep existing quota and timeout wording understandable. Add client rendering for missing custom configuration and unavailable CLI states.

- [ ] **Step 5: Run web tests and static asset checks**

  Run the focused test command, then:

  ```powershell
  uloop compile --project-path E:\Project\Demo\monsterhunter --force-recompile false --wait-for-domain-reload true
  ```

  Expected: focused tests pass, static asset contract tests pass, and Unity reports 0 compiler errors.

- [ ] **Step 6: Commit the web slice**

  ```powershell
  git add Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebContracts.cs Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebController.cs Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/Static/index.html Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/Static/organizer.js Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/Static/organizer.css Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyWebProviderStatusTests.cs
  git commit -m "Show selected hierarchy AI in the workbench"
  ```

### Task 7: Add bounded connection testing

**Files:**
- Modify: `Assets/PSDLayoutTool2/Editor/Settings/PsdLayoutProjectSettingsEditor.cs`
- Create: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyAiConnectionTester.cs`
- Create: `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyAiConnectionTesterTests.cs`

- [ ] **Step 1: Write failing tests for non-mutating connection checks**

  Assert that missing URL/key is rejected without starting a process, a successful fake process returns a provider-specific success, timeout is mapped to a timeout result, and the request contains no PSD path or apply command.

- [ ] **Step 2: Run the connection tests and verify red**

  ```powershell
  uloop test --project-path E:\Project\Demo\monsterhunter --test-platform EditMode --test-filter PsdHierarchyAiConnectionTesterTests --wait
  ```

  Expected: failure because the connection tester does not exist.

- [ ] **Step 3: Implement the tester using the same runner factory and child environment**

  Send a minimal schema-constrained response request through the selected provider runner with a short timeout, no PSD input, no Unity asset paths, no tools, and no session persistence. Return a structured result for success, auth, network, timeout, executable, and output-schema errors.

- [ ] **Step 4: Wire `测试连接` to the settings Inspector and verify green**

  Use the existing Inspector button only in Custom mode. Keep the key masked and do not show the value in a dialog. Run the focused test command and Unity compile.

- [ ] **Step 5: Commit the connection-test slice**

  ```powershell
  git add Assets/PSDLayoutTool2/Editor/Settings/PsdLayoutProjectSettingsEditor.cs Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyAiConnectionTester.cs Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyAiConnectionTesterTests.cs
  git commit -m "Add bounded hierarchy AI connection checks"
  ```

### Task 8: Full verification and handoff

**Files:**
- Test: all changed Editor tests and existing PSD hierarchy tests
- Inspect: `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/Static/*`

- [ ] **Step 1: Run all focused provider and hierarchy tests**

  ```powershell
  uloop test --project-path E:\Project\Demo\monsterhunter --test-platform EditMode --test-filter "PsdHierarchyAi.*|PsdHierarchyWeb.*|PsdHierarchyPlan.*|PsdHierarchyProfile.*" --wait
  ```

  Expected: all matching tests pass.

- [ ] **Step 2: Run the full Editor test assembly**

  ```powershell
  uloop test --project-path E:\Project\Demo\monsterhunter --test-platform EditMode --test-assembly PsdLayoutTool2.Editor.Tests --wait
  ```

  Expected: pass, or document unrelated pre-existing failures with their exact test names.

- [ ] **Step 3: Compile with the project Unity version**

  ```powershell
  uloop compile --project-path E:\Project\Demo\monsterhunter --force-recompile false --wait-for-domain-reload true
  ```

  Expected: 0 errors and 0 warnings attributable to this feature.

- [ ] **Step 4: Perform a real local smoke check without exposing credentials**

  In Unity, open the global settings asset and verify provider/mode/conditional fields, save a custom URL and key, switch providers, clear one key, and confirm the other remains. Open AI 整理 once per provider, verify the read-only web label, run `测试连接`, run one analysis, and inspect the Unity log plus retained failure package for absence of the key.

- [ ] **Step 5: Reopen modified Chinese files and inspect repository scope**

  Check all changed `.cs`, `.js`, `.html`, and `.md` files for mojibake, `???`, BOM drift, and escaped display text. Run:

  ```powershell
  git diff --check HEAD~8..HEAD
  git status --short
  ```

  Confirm no unrelated pre-existing files were staged and no API key appears in tracked files.

- [ ] **Step 6: Commit verification evidence or report gaps**

  Do not commit generated Unity Library/log/cache output. If all checks pass, record the commands and results in the final handoff. If a real provider call cannot run, report that as a verification gap rather than claiming runtime proof.

## Self-Review

- Spec coverage: project provider/mode/URL persistence is covered by Tasks 1 and 3; protected local keys by Task 2; default/custom execution and normalized plans by Task 4; session locking by Task 5; read-only web status and sanitized diagnostics by Task 6; bounded connection testing by Task 7; full verification and encoding checks by Task 8.
- Placeholder scan: no unresolved placeholder marker or unspecified implementation step is used; every task names files, tests, commands, and expected outcomes.
- Type consistency: `PsdHierarchyAiProvider`, `PsdHierarchyAiConnectionMode`, `PsdHierarchyAiSettingsSnapshot`, `IPsdAiSecretStore`, and `PsdHierarchyAiRunnerFactory` are introduced before later tasks consume them; all later runner/session/web tasks use the same snapshot and factory boundary.
