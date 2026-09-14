using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    public sealed class PsdCommonPrefabExtractionWindow : EditorWindow
    {
        string _componentName = "ReusableItem";
        PsdCommonPrefabPlan _plan;
        string _error;
        Vector2 _scroll;
        bool _hasGenerationSource;
        bool _useStates;

        [MenuItem("Tools/PSD2UIForm/抽取公共 Prefab…")]
        [MenuItem("GameObject/PSD2UIForm/抽取公共 Prefab…", false, 49)]
        public static void Open()
        {
            var window = GetWindow<PsdCommonPrefabExtractionWindow>("抽取公共 Prefab");
            window.minSize = new Vector2(460, 330);
            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("手动抽取公共组件", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("打开生成后的 UI Prefab，保存修改，再在 Hierarchy 中多选重复组件的根节点。预览后会将它们替换为公共 Prefab 实例。", MessageType.Info);
            EditorGUILayout.HelpBox("抽取规则会保存到界面旁的 .extraction.asset。记录过来源的界面可在输入不变时重复生成；源更新合并尚未实现，检测到变化会停止生成。旧版输出需先重新生成一次以记录来源，再抽取。", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            _componentName = EditorGUILayout.TextField("公共组件名称", _componentName);
            _useStates = EditorGUILayout.Toggle("不同结构（保留各自形态）", _useStates);
            if (EditorGUI.EndChangeCheck()) { _plan = null; _error = null; }
            if (GUILayout.Button("预览当前选区")) BuildPreview();
            if (!string.IsNullOrEmpty(_error)) EditorGUILayout.HelpBox(_error, MessageType.Error);
            if (_plan == null) return;
            if (!_hasGenerationSource)
                EditorGUILayout.HelpBox("当前界面没有生成来源记录。本次仍可手动抽取，但无法从 PSD 重复生成；如需重复生成，请先在已保存的 PSD 编辑 Prefab 中生成一次新 UI，再抽取。", MessageType.Warning);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("目标界面", _plan.PrefabPath, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("新公共资产", _plan.OutputPath, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            if (!_plan.UsesStates) EditorGUILayout.LabelField("模板：" + _plan.Names[0]);
            for (int i = 0; i < _plan.Sources.Count; i++)
                EditorGUILayout.LabelField((i + 1) + ". " + _plan.Names[i],
                    "节点 " + _plan.Sources[i] + (_plan.UsesStates ? " → " + _plan.SourceStates[i].State : ""));
            if (_plan.UsesStates)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("公共成员（按显示顺序）", EditorStyles.boldLabel);
                if (_plan.CommonMembers.Count == 0)
                    EditorGUILayout.LabelField("无可安全合并的成员，保留各分支完整结构。", EditorStyles.wordWrappedLabel);
                foreach (string member in _plan.CommonMembers) EditorGUILayout.LabelField(member, EditorStyles.wordWrappedLabel);
                foreach (var state in _plan.States)
                {
                    EditorGUILayout.LabelField(state.Name, EditorStyles.boldLabel);
                    foreach (string member in state.Members) EditorGUILayout.LabelField(member, EditorStyles.wordWrappedLabel);
                }
                EditorGUILayout.HelpBox("每个位置只启用自己的分支，保留原图片、文字和布局。未能安全共享的成员保留在分支内。已有嵌套 Prefab、跨组件引用及不支持的布局会阻止抽取。", MessageType.None);
            }
            else EditorGUILayout.HelpBox("按子节点顺序对应同结构成员。每个实例保留其序列化属性、图片、文字、布局、可见状态及内部引用。已有嵌套 Prefab 或跨组件引用会阻止抽取。", MessageType.None);
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("应用预览方案并保存")) Apply();
        }

        void BuildPreview()
        {
            _plan = null; _error = null;
            try
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage == null) throw new InvalidOperationException("请先打开生成后的 UI Prefab。");
                var selected = Selection.gameObjects;
                if (selected.Any(go => go.scene != stage.scene))
                    throw new InvalidOperationException("选区必须来自当前 Prefab。");
                var sources = selected.Select(go => PsdCommonPrefabExtraction.GetNodeAddress(stage.prefabContentsRoot.transform, go.transform)).ToArray();
                _plan = _useStates ? PsdCommonPrefabExtraction.PreviewStates(stage.assetPath, sources, _componentName)
                    : PsdCommonPrefabExtraction.Preview(stage.assetPath, sources, _componentName);
                var rules = PsdCommonPrefabPersistence.Find(stage.assetPath);
                _hasGenerationSource = rules != null && !string.IsNullOrEmpty(rules.sourceGuid) && !string.IsNullOrEmpty(rules.sourceFingerprint);
            }
            catch (Exception ex) { _error = ex.Message; }
        }

        void Apply()
        {
            string path = _plan.PrefabPath;
            try
            {
                // Revalidate before leaving the stage; never discard unsaved user edits.
                if (_plan.UsesStates) PsdCommonPrefabExtraction.PreviewStates(path, _plan.Sources.ToArray(), _componentName);
                else PsdCommonPrefabExtraction.Preview(path, _plan.Sources.ToArray(), _componentName);
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage == null || stage.assetPath != path)
                    throw new InvalidOperationException("当前 Prefab 已改变，请重新预览。");
                StageUtility.GoToMainStage();
                PsdCommonPrefabExtraction.Apply(_plan);
                _plan = null;
                _error = null;
                ShowNotification(new GUIContent("公共 Prefab 已生成，实例差异已保留"));
            }
            catch (Exception ex) { _error = ex.Message; }
            finally
            {
                if (PrefabStageUtility.GetCurrentPrefabStage() == null) PrefabStageUtility.OpenPrefab(path);
            }
        }
    }
}
