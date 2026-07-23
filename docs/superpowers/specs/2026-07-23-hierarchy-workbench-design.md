# PSD Hierarchy Workbench Design

## Goal

Turn hierarchy planning from a technical ID list into a reviewable editor workflow:
current Prefab hierarchy, AI grouping suggestions, and scoped refinement must be understandable
to artists and safe for incremental imports.

## Layout

The window has three persistent panes.

1. **Current Hierarchy**: Unity-style tree with search, foldouts, and selection.
2. **Suggested Groups**: compact group cards showing a readable name, member count, preview,
   confidence, and changed state. Cards are collapsed by default.
3. **Review Details**: the selected group's members, evidence, diff, and explicit actions:
   `Ping`, `Ping All`, `Accept`, and `Refine with AI`.

Internal stable IDs are never primary display text. They remain available only in technical
tooltips and are the only identity used to locate Prefab objects.

## Motion

Motion explains state changes; it must never delay editing or obscure selection.

- Group cards expand and collapse over 150-200 ms.
- Selecting a hierarchy node briefly highlights related suggestion cards for about 250 ms.
- Refinement changes animate only affected cards: removed members fade out and added members
  fade in. Unchanged cards stay still.
- Motion respects Unity's editor redraw cadence and has an immediate, non-animated fallback
  whenever the window repaints after a domain reload.

## Scoped AI Refinement

`Accept` locks a suggested group. Locked groups are carried forward as immutable baseline
groups for later refinement.

`Refine with AI` operates only on the selected unlocked group, or on a manually selected
set of hierarchy nodes when the user chooses to create a new suggestion. The request includes
the selected scope plus read-only neighboring context. It returns a diff, not a replacement
for the full plan. The user can accept or discard that diff before it becomes the proposed
plan.

## Safety and Incremental Behavior

- Ping resolves Profile local-file IDs to the currently opened Prefab Stage objects; it never
  selects by display name or guessed hierarchy path.
- Refinement never mutates a Prefab, Profile, material, or import data. Only the existing
  validated Apply action can write a plan.
- Existing accepted groups and protected/project-owned boundaries cannot be moved by AI.
- A domain reload clears transient motion/selection state but never changes the persisted
  plan or the Prefab.

## Verification

- Unit tests cover scope derivation and preservation of accepted groups.
- Editor verification opens the 7日任务拆分 fixture, tests Ping in Prefab Stage, and captures
  collapsed, expanded, and refined-diff states.
- Unity compilation must pass before handoff.
