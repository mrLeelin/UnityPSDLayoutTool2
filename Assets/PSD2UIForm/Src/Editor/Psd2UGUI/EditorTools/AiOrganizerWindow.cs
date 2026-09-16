using System;
using System.IO;
using System.Linq;
using AiHierarchyAnalysisOrchestratorNamespace;
using AiJobFileStoreNamespace;
using AiPathUtilityNamespace;
using IAiJobListenerNamespace;
using UnityEditor;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    internal sealed class AiOrganizerWindow : EditorWindow, IAiJobListener
    {
        [SerializeField] Psd2UIFormConverter _source;
        string _requirements = "", _destination = "", _error, _inputFingerprint;
        byte[] _inputSourceBytes;
        AiJobContext _job;
        AiPatchDocument _patch;
        AiAnalysisPackageDocument _package;
        AiOrganizerPreview _preview;
        bool _running, _showTerminal;
        bool[] _include;
        bool[][] _instanceIncluded;
        Vector2 _scroll;
        string _feedback = "";
        string _selectedNodeId;
        bool _revising;
        readonly System.Collections.Generic.Stack<AiPatchDocument> _history = new System.Collections.Generic.Stack<AiPatchDocument>();

        internal static void Open(Psd2UIFormConverter source)
        {
            var window = GetWindow<AiOrganizerWindow>("AI 整理 UI");
            if (window._source != source) { window.ResetSession(); window._destination = ""; }
            window._source = source;
            if (string.IsNullOrEmpty(window._destination))
            {
                string output = ScriptableSingleton<Psd2UIFormSettings>.Instance.UIFormOutputDir;
                window._destination = (string.IsNullOrEmpty(output) ? "Assets" : output.Replace('\\', '/').TrimEnd('/')) + "/" + source.uiFormName + "_Organized";
            }
            window.minSize = new Vector2(620, 500);
            window.Show();
        }

        void OnDisable() { ResetSession(); }
        void OnInspectorUpdate() { if (_running) Repaint(); }
        void ResetSession()
        {
            if (_running) AiHierarchyAnalysisOrchestrator.CancelOrganizerJob(_job);
            _running = false; _job = null;
            _preview?.Dispose(); _preview = null; _patch = null; _package = null;
            _history.Clear();
        }

        void OnGUI()
        {
            if (_source != null && GUILayout.Button("在网页工作台中整理")) AiOrganizerWebServer.Open(_source);
            EditorGUILayout.LabelField("整理层级 → 改名 → 抽取公共 Prefab", EditorStyles.boldLabel);
            EditorGUILayout.ObjectField("PSD 编辑树", _source, typeof(Psd2UIFormConverter), true);
            EditorGUILayout.HelpBox("先在下方整理树中修改名称、父节点和顺序，或填写反馈让 AI 修改当前方案。满意后生成预览并应用；确认应用之前不会修改原文件。已有结果的源更新合并尚未实现，请使用新输出文件夹。", MessageType.Info);
            using (new EditorGUI.DisabledScope(_running))
            {
                _requirements = EditorGUILayout.TextField("补充整理要求", _requirements);
                EditorGUI.BeginChangeCheck();
                _destination = EditorGUILayout.TextField("新输出文件夹", _destination);
                if (EditorGUI.EndChangeCheck()) InvalidatePreview();
                _showTerminal = EditorGUILayout.Toggle("显示 CLI 终端", _showTerminal);
                if (_patch == null && GUILayout.Button("AI 分析整理方案")) StartAnalysis();
            }
            if (_running)
            {
                string status = "AI 分析中…";
                if (_job != null && AiJobFileStore.TryReadJson<AiJobStatusDocument>(_job.StatusPath, out var value) && value != null)
                    status = value.stage + "  " + value.detail;
                EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("取消分析")) { AiHierarchyAnalysisOrchestrator.CancelOrganizerJob(_job); _running = false; _job = null; }
            }
            if (!string.IsNullOrEmpty(_error)) EditorGUILayout.HelpBox(_error, MessageType.Error);
            if (_job != null && GUILayout.Button("查看任务文件与日志")) EditorUtility.RevealInFinder(_job.JobDirectory);
            if (_patch == null) return;
            using (new EditorGUI.DisabledScope(_running))
            {
                _feedback = EditorGUILayout.TextField("修改反馈", _feedback);
                if (GUILayout.Button("按反馈调整当前方案")) StartAnalysis(true);
                using (new EditorGUI.DisabledScope(_history.Count == 0))
                    if (GUILayout.Button("撤销上一次层级或 AI 修改")) { ReplacePlan(_history.Pop()); }
            }
            using (new EditorGUI.DisabledScope(_running))
            {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawEditableTree();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("公共 Prefab 建议", EditorStyles.boldLabel);
            for (int i = 0; i < _patch.components.Count; i++)
            {
                var component = _patch.components[i];
                EditorGUI.BeginChangeCheck();
                _include[i] = EditorGUILayout.ToggleLeft(component.reason ?? component.name, _include[i]);
                component.name = EditorGUILayout.TextField("名称", component.name);
                EditorGUILayout.LabelField("结构模式", component.mode == "states" ? "保留不同结构" : "同结构");
                if (component.rootIds != null)
                {
                    for (int index = 0; index < component.rootIds.Length; index++)
                        _instanceIncluded[i][index] = EditorGUILayout.ToggleLeft(component.rootIds[index], _instanceIncluded[i][index]);
                }
                if (EditorGUI.EndChangeCheck()) InvalidatePreview();
            }
            if (_patch.components.Count == 0) EditorGUILayout.LabelField("本次未建议抽取公共组件。可补充要求后重新分析。");
            if (_preview != null)
            {
                EditorGUILayout.LabelField("将保存到", _preview.TargetPath, EditorStyles.wordWrappedLabel);
                foreach (var plan in _preview.Extractions)
                {
                    EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(plan.OutputPath), EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("实例", string.Join(", ", plan.Names), EditorStyles.wordWrappedLabel);
                    if (plan.UsesStates)
                    {
                        EditorGUILayout.LabelField("公共成员", string.Join(", ", plan.CommonMembers), EditorStyles.wordWrappedLabel);
                        foreach (var state in plan.States) EditorGUILayout.LabelField(state.Name, string.Join(", ", state.Members), EditorStyles.wordWrappedLabel);
                        for (int i = 0; i < plan.SourceStates.Count; i++) EditorGUILayout.LabelField(plan.Names[i], plan.SourceStates[i].State);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("生成完整预览")) BuildPreview();
            using (new EditorGUI.DisabledScope(_preview == null))
                if (GUILayout.Button("应用整理并保存 UI 与公共 Prefab")) Publish();
            }
        }

        void InvalidatePreview() { _preview?.Dispose(); _preview = null; }

        AiPatchDocument SelectedPlan()
        {
            var patch = JsonUtility.FromJson<AiPatchDocument>(JsonUtility.ToJson(_patch));
            for (int index = 0; index < patch.components.Count; index++)
                if (patch.components[index].rootIds != null)
                    patch.components[index].rootIds = patch.components[index].rootIds.Where((id, instance) => _instanceIncluded[index][instance]).ToArray();
            patch.components = patch.components.Where((component, index) => _include[index]).ToList();
            return patch;
        }

        void ReplacePlan(AiPatchDocument patch)
        {
            InvalidatePreview();
            _patch = patch;
            _patch.components = _patch.components ?? new System.Collections.Generic.List<AiOrganizerComponent>();
            _include = Enumerable.Repeat(true, _patch.components.Count).ToArray();
            _instanceIncluded = _patch.components.Select(component => Enumerable.Repeat(true, component.rootIds?.Length ?? 0).ToArray()).ToArray();
        }

        void DrawEditableTree()
        {
            EditorGUILayout.LabelField("整理后的层级树（点击节点修改）", EditorStyles.boldLabel);
            var tree = AiOrganizerPlanEditing.Tree(_package, _patch);
            foreach (var node in tree)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(node.Depth * 14);
                    if (GUILayout.Toggle(_selectedNodeId == node.Id, node.Name + "  [" + node.Type + "]", "Button")) _selectedNodeId = node.Id;
                }
            }
            var selected = tree.FirstOrDefault(node => node.Id == _selectedNodeId);
            if (selected == null) return;
            EditorGUILayout.LabelField("节点 ID", selected.Id);
            var parents = new[] { "root" }.Concat(tree.Where(node => node.Id != selected.Id).Select(node => node.Id)).ToArray();
            var labels = parents.Select(id => id == "root" ? "根节点" : tree.First(node => node.Id == id).Name + "  [" + id + "]").ToArray();
            EditorGUI.BeginChangeCheck();
            string name = EditorGUILayout.DelayedTextField("节点名称", selected.Name);
            int parentIndex = EditorGUILayout.Popup("父节点", Math.Max(0, Array.IndexOf(parents, selected.Parent)), labels);
            int order = EditorGUILayout.DelayedIntField("同级顺序（-1 为末尾）", selected.Index == int.MaxValue ? -1 : selected.Index);
            if (EditorGUI.EndChangeCheck())
            {
                try
                {
                    var next = AiOrganizerPlanEditing.Edit(_package, _patch, selected.Id, name, parents[parentIndex], order);
                    _history.Push(SelectedPlan());
                    _patch = next; InvalidatePreview(); _error = null;
                }
                catch (Exception ex) { _error = ex.Message; }
            }
        }

        void StartAnalysis(bool revise = false)
        {
            _error = null;
            try
            {
                string revision = revise ? AiOrganizerPlanEditing.RevisionInstructions(SelectedPlan(), _feedback) : "";
                if (revise) CheckInput();
                else ResetSession();
                _revising = revise;
                if (_source == null) throw new InvalidOperationException("请从 PSD 编辑树的 Inspector 打开 AI 整理 UI。");
                var sourcePath = PsdCommonPrefabPersistence.SourcePath(_source.gameObject);
                var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
                if (string.IsNullOrEmpty(sourcePath) || (stage != null && stage.assetPath == sourcePath && stage.scene.isDirty))
                    throw new InvalidOperationException("请先保存 PSD 编辑树，再开始分析。");
                string project = Directory.GetParent(Application.dataPath).FullName;
                string skill = AiPathUtility.ResolvePluginRelativePath(project, "AIPrompts/UIOrganizer/SKILL.md");
                string instructions = File.ReadAllText(skill) + "\n\nUser organization requirements:\n" + _requirements + revision;
                _inputSourceBytes = File.ReadAllBytes(sourcePath);
                _running = true;
                if (!AiHierarchyAnalysisOrchestrator.StartRecognitionJob(Psd2UIFormConverterEditor.GetOrCreate(_source), out string error,
                    this, !_showTerminal, instructions, job => { _job = job; _inputFingerprint = PsdExtractionSourceFingerprint.Capture(_source.gameObject); }))
                    throw new InvalidOperationException(error);
            }
            catch (Exception ex) { _running = false; _error = ex.Message; }
        }

        public void OnJobCompleted(AiJobContext job)
        {
            if (this == null || !_running || _job == null || _job.JobId != job.JobId) return;
            _running = false;
            try
            {
                CheckInput();
                if (!AiJobFileStore.TryReadJson<AiRecognitionCombinedResultDocument>(job.RecognitionCombinedPath, out var combined) || combined.organizerVersion != "1.0")
                    throw new InvalidOperationException("AI 未返回完整整理协议，请查看日志后重新分析。");
                if (!AiJobFileStore.TryReadJson<AiPatchDocument>(job.PatchPath, out var patch) ||
                    !AiJobFileStore.TryReadJson<AiAnalysisPackageDocument>(job.AnalysisPackagePath, out var package))
                    throw new InvalidOperationException("整理结果或分析快照缺失。");
                if (!new AiPatchValidatorNamespace.AiPatchValidator().ValidatePatch(patch, package, out string validation))
                    throw new InvalidOperationException(validation);
                if (_revising && _patch != null) _history.Push(SelectedPlan());
                _package = package;
                ReplacePlan(patch);
            }
            catch (Exception ex) { _error = ex.Message; }
            Repaint();
        }

        public void OnJobFailed(AiJobContext job, string message)
        {
            if (this == null || _job == null || _job.JobId != job.JobId) return;
            _running = false; _error = message; Repaint();
        }

        void CheckInput()
        {
            if (_source == null || PsdExtractionSourceFingerprint.Capture(_source.gameObject) != _inputFingerprint)
                throw new InvalidOperationException("编辑树或配置已变化，请重新分析。");
        }

        void BuildPreview()
        {
            InvalidatePreview(); _error = null;
            try
            {
                CheckInput();
                var patch = SelectedPlan();
                _preview = AiOrganizerPreview.Build(Psd2UIFormConverterEditor.GetOrCreate(_source), patch, _package, _destination, _inputFingerprint, _inputSourceBytes);
            }
            catch (Exception ex) { _error = ex.Message; }
        }

        void Publish()
        {
            _error = null;
            try
            {
                CheckInput();
                _preview.Publish(Psd2UIFormConverterEditor.GetOrCreate(_source));
                InvalidatePreview(); _patch = null;
                ShowNotification(new GUIContent("整理、改名和公共 Prefab 已保存"));
            }
            catch (Exception ex) { InvalidatePreview(); _error = ex.Message; }
        }
    }
}
