namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;

    /// <summary>
    /// 10：v1 写入、旧 CLI Runner 调用与无调用的兼容入口必须已经从交付面消失；
    /// 只保留明确标注的只读诊断和“拒绝”行为（旧后端取值仍然只会归一化为 Unity 核心）。
    /// </summary>
    public sealed class PsdHierarchyLegacyRemovalTests
    {
        private static readonly Assembly PluginAssembly = typeof(PsdHierarchyOrganizerEntry).Assembly;

        [Test]
        public void RetiredPythonPayloadExecutorIsGone()
        {
            Assert.That(
                PluginAssembly.GetType("PsdLayoutTool2.PsdHierarchyNativePayloadExecutor"),
                Is.Null,
                "运行 Python renderer 的旧执行器必须删除，不能再作为写入路径存在。");
        }

        [Test]
        public void RetiredExecutorCompatEntryPointsAreGone()
        {
            string[] removed =
            {
                "RequiresUnityCliRunner",
                "TryValidatePlanCapabilities",
                "BuildReapplyPreflightPlan",
                "Validate",
                "Apply",
            };

            string[] remaining = typeof(PsdHierarchyNativeCleanupExecutor)
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .Select(method => method.Name)
                .Where(name => removed.Contains(name, StringComparer.Ordinal))
                .Distinct()
                .ToArray();

            Assert.That(remaining, Is.Empty, "旧 Runner 兼容入口必须删除：" + string.Join(", ", remaining));

            // 保留的正式入口仍然存在，并且都要求权威快照上下文或 v2 重放计划。
            const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            Assert.That(
                typeof(PsdHierarchyNativeCleanupExecutor).GetMethod("ValidateAsync", AnyStatic),
                Is.Not.Null);
            Assert.That(
                typeof(PsdHierarchyNativeCleanupExecutor).GetMethod("ApplyAsync", AnyStatic),
                Is.Not.Null);
            Assert.That(
                typeof(PsdHierarchyNativeCleanupExecutor).GetMethod("ReapplyAsync", AnyStatic),
                Is.Not.Null);
        }

        [Test]
        public void RetiredAiSecondStageFollowUpChainIsGone()
        {
            Assert.That(
                PluginAssembly.GetType("PsdLayoutTool2.PsdHierarchyValidatedPlanDisposition"),
                Is.Null,
                "AI 自动第二阶段已经由共享核心原生完成，相关处置枚举必须删除。");

            string[] removed =
            {
                "BuildComponentExtractionFollowUpPrompt",
                "ShouldAutoApplyComponentFollowUp",
                "CanGenerateConfirmedComponentFollowUp",
                "ResolveValidatedPlanDisposition",
                "BuildActualExtractionIntents",
                "ExtractPostGroupingExtractionIntents",
                "HasConfirmedPostGroupingExtractionIntents",
                "ShouldQueueComponentExtractionFollowUp",
            };

            string[] remaining = typeof(PsdHierarchyChatWindow)
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .Select(method => method.Name)
                .Where(name => removed.Contains(name, StringComparer.Ordinal))
                .Distinct()
                .ToArray();

            Assert.That(remaining, Is.Empty, "旧 AI 第二阶段调用链必须删除：" + string.Join(", ", remaining));
        }

        [Test]
        public void LegacyRunnerBackendValueStillNormalizesToTheUnityCore()
        {
            // 保留的“拒绝”行为：旧设置值仍然只能解析为共享核心，不会重新打开 CLI 写入。
            Assert.That(
                PsdHierarchyCleanupExecutionSettingsSnapshot.Normalize(
                    PsdHierarchyCleanupExecutionBackend.UnityCliRunner),
                Is.EqualTo(PsdHierarchyCleanupExecutionBackend.NativeUnity));

            PsdHierarchyCleanupExecutionSettingsSnapshot resolved =
                PsdLayoutProjectSettings.instance.ResolveHierarchyCleanupExecutionSettings();
            Assert.That(
                resolved.backend,
                Is.EqualTo(PsdHierarchyCleanupExecutionBackend.NativeUnity),
                "正式链路只能解析为 Native Unity 核心。");
        }

        [Test]
        public async System.Threading.Tasks.Task RetiredV1PlansRemainPermanentlyRejectedWithoutWrites()
        {
            string plan = "{\"version\":1,\"prefabAssetPath\":\"Assets/UI/Example.prefab\"," +
                          "\"output\":{\"mode\":\"in_place\",\"assetPath\":\"Assets/UI/Example.prefab\"}," +
                          "\"prefabName\":\"Example\",\"nodeTransfers\":[],\"verify\":{}}";

            var result = await PsdHierarchyChatCleanupExecution.ReapplyPersistedPlanAsync(
                System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName,
                plan);

            Assert.That(result.success, Is.False);
            Assert.That(
                result.message,
                Does.Contain(PsdHierarchyChatCleanupExecution.ReplayRequiresFreshAnalysisMessage));
            Assert.That(
                PsdHierarchyCleanupReplayCoordinator.IsPermanentReplayFailure(
                    PsdHierarchyChatCleanupExecution.ReplayRequiresFreshAnalysisMessage),
                Is.True,
                "v1 计划必须是永久拒绝，不能作为瞬时可重试失败。");
        }
    }
}
