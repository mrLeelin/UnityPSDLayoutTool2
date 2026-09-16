# Hierarchy 九宫格标识 + 九宫格设置窗口

> 需求：在 Unity Hierarchy 里，只要是 `Image` 或 `Background` 类型的 `PsdLayerNode`，都带一颗九宫格开关；
> 开关有「黑 / 亮」两态，点击打开一个和 PSDLayoutTool2 九宫格窗口一致的设置窗口；
> 窗口里打勾 → Hierarchy 上的九宫格亮起；边距可自动推断、也可手动设置；数据要记录下来。

---

## 一、长什么样、怎么点

Hierarchy 行现在的布局（从右往左）：

```
…            名字          [九宫格]  [ref xxx]  [UIType ▾]
                          ↑ 新增     ↑ 原有      ↑ 原有
```

**九宫格标识**只在 `UIType == Image` 或 `UIType == Background` 的节点上出现。

| 状态 | 外观 | 含义 |
|---|---|---|
| 黑 | 深灰底板 + 暗灰 3×3 网格 | 未启用手动九宫格 |
| 亮 | 亮绿底板 + 深色 3×3 网格 | 已启用手动九宫格 |

标识尺寸是 **18–24px**（`Clamp(行高 + 4, 18, 24)`）。Hierarchy 行高只有 16px 左右，
所以它**允许略微溢出行的上下边界** —— 按行高缩到 13px 时 3×3 网格会糊成一团，分不出是什么。
网格线用 1.5px，另外把四个角淡淡填一层，用来在这么小的尺寸下表达"九个格子"的语义。

| 操作 | 行为 |
|---|---|
| **左键** | 打开「九宫格设置」窗口 |
| **Ctrl / Shift + 左键** | 直接开关（开启时若还没有边距，先自动推断一次） |
| **右键** | 菜单：打开设置 / 启用（自动推断）/ 启用（沿用已记录边距）/ 关闭 / 重新推断边距 / 清除数据 |
| 悬停 | 提示当前边距与可用操作 |

标识贴在引用按钮（`ref xxx`）左侧；没有引用按钮时贴在 UIType 下拉框左侧。
Hierarchy 面板太窄（标识左边缘推到 x < 44）时自动隐藏，不挤占名字区域。

---

## 二、九宫格设置窗口

`Psd2UiNineSliceWindow`，交互与 PSDLayoutTool2 的 `PsdNineSliceWindow` 保持一致：

- **左栏**：同一 `Psd2UIFormConverter` 下所有 Image / Background 节点列表，带模糊搜索，可直接切换编辑对象
- **右栏**：
  - 预览（透明底棋盘格 + 图像），**青色参考线**四条，可直接拖动；拖动中的那条变黄
  - `Left / Top / Right / Bottom` 四个整数输入框
  - 勾选框「使用手动九宫格（Override）」
  - 「使用自动推断候选」按钮 → 三重推断（等价于插件原逻辑）
  - 「重置为默认边距」
  - 「应用并写入图片（spriteBorder）」
  - 「清除九宫格数据」

编辑是**防抖自动应用**的（停止操作 0.35 秒后落盘），不需要每次点按钮。

**预览来源优先级**（三级回退，`Psd2UiNineSliceNodeState.ResolvePreviewTexture`）：

| 顺序 | 来源 | 说明 |
|---|---|---|
| ① | 节点上 `Image` / `RawImage` 引用的**导出 PNG** | 边距最终就是写进这张图的 `spriteBorder`，所见即所得 |
| ② | **按节点实时渲染**（`Render()`） | 与导出同一套渲染路径，像素空间和最终 PNG 严格一致；不经过编辑器宿主与预览缓存，**Prefab 隔离模式下也能出图** |
| ③ | 宿主提供的 PSD 图层预览 | 兜底，仅在 ①② 都拿不到时使用 |

② 优先于 ③ 是刻意的：③ 走的是 `RenderPreview()`，分辨率/采样可能与导出结果不同，
拿它当基准会让参考线位置和最终 PNG 对不上。

②是本窗口自己 `new` 的临时纹理（`hideFlags` 带 `DontUnloadUnusedAsset`，不会被自动回收），
所以换了节点或关闭窗口时必须调 `ReleaseRenderFallback()` 释放，否则每开一次窗口漏一张。

---

## 三、数据记录在哪

**记录在 `PsdLayerNode` 组件自身**，随 Prefab 一起序列化：

```csharp
[SerializeField][HideInInspector] internal bool nineSliceEnabled;   // 开关
[SerializeField][HideInInspector] internal int  nineSliceLeft;      // 左边距（源图像素）
[SerializeField][HideInInspector] internal int  nineSliceTop;
[SerializeField][HideInInspector] internal int  nineSliceRight;
[SerializeField][HideInInspector] internal int  nineSliceBottom;
```

读写集中在 `Psd2UiNineSliceNodeState`（Editor 侧），全部走 `Undo.RecordObject`，
**可撤销、可持久化、随 Prefab 迁移**。

- 关闭开关**不会清空**边距，方便再次打开时复用；
- 「清除九宫格数据」才会把开关与边距一起复位；
- 若节点属于 Prefab 实例，会额外 `PrefabUtility.RecordPrefabInstancePropertyModifications`。

---

## 四、开关打开后会影响什么

1. **导出图片时**（`UGUIParser.ExportAndLoadSprite` / `PsdLayerNodeEditorOps.ExportImageAsset`）
   → 走新重载 `EnsureNineSliceBorder(texturePath, PsdLayerNode)`：
   - 开关亮着 → 用节点记录的四个边距**直接覆盖** `TextureImporter.spriteBorder`，跳过命名规则与像素推断；
   - 开关关着 → 行为与改动前**完全一致**（图层名标签 → 像素推断）。
2. **`Image.Type` 强制为 `Sliced`**（`UGUIParser.ResolveImageType`）——
   否则 `spriteBorder` 设了也不生效。
3. **自动裁剪九宫格会跳过**：手动边距是照着当前图量的，`AutoCropMinimalNineSlice` 会改变像素尺寸，
   两者同时启用会互相打架，所以有手动九宫格时跳过自动裁剪。

---

## 五、改动清单

| 文件 | 类型 | 说明 |
|---|---|---|
| `Src/Runtime/Psd2UGUI/Runtime/PsdLayerNode.cs` | 改 | 新增 5 个序列化字段 + `SupportsNineSlice` / `HasNineSliceBorder` |
| `Src/Editor/Psd2UGUI/NineSlice/Psd2UiNineSliceNodeState.cs` | **新增** | 九宫格状态读写、预览/导出图解析、推断、写 spriteBorder、脏标记 |
| `Src/Editor/Psd2UGUI/NineSlice/Psd2UiNineSliceWindow.cs` | **新增** | 九宫格设置窗口 |
| `Src/Editor/Psd2UGUI/Conversion/Psd2UIFormConverter.cs` | 改 | Hierarchy 行绘制九宫格标识 + 右键菜单；`EnsureNineSliceBorder(string, PsdLayerNode)` 新重载 |
| `Src/Editor/Psd2UGUI/Conversion/UGUIParser.cs` | 改 | 手动九宫格时强制 Sliced；导出改用节点级重载；有手动边距时跳过自动裁剪 |
| `Src/Editor/Psd2UGUI/Conversion/PsdLayerNodeEditorOps.cs` | 改 | 导出改用节点级重载 |
| `Src/Editor/Psd2UGUI/EditorTools/PsdLayerNodeInspector.cs` | 改 | Inspector 增加「九宫格（9-Slice）」区块：开关 + 边距摘要 + 打开窗口按钮 |

改动前的原文件备份在：
`.workbuddy/backup/2026-09-12-nineslice-hierarchy-toggle/`

---

## 六、怎么验证

1. 切回 Unity 让它编译（会域重载一次），Console 应无 `error CS`。
2. 打开 `Assets/PSD2UIForm/Examples/Psd2UguiForm_UIFormEditor.prefab`（双击进 PrefabStage）。
3. Hierarchy 里找 `UIType = Image / Background` 的行 —— 右侧应出现深灰的九宫格图标。
   非 Image / Background 的行（TMPButton、FillColor、Scrolling_View_Vertical…）**不应出现**。
4. 左键点图标 → 弹出「九宫格设置」窗口；确认左侧列表、预览、四条青色参考线都在。
5. 拖动参考线或改数值 → 勾上「使用手动九宫格」→ 关掉窗口 → **Hierarchy 那颗标识变亮绿**。
6. 右键图标 → 「按当前图像重新推断边距」→ 看 `Editor.log` 里的
   `[Psd2UIForm] 九宫格边距重新推断完成(...)`。
7. 保存 Prefab（Ctrl+S）后，用文本编辑器打开该 `.prefab`，搜 `nineSliceEnabled` —— 应为 `1`，边距数值同步写入。
8. 重新生成 UIForm，确认 Image 变成 `Sliced` 且 sprite 的 Border 是手动那组值。

---

## 七、已知边界

- **边距坐标空间** = 该节点导出 PNG 的像素空间。若之后重新导出的图片尺寸发生变化（例如源 PSD 图层改了），
  已记录的边距不会自动缩放，需要重新推断或手改。
- 自动推断依赖图像有可见边界结构；纯色/全透明的图层会推断失败，此时仍可手动填写边距。
- 若该节点还没有导出过 PNG，窗口会退回使用 PSD 图层预览，边距只记录在节点上，
  等导出图片时才会写进 `spriteBorder`。
- 图层名的显式边距标签（`|9slice=左,上,右,下`）优先级高于像素推断；手动开关则高于两者。

---

## 八、看不到标识怎么办（首版踩过的坑）

标识是编译产物，**只要有一处 `error CS`，整个 Editor 程序集就不会更新**，
Hierarchy 上跑的还是上一版编译成功的代码 —— 现象是 `ref xxx` 按钮和 UIType 下拉框都正常，
就是没有九宫格图标。首版正是如此，两个错因已修：

| 错因 | 症状 | 修法 |
|---|---|---|
| `PsdLayerNode.NineSliceEnabled` 属性缺失（只剩字段 `nineSliceEnabled`） | 8 处 `CS1061` | 补 `internal bool NineSliceEnabled => nineSliceEnabled;` |
| `GenericMenu` 无 `AddItem(content, on, disabled, func)` 重载 | `CS1503` + `CS1660` | 置灰项改用 `menu.AddDisabledItem(GUIContent)` |

自查方法（不用开 Unity）：

```
grep -n "error CS\|CompileScripts" "C:\Users\li182\AppData\Local\Unity\Editor\Editor.log" | tail
```

日志行数没变 = Unity 还没重新编译（**后台或失焦时不会自动 refresh，切回 Unity 才会**）。
想让 Unity API 存在性立刻有答案，可直接在安装目录的托管 DLL 里查方法名：

```
grep -a -o "AddDisabledItem\|AddItem\|MenuFunction2" \
  "E:\UnityEngine\Engine\Installs_location\6000.3.6f1\Editor\Data\Managed\UnityEditor.dll" | sort | uniq -c
```

另外，标识在面板过窄时会主动让位（可用宽度不足时会隐藏），把 Hierarchy 面板拉宽即可看到。

### 窗口提示「无可用图像」

这是预览解析链路依赖了不该依赖的东西：首版只试"已导出的 PNG"和"编辑器宿主的预览缓存"，
而宿主在 **Prefab 隔离模式**下可能取不到，节点也常常还没导出过图片 —— 于是窗口空着。
现在加了②「按节点实时渲染」这一层，直接调 `PsdLayerNodeEditorOps.CreateNineSliceSourceTexture(node)`，
绕开宿主与缓存，正常情况下都能出图。

如果连②也拿不到（窗口里仍显示警告），说明该节点**真的没有可渲染的像素**：
空容器、纯 `FillColor`、没绑定 PSD 图层，或源 PSD 已不在工程里。
窗口的提示框里现在会把这几种可能和处理办法都列出来。
