namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using UnityEngine;

    /// <summary>
    /// 构建视觉分析的 AI 提示词，用于评估节点组合的视觉相似度
    /// </summary>
    internal static class PsdHierarchyVisualChatPrompt
    {
        /// <summary>
        /// 构建视觉相似度分析提示词（逻辑筛选后的视觉验证）
        /// </summary>
        internal static string BuildVisualSimilarityPrompt(
            PsdHierarchyChatContext context,
            string[] templateNodeIds,
            PsdHierarchyCrossParentPrefabInstance[] candidateInstances)
        {
            var builder = new StringBuilder();

            builder.AppendLine("# 视觉相似度分析任务");
            builder.AppendLine();
            builder.AppendLine("你正在协助 Unity Prefab 的组件化整理。用户已经通过**逻辑筛选**（结构、命名模式）找到了一组候选节点组合。");
            builder.AppendLine("现在需要你通过**视觉分析**评估每个候选组合与模板组的相似度。");
            builder.AppendLine();

            builder.AppendLine("## 模板组（用户选择）");
            builder.AppendLine("用户选择的代表性实例，作为组件化的模板：");
            for (int i = 0; i < templateNodeIds.Length; i++)
            {
                string nodeId = templateNodeIds[i];
                if (context.TryGetNodePath(nodeId, out string path))
                {
                    builder.AppendLine($"  {i + 1}. {path} (ID: {nodeId})");
                }
            }
            builder.AppendLine();

            builder.AppendLine("## 候选组合（逻辑筛选通过）");
            builder.AppendLine($"逻辑分析已找到 {candidateInstances.Length} 个结构相似的组合：");
            for (int i = 0; i < candidateInstances.Length; i++)
            {
                var instance = candidateInstances[i];
                builder.AppendLine($"### 候选 {i + 1} (序号 {instance.sequence})");
                foreach (string nodeId in instance.sourceNodeIds)
                {
                    if (context.TryGetNodePath(nodeId, out string path))
                    {
                        builder.AppendLine($"  - {path} (ID: {nodeId})");
                    }
                }
                builder.AppendLine();
            }

            builder.AppendLine("## 分析要求");
            builder.AppendLine();
            builder.AppendLine("为每个候选组合评估其与模板组的**视觉相似度**，考虑以下因素：");
            builder.AppendLine("1. **布局一致性**：位置、大小、间距是否相似？");
            builder.AppendLine("2. **颜色方案**：背景色、前景色、图标颜色是否一致？");
            builder.AppendLine("3. **视觉元素**：图标、文本、按钮等视觉元素是否匹配？");
            builder.AppendLine("4. **风格统一性**：整体视觉风格是否相同？");
            builder.AppendLine();
            builder.AppendLine("**注意**：");
            builder.AppendLine("- 文字内容、数值、图标具体图案的差异是正常的（这些是数据差异，不是结构差异）");
            builder.AppendLine("- 关注的是**布局结构**和**视觉风格**的相似性");
            builder.AppendLine("- 背景颜色、尺寸比例的显著差异需要标记");
            builder.AppendLine();

            builder.AppendLine("## 输出格式");
            builder.AppendLine();
            builder.AppendLine("请返回一个 JSON 对象，包含每个候选的评分：");
            builder.AppendLine();
            builder.AppendLine("```json");
            builder.AppendLine("{");
            builder.AppendLine("  \"analysisMethod\": \"visual_similarity_v1\",");
            builder.AppendLine("  \"templateNodeIds\": [\"n004\", \"n205\", \"n304\"],");
            builder.AppendLine("  \"scores\": [");
            builder.AppendLine("    {");
            builder.AppendLine("      \"sequence\": 1,");
            builder.AppendLine("      \"nodeIds\": [\"n001\", \"n201\", \"n301\"],");
            builder.AppendLine("      \"similarityScore\": 95,");
            builder.AppendLine("      \"reason\": \"布局完全一致，颜色方案相同，仅文字内容不同\"");
            builder.AppendLine("    },");
            builder.AppendLine("    {");
            builder.AppendLine("      \"sequence\": 4,");
            builder.AppendLine("      \"nodeIds\": [\"n004\", \"n205\", \"n304\"],");
            builder.AppendLine("      \"similarityScore\": 100,");
            builder.AppendLine("      \"reason\": \"模板组本身\"");
            builder.AppendLine("    },");
            builder.AppendLine("    {");
            builder.AppendLine("      \"sequence\": 5,");
            builder.AppendLine("      \"nodeIds\": [\"n005\", \"n206\", \"n305\"],");
            builder.AppendLine("      \"similarityScore\": 65,");
            builder.AppendLine("      \"reason\": \"布局相似但背景颜色不同（红色 vs 蓝色），可能是特殊状态\"");
            builder.AppendLine("    }");
            builder.AppendLine("  ]");
            builder.AppendLine("}");
            builder.AppendLine("```");
            builder.AppendLine();
            builder.AppendLine("**评分标准**：");
            builder.AppendLine("- 90-100: 视觉完全一致，强烈推荐组件化");
            builder.AppendLine("- 80-89: 视觉高度相似，推荐组件化");
            builder.AppendLine("- 60-79: 视觉有差异但可接受，建议用户确认");
            builder.AppendLine("- 0-59: 视觉差异较大，不推荐组件化");

            return builder.ToString();
        }

        /// <summary>
        /// 解析 AI 返回的视觉评分 JSON
        /// </summary>
        internal static bool TryParseVisualScores(
            string aiResponse,
            out PsdHierarchyVisualAnalysisResult result,
            out string error)
        {
            result = null;
            error = string.Empty;

            try
            {
                // 提取 JSON 代码块
                string json = ExtractJsonCodeBlock(aiResponse);
                if (string.IsNullOrWhiteSpace(json))
                {
                    json = aiResponse?.Trim();
                }

                JObject root = JObject.Parse(json);

                string method = root.Value<string>("analysisMethod") ?? "unknown";
                JArray templateIds = root["templateNodeIds"] as JArray;
                JArray scoresArray = root["scores"] as JArray;

                if (scoresArray == null || scoresArray.Count == 0)
                {
                    error = "AI 返回的评分数据为空";
                    return false;
                }

                var templateNodeIds = templateIds?.Values<string>().ToArray() ?? Array.Empty<string>();
                var scores = new List<PsdHierarchyVisualScore>();

                foreach (JObject scoreObj in scoresArray.OfType<JObject>())
                {
                    JArray nodeIds = scoreObj["nodeIds"] as JArray;
                    int score = scoreObj.Value<int?>("similarityScore") ?? 0;
                    string reason = scoreObj.Value<string>("reason") ?? string.Empty;

                    if (nodeIds != null && nodeIds.Count > 0)
                    {
                        // 为每个节点创建评分记录
                        foreach (string nodeId in nodeIds.Values<string>())
                        {
                            scores.Add(new PsdHierarchyVisualScore(nodeId, score, reason));
                        }
                    }
                }

                result = new PsdHierarchyVisualAnalysisResult(templateNodeIds, scores, method);
                return true;
            }
            catch (Exception exception)
            {
                error = "解析视觉评分失败：" + exception.Message;
                return false;
            }
        }

        private static string ExtractJsonCodeBlock(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            // 查找 ```json ... ``` 代码块
            int startMarker = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
            if (startMarker < 0)
            {
                startMarker = text.IndexOf("```", StringComparison.Ordinal);
            }

            if (startMarker < 0)
            {
                return text;
            }

            int jsonStart = text.IndexOf('\n', startMarker);
            if (jsonStart < 0)
            {
                return text;
            }

            int jsonEnd = text.IndexOf("```", jsonStart, StringComparison.Ordinal);
            if (jsonEnd < 0)
            {
                return text.Substring(jsonStart + 1).Trim();
            }

            return text.Substring(jsonStart + 1, jsonEnd - jsonStart - 1).Trim();
        }
    }
}
