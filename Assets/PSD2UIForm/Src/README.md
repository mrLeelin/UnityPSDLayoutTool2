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

## 程序集结构（Runtime / Editor 拆分）

本目录包含**两个**程序集，边界由 asmdef 强制：

| 程序集 | asmdef 位置 | 平台 | 内容 |
|---|---|---|---|
| `cn.efunstudio.psd2ugui` | `Runtime/cn.efunstudio.psd2ugui.asmdef` | 全平台 | 运行期组件：`PsdLayerNode`、各 `UIHelper`、键与数据类型、运行期判定规则 |
| `cn.efunstudio.psd2ugui.Editor` | 本目录根部 `cn.efunstudio.psd2ugui.Editor.asmdef` | 仅 Editor | 其余全部：`Psd2UIFormConverter`、`UGUIParser`、`PsdReader`、AI、Importing、Updates、EditorTools |

依赖方向固定为 **Editor → Runtime**（单向）。

**为什么要拆**：运行期组件会被挂在生成的 Prefab 上并随包发布，必须存在于播放器构建中；
而 PSD 解析 / 导图 / 生成逻辑本质上是编辑器行为，不应进包。

### 跨程序集调用：`Psd2UIFormEditorHost` 门面

运行期组件里仍有一部分方法只在编辑器期执行（生成、导图、预览），它们**不能直接引用**编辑器侧类型
（Unity 禁止平台无限制的 asmdef 引用 Editor-only 的 asmdef，`#if UNITY_EDITOR` 也救不了）。
统一约定：

- `Runtime/Psd2UGUI/Runtime/Psd2UIFormEditorHost.cs` 定义 `internal interface IPsd2UIFormEditorHost` + 注册点；
- `Psd2UGUI/Conversion/Psd2UIFormEditorHostAdapter.cs` 提供实现，并由 `[InitializeOnLoadMethod]` 注册；
- 运行期代码只调门面（`Psd2UIFormEditorHost.Current?.X(...)`）。**接口签名里不得出现编辑器侧类型。**
- 双程序集可见性由 `Runtime/Psd2UGUI/Runtime/AssemblyInfo.cs` 的 `InternalsVisibleTo("cn.efunstudio.psd2ugui.Editor")` 提供。

注意：门面未注册时（播放器构建中）相关入口会静默返回 `null`/`false`，这是设计行为。

## 目录结构

- `Runtime/`
  - 运行期程序集的全部源码（`Psd2UGUI/Runtime`、`Psd2UGUI/UIComponents`，以及被它们依赖的 PsdReader 数据模型）
- `PsdReader`
  - PSD 二进制读取、描述符、区段、图像资源、图层附加信息、链接图层和文本解析
  - 导入、重建、序列化（授权与输出保护已删除）
- `Psd2UGUI`
  - AI 分析与补丁、PSD 转 UGUI、UI 组件和编辑器工具
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

- **拆分边界不可回退**：`Runtime/` 下的任何文件都不得引用 `cn.efunstudio.psd2ugui.Editor` 里的类型；如需调用编辑器能力，走 `Psd2UIFormEditorHost` 门面。
- 运行期文件里调用 `UnityEditor` API（`AssetDatabase`、`EditorUtility`、`EditorGUI`、`EditorPrefs`…）必须包在 `#if UNITY_EDITOR` 内，并给出 `#else` 的安全默认值。
- 编辑器专用的新文件（Inspector、Drawer、Importer、菜单、AI 等）一律放在 `Runtime/` **之外**；不要为了省事把 `Editor`、`Scripts`、`Resources`、`StreamingAssets`、`Gizmos` 之类魔法目录塞进 `Runtime/`。
- 移动 `.cs` 文件时必须同时移动对应的 `.cs.meta`：Prefab 的组件绑定只认 GUID，`.meta` 丢了就掉脚本。
- 不要修改已登记的公共类型全名、程序集名称（`cn.efunstudio.psd2ugui` 必须由运行期程序集持有）、序列化字段键或配置中的类型字符串。
- 离线重建项目和嵌入资源位于 `Decompiled/PSD2UGUI_deobfuscated/Reconstruction`，不得放回 Unity 正式源码目录。

## 如何验证拆分（不需启动 Unity）

Unity 会为每个程序集生成 response file，直接用它与 csc 编译即可秒级得到结果：

```bash
PROJ=/e/Project/Demo/monsterhunter
RSP=$PROJ/Library/Bee/artifacts/*/cn.efunstudio.psd2ugui.rsp          # 运行期程序集
CSC=/e/UnityEngine/Engine/Installs_location/6000.3.6f1/Editor/Data/DotNetSdkRoslyn/csc.dll
RUN=/e/UnityEngine/Engine/Installs_location/6000.3.6f1/Editor/Data/netcorerun/netcorerun.exe

# 编辑器路径编译
"$RUN" "$CSC" @$RSP

# 播放器面编译：去掉 -define:UNITY_EDITOR，并剔除所有 -r:...UnityEditor* 引用
# → 0 error 才能保证运行期程序集既无编辑器 API、也无对 Editor 程序集的依赖
```

另有两条与编译状态无关的静态检查：

- Prefab 的 `m_Script` GUID 是否全部能解析到现存 `.cs.meta`（悬空必须为 0）；
- `MonoScript.GetClass()` 是否返回类型（返回 `null` 说明脚本无法绑定）。
