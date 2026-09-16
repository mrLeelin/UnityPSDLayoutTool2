# AI 整理网页工作台

在 PSD 编辑树 Inspector 点击「AI 整理 UI」，或选择编辑树根节点，执行 `Tools/PSD2UIForm/AI 整理 UI（网页）`。

1. 点击「开始整理」，可先在输入框补充要求。使用项目已有的 AI Provider 配置和认证。
2. AI 返回完整整理方案后，Unity 自动生成临时候选 Prefab、提取公共组件并渲染预览。
3. 在图中或层级中选择节点，输入修改意见。选区只是辅助定位，也可以直接描述整体修改。
4. 修订会带上当前完整方案，并产生新版本。点击历史版本可恢复方案，Unity 会重新生成该版本预览。
5. 点击「采用当前版本」保存真实 UI 和公共 Prefab，同时更新源编辑树。采用结束后，请从编辑树打开新会话。

原生整理窗口仍可通过 `Tools/PSD2UIForm/AI 整理 UI（原生窗口）` 打开。

## 执行与状态

HTML 不执行 Unity 资产写入，也不直接持有 AI Provider 密钥。工作台服务只监听 `127.0.0.1` 的动态端口。页面地址 fragment 携带独立会话凭据；API 校验凭据及 Origin，不开放跨源访问。

`POST /command` 的 202 只代表入队，响应返回 requestId。页面轮询 `GET /state`，等 lastRequestId 确认 Unity 已执行命令；AI 任务仍需等待 running=false 和实际结果。每条命令绑定 sessionId 与 revision，过期页面、重复提交、运行中修改和旧任务回调不能覆盖当前方案。

命令包括 start、revise、cancel、restore、apply。分析或候选生成失败保留上一版；版本切换也先生成成功再替换。发布沿用现有来源指纹和候选资产校验。

## 预览与验证边界

图片来自 Unity 在独立 PreviewScene 内渲染实际候选 Prefab；内置管线使用 Camera.Render，SRP 使用 [StandardRequest 渲染请求](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/User-Render-Requests.html) 输出到 RenderTexture，不支持该请求的管线会明确报错。不修改活动 Scene，也不在浏览器重画 Unity UI。画面使用 PSD 文档尺寸，最长边最高 1600 像素。

点击定位来自生成元数据中稳定节点 ID 与实际 RectTransform 的映射。同结构公共组件保留地址；状态提取会重建内部层级，只保留精确根映射，内部节点仍可从树选择。无唯一映射时不猜测节点。

结构与提取校验通过不代表自动视觉验收通过。页面明确显示「画面待检查」；运行态动画、自定义脚本和设备适配仍须在真实场景验证。

当前版本历史仅保留在本次编辑器会话中。重新编译、退出 Unity 或打开另一来源会关闭服务并清理未采用候选，旧页面显示连接失效。尚未实现跨重启恢复、采用后撤销以及对已有输出目录的更新合并。输出必须是 Assets 下尚不存在的新目录。

## 验证入口

- `AiOrganizerWebTests`：过期会话/版本、运行中互斥、采用后关闭、同源凭据边界。
- `AiOrganizerWorkflowTests.WebSession_RendersVersionsRejectsInvalidResultAndRestoresPreviousPlan`：实际 Prefab 渲染、节点映射、修订版本、失败保留、恢复与采用。
- `AiOrganizerWorkflowTests.WebBridge_QueuedHttpRequestReceivesMainThreadRejection`：实际 HTTP 202 入队响应、requestId 执行确认、过期版本拒绝。
- 真实 AI 多轮验证须单独运行带 Explicit 标记的集成测试，不能由受控方案测试替代。

### 2026-09-15 本机验证

Unity 6000.3.6f1 / URP 下编译通过；`Psd2UIForm.Tests` 非 Explicit 测试 64/64 通过（68.47 秒）。Playwright 验证了工作台连接、宽/窄布局、编译后旧会话断开并禁用按钮，以及网页发起真实 AI 请求的入队/执行状态。

外部 Claude CLI 的双轮 Explicit 测试尚未通过：出现过非 JSON 询问、空 owners、以及与源图层不兼容的 Text owner。校验器拒绝这些结果，源 Prefab 未覆盖。此项仍是 AI 输出可靠性验证缺口，不能用 64 项受控测试的通过代替。

七日任务通过真实网页进行了两次首次整理。第一次在 `psd:2` 的 Layer → Text 操作处拒绝；补充通用源图层能力提示后，第二次在 `psd:68` 的 Layer → Panel 操作处拒绝。两次都未产生可采用版本，未发布、未覆盖源编辑树。因此“七日任务全自动整理 + 自然语言二次修订”的端到端验收仍未完成；下一步需要解决识别方案的能力约束与错误自动修正，而非放宽本地资产校验。

### 2026-09-15 类型兼容修复

识别解析器和方案编译器共用源图层能力判断：nodeLabels 中不兼容的 Panel/Text 等基础标签回落到源节点支持的基础类型。合法 Panel 图层组、Null 标签与 owner 生成容器逻辑保留。整理引用兼容完整 `owner:<id>` 和精确的裸 owner ID，裸引用与现有源节点 ID 重名时优先保留源节点；未知引用仍由严格校验拒绝。

新增 `AiRecognitionCapabilityTests` 10 项回归。最小用例先复现 Layer → Panel 拒绝和裸 owner 改名目标不存在；修复后 `Psd2UIForm.Tests` 74/74 通过（76.01 秒）。保存的最新七日任务方案经真实解析、编译、校验链路生成 158 项操作和 3 组组件建议；这不替代真实资产预览或新一轮 AI 验收。

随后通过真实 WebSession.AcceptResult 路径成功生成 V1 候选及 369838 字节 PNG。资产导入超过 CLI 60 秒等待时限，但后台完成，HTTP state 确认 V1。与 PSD 原图对比仍有任务条背景偏移和部分文字缺失，因此本次会话明确标记视觉未通过，并暂停采用；并未发布。源 Prefab SHA-256 仍为 `DA72D0E1CC227F186C7DE50B5E42C255CCE066E45FF06D2E477E5E8B3CA4436F`。能力异常已修复，整体整理质量和真实 AI 二次修订仍未验收通过。
