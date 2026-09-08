# PSD2UIForm Unity 源码编译

此目录是 Unity 当前实际参与编译的源码，共 296 个 C# 文件。它是独立重建工程的显式副本，不是链接；后续修改此目录才会触发 Unity 编译。

## 当前命名状态

已将 1,763 项混淆名称改为有实现依据的语义名称，另改名 492 个参数、166 个局部变量；120 个文件已改为对应的语义文件名。两个源码树内容一致，全部 296 个脚本 GUID 和原配置引用保持。

这不是作者原始源码，也还不是“所有名称均可读”：至少 300 个清单内残留项、其他尚未命名的参数/局部变量，以及另行记录的 15 项兼容身份仍保留。125 个随机源码目录现已完成语义改名，源码和 GUID 未变；目录迁移后的报告在 `Decompiled/PSD2UIForm/Readability/DirectoryMigration/`。目录改名改变编译输入排序与 metadata token，严格对照保留这些差异，不宣称二进制相同。

优先阅读 `UGF/EditorTools/Psd2UGUI/Psd2UIFormConverter.cs`、`PsdLayerNode.cs`、`UGUIParser.cs`；解析器可直接搜索 `PsdBigEndianReader`、`PsdSectionReader`、`LayerRecordReader`。命名依据、剩余项与本轮测试在 `Decompiled/PSD2UIForm/Readability/`，早期 Unity 接入报告不是本轮命名后的验证快照。

## 接入方式

- 程序集名称保持 `cn.efunstudio.psd2ugui`，限定 Editor，启用 unsafe，明确引用 TMP 和 uGUI。
- `csc.rsp` 将五份 `EmbeddedResources/*.bytes` 按原资源名及 public/private 级别嵌入程序集。
- 原 `Libs/cn.efunstudio.psd2ugui.dll` 已移出 Assets；原文件和 meta 的校验备份在 `Decompiled/PSD2UIForm/SourceReplacement/Backup/`。`Psd2UIFormConfig.asset` 已通过 Unity `SerializedObject` 改为引用源码 `UGUIParser.cs`（源码 GUID `bd577050688cd024b9ed5e69ec6f06df`，fileID `11500000`）。
- 源码 asmdef 不再依赖 `PSD2UIFORM_USE_ORIGINAL_DLL` 切换宏，避免删除原 DLL 后留下失效回退路径；不修改激活、试用或许可逻辑。
- 未修改激活逻辑，也未自动打开工具窗口、进入 Play Mode 或保存场景。

## Unity 生成代码

旧导出包含 Unity 自动生成的 `UnitySourceGeneratedAssemblyMonoScriptTypes_v1`，会与 Unity 当前的 MonoScriptGenerator 重复。对应文件只在 `!UNITY_EDITOR` 下参与编译；Unity 工程内使用新生成的索引。其余业务源码不因本次接入改动。

## 2026-09-08 验证

- Unity 6000.3.6f1 真实工程编译和域重载：0 错误；目标程序集 364 警告，本轮工程合计 369 警告。
- 当前 Console：0 错误。接入前的 Unity 服务配置错误单独保存在基线中。
- 仅加载一个同名程序集，路径为 `Library/ScriptAssemblies/cn.efunstudio.psd2ugui.dll`。
- 296 个源文件、5 项嵌入资源及源码绑定的配置均通过检查；配置 7,567 个序列化属性除 `m_Script` 外保持不变，规则仍为 36 项。
- 对 Unity 实际输出 DLL 的 34 项行为测试、260 项断言、124 个常量检查通过。
- 完整 PSD 导入、编辑器功能操作、渲染和 Player 打包不在本次验证范围内。

完整报告：`Decompiled/PSD2UIForm/Audit/UnityIntegration/`；运行时断言：`Decompiled/PSD2UIForm/Tests/Inspect-UnityProject.csx`。

## 回退方式

如需审计原始文件，使用 `Decompiled/PSD2UIForm/SourceReplacement/Backup/` 中的校验备份；不要把原 DLL 与同名源码程序集同时放回 `Assets`，否则会产生重复程序集/类型冲突。源码配置绑定和源码程序集是当前唯一交付路径。
