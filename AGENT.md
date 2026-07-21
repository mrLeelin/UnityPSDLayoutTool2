# AGENT.md

## 项目定位

本仓库是 `UnityPSDLayoutTool2`，一个将 Photoshop PSD 图层导入 Unity 的 Editor 插件。

- 插件目录：`Assets/PSDLayoutTool2`
- 主要代码：`Assets/PSDLayoutTool2/Editor`
- PSD 解析代码：`Assets/PSDLayoutTool2/Editor/PsdFile`
- 命名空间：`PsdLayoutTool2`
- README 中已验证的 Unity 版本：`6000.3.7f1`
- 许可证：MIT，详见 `LICENSE.md`

## 修改原则

- 优先保持现有导入、布局、Prefab 生成行为兼容；不要为了局部修复重写 PSD 解析流程。
- 修改前先阅读相关模块和 `README.md`，并检查 `git status`，避免覆盖用户已有改动。
- 只修改完成任务所需的文件；不要提交 Unity 生成的 `Library/`、临时日志、缓存或本地设置。
- Unity 资源文件的 `.meta` 文件必须与资源一起维护；新增或删除资源时检查对应 `.meta`。
- Editor 专用代码必须留在 `Editor/` 路径下，避免把 UnityEditor 依赖带入运行时程序集。
- 新增或修改 C# 文件时保留 `PsdLayoutTool2` 命名空间，并遵循相邻文件的命名和结构风格。
- 中文或其他非 ASCII 文本写入后，重新打开文件检查是否出现 `???`、乱码、意外 BOM 或错误转义。

## 功能边界

- PSD Smart Object 不在当前解析支持范围内；相关测试素材应先栅格化。
- `|Animation` 当前只支持非 Unity UI 模式。
- `|Button` 当前只支持 Unity UI 模式。
- 文本层、透明度、蒙版 Alpha、隐藏层、同级重名和非法文件名字符都属于已有兼容行为，修改时应避免回归。
- 目标 Canvas 对齐、Canvas 尺寸映射、等比缩放和按名称锚点是相互关联的行为，调整其中一项时应检查其他模式。

## 验证要求

根据改动范围选择最小但足够的验证：

1. C# 修改：在目标 Unity 版本打开项目并等待编译完成，确认 Console 无新增编译错误。
2. PSD 解析或纹理导出修改：使用包含中文、透明度、蒙版、隐藏层和重名图层的 PSD，验证纹理导出结果。
3. 布局修改：分别验证目标 Canvas 模式和无目标 Canvas 时的 World Space Canvas 回退；检查等比与非等比缩放。
4. Prefab 修改：执行生成 Prefab，确认层级、资源引用、锚点、渲染顺序和透明度正确。
5. 诊断相关修改：检查日志目录 `<Unity项目根目录>/Library/PSDLayoutTool2/Logs`，确认关键阶段和异常堆栈可追踪。

如果无法启动 Unity 或执行真实 PSD 导入，必须明确说明只完成了静态检查，不能把编译或代码检查当作运行时验证。

## 输出与日志

- 默认生成资源目录、Prefab 输出位置和 Inspector 语言等行为以现有 Inspector 配置为准。
- 不要把 `Library/PSDLayoutTool2/Logs` 中的日志加入版本控制。
- 调试导入失败时，优先查看最新诊断日志中的 PSD 读取、输出路径、图层导出、`ImportAsset` 和 Prefab 保存阶段。

## Git 约束

- 保留无关的工作区修改，只暂存本次任务涉及的文件。
- 提交前检查 `git diff`、`git diff --cached` 和 `git status`。
- 若创建提交，提交信息应说明修改意图、约束、验证结果和未验证项；未明确要求时不要自动 push。
