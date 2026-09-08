namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.UIElements;

    internal sealed class PsdHierarchyLocalRepairWindow : EditorWindow
    {
        internal const string RootElementName = "psd-local-repair-root";
        internal const string TargetLabelName = "psd-local-repair-target";
        internal const string SelectionSummaryName = "psd-local-repair-selection";
        internal const string LockSelectionButtonName = "psd-local-repair-lock-selection";
        internal const string NestedPrefabNameFieldName = "psd-local-repair-prefab-name";
        internal const string AnalyzeButtonName = "psd-local-repair-analyze";
        internal const string ReviewName = "psd-local-repair-review";
        internal const string ConfirmButtonName = "psd-local-repair-confirm";
        internal const string StatusName = "psd-local-repair-status";
        internal const string ContinueAnalysisButtonName = "psd-local-repair-continue-analysis";

        private const string StyleSheetGuid = "18f53073502d4d7e89345f900b727c7e";

        private PsdHierarchyChatContext context;
        private PsdHierarchyLocalRepairScope scope;
        private string pendingPlanJson = string.Empty;
        private TextField prefabNameField;
        private Label selectionSummary;
        private Label reviewLabel;
        private Label statusLabel;
        private Button lockSelectionButton;
        private Button analyzeButton;
        private Button confirmButton;
        private Button continueAnalysisButton;
        private string[] lockedSelectionPaths = Array.Empty<string>();

        // 名称查找功能字段
        private VisualElement selectionModePanel;
        private VisualElement sceneSelectionPanel;
        private VisualElement nameSearchPanel;
        private TextField nameSearchField;
        private Button nameSearchButton;
        private Toggle fuzzySearchToggle;
        private ListView nameSearchResults;
        private List<GameObject> foundNodes = new List<GameObject>();

        // 冲突检测相关字段
        private List<LocalRepairConflict> detectedConflicts = new List<LocalRepairConflict>();
        private VisualElement conflictPanel;
        private ScrollView conflictScrollView;
        private Label conflictSummaryLabel;

        internal static void Open(PsdHierarchyChatContext chatContext)
        {
            var window = GetWindow<PsdHierarchyLocalRepairWindow>();
            window.titleContent = new GUIContent("局部整理");
            window.minSize = new Vector2(500f, 460f);
            window.Show();
            window.Initialize(chatContext);
        }

        internal static bool TryOpen(
            string sourcePsdAssetPath,
            string targetPrefabAssetPath,
            out string error)
        {
            if (!PsdHierarchyChatContextBuilder.TryCreate(
                    sourcePsdAssetPath,
                    targetPrefabAssetPath,
                    out PsdHierarchyChatContext chatContext,
                    out error))
            {
                return false;
            }

            Open(chatContext);
            error = string.Empty;
            return true;
        }

        public void CreateGUI()
        {
            RebuildUi();
        }

        internal void InitializeForTests(PsdHierarchyChatContext chatContext)
        {
            Initialize(chatContext);
        }

        private void Initialize(PsdHierarchyChatContext chatContext)
        {
            context = chatContext ?? throw new ArgumentNullException(nameof(chatContext));
            scope = null;
            pendingPlanJson = string.Empty;
            lockedSelectionPaths = Array.Empty<string>();
            RebuildUi();
        }

        private void RebuildUi()
        {
            rootVisualElement.Clear();
            string styleSheetPath = AssetDatabase.GUIDToAssetPath(StyleSheetGuid);
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(styleSheetPath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            var root = new VisualElement { name = RootElementName };
            root.AddToClassList("psd-hierarchy-chat");
            root.AddToClassList("psd-hierarchy-local-repair");
            root.style.flexGrow = 1f;
            root.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(root);

            var header = new VisualElement();
            header.AddToClassList("psd-hierarchy-chat-header");
            var title = new Label("局部整理") { name = "psd-local-repair-title" };
            title.AddToClassList("psd-hierarchy-chat-title");
            header.Add(title);
            header.Add(new Label("只整理当前 Prefab Stage 选中的节点；确认前不会修改 Prefab。"));
            var target = new Label(context == null ? string.Empty : context.targetPrefabAssetPath)
            {
                name = TargetLabelName,
            };
            target.AddToClassList("psd-hierarchy-chat-target");
            header.Add(target);
            root.Add(header);

            // 选择模式面板
            selectionModePanel = new VisualElement();
            selectionModePanel.AddToClassList("psd-local-repair-panel");
            selectionModePanel.Add(new Label("选择模式"));

            var modeContainer = new VisualElement();
            modeContainer.style.flexDirection = FlexDirection.Row;

            var sceneRadio = new RadioButton("场景选中");
            sceneRadio.value = true;
            var nameRadio = new RadioButton("名称查找");

            // 使用简单的事件监听实现互斥
            sceneRadio.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    nameRadio.value = false;
                    OnSelectionModeChanged(true);
                }
            });
            nameRadio.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    sceneRadio.value = false;
                    OnSelectionModeChanged(false);
                }
            });

            modeContainer.Add(sceneRadio);
            modeContainer.Add(nameRadio);
            selectionModePanel.Add(modeContainer);
            root.Add(selectionModePanel);

            // 场景选择面板
            sceneSelectionPanel = new VisualElement();
            sceneSelectionPanel.AddToClassList("psd-local-repair-panel");
            sceneSelectionPanel.Add(new Label("当前选区"));
            selectionSummary = new Label("尚未锁定选区") { name = SelectionSummaryName };
            selectionSummary.style.whiteSpace = WhiteSpace.Normal;
            sceneSelectionPanel.Add(selectionSummary);
            lockSelectionButton = new Button(LockCurrentSelection)
            {
                name = LockSelectionButtonName,
                text = "锁定当前选区",
                tooltip = "读取当前 Prefab Stage 的 Unity Selection，并将其作为本次局部整理范围。",
            };
            sceneSelectionPanel.Add(lockSelectionButton);
            root.Add(sceneSelectionPanel);

            // 名称查找面板
            nameSearchPanel = new VisualElement();
            nameSearchPanel.AddToClassList("psd-local-repair-panel");
            nameSearchPanel.style.display = DisplayStyle.None; // 默认隐藏
            nameSearchPanel.Add(new Label("名称查找"));

            nameSearchField = new TextField("节点名称")
            {
                value = "",
                tooltip = "输入要查找的节点名称，支持模糊匹配"
            };
            nameSearchPanel.Add(nameSearchField);

            var searchButtonRow = new VisualElement();
            searchButtonRow.style.flexDirection = FlexDirection.Row;

            nameSearchButton = new Button(SearchNodesByName)
            {
                text = "查找",
                tooltip = "在当前 Prefab 中查找匹配的节点"
            };
            searchButtonRow.Add(nameSearchButton);

            fuzzySearchToggle = new Toggle("模糊匹配") { value = true };
            searchButtonRow.Add(fuzzySearchToggle);

            nameSearchPanel.Add(searchButtonRow);

            // 搜索结果列表
            nameSearchResults = new ListView
            {
                style = { minHeight = 100, maxHeight = 200, flexGrow = 1 }
            };
            nameSearchResults.makeItem = () => new Label();
            nameSearchResults.bindItem = (element, index) =>
            {
                var label = element as Label;
                if (label != null && index < foundNodes.Count)
                {
                    GameObject node = foundNodes[index];
                    label.text = GetGameObjectPath(node);
                }
            };
            nameSearchResults.selectionChanged += OnSearchResultSelected;
            nameSearchPanel.Add(nameSearchResults);

            root.Add(nameSearchPanel);

            var planPanel = new VisualElement();
            planPanel.AddToClassList("psd-local-repair-panel");
            prefabNameField = new TextField("组件 Prefab 名称")
            {
                name = NestedPrefabNameFieldName,
                value = "DaySignRewardItem",
                tooltip = "使用 PascalCase 名称；组件会保存到目标 Prefab 同级的 Common 目录。",
            };
            planPanel.Add(prefabNameField);
            analyzeButton = new Button(AnalyzeLocalRepair)
            {
                name = AnalyzeButtonName,
                text = "分析局部整理",
                tooltip = "基于锁定选区生成可确认的组件化方案，不会写入 Prefab。",
            };
            planPanel.Add(analyzeButton);

            continueAnalysisButton = new Button(ContinueAnalysisAfterConflicts)
            {
                name = ContinueAnalysisButtonName,
                text = "继续分析（应用冲突策略）",
                tooltip = "按当前选择的冲突解决策略继续生成方案；选择\"跳过\"的节点会被排除。",
            };
            continueAnalysisButton.style.display = DisplayStyle.None;
            planPanel.Add(continueAnalysisButton);
            root.Add(planPanel);

            var reviewPanel = new VisualElement();
            reviewPanel.AddToClassList("psd-local-repair-panel");
            reviewPanel.Add(new Label("整理方案"));
            var reviewScroll = new ScrollView { name = ReviewName };
            reviewScroll.style.flexGrow = 1f;
            reviewScroll.style.minHeight = 150f;
            reviewLabel = new Label("点击\"分析局部整理\"后，这里会显示组件模板、匹配实例和未匹配节点。")
            {
                name = "psd-local-repair-review-content",
            };
            reviewLabel.style.whiteSpace = WhiteSpace.Normal;
            reviewScroll.Add(reviewLabel);
            reviewPanel.Add(reviewScroll);
            root.Add(reviewPanel);

            var footer = new VisualElement();
            footer.AddToClassList("psd-hierarchy-chat-footer");
            statusLabel = new Label("等待分析") { name = StatusName };
            statusLabel.AddToClassList("psd-hierarchy-chat-status");
            footer.Add(statusLabel);
            confirmButton = new Button(ConfirmPlan)
            {
                name = ConfirmButtonName,
                text = "确认并更新",
                tooltip = "确认当前方案后，才创建组件 Prefab 并更新目标 Prefab。",
            };
            confirmButton.SetEnabled(false);
            footer.Add(confirmButton);
            root.Add(footer);
        }

        private void LockCurrentSelection()
        {
            scope = null;
            lockedSelectionPaths = Array.Empty<string>();
            pendingPlanJson = string.Empty;
            confirmButton?.SetEnabled(false);
            HideConflictPanel();
            ShowContinueAnalysisButton(false);
            if (!TryRefreshContext(out string refreshError))
            {
                SetStatus(refreshError);
                return;
            }

            // 选区验证
            if (!ValidateSelection(out string validationError))
            {
                SetStatus(validationError);
                EditorUtility.DisplayDialog("选区验证失败", validationError, "确定");
                return;
            }

            if (!PsdHierarchyLocalRepairScope.TryCaptureCurrentSelection(
                    context,
                    PsdHierarchyLocalRepairScopeMode.SelectedNodes,
                    out PsdHierarchyLocalRepairScope capturedScope,
                    out string error))
            {
                SetStatus(error);
                return;
            }

            scope = capturedScope;
            lockedSelectionPaths = capturedScope.selectedPaths;
            context.localRepairScope = scope;
            reviewLabel.text = "选区已锁定，点击\"分析局部整理\"生成方案。";
            selectionSummary.text = string.Join(Environment.NewLine, lockedSelectionPaths);
            SetStatus("选区已锁定");
        }

        private async void AnalyzeLocalRepair()
        {
            if (!TryRefreshContext(out string refreshError))
            {
                SetStatus(refreshError);
                return;
            }

            if (lockedSelectionPaths.Length == 0)
            {
                if (!PsdHierarchyLocalRepairScope.TryCaptureCurrentSelection(
                        context,
                        PsdHierarchyLocalRepairScopeMode.SelectedNodes,
                        out scope,
                        out string selectionError))
                {
                    SetStatus(selectionError);
                    return;
                }

                lockedSelectionPaths = scope.selectedPaths;
            }
            else if (!PsdHierarchyLocalRepairScope.TryCreateFromSelectedPaths(
                         context,
                         lockedSelectionPaths,
                         PsdHierarchyLocalRepairScopeMode.SelectedNodes,
                         out scope,
                         out string rebindError))
            {
                SetStatus(rebindError);
                return;
            }

            context.localRepairScope = scope;
            selectionSummary.text = string.Join(Environment.NewLine, lockedSelectionPaths);

            // 冲突检测：先停下，让用户选择解决策略
            HideConflictPanel();
            ShowContinueAnalysisButton(false);

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            detectedConflicts = stage != null
                ? PsdHierarchyLocalRepairConflictDetector.DetectConflicts(scope, stage.prefabContentsRoot)
                : new List<LocalRepairConflict>();

            if (detectedConflicts.Count > 0)
            {
                ShowConflictPanel(detectedConflicts);
                ShowContinueAnalysisButton(true);
                pendingPlanJson = string.Empty;
                confirmButton?.SetEnabled(false);
                SetStatus($"检测到 {detectedConflicts.Count} 个冲突，请选择解决策略后继续");
                return;
            }

            await BuildPlanAsync();
        }

        private async Task BuildPlanAsync()
        {
            analyzeButton?.SetEnabled(false);
            SetStatus("正在分析局部整理（包括视觉相似度）...");

            try
            {
                var result = await PsdHierarchyChatCleanupExecution.TryBuildLocalPrefabOrganizationPlanWithVisualAsync(
                    context,
                    scope,
                    prefabNameField?.value);

                if (!result.success)
                {
                    pendingPlanJson = string.Empty;
                    confirmButton?.SetEnabled(false);
                    SetStatus(result.error);
                    reviewLabel.text = result.error;
                    return;
                }

                pendingPlanJson = result.planJson;
                reviewLabel.text = result.review + Environment.NewLine + Environment.NewLine +
                                   BuildPlanDetailsWithVisualScores(result.planJson, result.visualAnalysis);
                confirmButton?.SetEnabled(true);
                SetStatus("方案待确认");
            }
            catch (Exception exception)
            {
                pendingPlanJson = string.Empty;
                confirmButton?.SetEnabled(false);
                SetStatus("分析失败：" + exception.Message);
            }
            finally
            {
                analyzeButton?.SetEnabled(true);
            }
        }

        private async void ContinueAnalysisAfterConflicts()
        {
            if (!ApplyConflictSkipStrategy())
            {
                confirmButton?.SetEnabled(false);
                return;
            }

            ShowContinueAnalysisButton(false);
            await BuildPlanAsync();
        }

        private bool ApplyConflictSkipStrategy()
        {
            if (context == null || scope == null)
            {
                SetStatus("缺少当前整理目标。");
                return false;
            }

            var skippedPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (LocalRepairConflict conflict in detectedConflicts)
            {
                if (conflict.SelectedStrategy != ConflictResolutionStrategy.Skip)
                {
                    continue;
                }

                foreach (string nodePath in conflict.NodePaths ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(nodePath))
                    {
                        skippedPaths.Add(nodePath);
                    }
                }
            }

            if (skippedPaths.Count == 0)
            {
                return true;
            }

            string[] remainingPaths = lockedSelectionPaths
                .Where(path => !skippedPaths.Contains(path))
                .ToArray();
            if (remainingPaths.Length == 0)
            {
                SetStatus("所有节点都被跳过，无法生成方案。");
                reviewLabel.text = "所有节点都被跳过，请调整冲突策略后重新分析。";
                return false;
            }

            if (!PsdHierarchyLocalRepairScope.TryCreateFromSelectedPaths(
                    context,
                    remainingPaths,
                    PsdHierarchyLocalRepairScopeMode.SelectedNodes,
                    out scope,
                    out string rebindError))
            {
                SetStatus(rebindError);
                return false;
            }

            lockedSelectionPaths = remainingPaths;
            context.localRepairScope = scope;
            selectionSummary.text = string.Join(Environment.NewLine, lockedSelectionPaths);
            return true;
        }

        private void ShowContinueAnalysisButton(bool visible)
        {
            if (continueAnalysisButton == null)
            {
                return;
            }

            continueAnalysisButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }


        private async void ConfirmPlan()
        {
            if (context == null || scope == null || string.IsNullOrWhiteSpace(pendingPlanJson))
            {
                return;
            }

            confirmButton?.SetEnabled(false);
            analyzeButton?.SetEnabled(false);
            lockSelectionButton?.SetEnabled(false);
            SetStatus("正在确认并更新 Prefab...");

            string backupPath = string.Empty;
            bool restoredFromBackup = false;

            try
            {
                // 1. 创建备份
                backupPath = PsdHierarchyLocalRepairBackup.CreateBackup(
                    context.targetPrefabAssetPath,
                    $"局部整理备份 - {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                if (string.IsNullOrEmpty(backupPath))
                {
                    bool continueWithoutBackup = EditorUtility.DisplayDialog(
                        "警告",
                        "无法创建备份。是否继续执行局部整理？\n（不建议在无备份的情况下继续）",
                        "继续",
                        "取消");

                    if (!continueWithoutBackup)
                    {
                        SetStatus("已取消：备份失败");
                        confirmButton?.SetEnabled(true);
                        analyzeButton?.SetEnabled(true);
                        lockSelectionButton?.SetEnabled(true);
                        return;
                    }
                }

                // 2. 清理旧备份（保留 7 天内）
                PsdHierarchyLocalRepairBackup.CleanOldBackups(7);

                // 3. 应用方案。Native 后端通过 PrefabUtility.SaveAsPrefabAsset 写资产，
                //    不经 Unity Undo 系统，因此不注册 Undo；可回滚性由备份 + 事务保存 + 重放 Profile 兜底。
                PsdHierarchyChatCleanupExecutionResult result =
                    await PsdHierarchyChatCleanupExecution.ApplyConfirmedAsync(
                        context,
                        pendingPlanJson,
                        replaceReplayProfile: false);

                if (result.success)
                {
                    pendingPlanJson = string.Empty;
                    SetStatus("已确认并更新");
                    reviewLabel.text += Environment.NewLine + Environment.NewLine + result.message;
                }
                else
                {
                    SetStatus(result.message);
                    restoredFromBackup = OfferRestoreAfterFailure(backupPath, result.message);
                    if (!restoredFromBackup)
                    {
                        confirmButton?.SetEnabled(true);
                    }
                }
            }
            catch (Exception exception)
            {
                SetStatus("局部整理更新失败：" + exception.Message);
                restoredFromBackup = OfferRestoreAfterFailure(backupPath, exception.Message);
                if (!restoredFromBackup)
                {
                    confirmButton?.SetEnabled(true);
                }
            }
            finally
            {
                if (!restoredFromBackup)
                {
                    analyzeButton?.SetEnabled(true);
                    lockSelectionButton?.SetEnabled(true);
                }
            }
        }

        private bool OfferRestoreAfterFailure(string backupPath, string failureMessage)
        {
            if (string.IsNullOrEmpty(backupPath))
            {
                return false;
            }

            bool shouldRestore = EditorUtility.DisplayDialog(
                "执行失败",
                $"局部整理执行失败：{failureMessage}\n\n是否恢复到备份？",
                "恢复备份",
                "保持现状");
            if (!shouldRestore)
            {
                return false;
            }

            if (PsdHierarchyLocalRepairBackup.RestoreBackup(backupPath))
            {
                SetStatus("已从备份恢复，请关闭并重新打开 Prefab Stage 后继续。");
                DisableAllActions();
                return true;
            }

            SetStatus("备份恢复失败");
            confirmButton?.SetEnabled(true);
            return false;
        }

        private void DisableAllActions()
        {
            confirmButton?.SetEnabled(false);
            analyzeButton?.SetEnabled(false);
            lockSelectionButton?.SetEnabled(false);
        }


        private bool TryRefreshContext(out string error)
        {
            error = string.Empty;
            if (context == null)
            {
                error = "缺少当前 Prefab 整理目标。";
                return false;
            }

            if (!PsdHierarchyChatContextBuilder.TryCreate(
                    context.sourcePsdAssetPath,
                    context.targetPrefabAssetPath,
                    out PsdHierarchyChatContext refreshedContext,
                    out error))
            {
                return false;
            }

            context = refreshedContext;
            if (lockedSelectionPaths.Length > 0 &&
                !PsdHierarchyLocalRepairScope.TryCreateFromSelectedPaths(
                    context,
                    lockedSelectionPaths,
                    PsdHierarchyLocalRepairScopeMode.SelectedNodes,
                    out scope,
                    out error))
            {
                return false;
            }

            if (scope != null)
            {
                context.localRepairScope = scope;
            }

            return true;
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message ?? string.Empty;
            }
        }

        private static string BuildPlanDetails(string planJson)
        {
            try
            {
                JObject plan = JObject.Parse(planJson ?? string.Empty);
                JObject extraction = (plan["crossParentPrefabExtractions"] as JArray)?.OfType<JObject>().FirstOrDefault();
                if (extraction != null)
                {
                    string template = string.Join(", ", (extraction["templateSources"] as JArray ?? new JArray()).Values<string>());
                    int instanceCount = (extraction["instances"] as JArray)?.Count ?? 0;
                    string unmatched = string.Join(", ", (extraction["unmatched"] as JArray ?? new JArray()).Values<string>());
                    return "模板组：" + template + Environment.NewLine +
                           "匹配实例：" + instanceCount + " 组" + Environment.NewLine +
                           "未匹配节点：" + (string.IsNullOrEmpty(unmatched) ? "无" : unmatched);
                }

                JObject selected = (plan["selectedPrefabExtractions"] as JArray)?.OfType<JObject>().FirstOrDefault();
                return selected == null
                    ? "当前方案没有可执行的局部组件抽取。"
                    : "模板节点：" + string.Join(", ", (selected["sources"] as JArray ?? new JArray()).Values<string>());
            }
            catch (Exception exception)
            {
                return "方案详情读取失败：" + exception.Message;
            }
        }

        private string BuildPlanDetailsWithVisualScores(
            string planJson,
            PsdHierarchyVisualAnalysisResult visualAnalysis)
        {
            try
            {
                var builder = new StringBuilder();
                JObject plan = JObject.Parse(planJson ?? string.Empty);
                JObject extraction = (plan["crossParentPrefabExtractions"] as JArray)?.OfType<JObject>().FirstOrDefault();

                if (extraction != null)
                {
                    string template = string.Join(", ", (extraction["templateSources"] as JArray ?? new JArray()).Values<string>());
                    JArray instances = extraction["instances"] as JArray;
                    int instanceCount = instances?.Count ?? 0;
                    string unmatched = string.Join(", ", (extraction["unmatched"] as JArray ?? new JArray()).Values<string>());

                    builder.AppendLine("模板组：" + template);
                    builder.AppendLine("匹配实例：" + instanceCount + " 组");

                    // 显示视觉分析结果
                    if (visualAnalysis != null)
                    {
                        builder.AppendLine();
                        builder.AppendLine("【视觉相似度评分】");

                        if (instances != null)
                        {
                            int index = 1;
                            foreach (JObject instance in instances.OfType<JObject>())
                            {
                                int sequence = instance.Value<int?>("sequence") ?? 0;
                                JArray sources = instance["sources"] as JArray;

                                if (sources != null && sources.Count > 0)
                                {
                                    // 提取节点ID（去掉 "node:" 前缀）
                                    string firstNodeRef = sources[0].Value<string>();
                                    string nodeId = firstNodeRef?.Replace("node:", string.Empty) ?? string.Empty;

                                    if (visualAnalysis.TryGetScore(nodeId, out PsdHierarchyVisualScore score))
                                    {
                                        string scoreIcon = GetScoreIcon(score.similarityScore);
                                        builder.AppendLine($"  实例 {index} (序号 {sequence}): {scoreIcon} {score.similarityScore}% - {score.reason}");
                                    }
                                    else
                                    {
                                        builder.AppendLine($"  实例 {index} (序号 {sequence}): ? 未评分");
                                    }
                                }

                                index++;
                            }
                        }

                        builder.AppendLine();
                        builder.AppendLine("评分说明：");
                        builder.AppendLine("  ✓ 90-100%: 强烈推荐");
                        builder.AppendLine("  ✓ 80-89%: 推荐");
                        builder.AppendLine("  ? 60-79%: 有差异，建议审查");
                        builder.AppendLine("  ✗ 0-59%: 不推荐");
                    }

                    builder.AppendLine();
                    builder.AppendLine("未匹配节点：" + (string.IsNullOrEmpty(unmatched) ? "无" : unmatched));
                    return builder.ToString();
                }

                JObject selected = (plan["selectedPrefabExtractions"] as JArray)?.OfType<JObject>().FirstOrDefault();
                return selected == null
                    ? "当前方案没有可执行的局部组件抽取。"
                    : "模板节点：" + string.Join(", ", (selected["sources"] as JArray ?? new JArray()).Values<string>());
            }
            catch (Exception exception)
            {
                return "方案详情读取失败：" + exception.Message;
            }
        }

        private static string GetScoreIcon(int score)
        {
            if (score >= 90) return "✓✓";
            if (score >= 80) return "✓";
            if (score >= 60) return "?";
            return "✗";
        }

        private void OnSelectionModeChanged(bool isSceneMode)
        {
            if (isSceneMode)
            {
                sceneSelectionPanel.style.display = DisplayStyle.Flex;
                nameSearchPanel.style.display = DisplayStyle.None;
            }
            else
            {
                sceneSelectionPanel.style.display = DisplayStyle.None;
                nameSearchPanel.style.display = DisplayStyle.Flex;
            }
        }

        private void SearchNodesByName()
        {
            string searchName = nameSearchField?.value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(searchName))
            {
                SetStatus("请输入要查找的节点名称");
                return;
            }

            bool fuzzyMatch = fuzzySearchToggle?.value ?? true;

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
            {
                SetStatus("当前不在 Prefab 编辑模式中");
                return;
            }

            foundNodes.Clear();
            foundNodes = FindNodesByName(stage.prefabContentsRoot, searchName, fuzzyMatch);

            if (foundNodes.Count == 0)
            {
                SetStatus($"未找到匹配 '{searchName}' 的节点");
                nameSearchResults.itemsSource = null;
                nameSearchResults.Rebuild();
                return;
            }

            nameSearchResults.itemsSource = foundNodes;
            nameSearchResults.Rebuild();
            SetStatus($"找到 {foundNodes.Count} 个匹配节点");
        }

        private List<GameObject> FindNodesByName(GameObject root, string searchName, bool fuzzyMatch)
        {
            var results = new List<GameObject>();
            if (root == null) return results;

            SearchRecursive(root.transform, searchName, fuzzyMatch, results);
            return results;
        }

        private void SearchRecursive(Transform transform, string searchName, bool fuzzyMatch, List<GameObject> results)
        {
            if (transform == null) return;

            bool matches = fuzzyMatch
                ? transform.name.IndexOf(searchName, StringComparison.OrdinalIgnoreCase) >= 0
                : string.Equals(transform.name, searchName, StringComparison.OrdinalIgnoreCase);

            if (matches)
            {
                // 检查是否有 Identity（确保是 PSD 生成的节点）
                if (transform.GetComponent<PsdPrefabNodeIdentity>() != null)
                {
                    results.Add(transform.gameObject);
                }
            }

            foreach (Transform child in transform)
            {
                SearchRecursive(child, searchName, fuzzyMatch, results);
            }
        }

        private void OnSearchResultSelected(IEnumerable<object> selectedItems)
        {
            var selectedList = new List<object>(selectedItems);
            if (selectedList.Count == 0) return;

            GameObject selectedNode = selectedList[0] as GameObject;
            if (selectedNode != null)
            {
                // 高亮选中的节点
                Selection.activeGameObject = selectedNode;

                // 自动锁定为选区
                LockSearchResult(selectedNode);
            }
        }

        private void LockSearchResult(GameObject selectedNode)
        {
            scope = null;
            lockedSelectionPaths = new[] { GetGameObjectPath(selectedNode) };
            pendingPlanJson = string.Empty;
            confirmButton?.SetEnabled(false);
            HideConflictPanel();
            ShowContinueAnalysisButton(false);

            if (!TryRefreshContext(out string refreshError))
            {
                SetStatus(refreshError);
                return;
            }

            // 临时设置 Unity Selection
            var previousSelection = Selection.objects;
            Selection.activeGameObject = selectedNode;

            if (!PsdHierarchyLocalRepairScope.TryCaptureCurrentSelection(
                    context,
                    PsdHierarchyLocalRepairScopeMode.SelectedNodes,
                    out PsdHierarchyLocalRepairScope capturedScope,
                    out string error))
            {
                Selection.objects = previousSelection;
                SetStatus(error);
                return;
            }

            Selection.objects = previousSelection;

            scope = capturedScope;
            context.localRepairScope = scope;
            reviewLabel.text = "选区已锁定（通过名称查找），点击\"分析局部整理\"生成方案。";
            selectionSummary.text = string.Join(Environment.NewLine, lockedSelectionPaths);
            SetStatus("选区已锁定（名称查找）");
        }

        private static string GetGameObjectPath(GameObject obj)
        {
            if (obj == null) return string.Empty;

            string path = obj.name;
            Transform current = obj.transform.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private bool ValidateSelection(out string error)
        {
            error = string.Empty;

            // 检查是否有选中对象
            if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
            {
                error = "请先在 Prefab Stage 中选择要整理的节点。";
                return false;
            }

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
            {
                error = "当前不在 Prefab 编辑模式中。";
                return false;
            }

            // 检查选中的对象是否都在当前 Prefab 中
            GameObject prefabRoot = stage.prefabContentsRoot;
            foreach (GameObject selected in Selection.gameObjects)
            {
                if (!IsChildOf(selected.transform, prefabRoot.transform))
                {
                    error = $"选中的对象 '{selected.name}' 不属于当前编辑的 Prefab。";
                    return false;
                }
            }

            // 检查是否包含嵌套 Prefab 实例
            foreach (GameObject selected in Selection.gameObjects)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(selected) &&
                    PrefabUtility.GetNearestPrefabInstanceRoot(selected) == selected)
                {
                    error = $"选区包含嵌套 Prefab 实例 '{selected.name}'。\n局部整理不支持嵌套 Prefab，请展开后重试。";
                    return false;
                }
            }

            // 检查节点数量限制
            if (Selection.gameObjects.Length > 100)
            {
                error = $"选中的节点过多（{Selection.gameObjects.Length} 个），建议一次整理不超过 100 个节点。";
                return false;
            }

            // 检查是否有 PsdPrefabNodeIdentity
            bool hasIdentity = false;
            foreach (GameObject selected in Selection.gameObjects)
            {
                if (selected.GetComponent<PsdPrefabNodeIdentity>() != null)
                {
                    hasIdentity = true;
                    break;
                }
            }

            if (!hasIdentity)
            {
                error = "选中的节点中没有找到 PsdPrefabNodeIdentity 组件。\n请确保选择的是通过 PSD Layout Tool 生成的节点。";
                return false;
            }

            return true;
        }

        private bool IsChildOf(Transform child, Transform parent)
        {
            if (child == null || parent == null) return false;
            if (child == parent) return true;

            Transform current = child.parent;
            while (current != null)
            {
                if (current == parent) return true;
                current = current.parent;
            }

            return false;
        }

        private void ShowConflictPanel(List<LocalRepairConflict> conflicts)
        {
            // 如果已存在冲突面板，先移除
            HideConflictPanel();

            conflictPanel = new VisualElement();
            conflictPanel.name = "conflict-panel";
            conflictPanel.AddToClassList("psd-local-repair-panel");
            conflictPanel.style.backgroundColor = new Color(0.9f, 0.8f, 0.6f, 0.3f);
            conflictPanel.style.borderBottomWidth = 1f;
            conflictPanel.style.borderTopWidth = 1f;
            conflictPanel.style.borderBottomColor = new Color(0.8f, 0.6f, 0.2f);
            conflictPanel.style.borderTopColor = new Color(0.8f, 0.6f, 0.2f);
            conflictPanel.style.paddingTop = 8f;
            conflictPanel.style.paddingBottom = 8f;
            conflictPanel.style.marginTop = 8f;
            conflictPanel.style.marginBottom = 8f;

            // 冲突摘要
            int criticalCount = conflicts.Count(c => c.Severity == ConflictSeverity.Critical);
            int warningCount = conflicts.Count(c => c.Severity == ConflictSeverity.Warning);
            int infoCount = conflicts.Count(c => c.Severity == ConflictSeverity.Info);

            string summaryText = $"⚠️ 检测到 {conflicts.Count} 个潜在冲突";
            if (criticalCount > 0)
                summaryText += $" (严重: {criticalCount}";
            if (warningCount > 0)
                summaryText += $", 警告: {warningCount}";
            if (infoCount > 0)
                summaryText += $", 提示: {infoCount}";
            if (criticalCount > 0 || warningCount > 0 || infoCount > 0)
                summaryText += ")";

            conflictSummaryLabel = new Label(summaryText);
            conflictSummaryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            conflictSummaryLabel.style.marginBottom = 4f;
            conflictPanel.Add(conflictSummaryLabel);

            // 冲突列表
            conflictScrollView = new ScrollView();
            conflictScrollView.style.maxHeight = 200f;
            conflictScrollView.style.minHeight = 80f;

            foreach (var conflict in conflicts)
            {
                var conflictItem = CreateConflictItem(conflict);
                conflictScrollView.Add(conflictItem);
            }

            conflictPanel.Add(conflictScrollView);

            // 插入到 reviewPanel 之前
            var reviewPanelParent = reviewLabel?.parent?.parent;
            if (reviewPanelParent != null && reviewPanelParent.parent != null)
            {
                int reviewIndex = reviewPanelParent.parent.IndexOf(reviewPanelParent);
                reviewPanelParent.parent.Insert(reviewIndex, conflictPanel);
            }
        }

        private void HideConflictPanel()
        {
            if (conflictPanel != null && conflictPanel.parent != null)
            {
                conflictPanel.parent.Remove(conflictPanel);
                conflictPanel = null;
            }
        }

        private VisualElement CreateConflictItem(LocalRepairConflict conflict)
        {
            var item = new VisualElement();
            item.style.marginBottom = 6f;
            item.style.paddingLeft = 4f;
            item.style.paddingRight = 4f;
            item.style.paddingTop = 4f;
            item.style.paddingBottom = 4f;
            item.style.backgroundColor = new Color(1f, 1f, 1f, 0.1f);
            item.style.borderLeftWidth = 3f;

            // 根据严重级别设置边框颜色
            switch (conflict.Severity)
            {
                case ConflictSeverity.Critical:
                    item.style.borderLeftColor = new Color(0.8f, 0.2f, 0.2f);
                    break;
                case ConflictSeverity.Warning:
                    item.style.borderLeftColor = new Color(0.9f, 0.6f, 0.2f);
                    break;
                case ConflictSeverity.Info:
                    item.style.borderLeftColor = new Color(0.3f, 0.6f, 0.9f);
                    break;
            }

            // 冲突描述
            var descLabel = new Label($"{GetSeverityIcon(conflict.Severity)} {conflict.Description}");
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            descLabel.style.marginBottom = 2f;
            item.Add(descLabel);

            // 节点路径（小字）
            if (!string.IsNullOrEmpty(conflict.NodePath))
            {
                var pathLabel = new Label($"节点: {conflict.NodePath}");
                pathLabel.style.fontSize = 10f;
                pathLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                pathLabel.style.marginBottom = 2f;
                item.Add(pathLabel);
            }

            // 详细信息
            if (!string.IsNullOrEmpty(conflict.DetailedInfo))
            {
                var detailLabel = new Label(conflict.DetailedInfo);
                detailLabel.style.fontSize = 10f;
                detailLabel.style.whiteSpace = WhiteSpace.Normal;
                detailLabel.style.marginBottom = 4f;
                item.Add(detailLabel);
            }

            // 策略选择
            if (conflict.AvailableStrategies != null && conflict.AvailableStrategies.Count > 0)
            {
                var strategyRow = new VisualElement();
                strategyRow.style.flexDirection = FlexDirection.Row;
                strategyRow.style.alignItems = Align.Center;

                var strategyLabel = new Label("解决策略: ");
                strategyLabel.style.fontSize = 11f;
                strategyLabel.style.minWidth = 60f;
                strategyRow.Add(strategyLabel);

                // 创建策略下拉框
                var strategyChoices = conflict.AvailableStrategies
                    .Select(s => PsdHierarchyLocalRepairStrategyHelper.GetStrategyDisplayName(s))
                    .ToList();

                var strategyDropdown = new DropdownField(strategyChoices, 0);
                strategyDropdown.style.flexGrow = 1f;

                // 设置默认选中推荐策略
                int recommendedIndex = conflict.AvailableStrategies.IndexOf(conflict.RecommendedStrategy);
                if (recommendedIndex >= 0)
                {
                    strategyDropdown.index = recommendedIndex;
                    conflict.SelectedStrategy = conflict.RecommendedStrategy;
                }

                // 监听策略变化
                strategyDropdown.RegisterValueChangedCallback(evt =>
                {
                    int selectedIndex = strategyDropdown.index;
                    if (selectedIndex >= 0 && selectedIndex < conflict.AvailableStrategies.Count)
                    {
                        conflict.SelectedStrategy = conflict.AvailableStrategies[selectedIndex];
                    }
                });

                strategyRow.Add(strategyDropdown);

                // 策略说明（tooltip 或小文本）
                var strategyDesc = PsdHierarchyLocalRepairStrategyHelper.GetStrategyDescription(
                    conflict.SelectedStrategy);
                if (!string.IsNullOrEmpty(strategyDesc))
                {
                    strategyDropdown.tooltip = strategyDesc;
                }

                item.Add(strategyRow);
            }

            return item;
        }

        private static string GetSeverityIcon(ConflictSeverity severity)
        {
            switch (severity)
            {
                case ConflictSeverity.Critical:
                    return "🛑";
                case ConflictSeverity.Warning:
                    return "⚠️";
                case ConflictSeverity.Info:
                    return "ℹ️";
                default:
                    return "•";
            }
        }
    }
}
