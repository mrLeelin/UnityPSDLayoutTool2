namespace PsdLayoutTool2.Tests
{
    using System.Collections.Generic;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

    public sealed class PsdHierarchyCleanupReplayProfileTests
    {
        private const string SourceGuid = "0123456789abcdef0123456789abcdef";
        private const string TestFolder =
            "Assets/PSDLayoutTool2Settings/ReplayProfileTests";
        private const string TargetPath = TestFolder + "/ExampleView.prefab";
        private const string ComponentPath = TestFolder + "/Common/ReusableItem.prefab";

        [SetUp]
        public void SetUp()
        {
            EnsureFolder(TestFolder);
            var root = new GameObject("ExampleView");
            try
            {
                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, TargetPath), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(
                PsdHierarchyCleanupReplayProfile.GetProfilePath(TargetPath, SourceGuid));
            AssetDatabase.DeleteAsset(TestFolder);
        }

        [Test]
        public void ReplayPlansRetargetOnlyTheGeneratedPrefab()
        {
            PsdHierarchyCleanupReplayProfile profile = CreateProfile();
            try
            {
                const string stagedPath =
                    "Assets/PSDLayoutTool2Settings/HierarchyReplayTemp/candidate.prefab";

                Assert.That(profile.TryBuildReplayPlans(
                    SourceGuid,
                    TargetPath,
                    stagedPath,
                    out IReadOnlyList<string> replayPlanJsonStages,
                    out string error), Is.True, error);

                Assert.That(replayPlanJsonStages, Has.Count.EqualTo(1));
                var replayPlan = JObject.Parse(replayPlanJsonStages[0]);
                Assert.That(replayPlan.Value<string>("prefabAssetPath"), Is.EqualTo(stagedPath));
                Assert.That(replayPlan["output"].Value<string>("assetPath"), Is.EqualTo(stagedPath));
                Assert.That(
                    replayPlan["componentExtractions"][0].Value<string>("assetPath"),
                    Is.EqualTo(ComponentPath));
                Assert.That(replayPlan["verify"], Is.TypeOf<JObject>());
                Assert.That(((JObject)replayPlan["verify"]).HasValues, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void AppendStagePreservesExecutionOrder()
        {
            PsdHierarchyCleanupReplayProfile profile = CreateProfile();
            try
            {
                string secondPlan = CreateRunnerPlan("second_component");
                profile.AppendStage(SourceGuid, TargetPath, secondPlan);

                Assert.That(profile.TryBuildReplayPlans(
                    SourceGuid,
                    TargetPath,
                    "Assets/Temp/ExampleView.prefab",
                    out IReadOnlyList<string> stages,
                    out string error), Is.True, error);
                Assert.That(stages, Has.Count.EqualTo(2));
                Assert.That(
                    JObject.Parse(stages[0])["componentExtractions"][0].Value<string>("id"),
                    Is.EqualTo("reusable_item"));
                Assert.That(
                    JObject.Parse(stages[1])["componentExtractions"][0].Value<string>("id"),
                    Is.EqualTo("second_component"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void HierarchyOnlyStageCanBePersisted()
        {
            var profile = ScriptableObject.CreateInstance<PsdHierarchyCleanupReplayProfile>();
            try
            {
                var plan = JObject.Parse(CreateRunnerPlan());
                plan["componentExtractions"] = new JArray();
                plan["wrappers"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = "content",
                        ["parent"] = "ExampleView",
                        ["name"] = "[Content]",
                        ["siblingIndex"] = 0,
                    },
                };

                Assert.DoesNotThrow(() => profile.Initialize(
                    SourceGuid,
                    TargetPath,
                    plan.ToString(Newtonsoft.Json.Formatting.None)));
                Assert.That(profile.TryBuildReplayPlans(
                    SourceGuid,
                    TargetPath,
                    "Assets/Temp/ExampleView.prefab",
                    out IReadOnlyList<string> stages,
                    out string error), Is.True, error);
                Assert.That(stages, Has.Count.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ReplayProfileIsBoundToBothSourceAndTarget()
        {
            PsdHierarchyCleanupReplayProfile profile = CreateProfile();
            try
            {
                Assert.That(profile.TryBuildReplayPlans(
                    "fedcba9876543210fedcba9876543210",
                    TargetPath,
                    "Assets/Temp.prefab",
                    out _,
                    out string sourceError), Is.False);
                Assert.That(sourceError, Does.Contain("source PSD"));

                Assert.That(profile.TryBuildReplayPlans(
                    SourceGuid,
                    "Assets/UI/Prefab/OtherView.prefab",
                    "Assets/Temp.prefab",
                    out _,
                    out string targetError), Is.False);
                Assert.That(targetError, Does.Contain("target Prefab"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ProfilePathIsGenericAndTargetSpecific()
        {
            string first = PsdHierarchyCleanupReplayProfile.GetProfilePath(TargetPath, SourceGuid);
            string second = PsdHierarchyCleanupReplayProfile.GetProfilePath(
                "Assets/UI/Prefab/OtherView.prefab",
                SourceGuid);

            Assert.That(first, Does.StartWith(
                "Assets/PSDLayoutTool2Settings/HierarchyCleanupReplayProfiles/"));
            Assert.That(first, Does.EndWith(".asset"));
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(first, Does.Not.Contain("ExampleView"));
        }

        [Test]
        public void SchemaOnePlanMigratesWithoutLosingItsFirstStage()
        {
            var profile = ScriptableObject.CreateInstance<PsdHierarchyCleanupReplayProfile>();
            try
            {
                SetPrivateField(profile, "schemaVersion", 1);
                SetPrivateField(profile, "sourcePsdGuid", SourceGuid);
                SetPrivateField(profile, "targetPrefabPath", TargetPath);
                SetPrivateField(profile, "runnerPlanJson", CreateRunnerPlan("legacy_component"));
                SetPrivateField(profile, "runnerPlanStages", new List<string>());

                profile.AppendStage(SourceGuid, TargetPath, CreateRunnerPlan("second_component"));

                Assert.That(profile.TryBuildReplayPlans(
                    SourceGuid,
                    TargetPath,
                    "Assets/Temp/ExampleView.prefab",
                    out IReadOnlyList<string> stages,
                    out string error), Is.True, error);
                Assert.That(stages, Has.Count.EqualTo(2));
                Assert.That(
                    JObject.Parse(stages[0])["componentExtractions"][0].Value<string>("id"),
                    Is.EqualTo("legacy_component"));
                Assert.That(
                    JObject.Parse(stages[1])["componentExtractions"][0].Value<string>("id"),
                    Is.EqualTo("second_component"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void PendingReplayRequiresTheExactNonEmptyTargetGuid()
        {
            Assert.That(
                PsdHierarchyCleanupReplayCoordinator.TargetGuidMatches("abc", "abc"),
                Is.True);
            Assert.That(
                PsdHierarchyCleanupReplayCoordinator.TargetGuidMatches("abc", "def"),
                Is.False);
            Assert.That(
                PsdHierarchyCleanupReplayCoordinator.TargetGuidMatches(string.Empty, string.Empty),
                Is.False);
        }

        [Test]
        public void InterruptedStageRestartsFromItsCheckpointedIndex()
        {
            Assert.That(
                PsdHierarchyCleanupReplayCoordinator.ResolveRestartStage(1, 1, 3),
                Is.EqualTo(1));
            Assert.That(
                PsdHierarchyCleanupReplayCoordinator.ResolveRestartStage(2, -1, 3),
                Is.EqualTo(2));
            Assert.That(
                () => PsdHierarchyCleanupReplayCoordinator.ResolveRestartStage(2, 1, 3),
                Throws.TypeOf<System.IO.InvalidDataException>());
        }

        [Test]
        public void ProtectedRenameTargetsRequireTheRecordedGuid()
        {
            string textureFolder = TestFolder + "/Texture";
            EnsureFolder(textureFolder);
            string renamedTarget = textureFolder + "/ExampleView_Icon.mat";
            AssetDatabase.CreateAsset(
                new Material(Shader.Find("UI/Default")),
                renamedTarget);
            string expectedGuid = AssetDatabase.AssetPathToGUID(renamedTarget);

            var plan = JObject.Parse(CreateRunnerPlan());
            plan["textureRenames"] = new JArray
            {
                new JObject
                {
                    ["from"] = textureFolder + "/raw_icon.mat",
                    ["toName"] = "ExampleView_Icon",
                    ["expectedGuid"] = expectedGuid,
                },
            };
            PsdHierarchyCleanupReplayProfile profile = CreateProfile(
                plan.ToString(Newtonsoft.Json.Formatting.None));
            try
            {
                Assert.That(profile.TryGetProtectedRenameTargets(
                    SourceGuid,
                    TargetPath,
                    out IReadOnlyList<string> paths,
                    out string error), Is.True, error);
                Assert.That(paths, Is.EquivalentTo(new[] { renamedTarget }));

                Assert.That(AssetDatabase.RenameAsset(renamedTarget, "raw_icon"), Is.Empty);
                Assert.That(profile.TryGetProtectedRenameTargets(
                    SourceGuid,
                    TargetPath,
                    out IReadOnlyList<string> preRenamePaths,
                    out string preRenameError), Is.True, preRenameError);
                Assert.That(preRenamePaths, Is.Empty);

                var mismatchedPlan = JObject.Parse(
                    ((List<string>)GetPrivateField(profile, "runnerPlanStages"))[0]);
                mismatchedPlan["textureRenames"][0]["expectedGuid"] =
                    "ffffffffffffffffffffffffffffffff";
                SetPrivateField(
                    profile,
                    "runnerPlanStages",
                    new List<string>
                    {
                        mismatchedPlan.ToString(Newtonsoft.Json.Formatting.None),
                    });
                Assert.That(profile.TryGetProtectedRenameTargets(
                    SourceGuid,
                    TargetPath,
                    out _,
                    out string missingError), Is.False);
                Assert.That(missingError, Does.Contain("GUID no longer matches"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void LegacyPendingMigrationCanUseOnlyAStoredGuidBoundProfile()
        {
            PsdHierarchyCleanupReplayProfile profile = CreateProfile();
            string profilePath =
                PsdHierarchyCleanupReplayProfile.GetProfilePath(TargetPath, SourceGuid);
            EnsureFolder(System.IO.Path.GetDirectoryName(profilePath).Replace('\\', '/'));
            AssetDatabase.CreateAsset(profile, profilePath);
            AssetDatabase.SaveAssetIfDirty(profile);

            Assert.That(PsdHierarchyCleanupReplayProfile.TryGetVerifiedTargetGuid(
                TargetPath,
                out string verifiedGuid), Is.True);
            Assert.That(verifiedGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(TargetPath)));

            SetPrivateField(profile, "targetPrefabGuid", "ffffffffffffffffffffffffffffffff");
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            Assert.That(PsdHierarchyCleanupReplayProfile.TryGetVerifiedTargetGuid(
                TargetPath,
                out _), Is.False);
        }

        private static PsdHierarchyCleanupReplayProfile CreateProfile()
        {
            return CreateProfile(CreateRunnerPlan());
        }

        private static PsdHierarchyCleanupReplayProfile CreateProfile(string planJson)
        {
            var profile = ScriptableObject.CreateInstance<PsdHierarchyCleanupReplayProfile>();
            profile.Initialize(SourceGuid, TargetPath, planJson);
            return profile;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            System.Reflection.FieldInfo field = GetPrivateFieldInfo(target, fieldName);
            field.SetValue(target, value);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            return GetPrivateFieldInfo(target, fieldName).GetValue(target);
        }

        private static System.Reflection.FieldInfo GetPrivateFieldInfo(
            object target,
            string fieldName)
        {
            System.Reflection.FieldInfo field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing serialized field: " + fieldName);
            return field;
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    Assert.That(AssetDatabase.CreateFolder(current, parts[index]), Is.Not.Empty);
                current = next;
            }
        }

        private static string CreateRunnerPlan(string extractionId = "reusable_item")
        {
            return new JObject
            {
                ["version"] = 1,
                ["prefabAssetPath"] = TargetPath,
                ["output"] = new JObject
                {
                    ["mode"] = "in_place",
                    ["assetPath"] = TargetPath,
                },
                ["prefabName"] = "ExampleView",
                ["wrappers"] = new JArray(),
                ["moves"] = new JArray(),
                ["renames"] = new JArray(),
                ["emptyContainerRemovals"] = new JArray(),
                ["tightBounds"] = new JArray(),
                ["textureRenames"] = new JArray(),
                ["spriteAtlasRenames"] = new JArray(),
                ["componentFamilyDecisions"] = new JArray(),
                ["componentExtractions"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = extractionId,
                        ["template"] = "ExampleView/List/Item_1",
                        ["assetPath"] = ComponentPath,
                        ["instances"] = new JArray(
                            "ExampleView/List/Item_1",
                            "ExampleView/List/Item_2"),
                    },
                },
                ["stateComponentExtractions"] = new JArray(),
                ["variantComponentExtractions"] = new JArray(),
                ["statefulComponentExtractions"] = new JArray(),
                ["verify"] = new JObject
                {
                    ["nodes"] = 99,
                },
            }.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
