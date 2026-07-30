namespace PsdLayoutTool2.Tests
{
    using System.Linq;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using UnityEditor;

    public sealed class PsdHierarchyChatCleanupExecutionTests
    {
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
        public void RunnerPlanCapturesCurrentUnityGuidInsteadOfTrustingAiAssetRenameGuid()
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

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                context,
                plan.ToString(),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.True, error);
            string expectedGuid = AssetDatabase.AssetPathToGUID(assetPath);
            Assert.That(expectedGuid, Is.Not.Empty);
            Assert.That(
                JObject.Parse(runnerPlanJson)["textureRenames"]?[0]?["expectedGuid"]?.Value<string>(),
                Is.EqualTo(expectedGuid));
        }

        [Test]
        public void RunnerPlanRejectsMissingAssetRenameSourceBeforeExternalPreflight()
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

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                context,
                plan.ToString(),
                out _,
                out string error);

            Assert.That(prepared, Is.False);
            Assert.That(error, Does.Contain("textureRenames[0].from"));
            Assert.That(error, Does.Contain(missingAssetPath));
        }

        [Test]
        public void RunnerPlanDerivesPrefabNameFromReviewedAssetRenameTargets()
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

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                context,
                plan.ToString(),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.True, error);
            Assert.That(
                JObject.Parse(runnerPlanJson).Value<string>("prefabName"),
                Is.EqualTo("SevenDayTaskView"));
        }

        [Test]
        public void RunnerPlanRejectsConflictingPrefabNamesFromReviewedAssetRenameTargets()
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

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                context,
                plan.ToString(),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.False);
            Assert.That(runnerPlanJson, Is.Empty);
            Assert.That(
                error,
                Is.EqualTo(
                    "prefabName derivation failed before runner preflight: " +
                    "reviewed asset rename targets produced conflicting candidates; " +
                    "submittedPrefabName=7日任务拆分; " +
                    "candidates=OtherView, SevenDayTaskView; " +
                    "reviewedTargets=" +
                    "textureRenames[0].toName=SevenDayTaskView_OuterBackgroundFrame; " +
                    "textureRenames[1].toName=OtherView_OuterBackgroundFrame; " +
                    "required=one PascalCase name ending with View."));
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
        public void VersionTwoNodeReferencesResolveToTheExactSnapshotPaths()
        {
            PsdHierarchyChatContext context = CreateNodeSnapshotContext();

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                context,
                CreateNodeReferencePlan("node:n000002", "node:n000001", "snapshot-123"),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.True, error);
            var runnerPlan = JObject.Parse(runnerPlanJson);
            Assert.That(runnerPlan["version"].Value<int>(), Is.EqualTo(1));
            Assert.That(runnerPlan["snapshotFingerprint"], Is.Null);
            Assert.That(runnerPlan["moves"][0]["source"].Value<string>(), Is.EqualTo("Root/Group/15K"));
            Assert.That(runnerPlan["moves"][0]["destination"].Value<string>(), Is.EqualTo("Root/Group"));
        }

        [Test]
        public void VersionTwoPlanStablyDeduplicatesDirectChildVerificationNames()
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

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                CreateNodeSnapshotContext(),
                plan.ToString(),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.True, error);
            var runnerPlan = JObject.Parse(runnerPlanJson);
            Assert.That(
                runnerPlan["verify"]["directChildren"][0]["children"].Values<string>(),
                Is.EqualTo(new[] { "ItemFrame", "ItemIcon", "ItemLabel" }));
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
        public void VersionTwoPlanRejectsOmittedMandatoryComponentFamily()
        {
            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                CreateRequiredCandidateContext(),
                CreateNodeReferencePlan("node:n000003", "node:n000002", "snapshot-123"),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.False);
            Assert.That(runnerPlanJson, Is.Empty);
            Assert.That(error, Does.Contain("family_001").And.Contain("TaskItem"));
        }

        [Test]
        public void VersionTwoPlanRejectsSkipForMandatoryComponentFamily()
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

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                CreateRequiredCandidateContext(),
                plan.ToString(),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.False);
            Assert.That(runnerPlanJson, Is.Empty);
            Assert.That(error, Does.Contain("不能使用 skip"));
        }

        [Test]
        public void VersionTwoPlanDeterministicallyRepairsSkipForMandatoryVariantFamily()
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

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                CreateDistinctVariantCandidateContext(),
                plan.ToString(),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.True, error);
            var runnerPlan = JObject.Parse(runnerPlanJson);
            JObject decision = (JObject)runnerPlan["componentFamilyDecisions"][0];
            Assert.That(decision.Value<string>("mode"), Is.EqualTo("variant"));
            Assert.That(decision.Value<string>("extractionId"), Is.EqualTo("task_item_variant"));
            JObject extraction = (JObject)runnerPlan["variantComponentExtractions"][0];
            Assert.That(extraction.Value<string>("template"), Is.EqualTo("Root/TaskList/[TaskItem_1]"));
            Assert.That(extraction.Value<string>("assetPath"), Does.EndWith("/Common/TaskItemVariant.prefab"));
            Assert.That(((JArray)extraction["states"]).Count, Is.EqualTo(3));
            Assert.That(((JArray)extraction["instances"]).Count, Is.EqualTo(3));
        }

        [Test]
        public void VersionTwoPlanDerivesUniqueBracketedNameForInvalidVariantInstance()
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

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                CreateInvalidVariantInstanceNameContext(),
                plan.ToString(),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.True, error);
            var runnerPlan = JObject.Parse(runnerPlanJson);
            var extraction = (JObject)runnerPlan["variantComponentExtractions"][0];
            Assert.That(
                ((JArray)extraction["instances"])
                .OfType<JObject>()
                .Select(instance => instance.Value<string>("name")),
                Is.EqualTo(new[]
                {
                    "[TaskRow_4]",
                    "[TaskRow_3]",
                    "[TaskRow_1]",
                    "[TaskRow_2]",
                    "[TaskRow_5]",
                }));
        }

        [Test]
        public void ContextPlanExtractionReturnsTheDeterministicallyRepairedVariantJson()
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
            var approvedPlan = JObject.Parse(approvedPlanJson);
            Assert.That(approvedPlan.Value<long>("version"), Is.EqualTo(2));
            Assert.That(
                approvedPlan["componentFamilyDecisions"][0].Value<string>("mode"),
                Is.EqualTo("variant"));
            Assert.That(((JArray)approvedPlan["variantComponentExtractions"]).Count, Is.EqualTo(1));
        }

        [Test]
        public void VersionTwoPlanAcceptsMandatoryExtractionAndStripsCandidateMetadataForTheRunner()
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
            Assert.That(runnerPlan["componentFamilyDecisions"][0]["candidateId"], Is.Null);
            Assert.That(
                runnerPlan["componentExtractions"][0]["template"].Value<string>(),
                Is.EqualTo("Root/TaskList/[TaskItem_1]"));
        }

        [Test]
        public void VersionTwoPlanDowngradesSingleStateVariantToComponentExtraction()
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

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                CreateRequiredCandidateContext(),
                plan.ToString(),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.True, error);
            var runnerPlan = JObject.Parse(runnerPlanJson);
            Assert.That(runnerPlan["variantComponentExtractions"]?.Count(), Is.EqualTo(0));
            Assert.That(runnerPlan["componentFamilyDecisions"][0]["mode"].Value<string>(), Is.EqualTo("component"));
            Assert.That(runnerPlan["componentExtractions"][0]["id"].Value<string>(), Is.EqualTo("task_item"));
            Assert.That(
                runnerPlan["componentExtractions"][0]["instances"].Values<string>().ToArray(),
                Is.EqualTo(new[]
                {
                    "Root/TaskList/[TaskItem_1]",
                    "Root/TaskList/[TaskItem_2]",
                    "Root/TaskList/[TaskItem_3]",
                }));
        }

        [Test]
        public void VersionTwoPlanReportsDetailedErrorWhenRequiredVariantCannotBeReconstructed()
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

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                CreateRequiredCandidateContext("variant"),
                plan.ToString(),
                out _,
                out string error);

            Assert.That(prepared, Is.False);
            Assert.That(error, Does.Contain("candidateId=family_001"));
            Assert.That(error, Does.Contain("asset=TaskItem"));
            Assert.That(error, Does.Contain("sources=node:n000003,node:n000004,node:n000005"));
            Assert.That(
                error,
                Does.Contain("reason=recommendedMode=variant but fewer than two distinct recursive structures were observed"));
        }

        [Test]
        public void VersionTwoPlanCompletesMissingCommonAndSelectedStateMembersFromTheSnapshot()
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

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                CreateStatefulSnapshotContext(),
                plan.ToString(),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.True, error);
            var runnerPlan = JObject.Parse(runnerPlanJson);
            Assert.That(
                runnerPlan["statefulComponentExtractions"][0]["instances"][0]["stateSourceNames"]
                    .Values<string>(),
                Is.EqualTo(new[] { "Background", "LockIcon" }));
            Assert.That(
                runnerPlan["statefulComponentExtractions"][0]["instances"][0]["commonSourceNames"]
                    .Values<string>(),
                Is.EqualTo(new[] { "DayText" }));
            Assert.That(
                runnerPlan["statefulComponentExtractions"][0]["instances"][1]["stateSourceNames"]
                    .Values<string>(),
                Is.EqualTo(new[] { "AvailableBackground" }));
            Assert.That(
                runnerPlan["statefulComponentExtractions"][0]["common"]["members"][0]["sourceName"]
                    .Value<string>(),
                Is.EqualTo("DayText"));
            Assert.That(
                runnerPlan["statefulComponentExtractions"][0]["states"][0]["members"][1]["sourceName"]
                    .Value<string>(),
                Is.EqualTo("LockIcon"));
        }

        [Test]
        public void VersionTwoPlanRejectsEmptyStatefulCommonBeforeRunnerPreflight()
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

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                CreateStatefulSnapshotContext(),
                plan.ToString(),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.False);
            Assert.That(runnerPlanJson, Is.Empty);
            Assert.That(error, Does.Contain("common.members").And.Contain("must not be empty"));
        }

        [Test]
        public void ActualSnapshotNodeIdsResolveToObservedPrefabPaths()
        {
            const string sourcePsd = "Assets/PSDLayoutTool2/TestData/跳格子切图.psd";
            const string targetPrefab =
                "Assets/PSDLayoutTool2/TestData/跳格子切图/Prefab/跳格子切图.prefab";
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
            plan["prefabName"] = "JumpGridView";
            plan["moves"][0]["siblingIndex"] = source.Value<int>("siblingIndex");

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                context,
                plan.ToString(),
                out string runnerPlanJson,
                out string error);

            Assert.That(prepared, Is.True, error);
            var runnerPlan = JObject.Parse(runnerPlanJson);
            Assert.That(
                runnerPlan["moves"][0]["source"].Value<string>(),
                Is.EqualTo(source.Value<string>("path")));
            Assert.That(
                runnerPlan["moves"][0]["destination"].Value<string>(),
                Is.EqualTo(parent.Value<string>("path")));
        }

        [Test]
        public void NativeBackendAcceptsAHierarchyOnlyPlan()
        {
            Assert.That(
                PsdHierarchyNativeCleanupExecutor.TryValidatePlanCapabilities(
                    CreatePlan("Assets/UI/Prefab/ExampleView.prefab", true),
                    out string error),
                Is.True,
                error);
        }

        [Test]
        public void NativeBackendAcceptsNodeCountVerification()
        {
            var plan = JObject.Parse(CreatePlan("Assets/UI/Prefab/ExampleView.prefab", true));
            plan["verify"] = new JObject
            {
                ["nodes"] = 3,
            };

            Assert.That(
                PsdHierarchyNativeCleanupExecutor.TryValidatePlanCapabilities(plan.ToString(), out string error),
                Is.True,
                error);
        }

        [Test]
        public void NativeBackendAcceptsEmptyContainerRemovalPlans()
        {
            var plan = JObject.Parse(CreatePlan("Assets/UI/Prefab/ExampleView.prefab", true));
            plan["emptyContainerRemovals"] = new JArray
            {
                new JObject { ["source"] = "Root/LegacyGroup" },
            };

            Assert.That(
                PsdHierarchyNativeCleanupExecutor.TryValidatePlanCapabilities(plan.ToString(), out string error),
                Is.True,
                error);
        }

        [Test]
        public void NativeBackendAcceptsNonBlockingVerificationFields()
        {
            var plan = JObject.Parse(CreatePlan("Assets/UI/Prefab/ExampleView.prefab", true));
            plan["verify"] = new JObject
            {
                ["requireEnglishNames"] = true,
            };

            Assert.That(
                PsdHierarchyNativeCleanupExecutor.TryValidatePlanCapabilities(plan.ToString(), out string error),
                Is.True,
                error);
        }

        [Test]
        public void NativeBackendRejectsComponentExtractionBeforePrefabWrites()
        {
            var plan = JObject.Parse(CreatePlan("Assets/UI/Prefab/ExampleView.prefab", true));
            plan["componentExtractions"] = new JArray
            {
                new JObject { ["id"] = "item" },
            };

            Assert.That(
                PsdHierarchyNativeCleanupExecutor.TryValidatePlanCapabilities(plan.ToString(), out string error),
                Is.False);
            Assert.That(error, Does.Contain("componentExtractions").And.Contain("uLoop"));
        }

        [Test]
        public void NativeBackendRequestsUloopOnlyForPlansWithComplexOperations()
        {
            var hierarchyOnlyPlan = JObject.Parse(CreatePlan("Assets/UI/Prefab/ExampleView.prefab", true));

            Assert.That(
                PsdHierarchyNativeCleanupExecutor.RequiresUloopRunner(hierarchyOnlyPlan.ToString()),
                Is.False);

            hierarchyOnlyPlan["variantComponentExtractions"] = new JArray
            {
                new JObject { ["id"] = "task_item" },
            };

            Assert.That(
                PsdHierarchyNativeCleanupExecutor.RequiresUloopRunner(hierarchyOnlyPlan.ToString()),
                Is.True);
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

        private static PsdHierarchyChatContext CreateRequiredCandidateContext(string recommendedMode = null)
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
                "\"id\":\"family_001\",\"suggestedAssetName\":\"TaskItem\"," +
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

        private static PsdHierarchyChatContext CreateDistinctVariantCandidateContext()
        {
            const string snapshot =
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
                "\"componentFamilyCandidates\":[{\"id\":\"family_001\",\"suggestedAssetName\":\"TaskItem\"," +
                "\"recommendedMode\":\"variant\",\"parent\":\"node:n000002\"," +
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
                   "\"componentFamilyDecisions\":[],\"componentExtractions\":[]," +
                   "\"stateComponentExtractions\":[],\"variantComponentExtractions\":[]," +
                   "\"statefulComponentExtractions\":[],\"verify\":{}}";
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
                   "\"componentFamilyDecisions\":[],\"componentExtractions\":[]," +
                   "\"stateComponentExtractions\":[],\"variantComponentExtractions\":[]," +
                   "\"statefulComponentExtractions\":[],\"verify\":{}}";
        }
    }
}
