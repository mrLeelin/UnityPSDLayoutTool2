using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiAnalysisNodeEntry
    {
        public string id;

        public string shortId;

        public string parentId;

        public string[] childIds;

        public string onlyChildId;

        public string idPath;

        public string displayPath;

        public string name;

        public string layerName;

        public string[] nameTokens;

        public string layerType;

        public bool isGroupLayer;

        public bool isTextLayer;

        public bool isGeneratedNode;

        public string uiType;

        public RectData rect;

        public string imageFile;

        public string previewKind;

        public string previewSourceId;

        public string visualHash;

        public int renderLeafCount;

        public AiAtlasRef atlas;

        public string suffixMatch;

        public int siblingIndex;

        public int childCount;

        internal static AiAnalysisNodeEntry s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiAnalysisNodeEntry GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
