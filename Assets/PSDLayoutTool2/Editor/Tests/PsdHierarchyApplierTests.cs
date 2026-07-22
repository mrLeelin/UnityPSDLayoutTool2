namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using TMPro;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    public sealed class PsdHierarchyApplierTests
    {
        private GameObject rootObject;
        private RectTransform root;

        [SetUp]
        public void SetUp()
        {
            rootObject = new GameObject("Root", typeof(RectTransform));
            root = rootObject.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(1000f, 600f);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(rootObject);
        }

        [Test]
        public void ApplyReusesStableGroupsAndSupportsNestedGroupsAndRenames()
        {
            RectTransform first = Leaf("First", 0, new Vector2(-100f, 20f));
            RectTransform second = Leaf("Second", 1, new Vector2(10f, 20f));
            RectTransform third = Leaf("Third", 2, new Vector2(120f, 20f));
            Dictionary<string, RectTransform> registry = Registry(first, second, third);
            PsdHierarchyPlan plan = Plan(
                Group("outer", "", "Outer", "101"),
                Group("inner", "outer", "Inner", "102", "103"));
            plan.renames.Add(new PsdHierarchyPlanRename { stableId = "101", name = "Renamed" });

            PsdHierarchyApplier.Apply(root, plan, registry);
            PsdHierarchyApplier.Apply(root, plan, registry);

            Assert.That(first.name, Is.EqualTo("Renamed"));
            Assert.That(root.Find("Outer"), Is.Not.Null);
            Assert.That(root.Find("Outer/Inner"), Is.Not.Null);
            Assert.That(CountNamed(root, "Outer"), Is.EqualTo(1));
            Assert.That(CountNamed(root, "Inner"), Is.EqualTo(1));
        }

        [Test]
        public void ApplyPreservesGeometryVisualStateComponentOrderReferencesAndLeafOrder()
        {
            RectTransform first = Leaf("First", 0, new Vector2(-120f, 35f));
            RectTransform second = Leaf("Second", 1, new Vector2(45f, -20f));
            first.anchorMin = new Vector2(0.2f, 0.3f);
            first.anchorMax = new Vector2(0.2f, 0.3f);
            first.pivot = new Vector2(0.1f, 0.9f);
            first.sizeDelta = new Vector2(91f, 47f);
            first.localRotation = Quaternion.Euler(0f, 0f, 7f);
            first.localScale = new Vector3(1.2f, 0.8f, 1f);
            Image image = first.gameObject.AddComponent<Image>();
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
            Material material = new Material(Shader.Find("UI/Default"));
            image.material = material;
            TextMeshProUGUI text = second.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = "kept";
            text.fontMaterial = material;
            PsdHierarchyReferenceProbe probe = rootObject.AddComponent<PsdHierarchyReferenceProbe>();
            probe.target = first.gameObject;

            Dictionary<string, RectTransform> registry = Registry(first, second);
            PsdHierarchyApplySnapshot before = PsdHierarchyApplyVerifier.Capture(root, registry);
            PsdHierarchyApplier.Apply(root, Plan(Group("visuals", "", "Visuals", "101", "102")), registry);

            Assert.DoesNotThrow(() => PsdHierarchyApplyVerifier.VerifyUnchanged(before, root, registry));
            Assert.That(probe.target, Is.SameAs(first.gameObject));
            Assert.That(LeafNames(root), Is.EqualTo(new[] { "First", "Second" }));
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(image.fillCenter, Is.False);
            Assert.That(image.material, Is.SameAs(material));
            Assert.That(text.fontMaterial, Is.SameAs(material));
            UnityEngine.Object.DestroyImmediate(material);
        }

        [TestCase(typeof(Canvas))]
        [TestCase(typeof(Mask))]
        [TestCase(typeof(RectMask2D))]
        [TestCase(typeof(Button))]
        [TestCase(typeof(Animator))]
        public void ApplyRefusesProtectedUnityBoundaries(Type componentType)
        {
            RectTransform first = Leaf("First", 0, Vector2.zero);
            first.gameObject.AddComponent(componentType);

            Assert.Throws<PsdHierarchyApplyException>(() =>
                PsdHierarchyApplier.Apply(root, Plan(Group("g", "", "Group", "101")), Registry(first)));
        }

        [Test]
        public void ApplyRefusesProjectComponents()
        {
            RectTransform first = Leaf("First", 0, Vector2.zero);
            first.gameObject.AddComponent<PsdHierarchyReferenceProbe>();

            Assert.Throws<PsdHierarchyApplyException>(() =>
                PsdHierarchyApplier.Apply(root, Plan(Group("g", "", "Group", "101")), Registry(first)));
        }

        [Test]
        public void ApplyRefusesNestedPrefabMembers()
        {
            const string folder = "Assets/__PsdHierarchyApplierTests";
            const string path = folder + "/Nested.prefab";
            AssetDatabase.CreateFolder("Assets", "__PsdHierarchyApplierTests");
            GameObject source = new GameObject("Nested", typeof(RectTransform));
            PrefabUtility.SaveAsPrefabAsset(source, path);
            UnityEngine.Object.DestroyImmediate(source);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(root, false);
            try
            {
                Assert.Throws<PsdHierarchyApplyException>(() =>
                    PsdHierarchyApplier.Apply(root, Plan(Group("g", "", "Group", "101")),
                        new Dictionary<string, RectTransform> { { "101", instance.GetComponent<RectTransform>() } }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private RectTransform Leaf(string name, int sibling, Vector2 position)
        {
            GameObject item = new GameObject(name, typeof(RectTransform));
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.SetSiblingIndex(sibling);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(100f, 50f);
            return rect;
        }

        private static Dictionary<string, RectTransform> Registry(params RectTransform[] values)
        {
            var result = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            for (int index = 0; index < values.Length; index++)
            {
                result.Add((101 + index).ToString(), values[index]);
            }
            return result;
        }

        private static PsdHierarchyPlan Plan(params PsdHierarchyPlanGroup[] groups)
        {
            var plan = new PsdHierarchyPlan { schemaVersion = PsdHierarchyPlan.CurrentSchemaVersion };
            plan.groups.AddRange(groups);
            return plan;
        }

        private static PsdHierarchyPlanGroup Group(string key, string parent, string name, params string[] members)
        {
            return new PsdHierarchyPlanGroup
            {
                key = key,
                parentKey = parent,
                displayName = name,
                memberStableIds = new List<string>(members)
            };
        }

        private static int CountNamed(Transform transform, string name)
        {
            int count = transform.name == name ? 1 : 0;
            for (int index = 0; index < transform.childCount; index++) count += CountNamed(transform.GetChild(index), name);
            return count;
        }

        private static string[] LeafNames(Transform transform)
        {
            var result = new List<string>();
            CollectLeaves(transform, result);
            return result.ToArray();
        }

        private static void CollectLeaves(Transform transform, List<string> names)
        {
            for (int index = 0; index < transform.childCount; index++)
            {
                Transform child = transform.GetChild(index);
                if (child.childCount == 0) names.Add(child.name); else CollectLeaves(child, names);
            }
        }
    }

    public sealed class PsdHierarchyReferenceProbe : MonoBehaviour
    {
        public GameObject target;
    }
}
