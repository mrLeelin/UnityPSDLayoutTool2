# PSD UI 整理与公共组件

本词汇表约定 PSD2UIForm 与 PSDLayoutTool2 讨论 UI 整理、公共组件及生成结果时的用语。

## Language

**编辑树**：从 PSD 图层解析得到、用于配置控件类型和归属的中间结构。它不是最终交付的 UI Prefab。

**UI 整理**：识别控件类型、组织功能层级以及规划重复组件复用的整体工作。

**功能分组**：将属于同一控件或界面区域的节点组织在一起。功能分组不必成为独立 Prefab。

**图片引用（ref）**：让节点复用指定的图片资源。
_Avoid_: Prefab 引用、组件抽取

**公共 Prefab 引用（refp）**：在界面中使用指定的公共 Prefab。
_Avoid_: 自动抽取

**公共组件抽取**：从现有界面的重复部分提取可复用结构，并将原位置替换为保留各自差异的组件实例。
_Avoid_: 仅导出 Prefab、图片复用

**实例差异**：多个公共组件实例之间需要保留的内容、外观、布局或状态差别。

**状态组件**：将公共部分与不同状态的内容组织在同一组件中的结构。名称带 Variant 不代表它采用 Unity Prefab Variant 继承。

**九宫可观察性**：在不改变 Prefab 名称或运行时行为的前提下，通过编辑器层级标识与 Inspector 诊断，让开发者确认任意 Unity UI 节点是否使用了九宫 Sprite 和对应边距；不以节点是否由 PSD Layout Tool 生成作为过滤条件。

**九宫诊断**：Inspector 顶部的只读信息块。它仅在当前选中对象含有效九宫 Image 时出现，显示 Sprite、Sliced 状态和四边 Border；不替换 Unity 或第三方的 Image Inspector。

## 清理计划协议

**清理计划（v2）**：正式可审的层级清理执行文档。引用既有节点时必须使用快照中的 `node:<id>`，并携带 `snapshotFingerprint`。终端/外部 AI 与 Unity 自动 Apply 只认这一版。
_Avoid_: plan、计划文件（未标明版本时）、路径计划

**Runner 计划（v1）**：以层级路径引用节点的历史/内部格式。不可直接 Apply；仅允许只读诊断或明确标注的迁移输入。
_Avoid_: 正式计划、可执行计划

**权威快照**：由 Unity 生成、带指纹的节点清单。v2 计划中所有既有节点引用必须来自该快照，禁止手写路径或臆造 ID。

**Apply 哨兵**：人工在终端审核通过后，由 AI 写入的 `*.apply` 空文件。它是 Unity 自动校验并应用清理计划的唯一信号。
_Avoid_: 应用按钮、手动 Apply、Inspector 应用入口

**Apply 回执**：Unity 应用结束后写回的 `*.apply-result.json`，含 success / status / message，供终端 AI 读取失败原因并修订计划。
_Avoid_: 日志、Console 消息（二者不能替代回执文件）

**重放绑定证据**：v2 Replay Profile 与阶段一起保存的节点身份事实（`path` + `name` + `siblingIndex` + `components`）。快照节点 ID 是位置编号，PSD 更新后重放必须先用这些事实在重新生成的快照上证明**唯一**对应关系，否则停止并要求重新分析。
_Avoid_: 用相同编号或覆盖指纹代替对应关系证明

**分组后抽取意图**：v2 计划中的 `postGroupingExtractionIntents`，用**分组后**的层级路径描述第二阶段子 Prefab 抽取。Unity 保存并复验首阶段后自行刷新快照、重建并执行该阶段。
_Avoid_: 把分组后路径写成 `node:<id>`、让 AI 再申请第二次确认
