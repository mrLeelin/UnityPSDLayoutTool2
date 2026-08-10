namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal enum PsdHierarchyPlanWorkspaceState
    {
        Ready,
        NeedsAttention,
        NoSafeChanges,
        InfrastructureBlocked,
    }

    internal enum PsdHierarchyPlanIssueSeverity
    {
        Info,
        Warning,
        NeedsDecision,
        Blocked,
    }

    internal enum PsdHierarchyPlanIssueCategory
    {
        PlanExtraction,
        PlanPreparation,
        RunnerPreflight,
        Infrastructure,
    }

    internal enum PsdHierarchyPlanIssueState
    {
        Open,
        KeptUnchanged,
    }

    [Serializable]
    internal sealed class PsdHierarchyPlanIssue
    {
        public string id = string.Empty;
        public PsdHierarchyPlanIssueSeverity severity;
        public PsdHierarchyPlanIssueCategory category;
        public string[] candidateIds = Array.Empty<string>();
        public string[] affectedNodeIds = Array.Empty<string>();
        public string summary = string.Empty;
        public string technicalDetails = string.Empty;
        public string recommendedResolution = string.Empty;
        public PsdHierarchyPlanIssueState state;
    }

    [Serializable]
    internal sealed class PsdHierarchyPlanBatch
    {
        public string id = string.Empty;
        public bool enabled;
        public string planJson = string.Empty;
    }

    [Serializable]
    internal sealed class PsdHierarchyPlanWorkspace
    {
        public string snapshotFingerprint = string.Empty;
        public string reviewText = string.Empty;
        public string rawAssistantReply = string.Empty;
        public PsdHierarchyPlanWorkspaceState state;
        public List<PsdHierarchyPlanBatch> batches = new List<PsdHierarchyPlanBatch>();
        public List<PsdHierarchyPlanIssue> issues = new List<PsdHierarchyPlanIssue>();

        internal int SafeBatchCount => batches.Count(batch => batch.enabled);

        internal int QuarantinedBatchCount => batches.Count(batch => !batch.enabled);

        internal int OpenIssueCount => issues.Count(issue => issue.state == PsdHierarchyPlanIssueState.Open);

        internal int AffectedNodeCount => issues
            .SelectMany(issue => issue.affectedNodeIds ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .Count();

        internal static PsdHierarchyPlanWorkspace CreateReady(
            string snapshotFingerprint,
            string reviewText,
            string rawAssistantReply,
            string validatedPlanJson)
        {
            return new PsdHierarchyPlanWorkspace
            {
                snapshotFingerprint = snapshotFingerprint ?? string.Empty,
                reviewText = reviewText ?? string.Empty,
                rawAssistantReply = rawAssistantReply ?? string.Empty,
                state = PsdHierarchyPlanWorkspaceState.Ready,
                batches = new List<PsdHierarchyPlanBatch>
                {
                    new PsdHierarchyPlanBatch
                    {
                        id = "batch_compatibility_001",
                        enabled = true,
                        planJson = validatedPlanJson ?? string.Empty,
                    },
                },
            };
        }

        internal static PsdHierarchyPlanWorkspace CreateBlockedPlan(
            string snapshotFingerprint,
            string reviewText,
            string rawAssistantReply,
            PsdHierarchyPlanIssueCategory category,
            string summary,
            string technicalDetails,
            string recommendedResolution,
            IEnumerable<string> candidateIds,
            IEnumerable<string> affectedNodeIds,
            string quarantinedPlanJson = "")
        {
            return new PsdHierarchyPlanWorkspace
            {
                snapshotFingerprint = snapshotFingerprint ?? string.Empty,
                reviewText = reviewText ?? string.Empty,
                rawAssistantReply = rawAssistantReply ?? string.Empty,
                state = PsdHierarchyPlanWorkspaceState.NoSafeChanges,
                batches = new List<PsdHierarchyPlanBatch>
                {
                    new PsdHierarchyPlanBatch
                    {
                        id = "batch_compatibility_001",
                        enabled = false,
                        planJson = quarantinedPlanJson ?? string.Empty,
                    },
                },
                issues = new List<PsdHierarchyPlanIssue>
                {
                    CreateIssue(
                        category,
                        PsdHierarchyPlanIssueSeverity.NeedsDecision,
                        summary,
                        technicalDetails,
                        recommendedResolution,
                        candidateIds,
                        affectedNodeIds),
                },
            };
        }

        internal static PsdHierarchyPlanWorkspace CreateInfrastructureBlocked(
            string snapshotFingerprint,
            string reviewText,
            string rawAssistantReply,
            string technicalDetails,
            string recommendedResolution,
            string quarantinedPlanJson = "")
        {
            var batches = new List<PsdHierarchyPlanBatch>();
            if (!string.IsNullOrWhiteSpace(quarantinedPlanJson))
            {
                batches.Add(new PsdHierarchyPlanBatch
                {
                    id = "batch_compatibility_001",
                    enabled = false,
                    planJson = quarantinedPlanJson,
                });
            }

            return new PsdHierarchyPlanWorkspace
            {
                snapshotFingerprint = snapshotFingerprint ?? string.Empty,
                reviewText = reviewText ?? string.Empty,
                rawAssistantReply = rawAssistantReply ?? string.Empty,
                state = PsdHierarchyPlanWorkspaceState.InfrastructureBlocked,
                batches = batches,
                issues = new List<PsdHierarchyPlanIssue>
                {
                    CreateIssue(
                        PsdHierarchyPlanIssueCategory.Infrastructure,
                        PsdHierarchyPlanIssueSeverity.Blocked,
                        "分析或校验环境当前不可用。",
                        technicalDetails,
                        recommendedResolution,
                        Array.Empty<string>(),
                        Array.Empty<string>()),
                },
            };
        }

        internal bool TryGetEnabledPlan(out string planJson)
        {
            PsdHierarchyPlanBatch batch = batches.FirstOrDefault(candidate =>
                candidate.enabled && !string.IsNullOrWhiteSpace(candidate.planJson));
            planJson = batch?.planJson ?? string.Empty;
            return !string.IsNullOrWhiteSpace(planJson);
        }

        internal bool TryKeepOriginalStructure(string issueId)
        {
            PsdHierarchyPlanIssue issue = issues.FirstOrDefault(candidate =>
                string.Equals(candidate.id, issueId, StringComparison.Ordinal));
            if (issue == null)
            {
                return false;
            }

            issue.state = PsdHierarchyPlanIssueState.KeptUnchanged;
            return true;
        }

        internal static PsdHierarchyPlanIssue CreateIssue(
            PsdHierarchyPlanIssueCategory category,
            PsdHierarchyPlanIssueSeverity severity,
            string summary,
            string technicalDetails,
            string recommendedResolution,
            IEnumerable<string> candidateIds,
            IEnumerable<string> affectedNodeIds)
        {
            return new PsdHierarchyPlanIssue
            {
                id = "issue_" + GetCategoryId(category) + "_001",
                category = category,
                severity = severity,
                candidateIds = CopyIds(candidateIds),
                affectedNodeIds = CopyIds(affectedNodeIds),
                summary = summary ?? string.Empty,
                technicalDetails = technicalDetails ?? string.Empty,
                recommendedResolution = recommendedResolution ?? string.Empty,
                state = PsdHierarchyPlanIssueState.Open,
            };
        }

        private static string[] CopyIds(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static string GetCategoryId(PsdHierarchyPlanIssueCategory category)
        {
            switch (category)
            {
                case PsdHierarchyPlanIssueCategory.PlanExtraction:
                    return "plan_extraction";
                case PsdHierarchyPlanIssueCategory.PlanPreparation:
                    return "plan_preparation";
                case PsdHierarchyPlanIssueCategory.RunnerPreflight:
                    return "runner_preflight";
                case PsdHierarchyPlanIssueCategory.Infrastructure:
                    return "infrastructure";
                default:
                    return "unknown";
            }
        }
    }
}
