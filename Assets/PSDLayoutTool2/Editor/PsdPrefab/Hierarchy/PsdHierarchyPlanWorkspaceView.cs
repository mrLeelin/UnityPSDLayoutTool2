namespace PsdLayoutTool2
{
    using System;
    using System.Linq;
    using UnityEngine.UIElements;

    internal sealed class PsdHierarchyPlanWorkspaceActions
    {
        internal Action<string> keepOriginalStructure;
        internal Action<string[]> locateNodes;
        internal Action retryAnalysis;
        internal Action exportDiagnostics;
    }

    internal sealed class PsdHierarchyPlanWorkspaceView : VisualElement
    {
        internal const string RootName = "psd-hierarchy-plan-workspace";
        internal const string TitleName = "psd-hierarchy-plan-workspace-title";
        internal const string SummaryName = "psd-hierarchy-plan-workspace-summary";
        internal const string IssueListName = "psd-hierarchy-plan-workspace-issues";
        internal const string IssueStateName = "psd-hierarchy-plan-workspace-issue-state";
        internal const string KeepOriginalButtonName = "psd-hierarchy-plan-workspace-keep-original";
        internal const string LocateNodesButtonName = "psd-hierarchy-plan-workspace-locate";
        internal const string RetryButtonName = "psd-hierarchy-plan-workspace-retry";
        internal const string ShowDetailsButtonName = "psd-hierarchy-plan-workspace-details";
        internal const string ExportButtonName = "psd-hierarchy-plan-workspace-export";
        internal const string ApplySafeButtonName = "psd-hierarchy-plan-workspace-apply-safe";
        internal const string TechnicalDetailsName = "psd-hierarchy-plan-workspace-technical-details";
        internal const string IssueRowClassName = "psd-hierarchy-plan-workspace-issue";

        internal PsdHierarchyPlanWorkspaceView()
        {
            name = RootName;
            AddToClassList("psd-hierarchy-plan-workspace");
            style.display = DisplayStyle.None;
        }

        internal void Bind(
            PsdHierarchyPlanWorkspace workspace,
            PsdHierarchyPlanWorkspaceActions actions)
        {
            Clear();
            if (workspace == null)
            {
                style.display = DisplayStyle.None;
                return;
            }

            actions = actions ?? new PsdHierarchyPlanWorkspaceActions();
            style.display = DisplayStyle.Flex;
            Add(CreateHeader(workspace, actions));
            var summary = new Label(BuildSummary(workspace))
            {
                name = SummaryName,
            };
            summary.AddToClassList("psd-hierarchy-plan-workspace-summary");
            Add(summary);

            var issueList = new VisualElement
            {
                name = IssueListName,
            };
            issueList.AddToClassList("psd-hierarchy-plan-workspace-issues");
            foreach (PsdHierarchyPlanIssue issue in workspace.issues)
            {
                issueList.Add(CreateIssueRow(issue, actions));
            }

            Add(issueList);
        }

        private static VisualElement CreateHeader(
            PsdHierarchyPlanWorkspace workspace,
            PsdHierarchyPlanWorkspaceActions actions)
        {
            var header = new VisualElement();
            header.AddToClassList("psd-hierarchy-plan-workspace-header");

            var title = new Label(workspace.issues.Count > 0 ? "待处理问题" : "计划工作区")
            {
                name = TitleName,
            };
            title.AddToClassList("psd-hierarchy-plan-workspace-title");
            header.Add(title);

            if (workspace.issues.Count > 0)
            {
                var exportButton = new Button(() => actions.exportDiagnostics?.Invoke())
                {
                    name = ExportButtonName,
                    text = "导出诊断",
                    tooltip = "将当前工作区和完整错误写入 Library/PSDLayoutTool2/PlanWorkspaces",
                };
                exportButton.AddToClassList("psd-hierarchy-plan-workspace-button");
                exportButton.AddToClassList("psd-hierarchy-plan-workspace-button-secondary");
                header.Add(exportButton);
            }

            return header;
        }

        private static VisualElement CreateIssueRow(
            PsdHierarchyPlanIssue issue,
            PsdHierarchyPlanWorkspaceActions actions)
        {
            var row = new VisualElement();
            row.AddToClassList(IssueRowClassName);

            var heading = new VisualElement();
            heading.AddToClassList("psd-hierarchy-plan-workspace-issue-heading");
            var state = new Label(GetIssueStateText(issue))
            {
                name = IssueStateName,
            };
            state.AddToClassList("psd-hierarchy-plan-workspace-issue-state");
            state.AddToClassList(GetSeverityClass(issue));
            heading.Add(state);

            var summary = new Label(issue.summary ?? string.Empty);
            summary.AddToClassList("psd-hierarchy-plan-workspace-issue-summary");
            heading.Add(summary);
            row.Add(heading);

            if (issue.candidateIds?.Length > 0)
            {
                row.Add(CreateMetadataLabel("候选：" + string.Join(", ", issue.candidateIds)));
            }

            if (issue.affectedNodeIds?.Length > 0)
            {
                row.Add(CreateMetadataLabel("节点：" + string.Join(", ", issue.affectedNodeIds)));
            }

            if (!string.IsNullOrWhiteSpace(issue.recommendedResolution))
            {
                var recommendation = new Label(issue.recommendedResolution);
                recommendation.AddToClassList("psd-hierarchy-plan-workspace-recommendation");
                row.Add(recommendation);
            }

            var details = new Label(issue.technicalDetails ?? string.Empty)
            {
                name = TechnicalDetailsName,
            };
            details.AddToClassList("psd-hierarchy-plan-workspace-technical-details");
            details.style.display = DisplayStyle.None;

            var actionsRow = new VisualElement();
            actionsRow.AddToClassList("psd-hierarchy-plan-workspace-actions");

            var keepButton = CreateButton(
                KeepOriginalButtonName,
                "保持原结构",
                () => actions.keepOriginalStructure?.Invoke(issue.id));
            keepButton.SetEnabled(issue.state == PsdHierarchyPlanIssueState.Open);
            actionsRow.Add(keepButton);

            var locateButton = CreateButton(
                LocateNodesButtonName,
                "定位节点",
                () => actions.locateNodes?.Invoke((issue.affectedNodeIds ?? Array.Empty<string>()).ToArray()));
            locateButton.SetEnabled(issue.affectedNodeIds?.Length > 0);
            actionsRow.Add(locateButton);

            actionsRow.Add(CreateButton(
                RetryButtonName,
                "重新分析",
                () => actions.retryAnalysis?.Invoke()));

            Button detailsButton = null;
            detailsButton = CreateButton(
                ShowDetailsButtonName,
                "查看详情",
                () =>
                {
                    bool show = details.style.display.value == DisplayStyle.None;
                    details.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                    detailsButton.text = show ? "收起详情" : "查看详情";
                });
            actionsRow.Add(detailsButton);

            row.Add(actionsRow);
            row.Add(details);
            return row;
        }

        private static Button CreateButton(string name, string text, Action clicked)
        {
            var button = new Button(clicked)
            {
                name = name,
                text = text,
            };
            button.AddToClassList("psd-hierarchy-plan-workspace-button");
            return button;
        }

        private static Label CreateMetadataLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("psd-hierarchy-plan-workspace-metadata");
            return label;
        }

        private static string BuildSummary(PsdHierarchyPlanWorkspace workspace)
        {
            return "可执行 " + workspace.SafeBatchCount +
                   " · 隔离 " + workspace.QuarantinedBatchCount +
                   " · 待处理 " + workspace.OpenIssueCount +
                   " · 影响节点 " + workspace.AffectedNodeCount +
                   " · " + GetWorkspaceStateText(workspace.state);
        }

        private static string GetWorkspaceStateText(PsdHierarchyPlanWorkspaceState state)
        {
            switch (state)
            {
                case PsdHierarchyPlanWorkspaceState.Ready:
                    return "可确认执行";
                case PsdHierarchyPlanWorkspaceState.NeedsAttention:
                    return "需要处理";
                case PsdHierarchyPlanWorkspaceState.NoSafeChanges:
                    return "无安全变更";
                case PsdHierarchyPlanWorkspaceState.InfrastructureBlocked:
                    return "环境阻塞";
                default:
                    return "未知状态";
            }
        }

        private static string GetIssueStateText(PsdHierarchyPlanIssue issue)
        {
            if (issue.state == PsdHierarchyPlanIssueState.KeptUnchanged)
            {
                return "已保留原结构";
            }

            return issue.severity == PsdHierarchyPlanIssueSeverity.Blocked ? "阻塞" : "需处理";
        }

        private static string GetSeverityClass(PsdHierarchyPlanIssue issue)
        {
            if (issue.state == PsdHierarchyPlanIssueState.KeptUnchanged)
            {
                return "psd-hierarchy-plan-workspace-state-resolved";
            }

            return issue.severity == PsdHierarchyPlanIssueSeverity.Blocked
                ? "psd-hierarchy-plan-workspace-state-blocked"
                : "psd-hierarchy-plan-workspace-state-attention";
        }
    }
}
