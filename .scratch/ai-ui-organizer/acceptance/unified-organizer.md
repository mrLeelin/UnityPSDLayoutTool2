# 统一整理入口验收

- Unity 项目：E:/Project/Demo/monsterhunter。
- 最终常规回归：Psd2UIForm.Tests 45/45 通过，编译无错误。
- 真实 codex-cli：RealCli_ReturnsUnifiedPlanAndPublishes 通过（167.4 秒）。使用隔离的 PSD 副本，调用已有 provider，真实返回改名与公共组件建议，再经预览和发布生成资产；测试后删除副本。
- 受控测试覆盖：解析保留扩展字段和 owner ID 映射；分组、移动、改名后按新节点 ID 抽取；预览不修改源；未知节点拒绝；源变化拒绝；取消候选清理；发布保持全局输出设置；规则可再次复用。
- 修复实测发现的旧分组错误：HasConverterInstance 把编辑器逻辑对象强转 UnityEngine.Object；改用现有兼容接口。
- 审查修复：加载磁盘源后重新比对分析输入，发布回滚逐项尝试，预览不登记全局 Undo，发布不改全局导出默认值。
- 范围：首次统一整理生成已接通；已有输出的 PSD 更新合并、运行时状态切换和完整画面人工验收不在本次完成声明中。
