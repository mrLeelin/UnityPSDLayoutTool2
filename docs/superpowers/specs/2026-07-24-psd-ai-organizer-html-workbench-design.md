# PSD AI 整理 HTML 工作台正式方案

## 1. 结论

PSD Inspector 中的“AI 整理”按钮不再打开复杂的 Unity `EditorWindow`，而是启动一个仅绑定本机的临时服务，并打开正式 HTML 工作台。

HTML 负责完整画布、拖动缩放、分组框选、结果解释和用户确认。Unity C# 继续负责 PSD 解析、AI 调用、计划校验、Prefab 修改、失败回滚和资源保存。浏览器不能直接访问或修改工程文件。

本方案取代以下界面方案，但保留它们已经实现的底层安全能力：

- `2026-07-23-hierarchy-preview-panel-design.md`
- `2026-07-23-hierarchy-workbench-design.md`
- `PsdHierarchyOrganizerWindow` 的三栏预览交互

`2026-07-22-psd-ai-hierarchy-organizer-design.md` 中关于稳定 ID、严格计划校验、增量 Profile、事务应用、项目对象保护和失败回滚的约束继续有效。

## 2. 用户入口

PSD Inspector 保留一个主入口：

`AI 整理`

点击后的固定流程：

1. 通过现有 `PsdHierarchyOrganizerEntry.TryResolveAvailability` 解析唯一目标 Prefab。
2. 构建 PSD、Prefab、Profile 和稳定 ID 快照。
3. 创建一次本地 HTML 会话。
4. 启动仅监听 `127.0.0.1` 随机端口的临时服务。
5. 使用 Unity 的浏览器打开能力打开本地 URL。
6. HTML 加载完整 PSD 合成图和当前层级。
7. 用户在 HTML 中完成整理、重整、确认和应用。

入口不再调用 `PsdHierarchyOrganizerWindow.Open(...)`。底层输入仍由 `PsdHierarchyOrganizerEntry.BuildFromAssets(...)` 生成，避免出现两套 Prefab 解析和 AI 请求逻辑。

如果目标 Prefab 不存在、当前不是 Unity UI 模式、PSD 数据不可用或存在阻断性导入错误，Inspector 按钮保持禁用并显示现有原因，不启动空白网页。

## 3. 正式 HTML 界面

### 3.1 页面结构

页面只保留三个主要区域：

1. 顶部工具栏
   - 当前 PSD 和目标 Prefab。
   - 拖动查看、框选成组。
   - 缩小、缩放百分比、放大、适应宽度。
   - 显示或隐藏 AI 分组。
   - 当前状态：分析中、预览、应用中、完成或错误。

2. 完整画布
   - 显示完整 PSD 合成图，不再只展示局部缩略图。
   - 鼠标拖动平移画布。
   - 鼠标滚轮或按钮缩放。
   - 小地图显示当前视口位置。
   - AI 建议组以彩色边框覆盖在真实画面上。
   - 点击边框选择一个组。
   - 按住 `Shift` 拖动，或切换到“框选成组”，创建自定义候选范围。

3. 右侧组检查器
   - 当前组的来源节点。
   - 整理前名称和建议名称。
   - 建议层级。
   - AI 判断依据和风险。
   - 该组包含的真实节点。
   - 公共 Prefab 候选。
   - 接受当前分组、重新整理当前组。

页面底部只放全局动作：

- `调整选中区域`
- `应用命名与层级`
- 命名和层级应用完成后显示 `处理公共 Prefab`

默认不显示技术 ID、置信度表格、原始 JSON、固定三栏树或全部组的详细成员列表。技术信息放在可折叠的“诊断信息”中。

### 3.2 结果表达

结果不能只显示“重命名 26 个、层级调整 8 处”。

每个建议组必须同时提供：

- 完整画面上的实际范围。
- 真实整理前名称。
- 建议名称。
- 简化后的建议树。
- 代表性的前后变化，例如 `组 1 -> DailyTaskList`。
- 公共组件判断，例如 `DailyTaskItem × 5`。
- 风险说明，例如“其中一个实例带额外脚本绑定，暂不合并”。

数量只作为辅助信息。

### 3.3 多组重新整理

用户可以：

- 点击一个组重新整理。
- `Ctrl` 点击选择多个 AI 分组。
- 拖框生成一个自定义候选范围。
- 为本次重整输入一句补充要求。

重新整理请求只包含选中范围、直接邻居和只读保护边界。已接受且未选中的组保持锁定。返回结果是局部 diff，不替换完整计划。

“二次 AI 修复”统一改名为：

`重新整理选中区域`

## 4. 公共 Prefab 流程

公共 Prefab 是第二阶段，不能和首次命名、层级应用混在同一个确认动作中。

固定流程：

1. AI 分析时同步产生公共组件候选，但不创建资源。
2. 用户先应用命名和层级。
3. HTML 显示公共 Prefab 候选列表。
4. 用户勾选候选并确认。
5. Unity C# 创建选中的 Prefab，并将对应节点替换为实例。

候选必须同时满足：

- 语义职责一致。
- 子节点角色和结构兼容。
- Unity 组件类型兼容。
- 脚本绑定、序列化引用和嵌套 Prefab 边界允许合并。
- 可变化内容可以表达为文本、Sprite、状态或显式变体。
- 至少有两个实例。

相似外观不是充分条件。带额外业务脚本、不同组件结构、跨保护边界或引用关系不明确的节点只显示为“需要确认”，默认不勾选。

第一版采用“AI 提供候选，用户勾选后创建”，禁止全自动批量创建。

## 5. 本地服务架构

### 5.1 组件

建议新增以下 Editor-only 单元：

- `PsdHierarchyWebEntry`
  - 接替当前窗口入口。
  - 创建或复用当前 PSD 的 HTML 会话。
  - 打开本地 URL。

- `PsdHierarchyWebSession`
  - 保存当前 PSD、目标 Prefab、Profile、预览模型、工作计划、选择状态和任务状态。
  - 只保存稳定 ID，不把浏览器显示名称当作身份。

- `PsdHierarchyWebServer`
  - 使用 Editor-only C# 启动轻量本地 HTTP 服务。
  - 只绑定 `127.0.0.1` 和系统分配的随机端口。
  - 不引入 Node、npm、React、WebView 或外部服务依赖。

- `PsdHierarchyWebApi`
  - 把 HTTP 请求映射到现有 AI runner、计划校验器、应用器和 Prefab 候选分析器。
  - 所有 Unity API 操作切回 Unity 主线程。

- `PsdHierarchyWebSnapshotBuilder`
  - 生成完整 PSD 合成图、真实节点矩形、当前 Prefab 树和组覆盖数据。

- `Web/`
  - `index.html`
  - `organizer.css`
  - `organizer.js`
  - 无构建步骤、无远程 CDN、无线上字体。

所有 C# 文件放在 `Editor/PsdPrefab/Hierarchy/Web/` 下。HTML 静态资源放在同一 Editor 模块的 `Web/Static/` 下，不进入运行时构建。

### 5.2 临时文件

会话生成物放在：

`Library/PSDLayoutTool2/HierarchyWebSessions/<session-id>/`

包括：

- `composite.png`
- `snapshot.json`
- `draft-plan.json`
- 诊断日志

这些文件不写入 `Assets/`，不触发 Unity 资源导入，也不提交 Git。会话正常结束、超时或 Unity 退出后清理。

### 5.3 浏览器通信

页面通过本地 JSON API 与 Unity 通信：

- `GET /api/session`
  - 当前 PSD、目标 Prefab、状态和页面能力。

- `GET /api/composite`
  - 完整 PSD 合成图。

- `GET /api/snapshot`
  - 当前层级、稳定 ID、节点矩形和现有建议组。

- `POST /api/analyze`
  - 启动首次完整 AI 整理。

- `POST /api/refine`
  - 重新整理选中的一个或多个范围。

- `POST /api/accept`
  - 接受或解锁指定建议组。

- `POST /api/apply-hierarchy`
  - 调用现有严格校验和事务应用流程。

- `GET /api/prefab-candidates`
  - 获取公共 Prefab 候选。

- `POST /api/create-prefabs`
  - 创建用户勾选的候选并替换实例。

- `GET /api/status`
  - AI、应用和 Prefab 创建进度。

长任务采用状态轮询，不引入 WebSocket。浏览器每 500 毫秒轮询活动任务，空闲时停止轮询。

浏览器提交的数据只能包含会话令牌、稳定 ID、选择矩形、用户补充要求和明确动作。浏览器不能提交任意磁盘路径、命令、C#、资源属性或序列化补丁。

## 6. 安全边界

- 服务只监听 `127.0.0.1`，不监听局域网地址。
- 每次会话生成不可预测的会话 ID 和访问令牌。
- 所有 API 都校验令牌、Host 和会话状态。
- HTML 不加载任何远程脚本、图片、字体或分析服务。
- AI 仍然只生成计划 JSON，不能写 Unity 资源。
- HTML 不能绕过 `PsdHierarchyPlanValidator`、保护边界、稳定 ID 或指纹检查。
- 应用请求必须在 Unity 中重新读取当前 Prefab 和 Profile，避免浏览器使用过期快照。
- Prefab、Profile 和公共组件创建全部经过 C# 事务流程。
- 失败时恢复原 Prefab 和 Profile；HTML 显示失败原因和未应用状态。
- 同一个会话的应用动作在执行期间必须幂等并禁用重复提交。

## 7. 数据与坐标

完整画布以 PSD 像素坐标作为唯一显示坐标：

- 原点位于 PSD 左上角。
- 节点矩形由稳定图层 ID 和 PSD bounds 产生。
- Unity `RectTransform` 只用于交叉验证，不作为浏览器身份。
- 画布缩放和平移只影响显示，不修改节点数据。
- 拖框坐标先转换回 PSD 像素坐标，再由 Unity 计算相交节点。
- 组边框是成员叶节点 bounds 的并集，可增加固定视觉留白，但命中计算使用原始 bounds。

完整合成图优先使用现有 PSD 解码器生成。若合成图生成失败，页面仍可显示节点矩形和层级，但必须明确标记“画面预览不可用”，不能显示空白成功页。

## 8. 状态与恢复

页面状态：

1. `Preparing`
2. `Analyzing`
3. `Reviewing`
4. `Refining`
5. `ApplyingHierarchy`
6. `ReviewingPrefabCandidates`
7. `CreatingPrefabs`
8. `Completed`
9. `Failed`
10. `Disconnected`

Unity 域重载或服务退出时，HTML 显示“Unity 会话已断开”，不假装操作成功。

当前草稿计划写入 `Library` 会话目录。重新点击同一个 PSD 的“AI 整理”时，如果草稿的 PSD GUID、目标 Prefab 路径和指纹仍然一致，页面可以恢复草稿；否则创建新会话。

同一个 PSD 同时只允许一个可写会话。再次点击时聚焦或重新打开现有 URL，不创建两个互相覆盖的应用会话。

## 9. 与现有代码的关系

继续复用：

- `PsdHierarchyOrganizerEntry.TryResolveAvailability`
- `PsdHierarchyOrganizerEntry.BuildFromAssets`
- `IPsdHierarchyAiRunner`
- `CodexCliHierarchyRunner`
- 严格计划 JSON 解析
- `PsdHierarchyPlanValidator`
- `PsdHierarchyProfile`
- `PsdHierarchyApplier`
- `PsdHierarchyApplyVerifier`
- `PsdPrefabTransactionalSave`
- `PsdHierarchyPrefabCandidateAnalyzer`

界面替换后：

- `PsdInspector` 的 AI 按钮调用 `PsdHierarchyWebEntry.Open(assetPath)`。
- `PsdHierarchyOrganizerWindow` 不再作为用户主入口。
- 现有窗口可在迁移期间保留为诊断入口，但不出现在普通工作流中。
- HTML 不能复制一套独立的计划校验或应用规则。

## 10. 第一版明确范围

第一版包含：

- PSD Inspector 一键打开正式 HTML。
- 完整 PSD 画布。
- 平移、缩放、小地图。
- AI 分组覆盖框。
- 单组、多组和拖框选择。
- 命名与层级建议。
- 选中区域重新整理。
- 接受和锁定分组。
- 事务应用命名与层级。
- 公共 Prefab 候选确认与创建。
- 页面断线、错误和过期状态。

第一版不包含：

- 远程部署网站。
- 用户账号或云端存储。
- 多人实时协作。
- 浏览器直接编辑 Unity 序列化字段。
- 浏览器内直接修改 Sprite、材质、文本内容或九宫格设置。
- 无确认的全自动公共 Prefab 创建。
- 把 HTML 嵌入 Unity WebView。

正式界面使用系统浏览器。后续如果确实需要嵌入 Unity，再单独评估 WebView 依赖，不作为本次实现条件。

## 11. 完成标准

实现完成必须满足：

- 点击 PSD Inspector 的“AI 整理”只打开一个本地 HTML 会话。
- 页面展示真实完整 PSD，而不是示例图或局部卡片。
- 用户可以拖动、缩放、点击组和拖框选择。
- 页面所有整理前名称和节点数量来自真实 PSD/Prefab。
- 建议名称明确标记为 AI 建议。
- 多组选中重整不会修改已锁定的其他组。
- HTML 无权直接修改工程文件。
- 应用仍通过现有严格校验、保护边界和事务流程。
- 公共 Prefab 只有在用户勾选确认后创建。
- Unity 关闭或会话失效时，页面进入断开状态。
- 临时会话文件不进入 `Assets/`，不污染 Git。

## 12. 后续实施顺序

1. 建立本地会话和 HTTP 服务。
2. 把 V4 完整画布交互整理为正式静态 HTML/CSS/JS。
3. 接入真实 composite、snapshot 和 status API。
4. 接入首次 AI 分析与局部重整。
5. 接入接受、锁定和草稿恢复。
6. 接入现有命名与层级事务应用。
7. 接入公共 Prefab 候选和确认创建。
8. 将 PSD Inspector 主入口切换到 HTML。
9. 保留旧窗口作为隐藏诊断入口，完成迁移后再决定是否删除。
