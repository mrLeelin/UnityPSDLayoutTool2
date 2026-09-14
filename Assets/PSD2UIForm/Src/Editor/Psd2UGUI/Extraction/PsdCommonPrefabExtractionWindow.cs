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
            EditorGUILayout.LabelField("手动抽取同结构组件", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("打开生成后的 UI Prefab，保存修改，再在 Hierarchy 中多选重复组件的根节点。预览后会将它们替换为公共 Prefab 实例。", MessageType.Info);
            EditorGUILayout.HelpBox("此阶段只处理已生成的 UI。重新从 PSD 生成时恢复抽取规则的功能尚未实现。", MessageType.Warning);
            EditorGUI.BeginChangeCheck();
            _componentName = EditorGUILayout.TextField("公共组件名称", _componentName);
            if (EditorGUI.EndChangeCheck()) { _plan = null; _error = null; }
            if (GUILayout.Button("预览当前选区")) BuildPreview();
            if (!string.IsNullOrEmpty(_error)) EditorGUILayout.HelpBox(_error, MessageType.Error);
            if (_plan == null) return;
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("目标界面", _plan.PrefabPath, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("新公共资产", _plan.OutputPath, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("模板：" + _plan.Names[0]);
            for (int i = 0; i < _plan.Sources.Count; i++)
                EditorGUILayout.LabelField((i + 1) + ". " + _plan.Names[i], "节点 " + _plan.Sources[i]);
            EditorGUILayout.HelpBox("按子节点顺序对应同结构成员。每个实例保留其序列化属性、图片、文字、布局、可见状态及内部引用。已有嵌套 Prefab 或跨组件引用会阻止抽取。", MessageType.None);
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
                _plan = PsdCommonPrefabExtraction.Preview(stage.assetPath,
                    selected.Select(go => PsdCommonPrefabExtraction.GetNodeAddress(stage.prefabContentsRoot.transform, go.transform)).ToArray(), _componentName);
            }
            catch (Exception ex) { _error = ex.Message; }
        }

        void Apply()
        {
            string path = _plan.PrefabPath;
            try
            {
                // Revalidate before leaving the stage; never discard unsaved user edits.
                PsdCommonPrefabExtraction.Preview(path, _plan.Sources.ToArray(), _componentName);
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
