# internal 类型不能出现在 public 测试方法签名里

- 日期：2026-09-28
- 错误：`CS0051: Inconsistent accessibility: parameter type 'PsdHierarchyAiProvider' is less accessible than method ...`
- 位置：`Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyChatClientTests.cs`，新增的 `[TestCase]` 参数化测试

## 原因

`PsdHierarchyAiProvider`（以及本插件大部分 Hierarchy 类型）是 `internal`。测试程序集通过 InternalsVisibleTo 能在方法体里使用它们，
但 NUnit 测试方法必须是 `public`，public 方法的参数类型不能比方法本身更不可见。

## 下次避免

- 参数化测试（`[TestCase]`）的参数只用 `bool` / `int` / `string` 等公共类型，在方法体内再映射成 internal 枚举或类型。
- 写完测试后先让 Unity 重新编译（`unity command recompile` + `recompile_status`），确认 0 错误再跑测试。
