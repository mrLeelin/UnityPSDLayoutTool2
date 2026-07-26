namespace PsdLayoutTool2
{
    using TMPro;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(PsdLayoutProjectSettings))]
    internal sealed class PsdLayoutProjectSettingsEditor : UnityEditor.Editor
    {
        private string commonAssetNamingError = string.Empty;

        public override void OnInspectorGUI()
        {
            PsdLayoutProjectSettings settings = (PsdLayoutProjectSettings)target;
            DrawExternalAiSettings(settings);
            EditorGUILayout.Space();
            DrawOutputSettings(settings);
            EditorGUILayout.Space();
            DrawFontSettings(settings);
            EditorGUILayout.Space();
            DrawCommonAssetNaming(settings);
        }

        private static void DrawExternalAiSettings(PsdLayoutProjectSettings settings)
        {
            PsdHierarchyExternalAiSettingsSnapshot snapshot = settings.ResolveExternalAiSettings();
            EditorGUILayout.LabelField("AI 整理", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "点击 PSD Inspector 的“AI整理”后，会打开所选终端并执行下面的 AI 命令。整理技能和目标 Prefab 会作为任务上下文传给 AI。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            PsdHierarchyAiTerminal terminal = (PsdHierarchyAiTerminal)EditorGUILayout.EnumPopup(
                "终端",
                snapshot.terminal);
            string terminalExecutablePath = EditorGUILayout.TextField(
                new GUIContent("终端路径（可选）", "留空时使用所选终端的默认可执行路径。"),
                snapshot.terminalExecutablePath);
            string aiCommand = EditorGUILayout.TextField(
                new GUIContent("AI 命令", "例如 codex 或 claude，也可以填写可执行文件的完整路径。"),
                snapshot.aiCommand);
            string aiArguments = EditorGUILayout.TextField(
                new GUIContent("AI 命令参数（可选）", "这些参数会原样追加在任务提示之前。"),
                snapshot.aiArguments);
            string skillPath = EditorGUILayout.TextField(
                new GUIContent("整理技能路径", "支持相对项目根目录或绝对路径。"),
                snapshot.skillPath);
            if (EditorGUI.EndChangeCheck())
            {
                try
                {
                    settings.SetExternalAiSettings(
                        terminal,
                        terminalExecutablePath,
                        aiCommand,
                        aiArguments,
                        skillPath);
                }
                catch (System.ArgumentException exception)
                {
                    EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
                }
            }

            if (!snapshot.TryValidate(out string error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }

        private static void DrawOutputSettings(PsdLayoutProjectSettings settings)
        {
            PsdLayoutProjectOutputSnapshot snapshot = settings.ResolveOutputSettings();
            EditorGUILayout.LabelField("输出设置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "所有 PSD 共用这些输出规则。输出文件夹名留空时，自动使用当前 PSD 文件名。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            int outputMode = EditorGUILayout.Popup(
                "资源输出位置",
                snapshot.outputMode == PsdImporter.OutputDirectoryMode.AssetsRoot ? 1 : 0,
                new[] { "与 PSD 同目录", "Assets 根目录" });
            string outputFolderName = EditorGUILayout.TextField("输出文件夹名", snapshot.outputFolderName);
            int prefabMode = EditorGUILayout.Popup(
                "Prefab 输出位置",
                snapshot.prefabMode == PsdImporter.PrefabOutputMode.InsideOutputFolder ? 1 : 0,
                new[] { "输出文件夹同级（默认）", "输出文件夹内部" });
            int spriteAtlasVersion = EditorGUILayout.Popup(
                "图集版本",
                snapshot.spriteAtlasVersion == PsdImporter.SpriteAtlasVersion.V2 ? 1 : 0,
                new[] { "Sprite Atlas V1（默认）", "Sprite Atlas V2" });
            if (EditorGUI.EndChangeCheck())
            {
                settings.SetOutputSettings(
                    outputMode == 1
                        ? PsdImporter.OutputDirectoryMode.AssetsRoot
                        : PsdImporter.OutputDirectoryMode.PsdDirectory,
                    outputFolderName,
                    prefabMode == 1
                        ? PsdImporter.PrefabOutputMode.InsideOutputFolder
                        : PsdImporter.PrefabOutputMode.SiblingToOutputFolder,
                    spriteAtlasVersion == 1
                        ? PsdImporter.SpriteAtlasVersion.V2
                        : PsdImporter.SpriteAtlasVersion.V1);
            }
        }

        private static void DrawFontSettings(PsdLayoutProjectSettings settings)
        {
            PsdLayoutProjectFontSnapshot snapshot = settings.ResolveFontSettings();
            EditorGUILayout.LabelField("TextMeshPro 默认配置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "所有 PSD 导入共用的项目级默认配置。该资产属于使用方项目，可以提交到 Git。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            TMP_FontAsset font = (TMP_FontAsset)EditorGUILayout.ObjectField(
                new GUIContent("TMP 字体资产", "PSD 文本默认使用的 TMP_FontAsset。留空时使用 TMP 默认字体。"),
                snapshot.font,
                typeof(TMP_FontAsset),
                false);
            Material material = (Material)EditorGUILayout.ObjectField(
                new GUIContent("TMP 基础材质", "可选，用于生成描边和阴影材质变体。"),
                snapshot.baseMaterial,
                typeof(Material),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                settings.SetFontSettings(font, material);
                snapshot = settings.ResolveFontSettings();
            }

            if (snapshot.fontStatus == PsdProjectAssetStatus.Missing)
            {
                EditorGUILayout.HelpBox("配置的 TMP 字体已丢失或类型不正确。", MessageType.Warning);
            }

            if (snapshot.materialStatus == PsdProjectAssetStatus.Missing)
            {
                EditorGUILayout.HelpBox("配置的 TMP 基础材质已丢失或类型不正确。", MessageType.Warning);
            }

            if (snapshot.font != null && snapshot.baseMaterial != null &&
                !PsdPrefabTextMaterialFactory.IsCompatibleWithFont(snapshot.baseMaterial, snapshot.font))
            {
                EditorGUILayout.HelpBox(
                    "TMP 基础材质与所选字体图集不兼容。导入时将使用字体自带材质。",
                    MessageType.Warning);
            }
        }

        private void DrawCommonAssetNaming(PsdLayoutProjectSettings settings)
        {
            PsdCommonAssetNamingSnapshot naming = settings.ResolveCommonAssetNaming();
            EditorGUILayout.LabelField("通用资源命名", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "这些前缀同时用于 PSD 图层名和 Unity 资源名。前缀后的剩余文本作为映射表资源键，末尾下划线会自动补充。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            string prefabPrefix = EditorGUILayout.DelayedTextField(
                new GUIContent("Prefab 前缀", "可复用 Prefab 图层和 Prefab 资源名称使用的前缀。"),
                naming.prefabPrefix);
            string texturePrefix = EditorGUILayout.DelayedTextField(
                new GUIContent("Texture 前缀", "可复用纹理图层和纹理资源名称使用的前缀。"),
                naming.texturePrefix);
            if (EditorGUI.EndChangeCheck())
            {
                settings.TrySetCommonAssetPrefixes(prefabPrefix, texturePrefix, out commonAssetNamingError);
            }

            if (!string.IsNullOrEmpty(commonAssetNamingError))
            {
                EditorGUILayout.HelpBox(commonAssetNamingError, MessageType.Error);
            }
        }
    }
}
