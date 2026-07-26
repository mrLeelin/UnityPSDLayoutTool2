# Plan Format

Use one UTF-8 JSON plan per cleanup operation. Treat it as the reviewed execution contract.

## Required Fields

```json
{
  "version": 1,
  "prefabAssetPath": "Assets/UI/RewardPanel.prefab",
  "output": {
    "mode": "copy",
    "assetPath": "Assets/UI/RewardPanel.cleaned.prefab"
  },
  "prefabName": "RewardPanelView",
  "wrappers": [],
  "moves": [],
  "renames": [],
  "tightBounds": [],
  "textureRenames": [],
  "spriteAtlasRenames": [],
  "componentExtractions": [],
  "stateComponentExtractions": [],
  "variantComponentExtractions": [],
  "verify": {}
}
```

`output.mode` is `copy` or `in_place`. A copy needs a distinct `output.assetPath`; an in-place plan must use the source `prefabAssetPath` as `output.assetPath`.

All asset paths are project-relative paths beginning with `Assets/`. `prefabName` must use PascalCase and end with `View` when Texture or SpriteAtlas assets are renamed.

## Operations

`wrappers` are created in order. `parent` and every `source` path refer to the pre-apply Prefab tree, including the Prefab root name. Prefix a previously created wrapper ID with `@` when it is the parent or target.

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

## Shared Component Extraction

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

## Stateful Component Extraction

Use `stateComponentExtractions` when several **direct sibling roots occupy one visual slot** but represent mutually exclusive states of one logical component. This collapses those roots into one nested Prefab instead of producing one nested instance per source.

```json
{
  "stateComponentExtractions": [
    {
      "id": "seven_day_task_item",
      "template": "SevenDayTaskView/[TaskStates]/Task_01",
      "assetPath": "Assets/UI/Components/SevenDayTaskItem.prefab",
      "defaultState": "available",
      "states": [
        { "id": "locked", "source": "SevenDayTaskView/[TaskStates]/Task_01", "name": "[Locked]" },
        { "id": "available", "source": "SevenDayTaskView/[TaskStates]/Task_02", "name": "[Available]" },
        { "id": "completed", "source": "SevenDayTaskView/[TaskStates]/Task_03", "name": "[Completed]" }
      ]
    }
  ]
}
```

All `states[].source` paths must be direct siblings of `template`; `template` must be one of them. The generated root uses the output file name, such as `SevenDayTaskItem`, and contains a `[States]` child with the state names in the supplied order. Only `defaultState` is active in the saved component; branch selection at runtime remains outside this skill.

State branches may have different recursive signatures, but the sources must be visually overlapping and semantically mutually exclusive. Do not use this for simultaneously visible list entries. `stateComponentExtractions` cannot be combined with `componentExtractions`, `wrappers`, `moves`, `renames`, or `tightBounds`; use a separate plan. It rejects nested source Prefabs, external serialized references, source-path overlap, and existing output assets. A state extraction adds the component root and its `[States]` container to the final hierarchy, so update optional node/component counts in `verify` by two for each extracted state component.

## Variant List Component Extraction

Use `variantComponentExtractions` when several rows are visible at different list positions but represent one logical component in different visual states. It creates one shared Prefab and replaces every listed row with a nested instance. It does **not** collapse the rows into a single visible object.

```json
{
  "variantComponentExtractions": [
    {
      "id": "task_item",
      "template": "SevenDayTaskView/[TaskList]/[Task_01]",
      "assetPath": "Assets/UI/Prefab/Common/SevenDayTaskItem.prefab",
      "commonName": "[Common]",
      "statesName": "[States]",
      "defaultState": "in_progress",
      "states": [
        { "id": "in_progress", "source": "SevenDayTaskView/[TaskList]/[Task_01]", "name": "[State_InProgress]" },
        { "id": "claimable", "source": "SevenDayTaskView/[TaskList]/[Task_02]", "name": "[State_Claimable]" },
        { "id": "locked", "source": "SevenDayTaskView/[TaskList]/[Task_03]", "name": "[State_Locked]" }
      ],
      "instances": [
        { "source": "SevenDayTaskView/[TaskList]/[Task_01]", "name": "[TaskItem_01]", "state": "in_progress" },
        { "source": "SevenDayTaskView/[TaskList]/[Task_02]", "name": "[TaskItem_02]", "state": "claimable" },
        { "source": "SevenDayTaskView/[TaskList]/[Task_03]", "name": "[TaskItem_03]", "state": "locked" }
      ]
    }
  ]
}
```

`states[].source` must be direct siblings of `template`; every state source must appear exactly once in `instances`. The output root has direct `[Common]` and `[States]` children. Move only members proven common to every state into `[Common]`; leave it empty if no such proof exists. The runner normalizes each state root to the component origin, preserves each instance's original list position, and activates exactly `instances[].state`. `variantComponentExtractions` cannot be combined with the other extraction modes or hierarchy changes.

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

`forbiddenObjectNamePatterns` is a regex list checked against every GameObject name. Use it to reject PSD/export tokens, UUID names, punctuation-only names, and literal text values. `allowedMissingImagePathPrefixes` is an explicit exception list for known inherited nested-Prefab images with intentionally missing Sprite references; do not use it to silence missing Sprites in the target Prefab.

Run `-VerifyOnly` after an uncertain or timed-out apply. It does not mutate assets; it checks the saved Prefab, final hierarchy, asset GUIDs, Texture paths, and SpriteAtlas paths against this contract. It cannot reconstruct a pre-apply world-corner baseline, so the apply pass is responsible for the `0.01` world-corner invariant.
