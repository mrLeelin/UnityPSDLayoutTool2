# AI 整理 UI 本地任务

状态：01—03、05—06 已实现。统一 Inspector「AI 整理 UI」入口已打通整理层级、改名和公共 Prefab 抽取。真实 codex-cli 分析到发布验收通过。04 的 PSD 源更新合并和 07 的完整更新验收仍未完成。

使用：保存 PSD 编辑树 → Inspector「AI 整理 UI」→ 指定 Assets 下的新输出文件夹 →「AI 分析整理方案」→ 调整名称及实例选择 →「生成完整预览」→「应用整理并保存 UI 与公共 Prefab」。默认后台调用已有 CLI，终端可选。

发布保存该编辑树及新输出目录中的 UI、Images、Common 和抽取规则，不改变全局输出路径设置。此后通过原生成入口再次生成时，应选择此次输出目录；输入未变时复用已有结果，源更新合并仍由 04 实现。当前不覆盖已有输出目录。

[规格](spec.md)

- [01-manual-extraction](issues/01-manual-extraction.md)
- [02-repeat-generation](issues/02-repeat-generation.md)
- [03-state-extraction](issues/03-state-extraction.md)
- [04-preserve-edits](issues/04-preserve-edits.md)
- [05-organizer-window](issues/05-organizer-window.md)
- [06-skill-analysis](issues/06-skill-analysis.md)
- [07-integration](issues/07-integration.md)
