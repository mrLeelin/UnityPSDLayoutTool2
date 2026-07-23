# Hierarchy Preview Panel Design

## Goal

Make the PSD Hierarchy Preview readable as a Unity-style, two-pane hierarchy inspector without changing preview generation, import, or Apply behavior.

## Layout

- Keep the configured target Prefab path and Generate / Retry Preview, Import Manual Plan, and Cancel actions in a compact header.
- Place the preview below a draggable vertical splitter.
- The left pane, **Current Prefab**, renders the current node snapshot as an expandable Unity Hierarchy-style tree.
- The right pane, **Proposed Structure**, renders AI group nodes and their member leaves. Group nodes use a restrained blue tint; leaf rows use Unity-style foldout arrows and object icons.
- Selecting a proposed group shows its confidence and evidence in a compact inspector strip at the bottom of the right pane.

## Interaction and safety

- Existing Generate, Import Manual Plan, Cancel, and status/error behavior remain unchanged.
- The model is still the sole source of displayed data; UI state only stores splitter position, scroll positions, and expanded keys.
- Long names are clipped with tooltips. Empty/current-plan states remain explicit.

## Validation

- Add editor tests for the pure tree-building helpers: parent/child hierarchy, ordering, group/member rendering data, and no-plan state.
- Manually open the window in Unity and verify the two-pane layout, resizing, scrolling, selection details, and unchanged import controls.
