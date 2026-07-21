# Photoshop PSD Layout Exporter

这是一个基础 Photoshop UXP 插件。它不会生成旁路 JSON，而是把布局 manifest 写入当前 PSD/PSB 的内嵌 XMP 元数据。

## 支持内容

- 文档宽高和分辨率
- 图层稳定 `layerId`
- 父子关系和同级顺序
- 图层名称、类型、可见性和透明度
- 图层 bounds
- 基础文本内容、字体字号和对齐信息
- 每层 fingerprint
- 文档 fingerprint

## 安装

1. 安装 Photoshop 24.1 或更高版本。
2. 安装 Adobe UXP Developer Tool。
3. 打开 UXP Developer Tool。
4. 选择 **Add Plugin**，选择本目录中的 `manifest.json`。
5. 点击 **Load**。
6. 在 Photoshop 菜单 `Window > Plugins > PSD Layout Metadata Exporter` 打开面板。

## 使用

1. 打开并保存一个本地 `.psd` 或 `.psb` 文件。
2. 打开插件面板。
3. 点击 **写入 PSD 内嵌布局数据**。
4. 保存 PSD。

插件会将 manifest 写入自定义 XMP 命名空间：

```text
https://codex.openai.com/psd-layout/1.0/
```

## 当前限制

- 当前版本是增量更新的元数据基础，不修改 PSD 图层。
- 描边、阴影和多字体文本 run 尚未全部导出，下一步会加入同一个 manifest。
- `layerId` 是 Photoshop 图层身份；复制图层会产生新的身份，应视为新增图层。
- Unity 端下一步读取 PSD 的 XMP Image Resource，并将这些 fingerprint 用于 Preview/Diff/Apply。
