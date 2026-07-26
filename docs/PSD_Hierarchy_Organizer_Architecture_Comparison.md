# PSD AI 层级整理：HTML 工作台与 Prefab 原生整理对比

## 结论

不要继续把 HTML 工作台作为 PSD 图层分组、拖放和层级编辑的主界面。

但“直接在 Prefab 上操作”不应理解为每次拖动都立即写回 `.prefab` 文件。推荐方案是：以真实 Prefab 为唯一层级来源，在 Unity 原生窗口中进行暂存编辑和预览；只有用户明确点击“应用”时，才通过现有事务保存流程写回 Prefab。

HTML 可以保留为只读分析报告、PSD 合成预览或可分享链接，但不再承担层级编辑职责。

## 当前目标

用户需要完成的工作不是查看 AI 文本结果，而是完成一次可信的层级整理：

1. AI 基于 PSD 与目标 Prefab 生成命名和分组建议。
2. 用户能在真实层级中清楚地区分组、成员和未分组图层。
3. 用户能使用 Unity 原生的多选、拖放、展开、折叠、搜索和 Undo 调整建议。
4. 系统保护已有组件、绑定、受保护边界和增量同步身份。
5. 只有用户确认“应用”后，才把经过验证的计划写入 Prefab；失败必须可回滚。

## 已核实的现状

### HTML 工作台

当前网页不是直接编辑 Prefab，而是维护独立的 Web 会话和预览模型：

- `PsdHierarchyWebWorkbench` 为每次打开创建 loopback HTTP server、session 和 token，并通过 `Application.OpenURL` 打开浏览器。
- 网页端以 `/snapshot`、`/move`、`/accept`、`/apply` 等 HTTP 接口操作一个内存中的 `PsdHierarchyOrganizerPreviewModel`。
- `PsdHierarchyWebWorkbench` 在程序集重载和 Unity 退出时执行 `Shutdown()`；server 与 session registry 都会被释放。
- 浏览器中的选择、拖放、分组卡片和画布坐标是另一套状态，需要和 Unity 预览模型持续同步。

这解释了近期出现的现象：浏览器看起来连接正常，但重编译后会话失效；视觉上的图层和分组也不一定等同于真实 Prefab 当前的 Transform 层级。

### Prefab 应用基础

项目已有适合保留的安全基础：

- `PsdHierarchyPlanValidator` 在应用前校验计划和请求身份。
- `PsdHierarchyApplier` 会在失败时回滚已创建对象和 Transform 状态。
- `PsdPrefabTransactionalSave` 使用 `PrefabUtility.SaveAsPrefabAsset` 保存。
- `PsdPrefabIncrementalMerge` 已将计划应用接入增量 Prefab 更新。

因此，问题主要是交互界面和编辑会话的位置，不是 AI 计划校验或 Prefab 保存能力必须推倒重来。

## 方案对比

| 维度 | HTML 工作台作为主编辑器 | Unity 原生 Prefab 整理窗口 | 直接每次拖动写入 Prefab |
| --- | --- | --- | --- |
| 层级来源 | 独立快照，需要同步 | 真实 Prefab / 暂存 Prefab 内容 | 真实 Prefab |
| 多选与拖放 | 需要自行实现并维护 | 可使用 Unity TreeView、选择和 Undo 语义 | 可使用 Unity 原生交互 |
| 会话稳定性 | 受 loopback server、token、浏览器和域重载影响 | 跟随 Unity 编辑器生命周期 | 稳定，但错误会立即污染资产 |
| 组件和绑定保护 | 必须手工映射并重复校验 | 可直接检查 Transform、组件和 Prefab 状态 | 可直接检查，但误操作风险高 |
| PSD 合成图预览 | 较容易展示 | 需要额外 Preview 面板 | 需要额外 Preview 面板 |
| 自动化 / 外部查看 | 容易在浏览器中打开 | 主要在 Unity 中使用 | 主要在 Unity 中使用 |
| Undo / 回滚 | 自建撤销模型 | Unity Undo 加上事务回滚 | Unity Undo 依赖更强，批量 AI 修改风险高 |
| 实现维护成本 | 高：两套 UI、状态和拖放协议 | 中：一套 Unity UI，复用现有模型 | 低起步、高风险，需要补大量保护 |
| 适合作为主操作界面 | 否 | 是 | 否 |

## HTML 方案的优点和边界

### 优点

- 可以展示 PSD 合成大图、缩放画布、AI 解释和统计信息。
- 可以在 Codex 浏览器或普通浏览器中快速打开，只读分享方便。
- UI 可以独立于 Unity Inspector 布局，适合长文本分析报告。

### 缺点

- 浏览器并不知道 Unity 当前 Prefab Stage、选择、Undo 栈和组件约束。
- 需要复制多选、拖放、层级展开、受保护节点、会话操作状态等编辑器能力。
- Unity 重新编译或域重载会销毁 Web server/session，浏览器必须重新建立授权连接。
- “组卡片”“单图层组”“画布选中”容易成为另一个模型，而不是用户正在编辑的真实层级。
- 为修复浏览器拖放不断增加特殊逻辑，会提高维护成本，不能提高 Prefab 整理的可信度。

### 保留范围

HTML 可保留为只读页面，内容限定为：PSD 合成预览、AI 分析摘要、建议分组摘要、应用前后 diff、日志导出。它不应拥有编辑层级或应用 Prefab 的主流程。

## Prefab 原生方案的优点和边界

### 优点

- 用户看到的是实际 Transform 层级，而不是转换后的卡片模型。
- 多选、拖放、键盘操作、搜索、展开、折叠和 Prefab Stage 都符合 Unity 使用习惯。
- 可以在操作前即时标记受保护节点、已有项目组件和不可移动边界。
- 复用项目现有的验证、增量合并、事务保存和失败回滚能力。
- 用户无需理解 Web session、token 或浏览器重连。

### 风险

- 直接在资产上边拖边保存会使 AI 误判或误拖立即污染 Prefab。
- 真实 Prefab 中可能有脚本引用、嵌套 Prefab、组件顺序和项目自定义边界，不能只按视觉分组。
- Unity 原生窗口需要采用 UI Toolkit TreeView 或可靠的 TreeView 实现；继续堆叠 IMGUI 卡片并不能获得完整编辑器体验。

## 推荐架构：原生暂存编辑，显式事务应用

### 1. 建立编辑会话

- 从真实 Prefab 与 `PsdHierarchyProfile` 读取节点、稳定 ID、组件、边界和现有分组。
- AI 只返回 `PsdHierarchyPlan`，先由 `PsdHierarchyPlanValidator` 验证。
- 不创建 HTML 编辑会话，不改变原 Prefab。

### 2. Unity 原生窗口

- 使用 UI Toolkit `TreeView` 显示真实层级结构，而非图层卡片墙。
- 根节点下明确显示：AI 建议组、已有组、未分组图层、受保护节点。
- 允许多选和拖放；拖放只更新内存中的 draft plan 或隔离的 Prefab 内容，不立即保存资产。
- 右侧显示所选节点的名称、稳定 ID、组件、AI 证据、置信度和保护原因。
- 顶部提供筛选：仅看 AI 建议、仅看未分组、仅看可修改、仅看受保护。

### 3. 应用和回滚

- “应用”前再次校验 fingerprint、稳定 ID、计划完整性和受保护边界。
- 使用现有 `PsdHierarchyApplier` 和 `PsdPrefabTransactionalSave` 写入 Prefab。
- 应用失败时使用既有 rollback；应用成功后重新读取 Prefab，显示真实最终树和 diff。
- 浏览器报告若保留，只显示这个最终只读结果。

## 不推荐的两种极端

### 不推荐：继续扩展 HTML 编辑器

这会继续投入在拖放命中、session 重连、DOM 选择、浏览器坐标和同步问题上。即使暂时修好一个手势，也无法让它成为 Unity Prefab 的可靠编辑器。

### 不推荐：每次拖动立即保存真实 Prefab

这会把预览、AI 建议和实际资产混在一起。批量分组、错误的 AI 计划或误拖都可能污染 Prefab，并让撤销、增量身份和失败恢复复杂化。

## 实施决策

1. 停止新增 HTML 工作台的编辑功能；已存在网页接口可在完成迁移后删除或降级为只读报告。
2. 保留 `PsdHierarchyPlan`、验证器、应用器、事务保存和增量合并，不重写这些安全核心。
3. 将 `PsdHierarchyOrganizerWindow` 从当前 IMGUI 预览窗迁移为 Unity 原生的 UI Toolkit 层级编辑窗口。
4. 以“真实 Prefab 数据 + draft plan”为唯一编辑模型；浏览器不再有独立的可编辑层级状态。
5. 在原生窗口完成一条端到端路径后，再决定是否保留只读 HTML 分析报告。

## 验收标准

- 用户能在 Unity 窗口中看到完整且真实的 Prefab 层级。
- 用户能多选图层并拖入已有组或创建组，且拖动结果即时反映在原生树中。
- 受保护层、含项目组件层和已确认层不能被错误移动。
- “取消”不修改 Prefab；“应用”后可从 Prefab Stage 读取到同一层级。
- 应用失败后 Prefab/Profile 回到应用前状态。
- 浏览器关闭、网络异常或 Unity 编译不会影响正在编辑的 draft plan；如发生域重载，原生窗口可以从会话数据重新建立，或明确提示重新打开，而不会产生第二套层级真相。

## 代码依据

- `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebWorkbench.cs`: HTML server/session 创建、浏览器打开，以及域重载时 shutdown。
- `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/Web/PsdHierarchyWebController.cs`: Web endpoint 对预览模型执行分析、移动、接受和应用。
- `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyOrganizerWindow.cs`: 当前 preview model、手动移动和计划验证入口。
- `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyPlanValidator.cs`: 应用前的计划合同与层级验证。
- `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyApplier.cs`: 层级变更和失败 rollback。
- `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdPrefabTransactionalSave.cs`: Prefab 的事务式保存。
