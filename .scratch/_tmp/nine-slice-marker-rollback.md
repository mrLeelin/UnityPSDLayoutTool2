# 回退说明：Hierarchy 九宫标识点击 + PSD 面板九宫状态 + 立即应用按钮

本轮改动的基线不在 git 里（`PsdNineSliceObservability.cs` 等是未提交文件，
`PsdNineSliceWindow.cs` / `PsdNineSliceTests.cs` 的 git 基线混着更早的未提交 WIP），
所以把关键原文留在这里。

## 第三轮：「Crop & apply to exported PNG」按钮（真切图）

| 文件 | 变化 |
|---|---|
| `Assets/PSDLayoutTool2/Editor/NineSlice/PsdNineSliceExportedBorderApplier.cs` | 新增（含 `.meta`）：按九宫裁到最小可拉伸尺寸（`PsdNineSliceTextureProcessor.TryCropAndPersist`）+ 写 spriteBorder + 把引用它的 Image 改成 Sliced，并统计 RawImage 数量 |
| `Assets/PSDLayoutTool2/Editor/NineSlice/PsdNineSliceWindow.cs` | 新增按钮与 `ApplyToExportedPng()` |
| `Assets/PSDLayoutTool2/Editor/Tests/PsdNineSliceTests.cs` | 新增 `PsdNineSliceExportedBorderApplierTests`（4 个用例，含 3 个真建 PNG/Prefab 的集成用例） |

回退：

1. 删除 `PsdNineSliceExportedBorderApplier.cs` 与 `.cs.meta`。
2. `PsdNineSliceWindow.cs` 删掉按钮所在的
   `EditorGUI.BeginDisabledGroup(!nineSliceEnabled || selectedExportedBorder == null)` 段落
   （含 `No exported PNG for this layer yet...` 那行 miniLabel）与 `ApplyToExportedPng()` 方法，
   并把末尾 HelpBox 的文案恢复为单段英文。
3. `PsdNineSliceTests.cs` 删掉 `PsdNineSliceExportedBorderApplierTests` 类；
   `using UnityEngine.UI;` 若不再需要也一并去掉。

## 第二轮：PSD 面板显示「已经是九宫」（只看导出图 spriteBorder）

改动文件：

| 文件 | 变化 |
|---|---|
| `Assets/PSDLayoutTool2/Editor/NineSlice/PsdNineSliceExportedBorderLookup.cs` | 新增（含 `.meta`） |
| `Assets/PSDLayoutTool2/Editor/NineSlice/PsdNineSliceWindow.cs` | 列表状态点、面板状态行、按导出边框显示参考线、按导出边框填充四边距、只读时不再误写 override |
| `Assets/PSDLayoutTool2/Editor/Tests/PsdNineSliceTests.cs` | 新增 6 个用例（3 个纯逻辑 + 1 个格式 + 2 个 AssetDatabase 集成） |

回退：

1. 删除 `PsdNineSliceExportedBorderLookup.cs` 与 `.cs.meta`。
2. `PsdNineSliceWindow.cs` 里恢复：
   - `LoadSelectedPsdLayerState()`：去掉 `selectedExportedBorder` / `RefreshLayerState()` 调用，
     改回 `AssetImporter.GetAtPath(assetPath)` + `PsdNineSliceOverrideStore.TryGet(...)` 的写法。
   - `DrawNineSliceGuides()`：恢复 `if (!nineSliceEnabled ...)` 与固定的青色。
   - `DrawPsdLayerEditor()`：删掉 `DrawExportedBorderStatus()` 调用，恢复
     `"Manual override" / "No manual override"` 的 label 与 `Width(105)`，
     并把 `DrawBorderFields(...) && nineSliceEnabled` 改回 `DrawBorderFields(...)`。
   - `DrawPsdLayerList()`：恢复 `GUILayout.Toggle(selected, label, ...)` 单行写法，
     删除 `DrawLayerStatusBadge` / `BuildLayerStatusTooltip` / `ResolveExportedBorder` /
     `s_LayerStatusBadgeStyle`。
   - `DrawBorderFields()`：恢复 `hasManualOverride = true;` 的旧写法。
   - `OnFocus()` / `RefreshLayerState()` / 字段与 `Open()` 里的 `RefreshLayerState()` 调用一并删除。
3. `PsdNineSliceTests.cs`：删掉 `PsdNineSliceExportedBorder*` 与 `TextureFolderConventionMatches*`
   四个用例、`PsdNineSliceExportedBorderLookupTests` 类，以及 `using System.Collections.Generic;`
   / `using System.IO;` / `using UnityEngine;`（若不再需要）。

## 第一轮：Hierarchy 九宫标识点击打开 PSD 窗口

| 文件 | 变化 |
|---|---|
| `Assets/PSDLayoutTool2/Editor/NineSlice/PsdNineSliceSourcePsdResolver.cs` | 新增（含 `.meta`） |
| `Assets/PSDLayoutTool2/Editor/NineSlice/PsdNineSliceObservability.cs` | 改 `OpenNineSliceEditor`，删除死代码 `TryFindSourcePsd` |
| `Assets/PSDLayoutTool2/Editor/Tests/PsdNineSliceObservabilityTests.cs` | 新增 4 个解析用例 |

回退：

1. 删除 `PsdNineSliceSourcePsdResolver.cs` 与 `.cs.meta`。
2. 删除 `PsdNineSliceObservabilityTests.cs` 里的 `EnumerateConventionCandidates_*` 与
   `TryResolve_WithoutAPngPathOrLayerId_DoesNotClaimASourcePsd`，并去掉 `using System.Linq;`。
3. 用下面这段替换 `PsdNineSliceObservability.cs` 里新的 `OpenNineSliceEditor`：

```csharp
        internal static bool OpenNineSliceEditor(UnityEngine.Object asset)
        {
            string assetPath = asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath)) return false;
            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer != null && PsdNineSliceAssetState.TryReadLayerIdentity(importer.userData, out uint layerId) &&
                PsdNineSliceAssetState.TryReadSourcePsdIdentity(importer.userData, out string sourcePsdPath) &&
                AssetDatabase.LoadAssetAtPath<Object>(sourcePsdPath) != null)
            {
                PsdNineSliceWindow.Open(sourcePsdPath, layerId);
                return true;
            }
            PsdNineSliceWindow.Open(assetPath);
            return true;
        }
```

4. 如需恢复被删掉的 `TryFindSourcePsd`（它从未被调用，逻辑已并入 `PsdNineSliceSourcePsdResolver`），
   可从本次会话记录里取回原文。
