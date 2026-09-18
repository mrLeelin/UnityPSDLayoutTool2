namespace PsdLayoutTool2.Tests
{
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    public sealed class PsdHierarchyV2BasicCleanupTests
    {
        private const string Folder = "Assets/__PsdHierarchyV2BasicCleanupTests";
        private const string AssetPath = Folder + "/ExampleView.prefab";
        private const string MaterialPath = Folder + "/Example.mat";
        private PsdHierarchyChatContext context;
        private bool ownsFolder;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False, "Test fixture path is already occupied.");
            AssetDatabase.CreateFolder("Assets", "__PsdHierarchyV2BasicCleanupTests");
            ownsFolder = true;
            var material = new Material(Shader.Find("UI/Default"));
            AssetDatabase.CreateAsset(material, MaterialPath);
            var root = new GameObject("ExampleView", typeof(RectTransform));
            try
            {
                var item = new GameObject("Item", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                item.transform.SetParent(root.transform, false);
                ((RectTransform)item.transform).sizeDelta = new Vector2(80, 40);
                item.GetComponent<Image>().color = new Color(0.15f, 0.7f, 0.4f);
                item.GetComponent<Image>().material = material;
                var scroll = root.AddComponent<ScrollRect>();
                scroll.enabled = false;
                scroll.content = (RectTransform)item.transform;
                var empty = new GameObject("Empty", typeof(RectTransform));
                empty.transform.SetParent(root.transform, false);
                PrefabUtility.SaveAsPrefabAsset(root, AssetPath);
            }
            finally { Object.DestroyImmediate(root); }

            string project = Directory.GetParent(Application.dataPath).FullName;
            string fingerprint = PsdHierarchyChatContextBuilder.ComputeFileFingerprint(Path.Combine(project, AssetPath));
            var nodes = new JArray();
            string[] paths = { "ExampleView", "ExampleView/Item", "ExampleView/Empty" };
            for (int index = 0; index < paths.Length; index++)
                nodes.Add(new JObject { ["id"] = "n" + index, ["path"] = paths[index] });
            context = new PsdHierarchyChatContext(project, "", AssetPath, "", "", "",
                hierarchySnapshotJson: new JObject { ["nodes"] = nodes }.ToString(),
                hierarchySnapshotFingerprint: fingerprint);
        }

        [TearDown]
        public void TearDown()
        {
            if (ownsFolder) AssetDatabase.DeleteAsset(Folder);
            ownsFolder = false;
        }

        private JObject Plan()
        {
            var plan = new JObject
            {
                ["version"] = 2, ["snapshotFingerprint"] = context.hierarchySnapshotFingerprint,
                ["prefabName"] = "ExampleView", ["prefabAssetPath"] = AssetPath,
                ["output"] = new JObject { ["mode"] = "in_place", ["assetPath"] = AssetPath },
                ["verify"] = new JObject()
            };
            foreach (string key in new[] { "wrappers", "moves", "renames", "emptyContainerRemovals", "tightBounds",
                "textureRenames", "spriteAtlasRenames", "componentFamilyDecisions", "containmentResolutions",
                "flatSiblingResolutions", "componentExtractions", "stateComponentExtractions",
                "variantComponentExtractions", "statefulComponentExtractions" }) plan[key] = new JArray();
            plan["renames"] = new JArray(new JObject { ["target"] = "node:n1", ["name"] = "Icon" });
            return plan;
        }

        [Test]
        public void PreparingReviewedPlanPreservesNodeIdsAndDocument()
        {
            JObject plan = Plan();
            Assert.That(PsdHierarchyChatCleanupExecution.TryPrepareExecutionPlan(context, plan.ToString(),
                out string prepared, out string error), Is.True, error);
            Assert.That(JToken.DeepEquals(plan, JObject.Parse(prepared)), Is.True, prepared);
        }

        [Test]
        public async Task SimpleApplySavesAndReloadsRenamedNode()
        {
            var result = await PsdHierarchyNativeCleanupExecutor.ApplyAsync(context, Plan().ToString());
            Assert.That(result.success, Is.True, result.message);
            var saved = PrefabUtility.LoadPrefabContents(AssetPath);
            try { Assert.That(saved.transform.GetChild(0).name, Is.EqualTo("Icon")); }
            finally { PrefabUtility.UnloadPrefabContents(saved); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ExistingMissingControllerIsPreservedAcrossCleanup(bool moveAndRename)
        {
            AddNestedPrefabWithMissingController(keepNested: !moveAndRename);
            JObject plan = Plan();
            if (moveAndRename)
            {
                plan["wrappers"] = new JArray(new JObject { ["id"] = "tips", ["parent"] = "node:n0", ["name"] = "Tips", ["siblingIndex"] = 2 });
                plan["moves"] = new JArray(new JObject { ["source"] = "node:n3", ["destination"] = "@tips", ["siblingIndex"] = 0 });
                ((JArray)plan["renames"]).Add(new JObject { ["target"] = "node:n3", ["name"] = "RenamedTip" });
            }
            byte[] original = File.ReadAllBytes(AssetPath);
            var preflight = await PsdHierarchyNativeCleanupExecutor.ValidateAsync(context, plan.ToString());
            Assert.That(preflight.success, Is.True, preflight.message);
            Assert.That(File.ReadAllBytes(AssetPath), Is.EqualTo(original));

            int missingId = ReadMissingControllerId();
            var result = await PsdHierarchyNativeCleanupExecutor.ApplyAsync(context, plan.ToString());

            Assert.That(result.success, Is.True, result.message);
            Assert.That(result.message, Does.Contain("Existing unresolved references preserved: 1"));
            Assert.That(result.message, Does.Contain("Animator.m_Controller"));
            Assert.That(ReadMissingControllerId(), Is.EqualTo(missingId));
            GameObject saved = PrefabUtility.LoadPrefabContents(AssetPath);
            try
            {
                Assert.That(saved.transform.GetChild(0).name, Is.EqualTo("Icon"));
                Assert.That(PrefabUtility.IsPartOfPrefabInstance(saved.GetComponentInChildren<Animator>(true)), Is.EqualTo(!moveAndRename));
            }
            finally { PrefabUtility.UnloadPrefabContents(saved); }
        }

        [Test]
        public void ChangingMissingReferenceOwnersOrValuesDuringSimulationIsRejected()
        {
            AddNestedPrefabWithMissingController();
            System.Type expectations = typeof(PsdHierarchyNativeCleanupExecutor).GetNestedType(
                "PersistedExpectations", System.Reflection.BindingFlags.NonPublic);
            var captureMissing = expectations.GetMethod("CaptureMissingReferences",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var capture = expectations.GetMethod("Capture",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            byte[] original = File.ReadAllBytes(AssetPath);
            foreach (bool removeOwner in new[] { false, true })
            {
                GameObject root = PrefabUtility.LoadPrefabContents(AssetPath);
                try
                {
                    object baseline = captureMissing.Invoke(null, new object[] { root });
                    Animator owner = root.GetComponentInChildren<Animator>(true);
                    if (removeOwner) Object.DestroyImmediate(owner);
                    else
                    {
                        var serialized = new SerializedObject(owner);
                        serialized.FindProperty("m_Controller").objectReferenceInstanceIDValue = 0;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    var error = Assert.Throws<System.Reflection.TargetInvocationException>(
                        () => capture.Invoke(null, new[] { (object)root, baseline }));
                    Assert.That(error.InnerException, Is.TypeOf<System.InvalidOperationException>());
                    Assert.That(error.InnerException.Message, Does.Contain("unresolved reference"));
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Assert.That(File.ReadAllBytes(AssetPath), Is.EqualTo(original));
        }

        [Test]
        public void ClearingAnExistingMissingControllerDuringSaveIsReportedAsPartial()
        {
            AddNestedPrefabWithMissingController();
            var result = PsdHierarchyNativeCleanupExecutor.ExecuteV2(
                context, Plan().ToString(), true,
                savePrefabAsset: (root, path) =>
                {
                    var serialized = new SerializedObject(root.GetComponentInChildren<Animator>(true));
                    serialized.FindProperty("m_Controller").objectReferenceInstanceIDValue = 0;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    return PrefabUtility.SaveAsPrefabAsset(root, path);
                });
            Assert.That(result.state, Is.EqualTo(PsdHierarchyCleanupExecutionState.Partial), result.message);
            Assert.That(result.stage, Is.EqualTo("verify"));
            Assert.That(result.message, Does.Contain("serialized object references differ"));
        }

        [Test]
        public void IntroducingAMissingReferenceDuringSaveIsStillRejectedByVerification()
        {
            var result = PsdHierarchyNativeCleanupExecutor.ExecuteV2(
                context, Plan().ToString(), true,
                savePrefabAsset: (root, path) =>
                {
                    GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                    Assert.That(AssetDatabase.DeleteAsset(MaterialPath), Is.True);
                    return saved;
                });
            Assert.That(result.state, Is.EqualTo(PsdHierarchyCleanupExecutionState.Partial), result.message);
            Assert.That(result.stage, Is.EqualTo("verify"));
        }

        private void AddNestedPrefabWithMissingController(bool keepNested = true)
        {
            string controllerPath = Folder + "/Missing.controller";
            string nestedPath = Folder + "/TipButton.prefab";
            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var button = new GameObject("TipButton", typeof(RectTransform), typeof(Animator));
            try
            {
                button.GetComponent<Animator>().runtimeAnimatorController = controller;
                Assert.That(PrefabUtility.SaveAsPrefabAsset(button, nestedPath), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(button); }
            GameObject root = PrefabUtility.LoadPrefabContents(AssetPath);
            try
            {
                var nested = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(nestedPath), root.transform);
                nested.name = "TipButton";
                if (!keepNested)
                    PrefabUtility.UnpackPrefabInstance(nested, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, AssetPath), Is.Not.Null);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Assert.That(AssetDatabase.DeleteAsset(controllerPath), Is.True);
            JObject snapshot = JObject.Parse(context.hierarchySnapshotJson);
            ((JArray)snapshot["nodes"]).Add(new JObject { ["id"] = "n3", ["path"] = "ExampleView/TipButton" });
            context = new PsdHierarchyChatContext(context.projectRoot, "", AssetPath, "", "", "",
                hierarchySnapshotJson: snapshot.ToString(),
                hierarchySnapshotFingerprint: PsdHierarchyChatContextBuilder.ComputeFileFingerprint(AssetPath));
            Assert.That(ReadMissingControllerId(), Is.Not.Zero);
        }

        private static int ReadMissingControllerId()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(AssetPath);
            try
            {
                var serialized = new SerializedObject(root.GetComponentInChildren<Animator>(true));
                SerializedProperty property = serialized.FindProperty("m_Controller");
                Assert.That(property.objectReferenceValue, Is.Null);
                return property.objectReferenceInstanceIDValue;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public async Task MissingContextReturnsRejectedWithFailureStage()
        {
            byte[] original = File.ReadAllBytes(AssetPath);
            var results = new[]
            {
                await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(null, Plan().ToString()),
                await PsdHierarchyChatCleanupExecution.ApplyConfirmedAsync(null, Plan().ToString())
            };
            foreach (var result in results)
            {
                Assert.That(result.state, Is.EqualTo(PsdHierarchyCleanupExecutionState.Rejected));
                Assert.That(result.stage, Is.EqualTo("context"));
            }
            Assert.That(File.ReadAllBytes(AssetPath), Is.EqualTo(original));
        }

        [TestCase(false, 1, "preflight-cleanup")]
        [TestCase(true, 1, "save-cleanup")]
        [TestCase(true, 2, "verify-cleanup")]
        public void UnloadFailureReturnsStructuredStateWithoutRetry(bool save, int failOnUnload, string stage)
        {
            byte[] original = File.ReadAllBytes(AssetPath);
            int unloadCount = 0;
            var result = PsdHierarchyNativeCleanupExecutor.ExecuteV2(context, Plan().ToString(), save, root =>
            {
                PrefabUtility.UnloadPrefabContents(root);
                if (++unloadCount == failOnUnload) throw new System.InvalidOperationException("Injected unload failure");
            });
            Assert.That(result.state, Is.EqualTo(save ? PsdHierarchyCleanupExecutionState.Partial : PsdHierarchyCleanupExecutionState.Rejected));
            Assert.That(result.stage, Is.EqualTo(stage));
            Assert.That(result.message, Does.Contain("Injected unload failure"));
            Assert.That(unloadCount, Is.EqualTo(failOnUnload), "Cleanup must not retry after failure.");
            if (!save) Assert.That(File.ReadAllBytes(AssetPath), Is.EqualTo(original));
            else
            {
                var saved = PrefabUtility.LoadPrefabContents(AssetPath);
                try { Assert.That(saved.transform.GetChild(0).name, Is.EqualTo("Icon")); }
                finally { PrefabUtility.UnloadPrefabContents(saved); }
            }
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void SaveExceptionIsUncertainWithoutRetryOrRollback(bool writeBeforeThrow, bool failCleanup)
        {
            byte[] original = File.ReadAllBytes(AssetPath);
            int saveCount = 0;
            var result = PsdHierarchyNativeCleanupExecutor.ExecuteV2(context, Plan().ToString(), true,
                root =>
                {
                    PrefabUtility.UnloadPrefabContents(root);
                    if (failCleanup) throw new System.InvalidOperationException("Injected cleanup failure");
                },
                (root, path) =>
                {
                    saveCount++;
                    if (writeBeforeThrow) PrefabUtility.SaveAsPrefabAsset(root, path);
                    throw new System.InvalidOperationException("Injected save failure");
                });
            Assert.That(result.state, Is.EqualTo(PsdHierarchyCleanupExecutionState.Uncertain));
            Assert.That(result.stage, Is.EqualTo("save"));
            Assert.That(result.message, Does.Contain("Injected save failure"));
            if (failCleanup) Assert.That(result.message, Does.Contain("Injected cleanup failure"));
            Assert.That(saveCount, Is.EqualTo(1));
            if (!writeBeforeThrow) Assert.That(File.ReadAllBytes(AssetPath), Is.EqualTo(original));
            else
            {
                var saved = PrefabUtility.LoadPrefabContents(AssetPath);
                try { Assert.That(saved.transform.GetChild(0).name, Is.EqualTo("Icon")); }
                finally { PrefabUtility.UnloadPrefabContents(saved); }
            }
        }

        [Test]
        public void PersistedMismatchIsPartialWithoutRollback()
        {
            int unloadCount = 0;
            var result = PsdHierarchyNativeCleanupExecutor.ExecuteV2(context, Plan().ToString(), true, root =>
            {
                PrefabUtility.UnloadPrefabContents(root);
                if (++unloadCount != 1) return;
                var saved = PrefabUtility.LoadPrefabContents(AssetPath);
                try
                {
                    saved.transform.GetChild(0).name = "ChangedAfterSave";
                    PrefabUtility.SaveAsPrefabAsset(saved, AssetPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(saved); }
            });
            Assert.That(result.state, Is.EqualTo(PsdHierarchyCleanupExecutionState.Partial));
            Assert.That(result.stage, Is.EqualTo("verify"));
            Assert.That(result.message, Does.Contain("Persisted hierarchy differs"));
            Assert.That(unloadCount, Is.EqualTo(2));
            var persisted = PrefabUtility.LoadPrefabContents(AssetPath);
            try { Assert.That(persisted.transform.GetChild(0).name, Is.EqualTo("ChangedAfterSave")); }
            finally { PrefabUtility.UnloadPrefabContents(persisted); }
        }

        [Test]
        public async Task PreflightDoesNotSaveDirtyRelatedAssetsOrReplayProfiles()
        {
            const string profiles = "Assets/PSDLayoutTool2Settings/HierarchyCleanupReplayProfiles";
            string[] beforePaths = Directory.Exists(profiles)
                ? Directory.GetFiles(profiles, "*", SearchOption.AllDirectories).OrderBy(path => path).ToArray()
                : new string[0];
            byte[][] beforeProfiles = beforePaths.Select(File.ReadAllBytes).ToArray();
            byte[] prefabBytes = File.ReadAllBytes(AssetPath);
            byte[] materialBytes = File.ReadAllBytes(MaterialPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            material.color = Color.magenta;
            EditorUtility.SetDirty(material);
            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, Plan().ToString());
            Assert.That(result.success, Is.True, result.message);
            Assert.That(File.ReadAllBytes(AssetPath), Is.EqualTo(prefabBytes));
            Assert.That(File.ReadAllBytes(MaterialPath), Is.EqualTo(materialBytes));
            string[] afterPaths = Directory.Exists(profiles)
                ? Directory.GetFiles(profiles, "*", SearchOption.AllDirectories).OrderBy(path => path).ToArray()
                : new string[0];
            Assert.That(afterPaths, Is.EqualTo(beforePaths));
            for (int index = 0; index < beforePaths.Length; index++)
                Assert.That(File.ReadAllBytes(beforePaths[index]), Is.EqualTo(beforeProfiles[index]));
        }

        [Test]
        public async Task BoundNodeIdentitySurvivesSiblingNameCollision()
        {
            JObject plan = Plan();
            plan["renames"] = new JArray(
                new JObject { ["target"] = "node:n1", ["name"] = "Empty" },
                new JObject { ["target"] = "node:n2", ["name"] = "Second" });
            var result = await PsdHierarchyNativeCleanupExecutor.ApplyAsync(context, plan.ToString());
            Assert.That(result.success, Is.True, result.message);
            var saved = PrefabUtility.LoadPrefabContents(AssetPath);
            try
            {
                Assert.That(saved.transform.GetChild(0).name, Is.EqualTo("Empty"));
                Assert.That(saved.transform.GetChild(1).name, Is.EqualTo("Second"));
                Assert.That(saved.GetComponent<ScrollRect>().content, Is.SameAs(saved.transform.GetChild(0)));
            }
            finally { PrefabUtility.UnloadPrefabContents(saved); }
        }

        [Test]
        public async Task GroupMoveRenameTightenAndRemovePreserveRenderedImage()
        {
            JObject plan = Plan();
            plan["wrappers"] = new JArray(new JObject { ["id"] = "content", ["name"] = "Content", ["parent"] = "node:n0", ["siblingIndex"] = 0 });
            plan["moves"] = new JArray(new JObject { ["source"] = "node:n1", ["destination"] = "@content", ["siblingIndex"] = 0 });
            plan["tightBounds"] = new JArray(new JObject { ["target"] = "@content" });
            plan["emptyContainerRemovals"] = new JArray(new JObject { ["source"] = "node:n2" });
            plan["verify"] = new JObject
            {
                ["nodes"] = 3,
                ["hierarchy"] = new JArray(new JObject { ["path"] = "ExampleView/Content", ["childCount"] = 1 }),
                ["absentPaths"] = new JArray("ExampleView/Empty"),
                ["directChildren"] = new JArray(new JObject { ["path"] = "ExampleView", ["children"] = new JArray("Content") },
                    new JObject { ["path"] = "ExampleView/Content", ["children"] = new JArray("Icon") }),
                ["tightBounds"] = new JArray(new JObject { ["path"] = "ExampleView/Content" })
            };
            byte[] original = File.ReadAllBytes(AssetPath);
            Color32[] before = Render("before");
            var preflight = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());
            Assert.That(preflight.success, Is.True, preflight.message);
            Assert.That(File.ReadAllBytes(AssetPath), Is.EqualTo(original));
            var result = await PsdHierarchyNativeCleanupExecutor.ApplyAsync(context, plan.ToString());
            Assert.That(result.state, Is.EqualTo(PsdHierarchyCleanupExecutionState.Success), result.message);
            Assert.That(result.message, Does.Contain("VERIFY_OK"));
            var saved = PrefabUtility.LoadPrefabContents(AssetPath);
            try
            {
                Assert.That(saved.transform.childCount, Is.EqualTo(1));
                var group = (RectTransform)saved.transform.GetChild(0);
                Assert.That(group.name, Is.EqualTo("Content"));
                Assert.That(group.rect.size.x, Is.EqualTo(80).Within(0.01));
                Assert.That(group.rect.size.y, Is.EqualTo(40).Within(0.01));
                Assert.That(group.GetChild(0).name, Is.EqualTo("Icon"));
                Assert.That(saved.GetComponent<ScrollRect>().content, Is.SameAs(group.GetChild(0)));
            }
            finally { PrefabUtility.UnloadPrefabContents(saved); }
            Assert.That(Render("after"), Is.EqualTo(before), "Rendering changed after structural cleanup.");
        }

        [TestCase("v1")]
        [TestCase("missingVersion")]
        [TestCase("stringVersion")]
        [TestCase("unknownNode")]
        [TestCase("staleFingerprint")]
        [TestCase("wrongTarget")]
        [TestCase("wrongOutput")]
        [TestCase("unknownArray")]
        [TestCase("unknownObject")]
        [TestCase("unknownNestedField")]
        [TestCase("unsupportedExtraction")]
        [TestCase("unsupportedVerify")]
        [TestCase("malformedVerify")]
        [TestCase("failedVerify")]
        [TestCase("rootMove")]
        [TestCase("cyclicMove")]
        [TestCase("nonEmptyRemoval")]
        [TestCase("numericName")]
        [TestCase("unknownVerifyField")]
        [TestCase("missingWrapper")]
        [TestCase("duplicateWrapper")]
        public async Task InvalidPlansAreRejectedWithoutWriting(string scenario)
        {
            JObject plan = Plan();
            switch (scenario)
            {
                case "v1": plan["version"] = 1; break;
                case "missingVersion": plan.Remove("version"); break;
                case "stringVersion": plan["version"] = "2"; break;
                case "unknownNode": plan["renames"][0]["target"] = "node:unknown"; break;
                case "staleFingerprint": plan["snapshotFingerprint"] = "outdated"; break;
                case "wrongTarget": plan["prefabAssetPath"] = "Assets/Other.prefab"; break;
                case "wrongOutput": plan["output"]["mode"] = "new"; break;
                case "unknownArray": plan["unknownOperation"] = new JArray(1); break;
                case "unknownObject": plan["unknownOperation"] = new JObject { ["enabled"] = true }; break;
                case "unknownNestedField": plan["renames"][0]["futureOperation"] = new JObject { ["enabled"] = true }; break;
                case "unsupportedExtraction": plan["componentExtractions"] = new JArray(new JObject { ["template"] = "node:n1", ["instances"] = new JArray() }); break;
                case "unsupportedVerify": plan["verify"]["futureCheck"] = true; break;
                case "malformedVerify": plan["verify"]["directChildren"] = "invalid"; break;
                case "failedVerify": plan["verify"]["nodes"] = 500; break;
                case "rootMove": plan["moves"] = new JArray(new JObject { ["source"] = "node:n0", ["destination"] = "node:n1", ["siblingIndex"] = 0 }); break;
                case "cyclicMove": plan["moves"] = new JArray(new JObject { ["source"] = "node:n1", ["destination"] = "node:n1", ["siblingIndex"] = 0 }); break;
                case "nonEmptyRemoval": plan["emptyContainerRemovals"] = new JArray(new JObject { ["source"] = "node:n0" }); break;
                case "numericName": plan["renames"][0]["name"] = 123; break;
                case "unknownVerifyField": plan["verify"]["hierarchy"] = new JArray(new JObject { ["path"] = "ExampleView", ["childCount"] = 2, ["futureCheck"] = true }); break;
                case "missingWrapper": plan["renames"][0]["target"] = "@missing"; break;
                case "duplicateWrapper":
                    var wrapper = new JObject { ["id"] = "content", ["name"] = "Content", ["parent"] = "node:n0", ["siblingIndex"] = 0 };
                    plan["wrappers"] = new JArray(wrapper, wrapper.DeepClone());
                    break;
            }
            byte[] original = File.ReadAllBytes(AssetPath);
            var result = await PsdHierarchyChatCleanupExecution.ApplyConfirmedAsync(context, plan.ToString());
            Assert.That(result.state, Is.EqualTo(PsdHierarchyCleanupExecutionState.Rejected), result.message);
            Assert.That(result.stage, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(AssetPath), Is.EqualTo(original));
        }

        [Test]
        public async Task PrefabChangedAfterSnapshotIsRejectedWithoutWriting()
        {
            var root = PrefabUtility.LoadPrefabContents(AssetPath);
            try { root.transform.GetChild(0).name = "Changed"; PrefabUtility.SaveAsPrefabAsset(root, AssetPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            byte[] original = File.ReadAllBytes(AssetPath);
            var result = await PsdHierarchyChatCleanupExecution.ApplyConfirmedAsync(context, Plan().ToString());
            Assert.That(result.state, Is.EqualTo(PsdHierarchyCleanupExecutionState.Rejected), result.message);
            Assert.That(File.ReadAllBytes(AssetPath), Is.EqualTo(original));
        }

        private static Color32[] Render(string name)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("V2TestCamera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.enabled = false;
            GameObject instance = null;
            var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            var target = new RenderTexture(256, 256, 24);
            RenderTexture previous = RenderTexture.active;
            try
            {
                instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, scene);
                var canvas = instance.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                camera.orthographic = true;
                camera.orthographicSize = 100;
                camera.transform.position = new Vector3(0, 0, -100);
                camera.transform.rotation = Quaternion.identity;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = target;
                canvas.worldCamera = camera;
                foreach (Graphic graphic in instance.GetComponentsInChildren<Graphic>()) graphic.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                texture.Apply();
                Color32[] pixels = texture.GetPixels32();
                Assert.That(pixels.Count(pixel => pixel.g > 100), Is.GreaterThan(1000), "The render must contain the UI image.");
                string evidence = Path.Combine(Application.dataPath, "../Assets/UnityPSDLayoutTool2/.scratch/v2-review/evidence");
                Directory.CreateDirectory(evidence);
                File.WriteAllBytes(Path.Combine(evidence, "01-" + name + ".png"), texture.EncodeToPNG());
                return pixels;
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(target);
            }
        }
    }
}
