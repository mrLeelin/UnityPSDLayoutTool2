# NUnit 集合断言用错约束，导致编译失败

- 日期：2026-09-28
- 错误：`CS1503: Argument 1: cannot convert from 'int' to 'string'`
- 位置：`Assets/PSDLayoutTool2/Editor/Tests/PsdLayoutProjectSettingsWebServerTests.cs`

## 原因

写了 `Assert.That(candidates, Does.Not.Contain(80))`。`Does.Contain` / `Does.Not.Contain` 只有 `string` 重载（子串断言），
对整型集合判断"不包含某元素"要用集合约束。

## 下次避免

- 集合是否包含元素：`Has.Member(x)` / `Has.No.Member(x)`（或 `Does.Contain` 只用于字符串和字符串集合）。
- 写完测试先 recompile，确认 0 错误再跑测试。
