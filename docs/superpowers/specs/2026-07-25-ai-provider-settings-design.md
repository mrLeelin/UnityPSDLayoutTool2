# AI Provider Settings Design

## Goal

Allow the PSD hierarchy organizer to use either Claude or Codex. The selected provider and its connection mode are configured only in the existing project-wide PSD Layout Tool settings. The browser workbench shows the effective provider as read-only information and never edits credentials.

The AI remains a read-only planner. Unity continues to own validation, preview, explicit apply, rollback, and Prefab/Profile writes.

## Settings Model

Add a project setting for the active provider:

- `Codex` remains the backward-compatible default.
- `Claude` is the second supported provider.

Keep an independent connection configuration for each provider so switching providers does not discard the previous values:

- Connection mode: `Default` or `Custom`.
- Custom API base URL.
- A reference to a machine-local secret; never the API key itself.

The active provider, both connection modes, and both API base URLs are serialized in `PsdLayoutProjectSettings`. They are project-owned and may be committed to Git. API keys are stored separately for the current OS user and project and must never be serialized into a Unity asset, scene, Prefab, `.meta` file, log, command line, request package, or web response.

Use an `IPsdAiSecretStore` boundary. The Windows implementation stores only DPAPI-protected ciphertext scoped to the current Windows user. Its lookup key includes the project identity and provider so Claude and Codex credentials cannot collide across providers or projects. If the protected store is unavailable or fails, custom mode is blocked with a specific local-secret error instead of falling back to plaintext storage.

## Global Settings UI

Add a `层级整理 AI` section to the existing `PsdLayoutProjectSettings` Inspector:

1. `提供方`: Claude or Codex.
2. `连接方式`: Default or Custom.
3. Custom-only fields:
   - `API 地址`
   - `API Key`
   - reveal/hide control
   - `测试连接`

Visibility rules:

- Only the active provider's connection fields are shown.
- Default mode hides the API address, API key, reveal control, and connection-test command.
- Custom mode shows all custom fields.
- The API key is masked by default. Revealing it affects only the current Inspector UI state and is reset when the Inspector is recreated.
- Switching provider restores that provider's previously saved mode, URL, and machine-local key state.
- A saved key is represented by a neutral placeholder; the UI does not serialize it back into the settings object.
- The user can replace or explicitly clear a saved key.

Validation rules:

- Default mode does not require a URL or key.
- Custom mode requires a non-empty absolute API URL and a saved non-empty key.
- HTTPS is required for non-loopback addresses. HTTP is allowed only for loopback development endpoints.
- Invalid custom settings disable AI analysis and show the exact missing or invalid field.

## Browser Workbench

The workbench does not contain configuration controls. It displays one read-only status label:

- `当前 AI：Codex（默认）`
- `当前 AI：Codex（自定义）`
- `当前 AI：Claude（默认）`
- `当前 AI：Claude（自定义）`

The effective provider snapshot is captured when the workbench session opens. Changing global settings does not silently switch an already-open session; reopening `AI 整理` creates a session with the new settings.

Failure messages include the provider, connection mode, operation stage, process exit code when available, and a bounded sanitized detail. They must not contain the API key, authorization headers, or an API URL containing user information or query secrets.

## Provider Execution

Introduce a provider factory that returns an `IPsdHierarchyAiRunner` from an immutable settings snapshot:

```text
PsdHierarchyAiRunnerFactory
  Codex -> CodexCliHierarchyRunner
  Claude -> ClaudeCliHierarchyRunner
```

Both runners consume the same bounded hierarchy request, focus scope, prompt contract, JSON Schema, timeout policy, output limits, cancellation behavior, and Unity-side plan validation.

Default mode:

- Uses the provider's existing CLI authentication and default model.
- Does not pass a model override.
- Does not set custom endpoint or credential environment variables.

Custom mode:

- Resolves the provider key from the machine-local secret store immediately before process launch.
- Injects provider-specific endpoint and credential values only into the child process environment.
- Never changes process-wide or user-wide environment variables.
- Never places the key in command-line arguments.
- Omits model overrides so the selected CLI/provider keeps its configured default model.

Codex keeps its existing read-only, ephemeral structured-output invocation. Claude uses non-interactive print mode, structured JSON Schema output, no session persistence, and no tools. Provider-specific output envelopes are normalized into the same `PsdHierarchyPlan` before Unity validation.

The shared process adapter must use provider-neutral diagnostics rather than hard-coded `Codex process` messages.

## Test Connection

`测试连接` performs a provider-specific, bounded, non-mutating health check:

- It validates the URL and presence of a local key before launch.
- It starts the selected CLI with the same custom child-process environment used by hierarchy analysis.
- It requests a minimal structured response without reading PSD data or Unity assets.
- It uses a short timeout and cannot create or modify project files.
- The result identifies authentication, connection, timeout, executable, and structured-output failures separately.

Passing this check proves only CLI and endpoint connectivity. It does not prove that a full PSD hierarchy request will pass plan validation.

## Error Handling And Redaction

Redaction is applied before any provider error reaches Unity logs, the browser DTO, dialogs, or persisted diagnostic packages. At minimum it removes:

- Exact API keys and bearer tokens.
- Authorization header values.
- Credential-bearing URL user information.
- Known provider credential environment-variable values.

A failed hierarchy request may retain its bounded request package for diagnosis, but the package contains no credentials or custom process environment. Success cleanup and cancellation ownership rules remain unchanged.

## Verification

Use test-first implementation. Focused Editor tests cover:

- Backward-compatible Codex/default settings.
- Independent Claude and Codex mode/URL persistence.
- Default/custom field visibility as pure UI-state logic.
- URL validation, including HTTPS and loopback HTTP.
- Provider factory selection and immutable session snapshots.
- Default invocations omitting endpoint, key, and model overrides.
- Custom invocations injecting endpoint/key only into child-process environment.
- Claude and Codex structured-output normalization.
- Missing secret, invalid URL, unavailable executable, authentication, timeout, and non-zero exit errors.
- Redaction across Unity-facing and web-facing error paths.
- Read-only workbench provider labels.
- Connection tests remaining non-mutating and bounded.

After focused tests pass, run Unity compilation and the complete PSD Layout Tool Editor test assembly. Reopen every modified Chinese text file and confirm there is no mojibake, `???`, unintended BOM change, or escaped `\uXXXX` display text.

## Non-Goals

- Selecting explicit Claude or Codex model names.
- Configuring providers from the browser workbench.
- Automatic provider fallback after failure.
- Storing API keys in project assets or Git.
- Allowing either AI provider to modify Unity resources directly.
