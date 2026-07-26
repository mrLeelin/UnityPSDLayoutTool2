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

Run `-VerifyOnly` after an uncertain or timed-out apply. It does not mutate assets; it checks the saved Prefab, final hierarchy, asset GUIDs, Texture paths, and SpriteAtlas paths against this contract. It cannot reconstruct a pre-apply world-corner baseline, so the apply pass is responsible for the `0.01` world-corner invariant.
