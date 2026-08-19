# PSD Inspector Action Layout Design

## Goal

Replace the current flat stack of equal-weight buttons with a compact workflow
layout that is easier to scan. This change affects only the PSD Inspector action
area. Existing commands retain their behavior and availability rules.

The `Local Repair` button is removed from the Inspector. Its underlying window
and APIs remain available to other entry points; this work does not delete the
feature.

## Approved Layout

The action area is divided into three labeled sections.

### Generate And Update

- `Full Generate Prefab` is the only full-width primary action and receives a
  restrained blue Unity-style emphasis.
- `Regenerate With Confirmed Cleanup` and
  `Incremental Update (Preserve Organization)` share the next row.
- Existing disabled-state logic remains unchanged.

### Hierarchy Organization

- `AI Organize` and `Copy AI Prompt` share one row with equal width.
- `Local Repair` is not rendered.

### Utilities

- `Ping Prefab` and `Open 9-Slice Tool` share one row.
- `Open Log Folder` and `Reveal Latest Log` share one row.
- Utility actions keep the standard Unity button appearance so they remain
  visually secondary to generation and organization.

The existing separator and `Unity Texture Import Settings` section remain below
the action area.

## Spacing And Responsiveness

- Section labels use Unity's small bold label style.
- Sections have 8 pixels of vertical separation; rows have 4 pixels.
- Action buttons use a stable height of 24 pixels.
- At normal Inspector widths, paired actions use two equal columns.
- When the available Inspector width is too narrow for the localized labels,
  paired actions stack vertically instead of truncating or overlapping.
- Tooltips and confirmation dialogs remain unchanged except where their button
  moves to a different section.

## Implementation Boundary

- Keep the existing IMGUI `OnInspectorGUI` implementation.
- Add only small local drawing helpers when they remove repeated row, spacing,
  or primary-style code.
- Do not change generation, AI, clipboard, Prefab lookup, or logging behavior.
- Do not modify or remove the local-repair implementation outside the Inspector
  button entry point.

## Verification

- EditMode tests verify the intended action grouping and absence of the local
  repair button from the Inspector action layout where practical.
- A fresh Unity compilation must complete with no compiler errors.
- Visual inspection covers wide and narrow Inspector widths, disabled buttons,
  Chinese labels, and English labels.
- Existing unrelated working-tree changes remain untouched.
