namespace PsdLayoutTool2
{
    using System;
    using System.IO;
    using TMPro;
    using UnityEditor;
    using UnityEngine;

    internal enum PsdHierarchyAiCredentialState
    {
        Missing,
        Saved,
        ReplacementPending,
        ClearPending,
    }

    internal enum PsdHierarchyAiSettingsStatusSeverity
    {
        None,
        Info,
        Error,
    }

    internal readonly struct PsdHierarchyAiSettingsUiState
    {
        private PsdHierarchyAiSettingsUiState(
            bool showCustomControls,
            bool testConnectionEnabled,
            string baseUrlError,
            PsdHierarchyAiCredentialState credentialState,
            bool secretStoreAvailable)
        {
            showBaseUrl = showCustomControls;
            showApiKey = showCustomControls;
            showRevealKey = showCustomControls;
            showTestConnection = showCustomControls;
            this.testConnectionEnabled = testConnectionEnabled;
            this.baseUrlError = baseUrlError ?? string.Empty;
            this.credentialState = credentialState;
            this.secretStoreAvailable = secretStoreAvailable;
            credentialActionsEnabled = showCustomControls && secretStoreAvailable;
            statusSeverity = secretStoreAvailable
                ? PsdHierarchyAiSettingsStatusSeverity.None
                : PsdHierarchyAiSettingsStatusSeverity.Error;
        }

        internal readonly bool showBaseUrl;
        internal readonly bool showApiKey;
        internal readonly bool showRevealKey;
        internal readonly bool showTestConnection;
        internal readonly bool testConnectionEnabled;
        internal readonly string baseUrlError;
        internal readonly PsdHierarchyAiCredentialState credentialState;
        internal readonly bool secretStoreAvailable;
        internal readonly bool credentialActionsEnabled;
        internal readonly PsdHierarchyAiSettingsStatusSeverity statusSeverity;

        internal static PsdHierarchyAiSettingsUiState Resolve(
            PsdHierarchyAiConnectionMode mode,
            string baseUrl,
            bool hasSavedKey,
            bool hasPendingReplacement,
            bool hasPendingClear,
            bool secretStoreAvailable)
        {
            if (mode == PsdHierarchyAiConnectionMode.Default)
            {
                return new PsdHierarchyAiSettingsUiState(
                    false,
                    false,
                    string.Empty,
                    hasSavedKey
                        ? PsdHierarchyAiCredentialState.Saved
                        : PsdHierarchyAiCredentialState.Missing,
                    secretStoreAvailable);
            }

            if (mode != PsdHierarchyAiConnectionMode.Custom)
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported AI connection mode.");
            }

            bool validUrl = PsdHierarchyAiConnectionSettings.TryValidateBaseUrl(baseUrl, out string error);
            PsdHierarchyAiCredentialState credentialState = hasPendingClear
                ? PsdHierarchyAiCredentialState.ClearPending
                : hasPendingReplacement
                    ? PsdHierarchyAiCredentialState.ReplacementPending
                    : hasSavedKey
                        ? PsdHierarchyAiCredentialState.Saved
                        : PsdHierarchyAiCredentialState.Missing;
            bool canTest = validUrl && secretStoreAvailable &&
                credentialState == PsdHierarchyAiCredentialState.Saved;
            return new PsdHierarchyAiSettingsUiState(
                true,
                canTest,
                error,
                credentialState,
                secretStoreAvailable);
        }
    }

    /// <summary>
    /// PSD Layout Tool 项目级配置的唯一编辑入口。
    /// PSD 资源 Inspector 只提供用于打开该配置资产编辑窗口的按钮。
    /// </summary>
    [CustomEditor(typeof(PsdLayoutProjectSettings))]
    internal sealed class PsdLayoutProjectSettingsEditor : UnityEditor.Editor
    {
        internal static Action<PsdLayoutProjectSettings, PsdHierarchyAiProvider> testConnectionRequested;

        private string commonAssetNamingError = string.Empty;
        private IPsdAiSecretStore aiSecretStore;
        private string projectIdentity = string.Empty;
        private string codexBaseUrlDraft = string.Empty;
        private string claudeBaseUrlDraft = string.Empty;
        private string apiKeyDraft = string.Empty;
        private string aiStatus = string.Empty;
        private PsdHierarchyAiSettingsStatusSeverity aiStatusSeverity;
        private bool revealApiKey;
        private bool editingApiKey;
        private bool clearKeyRequested;

        private void OnEnable()
        {
            aiSecretStore = new PsdAiSecretStore();
            DirectoryInfo projectDirectory = Directory.GetParent(Application.dataPath);
            projectIdentity = projectDirectory == null ? Application.dataPath : projectDirectory.FullName;

            if (target is PsdLayoutProjectSettings settings)
            {
                PsdHierarchyAiSettingsSnapshot snapshot = settings.ResolveAiSettings();
                codexBaseUrlDraft = snapshot.codex.baseUrl;
                claudeBaseUrlDraft = snapshot.claude.baseUrl;
            }

            ResetCredentialUi();
        }

        public override void OnInspectorGUI()
        {
            PsdLayoutProjectSettings settings = (PsdLayoutProjectSettings)target;
            DrawAiSettings(settings);
            EditorGUILayout.Space();
            DrawOutputSettings(settings);
            EditorGUILayout.Space();
            DrawFontSettings(settings);
            EditorGUILayout.Space();
            DrawCommonAssetNaming(settings);
        }

        private void DrawAiSettings(PsdLayoutProjectSettings settings)
        {
            PsdHierarchyAiSettingsSnapshot snapshot = settings.ResolveAiSettings();
            EditorGUILayout.LabelField("层级整理 AI", EditorStyles.boldLabel);

            int providerIndex = snapshot.provider == PsdHierarchyAiProvider.Claude ? 0 : 1;
            int selectedProviderIndex = EditorGUILayout.Popup("AI 提供方", providerIndex, new[] { "Claude", "Codex" });
            PsdHierarchyAiProvider selectedProvider = selectedProviderIndex == 0
                ? PsdHierarchyAiProvider.Claude
                : PsdHierarchyAiProvider.Codex;
            if (selectedProvider != snapshot.provider)
            {
                settings.SetAiProvider(selectedProvider);
                ResetCredentialUi();
                snapshot = settings.ResolveAiSettings();
            }

            PsdHierarchyAiConnectionSnapshot connection = snapshot.activeConnection;
            int modeIndex = connection.mode == PsdHierarchyAiConnectionMode.Custom ? 1 : 0;
            int selectedModeIndex = EditorGUILayout.Popup("连接方式", modeIndex, new[] { "默认", "自定义" });
            PsdHierarchyAiConnectionMode selectedMode = selectedModeIndex == 1
                ? PsdHierarchyAiConnectionMode.Custom
                : PsdHierarchyAiConnectionMode.Default;
            if (selectedMode != connection.mode)
            {
                settings.SetAiConnectionMode(snapshot.provider, selectedMode);
                ResetCredentialUi();
                snapshot = settings.ResolveAiSettings();
                connection = snapshot.activeConnection;
            }

            bool hasSavedKey = TryHasSavedKey(settings, snapshot.provider, out bool secretStoreAvailable);
            string baseUrlDraft = GetBaseUrlDraft(snapshot.provider);
            PsdHierarchyAiSettingsUiState state = PsdHierarchyAiSettingsUiState.Resolve(
                connection.mode,
                baseUrlDraft,
                hasSavedKey,
                editingApiKey && hasSavedKey,
                clearKeyRequested,
                secretStoreAvailable);
            if (!state.showBaseUrl)
            {
                EditorGUILayout.HelpBox("使用所选 AI 命令行工具的默认登录和默认模型。", MessageType.Info);
                DrawAiStatus();
                return;
            }

            EditorGUI.BeginChangeCheck();
            string updatedBaseUrl = EditorGUILayout.DelayedTextField("API 地址", baseUrlDraft);
            if (EditorGUI.EndChangeCheck())
            {
                SetBaseUrlDraft(snapshot.provider, updatedBaseUrl);
                if (string.IsNullOrWhiteSpace(updatedBaseUrl) ||
                    PsdHierarchyAiConnectionSettings.TryValidateBaseUrl(updatedBaseUrl, out _))
                {
                    settings.SetAiBaseUrl(snapshot.provider, updatedBaseUrl);
                }

                state = PsdHierarchyAiSettingsUiState.Resolve(
                    connection.mode,
                    updatedBaseUrl,
                    hasSavedKey,
                    editingApiKey && hasSavedKey,
                    clearKeyRequested,
                    secretStoreAvailable);
            }

            if (!string.IsNullOrEmpty(state.baseUrlError))
            {
                EditorGUILayout.HelpBox(state.baseUrlError, MessageType.Error);
            }

            DrawApiKey(settings, snapshot.provider, hasSavedKey, state);
            DrawTestConnection(settings, snapshot.provider, state);
            DrawAiStatus();
        }

        private void DrawApiKey(
            PsdLayoutProjectSettings settings,
            PsdHierarchyAiProvider provider,
            bool hasSavedKey,
            PsdHierarchyAiSettingsUiState state)
        {
            if (!state.showApiKey)
            {
                return;
            }

            if (!state.credentialActionsEnabled)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("API Key", "本机密钥存储不可用");
                }

                return;
            }

            if (clearKeyRequested)
            {
                EditorGUILayout.HelpBox("确认清除当前 AI 提供方已保存的 API Key。", MessageType.Warning);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("确认清除"))
                {
                    TryClearKey(settings, provider);
                }

                if (GUILayout.Button("取消"))
                {
                    clearKeyRequested = false;
                }

                EditorGUILayout.EndHorizontal();
                return;
            }

            if (hasSavedKey && !editingApiKey)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("API Key", "已保存");
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("替换 Key"))
                {
                    editingApiKey = true;
                    revealApiKey = false;
                    apiKeyDraft = string.Empty;
                }

                if (GUILayout.Button("清除 Key"))
                {
                    clearKeyRequested = true;
                }

                EditorGUILayout.EndHorizontal();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            apiKeyDraft = revealApiKey
                ? EditorGUILayout.TextField("API Key", apiKeyDraft)
                : EditorGUILayout.PasswordField("API Key", apiKeyDraft);
            GUIContent revealContent = EditorGUIUtility.IconContent(
                revealApiKey ? "animationvisibilitytoggleon" : "animationvisibilitytoggleoff");
            revealContent.tooltip = revealApiKey ? "隐藏 API Key" : "显示 API Key";
            if (GUILayout.Button(revealContent, GUILayout.Width(28), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
            {
                revealApiKey = !revealApiKey;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(hasSavedKey ? "保存替换" : "保存 Key"))
            {
                TrySaveKey(settings, provider);
            }

            if (hasSavedKey && GUILayout.Button("取消替换"))
            {
                ResetCredentialUi();
            }

            EditorGUILayout.EndHorizontal();
            if (!hasSavedKey && string.IsNullOrWhiteSpace(apiKeyDraft))
            {
                EditorGUILayout.HelpBox("API Key 不能为空。", MessageType.Error);
            }
        }

        private static void DrawTestConnection(
            PsdLayoutProjectSettings settings,
            PsdHierarchyAiProvider provider,
            PsdHierarchyAiSettingsUiState state)
        {
            if (!state.showTestConnection)
            {
                return;
            }

            bool hasTester = testConnectionRequested != null;
            using (new EditorGUI.DisabledScope(!state.testConnectionEnabled || !hasTester))
            {
                if (GUILayout.Button("测试连接"))
                {
                    testConnectionRequested.Invoke(settings, provider);
                }
            }

            if (!hasTester)
            {
                EditorGUILayout.HelpBox("连接测试将在后续步骤启用。", MessageType.Info);
            }
        }

        private bool TryHasSavedKey(
            PsdLayoutProjectSettings settings,
            PsdHierarchyAiProvider provider,
            out bool secretStoreAvailable)
        {
            try
            {
                bool hasSavedKey = settings.HasSavedAiKey(provider, aiSecretStore, projectIdentity);
                secretStoreAvailable = true;
                return hasSavedKey;
            }
            catch (PsdAiSecretStoreException exception)
            {
                secretStoreAvailable = false;
                SetAiError(exception.Message);
                return false;
            }
        }

        private void TrySaveKey(PsdLayoutProjectSettings settings, PsdHierarchyAiProvider provider)
        {
            if (string.IsNullOrWhiteSpace(apiKeyDraft))
            {
                aiStatus = "API Key 不能为空。";
                return;
            }

            try
            {
                settings.SaveAiKey(provider, apiKeyDraft, aiSecretStore, projectIdentity);
                apiKeyDraft = string.Empty;
                revealApiKey = false;
                editingApiKey = false;
                aiStatus = "API Key 已保存到当前 Windows 用户的本机安全存储。";
                aiStatusSeverity = PsdHierarchyAiSettingsStatusSeverity.Info;
            }
            catch (PsdAiSecretStoreException exception)
            {
                SetAiError(exception.Message);
            }
        }

        private void TryClearKey(PsdLayoutProjectSettings settings, PsdHierarchyAiProvider provider)
        {
            try
            {
                settings.ClearAiKey(provider, aiSecretStore, projectIdentity);
                ResetCredentialUi();
                aiStatus = "API Key 已清除。";
                aiStatusSeverity = PsdHierarchyAiSettingsStatusSeverity.Info;
            }
            catch (PsdAiSecretStoreException exception)
            {
                SetAiError(exception.Message);
            }
        }

        private void DrawAiStatus()
        {
            if (!string.IsNullOrEmpty(aiStatus))
            {
                EditorGUILayout.HelpBox(
                    aiStatus,
                    aiStatusSeverity == PsdHierarchyAiSettingsStatusSeverity.Error
                        ? MessageType.Error
                        : MessageType.Info);
            }
        }

        private void SetAiError(string message)
        {
            aiStatus = message;
            aiStatusSeverity = PsdHierarchyAiSettingsStatusSeverity.Error;
        }

        private string GetBaseUrlDraft(PsdHierarchyAiProvider provider)
        {
            return provider == PsdHierarchyAiProvider.Claude ? claudeBaseUrlDraft : codexBaseUrlDraft;
        }

        private void SetBaseUrlDraft(PsdHierarchyAiProvider provider, string value)
        {
            if (provider == PsdHierarchyAiProvider.Claude)
            {
                claudeBaseUrlDraft = value ?? string.Empty;
            }
            else
            {
                codexBaseUrlDraft = value ?? string.Empty;
            }
        }

        private void ResetCredentialUi()
        {
            apiKeyDraft = string.Empty;
            aiStatus = string.Empty;
            aiStatusSeverity = PsdHierarchyAiSettingsStatusSeverity.None;
            revealApiKey = false;
            editingApiKey = false;
            clearKeyRequested = false;
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
                EditorGUILayout.HelpBox("配置的 TMP 字体已丢失或资源类型不正确。", MessageType.Warning);
            }

            if (snapshot.materialStatus == PsdProjectAssetStatus.Missing)
            {
                EditorGUILayout.HelpBox("配置的 TMP 基础材质已丢失或资源类型不正确。", MessageType.Warning);
            }

            if (snapshot.font != null && snapshot.baseMaterial != null &&
                !PsdPrefabTextMaterialFactory.IsCompatibleWithFont(snapshot.baseMaterial, snapshot.font))
            {
                EditorGUILayout.HelpBox(
                    "TMP 基础材质与所选字体图集不兼容，导入时将使用字体自带材质。",
                    MessageType.Warning);
            }
        }

        private void DrawCommonAssetNaming(PsdLayoutProjectSettings settings)
        {
            PsdCommonAssetNamingSnapshot naming = settings.ResolveCommonAssetNaming();
            EditorGUILayout.LabelField("通用资源命名", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "这些前缀同时用于 PSD 图层名和 Unity 资源名，前缀后的剩余文本作为映射表资源键。" +
                "末尾下划线会自动补充。例如：" + naming.prefabPrefix + "Button_Green 和 " +
                naming.texturePrefix + "Lock。",
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
