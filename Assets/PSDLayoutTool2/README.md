# Unity PSD Layout Tool 2

Unity 编辑器 PSD 导入工具，可导出图层纹理、在场景中生成布局，并创建可重复导入的 Prefab。

## 要求

- Unity `6000.3` 或更高版本
- Package Manager 可访问本包声明的 `uGUI`、`Newtonsoft Json` 和 `Unity Test Framework` 依赖

## 安装

在 Unity 中打开 `Window > Package Management > Package Manager`，选择 **Install package from git URL**，输入：

```text
https://github.com/mrLeelin/UnityPSDLayoutTool2.git
```

也可以选择 **Install package from disk** 并定位到仓库根目录的 `package.json`，或选择 **Install package from tarball** 安装发布的 `.tgz` 文件。

建议在正式项目中把 Git URL 固定到标签或提交，例如：

```text
https://github.com/mrLeelin/UnityPSDLayoutTool2.git#v0.1.1
```

## 使用

1. 将 `.psd` 文件放入项目的 `Assets` 目录。
2. 在 Project 窗口选中 PSD。
3. 在 Inspector 中使用 **PSD Layout Tool 2** 的导出、布局或 Prefab 生成功能。

生成的配置和共享材质保存在消费项目的 `Assets/PSDLayoutTool2Settings` 下，日志保存在 `<项目根目录>/Library/PSDLayoutTool2/Logs`。

每次导入日志都会保留关键阶段、异常堆栈和性能基线摘要。搜索 `PERFORMANCE_BASELINE` 查看总耗时、进程/Unity 内存变化，搜索 `PHASE_SUMMARY` 查看最慢阶段，搜索 `METRIC` 查看本次导入的图层和生成资源计数。

在 PSD Inspector 的“生成与更新”区域可以按需点击“视觉对比（按需）”。该操作不会加入普通导入流程，会读取 PSD 合成图并渲染当前 Prefab，输出 `source.png`、`render.png`、`diff.png` 和 JSON 报告到 `<Unity 项目根目录>/Library/PSDLayoutTool2/VisualComparison`，同时在诊断日志中记录对比结果和误差指标。

AI 层级整理功能需要本机另外安装并可执行 `codex` CLI；普通 PSD 导入不依赖该命令行工具。

完整功能说明和截图见[项目主页](https://github.com/mrLeelin/UnityPSDLayoutTool2)。
