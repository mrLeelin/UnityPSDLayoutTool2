# CLAUDE.md

本文件提供 Claude Code 在该仓库工作时的指导。

## 项目定位

`UnityPSDLayoutTool2` — 将 Photoshop PSD 图层导入 Unity 的 Editor 插件。支持纹理导出、场景布局、Prefab 生成和增量更新。

`FigmaMcpRelay`（`E:\Project\Tools\FigmaMcpRelay`）在稳定身份、增量同步和事务验证方面有成熟的实现，本插件的部分功能可参考其设计。工作路径不同：本插件直接读取 PSD 文件，Figma Relay 通过 Figma API 中转。

### 核心目录

- `Assets/PSDLayoutTool2/Editor/PsdFile/` — PSD 二进制解析器
- `Assets/PSDLayoutTool2/Editor/PsdPrefab/` — PSD→Prefab 转换管线（中间模型、差异计算、材质工厂）
- `Assets/PSDLayoutTool2/Editor/PsdImporter.cs` — 主导入器（三大工作流）
- `Assets/PSDLayoutTool2/Editor/PsdInspector.cs` — 自定义 Inspector GUI
- `Assets/PSDLayoutTool2/Editor/PsdLogger.cs` — 诊断日志写入器
- `Assets/PSDLayoutTool2/Runtime/` — 运行时组件（`PsdPrefabNodeIdentity`、`PsdPrefabNodeKind`）
- `PhotoshopPsdLayoutExporter/` — Photoshop UXP 插件（向 PSD XMP 写入 layerId、fingerprint）

### 验证版本

- Unity 6000.3.7f1
- 许可证：MIT

## 代码架构

### 命名空间

- `PhotoshopFile` — PSD 二进制解析器（仅 Editor）
- `PsdLayoutTool2` — 导入器/导出器/Inspector/转换管线
- `PsdLayoutTool2.Tests` — 单元测试

### PSD 二进制解析 (`PsdFile/`)

- `PsdFile.cs` — 核心加载器，读取文件头、颜色模式、图像资源、图层/蒙版、XMP 元数据
- `BinaryReverseReader.cs` — 大端序二进制读取器，支持 Photoshop 描述符解析
- `ImageDecoder.cs` — PSD 像素数据 → Unity Texture2D
- `PsdEmbeddedLayoutManifest.cs` — PSD XMP 中的 JSON 清单反序列化（layerId、parentId、fingerprint）
- `Layers/Layer.cs` — 图层数据：矩形、通道、文本属性、图层效果、`lsct` section divider
- `Layers/PsdTextStyle.cs` — 归一化的文本展示数据（行高、描边、阴影）

### 导入器 (`PsdImporter.cs` ~180KB)

三大工作流由同一条 `Import()` 管线处理：

1. **Export Layers As Textures** — 仅导出 PNG
2. **Layout In Current Scene** — 导出 PNG + 在场景中创建 GameObject
3. **Generate Prefab** — 导出 PNG + 创建并保存 Prefab

核心流程：

```text
读取 PSD
→ BuildLayerTree() 构建图层树（基于 lsct 或 IsPixelDataIrrelevant 检测组）
→ BuildLayerImportInfoMap() 创建层元数据（唯一名称、锚点、布局矩形）
→ 冲突分析（对比现有生成目标）
→ ExportTree() 导出纹理 / 创建 GameObject / 保存 Prefab
```

### PSD→Prefab 转换管线 (`PsdPrefab/`)

此管线将 PSD 解析结果转为中间模型，再通过差异计算实现增量更新：

- `PsdPrefabModels.cs` — 中间数据模型（`DocumentModel` / `NodeModel` / `TextModel`）
- `PsdPrefabModelBuilder.cs` — 从清单或原生 PSD 图层构建中间模型
- `PsdPrefabDiff.cs` — 基于 stable ID 的差异计算（Added/Updated/Unchanged/Removed）
- `PsdPrefabConversionPipeline.cs` — 编排转换计划
- `PsdPrefabTextMaterialFactory.cs` — TMP 材质创建与文件夹创建
- `PsdPrefabTextMaterialSignature.cs` — 材质参数签名缓存

### 运行时组件 (`Runtime/`)

- `PsdPrefabNodeIdentity.cs` — MonoBehaviour，存储 stableId / fingerprint / kind，供增量更新使用
- `PsdPrefabNodeKind.cs` — 枚举：Group / Image / Text

## FigmaMcpRelay 参考

详见 [FIGMA_REFERENCE.md](FIGMA_REFERENCE.md)。

## 图层组检测逻辑

`BuildLayerTree()` 和 `IsStartGroup()` / `IsEndGroup()` 是图层树构建的核心：

- **`IsStartGroup()`** (`PsdImporter.cs:2797-2812`):
  1. 优先检查 `layer.IsGroupStart`（`SectionType == 1 || 2`）
  2. 有 `lsct` 但不是组开始 → 不是组
  3. 回退：检查 `IsPixelDataIrrelevant`（旧 PSD 格式）

- **`IsEndGroup()`** (`PsdImporter.cs:2817-2823`):
  1. `layer.IsGroupEnd`（`SectionType == 3`）
  2. 回退：名称匹配 `</Layer set>` / `</Layer group>` / ` copy`

- **`SectionType`** (`Layer.cs:197`) — 从 `lsct` adjustment info 读取：
  - -1：无 lsct 标签
  - 0：other（普通层）
  - 1：open folder（组开始）
  - 2：closed folder（组开始）
  - 3：bounding（组结束）

## 文字层处理

### 文字解析（`Layer.ReadTextLayer()`）

PSD 的 `TySh` 调整层包含文字描述符，使用 PostScript 字符串格式：

- `/Text` — 文字内容，编码为 PostScript 字符串 `(UTF‑16BE)`，以 `)` 结束
- `/FontSize` — 字号（点）
- `/FontSet` → `/Name` — 字体名，PostScript 字符串
- `/FillColor` → `/Values` — 填充色（RGBA）
- `/Justification` — 对齐（0=左, 1=右, 2=中）
- `/FontSize` / `/Leading` — 字号和行高

**关键实现细节：** 文字和字体名使用 `ReadPostScriptUtf16String()` 读取，该函数处理 PostScript 转义并查找 `)` 为结束符，而非以 `\0` 终止的 `ReadString()`。

### 文字样式（`PsdTextStyle`）

存储归一化的展示数据：
- `LineHeight` — PSD 像素单位的行高
- `StrokeEnabled` / `StrokeWidth` / `StrokeColor` — 描边
- `ShadowEnabled` / `ShadowDistance` / `ShadowAngle` / `ShadowBlur` — 阴影

### 字体缩放（`GetUIFontSize()`）

在目标 Canvas 坐标模式下，字体大小使用 `GetTargetCanvasUniformScale()` 缩放（取宽高缩放的最小值），防止不同宽高比的 Canvas 导致文本溢出重叠。

### TMP 材质工厂

`PsdPrefabTextMaterialFactory` 根据文字样式签名创建/复用 `TextMeshPro` 材质：
- 签名通过 `PsdPrefabTextMaterialSignature` 计算
- 材质保存在输出目录的 TMP 子文件夹
- 使用 `AssetDatabase.CreateFolder` 逐级创建目录

## 常用命令

### 测试

Unity Test Runner 中运行 `PsdLayoutTool2.Tests` 测试集。
测试文件：`Assets/PSDLayoutTool2/Editor/Tests/PsdFileImportTests.cs`

### 构建与验证

- 本项目是 Unity Editor 插件，无独立构建脚本
- 修改 C# 后在 Unity 等待编译完成，确认 Console 无新增编译错误
- 诊断日志：`Library/PSDLayoutTool2/Logs/`

### 验证范围

| 改动范围 | 验证方式 |
|---------|---------|
| C# 修改 | Unity 编译通过 |
| PSD 解析/纹理导出 | 用含中文、透明度、蒙版、隐藏层、重名的 PSD 测试 |
| 树/组检测修改 | 对比修改前后的图层树结构、导出的 PNG 数量 |
| 布局修改 | 分别验证目标 Canvas 模式和 World Space Canvas 回退 |
| Prefab 修改 | 检查层级、资源引用、锚点、渲染顺序、透明度 |
| 文字层修改 | 检查 GameObjects 名称、文字内容、字体、位置、缩放的正确性 |
| 诊断相关 | 检查日志目录，确认关键阶段可追踪 |

## 修改原则

- 优先保持现有导入/布局/Prefab 生成行为兼容
- 只修改完成任务所需的文件，不提交 `Library/`、临时日志或缓存
- `.meta` 文件必须与资源一起维护
- 保留 `PsdLayoutTool2` 命名空间，遵循相邻文件的风格
- 中文文本写入后重新打开文件检查是否出现乱码、`???` 或错误转义
- **每次修改前先写计划，用户确认后才改代码**
- **每次修改后自动验证（编译通过 + 对比影响）**
- **每次修改后给出 diff 并支持回退**

## 功能边界

- PSD Smart Object 不在当前解析范围内
- `|Animation` 只支持非 Unity UI 模式
- `|Button` 只支持 Unity UI 模式
- 目标 Canvas 对齐、尺寸映射、等比缩放、按名称锚点是相互关联的行为
- `PsdPrefab` 管线目前是只读分析（创建 Plan），不写入 Prefab；Prefab 生成由 `PsdImporter` 的旧工作流完成

## 增量更新身份设计

本插件的增量更新通过以下身份链实现：

```text
PSD 图层 (lyid)
  → XMP manifest (layerId / parentId / fingerprint)
  → PsdPrefabNodeModel (stableId / parentStableId / contentFingerprint)
  → PsdPrefabNodeChange (Added / Updated / Unchanged / Removed)
  → PsdPrefabNodeIdentity (stableId / fingerprint 写入 Unity 节点)
```

无 XMP 清单时回退为 `native_` hash（`parentId + "/" + siblingIndex + "/" + name` → FNV-1a）。

## Git 约束

- 保留无关的工作区修改，只暂存任务涉及的文件
- 提交前检查 `git diff`、`git diff --cached`、`git status`
- 提交信息说明修改意图、约束、验证结果和未验证项
- 未明确要求时不要自动 push
