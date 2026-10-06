---
name: recttransform-anchor-cleanup
description: Review RectTransform anchors in an existing Unity Prefab when anchor organization is enabled, preserving the current visual layout and layout-system behavior. Use as a companion to prefab-hierarchy-cleanup; do not use it to redesign runtime UI or invent anchor changes from names alone.
---

# RectTransform Anchor Cleanup

Use this skill only when the hierarchy cleanup session explicitly says that anchor organization is enabled. When it is disabled, preserve every existing `anchorMin`, `anchorMax`, `pivot`, `anchoredPosition`, `sizeDelta`, `offsetMin`, and `offsetMax` value and do not propose anchor changes.

## Evidence before a decision

- Treat the authoritative Unity snapshot as the source of node identity, parentage, geometry, components, sibling order, nested Prefab boundaries, and current RectTransform values.
- Inspect the parent and the complete visual unit together. A child anchor is meaningful only relative to its parent size, sibling layout, pivot, and the way the unit is expected to resize.
- Use geometry and visual evidence to distinguish a fixed-size control, an edge-attached control, a proportional element, and a true stretch region. Names and PSD layer labels are hints only.
- Record observed facts separately from inferences and unknowns. If the evidence does not identify the intended responsive behavior, preserve the current anchors and report the ambiguity.

## Safe anchor decisions

- Use a point anchor for a fixed-size element whose position should remain tied to a stable point in the parent.
- Use a one-axis edge anchor only when the element consistently follows that parent edge across the available evidence.
- Use stretch anchors only when the element is a container or background whose bounds are intended to follow the parent edge or both edges. Do not stretch a fixed-size icon, label, or button merely because it is near an edge.
- Keep the pivot consistent with the element's alignment and animation origin. Do not change pivot as a side effect of changing anchors unless the review explains the visual and behavioral reason.
- For mixed or nested layouts, prefer the smallest anchor change that expresses the observed relationship and leaves child geometry stable.

## Preserve the rendered result

Before any proposed change, capture the current parent-relative and world-space rectangle. Recompute the corresponding `anchoredPosition`, `sizeDelta`, or offsets so the element keeps the same rendered bounds at the captured reference size. Verify both corners after the change and check at least one alternate parent size when the new anchors are intended to provide responsiveness.

Do not change anchors or offsets on a RectTransform controlled by `HorizontalLayoutGroup`, `VerticalLayoutGroup`, `GridLayoutGroup`, `ContentSizeFitter`, `AspectRatioFitter`, a `DrivenRectTransformTracker`, an Animator or animation clip, a nested Prefab boundary, or a serialized binding unless the evidence and the supported executor explicitly cover that control relationship. If a component drives the value, preserve it and record the reason.

## Review and plan contract

- The review must list each candidate by exact `node:<id>`, current anchor/pivot/offset values, proposed values, geometry evidence, affected parent relationship, and risk.
- State explicitly whether the proposal is fixed-point, edge-attached, proportional, or stretch behavior.
- Keep anchor work opt-in and separate from grouping, naming, extraction, and asset renames. Do not let an anchor hypothesis expand the cleanup scope.
- The current version 2 hierarchy plan has no independent anchor-operation array. Do not invent one, put anchor fields into unrelated operations, or claim that a plan applied an anchor change when the executor cannot represent it. Until a supported anchor operation exists, anchor findings are review and verification requirements only; leave the Prefab unchanged by that part of the plan.
- Visual acceptance requires a before/after inspection of the actual Prefab or generated UI. A passing JSON lint or apply receipt alone does not prove anchor correctness.

## Output vocabulary

Use these labels in the review so the decision is auditable: `preserve`, `point`, `edge`, `proportional`, `stretch`, `driven-by-layout`, `animation-controlled`, `nested-prefab-boundary`, and `insufficient-evidence`.
