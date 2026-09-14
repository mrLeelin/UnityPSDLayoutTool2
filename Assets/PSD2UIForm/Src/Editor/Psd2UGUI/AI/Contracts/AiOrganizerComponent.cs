using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiOrganizerComponent
    {
        public string name;
        public string mode = "same";
        public string[] rootIds;
        public string reason;
    }

    [Serializable]
    internal sealed class AiOrganizerRename
    {
        public string nodeId;
        public string name;
        public string reason;
    }
}
