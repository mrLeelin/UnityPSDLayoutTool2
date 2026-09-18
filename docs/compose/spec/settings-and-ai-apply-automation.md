---
feature: settings-and-ai-apply-automation
status: delivered
updated: 2026-09-17
branch: main
commits: e81265f4d47a4c764c60ba2cb7d6ec4b61220bdc..uncommitted
---

# Settings & AI Apply Automation

## Report

**What was built** — 三项能力：(1) 9528 设置页在编译域重载后用 SessionState 自动恢复；(2) 团队共享 asset 与 `UserSettings/PsdLayoutTool2/user-settings.json` 个人配置拆分，含首次迁移；(3) 终端/外部 AI 在人工审核后写 `.apply` 哨兵，Unity 自动 Apply 并回写 `.apply-result.json`，Inspector 去掉「应用AI计划」。

**Verification** — 独立 Reviewer 静态评审：验收标准均满足；无 critical。3 个 major（Load 缓存可变污染、损坏 JSON 阻断迁移、SKILL.md R6 过时）已修复；AI Set 恢复 Trim，工程根与 watcher 统一优先 `Application.dataPath` 父目录。Unity 编译/EditMode 套件未在本环境执行。

**Journey log**
- 哨兵 Apply 必须配套 `apply-result.json`，否则终端 AI 收不到校验错误，无法自愈。
- 迁移不能只看 `File.Exists`，损坏 JSON 会永久跳过播种。
- `Load()` 必须返回副本，否则 Save 失败时内存与磁盘不一致。

## [S1] Problem

1. 网页设置服务（9528）在脚本编译域重载后被关掉且不恢复，浏览器网址打不开。
2. 共享 `PsdLayoutProjectSettings.asset` 混放团队管线配置与个人 AI/端口/可视化偏好，容易被提交进 git 互相覆盖。
3. 「AI整理」只产出计划，必须再点「应用AI计划」；审核在终端完成，回 Unity 多一次点击。
4. 哨兵自动 Apply 失败时错误只在 Unity 对话框，终端 AI 读不到，无法自行修订。

## [S2] Design

### S2.1 Web server resume

- `SessionState` 键 `PsdLayoutTool2.SettingsWebServer.Resume`。
- 编译前 `CloseListenerOnly`（保留标记）；退出/重新 Open 清标记。
- `[InitializeOnLoadMethod]` + `delayCall` 静默 `Start`，不 `OpenURL`。

### S2.2 Shared vs personal settings

- 共享 asset：字体、命名前缀、输出、组件类型、`autoCrop`、cleanup backend。
- 个人 JSON：`UserSettings/PsdLayoutTool2/user-settings.json`（AI 四元组、preview port、`showImageMarkers`）。
- `PsdLayoutProjectSettings` 的 Resolve/Set 收口分流；首次无 JSON 时从 asset 播种并重置 asset 个人字段。
- API Key 仍 EditorPrefs。

### S2.3 Apply sentinel + result feedback

- 开终端/复制提示词时写 `*.session.json`（psd/prefab/plan/review 路径）。
- 人工审核后 AI 写 `*.apply`。
- Watcher 轮询 `Library/PsdHierarchyTerminal/*.apply`，`ApplyConfirmedAsync`，写 `*.apply-result.json`（success/status/message）。
- 哨兵改名 `.applied` / `.apply-failed`；契约要求 AI 轮询 result 并在失败时按 message 用 `node:<id>` 修订。
- Inspector 移除「应用AI计划」；保留「AI整理」+「AI提示词复制」。

## [S3] Out of Scope

- 不改共享字段进 JSON。
- 不改 API Key 存储。
- 不自动改使用方工程 `.gitignore`。
- 不把「AI整理」改回内置 ChatWindow。
- 不做 HTTP Apply 接口。

## Tasks

- [x] T1: Web 服务编译后自动恢复 — acceptance: 编译后 localhost:9528 可访问且不重复弹浏览器 (covers: S2.1)
- [x] T2: 个人配置 UserSettings JSON + 迁移 — acceptance: 改 AI/端口不 dirty 共享 asset 个人段；首次迁移播种 (covers: S2.2)
- [x] T3: Apply 哨兵自动应用并移除手动按钮 — acceptance: 终端写 .apply 后 Prefab 更新；Inspector 无「应用AI计划」(covers: S2.3)
- [x] T4: apply-result.json 回写给 AI — acceptance: 失败时 result.message 含校验错误全文；契约含轮询说明 (covers: S2.3)
- [x] T5: 独立 Reviewer 评审 — acceptance: 无 critical；major 已修复 (covers: S1; S2.1; S2.2; S2.3)
