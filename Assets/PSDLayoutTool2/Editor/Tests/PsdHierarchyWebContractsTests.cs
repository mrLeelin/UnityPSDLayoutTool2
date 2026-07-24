namespace PsdLayoutTool2.Tests
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using PsdLayoutTool2.Editor;

    public sealed class PsdHierarchyWebContractsTests
    {
        [Test]
        public void Snapshot_RoundTripsNodesGroupsWarningsAndPrefabCandidates()
        {
            var source = new PsdHierarchyWebSnapshotDto
            {
                canvas = new PsdHierarchyWebBoundsDto { width = 1080f, height = 2340f },
                nodes = new List<PsdHierarchyWebNodeDto>
                {
                    new PsdHierarchyWebNodeDto
                    {
                        stableId = "layer:17",
                        name = "任务标题",
                        bounds = new PsdHierarchyWebBoundsDto { x = 12f, y = 24f, width = 320f, height = 64f },
                        proposedGroupKey = "daily-list",
                        isLocked = true
                    }
                },
                groups = new List<PsdHierarchyWebGroupDto>
                {
                    new PsdHierarchyWebGroupDto
                    {
                        key = "daily-list",
                        displayName = "每日任务",
                        memberStableIds = new List<string> { "layer:17" },
                        isAccepted = true
                    }
                },
                warnings = new List<PsdHierarchyWebWarningDto>
                {
                    new PsdHierarchyWebWarningDto
                    {
                        code = "protected-boundary",
                        message = "不能跨越保护边界",
                        stableIds = new List<string> { "layer:17" }
                    }
                },
                prefabCandidates = new List<PsdHierarchyWebPrefabCandidateDto>
                {
                    new PsdHierarchyWebPrefabCandidateDto
                    {
                        candidateId = "candidate:daily-card",
                        proposedName = "DailyTaskCard",
                        representativeStableId = "layer:17",
                        instanceStableIds = new List<string> { "layer:17", "layer:18" }
                    }
                }
            };

            string json = JsonConvert.SerializeObject(source);
            PsdHierarchyWebSnapshotDto result = JsonConvert.DeserializeObject<PsdHierarchyWebSnapshotDto>(json);

            Assert.That(json, Does.Contain("\"stableId\""));
            Assert.That(json, Does.Not.Contain("\"StableId\""));
            Assert.That(result.canvas.width, Is.EqualTo(1080f));
            Assert.That(result.nodes[0].name, Is.EqualTo("任务标题"));
            Assert.That(result.nodes[0].isLocked, Is.True);
            CollectionAssert.AreEqual(new[] { "layer:17" }, result.groups[0].memberStableIds);
            Assert.That(result.groups[0].isAccepted, Is.True);
            Assert.That(result.warnings[0].message, Is.EqualTo("不能跨越保护边界"));
            CollectionAssert.AreEqual(
                new[] { "layer:17", "layer:18" },
                result.prefabCandidates[0].instanceStableIds);
        }

        [Test]
        public void RefineRequest_RoundTripsStableIdsAndInstruction()
        {
            var source = new PsdHierarchyWebRefineRequest
            {
                stableIds = new List<string> { "layer:17", "layer:18" },
                instruction = "这两个任务属于同一个列表项"
            };

            string json = JsonConvert.SerializeObject(source);
            PsdHierarchyWebRefineRequest result =
                JsonConvert.DeserializeObject<PsdHierarchyWebRefineRequest>(json);

            CollectionAssert.AreEqual(source.stableIds, result.stableIds);
            Assert.That(result.instruction, Is.EqualTo("这两个任务属于同一个列表项"));
        }

        [Test]
        public void OperationState_DefaultsToIdle()
        {
            var state = new PsdHierarchyWebOperationState();

            Assert.That(state.operationId, Is.Empty);
            Assert.That(state.kind, Is.EqualTo(PsdHierarchyWebOperationKind.None));
            Assert.That(state.status, Is.EqualTo(PsdHierarchyWebOperationStatus.Idle));
            Assert.That(state.message, Is.Empty);
        }

        [Test]
        public void OperationEnums_UseStableLowerCamelStringTokens()
        {
            AssertEnumToken(PsdHierarchyWebOperationKind.None, "none");
            AssertEnumToken(PsdHierarchyWebOperationKind.Analyze, "analyze");
            AssertEnumToken(PsdHierarchyWebOperationKind.Refine, "refine");
            AssertEnumToken(PsdHierarchyWebOperationKind.Apply, "apply");
            AssertEnumToken(PsdHierarchyWebOperationKind.CreatePrefabs, "createPrefabs");

            AssertEnumToken(PsdHierarchyWebOperationStatus.Idle, "idle");
            AssertEnumToken(PsdHierarchyWebOperationStatus.Running, "running");
            AssertEnumToken(PsdHierarchyWebOperationStatus.Succeeded, "succeeded");
            AssertEnumToken(PsdHierarchyWebOperationStatus.Failed, "failed");
        }

        [Test]
        public void OperationState_HasExactProtocolShape()
        {
            string json = JsonConvert.SerializeObject(new PsdHierarchyWebOperationState());

            Assert.That(
                json,
                Is.EqualTo("{\"operationId\":\"\",\"kind\":\"none\",\"status\":\"idle\",\"message\":\"\"}"));
        }

        [Test]
        public void Contracts_DefaultToEmptySerializableValues()
        {
            var session = new PsdHierarchyWebSessionDto();
            var snapshot = new PsdHierarchyWebSnapshotDto();
            var node = new PsdHierarchyWebNodeDto();
            var group = new PsdHierarchyWebGroupDto();
            var warning = new PsdHierarchyWebWarningDto();
            var candidate = new PsdHierarchyWebPrefabCandidateDto();
            var accept = new PsdHierarchyWebAcceptRequest();
            var apply = new PsdHierarchyWebApplyRequest();
            var create = new PsdHierarchyWebCreatePrefabsRequest();

            Assert.That(session.sessionId, Is.Empty);
            Assert.That(session.operation, Is.Not.Null);
            Assert.That(snapshot.canvas, Is.Not.Null);
            Assert.That(snapshot.nodes, Is.Empty);
            Assert.That(snapshot.groups, Is.Empty);
            Assert.That(snapshot.warnings, Is.Empty);
            Assert.That(snapshot.prefabCandidates, Is.Empty);
            Assert.That(node.bounds, Is.Not.Null);
            Assert.That(group.memberStableIds, Is.Empty);
            Assert.That(warning.stableIds, Is.Empty);
            Assert.That(candidate.instanceStableIds, Is.Empty);
            Assert.That(candidate.instanceControlledDifferences, Is.Empty);
            Assert.That(accept.groupKeys, Is.Empty);
            Assert.That(apply.confirmed, Is.False);
            Assert.That(create.candidateIds, Is.Empty);
        }

        private static void AssertEnumToken<T>(T value, string expectedToken)
        {
            Assert.That(JsonConvert.SerializeObject(value), Is.EqualTo("\"" + expectedToken + "\""));
        }
    }
}
