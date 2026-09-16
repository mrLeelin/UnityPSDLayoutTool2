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
