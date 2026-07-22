namespace PsdLayoutTool2
{
    using System;
    using UnityEngine;

    /// <summary>统一字体、描边和阴影的材质签名，规则对齐 UnityBridge。</summary>
    public static class PsdPrefabTextMaterialSignature
    {
        public static string Build(PsdPrefabTextModel text, string fontAssetPath, string baseMaterialPath)
        {
            if (text == null)
            {
                return string.Empty;
            }

            PsdPrefabTextEffectModel effect = text.effect ?? new PsdPrefabTextEffectModel();
            return string.Join("|", new[]
            {
                fontAssetPath ?? string.Empty,
                baseMaterialPath ?? string.Empty,
                Mathf.RoundToInt(text.fontSize * 100f).ToString("000000"),
                effect.hasOutline ? "outline" : "no-outline",
                ColorUtility.ToHtmlStringRGBA(effect.outlineColor),
                Mathf.RoundToInt(effect.outlineWidth * 100f).ToString("0000"),
                effect.hasShadow ? "shadow" : "no-shadow",
                ColorUtility.ToHtmlStringRGBA(effect.shadowColor),
                Mathf.RoundToInt(effect.shadowOffsetX * 100f).ToString("0000"),
                Mathf.RoundToInt(effect.shadowOffsetY * 100f).ToString("0000"),
                Mathf.RoundToInt(effect.shadowSoftness * 100f).ToString("0000")
            });
        }
    }
}
