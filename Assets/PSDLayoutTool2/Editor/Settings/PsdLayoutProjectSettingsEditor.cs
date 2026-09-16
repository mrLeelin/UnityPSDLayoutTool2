namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using TMPro;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    [CustomEditor(typeof(PsdLayoutProjectSettings))]
    internal sealed class PsdLayoutProjectSettingsEditor : UnityEditor.Editor
    {
        private const string AiSectionName = "psd-project-settings-ai";
        private const string CleanupExecutionSectionName = "psd-project-settings-cleanup-execution";
        private const string OutputSectionName = "psd-project-settings-output";
        private const string UiComponentSectionName = "psd-project-settings-ui-components";
        private const string FontSectionName = "psd-project-settings-font";
        private const string CommonNamingSectionName = "psd-project-settings-common-naming";
        private const string FixedOutputContentName = "psd-project-settings-fixed-output";
        internal const string CommonCatalogRefreshButtonName =
            "psd-project-settings-common-catalog-refresh";

        public override VisualElement CreateInspectorGUI()
        {
            PsdLayoutProjectSettings settings = (PsdLayoutProjectSettings)target;
            var root = new VisualElement();
            root.style.marginLeft = 3;
            root.style.marginRight = 3;
            root.style.marginTop = 4;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 4;
            root.style.backgroundColor = new Color(0.11f, 0.12f, 0.14f, 1f);
            root.style.borderLeftWidth = 1;
            root.style.borderRightWidth = 1;
            root.style.borderTopWidth = 1;
            root.style.borderBottomWidth = 1;
            root.style.borderLeftColor = new Color(0.36f, 0.75f, 0.65f, 1f);
            root.style.borderRightColor = new Color(0.36f, 0.75f, 0.65f, 1f);
            root.style.borderTopColor = new Color(0.36f, 0.75f, 0.65f, 1f);
            root.style.borderBottomColor = new Color(0.36f, 0.75f, 0.65f, 1f);
            root.style.borderTopLeftRadius = 6;
            root.style.borderTopRightRadius = 6;
            root.style.borderBottomLeftRadius = 6;
            root.style.borderBottomRightRadius = 6;
            root.Add(CreateHeader());
            root.Add(CreateHierarchyAiSection(settings));
            root.Add(CreateHierarchyCleanupExecutionSection(settings));
            root.Add(CreateOutputSection(settings));
            root.Add(CreateUiComponentSection(settings));
            root.Add(CreateFontSection(settings));
            root.Add(CreatePreviewServerSection(settings));
            root.Add(CreateCommonNamingSection(settings));
            return root;
        }

        private static VisualElement CreateHeader()
        {
            var header = new VisualElement();
            header.style.marginBottom = 6;
            header.style.paddingLeft = 2;
            header.style.paddingRight = 2;
            header.style.paddingTop = 3;
            header.style.paddingBottom = 8;
            var headingRow = new VisualElement();
            headingRow.style.flexDirection = FlexDirection.Row;
            headingRow.style.alignItems = Align.Center;

            var title = new Label("PSD Layout Tool 全局配置");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 14;
            title.style.color = new Color(0.93f, 0.96f, 1f, 1f);
            title.style.flexGrow = 1;
            var badge = new Label("PROJECT SETTINGS");
            badge.style.fontSize = 9;
            badge.style.color = new Color(0.58f, 0.74f, 0.98f, 1f);
            badge.style.backgroundColor = new Color(0.1f, 0.22f, 0.38f, 1f);
            badge.style.paddingLeft = 6;
            badge.style.paddingRight = 6;
            badge.style.paddingTop = 2;
            badge.style.paddingBottom = 2;
            badge.style.borderTopLeftRadius = 3;
            badge.style.borderTopRightRadius = 3;
            badge.style.borderBottomLeftRadius = 3;
            badge.style.borderBottomRightRadius = 3;
            headingRow.Add(title);
            headingRow.Add(badge);
            var description = new Label("项目级导入、AI 整理与公共资源规则");
            description.style.marginTop = 2;
            description.style.fontSize = 11;
            description.style.color = new Color(0.68f, 0.71f, 0.75f, 1f);
            var divider = new VisualElement();
            divider.style.height = 1;
            divider.style.marginTop = 7;
            divider.style.marginBottom = 7;
            divider.style.backgroundColor = new Color(0.22f, 0.25f, 0.3f, 1f);
            header.Add(headingRow);
            header.Add(divider);
            header.Add(description);
            return header;
        }

        private static VisualElement CreateSection(string name, string title)
        {
            var section = new VisualElement
            {
                name = name,
            };
            section.style.marginBottom = 8;
            section.style.paddingLeft = 10;
            section.style.paddingRight = 10;
            section.style.paddingTop = 9;
            section.style.paddingBottom = 10;
            section.style.backgroundColor = new Color(0.15f, 0.165f, 0.19f, 1f);
            section.style.borderLeftWidth = 1;
            section.style.borderRightWidth = 1;
            section.style.borderTopWidth = 1;
            section.style.borderBottomWidth = 1;
            section.style.borderLeftColor = new Color(0.27f, 0.30f, 0.35f, 1f);
            section.style.borderRightColor = new Color(0.27f, 0.30f, 0.35f, 1f);
            section.style.borderTopColor = new Color(0.27f, 0.30f, 0.35f, 1f);
            section.style.borderBottomColor = new Color(0.27f, 0.30f, 0.35f, 1f);
            section.style.borderTopLeftRadius = 5;
            section.style.borderTopRightRadius = 5;
            section.style.borderBottomLeftRadius = 5;
            section.style.borderBottomRightRadius = 5;

            var sectionTitle = new Label(title);
            sectionTitle.style.fontSize = 12;
            sectionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            sectionTitle.style.color = new Color(0.89f, 0.93f, 0.99f, 1f);
            section.Add(sectionTitle);

            var divider = new VisualElement();
            divider.style.height = 1;
            divider.style.marginTop = 7;
            divider.style.marginBottom = 6;
            divider.style.backgroundColor = new Color(0.22f, 0.25f, 0.31f, 1f);
            section.Add(divider);
            return section;
        }

        private static void ReplaceSection(VisualElement oldSection, VisualElement replacement)
        {
            VisualElement parent = oldSection.parent;
            if (parent == null)
            {
                return;
            }

            int index = parent.IndexOf(oldSection);
            parent.RemoveAt(index);
            parent.Insert(index, replacement);
        }

        private static HelpBox CreateHiddenErrorBox()
        {
            var errorBox = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            errorBox.style.display = DisplayStyle.None;
            return errorBox;
        }

        private static void ShowError(HelpBox errorBox, string error)
        {
            errorBox.text = error;
            errorBox.messageType = HelpBoxMessageType.Error;
            errorBox.style.display = string.IsNullOrEmpty(error) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static TextField CreateDelayedTextField(string label, string value, string name, string tooltip)
        {
            var field = new TextField(label)
            {
                name = name,
                value = value ?? string.Empty,
                isDelayed = true,
                tooltip = tooltip,
            };
            return field;
        }

        private static VisualElement CreateHierarchyCleanupExecutionSection(PsdLayoutProjectSettings settings)
        {
            VisualElement section = CreateSection(CleanupExecutionSectionName, "Prefab Cleanup Execution");
            PsdHierarchyCleanupExecutionSettingsSnapshot snapshot =
                settings.ResolveHierarchyCleanupExecutionSettings();
            var choices = new List<string>
            {
                "Native Unity (default)",
                "Unity CLI runner (optional)",
            };
            int selectedIndex = snapshot.backend == PsdHierarchyCleanupExecutionBackend.UnityCliRunner ? 1 : 0;
            var backendField = new PopupField<string>("Backend", choices, selectedIndex)
            {
                name = "psd-project-settings-cleanup-execution-backend",
                tooltip = "Native Unity executes cleanup in the current Editor. Unity CLI remains an optional alternate runner.",
            };
            section.Add(new HelpBox(
                selectedIndex == 0
                    ? "Native Unity is active. Hierarchy cleanup, component Prefab extraction, private asset renames, validation, and failure handling run in the current Unity Editor."
                    : "Unity CLI runner is active for this project. It supports component extraction and asset renames.",
                selectedIndex == 0 ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning));
            section.Add(backendField);
            backendField.RegisterValueChangedCallback(change =>
            {
                settings.SetHierarchyCleanupExecutionBackend(
                    choices.IndexOf(change.newValue) == 1
                        ? PsdHierarchyCleanupExecutionBackend.UnityCliRunner
                        : PsdHierarchyCleanupExecutionBackend.NativeUnity);
                ReplaceSection(section, CreateHierarchyCleanupExecutionSection(settings));
            });
            return section;
        }

        private static VisualElement CreateHierarchyAiSection(PsdLayoutProjectSettings settings)
        {
            VisualElement section = CreateSection(AiSectionName, "AI 层级整理");
            IReadOnlyList<PsdHierarchyAiCliDescriptor> installed = PsdHierarchyAiCliDiscovery.FindInstalled();
            PsdHierarchyAiSettingsSnapshot snapshot = settings.ResolveHierarchyAiSettings();

            // 第一项永远是「不启用」，这样默认状态和「想关掉」都有明确落点。
            var providerChoices = new List<string> { "不启用" };
            var providerValues = new List<PsdHierarchyAiProvider> { PsdHierarchyAiProvider.None };
            for (int index = 0; index < installed.Count; index++)
            {
                providerChoices.Add(installed[index].displayName);
                providerValues.Add(installed[index].provider);
            }

            int selectedIndex = providerValues.IndexOf(snapshot.provider);
            if (selectedIndex < 0)
            {
                // 配置里选的 CLI 现在没装了：照实显示并标注不可用，不静默改掉用户的选择。
                providerChoices.Add(PsdHierarchyChatClient.GetProviderDisplayName(snapshot.provider) + "（当前不可用）");
                providerValues.Add(snapshot.provider);
                selectedIndex = providerValues.Count - 1;
            }

            var errorBox = CreateHiddenErrorBox();
            var providerField = new PopupField<string>("AI", providerChoices, selectedIndex)
            {
                name = "psd-project-settings-ai-provider",
            };
            section.Add(providerField);

            if (installed.Count == 0)
            {
                section.Add(new HelpBox(
                    "未检测到任何受支持的 AI CLI（Claude / Codex / Grok / Pi）。" +
                    "请先安装其中一个并重启 Unity，再使用 AI 整理。",
                    HelpBoxMessageType.Error));
            }

            void ApplySettings(
                PsdHierarchyAiProvider provider,
                string endpoint,
                string model,
                string effort)
            {
                try
                {
                    settings.SetHierarchyAiSettings(provider, endpoint, model, effort);
                    ReplaceSection(section, CreateHierarchyAiSection(settings));
                }
                catch (ArgumentException exception)
                {
                    ShowError(errorBox, exception.Message);
                }
            }

            providerField.RegisterValueChangedCallback(change =>
            {
                int index = providerChoices.IndexOf(change.newValue);
                if (index >= 0)
                {
                    ApplySettings(
                        providerValues[index],
                        snapshot.customEndpoint,
                        snapshot.customModel,
                        snapshot.reasoningEffort);
                }
            });

            if (!snapshot.isConfigured)
            {
                section.Add(new HelpBox(
                    "AI 整理当前未启用。选择上面的 CLI 后会显示模型、思考程度与 API 设置。",
                    HelpBoxMessageType.Info));
                section.Add(errorBox);
                return section;
            }

            PsdHierarchyAiCliDiscovery.TryGetSupported(snapshot.provider, out PsdHierarchyAiCliDescriptor supported);
            bool usesCustomApi = snapshot.connectionMode == PsdHierarchyAiConnectionMode.CustomApi;

            var modelField = CreateDelayedTextField(
                "模型名称",
                snapshot.customModel,
                "psd-project-settings-ai-model",
                string.IsNullOrEmpty(supported.defaultModelHint)
                    ? "留空时使用该 CLI 自身的模型配置。"
                    : "留空时使用该 CLI 自身的模型配置。例如 " + supported.defaultModelHint);
            var effortField = CreateDelayedTextField(
                "思考程度",
                snapshot.reasoningEffort,
                "psd-project-settings-ai-effort",
                string.IsNullOrEmpty(supported.reasoningEffortHint)
                    ? "留空时使用该 CLI 自身的配置。"
                    : "留空时使用该 CLI 自身的配置。可选：" + supported.reasoningEffortHint);
            var endpointField = CreateDelayedTextField(
                "API 地址（留空走本机 CLI）",
                snapshot.customEndpoint,
                "psd-project-settings-ai-endpoint",
                "留空表示调用本机 " + supported.displayName +
                " CLI，不打开外部终端、不需要 API Key；填写后才走自定义 API。");
            section.Add(modelField);
            section.Add(effortField);
            section.Add(endpointField);

            modelField.RegisterValueChangedCallback(change =>
                ApplySettings(snapshot.provider, endpointField.value, change.newValue, effortField.value));
            effortField.RegisterValueChangedCallback(change =>
                ApplySettings(snapshot.provider, endpointField.value, modelField.value, change.newValue));
            endpointField.RegisterValueChangedCallback(change =>
                ApplySettings(snapshot.provider, change.newValue, modelField.value, effortField.value));

            if (!usesCustomApi)
            {
                section.Add(new HelpBox(
                    "当前走本机 " + supported.displayName + " CLI：模型与思考程度留空即使用 CLI 自身配置，也不需要 API Key。",
                    HelpBoxMessageType.Info));
                section.Add(errorBox);
                return section;
            }

            if (!PsdHierarchyChatClient.HasBuiltInApiDefaults(snapshot.provider))
            {
                section.Add(new HelpBox(
                    supported.displayName + " 没有可以预设的官方 API 地址，走自定义 API 时必须自己填地址和模型。",
                    HelpBoxMessageType.Warning));
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var secretStore = new PsdHierarchyAiSecretStore();
            string existingKey = string.Empty;
            try
            {
                secretStore.TryReadApiKey(projectRoot, snapshot.provider, out existingKey);
            }
            catch (InvalidOperationException exception)
            {
                ShowError(errorBox, exception.Message);
            }

            var apiKeyField = CreateDelayedTextField(
                "API Key（本地加密保存）",
                existingKey,
                "psd-project-settings-ai-api-key",
                "不会写入项目配置或 Git。清空并确认后会删除本地保存的 Key。");
            apiKeyField.isPasswordField = true;
            section.Add(apiKeyField);
            apiKeyField.RegisterValueChangedCallback(change =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(change.newValue))
                    {
                        secretStore.ClearApiKey(projectRoot, snapshot.provider);
                    }
                    else
                    {
                        secretStore.SaveApiKey(projectRoot, snapshot.provider, change.newValue);
                    }
                }
                catch (InvalidOperationException exception)
                {
                    ShowError(errorBox, exception.Message);
                }
            });

            section.Add(errorBox);
            return section;
        }

        private static VisualElement CreateOutputSection(PsdLayoutProjectSettings settings)
        {
            VisualElement section = CreateSection(OutputSectionName, "输出设置");
            PsdLayoutProjectOutputSnapshot snapshot = settings.ResolveOutputSettings();
            var outputModeChoices = new List<string> { "与 PSD 同目录", "固定位置" };
            int outputModeIndex = snapshot.outputMode == PsdImporter.OutputDirectoryMode.PsdDirectory ? 0 : 1;
            var outputModeField = new PopupField<string>("资源输出位置", outputModeChoices, outputModeIndex)
            {
                name = "psd-project-settings-output-mode",
            };
            section.Add(outputModeField);

            void ApplySettings(
                PsdImporter.OutputDirectoryMode outputMode,
                PsdImporter.PrefabOutputMode prefabMode,
                string atlasOutputPath,
                string textureOutputPath,
                string prefabOutputPath,
                PsdImporter.SpriteAtlasVersion spriteAtlasVersion)
            {
                if (outputMode != PsdImporter.OutputDirectoryMode.PsdDirectory)
                {
                    outputMode = PsdImporter.OutputDirectoryMode.FixedPath;
                }

                settings.SetOutputSettings(
                    outputMode,
                    snapshot.outputFolderName,
                    string.Empty,
                    prefabMode,
                    atlasOutputPath,
                    textureOutputPath,
                    prefabOutputPath,
                    spriteAtlasVersion);
                ReplaceSection(section, CreateOutputSection(settings));
            }

            outputModeField.RegisterValueChangedCallback(change =>
            {
                PsdImporter.OutputDirectoryMode outputMode = outputModeChoices.IndexOf(change.newValue) == 1
                    ? PsdImporter.OutputDirectoryMode.FixedPath
                    : PsdImporter.OutputDirectoryMode.PsdDirectory;
                ApplySettings(
                    outputMode,
                    snapshot.prefabMode,
                    snapshot.atlasOutputPath,
                    snapshot.textureOutputPath,
                    snapshot.prefabOutputPath,
                    snapshot.spriteAtlasVersion);
            });

            if (snapshot.outputMode == PsdImporter.OutputDirectoryMode.PsdDirectory)
            {
                return section;
            }

            var fixedOutput = new VisualElement { name = FixedOutputContentName };
            section.Add(fixedOutput);

            var prefabModeChoices = new List<string> { "输出文件夹同级（默认）", "输出文件夹内部", "自定义位置" };
            int prefabModeIndex = snapshot.prefabMode == PsdImporter.PrefabOutputMode.CustomPath ||
                                  !string.IsNullOrWhiteSpace(snapshot.prefabOutputPath)
                ? 2
                : snapshot.prefabMode == PsdImporter.PrefabOutputMode.InsideOutputFolder ? 1 : 0;
            var prefabModeField = new PopupField<string>("Prefab 输出位置", prefabModeChoices, prefabModeIndex)
            {
                name = "psd-project-settings-prefab-output-mode",
            };
            fixedOutput.Add(prefabModeField);
            prefabModeField.RegisterValueChangedCallback(change =>
            {
                int selectedMode = prefabModeChoices.IndexOf(change.newValue);
                PsdImporter.PrefabOutputMode prefabMode = selectedMode == 1
                    ? PsdImporter.PrefabOutputMode.InsideOutputFolder
                    : selectedMode == 2
                        ? PsdImporter.PrefabOutputMode.CustomPath
                        : PsdImporter.PrefabOutputMode.SiblingToOutputFolder;
                ApplySettings(
                    snapshot.outputMode,
                    prefabMode,
                    snapshot.atlasOutputPath,
                    snapshot.textureOutputPath,
                    selectedMode == 2 ? snapshot.prefabOutputPath : string.Empty,
                    snapshot.spriteAtlasVersion);
            });
            if (prefabModeIndex == 2)
            {
                var prefabOutputPathField = CreateDelayedTextField(
                    "Prefab 输出目录",
                    snapshot.prefabOutputPath,
                    "psd-project-settings-prefab-output-path",
                    "项目内 Assets 路径，例如 Assets/UI/Prefabs。");
                fixedOutput.Add(prefabOutputPathField);
                prefabOutputPathField.RegisterValueChangedCallback(change =>
                    ApplySettings(
                        snapshot.outputMode,
                        PsdImporter.PrefabOutputMode.CustomPath,
                        snapshot.atlasOutputPath,
                        snapshot.textureOutputPath,
                        change.newValue,
                        snapshot.spriteAtlasVersion));
            }

            AddOutputPathControls(
                fixedOutput,
                "图集输出位置",
                "图集输出目录",
                "psd-project-settings-atlas-output-path",
                snapshot.atlasOutputPath,
                path => ApplySettings(
                    snapshot.outputMode,
                    snapshot.prefabMode,
                    path,
                    snapshot.textureOutputPath,
                    snapshot.prefabOutputPath,
                    snapshot.spriteAtlasVersion));
            AddOutputPathControls(
                fixedOutput,
                "贴图输出位置",
                "贴图输出目录",
                "psd-project-settings-texture-output-path",
                snapshot.textureOutputPath,
                path => ApplySettings(
                    snapshot.outputMode,
                    snapshot.prefabMode,
                    snapshot.atlasOutputPath,
                    path,
                    snapshot.prefabOutputPath,
                    snapshot.spriteAtlasVersion));

            var atlasVersionChoices = new List<string> { "Sprite Atlas V1（默认）", "Sprite Atlas V2" };
            int atlasVersionIndex = snapshot.spriteAtlasVersion == PsdImporter.SpriteAtlasVersion.V2 ? 1 : 0;
            var atlasVersionField = new PopupField<string>("图集版本", atlasVersionChoices, atlasVersionIndex)
            {
                name = "psd-project-settings-atlas-version",
            };
            fixedOutput.Add(atlasVersionField);
            atlasVersionField.RegisterValueChangedCallback(change =>
                ApplySettings(
                    snapshot.outputMode,
                    snapshot.prefabMode,
                    snapshot.atlasOutputPath,
                    snapshot.textureOutputPath,
                    snapshot.prefabOutputPath,
                    atlasVersionChoices.IndexOf(change.newValue) == 1
                        ? PsdImporter.SpriteAtlasVersion.V2
                        : PsdImporter.SpriteAtlasVersion.V1));
            return section;
        }

        private static void AddOutputPathControls(
            VisualElement parent,
            string modeLabel,
            string pathLabel,
            string pathElementName,
            string currentPath,
            Action<string> applyPath)
        {
            var choices = new List<string> { "输出根目录下（默认）", "自定义位置" };
            int currentIndex = string.IsNullOrWhiteSpace(currentPath) ? 0 : 1;
            var modeField = new PopupField<string>(modeLabel, choices, currentIndex);
            parent.Add(modeField);
            TextField pathField = null;

            void AddPathField()
            {
                if (pathField != null)
                {
                    return;
                }

                pathField = CreateDelayedTextField(
                    pathLabel,
                    currentPath,
                    pathElementName,
                    "项目内 Assets 路径。");
                parent.Add(pathField);
                pathField.RegisterValueChangedCallback(change => applyPath(change.newValue));
            }

            modeField.RegisterValueChangedCallback(change =>
            {
                if (choices.IndexOf(change.newValue) == 1)
                {
                    AddPathField();
                    return;
                }

                applyPath(string.Empty);
            });
            if (currentIndex == 1)
            {
                AddPathField();
            }
        }

        private static VisualElement CreateUiComponentSection(PsdLayoutProjectSettings settings)
        {
            VisualElement section = CreateSection(UiComponentSectionName, "UI 组件类型");
            PsdLayoutProjectUiComponentSnapshot snapshot = settings.ResolveUiComponentSettings();
            string imageTypeName = snapshot.imageComponentType.FullName;
            string buttonTypeName = snapshot.buttonComponentType.FullName;
            section.Add(new HelpBox(
                "填写生成节点要挂载的组件类名。可填写完整命名空间，或填写唯一的类名；Image 类型必须继承 UnityEngine.UI.Image，Button 类型可使用任意 MonoBehaviour（包括 UnityEngine.UI.Button）。",
                HelpBoxMessageType.Info));

            var validationBox = CreateHiddenErrorBox();
            TextField imageField = null;
            TextField buttonField = null;

            void ApplyTypes(string imageType, string buttonType)
            {
                if (!settings.TrySetUiComponentTypes(imageType, buttonType, out string error))
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        ShowError(validationBox, error);
                    }

                    return;
                }

                ReplaceSection(section, CreateUiComponentSection(settings));
            }

            imageField = CreateFuzzyComponentTypeField(
                section,
                "Image 组件类型",
                imageTypeName,
                "psd-project-settings-image-component-type",
                "例如 Game.UI.CustomImage。留空时使用 UnityEngine.UI.Image。",
                typeof(UnityEngine.UI.Image),
                value => ApplyTypes(value, buttonField.value));
            buttonField = CreateFuzzyComponentTypeField(
                section,
                "Button 组件类型",
                buttonTypeName,
                "psd-project-settings-button-component-type",
                "例如 Game.UI.CustomTouchButton。留空时使用 UnityEngine.UI.Button。",
                typeof(MonoBehaviour),
                value => ApplyTypes(imageField.value, value));
            section.Add(validationBox);
            return section;
        }

        private static TextField CreateFuzzyComponentTypeField(
            VisualElement parent,
            string label,
            string value,
            string name,
            string tooltip,
            Type expectedBaseType,
            Action<string> onCommitted)
        {
            var field = new TextField(label)
            {
                value = value,
                name = name,
                tooltip = tooltip,
                isDelayed = false,
            };
            var suggestions = new VisualElement();
            suggestions.style.display = DisplayStyle.None;
            suggestions.style.maxHeight = 144;
            suggestions.style.overflow = Overflow.Hidden;
            suggestions.style.marginLeft = 8;
            suggestions.style.marginTop = -2;
            suggestions.style.marginBottom = 4;
            suggestions.style.borderLeftWidth = 1;
            suggestions.style.borderRightWidth = 1;
            suggestions.style.borderTopWidth = 1;
            suggestions.style.borderBottomWidth = 1;
            suggestions.style.borderLeftColor = new Color(0.25f, 0.35f, 0.5f, 1f);
            suggestions.style.borderRightColor = new Color(0.25f, 0.35f, 0.5f, 1f);
            suggestions.style.borderTopColor = new Color(0.25f, 0.35f, 0.5f, 1f);
            suggestions.style.borderBottomColor = new Color(0.25f, 0.35f, 0.5f, 1f);

            void RefreshSuggestions(string query)
            {
                suggestions.Clear();
                List<string> matches = PsdLayoutProjectUiComponentSettings.FindComponentTypeNames(
                    expectedBaseType,
                    query,
                    12);
                if (matches.Count == 0)
                {
                    suggestions.style.display = DisplayStyle.None;
                    return;
                }

                foreach (string match in matches)
                {
                    var option = new Button(() =>
                    {
                        field.SetValueWithoutNotify(match);
                        onCommitted(match);
                    })
                    {
                        text = match,
                        tooltip = match,
                    };
                    option.style.unityTextAlign = TextAnchor.MiddleLeft;
                    option.style.fontSize = 10;
                    option.style.height = 21;
                    option.style.marginLeft = 0;
                    option.style.marginRight = 0;
                    suggestions.Add(option);
                }

                suggestions.style.display = DisplayStyle.Flex;
            }

            field.RegisterValueChangedCallback(change => RefreshSuggestions(change.newValue));
            field.RegisterCallback<FocusInEvent>(_ => RefreshSuggestions(field.value));
            field.RegisterCallback<FocusOutEvent>(_ =>
                suggestions.schedule.Execute(() =>
                {
                    suggestions.style.display = DisplayStyle.None;
                    onCommitted(field.value);
                }).ExecuteLater(120));
            parent.Add(field);
            parent.Add(suggestions);
            return field;
        }

        private static VisualElement CreateFontSection(PsdLayoutProjectSettings settings)
        {
            VisualElement section = CreateSection(FontSectionName, "TextMeshPro 默认配置");
            PsdLayoutProjectFontSnapshot snapshot = settings.ResolveFontSettings();
            section.Add(new HelpBox(
                "所有 PSD 导入共用的项目级默认配置。该资产属于使用方项目，可以提交到 Git。",
                HelpBoxMessageType.Info));

            var fontField = new ObjectField("TMP 字体资产")
            {
                name = "psd-project-settings-tmp-font",
                objectType = typeof(TMP_FontAsset),
                value = snapshot.font,
                tooltip = "PSD 文本默认使用的 TMP_FontAsset。留空时使用 TMP 默认字体。",
            };
            var materialField = new ObjectField("TMP 基础材质")
            {
                name = "psd-project-settings-tmp-material",
                objectType = typeof(Material),
                value = snapshot.baseMaterial,
                tooltip = "可选，用于生成描边和阴影材质变体。",
            };
            section.Add(fontField);
            section.Add(materialField);
            fontField.RegisterValueChangedCallback(change =>
            {
                settings.SetFontSettings(change.newValue as TMP_FontAsset, snapshot.baseMaterial);
                ReplaceSection(section, CreateFontSection(settings));
            });
            materialField.RegisterValueChangedCallback(change =>
            {
                settings.SetFontSettings(snapshot.font, change.newValue as Material);
                ReplaceSection(section, CreateFontSection(settings));
            });

            if (snapshot.fontStatus == PsdProjectAssetStatus.Missing)
            {
                section.Add(new HelpBox("配置的 TMP 字体已丢失或类型不正确。", HelpBoxMessageType.Warning));
            }

            if (snapshot.materialStatus == PsdProjectAssetStatus.Missing)
            {
                section.Add(new HelpBox("配置的 TMP 基础材质已丢失或类型不正确。", HelpBoxMessageType.Warning));
            }

            if (snapshot.font != null && snapshot.baseMaterial != null &&
                !PsdPrefabTextMaterialFactory.IsCompatibleWithFont(snapshot.baseMaterial, snapshot.font))
            {
                section.Add(new HelpBox(
                    "TMP 基础材质与所选字体图集不兼容。导入时将使用字体自带材质。",
                    HelpBoxMessageType.Warning));
            }

            return section;
        }

        private static VisualElement CreatePreviewServerSection(PsdLayoutProjectSettings settings)
        {
            VisualElement section = CreateSection("psd-project-settings-preview-server", "本地资源预览服务");
            var port = new IntegerField("端口") { value = settings.ResolvePreviewServerPort(), isDelayed = true, name = "psd-project-settings-preview-server-port" };
            var status = new Label();
            var actions = new VisualElement(); actions.style.flexDirection = FlexDirection.Row;
            var start = new Button { text = "启动", name = "psd-project-settings-preview-server-start" };
            var stop = new Button { text = "停止" };
            var open = new Button { text = "在浏览器打开" };
            actions.Add(start); actions.Add(stop); actions.Add(open);
            section.Add(port); section.Add(status); section.Add(actions);
            void Refresh()
            {
                bool running = PsdCommonAssetPreviewServer.IsRunning;
                string address = PsdCommonAssetPreviewServer.GetLocalAddress();
                status.text = running ? "● 运行中  " + address : string.IsNullOrEmpty(PsdCommonAssetPreviewServer.Error) ? "● 已停止" : PsdCommonAssetPreviewServer.Error;
                status.style.color = running ? new Color(0.25f, 0.85f, 0.5f) : new Color(0.75f, 0.78f, 0.84f);
                start.SetEnabled(!running); stop.SetEnabled(running); open.SetEnabled(running);
            }
            port.RegisterValueChangedCallback(change => { settings.TrySetPreviewServerPort(change.newValue, out _); Refresh(); });
            start.clicked += () => { PsdCommonAssetPreviewServer.Start(settings.ResolvePreviewServerPort()); Refresh(); };
            stop.clicked += () => { PsdCommonAssetPreviewServer.Stop(); Refresh(); };
            open.clicked += () => Application.OpenURL(PsdCommonAssetPreviewServer.GetLocalAddress());
            Refresh(); return section;
        }

        private static VisualElement CreateCommonNamingSection(PsdLayoutProjectSettings settings)
        {
            VisualElement section = CreateSection(CommonNamingSectionName, "通用资源命名");
            PsdCommonAssetNamingSnapshot naming = settings.ResolveCommonAssetNaming();
            section.Add(new HelpBox(
                "这些前缀同时用于 PSD 图层名和 Unity 资源名。前缀后的剩余文本作为映射表资源键，末尾下划线会自动补全。",
                HelpBoxMessageType.Info));

            var prefabPrefixField = CreateDelayedTextField(
                "Prefab 前缀",
                naming.prefabPrefix,
                "psd-project-settings-prefab-prefix",
                "可复用 Prefab 图层和 Prefab 资源名称使用的前缀。");
            var texturePrefixField = CreateDelayedTextField(
                "Texture 前缀",
                naming.texturePrefix,
                "psd-project-settings-texture-prefix",
                "可复用纹理图层和纹理资源名称使用的前缀。");
            var validationBox = CreateHiddenErrorBox();
            section.Add(prefabPrefixField);
            section.Add(texturePrefixField);
            section.Add(validationBox);

            var refreshCatalog = new Button(() =>
            {
                PingCommonAssetCatalog(PsdCommonAssetCatalog.CreateOrRefresh());
            })
            {
                name = CommonCatalogRefreshButtonName,
                text = "生成 / 刷新公共资源映射表",
                tooltip = "扫描项目中的公共 Prefab 和 Texture 资源，并更新公共资源映射表。",
            };
            refreshCatalog.style.marginTop = 6;
            section.Add(refreshCatalog);

            void ApplyPrefixes(string prefabPrefix, string texturePrefix)
            {
                if (!settings.TrySetCommonAssetPrefixes(prefabPrefix, texturePrefix, out string error))
                {
                    ShowError(validationBox, error);
                    return;
                }

                ReplaceSection(section, CreateCommonNamingSection(settings));
            }

            prefabPrefixField.RegisterValueChangedCallback(change => ApplyPrefixes(change.newValue, texturePrefixField.value));
            texturePrefixField.RegisterValueChangedCallback(change => ApplyPrefixes(prefabPrefixField.value, change.newValue));
            return section;
        }

        internal static void PingCommonAssetCatalog(PsdCommonAssetCatalog catalog)
        {
            if (catalog != null)
            {
                EditorGUIUtility.PingObject(catalog);
            }
        }
    }
}
