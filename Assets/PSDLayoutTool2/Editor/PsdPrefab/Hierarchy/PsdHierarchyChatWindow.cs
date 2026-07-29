namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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
        internal const string CopyMessageButtonClassName = "psd-hierarchy-chat-message-copy";

        private const string StyleSheetGuid = "18f53073502d4d7e89345f900b727c7e";
        private const int MaxAutomaticPlanRepairAttempts = 1;
        private const string ComponentExtractionFollowUpPrompt =
            "第一阶段层级整理已经应用。请基于当前最新快照继续完成第二阶段：为所有 requiresExtraction:true 的重复组件候选生成完整的 Prefab 抽取计划。不要重新组织已完成的层级；只处理这些组件候选并返回完整 JSON 计划。";

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
        private string pendingPlanJson = string.Empty;
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
            pendingPlanJson = string.Empty;
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

            string prompt = draftField.value ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(pendingPlanJson) &&
                (string.IsNullOrWhiteSpace(prompt) || PsdHierarchyChatCleanupExecution.IsApplyIntent(prompt)))
            {
                ApplyPendingPlan(string.IsNullOrWhiteSpace(prompt) ? "确认" : prompt);
                return;
            }

            if (PsdHierarchyChatCleanupExecution.IsApplyIntent(prompt))
            {
                AppendMessage("system", "当前没有通过校验的执行计划，因此不会重新请求 AI 或修改 Prefab。请先等待完整计划生成。 ");
                return;
            }

            SetPendingPlan(string.Empty);
            SendMessage(PsdHierarchyChatClient.ResolveUserPrompt(prompt), false);
        }

        private async void SendMessage(
            string prompt,
            bool isInitialRequest,
            bool showUserMessage = true)
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
            if (showUserMessage)
            {
                AppendMessage("user", prompt);
            }
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
                string lastAssistantReply = string.Empty;
                string lastPlanError = string.Empty;
                string initialReviewText = string.Empty;
                for (int repairAttempt = 0; repairAttempt <= MaxAutomaticPlanRepairAttempts; repairAttempt++)
                {
                    PsdHierarchyChatSendResult result = await PsdHierarchyChatClient.SendWithCliSessionAsync(
                        context,
                        connection,
                        conversation,
                        cliSessionId);
                    if (!result.success)
                    {
                        HideThinkingIndicator();
                        SetPendingPlan(string.Empty);
                        string prefix = repairAttempt == 0 ? string.Empty : "AI 自动补全失败：";
                        AppendMessage("system", prefix + result.message + "。本轮未修改 Prefab。");
                        SetSending(false, repairAttempt == 0 ? "发送失败" : "自动补全失败");
                        return;
                    }

                    if (connection.connectionMode == PsdHierarchyAiConnectionMode.LocalCli)
                    {
                        cliSessionId = result.cliSessionId;
                        RefreshConnectionUi();
                    }

                    lastAssistantReply = result.message;
                    conversation.Add(new PsdHierarchyChatMessage("assistant", result.message));
                    if (repairAttempt == 0)
                    {
                        initialReviewText = PsdHierarchyChatCleanupExecution.ExtractReviewText(result.message);
                    }

                    if (PsdHierarchyChatCleanupExecution.TryExtractApprovedPlan(
                            result.message,
                            context,
                            out string planJson,
                            out string planError))
                    {
                        ShowThinkingIndicator("正在使用执行器校验 AI 计划...");
                        SetSending(true, "正在校验执行计划...");
                        PsdHierarchyChatCleanupExecutionResult validation =
                            await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, planJson);
                        HideThinkingIndicator();
                        if (validation.success)
                        {
                            string reviewableReply = repairAttempt == 0
                                ? result.message
                                : PsdHierarchyChatCleanupExecution.ComposeReviewableReply(initialReviewText, planJson);
                            AppendMessage("assistant", reviewableReply);
                            SetPendingPlan(planJson);
                            AppendMessage("system", "方案已就绪。点击“确认并更新”或回复“确认”即可直接更新当前 Prefab；确认不会再发送给 AI。 ");
                            SetSending(false, "方案待确认");
                            return;
                        }

                        lastPlanError = validation.message;
                    }
                    else
                    {
                        lastPlanError = planError;
                    }

                    if (repairAttempt >= MaxAutomaticPlanRepairAttempts)
                    {
                        break;
                    }

                    int nextAttempt = repairAttempt + 1;
                    string repairPrompt = PsdHierarchyChatClient.BuildJsonOnlyPlanRepairPrompt(lastPlanError, context);
                    conversation.Add(new PsdHierarchyChatMessage("user", repairPrompt));
                    ShowThinkingIndicator(
                        "AI 返回的计划未通过校验，正在同一会话自动补全（" + nextAttempt + "/" +
                        MaxAutomaticPlanRepairAttempts + "）...");
                    SetSending(
                        true,
                        "正在自动补全计划（" + nextAttempt + "/" + MaxAutomaticPlanRepairAttempts + "）...");
                }

                HideThinkingIndicator();
                SetPendingPlan(string.Empty);
                ResetFailedPlanConversation();
                string failedReply = string.IsNullOrWhiteSpace(initialReviewText)
                    ? lastAssistantReply
                    : initialReviewText;
                if (!string.IsNullOrWhiteSpace(failedReply))
                {
                    AppendMessage("assistant", failedReply);
                }

                AppendMessage(
                    "system",
                    "AI 自动补全 " + MaxAutomaticPlanRepairAttempts + " 次后仍未生成可执行计划：" +
                    lastPlanError + "。本轮未修改 Prefab。");
                SetSending(false, "计划生成失败");
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

        private async void ApplyPendingPlan(string confirmation)
        {
            if (context == null || isSending || string.IsNullOrWhiteSpace(pendingPlanJson))
            {
                return;
            }

            string planToApply = pendingPlanJson;
            SetPendingPlan(string.Empty);
            isSending = true;
            AppendMessage("user", confirmation.Trim());
            if (draftField != null)
            {
                draftField.value = string.Empty;
            }

            ShowThinkingIndicator("正在校验已确认方案，并通过 Unity Editor API 更新 Prefab...");
            SetSending(true, "正在更新 Prefab...");
            bool queueComponentExtractionFollowUp = false;
            bool queueFailureReanalysis = false;
            string applyFailure = string.Empty;
            try
            {
                PsdHierarchyChatCleanupExecutionResult result =
                    await PsdHierarchyChatCleanupExecution.ApplyConfirmedAsync(context, planToApply);
                HideThinkingIndicator();
                AppendMessage("system", result.message);
                SetSending(false, result.success ? "更新完成" : "更新失败");
                queueComponentExtractionFollowUp = result.success && IsHierarchyOnlyPlan(planToApply);
                if (!result.success)
                {
                    bool discarded = PsdHierarchyChatCleanupExecution.TryDiscardFailedReplayStage(
                        context,
                        planToApply,
                        out string discardError);
                    if (discarded)
                    {
                        AppendMessage("system", "已移除未执行成功的重放阶段，避免下次 PSD 生成再次重放旧计划。");
                    }
                    else if (!string.IsNullOrEmpty(discardError))
                    {
                        AppendMessage("system", "未能清理失败计划的旧重放阶段：" + discardError);
                    }

                    applyFailure = result.message;
                    queueFailureReanalysis = true;
                }
            }
            catch (Exception exception)
            {
                HideThinkingIndicator();
                AppendMessage("system", "更新 Prefab 时发生异常：" + exception.Message);
                SetSending(false, "更新失败");
            }
            finally
            {
                HideThinkingIndicator();
                isSending = false;
            }

            if (queueComponentExtractionFollowUp)
            {
                QueueComponentExtractionFollowUp();
            }
            else if (queueFailureReanalysis)
            {
                QueueFailedApplyReanalysis(applyFailure);
            }
        }

        private void QueueFailedApplyReanalysis(string failure)
        {
            if (!PsdHierarchyChatContextBuilder.TryCreate(
                    context.sourcePsdAssetPath,
                    context.targetPrefabAssetPath,
                    out PsdHierarchyChatContext refreshedContext,
                    out string error))
            {
                AppendMessage("system", "更新失败后无法刷新当前 Prefab 快照：" + error);
                return;
            }

            context = refreshedContext;
            ResetFailedPlanConversation();
            AppendMessage("system", "已刷新当前 Prefab 快照，正在基于失败原因生成新的待确认计划。");
            rootVisualElement.schedule.Execute(() => SendMessage(
                "The confirmed cleanup plan failed before the Prefab was saved. Re-analyze the current authoritative snapshot and return a complete replacement JSON plan. Do not repeat the failed extraction unchanged and do not request confirmation in this reply. Failure detail:\n" +
                (failure ?? string.Empty),
                false,
                false)).ExecuteLater(1);
        }

        private void QueueComponentExtractionFollowUp()
        {
            if (!PsdHierarchyChatContextBuilder.TryCreate(
                    context.sourcePsdAssetPath,
                    context.targetPrefabAssetPath,
                    out PsdHierarchyChatContext refreshedContext,
                    out string error))
            {
                AppendMessage("system", "第一阶段已完成，但无法刷新第二阶段抽取快照：" + error);
                return;
            }

            context = refreshedContext;
            ResetFailedPlanConversation();
            if (context.componentFamilyCandidates == null ||
                !context.componentFamilyCandidates.Any(candidate => candidate.requiresExtraction))
            {
                return;
            }

            AppendMessage("system", "第一阶段已完成。正在基于最新层级自动生成重复组件的第二阶段抽取方案；该方案生成后仍需确认才会写入 Prefab。");
            rootVisualElement.schedule.Execute(() => SendMessage(ComponentExtractionFollowUpPrompt, false)).ExecuteLater(1);
        }

        private static bool IsHierarchyOnlyPlan(string planJson)
        {
            try
            {
                var plan = Newtonsoft.Json.Linq.JObject.Parse(planJson ?? string.Empty);
                return new[]
                    {
                        "componentExtractions",
                        "stateComponentExtractions",
                        "variantComponentExtractions",
                        "statefulComponentExtractions",
                    }
                    .All(property => !(plan[property] is Newtonsoft.Json.Linq.JArray operations) || operations.Count == 0);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return false;
            }
        }

        internal void ShowThinkingIndicator(string content = "正在分析：读取整理技能、完整层级、节点几何、组件与重复结构...")
        {
            HideThinkingIndicator();
            if (messagesView == null)
            {
                return;
            }

            var indicator = new VisualElement { name = ThinkingIndicatorElementName };
            thinkingIndicator = indicator;
            indicator.AddToClassList("psd-hierarchy-chat-thinking");
            var roleLabel = new Label("AI");
            roleLabel.AddToClassList("psd-hierarchy-chat-message-role");
            indicator.Add(roleLabel);
            var contentLabel = new Label(content);
            contentLabel.AddToClassList("psd-hierarchy-chat-thinking-content");
            contentLabel.style.whiteSpace = WhiteSpace.Normal;
            indicator.Add(contentLabel);
            ScrollView targetView = messagesView;
            targetView.Add(indicator);
            targetView.schedule.Execute(() => ScrollToIfAttached(targetView, indicator));
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

        private void SetPendingPlan(string planJson)
        {
            pendingPlanJson = planJson ?? string.Empty;
            bool canApply = !string.IsNullOrWhiteSpace(pendingPlanJson);
            if (sendButton != null)
            {
                sendButton.text = canApply ? "确认并更新" : "发送追问";
            }

            if (draftField != null)
            {
                draftField.tooltip = canApply
                    ? "点击确认并更新，或输入“确认”后发送。输入其他内容会继续追问并使当前方案失效。"
                    : "继续追问 AI";
            }
        }

        private void ResetFailedPlanConversation()
        {
            ResetConversationForFreshSnapshot(conversation, ref cliSessionId);
            RefreshConnectionUi();
        }

        internal static void ResetConversationForFreshSnapshot(
            List<PsdHierarchyChatMessage> conversation,
            ref string cliSessionId)
        {
            if (conversation == null)
            {
                throw new ArgumentNullException(nameof(conversation));
            }

            conversation.Clear();
            cliSessionId = string.Empty;
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

            VisualElement message = CreateMessageElement(role, content);
            ScrollView targetView = messagesView;
            targetView.Add(message);
            targetView.schedule.Execute(() => ScrollToIfAttached(targetView, message));
        }

        internal static void ScrollToIfAttached(ScrollView scrollView, VisualElement child)
        {
            if (scrollView == null || child == null || scrollView.panel == null ||
                child.panel != scrollView.panel)
            {
                return;
            }

            VisualElement ancestor = child.parent;
            while (ancestor != null && ancestor != scrollView)
            {
                ancestor = ancestor.parent;
            }

            if (ancestor == scrollView)
            {
                scrollView.ScrollTo(child);
            }
        }

        internal static VisualElement CreateMessageElement(string role, string content)
        {
            string messageContent = content ?? string.Empty;
            var message = new VisualElement();
            message.AddToClassList("psd-hierarchy-chat-message");
            message.AddToClassList("psd-hierarchy-chat-message-" + role);
            var roleLabel = new Label(RoleLabel(role));
            roleLabel.AddToClassList("psd-hierarchy-chat-message-role");
            message.Add(roleLabel);
            var contentLabel = new Label(messageContent);
            contentLabel.AddToClassList("psd-hierarchy-chat-message-content");
            contentLabel.style.whiteSpace = WhiteSpace.Normal;
            message.Add(contentLabel);

            var copyButton = new Button(() => CopyMessageToClipboard(messageContent))
            {
                tooltip = "复制完整消息",
            };
            copyButton.AddToClassList(CopyMessageButtonClassName);
            GUIContent copyIconContent = EditorGUIUtility.IconContent("d_TreeEditor.Duplicate");
            if (copyIconContent.image != null)
            {
                var copyIcon = new Image
                {
                    image = copyIconContent.image,
                    pickingMode = PickingMode.Ignore,
                };
                copyIcon.AddToClassList("psd-hierarchy-chat-message-copy-icon");
                copyButton.Add(copyIcon);
            }
            else
            {
                copyButton.text = "复制";
            }

            message.Add(copyButton);
            return message;
        }

        internal static void CopyMessageToClipboard(string content)
        {
            EditorGUIUtility.systemCopyBuffer = content ?? string.Empty;
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
