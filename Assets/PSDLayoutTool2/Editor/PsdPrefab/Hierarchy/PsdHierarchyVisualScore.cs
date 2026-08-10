namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// 视觉相似度评分结果
    /// </summary>
    internal readonly struct PsdHierarchyVisualScore
    {
        internal PsdHierarchyVisualScore(
            string nodeId,
            int similarityScore,
            string reason)
        {
            this.nodeId = nodeId ?? string.Empty;
            this.similarityScore = Math.Max(0, Math.Min(100, similarityScore));
            this.reason = reason ?? string.Empty;
        }

        /// <summary>节点 ID</summary>
        internal readonly string nodeId;

        /// <summary>视觉相似度评分 (0-100)</summary>
        internal readonly int similarityScore;

        /// <summary>评分原因说明</summary>
        internal readonly string reason;

        /// <summary>是否通过推荐阈值 (80%)</summary>
        internal bool IsRecommended => similarityScore >= 80;

        /// <summary>是否可接受 (60%+)</summary>
        internal bool IsAcceptable => similarityScore >= 60;
    }

    /// <summary>
    /// 视觉分析结果集
    /// </summary>
    internal sealed class PsdHierarchyVisualAnalysisResult
    {
        internal PsdHierarchyVisualAnalysisResult(
            IEnumerable<string> templateNodeIds,
            IEnumerable<PsdHierarchyVisualScore> scores,
            string analysisMethod)
        {
            this.templateNodeIds = (templateNodeIds ?? Array.Empty<string>()).ToArray();
            this.scoresByNodeId = (scores ?? Array.Empty<PsdHierarchyVisualScore>())
                .ToDictionary(s => s.nodeId, s => s, StringComparer.Ordinal);
            this.analysisMethod = analysisMethod ?? "unknown";
        }

        internal readonly string[] templateNodeIds;
        internal readonly string analysisMethod;
        private readonly Dictionary<string, PsdHierarchyVisualScore> scoresByNodeId;

        /// <summary>获取指定节点的视觉评分</summary>
        internal bool TryGetScore(string nodeId, out PsdHierarchyVisualScore score)
        {
            return scoresByNodeId.TryGetValue(nodeId ?? string.Empty, out score);
        }

        /// <summary>获取所有评分</summary>
        internal IEnumerable<PsdHierarchyVisualScore> GetAllScores()
        {
            return scoresByNodeId.Values;
        }

        /// <summary>获取推荐的节点（评分 >= 80%）</summary>
        internal string[] GetRecommendedNodeIds()
        {
            return scoresByNodeId.Values
                .Where(s => s.IsRecommended)
                .Select(s => s.nodeId)
                .ToArray();
        }

        /// <summary>获取可接受的节点（评分 >= 60%）</summary>
        internal string[] GetAcceptableNodeIds()
        {
            return scoresByNodeId.Values
                .Where(s => s.IsAcceptable)
                .Select(s => s.nodeId)
                .ToArray();
        }
    }
}
