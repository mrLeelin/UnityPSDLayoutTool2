# PSD2UIForm 源码目录

本目录只保存 Unity 正式编译所需的源码、Unity 元数据和程序集定义。源码已经按职责整理，并完成混淆标识符的语义化重命名；本次整理不改变运行逻辑、程序集名称、Unity GUID 或资源引用。

## 授权（已全部删除，默认完全授权）

- 整个 `PsdReader/Licensing` 目录（License / 授权校验 / 输出水印保护 / 授权窗口）已删除。
- 默认行为：完全授权、无水印、无激活流程、无联网更新检查。
- 仅保留最小占位：
  - `PsdReader/Authorization/AlwaysAuthorized.cs`：`IsAuthorized=true`、`HasMainFeature=true`、本地 SHA256 工具。
  - `PsdReaderProductAccess`：对外状态始终为“完整版”，管理窗口/激活/导出授权均为无操作。
- `PsdRenderedImage` 不再创建 `RenderProtectionSession`；渲染结果无水印叠加。
- 菜单 `Tools/Psd2UIForm/Other/LicenseWindow`、`Clear License`、`Check Update` 已移除；仅保留 `Window/PSDReader/Force Reset`。
- Inspector 中的授权/订单 UI 已改为“默认完全授权”文案。

## 目录结构

- `PsdReader`
  - PSD 二进制读取、描述符、区段、图像资源、图层附加信息、链接图层和文本解析
  - 导入、重建、序列化（授权与输出保护已删除）
- `Psd2UGUI`
  - AI 分析与补丁、PSD 转 UGUI、运行时数据、UI 组件和编辑器工具
- `Internal`
  - 兼容辅助、诊断和生成代码

## 可读命名整理

- 158 个随机命名空间已按源文件职责改成唯一、可读的命名空间，例如：
  - `MNAIFj960MlipxY8l7o` → `ScriptableSingletonPathAttributeNamespace`
  - `QuKrWjpN7Qo8Q0yF0Rn` → `PsdBinaryReaderNamespace`
- 8 个随机类型、49 个随机成员和 18 个随机局部变量已完成编译器语义级重命名。
- `P_0`、`P_1` 等无意义参数以及生成绑定器中的短参数已改成用途明确的名称。
- `CreateLicenseStatus`、`CloneLicenseStatus`、27 个直接调用点所在的 12 个方法、3 个直接辅助方法及状态展示入口已清理 `value`、`text`、`obj` 等反编译占位名；状态构建方法中的反编译跳转标签也已移除。
- `LicenseResultCode` 与 `LicenseAvailabilityState` 的现有数值语义已写入源码注释；为保持原程序集成员表和元数据令牌不变，没有新增枚举字段。
- 无法从现有逻辑恢复原义、且从未读写的 9 个保护记录预留字段，统一命名为 `ReservedText01` 至 `ReservedText09`，不推测不存在的业务含义。

完整迁移记录：

- [文件布局迁移表](../../../../Decompiled/PSD2UGUI_deobfuscated/FileLayoutMigration.csv)
- [命名空间迁移表](../../../../Decompiled/PSD2UGUI_deobfuscated/NamespaceMigration.csv)
- [类型、成员、局部变量和参数迁移表](../../../../Decompiled/PSD2UGUI_deobfuscated/SymbolMigration.csv)

## 兼容性处理

- 程序集名称继续使用 `cn.efunstudio.psd2ugui`。
- `PsdReaderUpdateCache` 的 5 个持久化字段保留 `FormerlySerializedAs`，兼容旧版 `JsonUtility`/`EditorPrefs` 数据键。
- `ProjectLicenseExportWindow` 和 `PsdReaderLicenseWindow` 使用 `MovedFrom`，兼容旧 Unity 编辑器布局中保存的类型身份。
- 保护记录指纹、反射绑定器的类型/字段/方法元数据令牌以及所有现有字符串常量均保持不变。

## 维护约束

- `cn.efunstudio.psd2ugui.asmdef` 必须保留在本目录根部，且不得改成 Editor-only 程序集。
- 不要创建名为 `Editor`、`Scripts`、`Resources`、`StreamingAssets` 或 `Gizmos` 的子目录。
- 移动 `.cs` 文件时必须同时移动对应的 `.cs.meta`，以保持 Unity GUID。
- 不要修改已登记的公共类型全名、程序集名称、序列化字段键或配置中的类型字符串。
- 离线重建项目和嵌入资源位于 `Decompiled/PSD2UGUI_deobfuscated/Reconstruction`，不得放回 Unity 正式源码目录。
