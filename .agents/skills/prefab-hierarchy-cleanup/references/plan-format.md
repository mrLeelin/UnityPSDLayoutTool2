# Plan Format

Use one UTF-8 JSON plan per cleanup operation. Treat it as the reviewed execution contract.

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
  "componentExtractions": [],
  "stateComponentExtractions": [],
  "variantComponentExtractions": [],
  "statefulComponentExtractions": [],
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
- every `template`, `common.source`, `states[].source`, and `instances[].source` in state, variant, and stateful extraction contracts.

Asset paths, output verification paths, new semantic names, state/member names, and wrapper IDs are not node references. Keep their existing schema below. Never invent a node ID, derive one from a GameObject name, or emit a raw pre-apply hierarchy path in an existing-node reference. If the intended object cannot be proven from the supplied snapshot, omit that operation and report the ambiguity in the review.

Before validation or apply, the Unity window verifies the snapshot fingerprint, resolves every node ID to the exact original path, rejects unknown IDs and raw paths, then writes a temporary internal version 1 runner plan. The AI must never emit that internal plan.

## Internal Runner Plan (Version 1)

The bundled PowerShell/Python runner remains compatible with existing version 1 path plans. This section defines its operation shapes and is also the shape reference for version 2 chat plans. Direct script callers use version 1; the Unity AI chat window performs the conversion automatically.

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
  "componentExtractions": [],
  "stateComponentExtractions": [],
  "variantComponentExtractions": [],
  "statefulComponentExtractions": [],
  "verify": {}
}
```

`output.mode` must be `in_place`, and `output.assetPath` must exactly equal `prefabAssetPath`. This cleanup never creates a `.cleaned.prefab`, duplicate, or replacement for the target Prefab.

All asset paths are project-relative paths beginning with `Assets/`. `prefabName` must use PascalCase and end with `View` when Texture or SpriteAtlas assets are renamed.

### Operations

`wrappers` are created in order. `parent` and every `source` path refer to the pre-apply Prefab tree, including the Prefab root name. Prefix a previously created wrapper ID with `@` when it is the parent or target.

An `@` reference names only a wrapper root: it must be exactly `@wrapperId`, never `@wrapperId/Child`. Existing nodes must always use their original, full pre-apply Prefab path in `moves.source`, `renames.target`, and `emptyContainerRemovals.source`, even when the operation later moves that node into a wrapper. `tightBounds.target` may use an exact wrapper-root reference or an existing pre-apply path. The renderer resolves existing nodes before it creates wrappers and applies moves, so post-move paths are not valid plan inputs.

For direct internal runner plans, every source-tree path is an identity contract and must be copied exactly from the Unity snapshot, including its original sibling name and duplicate occurrence marker where applicable. Do not infer a GameObject name from a `TextMeshProUGUI.text` value, a Sprite name, or a visual label. The AI chat window never accepts these paths from the model: it resolves validated version 2 node IDs into this internal form and then runs the same source-path preflight.

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

Use inner-to-outer order so nested groups are tightened before their parents. When omitted, every new wrapper is tightened automatically.

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

`parent`, at least two unique `sources`, `mode`, and `reason` are required. Use `skip` only with a concrete preservation or safety reason. For every other mode, `extractionId` must reference exactly one matching extraction, and the declared sources must cover that extraction exactly. Valid modes are `component`, `state`, `variant`, and `stateful`.

## Optional Component Extraction

The `componentExtractions`, `stateComponentExtractions`, `variantComponentExtractions`, and `statefulComponentExtractions` fields are allowed only in a separate plan that the user explicitly approved. They may create a reusable component under `Prefab/Common`, but do not change the rule that the main target Prefab is saved in place at `prefabAssetPath`. Do not include them unless the reviewed request explicitly calls for a reusable, state, variant, or stateful component.

### Shared Component Extraction

Run extraction in a separate plan after hierarchy cleanup. Each entry creates one shared nested Prefab from `template` and replaces every `instances` entry with an instance of that asset. The template must be included in `instances`.

Use `scripts/find_prefab_component_candidates.py` only to discover candidate families. Its report requires matching recursive signatures and a common parent, but cannot prove absence of external serialized references. The Unity apply pass is authoritative for that check.

```json
{
  "componentExtractions": [
    {
      "id": "content_card",
      "template": "RewardPanel/[Content]/[ContentCard_1]",
      "assetPath": "Assets/UI/Components/ContentCard.prefab",
      "instances": [
        "RewardPanel/[Content]/[ContentCard_1]",
        "RewardPanel/[Content]/[ContentCard_2]",
        "RewardPanel/[Content]/[ContentCard_3]"
      ]
    }
  ]
}
```

Use this only when all listed units have the same recursive component/child signature. Sprite, text, color, active state, and RectTransform differences become nested-instance overrides. The runner rejects source units with nested Prefabs or external serialized references, refuses to overwrite an existing component asset, preserves RectTransform world corners, and verifies every final instance points to `assetPath`.

The component asset root is named from the output filename (for example `ContentCard.prefab` has a `ContentCard` root); every original instance name is preserved as an instance override. A plan may contain multiple families only when none of their instance paths overlap or nest. `componentExtractions` cannot be combined with `wrappers`, `moves`, `renames`, or `tightBounds`; first complete the hierarchy plan, then extract the approved families in a second plan.

### Stateful Component Extraction

Use `stateComponentExtractions` when several **direct sibling roots occupy one visual slot** but represent mutually exclusive states of one logical component. This collapses those roots into one nested Prefab instead of producing one nested instance per source.

```json
{
  "stateComponentExtractions": [
    {
      "id": "inventory_item",
      "template": "InventoryPanelView/[ItemStates]/Item_01",
      "assetPath": "Assets/UI/Components/InventoryItem.prefab",
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

State branches may have different recursive signatures, but the sources must be visually overlapping and semantically mutually exclusive. Do not use this for simultaneously visible list entries. `stateComponentExtractions` cannot be combined with `componentExtractions`, `wrappers`, `moves`, `renames`, or `tightBounds`; use a separate plan. It rejects nested source Prefabs, external serialized references, source-path overlap, and existing output assets. A state extraction adds the component root and its `[States]` container to the final hierarchy, so update optional node/component counts in `verify` by two for each extracted state component.

### Variant List Component Extraction

Use `variantComponentExtractions` when several rows are visible at different list positions but represent one logical component in different visual states. It creates one shared Prefab and replaces every listed row with a nested instance. It does **not** collapse the rows into a single visible object.

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

`states[].source` must be direct siblings of `template`; every state source must appear exactly once in `instances`. The output root has direct `[Common]` and `[States]` children. Move only members proven common to every state into `[Common]`; leave it empty if no such proof exists. The runner normalizes each state root to the component origin, preserves each instance's original list position, and activates exactly `instances[].state`. `variantComponentExtractions` cannot be combined with the other extraction modes or hierarchy changes.

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

`common.members` specifies the reusable `[Common]` contract. Each state specifies its branch members. Each instance maps all its direct members using `commonSourceNames` and `stateSourceNames`; the runner rejects an unmapped or duplicated child. This mode is exclusive with every other extraction mode and hierarchy operation. It rejects nested Prefabs, external references, incomplete member mapping, and an existing output asset.

## Private Asset Renames

List only assets proven private to the current Prefab. `toName` has no extension and must use the exact `PrefabName_` prefix.

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

Read each `expectedGuid` before presenting the plan. The runner checks it before and after `AssetDatabase.RenameAsset`; it fails if a renamed asset does not retain that GUID. This lets `-VerifyOnly` prove the actual saved state after an interrupted apply. Do not add shared assets to this list.

When the full private Texture directory belongs to this Prefab, list every Texture in it. Set `verify.privateTextureDirectory`, `verify.requireAllPrivateTextureAssetsPrefixed`, and `verify.texturePathPrefix` so the final verification rejects any residual non-prefixed file.

## Verification Contract

Use counts captured during the read-only snapshot. `hierarchy` paths are post-apply paths and make the final tree reviewable after a timeout.

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
      "^(?:\\d+|\\+|img_|ui_|daily_)",
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

Post-save verification is diagnostic and non-blocking. A contract mismatch returns `VERIFY_WARN issue=...` so the caller can display and carry the issue into the next cleanup step without treating the Unity command as failed. The target cannot load, an operation precondition fails, a hierarchy mutation fails, or saving the target fails remain hard failures; those conditions have no trustworthy output to continue from. `VERIFY_WARN` is not completion proof and must be included in the next status message or plan.

Run `-VerifyOnly` after an uncertain or timed-out apply. It does not mutate assets; it checks the saved Prefab, final hierarchy, asset GUIDs, Texture paths, and SpriteAtlas paths against this contract. It cannot reconstruct a pre-apply world-corner baseline, so the apply pass is responsible for the `0.01` world-corner invariant.
