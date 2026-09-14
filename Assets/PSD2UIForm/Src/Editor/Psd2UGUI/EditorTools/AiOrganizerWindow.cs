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
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("整理层级 → 改名 → 抽取公共 Prefab", EditorStyles.boldLabel);
            EditorGUILayout.ObjectField("PSD 编辑树", _source, typeof(Psd2UIFormConverter), true);
            EditorGUILayout.HelpBox("AI 提出完整方案，你可以修改名称或排除实例。生成预览后，统一保存编辑树和含公共组件的 UI。已有结果的源更新合并尚未实现，请使用新输出文件夹。", MessageType.Info);
            using (new EditorGUI.DisabledScope(_running))
            {
                _requirements = EditorGUILayout.TextField("补充整理要求", _requirements);
                EditorGUI.BeginChangeCheck();
                _destination = EditorGUILayout.TextField("新输出文件夹", _destination);
                if (EditorGUI.EndChangeCheck()) InvalidatePreview();
                _showTerminal = EditorGUILayout.Toggle("显示 CLI 终端", _showTerminal);
                if (GUILayout.Button("AI 分析整理方案")) StartAnalysis();
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
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("层级、类型与命名", EditorStyles.boldLabel);
            foreach (var op in _patch.operations)
            {
                if (op.op == "rename_node")
                {
                    EditorGUI.BeginChangeCheck();
                    op.name = EditorGUILayout.TextField(op.targetId, op.name);
                    if (EditorGUI.EndChangeCheck()) InvalidatePreview();
                }
                else EditorGUILayout.LabelField(op.op + "  " + (op.targetId ?? op.id),
                    op.name ?? op.newParentId ?? op.uiType ?? "", EditorStyles.wordWrappedLabel);
            }
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

        void InvalidatePreview() { _preview?.Dispose(); _preview = null; }

        void StartAnalysis()
        {
            ResetSession(); _error = null;
            try
            {
                if (_source == null) throw new InvalidOperationException("请从 PSD 编辑树的 Inspector 打开 AI 整理 UI。");
                var sourcePath = PsdCommonPrefabPersistence.SourcePath(_source.gameObject);
                var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
                if (string.IsNullOrEmpty(sourcePath) || (stage != null && stage.assetPath == sourcePath && stage.scene.isDirty))
                    throw new InvalidOperationException("请先保存 PSD 编辑树，再开始分析。");
                string project = Directory.GetParent(Application.dataPath).FullName;
                string skill = AiPathUtility.ResolvePluginRelativePath(project, "AIPrompts/UIOrganizer/SKILL.md");
                string instructions = File.ReadAllText(skill) + "\n\nUser organization requirements:\n" + _requirements;
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
                if (!AiJobFileStore.TryReadJson<AiPatchDocument>(job.PatchPath, out _patch) ||
                    !AiJobFileStore.TryReadJson<AiAnalysisPackageDocument>(job.AnalysisPackagePath, out _package))
                    throw new InvalidOperationException("整理结果或分析快照缺失。");
                _patch.components = _patch.components ?? new System.Collections.Generic.List<AiOrganizerComponent>();
                _include = Enumerable.Repeat(true, _patch.components.Count).ToArray();
                _instanceIncluded = _patch.components.Select(component => Enumerable.Repeat(true, component.rootIds?.Length ?? 0).ToArray()).ToArray();
            }
            catch (Exception ex) { _error = ex.Message; _patch = null; }
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
                var patch = JsonUtility.FromJson<AiPatchDocument>(JsonUtility.ToJson(_patch));
                for (int index = 0; index < patch.components.Count; index++)
                    if (patch.components[index].rootIds != null)
                        patch.components[index].rootIds = patch.components[index].rootIds.Where((id, instance) => _instanceIncluded[index][instance]).ToArray();
                patch.components = patch.components.Where((component, index) => _include[index]).ToList();
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
