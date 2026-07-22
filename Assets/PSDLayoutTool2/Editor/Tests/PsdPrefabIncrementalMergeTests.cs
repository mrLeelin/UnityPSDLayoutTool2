namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using TMPro;
    using UnityEditor.Events;
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

        [Test]
        public void ProfilePathIsGuidKeyedInFixedSettingsFolder()
        {
            Assert.That(PsdPrefabTransactionalSave.GetProfilePath(
                    "Assets/Any/Nested/Target.prefab", "ABC-def_123"),
                Is.EqualTo("Assets/PSDLayoutTool2Settings/HierarchyProfiles/ABC-def_123.asset"));
            Assert.Throws<ArgumentException>(() => PsdPrefabTransactionalSave.GetProfilePath(
                TargetPath, "../escape"));
        }

        [Test]
        public void BoundProfileRejectsModeSwitchAndSameNameCopiedTargetGuid()
        {
            GameObject source = Root("Root");
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            PrefabUtility.SaveAsPrefabAsset(source, SameNamePath);
            UnityEngine.Object.DestroyImmediate(source);
            PsdHierarchyProfile profile = Profile();
            profile.targetPrefabPath = TargetPath;
            profile.targetPrefabGuid = AssetDatabase.AssetPathToGUID(TargetPath);

            Assert.DoesNotThrow(() => PsdPrefabTransactionalSave.ValidateProfileTargetBinding(profile, TargetPath));
            Assert.Throws<InvalidOperationException>(() =>
                PsdPrefabTransactionalSave.ValidateProfileTargetBinding(profile, SameNamePath));
            profile.targetPrefabPath = SameNamePath;
            Assert.Throws<InvalidOperationException>(() =>
                PsdPrefabTransactionalSave.ValidateProfileTargetBinding(profile, SameNamePath));
            UnityEngine.Object.DestroyImmediate(profile);
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
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            UnityEngine.Object.DestroyImmediate(source);
            long retainedId = LocalId(TargetPath, "Old");
            PsdHierarchyProfile profile = Profile(Node("101", retainedId, "Root/Old"));

            GameObject candidate = Root("Root");
            RectTransform candidateLeaf = Child(candidate, "Fresh", "101");
            candidateLeaf.anchorMin = new Vector2(0.12f, 0.23f);
            candidateLeaf.anchorMax = new Vector2(0.81f, 0.92f);
            candidateLeaf.pivot = new Vector2(0.17f, 0.83f);
            candidateLeaf.anchoredPosition3D = new Vector3(37f, -19f, 6f);
            candidateLeaf.sizeDelta = new Vector2(222f, 111f);
            candidateLeaf.offsetMin = new Vector2(3f, 4f);
            candidateLeaf.offsetMax = new Vector2(-5f, -6f);
            candidateLeaf.localRotation = Quaternion.Euler(0f, 0f, 13f);
            candidateLeaf.localScale = new Vector3(1.2f, 0.8f, 1f);
            candidateLeaf.gameObject.SetActive(false);
            Image candidateImage = candidateLeaf.gameObject.AddComponent<Image>();
            candidateImage.color = Color.cyan;
            candidateImage.type = Image.Type.Filled;
            candidateImage.fillMethod = Image.FillMethod.Radial180;
            candidateImage.fillOrigin = 2;
            candidateImage.fillAmount = 0.37f;
            candidateImage.fillClockwise = false;
            candidateImage.fillCenter = false;
            candidateImage.raycastTarget = false;
            candidateImage.raycastPadding = new Vector4(1f, 2f, 3f, 4f);
            candidateImage.preserveAspect = true;
            Texture2D texture = new Texture2D(4, 4);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            const string texturePath = Folder + "/ExternalTexture.asset";
            AssetDatabase.CreateAsset(texture, texturePath);
            AssetDatabase.AddObjectToAsset(sprite, texture);
            AssetDatabase.SaveAssetIfDirty(texture);
            candidateImage.sprite = sprite;
            Material material = new Material(Shader.Find("UI/Default"));
            material.color = Color.magenta;
            const string materialPath = Folder + "/ExternalMaterial.mat";
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssetIfDirty(material);
            byte[] materialBytesBefore = File.ReadAllBytes(FullPath(materialPath));
            candidateImage.material = material;
            PsdPrefabIncrementalCustomProbe candidateCustom = candidateLeaf.gameObject.AddComponent<PsdPrefabIncrementalCustomProbe>();
            candidateCustom.value = 99;
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                RectTransform loadedRetained = loaded.transform.Find("Old") as RectTransform;
                RectTransform loadedBusiness = loaded.transform.Find("Old/Business") as RectTransform;
                PsdPrefabIncrementalCustomProbe loadedCustom =
                    loadedRetained.gameObject.AddComponent<PsdPrefabIncrementalCustomProbe>();
                loadedCustom.value = 7;
                PsdPrefabIncrementalReferenceProbe loadedProbe =
                    loaded.AddComponent<PsdPrefabIncrementalReferenceProbe>();
                loadedProbe.target = loadedRetained.gameObject;
                Type[] componentOrder = loadedRetained.GetComponents<Component>().Select(component => component.GetType()).ToArray();
                Color materialColorBefore = material.color;
                PsdPrefabIncrementalMergeResult result = PsdPrefabIncrementalMerge.Merge(
                    TargetPath, loaded, candidate,
                    new Dictionary<string, RectTransform> { { "101", candidateLeaf } },
                    profile, EmptyPlan());

                RectTransform actual = result.generatedByStableId["101"];
                Assert.That(actual.name, Is.EqualTo("Fresh"));
                Assert.That(actual.anchorMin, Is.EqualTo(candidateLeaf.anchorMin));
                Assert.That(actual.anchorMax, Is.EqualTo(candidateLeaf.anchorMax));
                Assert.That(actual.pivot, Is.EqualTo(candidateLeaf.pivot));
                Assert.That(actual.anchoredPosition3D, Is.EqualTo(candidateLeaf.anchoredPosition3D));
                Assert.That(actual.sizeDelta, Is.EqualTo(candidateLeaf.sizeDelta));
                Assert.That(actual.offsetMin, Is.EqualTo(candidateLeaf.offsetMin));
                Assert.That(actual.offsetMax, Is.EqualTo(candidateLeaf.offsetMax));
                Assert.That(actual.localRotation, Is.EqualTo(candidateLeaf.localRotation));
                Assert.That(actual.localScale, Is.EqualTo(candidateLeaf.localScale));
                Assert.That(actual.gameObject.activeSelf, Is.False);
                Assert.That(actual.GetComponent<Image>().color, Is.EqualTo(Color.cyan));
                Assert.That(actual.GetComponent<Image>().sprite, Is.SameAs(sprite), "Sprite must be copied; a null Sprite renders white.");
                Assert.That(actual.GetComponent<Image>().material, Is.SameAs(material));
                Assert.That(actual.GetComponent<Image>().type, Is.EqualTo(Image.Type.Filled));
                Assert.That(actual.GetComponent<Image>().fillMethod, Is.EqualTo(Image.FillMethod.Radial180));
                Assert.That(actual.GetComponent<Image>().fillOrigin, Is.EqualTo(2));
                Assert.That(actual.GetComponent<Image>().fillAmount, Is.EqualTo(0.37f));
                Assert.That(actual.GetComponent<Image>().fillClockwise, Is.False);
                Assert.That(actual.GetComponent<Image>().fillCenter, Is.False);
                Assert.That(actual.GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(actual.GetComponent<Image>().raycastPadding, Is.EqualTo(new Vector4(1f, 2f, 3f, 4f)));
                Assert.That(actual.GetComponent<Image>().preserveAspect, Is.True);
                Assert.That(material.color, Is.EqualTo(materialColorBefore), "Material assets/references must never be mutated.");
                Assert.That(File.ReadAllBytes(FullPath(materialPath)), Is.EqualTo(materialBytesBefore));
                Assert.That(actual.Find("Business"), Is.SameAs(loadedBusiness));
                Assert.That(actual, Is.SameAs(loadedRetained));
                Assert.That(loadedProbe.target, Is.SameAs(actual.gameObject));
                Assert.That(actual.GetComponent<PsdPrefabIncrementalCustomProbe>().value, Is.EqualTo(7));
                Assert.That(actual.GetComponents<PsdPrefabIncrementalCustomProbe>().Length, Is.EqualTo(1));
                Assert.That(actual.GetComponents<Component>().Select(component => component.GetType()), Is.EqualTo(componentOrder));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(candidate);
                UnityEngine.Object.DestroyImmediate(profile);
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
        public void FirstAdoptionUsesDeterministicHierarchyAndVisualEvidence()
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
                PsdPrefabIncrementalMergeResult result = PsdPrefabIncrementalMerge.Merge(
                    TargetPath, loaded, candidate,
                    new Dictionary<string, RectTransform> { { "101", candidateLeaf } }, profile, EmptyPlan());
                Assert.That(result.generatedByStableId["101"], Is.SameAs(loaded.transform.Find("Same")));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(candidate);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void UnknownOwnershipAndAmbiguousSameNameResourceEvidenceFailClosed()
        {
            GameObject source = Root("Root");
            RectTransform wrapperA = Child(source, "WrapperA", null);
            RectTransform wrapperB = Child(source, "WrapperB", null);
            RectTransform first = Child(wrapperA.gameObject, "Same", null);
            RectTransform second = Child(wrapperB.gameObject, "Same", null);
            first.gameObject.AddComponent<Image>();
            second.gameObject.AddComponent<Image>();
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            UnityEngine.Object.DestroyImmediate(source);
            GameObject candidate = Root("DifferentRoot");
            RectTransform candidateLeaf = Child(candidate, "Same", null);
            candidateLeaf.gameObject.AddComponent<Image>();
            PsdHierarchyProfile profile = Profile(Node("101", 0L, string.Empty));
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                Assert.Throws<PsdPrefabIncrementalMergeException>(() => PsdPrefabIncrementalMerge.Merge(
                    TargetPath, loaded, candidate,
                    new Dictionary<string, RectTransform> { { "101", candidateLeaf } }, profile, EmptyPlan()));

                profile.nodes[0].ownership = PsdHierarchyNodeOwnership.Unknown;
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
        public void NotEmittedNativeLayerDoesNotMatchOrBlockBusinessObject()
        {
            GameObject source = Root("Root");
            RectTransform business = Child(source, "Business", null);
            business.gameObject.AddComponent<PsdPrefabIncrementalReferenceProbe>();
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            UnityEngine.Object.DestroyImmediate(source);
            PsdHierarchyProfileNode record = Node("101", 0L, string.Empty);
            record.ownership = PsdHierarchyNodeOwnership.NotEmitted;
            PsdHierarchyProfile profile = Profile(record);
            GameObject candidate = Root("Root");
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                PsdPrefabIncrementalMergeResult result = PsdPrefabIncrementalMerge.Merge(
                    TargetPath, loaded, candidate, new Dictionary<string, RectTransform>(), profile, EmptyPlan());
                Assert.That(result.generatedByStableId, Is.Empty);
                Assert.That(loaded.transform.Find("Business"), Is.Not.Null);
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
            newCandidate.gameObject.AddComponent<PsdPrefabIncrementalCustomProbe>().value = 99;
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                PsdPrefabIncrementalMergeResult result = PsdPrefabIncrementalMerge.Merge(
                    TargetPath, loaded, candidate,
                    new Dictionary<string, RectTransform> { { "101", oldCandidate }, { "102", newCandidate } },
                    profile, EmptyPlan());
                Assert.That(result.generatedByStableId["102"].name, Is.EqualTo("New"));
                Assert.That(result.generatedByStableId["102"].parent, Is.SameAs(loaded.transform));
                Assert.That(result.generatedByStableId["102"].GetComponent<PsdPrefabIncrementalCustomProbe>(), Is.Null,
                    "Candidate-only custom components must not leak into the target Prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(candidate);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void MergeCopiesTmpOwnedFieldsAndSharedReferencesWithoutChangingCustomComponentOrder()
        {
            GameObject source = Root("Root");
            RectTransform retained = Child(source, "Old Text", null);
            retained.gameObject.AddComponent<TextMeshProUGUI>();
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            UnityEngine.Object.DestroyImmediate(source);
            long localId = LocalId(TargetPath, "Old Text");
            PsdHierarchyProfile profile = Profile(Node("101", localId, "Root/Old Text"));
            GameObject candidate = Root("Root");
            RectTransform candidateTextRect = Child(candidate, "Fresh Text", null);
            TextMeshProUGUI candidateText = candidateTextRect.gameObject.AddComponent<TextMeshProUGUI>();
            candidateText.text = "增量文本验证";
            candidateText.font = TMP_Settings.defaultFontAsset;
            candidateText.fontSharedMaterial = candidateText.font != null ? candidateText.font.material : null;
            candidateText.fontSize = 37f;
            candidateText.fontStyle = FontStyles.Bold | FontStyles.Italic;
            candidateText.color = Color.yellow;
            candidateText.alignment = TextAlignmentOptions.BottomRight;
            candidateText.richText = false;
            candidateText.textWrappingMode = TextWrappingModes.NoWrap;
            candidateText.overflowMode = TextOverflowModes.Ellipsis;
            candidateText.characterSpacing = 2f;
            candidateText.wordSpacing = 3f;
            candidateText.lineSpacing = 4f;
            candidateText.paragraphSpacing = 5f;
            candidateText.enableAutoSizing = true;
            candidateText.fontSizeMin = 12f;
            candidateText.fontSizeMax = 44f;
            candidateText.margin = new Vector4(1f, 2f, 3f, 4f);
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                RectTransform loadedTextRect = loaded.transform.Find("Old Text") as RectTransform;
                loadedTextRect.gameObject.AddComponent<PsdPrefabIncrementalCustomProbe>().value = 5;
                Type[] beforeOrder = loadedTextRect.GetComponents<Component>().Select(component => component.GetType()).ToArray();
                PsdPrefabIncrementalMergeResult result = PsdPrefabIncrementalMerge.Merge(
                    TargetPath, loaded, candidate,
                    new Dictionary<string, RectTransform> { { "101", candidateTextRect } }, profile, EmptyPlan());
                TextMeshProUGUI actual = result.generatedByStableId["101"].GetComponent<TextMeshProUGUI>();
                Assert.That(actual.text, Is.EqualTo(candidateText.text));
                Assert.That(actual.font, Is.SameAs(candidateText.font));
                Assert.That(actual.fontSharedMaterial, Is.SameAs(candidateText.fontSharedMaterial));
                Assert.That(actual.fontSize, Is.EqualTo(37f));
                Assert.That(actual.fontStyle, Is.EqualTo(candidateText.fontStyle));
                Assert.That(actual.color, Is.EqualTo(Color.yellow));
                Assert.That(actual.alignment, Is.EqualTo(TextAlignmentOptions.BottomRight));
                Assert.That(actual.richText, Is.False);
                Assert.That(actual.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
                Assert.That(actual.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
                Assert.That(actual.characterSpacing, Is.EqualTo(2f));
                Assert.That(actual.wordSpacing, Is.EqualTo(3f));
                Assert.That(actual.lineSpacing, Is.EqualTo(4f));
                Assert.That(actual.paragraphSpacing, Is.EqualTo(5f));
                Assert.That(actual.enableAutoSizing, Is.True);
                Assert.That(actual.fontSizeMin, Is.EqualTo(12f));
                Assert.That(actual.fontSizeMax, Is.EqualTo(44f));
                Assert.That(actual.margin, Is.EqualTo(new Vector4(1f, 2f, 3f, 4f)));
                Assert.That(actual.GetComponent<PsdPrefabIncrementalCustomProbe>().value, Is.EqualTo(5));
                Assert.That(actual.GetComponents<Component>().Select(component => component.GetType()), Is.EqualTo(beforeOrder));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(candidate);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void RetainedLegacyUiComponentsSyncOwnedFieldsButPreserveButtonOnClick()
        {
            GameObject source = Root("Root");
            RectTransform retained = Child(source, "Legacy", null);
            Text oldText = retained.gameObject.AddComponent<Text>();
            Outline oldOutline = retained.gameObject.AddComponent<Outline>();
            Shadow oldShadow = retained.gameObject.AddComponent<Shadow>();
            AspectRatioFitter oldAspect = retained.gameObject.AddComponent<AspectRatioFitter>();
            retained.gameObject.AddComponent<Button>();
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            UnityEngine.Object.DestroyImmediate(source);
            PsdHierarchyProfileNode record = Node("101", LocalId(TargetPath, "Legacy"), "Root/Legacy");
            record.importerOwnedComponentTypes = new List<string>
            {
                typeof(Text).FullName, typeof(Outline).FullName, typeof(Shadow).FullName,
                typeof(AspectRatioFitter).FullName, typeof(Button).FullName
            };
            PsdHierarchyProfile profile = Profile(record);
            GameObject candidate = Root("Root");
            RectTransform candidateRect = Child(candidate, "Legacy", null);
            Text text = candidateRect.gameObject.AddComponent<Text>();
            text.text = "增量旧版文本";
            text.fontSize = 31;
            text.fontStyle = FontStyle.BoldAndItalic;
            text.alignment = TextAnchor.LowerRight;
            text.color = Color.green;
            text.raycastTarget = false;
            Outline outline = candidateRect.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.red;
            outline.effectDistance = new Vector2(3f, -4f);
            outline.useGraphicAlpha = false;
            Shadow shadow = candidateRect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Color.blue;
            shadow.effectDistance = new Vector2(-2f, 5f);
            AspectRatioFitter aspect = candidateRect.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 1.75f;
            Button button = candidateRect.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            button.transition = Selectable.Transition.ColorTint;
            button.interactable = false;
            button.navigation = new Navigation { mode = Navigation.Mode.None, wrapAround = true };
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                RectTransform loadedLegacy = loaded.transform.Find("Legacy") as RectTransform;
                PsdPrefabIncrementalCustomProbe handler =
                    loadedLegacy.gameObject.AddComponent<PsdPrefabIncrementalCustomProbe>();
                UnityEventTools.AddPersistentListener(
                    loadedLegacy.GetComponent<Button>().onClick, handler.HandleClick);
                PsdPrefabIncrementalMergeResult result = PsdPrefabIncrementalMerge.Merge(
                    TargetPath, loaded, candidate,
                    new Dictionary<string, RectTransform> { { "101", candidateRect } }, profile, EmptyPlan());
                RectTransform actual = result.generatedByStableId["101"];
                Assert.That(actual.GetComponent<Text>().text, Is.EqualTo("增量旧版文本"));
                Assert.That(actual.GetComponent<Text>().fontSize, Is.EqualTo(31));
                Assert.That(actual.GetComponent<Outline>().effectDistance, Is.EqualTo(new Vector2(3f, -4f)));
                Assert.That(actual.GetComponent<Shadow>().effectColor, Is.EqualTo(Color.blue));
                Assert.That(actual.GetComponent<AspectRatioFitter>().aspectRatio, Is.EqualTo(1.75f));
                Assert.That(actual.GetComponent<Button>().targetGraphic, Is.SameAs(actual.GetComponent<Text>()));
                Assert.That(actual.GetComponent<Button>().interactable, Is.False);
                Assert.That(actual.GetComponent<Button>().onClick.GetPersistentEventCount(), Is.EqualTo(1));
                Assert.That(record.importerOwnedComponentTypes, Does.Contain(typeof(Button).FullName));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(candidate);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void MissingPreviouslyOwnedComponentRemovesOnlyWhenUnreferencedAndFailsClosedForBusinessEvent()
        {
            GameObject source = Root("Root");
            RectTransform retained = Child(source, "Legacy", null);
            retained.gameObject.AddComponent<Outline>();
            Button button = retained.gameObject.AddComponent<Button>();
            PsdPrefabIncrementalCustomProbe handler = retained.gameObject.AddComponent<PsdPrefabIncrementalCustomProbe>();
            UnityEventTools.AddPersistentListener(button.onClick, handler.HandleClick);
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            UnityEngine.Object.DestroyImmediate(source);
            PsdHierarchyProfileNode record = Node("101", LocalId(TargetPath, "Legacy"), "Root/Legacy");
            record.importerOwnedComponentTypes = new List<string> { typeof(Outline).FullName, typeof(Button).FullName };
            PsdHierarchyProfile profile = Profile(record);
            GameObject candidate = Root("Root");
            RectTransform candidateRect = Child(candidate, "Legacy", null);
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                Assert.Throws<PsdPrefabIncrementalMergeException>(() => PsdPrefabIncrementalMerge.Merge(
                    TargetPath, loaded, candidate,
                    new Dictionary<string, RectTransform> { { "101", candidateRect } }, profile, EmptyPlan()));
                Assert.That(loaded.transform.Find("Legacy").GetComponent<Button>(), Is.Not.Null);
                Assert.That(loaded.transform.Find("Legacy").GetComponent<Outline>(), Is.Not.Null,
                    "Failure must roll back earlier safe component removals.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(candidate);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void MissingPreviouslyOwnedComponentsFailClosedForListAndNestedBusinessReferences()
        {
            GameObject source = Root("Root");
            RectTransform retained = Child(source, "Legacy", null);
            Outline outline = retained.gameObject.AddComponent<Outline>();
            AspectRatioFitter aspect = retained.gameObject.AddComponent<AspectRatioFitter>();
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            UnityEngine.Object.DestroyImmediate(source);

            PsdHierarchyProfileNode record = Node("101", LocalId(TargetPath, "Legacy"), "Root/Legacy");
            record.importerOwnedComponentTypes = new List<string>
            {
                typeof(Outline).FullName,
                typeof(AspectRatioFitter).FullName
            };
            PsdHierarchyProfile profile = Profile(record);
            GameObject candidate = Root("Root");
            RectTransform candidateRect = Child(candidate, "Legacy", null);
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                RectTransform loadedLegacy = loaded.transform.Find("Legacy") as RectTransform;
                Outline loadedOutline = loadedLegacy.GetComponent<Outline>();
                AspectRatioFitter loadedAspect = loadedLegacy.GetComponent<AspectRatioFitter>();
                PsdPrefabIncrementalNestedReferenceProbe loadedReferences =
                    loaded.AddComponent<PsdPrefabIncrementalNestedReferenceProbe>();
                loadedReferences.outlines.Add(loadedOutline);
                loadedReferences.nested.aspectRatioFitter = loadedAspect;

                // Prove the hidden list/array element is scanned independently.
                loadedReferences.nested.aspectRatioFitter = null;
                Assert.Throws<PsdPrefabIncrementalMergeException>(() => PsdPrefabIncrementalMerge.Merge(
                    TargetPath, loaded, candidate,
                    new Dictionary<string, RectTransform> { { "101", candidateRect } }, profile, EmptyPlan()));
                Assert.That(loadedLegacy.GetComponent<Outline>(), Is.Not.Null);
                Assert.That(loadedLegacy.GetComponent<AspectRatioFitter>(), Is.Not.Null,
                    "A deep-reference failure must occur before any previously-owned component is removed.");

                // Then isolate the nested serializable reference. Both stale
                // components must still be present after the second failure.
                loadedReferences.outlines.Clear();
                loadedReferences.nested.aspectRatioFitter = loadedAspect;
                Assert.Throws<PsdPrefabIncrementalMergeException>(() => PsdPrefabIncrementalMerge.Merge(
                    TargetPath, loaded, candidate,
                    new Dictionary<string, RectTransform> { { "101", candidateRect } }, profile, EmptyPlan()));
                Assert.That(loadedLegacy.GetComponent<Outline>(), Is.SameAs(loadedOutline));
                Assert.That(loadedLegacy.GetComponent<AspectRatioFitter>(), Is.SameAs(loadedAspect));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(candidate);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void NewNodeCreatesOnlyAllowlistedComponentsAndSafeStaleOwnedEffectIsRemoved()
        {
            GameObject source = Root("Root");
            RectTransform old = Child(source, "Old", null);
            old.gameObject.AddComponent<Outline>();
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            UnityEngine.Object.DestroyImmediate(source);
            PsdHierarchyProfileNode oldRecord = Node("101", LocalId(TargetPath, "Old"), "Root/Old");
            oldRecord.importerOwnedComponentTypes = new List<string> { typeof(Outline).FullName };
            PsdHierarchyProfileNode newRecord = Node("102", 0L, string.Empty);
            newRecord.pendingCreation = true;
            PsdHierarchyProfile profile = Profile(oldRecord, newRecord);
            GameObject candidate = Root("Root");
            RectTransform oldCandidate = Child(candidate, "Old", null);
            RectTransform fresh = Child(candidate, "Fresh", null);
            fresh.gameObject.AddComponent<Text>().text = "新节点";
            fresh.gameObject.AddComponent<Shadow>();
            fresh.gameObject.AddComponent<PsdPrefabIncrementalCustomProbe>();
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                PsdPrefabIncrementalMergeResult result = PsdPrefabIncrementalMerge.Merge(
                    TargetPath, loaded, candidate,
                    new Dictionary<string, RectTransform> { { "101", oldCandidate }, { "102", fresh } },
                    profile, EmptyPlan());
                Assert.That(result.generatedByStableId["101"].GetComponent<Outline>(), Is.Null);
                Assert.That(result.generatedByStableId["102"].GetComponent<Text>(), Is.Not.Null);
                Assert.That(result.generatedByStableId["102"].GetComponent<Shadow>(), Is.Not.Null);
                Assert.That(result.generatedByStableId["102"].GetComponent<PsdPrefabIncrementalCustomProbe>(), Is.Null);
                Assert.That(newRecord.importerOwnedComponentTypes,
                    Is.EquivalentTo(new[] { typeof(Text).FullName, typeof(Shadow).FullName }));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(candidate);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ProfileExistsWithMissingTargetFailsClosedWhileAbsentProfileKeepsLegacyEligibility()
        {
            PsdHierarchyProfile profile = Profile();
            profile.targetPrefabPath = TargetPath;
            profile.targetPrefabGuid = "missing-guid";
            AssetDatabase.CreateAsset(profile, ProfilePath);

            Assert.Throws<InvalidOperationException>(() =>
                PsdPrefabTransactionalSave.ResolveBoundProfileForImport(ProfilePath, TargetPath));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath), Is.Null);
            Assert.That(PsdPrefabTransactionalSave.ResolveBoundProfileForImport(
                Folder + "/Absent.asset", TargetPath), Is.Null);
        }

        [Test]
        public void BoundProfileRejectsNonUiModeBeforePrefabOrProfileBytesCanChange()
        {
            const string settingsFolder = "Assets/PSDLayoutTool2Settings";
            const string profilesFolder = settingsFolder + "/HierarchyProfiles";
            string sourceGuid = "mode-switch-" + Guid.NewGuid().ToString("N");
            string fixedProfilePath = PsdPrefabTransactionalSave.GetProfilePath(TargetPath, sourceGuid);
            bool settingsFolderExisted = AssetDatabase.IsValidFolder(settingsFolder);
            bool profilesFolderExisted = AssetDatabase.IsValidFolder(profilesFolder);
            bool createdSettingsFolder = false;
            bool createdProfilesFolder = false;
            bool createdTestProfile = false;
            try
            {
                if (!settingsFolderExisted)
                {
                    AssetDatabase.CreateFolder("Assets", "PSDLayoutTool2Settings");
                    createdSettingsFolder = true;
                }
                if (!profilesFolderExisted)
                {
                    AssetDatabase.CreateFolder(settingsFolder, "HierarchyProfiles");
                    createdProfilesFolder = true;
                }

                GameObject source = Root("Root");
                PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
                UnityEngine.Object.DestroyImmediate(source);
                PsdHierarchyProfile profile = Profile();
                profile.sourcePsdGuid = sourceGuid;
                profile.targetPrefabPath = TargetPath;
                profile.targetPrefabGuid = AssetDatabase.AssetPathToGUID(TargetPath);
                Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fixedProfilePath), Is.Null);
                AssetDatabase.CreateAsset(profile, fixedProfilePath);
                createdTestProfile = true;
                AssetDatabase.SaveAssetIfDirty(profile);
                byte[] prefabBefore = File.ReadAllBytes(FullPath(TargetPath));
                byte[] profileBefore = File.ReadAllBytes(FullPath(fixedProfilePath));

                Assert.Throws<InvalidOperationException>(() =>
                    PsdImporter.ResolveHierarchyProfileBeforePrefabImport(sourceGuid, TargetPath, false));
                Assert.That(File.ReadAllBytes(FullPath(TargetPath)), Is.EqualTo(prefabBefore));
                Assert.That(File.ReadAllBytes(FullPath(fixedProfilePath)), Is.EqualTo(profileBefore));
            }
            finally
            {
                if (createdTestProfile)
                    AssetDatabase.DeleteAsset(fixedProfilePath);
                if (createdProfilesFolder && IsAssetDirectoryEmpty(profilesFolder))
                    AssetDatabase.DeleteAsset(profilesFolder);
                if (createdSettingsFolder && IsAssetDirectoryEmpty(settingsFolder))
                    AssetDatabase.DeleteAsset(settingsFolder);
            }

            Assert.That(PsdImporter.ResolveHierarchyProfileBeforePrefabImport(
                "mode-switch-absent-guid", TargetPath, false), Is.Null,
                "A non-UI import without a Profile must retain the legacy save path.");
        }

        [Test]
        public void SuccessfulTransactionPreservesTargetGuidAndLocalIdAndWritesProfileIdentityAfterPrefabSave()
        {
            CreateTargetAndProfile(out long originalLocalId, out string targetGuid);
            PsdHierarchyProfile working = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(ProfilePath));
            working.nodes[0].importerOwnedComponentTypes.Add(typeof(Image).FullName);
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            loaded.transform.Find("Old").name = "Fresh";
            try
            {
                Assert.That(working.nodes[0].localFileId, Is.Zero);
                PsdPrefabTransactionalSave.Save(
                    TargetPath, loaded, ProfilePath, working,
                    new Dictionary<string, RectTransform> { { "101", loaded.transform.Find("Fresh") as RectTransform } },
                    new Dictionary<string, RectTransform>(), new[] { TemporaryPath }, null, true);
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
            Assert.That(saved.nodes[0].lastKnownPath, Is.EqualTo("Target/Fresh"),
                "Unity names loaded Prefab contents from the target asset filename.");
            Assert.That(saved.targetPrefabPath, Is.EqualTo(TargetPath));
            Assert.That(saved.targetPrefabGuid, Is.EqualTo(targetGuid));
            Assert.That(saved.nodes[0].importerOwnedComponentTypes, Is.EqualTo(new[] { typeof(Image).FullName }));
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
                    new Dictionary<string, RectTransform>(), Array.Empty<string>(), null, true);

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
        [TestCase(PsdPrefabTransactionStage.BeforeProfileCopy)]
        [TestCase(PsdPrefabTransactionStage.DuringProfileCopy)]
        [TestCase(PsdPrefabTransactionStage.AfterProfileCopy)]
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
                    stage => { if (stage == failureStage) throw new InvalidOperationException("injected"); }, true));
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

        [TestCase(PsdPrefabTransactionStage.BeforeProfileCopy)]
        [TestCase(PsdPrefabTransactionStage.DuringProfileCopy)]
        [TestCase(PsdPrefabTransactionStage.AfterProfileCopy)]
        public void NewProfileCreationFailureRestoresOriginalAbsenceAndCleansMeta(PsdPrefabTransactionStage failureStage)
        {
            GameObject source = Root("Root");
            Child(source, "Old", null);
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            UnityEngine.Object.DestroyImmediate(source);
            GameObject temporary = Root("Temporary");
            PrefabUtility.SaveAsPrefabAsset(temporary, TemporaryPath);
            UnityEngine.Object.DestroyImmediate(temporary);
            const string newProfilePath = Folder + "/NewProfile.asset";
            PsdHierarchyProfile working = Profile(Node("101", 0L, string.Empty));
            working.nodes[0].pendingCreation = true;
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                Assert.Throws<InvalidOperationException>(() => PsdPrefabTransactionalSave.Save(
                    TargetPath, loaded, newProfilePath, working,
                    new Dictionary<string, RectTransform> { { "101", loaded.transform.Find("Old") as RectTransform } },
                    new Dictionary<string, RectTransform>(), new[] { TemporaryPath },
                    stage => { if (stage == failureStage) throw new InvalidOperationException("injected"); }, true));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(working);
            }

            Assert.That(File.Exists(FullPath(newProfilePath)), Is.False);
            Assert.That(File.Exists(FullPath(newProfilePath) + ".meta"), Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(newProfilePath), Is.Null);
            Assert.That(File.Exists(FullPath(TemporaryPath)), Is.False);
            Assert.That(File.Exists(FullPath(TemporaryPath) + ".meta"), Is.False);
        }

        [Test]
        public void FirstProfileSaveCreatesMissingDirectoryChainWithoutGlobalRefresh()
        {
            GameObject source = Root("Root");
            Child(source, "Old", null);
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            UnityEngine.Object.DestroyImmediate(source);
            string profilePath = Folder + "/Created/HierarchyProfiles/Profile.asset";
            PsdHierarchyProfile working = Profile(Node("101", 0L, string.Empty));
            working.nodes[0].pendingCreation = true;
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                PsdPrefabTransactionalSave.Save(
                    TargetPath, loaded, profilePath, working,
                    new Dictionary<string, RectTransform> { { "101", loaded.transform.Find("Old") as RectTransform } },
                    new Dictionary<string, RectTransform>(), Array.Empty<string>(), null, true);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(working);
            }

            Assert.That(AssetDatabase.IsValidFolder(Folder + "/Created"), Is.True);
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/Created/HierarchyProfiles"), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(profilePath), Is.Not.Null);
        }

        [Test]
        public void FailedFirstProfileSaveRemovesOnlyTransactionCreatedEmptyDirectories()
        {
            GameObject source = Root("Root");
            Child(source, "Old", null);
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            UnityEngine.Object.DestroyImmediate(source);
            string profilePath = Folder + "/RollbackCreated/HierarchyProfiles/Profile.asset";
            PsdHierarchyProfile working = Profile(Node("101", 0L, string.Empty));
            working.nodes[0].pendingCreation = true;
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                Assert.Throws<InvalidOperationException>(() => PsdPrefabTransactionalSave.Save(
                    TargetPath, loaded, profilePath, working,
                    new Dictionary<string, RectTransform> { { "101", loaded.transform.Find("Old") as RectTransform } },
                    new Dictionary<string, RectTransform>(), Array.Empty<string>(),
                    stage => { if (stage == PsdPrefabTransactionStage.DuringProfileCopy) throw new InvalidOperationException("injected"); },
                    true));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(working);
            }

            Assert.That(File.Exists(FullPath(profilePath)), Is.False);
            Assert.That(File.Exists(FullPath(profilePath) + ".meta"), Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(profilePath), Is.Null);
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/RollbackCreated/HierarchyProfiles"), Is.False);
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/RollbackCreated"), Is.False);
            Assert.That(Directory.Exists(FullPath(Folder + "/RollbackCreated")), Is.False);
        }

        [Test]
        public void FailedFirstProfileSaveKeepsPreexistingEmptyDirectories()
        {
            AssetDatabase.CreateFolder(Folder, "Existing");
            AssetDatabase.CreateFolder(Folder + "/Existing", "HierarchyProfiles");
            GameObject source = Root("Root");
            Child(source, "Old", null);
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            UnityEngine.Object.DestroyImmediate(source);
            string profilePath = Folder + "/Existing/HierarchyProfiles/Profile.asset";
            PsdHierarchyProfile working = Profile(Node("101", 0L, string.Empty));
            working.nodes[0].pendingCreation = true;
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                Assert.Throws<InvalidOperationException>(() => PsdPrefabTransactionalSave.Save(
                    TargetPath, loaded, profilePath, working,
                    new Dictionary<string, RectTransform> { { "101", loaded.transform.Find("Old") as RectTransform } },
                    new Dictionary<string, RectTransform>(), Array.Empty<string>(),
                    stage => { if (stage == PsdPrefabTransactionStage.DuringProfileCopy) throw new InvalidOperationException("injected"); },
                    true));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(working);
            }

            Assert.That(AssetDatabase.IsValidFolder(Folder + "/Existing"), Is.True);
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/Existing/HierarchyProfiles"), Is.True);
        }

        [Test]
        public void PreviousGroupIdentityRemovesDisappearedShellWhileFinalProfileUsesNewPlanOnly()
        {
            GameObject source = Root("Root");
            RectTransform shell = Child(source, "Old Group", null);
            Child(shell.gameObject, "Old", "101");
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            UnityEngine.Object.DestroyImmediate(source);
            long leafId = LocalId(TargetPath, "Old Group/Old");
            long shellId = LocalId(TargetPath, "Old Group");
            PsdHierarchyProfile persisted = Profile(Node("101", leafId, "Root/Old Group/Old"));
            persisted.groups.Add(new PsdHierarchyProfileGroup
            {
                key = "old_group", displayName = "Old Group", localFileId = shellId, lastKnownPath = "Root/Old Group",
                stableLayerIds = new List<string> { "101" }
            });
            AssetDatabase.CreateAsset(persisted, ProfilePath);
            AssetDatabase.SaveAssetIfDirty(persisted);

            persisted.targetPrefabPath = TargetPath;
            persisted.targetPrefabGuid = AssetDatabase.AssetPathToGUID(TargetPath);
            EditorUtility.SetDirty(persisted);
            AssetDatabase.SaveAssetIfDirty(persisted);
            PsdHierarchyProfile working = UnityEngine.Object.Instantiate(persisted);
            working.groups.Clear();
            GameObject candidate = Root("Root");
            RectTransform candidateLeaf = Child(candidate, "Fresh", "101");
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                PsdPrefabIncrementalMergeResult merged = PsdPrefabIncrementalMerge.Merge(
                    TargetPath, loaded, candidate,
                    new Dictionary<string, RectTransform> { { "101", candidateLeaf } },
                    working, persisted.groups, new PsdHierarchyPlan());
                Assert.That(loaded.transform.Find("Old Group"), Is.Null);
                Assert.That(loaded.transform.Find("Fresh"), Is.Not.Null);
                Assert.That(merged.groupsByKey, Is.Empty);

                PsdPrefabTransactionalSave.Save(
                    TargetPath, loaded, ProfilePath, working,
                    merged.generatedByStableId, merged.groupsByKey,
                    Array.Empty<string>(), null, false);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(candidate);
                UnityEngine.Object.DestroyImmediate(working);
            }

            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath).transform.Find("Old Group"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(ProfilePath).groups, Is.Empty);
        }

        [Test]
        public void DisappearedGroupTransactionFailureRestoresOldShellAndPreviousProfile()
        {
            GameObject source = Root("Root");
            RectTransform shell = Child(source, "Old Group", null);
            Child(shell.gameObject, "Old", "101");
            PrefabUtility.SaveAsPrefabAsset(source, TargetPath);
            UnityEngine.Object.DestroyImmediate(source);
            long leafId = LocalId(TargetPath, "Old Group/Old");
            long shellId = LocalId(TargetPath, "Old Group");
            PsdHierarchyProfile persisted = Profile(Node("101", leafId, "Root/Old Group/Old"));
            persisted.groups.Add(new PsdHierarchyProfileGroup
            {
                key = "old_group", displayName = "Old Group", localFileId = shellId,
                lastKnownPath = "Root/Old Group", stableLayerIds = new List<string> { "101" }
            });
            AssetDatabase.CreateAsset(persisted, ProfilePath);
            AssetDatabase.SaveAssetIfDirty(persisted);
            persisted.targetPrefabPath = TargetPath;
            persisted.targetPrefabGuid = AssetDatabase.AssetPathToGUID(TargetPath);
            EditorUtility.SetDirty(persisted);
            AssetDatabase.SaveAssetIfDirty(persisted);
            byte[] profileBefore = File.ReadAllBytes(FullPath(ProfilePath));

            PsdHierarchyProfile working = UnityEngine.Object.Instantiate(persisted);
            working.groups.Clear();
            GameObject candidate = Root("Root");
            RectTransform candidateLeaf = Child(candidate, "Fresh", "101");
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                PsdPrefabIncrementalMergeResult merged = PsdPrefabIncrementalMerge.Merge(
                    TargetPath, loaded, candidate,
                    new Dictionary<string, RectTransform> { { "101", candidateLeaf } },
                    working, persisted.groups, new PsdHierarchyPlan());
                Assert.Throws<InvalidOperationException>(() => PsdPrefabTransactionalSave.Save(
                    TargetPath, loaded, ProfilePath, working,
                    merged.generatedByStableId, merged.groupsByKey, Array.Empty<string>(),
                    stage => { if (stage == PsdPrefabTransactionStage.AfterPrefabSave) throw new InvalidOperationException("injected"); },
                    false));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(candidate);
                UnityEngine.Object.DestroyImmediate(working);
            }

            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath).transform.Find("Old Group/Old"), Is.Not.Null);
            Assert.That(File.ReadAllBytes(FullPath(ProfilePath)), Is.EqualTo(profileBefore));
            Assert.That(AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(ProfilePath).groups.Single().key,
                Is.EqualTo("old_group"));
        }

        [Test]
        public void CanonicalFinalVerificationDetectsDeepProfileCorruptionAndRollsBack()
        {
            CreateTargetAndProfile(out _, out _);
            byte[] before = File.ReadAllBytes(FullPath(ProfilePath));
            PsdHierarchyProfile working = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(ProfilePath));
            GameObject loaded = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                Assert.Throws<InvalidOperationException>(() => PsdPrefabTransactionalSave.Save(
                    TargetPath, loaded, ProfilePath, working,
                    new Dictionary<string, RectTransform> { { "101", loaded.transform.Find("Old") as RectTransform } },
                    new Dictionary<string, RectTransform>(), Array.Empty<string>(),
                    stage =>
                    {
                        if (stage != PsdPrefabTransactionStage.AfterProfileSave) return;
                        PsdHierarchyProfile corrupted = AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(ProfilePath);
                        corrupted.nodes[0].geometryFingerprint = "partial-corruption";
                        EditorUtility.SetDirty(corrupted);
                        AssetDatabase.SaveAssetIfDirty(corrupted);
                    }, true));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
                UnityEngine.Object.DestroyImmediate(working);
            }
            Assert.That(File.ReadAllBytes(FullPath(ProfilePath)), Is.EqualTo(before));
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
            return new PsdHierarchyProfileNode
            {
                stableId = stableId,
                ownership = PsdHierarchyNodeOwnership.Generated,
                localFileId = localFileId,
                lastKnownPath = path
            };
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

        private static bool IsAssetDirectoryEmpty(string assetPath)
        {
            string fullPath = FullPath(assetPath);
            return Directory.Exists(fullPath) && !Directory.EnumerateFileSystemEntries(fullPath).Any();
        }
    }

    public sealed class PsdPrefabIncrementalReferenceProbe : MonoBehaviour
    {
        public GameObject target;
    }

    public sealed class PsdPrefabIncrementalCustomProbe : MonoBehaviour
    {
        public int value;
        public void HandleClick() { value++; }
    }

    [Serializable]
    public sealed class PsdPrefabIncrementalNestedReferences
    {
        public AspectRatioFitter aspectRatioFitter;
    }

    public sealed class PsdPrefabIncrementalNestedReferenceProbe : MonoBehaviour
    {
        [HideInInspector]
        public List<Outline> outlines = new List<Outline>();
        public PsdPrefabIncrementalNestedReferences nested = new PsdPrefabIncrementalNestedReferences();
    }
}
