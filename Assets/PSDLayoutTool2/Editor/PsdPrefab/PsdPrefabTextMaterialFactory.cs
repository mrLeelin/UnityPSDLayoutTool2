namespace PsdLayoutTool2
{
    using System;
    using System.IO;
    using System.Linq;
    using TMPro;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Rendering;

    /// <summary>
    /// Creates and reuses TMP materials for PSD text styles.
    /// </summary>
    internal static class PsdPrefabTextMaterialFactory
    {
        public static Material GetOrCreate(
            PsdPrefabTextModel text,
            TMP_FontAsset font,
            Material baseMaterial)
        {
            if (text == null || font == null)
            {
                return null;
            }

            baseMaterial = IsCompatibleWithFont(baseMaterial, font) ? baseMaterial : font.material;
            if (baseMaterial == null)
            {
                return null;
            }

            if (CanUseBaseMaterialDirectly(text.effect, baseMaterial))
            {
                return baseMaterial;
            }

            string fontPath = AssetDatabase.GetAssetPath(font);
            string baseMaterialPath = AssetDatabase.GetAssetPath(baseMaterial);
            string signature = PsdPrefabTextMaterialSignature.Build(text, fontPath, baseMaterialPath);
            string materialFolder = GetCommonMaterialFolder(baseMaterial, font);
            EnsureAssetFolder(materialFolder);

            string materialPath = materialFolder + "/" +
                BuildMaterialFileName(font.name, ComputeFnv1a(signature));
            Material material = new Material(baseMaterial);
            ApplyMaterialProperties(material, text.effect, text.fontSize, font);
            try
            {
                for (int variant = 0; ; variant++)
                {
                    string candidatePath = variant == 0
                        ? materialPath
                        : Path.ChangeExtension(materialPath, null) + "_" + variant + ".mat";
                    Material existing = AssetDatabase.LoadAssetAtPath<Material>(candidatePath);
                    if (existing != null)
                    {
                        if (AreMaterialsEquivalent(existing, material))
                        {
                            EnsureMainObjectNameMatchesFileName(existing, candidatePath);
                            return existing;
                        }

                        continue;
                    }

                    material.name = Path.GetFileNameWithoutExtension(candidatePath);
                    AssetDatabase.CreateAsset(material, candidatePath);
                    Material createdMaterial = material;
                    material = null;
                    AssetDatabase.SaveAssetIfDirty(createdMaterial);
                    return createdMaterial;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("PSDLayoutTool2: Failed to create TMP material at " + materialPath + ": " + ex.Message);
                return null;
            }
            finally
            {
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }
        }

        internal static void EnsureMainObjectNameMatchesFileName(Material material, string assetPath)
        {
            if (material == null || string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            string expectedName = Path.GetFileNameWithoutExtension(assetPath);
            if (string.Equals(material.name, expectedName, StringComparison.Ordinal))
            {
                return;
            }

            material.name = expectedName;
            EditorUtility.SetDirty(material);
            if (AssetDatabase.Contains(material))
            {
                AssetDatabase.SaveAssetIfDirty(material);
            }
        }

        /// <summary>
        /// Compares a generated candidate with an existing material without
        /// modifying either asset. Name and hide flags are intentionally ignored.
        /// </summary>
        private static bool AreMaterialsEquivalent(Material existing, Material desired)
        {
            if (existing == null || desired == null || existing.shader != desired.shader ||
                existing.renderQueue != desired.renderQueue ||
                existing.enableInstancing != desired.enableInstancing ||
                existing.doubleSidedGI != desired.doubleSidedGI ||
                existing.globalIlluminationFlags != desired.globalIlluminationFlags)
            {
                return false;
            }

            string[] existingKeywords = existing.shaderKeywords.OrderBy(keyword => keyword, StringComparer.Ordinal).ToArray();
            string[] desiredKeywords = desired.shaderKeywords.OrderBy(keyword => keyword, StringComparer.Ordinal).ToArray();
            if (!existingKeywords.SequenceEqual(desiredKeywords))
            {
                return false;
            }

            Shader shader = desired.shader;
            for (int index = 0; index < shader.GetPropertyCount(); index++)
            {
                int propertyId = shader.GetPropertyNameId(index);
                ShaderPropertyType propertyType = shader.GetPropertyType(index);
                switch (propertyType)
                {
                    case ShaderPropertyType.Color:
                        if (!Approximately(existing.GetColor(propertyId), desired.GetColor(propertyId)))
                        {
                            return false;
                        }
                        break;
                    case ShaderPropertyType.Vector:
                        if (!Approximately(existing.GetVector(propertyId), desired.GetVector(propertyId)))
                        {
                            return false;
                        }
                        break;
                    case ShaderPropertyType.Texture:
                        if (existing.GetTexture(propertyId) != desired.GetTexture(propertyId) ||
                            !Approximately(existing.GetTextureScale(propertyId), desired.GetTextureScale(propertyId)) ||
                            !Approximately(existing.GetTextureOffset(propertyId), desired.GetTextureOffset(propertyId)))
                        {
                            return false;
                        }
                        break;
                    default:
                        if (!Mathf.Approximately(existing.GetFloat(propertyId), desired.GetFloat(propertyId)))
                        {
                            return false;
                        }
                        break;
                }
            }

            return true;
        }

        private static bool Approximately(Color left, Color right)
        {
            return Mathf.Approximately(left.r, right.r) &&
                Mathf.Approximately(left.g, right.g) &&
                Mathf.Approximately(left.b, right.b) &&
                Mathf.Approximately(left.a, right.a);
        }

        private static bool Approximately(Vector4 left, Vector4 right)
        {
            return Mathf.Approximately(left.x, right.x) &&
                Mathf.Approximately(left.y, right.y) &&
                Mathf.Approximately(left.z, right.z) &&
                Mathf.Approximately(left.w, right.w);
        }

        private static bool Approximately(Vector2 left, Vector2 right)
        {
            return Mathf.Approximately(left.x, right.x) &&
                Mathf.Approximately(left.y, right.y);
        }

        /// <summary>
        /// Resolves the shared location for generated TMP material variants.
        /// Prefer the selected base material's folder so all PSD exports reuse
        /// the project's common material library instead of creating a
        /// TextMaterials folder beside every exported PSD.
        /// </summary>
        private static string GetCommonMaterialFolder(Material baseMaterial, TMP_FontAsset font)
        {
            string baseMaterialPath = AssetDatabase.GetAssetPath(baseMaterial);
            if (IsWritableAssetPath(baseMaterialPath))
            {
                return Path.GetDirectoryName(baseMaterialPath).Replace('\\', '/');
            }

            string fontPath = AssetDatabase.GetAssetPath(font);
            if (IsWritableAssetPath(fontPath))
            {
                return Path.GetDirectoryName(fontPath).Replace('\\', '/');
            }

            return "Assets/PSDLayoutTool2Settings/Common";
        }

        private static bool IsWritableAssetPath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                assetPath.StartsWith("Assets/", StringComparison.Ordinal);
        }

        private static void ApplyMaterialProperties(
            Material material,
            PsdPrefabTextEffectModel effect,
            float fontSize)
        {
            ApplyMaterialProperties(material, effect, fontSize, null);
        }

        private static void ApplyMaterialProperties(
            Material material,
            PsdPrefabTextEffectModel effect,
            float fontSize,
            TMP_FontAsset font)
        {
            effect = effect ?? new PsdPrefabTextEffectModel();
            bool hasOutline = effect.hasOutline && effect.outlineWidth > 0.001f;
            bool hasShadow = effect.hasShadow;
            // 这里只计算候选材质应该具有的文字效果参数。
            // GetOrCreate 会先用这些参数与已有材质做只读比较：完全匹配时直接复用，
            // 不匹配时创建新的材质变体，绝不会把计算结果回写到已有字体材质。
            float outlineWidth = PsdTextEffectConversion.ConvertOutline(
                effect.outlineWidth,
                fontSize);
            float pointSize = font == null ? fontSize : font.faceInfo.pointSize;
            float gradientScale = material.HasProperty("_GradientScale")
                ? material.GetFloat("_GradientScale")
                : 1f;
            float shadowOffsetX = PsdTextEffectConversion.ConvertUnderlayPixelValue(
                effect.shadowOffsetX,
                fontSize,
                pointSize,
                gradientScale,
                true);
            float shadowOffsetY = PsdTextEffectConversion.ConvertUnderlayPixelValue(
                effect.shadowOffsetY,
                fontSize,
                pointSize,
                gradientScale,
                true);
            float shadowSoftness = PsdTextEffectConversion.ConvertUnderlayPixelValue(
                effect.shadowSoftness,
                fontSize,
                pointSize,
                gradientScale);
            float shadowDilate = PsdTextEffectConversion.ConvertUnderlayPixelValue(
                effect.shadowDilate,
                fontSize,
                pointSize,
                gradientScale,
                true);

            SetMaterialFloat(material, "_OutlineWidth", hasOutline ? outlineWidth : 0f);
            SetMaterialFloat(
                material,
                "_FaceDilate",
                hasOutline ? PsdTextEffectConversion.ConvertFaceDilate(outlineWidth) : 0f);
            SetMaterialColor(material, "_OutlineColor", hasOutline ? effect.outlineColor : Color.black);
            SetMaterialFloat(material, "_UnderlayOffsetX", hasShadow ? shadowOffsetX : 0f);
            SetMaterialFloat(material, "_UnderlayOffsetY", hasShadow ? shadowOffsetY : 0f);
            SetMaterialFloat(material, "_UnderlaySoftness", hasShadow ? shadowSoftness : 0f);
            SetMaterialFloat(material, "_UnderlayDilate", hasShadow ? shadowDilate : 0f);
            SetMaterialColor(material, "_UnderlayColor", hasShadow ? effect.shadowColor : Color.black);
            SetKeyword(material, "OUTLINE_ON", hasOutline);
            SetKeyword(material, "UNDERLAY_ON", hasShadow);
        }

        internal static bool CanUseBaseMaterialDirectly(
            PsdPrefabTextEffectModel effect,
            Material baseMaterial)
        {
            if (baseMaterial == null)
            {
                return false;
            }

            effect = effect ?? new PsdPrefabTextEffectModel();
            bool hasOutline = effect.hasOutline && effect.outlineWidth > 0.001f;
            return !hasOutline &&
                !effect.hasShadow &&
                !baseMaterial.IsKeywordEnabled("OUTLINE_ON") &&
                !baseMaterial.IsKeywordEnabled("UNDERLAY_ON") &&
                !baseMaterial.IsKeywordEnabled("UNDERLAY_INNER");
        }

        internal static bool IsCompatibleWithFont(Material material, TMP_FontAsset font)
        {
            if (material == null || font == null)
            {
                return false;
            }

            try
            {
                Texture atlas = font.atlasTexture;
                return atlas != null && material.mainTexture == atlas;
            }
            catch (NullReferenceException)
            {
                // A newly created TMP font can have no initialized atlas yet.
                return false;
            }
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

            // Build the folder hierarchy one segment at a time using AssetDatabase.CreateFolder
            // so Unity immediately recognises each intermediate folder.
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }

            if (!AssetDatabase.IsValidFolder(assetFolder))
            {
                // Last-resort: create on disk and force a refresh
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string absoluteFolder = Path.Combine(
                    projectRoot,
                    assetFolder.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(absoluteFolder);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
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

        internal static string BuildMaterialFileName(string fontName, string signatureHash)
        {
            string source = string.IsNullOrWhiteSpace(fontName) ? "TMPFont" : fontName.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            string prefix = new string(source
                .Select(character => invalid.Contains(character) ? '_' : character)
                .ToArray())
                .Trim()
                .TrimEnd('.');
            if (string.IsNullOrEmpty(prefix))
            {
                prefix = "TMPFont";
            }

            return prefix + "_PSDTextMaterial_" + (signatureHash ?? string.Empty) + ".mat";
        }
    }
}
