# AI 整理 UI 本地任务

状态：01—03、05—06 已实现。统一 Inspector「AI 整理 UI」入口已打通整理层级、改名和公共 Prefab 抽取。真实 codex-cli 分析到发布验收通过。04 的 PSD 源更新合并和 07 的完整更新验收仍未完成。

使用：保存 PSD 编辑树 → Inspector「AI 整理 UI」→ 指定 Assets 下的新输出文件夹 →「AI 分析整理方案」→ 调整名称及实例选择 →「生成完整预览」→「应用整理并保存 UI 与公共 Prefab」。默认后台调用已有 CLI，终端可选。

方案不满意时：在窗口里的整理树点击节点，修改名称、父节点或同级顺序；也可填写「修改反馈」，点击「按反馈调整当前方案」。AI 修订会携带当前方案（组件范围以当前勾选为准），成功才替换当前方案；失败或取消保留上一版。「撤销上一次层级或 AI 修改」可以回到之前的方案。每次修改后需重新生成完整预览，应用前原编辑文件不变。这里修改的是待应用的方案，尚不提供发布后的一键撤销。

发布保存该编辑树及新输出目录中的 UI、Images、Common 和抽取规则，不改变全局输出路径设置。此后通过原生成入口再次生成时，应选择此次输出目录；输入未变时复用已有结果，源更新合并仍由 04 实现。当前不覆盖已有输出目录。

[规格](spec.md)

- [01-manual-extraction](issues/01-manual-extraction.md)
- [02-repeat-generation](issues/02-repeat-generation.md)
- [03-state-extraction](issues/03-state-extraction.md)
- [04-preserve-edits](issues/04-preserve-edits.md)
- [05-organizer-window](issues/05-organizer-window.md)
- [06-skill-analysis](issues/06-skill-analysis.md)
- [07-integration](issues/07-integration.md)
