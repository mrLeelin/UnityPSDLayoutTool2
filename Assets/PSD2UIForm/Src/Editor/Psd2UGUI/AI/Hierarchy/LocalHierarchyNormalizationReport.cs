using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace LocalHierarchyNormalizationReportNamespace
{
    internal sealed class LocalHierarchyNormalizationReport
    {
        internal int MovedNodeCount;

        internal int BackgroundDowngradeCount;

        internal int FlattenedNullGroupCount;

        internal readonly List<string> Warnings = new List<string>();

        internal readonly List<string> Actions = new List<string>();

        private static LocalHierarchyNormalizationReport s_ObfuscationSentinel;

        [SpecialName]
        internal bool HasChanges()
        {
            if (MovedNodeCount <= 0 && BackgroundDowngradeCount <= 0)
            {
                return FlattenedNullGroupCount > 0;
            }
            return true;
        }

        internal string BuildSummary()
        {
            StringBuilder stringBuilder = new StringBuilder(256);
            stringBuilder.AppendLine("本地归一化完成。");
            stringBuilder.AppendLine($"移动子控件: {MovedNodeCount}");
            stringBuilder.AppendLine($"Background 降级为 Image: {BackgroundDowngradeCount}");
            stringBuilder.AppendLine($"扁平化 Null 包裹层: {FlattenedNullGroupCount}");
            if (Warnings.Count > 0)
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("警告:");
                for (int i = 0; i < Warnings.Count; i++)
                {
                    stringBuilder.AppendLine("- " + Warnings[i]);
                }
            }
            return stringBuilder.ToString().TrimEnd();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LocalHierarchyNormalizationReport GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
