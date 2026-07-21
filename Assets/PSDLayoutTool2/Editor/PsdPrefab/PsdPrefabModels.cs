namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>统一的 PSD 到 Prefab 中间文档模型。</summary>
    [Serializable]
    public sealed class PsdPrefabDocumentModel
    {
        public int schemaVersion = 1;
        public string sourceFingerprint;
        public int width;
        public int height;
        public float resolution;
        public List<PsdPrefabNodeModel> nodes = new List<PsdPrefabNodeModel>();
    }

    /// <summary>一个 PSD 节点的稳定、可比较表示。</summary>
    [Serializable]
    public sealed class PsdPrefabNodeModel
    {
        public string stableId;
        public string parentStableId;
        public int siblingIndex;
        public string name;
        public PsdPrefabNodeKind kind;
        public bool visible = true;
        public float opacity = 1f;
        public Rect bounds;
        public string contentFingerprint;
        public string assetFingerprint;
        public PsdPrefabTextModel text;
    }

    /// <summary>文字内容和可渲染样式的中间表示。</summary>
    [Serializable]
    public sealed class PsdPrefabTextModel
    {
        public string contents;
        public string fontFamily;
        public float fontSize;
        public Color fillColor = Color.white;
        public float lineHeight;
        public PsdPrefabTextEffectModel effect = new PsdPrefabTextEffectModel();
    }

    /// <summary>TMP 材质需要的描边和阴影参数。</summary>
    [Serializable]
    public sealed class PsdPrefabTextEffectModel
    {
        public bool hasOutline;
        public Color outlineColor = Color.black;
        public float outlineWidth;
        public bool hasShadow;
        public Color shadowColor = Color.black;
        public float shadowOffsetX;
        public float shadowOffsetY;
        public float shadowSoftness;
        public float shadowDilate;
    }

    /// <summary>一次转换所需的可替换策略。</summary>
    public sealed class PsdPrefabConversionContext
    {
        public PsdPrefabDocumentModel source;
        public PsdPrefabDocumentModel previous;
        public PsdPrefabConversionOptions options = new PsdPrefabConversionOptions();
        public PsdPrefabResourceCache resources = new PsdPrefabResourceCache();
    }

    [Serializable]
    public sealed class PsdPrefabConversionOptions
    {
        public string selectedFontAssetPath;
        public string baseTextMaterialPath;
        public string outputFolder;
        public bool useTextMeshPro = true;
        public bool preserveUnchangedAssets = true;
    }

    /// <summary>以参数签名为键的资源缓存，避免相同字体材质重复生成。</summary>
    public sealed class PsdPrefabResourceCache
    {
        private readonly Dictionary<string, string> assetPathBySignature = new Dictionary<string, string>(StringComparer.Ordinal);

        public bool TryGet(string signature, out string assetPath)
        {
            return assetPathBySignature.TryGetValue(signature ?? string.Empty, out assetPath);
        }

        public void Set(string signature, string assetPath)
        {
            assetPathBySignature[signature ?? string.Empty] = assetPath ?? string.Empty;
        }
    }
}
