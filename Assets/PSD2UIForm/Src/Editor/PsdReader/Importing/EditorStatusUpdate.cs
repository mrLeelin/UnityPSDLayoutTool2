using UnityEngine;

namespace cn.efunstudio.psdreader
{
    internal class EditorStatusUpdate : CustomYieldInstruction
    {
        public string Label;

        public float PercentComplete;

        public bool HasLabelUpdate;

        public bool HasPercentUpdate;

        internal static EditorStatusUpdate s_ObfuscationSentinel;

        public override bool keepWaiting => false;

        public EditorStatusUpdate(string label)
        {
            HasPercentUpdate = false;
            HasLabelUpdate = true;
            Label = label;
        }

        public EditorStatusUpdate(float percent)
        {
            HasPercentUpdate = true;
            PercentComplete = percent;
            HasLabelUpdate = false;
        }

        public EditorStatusUpdate(string label, float percent)
        {
            HasPercentUpdate = true;
            PercentComplete = percent;
            HasLabelUpdate = true;
            Label = label;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static EditorStatusUpdate GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
