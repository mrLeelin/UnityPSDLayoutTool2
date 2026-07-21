# Figma MCP Relay 对 PSD → Prefab 插件的改造设计

> 状态：设计基线，暂不包含代码改造。
>
> 目标：吸收 `E:\Project\Tools\FigmaMcpRelay` 中已经验证的 PSD 分层、增量同步、事务验证和 Prefab 导入经验，改造当前 `UnityPSDLayoutTool2`，同时保持现有直接 PSD → Unity 场景/Prefab 工作流可用。

## 1. 阅读范围与结论

本设计基于以下 Relay 代码和文档整理：

- `ai/skills/psd-layer-to-figma/scripts/export_psd_layers.py`
- `ai/skills/psd-layer-to-figma/scripts/submit_psd_import_job.py`
- `code/06_psd_incremental.mjs`
- `code.js` 中 `preparePsdIncrementalUpdate`、`applyPsdIncrementalUpdate`、`rollbackPsdIncrementalMutation`
- `ai/skills/psd-layer-to-figma/references/figma-import-workflow.md`
- `ai/skills/figma-to-prefab/references/workflow-figma-to-unity.md`
- `ai/skills/figma-to-prefab/references/json-spec-format.md`
- `ai/skills/figma-to-prefab/references/figma-ugui-import-conventions.md`

Relay 的成熟部分不是某一个 UI API，而是将导入拆成了明确的数据契约和可验证阶段：

```text
PSD 读取
  → 稳定身份与规范化 sourceState
  → manifest / asset manifest
  → 只读 diff / preview
  → fingerprint 确认
  → apply PSD 所有字段
  → 结构与字段验证
  → 写入新的源状态
  → 失败时回滚
```

当前插件的改造重点应放在这条数据和事务链上，而不是先增加更多 Inspector 选项。

## 2. Relay 中必须复用的设计原则

### 2.1 稳定身份优先于名称和数组索引

Relay 从 Photoshop 图层读取内部 `lyid`，规范化为 `layerId`。`psdLayerIndex` 只能作为遍历顺序，不能作为长期匹配身份。

当前插件后续应：

- 优先读取 PSD 图层的稳定内部 ID。
- 对缺失、过短、零值和重复 ID 报告冲突，不自动猜测。
- 名称只负责显示和生成 Unity 名称，不能单独决定更新/删除匹配。
- 同名兄弟节点仍需要稳定的安全名称和顺序，但它是展示/路径策略，不是主身份。

### 2.2 统一的 `sourceState` 是增量同步基础

Relay 的 `sourceState` 统一描述：

- `layerId`
- `mode`
- `geometry`：位置、尺寸、旋转
- `display`：可见性、透明度、混合模式、constraints
- `content`：内容哈希和资源引用
- `text`：字符、字体、字号、行高、对齐、颜色、描边、阴影
- `nineSlice`
- `unsupported`

当前插件应将 PSD 解析结果先转换为同类规范化模型，再分别生成纹理、场景节点和 Prefab。不要让每个输出阶段直接读取 PSD 原始结构并各自解释一遍。

建议版本化：

```json
{
  "version": 1,
  "layerId": "lyid:...",
  "mode": "image",
  "geometry": { "x": 0, "y": 0, "width": 100, "height": 50, "rotation": 0 },
  "display": { "visible": true, "opacity": 1, "blendMode": null, "constraints": {} },
  "content": { "contentHash": "sha256:...", "assetKey": "..." },
  "text": null,
  "nineSlice": null
}
```

字段缺失必须有明确默认值，规范化后再计算指纹，避免同一 PSD 因字段顺序或旧 manifest 形态不同而产生假变化。

### 2.3 明确 PSD 拥有的字段和 Unity 拥有的字段

建议把字段所有权定义为：

| 字段 | PSD 拥有 | Unity/项目拥有 | 同步策略 |
| --- | --- | --- | --- |
| 图层身份、源名称、源层级 | 是 | 否 | 用于匹配和结构更新 |
| 图像像素、文字内容 | 是 | 否 | 有变化才替换 |
| PSD 位置、尺寸、旋转 | 是 | 可配置映射 | 按导入模式转换后更新 |
| PSD 可见性、透明度、混合模式 | 是 | 否 | 更新显示属性 |
| Unity 节点名称安全化结果 | 否 | 是 | 保持稳定路径，必要时生成后缀 |
| Unity 组件、业务脚本、序列化绑定 | 否 | 是 | 默认保护，不被 PSD 覆盖 |
| Prefab 输出路径和目录 | 否 | 是 | 由 Inspector/项目配置决定 |
| Anchor/Canvas 映射策略 | 否 | 是 | 由导入配置控制，不写回 PSD |

默认情况下不要删除 Unity 侧业务组件，也不要用 PSD 重建整个 Prefab。只修改 PSD 明确拥有的节点字段。

### 2.4 Preview、Apply 和 Baseline Adoption 必须分开

Relay 的增量导入分成三个语义：

1. `Preview`：只读计算差异，不修改画布或 Prefab。
2. `Apply`：重新计算并校验 preview 指纹，确认期间发生变化则阻断。
3. `Adopt Baseline`：只在旧资源缺少源身份时写入基线，不改变视觉字段。

当前插件建议对应为：

- `分析差异`：在 Inspector 显示 added/changed/missing/conflict。
- `确认更新`：只更新 PSD 所有字段，并保留 Unity 业务字段。
- `采用当前资源为基线`：仅用于已有旧 Prefab 的首次迁移，默认不自动执行。

没有 preview 指纹、冲突或资源校验失败时，禁止执行写入。

### 2.5 失败必须可回滚，成功必须可验证

Apply 前捕获：

- 根节点源元数据；
- 每个将更新节点的 PSD 元数据；
- 将替换的 Image/Text/RectTransform 字段；
- 新增节点列表；
- 原有层级结构快照。

Apply 后验证：

- 根节点和子节点结构没有非预期变化；
- `layerId` 没有丢失、重复或错配；
- PSD 拥有字段与 sourceState 一致；
- 业务组件和受保护字段没有被覆盖；
- 新增资源引用存在；
- Prefab 保存结果可重新加载。

任一步失败都应恢复旧值、删除本次新增节点，并报告回滚是否完整。不能只捕获异常后宣称导入失败，因为异常本身不能证明没有部分写入。

## 3. 面向当前 PSD → Prefab 的目标架构

### 3.1 建议的模块边界

在现有 `Assets/PSDLayoutTool2/Editor` 下逐步形成以下职责，不要求一次性重命名全部旧代码：

```text
PsdFile/
  PSD 二进制解析

PsdSource/
  原始 Layer → PsdSourceDocument/sourceState
  layerId、contentHash、文本、透明度、九宫格元数据

PsdDiff/
  当前 Prefab/导入基线 → incoming sourceState 的差异计算
  added / changed / missing / conflicts

PsdImport/
  Preview、Apply、Baseline、Rollback、验证

PsdOutput/
  PNG、SpriteImporter、Scene、Prefab 生成

Diagnostics/
  operationId、阶段日志、统计、失败原因、回滚报告
```

现有 `PsdImporter` 和 `PsdInspector` 可以继续作为入口，先把解析、差异计算和写入逻辑抽成可单测的纯数据函数。

### 3.2 推荐的导入状态机

```text
Idle
  → ReadingPsd
  → Normalized
  → PreviewReady
  → AwaitingConfirmation
  → Applying
  → Verifying
  → Succeeded

Reading/Preview/Applying/Verifying 任意阶段失败
  → RollingBack
  → FailedWithRollbackReport
```

每次运行使用同一个 `operationId`，日志至少覆盖：`started`、`progress`、`succeeded`/`failed`/`cancelled`。批量处理要记录总数、成功、跳过、冲突和失败数量。

## 4. 差异和冲突语义

### 4.1 正常差异

- `unchanged`：`layerId` 和 sourceState 指纹相同，跳过。
- `changed`：同一 `layerId` 的源状态或内容变化，只更新 PSD 拥有字段。
- `added`：PSD 新增图层，先生成计划；确认后创建节点和资源。
- `missing`：旧 Prefab 中存在、PSD 中缺失的图层，默认保留并标记，不直接删除。

### 4.2 必须阻断的冲突

- 缺失或重复 `layerId`；
- 同一 `layerId` 对应多个 Unity 节点；
- Preview 后 PSD、Prefab 或资源内容发生变化；
- 纹理缺失、PNG 签名错误或 hash 不匹配；
- 目标 Prefab/场景不是预期目标；
- 试图修改业务脚本、PrefabInstance 或受保护节点；
- 九宫格元数据缺失或尺寸不满足切片规则；
- 父子坐标仍是根坐标而非直接父节点相对坐标。

不应通过“按名称最相似”自动解决身份冲突。应输出冲突层、候选节点和阻断原因。

## 5. Unity Prefab 适配规则

Relay 的 Figma → Unity 导入经验对本插件同样适用：

- 节点树可使用扁平数组 + `childIndices` 的中间 Spec，避免递归对象直接序列化导致父子顺序不稳定。
- 除根节点外，节点位置必须是相对直接父节点的坐标，不能把根坐标直接写给所有子节点。
- `RectTransform` 的 anchor、pivot、anchoredPosition、sizeDelta 必须由统一转换器计算，不能在多个输出分支重复推断。
- 普通图片和九宫格图片必须区分；九宫格 PNG 使用最小必要尺寸，不得把完整大图直接当九宫 Sprite。
- 文本层必须保留文本内容、字体、字号、行高、对齐和透明度；字体找不到时记录降级，而不是静默替换。
- 生成的展示型 Image 默认 `raycastTarget = false`，除非导入配置明确声明它是点击拦截节点。
- 不自动挂载业务脚本、不自动绑定 SerializeField、不自动将普通图层猜测成按钮或公共 Prefab。

## 6. 资源和元数据存储决策

Relay 的 Figma 侧使用 `SharedPluginData` 保存 PSD 身份，不依赖外部 mapping JSON。当前 Unity 插件没有 Figma 文档作为长期存储，因此改造时需要明确选择一种 Unity 内部基线存储：

### 推荐方向

将源身份和导入基线存放在生成资源自身可追踪的位置，并与 Prefab/资源 GUID 绑定；不要以随机临时文件或用户不可见的全局缓存作为唯一来源。

具体载体需要在实现前结合现有 Prefab 结构决定：

- 若允许新增 Editor-only 元数据组件，可将 root/layer 的 `layerId` 和 source fingerprint 序列化在 Prefab 中；
- 若不允许改变运行时 Prefab 结构，可研究 Unity AssetImporter/UserData 或已有 Editor metadata 机制；
- 不建议把完整映射放入项目外部 JSON，也不建议只用节点名称和路径匹配。

这项选择会影响旧 Prefab 迁移和 `.meta` 兼容性，进入实现前应先做一个最小原型和回读测试。

## 7. 九宫格迁移规则

Relay 已将九宫格表达为父节点源图 + `__slice_*` 子节点，并将 source rect 与 target rect 分开保存。当前插件可以不创建这些 Figma 辅助节点，但应保留同样的语义：

- 源图尺寸、border、slice kind 是源数据，不从最终 RectTransform 反推；
- `9slice`、`h3slice`、`v3slice` 使用不同的最小尺寸规则；
- 源图透明度和隐藏源图不能因为“不可见”而被错误跳过；
- Apply 前逐张验证 PNG 尺寸、border 和导入设置；
- Prefab 中 `m_Sprite` 不得出现空引用残留。

## 8. 分阶段实施计划

### 阶段 0：基线和回归样本

- 保留当前旧行为测试样本。
- 准备包含中文、透明度、蒙版、隐藏层、重名层、文本、九宫格和按钮标签的 PSD。
- 记录当前导出的 PNG、场景层级、Prefab 层级和日志。

### 阶段 1：规范化数据模型

- 增加 `PsdSourceDocument`、`PsdSourceLayer` 和版本化 `sourceState`。
- 实现 layerId 校验、contentHash、名称安全化和父子关系校验。
- 先写纯 C# 单元测试，不改变最终导出结果。

### 阶段 2：Preview/Diff

- 实现 added/changed/missing/unchanged/conflicts。
- 在 Inspector 增加只读差异摘要。
- 生成 baseline fingerprint。
- 验证旧资源首次迁移时不会误删或误改视觉字段。

### 阶段 3：受保护 Apply 和回滚

- 只写入 PSD 所有字段。
- 捕获结构、字段、元数据和新增节点回滚记录。
- Apply 后执行结构、字段、引用和源状态验证。

### 阶段 4：Prefab/资源细节门禁

- 补齐九宫格最小 PNG、SpriteImporter、父子相对坐标和 TMP 规则。
- 增加重复导入、旧 Prefab、资源被多 Prefab 引用时的测试。

### 阶段 5：运行时验证和文档更新

- 使用真实 Unity 编辑器验证编译、Prefab 加载、场景布局和截图。
- 将已验证的 metadata 存储方案、迁移命令和限制补充到 README。

## 9. 未来实现的验收清单

- [ ] 相同 PSD 重复导入不会重复创建节点或资源。
- [ ] 图层改名但 `layerId` 不变时能正确匹配，且 Unity 安全名称规则稳定。
- [ ] 图层删除默认进入 missing/待处理状态，不静默删除业务对象。
- [ ] 新增图层能在 Preview 中显示，Apply 失败时不留下半成品。
- [ ] Preview 后修改 PSD 或目标 Prefab 会触发 stale-preview 阻断。
- [ ] 中文图层名和中文文本不会乱码。
- [ ] 透明度、蒙版 Alpha、隐藏层和渲染顺序无回归。
- [ ] 目标 Canvas、World Space Canvas、等比/非等比缩放均有验证。
- [ ] 九宫格 PNG 尺寸、border 和 SpriteImporter 设置通过门禁。
- [ ] Prefab 中没有空 Sprite 引用，业务脚本和序列化字段未被覆盖。
- [ ] 成功和失败/回滚链路都能通过 `operationId` 从日志还原。
- [ ] 无法运行 Unity 时，报告明确区分静态检查、编译验证和运行时验证。

## 10. 当前不应直接做的事情

- 不要直接修改 Relay 仓库的脏文件来“顺手兼容” Unity 插件。
- 不要先把 Figma WebSocket、MCP 请求或外部服务嵌入当前 Unity 插件；第一阶段只移植稳定身份、sourceState、diff、事务和验证思想。
- 不要用手写 Prefab YAML 或临时 JSON 映射替代正式的资源/基线方案。
- 不要在没有回归样本和失败回滚验证前，替换现有 `PsdImporter` 的主导入路径。

## 参考实现定位

后续实现时优先从以下函数和文件开始对照：

- Relay：`code/06_psd_incremental.mjs` 的 `normalizePsdSourceState`、`buildPsdIncrementalDiff`。
- Relay：`code.js` 的 `preparePsdIncrementalUpdate`、`applyPsdIncrementalUpdate`、`rollbackPsdIncrementalMutation`。
- Relay：`ai/skills/psd-layer-to-figma/scripts/export_psd_layers.py` 的 `_read_psd_layer_id`、`_find_duplicate_layer_ids`、`_generate_summary`。
- Relay：`ai/skills/figma-to-prefab/references/json-spec-format.md` 的 `childIndices`、父子相对坐标、九宫格和 PrefabInstance 规则。
- 当前插件：`Assets/PSDLayoutTool2/Editor/PsdImporter.cs`、`PsdInspector.cs`、`PsdLogger.cs` 及 `PsdFile/`。
