# Prefab Hierarchy Cleanup Review

## Target Prefab
- **Path**: `Assets/PSDLayoutTool2/TestData/7日任务拆分/Prefab/7日任务拆分.prefab`
- **Fingerprint**: `c6d75b7826ac4688bd4646847f5ee6b032146f3fe25511e8bdb5a06408c756b4`
- **Total Nodes**: 105
- **Components**: 105 RectTransform, 96 CanvasRenderer, 59 Image, 37 TextMeshProUGUI

## Current Issues

| Issue | Description | Impact |
|-------|-------------|--------|
| Flat Structure | Main container (n000001) has 39 direct children | Poor maintainability, unclear organization |
| No Semantic Grouping | Backgrounds, buttons, text mixed at same level | Difficult to understand UI structure |
| PSD Export Names | Nodes named `ui_daily_*`, `daily_*`, `Common_Texture_*` | Non-descriptive, hard to maintain |
| Flat Sibling Findings | 3 findings where backgrounds contain related elements | Missing logical groupings |

## Proposed Semantic Hierarchy

```
7日任务拆分 (Root)
  └─ [Screen] (Main Container - n000001)
      ├─ [Background] (Background Images)
      │   ├─ DailyBgBig1 (n000031)
      │   ├─ DailyBgBig2 (n000032)
      │   ├─ DailyBgBig3 (n000033)
      │   └─ DailyBgBig4 (n000034)
      ├─ [DayNavigationBar] (Day Selection)
      │   ├─ [DayItem_7] (n000003-n000005)
      │   ├─ [DayItem_6] (n000011-n000013)
      │   ├─ [DayItem_5] (n000021-n000022)
      │   ├─ [DayItem_4] (n000023-n000024)
      │   ├─ [DayItem_3] (n000025-n000026)
      │   ├─ [DayItem_2] (n000027-n000028)
      │   └─ [DayItem_1] (n000029-n000030)
      ├─ [ProgressBar] (Progress Section - n000035)
      │   ├─ ProgressBackground
      │   ├─ ProgressFill
      │   ├─ MilestoneMarkers
      │   ├─ RewardIcons
      │   └─ RewardValues
      ├─ [TaskList] (Task Items - n000059)
      │   ├─ [Day5Tasks] (n000060)
      │   ├─ [Day4Tasks] (n000064)
      │   ├─ [Day3Tasks] (n000067)
      │   ├─ [Day2Tasks] (n000075)
      │   └─ [Day1Tasks] (n000087)
      └─ [BottomBar] (Bottom Elements)
          ├─ ButtonBackground (n000101)
          ├─ TimerIcon (n000102)
          ├─ TimerText (n000103)
          └─ ButtonForeground (n000104)
```

## Grouping and Naming Table

| Wrapper Name | Parent Node | Ordered Members | Sibling Order | Evidence | Inference/Unknown | Risk |
|--------------|-------------|-----------------|---------------|----------|-------------------|------|
| [Background] | n000001 | n000031, n000032, n000033, n000034 | 0 | 4 large background images at same position | Background layer group | Low |
| [DayNavigationBar] | n000001 | n000003-n000030 (28 nodes) | 1 | Day selection UI elements | Day navigation container | Medium |
| [ProgressBar] | n000001 | n000035 (23 children) | 2 | Progress tracking UI | Already grouped | Low |
| [TaskList] | n000001 | n000059 (5 day containers) | 3 | Task list with daily items | Already grouped | Low |
| [BottomBar] | n000001 | n000101-n000104 | 4 | Timer and button elements | Bottom action area | Low |

## Flat Sibling Resolutions

| Finding ID | Background | Members | Resolution | New Wrapper |
|------------|------------|---------|------------|-------------|
| flat_sibling_001 | n000003 | n000003, n000004, n000005 | Group | [DayItem_7] |
| flat_sibling_002 | n000011 | n000011, n000012, n000013 | Group | [DayItem_6] |
| flat_sibling_003 | n000101 | n000101, n000102, n000103, n000104 | Group | [BottomBar] |

## Child Prefab Extraction

| Component ID | Output Asset Path | Mode | Ordered Instances | Ordered Common Members | States | Default State | Evidence | Risk |
|--------------|-------------------|------|-------------------|------------------------|--------|---------------|----------|------|
| None | N/A | N/A | N/A | N/A | N/A | N/A | No repeated structures identified | N/A |

## Preservation Table

| Category | Items | Status |
|----------|-------|--------|
| Layout | All RectTransform positions and sizes | Preserve |
| Components | 105 RectTransform, 96 CanvasRenderer, 59 Image, 37 TextMeshProUGUI | Preserve |
| References | All serialized object references | Preserve |
| Active States | All GameObject active states | Preserve |
| Sibling Order | Relative ordering within groups | Preserve |
| Nested Prefabs | None detected | N/A |
| Generated Assets | None | N/A |

## Verification Contract

```json
{
  "verify": {
    "nodes": 105,
    "components": 297,
    "objectReferences": 0,
    "missingComponents": 0,
    "images": 59,
    "requireEnglishNames": true,
    "forbiddenObjectNamePatterns": [
      "^(?:\\d+|\\+|img_|ui_|daily_)",
      "^\\d+(?:_\\d+)?$"
    ],
    "tightBounds": [
      { "path": "7日任务拆分/[Screen]/[Background]" },
      { "path": "7日任务拆分/[Screen]/[DayNavigationBar]" },
      { "path": "7日任务拆分/[Screen]/[ProgressBar]" },
      { "path": "7日任务拆分/[Screen]/[TaskList]" },
      { "path": "7日任务拆分/[Screen]/[BottomBar]" }
    ],
    "hierarchy": [
      { "path": "7日任务拆分/[Screen]", "childCount": 5 },
      { "path": "7日任务拆分/[Screen]/[Background]", "childCount": 4 },
      { "path": "7日任务拆分/[Screen]/[DayNavigationBar]", "childCount": 7 },
      { "path": "7日任务拆分/[Screen]/[ProgressBar]", "childCount": 23 },
      { "path": "7日任务拆分/[Screen]/[TaskList]", "childCount": 5 },
      { "path": "7日任务拆分/[Screen]/[BottomBar]", "childCount": 4 }
    ],
    "directChildren": [
      {
        "path": "7日任务拆分/[Screen]/[DayNavigationBar]/[DayItem_7]",
        "children": ["DayBackground", "DayLabel", "DayNumber"]
      },
      {
        "path": "7日任务拆分/[Screen]/[DayNavigationBar]/[DayItem_6]",
        "children": ["DayBackground", "DayLabel", "DayNumber"]
      }
    ]
  }
}
```

## Risks and Unknowns

| Risk | Description | Mitigation |
|------|-------------|------------|
| Day Navigation Complexity | 28 nodes with mixed content types | Careful grouping, preserve all members |
| Text Values as Names | Nodes named "1", "2", "3" etc. | Rename to semantic names like "DayNumber" |
| PSD Export Names | Many nodes with `ui_*`, `daily_*` prefixes | Rename to English semantic names |
| No Component Extraction | No repeated structures identified | Focus on hierarchy cleanup only |

## Confirmation Required

Please review the proposed semantic grouping, naming, and hierarchy structure. Once confirmed, I will:

1. Create the version 2 in_place plan
2. Apply the hierarchy stage
3. Refresh the snapshot
4. Run final verification

**Do you confirm this complete grouping, naming, and hierarchy scheme?**
