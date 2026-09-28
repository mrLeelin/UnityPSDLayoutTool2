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

For `output.mode: "in_place"`, the Prefab root is asset identity, not a semantic naming target. The snapshot marks it with `isPrefabRoot: true`, `renameLocked: true` and `requiredName` (the Prefab file name without extension). Never put the root in `renames[]`, even when its name is Chinese, contains `_psd`, or is not PascalCase; Unity rejects such a plan before confirmation. Every post-apply verification path must begin with that unchanged root name. `prefabName` is not the root name and does not override this rule. To get a different root name, rename the Prefab asset file outside this plan.

Before validation or preflight, the Unity window verifies the snapshot fingerprint, requires every existing-node reference to be an exact `node:<id>` from that snapshot, and rejects unknown IDs, raw paths, a renamed Prefab root, and unsupported non-empty operation arrays. It reports every independent problem in one message. It does **not** rewrite the reviewed plan and derives nothing on your behalf: the reviewed JSON text itself is fingerprinted, preflighted in memory and applied.

The snapshot fingerprint is the hash of the Prefab file. Every successful apply changes it, so always read the current snapshot again before authoring the next plan; a plan carrying an old `snapshotFingerprint` is rejected.

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

## Operations

All asset paths are project-relative paths beginning with `Assets/`. `output.mode` must be `in_place`, and `output.assetPath` must exactly equal `prefabAssetPath`. This cleanup never creates a `.cleaned.prefab`, duplicate, or replacement for the target Prefab.

`wrappers` are created in order. `parent` is an existing `node:<id>` or an earlier `@wrapperId`. An `@` reference names only a wrapper root: it must be exactly `@wrapperId`, never `@wrapperId/Child`. Every existing node keeps its snapshot `node:<id>` in `moves[].source`, `renames[].target`, `emptyContainerRemovals[].source` and `tightBounds[].target`, even when an earlier operation moves it into a wrapper: Unity binds all references against the pre-apply snapshot before it creates wrappers or applies moves.

Copy each ID exactly from the snapshot. Do not infer a node from a `TextMeshProUGUI.text` value, a Sprite name, a visual label, or a guessed hierarchy path.

```json
{
  "wrappers": [
    { "id": "content", "parent": "node:n000001", "name": "[Content]", "siblingIndex": 0 }
  ],
  "moves": [
    { "source": "node:n000014", "destination": "@content", "siblingIndex": 0 }
  ],
  "renames": [
    { "target": "node:n000014", "name": "TitleLabel" },
    { "target": "@content", "name": "[Content]" }
  ]
}
```

Each move source must be unique. A wrapper cannot overwrite an existing child. Reparenting preserves world position.

`renames` must give every remaining internal node in the reviewed scope an English semantic name (only `A-Z a-z 0-9 _ [ ]`; not a numeric value, displayed value, PSD/export token, or UUID-like name). Before confirmation the Unity chat window computes every node's final name from the snapshot plus `renames` and lists every node that is still not semantic, so rename all of them in one complete plan. The Prefab root, nodes inside a nested Prefab instance, removed containers and extracted subtrees are exempt.

`emptyContainerRemovals` is intentionally narrow: use it only to remove an existing structural grouping container after every current direct child is either moved out or removed by an earlier entry in the same list. Nested removals must be ordered child before parent. The read-only preflight creates the planned wrappers, performs the planned moves, and attempts the removals on an unsaved Prefab instance; it reports every removal that would remain non-empty, including its remaining direct children, before the plan can be confirmed. Unity also rejects any target with a component other than `Transform` or a target referenced elsewhere in the Prefab. Pair every removal with `verify.absentPaths`.

```json
{
  "emptyContainerRemovals": [
    { "source": "node:n000030" }
  ]
}
```

For repeated foreground units, keep area-scale backgrounds in a sibling region group. For example, use `[MapSectors] / SectorBackground_*` beside `[MapMarkers] / [MapMarker_*] / MarkerBackground + MarkerIcon`; do not include a map sector in the marker wrapper just because it is spatially adjacent.

Bind repeated labels, badges, counters, and interaction targets to their matching repeated unit when geometry and cardinality agree. Do not put four marker labels into a generic navigation group solely because they are text.

```json
{
  "tightBounds": [
    { "target": "@content" },
    { "target": "node:n000002" }
  ]
}
```

New wrappers are **not** tightened automatically: a new wrapper keeps `sizeDelta = 0` unless it is listed in `tightBounds`. Always emit `tightBounds` for every new wrapper, inner to outer (see SKILL.md, Project-verified engine realities, R1).

## Component Family Decisions

`componentFamilyDecisions` is required in every plan, including ordinary hierarchy-only plans. It makes the reusable-component decision reviewable instead of allowing a repeated family to disappear behind empty extraction arrays. Names in this document are examples only.

```json
{
  "componentFamilyDecisions": [
    {
      "candidateId": "family_001",
      "parent": "node:n000010",
      "sources": ["node:n000011", "node:n000012"],
      "mode": "skip",
      "reason": "The two units have project-owned bindings that must remain local."
    },
    {
      "candidateId": "family_002",
      "parent": "node:n000010",
      "sources": ["node:n000013", "node:n000014"],
      "mode": "stateful",
      "extractionId": "day_marker",
      "reason": "The repeated markers differ only by explicit visual state and instance values."
    }
  ]
}
```

`candidateId`, `parent`, at least two unique `sources`, `mode`, and `reason` are required; `parent` and `sources` must exactly match the snapshot candidate. Use `skip` only with a concrete preservation or safety reason; a candidate marked `requiresExtraction: true` cannot use `skip`. For every other mode, `extractionId` must reference exactly one matching extraction (or `postGroupingExtractionIntents` entry), and that extraction's sources must completely cover the candidate. Valid modes are `component`, `state`, `variant`, and `stateful`.

A numbered family whose members do not all share one recursive structure additionally reports `numbered_structure_subset` candidates, one per structure bucket, each carrying `familyCandidateId` and the members of that bucket. A bucket with at least two members recommends `component`; a lone member recommends `skip` and sets `requiresExtraction: false`, because it has no peer to share a Prefab with. A subset is never forced while its own family is already required, since both claim the same sources and only one decision may own a source. Choosing both boundaries for the same member fails validation with a duplicate-instance error.

### Snapshot Findings

`componentFamilyCandidates`, `containmentFindings`, and `flatSiblingFindings` are measurements written into the snapshot by Unity. Read them; never author them in a plan.

`containmentFindings` records cases where every member of an inner numbered family sits fully inside a distinct member of an outer family. `flatSiblingFindings` records direct leaf siblings with consecutive source order where the first layer fully contains at least two following layers at a small area ratio; an explicit structural parent named `[... ]` is already a resolved boundary and is excluded. `containmentResolutions` and `flatSiblingResolutions` are **not executable** and must stay empty arrays: resolve these findings with ordinary `wrappers` / `moves`, or describe the pending grouping in the review text.

The derived members are a minimum set, not proof of complete visual membership. Audit the complete sibling sequence around each finding for same-slot labels, counters, locks, status icons, and status values. Every proven additional member must be included through an explicit move into the same wrapper and in `verify.directChildren`; otherwise the review must report the ambiguity and not claim a complete grouped unit.

## Optional Component Extraction

The `componentExtractions`, `stateComponentExtractions`, `variantComponentExtractions`, and `statefulComponentExtractions` fields may appear together with wrappers, moves, renames, and tight bounds in the one plan the user explicitly approves. They create reusable components directly under the target Prefab's sibling `Common` directory; the main target Prefab is still saved in place at `prefabAssetPath`. Include them only when the reviewed request calls for a reusable, state, variant, or stateful component. Every source is an existing `node:<id>`. A plan may contain multiple families and extraction modes only when no sources overlap or nest.

Every component `assetPath` must be a new PascalCase `.prefab` directly under the target Prefab's sibling `Common` directory. The component asset root is named from the output file name (for example `ContentCard.prefab` has a `ContentCard` root); every original instance name is preserved as an instance override. Extraction rejects source units with nested Prefabs or external serialized references, preserves RectTransform world corners, and verifies every final instance points to `assetPath`.

### Shared Component Extraction

Each entry creates one shared nested Prefab from `template` and replaces every `instances` entry with an instance of that asset. The template must be included in `instances`.

Use `scripts/find_prefab_component_candidates.py` only to discover candidate families. It reports matching recursive signatures and high-confidence same-parent numbered families with matching anchors and pivot; numbered candidates may differ in `sizeDelta` or state structure and therefore recommend `stateful`. The report cannot prove absence of external serialized references; the Unity apply pass is authoritative for that check.

```json
{
  "componentExtractions": [
    {
      "id": "content_card",
      "template": "node:n000021",
      "assetPath": "Assets/UI/Prefab/Common/ContentCard.prefab",
      "instances": ["node:n000021", "node:n000022", "node:n000023"]
    }
  ]
}
```

Use this only when all listed units have the same recursive component/child signature. Sprite, text, color, active state, and RectTransform differences become nested-instance overrides.

### State Component Extraction

Use `stateComponentExtractions` when several **direct sibling roots occupy one visual slot** but represent mutually exclusive states of one logical component. This collapses those roots into one nested Prefab instead of producing one nested instance per source.

```json
{
  "stateComponentExtractions": [
    {
      "id": "inventory_item",
      "template": "node:n000031",
      "assetPath": "Assets/UI/Prefab/Common/InventoryItem.prefab",
      "defaultState": "available",
      "states": [
        { "id": "locked", "source": "node:n000031", "name": "[Locked]" },
        { "id": "available", "source": "node:n000032", "name": "[Available]" },
        { "id": "completed", "source": "node:n000033", "name": "[Completed]" }
      ]
    }
  ]
}
```

All `states[].source` nodes must be direct siblings of `template`; `template` must be one of them, at least two states are required, and those sources must not be referenced from outside the extracted states. The generated root contains a `[States]` child with the state names in the supplied order. Only `defaultState` is active in the saved component; branch selection at runtime remains outside this skill. Do not use this for simultaneously visible list entries.

### Variant List Component Extraction

Use `variantComponentExtractions` only when several rows are visible at different list positions, represent one logical component, and have at least two distinct observed visual states. It creates one shared Prefab and replaces every listed row with a nested instance. When every visible row has one observed state, use `componentExtractions` instead; do not invent a second state.

```json
{
  "variantComponentExtractions": [
    {
      "id": "inventory_item",
      "template": "node:n000041",
      "assetPath": "Assets/UI/Prefab/Common/InventoryItem.prefab",
      "commonName": "[Common]",
      "statesName": "[States]",
      "defaultState": "in_progress",
      "states": [
        { "id": "in_progress", "source": "node:n000041", "name": "[State_InProgress]" },
        { "id": "claimable", "source": "node:n000042", "name": "[State_Claimable]" },
        { "id": "locked", "source": "node:n000043", "name": "[State_Locked]" }
      ],
      "instances": [
        { "source": "node:n000041", "name": "[Item_01]", "state": "in_progress" },
        { "source": "node:n000042", "name": "[Item_02]", "state": "claimable" },
        { "source": "node:n000043", "name": "[Item_03]", "state": "locked" }
      ]
    }
  ]
}
```

`states[].source` must be direct siblings of `template` and contain one representative row for each unique visual state. `instances` must contain every visible repeated row exactly once; multiple rows may select the same state. Every state representative must appear once in `instances`, and each instance's structure must match its selected state source. The output root has direct `[Common]` and `[States]` children; move only members proven common to every state into `[Common]`, and leave it empty if no such proof exists. Unity preserves each instance's original list position and activates exactly `instances[].state`.

### Stateful Repeated Component Extraction

Use `statefulComponentExtractions` for repeated items that contain real shared content plus a small number of visual states. It creates one shared nested Prefab, moves the reviewed shared members into `[Common]`, creates one state branch per visual state, and replaces every source item with an instance of that asset. `[States]` is created before `[Common]`, preserving the expected UI draw order for shared labels over state backgrounds.

```json
{
  "statefulComponentExtractions": [
    {
      "id": "inventory_item",
      "template": "node:n000051",
      "assetPath": "Assets/UI/Prefab/Common/InventoryItem.prefab",
      "common": {
        "source": "node:n000051",
        "members": [
          { "sourceName": "ItemLabel", "name": "ItemLabel" },
          { "sourceName": "ItemValue", "name": "ItemValue" }
        ]
      },
      "states": [
        {
          "id": "available",
          "source": "node:n000051",
          "name": "[State_Available]",
          "members": [
            { "sourceName": "ItemBackground", "name": "AvailableBackground" }
          ]
        },
        {
          "id": "locked",
          "source": "node:n000053",
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
          "source": "node:n000051",
          "name": "[Item_01]",
          "state": "available",
          "commonSourceNames": ["ItemLabel", "ItemValue"],
          "stateSourceNames": ["ItemBackground"]
        },
        {
          "source": "node:n000053",
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

`common.members` specifies the reusable `[Common]` contract. Each state specifies its branch members. A state may use an empty `members` array only for an explicit all-common state: every direct child of each instance using that state must be covered by `commonSourceNames`, and its `stateSourceNames` must be `[]`. An empty branch never permits an unmapped child or an invented placeholder state. Every instance maps all its direct members exactly once through `commonSourceNames` and `stateSourceNames`; Unity rejects an unmapped or duplicated child and never completes a missing list for you.

## Private Asset Renames

List only assets proven private to the current Prefab (a Texture referenced by it, or a SpriteAtlas in the Prefab's own folder). `toName` has no extension, and the target path must not exist yet.

`prefabName` **is** the naming prefix Unity enforces, and it is independent of the Prefab asset file name and root name:

- every `textureRenames[].toName` must start with `<prefabName>_`;
- every `spriteAtlasRenames[].toName` must equal `prefabName`.

Choose `prefabName` as the English PascalCase name you want the assets to carry (it should end with `View`, for example `MainScreenView`) and make every `toName` agree with it. Unity compares the prefix verbatim, so a Chinese `prefabName` forces Chinese prefixes; fix a prefix mismatch by making `prefabName` and all `toName` values consistent, not by switching to the Prefab file name.

```json
{
  "prefabName": "RewardPanelView",
  "textureRenames": [
    {
      "from": "Assets/UI/RewardPanel/Texture/old_background.png",
      "toName": "RewardPanelView_Background",
      "expectedGuid": ""
    }
  ],
  "spriteAtlasRenames": [
    {
      "from": "Assets/UI/RewardPanel/Atlas/old.spriteatlas",
      "toName": "RewardPanelView",
      "expectedGuid": ""
    }
  ]
}
```

Leave `expectedGuid` empty so Unity captures the current `AssetDatabase` GUID, or paste an exact GUID to pin it. Unity validates each `from` asset, rejects a GUID mismatch, refuses an existing target path, performs the rename with `AssetDatabase.RenameAsset` (which keeps the asset identity, so Prefab references stay valid) and re-checks the GUID afterwards. Renames run before the hierarchy operations and the final save; a mid-flight failure reports which renames already happened instead of silently redoing or rolling back. One Texture referenced by several nodes gets exactly one entry. Do not add shared assets to this list. When the full private Texture directory belongs to this Prefab, list every Texture in it.

## Verification Contract

`verify` accepts exactly these keys; **any other key is rejected before any write**:

| key | shape | meaning |
|---|---|---|
| `nodes` | non-negative integer | total node count after apply |
| `hierarchy` | `[{ "path", "childCount" }]` | child count of a post-apply path |
| `absentPaths` | `["path", ...]` | post-apply paths that must not exist |
| `directChildren` | `[{ "path", "children": [...] }]` | exact direct-child names in post-apply sibling order |
| `tightBounds` | `[{ "path" }]` | container bounds exactly match its direct children |

Every path is a post-apply hierarchy path that begins with the unchanged Prefab root name. For extraction stages (`componentExtractions`, `stateComponentExtractions`, `variantComponentExtractions`, `statefulComponentExtractions`) the saved tree expands, so `nodes` must be the post-expansion expectation; omit it or take a fresh snapshot first (SKILL.md R2).

```json
{
  "verify": {
    "nodes": 12,
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

`directChildren` is optional for unrelated containers, but required for every repeated unit whose semantic membership is changed. Each entry lists every child name exactly once. `absentPaths` pairs with `emptyContainerRemovals` to prove the old grouping container is gone after saving.

Naming, Texture prefixes and missing Sprites are not `verify` keys: the naming gate and the asset-rename checks above run before confirmation, and the preservation audit (`scripts/audit_prefab_preservation.py`) proves content survival afterwards.

Post-save verification distinguishes a saved asset from a completed workflow. A contract mismatch after the save is reported as `partial`; stop before any automatic next stage and verify the on-disk Prefab. A load failure, an operation precondition failure, a hierarchy mutation failure, or a save failure is a hard failure. A `partial` or `uncertain` result is never completion proof.
