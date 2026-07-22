namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.UI;

    public sealed class PsdPrefabIncrementalMergeTests
    {
        private const string Folder = "Assets/__PsdPrefabIncrementalMergeTests";
        private const string TargetPath = Folder + "/Target.prefab";
        private const string SameNamePath = Folder + "/SameName/Target.prefab";
        private const string ProfilePath = Folder + "/Target.HierarchyProfile.asset";
        private const string TemporaryPath = Folder + "/Candidate.prefab";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.CreateFolder("Assets", "__PsdPrefabIncrementalMergeTests");
            AssetDatabase.CreateFolder(Folder, "SameName");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void MergeUpdatesRetainedObjectInPlaceAndKeepsBusinessChildAndSerializedReference()
        {
            GameObject source = Root("Root");
            RectTransform retained = Child(source, "Old", "101");
            retained.gameObject.AddComponent<Image>();
            RectTransform business = Child(retained.gameObject, "Business", null);
            PsdPrefabIncrementalReferenceProbe probe = source.AddComponent<PsdPrefabIncrementalReferenceProbe>();
            probe.target = retained.gameObject;
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            UnityEngine.Object.DestroyImmediate(source);
            long retainedId = LocalId(TargetPath, "Old");
            PsdHierarchyProfile profile = Profile(Node("101", retainedId, "Root/Old"));

            GameObject candidate = Root("Root");
            RectTransform candidateLeaf = Child(candidate, "Fresh", "101");
            candidateLeaf.anchoredPosition = new Vector2(37f, -19f);
            Image candidateImage = candidateLeaf.gameObject.AddComponent<Image>();
            candidateImage.color = Color.cyan;
            Texture2D texture = new Texture2D(4, 4);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            candidateImage.sprite = sprite;
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                RectTransform loadedRetained = loaded.transform.Find("Old") as RectTransform;
                RectTransform loadedBusiness = loaded.transform.Find("Old/Business") as RectTransform;
                PsdPrefabIncrementalReferenceProbe loadedProbe = loaded.GetComponent<PsdPrefabIncrementalReferenceProbe>();
                PsdPrefabIncrementalMergeResult result = PsdPrefabIncrementalMerge.Merge(
                    TargetPath, loaded, candidate,
                    new Dictionary<string, RectTransform> { { "101", candidateLeaf } },
                    profile, EmptyPlan());

                RectTransform actual = result.generatedByStableId["101"];
                Assert.That(actual.name, Is.EqualTo("Fresh"));
                Assert.That(actual.anchoredPosition, Is.EqualTo(new Vector2(37f, -19f)));
                Assert.That(actual.GetComponent<Image>().color, Is.EqualTo(Color.cyan));
                Assert.That(actual.GetComponent<Image>().sprite, Is.SameAs(sprite), "Sprite must be copied; a null Sprite renders white.");
                Assert.That(actual.Find("Business"), Is.SameAs(loadedBusiness));
                Assert.That(actual, Is.SameAs(loadedRetained));
                Assert.That(loadedProbe.target, Is.SameAs(actual.gameObject));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(candidate);
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void MergeRetainsMissingGeneratedNodeAsPendingAndBlocksMissingRecordedObject()
        {
            GameObject source = Root("Root");
            Child(source, "Present", "101");
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            UnityEngine.Object.DestroyImmediate(source);
            long localId = LocalId(TargetPath, "Present");
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            GameObject candidate = Root("Root");
            PsdHierarchyProfile profile = Profile(Node("101", localId, "Root/Present"));
            try
            {
                PsdPrefabIncrementalMergeResult result = PsdPrefabIncrementalMerge.Merge(
                    TargetPath, loaded, candidate, new Dictionary<string, RectTransform>(), profile, EmptyPlan());
                Assert.That(result.pendingMissingStableIds, Is.EqualTo(new[] { "101" }));

                profile.nodes[0].localFileId = localId + 9999;
                Assert.Throws<PsdPrefabIncrementalMergeException>(() => PsdPrefabIncrementalMerge.Merge(
                    TargetPath, loaded, candidate, new Dictionary<string, RectTransform>(), profile, EmptyPlan()));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(candidate);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void FirstAdoptionWithoutRecordedNativeIdentityFailsClosed()
        {
            GameObject source = Root("Root");
            Child(source, "Same", "101");
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            UnityEngine.Object.DestroyImmediate(source);
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            GameObject candidate = Root("Root");
            RectTransform candidateLeaf = Child(candidate, "Same", "101");
            PsdHierarchyProfile profile = Profile(Node("101", 0L, "Root/Same"));
            try
            {
                Assert.Throws<PsdPrefabIncrementalMergeException>(() => PsdPrefabIncrementalMerge.Merge(
                    TargetPath, loaded, candidate,
                    new Dictionary<string, RectTransform> { { "101", candidateLeaf } }, profile, EmptyPlan()));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(candidate);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ExplicitPendingCreationAddsNewNativeIdWithoutGuessingAnExistingObject()
        {
            GameObject source = Root("Root");
            Child(source, "Old", "101");
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            UnityEngine.Object.DestroyImmediate(source);
            long oldId = LocalId(TargetPath, "Old");
            PsdHierarchyProfileNode oldRecord = Node("101", oldId, "Root/Old");
            PsdHierarchyProfileNode newRecord = Node("102", 0L, string.Empty);
            newRecord.pendingCreation = true;
            PsdHierarchyProfile profile = Profile(oldRecord, newRecord);
            GameObject candidate = Root("Root");
            RectTransform oldCandidate = Child(candidate, "Old", "101");
            RectTransform newCandidate = Child(candidate, "New", "102");
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                PsdPrefabIncrementalMergeResult result = PsdPrefabIncrementalMerge.Merge(
                    TargetPath, loaded, candidate,
                    new Dictionary<string, RectTransform> { { "101", oldCandidate }, { "102", newCandidate } },
                    profile, EmptyPlan());
                Assert.That(result.generatedByStableId["102"].name, Is.EqualTo("New"));
                Assert.That(result.generatedByStableId["102"].parent, Is.SameAs(loaded.transform));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(candidate);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void SuccessfulTransactionPreservesTargetGuidAndLocalIdAndWritesProfileIdentityAfterPrefabSave()
        {
            CreateTargetAndProfile(out long originalLocalId, out string targetGuid);
            PsdHierarchyProfile working = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(ProfilePath));
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            loaded.transform.Find("Old").name = "Fresh";
            try
            {
                Assert.That(working.nodes[0].localFileId, Is.Zero);
                PsdPrefabTransactionalSave.Save(
                    TargetPath, loaded, ProfilePath, working,
                    new Dictionary<string, RectTransform> { { "101", loaded.transform.Find("Fresh") as RectTransform } },
                    new Dictionary<string, RectTransform>(), new[] { TemporaryPath }, null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(working);
            }

            PsdHierarchyProfile saved = AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(ProfilePath);
            Assert.That(AssetDatabase.AssetPathToGUID(TargetPath), Is.EqualTo(targetGuid));
            Assert.That(LocalId(TargetPath, "Fresh"), Is.EqualTo(originalLocalId));
            Assert.That(saved.nodes[0].localFileId, Is.EqualTo(originalLocalId));
            Assert.That(saved.nodes[0].lastKnownPath, Is.EqualTo("Root/Fresh"));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(TemporaryPath), Is.Null);
            Assert.That(File.Exists(FullPath(TemporaryPath) + ".meta"), Is.False);
        }

        [Test]
        public void RetainedLocalIdKeepsPrefabInstanceOverrideInIsolatedPreviewScene()
        {
            CreateTargetAndProfile(out long originalLocalId, out _);
            PsdHierarchyProfile working = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(ProfilePath));
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath), previewScene);
            Transform instanceLeaf = instance.transform.Find("Old");
            instanceLeaf.localScale = new Vector3(1.7f, 0.8f, 1f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instanceLeaf);
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            loaded.transform.Find("Old").name = "Fresh";
            try
            {
                PsdPrefabTransactionalSave.Save(
                    TargetPath, loaded, ProfilePath, working,
                    new Dictionary<string, RectTransform> { { "101", loaded.transform.Find("Fresh") as RectTransform } },
                    new Dictionary<string, RectTransform>(), Array.Empty<string>(), null);

                Assert.That(LocalId(TargetPath, "Fresh"), Is.EqualTo(originalLocalId));
                Assert.That(instanceLeaf.localScale, Is.EqualTo(new Vector3(1.7f, 0.8f, 1f)));
                Assert.That(PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false), Is.True);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(working);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        [TestCase(PsdPrefabTransactionStage.AfterPrefabSave)]
        [TestCase(PsdPrefabTransactionStage.AfterReimportVerification)]
        [TestCase(PsdPrefabTransactionStage.AfterProfileSave)]
        [TestCase(PsdPrefabTransactionStage.AfterFinalVerification)]
        public void EveryInjectedFailureRestoresPrefabProfileMetaAndLeavesSameNameSiblingUntouched(PsdPrefabTransactionStage failureStage)
        {
            CreateTargetAndProfile(out _, out _);
            GameObject sibling = Root("Sibling Sentinel");
            PrefabUtility.SaveAsPrefabAsset(sibling, SameNamePath);
            UnityEngine.Object.DestroyImmediate(sibling);
            GameObject temporary = Root("Temporary");
            PrefabUtility.SaveAsPrefabAsset(temporary, TemporaryPath);
            UnityEngine.Object.DestroyImmediate(temporary);
            byte[] targetBefore = File.ReadAllBytes(FullPath(TargetPath));
            byte[] targetMetaBefore = File.ReadAllBytes(FullPath(TargetPath) + ".meta");
            byte[] profileBefore = File.ReadAllBytes(FullPath(ProfilePath));
            byte[] profileMetaBefore = File.ReadAllBytes(FullPath(ProfilePath) + ".meta");
            byte[] siblingBefore = File.ReadAllBytes(FullPath(SameNamePath));
            PsdHierarchyProfile working = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(ProfilePath));
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            loaded.transform.Find("Old").name = "Must Roll Back";
            try
            {
                Assert.Throws<InvalidOperationException>(() => PsdPrefabTransactionalSave.Save(
                    TargetPath, loaded, ProfilePath, working,
                    new Dictionary<string, RectTransform> { { "101", loaded.transform.Find("Must Roll Back") as RectTransform } },
                    new Dictionary<string, RectTransform>(), new[] { TemporaryPath },
                    stage => { if (stage == failureStage) throw new InvalidOperationException("injected"); }));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(working);
            }

            Assert.That(File.ReadAllBytes(FullPath(TargetPath)), Is.EqualTo(targetBefore));
            Assert.That(File.ReadAllBytes(FullPath(TargetPath) + ".meta"), Is.EqualTo(targetMetaBefore));
            Assert.That(File.ReadAllBytes(FullPath(ProfilePath)), Is.EqualTo(profileBefore));
            Assert.That(File.ReadAllBytes(FullPath(ProfilePath) + ".meta"), Is.EqualTo(profileMetaBefore));
            Assert.That(File.ReadAllBytes(FullPath(SameNamePath)), Is.EqualTo(siblingBefore));
            Assert.That(File.Exists(FullPath(TemporaryPath)), Is.False);
            Assert.That(File.Exists(FullPath(TemporaryPath) + ".meta"), Is.False);
        }

        private static void CreateTargetAndProfile(out long localId, out string guid)
        {
            GameObject source = Root("Root");
            Child(source, "Old", "101");
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            UnityEngine.Object.DestroyImmediate(source);
            localId = LocalId(TargetPath, "Old");
            guid = AssetDatabase.AssetPathToGUID(TargetPath);
            PsdHierarchyProfile profile = Profile(Node("101", 0L, string.Empty));
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssetIfDirty(profile);
        }

        private static PsdHierarchyProfile Profile(params PsdHierarchyProfileNode[] nodes)
        {
            PsdHierarchyProfile value = ScriptableObject.CreateInstance<PsdHierarchyProfile>();
            value.sourcePsdGuid = "source-guid";
            value.nodes = new List<PsdHierarchyProfileNode>(nodes);
            return value;
        }

        private static PsdHierarchyProfileNode Node(string stableId, long localFileId, string path)
        {
            return new PsdHierarchyProfileNode { stableId = stableId, localFileId = localFileId, lastKnownPath = path };
        }

        private static PsdHierarchyPlan EmptyPlan()
        {
            return new PsdHierarchyPlan { schemaVersion = PsdHierarchyPlan.CurrentSchemaVersion };
        }

        private static GameObject Root(string name)
        {
            return new GameObject(name, typeof(RectTransform));
        }

        private static RectTransform Child(GameObject parent, string name, string stableId)
        {
            RectTransform value = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            value.SetParent(parent.transform, false);
            value.sizeDelta = new Vector2(100f, 50f);
            return value;
        }

        private static long LocalId(string assetPath, string objectName)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            Transform target = root.name == objectName ? root.transform : root.transform.Find(objectName);
            Assert.That(target, Is.Not.Null, "Expected Prefab object was not found: " + objectName);
            string guid;
            long localId;
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target.gameObject, out guid, out localId), Is.True);
            return localId;
        }

        private static string FullPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }
    }

    public sealed class PsdPrefabIncrementalReferenceProbe : MonoBehaviour
    {
        public GameObject target;
    }
}
