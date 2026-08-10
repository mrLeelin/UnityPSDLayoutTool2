namespace PsdLayoutTool2.Tests
{
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    public sealed class PsdHierarchyLocalRepairScopeTests
    {
        [Test]
        public void SelectedNodesWithSiblingsAllowsSiblingMovesButRejectsOutsideNodes()
        {
            PsdHierarchyChatContext context = CreateContext();
            Assert.That(
                PsdHierarchyLocalRepairScope.TryCreateFromSelectedPaths(
                    context,
                    new[] { "Root/Container/Selected" },
                    PsdHierarchyLocalRepairScopeMode.SelectedNodesWithSiblings,
                    out PsdHierarchyLocalRepairScope scope,
                    out string error),
                Is.True,
                error);

            JObject validPlan = Plan("node:n003", "node:n002");
            Assert.DoesNotThrow(() => scope.ValidatePlan(validPlan));

            JObject outsidePlan = Plan("node:n004", "node:n002");
            System.InvalidOperationException exception = Assert.Throws<System.InvalidOperationException>(
                () => scope.ValidatePlan(outsidePlan));
            Assert.That(exception.Message, Does.Contain("越界"));
        }

        [Test]
        public void SelectedParentSubtreeIncludesAllDescendants()
        {
            PsdHierarchyChatContext context = CreateContext();
            Assert.That(
                PsdHierarchyLocalRepairScope.TryCreateFromSelectedPaths(
                    context,
                    new[] { "Root/Container" },
                    PsdHierarchyLocalRepairScopeMode.SelectedParentSubtree,
                    out PsdHierarchyLocalRepairScope scope,
                    out string error),
                Is.True,
                error);

            Assert.DoesNotThrow(() => scope.ValidatePlan(Plan("node:n005", "node:n002")));
        }

        [Test]
        public void LocalRepairRejectsExtractionAndAssetRenameOperations()
        {
            PsdHierarchyChatContext context = CreateContext();
            Assert.That(
                PsdHierarchyLocalRepairScope.TryCreateFromSelectedPaths(
                    context,
                    new[] { "Root/Container/Selected" },
                    PsdHierarchyLocalRepairScopeMode.SelectedNodes,
                    out PsdHierarchyLocalRepairScope scope,
                    out string error),
                Is.True,
                error);

            JObject plan = Plan("node:n002", "node:n002");
            plan["textureRenames"] = new JArray(new JObject { ["from"] = "Assets/A.png" });

            Assert.That(
                Assert.Throws<System.InvalidOperationException>(() => scope.ValidatePlan(plan)).Message,
                Does.Contain("textureRenames"));
        }

        [Test]
        public void LocalRepairSelectedPrefabExtractionMustMatchTheLockedSelection()
        {
            PsdHierarchyChatContext context = CreateContext();
            Assert.That(
                PsdHierarchyLocalRepairScope.TryCreateFromSelectedPaths(
                    context,
                    new[] { "Root/Container/Selected", "Root/Container/Sibling" },
                    PsdHierarchyLocalRepairScopeMode.SelectedNodes,
                    out PsdHierarchyLocalRepairScope scope,
                    out string error),
                Is.True,
                error);

            JObject plan = Plan("node:n002", "node:n002");
            plan["moves"] = new JArray();
            plan["selectedPrefabExtractions"] = new JArray
            {
                new JObject
                {
                    ["id"] = "day_sign_card",
                    ["name"] = "DaySignCard",
                    ["assetPath"] = "Assets/UI/Prefab/Common/DaySignCard.prefab",
                    ["parent"] = "node:n001",
                    ["sources"] = new JArray("node:n002"),
                },
            };

            Assert.That(
                Assert.Throws<System.InvalidOperationException>(() => scope.ValidatePlan(plan)).Message,
                Does.Contain("sources"));
        }

        [Test]
        public void CrossParentExtractionBuildsFourReviewedGroupsAndLeavesTheStartMarkerUntouched()
        {
            PsdHierarchyChatContext context = CreateCrossParentContext();
            Assert.That(
                PsdHierarchyLocalRepairScope.TryCreateFromSelectedPaths(
                    context,
                    new[]
                    {
                        "Root/Reward/GiftBox4",
                        "Root/Progress/DateMarker5",
                        "Root/Progress/DateText4",
                    },
                    PsdHierarchyLocalRepairScopeMode.SelectedNodes,
                    out PsdHierarchyLocalRepairScope scope,
                    out string error),
                Is.True,
                error);

            Assert.That(
                scope.TryCreateCrossParentPrefabExtraction(
                    context,
                    "DaySignRewardItem",
                    out PsdHierarchyCrossParentPrefabExtraction extraction,
                    out error),
                Is.True,
                error);
            Assert.That(extraction.rootNodeId, Is.EqualTo("n000"));
            Assert.That(extraction.instances, Has.Length.EqualTo(4));
            Assert.That(extraction.unmatchedNodeIds, Is.EqualTo(new[] { "n101" }));
            Assert.That(extraction.templateSourceNodeIds, Is.EquivalentTo(new[] { "n004", "n205", "n304" }));
        }

        [Test]
        public void CrossParentExtractionRejectsAReviewedGroupOutsideTheCommonRoot()
        {
            PsdHierarchyChatContext context = CreateCrossParentContext();
            Assert.That(
                PsdHierarchyLocalRepairScope.TryCreateFromSelectedPaths(
                    context,
                    new[]
                    {
                        "Root/Reward/GiftBox4",
                        "Root/Progress/DateMarker5",
                        "Root/Progress/DateText4",
                    },
                    PsdHierarchyLocalRepairScopeMode.SelectedNodes,
                    out PsdHierarchyLocalRepairScope scope,
                    out string error),
                Is.True,
                error);

            JObject plan = Plan("node:n002", "node:n002");
            plan["moves"] = new JArray();
            plan["crossParentPrefabExtractions"] = new JArray(new JObject
            {
                ["id"] = "day_sign_reward_item",
                ["name"] = "DaySignRewardItem",
                ["assetPath"] = "Assets/UI/Common/DaySignRewardItem.prefab",
                ["root"] = "node:n000",
                ["templateSources"] = new JArray("node:n004", "node:n205", "node:n304"),
                ["instances"] = new JArray(
                    new JObject { ["sources"] = new JArray("node:n001", "node:n201", "node:n301") },
                    new JObject { ["sources"] = new JArray("node:n004", "node:n205", "node:n304") }),
                ["unmatched"] = new JArray("node:n999"),
            });

            Assert.That(
                Assert.Throws<System.InvalidOperationException>(() => scope.ValidatePlan(plan)).Message,
                Does.Contain("Unmatched nodes"));
        }

        private static PsdHierarchyChatContext CreateContext()
        {
            const string snapshot = "{\"nodes\":[" +
                                    "{\"id\":\"n000\",\"path\":\"Root\",\"parentId\":\"\"}," +
                                    "{\"id\":\"n001\",\"path\":\"Root/Container\",\"parentId\":\"n000\"}," +
                                    "{\"id\":\"n002\",\"path\":\"Root/Container/Selected\",\"parentId\":\"n001\"}," +
                                    "{\"id\":\"n003\",\"path\":\"Root/Container/Sibling\",\"parentId\":\"n001\"}," +
                                    "{\"id\":\"n005\",\"path\":\"Root/Container/Selected/Descendant\",\"parentId\":\"n002\"}," +
                                    "{\"id\":\"n004\",\"path\":\"Root/Outside\",\"parentId\":\"n000\"}]}";
            return new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Example.prefab",
                "Skill",
                "Skill content",
                "Prefab content",
                hierarchySnapshotJson: snapshot);
        }

        private static PsdHierarchyChatContext CreateCrossParentContext()
        {
            const string snapshot = "{\"nodes\":[" +
                                    "{\"id\":\"n000\",\"path\":\"Root\",\"name\":\"Root\",\"parentId\":\"\"}," +
                                    "{\"id\":\"n010\",\"path\":\"Root/Reward\",\"name\":\"Reward\",\"parentId\":\"n000\"}," +
                                    "{\"id\":\"n020\",\"path\":\"Root/Progress\",\"name\":\"Progress\",\"parentId\":\"n000\"}," +
                                    "{\"id\":\"n101\",\"path\":\"Root/Progress/DateMarker1\",\"name\":\"DateMarker1\",\"parentId\":\"n020\"}," +
                                    "{\"id\":\"n201\",\"path\":\"Root/Progress/DateMarker2\",\"name\":\"DateMarker2\",\"parentId\":\"n020\"}," +
                                    "{\"id\":\"n202\",\"path\":\"Root/Progress/DateMarker3\",\"name\":\"DateMarker3\",\"parentId\":\"n020\"}," +
                                    "{\"id\":\"n203\",\"path\":\"Root/Progress/DateMarker4\",\"name\":\"DateMarker4\",\"parentId\":\"n020\"}," +
                                    "{\"id\":\"n205\",\"path\":\"Root/Progress/DateMarker5\",\"name\":\"DateMarker5\",\"parentId\":\"n020\"}," +
                                    "{\"id\":\"n301\",\"path\":\"Root/Progress/DateText1\",\"name\":\"DateText1\",\"parentId\":\"n020\"}," +
                                    "{\"id\":\"n302\",\"path\":\"Root/Progress/DateText2\",\"name\":\"DateText2\",\"parentId\":\"n020\"}," +
                                    "{\"id\":\"n303\",\"path\":\"Root/Progress/DateText3\",\"name\":\"DateText3\",\"parentId\":\"n020\"}," +
                                    "{\"id\":\"n304\",\"path\":\"Root/Progress/DateText4\",\"name\":\"DateText4\",\"parentId\":\"n020\"}," +
                                    "{\"id\":\"n001\",\"path\":\"Root/Reward/GiftBox1\",\"name\":\"GiftBox1\",\"parentId\":\"n010\"}," +
                                    "{\"id\":\"n002\",\"path\":\"Root/Reward/GiftBox2\",\"name\":\"GiftBox2\",\"parentId\":\"n010\"}," +
                                    "{\"id\":\"n003\",\"path\":\"Root/Reward/GiftBox3\",\"name\":\"GiftBox3\",\"parentId\":\"n010\"}," +
                                    "{\"id\":\"n004\",\"path\":\"Root/Reward/GiftBox4\",\"name\":\"GiftBox4\",\"parentId\":\"n010\"}]}";
            return new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Example.prefab",
                "Skill",
                "Skill content",
                "Prefab content",
                hierarchySnapshotJson: snapshot);
        }

        private static JObject Plan(string source, string destination)
        {
            return new JObject
            {
                ["wrappers"] = new JArray(),
                ["moves"] = new JArray(new JObject
                {
                    ["source"] = source,
                    ["destination"] = destination,
                }),
                ["renames"] = new JArray(),
                ["emptyContainerRemovals"] = new JArray(),
                ["tightBounds"] = new JArray(),
                ["componentFamilyDecisions"] = new JArray(),
                ["componentExtractions"] = new JArray(),
                ["stateComponentExtractions"] = new JArray(),
                ["variantComponentExtractions"] = new JArray(),
                ["statefulComponentExtractions"] = new JArray(),
                ["textureRenames"] = new JArray(),
                ["spriteAtlasRenames"] = new JArray(),
                ["flatSiblingResolutions"] = new JArray(),
            };
        }
    }
}
