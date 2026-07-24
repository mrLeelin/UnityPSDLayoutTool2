namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using PsdLayoutTool2.Editor;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    public sealed class PsdCommonPrefabCreatorTests
    {
        private const string RootFolder = "Assets/__PsdCommonPrefabCreatorTests";
        private string targetPath;
        private string sourceGuid;
        private string profilePath;

        [SetUp]
        public void SetUp()
        {
            if (AssetDatabase.IsValidFolder(RootFolder)) AssetDatabase.DeleteAsset(RootFolder);
            if (!AssetDatabase.IsValidFolder(RootFolder))
                AssetDatabase.CreateFolder("Assets", "__PsdCommonPrefabCreatorTests");
            targetPath = RootFolder + "/Target.prefab";
            sourceGuid = "commonprefab" + Guid.NewGuid().ToString("N");
            profilePath = PsdPrefabTransactionalSave.GetProfilePath(targetPath, sourceGuid);
            CreateTargetAndProfile();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(RootFolder);
            AssetDatabase.DeleteAsset(profilePath);
            AssetDatabase.Refresh();
        }

        [Test]
        public void Create_ReplacesEquivalentRootsWithOnePrefabAndPreservesReferences()
        {
            Type creator = typeof(PsdHierarchyWebController).Assembly.GetType(
                "PsdLayoutTool2.Editor.PsdCommonPrefabCreator");
            Assert.That(creator, Is.Not.Null, "The confirmed workflow needs a real Unity Prefab creator.");
            MethodInfo create = creator.GetMethod(
                "Create", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(create, Is.Not.Null);

            create.Invoke(null, new object[]
            {
                targetPath,
                sourceGuid,
                new List<PsdHierarchyWebPrefabCandidateDto>
                {
                    new PsdHierarchyWebPrefabCandidateDto
                    {
                        candidateId = "candidate:101",
                        proposedName = "DailyTaskItem",
                        representativeStableId = "101",
                        instanceStableIds = new List<string> { "101", "201" }
                    }
                }
            });

            GameObject target = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
            Transform first = target.transform.Find("CardA");
            Transform second = target.transform.Find("CardB");
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            UnityEngine.Object firstSource = PrefabUtility.GetCorrespondingObjectFromSource(first.gameObject);
            UnityEngine.Object secondSource = PrefabUtility.GetCorrespondingObjectFromSource(second.gameObject);
            Assert.That(firstSource, Is.Not.Null);
            Assert.That(secondSource, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(firstSource),
                Is.EqualTo(AssetDatabase.GetAssetPath(secondSource)));
            StringAssert.EndsWith("/DailyTaskItem.prefab", AssetDatabase.GetAssetPath(firstSource));
            Assert.That(first.GetComponent<Button>().targetGraphic, Is.SameAs(first.Find("Icon").GetComponent<Image>()));
            Assert.That(second.GetComponent<Button>().targetGraphic, Is.SameAs(second.Find("Icon").GetComponent<Image>()));
            Assert.That(first.GetComponent<Image>().color, Is.EqualTo(Color.white));
            Assert.That(second.GetComponent<Image>().color, Is.EqualTo(Color.green));

            PsdHierarchyProfile profile = AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(profilePath);
            Assert.That(profile, Is.Not.Null);
            var persistentIds = new HashSet<long>(target.GetComponentsInChildren<Transform>(true)
                .Select(LocalId).Where(value => value > 0L));
            Assert.That(profile.nodes, Has.All.Matches<PsdHierarchyProfileNode>(
                node => node.localFileId > 0L && persistentIds.Contains(node.localFileId)));
        }

        private void CreateTargetAndProfile()
        {
            var root = new GameObject("Target", typeof(RectTransform));
            try
            {
                CreateCard(root.transform, "CardA", Color.white);
                CreateCard(root.transform, "CardB", Color.green);
                PrefabUtility.SaveAsPrefabAsset(root, targetPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            GameObject persistent = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
            var profile = ScriptableObject.CreateInstance<PsdHierarchyProfile>();
            profile.sourcePsdGuid = sourceGuid;
            profile.targetPrefabPath = targetPath;
            profile.targetPrefabGuid = AssetDatabase.AssetPathToGUID(targetPath);
            AddNode(profile, "101", persistent.transform.Find("CardA"));
            AddNode(profile, "102", persistent.transform.Find("CardA/Icon"));
            AddNode(profile, "201", persistent.transform.Find("CardB"));
            AddNode(profile, "202", persistent.transform.Find("CardB/Icon"));
            EnsureAssetFolder(System.IO.Path.GetDirectoryName(profilePath).Replace('\\', '/'));
            AssetDatabase.CreateAsset(profile, profilePath);
            AssetDatabase.SaveAssets();
        }

        private static void CreateCard(Transform parent, string name, Color color)
        {
            var card = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            card.transform.SetParent(parent, false);
            card.GetComponent<Image>().color = color;
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            icon.transform.SetParent(card.transform, false);
            card.GetComponent<Button>().targetGraphic = icon.GetComponent<Image>();
        }

        private static void AddNode(PsdHierarchyProfile profile, string stableId, Transform target)
        {
            profile.nodes.Add(new PsdHierarchyProfileNode
            {
                stableId = stableId,
                ownership = PsdHierarchyNodeOwnership.Generated,
                localFileId = LocalId(target),
                lastKnownPath = target.name,
                importerOwnedComponentTypes = new List<string>()
            });
        }

        private static long LocalId(Transform target)
        {
            string guid;
            long localId;
            return target != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                target.gameObject, out guid, out localId) ? localId : 0L;
        }

        private static void EnsureAssetFolder(string path)
        {
            string current = "Assets";
            foreach (string segment in path.Substring("Assets".Length).Trim('/').Split('/'))
            {
                if (string.IsNullOrEmpty(segment)) continue;
                string next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment);
                current = next;
            }
        }
    }
}
