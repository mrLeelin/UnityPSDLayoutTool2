namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
            DrawHierarchyAiSettings(settings);
            EditorGUILayout.Space();
            DrawOutputSettings(settings);
            EditorGUILayout.Space();
            DrawFontSettings(settings);
            EditorGUILayout.Space();
            DrawCommonAssetNaming(settings);
        }

        private static void DrawHierarchyAiSettings(PsdLayoutProjectSettings settings)
        {
            EditorGUILayout.LabelField("AI 层级整理", EditorStyles.boldLabel);
            IReadOnlyList<PsdHierarchyAiCliDescriptor> installed = PsdHierarchyAiCliDiscovery.FindInstalled();
            if (installed.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "未检测到 Claude 或 Codex CLI。请先安装其中一个并重启 Unity，再使用 AI整理。",
                    MessageType.Error);
                return;
            }

            PsdHierarchyAiSettingsSnapshot snapshot = settings.ResolveHierarchyAiSettings();
            int selectedIndex = FindProviderIndex(installed, snapshot.provider);
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
                settings.SetHierarchyAiSettings(
                    installed[selectedIndex].provider,
                    snapshot.connectionMode,
                    snapshot.customEndpoint,
                    snapshot.customModel);
                snapshot = settings.ResolveHierarchyAiSettings();
            }

            var displayNames = new string[installed.Count];
            for (int index = 0; index < installed.Count; index++)
            {
                displayNames[index] = installed[index].displayName;
            }

            EditorGUI.BeginChangeCheck();
            int providerIndex = EditorGUILayout.Popup("AI", selectedIndex, displayNames);
            int connectionModeIndex = EditorGUILayout.Popup(
                "连接方式",
                snapshot.connectionMode == PsdHierarchyAiConnectionMode.CustomApi ? 1 : 0,
                new[] { "默认（本机 CLI）", "自定义 API" });
            PsdHierarchyAiConnectionMode connectionMode = connectionModeIndex == 1
                ? PsdHierarchyAiConnectionMode.CustomApi
                : PsdHierarchyAiConnectionMode.LocalCli;
            string endpoint = snapshot.customEndpoint;
            string model = snapshot.customModel;
            if (connectionMode == PsdHierarchyAiConnectionMode.LocalCli)
            {
                EditorGUILayout.HelpBox(
                    "默认：后台调用本机 " + installed[providerIndex].displayName +
                    " CLI，不会打开外部终端，也不需要填写 API Key。",
                    MessageType.Info);
            }
            else
            {
                endpoint = EditorGUILayout.TextField(
                    new GUIContent("自定义 API 地址", "留空时使用所选 AI 的官方默认地址。"),
                    endpoint);
                model = EditorGUILayout.TextField(
                    new GUIContent("模型", "留空时使用所选 AI 的默认模型。"),
                    model);
                DrawStoredApiKey(installed[providerIndex].provider);
            }

            if (EditorGUI.EndChangeCheck())
            {
                try
                {
                    settings.SetHierarchyAiSettings(
                        installed[providerIndex].provider,
                        connectionMode,
                        endpoint,
                        model);
                }
                catch (ArgumentException exception)
                {
                    EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
                }
            }
        }

        private static int FindProviderIndex(
            IReadOnlyList<PsdHierarchyAiCliDescriptor> installed,
            PsdHierarchyAiProvider provider)
        {
            for (int index = 0; index < installed.Count; index++)
            {
                if (installed[index].provider == provider)
                {
                    return index;
                }
            }

            return -1;
        }

        private static void DrawStoredApiKey(PsdHierarchyAiProvider provider)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var secretStore = new PsdHierarchyAiSecretStore();
            string existingKey = string.Empty;
            try
            {
                secretStore.TryReadApiKey(projectRoot, provider, out existingKey);
            }
            catch (InvalidOperationException exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
            }

            EditorGUI.BeginChangeCheck();
            string apiKey = EditorGUILayout.PasswordField(
                new GUIContent("API Key（本机加密保存）", "不会写入项目配置或 Git。清空并确认后会删除本机保存的 Key。"),
                existingKey);
            if (EditorGUI.EndChangeCheck())
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        secretStore.ClearApiKey(projectRoot, provider);
                    }
                    else
                    {
                        secretStore.SaveApiKey(projectRoot, provider, apiKey);
                    }
                }
                catch (InvalidOperationException exception)
                {
                    EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
                }
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
