# 工单02验收

## 独立编辑器进程重启

`RestartProbe.cs` 是独立项目验收入口，不放入正式插件运行时代码。

1. 创建临时 Unity 项目，复制当前 `Assets/PSD2UIForm` 插件（保持原目录层级）和 TextMesh Pro 资源。使用项目现有版本的 UGUI、Test Framework 和 Unity 内置模块。
2. 将 `RestartProbe.cs` 复制到临时项目的 `Assets/Editor/`。
3. 使用 Unity 6000.3.6f1 运行：

```text
Unity.exe -batchmode -nographics -quit -projectPath <临时项目> -executeMethod PsdExtractionRestartProbe.Setup -logFile <setup.log>
Unity.exe -batchmode -nographics -quit -projectPath <临时项目> -executeMethod PsdExtractionRestartProbe.Verify -logFile <verify.log>
```

第二条须等第一进程退出再执行。验证程序要求 PID 不同，并通过正式 converter 生成入口验证规则和实例身份在新进程中仍有效，目标及公共 Prefab 的 SHA-256 不变。结果记录在 `restart-result.json`；本次两进程均以退出码0结束。

## 保存异常

`CommonPrefabExtractionTests.Extraction_RestoresAssetsWhenRulesWriteFails` 在真实规则资产已写入后注入 IOException，覆盖有/无来源基线两种情况。验证目标 Prefab 和 meta 内容恢复、既有规则恢复、新公共资产目录清理。这模拟单次写入后异常，不代表操作系统断电或恢复写入本身也失败时的保证。

## 真实PSD

显式运行 `RealPsdExtractionAcceptance.FirstGeneration_ExtractReloadAndRepeatGeneration_PreservesPrefabGraph`。该测试从真实签到 PSD 的临时副本生成 UI，再抽取可安全处理的 Image 节点并重复生成。测试恢复输出设置并清理临时资产；不修改正式签到界面。

```text
unity command run_tests --mode editor --filter RealPsdExtractionAcceptance --include_explicit true --async_tests true --project-path E:/Project/Demo/monsterhunter --json
unity command test_status --project-path E:/Project/Demo/monsterhunter --json
```

2026-09-14：显式测试 1/1 通过，55.51秒；插件常规 EditMode 全量测试 35/35 通过，29.2秒。首次生成通过 `LoadPrefabContents` 提供活动 converter 上下文；直接使用 `AssetDatabase.LoadAssetAtPath` 得到的持久资产首次生成会缺少该上下文，不属于本次验证的编辑流程。跨进程验收使用合成界面；真实 PSD 验收使用同一进程重新载入资产，二者分别证明对应边界。未做截图视觉比对。
