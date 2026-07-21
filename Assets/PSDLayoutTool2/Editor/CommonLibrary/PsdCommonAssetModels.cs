namespace PsdLayoutTool2
{
    /// <summary>
    /// The public Unity asset type requested by a hard Common_* PSD rule.
    /// </summary>
    public enum PsdCommonAssetKind
    {
        Prefab,
        Texture
    }

    /// <summary>
    /// Immutable result of parsing one PSD layer name into a public asset key.
    /// </summary>
    public sealed class PsdCommonAssetReference
    {
        public PsdCommonAssetReference(PsdCommonAssetKind kind, string key)
        {
            Kind = kind;
            Key = key ?? string.Empty;
        }

        public PsdCommonAssetKind Kind { get; private set; }
        public string Key { get; private set; }
    }
}
