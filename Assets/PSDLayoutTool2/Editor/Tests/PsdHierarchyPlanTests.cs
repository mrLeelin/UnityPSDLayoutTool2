namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using UnityEngine;

    public sealed class PsdHierarchyPlanTests
    {
        [Test]
        public void ValidStrictPlanParsesAndValidates()
        {
            PsdHierarchyRequest request = Request(Node("101", 0), Node("102", 1));
            string json = PlanJson(
                "\"groups\":[{\"key\":\"header\",\"parentKey\":\"\",\"memberStableIds\":[\"101\",\"102\"],\"displayName\":\"Header\",\"evidence\":\"Adjacent header art\",\"confidence\":0.9}]," +
                "\"renames\":[{\"stableId\":\"101\",\"name\":\"Title Icon\",\"evidence\":\"PSD name\",\"confidence\":0.8}]",
                request.sourceFingerprint);

            PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(json);
            PsdHierarchyPlanValidator.Validate(plan, request);

            Assert.That(plan.groups[0].key, Is.EqualTo("header"));
            Assert.That(plan.renames[0].stableId, Is.EqualTo("101"));
        }

        [TestCase("\"command\":\"rm -rf Assets\"")]
        [TestCase("\"code\":\"Debug.Log(1)\"")]
        [TestCase("\"delete\":[\"101\"]")]
        [TestCase("\"material\":\"Assets/Hack.mat\"")]
        [TestCase("\"unityProperties\":{}")]
        public void AuthorityBearingTopLevelFieldsAreRejected(string forbiddenProperty)
        {
            string json = "{\"schemaVersion\":1,\"sourceFingerprint\":\"source-v1\",\"groups\":[],\"renames\":[]," + forbiddenProperty + "}";

            Assert.Throws<PsdHierarchyPlanFormatException>(() => PsdHierarchyPlanJson.Parse(json));
        }

        [Test]
        public void UnknownNestedFieldIsRejected()
        {
            string json = PlanJson(
                "\"groups\":[{\"key\":\"header\",\"parentKey\":\"\",\"memberStableIds\":[\"101\"],\"displayName\":\"Header\",\"evidence\":\"x\",\"confidence\":0.9,\"surprise\":true}],\"renames\":[]",
                "source-v1");

            Assert.Throws<PsdHierarchyPlanFormatException>(() => PsdHierarchyPlanJson.Parse(json));
        }

        [Test]
        public void DuplicateJsonPropertiesAreRejectedBeforeDeserialization()
        {
            string json = "{\"schemaVersion\":1,\"schemaVersion\":1,\"sourceFingerprint\":\"source-v1\",\"groups\":[],\"renames\":[]}";

            Assert.Throws<PsdHierarchyPlanFormatException>(() => PsdHierarchyPlanJson.Parse(json));
        }

        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("-Infinity")]
        public void NonFiniteConfidenceIsRejected(string value)
        {
            string json = PlanJson(
                "\"groups\":[{\"key\":\"header\",\"parentKey\":\"\",\"memberStableIds\":[\"101\"],\"displayName\":\"Header\",\"evidence\":\"x\",\"confidence\":" + value + "}],\"renames\":[]",
                "source-v1");

            Assert.Throws<PsdHierarchyPlanFormatException>(() => PsdHierarchyPlanJson.Parse(json));
        }

        [Test]
        public void UnsupportedSchemaIsRejected()
        {
            string json = "{\"schemaVersion\":2,\"sourceFingerprint\":\"source-v1\",\"groups\":[],\"renames\":[]}";

            Assert.Throws<PsdHierarchyPlanFormatException>(() => PsdHierarchyPlanJson.Parse(json));
        }

        [TestCase("999")]
        [TestCase("fallback_deadbeef")]
        public void UnknownOrUnstableMemberIdsAreRejected(string memberId)
        {
            PsdHierarchyRequest request = Request(Node("101", 0));
            PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":[{\"key\":\"header\",\"parentKey\":\"\",\"memberStableIds\":[\"" + memberId + "\"],\"displayName\":\"Header\",\"evidence\":\"x\",\"confidence\":0.9}],\"renames\":[]",
                request.sourceFingerprint));

            Assert.Throws<PsdHierarchyPlanValidationException>(() => PsdHierarchyPlanValidator.Validate(plan, request));
        }

        [Test]
        public void DuplicateMemberWithinAGroupIsRejected()
        {
            PsdHierarchyRequest request = Request(Node("101", 0));
            PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":[{\"key\":\"header\",\"parentKey\":\"\",\"memberStableIds\":[\"101\",\"101\"],\"displayName\":\"Header\",\"evidence\":\"x\",\"confidence\":0.9}],\"renames\":[]",
                request.sourceFingerprint));

            Assert.Throws<PsdHierarchyPlanValidationException>(() => PsdHierarchyPlanValidator.Validate(plan, request));
        }

        [Test]
        public void MemberAssignedToMultipleGroupsIsRejected()
        {
            PsdHierarchyRequest request = Request(Node("101", 0));
            PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":[" + Group("a", "", "101") + "," + Group("b", "", "101") + "],\"renames\":[]",
                request.sourceFingerprint));

            Assert.Throws<PsdHierarchyPlanValidationException>(() => PsdHierarchyPlanValidator.Validate(plan, request));
        }

        [Test]
        public void GroupParentCycleIsRejected()
        {
            PsdHierarchyRequest request = Request(Node("101", 0), Node("102", 1));
            PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":[" + Group("a", "b", "101") + "," + Group("b", "a", "102") + "],\"renames\":[]",
                request.sourceFingerprint));

            Assert.Throws<PsdHierarchyPlanValidationException>(() => PsdHierarchyPlanValidator.Validate(plan, request));
        }

        [Test]
        public void ProtectedBoundaryCrossingIsRejected()
        {
            PsdHierarchyRequest request = Request(
                Node("101", 0, "", "boundary-a"),
                Node("102", 1, "", "boundary-b"));
            PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":[[GROUP]],\"renames\":[]".Replace("[GROUP]", Group("mixed", "", "101", "102")),
                request.sourceFingerprint));

            Assert.Throws<PsdHierarchyPlanValidationException>(() => PsdHierarchyPlanValidator.Validate(plan, request));
        }

        [Test]
        public void NonContiguousSiblingMoveIsRejected()
        {
            PsdHierarchyRequest request = Request(Node("101", 0), Node("102", 1), Node("103", 2));
            PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":[" + Group("skip-middle", "", "101", "103") + "],\"renames\":[]",
                request.sourceFingerprint));

            Assert.Throws<PsdHierarchyPlanValidationException>(() => PsdHierarchyPlanValidator.Validate(plan, request));
        }

        [Test]
        public void UnknownOrDuplicateRenameIdsAreRejected()
        {
            PsdHierarchyRequest request = Request(Node("101", 0));
            PsdHierarchyPlan unknown = PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":[],\"renames\":[{\"stableId\":\"999\",\"name\":\"X\",\"evidence\":\"x\",\"confidence\":0.5}]",
                request.sourceFingerprint));
            PsdHierarchyPlan duplicate = PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":[],\"renames\":[{\"stableId\":\"101\",\"name\":\"X\",\"evidence\":\"x\",\"confidence\":0.5},{\"stableId\":\"101\",\"name\":\"Y\",\"evidence\":\"y\",\"confidence\":0.6}]",
                request.sourceFingerprint));

            Assert.Throws<PsdHierarchyPlanValidationException>(() => PsdHierarchyPlanValidator.Validate(unknown, request));
            Assert.Throws<PsdHierarchyPlanValidationException>(() => PsdHierarchyPlanValidator.Validate(duplicate, request));
        }

        [Test]
        public void ContextBuilderExportsOnlyBoundedMetadata()
        {
            var document = new PsdPrefabDocumentModel
            {
                sourceFingerprint = "source-v1",
                width = 100,
                height = 200,
                nodes = new List<PsdPrefabNodeModel>
                {
                    new PsdPrefabNodeModel
                    {
                        stableId = "101",
                        parentStableId = string.Empty,
                        siblingIndex = 0,
                        name = "Icon",
                        kind = PsdPrefabNodeKind.Image,
                        bounds = new Rect(1, 2, 30, 40),
                        assetFingerprint = "pixel-derived-fingerprint"
                    }
                }
            };
            var prefab = new[]
            {
                new PsdHierarchyPrefabNodeMetadata
                {
                    stableId = "101",
                    parentStableId = string.Empty,
                    siblingIndex = 0,
                    hierarchyPath = "Root/Icon",
                    componentTypes = new List<string> { "RectTransform", "Image" },
                    protectedBoundaryStableId = ""
                }
            };

            PsdHierarchyRequest request = PsdHierarchyContextBuilder.Build(document, prefab);
            string json = PsdHierarchyPlanJson.SerializeRequest(request);

            Assert.That(request.nodes[0].rectangle.width, Is.EqualTo(30));
            Assert.That(request.currentPrefabHierarchy[0].componentTypes, Is.EqualTo(new[] { "RectTransform", "Image" }));
            Assert.That(json, Does.Not.Contain("pixel-derived-fingerprint"));
            Assert.That(json, Does.Not.Contain("texture"));
            Assert.That(json, Does.Not.Contain("command"));
            Assert.That(json, Does.Not.Contain("write"));
        }

        private static PsdHierarchyRequest Request(params PsdHierarchyRequestNode[] nodes)
        {
            return new PsdHierarchyRequest
            {
                schemaVersion = PsdHierarchyRequest.CurrentSchemaVersion,
                sourceFingerprint = "source-v1",
                nodes = new List<PsdHierarchyRequestNode>(nodes),
                currentPrefabHierarchy = new List<PsdHierarchyPrefabNodeMetadata>()
            };
        }

        private static PsdHierarchyRequestNode Node(string id, int siblingIndex, string parentId = "", string boundaryId = "")
        {
            return new PsdHierarchyRequestNode
            {
                stableId = id,
                originalName = "Node " + id,
                kind = PsdPrefabNodeKind.Image.ToString(),
                parentStableId = parentId,
                siblingIndex = siblingIndex,
                protectedBoundaryStableId = boundaryId
            };
        }

        private static string PlanJson(string body, string fingerprint)
        {
            return "{\"schemaVersion\":1,\"sourceFingerprint\":" + JsonConvert.ToString(fingerprint) + "," + body + "}";
        }

        private static string Group(string key, string parentKey, params string[] ids)
        {
            return "{\"key\":" + JsonConvert.ToString(key) +
                   ",\"parentKey\":" + JsonConvert.ToString(parentKey) +
                   ",\"memberStableIds\":" + JsonConvert.SerializeObject(ids) +
                   ",\"displayName\":\"Group\",\"evidence\":\"x\",\"confidence\":0.9}";
        }
    }
}
