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
        public void NestedGroupClosureCannotCrossProtectedBoundaries()
        {
            PsdHierarchyRequest request = Request(
                Node("101", 0, "", "boundary-a"),
                Node("102", 1, "", "boundary-b"));
            PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":[" + Group("parent", "", "101") + "," + Group("child", "parent", "102") + "],\"renames\":[]",
                request.sourceFingerprint));

            Assert.Throws<PsdHierarchyPlanValidationException>(() => PsdHierarchyPlanValidator.Validate(plan, request));
        }

        [Test]
        public void NestedGroupClosureCannotCombineDifferentCurrentParents()
        {
            PsdHierarchyRequest request = Request(Node("101", 0, "201"), Node("102", 1, "202"));
            PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":[" + Group("parent", "", "101") + "," + Group("child", "parent", "102") + "],\"renames\":[]",
                request.sourceFingerprint));

            Assert.Throws<PsdHierarchyPlanValidationException>(() => PsdHierarchyPlanValidator.Validate(plan, request));
        }

        [Test]
        public void NestedGroupClosureMustRemainContiguous()
        {
            PsdHierarchyRequest request = Request(Node("101", 0), Node("102", 1), Node("103", 2));
            PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":[" + Group("parent", "", "101") + "," + Group("child", "parent", "103") + "],\"renames\":[]",
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

        [Test]
        public void ContextBuilderRejectsDuplicatePrefabStableIdsEvenWhenProtectionDiffers()
        {
            PsdPrefabDocumentModel document = DocumentModel(1);
            var prefab = new[]
            {
                PrefabNode("101", false, false),
                PrefabNode("101", true, false)
            };

            Assert.Throws<ArgumentException>(() => PsdHierarchyContextBuilder.Build(document, prefab));
        }

        [Test]
        public void ContextBuilderRejectsDuplicatePrefabStableIdsEvenWhenProjectComponentsDiffer()
        {
            PsdPrefabDocumentModel document = DocumentModel(1);
            var prefab = new[]
            {
                PrefabNode("101", false, false),
                PrefabNode("101", false, true)
            };

            Assert.Throws<ArgumentException>(() => PsdHierarchyContextBuilder.Build(document, prefab));
        }

        [Test]
        public void ValidatorUsesPrefabMetadataAsAuthoritativeProtectionFacts()
        {
            PsdHierarchyRequest request = Request(Node("101", 0));
            request.currentPrefabHierarchy.Add(PrefabNode("101", false, true));
            PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":[" + Group("project-owned", "", "101") + "],\"renames\":[]",
                request.sourceFingerprint));

            Assert.Throws<PsdHierarchyPlanValidationException>(() => PsdHierarchyPlanValidator.Validate(plan, request));
        }

        [Test]
        public void ValidatorRejectsDuplicatePrefabMetadataBeforeUsingProtectionFacts()
        {
            PsdHierarchyRequest request = Request(Node("101", 0));
            request.currentPrefabHierarchy.Add(PrefabNode("101", false, false));
            request.currentPrefabHierarchy.Add(PrefabNode("101", true, true));
            PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(PlanJson("\"groups\":[],\"renames\":[]", request.sourceFingerprint));

            Assert.Throws<PsdHierarchyPlanValidationException>(() => PsdHierarchyPlanValidator.Validate(plan, request));
        }

        [Test]
        public void ContentOnlyFingerprintDifferenceDoesNotInvalidateHierarchyPlan()
        {
            PsdHierarchyRequest request = Request(Node("101", 0));
            request.contentFingerprint = "content-new";
            PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(PlanJson("\"groups\":[],\"renames\":[]", request.sourceFingerprint));
            plan.contentFingerprint = "content-old";

            Assert.That(PsdHierarchyPlanValidator.EvaluateFingerprints(plan, request),
                Is.EqualTo(PsdHierarchyPlanFingerprintStatus.Valid));
            Assert.DoesNotThrow(() => PsdHierarchyPlanValidator.Validate(plan, request));
        }

        [Test]
        public void StructureFingerprintDifferenceRequiresReplan()
        {
            PsdHierarchyRequest request = Request(Node("101", 0));
            PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(PlanJson("\"groups\":[],\"renames\":[]", request.sourceFingerprint));
            plan.structureFingerprint = "structure-old";

            Assert.That(PsdHierarchyPlanValidator.EvaluateFingerprints(plan, request),
                Is.EqualTo(PsdHierarchyPlanFingerprintStatus.RequiresReplan));
            Assert.Throws<PsdHierarchyPlanValidationException>(() => PsdHierarchyPlanValidator.Validate(plan, request));
        }

        [Test]
        public void GeometryFingerprintDifferenceRequiresValidationAndCannotDirectlyApply()
        {
            PsdHierarchyRequest request = Request(Node("101", 0));
            PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(PlanJson("\"groups\":[],\"renames\":[]", request.sourceFingerprint));
            plan.geometryFingerprint = "geometry-old";

            Assert.That(PsdHierarchyPlanValidator.EvaluateFingerprints(plan, request),
                Is.EqualTo(PsdHierarchyPlanFingerprintStatus.RequiresGeometryValidation));
            Assert.Throws<PsdHierarchyPlanValidationException>(() => PsdHierarchyPlanValidator.Validate(plan, request));
        }

        [Test]
        public void JsonCharacterQuotaAcceptsLimitAndRejectsLimitPlusOne()
        {
            string core = PlanJson("\"groups\":[],\"renames\":[]", "source-v1");
            string atLimit = core + new string(' ', PsdHierarchyContractLimits.MaxJsonCharacters - core.Length);

            Assert.DoesNotThrow(() => PsdHierarchyPlanJson.Parse(atLimit));
            Assert.Throws<PsdHierarchyPlanFormatException>(() => PsdHierarchyPlanJson.Parse(atLimit + " "));
        }

        [Test]
        public void JsonByteQuotaRejectsOversizedUtf8BeforeParsing()
        {
            string oversized = new string('中', (PsdHierarchyContractLimits.MaxJsonUtf8Bytes / 3) + 1);

            Assert.Throws<PsdHierarchyPlanFormatException>(() => PsdHierarchyPlanJson.Parse(oversized));
        }

        [Test]
        public void PlanCollectionQuotasAcceptLimitsAndRejectNextItems()
        {
            string groupsAtLimit = JsonConvert.SerializeObject(BuildGroups(PsdHierarchyContractLimits.MaxGroups, 1));
            string renamesAtLimit = JsonConvert.SerializeObject(BuildRenames(PsdHierarchyContractLimits.MaxRenames));
            Assert.DoesNotThrow(() => PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":" + groupsAtLimit + ",\"renames\":" + renamesAtLimit, "source-v1")));

            Assert.Throws<PsdHierarchyPlanFormatException>(() => PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":" + JsonConvert.SerializeObject(BuildGroups(PsdHierarchyContractLimits.MaxGroups + 1, 1)) + ",\"renames\":[]", "source-v1")));
            Assert.Throws<PsdHierarchyPlanFormatException>(() => PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":[],\"renames\":" + JsonConvert.SerializeObject(BuildRenames(PsdHierarchyContractLimits.MaxRenames + 1)), "source-v1")));
        }

        [Test]
        public void MembershipQuotasAcceptLimitsAndRejectNextMembers()
        {
            Assert.DoesNotThrow(() => PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":" + JsonConvert.SerializeObject(BuildGroups(1, PsdHierarchyContractLimits.MaxMembersPerGroup)) + ",\"renames\":[]", "source-v1")));
            Assert.Throws<PsdHierarchyPlanFormatException>(() => PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":" + JsonConvert.SerializeObject(BuildGroups(1, PsdHierarchyContractLimits.MaxMembersPerGroup + 1)) + ",\"renames\":[]", "source-v1")));

            int groupCount = PsdHierarchyContractLimits.MaxTotalMemberships / PsdHierarchyContractLimits.MaxMembersPerGroup;
            Assert.DoesNotThrow(() => PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":" + JsonConvert.SerializeObject(BuildGroups(groupCount, PsdHierarchyContractLimits.MaxMembersPerGroup)) + ",\"renames\":[]", "source-v1")));
            Assert.Throws<PsdHierarchyPlanFormatException>(() => PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":" + JsonConvert.SerializeObject(BuildGroups(groupCount + 1, PsdHierarchyContractLimits.MaxMembersPerGroup)) + ",\"renames\":[]", "source-v1")));
        }

        [Test]
        public void PlanStringQuotaAcceptsLimitAndRejectsLimitPlusOne()
        {
            List<Dictionary<string, object>> groups = BuildGroups(1, 1);
            groups[0]["evidence"] = new string('e', PsdHierarchyContractLimits.MaxEvidenceLength);
            Assert.DoesNotThrow(() => PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":" + JsonConvert.SerializeObject(groups) + ",\"renames\":[]", "source-v1")));

            groups[0]["evidence"] = new string('e', PsdHierarchyContractLimits.MaxEvidenceLength + 1);
            Assert.Throws<PsdHierarchyPlanFormatException>(() => PsdHierarchyPlanJson.Parse(PlanJson(
                "\"groups\":" + JsonConvert.SerializeObject(groups) + ",\"renames\":[]", "source-v1")));
        }

        [Test]
        public void ContextNodeAndPrefabQuotasAcceptLimitsAndRejectNextItems()
        {
            Assert.DoesNotThrow(() => PsdHierarchyContextBuilder.Build(
                DocumentModel(PsdHierarchyContractLimits.MaxContextNodes), new PsdHierarchyPrefabNodeMetadata[0]));
            Assert.Throws<ArgumentException>(() => PsdHierarchyContextBuilder.Build(
                DocumentModel(PsdHierarchyContractLimits.MaxContextNodes + 1), new PsdHierarchyPrefabNodeMetadata[0]));

            PsdPrefabDocumentModel empty = DocumentModel(0);
            Assert.DoesNotThrow(() => PsdHierarchyContextBuilder.Build(empty, BuildPrefabNodes(PsdHierarchyContractLimits.MaxPrefabMetadataNodes)));
            Assert.Throws<ArgumentException>(() => PsdHierarchyContextBuilder.Build(empty, BuildPrefabNodes(PsdHierarchyContractLimits.MaxPrefabMetadataNodes + 1)));
        }

        [Test]
        public void ContextComponentPreviewAndPathQuotasAcceptLimitsAndRejectNextItems()
        {
            PsdHierarchyPrefabNodeMetadata prefab = PrefabNode("101", false, false);
            prefab.hierarchyPath = new string('p', PsdHierarchyContractLimits.MaxHierarchyPathLength);
            prefab.componentTypes = EnumerableStrings(PsdHierarchyContractLimits.MaxComponentTypesPerNode, "C");
            Assert.DoesNotThrow(() => PsdHierarchyContextBuilder.Build(DocumentModel(1), new[] { prefab },
                BuildPreviews(PsdHierarchyContractLimits.MaxPreviews)));

            prefab.hierarchyPath += "p";
            Assert.Throws<ArgumentException>(() => PsdHierarchyContextBuilder.Build(DocumentModel(1), new[] { prefab }));
            prefab.hierarchyPath = "Root/Node";
            prefab.componentTypes.Add("Overflow");
            Assert.Throws<ArgumentException>(() => PsdHierarchyContextBuilder.Build(DocumentModel(1), new[] { prefab }));
            Assert.Throws<ArgumentException>(() => PsdHierarchyContextBuilder.Build(DocumentModel(1), new[] { PrefabNode("101", false, false) },
                BuildPreviews(PsdHierarchyContractLimits.MaxPreviews + 1)));
        }

        [Test]
        public void FullValidatorRejectsProgrammaticPlanThatBypassesJsonQuotas()
        {
            PsdHierarchyRequest request = Request(Node("101", 0));
            PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(PlanJson("\"groups\":[],\"renames\":[]", request.sourceFingerprint));
            for (int index = 0; index <= PsdHierarchyContractLimits.MaxGroups; index++)
            {
                plan.groups.Add(new PsdHierarchyPlanGroup
                {
                    key = "group-" + index,
                    memberStableIds = new List<string> { "101" }
                });
            }

            PsdHierarchyPlanValidationException exception = Assert.Throws<PsdHierarchyPlanValidationException>(
                () => PsdHierarchyPlanValidator.Validate(plan, request));
            Assert.That(exception.Message, Does.Contain("group limit"));
        }

        [Test]
        public void FullValidatorRejectsProgrammaticRequestThatBypassesContextQuotas()
        {
            PsdHierarchyRequest request = Request(Node("101", 0));
            request.currentPrefabHierarchy = BuildPrefabNodes(PsdHierarchyContractLimits.MaxPrefabMetadataNodes + 1);
            PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(PlanJson("\"groups\":[],\"renames\":[]", request.sourceFingerprint));

            Assert.Throws<PsdHierarchyPlanValidationException>(() => PsdHierarchyPlanValidator.Validate(plan, request));
        }

        private static PsdHierarchyRequest Request(params PsdHierarchyRequestNode[] nodes)
        {
            return new PsdHierarchyRequest
            {
                schemaVersion = PsdHierarchyRequest.CurrentSchemaVersion,
                sourceFingerprint = "source-v1",
                contentFingerprint = "content-v1",
                structureFingerprint = "structure-v1",
                geometryFingerprint = "geometry-v1",
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
            return "{\"schemaVersion\":1,\"sourceFingerprint\":" + JsonConvert.ToString(fingerprint) +
                   ",\"contentFingerprint\":\"content-v1\",\"structureFingerprint\":\"structure-v1\",\"geometryFingerprint\":\"geometry-v1\"," + body + "}";
        }

        private static string Group(string key, string parentKey, params string[] ids)
        {
            return "{\"key\":" + JsonConvert.ToString(key) +
                   ",\"parentKey\":" + JsonConvert.ToString(parentKey) +
                   ",\"memberStableIds\":" + JsonConvert.SerializeObject(ids) +
                   ",\"displayName\":\"Group\",\"evidence\":\"x\",\"confidence\":0.9}";
        }

        private static PsdPrefabDocumentModel DocumentModel(int nodeCount)
        {
            var document = new PsdPrefabDocumentModel { sourceFingerprint = "source-v1", width = 100, height = 100 };
            for (int index = 0; index < nodeCount; index++)
            {
                document.nodes.Add(new PsdPrefabNodeModel
                {
                    stableId = (index + 101).ToString(),
                    parentStableId = string.Empty,
                    siblingIndex = index,
                    name = "Node",
                    kind = PsdPrefabNodeKind.Image,
                    bounds = Rect.zero
                });
            }

            return document;
        }

        private static PsdHierarchyPrefabNodeMetadata PrefabNode(string id, bool protectedBoundary, bool projectComponents)
        {
            return new PsdHierarchyPrefabNodeMetadata
            {
                stableId = id,
                hierarchyPath = "Root/Node",
                componentTypes = new List<string> { "RectTransform" },
                isProtectedBoundary = protectedBoundary,
                hasProjectComponents = projectComponents,
                protectedBoundaryStableId = protectedBoundary ? id : string.Empty
            };
        }

        private static List<PsdHierarchyPrefabNodeMetadata> BuildPrefabNodes(int count)
        {
            var result = new List<PsdHierarchyPrefabNodeMetadata>(count);
            for (int index = 0; index < count; index++)
            {
                result.Add(PrefabNode((index + 10001).ToString(), false, false));
            }

            return result;
        }

        private static List<PsdHierarchyPreviewReference> BuildPreviews(int count)
        {
            var result = new List<PsdHierarchyPreviewReference>(count);
            for (int index = 0; index < count; index++)
            {
                result.Add(new PsdHierarchyPreviewReference { key = "preview-" + index, kind = "crop" });
            }

            return result;
        }

        private static List<string> EnumerableStrings(int count, string prefix)
        {
            var result = new List<string>(count);
            for (int index = 0; index < count; index++)
            {
                result.Add(prefix + index);
            }

            return result;
        }

        private static List<Dictionary<string, object>> BuildGroups(int count, int membersPerGroup)
        {
            var groups = new List<Dictionary<string, object>>(count);
            for (int groupIndex = 0; groupIndex < count; groupIndex++)
            {
                var members = new List<string>(membersPerGroup);
                for (int memberIndex = 0; memberIndex < membersPerGroup; memberIndex++)
                {
                    members.Add((1000000 + groupIndex * membersPerGroup + memberIndex).ToString());
                }

                groups.Add(new Dictionary<string, object>
                {
                    { "key", "group-" + groupIndex },
                    { "parentKey", string.Empty },
                    { "memberStableIds", members },
                    { "displayName", "Group" },
                    { "evidence", "e" },
                    { "confidence", 0.5 }
                });
            }

            return groups;
        }

        private static List<Dictionary<string, object>> BuildRenames(int count)
        {
            var renames = new List<Dictionary<string, object>>(count);
            for (int index = 0; index < count; index++)
            {
                renames.Add(new Dictionary<string, object>
                {
                    { "stableId", (2000000 + index).ToString() },
                    { "name", "Rename" },
                    { "evidence", "e" },
                    { "confidence", 0.5 }
                });
            }

            return renames;
        }
    }
}
