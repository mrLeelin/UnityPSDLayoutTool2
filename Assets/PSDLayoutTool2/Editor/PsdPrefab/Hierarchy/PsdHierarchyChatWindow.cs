namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    internal sealed class PsdHierarchyChatWindow : EditorWindow
    {
        internal const string RootElementName = "psd-hierarchy-chat-root";
        internal const string MessagesElementName = "psd-hierarchy-chat-messages";
        internal const string DraftFieldName = "psd-hierarchy-chat-draft";
        internal const string SendButtonName = "psd-hierarchy-chat-send";
        internal const string PingPsdButtonName = "psd-hierarchy-chat-ping-psd";
        internal const string PingPrefabButtonName = "psd-hierarchy-chat-ping-prefab";
        internal const string ThinkingIndicatorElementName = "psd-hierarchy-chat-thinking";
        internal const string AgentInfoElementName = "psd-hierarchy-chat-agent-info";
        internal const string OpenCliButtonName = "psd-hierarchy-chat-open-cli";

        private const string StyleSheetGuid = "18f53073502d4d7e89345f900b727c7e";

        private PsdHierarchyChatContext context;
        private readonly List<PsdHierarchyChatMessage> conversation = new List<PsdHierarchyChatMessage>();

        private ScrollView messagesView;
        private TextField draftField;
        private Label statusLabel;
        private Label agentInfoLabel;
        private Button sendButton;
        private Button openCliButton;
        private VisualElement thinkingIndicator;
        private PsdHierarchyChatConnection activeConnection;
        private string cliSessionId = string.Empty;
        private bool initialRequestQueued;
        private bool isSending;
        private bool hasActiveConnection;

        [MenuItem("Tools/PSD Layout Tool 2/AI Hierarchy Chat")]
        private static void ShowEmptyWindow()
        {
            PsdHierarchyChatWindow window = GetWindow<PsdHierarchyChatWindow>();
            window.titleContent = new GUIContent("AI 层级整理");
            window.minSize = new Vector2(560f, 600f);
            window.Show();
        }

        internal static bool TryOpen(string sourcePsdAssetPath, string targetPrefabAssetPath, out string error)
        {
            if (!PsdHierarchyChatContextBuilder.TryCreate(
                    sourcePsdAssetPath,
                    targetPrefabAssetPath,
                    out PsdHierarchyChatContext chatContext,
                    out error))
            {
                return false;
            }

            PsdHierarchyChatWindow window = GetWindow<PsdHierarchyChatWindow>();
            window.titleContent = new GUIContent("AI 层级整理");
            window.minSize = new Vector2(560f, 600f);
            window.Show();
            window.Initialize(chatContext);
            error = string.Empty;
            return true;
        }

        public void CreateGUI()
        {
            RebuildUi();
        }

        internal void InitializeForTests(PsdHierarchyChatContext chatContext)
        {
            Initialize(chatContext, false);
        }

        private void Initialize(PsdHierarchyChatContext chatContext)
        {
            Initialize(chatContext, true);
        }

        private void Initialize(PsdHierarchyChatContext chatContext, bool autoSendInitialRequest)
        {
            context = chatContext ?? throw new ArgumentNullException(nameof(chatContext));
            conversation.Clear();
            initialRequestQueued = false;
            isSending = false;
            hasActiveConnection = false;
            activeConnection = default(PsdHierarchyChatConnection);
            cliSessionId = string.Empty;
            RebuildUi();
            if (autoSendInitialRequest)
            {
                QueueInitialRequest();
            }
        }

        private void RebuildUi()
        {
            rootVisualElement.Clear();
            thinkingIndicator = null;
            agentInfoLabel = null;
            openCliButton = null;
            string styleSheetPath = AssetDatabase.GUIDToAssetPath(StyleSheetGuid);
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(styleSheetPath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            var root = new VisualElement { name = RootElementName };
            root.AddToClassList("psd-hierarchy-chat");
            root.style.flexGrow = 1f;
            root.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(root);

            if (context == null)
            {
                var emptyState = new Label("从 PSD Inspector 点击“AI整理”后打开此窗口。")
                {
                    name = "psd-hierarchy-chat-empty-state",
                };
                emptyState.AddToClassList("psd-hierarchy-chat-empty-state");
                root.Add(emptyState);
                return;
            }

            var header = new VisualElement { name = "psd-hierarchy-chat-header" };
            header.AddToClassList("psd-hierarchy-chat-header");
            var titleRow = new VisualElement();
            titleRow.AddToClassList("psd-hierarchy-chat-title-row");
            var title = new Label("AI 层级整理") { name = "psd-hierarchy-chat-title" };
            title.AddToClassList("psd-hierarchy-chat-title");
            titleRow.Add(title);
            statusLabel = new Label("准备分析")
            {
                name = "psd-hierarchy-chat-status",
            };
            statusLabel.AddToClassList("psd-hierarchy-chat-status");
            titleRow.Add(statusLabel);
            header.Add(titleRow);

            var targetCaption = new Label("目标 Prefab");
            targetCaption.AddToClassList("psd-hierarchy-chat-target-caption");
            header.Add(targetCaption);
            var targetLabel = new Label(context.targetPrefabAssetPath)
            {
                name = "psd-hierarchy-chat-target",
            };
            targetLabel.AddToClassList("psd-hierarchy-chat-target");
            header.Add(targetLabel);

            agentInfoLabel = new Label(BuildConnectionSummary())
            {
                name = AgentInfoElementName,
            };
            agentInfoLabel.AddToClassList("psd-hierarchy-chat-agent-info");
            header.Add(agentInfoLabel);

            var resourceActions = new VisualElement { name = "psd-hierarchy-chat-resource-actions" };
            resourceActions.AddToClassList("psd-hierarchy-chat-resource-actions");
            resourceActions.Add(CreatePingButton(
                PingPsdButtonName,
                context.sourcePsdAssetPath,
                "PSD"));
            resourceActions.Add(CreatePingButton(
                PingPrefabButtonName,
                context.targetPrefabAssetPath,
                "Prefab"));
            header.Add(resourceActions);

            openCliButton = new Button(OpenCurrentConversationInCli)
            {
                name = OpenCliButtonName,
                text = "打开本次 CLI",
                tooltip = "在外部终端中恢复当前 CLI 会话。",
            };
            openCliButton.AddToClassList("psd-hierarchy-chat-open-cli");
            header.Add(openCliButton);
            RefreshConnectionUi();
            root.Add(header);

            messagesView = new ScrollView { name = MessagesElementName };
            messagesView.AddToClassList("psd-hierarchy-chat-messages");
            messagesView.style.flexGrow = 1f;
            messagesView.style.minHeight = 260f;
            root.Add(messagesView);

            var composer = new VisualElement();
            composer.AddToClassList("psd-hierarchy-chat-composer");
            composer.style.flexShrink = 0f;
            draftField = new TextField
            {
                name = DraftFieldName,
                multiline = true,
                tooltip = "继续追问 AI",
            };
            draftField.AddToClassList("psd-hierarchy-chat-draft");
            composer.Add(draftField);

            var footer = new VisualElement();
            footer.AddToClassList("psd-hierarchy-chat-footer");
            sendButton = new Button(SendCurrentMessage) { name = SendButtonName, text = "发送追问" };
            sendButton.AddToClassList("psd-hierarchy-chat-send");
            footer.Add(sendButton);
            composer.Add(footer);
            root.Add(composer);
        }

        private static Button CreatePingButton(string elementName, string assetPath, string assetType)
        {
            string displayName = Path.GetFileName(assetPath);
            var button = new Button(() => PingAsset(assetPath))
            {
                name = elementName,
                text = string.IsNullOrEmpty(displayName) ? assetType : displayName,
                tooltip = "在 Project 中定位 " + assetType + "：" + assetPath,
            };
            button.AddToClassList("psd-hierarchy-chat-resource-button");

            if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
            {
                button.SetEnabled(false);
                button.tooltip = assetType + " 资源不存在：" + assetPath;
            }

            return button;
        }

        private static void PingAsset(string assetPath)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
            {
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void QueueInitialRequest()
        {
            if (initialRequestQueued || context == null)
            {
                return;
            }

            initialRequestQueued = true;
            rootVisualElement.schedule.Execute(() =>
            {
                SendMessage(PsdHierarchyChatClient.DefaultUserPrompt, true);
            }).ExecuteLater(1);
        }

        private void SendCurrentMessage()
        {
            if (draftField == null)
            {
                return;
            }

            SendMessage(PsdHierarchyChatClient.ResolveUserPrompt(draftField.value), false);
        }

        private async void SendMessage(string prompt, bool isInitialRequest)
        {
            if (context == null || isSending)
            {
                return;
            }

            if (!TryResolveConnection(out PsdHierarchyChatConnection connection, out string configurationError))
            {
                AppendMessage("system", configurationError);
                SetSending(false, "请先完成全局配置。");
                return;
            }

            isSending = true;
            conversation.Add(new PsdHierarchyChatMessage("user", prompt));
            AppendMessage("user", prompt);
            ShowThinkingIndicator();
            if (!isInitialRequest && draftField != null)
            {
                draftField.value = string.Empty;
            }

            SetSending(
                true,
                isInitialRequest ? "正在分析完整层级..." : "正在发送追问...");

            try
            {
                PsdHierarchyChatSendResult result = await PsdHierarchyChatClient.SendWithCliSessionAsync(
                    context,
                    connection,
                    conversation,
                    cliSessionId);
                if (result.success)
                {
                    if (connection.connectionMode == PsdHierarchyAiConnectionMode.LocalCli)
                    {
                        cliSessionId = result.cliSessionId;
                        RefreshConnectionUi();
                    }

                    HideThinkingIndicator();
                    conversation.Add(new PsdHierarchyChatMessage("assistant", result.message));
                    AppendMessage("assistant", result.message);
                    SetSending(false, "分析完成");
                }
                else
                {
                    HideThinkingIndicator();
                    AppendMessage("system", result.message);
                    SetSending(false, "发送失败");
                }
            }
            catch (Exception exception)
            {
                HideThinkingIndicator();
                AppendMessage("system", "AI 对话发生异常：" + exception.Message);
                SetSending(false, "发送失败");
            }
            finally
            {
                HideThinkingIndicator();
                isSending = false;
            }
        }

        internal void ShowThinkingIndicator()
        {
            HideThinkingIndicator();
            if (messagesView == null)
            {
                return;
            }

            thinkingIndicator = new VisualElement { name = ThinkingIndicatorElementName };
            thinkingIndicator.AddToClassList("psd-hierarchy-chat-thinking");
            var roleLabel = new Label("AI");
            roleLabel.AddToClassList("psd-hierarchy-chat-message-role");
            thinkingIndicator.Add(roleLabel);
            var contentLabel = new Label("正在分析：读取整理技能、完整层级、节点几何、组件与重复结构...");
            contentLabel.AddToClassList("psd-hierarchy-chat-thinking-content");
            contentLabel.style.whiteSpace = WhiteSpace.Normal;
            thinkingIndicator.Add(contentLabel);
            messagesView.Add(thinkingIndicator);
            messagesView.schedule.Execute(() => messagesView.ScrollTo(thinkingIndicator));
        }

        internal void HideThinkingIndicator()
        {
            if (thinkingIndicator == null)
            {
                return;
            }

            thinkingIndicator.RemoveFromHierarchy();
            thinkingIndicator = null;
        }

        private void SetSending(bool sending, string status)
        {
            if (draftField != null) draftField.SetEnabled(!sending);
            if (sendButton != null) sendButton.SetEnabled(!sending);
            if (statusLabel != null) statusLabel.text = status;
        }

        private void OpenCurrentConversationInCli()
        {
            if (!TryResolveConnection(out PsdHierarchyChatConnection connection, out string error))
            {
                AppendMessage("system", error);
                return;
            }

            if (!PsdHierarchyChatClient.TryOpenInteractiveCli(
                    connection,
                    context.projectRoot,
                    cliSessionId,
                    out error))
            {
                AppendMessage("system", error);
                return;
            }

            AppendMessage("system", "已在外部终端恢复本次对话的同一 CLI 会话。");
        }

        private void AppendMessage(string role, string content)
        {
            if (messagesView == null)
            {
                return;
            }

            var message = new VisualElement();
            message.AddToClassList("psd-hierarchy-chat-message");
            message.AddToClassList("psd-hierarchy-chat-message-" + role);
            var roleLabel = new Label(RoleLabel(role));
            roleLabel.AddToClassList("psd-hierarchy-chat-message-role");
            message.Add(roleLabel);
            var contentLabel = new Label(content ?? string.Empty);
            contentLabel.AddToClassList("psd-hierarchy-chat-message-content");
            contentLabel.style.whiteSpace = WhiteSpace.Normal;
            message.Add(contentLabel);
            messagesView.Add(message);
            messagesView.schedule.Execute(() => messagesView.ScrollTo(message));
        }

        private bool TryResolveConnection(out PsdHierarchyChatConnection connection, out string error)
        {
            if (hasActiveConnection)
            {
                connection = activeConnection;
                return connection.TryValidate(out error);
            }

            connection = default(PsdHierarchyChatConnection);
            PsdHierarchyAiSettingsSnapshot settings = PsdLayoutProjectSettings.instance.ResolveHierarchyAiSettings();
            if (!settings.TryValidate(out error))
            {
                return false;
            }

            if (settings.connectionMode == PsdHierarchyAiConnectionMode.LocalCli)
            {
                if (!PsdHierarchyAiCliDiscovery.TryGetInstalled(settings.provider, out PsdHierarchyAiCliDescriptor cli))
                {
                    error = "全局配置选择的 AI CLI 当前不可用，请打开全局配置重新选择。";
                    return false;
                }

                connection = new PsdHierarchyChatConnection(
                    settings.provider,
                    settings.connectionMode,
                    cli.executablePath,
                    string.Empty,
                    string.Empty,
                    string.Empty);
            }
            else
            {
                var secretStore = new PsdHierarchyAiSecretStore();
                if (!secretStore.TryReadApiKey(context.projectRoot, settings.provider, out string apiKey))
                {
                    error = "请先在全局配置中填写自定义 API Key。";
                    return false;
                }

                connection = new PsdHierarchyChatConnection(
                    settings.provider,
                    settings.connectionMode,
                    string.Empty,
                    settings.ResolveEndpoint(),
                    settings.ResolveModel(),
                    apiKey);
            }

            if (!connection.TryValidate(out error))
            {
                return false;
            }

            activeConnection = connection;
            hasActiveConnection = true;
            RefreshConnectionUi();
            return true;
        }

        private string BuildConnectionSummary()
        {
            if (hasActiveConnection)
            {
                string summary = "Agent：" + PsdHierarchyChatClient.GetProviderDisplayName(activeConnection.provider) +
                    "    模型：" + PsdHierarchyChatClient.GetModelDisplayName(activeConnection);
                if (activeConnection.connectionMode != PsdHierarchyAiConnectionMode.LocalCli)
                {
                    return summary;
                }

                return summary + "    会话：" + (string.IsNullOrWhiteSpace(cliSessionId) ? "创建中" : "已建立");
            }

            PsdHierarchyAiSettingsSnapshot settings = PsdLayoutProjectSettings.instance.ResolveHierarchyAiSettings();
            string model = settings.connectionMode == PsdHierarchyAiConnectionMode.LocalCli
                ? "本地 CLI 默认"
                : settings.ResolveModel();
            return "Agent：" + PsdHierarchyChatClient.GetProviderDisplayName(settings.provider) +
                "    模型：" + model;
        }

        private void RefreshConnectionUi()
        {
            if (agentInfoLabel != null)
            {
                agentInfoLabel.text = BuildConnectionSummary();
            }

            if (openCliButton != null)
            {
                PsdHierarchyAiSettingsSnapshot settings = PsdLayoutProjectSettings.instance.ResolveHierarchyAiSettings();
                bool canOpenCli = hasActiveConnection
                    ? activeConnection.connectionMode == PsdHierarchyAiConnectionMode.LocalCli &&
                      File.Exists(activeConnection.cliExecutablePath) &&
                      !string.IsNullOrWhiteSpace(cliSessionId)
                    : false;
                openCliButton.SetEnabled(canOpenCli);
                openCliButton.tooltip = canOpenCli
                    ? "在外部终端恢复与窗口完全相同的 CLI 会话。"
                    : settings.connectionMode == PsdHierarchyAiConnectionMode.CustomApi
                        ? "当前会话使用自定义 API，不能打开本地 CLI。"
                        : hasActiveConnection
                            ? "AI 返回后将生成可恢复的 CLI 会话。"
                            : "请先开始本次对话以创建 CLI 会话。";
            }
        }

        private static string RoleLabel(string role)
        {
            if (string.Equals(role, "assistant", StringComparison.Ordinal)) return "AI";
            if (string.Equals(role, "system", StringComparison.Ordinal)) return "系统";
            return "你";
        }
    }
}
