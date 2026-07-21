namespace PsdLayoutTool2
{
    using UnityEngine;

    /// <summary>
    /// 写入生成 Prefab 的节点身份，使后续增量更新不依赖 GameObject 名称。
    /// </summary>
    public sealed class PsdPrefabNodeIdentity : MonoBehaviour
    {
        [SerializeField] private string stableId;
        [SerializeField] private string sourceFingerprint;
        [SerializeField] private string contentFingerprint;
        [SerializeField] private PsdPrefabNodeKind kind;

        public string StableId { get { return stableId; } }
        public string SourceFingerprint { get { return sourceFingerprint; } }
        public string ContentFingerprint { get { return contentFingerprint; } }
        public PsdPrefabNodeKind Kind { get { return kind; } }

        public void Set(string nodeStableId, string source, string content, PsdPrefabNodeKind nodeKind)
        {
            stableId = nodeStableId ?? string.Empty;
            sourceFingerprint = source ?? string.Empty;
            contentFingerprint = content ?? string.Empty;
            kind = nodeKind;
        }
    }
}
