# ADR 0002：Chat 与 CLI 共用 Unity 清理核心

## 状态
已接受。

## 决策
Chat 和 CLI 都只提交 v2 计划或调用明确的 Unity Editor 命令。Prefab、AssetDatabase、嵌套边界、序列化引用、预检、Apply、重快照和 Verify 全部由 Unity 侧共享核心负责。

## 原因
Unity 是 Prefab 和序列化引用的权威运行环境。保留 Python 或 CLI 的第二套执行实现会让同一计划产生不同结果。

## 结果
Python 工具保留只读快照与诊断用途；正式执行必须经过 Unity 共享核心，并输出可审计的阶段结果。
