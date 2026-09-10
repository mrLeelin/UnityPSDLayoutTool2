using System;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    public class UGUIParseRule
    {
        public GUIType UIType;

        public string UITypeDesc;

        public string[] TypeMatches;

        public GameObject UIPrefab;

        public string UIHelper;

        public string Comment;

        internal static UGUIParseRule s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static UGUIParseRule GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
