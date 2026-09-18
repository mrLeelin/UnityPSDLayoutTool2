namespace PsdLayoutTool2.Tests
{
    using System.Linq;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    public sealed class PsdHierarchyChatCleanupExecutionTests
    {
        [TestCase("确认")]
        [TestCase("满意")]
        [TestCase("满意了")]
        [TestCase("满意！")]
        public void ExplicitConfirmationAcceptsTheDocumentedSingleApprovalWords(string input)
        {
            Assert.That(PsdHierarchyChatCleanupExecution.IsExplicitConfirmation(input), Is.True);
        }

        [Test]
        public void ReviewedJsonPlanForTheCurrentPrefabCanBeConfirmed()
        {
            const string target = "Assets/UI/Prefab/ExampleView.prefab";
            string reply = "方案如下。\n```json\n" + CreatePlan(target, true) + "\n```";

            bool extracted = PsdHierarchyChatCleanupExecution.TryExtractApprovedPlan(
                reply,
                target,
                out string plan,
                out string error);

            Assert.That(extracted, Is.True, error);
            Assert.That(plan, Does.Contain("ExampleView.prefab"));
            Assert.That(PsdHierarchyChatCleanupExecution.IsExplicitConfirmation("确认"), Is.True);
            Assert.That(PsdHierarchyChatCleanupExecution.IsExplicitConfirmation("可以执行"), Is.True);
            Assert.That(PsdHierarchyChatCleanupExecution.IsExplicitConfirmation("确认。"), Is.True);
            Assert.That(PsdHierarchyChatCleanupExecution.IsExplicitConfirmation("好的"), Is.True);
        }

        [Test]
        public void PlanForAnotherPrefabIsNeverMadeExecutable()
        {
            string reply = "```json\n" + CreatePlan("Assets/UI/Other.prefab", true) + "\n```";

            bool extracted = PsdHierarchyChatCleanupExecution.TryExtractApprovedPlan(
                reply,
                "Assets/UI/Prefab/ExampleView.prefab",
                out string plan,
                out string error);

            Assert.That(extracted, Is.False);
            Assert.That(plan, Is.Empty);
            Assert.That(error, Does.Contain("当前目标 Prefab"));
        }

        [Test]
        public void PlanWithoutVersionIsNeverMadeExecutable()
        {
            string reply = "```json\n" + CreatePlan("Assets/UI/Prefab/ExampleView.prefab", false) + "\n```";

            bool extracted = PsdHierarchyChatCleanupExecution.TryExtractApprovedPlan(
                reply,
                "Assets/UI/Prefab/ExampleView.prefab",
                out string plan,
                out string error);

            Assert.That(extracted, Is.False);
            Assert.That(plan, Is.Empty);
            Assert.That(error, Does.Contain("version 必须为 1"));
        }

        [Test]
        public void NonConfirmationTextDoesNotApplyThePendingPlan()
        {
            Assert.That(PsdHierarchyChatCleanupExecution.IsExplicitConfirmation("请把标题换一下"), Is.False);
            Assert.That(PsdHierarchyChatCleanupExecution.IsExplicitConfirmation("确认后怎么办？"), Is.False);
        }

        [Test]
        public void ApplyIntentRecognizesModifyPrefabWithoutTreatingAQuestionAsConfirmation()
        {
            Assert.That(PsdHierarchyChatCleanupExecution.IsApplyIntent("修改 Prefab"), Is.True);
            Assert.That(PsdHierarchyChatCleanupExecution.IsApplyIntent("修改吧"), Is.True);
            Assert.That(PsdHierarchyChatCleanupExecution.IsApplyIntent("请修改标题"), Is.False);
        }

        [Test]
        public void CorrectedJsonKeepsTheInitialReviewForTheUserConfirmation()
        {
            const string target = "Assets/UI/Prefab/ExampleView.prefab";
            const string firstReply = "一、分析摘要\n这里是完整分析。\n```json\n{\"invalid\":true}\n```";

            string review = PsdHierarchyChatCleanupExecution.ExtractReviewText(firstReply);
            string combined = PsdHierarchyChatCleanupExecution.ComposeReviewableReply(
                review,
                CreatePlan(target, true));

            Assert.That(review, Is.EqualTo("一、分析摘要\n这里是完整分析。"));
            Assert.That(combined, Does.StartWith(review));
            Assert.That(PsdHierarchyChatCleanupExecution.TryExtractApprovedPlan(
                combined,
                target,
                out string plan,
                out string error), Is.True, error);
            Assert.That(plan, Does.Contain("ExampleView.prefab"));
        }

        [Test]
        public void RunnerFailureEnvelopeReportsTheUnderlyingUnityPreflightError()
        {
            const string runnerOutput =
                "{\"success\":false,\"error\":\"Plan source path was not found for renames[0].target: DayMarkers/20\"}";

            string error = PsdHierarchyChatCleanupExecution.SummarizeFailure(runnerOutput);

            Assert.That(
                error,
                Is.EqualTo("Plan source path was not found for renames[0].target: DayMarkers/20"));
        }

        [Test]
        public void BareJsonPlanCanBeRecoveredWhenProviderOmitsMarkdownFence()
        {
            const string target = "Assets/UI/Prefab/ExampleView.prefab";
            bool extracted = PsdHierarchyChatCleanupExecution.TryExtractApprovedPlan(
                CreatePlan(target, true),
                target,
                out string plan,
                out string error);

            Assert.That(extracted, Is.True, error);
            Assert.That(plan, Does.Contain("ExampleView.prefab"));
        }

        [Test]
        public void JsonFenceAllowsWhitespaceUppercaseAndBom()
        {
            const string target = "Assets/UI/Prefab/ExampleView.prefab";
            string reply = "\uFEFF``` JSON\r\n" + CreatePlan(target, true) + "\r\n```";
            bool extracted = PsdHierarchyChatCleanupExecution.TryExtractApprovedPlan(
                reply,
                target,
                out string plan,
                out string error);

            Assert.That(extracted, Is.True, error);
            Assert.That(plan, Does.Contain("ExampleView.prefab"));
        }

        [Test]
        public void RunnerPlanPreservesReviewedAssetRenameGuid()
        {
            const string assetPath =
                "Assets/PSDLayoutTool2/TestData/7日任务拆分/Texture/daily_bgbig1_932.png";
            PsdHierarchyChatContext context = CreateNodeSnapshotContext();
            var plan = JObject.Parse(CreateNodeReferencePlan(
                "node:n000001",
                "node:n000002",
                "snapshot-123"));
            plan["moves"] = new JArray();
            plan["textureRenames"] = new JArray
            {
                new JObject
                {
                    ["from"] = assetPath,
                    ["toName"] = "ExampleView_OuterBackgroundFrame",
                    ["expectedGuid"] = "905d7b156324ebf4cb8e9c8717f3dc84",
                },
            };

            AssertPreparedPlanIsUnchanged(context, plan);
        }

        [Test]
        public void PreparedVersionTwoPlanPreservesPostGroupingExtractionIntentsForLaterMigration()
        {
            PsdHierarchyChatContext context = CreateNodeSnapshotContext();
            var plan = JObject.Parse(CreateNodeReferencePlan(
                "node:n000001",
                "node:n000002",
                "snapshot-123"));
            plan["moves"] = new JArray();
            plan["postGroupingExtractionIntents"] = new JArray(new JObject
            {
                ["id"] = "task_item",
                ["mode"] = "component",
                ["assetPath"] = "Assets/UI/Prefab/Common/TaskItem.prefab",
                ["templatePath"] = "Root/Child",
                ["commonMembers"] = new JArray(),
                ["instances"] = new JArray(new JObject
                {
                    ["path"] = "Root/Child",
                    ["state"] = string.Empty,
                    ["commonSourceNames"] = new JArray(),
                    ["stateSourceNames"] = new JArray(),
                }),
                ["states"] = new JArray(),
                ["defaultState"] = string.Empty,
            });

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                context,
                plan.ToString(),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.True, error);
            Assert.That(
                JObject.Parse(runnerPlanJson)["postGroupingExtractionIntents"],
                Is.EqualTo(plan["postGroupingExtractionIntents"]));
        }

        [Test]
        public void RunnerPlanRejectsIncompletePostGroupingExtractionIntentBeforeFirstStageExecution()
        {
            PsdHierarchyChatContext context = CreateNodeSnapshotContext();
            var plan = JObject.Parse(CreateNodeReferencePlan(
                "node:n000001",
                "node:n000002",
                "snapshot-123"));
            plan["moves"] = new JArray();
            plan["postGroupingExtractionIntents"] = new JArray(new JObject
            {
                ["id"] = "task_item",
                ["mode"] = "state",
                ["assetPath"] = "Assets/UI/Prefab/Common/TaskItem.prefab",
                ["templatePath"] = "Root/Child",
                ["commonMembers"] = new JArray("Background"),
                ["instances"] = new JArray(new JObject
                {
                    ["path"] = "Root/Child",
                    ["commonSourceNames"] = new JArray(),
                    ["stateSourceNames"] = new JArray(),
                }),
                ["states"] = new JArray(new JObject
                {
                    ["id"] = "available",
                    ["name"] = "Available",
                    ["sourcePath"] = "Root/Child",
                    ["members"] = new JArray("Icon"),
                }),
                ["defaultState"] = "available",
            });

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                context,
                plan.ToString(),
                out _,
                out string error);

            Assert.That(prepared, Is.False);
            Assert.That(error, Does.Contain("instances[0].state"));
        }

        [Test]
        public void RunnerPlanRejectsPostGroupingInstanceStateThatIsNotDeclared()
        {
            PsdHierarchyChatContext context = CreateNodeSnapshotContext();
            var plan = JObject.Parse(CreateNodeReferencePlan(
                "node:n000001",
                "node:n000002",
                "snapshot-123"));
            plan["moves"] = new JArray();
            plan["postGroupingExtractionIntents"] = CreateStatefulIntent("missing_state");

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                context,
                plan.ToString(),
                out _,
                out string error);

            Assert.That(prepared, Is.False);
            Assert.That(error, Does.Contain("missing_state").And.Contain("state id"));
        }

        [Test]
        public void RunnerPlanRejectsComponentIntentWithInstanceState()
        {
            PsdHierarchyChatContext context = CreateNodeSnapshotContext();
            var plan = JObject.Parse(CreateNodeReferencePlan(
                "node:n000001",
                "node:n000002",
                "snapshot-123"));
            plan["moves"] = new JArray();
            plan["postGroupingExtractionIntents"] = new JArray(new JObject
            {
                ["id"] = "task_item",
                ["mode"] = "component",
                ["assetPath"] = "Assets/UI/Prefab/Common/TaskItem.prefab",
                ["templatePath"] = "Root/Child",
                ["commonMembers"] = new JArray(),
                ["instances"] = new JArray(new JObject
                {
                    ["path"] = "Root/Child",
                    ["state"] = "unexpected",
                }),
                ["states"] = new JArray(),
                ["defaultState"] = string.Empty,
            });

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                context,
                plan.ToString(),
                out _,
                out string error);

            Assert.That(prepared, Is.False);
            Assert.That(error, Does.Contain("component").And.Contain("state"));
        }

        private static JArray CreateStatefulIntent(string instanceState)
        {
            return new JArray(new JObject
            {
                ["id"] = "task_item",
                ["mode"] = "stateful",
                ["assetPath"] = "Assets/UI/Prefab/Common/TaskItem.prefab",
                ["templatePath"] = "Root/Child",
                ["commonMembers"] = new JArray("Label"),
                ["instances"] = new JArray(new JObject
                {
                    ["path"] = "Root/Child",
                    ["state"] = instanceState,
                    ["commonSourceNames"] = new JArray("Label"),
                    ["stateSourceNames"] = new JArray("Background"),
                }),
                ["states"] = new JArray(new JObject
                {
                    ["id"] = "available",
                    ["name"] = "[State_Available]",
                    ["sourcePath"] = "Root/Child",
                    ["members"] = new JArray("Background"),
                }),
                ["defaultState"] = "available",
            });
        }

        [Test]
        public void RunnerPlanPreservesMissingAssetRenameSourceForApplyPreflight()
        {
            const string missingAssetPath =
                "Assets/PSDLayoutTool2/TestData/7日任务拆分/Texture/missing_texture.png";
            PsdHierarchyChatContext context = CreateNodeSnapshotContext();
            var plan = JObject.Parse(CreateNodeReferencePlan(
                "node:n000001",
                "node:n000002",
                "snapshot-123"));
            plan["moves"] = new JArray();
            plan["textureRenames"] = new JArray
            {
                new JObject
                {
                    ["from"] = missingAssetPath,
                    ["toName"] = "ExampleView_MissingTexture",
                    ["expectedGuid"] = "00000000000000000000000000000000",
                },
            };

            AssertPreparedPlanIsUnchanged(context, plan);
        }

        [Test]
        public void RunnerPlanPreservesAssetRenameSourceOutsideCurrentDependenciesForApplyPreflight()
        {
            const string allowedAssetPath =
                "Assets/PSDLayoutTool2/TestData/7日任务拆分/Texture/daily_bgbig1_932.png";
            const string unreferencedAssetPath =
                "Assets/PSDLayoutTool2/TestData/7日签到拆分/Texture/Currency_Power_2_385.png";
            PsdHierarchyChatContext context = CreateAssetRenameContext(new[] { allowedAssetPath });
            var plan = JObject.Parse(CreateNodeReferencePlan(
                "node:n000001",
                "node:n000002",
                "snapshot-123"));
            plan["moves"] = new JArray();
            plan["textureRenames"] = new JArray
            {
                new JObject
                {
                    ["from"] = unreferencedAssetPath,
                    ["toName"] = "ExampleView_Icon",
                    ["expectedGuid"] = string.Empty,
                },
            };

            AssertPreparedPlanIsUnchanged(context, plan);
        }

        [Test]
        public void RunnerPlanPreservesAssetRenamesWhenCurrentDependenciesAreEmpty()
        {
            const string assetPath =
                "Assets/PSDLayoutTool2/TestData/7日任务拆分/Texture/daily_bgbig1_932.png";
            PsdHierarchyChatContext context = CreateAssetRenameContext(System.Array.Empty<string>());
            var plan = JObject.Parse(CreateNodeReferencePlan(
                "node:n000001",
                "node:n000002",
                "snapshot-123"));
            plan["moves"] = new JArray();
            plan["textureRenames"] = new JArray
            {
                new JObject
                {
                    ["from"] = assetPath,
                    ["toName"] = "ExampleView_Icon",
                    ["expectedGuid"] = string.Empty,
                },
            };

            AssertPreparedPlanIsUnchanged(context, plan);
        }

        [Test]
        public void RunnerPlanPreservesReviewedPrefabNameBesideAssetRenameTargets()
        {
            const string assetPath =
                "Assets/PSDLayoutTool2/TestData/7日任务拆分/Texture/daily_bgbig1_932.png";
            PsdHierarchyChatContext context = CreateNodeSnapshotContext();
            var plan = JObject.Parse(CreateNodeReferencePlan(
                "node:n000001",
                "node:n000002",
                "snapshot-123"));
            plan["moves"] = new JArray();
            plan["prefabName"] = "7日任务拆分";
            plan["textureRenames"] = new JArray
            {
                new JObject
                {
                    ["from"] = assetPath,
                    ["toName"] = "SevenDayTaskView_OuterBackgroundFrame",
                    ["expectedGuid"] = string.Empty,
                },
            };

            AssertPreparedPlanIsUnchanged(context, plan);
        }

        [Test]
        public void VersionTwoPlanDoesNotDeriveMissingPrefabNameFromAssetRenameTargets()
        {
            const string assetPath =
                "Assets/PSDLayoutTool2/TestData/7日任务拆分/Texture/daily_bgbig1_932.png";
            PsdHierarchyChatContext context = CreateNodeSnapshotContext();
            var plan = JObject.Parse(CreateNodeReferencePlan(
                "node:n000001",
                "node:n000002",
                "snapshot-123"));
            plan["moves"] = new JArray();
            plan.Remove("prefabName");
            plan["textureRenames"] = new JArray
            {
                new JObject
                {
                    ["from"] = assetPath,
                    ["toName"] = "SevenDayTaskView_OuterBackgroundFrame",
                    ["expectedGuid"] = string.Empty,
                },
            };

            bool extracted = PsdHierarchyChatCleanupExecution.TryExtractApprovedPlan(
                "```json\n" + plan.ToString() + "\n```",
                context,
                out string approvedPlanJson,
                out string error);

            Assert.That(extracted, Is.True, error);
            Assert.That(JToken.DeepEquals(JObject.Parse(approvedPlanJson), plan), Is.True);
        }

        [Test]
        public void VersionTwoHierarchyOnlyPlanDoesNotDeriveMissingPrefabName()
        {
            PsdHierarchyChatContext context = CreateNodeSnapshotContext();
            var plan = JObject.Parse(CreateNodeReferencePlan(
                "node:n000001",
                "node:n000002",
                "snapshot-123"));
            plan.Remove("prefabName");
            plan["textureRenames"] = new JArray();
            plan["spriteAtlasRenames"] = new JArray();

            bool extracted = PsdHierarchyChatCleanupExecution.TryExtractApprovedPlan(
                "```json\n" + plan.ToString() + "\n```",
                context,
                out string approvedPlanJson,
                out string error);

            Assert.That(extracted, Is.True, error);
            Assert.That(JToken.DeepEquals(JObject.Parse(approvedPlanJson), plan), Is.True);
        }

        [Test]
        public void NativeBackendDoesNotAutomaticallySwitchToUnityCliForComponentExtraction()
        {
            var plan = JObject.Parse(CreatePlan(
                "Assets/UI/Prefab/ExampleView.prefab",
                true));
            plan["componentExtractions"] = new JArray(new JObject());

            PsdHierarchyCleanupExecutionBackend backend =
                PsdHierarchyChatCleanupExecution.ResolveExecutionBackendForPlan(
                    PsdHierarchyCleanupExecutionBackend.NativeUnity,
                    plan.ToString());

            Assert.That(backend, Is.EqualTo(PsdHierarchyCleanupExecutionBackend.NativeUnity));
        }

        [Test]
        public void RunnerPlanPreservesConflictingAssetRenamePrefixesForApplyPreflight()
        {
            const string assetPath =
                "Assets/PSDLayoutTool2/TestData/7日任务拆分/Texture/daily_bgbig1_932.png";
            PsdHierarchyChatContext context = CreateNodeSnapshotContext();
            var plan = JObject.Parse(CreateNodeReferencePlan(
                "node:n000001",
                "node:n000002",
                "snapshot-123"));
            plan["moves"] = new JArray();
            plan["prefabName"] = "7日任务拆分";
            plan["textureRenames"] = new JArray
            {
                new JObject
                {
                    ["from"] = assetPath,
                    ["toName"] = "SevenDayTaskView_OuterBackgroundFrame",
                    ["expectedGuid"] = string.Empty,
                },
                new JObject
                {
                    ["from"] = assetPath,
                    ["toName"] = "OtherView_OuterBackgroundFrame",
                    ["expectedGuid"] = string.Empty,
                },
            };

            AssertPreparedPlanIsUnchanged(context, plan);
        }

        [Test]
        public void RunnerFailureSummaryOmitsTheRepeatedExecutionStack()
        {
            const string runnerOutput =
                "{\"success\":false,\"error\":\"Unity preflight failed: Plan source path was not found for moves[18].source: Root/main_spdb2 Candidate source paths: Root/ui_main_spdb2 Execution exception: Plan source path was not found Stack trace: DynamicCommand\"}";

            string error = PsdHierarchyChatCleanupExecution.SummarizeFailure(runnerOutput);

            Assert.That(
                error,
                Is.EqualTo(
                    "Plan source path was not found for moves[18].source: Root/main_spdb2 Candidate source paths: Root/ui_main_spdb2"));
        }

        [Test]
        public void VersionTwoNodeReferencesAndFingerprintRemainStableAfterPreparation()
        {
            PsdHierarchyChatContext context = CreateNodeSnapshotContext();

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                context,
                CreateNodeReferencePlan("node:n000002", "node:n000001", "snapshot-123"),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.True, error);
            var runnerPlan = JObject.Parse(runnerPlanJson);
            Assert.That(runnerPlan["version"].Value<int>(), Is.EqualTo(2));
            Assert.That(runnerPlan["snapshotFingerprint"].Value<string>(), Is.EqualTo("snapshot-123"));
            Assert.That(runnerPlan["moves"][0]["source"].Value<string>(), Is.EqualTo("node:n000002"));
            Assert.That(runnerPlan["moves"][0]["destination"].Value<string>(), Is.EqualTo("node:n000001"));
        }

        [Test]
        public void VersionTwoPlanPreservesReviewedDirectChildVerificationNames()
        {
            var plan = JObject.Parse(
                CreateNodeReferencePlan("node:n000002", "node:n000001", "snapshot-123"));
            plan["verify"] = new JObject
            {
                ["directChildren"] = new JArray
                {
                    new JObject
                    {
                        ["path"] = "Root/Group/[Item_1]",
                        ["children"] = new JArray(
                            "ItemFrame",
                            "ItemIcon",
                            "ItemFrame",
                            "ItemLabel",
                            "ItemIcon"),
                    },
                },
            };

            AssertPreparedPlanIsUnchanged(CreateNodeSnapshotContext(), plan);
        }

        [Test]
        public void VersionTwoPlanRejectsAnUnknownNodeReference()
        {
            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                CreateNodeSnapshotContext(),
                CreateNodeReferencePlan("node:n999999", "node:n000001", "snapshot-123"),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.False);
            Assert.That(runnerPlanJson, Is.Empty);
            Assert.That(error, Does.Contain("n999999").And.Contain("不存在"));
        }

        [Test]
        public void VersionTwoPlanRejectsRawSourcePaths()
        {
            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                CreateNodeSnapshotContext(),
                CreateNodeReferencePlan("Root/Imagined/15K", "node:n000001", "snapshot-123"),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.False);
            Assert.That(runnerPlanJson, Is.Empty);
            Assert.That(error, Does.Contain("moves[0].source").And.Contain("node:"));
        }

        [Test]
        public void VersionTwoPlanRejectsAStaleSnapshotFingerprint()
        {
            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                CreateNodeSnapshotContext(),
                CreateNodeReferencePlan("node:n000002", "node:n000001", "stale-snapshot"),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.False);
            Assert.That(runnerPlanJson, Is.Empty);
            Assert.That(error, Does.Contain("快照").And.Contain("失效"));
        }

        [Test]
        public void VersionTwoPlanReportsAllInvalidNodeReferencesAtOnce()
        {
            var plan = JObject.Parse(
                CreateNodeReferencePlan("node:n999998", "node:n000001", "snapshot-123"));
            ((JArray)plan["moves"]).Add(new JObject
            {
                ["source"] = "node:n999999",
                ["destination"] = "node:n000001",
                ["siblingIndex"] = 1,
            });

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                CreateNodeSnapshotContext(),
                plan.ToString(),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.False);
            Assert.That(runnerPlanJson, Is.Empty);
            Assert.That(error, Does.Contain("n999998").And.Contain("n999999"));
        }

        [Test]
        public void VersionTwoPreparationDoesNotAutoCompleteAnUnresolvedFlatSiblingFinding()
        {
            var plan = JObject.Parse(
                CreateNodeReferencePlan("node:n000002", "node:n000001", "snapshot-123"));
            plan["moves"] = new JArray();

            AssertPreparedPlanIsUnchanged(CreateFlatSiblingContext(), plan);
        }

        [Test]
        public void VersionTwoPreparationDoesNotDropReviewedContainerRemovals()
        {
            var plan = JObject.Parse(
                CreateNodeReferencePlan("node:n000004", "node:n000003", "snapshot-123"));
            plan["moves"] = new JArray();
            plan["containmentResolutions"] = new JArray();
            plan["flatSiblingResolutions"] = new JArray();
            plan["emptyContainerRemovals"] = new JArray
            {
                new JObject { ["source"] = "node:n000003" },
                new JObject { ["source"] = "node:n000002" },
                new JObject { ["source"] = "node:n000007" },
            };

            AssertPreparedPlanIsUnchanged(CreateNestedFlatSiblingContext(), plan);
        }

        [Test]
        public void VersionTwoPreparationDoesNotResolveAFlatSiblingFindingImplicitly()
        {
            var plan = JObject.Parse(
                CreateNodeReferencePlan("node:n000002", "node:n000001", "snapshot-123"));
            AssertPreparedPlanIsUnchanged(CreateFlatSiblingContext(), plan);
        }

        [Test]
        public void VersionTwoPreparationDoesNotRepairFlatSiblingWrapperPlacement()
        {
            var plan = JObject.Parse(CreateFlatSiblingGroupPlan("node:n000006"));
            AssertPreparedPlanIsUnchanged(CreateFlatSiblingContext(), plan);
        }

        [Test]
        public void VersionTwoPreparationDoesNotRewriteFlatSiblingGroupWithAdditionalSibling()
        {
            var plan = JObject.Parse(CreateFlatSiblingGroupPlan("node:n000006"));
            ((JArray)plan["moves"]).Add(new JObject
            {
                ["source"] = "node:n000006",
                    ["destination"] = "@legacy_wrapper",
                ["siblingIndex"] = 4,
            });

            AssertPreparedPlanIsUnchanged(CreateFlatSiblingContext(), plan);
        }

        [Test]
        public void VersionTwoPreparationDoesNotReassignFlatSiblingMembersAcrossFindings()
        {
            var plan = JObject.Parse(
                CreateNodeReferencePlan("node:n000002", "node:n000001", "snapshot-123"));
            plan["wrappers"] = new JArray
            {
                new JObject
                {
                    ["id"] = "flat_group_001",
                    ["parent"] = "node:n000001",
                    ["name"] = "[FlatGroup001]",
                    ["siblingIndex"] = 0,
                },
                new JObject
                {
                    ["id"] = "flat_group_002",
                    ["parent"] = "node:n000006",
                    ["name"] = "[FlatGroup002]",
                    ["siblingIndex"] = 5,
                },
            };
            plan["moves"] = new JArray
            {
                new JObject { ["source"] = "node:n000002", ["destination"] = "@flat_group_001", ["siblingIndex"] = 0 },
                new JObject { ["source"] = "node:n000003", ["destination"] = "@flat_group_001", ["siblingIndex"] = 1 },
                new JObject { ["source"] = "node:n000004", ["destination"] = "@flat_group_001", ["siblingIndex"] = 2 },
                new JObject { ["source"] = "node:n000005", ["destination"] = "@flat_group_002", ["siblingIndex"] = 0 },
                new JObject { ["source"] = "node:n000006", ["destination"] = "@flat_group_002", ["siblingIndex"] = 1 },
                new JObject { ["source"] = "node:n000007", ["destination"] = "@flat_group_002", ["siblingIndex"] = 2 },
                new JObject { ["source"] = "node:n000002", ["destination"] = "@flat_group_002", ["siblingIndex"] = 3 },
            };
            plan["tightBounds"] = new JArray
            {
                new JObject { ["target"] = "@flat_group_001" },
                new JObject { ["target"] = "@flat_group_002" },
            };
            plan["flatSiblingResolutions"] = new JArray
            {
                new JObject { ["findingId"] = "flat_sibling_001", ["mode"] = "group", ["wrapperId"] = "flat_group_001" },
                new JObject { ["findingId"] = "flat_sibling_002", ["mode"] = "group", ["wrapperId"] = "flat_group_002" },
            };

            AssertPreparedPlanIsUnchanged(CreateMultipleFlatSiblingContext(), plan);
        }

        [Test]
        public void VersionTwoPreparationDoesNotMoveAFlatSiblingMemberBackToAnotherWrapper()
        {
            var plan = JObject.Parse(CreateFlatSiblingGroupPlan("node:n000001"));
            ((JArray)plan["wrappers"]).Add(new JObject
            {
                ["id"] = "unrelated_group",
                ["parent"] = "node:n000001",
                ["name"] = "[UnrelatedGroup]",
                ["siblingIndex"] = 4,
            });
            ((JArray)plan["moves"]).Add(new JObject
            {
                ["source"] = "node:n000002",
                ["destination"] = "@unrelated_group",
                ["siblingIndex"] = 0,
            });

            AssertPreparedPlanIsUnchanged(CreateFlatSiblingContext(), plan);
        }

        [Test]
        public void VersionTwoPreparationDoesNotNormalizeAFlatSiblingGroup()
        {
            var plan = JObject.Parse(CreateFlatSiblingGroupPlan("node:n000006"));
            ((JArray)plan["moves"]).Add(new JObject
            {
                ["source"] = "node:n000007",
                    ["destination"] = "@legacy_wrapper",
                ["siblingIndex"] = 4,
            });

            AssertPreparedPlanIsUnchanged(CreateFlatSiblingContext(), plan);
        }

        [Test]
        public void VersionTwoPlansWithDifferentReviewedFlatSiblingSyntaxRemainDistinct()
        {
            var first = JObject.Parse(CreateFlatSiblingGroupPlan("node:n000001"));
            var second = JObject.Parse(CreateFlatSiblingGroupPlan("node:n000001"));
            first["wrappers"][0]["id"] = "flat_group_001";
            first["moves"].OfType<JObject>().First()["destination"] = "@flat_group_001";
            first["flatSiblingResolutions"][0]["wrapperId"] = "flat_group_001";
            second["wrappers"][0]["id"] = "another_wrapper";
            second["wrappers"][0]["parent"] = "node:n000006";
            second["moves"].OfType<JObject>().First()["destination"] = "@another_wrapper";
            second["flatSiblingResolutions"][0]["wrapperId"] = "another_wrapper";

            AssertPreparedPlanIsUnchanged(CreateFlatSiblingContext(), first);
            AssertPreparedPlanIsUnchanged(CreateFlatSiblingContext(), second);
            Assert.That(JToken.DeepEquals(first, second), Is.False);
        }

        [Test]
        public void VersionTwoPlanCarriesAResolvedFlatSiblingFindingIntoTheRunnerPlan()
        {
            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                CreateFlatSiblingContext(),
                CreateFlatSiblingGroupPlan("node:n000001"),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.True, error);
            var runnerPlan = JObject.Parse(runnerPlanJson);
            Assert.That(runnerPlan["flatSiblingResolutions"], Has.Count.EqualTo(1));
            Assert.That(
                runnerPlan["flatSiblingResolutions"][0].Value<string>("findingId"),
                Is.EqualTo("flat_sibling_001"));
        }

        [Test]
        public void VersionTwoPreparationDoesNotInventAnOmittedComponentFamily()
        {
            var plan = JObject.Parse(
                CreateNodeReferencePlan("node:n000003", "node:n000002", "snapshot-123"));
            AssertPreparedPlanIsUnchanged(CreateRequiredCandidateContext(), plan);
        }

        [Test]
        public void VersionTwoPreparationPreservesReviewedSkipForLaterApplyRejection()
        {
            var plan = JObject.Parse(CreateNodeReferencePlan("node:n000003", "node:n000002", "snapshot-123"));
            plan["componentFamilyDecisions"] = new JArray
            {
                new JObject
                {
                    ["candidateId"] = "family_001",
                    ["parent"] = "node:n000002",
                    ["sources"] = new JArray("node:n000003", "node:n000004", "node:n000005"),
                    ["mode"] = "skip",
                    ["reason"] = "not needed",
                },
            };

            AssertPreparedPlanIsUnchanged(CreateRequiredCandidateContext(), plan);
        }

        [Test]
        public void VersionTwoPreparationDoesNotRepairSkippedMandatoryComponentFamily()
        {
            var plan = JObject.Parse(CreateNodeReferencePlan("node:n000003", "node:n000002", "snapshot-123"));
            plan["moves"] = new JArray();
            plan["componentFamilyDecisions"] = new JArray
            {
                new JObject
                {
                    ["candidateId"] = "family_001",
                    ["parent"] = "node:n000002",
                    ["sources"] = new JArray("node:n000003", "node:n000004", "node:n000005"),
                    ["mode"] = "skip",
                    ["reason"] = "AI could not complete the extraction.",
                },
            };

            AssertPreparedPlanIsUnchanged(CreateRequiredCandidateContext("component"), plan);
        }

        [Test]
        public void VersionTwoPreparationDoesNotSynthesizeFallbackForNonEnglishComponentCandidateName()
        {
            var plan = JObject.Parse(CreateNodeReferencePlan("node:n000003", "node:n000002", "snapshot-123"));
            plan["moves"] = new JArray();
            plan["containmentResolutions"] = new JArray();
            plan["flatSiblingResolutions"] = new JArray();
            plan["componentFamilyDecisions"] = new JArray
            {
                new JObject
                {
                    ["candidateId"] = "family_001",
                    ["parent"] = "node:n000002",
                    ["sources"] = new JArray("node:n000003", "node:n000004", "node:n000005"),
                    ["mode"] = "skip",
                    ["reason"] = "Exercise deterministic repair for an existing snapshot.",
                },
            };

            AssertPreparedPlanIsUnchanged(CreateRequiredCandidateContext("component", "组 16"), plan);
        }

        [Test]
        public void VersionTwoPreparationDoesNotRepairSkippedMandatoryVariantFamily()
        {
            var plan = JObject.Parse(CreateNodeReferencePlan("node:n000003", "node:n000002", "snapshot-123"));
            plan["moves"] = new JArray();
            plan["componentFamilyDecisions"] = new JArray
            {
                new JObject
                {
                    ["candidateId"] = "family_001",
                    ["parent"] = "node:n000002",
                    ["sources"] = new JArray("node:n000003", "node:n000004", "node:n000005"),
                    ["mode"] = "skip",
                    ["reason"] = "AI could not find a shared direct child.",
                },
            };

            AssertPreparedPlanIsUnchanged(CreateDistinctVariantCandidateContext(), plan);
        }

        [Test]
        public void VersionTwoPreparationPreservesVariantSourcesSplitByAReviewedMove()
        {
            var plan = JObject.Parse(CreateNodeReferencePlan("node:n000004", "node:n000001", "snapshot-123"));
            plan["containmentResolutions"] = new JArray();
            plan["flatSiblingResolutions"] = new JArray();
            plan["componentFamilyDecisions"] = new JArray
            {
                new JObject
                {
                    ["candidateId"] = "family_001",
                    ["parent"] = "node:n000002",
                    ["sources"] = new JArray("node:n000003", "node:n000004", "node:n000005"),
                    ["mode"] = "skip",
                    ["reason"] = "Exercise the deterministic required-variant fallback.",
                },
            };

            AssertPreparedPlanIsUnchanged(CreateDistinctVariantCandidateContext(), plan);
        }

        [Test]
        public void VersionTwoPreparationDoesNotCompletePartialVariantFamilyMoves()
        {
            var plan = JObject.Parse(CreateNodeReferencePlan("node:n000003", "@day_card_markers", "snapshot-123"));
            plan["wrappers"] = new JArray
            {
                new JObject
                {
                    ["id"] = "day_card_markers",
                    ["parent"] = "node:n000002",
                    ["name"] = "[DayCardMarkers]",
                    ["siblingIndex"] = 0,
                },
            };
            plan["moves"] = new JArray
            {
                new JObject
                {
                    ["source"] = "node:n000003",
                    ["destination"] = "@day_card_markers",
                    ["siblingIndex"] = 0,
                },
                new JObject
                {
                    ["source"] = "node:n000004",
                    ["destination"] = "@day_card_markers",
                    ["siblingIndex"] = 1,
                },
            };
            plan["tightBounds"] = new JArray
            {
                new JObject { ["target"] = "@day_card_markers" },
            };
            plan["containmentResolutions"] = new JArray();
            plan["flatSiblingResolutions"] = new JArray();
            plan["componentFamilyDecisions"] = new JArray
            {
                new JObject
                {
                    ["candidateId"] = "family_001",
                    ["parent"] = "node:n000002",
                    ["sources"] = new JArray("node:n000003", "node:n000004", "node:n000005"),
                    ["mode"] = "skip",
                    ["reason"] = "Exercise deterministic completion of a partial family move.",
                },
            };

            AssertPreparedPlanIsUnchanged(CreateDistinctVariantCandidateContext(), plan);
        }

        [Test]
        public void VersionTwoPreparationPreservesOverlappingComplexExtractionsForApplyRejection()
        {
            var plan = JObject.Parse(CreateNodeReferencePlan("node:n000003", "node:n000002", "snapshot-123"));
            plan["moves"] = new JArray();
            plan["componentFamilyDecisions"] = new JArray
            {
                new JObject
                {
                    ["candidateId"] = "family_001",
                    ["parent"] = "node:n000002",
                    ["sources"] = new JArray("node:n000003", "node:n000004", "node:n000005"),
                    ["mode"] = "component",
                    ["extractionId"] = "task_item_a",
                },
            };
            plan["componentExtractions"] = new JArray
            {
                new JObject
                {
                    ["id"] = "task_item_a",
                    ["template"] = "node:n000003",
                    ["assetPath"] = "Assets/UI/Prefab/Common/TaskItem.prefab",
                    ["instances"] = new JArray("node:n000003", "node:n000004", "node:n000005"),
                },
                new JObject
                {
                    ["id"] = "task_item_b",
                    ["template"] = "node:n000003",
                    ["assetPath"] = "Assets/UI/Prefab/Common/TaskItem.prefab",
                    ["instances"] = new JArray("node:n000003", "node:n000004", "node:n000005"),
                },
            };

            AssertPreparedPlanIsUnchanged(CreateRequiredCandidateContext("component"), plan);
        }

        [Test]
        public void VersionTwoPreparationDoesNotReplaceSkippedStatefulFamilyWithVariantFallback()
        {
            var plan = JObject.Parse(CreateNodeReferencePlan("node:n000003", "node:n000002", "snapshot-123"));
            plan["moves"] = new JArray();
            plan["componentFamilyDecisions"] = new JArray
            {
                new JObject
                {
                    ["candidateId"] = "family_001",
                    ["parent"] = "node:n000002",
                    ["sources"] = new JArray("node:n000003", "node:n000004", "node:n000005"),
                    ["mode"] = "skip",
                    ["reason"] = "AI could not complete the stateful mapping.",
                },
            };

            AssertPreparedPlanIsUnchanged(CreateDistinctVariantCandidateContext("stateful"), plan);
        }

        [Test]
        public void VersionTwoPreparationDoesNotSynthesizeFallbackForNonEnglishVariantCandidateName()
        {
            var plan = JObject.Parse(CreateNodeReferencePlan("node:n000003", "node:n000002", "snapshot-123"));
            plan["moves"] = new JArray();
            plan["containmentResolutions"] = new JArray();
            plan["flatSiblingResolutions"] = new JArray();
            plan["componentFamilyDecisions"] = new JArray
            {
                new JObject
                {
                    ["candidateId"] = "family_001",
                    ["parent"] = "node:n000002",
                    ["sources"] = new JArray("node:n000003", "node:n000004", "node:n000005"),
                    ["mode"] = "skip",
                    ["reason"] = "Exercise the deterministic fallback for an existing snapshot.",
                },
            };

            AssertPreparedPlanIsUnchanged(CreateDistinctVariantCandidateContext("variant", "组 16"), plan);
        }

        [Test]
        public void VersionTwoPreparationDoesNotDeriveNamesForInvalidVariantInstances()
        {
            var plan = JObject.Parse(CreateNodeReferencePlan("node:n000003", "node:n000002", "snapshot-123"));
            plan["moves"] = new JArray();
            plan["componentFamilyDecisions"] = new JArray
            {
                new JObject
                {
                    ["candidateId"] = "family_001",
                    ["parent"] = "node:n000002",
                    ["sources"] = new JArray(
                        "node:n000003",
                        "node:n000004",
                        "node:n000005",
                        "node:n000006",
                        "node:n000007"),
                    ["mode"] = "skip",
                    ["reason"] = "AI skipped the mandatory family.",
                },
            };

            AssertPreparedPlanIsUnchanged(CreateInvalidVariantInstanceNameContext(), plan);
        }

        [Test]
        public void ContextPlanExtractionReturnsTheReviewedVersionTwoJson()
        {
            var plan = JObject.Parse(CreateNodeReferencePlan("node:n000003", "node:n000002", "snapshot-123"));
            plan["moves"] = new JArray();
            plan["componentFamilyDecisions"] = new JArray
            {
                new JObject
                {
                    ["candidateId"] = "family_001",
                    ["parent"] = "node:n000002",
                    ["sources"] = new JArray("node:n000003", "node:n000004", "node:n000005"),
                    ["mode"] = "skip",
                    ["reason"] = "AI skipped the mandatory family.",
                },
            };
            string reply = "review\n```json\n" + plan.ToString() + "\n```";

            bool extracted = PsdHierarchyChatCleanupExecution.TryExtractApprovedPlan(
                reply,
                CreateDistinctVariantCandidateContext(),
                out string approvedPlanJson,
                out string error);

            Assert.That(extracted, Is.True, error);
            Assert.That(JToken.DeepEquals(JObject.Parse(approvedPlanJson), plan), Is.True);
        }

        [Test]
        public void VersionTwoPlanAcceptsMandatoryExtractionWithoutRewritingReviewedMetadata()
        {
            var plan = JObject.Parse(CreateNodeReferencePlan("node:n000003", "node:n000002", "snapshot-123"));
            plan["componentFamilyDecisions"] = new JArray
            {
                new JObject
                {
                    ["candidateId"] = "family_001",
                    ["parent"] = "node:n000002",
                    ["sources"] = new JArray("node:n000003", "node:n000004", "node:n000005"),
                    ["mode"] = "component",
                    ["extractionId"] = "task_item",
                    ["reason"] = "Same repeated component family.",
                },
            };
            plan["componentExtractions"] = new JArray
            {
                new JObject
                {
                    ["id"] = "task_item",
                    ["template"] = "node:n000003",
                    ["assetPath"] = "Assets/UI/Prefab/Common/TaskItem.prefab",
                    ["instances"] = new JArray("node:n000003", "node:n000004", "node:n000005"),
                },
            };

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                CreateRequiredCandidateContext(),
                plan.ToString(),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.True, error);
            var runnerPlan = JObject.Parse(runnerPlanJson);
            Assert.That(
                runnerPlan["componentFamilyDecisions"][0]["candidateId"].Value<string>(),
                Is.EqualTo("family_001"));
            Assert.That(
                runnerPlan["componentExtractions"][0]["template"].Value<string>(),
                Is.EqualTo("node:n000003"));
        }

        [Test]
        public void VersionTwoPlanDoesNotDowngradeSingleStateVariantDuringPreparation()
        {
            var plan = JObject.Parse(
                CreateNodeReferencePlan("node:n000003", "node:n000002", "snapshot-123"));
            plan["moves"] = new JArray();
            plan["componentFamilyDecisions"] = new JArray
            {
                new JObject
                {
                    ["candidateId"] = "family_001",
                    ["parent"] = "node:n000002",
                    ["sources"] = new JArray("node:n000003", "node:n000004", "node:n000005"),
                    ["mode"] = "variant",
                    ["extractionId"] = "task_item",
                    ["reason"] = "The plan selected one observed visual state.",
                },
            };
            plan["variantComponentExtractions"] = new JArray
            {
                new JObject
                {
                    ["id"] = "task_item",
                    ["template"] = "node:n000003",
                    ["assetPath"] = "Assets/UI/Prefab/Common/TaskItem.prefab",
                    ["defaultState"] = "normal",
                    ["states"] = new JArray
                    {
                        new JObject
                        {
                            ["id"] = "normal",
                            ["source"] = "node:n000003",
                            ["name"] = "[State_Normal]",
                        },
                    },
                    ["instances"] = new JArray
                    {
                        new JObject { ["source"] = "node:n000003", ["name"] = "[TaskItem_1]", ["state"] = "normal" },
                        new JObject { ["source"] = "node:n000004", ["name"] = "[TaskItem_2]", ["state"] = "normal" },
                        new JObject { ["source"] = "node:n000005", ["name"] = "[TaskItem_3]", ["state"] = "normal" },
                    },
                },
            };

            AssertPreparedPlanIsUnchanged(CreateRequiredCandidateContext(), plan);
        }

        [Test]
        public void VersionTwoPlanPreservesRequiredVariantForApplyValidation()
        {
            var plan = JObject.Parse(
                CreateNodeReferencePlan("node:n000003", "node:n000002", "snapshot-123"));
            plan["moves"] = new JArray();
            plan["componentFamilyDecisions"] = new JArray
            {
                new JObject
                {
                    ["candidateId"] = "family_001",
                    ["parent"] = "node:n000002",
                    ["sources"] = new JArray("node:n000003", "node:n000004", "node:n000005"),
                    ["mode"] = "variant",
                    ["extractionId"] = "task_item",
                    ["reason"] = "The plan selected one observed visual state.",
                },
            };
            plan["variantComponentExtractions"] = new JArray
            {
                new JObject
                {
                    ["id"] = "task_item",
                    ["template"] = "node:n000003",
                    ["assetPath"] = "Assets/UI/Prefab/Common/TaskItem.prefab",
                    ["defaultState"] = "normal",
                    ["states"] = new JArray
                    {
                        new JObject
                        {
                            ["id"] = "normal",
                            ["source"] = "node:n000003",
                            ["name"] = "[State_Normal]",
                        },
                    },
                    ["instances"] = new JArray
                    {
                        new JObject { ["source"] = "node:n000003", ["name"] = "[TaskItem_1]", ["state"] = "normal" },
                        new JObject { ["source"] = "node:n000004", ["name"] = "[TaskItem_2]", ["state"] = "normal" },
                        new JObject { ["source"] = "node:n000005", ["name"] = "[TaskItem_3]", ["state"] = "normal" },
                    },
                },
            };

            AssertPreparedPlanIsUnchanged(CreateRequiredCandidateContext("variant"), plan);
        }

        [Test]
        public void VersionTwoPlanDoesNotCompleteMissingStateMembersDuringPreparation()
        {
            var plan = JObject.Parse(
                CreateNodeReferencePlan("node:n000002", "node:n000001", "snapshot-123"));
            plan["moves"] = new JArray();
            plan["renames"] = new JArray
            {
                new JObject { ["target"] = "node:n000004", ["name"] = "DayText" },
                new JObject { ["target"] = "node:n000005", ["name"] = "LockIcon" },
            };
            plan["statefulComponentExtractions"] = new JArray
            {
                new JObject
                {
                    ["id"] = "day_card",
                    ["template"] = "node:n000002",
                    ["assetPath"] = "Assets/UI/Prefab/Common/DayCard.prefab",
                    ["common"] = new JObject
                    {
                        ["source"] = "node:n000002",
                        ["members"] = new JArray
                        {
                            new JObject { ["sourceName"] = "DayLabel", ["name"] = "DayLabel" },
                        },
                    },
                    ["states"] = new JArray
                    {
                        new JObject
                        {
                            ["id"] = "locked",
                            ["source"] = "node:n000002",
                            ["name"] = "[Locked]",
                            ["members"] = new JArray
                            {
                                new JObject { ["sourceName"] = "Background", ["name"] = "Background" },
                                new JObject { ["sourceName"] = "Lock", ["name"] = "Lock" },
                            },
                        },
                        new JObject
                        {
                            ["id"] = "available",
                            ["source"] = "node:n000006",
                            ["name"] = "[Available]",
                            ["members"] = new JArray
                            {
                                new JObject { ["sourceName"] = "AvailableBackground", ["name"] = "Background" },
                            },
                        },
                    },
                    ["defaultState"] = "available",
                    ["instances"] = new JArray
                    {
                        new JObject
                        {
                            ["source"] = "node:n000002",
                            ["name"] = "[DayCard_1]",
                            ["state"] = "locked",
                            ["commonSourceNames"] = new JArray(),
                            ["stateSourceNames"] = new JArray("Background", "Lock"),
                        },
                        new JObject
                        {
                            ["source"] = "node:n000006",
                            ["name"] = "[DayCard_2]",
                            ["state"] = "available",
                            ["commonSourceNames"] = new JArray("AvailableLabel"),
                            ["stateSourceNames"] = new JArray(),
                        },
                    },
                },
            };

            AssertPreparedPlanIsUnchanged(CreateStatefulSnapshotContext(), plan);
        }

        [Test]
        public void VersionTwoPlanDoesNotFallbackWhenStatefulInstanceHasNoProvableMapping()
        {
            var plan = JObject.Parse(
                CreateNodeReferencePlan("node:n000002", "node:n000001", "snapshot-123"));
            plan["moves"] = new JArray();
            plan["renames"] = new JArray
            {
                new JObject { ["target"] = "node:n000004", ["name"] = "DayText" },
                new JObject { ["target"] = "node:n000005", ["name"] = "LockIcon" },
            };
            plan["statefulComponentExtractions"] = new JArray
            {
                new JObject
                {
                    ["id"] = "day_card",
                    ["template"] = "node:n000001",
                    ["assetPath"] = "Assets/UI/Prefab/Common/DayCard.prefab",
                    ["common"] = new JObject
                    {
                        ["source"] = "node:n000006",
                        ["members"] = new JArray
                        {
                            new JObject { ["sourceName"] = "AvailableLabel", ["name"] = "DayLabel" },
                        },
                    },
                    ["states"] = new JArray
                    {
                        new JObject
                        {
                            ["id"] = "locked",
                            ["source"] = "node:n000002",
                            ["name"] = "[Locked]",
                            ["members"] = new JArray
                            {
                                new JObject { ["sourceName"] = "Background", ["name"] = "Background" },
                                new JObject { ["sourceName"] = "Lock", ["name"] = "Lock" },
                            },
                        },
                        new JObject
                        {
                            ["id"] = "available",
                            ["source"] = "node:n000006",
                            ["name"] = "[Available]",
                            ["members"] = new JArray
                            {
                                new JObject { ["sourceName"] = "AvailableBackground", ["name"] = "Background" },
                                new JObject { ["sourceName"] = "AvailableLabel", ["name"] = "DayLabel" },
                            },
                        },
                    },
                    ["defaultState"] = "locked",
                    ["instances"] = new JArray
                    {
                        new JObject
                        {
                            ["source"] = "node:n000002",
                            ["name"] = "[DayCard_1]",
                            ["state"] = "locked",
                            ["commonSourceNames"] = new JArray(),
                            ["stateSourceNames"] = new JArray(),
                        },
                        new JObject
                        {
                            ["source"] = "node:n000002",
                            ["name"] = "[DayCard_2]",
                            ["state"] = "available",
                            ["commonSourceNames"] = new JArray(),
                            ["stateSourceNames"] = new JArray(),
                        },
                    },
                },
            };
            ((JObject)plan["statefulComponentExtractions"][0]).Remove("id");
            ((JObject)plan["statefulComponentExtractions"][0]).Remove("template");
            ((JObject)plan["statefulComponentExtractions"][0]).Remove("assetPath");

            JObject reviewedPlan = (JObject)plan.DeepClone();
            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                CreateStatefulSnapshotContext(),
                plan.ToString(),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.False);
            Assert.That(runnerPlanJson, Is.Empty);
            Assert.That(
                error,
                Does.Contain("节点引用校验失败")
                    .And.Contain("statefulComponentExtractions[0].template"));
            Assert.That(
                JToken.DeepEquals(plan, reviewedPlan),
                Is.True,
                "Rejected formal v2 plans must remain exactly as reviewed.");
        }

        [Test]
        public void VersionTwoPlanDoesNotFallbackWhenStateMemberIsNotAnObservedDirectChild()
        {
            var plan = JObject.Parse(
                CreateNodeReferencePlan("node:n000002", "node:n000001", "snapshot-123"));
            plan["moves"] = new JArray();
            plan["statefulComponentExtractions"] = new JArray
            {
                new JObject
                {
                    ["id"] = "day_marker",
                    ["template"] = "node:n000002",
                    ["assetPath"] = "Assets/UI/Prefab/Common/DayMarker.prefab",
                    ["common"] = new JObject
                    {
                        ["source"] = "node:n000002",
                        ["members"] = new JArray
                        {
                            new JObject { ["sourceName"] = "DayLabel", ["name"] = "DayLabel" },
                        },
                    },
                    ["states"] = new JArray
                    {
                        new JObject
                        {
                            ["id"] = "locked",
                            ["source"] = "node:n000002",
                            ["name"] = "[Locked]",
                            ["members"] = new JArray
                            {
                                new JObject { ["sourceName"] = "Background", ["name"] = "Background" },
                                new JObject { ["sourceName"] = "Lock", ["name"] = "Lock" },
                            },
                        },
                        new JObject
                        {
                            ["id"] = "available",
                            ["source"] = "node:n000006",
                            ["name"] = "[Available]",
                            ["members"] = new JArray
                            {
                                new JObject { ["sourceName"] = "AvailableBackground", ["name"] = "Background" },
                                new JObject { ["sourceName"] = "DayMarkerLock", ["name"] = "Lock" },
                            },
                        },
                    },
                    ["defaultState"] = "locked",
                    ["instances"] = new JArray
                    {
                        new JObject
                        {
                            ["source"] = "node:n000002",
                            ["name"] = "[DayMarker_1]",
                            ["state"] = "locked",
                            ["commonSourceNames"] = new JArray(),
                            ["stateSourceNames"] = new JArray(),
                        },
                        new JObject
                        {
                            ["source"] = "node:n000006",
                            ["name"] = "[DayMarker_2]",
                            ["state"] = "available",
                            ["commonSourceNames"] = new JArray(),
                            ["stateSourceNames"] = new JArray(),
                        },
                    },
                },
            };

            AssertPreparedPlanIsUnchanged(CreateStatefulSnapshotContext(), plan);
        }

        [Test]
        public void VersionTwoPlanPreservesEmptyStatefulCommonForApplyRejection()
        {
            var plan = JObject.Parse(CreateNodeReferencePlan("node:n000002", "node:n000001", "snapshot-123"));
            plan["moves"] = new JArray();
            plan["statefulComponentExtractions"] = new JArray
            {
                new JObject
                {
                    ["id"] = "day_card",
                    ["template"] = "node:n000002",
                    ["assetPath"] = "Assets/UI/Prefab/Common/DayCard.prefab",
                    ["common"] = new JObject
                    {
                        ["source"] = "node:n000002",
                        ["members"] = new JArray(),
                    },
                    ["states"] = new JArray
                    {
                        new JObject
                        {
                            ["id"] = "locked",
                            ["source"] = "node:n000002",
                            ["name"] = "[Locked]",
                            ["members"] = new JArray
                            {
                                new JObject { ["sourceName"] = "Background", ["name"] = "Background" },
                                new JObject { ["sourceName"] = "DayLabel", ["name"] = "DayLabel" },
                                new JObject { ["sourceName"] = "Lock", ["name"] = "Lock" },
                            },
                        },
                        new JObject
                        {
                            ["id"] = "available",
                            ["source"] = "node:n000006",
                            ["name"] = "[Available]",
                            ["members"] = new JArray
                            {
                                new JObject { ["sourceName"] = "AvailableBackground", ["name"] = "Background" },
                                new JObject { ["sourceName"] = "AvailableLabel", ["name"] = "DayLabel" },
                            },
                        },
                    },
                    ["defaultState"] = "available",
                    ["instances"] = new JArray
                    {
                        new JObject
                        {
                            ["source"] = "node:n000002",
                            ["name"] = "[DayCard_1]",
                            ["state"] = "locked",
                            ["commonSourceNames"] = new JArray(),
                            ["stateSourceNames"] = new JArray("Background", "DayLabel", "Lock"),
                        },
                        new JObject
                        {
                            ["source"] = "node:n000006",
                            ["name"] = "[DayCard_2]",
                            ["state"] = "available",
                            ["commonSourceNames"] = new JArray(),
                            ["stateSourceNames"] = new JArray("AvailableBackground", "AvailableLabel"),
                        },
                    },
                },
            };

            AssertPreparedPlanIsUnchanged(CreateStatefulSnapshotContext(), plan);
        }

        [Test]
        public void ActualSnapshotNodeIdsRemainBoundToTheReviewedFingerprint()
        {
            const string folder = "Assets/__PsdHierarchyActualSnapshotTests";
            const string sourcePsd = folder + "/Source.psd";
            const string targetPrefab = folder + "/ExampleView.prefab";
            Assert.That(AssetDatabase.IsValidFolder(folder), Is.False, "Test fixture path is already occupied.");
            AssetDatabase.CreateFolder("Assets", "__PsdHierarchyActualSnapshotTests");

            GameObject root = new GameObject("ExampleView", typeof(RectTransform));
            try
            {
                var child = new GameObject("Content", typeof(RectTransform));
                child.transform.SetParent(root.transform, false);
                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, targetPrefab), Is.Not.Null);

                Assert.That(PsdHierarchyChatContextBuilder.TryCreate(
                    sourcePsd,
                    targetPrefab,
                    out PsdHierarchyChatContext context,
                    out string contextError), Is.True, contextError);

                var snapshot = JObject.Parse(context.hierarchySnapshotJson);
                JArray nodes = (JArray)snapshot["nodes"];
                JObject source = nodes.OfType<JObject>()
                    .First(node => !string.IsNullOrEmpty(node.Value<string>("parentId")));
                string parentId = source.Value<string>("parentId");
                JObject parent = nodes.OfType<JObject>()
                    .First(node => string.Equals(node.Value<string>("id"), parentId));

                var plan = JObject.Parse(CreateNodeReferencePlan(
                    "node:" + source.Value<string>("id"),
                    "node:" + parent.Value<string>("id"),
                    context.hierarchySnapshotFingerprint));
                plan["prefabAssetPath"] = targetPrefab;
                plan["output"]["assetPath"] = targetPrefab;
                plan["prefabName"] = "ExampleView";
                plan["moves"][0]["siblingIndex"] = source.Value<int>("siblingIndex");

                bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                    context,
                    plan.ToString(),
                    out string runnerPlanJson,
                    out string error);

                Assert.That(prepared, Is.True, error);
                var runnerPlan = JObject.Parse(runnerPlanJson);
                Assert.That(runnerPlan.Value<long>("version"), Is.EqualTo(2));
                Assert.That(
                    runnerPlan.Value<string>("snapshotFingerprint"),
                    Is.EqualTo(context.hierarchySnapshotFingerprint));
                Assert.That(
                    runnerPlan["moves"][0]["source"].Value<string>(),
                    Is.EqualTo("node:" + source.Value<string>("id")));
                Assert.That(
                    runnerPlan["moves"][0]["destination"].Value<string>(),
                    Is.EqualTo("node:" + parent.Value<string>("id")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void ApprovedPlanRejectsUnknownNonEmptyOperationArray()
        {
            const string target = "Assets/UI/Prefab/ExampleView.prefab";
            var plan = JObject.Parse(CreatePlan(target, true));
            plan["futureComponentExtractions"] = new JArray
            {
                new JObject { ["id"] = "unsupported" },
            };

            bool extracted = PsdHierarchyChatCleanupExecution.TryExtractApprovedPlan(
                "```json\n" + plan + "\n```",
                target,
                out _,
                out string error);

            Assert.That(extracted, Is.False);
            Assert.That(error, Does.Contain("futureComponentExtractions").And.Contain("Unsupported"));
        }

        private static PsdHierarchyChatContext CreateFlatSiblingContext()
        {
            const string snapshot =
                "{\"schemaVersion\":1,\"prefabAssetPath\":\"Assets/UI/Prefab/ExampleView.prefab\"," +
                "\"fingerprint\":\"snapshot-123\",\"nodes\":[" +
                "{\"id\":\"n000001\",\"path\":\"Root\",\"parentId\":\"\",\"siblingIndex\":0}," +
                "{\"id\":\"n000002\",\"path\":\"Root/ActionButtonPrimary\",\"parentId\":\"n000001\",\"siblingIndex\":0}," +
                "{\"id\":\"n000003\",\"path\":\"Root/TimerLabel\",\"parentId\":\"n000001\",\"siblingIndex\":1}," +
                "{\"id\":\"n000004\",\"path\":\"Root/DurationLabel\",\"parentId\":\"n000001\",\"siblingIndex\":2}," +
                "{\"id\":\"n000005\",\"path\":\"Root/ActionButtonSecondary\",\"parentId\":\"n000001\",\"siblingIndex\":3}," +
                "{\"id\":\"n000006\",\"path\":\"Root/[BottomBar]\",\"parentId\":\"n000001\",\"siblingIndex\":4}," +
                "{\"id\":\"n000007\",\"path\":\"Root/[BottomBar]/Detail\",\"parentId\":\"n000006\",\"siblingIndex\":0}]," +
                "\"flatSiblingFindings\":[{\"id\":\"flat_sibling_001\",\"parent\":\"node:n000001\"," +
                "\"background\":\"node:n000002\",\"members\":[\"node:n000002\",\"node:n000003\",\"node:n000004\",\"node:n000005\"]}]}";
            return new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                "Plan Format",
                snapshot,
                "snapshot-123",
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/snapshot-123.json");
        }

        private static PsdHierarchyChatContext CreateNestedFlatSiblingContext()
        {
            const string snapshot =
                "{\"schemaVersion\":1,\"prefabAssetPath\":\"Assets/UI/Prefab/ExampleView.prefab\"," +
                "\"fingerprint\":\"snapshot-123\",\"nodes\":[" +
                "{\"id\":\"n000001\",\"path\":\"Root\",\"parentId\":\"\",\"siblingIndex\":0}," +
                "{\"id\":\"n000002\",\"path\":\"Root/Outer\",\"parentId\":\"n000001\",\"siblingIndex\":0}," +
                "{\"id\":\"n000003\",\"path\":\"Root/Outer/Group\",\"parentId\":\"n000002\",\"siblingIndex\":0}," +
                "{\"id\":\"n000004\",\"path\":\"Root/Outer/Group/Background\",\"parentId\":\"n000003\",\"siblingIndex\":0}," +
                "{\"id\":\"n000005\",\"path\":\"Root/Outer/Group/Label\",\"parentId\":\"n000003\",\"siblingIndex\":1}," +
                "{\"id\":\"n000006\",\"path\":\"Root/Outer/Group/Icon\",\"parentId\":\"n000003\",\"siblingIndex\":2}," +
                "{\"id\":\"n000007\",\"path\":\"Root/LegacyEmpty\",\"parentId\":\"n000001\",\"siblingIndex\":1}]," +
                "\"flatSiblingFindings\":[{\"id\":\"flat_sibling_001\",\"parent\":\"node:n000003\"," +
                "\"background\":\"node:n000004\",\"members\":[\"node:n000004\",\"node:n000005\",\"node:n000006\"]}]}";
            return new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                "Plan Format",
                snapshot,
                "snapshot-123",
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/snapshot-123.json");
        }

        private static PsdHierarchyChatContext CreateMultipleFlatSiblingContext()
        {
            const string snapshot =
                "{\"schemaVersion\":1,\"prefabAssetPath\":\"Assets/UI/Prefab/ExampleView.prefab\"," +
                "\"fingerprint\":\"snapshot-123\",\"nodes\":[" +
                "{\"id\":\"n000001\",\"path\":\"Root\",\"parentId\":\"\",\"siblingIndex\":0}," +
                "{\"id\":\"n000002\",\"path\":\"Root/ActionButtonPrimary\",\"parentId\":\"n000001\",\"siblingIndex\":0}," +
                "{\"id\":\"n000003\",\"path\":\"Root/TimerLabel\",\"parentId\":\"n000001\",\"siblingIndex\":1}," +
                "{\"id\":\"n000004\",\"path\":\"Root/DurationLabel\",\"parentId\":\"n000001\",\"siblingIndex\":2}," +
                "{\"id\":\"n000005\",\"path\":\"Root/ActionButtonSecondary\",\"parentId\":\"n000001\",\"siblingIndex\":3}," +
                "{\"id\":\"n000006\",\"path\":\"Root/SecondBackground\",\"parentId\":\"n000001\",\"siblingIndex\":4}," +
                "{\"id\":\"n000007\",\"path\":\"Root/SecondLabel\",\"parentId\":\"n000001\",\"siblingIndex\":5}]," +
                "\"flatSiblingFindings\":[" +
                "{\"id\":\"flat_sibling_001\",\"parent\":\"node:n000001\",\"background\":\"node:n000002\",\"members\":[\"node:n000002\",\"node:n000003\",\"node:n000004\"]}," +
                "{\"id\":\"flat_sibling_002\",\"parent\":\"node:n000001\",\"background\":\"node:n000005\",\"members\":[\"node:n000005\",\"node:n000006\",\"node:n000007\"]}]}";
            return new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                "Plan Format",
                snapshot,
                "snapshot-123",
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/snapshot-123.json");
        }

        private static string CreateFlatSiblingGroupPlan(string wrapperParent)
        {
            var plan = JObject.Parse(
                CreateNodeReferencePlan("node:n000002", "node:n000001", "snapshot-123"));
            plan["wrappers"] = new JArray
            {
                new JObject
                {
                    ["id"] = "legacy_wrapper",
                    ["parent"] = wrapperParent,
                    ["name"] = "[LegacyGroup]",
                    ["siblingIndex"] = 5,
                },
            };
            plan["moves"] = new JArray
            {
                new JObject { ["source"] = "node:n000002", ["destination"] = "@legacy_wrapper", ["siblingIndex"] = 0 },
                new JObject { ["source"] = "node:n000003", ["destination"] = "@legacy_wrapper", ["siblingIndex"] = 1 },
                new JObject { ["source"] = "node:n000004", ["destination"] = "@legacy_wrapper", ["siblingIndex"] = 2 },
                new JObject { ["source"] = "node:n000005", ["destination"] = "@legacy_wrapper", ["siblingIndex"] = 3 },
            };
            plan["tightBounds"] = new JArray
            {
                new JObject { ["target"] = "@legacy_wrapper" },
            };
            plan["flatSiblingResolutions"] = new JArray
            {
                new JObject
                {
                    ["findingId"] = "flat_sibling_001",
                    ["mode"] = "group",
                    ["wrapperId"] = "legacy_wrapper",
                },
            };
            return plan.ToString();
        }

        private static PsdHierarchyChatContext CreateNodeSnapshotContext()
        {
            const string snapshot =
                "{\"schemaVersion\":1,\"prefabAssetPath\":\"Assets/UI/Prefab/ExampleView.prefab\"," +
                "\"fingerprint\":\"snapshot-123\",\"nodes\":[" +
                "{\"id\":\"n000001\",\"path\":\"Root/Group\"}," +
                "{\"id\":\"n000002\",\"path\":\"Root/Group/15K\"}]}";
            return new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                "Plan Format",
                snapshot,
                "snapshot-123",
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/snapshot-123.json");
        }

        private static PsdHierarchyChatContext CreateAssetRenameContext(string[] assetRenameSourcePaths)
        {
            const string snapshot =
                "{\"schemaVersion\":1,\"prefabAssetPath\":\"Assets/UI/Prefab/ExampleView.prefab\"," +
                "\"fingerprint\":\"snapshot-123\",\"nodes\":[" +
                "{\"id\":\"n000001\",\"path\":\"Root/Group\"}," +
                "{\"id\":\"n000002\",\"path\":\"Root/Group/15K\"}]}";
            return new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                "Plan Format",
                snapshot,
                "snapshot-123",
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/snapshot-123.json",
                assetRenameSourcePaths);
        }

        private static PsdHierarchyChatContext CreateRequiredCandidateContext(
            string recommendedMode = null,
            string suggestedAssetName = "TaskItem")
        {
            string recommendedModeJson = string.IsNullOrWhiteSpace(recommendedMode)
                ? string.Empty
                : "\"recommendedMode\":\"" + recommendedMode + "\",";
            string snapshot =
                "{\"schemaVersion\":1,\"prefabAssetPath\":\"Assets/UI/Prefab/ExampleView.prefab\"," +
                "\"fingerprint\":\"snapshot-123\",\"nodes\":[" +
                "{\"id\":\"n000001\",\"path\":\"Root\"}," +
                "{\"id\":\"n000002\",\"path\":\"Root/TaskList\"}," +
                "{\"id\":\"n000003\",\"path\":\"Root/TaskList/[TaskItem_1]\"}," +
                "{\"id\":\"n000004\",\"path\":\"Root/TaskList/[TaskItem_2]\"}," +
                "{\"id\":\"n000005\",\"path\":\"Root/TaskList/[TaskItem_3]\"}]," +
                "\"componentFamilyCandidates\":[{" +
                "\"id\":\"family_001\",\"suggestedAssetName\":\"" + suggestedAssetName + "\"," +
                recommendedModeJson +
                "\"parent\":\"node:n000002\",\"sources\":[\"node:n000003\",\"node:n000004\",\"node:n000005\"]," +
                "\"requiresExtraction\":true}]}";
            return new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                "Plan Format",
                snapshot,
                "snapshot-123",
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/snapshot-123.json");
        }

        private static PsdHierarchyChatContext CreateDistinctVariantCandidateContext(
            string recommendedMode = "variant",
            string suggestedAssetName = "TaskItem")
        {
            string snapshot =
                "{\"schemaVersion\":1,\"prefabAssetPath\":\"Assets/UI/Prefab/ExampleView.prefab\"," +
                "\"fingerprint\":\"snapshot-123\",\"nodes\":[" +
                "{\"id\":\"n000001\",\"path\":\"Root\",\"name\":\"Root\",\"parentId\":\"\",\"siblingIndex\":0,\"components\":[\"RectTransform\"]}," +
                "{\"id\":\"n000002\",\"path\":\"Root/TaskList\",\"name\":\"TaskList\",\"parentId\":\"n000001\",\"siblingIndex\":0,\"components\":[\"RectTransform\"]}," +
                "{\"id\":\"n000003\",\"path\":\"Root/TaskList/[TaskItem_1]\",\"name\":\"[TaskItem_1]\",\"parentId\":\"n000002\",\"siblingIndex\":0,\"components\":[\"RectTransform\"]}," +
                "{\"id\":\"n000004\",\"path\":\"Root/TaskList/[TaskItem_2]\",\"name\":\"[TaskItem_2]\",\"parentId\":\"n000002\",\"siblingIndex\":1,\"components\":[\"RectTransform\"]}," +
                "{\"id\":\"n000005\",\"path\":\"Root/TaskList/[TaskItem_3]\",\"name\":\"[TaskItem_3]\",\"parentId\":\"n000002\",\"siblingIndex\":2,\"components\":[\"RectTransform\"]}," +
                "{\"id\":\"n000006\",\"path\":\"Root/TaskList/[TaskItem_1]/Background\",\"name\":\"Background\",\"parentId\":\"n000003\",\"siblingIndex\":0,\"components\":[\"RectTransform\",\"Image\"]}," +
                "{\"id\":\"n000007\",\"path\":\"Root/TaskList/[TaskItem_2]/Content\",\"name\":\"Content\",\"parentId\":\"n000004\",\"siblingIndex\":0,\"components\":[\"RectTransform\",\"Text\"]}," +
                "{\"id\":\"n000008\",\"path\":\"Root/TaskList/[TaskItem_3]/LockIcon\",\"name\":\"LockIcon\",\"parentId\":\"n000005\",\"siblingIndex\":0,\"components\":[\"RectTransform\",\"Image\"]}," +
                "{\"id\":\"n000009\",\"path\":\"Root/TaskList/[TaskItem_3]/LockIcon/Overlay\",\"name\":\"Overlay\",\"parentId\":\"n000008\",\"siblingIndex\":0,\"components\":[\"RectTransform\"]}]," +
                "\"componentFamilyCandidates\":[{\"id\":\"family_001\",\"suggestedAssetName\":\"" + suggestedAssetName + "\"," +
                "\"recommendedMode\":\"" + recommendedMode + "\",\"parent\":\"node:n000002\"," +
                "\"sources\":[\"node:n000003\",\"node:n000004\",\"node:n000005\"],\"requiresExtraction\":true}]}";
            return new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                "Plan Format",
                snapshot,
                "snapshot-123",
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/snapshot-123.json");
        }

        private static PsdHierarchyChatContext CreateInvalidVariantInstanceNameContext()
        {
            const string snapshot =
                "{\"schemaVersion\":1,\"prefabAssetPath\":\"Assets/UI/Prefab/ExampleView.prefab\"," +
                "\"fingerprint\":\"snapshot-123\",\"nodes\":[" +
                "{\"id\":\"n000001\",\"path\":\"Root\",\"name\":\"Root\",\"parentId\":\"\",\"siblingIndex\":0,\"components\":[\"RectTransform\"]}," +
                "{\"id\":\"n000002\",\"path\":\"Root/TaskList\",\"name\":\"TaskList\",\"parentId\":\"n000001\",\"siblingIndex\":0,\"components\":[\"RectTransform\"]}," +
                "{\"id\":\"n000003\",\"path\":\"Root/TaskList/[TaskRow_4]\",\"name\":\"[TaskRow_4]\",\"parentId\":\"n000002\",\"siblingIndex\":0,\"components\":[\"RectTransform\"]}," +
                "{\"id\":\"n000004\",\"path\":\"Root/TaskList/[TaskRow_3]\",\"name\":\"[TaskRow_3]\",\"parentId\":\"n000002\",\"siblingIndex\":1,\"components\":[\"RectTransform\"]}," +
                "{\"id\":\"n000005\",\"path\":\"Root/TaskList/[TaskRow_1]\",\"name\":\"[TaskRow_1]\",\"parentId\":\"n000002\",\"siblingIndex\":2,\"components\":[\"RectTransform\"]}," +
                "{\"id\":\"n000006\",\"path\":\"Root/TaskList/[TaskRow_2]\",\"name\":\"[TaskRow_2]\",\"parentId\":\"n000002\",\"siblingIndex\":3,\"components\":[\"RectTransform\"]}," +
                "{\"id\":\"n000007\",\"path\":\"Root/TaskList/1\",\"name\":\"1\",\"parentId\":\"n000002\",\"siblingIndex\":4,\"components\":[\"RectTransform\"]}," +
                "{\"id\":\"n000008\",\"path\":\"Root/TaskList/[TaskRow_4]/Background\",\"name\":\"Background\",\"parentId\":\"n000003\",\"siblingIndex\":0,\"components\":[\"RectTransform\",\"Image\"]}," +
                "{\"id\":\"n000009\",\"path\":\"Root/TaskList/[TaskRow_3]/Content\",\"name\":\"Content\",\"parentId\":\"n000004\",\"siblingIndex\":0,\"components\":[\"RectTransform\",\"Text\"]}," +
                "{\"id\":\"n000010\",\"path\":\"Root/TaskList/[TaskRow_1]/LockIcon\",\"name\":\"LockIcon\",\"parentId\":\"n000005\",\"siblingIndex\":0,\"components\":[\"RectTransform\",\"Image\"]}," +
                "{\"id\":\"n000011\",\"path\":\"Root/TaskList/[TaskRow_1]/LockIcon/Overlay\",\"name\":\"Overlay\",\"parentId\":\"n000010\",\"siblingIndex\":0,\"components\":[\"RectTransform\"]}," +
                "{\"id\":\"n000012\",\"path\":\"Root/TaskList/[TaskRow_2]/Progress\",\"name\":\"Progress\",\"parentId\":\"n000006\",\"siblingIndex\":0,\"components\":[\"RectTransform\",\"Text\"]}," +
                "{\"id\":\"n000013\",\"path\":\"Root/TaskList/[TaskRow_2]/Progress/Value\",\"name\":\"Value\",\"parentId\":\"n000012\",\"siblingIndex\":0,\"components\":[\"RectTransform\"]}," +
                "{\"id\":\"n000014\",\"path\":\"Root/TaskList/1/Reward\",\"name\":\"Reward\",\"parentId\":\"n000007\",\"siblingIndex\":0,\"components\":[\"RectTransform\",\"Image\"]}," +
                "{\"id\":\"n000015\",\"path\":\"Root/TaskList/1/Reward/Amount\",\"name\":\"Amount\",\"parentId\":\"n000014\",\"siblingIndex\":0,\"components\":[\"RectTransform\"]}," +
                "{\"id\":\"n000016\",\"path\":\"Root/TaskList/1/Reward/Amount/Label\",\"name\":\"Label\",\"parentId\":\"n000015\",\"siblingIndex\":0,\"components\":[\"RectTransform\"]}]," +
                "\"componentFamilyCandidates\":[{\"id\":\"family_001\",\"suggestedAssetName\":\"TaskRow\"," +
                "\"recommendedMode\":\"variant\",\"parent\":\"node:n000002\"," +
                "\"sources\":[\"node:n000003\",\"node:n000004\",\"node:n000005\",\"node:n000006\",\"node:n000007\"],\"requiresExtraction\":true}]}";
            return new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                "Plan Format",
                snapshot,
                "snapshot-123",
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/snapshot-123.json");
        }

        private static PsdHierarchyChatContext CreateStatefulSnapshotContext()
        {
            const string snapshot =
                "{\"schemaVersion\":1,\"prefabAssetPath\":\"Assets/UI/Prefab/ExampleView.prefab\"," +
                "\"fingerprint\":\"snapshot-123\",\"nodes\":[" +
                "{\"id\":\"n000001\",\"path\":\"Root/Cards\",\"name\":\"Cards\",\"parentId\":\"\",\"siblingIndex\":0}," +
                "{\"id\":\"n000002\",\"path\":\"Root/Cards/[DayCard_1]\",\"name\":\"[DayCard_1]\",\"parentId\":\"n000001\",\"siblingIndex\":0}," +
                "{\"id\":\"n000003\",\"path\":\"Root/Cards/[DayCard_1]/Background\",\"name\":\"Background\",\"parentId\":\"n000002\",\"siblingIndex\":0}," +
                "{\"id\":\"n000004\",\"path\":\"Root/Cards/[DayCard_1]/DayLabel\",\"name\":\"DayLabel\",\"parentId\":\"n000002\",\"siblingIndex\":1}," +
                "{\"id\":\"n000005\",\"path\":\"Root/Cards/[DayCard_1]/Lock\",\"name\":\"Lock\",\"parentId\":\"n000002\",\"siblingIndex\":2}," +
                "{\"id\":\"n000006\",\"path\":\"Root/Cards/[DayCard_2]\",\"name\":\"[DayCard_2]\",\"parentId\":\"n000001\",\"siblingIndex\":1}," +
                "{\"id\":\"n000007\",\"path\":\"Root/Cards/[DayCard_2]/AvailableBackground\",\"name\":\"AvailableBackground\",\"parentId\":\"n000006\",\"siblingIndex\":0}," +
                "{\"id\":\"n000008\",\"path\":\"Root/Cards/[DayCard_2]/AvailableLabel\",\"name\":\"AvailableLabel\",\"parentId\":\"n000006\",\"siblingIndex\":1}]}";
            return new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                "Plan Format",
                snapshot,
                "snapshot-123",
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/snapshot-123.json");
        }

        [Test]
        public async System.Threading.Tasks.Task ContextFreeSelectedPrefabExtractionIsRejectedWithoutWrites()
        {
            const string folder = "Assets/__PsdHierarchySelectedPrefabExtractionTests";
            const string targetPath = folder + "/ExampleView.prefab";
            const string componentPath = folder + "/Common/DaySignCard.prefab";
            AssetDatabase.DeleteAsset(folder);
            AssetDatabase.CreateFolder("Assets", "__PsdHierarchySelectedPrefabExtractionTests");

            GameObject root = new GameObject("ExampleView", typeof(RectTransform));
            GameObject loaded = null;
            try
            {
                foreach (string name in new[] { "GiftBox4", "DateText4", "DateMarker5" })
                {
                    var child = new GameObject(name, typeof(RectTransform));
                    child.transform.SetParent(root.transform, false);
                }

                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, targetPath), Is.Not.Null);
                string plan = CreateSelectedPrefabExtractionPlan(targetPath, componentPath);

                // 无上下文的 v1 路径计划不能通过任何保留入口写入：重放入口在写入前永久拒绝。
                PsdHierarchyChatCleanupExecutionResult result =
                    await PsdHierarchyChatCleanupExecution.ReapplyPersistedPlanAsync(
                        System.IO.Directory.GetParent(Application.dataPath).FullName,
                        plan);
                Assert.That(result.success, Is.False);
                Assert.That(
                    result.message,
                    Does.Contain(PsdHierarchyChatCleanupExecution.ReplayRequiresFreshAnalysisMessage));
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(componentPath), Is.Null);

                loaded = PrefabUtility.LoadPrefabContents(targetPath);
                Assert.That(
                    loaded.transform.Cast<Transform>().Select(child => child.name),
                    Is.EqualTo(new[] { "GiftBox4", "DateText4", "DateMarker5" }));
            }
            finally
            {
                if (loaded != null)
                {
                    PrefabUtility.UnloadPrefabContents(loaded);
                }

                UnityEngine.Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public async System.Threading.Tasks.Task ContextFreeCrossParentPrefabExtractionIsRejectedWithoutWrites()
        {
            const string folder = "Assets/__PsdHierarchyCrossParentExtractionTests";
            const string targetPath = folder + "/ExampleView.prefab";
            const string componentPath = folder + "/Common/DaySignRewardItem.prefab";
            AssetDatabase.DeleteAsset(folder);
            AssetDatabase.CreateFolder("Assets", "__PsdHierarchyCrossParentExtractionTests");

            GameObject root = new GameObject("ExampleView", typeof(RectTransform));
            GameObject loaded = null;
            try
            {
                Transform screen = CreateRectTransform("Screen", root.transform, Vector2.zero);
                Transform reward = CreateRectTransform("Reward", screen, Vector2.zero);
                Transform progress = CreateRectTransform("Progress", screen, Vector2.zero);
                for (int index = 1; index <= 4; index++)
                {
                    CreateImageRectTransform("GiftBox" + index, reward, new Vector2(index * 100f, -42f), index);
                    CreateImageRectTransform("DateText" + index, progress, new Vector2(index * 100f, 2f), index + 10);
                }

                for (int index = 1; index <= 5; index++)
                {
                    CreateImageRectTransform("DateMarker" + index, progress, new Vector2((index - 1) * 100f, 0f), index + 20);
                }

                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, targetPath), Is.Not.Null);
                string plan = CreateCrossParentPrefabExtractionPlan(targetPath, componentPath);

                // 无上下文的 v1 路径计划不能通过任何保留入口写入：重放入口在写入前永久拒绝。
                PsdHierarchyChatCleanupExecutionResult result =
                    await PsdHierarchyChatCleanupExecution.ReapplyPersistedPlanAsync(
                        System.IO.Directory.GetParent(Application.dataPath).FullName,
                        plan);
                Assert.That(result.success, Is.False);
                Assert.That(
                    result.message,
                    Does.Contain(PsdHierarchyChatCleanupExecution.ReplayRequiresFreshAnalysisMessage));
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(componentPath), Is.Null);

                loaded = PrefabUtility.LoadPrefabContents(targetPath);
                Transform loadedScreen = loaded.transform.Find("Screen");
                Assert.That(loadedScreen.Find("Progress/DateMarker1"), Is.Not.Null);
                Assert.That(loadedScreen.Find("Reward/GiftBox4"), Is.Not.Null);
                Assert.That(loadedScreen.Find("Progress/DateText4"), Is.Not.Null);
            }
            finally
            {
                if (loaded != null)
                {
                    PrefabUtility.UnloadPrefabContents(loaded);
                }

                UnityEngine.Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static Transform CreateRectTransform(string name, Transform parent, Vector2 position)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            ((RectTransform)node.transform).anchoredPosition = position;
            return node.transform;
        }

        private static Transform CreateImageRectTransform(string name, Transform parent, Vector2 position, int colorSeed)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Image));
            node.transform.SetParent(parent, false);
            ((RectTransform)node.transform).anchoredPosition = position;
            node.GetComponent<Image>().color = ColorFor(colorSeed);
            return node.transform;
        }

        private static Color ColorFor(int seed)
        {
            return new Color((seed % 5) / 4f, (seed % 7) / 6f, (seed % 11) / 10f, 1f);
        }

        private static string CreateCrossParentPrefabExtractionPlan(string targetPath, string componentPath)
        {
            return "{\"version\":1," +
                   "\"prefabAssetPath\":\"" + targetPath + "\"," +
                   "\"output\":{\"mode\":\"in_place\",\"assetPath\":\"" + targetPath + "\"}," +
                   "\"prefabName\":\"ExampleView\",\"wrappers\":[],\"moves\":[],\"renames\":[]," +
                   "\"emptyContainerRemovals\":[],\"tightBounds\":[],\"textureRenames\":[]," +
                   "\"spriteAtlasRenames\":[],\"componentFamilyDecisions\":[],\"componentExtractions\":[]," +
                   "\"stateComponentExtractions\":[],\"variantComponentExtractions\":[]," +
                   "\"statefulComponentExtractions\":[],\"crossParentPrefabExtractions\":[{" +
                   "\"id\":\"day_sign_reward_item\",\"name\":\"DaySignRewardItem\",\"assetPath\":\"" + componentPath + "\"," +
                   "\"root\":\"ExampleView/Screen\",\"templateSources\":[\"ExampleView/Screen/Progress/DateMarker5\",\"ExampleView/Screen/Progress/DateText4\",\"ExampleView/Screen/Reward/GiftBox4\"]," +
                   "\"instances\":[" +
                   "{\"sequence\":1,\"sources\":[\"ExampleView/Screen/Progress/DateMarker2\",\"ExampleView/Screen/Progress/DateText1\",\"ExampleView/Screen/Reward/GiftBox1\"]}," +
                   "{\"sequence\":2,\"sources\":[\"ExampleView/Screen/Progress/DateMarker3\",\"ExampleView/Screen/Progress/DateText2\",\"ExampleView/Screen/Reward/GiftBox2\"]}," +
                   "{\"sequence\":3,\"sources\":[\"ExampleView/Screen/Progress/DateMarker4\",\"ExampleView/Screen/Progress/DateText3\",\"ExampleView/Screen/Reward/GiftBox3\"]}," +
                   "{\"sequence\":4,\"sources\":[\"ExampleView/Screen/Progress/DateMarker5\",\"ExampleView/Screen/Progress/DateText4\",\"ExampleView/Screen/Reward/GiftBox4\"]}]," +
                   "\"unmatched\":[\"ExampleView/Screen/Progress/DateMarker1\"]}],\"verify\":{}}";
        }

        private static string CreateNodeReferencePlan(string source, string destination, string fingerprint)
        {
            return "{\"version\":2," +
                   "\"snapshotFingerprint\":\"" + fingerprint + "\"," +
                   "\"prefabAssetPath\":\"Assets/UI/Prefab/ExampleView.prefab\"," +
                   "\"output\":{\"mode\":\"in_place\",\"assetPath\":\"Assets/UI/Prefab/ExampleView.prefab\"}," +
                   "\"prefabName\":\"ExampleView\",\"wrappers\":[]," +
                   "\"moves\":[{\"source\":\"" + source + "\",\"destination\":\"" + destination + "\",\"siblingIndex\":0}]," +
                   "\"renames\":[],\"emptyContainerRemovals\":[],\"tightBounds\":[]," +
                   "\"textureRenames\":[],\"spriteAtlasRenames\":[]," +
                   "\"containmentResolutions\":[],\"flatSiblingResolutions\":[]," +
                   "\"componentFamilyDecisions\":[],\"componentExtractions\":[]," +
                   "\"stateComponentExtractions\":[],\"variantComponentExtractions\":[]," +
                   "\"statefulComponentExtractions\":[],\"postGroupingExtractionIntents\":[],\"verify\":{}}";
        }

        private static string CreateSelectedPrefabExtractionPlan(string targetPath, string componentPath)
        {
            return "{\"version\":1," +
                   "\"prefabAssetPath\":\"" + targetPath + "\"," +
                   "\"output\":{\"mode\":\"in_place\",\"assetPath\":\"" + targetPath + "\"}," +
                   "\"prefabName\":\"ExampleView\",\"wrappers\":[],\"moves\":[],\"renames\":[]," +
                   "\"emptyContainerRemovals\":[],\"tightBounds\":[],\"textureRenames\":[]," +
                   "\"spriteAtlasRenames\":[],\"componentFamilyDecisions\":[],\"componentExtractions\":[]," +
                   "\"stateComponentExtractions\":[],\"variantComponentExtractions\":[]," +
                   "\"statefulComponentExtractions\":[],\"selectedPrefabExtractions\":[{" +
                   "\"id\":\"day_sign_card\",\"name\":\"DaySignCard\",\"assetPath\":\"" + componentPath + "\"," +
                   "\"parent\":\"ExampleView\",\"sources\":[\"ExampleView/GiftBox4\",\"ExampleView/DateText4\",\"ExampleView/DateMarker5\"]}]," +
                   "\"verify\":{}}";
        }

        private static string CreatePlan(string target, bool includeVersion)
        {
            string version = includeVersion ? "\"version\": 1," : string.Empty;
            return "{" + version +
                   "\"prefabAssetPath\":\"" + target + "\"," +
                   "\"output\":{\"mode\":\"in_place\",\"assetPath\":\"" + target + "\"}," +
                   "\"prefabName\":\"ExampleView\"," +
                   "\"wrappers\":[],\"moves\":[],\"renames\":[],\"emptyContainerRemovals\":[]," +
                   "\"tightBounds\":[],\"textureRenames\":[],\"spriteAtlasRenames\":[]," +
                   "\"containmentResolutions\":[],\"flatSiblingResolutions\":[]," +
                   "\"componentFamilyDecisions\":[],\"componentExtractions\":[]," +
                   "\"stateComponentExtractions\":[],\"variantComponentExtractions\":[]," +
                   "\"statefulComponentExtractions\":[],\"postGroupingExtractionIntents\":[],\"verify\":{}}";
        }

        private static void AssertPreparedPlanIsUnchanged(PsdHierarchyChatContext context, JObject reviewedPlan)
        {
            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                context,
                reviewedPlan.ToString(),
                out string executionPlanJson,
                out string error);

            Assert.That(prepared, Is.True, error);
            Assert.That(
                JToken.DeepEquals(JObject.Parse(executionPlanJson), reviewedPlan),
                Is.True,
                "Formal v2 preparation must not add, remove, or rewrite reviewed operations.");
        }
    }
}
