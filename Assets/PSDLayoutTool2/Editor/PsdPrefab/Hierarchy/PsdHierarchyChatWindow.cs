namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json.Linq;
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
        internal const string OpenLocalRepairButtonName = "psd-hierarchy-chat-open-local-repair";
        internal const string RecoverySectionName = "psd-hierarchy-chat-recovery";
        internal const string RegeneratePlanButtonName = "psd-hierarchy-chat-regenerate-plan";
        internal const string CopyMessageButtonClassName = "psd-hierarchy-chat-message-copy";

        private const string StyleSheetGuid = "18f53073502d4d7e89345f900b727c7e";
        private const int MaxAutomaticPlanRepairAttempts = 1;
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
        private bool hasAppliedCleanupStage;
        private bool hasActiveConnection;
        private bool requiresReplayProfileReplacement;
        private string pendingRecoveryFailure = string.Empty;
        private Label recoverySummary;
        private Button regeneratePlanButton;
        private PsdHierarchyPlanWorkspace currentWorkspace;
        private PsdHierarchyPlanWorkspaceView workspaceView;

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

        internal PsdHierarchyPlanWorkspace CurrentWorkspaceForTests => currentWorkspace;

        internal void SetWorkspaceForTests(PsdHierarchyPlanWorkspace workspace)
        {
            SetPlanWorkspace(workspace);
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
            hasAppliedCleanupStage = PsdHierarchyCleanupReplayProfile.HasConfirmedStages(
                chatContext.sourcePsdAssetPath,
                chatContext.targetPrefabAssetPath);
            requiresReplayProfileReplacement = PsdHierarchyCleanupReplayProfile.RequiresRebind(
                chatContext.sourcePsdAssetPath,
                chatContext.targetPrefabAssetPath,
                out pendingRecoveryFailure);
            hasActiveConnection = false;
            activeConnection = default(PsdHierarchyChatConnection);
            cliSessionId = string.Empty;
            pendingPlanJson = string.Empty;
            currentWorkspace = null;
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
            var openLocalRepairButton = new Button(OpenLocalRepairWindow)
            {
                name = OpenLocalRepairButtonName,
                text = "局部整理",
                tooltip = "打开独立的局部整理窗口，分析当前 Prefab Stage 中选中的节点。",
            };
            openLocalRepairButton.AddToClassList("psd-hierarchy-chat-open-local-repair");
            header.Add(openLocalRepairButton);
            RefreshConnectionUi();
            root.Add(header);
            root.Add(CreateRecoverySection());
            workspaceView = new PsdHierarchyPlanWorkspaceView();
            root.Add(workspaceView);
            workspaceView.Bind(currentWorkspace, CreateWorkspaceActions());

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

        private VisualElement CreateRecoverySection()
        {
            var section = new VisualElement { name = RecoverySectionName };
            section.AddToClassList("psd-hierarchy-chat-recovery");
            section.Add(new Label("计划恢复"));
            recoverySummary = new Label();
            section.Add(recoverySummary);
            regeneratePlanButton = new Button(RequestRecoveryPlan)
            {
                name = RegeneratePlanButtonName,
                text = "基于当前快照重新生成",
                tooltip = "刷新当前 Prefab 快照，并基于失败原因请求一份新的待确认计划。",
            };
            section.Add(regeneratePlanButton);
            UpdateRecoveryUi(canStartRequest: !isSending);
            return section;
        }

        private void UpdateRecoveryUi(bool canStartRequest)
        {
            bool hasRecovery = !string.IsNullOrWhiteSpace(pendingRecoveryFailure);
            if (recoverySummary != null)
            {
                recoverySummary.text = hasRecovery
                    ? "上次计划未修改 Prefab。恢复请求会重新读取当前层级和资源证据。"
                    : "当前没有需要恢复的失败计划。";
            }

            if (regeneratePlanButton != null)
            {
                regeneratePlanButton.SetEnabled(hasRecovery && canStartRequest);
            }
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

        private PsdHierarchyPlanWorkspaceActions CreateWorkspaceActions()
        {
            return new PsdHierarchyPlanWorkspaceActions
            {
                keepOriginalStructure = KeepOriginalStructure,
                locateNodes = LocateWorkspaceNodes,
                retryAnalysis = RequestRecoveryPlan,
                exportDiagnostics = ExportWorkspaceDiagnostics,
            };
        }

        private void KeepOriginalStructure(string issueId)
        {
            if (currentWorkspace == null || !currentWorkspace.TryKeepOriginalStructure(issueId))
            {
                return;
            }

            SetPlanWorkspace(currentWorkspace);
            PersistWorkspaceDiagnostics(currentWorkspace);
            AppendMessage("system", "已记录“保持原结构”。该问题不会产生可执行变更。");
        }

        private void LocateWorkspaceNodes(string[] nodeIds)
        {
            if (context == null || nodeIds == null || nodeIds.Length == 0)
            {
                return;
            }

            string[] paths = nodeIds
                .Select(nodeId => context.TryGetNodePath(nodeId, out string path) ? path : string.Empty)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            PingAsset(context.targetPrefabAssetPath);
            AppendMessage(
                "system",
                paths.Length > 0
                    ? "受影响节点：\n" + string.Join("\n", paths)
                    : "当前快照中未找到这些节点；请刷新快照后重试。");
        }

        private void ExportWorkspaceDiagnostics()
        {
            if (context == null || currentWorkspace == null)
            {
                return;
            }

            PsdHierarchyPlanWorkspaceStoreResult result =
                PsdHierarchyPlanWorkspaceStore.TrySave(context.projectRoot, currentWorkspace);
            if (!result.success)
            {
                AppendMessage("system", "导出诊断失败：" + result.error);
                return;
            }

            AppendMessage("system", "诊断已导出：" + result.path);
            EditorUtility.RevealInFinder(result.path);
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

            SetPlanWorkspace(null);
            if (!TryRefreshContextBeforeNewRequest(out string refreshError))
            {
                HandleCompletedPlanFailure(
                    string.Empty,
                    string.Empty,
                    PsdHierarchyPlanIssueCategory.Infrastructure,
                    "发送前无法刷新当前 Prefab 权威快照：" + refreshError);
                return;
            }

            SendMessage(PsdHierarchyChatClient.ResolveUserPrompt(prompt), false);
        }

        private bool TryRefreshContextBeforeNewRequest(out string error)
        {
            PsdHierarchyChatContext previousContext = context;
            if (!PsdHierarchyChatContextBuilder.TryCreate(
                    previousContext.sourcePsdAssetPath,
                    previousContext.targetPrefabAssetPath,
                    out PsdHierarchyChatContext refreshedContext,
                    out error))
            {
                return false;
            }

            context = refreshedContext;
            if (HasAuthoritativeAnalysisChanged(previousContext, refreshedContext))
            {
                ResetFailedPlanConversation();
                AppendMessage(
                    "system",
                    BuildAuthoritativeAnalysisRefreshNotice(previousContext, refreshedContext));
            }

            error = string.Empty;
            return true;
        }

        private void OpenLocalRepairWindow()
        {
            if (context == null)
            {
                return;
            }

            PsdHierarchyLocalRepairWindow.Open(context);
        }

        internal static bool HasAuthoritativeAnalysisChanged(
            PsdHierarchyChatContext previousContext,
            PsdHierarchyChatContext refreshedContext)
        {
            if (previousContext == null || refreshedContext == null)
            {
                return true;
            }

            return !string.Equals(
                       previousContext.hierarchySnapshotJson,
                       refreshedContext.hierarchySnapshotJson,
                       StringComparison.Ordinal) ||
                   !string.Equals(
                       previousContext.skillContent,
                       refreshedContext.skillContent,
                       StringComparison.Ordinal) ||
                   !string.Equals(
                       previousContext.planFormatContent,
                       refreshedContext.planFormatContent,
                       StringComparison.Ordinal);
        }

        private static string BuildAuthoritativeAnalysisRefreshNotice(
            PsdHierarchyChatContext previousContext,
            PsdHierarchyChatContext refreshedContext)
        {
            string oldFingerprint = string.IsNullOrWhiteSpace(previousContext.hierarchySnapshotFingerprint)
                ? "<empty>"
                : previousContext.hierarchySnapshotFingerprint;
            string newFingerprint = string.IsNullOrWhiteSpace(refreshedContext.hierarchySnapshotFingerprint)
                ? "<empty>"
                : refreshedContext.hierarchySnapshotFingerprint;
            int oldCandidateCount = previousContext.componentFamilyCandidates?.Count ?? 0;
            int newCandidateCount = refreshedContext.componentFamilyCandidates?.Count ?? 0;
            string reason = string.Equals(oldFingerprint, newFingerprint, StringComparison.Ordinal)
                ? "Prefab 文件指纹相同，但候选分析结果或整理规则已变化；旧候选不会继续参与本轮计划。"
                : "Prefab 文件内容已变化；旧节点引用和旧候选不会继续参与本轮计划。";
            return "检测到发送前的权威分析内容已变化，已刷新当前 Prefab 快照并清空旧 AI 会话。" +
                   "旧快照 fingerprint=" + oldFingerprint + "，候选数=" + oldCandidateCount +
                   "；新快照 fingerprint=" + newFingerprint + "，候选数=" + newCandidateCount + "。" +
                   reason;
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
                HandleCompletedPlanFailure(
                    string.Empty,
                    string.Empty,
                    PsdHierarchyPlanIssueCategory.Infrastructure,
                    configurationError);
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
                PsdHierarchyPlanIssueCategory lastFailureCategory =
                    PsdHierarchyPlanIssueCategory.PlanExtraction;
                int maxRepairAttempts = MaxAutomaticPlanRepairAttempts;
                for (int repairAttempt = 0; repairAttempt <= maxRepairAttempts; repairAttempt++)
                {
                    PsdHierarchyChatSendResult result = await PsdHierarchyChatClient.SendWithCliSessionAsync(
                        context,
                        connection,
                        conversation,
                        cliSessionId);
                    if (!result.success)
                    {
                        HideThinkingIndicator();
                        string prefix = repairAttempt == 0 ? string.Empty : "AI 自动补全失败：";
                        HandleCompletedPlanFailure(
                            initialReviewText,
                            lastAssistantReply,
                            PsdHierarchyPlanIssueCategory.Infrastructure,
                            prefix + result.message);
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

                    bool hasPlanPayload = !string.IsNullOrWhiteSpace(
                        PsdHierarchyChatCleanupExecution.ExtractJsonCodeBlock(result.message));
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
                            string reviewableReply = PsdHierarchyChatCleanupExecution.ComposeReviewableReply(
                                initialReviewText,
                                planJson);
                            AppendMessage("assistant", reviewableReply);
                            SetPlanWorkspace(PsdHierarchyChatCleanupExecution.CreateReadyWorkspace(
                                context,
                                initialReviewText,
                                result.message,
                                planJson));
                            AppendMessage("system", "方案已就绪。点击“确认并更新”或回复“确认”即可直接更新当前 Prefab；确认不会再发送给 AI。 ");
                            SetSending(false, "方案待确认");
                            return;
                        }

                        lastPlanError = validation.message;
                        lastFailureCategory = PsdHierarchyPlanIssueCategory.RunnerPreflight;
                        if (IsNonRepairableValidationFailure(validation.message))
                        {
                            break;
                        }
                    }
                    else
                    {
                        lastPlanError = planError;
                        lastFailureCategory = hasPlanPayload
                            ? PsdHierarchyPlanIssueCategory.PlanPreparation
                            : PsdHierarchyPlanIssueCategory.PlanExtraction;
                    }

                    if (repairAttempt >= maxRepairAttempts)
                    {
                        break;
                    }

                    int nextAttempt = repairAttempt + 1;
                    string repairPrompt = PsdHierarchyChatClient.BuildJsonOnlyPlanRepairPrompt(lastPlanError, context);
                    conversation.Add(new PsdHierarchyChatMessage("user", repairPrompt));
                    if (TryResetCliSessionForAutomaticPlanRepair(
                            connection.connectionMode,
                            ref cliSessionId))
                    {
                        RefreshConnectionUi();
                    }
                    ShowThinkingIndicator(
                        "AI 返回的计划未通过校验，正在同一会话自动补全（" + nextAttempt + "/" +
                        maxRepairAttempts + "）...");
                    SetSending(
                        true,
                        "正在自动补全计划（" + nextAttempt + "/" + maxRepairAttempts + "）...");
                }

                HideThinkingIndicator();
                HandleCompletedPlanFailure(
                    initialReviewText,
                    lastAssistantReply,
                    lastFailureCategory,
                    lastPlanError);
            }
            catch (Exception exception)
            {
                HideThinkingIndicator();
                HandleCompletedPlanFailure(
                    string.Empty,
                    string.Empty,
                    PsdHierarchyPlanIssueCategory.Infrastructure,
                    "AI 对话发生异常：" + exception.Message);
            }
            finally
            {
                HideThinkingIndicator();
                isSending = false;
            }
        }

        private async void ApplyPendingPlan(string confirmation, bool appendUserConfirmation = true)
        {
            if (context == null || isSending || string.IsNullOrWhiteSpace(pendingPlanJson))
            {
                return;
            }

            string planToApply = pendingPlanJson;
            SetPendingPlan(string.Empty);
            isSending = true;
            if (appendUserConfirmation)
            {
                AppendMessage("user", confirmation.Trim());
            }
            if (draftField != null)
            {
                draftField.value = string.Empty;
            }

            ShowThinkingIndicator("正在校验已确认方案，并通过 Unity Editor API 更新 Prefab...");
            SetSending(true, "正在更新 Prefab...");
            try
            {
                PsdHierarchyChatCleanupExecutionResult result =
                    await PsdHierarchyChatCleanupExecution.ApplyConfirmedAsync(
                        context,
                        planToApply,
                        ShouldReplaceReplayProfile(
                            hasAppliedCleanupStage,
                            requiresReplayProfileReplacement));
                HideThinkingIndicator();
                AppendMessage("system", result.message);
                bool verificationWarning = result.success &&
                    result.message.IndexOf("VERIFY_WARN", StringComparison.OrdinalIgnoreCase) >= 0;
                if (result.success && !verificationWarning)
                {
                    hasAppliedCleanupStage = true;
                    requiresReplayProfileReplacement = false;
                    pendingRecoveryFailure = string.Empty;
                    SetPlanWorkspace(null);
                }
                if (verificationWarning)
                {
                    HandleApplyFailure(planToApply, result.message, infrastructureFailure: false);
                }
                else if (!result.success)
                {
                    bool requiresProfileReplacement =
                        PsdHierarchyCleanupReplayCoordinator.IsPermanentReplayFailure(result.message);
                    requiresReplayProfileReplacement |= requiresProfileReplacement;
                    if (requiresProfileReplacement)
                    {
                        string sourceGuid = AssetDatabase.AssetPathToGUID(context.sourcePsdAssetPath);
                        PsdHierarchyCleanupReplayProfile.TryMarkRequiresRebindByGuid(
                            sourceGuid,
                            context.targetPrefabAssetPath,
                            result.message);
                    }

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

                    HandleApplyFailure(
                        planToApply,
                        result.message,
                        infrastructureFailure: false);
                }
                if (result.success && !verificationWarning)
                {
                    SetSending(false, "更新完成");
                }
            }
            catch (Exception exception)
            {
                HideThinkingIndicator();
                HandleApplyFailure(
                    planToApply,
                    "更新 Prefab 时发生异常：" + exception.Message,
                    infrastructureFailure: true);
            }
            finally
            {
                HideThinkingIndicator();
                isSending = false;
            }

            // 分组后抽取由共享执行核心在首阶段保存并核验后原生完成（见
            // PsdHierarchyNativeCleanupExecutor.ExecuteV2 的第二阶段），因此这里不再排队
            // AI 子 Prefab 阶段：重复执行会试图抽取已经变成实例的行。
        }

        internal void HandleCompletedPlanFailure(
            string reviewText,
            string rawAssistantReply,
            PsdHierarchyPlanIssueCategory category,
            string error)
        {
            PsdHierarchyPlanWorkspace workspace =
                PsdHierarchyChatCleanupExecution.CreateIssueWorkspace(
                    context,
                    reviewText,
                    rawAssistantReply,
                    category,
                    error);
            SetPlanWorkspace(workspace);
            pendingRecoveryFailure = string.IsNullOrWhiteSpace(error)
                ? "The previous plan could not be prepared."
                : error.Trim();
            PersistWorkspaceDiagnostics(workspace);

            if (!string.IsNullOrWhiteSpace(reviewText))
            {
                AppendMessage("assistant", reviewText);
            }

            AppendMessage(
                "system",
                category == PsdHierarchyPlanIssueCategory.Infrastructure
                    ? "当前环境阻止了分析或校验。详情、重试和诊断导出仍保留在“待处理问题”中；本轮未修改 Prefab。"
                    : "分析已完成，但当前没有可安全执行的变更。请在“待处理问题”中处理；本轮未修改 Prefab。");
            UpdateRecoveryUi(canStartRequest: true);
            SetSending(
                false,
                category == PsdHierarchyPlanIssueCategory.Infrastructure
                    ? "环境阻塞"
                    : "存在待处理问题");
        }

        internal void HandleApplyFailure(
            string planJson,
            string error,
            bool infrastructureFailure)
        {
            PsdHierarchyPlanIssueCategory category = infrastructureFailure
                ? PsdHierarchyPlanIssueCategory.Infrastructure
                : PsdHierarchyPlanIssueCategory.RunnerPreflight;
            PsdHierarchyPlanWorkspace workspace =
                PsdHierarchyChatCleanupExecution.CreateIssueWorkspace(
                    context,
                    currentWorkspace?.reviewText,
                    currentWorkspace?.rawAssistantReply,
                    category,
                    error,
                    planJson);
            SetPlanWorkspace(workspace);
            pendingRecoveryFailure = string.IsNullOrWhiteSpace(error)
                ? "The confirmed plan could not be applied."
                : error.Trim();
            PersistWorkspaceDiagnostics(workspace);
            AppendMessage(
                "system",
                "原计划已保留为禁用批次，不会自动重试。请在“待处理问题”中查看详情或重新分析。");
            UpdateRecoveryUi(canStartRequest: true);
            SetSending(false, infrastructureFailure ? "环境阻塞" : "存在待处理问题");
        }

        private void PersistWorkspaceDiagnostics(PsdHierarchyPlanWorkspace workspace)
        {
            if (context == null || workspace == null)
            {
                return;
            }

            PsdHierarchyPlanWorkspaceStoreResult result =
                PsdHierarchyPlanWorkspaceStore.TrySave(context.projectRoot, workspace);
            if (!result.success)
            {
                AppendMessage("system", "自动保存计划诊断失败：" + result.error);
            }
        }

        internal static bool TryResetCliSessionForAutomaticPlanRepair(
            PsdHierarchyAiConnectionMode connectionMode,
            ref string cliSessionId)
        {
            // JSON-only repair stays in the same provider session so the skill,
            // plan format, and snapshot are not read again from scratch.
            return false;
        }

        private void RequestRecoveryPlan()
        {
            if (context == null || isSending || string.IsNullOrWhiteSpace(pendingRecoveryFailure))
            {
                return;
            }

            if (!TryRefreshContextBeforeNewRequest(out string error))
            {
                AppendMessage("system", "更新失败后无法刷新当前 Prefab 快照：" + error);
                return;
            }

            ResetFailedPlanConversation();
            AppendMessage("system", "已刷新当前 Prefab 快照，正在基于失败原因生成新的待确认计划。");
            SendMessage(
                PsdHierarchyChatClient.BuildJsonOnlyPlanRecoveryPrompt(
                    pendingRecoveryFailure,
                    context),
                false,
                false);
        }

        internal static bool ShouldReplaceReplayProfile(bool hasAppliedCleanupStage)
        {
            return ShouldReplaceReplayProfile(
                hasAppliedCleanupStage,
                requiresReplayProfileReplacement: false);
        }

        internal static bool ShouldReplaceReplayProfile(
            bool hasAppliedCleanupStage,
            bool requiresReplayProfileReplacement)
        {
            return !hasAppliedCleanupStage || requiresReplayProfileReplacement;
        }

        /// <summary>这些失败重试一次也不会变好：超时、配额/长度超限与只读核验告警。</summary>
        internal static bool IsNonRepairableValidationFailure(string message)
        {
            string value = message ?? string.Empty;
            return value.IndexOf("超时", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("exceeded", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("VERIFY_WARN", StringComparison.OrdinalIgnoreCase) >= 0;
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
            UpdateRecoveryUi(canStartRequest: !sending);
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

        private void SetPlanWorkspace(PsdHierarchyPlanWorkspace workspace)
        {
            currentWorkspace = workspace;
            if (workspace != null && workspace.TryGetEnabledPlan(out string planJson))
            {
                SetPendingPlan(planJson);
            }
            else
            {
                SetPendingPlan(string.Empty);
            }

            workspaceView?.Bind(workspace, CreateWorkspaceActions());
        }

        private void ResetFailedPlanConversation()
        {
            SetPlanWorkspace(null);
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
                    error = "全局配置选择的 " + PsdHierarchyChatClient.GetProviderDisplayName(settings.provider) +
                        " CLI 当前不可用，请打开全局配置重新选择。";
                    return false;
                }

                // 本地 CLI 下模型与思考程度留空就表示不传对应参数、使用 CLI 自身配置，
                // 所以这里传原始值，而不是会补上 provider 默认模型的 ResolveModel()。
                connection = new PsdHierarchyChatConnection(
                    settings.provider,
                    settings.connectionMode,
                    cli.executablePath,
                    string.Empty,
                    settings.customModel,
                    string.Empty,
                    settings.ResolveReasoningEffort());
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
                    apiKey,
                    settings.ResolveReasoningEffort());
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
            string model = settings.connectionMode == PsdHierarchyAiConnectionMode.CustomApi
                ? settings.ResolveModel()
                : BuildLocalCliModelSummary(settings);
            return "Agent：" + PsdHierarchyChatClient.GetProviderDisplayName(settings.provider) +
                "    模型：" + model;
        }

        /// <summary>本地 CLI 下把「没填就用 CLI 默认」如实显示出来，而不是补一个 provider 默认模型。</summary>
        private static string BuildLocalCliModelSummary(PsdHierarchyAiSettingsSnapshot settings)
        {
            string model = (settings.customModel ?? string.Empty).Trim();
            string effort = settings.ResolveReasoningEffort();
            if (string.IsNullOrEmpty(model) && string.IsNullOrEmpty(effort))
            {
                return "CLI 默认";
            }

            if (string.IsNullOrEmpty(model))
            {
                return "CLI 默认 · 思考 " + effort;
            }

            return string.IsNullOrEmpty(effort) ? model : model + " · 思考 " + effort;
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
