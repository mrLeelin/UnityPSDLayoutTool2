namespace PsdLayoutTool2.Tests
{
    using System.IO;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.Events;
    using UnityEngine;
    using UnityEngine.UI;
    using Object = UnityEngine.Object;

    public sealed class PsdHierarchyV2ComponentExtractionTests
    {
        private const string Folder = "Assets/__PsdV2ComponentExtractionTests";
        private const string Target = Folder + "/View.prefab";
        private const string ComponentPath = Folder + "/Card.prefab";
        private PsdHierarchyChatContext context;
        private bool ownsFolder;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False);
            AssetDatabase.CreateFolder("Assets", "__PsdV2ComponentExtractionTests");
            ownsFolder = true;
            var root = new GameObject("View", typeof(RectTransform));
            try
            {
                var external = root.AddComponent<PsdHierarchyExtractionReferenceProbe>();
                for (int i = 0; i < 2; i++)
                {
                    var card = new GameObject(
                        i == 0 ? "First" : "Second",
                        typeof(RectTransform),
                        typeof(Image),
                        typeof(ScrollRect),
                        typeof(PsdHierarchyExtractionValueProbe));
                    card.transform.SetParent(root.transform, false);
                    var rect = (RectTransform)card.transform;
                    rect.anchoredPosition = new Vector2(i == 0 ? -50 : 50, 0);
                    rect.sizeDelta = new Vector2(70, 80);
                    card.GetComponent<Image>().color = i == 0 ? Color.green : Color.red;
                    var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
                    label.transform.SetParent(card.transform, false);
                    label.GetComponent<Text>().text = i == 0 ? "One" : "Two";
                    label.GetComponent<Text>().font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    var scroll = card.GetComponent<ScrollRect>();
                    scroll.enabled = false;
                    scroll.content = (RectTransform)label.transform;
                    var probe = card.GetComponent<PsdHierarchyExtractionValueProbe>();
                    probe.nested.label = i == 0 ? "first-nested" : "second-nested";
                    probe.nested.target = label;
                    probe.nested.hiddenTarget = label;
                    probe.targets.Add(label);
                    UnityEventTools.AddPersistentListener(probe.onInvoke, probe.HandleInvoke);
                    if (i == 1)
                    {
                        probe.nested.target = root.transform.GetChild(0).GetChild(0).gameObject;
                        card.SetActive(false);
                        card.layer = 5;
                        external.targets = new[] { card, label };
                        external.nested.target = label;
                        external.hiddenTarget = label;
                        UnityEventTools.AddPersistentListener(external.onInvoke, probe.HandleInvoke);
                    }
                }
                var owner = root.AddComponent<ScrollRect>();
                owner.enabled = false;
                owner.content = (RectTransform)root.transform.GetChild(1).GetChild(0);
                PrefabUtility.SaveAsPrefabAsset(root, Target);
            }
            finally { Object.DestroyImmediate(root); }
            RefreshContext();
        }

        private void RefreshContext()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            context = new PsdHierarchyChatContext(project, "", Target, "", "", "",
                hierarchySnapshotJson: new JObject { ["nodes"] = new JArray(
                    new JObject { ["id"] = "root", ["path"] = "View" },
                    new JObject { ["id"] = "first", ["path"] = "View/First" },
                    new JObject { ["id"] = "second", ["path"] = "View/Second" },
                    new JObject { ["id"] = "label", ["path"] = "View/First/Label" }) }.ToString(),
                hierarchySnapshotFingerprint: PsdHierarchyChatContextBuilder.ComputeFileFingerprint(Path.Combine(project, Target)));
        }

        [TearDown]
        public void TearDown()
        {
            if (ownsFolder) AssetDatabase.DeleteAsset(Folder);
        }

        private JObject Plan()
        {
            var plan = new JObject
            {
                ["version"] = 2, ["snapshotFingerprint"] = context.hierarchySnapshotFingerprint,
                ["prefabName"] = "View", ["prefabAssetPath"] = Target,
                ["output"] = new JObject { ["mode"] = "in_place", ["assetPath"] = Target },
                ["verify"] = new JObject { ["nodes"] = 5 }
            };
            foreach (string key in new[] { "wrappers", "moves", "renames", "tightBounds", "emptyContainerRemovals",
                "textureRenames", "spriteAtlasRenames", "componentFamilyDecisions", "containmentResolutions",
                "flatSiblingResolutions", "stateComponentExtractions", "variantComponentExtractions", "statefulComponentExtractions" })
                plan[key] = new JArray();
            plan["componentExtractions"] = new JArray(new JObject
            {
                ["id"] = "card", ["name"] = "Card", ["assetPath"] = ComponentPath,
                ["template"] = "node:first", ["instances"] = new JArray("node:first", "node:second")
            });
            return plan;
        }

        [Test]
        public async Task SharedEntryExtractsAndReloadsInstancesWithDifferencesAndReferences()
        {
            JObject plan = Plan();
            byte[] before = File.ReadAllBytes(Target);
            var preflight = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());
            Assert.That(preflight.success, Is.True, preflight.message);
            Assert.That(File.ReadAllBytes(Target), Is.EqualTo(before));
            Assert.That(File.Exists(ComponentPath), Is.False);
            var result = await PsdHierarchyNativeCleanupExecutor.ApplyAsync(context, plan.ToString());
            Assert.That(result.success, Is.True, result.message);
            var root = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    var card = root.transform.GetChild(i);
                    Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(card.gameObject), Is.EqualTo(ComponentPath));
                    Assert.That(card.name, Is.EqualTo(i == 0 ? "First" : "Second"));
                    Assert.That(card.GetComponent<Image>().color, Is.EqualTo(i == 0 ? Color.green : Color.red));
                    Assert.That(card.GetChild(0).GetComponent<Text>().text, Is.EqualTo(i == 0 ? "One" : "Two"));
                    Assert.That(((RectTransform)card).anchoredPosition.x, Is.EqualTo(i == 0 ? -50 : 50));
                    Assert.That(card.GetComponent<ScrollRect>().content, Is.EqualTo(card.GetChild(0)));
                    Assert.That(card.gameObject.activeSelf, Is.EqualTo(i == 0));
                    Assert.That(card.gameObject.layer, Is.EqualTo(i == 0 ? 0 : 5));
                    var probe = card.GetComponent<PsdHierarchyExtractionValueProbe>();
                    Assert.That(probe.nested.label, Is.EqualTo(i == 0 ? "first-nested" : "second-nested"));
                    GameObject expectedNestedTarget = i == 0
                        ? card.GetChild(0).gameObject
                        : root.transform.GetChild(0).GetChild(0).gameObject;
                    Assert.That(probe.nested.target, Is.EqualTo(expectedNestedTarget));
                    Assert.That(probe.nested.hiddenTarget, Is.EqualTo(card.GetChild(0).gameObject));
                    Assert.That(probe.targets, Is.EqualTo(new[] { card.GetChild(0).gameObject }));
                    Assert.That(probe.onInvoke.GetPersistentTarget(0), Is.EqualTo(probe));
                }
                Transform second = root.transform.GetChild(1);
                Assert.That(root.GetComponent<ScrollRect>().content, Is.EqualTo(second.GetChild(0)));
                var external = root.GetComponent<PsdHierarchyExtractionReferenceProbe>();
                Assert.That(external.targets, Is.EqualTo(new[] { second.gameObject, second.GetChild(0).gameObject }));
                Assert.That(external.nested.target, Is.EqualTo(second.GetChild(0).gameObject));
                Assert.That(external.hiddenTarget, Is.EqualTo(second.GetChild(0).gameObject));
                Assert.That(external.onInvoke.GetPersistentTarget(0),
                    Is.EqualTo(second.GetComponent<PsdHierarchyExtractionValueProbe>()));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        [Test]
        public async Task ExistingExtractionTargetIsRejectedBeforeAnyWrite()
        {
            var existing = new GameObject("Card", typeof(RectTransform));
            try
            {
                Assert.That(PrefabUtility.SaveAsPrefabAsset(existing, ComponentPath), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(existing);
            }

            byte[] existingBytes = File.ReadAllBytes(ComponentPath);
            byte[] before = File.ReadAllBytes(Target);

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, Plan().ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("already exists"));
            Assert.That(File.ReadAllBytes(ComponentPath), Is.EqualTo(existingBytes));
            Assert.That(File.ReadAllBytes(Target), Is.EqualTo(before));
        }

        [Test]
        public async Task IncompatibleInstanceStructureIsRejectedBeforeAnyWrite()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                var extra = new GameObject("Extra", typeof(RectTransform));
                extra.transform.SetParent(root.transform.GetChild(1), false);
                PrefabUtility.SaveAsPrefabAsset(root, Target);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            RefreshContext();
            byte[] before = File.ReadAllBytes(Target);

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, Plan().ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("structure differs"));
            Assert.That(File.ReadAllBytes(Target), Is.EqualTo(before));
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task OverlappingExtractionSourcesAreRejectedBeforeAnyWrite()
        {
            JObject plan = Plan();
            plan["componentExtractions"] = new JArray(
                new JObject
                {
                    ["id"] = "card",
                    ["name"] = "Card",
                    ["assetPath"] = ComponentPath,
                    ["template"] = "node:first",
                    ["instances"] = new JArray("node:first"),
                },
                new JObject
                {
                    ["id"] = "card_again",
                    ["name"] = "CardAgain",
                    ["assetPath"] = Folder + "/CardAgain.prefab",
                    ["template"] = "node:first",
                    ["instances"] = new JArray("node:first"),
                });
            byte[] before = File.ReadAllBytes(Target);

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("overlaps"));
            Assert.That(File.ReadAllBytes(Target), Is.EqualTo(before));
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task MissingMandatoryCandidateDecisionIsRejectedBeforeAnyWrite()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            context = new PsdHierarchyChatContext(
                project, "", Target, "", "", "",
                hierarchySnapshotJson: new JObject
                {
                    ["nodes"] = new JArray(
                        new JObject { ["id"] = "root", ["path"] = "View" },
                        new JObject { ["id"] = "first", ["path"] = "View/First" },
                        new JObject { ["id"] = "second", ["path"] = "View/Second" }),
                    ["componentFamilyCandidates"] = new JArray(new JObject
                    {
                        ["id"] = "family_001",
                        ["requiresExtraction"] = true,
                        ["recommendedMode"] = "component",
                        ["parent"] = "node:root",
                        ["sources"] = new JArray("node:first", "node:second"),
                    }),
                }.ToString(),
                hierarchySnapshotFingerprint: PsdHierarchyChatContextBuilder.ComputeFileFingerprint(
                    Path.Combine(project, Target)));

            // 抽取本身合法，但强制候选缺少 componentFamilyDecisions 决策，必须写入前拒绝。
            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, Plan().ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("family_001"));
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task MandatoryComponentCandidateRequiresItsActualSourcesToBeCovered()
        {
            AddMandatoryCandidate("component", "node:root", "node:first", "node:second");
            JObject plan = Plan();
            plan["componentFamilyDecisions"] = new JArray(new JObject
            {
                ["candidateId"] = "family_001",
                ["parent"] = "node:root",
                ["sources"] = new JArray("node:first", "node:second"),
                ["mode"] = "component",
                ["extractionId"] = "card",
            });

            var accepted = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());
            Assert.That(accepted.success, Is.True, accepted.message);

            AddMandatoryCandidate("component", "node:root", "node:first", "node:second", "node:label");
            plan["componentFamilyDecisions"][0]["sources"] =
                new JArray("node:first", "node:second", "node:label");
            var rejected = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());
            Assert.That(rejected.success, Is.False);
            Assert.That(rejected.message, Does.Contain("does not cover every snapshot candidate source"));
        }

        private void AddMandatoryCandidate(string mode, string parent, params string[] sources)
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            JObject snapshot = JObject.Parse(context.hierarchySnapshotJson);
            snapshot["componentFamilyCandidates"] = new JArray(new JObject
            {
                ["id"] = "family_001",
                ["requiresExtraction"] = true,
                ["recommendedMode"] = mode,
                ["parent"] = parent,
                ["sources"] = new JArray(sources),
            });
            context = new PsdHierarchyChatContext(
                project, "", Target, "", "", "",
                hierarchySnapshotJson: snapshot.ToString(),
                hierarchySnapshotFingerprint: context.hierarchySnapshotFingerprint);
        }
    }

}
