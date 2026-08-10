namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// 自动检测并报告 AI 聊天计划中缺失的组件抽取
    /// </summary>
    internal static class PsdHierarchyChatPlanDiagnostics
    {
        /// <summary>
        /// 检查计划是否遗漏了必需的组件候选
        /// </summary>
        internal static string DiagnoseRequiredCandidates(
            JObject plan,
            PsdHierarchyChatContext context)
        {
            if (context?.componentFamilyCandidates == null || plan == null)
            {
                return string.Empty;
            }

            var requiredCandidates = context.componentFamilyCandidates
                .Where(c => c.requiresExtraction)
                .ToArray();

            if (requiredCandidates.Length == 0)
            {
                return string.Empty;
            }

            // 检查已处理的候选
            JArray decisions = plan["componentFamilyDecisions"] as JArray ?? new JArray();
            var processedCandidateIds = new HashSet<string>(
                decisions.OfType<JObject>()
                    .Select(d => d.Value<string>("candidateId"))
                    .Where(id => !string.IsNullOrEmpty(id)),
                StringComparer.Ordinal);

            // 找出被跳过的强制候选
            var skippedCandidates = requiredCandidates
                .Where(c => !processedCandidateIds.Contains(c.id))
                .ToArray();

            if (skippedCandidates.Length == 0)
            {
                return string.Empty;
            }

            // 生成诊断报告
            var report = new System.Text.StringBuilder();
            report.AppendLine("⚠️ AI 跳过了以下强制组件候选（requiresExtraction=true）：");
            report.AppendLine();

            foreach (var candidate in skippedCandidates)
            {
                report.AppendLine($"【{candidate.suggestedAssetName}】");
                report.AppendLine($"  ID: {candidate.id}");
                report.AppendLine($"  模式: {candidate.recommendedMode}");
                report.AppendLine($"  实例数: {candidate.sources.Count}");
                report.AppendLine($"  实例: {string.Join(", ", candidate.sources)}");
                report.AppendLine();
            }

            report.AppendLine("建议：");
            report.AppendLine("1. 使用「AI 局部整理」功能手动选择这些节点进行组件化");
            report.AppendLine("2. 或者重新触发全局整理，AI 应该会重新尝试处理");

            return report.ToString();
        }

        /// <summary>
        /// 在 Console 中输出诊断信息
        /// </summary>
        internal static void LogMissingExtractions(JObject plan, PsdHierarchyChatContext context)
        {
            string diagnosis = DiagnoseRequiredCandidates(plan, context);
            if (!string.IsNullOrEmpty(diagnosis))
            {
                UnityEngine.Debug.LogWarning(
                    "[PSD 层级整理] " + diagnosis);
            }
        }
    }
}
