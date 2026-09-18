# Plan Format

Use one UTF-8 JSON plan per cleanup operation. Treat it as the reviewed execution contract.

> **Current Unity executor capability (authoritative over the rest of this file).**
> Executable now: `wrappers`, `moves`, `renames`, `tightBounds`, `emptyContainerRemovals`,
> `componentExtractions` with matching `componentFamilyDecisions` mode `component` (template must also
> appear in `instances`; every instance must share the template's recursive component structure;
> `assetPath` must be a new PascalCase `.prefab` under `Assets/`), `stateComponentExtractions`
> (mutually exclusive direct-sibling roots in one visual slot; `template` must be one of
> `states[].source`; those sources must not be referenced from outside), and
> `variantComponentExtractions` (rows visible at different list positions; every state
> representative must also appear once in `instances`; each instance's structure must match its
> selected state source), `statefulComponentExtractions` (repeated items with real shared content
> plus a few states; `[States]` is created before `[Common]`; every direct child of an instance
> source must be mapped exactly once by `commonSourceNames` + `stateSourceNames`), and
> `textureRenames` / `spriteAtlasRenames` (`toName` without extension; every Texture `toName` must
> start with `<prefabName>_`; every SpriteAtlas `toName` must equal `prefabName`; each `from` must
> be a private asset of the target Prefab; the target path must not exist yet).
> `postGroupingExtractionIntents` is also executable, but only as an automatic **second stage**:
> after the hierarchy stage is saved and re-verified, Unity refreshes the authoritative snapshot
> and applies the reviewed intents itself. Its `templatePath` and every `instances[].path` are
> post-grouping hierarchy paths (not `node:<id>`), and every `requiresExtraction` candidate of the
> refreshed snapshot must be covered by exactly one same-mode `component`, `state`, `variant`, or
> `stateful` intent.
> Everything else — `containmentResolutions`, `flatSiblingResolutions`,
> `selectedPrefabExtractions`, `crossParentPrefabExtractions` —
> **must be empty arrays**: Unity refuses a non-empty unsupported array before any write. Unity no
> longer normalizes, repairs, force-extracts or converts the reviewed plan, so nothing here is
> derived on your behalf.

## Unity AI Chat Plan (Version 2)

The Unity AI hierarchy chat window accepts only version 2 plans. The window supplies an authoritative node snapshot generated through Unity Editor APIs. Copy its `fingerprint` into `snapshotFingerprint`, and reference every existing Prefab node as `node:<id>` using an ID present in that snapshot.

```json
{
  "version": 2,
  "snapshotFingerprint": "8f13c6...",
  "prefabAssetPath": "Assets/UI/RewardPanel.prefab",
  "output": {
    "mode": "in_place",
    "assetPath": "Assets/UI/RewardPanel.prefab"
  },
  "prefabName": "RewardPanelView",
  "wrappers": [
    { "id": "content", "parent": "node:n000001", "name": "[Content]", "siblingIndex": 0 }
  ],
  "moves": [
    { "source": "node:n000014", "destination": "@content", "siblingIndex": 0 }
  ],
  "renames": [],
  "emptyContainerRemovals": [],
  "tightBounds": [{ "target": "@content" }],
  "textureRenames": [],
  "spriteAtlasRenames": [],
  "componentFamilyDecisions": [],
  "containmentResolutions": [],
  "flatSiblingResolutions": [],
  "componentExtractions": [],
  "stateComponentExtractions": [],
  "variantComponentExtractions": [],
  "statefulComponentExtractions": [],
  "postGroupingExtractionIntents": [],
  "verify": {}
}
```

The following fields are existing-node references and therefore must use `node:<id>` in a chat plan:

- `wrappers[].parent`, unless it is an earlier `@wrapperId`;
- `moves[].source` and `moves[].destination`, with `destination` also allowing `@wrapperId`;
- `renames[].target`, with `@wrapperId` allowed;
- `emptyContainerRemovals[].source`;
- `tightBounds[].target`, with `@wrapperId` allowed;
- `componentFamilyDecisions[].parent` and every entry in `sources`;
- `componentExtractions[].template` and every entry in `instances`;
- every `template`, `common.source`, `states[].source`, and `instances[].source` in state, variant, and stateful extraction contracts;
- `containmentResolutions[].source`, and `newParent` unless it is an `@wrapperId`.

Asset paths, output verification paths, new semantic names, state/member names, and wrapper IDs are not node references. Keep their existing schema below. Never invent a node ID, derive one from a GameObject name, or emit a raw pre-apply hierarchy path in an existing-node reference. If the intended object cannot be proven from the supplied snapshot, omit that operation and report the ambiguity in the review.

Every plan-owned ID, including `wrappers[].id`, extraction `id`, and state `id`, must be lower snake_case matching `^[a-z][a-z0-9_]*$`. Use `screen_root`, `day_markers`, or `task_in_progress`; do not use PascalCase, kebab-case, spaces, brackets, or an `@` prefix. The `@` prefix is reserved only for a reference to an earlier wrapper, for example `@screen_root`.

For `output.mode: "in_place"`, the Prefab root is asset identity, not a semantic naming target. A chat plan must not rename the snapshot root to a name different from `Path.GetFileNameWithoutExtension(prefabAssetPath)`, and every post-apply verification path must begin with that same asset-compatible root name. `prefabName` does not override this rule.

Before validation or preflight, the Unity window verifies the snapshot fingerprint, requires every existing-node reference to be an exact `node:<id>` from that snapshot, and rejects unknown IDs, raw paths and unsupported non-empty operation arrays. It does **not** rewrite the reviewed plan: there is no conversion into an internal version 1 runner plan, no `requiredComponentFamilies` / `containmentFindings` / `flatSiblingFindings` derivation, and no `expectedGuid` injection. The reviewed JSON text itself is fingerprinted, preflighted in memory and applied.

Approval binds the complete reviewed JSON. If Unity returns `rejected`, nothing was written, but the approved request is finished: do not edit its plan or write another `.apply`. Any correction is a complete new plan and review under a new request and requires fresh explicit human approval. A `partial` or `uncertain` result also requires verification of the on-disk Prefab before a new review.

### Post-Grouping Extraction Intent

`postGroupingExtractionIntents` is a Unity AI chat review field. Use it when the
confirmed hierarchy operations create repeated-unit roots that cannot be valid
extraction sources in the current snapshot. It freezes the child-Prefab work shown
in the first review so the refreshed second-stage plan can be applied without a
second user confirmation.

```json
{
  "postGroupingExtractionIntents": [
    {
      "id": "task_item",
      "mode": "stateful",
      "assetPath": "Assets/UI/Prefab/Common/TaskItem.prefab",
      "templatePath": "TaskView/[TaskList]/[TaskItem_1]",
      "commonMembers": ["TaskLabel", "TaskValue"],
      "instances": [
        {
          "path": "TaskView/[TaskList]/[TaskItem_1]",
          "state": "in_progress",
          "commonSourceNames": ["TaskLabel", "TaskValue"],
          "stateSourceNames": ["ProgressBackground"]
        },
        {
          "path": "TaskView/[TaskList]/[TaskItem_2]",
          "state": "claimable",
          "commonSourceNames": ["TaskLabel", "TaskValue"],
          "stateSourceNames": ["ClaimButton"]
        }
      ],
      "states": [
        {
          "id": "in_progress",
          "name": "[State_InProgress]",
          "sourcePath": "TaskView/[TaskList]/[TaskItem_1]",
          "members": ["ProgressBackground"]
        },
        {
          "id": "claimable",
          "name": "[State_Claimable]",
          "sourcePath": "TaskView/[TaskList]/[TaskItem_2]",
          "members": ["ClaimButton"]
        }
      ],
      "defaultState": "in_progress"
    }
  ]
}
```

`mode` is exactly `component`, `state`, `variant`, or `stateful`. `templatePath`
and every `instances[].path`/`states[].sourcePath` are complete normalized paths
in the expected post-grouping hierarchy; leaf names are insufficient. `instances`
preserves reviewed source-path order; use an empty `state` for `component`, and
record the selected state for the other modes. `states` preserves the reviewed
state ID/name/member order and is empty for `component`. `commonMembers` records
the ordered reviewed Common member names and is empty when the mode has no Common
contract. Every instance includes ordered `commonSourceNames` and
`stateSourceNames`, using empty arrays when the mode has no such mapping.
`defaultState` is empty for `component` and otherwise matches the reviewed
extraction contract. Every instance state must reference a declared state ID.

An empty `postGroupingExtractionIntents` array is valid only after an unsaved
simulation of the complete hierarchy stage has been resnapshotted. The
simulation must show no required component family and no reviewed reusable
family. If it creates a candidate that requires extraction, that extraction's
complete intent must be present here before the user can confirm; it cannot be
discovered and added after confirmation.

A mandatory (`requiresExtraction: true`) candidate that only becomes extractable
after the grouping must still be published exactly once in `componentFamilyDecisions`.
Its `parent` and `sources` must exactly match the snapshot candidate. `recommendedMode`
is advisory only. Its `mode` must be `component`, `state`, `variant`, or `stateful`
and must match the actual extraction list or `postGroupingExtractionIntents` entry
named by `extractionId`. Unity accepts that deferral, then verifies that
the rebuilt extraction sources completely cover the refreshed candidate sources
before executing the second stage. A missing, duplicate, wrong-mode, wrong-parent,
wrong-source, or incomplete-coverage decision in the reviewed first-stage contract is refused
before any write. A mismatch found only against the refreshed second-stage candidate leaves the
first stage saved and returns `partial`.

After the first apply, Unity resolves every intent path against the
refreshed authoritative snapshot, rebuilds the second-stage plan (including the
`componentFamilyDecisions` entries required by the refreshed snapshot) and applies
it in the same confirmed workflow. A path that no longer exists, an unresolvable
mode, or a required candidate the reviewed intents do not cover is blocking: the
first stage stays saved, the result is reported as `partial`, and the user must
re-analyze the current Prefab.

## Internal Runner Plan (Version 1)

The bundled PowerShell/Python runner used to execute version 1 path plans. Its `apply` and
`reapply` modes are **retired** (ADR 0001/0002): the script now refuses them, and no Unity code
path calls it. Version 1 plans are therefore read-only diagnostics; formal writes happen only
through a version 2 plan applied by the Unity shared core. This section remains as the operation
shape reference, but its path-based references do **not** apply to version 2 chat plans, which
must use `node:<id>`.

### Required Fields

```json
{
  "version": 1,
  "prefabAssetPath": "Assets/UI/RewardPanel.prefab",
  "output": {
    "mode": "in_place",
    "assetPath": "Assets/UI/RewardPanel.prefab"
  },
  "prefabName": "RewardPanelView",
  "wrappers": [],
  "moves": [],
  "renames": [],
  "emptyContainerRemovals": [],
  "tightBounds": [],
  "textureRenames": [],
  "spriteAtlasRenames": [],
  "componentFamilyDecisions": [],
  "containmentResolutions": [],
  "flatSiblingResolutions": [],
  "componentExtractions": [],
  "stateComponentExtractions": [],
  "variantComponentExtractions": [],
  "statefulComponentExtractions": [],
  "verify": {}
}
```

`output.mode` must be `in_place`, and `output.assetPath` must exactly equal `prefabAssetPath`. This cleanup never creates a `.cleaned.prefab`, duplicate, or replacement for the target Prefab.

All asset paths are project-relative paths beginning with `Assets/`. For a direct version 1 runner plan, `prefabName` must use PascalCase and end with `View` when Texture or SpriteAtlas assets are renamed. In a version 2 plan the field stays required for schema stability only: the current executor never derives or trusts it for private-asset work.

### Operations

`wrappers` are created in order. `parent` and every `source` path refer to the pre-apply Prefab tree, including the Prefab root name. Prefix a previously created wrapper ID with `@` when it is the parent or target.

Every wrapper `id` must be lower snake_case matching `^[a-z][a-z0-9_]*$`; `screen_root` is valid, while `[Screen]`, `ScreenRoot`, `screen-root`, and `@screen_root` are invalid IDs. Use the `@` prefix only when referencing a previously created wrapper.

An `@` reference names only a wrapper root: it must be exactly `@wrapperId`, never `@wrapperId/Child`. Existing nodes must always use their original, full pre-apply Prefab path in `moves.source`, `renames.target`, and `emptyContainerRemovals.source`, even when the operation later moves that node into a wrapper. `tightBounds.target` may use an exact wrapper-root reference or an existing pre-apply path. The renderer resolves existing nodes before it creates wrappers and applies moves, so post-move paths are not valid plan inputs.

For direct internal runner plans, every source-tree path is an identity contract and must be copied exactly from the Unity snapshot, including its original sibling name and duplicate occurrence marker where applicable. Do not infer a GameObject name from a `TextMeshProUGUI.text` value, a Sprite name, or a visual label. A version 2 plan never contains these paths: it uses `node:<id>` references, which the Unity core binds against the authoritative snapshot at apply time.

```json
{
  "wrappers": [
    { "id": "content", "parent": "RewardPanel/Root", "name": "[Content]", "siblingIndex": 0 }
  ],
  "moves": [
    { "source": "RewardPanel/Root/Title", "destination": "@content", "siblingIndex": 0 }
  ],
  "renames": [
    { "target": "RewardPanel/Root", "name": "[Screen]" },
    { "target": "@content", "name": "[Content]" }
  ]
}
```

Each move source must be unique. A wrapper cannot overwrite an existing child. Reparenting is performed with world-position preservation. Every wrapper is tightened to the exact union of its direct child `RectTransform` bounds after all moves; use `tightBounds` to also tighten existing semantic containers.

`emptyContainerRemovals` is intentionally narrow: use it only to remove an existing structural grouping container after every current direct child is either moved out or removed by an earlier entry in the same list. Nested removals must be ordered child before parent. The read-only preflight creates the planned wrappers, performs the planned moves, and attempts the removals on an unsaved Prefab instance; it reports every removal that would remain non-empty, including its remaining direct child paths, before the plan can be confirmed. The runner also rejects any target with a component other than `Transform` or a target referenced elsewhere in the Prefab. Pair every removal with `verify.absentPaths` so the saved hierarchy proves that a former flat type/layer group was not left behind.

```json
{
  "emptyContainerRemovals": [
    { "source": "RewardPanel/[LegacyLabels]" }
  ]
}
```

For repeated foreground units, keep area-scale backgrounds in a sibling region group. For example, use `[MapSectors] / SectorBackground_*` beside `[MapMarkers] / [MapMarker_*] / MarkerBackground + MarkerIcon`; do not include a map sector in the marker wrapper just because it is spatially adjacent.

Bind repeated labels, badges, counters, and interaction targets to their matching repeated unit when geometry and cardinality agree. Do not put four marker labels into a generic navigation group solely because they are text.

```json
{
  "tightBounds": [
    { "target": "@content" },
    { "target": "RewardPanel/[Screen]" }
  ]
}
```

Use inner-to-outer order so nested groups are tightened before their parents. When omitted, every new wrapper is tightened automatically. **In this project's NativeUnity engine that is NOT true:**
a new wrapper keeps `sizeDelta = 0` unless it is listed explicitly. Always emit `tightBounds` for every new
wrapper, inner -> outer (see SKILL.md -> Project-verified engine realities, R1).

## Component Family Decisions

`componentFamilyDecisions` is required in every new plan created through this skill, including ordinary hierarchy-only plans. It makes the reusable-component decision reviewable instead of allowing a repeated family to disappear behind empty extraction arrays. The renderer accepts legacy v1 plans without the field so previously saved plans remain runnable. Names in this document are examples only; the schema is data-driven and applies to every Prefab type.

```json
{
  "componentFamilyDecisions": [
    {
      "parent": "InventoryPanelView/[ItemList]",
      "sources": [
        "InventoryPanelView/[ItemList]/[Item_01]",
        "InventoryPanelView/[ItemList]/[Item_02]"
      ],
      "mode": "skip",
      "reason": "The two units have project-owned bindings that must remain local."
    },
    {
      "parent": "InventoryPanelView/[ItemList]",
      "sources": [
        "InventoryPanelView/[ItemList]/[Item_03]",
        "InventoryPanelView/[ItemList]/[Item_04]"
      ],
      "mode": "stateful",
      "extractionId": "day_marker",
      "reason": "The repeated markers differ only by explicit visual state and instance values."
    }
  ]
}
```

`parent`, at least two unique `sources`, `mode`, and `reason` are required. Use `skip` only with a concrete preservation or safety reason. For every other mode, `extractionId` must reference exactly one matching extraction, and the declared sources must cover that extraction exactly. Valid modes are `component`, `state`, `variant`, and `stateful`. In a Unity AI chat plan, every snapshot candidate decision must also include its exact `candidateId`; a candidate marked `requiresExtraction: true` cannot use `skip`.

A numbered family whose members do not all share one recursive structure additionally reports `numbered_structure_subset` candidates, one per structure bucket, each carrying `familyCandidateId` and the members of that bucket. A bucket with at least two members recommends `component`; a lone member recommends `skip` and sets `requiresExtraction: false`, because it has no peer to share a Prefab with. A subset is never forced while its own family is already required, since both claim the same sources and only one decision may own a source. Choosing both boundaries for the same member fails validation with a duplicate-instance error.

### Required Component Families

`requiredComponentFamilies` carries the authoritative snapshot candidates that must be extracted, so the shared plan validator enforces the same rule the Unity AI chat path enforces. **Not executable yet.** Unity used to write this field when it converted a version 2 node-ID plan into a version 1 runner plan; that conversion is retired, so nothing derives it now. Keep extraction arrays empty and do not author this field by hand.

```json
{
  "requiredComponentFamilies": [
    {
      "candidateId": "family_001",
      "parent": "InventoryPanelView/[ItemList]",
      "sources": [
        "InventoryPanelView/[ItemList]/[Item_03]",
        "InventoryPanelView/[ItemList]/[Item_04]"
      ]
    }
  ]
}
```

Each entry needs `candidateId`, `parent`, and at least two unique `sources`. Every entry must be matched by one `componentFamilyDecisions` entry covering the same source set in any order and declaring the same `parent`; that decision may not use `skip`. The field is absent when the snapshot has no forced candidate, and legacy v1 plans without it stay runnable.

### Geometry Containment

`containmentFindings` carries measured geometry, not opinion: Unity compares the world rectangles of two numbered repeated families and records the cases where every member of the inner family sits fully inside a distinct member of the outer family. **Not executable yet.** Unity used to write this field during the retired version 2 → version 1 conversion; nothing derives it now. Do not author it by hand.

```json
{
  "containmentFindings": [
    {
      "innerCandidateId": "family_002",
      "innerParent": "ParkourView/[TopCoinDisplays]",
      "mapping": [
        {
          "source": "ParkourView/[TopCoinDisplays]/[CoinDisplay_1]",
          "containedBy": "ParkourView/[MainContent]/[StoryCard_1]"
        }
      ]
    }
  ]
}
```

Every finding member needs one `containmentResolutions` entry, which the AI does author:

```json
{
  "containmentResolutions": [
    {
      "source": "ParkourView/[TopCoinDisplays]/[CoinDisplay_1]",
      "mode": "reparent",
      "newParent": "ParkourView/[MainContent]/[StoryCard_1]"
    }
  ]
}
```

`source` and `mode` are always required. `mode: "reparent"` needs `newParent`, which must be the containing node from the finding or a descendant of it, or a wrapper reference starting with `@`. `mode: "keep"` needs `evidence` of at least 20 characters explaining why a node that is geometrically inside a repeated unit still belongs outside it — a shared layout group, an animation driver, or a region-scale background are the usual reasons. Duplicate sources are rejected. A finding with no resolution is a hard error, so a plan cannot silently repeat the misgrouping the measurement found.

### Flat Sibling Visual Clusters

`flatSiblingFindings` is another Unity-written internal field. It is emitted only when direct leaf siblings have consecutive source order and the first layer fully contains at least two following layers at a small area ratio. An explicit structural parent named `[... ]` is already a resolved boundary and is excluded, so an existing `[DayItem_1]` or `[TimedRewardPanel]` never requires a second wrapper around its leaves. This is a conservative signal that a background, counter, timer, or button layers were left flat by the PSD source hierarchy. Do not author `flatSiblingFindings` in a chat plan.

```json
{
  "flatSiblingFindings": [
    {
      "id": "flat_sibling_001",
      "parent": "RewardPanel/Root",
      "background": "RewardPanel/Root/ui_daily_bt1",
      "members": [
        "RewardPanel/Root/ui_daily_bt1",
        "RewardPanel/Root/Timer",
        "RewardPanel/Root/4d23h",
        "RewardPanel/Root/ui_daily_bt2"
      ]
    }
  ]
}
```

Every finding needs exactly one `flatSiblingResolutions` entry in the chat plan. Its wrapper ID is deterministic:

```json
{
  "flatSiblingResolutions": [
    {
      "findingId": "flat_sibling_001",
      "mode": "group",
      "wrapperId": "flat_sibling_001_group"
    }
  ]
}
```

`mode` must be `"group"`, and `wrapperId` must equal `<findingId>_group`. **Not executable yet.** Unity no longer removes AI-authored operations or derives a wrapper for a finding, and the current executor rejects any non-empty `flatSiblingResolutions` before writing. Keep the array empty and describe the grouping you would apply in the review text. An existing semantic container such as `[BottomBar]` is still not an acceptable substitute for the derived wrapper.

The derived members are a minimum set, not proof of complete visual membership. Before the plan is confirmable, audit the complete sibling sequence around each finding for same-slot labels, counters, locks, status icons, and status values. Every proven additional member must be included through an explicit move into the same wrapper and in `verify.directChildren`; otherwise the review must report the ambiguity and not claim a complete grouped unit.

## Optional Component Extraction

The `componentExtractions`, `stateComponentExtractions`, `variantComponentExtractions`, and `statefulComponentExtractions` fields may appear together with wrappers, moves, renames, and tight bounds in the one plan the user explicitly approves. They may create reusable components directly under the target Prefab's sibling `Common` directory, but do not change the rule that the main target Prefab is saved in place at `prefabAssetPath`. Do not include them unless the reviewed request explicitly calls for a reusable, state, variant, or stateful component.

### Shared Component Extraction

Each entry creates one shared nested Prefab from `template` and replaces every `instances` entry with an instance of that asset. The template must be included in `instances`. The runner captures every approved source before hierarchy moves, so non-overlapping extractions can be reviewed and applied together with hierarchy cleanup.

Use `scripts/find_prefab_component_candidates.py` only to discover candidate families. It reports matching recursive signatures and high-confidence same-parent numbered families with matching anchors and pivot; numbered candidates may differ in `sizeDelta` or state structure and therefore recommend `stateful`. The report cannot prove absence of external serialized references. The Unity apply pass is authoritative for that check.

```json
{
  "componentExtractions": [
    {
      "id": "content_card",
      "template": "RewardPanel/[Content]/[ContentCard_1]",
      "assetPath": "Assets/UI/Prefab/Common/ContentCard.prefab",
      "instances": [
        "RewardPanel/[Content]/[ContentCard_1]",
        "RewardPanel/[Content]/[ContentCard_2]",
        "RewardPanel/[Content]/[ContentCard_3]"
      ]
    }
  ]
}
```

Use this only when all listed units have the same recursive component/child signature. Sprite, text, color, active state, and RectTransform differences become nested-instance overrides. The runner rejects source units with nested Prefabs or external serialized references, overwrites the declared component asset path with the current extraction, preserves RectTransform world corners, and verifies every final instance points to `assetPath`.

The component asset root is named from the output filename (for example `ContentCard.prefab` has a `ContentCard` root); every original instance name is preserved as an instance override. Every component `assetPath` must be a PascalCase `.prefab` directly under the target Prefab's sibling `Common` directory. A plan may contain multiple families and extraction modes only when no source or instance paths overlap or nest.

### Stateful Component Extraction

Use `stateComponentExtractions` when several **direct sibling roots occupy one visual slot** but represent mutually exclusive states of one logical component. This collapses those roots into one nested Prefab instead of producing one nested instance per source.

```json
{
  "stateComponentExtractions": [
    {
      "id": "inventory_item",
      "template": "InventoryPanelView/[ItemStates]/Item_01",
      "assetPath": "Assets/UI/Prefab/Common/InventoryItem.prefab",
      "defaultState": "available",
      "states": [
        { "id": "locked", "source": "InventoryPanelView/[ItemStates]/Item_01", "name": "[Locked]" },
        { "id": "available", "source": "InventoryPanelView/[ItemStates]/Item_02", "name": "[Available]" },
        { "id": "completed", "source": "InventoryPanelView/[ItemStates]/Item_03", "name": "[Completed]" }
      ]
    }
  ]
}
```

All `states[].source` paths must be direct siblings of `template`; `template` must be one of them. The generated root uses the output file name, such as `InventoryItem`, and contains a `[States]` child with the state names in the supplied order. Only `defaultState` is active in the saved component; branch selection at runtime remains outside this skill.

State branches may have different recursive signatures, but the sources must be visually overlapping and semantically mutually exclusive. Do not use this for simultaneously visible list entries. It rejects nested source Prefabs, external serialized references, or source-path overlap, and overwrites the declared output asset path with the current extraction. A state extraction adds the component root and its `[States]` container to the final hierarchy, so update optional node/component counts in `verify` by two for each extracted state component.

### Variant List Component Extraction

Use `variantComponentExtractions` only when several rows are visible at different list positions, represent one logical component, and have at least two distinct observed visual states. It creates one shared Prefab and replaces every listed row with a nested instance. It does **not** collapse the rows into a single visible object. When every visible row has one observed state, use `componentExtractions` instead; do not invent a second state to satisfy this schema.

```json
{
  "variantComponentExtractions": [
    {
      "id": "inventory_item",
      "template": "InventoryPanelView/[ItemList]/[Item_01]",
      "assetPath": "Assets/UI/Prefab/Common/InventoryItem.prefab",
      "commonName": "[Common]",
      "statesName": "[States]",
      "defaultState": "in_progress",
      "states": [
        { "id": "in_progress", "source": "InventoryPanelView/[ItemList]/[Item_01]", "name": "[State_InProgress]" },
        { "id": "claimable", "source": "InventoryPanelView/[ItemList]/[Item_02]", "name": "[State_Claimable]" },
        { "id": "locked", "source": "InventoryPanelView/[ItemList]/[Item_03]", "name": "[State_Locked]" }
      ],
      "instances": [
        { "source": "InventoryPanelView/[ItemList]/[Item_01]", "name": "[Item_01]", "state": "in_progress" },
        { "source": "InventoryPanelView/[ItemList]/[Item_02]", "name": "[Item_02]", "state": "claimable" },
        { "source": "InventoryPanelView/[ItemList]/[Item_03]", "name": "[Item_03]", "state": "locked" }
      ]
    }
  ]
}
```

`states[].source` must be direct siblings of `template` and contain one representative row for each unique visual state. `instances` must contain every visible repeated row exactly once; multiple instance rows may select the same state through `instances[].state`. Every state representative source must appear once in `instances`, but an instance source does not need to be a state representative. The output root has direct `[Common]` and `[States]` children. Move only members proven common to every state into `[Common]`; leave it empty if no such proof exists. The runner normalizes each state root to the component origin, preserves each instance's original list position, and activates exactly `instances[].state`. It can be combined with other non-overlapping extraction modes and hierarchy changes in the reviewed plan.

### Stateful Repeated Component Extraction

Use `statefulComponentExtractions` for repeated items that contain real shared content plus a small number of visual states. It creates one shared nested Prefab, moves the reviewed shared members into `[Common]`, creates one state branch per visual state, and replaces every source item with an instance of that asset. `[States]` is created before `[Common]`, preserving the expected UI draw order for shared labels over state backgrounds.

```json
{
  "statefulComponentExtractions": [
    {
      "id": "inventory_item",
      "template": "InventoryPanelView/[ItemList]/[Item_01]",
      "assetPath": "Assets/UI/Prefab/Common/InventoryItem.prefab",
      "common": {
        "source": "InventoryPanelView/[ItemList]/[Item_01]",
        "members": [
          { "sourceName": "ItemLabel", "name": "ItemLabel" },
          { "sourceName": "ItemValue", "name": "ItemValue" }
        ]
      },
      "states": [
        {
          "id": "available",
          "source": "InventoryPanelView/[ItemList]/[Item_01]",
          "name": "[State_Available]",
          "members": [
            { "sourceName": "ItemBackground", "name": "AvailableBackground" }
          ]
        },
        {
          "id": "locked",
          "source": "InventoryPanelView/[ItemList]/[Item_03]",
          "name": "[State_Locked]",
          "members": [
            { "sourceName": "ItemBackground", "name": "LockedBackground" },
            { "sourceName": "ItemLock", "name": "LockIcon" }
          ]
        }
      ],
      "defaultState": "available",
      "instances": [
        {
          "source": "InventoryPanelView/[ItemList]/[Item_01]",
          "name": "[Item_01]",
          "state": "available",
          "commonSourceNames": ["ItemLabel", "ItemValue"],
          "stateSourceNames": ["ItemBackground"]
        },
        {
          "source": "InventoryPanelView/[ItemList]/[Item_03]",
          "name": "[Item_03]",
          "state": "locked",
          "commonSourceNames": ["ItemLabel", "ItemValue"],
          "stateSourceNames": ["ItemBackground", "ItemLock"]
        }
      ]
    }
  ]
}
```

`common.members` specifies the reusable `[Common]` contract. Each state specifies its branch members. A state may use an empty `members` array only for an explicit all-common state: every direct child of each instance using that state must be covered by `commonSourceNames`, and its `stateSourceNames` must be `[]`. An empty branch never permits an unmapped child or an invented placeholder state. Each instance otherwise maps all its direct members using `commonSourceNames` and `stateSourceNames`; the runner rejects an unmapped or duplicated child. **Not executable yet.** The former version 2 chat conversion that could rebuild an incomplete, duplicated, or invalid Common/State instance list from the authoritative snapshot is retired, and the current executor does not execute stateful extraction at all. The missing side is the ordered direct-child complement, and the final counts must exactly equal `common.members` plus the selected state's `members`. This conversion cannot invent a member or bypass a structural mismatch. Direct version 1 runner plans still require both complete explicit lists. Stateful extraction can be combined with other non-overlapping extraction modes and hierarchy operations. It rejects nested Prefabs, external references, or incomplete member mapping, and overwrites the declared output asset path with the current extraction.

## Private Asset Renames

List only assets proven private to the current Prefab. `toName` has no extension. Every Texture `toName` must use the exact `PrefabName_` prefix, and every SpriteAtlas `toName` must equal that same `PrefabName`.

```json
{
  "textureRenames": [
    {
      "from": "Assets/UI/RewardPanel/Texture/old_background.png",
      "toName": "RewardPanelView_Background",
      "expectedGuid": "00000000000000000000000000000000"
    }
  ],
  "spriteAtlasRenames": [
    {
      "from": "Assets/UI/RewardPanel/Atlas/old.spriteatlas",
      "toName": "RewardPanelView",
      "expectedGuid": "00000000000000000000000000000000"
    }
  ]
}
```

For a direct version 1 runner plan, read each `expectedGuid` from Unity before presenting the plan. **Executable now (v2):** Unity validates each `from` asset, captures its current `AssetDatabase` GUID when `expectedGuid` is empty, rejects a mismatch, refuses an existing target path, performs the rename (which keeps the asset identity, so Prefab references stay valid) and re-checks the GUID afterwards. Renames run before the hierarchy operations and the final save; a mid-flight failure reports which renames already happened instead of silently redoing or rolling back. Do not add shared assets to this list.

**Not executable yet.** No version 2 conversion happens any more, so Unity does not derive an internal `prefabName` from reviewed `toName` values. Each Texture contributes the substring before its first underscore; each SpriteAtlas contributes its full `toName`. All candidates must be identical and match `^[A-Z][A-Za-z0-9]*View$`. The version 2 `prefabName` field is not used to override those reviewed targets. If the candidates conflict, a Texture lacks the required underscore, or the common candidate is invalid, conversion stops before the external runner and reports the submitted `prefabName`, every candidate, and each indexed `toName`. Direct version 1 runner plans remain explicit and are not normalized.

When the full private Texture directory belongs to this Prefab, list every Texture in it. Set `verify.privateTextureDirectory`, `verify.requireAllPrivateTextureAssetsPrefixed`, and `verify.texturePathPrefix` so the final verification rejects any residual non-prefixed file.

## Verification Contract

Use counts captured during the read-only snapshot. **Exception for extraction stages** (`componentExtractions`,
`stateComponentExtractions`, `variantComponentExtractions`, `statefulComponentExtractions`): the saved tree expands,
so the counts must be the post-expansion expectation — otherwise the apply saves first and then reports
`VERIFY_WARN issue=nodes expected=.. actual=..`. Omit them or re-snapshot first (SKILL.md R2). `hierarchy` paths are post-apply paths and make the final tree reviewable after a timeout.

```json
{
  "verify": {
    "nodes": 12,
    "components": 30,
    "objectReferences": 18,
    "missingComponents": 0,
    "images": 4,
    "prefixedTextures": 4,
    "texturePathPrefix": "Assets/UI/RewardPanel/Texture/RewardPanelView_",
    "requireAllImageTexturesPrefixed": true,
    "requireEnglishNames": true,
    "forbiddenObjectNamePatterns": [
      "^(?:\\d+|\\+|img_|ui_)",
      "^\\d+(?:_\\d+)?$"
    ],
    "allowedMissingImagePathPrefixes": [
      "RewardPanel/LegacyNestedPrefab/"
    ],
    "privateTextureDirectory": "Assets/UI/RewardPanel/Texture",
    "requireAllPrivateTextureAssetsPrefixed": true,
    "tightBounds": [
      { "path": "RewardPanel/[Screen]/[Content]" }
    ],
    "hierarchy": [
      { "path": "RewardPanel/[Screen]", "childCount": 1 },
      { "path": "RewardPanel/[Screen]/[Content]", "childCount": 2 }
    ],
    "absentPaths": [
      "RewardPanel/[LegacyLabels]"
    ],
    "directChildren": [
      {
        "path": "RewardPanel/[Screen]/[Content]/[Item_1]",
        "children": ["ItemFrame", "ItemIcon", "ItemLabel"]
      }
    ]
  }
}
```

`directChildren` is optional for unrelated containers, but required for every repeated unit whose semantic membership is changed. It verifies exact direct-child names and sibling order after saving.

`absentPaths` is an optional string list. Use it with `emptyContainerRemovals` to prove the old grouping container is gone after saving.

`forbiddenObjectNamePatterns` is a regex list checked against every GameObject name. Use it to reject PSD/export tokens, UUID names, punctuation-only names, and literal text values. `allowedMissingImagePathPrefixes` is an explicit exception list for known inherited nested-Prefab images with intentionally missing Sprite references; do not use it to silence missing Sprites in the target Prefab.

The runner reports missing Sprite references found inside unchanged nested Prefab instances in `ignoredNestedMissingSpritePaths`; they do not fail outer-Prefab hierarchy validation. Missing Sprites owned by the target Prefab are reported as target-owned verification issues unless explicitly listed in `allowedMissingImagePathPrefixes`; they must not be silently treated as inherited nested-Prefab issues.

Post-save verification distinguishes a saved asset from a completed workflow. A contract mismatch returns `VERIFY_WARN issue=...`; the caller must display it and stop before any automatic next stage. The target cannot load, an operation precondition fails, a hierarchy mutation fails, or saving fails remain hard failures. `VERIFY_WARN` is not completion proof.

Run `-VerifyOnly` after an uncertain or timed-out apply. It does not mutate assets; it checks the saved Prefab, final hierarchy, asset GUIDs, Texture paths, and SpriteAtlas paths against this contract. It cannot reconstruct a pre-apply world-corner baseline, so the apply pass is responsible for the `0.01` world-corner invariant.
