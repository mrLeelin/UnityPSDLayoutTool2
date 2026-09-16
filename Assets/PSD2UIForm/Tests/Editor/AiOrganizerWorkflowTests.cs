using System;
using System.IO;
using System.Linq;
using LayerNodeIdUtilityNamespace;
using NUnit.Framework;
using UGF.EditorTools.Psd2UGUI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Collections;
using AiHierarchyAnalysisOrchestratorNamespace;
using AiJobFileStoreNamespace;
using IAiJobListenerNamespace;
using UnityEngine.TestTools;

namespace Psd2UIForm.Tests
{
    public class AiOrganizerWorkflowTests
    {
        const string Folder = "Assets/__AiOrganizerTests";
        const string Source = Folder + "/Source.prefab";
        const string Fixture = "Assets/UnityPSDLayoutTool2/Assets/PSD2UIForm/Examples/Psd2UguiForm_UIFormEditor.prefab";
        GameObject _source;
        bool _ownsFolder;
        string _images, _forms;
        bool _useForms;
        Object[] _selection;

        [SetUp] public void Setup()
        {
            _ownsFolder = false;
            _selection = Selection.objects;
            var settings = UGF.EditorTools.Psd2UGUI.ScriptableSingleton<Psd2UIFormSettings>.Instance;
            _images = settings.UIImagesOutputDir; _forms = settings.UIFormOutputDir; _useForms = settings.UseUIFormOutputDir;
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False);
            AssetDatabase.CreateFolder("Assets", "__AiOrganizerTests"); _ownsFolder = true;
            Assert.That(AssetDatabase.CopyAsset(Fixture, Source), Is.True);
            _source = PrefabUtility.LoadPrefabContents(Source);
            var nodes = _source.GetComponentsInChildren<PsdLayerNode>(true);
            var selected = nodes.Where(node => node.name == "Gradient" || node.name == "Glow").ToArray();
            Assert.That(selected.Length, Is.EqualTo(2));
            foreach (var node in selected) node.transform.SetParent(_source.transform, true);
            foreach (Transform child in _source.transform.Cast<Transform>().ToArray())
                if (!selected.Any(node => node.transform == child)) Object.DestroyImmediate(child.gameObject);
            _source.GetComponent<Psd2UIFormConverter>().uiFormName = "OrganizedTest";
            PrefabUtility.SaveAsPrefabAsset(_source, Source);
        }

        [TearDown] public void Cleanup()
        {
            // 发布会选中结果；删除测试目录前先释放 Inspector 对测试 Prefab 的选中状态。
            Selection.objects = _selection.Where(item => item != null).ToArray();
            if (_source != null) PrefabUtility.UnloadPrefabContents(_source);
            var settings = UGF.EditorTools.Psd2UGUI.ScriptableSingleton<Psd2UIFormSettings>.Instance;
            settings.UIImagesOutputDir = _images; settings.UIFormOutputDir = _forms; settings.UseUIFormOutputDir = _useForms;
            UGF.EditorTools.Psd2UGUI.ScriptableSingleton<Psd2UIFormSettings>.SaveInstance();
            if (_ownsFolder) AssetDatabase.DeleteAsset(Folder);
        }

        void Documents(out AiAnalysisPackageDocument package, out AiPatchDocument patch)
        {
            var editor = Psd2UIFormConverterEditor.GetOrCreate(_source.GetComponent<Psd2UIFormConverter>());
            package = new AiAnalysisPackageDocument { version = "4.0", treeHash = "controlled-input" };
            patch = new AiPatchDocument { version = "2.0", treeHash = package.treeHash };
            foreach (var node in _source.GetComponentsInChildren<PsdLayerNode>(true))
            {
                string id = LayerNodeIdUtility.GetStableNodeId(editor, node);
                package.nodes.Add(new AiAnalysisNodeEntry { id = id, name = node.name, uiType = node.UIType.ToString(), layerType = "Layer", parentId = "" });
                patch.analysis.Add(new AiAuditEntry { targetId = id, currentUIType = node.UIType.ToString(), predictedUIType = node.UIType.ToString(), verdict = "correct", confidence = 1, reason = "Controlled fixture" });
                patch.operations.Add(new AiPatchOperation { op = "rename_node", targetId = id, name = "Organized_" + node.name, reason = "Readable name", confidence = 1 });
            }
            patch.components.Add(new AiOrganizerComponent { name = "SharedDecoration", mode = "same", rootIds = package.nodes.Select(node => node.id).ToArray() });
        }

        [Test] public void PreviewPublish_PreservesSourceUntilApplyAndProducesReusableGraph()
        {
            Documents(out var package, out var patch);
            byte[] original = File.ReadAllBytes(Source);
            var editor = Psd2UIFormConverterEditor.GetOrCreate(_source.GetComponent<Psd2UIFormConverter>());
            using (var preview = AiOrganizerPreview.Build(editor, patch, package, Folder + "/Published"))
            {
                Assert.That(File.ReadAllBytes(Source), Is.EqualTo(original));
                Assert.That(preview.Extractions.Count, Is.EqualTo(1));
                Assert.That(AssetDatabase.IsValidFolder(Folder + "/Published"), Is.False);
                preview.Publish(editor);
                var savedSource = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
                Assert.That(savedSource.GetComponentsInChildren<PsdLayerNode>(true).All(node => node.name.StartsWith("Organized_")), Is.True);
                Assert.That(PsdCommonPrefabPersistence.TryReuse(savedSource, preview.TargetPath, out var reused), Is.True);
                Assert.That(reused, Is.Not.Null);
                Assert.That(PsdCommonPrefabPersistence.Find(preview.TargetPath).rules.Count, Is.EqualTo(1));
                var settings = UGF.EditorTools.Psd2UGUI.ScriptableSingleton<Psd2UIFormSettings>.Instance;
                Assert.That(settings.UIImagesOutputDir, Is.EqualTo(_images));
                Assert.That(settings.UIFormOutputDir, Is.EqualTo(_forms));
                Assert.That(settings.UseUIFormOutputDir, Is.EqualTo(_useForms));
            }
        }

        [Test] public void WebSession_RendersVersionsRejectsInvalidResultAndRestoresPreviousPlan()
        {
            Documents(out var package, out var patch);
            byte[] original = File.ReadAllBytes(Source);
            using (var session = new AiOrganizerWebSession(_source.GetComponent<Psd2UIFormConverter>()))
            {
                session.State.destination = Folder + "/WebPublished";
                session.CaptureInput();
                session.AcceptResult(patch, package, "First");
                Assert.That(session.State.activeVersion, Is.EqualTo(1));
                Assert.That(session.PreviewPng.Length, Is.GreaterThan(100));
                Assert.That(session.State.nodes.Count(n => n.bounds != null && n.bounds.w > 0 && n.bounds.h > 0), Is.EqualTo(2));
                var texture = new Texture2D(2, 2);
                try
                {
                    Assert.IsTrue(texture.LoadImage(session.PreviewPng));
                    Assert.That(texture.GetPixels32().Count(p => p.a > 0), Is.GreaterThan(20), "真实 UI 渲染不能是全透明图片。");
                }
                finally { Object.DestroyImmediate(texture); }
                var second = JsonUtility.FromJson<AiPatchDocument>(JsonUtility.ToJson(patch));
                second.operations[0].name = "FeedbackName";
                session.AcceptResult(second, package, "Rename from feedback");
                Assert.That(session.State.activeVersion, Is.EqualTo(2));
                Assert.That(session.State.nodes.Any(n => n.name == "FeedbackName"), Is.True);
                byte[] previewBytes = session.PreviewPng;
                var invalid = JsonUtility.FromJson<AiPatchDocument>(JsonUtility.ToJson(second));
                invalid.treeHash = "stale";
                Assert.Throws<InvalidOperationException>(() => session.AcceptResult(invalid, package, "Invalid"));
                Assert.That(session.State.activeVersion, Is.EqualTo(2));
                Assert.That(session.PreviewPng, Is.SameAs(previewBytes));
                session.Execute(new AiOrganizerWebCommand { action = "restore", sessionId = session.State.sessionId,
                    revision = session.State.revision, version = 1 });
                Assert.That(session.State.nodes.Any(n => n.name == "FeedbackName"), Is.False);
                Assert.That(File.ReadAllBytes(Source), Is.EqualTo(original));
                session.Execute(new AiOrganizerWebCommand { action = "apply", sessionId = session.State.sessionId, revision = session.State.revision });
                Assert.That(session.State.publishedPath, Is.EqualTo(Folder + "/WebPublished/OrganizedTest.prefab"));
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(session.State.publishedPath), Is.Not.Null);
            }
        }

        [Test] public void ChangedSinceAnalysis_RejectsPreviewBeforeWriting()
        {
            Documents(out var package, out var patch);
            var editor = Psd2UIFormConverterEditor.GetOrCreate(_source.GetComponent<Psd2UIFormConverter>());
            string fingerprint = PsdExtractionSourceFingerprint.Capture(_source);
            byte[] bytes = File.ReadAllBytes(Source);
            _source.transform.GetChild(0).name = "ChangedAfterAnalysis";
            PrefabUtility.SaveAsPrefabAsset(_source, Source);
            byte[] changed = File.ReadAllBytes(Source);
            Assert.Throws<InvalidOperationException>(() => AiOrganizerPreview.Build(editor, patch, package, Folder + "/Published", fingerprint, bytes));
            Assert.That(File.ReadAllBytes(Source), Is.EqualTo(changed));
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/Published"), Is.False);
        }

        [Test] public void GroupThenExtract_UsesNewGroupIdentity()
        {
            Documents(out var package, out var patch);
            var ids = package.nodes.Select(node => node.id).ToArray();
            for (int i = 0; i < ids.Length; i++)
            {
                string groupId = "gen:organizer:" + i;
                patch.operations.Insert(i, new AiPatchOperation { op = "create_group", id = groupId, parentId = "root", name = "DecorationGroup" + i, uiType = "Null", confidence = 1, reason = "Functional grouping" });
                patch.operations.Add(new AiPatchOperation { op = "move_node", targetId = ids[i], newParentId = groupId, insertIndex = 0, confidence = 1, reason = "Group membership" });
                patch.components[0].rootIds[i] = groupId;
            }
            AiPatchValidatorNamespace.AiPatchValidator.NormalizeOperationOrder(patch);
            patch = AiOrganizerPlanEditing.Edit(package, patch, "gen:organizer:0", "DecorationGroupManuallyRenamed", "root", 0);
            var editor = Psd2UIFormConverterEditor.GetOrCreate(_source.GetComponent<Psd2UIFormConverter>());
            using (var preview = AiOrganizerPreview.Build(editor, patch, package, Folder + "/Published"))
            {
                preview.Publish(editor);
                var saved = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
                Assert.That(saved.transform.childCount, Is.EqualTo(2));
                Assert.That(saved.transform.GetChild(0).name, Does.StartWith("DecorationGroup"));
                Assert.That(saved.transform.GetChild(0).GetChild(0).name, Does.StartWith("Organized_"));
                Assert.That(PsdCommonPrefabPersistence.Find(preview.TargetPath).rules.Count, Is.EqualTo(1));
            }
        }

        [TestCase(GUIType.TMPButton)]
        [TestCase(GUIType.TMPText)]
        [TestCase(GUIType.TMPDropdown)]
        [TestCase(GUIType.TMPInputField)]
        [TestCase(GUIType.TMPToggle)]
        [TestCase(GUIType.RawImage)]
        public void StructureValidation_AcceptsSupportedGenerationAliases(GUIType type)
        {
            foreach (var node in _source.GetComponentsInChildren<PsdLayerNode>(true)) node.UIType = type;
            var warnings = new System.Collections.Generic.List<string>();
            new AiHierarchyStructureValidatorNamespace.AiHierarchyStructureValidator().ValidateHierarchy(
                Psd2UIFormConverterEditor.GetOrCreate(_source.GetComponent<Psd2UIFormConverter>()), warnings);
            Assert.That(warnings, Is.Empty);
        }

        [Test] public void InvalidExtraction_DoesNotSavePartialRenames()
        {
            Documents(out var package, out var patch);
            patch.components[0].rootIds[1] = "psd:does-not-exist";
            byte[] original = File.ReadAllBytes(Source);
            Assert.Throws<InvalidOperationException>(() => AiOrganizerPreview.Build(
                Psd2UIFormConverterEditor.GetOrCreate(_source.GetComponent<Psd2UIFormConverter>()), patch, package, Folder + "/Published"));
            Assert.That(File.ReadAllBytes(Source), Is.EqualTo(original));
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/Published"), Is.False);
        }

        [Test] public void ChangedSource_RejectsPublishAndDisposeRemovesCandidate()
        {
            Documents(out var package, out var patch);
            var editor = Psd2UIFormConverterEditor.GetOrCreate(_source.GetComponent<Psd2UIFormConverter>());
            string candidate;
            byte[] original = File.ReadAllBytes(Source);
            using (var preview = AiOrganizerPreview.Build(editor, patch, package, Folder + "/Published"))
            {
                candidate = preview.PreviewPath;
                _source.transform.GetChild(0).name = "ChangedWhilePreviewing";
                Assert.Throws<InvalidOperationException>(() => preview.Publish(editor));
                Assert.That(File.ReadAllBytes(Source), Is.EqualTo(original));
            }
            Assert.That(File.Exists(candidate), Is.False);
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/Published"), Is.False);
        }

        [Test, Explicit("Replays the local seven-day AI result against an isolated authoring copy.")]
        public void SevenDaySavedResult_BuildsCompletePreview()
        {
            PrefabUtility.UnloadPrefabContents(_source);
            _source = null;
            AssetDatabase.DeleteAsset(Source);
            Assert.That(AssetDatabase.CopyAsset("Assets/UnityPSDLayoutTool2/Assets/PSD2UIForm/Examples/7日任务拆分_UIFormEditor.prefab", Source), Is.True);
            _source = PrefabUtility.LoadPrefabContents(Source);
            string job = "Library/Psd2UIForm/AiJobs/7日任务拆分.psd/";
            Assert.That(AiJobFileStore.TryReadJson<AiPatchDocument>(job + "response/patch.json", out var patch), Is.True);
            Assert.That(AiJobFileStore.TryReadJson<AiAnalysisPackageDocument>(job + "request/analysis-package.json", out var package), Is.True);
            byte[] original = File.ReadAllBytes(Source);
            using (var preview = AiOrganizerPreview.Build(Psd2UIFormConverterEditor.GetOrCreate(_source.GetComponent<Psd2UIFormConverter>()), patch, package, Folder + "/Published"))
            {
                Assert.That(File.Exists(preview.PreviewPath), Is.True);
                Assert.That(preview.Extractions.Count, Is.EqualTo(patch.components.Count));
                Assert.That(File.ReadAllBytes(Source), Is.EqualTo(original));
            }
        }

        sealed class Listener : IAiJobListener
        {
            internal bool Done;
            internal string Error;
            public void OnJobCompleted(AiJobContext context) { Done = true; }
            public void OnJobFailed(AiJobContext context, string error) { Error = error; Done = true; }
        }

        [Test] public void RecognitionNormalization_RetainsNamesAndOwnerExtractionIds()
        {
            Documents(out var package, out var ignored);
            var combined = new AiRecognitionCombinedResultDocument { version = "2.0", treeHash = package.treeHash, organizerVersion = "1.0" };
            foreach (var node in package.nodes)
            {
                combined.owners.Add(new AiRecognitionOwnerEntry { ownerId = node.id, ownerType = "Image", carrierNodeId = node.id, memberNodeIds = new[] { node.id }, confidence = 1, reason = "Image" });
                combined.nodeLabels.Add(new AiRecognitionNodeLabelEntry { nodeId = node.id, currentUIType = "Image", labelType = "Image", confidence = 1, reason = "Image" });
                combined.renames.Add(new AiOrganizerRename { nodeId = "owner:" + node.id, name = "New_" + node.name });
            }
            combined.components.Add(new AiOrganizerComponent { name = "Shared", mode = "same", rootIds = package.nodes.Select(node => "owner:" + node.id).ToArray() });
            string path = Folder + "/combined.json";
            File.WriteAllText(path, JsonUtility.ToJson(combined));
            var parser = new AiRecognitionResultParserNamespace.AiRecognitionResultParser();
            Assert.That(parser.TryLoadCombinedRecognitionResult(new AiJobContext { RecognitionCombinedPath = path }, package, out var normalized, out string error), Is.True, error);
            Assert.That(new AiPatchPlannerNamespace.AiPatchPlanner().TryBuildPatchFromCombinedRecognition(package, normalized, out var patch, out error), Is.True, error);
            Assert.That(patch.components.Count, Is.EqualTo(1));
            Assert.That(patch.components[0].rootIds, Is.EqualTo(package.nodes.Select(node => node.id).ToArray()));
            Assert.That(patch.operations.Count(operation => operation.op == "rename_node"), Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator WebBridge_QueuedHttpRequestReceivesMainThreadRejection()
        {
            string url = AiOrganizerWebServer.Open(_source.GetComponent<Psd2UIFormConverter>(), false);
            var uri = new Uri(url);
            string command = JsonUtility.ToJson(new AiOrganizerWebCommand { sessionId = AiOrganizerWebServer.Current.Session.State.sessionId, revision = -1, action = "apply" });
            try
            {
                var request = System.Threading.Tasks.Task.Run(() =>
                {
                    var http = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri.GetLeftPart(UriPartial.Authority) + "/command");
                    http.Method = "POST"; http.ContentType = "application/json"; http.Timeout = 10000;
                    http.Headers["X-Organizer-Token"] = uri.Fragment.Substring(1);
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(command);
                    http.ContentLength = bytes.Length;
                    using (var stream = http.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
                    using (var response = (System.Net.HttpWebResponse)http.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream()))
                        return Tuple.Create(response.StatusCode, reader.ReadToEnd());
                });
                DateTime deadline = DateTime.UtcNow.AddSeconds(15);
                while (!request.IsCompleted && DateTime.UtcNow < deadline) yield return null;
                Assert.That(request.IsCompleted, Is.True, "HTTP request timed out.");
                Assert.That(request.IsFaulted, Is.False, request.Exception?.ToString());
                Assert.That(request.Result.Item1, Is.EqualTo(System.Net.HttpStatusCode.Accepted));
                var state = AiOrganizerWebServer.Current.Session.State;
                while (string.IsNullOrEmpty(state.lastRequestId) && DateTime.UtcNow < deadline) yield return null;
                Assert.That(state.lastRequestId, Is.Not.Empty);
                Assert.That(request.Result.Item2, Does.Contain(state.lastRequestId));
                Assert.That(state.commandError, Does.Contain("页面版本已过期"));
                Assert.That(state.activeVersion, Is.Zero);
            }
            finally { AiOrganizerWebServer.StopCurrent(); }
        }

        [UnityTest, Explicit("Runs two real AI rounds through the web session using an isolated source.")]
        public IEnumerator WebSession_RealCliCreatesPreviewThenRevisesSelectedNode()
        {
            var editor = Psd2UIFormConverterEditor.GetOrCreate(_source.GetComponent<Psd2UIFormConverter>());
            string psdCopy = Folder + "/WebCliFixture.psd";
            Assert.That(AssetDatabase.CopyAsset(editor.GetSourcePsdAssetPath(), psdCopy), Is.True);
            var source = _source.GetComponent<Psd2UIFormConverter>();
            source.psdAssetPath = psdCopy;
            source.psdAsset = AssetDatabase.LoadAssetAtPath<Sprite>(psdCopy);
            PrefabUtility.SaveAsPrefabAsset(_source, Source);
            byte[] original = File.ReadAllBytes(Source);
            using (var session = new AiOrganizerWebSession(source))
            {
                session.State.destination = Folder + "/WebCliOutput";
                session.Execute(new AiOrganizerWebCommand { sessionId = session.State.sessionId, revision = session.State.revision,
                    action = "start", feedback = "Integration smoke: preserve both source Image nodes and their Image types. Return one Image owner per existing node, with that node as carrier and sole member. Only rename them meaningfully. Do not create new group nodes, roles, or common prefabs. Return the complete organizer protocol." });
                DateTime deadline = DateTime.UtcNow.AddSeconds(240);
                while (session.State.running && DateTime.UtcNow < deadline) yield return null;
                Assert.That(session.State.running, Is.False, "首次 AI 分析超时。");
                Assert.That(session.State.error, Is.Empty);
                Assert.That(session.State.activeVersion, Is.EqualTo(1));
                Assert.That(session.PreviewPng.Length, Is.GreaterThan(100));
                string nodeId = session.State.nodes.First().id;
                session.Execute(new AiOrganizerWebCommand { sessionId = session.State.sessionId, revision = session.State.revision,
                    action = "revise", nodeId = nodeId,
                    feedback = "Rename only the selected node to FeedbackDecoration. Keep all other accepted names, Image types, structure and empty common prefab list unchanged. Return the complete replacement organizer result." });
                deadline = DateTime.UtcNow.AddSeconds(240);
                while (session.State.running && DateTime.UtcNow < deadline) yield return null;
                Assert.That(session.State.running, Is.False, "修订 AI 分析超时。");
                Assert.That(session.State.error, Is.Empty);
                Assert.That(session.State.activeVersion, Is.EqualTo(2));
                Assert.That(session.State.nodes.Single(n => n.id == nodeId).name, Is.EqualTo("FeedbackDecoration"));
                Assert.That(File.ReadAllBytes(Source), Is.EqualTo(original));
            }
        }

        [UnityTest, Explicit("Invokes the configured external AI CLI; requires its existing authentication.")]
        public IEnumerator RealCli_ReturnsUnifiedPlanAndPublishes()
        {
            var listener = new Listener();
            AiJobContext job = null;
            var editor = Psd2UIFormConverterEditor.GetOrCreate(_source.GetComponent<Psd2UIFormConverter>());
            string fixtureCopy = Folder + "/OrganizerCliFixture.psd";
            Assert.That(AssetDatabase.CopyAsset(editor.GetSourcePsdAssetPath(), fixtureCopy), Is.True);
            var shell = _source.GetComponent<Psd2UIFormConverter>();
            shell.psdAssetPath = fixtureCopy;
            shell.psdAsset = AssetDatabase.LoadAssetAtPath<Sprite>(fixtureCopy);
            PrefabUtility.SaveAsPrefabAsset(_source, Source);
            string instructions = File.ReadAllText("Assets/UnityPSDLayoutTool2/Assets/PSD2UIForm/AIPrompts/UIOrganizer/SKILL.md") +
                "\nIntegration smoke: only two decorative Image nodes exist. Keep their Image types; propose meaningful distinct renames and exactly one same-structure component using their two real node IDs. Do not create extra owners or roles.";
            Assert.That(AiHierarchyAnalysisOrchestrator.StartRecognitionJob(editor, out string error, listener, true,
                instructions, context => job = context), Is.True, error);
            try
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(180);
                while (!listener.Done && DateTime.UtcNow < deadline) yield return null;
                Assert.That(listener.Done, Is.True, "CLI did not finish in 180 seconds");
                Assert.That(listener.Error, Is.Null);
                Assert.That(AiJobFileStore.TryReadJson<AiPatchDocument>(job.PatchPath, out var patch), Is.True);
                Assert.That(AiJobFileStore.TryReadJson<AiAnalysisPackageDocument>(job.AnalysisPackagePath, out var package), Is.True);
                Assert.That(patch.components.Count, Is.EqualTo(1));
                Assert.That(patch.operations.Any(operation => operation.op == "rename_node"), Is.True);
                using (var preview = AiOrganizerPreview.Build(editor, patch, package, Folder + "/Published"))
                {
                    preview.Publish(editor);
                    Assert.That(File.Exists(preview.TargetPath), Is.True);
                    Assert.That(PsdCommonPrefabPersistence.Find(preview.TargetPath).rules.Count, Is.EqualTo(1));
                }
            }
            finally { AiHierarchyAnalysisOrchestrator.CancelOrganizerJob(job); }
        }
    }
}
