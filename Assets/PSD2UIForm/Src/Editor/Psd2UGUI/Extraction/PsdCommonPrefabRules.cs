using System;
using System.Collections.Generic;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    /// <summary>Editor-only durable provenance and instance identities for one generated UI asset.</summary>
    public sealed class PsdCommonPrefabRules : ScriptableObject
    {
        public int version = 1;
        public string targetGuid;
        public string targetPath;
        public string sourceGuid;
        public string sourceFingerprint;
        public List<PsdCommonPrefabRule> rules = new List<PsdCommonPrefabRule>();
    }

    [Serializable]
    public sealed class PsdCommonPrefabRule
    {
        public string commonGuid;
        public List<string> instanceIds = new List<string>();
    }
}
