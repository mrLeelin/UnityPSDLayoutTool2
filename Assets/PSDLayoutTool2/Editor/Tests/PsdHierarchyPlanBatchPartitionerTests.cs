namespace PsdLayoutTool2.Tests
{
    using System.Linq;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    public sealed class PsdHierarchyPlanBatchPartitionerTests
    {
        [Test]
        public void QuarantiningOneCandidateKeepsIndependentOperations()
        {
            PsdHierarchyChatContext context = CreateContext();
            JObject plan = CreatePlan();

            bool success = PsdHierarchyPlanBatchPartitioner.TryBuildSafePlan(
                plan,
                context,
                new[] { "family_bad" },
                out JObject safePlan,
                out string error);

            Assert.That(success, Is.True, error);
            Assert.That(safePlan["moves"].Values<JObject>().Single().Value<string>("source"), Is.EqualTo("node:good"));
            Assert.That(
                safePlan["componentFamilyDecisions"].Values<JObject>().Single().Value<string>("candidateId"),
                Is.EqualTo("family_good"));
            Assert.That(safePlan["variantComponentExtractions"].Values<JObject>(), Is.Empty);
            Assert.That(safePlan["componentExtractions"].Values<JObject>().Single().Value<string>("id"), Is.EqualTo("good_component"));
        }

        [Test]
        public void FailureWithoutCandidateIdCanResolveFromExtractionIndex()
        {
            PsdHierarchyChatContext context = CreateContext();
            JObject plan = CreatePlan();

            bool success = PsdHierarchyPlanBatchPartitioner.TryResolveCandidateIdFromFailure(
                "variantComponentExtractions[0] variant sources must remain direct siblings after planned moves",
                plan,
                context,
                out string candidateId);

            Assert.That(success, Is.True);
            Assert.That(candidateId, Is.EqualTo("family_bad"));
        }

        [Test]
        public void FailureWithNodeContextResolvesTheUniqueOwningCandidate()
        {
            PsdHierarchyChatContext context = CreateContext();

            bool success = PsdHierarchyPlanBatchPartitioner.TryResolveCandidateIdFromFailure(
                "variant sources conflict: source=node:bad_child",
                CreatePlan(),
                context,
                out string candidateId);

            Assert.That(success, Is.True);
            Assert.That(candidateId, Is.EqualTo("family_bad"));
        }

        [Test]
        public void SafePlanPreparesWithoutReintroducingQuarantinedRequiredCandidate()
        {
            PsdHierarchyChatContext context = CreateContext();
            Assert.That(PsdHierarchyPlanBatchPartitioner.TryBuildSafePlan(
                CreatePlan(),
                context,
                new[] { "family_bad" },
                out JObject safePlan,
                out string partitionError), Is.True, partitionError);

            bool prepared = PsdHierarchyChatCleanupExecution.TryPrepareRunnerPlan(
                context,
                safePlan.ToString(),
                out string runnerPlanJson,
                out string preparationError);

            Assert.That(prepared, Is.True, preparationError);
            JObject runnerPlan = JObject.Parse(runnerPlanJson);
            Assert.That(
                runnerPlan["componentFamilyDecisions"].Values<JObject>()
                    .Select(decision => decision.Value<string>("candidateId")),
                Is.EqualTo(new[] { "family_good" }));
        }

        private static JObject CreatePlan()
        {
            return JObject.Parse(
                "{" +
                "\"version\":2,\"snapshotFingerprint\":\"snapshot-batch\"," +
                "\"prefabAssetPath\":\"Assets/UI/Prefab/ExampleView.prefab\",\"prefabName\":\"ExampleView\"," +
                "\"output\":{\"mode\":\"in_place\",\"assetPath\":\"Assets/UI/Prefab/ExampleView.prefab\"}," +
                "\"wrappers\":[]," +
                "\"moves\":[{\"source\":\"node:bad\",\"destination\":\"node:bad_parent\"},{\"source\":\"node:good\",\"destination\":\"node:good_parent\"}]," +
                "\"renames\":[],\"emptyContainerRemovals\":[],\"tightBounds\":[],\"textureRenames\":[],\"spriteAtlasRenames\":[]," +
                "\"componentFamilyDecisions\":[" +
                "{\"candidateId\":\"family_bad\",\"parent\":\"node:bad_parent\",\"sources\":[\"node:bad\",\"node:bad_sibling\"],\"mode\":\"variant\",\"extractionId\":\"bad_variant\"}," +
                "{\"candidateId\":\"family_good\",\"parent\":\"node:good_parent\",\"sources\":[\"node:good\",\"node:good_sibling\"],\"mode\":\"component\",\"extractionId\":\"good_component\"}]," +
                "\"containmentResolutions\":[],\"flatSiblingResolutions\":[]," +
                "\"componentExtractions\":[{\"id\":\"good_component\",\"template\":\"node:good\",\"assetPath\":\"Assets/UI/Common/Good.prefab\",\"instances\":[\"node:good\",\"node:good_sibling\"]}]," +
                "\"stateComponentExtractions\":[]," +
                "\"variantComponentExtractions\":[{\"id\":\"bad_variant\",\"template\":\"node:bad\",\"assetPath\":\"Assets/UI/Common/Bad.prefab\",\"states\":[],\"instances\":[{\"source\":\"node:bad\"}]}]," +
                "\"statefulComponentExtractions\":[],\"verify\":{}" +
                "}");
        }

        private static PsdHierarchyChatContext CreateContext()
        {
            return new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                "Plan Format",
                "{\"fingerprint\":\"snapshot-batch\",\"nodes\":[" +
                "{\"id\":\"root\",\"path\":\"Root\",\"parentId\":\"\"}," +
                "{\"id\":\"bad_parent\",\"path\":\"Root/BadParent\",\"parentId\":\"root\"}," +
                "{\"id\":\"good_parent\",\"path\":\"Root/GoodParent\",\"parentId\":\"root\"}," +
                "{\"id\":\"bad\",\"path\":\"Root/BadParent/Bad\",\"parentId\":\"bad_parent\"}," +
                "{\"id\":\"bad_child\",\"path\":\"Root/Bad/Child\",\"parentId\":\"bad\"}," +
                "{\"id\":\"bad_sibling\",\"path\":\"Root/BadParent/BadSibling\",\"parentId\":\"bad_parent\"}," +
                "{\"id\":\"good\",\"path\":\"Root/GoodParent/Good\",\"parentId\":\"good_parent\"}," +
                "{\"id\":\"good_sibling\",\"path\":\"Root/GoodParent/GoodSibling\",\"parentId\":\"good_parent\"}]," +
                "\"componentFamilyCandidates\":[" +
                "{\"id\":\"family_bad\",\"suggestedAssetName\":\"组 16\",\"parent\":\"node:bad_parent\",\"sources\":[\"node:bad\",\"node:bad_sibling\"],\"requiresExtraction\":true,\"recommendedMode\":\"variant\"}," +
                "{\"id\":\"family_good\",\"suggestedAssetName\":\"Good\",\"parent\":\"node:good_parent\",\"sources\":[\"node:good\",\"node:good_sibling\"],\"requiresExtraction\":true,\"recommendedMode\":\"component\"}]," +
                "\"containmentFindings\":[],\"flatSiblingFindings\":[]}",
                "snapshot-batch",
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/snapshot-batch.json");
        }
    }
}
