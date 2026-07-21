# FigmaMcpRelay 参考

`E:\Project\Tools\FigmaMcpRelay` 在 PSD 图层分层、增量同步和事务验证方面有成熟实现，本插件的部分功能可参考其设计。工作路径不同：本插件直接读取 PSD 文件，Figma Relay 通过 Figma API 中转。

## 可参考的设计

### 1. 稳定身份优先于名称

Figma Relay 使用 `normalizePsdLayerId()` 从 Photoshop 读取 layerId，规范化后作为长期身份。名称只用于显示和 Unity 安全名称生成，不决定匹配。

**本插件的对应实现：**
- `PsdPrefabNodeIdentity.stableId` — 用于增量 diff 匹配
- `PsdEmbeddedLayoutLayer.layerId` — 从 XMP 清单读取的稳定 ID
- 回退 `native_` hash（parentId + siblingIndex + name）

### 2. 统一 sourceState 模型

Figma Relay 的 `normalizePsdSourceState()` 将 PSD 图层归一化为版本化结构：

```
{
  version: 3,
  layerId, mode,
  geometry: { x, y, width, height, rotation },
  display: { visible, opacity, blendMode, constraints },
  content: { contentHash, assetKey },
  text: { characters, fontFamily, fontSize, leading, ... },
  nineSlice,
  unsupported
}
```

**本插件的对应实现：**
- `PsdPrefabNodeModel` + `PsdPrefabTextModel` — 中间模型
- `contentFingerprint` / `assetFingerprint` — FNV-1a 内容指纹

### 3. 字段所有权分离

| 字段 | PSD 拥有 | Unity/项目拥有 |
|------|---------|---------------|
| 图层身份、名称、层级 | 是 | 否 |
| 图像像素、文字内容 | 是 | 否 |
| 位置/尺寸/旋转 | 是 | 可配置映射 |
| 可见性、透明度 | 是 | 否 |
| Unity 安全名称 | 否 | 是 |
| 业务脚本、序列化绑定 | 否 | 是 |
| Anchor/Canvas 映射策略 | 否 | 是 |

### 4. Preview → Apply → Verify 事务

Figma Relay 将导入拆分为三个阶段：

1. **Preview** — 只读计算差异，不修改
2. **Apply** — 按差异计划只写入 PSD 拥有字段
3. **Verify** — 验证层级、资源引用、源状态一致

每个阶段失败都可回滚，成功有验证报告。

**本插件的对应实现：**
- `PsdPrefabDiff.Compare()` — 差异计算
- `PsdPrefabConversionPipeline.CreatePlan()` — 转换计划
- `PsdPrefabNodeIdentity` — 节点身份记录（用于后续增量更新）

### 5. FigmaBridge 节点绑定

`FigmaPrefabNodeBinding.cs` 在每个 Unity 节点上记录 `figmaNodeId`（对应本插件的 `stableId`）、`structuralPath`、`sourcePrefabGuid`。本插件的 `PsdPrefabNodeIdentity` 功能类似。

## 参考文件列表

- `normalizePsdLayerId` / `normalizePsdSourceState` / `diffPsdSourceStates` — `code/06_psd_incremental.mjs`
- `preparePsdIncrementalUpdate` / `applyPsdIncrementalUpdate` / `rollbackPsdIncrementalMutation` — `code.js`
- `FigmaPrefabNodeBinding.cs` — 节点身份绑定
- `FigmaPrefabGenerator.cs` — Prefab 生成器
- `export_psd_layers.py` — PSD 图层导出脚本（layerId 读取、重复检测）
- 设计基线文档：`FIGMA_MCP_RELAY_TO_PSD_PREFAB_DESIGN.md`、`PSD_TO_PREFAB_FINAL_PRODUCT_SPEC.md`
