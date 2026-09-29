namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;

    /// <summary>
    /// 已退役的写入路径、旧执行后端和无调用的兼容入口必须已经从交付面消失；
    /// 只保留明确的“拒绝”行为：不受支持的计划版本永久拒绝并要求重新分析。
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
                "旧的外部载荷执行器必须删除，不能再作为写入路径存在。");
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

            Assert.That(remaining, Is.Empty, "旧兼容入口必须删除：" + string.Join(", ", remaining));

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

            string[] remaining = typeof(PsdHierarchyChatCleanupExecution)
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .Select(method => method.Name)
                .Where(name => removed.Contains(name, StringComparer.Ordinal))
                .Distinct()
                .ToArray();

            Assert.That(remaining, Is.Empty, "旧 AI 第二阶段调用链必须删除：" + string.Join(", ", remaining));
        }

        [Test]
        public void RetiredExecutionBackendSettingIsGone()
        {
            Assert.That(PluginAssembly.GetType("PsdLayoutTool2.PsdHierarchyCleanupExecutionBackend"), Is.Null);
            Assert.That(PluginAssembly.GetType("PsdLayoutTool2.PsdHierarchyCleanupExecutionSettings"), Is.Null);
            Assert.That(
                typeof(PsdHierarchyChatCleanupExecution).GetMethod(
                    "TryPrepareRunnerPlan",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static),
                Is.Null);
        }

        [Test]
        public async System.Threading.Tasks.Task UnsupportedPlanVersionsRemainPermanentlyRejectedWithoutWrites()
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
                "不受支持的计划版本必须是永久拒绝，不能作为瞬时可重试失败。");
        }
    }
}
