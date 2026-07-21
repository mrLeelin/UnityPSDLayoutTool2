namespace PsdLayoutTool2
{
    using System;
    using TMPro;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Creates and reuses TMP materials for PSD text styles.
    /// </summary>
    internal static class PsdPrefabTextMaterialFactory
    {
        public static Material GetOrCreate(
            PsdPrefabTextModel text,
            TMP_FontAsset font,
            Material baseMaterial,
            string outputFolder)
        {
            if (text == null || font == null)
            {
                return null;
            }

            baseMaterial = baseMaterial != null ? baseMaterial : font.material;
            if (baseMaterial == null)
            {
                return null;
            }

            string fontPath = AssetDatabase.GetAssetPath(font);
            string baseMaterialPath = AssetDatabase.GetAssetPath(baseMaterial);
            string signature = PsdPrefabTextMaterialSignature.Build(text, fontPath, baseMaterialPath);
            string materialFolder = outputFolder + "/TextMaterials";
            EnsureAssetFolder(materialFolder);

            string materialPath = materialFolder + "/PSDTextMaterial_" + ComputeFnv1a(signature) + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (existing != null)
            {
                return existing;
            }

            Material material = new Material(baseMaterial)
            {
                name = System.IO.Path.GetFileNameWithoutExtension(materialPath)
            };
            ApplyMaterialProperties(material, text.effect);
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        }

        private static void ApplyMaterialProperties(Material material, PsdPrefabTextEffectModel effect)
        {
            effect = effect ?? new PsdPrefabTextEffectModel();
            bool hasOutline = effect.hasOutline && effect.outlineWidth > 0.001f;
            bool hasShadow = effect.hasShadow;

            SetMaterialFloat(material, "_OutlineWidth", hasOutline ? effect.outlineWidth : 0f);
            SetMaterialFloat(material, "_FaceDilate", hasOutline ? effect.outlineWidth * 0.5f : 0f);
            SetMaterialColor(material, "_OutlineColor", hasOutline ? effect.outlineColor : Color.black);
            SetMaterialFloat(material, "_UnderlayOffsetX", hasShadow ? effect.shadowOffsetX : 0f);
            SetMaterialFloat(material, "_UnderlayOffsetY", hasShadow ? effect.shadowOffsetY : 0f);
            SetMaterialFloat(material, "_UnderlaySoftness", hasShadow ? effect.shadowSoftness : 0f);
            SetMaterialFloat(material, "_UnderlayDilate", hasShadow ? effect.shadowDilate : 0f);
            SetMaterialColor(material, "_UnderlayColor", hasShadow ? effect.shadowColor : Color.black);
            SetKeyword(material, "OUTLINE_ON", hasOutline);
            SetKeyword(material, "UNDERLAY_ON", hasShadow);
        }

        private static void SetMaterialFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private static void SetMaterialColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, value);
            }
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            string[] parts = assetFolder.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                throw new InvalidOperationException("TMP material output must be inside the Assets folder: " + assetFolder);
            }

            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static string ComputeFnv1a(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char character in value ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }

                return hash.ToString("x8");
            }
        }
    }
}
