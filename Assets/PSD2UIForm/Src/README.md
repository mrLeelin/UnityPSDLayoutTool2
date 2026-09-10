# PSD2UIForm 源码目录

本目录只保存 Unity 正式编译所需的源码、Unity 元数据和程序集定义。源码已经按职责整理，并完成混淆标识符的语义化重命名；本次整理不改变运行逻辑、程序集名称、Unity GUID 或资源引用。

## 临时完整授权测试

- `PsdReaderLicenseService.cs` 文件顶部当前启用了 `PSD2UIFORM_FORCE_AUTHORIZED_TEST`。
- 开启时，授权状态固定为 `ResultCode = 1`、`StatusText = "Active"`、`Features = ["Main"]`，同时生成字段和状态哈希自洽的测试授权保护上下文。
- `HasMainFeature()`、16-bit PSD 输出、导出授权标记和保护资源签章会统一按授权态工作；“试用版”与 `efunstudio.cn` 可见水印及其文字边缘不会生成。
- 真实激活、签名验证和工程授权文件导出仍使用原校验流程，不会产生可分发的伪授权文件。
- 关闭测试时，删除文件顶部的 `#define PSD2UIFORM_FORCE_AUTHORIZED_TEST` 即可恢复全部真实授权判断。
- 切换后应清理旧预览和已导出的带水印图片并重新生成，避免复用旧缓存；此开关只能用于本地测试，不能随正式版本发布。
- `ClockWatermarkCipher` 是授权缓存的防时间回拨密文，不是画面水印，本测试不会修改它。

## 目录结构

- `PsdReader`
  - PSD 二进制读取、描述符、区段、图像资源、图层附加信息、链接图层和文本解析
  - 导入、重建、序列化、授权、输出保护和更新检查
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
