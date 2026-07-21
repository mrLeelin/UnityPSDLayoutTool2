namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// PSD→Prefab 转换总管线。解析器和 Unity 写入器通过模型与变更计划解耦。
    /// </summary>
    public sealed class PsdPrefabConversionPipeline
    {
        public PsdPrefabConversionPlan CreatePlan(PsdPrefabConversionContext context)
        {
            if (context == null || context.source == null)
            {
                throw new ArgumentNullException("context", "PSD 转换上下文或源模型为空。");
            }

            return new PsdPrefabConversionPlan
            {
                sourceFingerprint = context.source.sourceFingerprint,
                changes = PsdPrefabDiff.Compare(context.previous, context.source)
            };
        }
    }

    [Serializable]
    public sealed class PsdPrefabConversionPlan
    {
        public string sourceFingerprint;
        public List<PsdPrefabNodeChange> changes = new List<PsdPrefabNodeChange>();

        public int Count(PsdPrefabChangeKind kind)
        {
            int count = 0;
            foreach (PsdPrefabNodeChange change in changes)
            {
                if (change.kind == kind)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
