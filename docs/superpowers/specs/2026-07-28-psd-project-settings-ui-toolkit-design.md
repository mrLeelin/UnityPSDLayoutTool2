# PSD Layout Tool Project Settings UI Toolkit Design

## Goal

Replace the legacy IMGUI inspector for `PsdLayoutProjectSettings` with a UI Toolkit inspector. The project settings asset remains the single source of truth and continues to be selected and edited in the Unity Inspector.

## Scope

- Replace `PsdLayoutProjectSettingsEditor.OnInspectorGUI()` with `CreateInspectorGUI()`.
- Keep the four existing configuration groups: AI hierarchy organization, output settings, TextMeshPro defaults, and common asset naming.
- Preserve existing settings setters, normalization rules, error messages, secret storage, and asset references.
- Keep the existing custom inspector target and namespace `PsdLayoutTool2`.

## Layout

The inspector uses a narrow, editor-native layout with a titled header and four foldout sections.

1. **AI hierarchy organization**: provider and connection mode are always visible. Custom endpoint, model, and locally encrypted API key are visible only for the custom API mode. The missing-CLI and local-CLI guidance remain inline help boxes.
2. **Output settings**: output location is always visible. Fixed-path controls and Sprite Atlas version are visible only when the fixed-path option is selected.
3. **TextMeshPro defaults**: font and base material fields remain together. Missing asset and font/material compatibility warnings remain directly below those fields.
4. **Common asset naming**: Prefab and Texture prefixes appear as a single property panel with short explanation text and inline validation. The UI must show the same normalization and duplicate-prefix error as the existing logic.

## Interaction And Data Flow

- UI Toolkit fields are initialized from each `Resolve*Settings()` snapshot.
- User edits invoke the existing `SetHierarchyAiSettings`, `SetOutputSettings`, `SetFontSettings`, and `TrySetCommonAssetPrefixes` methods.
- After a successful update, the affected section refreshes from the resolved snapshot so normalized values are immediately displayed.
- Errors and warnings are displayed as `HelpBox` elements inside the related section. No validation logic moves into the visual layer.
- API key handling remains local-only through `PsdHierarchyAiSecretStore`; the project settings asset never stores the key.

## Non-Goals

- Do not add a new Project Settings page or change the selected asset workflow.
- Do not change PSD importing, common asset catalog scanning, resource naming semantics, output paths, or AI execution behavior.
- Do not modify existing serialized field names or migrate configuration data.

## Verification

- Add or update Editor tests for the UI Toolkit control visibility and existing settings mutations where feasible.
- Compile the package in the target Unity editor and confirm no new Console errors.
- Open the `PsdLayoutProjectSettings` asset and verify each section, conditional controls, warnings, prefix normalization, and persisted values manually.
