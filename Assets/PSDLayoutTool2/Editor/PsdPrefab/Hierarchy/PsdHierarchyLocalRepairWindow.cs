namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using UnityEditor;
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
        private string[] lockedSelectionPaths = Array.Empty<string>();

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

            var selectionPanel = new VisualElement();
            selectionPanel.AddToClassList("psd-local-repair-panel");
            selectionPanel.Add(new Label("当前选区"));
            selectionSummary = new Label("尚未锁定选区") { name = SelectionSummaryName };
            selectionSummary.style.whiteSpace = WhiteSpace.Normal;
            selectionPanel.Add(selectionSummary);
            lockSelectionButton = new Button(LockCurrentSelection)
            {
                name = LockSelectionButtonName,
                text = "锁定当前选区",
                tooltip = "读取当前 Prefab Stage 的 Unity Selection，并将其作为本次局部整理范围。",
            };
            selectionPanel.Add(lockSelectionButton);
            root.Add(selectionPanel);

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
            root.Add(planPanel);

            var reviewPanel = new VisualElement();
            reviewPanel.AddToClassList("psd-local-repair-panel");
            reviewPanel.Add(new Label("整理方案"));
            var reviewScroll = new ScrollView { name = ReviewName };
            reviewScroll.style.flexGrow = 1f;
            reviewScroll.style.minHeight = 150f;
            reviewLabel = new Label("点击“分析局部整理”后，这里会显示组件模板、匹配实例和未匹配节点。")
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
            if (!TryRefreshContext(out string refreshError))
            {
                SetStatus(refreshError);
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
            reviewLabel.text = "选区已锁定，点击“分析局部整理”生成方案。";
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

            // 禁用按钮，显示分析中状态
            analyzeButton?.SetEnabled(false);
            SetStatus("正在分析局部整理（包括视觉相似度）...");

            try
            {
                // 异步构建方案（包含视觉分析）
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
            try
            {
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
                    confirmButton?.SetEnabled(true);
                }
            }
            catch (Exception exception)
            {
                SetStatus("局部整理更新失败：" + exception.Message);
                confirmButton?.SetEnabled(true);
            }
            finally
            {
                analyzeButton?.SetEnabled(true);
                lockSelectionButton?.SetEnabled(true);
            }
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
    }
}
