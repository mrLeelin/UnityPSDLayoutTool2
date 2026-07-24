namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
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
            PsdImporter.EndGeneratedUiNodeRegistry();
            if (rootObject != null)
            {
                Transform top = rootObject.transform.root;
                UnityEngine.Object.DestroyImmediate(top.gameObject);
            }
        }

        [Test]
        public void SameKeyReusesSameObjectAcrossDisplayRenameAndSupportsNestedGroups()
        {
            RectTransform first = Leaf("First", 0, new Vector2(-100f, 20f));
            RectTransform second = Leaf("Second", 1, new Vector2(10f, 20f));
            RectTransform third = Leaf("Third", 2, new Vector2(120f, 20f));
            Dictionary<string, RectTransform> registry = Registry(first, second, third);
            PsdHierarchyPlan initial = Plan(
                Group("outer", "", "Outer", "101"),
                Group("inner", "outer", "Inner", "102", "103"));

            PsdHierarchyApplyResult firstApply = PsdHierarchyApplier.Apply(root, initial, registry, EmptyGroups());
            RectTransform outer = firstApply.groupsByKey["outer"];
            RectTransform inner = firstApply.groupsByKey["inner"];
            PsdHierarchyPlan renamed = Plan(
                Group("outer", "", "Outer Renamed", "101"),
                Group("inner", "outer", "Inner Renamed", "102", "103"));
            renamed.renames.Add(new PsdHierarchyPlanRename { stableId = "101", name = "Leaf Renamed" });

            PsdHierarchyApplyResult secondApply = PsdHierarchyApplier.Apply(root, renamed, registry, firstApply.groupsByKey);

            Assert.That(secondApply.groupsByKey["outer"], Is.SameAs(outer));
            Assert.That(secondApply.groupsByKey["inner"], Is.SameAs(inner));
            Assert.That(secondApply.createdGroupKeys, Is.Empty);
            Assert.That(outer.name, Is.EqualTo("Outer Renamed"));
            Assert.That(inner.name, Is.EqualTo("Inner Renamed"));
            Assert.That(first.name, Is.EqualTo("Leaf Renamed"));
        }

        [Test]
        public void SameDisplayNameProjectRectIsNeverClaimedWithoutExplicitKeyMapping()
        {
            RectTransform projectRect = new GameObject("Visuals", typeof(RectTransform)).GetComponent<RectTransform>();
            projectRect.SetParent(root, false);
            RectTransform leaf = Leaf("Leaf", 1, Vector2.zero);

            PsdHierarchyApplyResult result = PsdHierarchyApplier.Apply(
                root, Plan(Group("visuals", "", "Visuals", "101")), Registry(leaf), EmptyGroups());

            Assert.That(result.groupsByKey["visuals"], Is.Not.SameAs(projectRect));
            Assert.That(projectRect.parent, Is.SameAs(root));
            Assert.That(root.GetComponentsInChildren<RectTransform>(true).Count(value => value.name == "Visuals"), Is.EqualTo(2));
        }

        [Test]
        public void ChildFirstResolutionReusesExistingInnerAndWrapsItWithDirectOuterMember()
        {
            RectTransform inner = new GameObject("Inner Old", typeof(RectTransform)).GetComponent<RectTransform>();
            inner.SetParent(root, false);
            ConfigureIdentityGroup(inner);
            RectTransform first = LeafUnder(inner, "A", 0, new Vector2(-80f, 10f));
            RectTransform second = LeafUnder(inner, "B", 1, new Vector2(10f, 10f));
            RectTransform third = Leaf("C", 1, new Vector2(100f, 10f));
            first.gameObject.AddComponent<Image>();
            second.gameObject.AddComponent<Image>();
            third.gameObject.AddComponent<Image>();
            Dictionary<string, RectTransform> registry = Registry(first, second, third);
            PsdHierarchyPlan plan = Plan(
                Group("outer", "", "Outer", "103"),
                Group("inner", "outer", "Inner", "101", "102"));
            var existing = new Dictionary<string, RectTransform> { { "inner", inner } };

            PsdHierarchyApplyResult result = PsdHierarchyApplier.Apply(root, plan, registry, existing);

            Assert.That(result.groupsByKey["inner"], Is.SameAs(inner));
            Assert.That(result.createdGroupKeys, Is.EquivalentTo(new[] { "outer" }));
            RectTransform outer = result.groupsByKey["outer"];
            Assert.That(inner.parent, Is.SameAs(outer));
            Assert.That(third.parent, Is.SameAs(outer));
            Assert.That(first.parent, Is.SameAs(inner));
            Assert.That(second.parent, Is.SameAs(inner));
            Assert.That(outer.parent, Is.SameAs(root));
        }

        [Test]
        public void ParentGroupMayContainOnlyAnExistingChildGroup()
        {
            RectTransform inner = new GameObject("Inner", typeof(RectTransform)).GetComponent<RectTransform>();
            inner.SetParent(root, false);
            ConfigureIdentityGroup(inner);
            RectTransform first = LeafUnder(inner, "A", 0, Vector2.zero);
            first.gameObject.AddComponent<Image>();
            PsdHierarchyPlan plan = Plan(
                Group("outer", "", "Outer"),
                Group("inner", "outer", "Inner", "101"));

            PsdHierarchyApplyResult result = PsdHierarchyApplier.Apply(
                root, plan, Registry(first), new Dictionary<string, RectTransform> { { "inner", inner } });

            Assert.That(result.groupsByKey["inner"], Is.SameAs(inner));
            Assert.That(inner.parent, Is.SameAs(result.groupsByKey["outer"]));
            Assert.That(result.groupsByKey["outer"].parent, Is.SameAs(root));
        }

        [Test]
        public void CycleIsRejectedBeforeAnyHierarchyMutation()
        {
            RectTransform first = Leaf("A", 0, Vector2.zero);
            RectTransform second = Leaf("B", 1, Vector2.zero);
            string before = GraphSignature(root);
            PsdHierarchyPlan cycle = Plan(
                Group("outer", "inner", "Outer", "101"),
                Group("inner", "outer", "Inner", "102"));

            Assert.Throws<PsdHierarchyApplyException>(() =>
                PsdHierarchyApplier.Apply(root, cycle, Registry(first, second), EmptyGroups()));

            Assert.That(GraphSignature(root), Is.EqualTo(before));
        }

        [Test]
        public void UnknownParentIsRejectedBeforeAnyHierarchyMutation()
        {
            RectTransform first = Leaf("A", 0, Vector2.zero);
            string before = GraphSignature(root);
            PsdHierarchyPlan invalid = Plan(Group("inner", "missing", "Inner", "101"));

            Assert.Throws<PsdHierarchyApplyException>(() =>
                PsdHierarchyApplier.Apply(root, invalid, Registry(first), EmptyGroups()));

            Assert.That(GraphSignature(root), Is.EqualTo(before));
        }

        [Test]
        public void ExistingChildGroupCanBePromotedToLogicalRootWhenOldParentDisappears()
        {
            RectTransform outer = OwnedGroup("Outer", root, 0);
            RectTransform inner = OwnedGroup("Inner", outer, 0);
            RectTransform leaf = LeafUnder(inner, "A", 0, Vector2.zero);
            leaf.gameObject.AddComponent<Image>();
            var oldGroups = new Dictionary<string, RectTransform>
            {
                { "outer", outer },
                { "inner", inner }
            };

            PsdHierarchyApplyResult result = PsdHierarchyApplier.Apply(
                root, Plan(Group("inner", "", "Inner", "101")), Registry(leaf), oldGroups);

            Assert.That(result.groupsByKey["inner"], Is.SameAs(inner));
            Assert.That(inner.parent, Is.SameAs(root));
            Assert.That(outer == null, Is.True);
            Assert.That(leaf.parent, Is.SameAs(inner));
        }

        [Test]
        public void RemovingOldGroupUngroupsAllMembersAndDestroysOnlyTheContainer()
        {
            RectTransform old = OwnedGroup("Old", root, 0);
            RectTransform first = LeafUnder(old, "A", 0, new Vector2(-20f, 0f));
            RectTransform second = LeafUnder(old, "B", 1, new Vector2(20f, 0f));
            first.gameObject.AddComponent<Image>();
            second.gameObject.AddComponent<Image>();

            PsdHierarchyApplyResult result = PsdHierarchyApplier.Apply(
                root, Plan(), Registry(first, second), new Dictionary<string, RectTransform> { { "old", old } });

            Assert.That(result.groupsByKey, Is.Empty);
            Assert.That(first.parent, Is.SameAs(root));
            Assert.That(second.parent, Is.SameAs(root));
            Assert.That(old == null, Is.True);
        }

        [Test]
        public void RemovingNestedOldGroupsPromotesAllLeavesAndProjectChildThenDestroysShellsDeepestFirst()
        {
            RectTransform outer = OwnedGroup("Outer", root, 0);
            RectTransform inner = OwnedGroup("Inner", outer, 0);
            RectTransform first = LeafUnder(inner, "A", 0, new Vector2(-20f, 0f));
            RectTransform second = LeafUnder(inner, "B", 1, new Vector2(20f, 0f));
            first.gameObject.AddComponent<Image>();
            second.gameObject.AddComponent<Image>();
            RectTransform project = LeafUnder(inner, "Business", 2, new Vector2(40f, 0f));
            project.gameObject.AddComponent<Image>();
            PsdHierarchyReferenceProbe probe = project.gameObject.AddComponent<PsdHierarchyReferenceProbe>();
            probe.target = first.gameObject;
            var oldGroups = new Dictionary<string, RectTransform> { { "outer", outer }, { "inner", inner } };

            PsdHierarchyApplier.Apply(root, Plan(), Registry(first, second), oldGroups);

            Assert.That(first.parent, Is.SameAs(root));
            Assert.That(second.parent, Is.SameAs(root));
            Assert.That(project.parent, Is.SameAs(root));
            Assert.That(probe.target, Is.SameAs(first.gameObject));
            Assert.That(inner == null, Is.True);
            Assert.That(outer == null, Is.True);
        }

        [Test]
        public void VerifierFailureWhileRemovingNestedShellsRestoresBothGroupsAndFullHierarchy()
        {
            RectTransform outer = OwnedGroup("Outer", root, 0);
            RectTransform inner = OwnedGroup("Inner", outer, 0);
            RectTransform first = LeafUnder(inner, "A", 0, Vector2.zero);
            first.gameObject.AddComponent<Image>();
            RectTransform project = LeafUnder(inner, "Business", 1, new Vector2(30f, 0f));
            project.gameObject.AddComponent<Image>();
            string before = GraphSignature(root);

            Assert.Throws<PsdHierarchyApplyException>(() => PsdHierarchyApplier.Apply(
                root,
                Plan(),
                Registry(first),
                new Dictionary<string, RectTransform> { { "outer", outer }, { "inner", inner } },
                stage =>
                {
                    if (stage == PsdHierarchyApplyStage.BeforeVerification) project.SetSiblingIndex(0);
                }));

            Assert.That(outer == null, Is.False);
            Assert.That(inner == null, Is.False);
            Assert.That(GraphSignature(root), Is.EqualTo(before));
            Assert.That(inner.parent, Is.SameAs(outer));
            Assert.That(first.parent, Is.SameAs(inner));
            Assert.That(project.parent, Is.SameAs(inner));
        }

        [Test]
        public void UnnestingExistingInnerGroupRestoresSiblingByMinimumDescendantVisualRank()
        {
            RectTransform outer = OwnedGroup("Outer", root, 0);
            RectTransform inner = OwnedGroup("Inner", outer, 0);
            RectTransform first = LeafUnder(inner, "A", 0, Vector2.zero);
            RectTransform second = LeafUnder(outer, "C", 1, new Vector2(20f, 0f));
            RectTransform other = Leaf("Other", 1, new Vector2(40f, 0f));
            first.gameObject.AddComponent<Image>();
            second.gameObject.AddComponent<Image>();
            other.gameObject.AddComponent<Image>();
            var oldGroups = new Dictionary<string, RectTransform> { { "outer", outer }, { "inner", inner } };
            PsdHierarchyPlan plan = Plan(
                Group("outer", "", "Outer", "102"),
                Group("inner", "", "Inner", "101"));

            PsdHierarchyApplier.Apply(root, plan, Registry(first, second), oldGroups);

            Assert.That(inner.parent, Is.SameAs(root));
            Assert.That(outer.parent, Is.SameAs(root));
            Assert.That(inner.GetSiblingIndex(), Is.LessThan(outer.GetSiblingIndex()));
            Assert.That(outer.GetSiblingIndex(), Is.LessThan(other.GetSiblingIndex()));
        }

        [Test]
        public void SerializedReferenceToDisappearedGroupFailsClosedAndRestoresHierarchy()
        {
            RectTransform old = OwnedGroup("Old", root, 0);
            RectTransform leaf = LeafUnder(old, "A", 0, Vector2.zero);
            leaf.gameObject.AddComponent<Image>();
            PsdHierarchyReferenceProbe probe = rootObject.AddComponent<PsdHierarchyReferenceProbe>();
            probe.target = leaf.gameObject;
            probe.rectTarget = old;
            string before = GraphSignature(root);

            Assert.Throws<PsdHierarchyApplyException>(() => PsdHierarchyApplier.Apply(
                root, Plan(), Registry(leaf), new Dictionary<string, RectTransform> { { "old", old } }));

            Assert.That(GraphSignature(root), Is.EqualTo(before));
            Assert.That(old == null, Is.False);
            Assert.That(leaf.parent, Is.SameAs(old));
            Assert.That(probe.target, Is.SameAs(leaf.gameObject));
            Assert.That(probe.rectTarget, Is.SameAs(old));
        }

        [TestCase("hidden")]
        [TestCase("list")]
        [TestCase("array")]
        [TestCase("nested")]
        public void DeepSerializedReferenceShapesFailClosedAndLeafReferencesRemainAllowed(string referenceShape)
        {
            RectTransform old = OwnedGroup("Old", root, 0);
            RectTransform leaf = LeafUnder(old, "A", 0, Vector2.zero);
            leaf.gameObject.AddComponent<Image>();
            PsdHierarchyDeepReferenceProbe probe = rootObject.AddComponent<PsdHierarchyDeepReferenceProbe>();

            // These leaf references are deliberately legal and prove the scan
            // rejects only the disappearing generated container target set.
            probe.allowedLeaf = leaf;
            probe.rectTargets.Add(leaf);
            probe.gameObjectTargets = new[] { leaf.gameObject };
            probe.nested.allowedLeaf = leaf.gameObject;
            switch (referenceShape)
            {
                case "hidden":
                    probe.hiddenRectTarget = old;
                    break;
                case "list":
                    probe.rectTargets.Add(old);
                    break;
                case "array":
                    probe.gameObjectTargets = new[] { leaf.gameObject, old.gameObject };
                    break;
                case "nested":
                    probe.nested.target = old.gameObject;
                    break;
            }
            string before = GraphSignature(root);

            Assert.Throws<PsdHierarchyApplyException>(() => PsdHierarchyApplier.Apply(
                root, Plan(), Registry(leaf), new Dictionary<string, RectTransform> { { "old", old } }));

            Assert.That(GraphSignature(root), Is.EqualTo(before));
            Assert.That(old == null, Is.False);
            Assert.That(leaf.parent, Is.SameAs(old));
            Assert.That(probe.allowedLeaf, Is.SameAs(leaf));
            Assert.That(probe.nested.allowedLeaf, Is.SameAs(leaf.gameObject));
        }

        [Test]
        public void MemberRemovedFromReusedGroupIsPromotedWithoutAffectingRemainingMember()
        {
            RectTransform old = OwnedGroup("Old", root, 0);
            RectTransform first = LeafUnder(old, "A", 0, new Vector2(-20f, 0f));
            RectTransform second = LeafUnder(old, "B", 1, new Vector2(20f, 0f));
            first.gameObject.AddComponent<Image>();
            second.gameObject.AddComponent<Image>();

            PsdHierarchyApplier.Apply(
                root,
                Plan(Group("old", "", "Old", "101")),
                Registry(first, second),
                new Dictionary<string, RectTransform> { { "old", old } });

            Assert.That(first.parent, Is.SameAs(old));
            Assert.That(second.parent, Is.SameAs(root));
        }

        [Test]
        public void ProjectOwnedChildInRemovedGeneratedGroupIsSafelyPromoted()
        {
            RectTransform old = OwnedGroup("Old", root, 0);
            RectTransform leaf = LeafUnder(old, "A", 0, Vector2.zero);
            leaf.gameObject.AddComponent<Image>();
            RectTransform projectChild = LeafUnder(old, "Business", 1, new Vector2(30f, 0f));
            projectChild.gameObject.AddComponent<Image>();
            PsdHierarchyReferenceProbe probe = projectChild.gameObject.AddComponent<PsdHierarchyReferenceProbe>();
            probe.target = leaf.gameObject;

            PsdHierarchyApplier.Apply(
                root, Plan(), Registry(leaf), new Dictionary<string, RectTransform> { { "old", old } });

            Assert.That(projectChild.parent, Is.SameAs(root));
            Assert.That(probe.target, Is.SameAs(leaf.gameObject));
            Assert.That(old == null, Is.True);
        }

        [TestCase(PsdHierarchyApplyStage.MemberMoved)]
        [TestCase(PsdHierarchyApplyStage.BeforeVerification)]
        public void FailureWhileRemovingOldGroupRestoresContainerChildrenAndOrdering(PsdHierarchyApplyStage failureStage)
        {
            RectTransform old = OwnedGroup("Old", root, 0);
            RectTransform leaf = LeafUnder(old, "A", 0, Vector2.zero);
            leaf.gameObject.AddComponent<Image>();
            RectTransform business = LeafUnder(old, "Business", 1, new Vector2(20f, 0f));
            business.gameObject.AddComponent<Image>();
            string before = GraphSignature(root);
            int injected = 0;

            Assert.That(() => PsdHierarchyApplier.Apply(
                root,
                Plan(),
                Registry(leaf),
                new Dictionary<string, RectTransform> { { "old", old } },
                stage =>
                {
                    if (stage != failureStage || injected++ != 0) return;
                    if (stage == PsdHierarchyApplyStage.BeforeVerification)
                    {
                        business.SetSiblingIndex(0);
                        return;
                    }
                    throw new InvalidOperationException("injected removed-group failure");
                }), Throws.InstanceOf<InvalidOperationException>());

            Assert.That(old == null, Is.False);
            Assert.That(GraphSignature(root), Is.EqualTo(before));
            Assert.That(leaf.parent, Is.SameAs(old));
            Assert.That(business.parent, Is.SameAs(old));
        }

        [Test]
        public void ApplyPreservesFullRectVisualComponentsMaterialsNineSliceReferencesAndOrder()
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            root.SetParent(canvasObject.transform, false);
            root.localRotation = Quaternion.Euler(0f, 0f, 13f);
            root.localScale = new Vector3(0.7f, 1.3f, 1f);
            RectTransform first = Leaf("First", 0, new Vector2(-120f, 35f));
            RectTransform second = Leaf("Second", 1, new Vector2(45f, -20f));
            first.anchorMin = new Vector2(0.2f, 0.3f);
            first.anchorMax = new Vector2(0.8f, 0.75f);
            first.pivot = new Vector2(0.1f, 0.9f);
            first.offsetMin = new Vector2(17f, 19f);
            first.offsetMax = new Vector2(-23f, -29f);
            first.anchoredPosition3D = new Vector3(first.anchoredPosition.x, first.anchoredPosition.y, 6f);
            first.localRotation = Quaternion.Euler(0f, 0f, 7f);
            first.localScale = new Vector3(1.2f, 0.8f, 1f);
            first.gameObject.SetActive(false);

            Texture2D texture = new Texture2D(10, 10);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 10, 10), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(1f, 2f, 3f, 4f));
            Material imageMaterial = new Material(Shader.Find("UI/Default"));
            Image image = first.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillMethod = Image.FillMethod.Radial180;
            image.fillAmount = 0.42f;
            image.fillCenter = false;
            image.preserveAspect = false;
            image.material = imageMaterial;
            TextMeshProUGUI text = second.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = "kept";
            Material sharedTextMaterial = new Material(Shader.Find("UI/Default"));
            text.fontSharedMaterial = sharedTextMaterial;
            PsdHierarchyReferenceProbe probe = rootObject.AddComponent<PsdHierarchyReferenceProbe>();
            probe.target = first.gameObject;

            Dictionary<string, RectTransform> registry = Registry(first, second);
            PsdHierarchyApplySnapshot before = PsdHierarchyApplyVerifier.Capture(root, registry);
            Component[] firstComponents = first.GetComponents<Component>();
            PsdHierarchyApplier.Apply(root, Plan(Group("visuals", "", "Visuals", "101", "102")), registry, EmptyGroups());

            Assert.DoesNotThrow(() => PsdHierarchyApplyVerifier.VerifyUnchanged(before, root, registry));
            Assert.That(probe.target, Is.SameAs(first.gameObject));
            Assert.That(first.GetComponents<Component>(), Is.EqualTo(firstComponents));
            Assert.That(image.sprite.border, Is.EqualTo(new Vector4(1f, 2f, 3f, 4f)));
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(image.fillMethod, Is.EqualTo(Image.FillMethod.Radial180));
            Assert.That(image.fillAmount, Is.EqualTo(0.42f));
            Assert.That(image.fillCenter, Is.False);
            Assert.That(image.preserveAspect, Is.False);
            Assert.That(image.material, Is.SameAs(imageMaterial));
            Assert.That(text.fontSharedMaterial, Is.SameAs(sharedTextMaterial));
            Assert.That(first.gameObject.activeSelf, Is.False);
            UnityEngine.Object.DestroyImmediate(imageMaterial);
            UnityEngine.Object.DestroyImmediate(sharedTextMaterial);
            UnityEngine.Object.DestroyImmediate(sprite);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        [Test]
        public void ApplyAllowsDisjointNonContiguousVisualLeavesToBecomeSemanticGroup()
        {
            RectTransform first = Leaf("First", 0, Vector2.zero);
            RectTransform unrelated = Leaf("Unrelated", 1, new Vector2(300f, 0f));
            RectTransform last = Leaf("Last", 2, Vector2.zero);
            first.gameObject.AddComponent<Image>();
            unrelated.gameObject.AddComponent<Image>();
            last.gameObject.AddComponent<Image>();
            Dictionary<string, RectTransform> registry = Registry(first, unrelated, last);

            Assert.DoesNotThrow(() => PsdHierarchyApplier.Apply(
                root,
                Plan(Group("semantic-card", "", "Semantic Card", "101", "103")),
                registry,
                EmptyGroups()));
            Assert.That(first.parent, Is.SameAs(last.parent));
            Assert.That(unrelated.parent, Is.SameAs(root));
        }

        [Test]
        public void ApplyRejectsOverlappingNonContiguousVisualLeaves()
        {
            RectTransform first = Leaf("First", 0, Vector2.zero);
            RectTransform overlapping = Leaf("Overlapping", 1, Vector2.zero);
            RectTransform last = Leaf("Last", 2, Vector2.zero);
            first.gameObject.AddComponent<Image>();
            overlapping.gameObject.AddComponent<Image>();
            last.gameObject.AddComponent<Image>();
            Dictionary<string, RectTransform> registry = Registry(first, overlapping, last);

            Assert.Throws<PsdHierarchyApplyException>(() => PsdHierarchyApplier.Apply(
                root,
                Plan(Group("unsafe-card", "", "Unsafe Card", "101", "103")),
                registry,
                EmptyGroups()));
            Assert.That(first.parent, Is.SameAs(root));
            Assert.That(overlapping.parent, Is.SameAs(root));
            Assert.That(last.parent, Is.SameAs(root));
        }

        [Test]
        public void ApplyRejectsReorderWhenCrossedVisualHasNoRectTransformGeometry()
        {
            RectTransform first = Leaf("First", 0, Vector2.zero);
            var spriteObject = new GameObject("Sprite", typeof(SpriteRenderer));
            spriteObject.transform.SetParent(root, false);
            spriteObject.transform.SetSiblingIndex(1);
            RectTransform last = Leaf("Last", 2, Vector2.zero);
            first.gameObject.AddComponent<Image>();
            last.gameObject.AddComponent<Image>();
            Dictionary<string, RectTransform> registry = Registry(first, last);

            Assert.Throws<PsdHierarchyApplyException>(() => PsdHierarchyApplier.Apply(
                root,
                Plan(Group("semantic-card", "", "Semantic Card", "101", "102")),
                registry,
                EmptyGroups()));
            Assert.That(first.parent, Is.SameAs(root));
            Assert.That(spriteObject.transform.parent, Is.SameAs(root));
            Assert.That(last.parent, Is.SameAs(root));
        }

        [Test]
        public void VerifierIncludesZeroIdAndProjectOwnedVisuals()
        {
            RectTransform generated = Leaf("Generated", 0, Vector2.zero);
            generated.gameObject.AddComponent<Image>();
            RectTransform zeroId = Leaf("Zero", 1, Vector2.zero);
            zeroId.gameObject.AddComponent<Image>();
            Dictionary<string, RectTransform> registry = Registry(generated);
            PsdHierarchyApplySnapshot before = PsdHierarchyApplyVerifier.Capture(root, registry);
            RectTransform foreignParent = new GameObject("Foreign", typeof(RectTransform)).GetComponent<RectTransform>();
            foreignParent.SetParent(root, false);
            zeroId.SetParent(foreignParent, false);

            Assert.Throws<PsdHierarchyApplyException>(() => PsdHierarchyApplyVerifier.VerifyUnchanged(before, root, registry));
        }

        [TestCase(PsdHierarchyApplyStage.MemberMoved)]
        [TestCase(PsdHierarchyApplyStage.BeforeVerification)]
        public void AnyMidApplyOrVerifierFailureRollsBackCompleteGraph(PsdHierarchyApplyStage failureStage)
        {
            RectTransform first = Leaf("First", 0, Vector2.zero);
            first.gameObject.AddComponent<Image>();
            RectTransform zeroId = Leaf("Zero", 1, Vector2.zero);
            zeroId.gameObject.AddComponent<Image>();
            string before = GraphSignature(root);
            int injected = 0;

            Assert.That(() => PsdHierarchyApplier.Apply(
                root,
                Plan(Group("g", "", "Changed", "101")),
                Registry(first),
                EmptyGroups(),
                stage =>
                {
                    if (stage != failureStage || injected++ != 0) return;
                    if (stage == PsdHierarchyApplyStage.BeforeVerification)
                    {
                        zeroId.SetSiblingIndex(0);
                        return;
                    }
                    throw new InvalidOperationException("injected middle failure");
                }), Throws.InstanceOf<InvalidOperationException>());

            Assert.That(GraphSignature(root), Is.EqualTo(before));
            Assert.That(root.GetComponentsInChildren<RectTransform>(true).Any(value => value.name == "Changed"), Is.False);
        }

        [Test]
        public void RegistryEnforcesDurableOneToOneIdentityAndLifecycleClear()
        {
            RectTransform first = Leaf("First", 0, Vector2.zero);
            RectTransform second = Leaf("Second", 1, Vector2.zero);
            PsdImporter.BeginGeneratedUiNodeRegistry(true);
            PsdImporter.RegisterGeneratedUiNode(0U, first);
            Assert.That(PsdImporter.CaptureGeneratedUiNodeRegistry(), Is.Empty);
            PsdImporter.RegisterGeneratedUiNode(101U, first);
            Assert.Throws<InvalidOperationException>(() => PsdImporter.RegisterGeneratedUiNode(101U, second));
            Assert.Throws<InvalidOperationException>(() => PsdImporter.RegisterGeneratedUiNode(102U, first));
            PsdImporter.EndGeneratedUiNodeRegistry();
            Assert.That(PsdImporter.CaptureGeneratedUiNodeRegistry(), Is.Empty);
        }

        [Test]
        public void RegistryRejectsDifferentStableIdsPointingToSameLeaf()
        {
            RectTransform first = Leaf("First", 0, Vector2.zero);
            var invalid = new Dictionary<string, RectTransform> { { "101", first }, { "102", first } };

            Assert.Throws<PsdHierarchyApplyException>(() => PsdHierarchyApplier.Apply(
                root, Plan(Group("g", "", "Group", "101")), invalid, EmptyGroups()));
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
            Assert.Throws<PsdHierarchyApplyException>(() => PsdHierarchyApplier.Apply(
                root, Plan(Group("g", "", "Group", "101")), Registry(first), EmptyGroups()));
        }

        [Test]
        public void ApplyRefusesProjectComponents()
        {
            RectTransform first = Leaf("First", 0, Vector2.zero);
            first.gameObject.AddComponent<PsdHierarchyReferenceProbe>();
            Assert.Throws<PsdHierarchyApplyException>(() => PsdHierarchyApplier.Apply(
                root, Plan(Group("g", "", "Group", "101")), Registry(first), EmptyGroups()));
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
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            instance.transform.SetParent(root, false);
            try
            {
                Assert.Throws<PsdHierarchyApplyException>(() => PsdHierarchyApplier.Apply(
                    root, Plan(Group("g", "", "Group", "101")),
                    new Dictionary<string, RectTransform> { { "101", instance.GetComponent<RectTransform>() } }, EmptyGroups()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private RectTransform Leaf(string name, int sibling, Vector2 position)
        {
            return LeafUnder(root, name, sibling, position);
        }

        private static RectTransform LeafUnder(RectTransform parent, string name, int sibling, Vector2 position)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.SetSiblingIndex(sibling);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(100f, 50f);
            return rect;
        }

        private static void ConfigureIdentityGroup(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition3D = Vector3.zero;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private static RectTransform OwnedGroup(string name, RectTransform parent, int sibling)
        {
            RectTransform result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            result.SetParent(parent, false);
            result.SetSiblingIndex(sibling);
            ConfigureIdentityGroup(result);
            return result;
        }

        private static Dictionary<string, RectTransform> Registry(params RectTransform[] values)
        {
            var result = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            for (int index = 0; index < values.Length; index++) result.Add((101 + index).ToString(), values[index]);
            return result;
        }

        private static Dictionary<string, RectTransform> EmptyGroups()
        {
            return new Dictionary<string, RectTransform>(StringComparer.Ordinal);
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

        private static string GraphSignature(RectTransform scope)
        {
            var result = new StringBuilder();
            foreach (Transform transform in scope.GetComponentsInChildren<Transform>(true))
            {
                RectTransform rect = transform as RectTransform;
                result.Append(transform.GetInstanceID()).Append('|')
                    .Append(transform.parent == null ? 0 : transform.parent.GetInstanceID()).Append('|')
                    .Append(transform.GetSiblingIndex()).Append('|').Append(transform.name).Append('|')
                    .Append(transform.localPosition).Append('|').Append(transform.localRotation).Append('|').Append(transform.localScale);
                if (rect != null)
                {
                    result.Append('|').Append(rect.anchorMin).Append('|').Append(rect.anchorMax).Append('|').Append(rect.pivot)
                        .Append('|').Append(rect.anchoredPosition3D).Append('|').Append(rect.sizeDelta)
                        .Append('|').Append(rect.offsetMin).Append('|').Append(rect.offsetMax);
                }
                result.AppendLine();
            }
            return result.ToString();
        }
    }

    public sealed class PsdHierarchyReferenceProbe : MonoBehaviour
    {
        public GameObject target;
        public RectTransform rectTarget;
    }

    public sealed class PsdHierarchyDeepReferenceProbe : MonoBehaviour
    {
        [HideInInspector]
        public RectTransform hiddenRectTarget;

        public RectTransform allowedLeaf;
        public List<RectTransform> rectTargets = new List<RectTransform>();
        public GameObject[] gameObjectTargets;
        public PsdHierarchyNestedReference nested = new PsdHierarchyNestedReference();
    }

    [Serializable]
    public sealed class PsdHierarchyNestedReference
    {
        public GameObject target;
        public GameObject allowedLeaf;
    }
}
