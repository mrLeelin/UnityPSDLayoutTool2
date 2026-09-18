# ADR 0002：Chat 与 CLI 共用 Unity 清理核心

## 状态
已接受。

## 决策
Chat 和 CLI 都只提交 v2 计划或调用明确的 Unity Editor 命令。Prefab、AssetDatabase、嵌套边界、序列化引用、预检、Apply、重快照和 Verify 全部由 Unity 侧共享核心负责。

**2026-09-17 落地补充（实现对齐）：**
- 设置中的 `UnityCliRunner` 后端选项已移除；`Resolve` 将历史 CLI 值归一为 `NativeUnity`。
- `render_prefab_cleanup.py` 的 `apply`/`reapply` 已退役；脚本仅保留只读 `verify`/诊断（仍要求 v1 快照格式）。
- 正式 Apply 路径：终端/外部 AI 写 v2 计划 + 人工审核后的 `*.apply` 哨兵 → Unity Native 执行 → `*.apply-result.json` 回执。
- Python 载荷执行器（`PsdHierarchyNativePayloadExecutor`，`Process.Start("python", render_prefab_cleanup.py)`）
  与旧 Runner 兼容入口（`RequiresUnityCliRunner`／`TryValidatePlanCapabilities`／`BuildReapplyPreflightPlan`／
  无上下文的 `Validate`/`Apply`）已从交付面删除；`PsdHierarchyLegacyRemovalTests` 锁定它们不再出现。
- v2 Replay Profile 逐阶段保存**节点绑定证据**（`path`+`name`+`siblingIndex`+`components`），
  重放时在重新生成的快照上要求唯一对应关系后再经共享核心执行；v1 计划或无证据阶段永久要求重新分析。

## 原因
Unity 是 Prefab 和序列化引用的权威运行环境。保留 Python 或 CLI 的第二套执行实现会让同一计划产生不同结果。

## 结果
Python 工具保留只读快照与诊断用途；正式执行必须经过 Unity 共享核心，并输出可审计的阶段结果。
