using System.IO;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;
namespace UGF.EditorTools.Psd2UGUI
{
    internal sealed class RightClickExtension
    {
        private const string CropMinimalNineSliceMenuPath = "Assets/Psd2UIForm/Crop Minimal 9-Slice";

        internal static RightClickExtension s_ObfuscationSentinel;

        [MenuItem("Assets/Psd2UIForm/Auto Sprite Border", priority = 1004)]
        private static void AutoSpriteSliceBorder()
        {
            string[] assetGUIDs = Selection.assetGUIDs;
            for (int i = 0; i < assetGUIDs.Length; i++)
            {
                string text = AssetDatabase.GUIDToAssetPath(assetGUIDs[i]);
                AssetImporter atPath = AssetImporter.GetAtPath(text);
                TextureImporter val = (TextureImporter)(object)((atPath is TextureImporter) ? atPath : null);
                if (!((Object)(object)val == (Object)null) && (int)val.textureType == 8)
                {
                    bool isReadable;
                    if (!(isReadable = val.isReadable))
                    {
                        val.isReadable = true;
                        ((AssetImporter)val).SaveAndReimport();
                    }
                    Sprite val2 = AssetDatabase.LoadAssetAtPath<Sprite>(text);
                    if ((Object)(object)val2 != (Object)null && (Object)(object)val2.texture != (Object)null)
                    {
                        val.spriteBorder = UGUIParser.CalculateNineSliceBorder(val2.texture, 0);
                    }
                    val.isReadable = isReadable;
                    ((AssetImporter)val).SaveAndReimport();
                }
            }
        }

        [MenuItem("Assets/Psd2UIForm/Crop Minimal 9-Slice", priority = 1005)]
        private static void CropMinimalNineSlice()
        {
            int num = 0;
            int num2 = 0;
            string[] assetGUIDs = Selection.assetGUIDs;
            for (int i = 0; i < assetGUIDs.Length; i++)
            {
                string text = AssetDatabase.GUIDToAssetPath(assetGUIDs[i]);
                if (!AssetDatabase.IsValidFolder(text))
                {
                    if (!TryCropMinimalNineSlice(text))
                    {
                        num2++;
                    }
                    else
                    {
                        num++;
                    }
                    continue;
                }
                string[] array = AssetDatabase.FindAssets("t:Texture2D", new string[1] { text });
                for (int j = 0; j < array.Length; j++)
                {
                    if (TryCropMinimalNineSlice(AssetDatabase.GUIDToAssetPath(array[j])))
                    {
                        num++;
                    }
                    else
                    {
                        num2++;
                    }
                }
            }
            if (num > 0)
            {
                Debug.Log((object)$"Crop Minimal 9-Slice finished. Success:{num} Skip:{num2}");
            }
        }

        [MenuItem("Assets/Psd2UIForm/Crop Minimal 9-Slice", true)]
        private static bool ValidateCropMinimalNineSlice()
        {
            string[] assetGUIDs = Selection.assetGUIDs;
            int num = 0;
            while (true)
            {
                if (num < assetGUIDs.Length)
                {
                    string text = AssetDatabase.GUIDToAssetPath(assetGUIDs[num]);
                    if (AssetDatabase.IsValidFolder(text))
                    {
                        return true;
                    }
                    AssetImporter atPath = AssetImporter.GetAtPath(text);
                    TextureImporter val = (TextureImporter)(object)((atPath is TextureImporter) ? atPath : null);
                    if ((Object)(object)val != (Object)null && (int)val.textureType == 8 && (int)val.spriteImportMode == 1)
                    {
                        break;
                    }
                    num++;
                    continue;
                }
                return false;
            }
            return true;
        }

        internal static bool TryCropMinimalNineSlice(string assetPath)
        {
            AssetImporter atPath = AssetImporter.GetAtPath(assetPath);
            TextureImporter val = (TextureImporter)(object)((atPath is TextureImporter) ? atPath : null);
            if ((Object)(object)val == (Object)null || (int)val.textureType != 8)
            {
                return false;
            }
            if ((int)val.spriteImportMode != 1)
            {
                Debug.LogWarning((object)("Crop Minimal 9-Slice only supports Single Sprite: " + assetPath));
                return false;
            }
            Vector4 spriteBorder = val.spriteBorder;
            int num = Mathf.RoundToInt(spriteBorder.x);
            int num2 = Mathf.RoundToInt(spriteBorder.y);
            int num3 = Mathf.RoundToInt(spriteBorder.z);
            int num4 = Mathf.RoundToInt(spriteBorder.w);
            if (!(spriteBorder == Vector4.zero) && num >= 0 && num2 >= 0 && num3 >= 0 && num4 >= 0)
            {
                string assetFullPath = GetAssetFullPath(assetPath);
                if (!File.Exists(assetFullPath))
                {
                    Debug.LogWarning((object)("Sprite file not found: " + assetPath));
                    return false;
                }
                Texture2D val2 = null;
                Texture2D val3 = null;
                try
                {
                    val2 = new Texture2D(2, 2, (TextureFormat)4, false);
                    if (!ImageConversion.LoadImage(val2, File.ReadAllBytes(assetFullPath), false))
                    {
                        Debug.LogWarning((object)("Load sprite source failed: " + assetPath));
                        return false;
                    }
                    int width = ((Texture)val2).width;
                    int height = ((Texture)val2).height;
                    int num5 = width - num - num3;
                    int num6 = height - num2 - num4;
                    if (num5 > 0 && num6 > 0)
                    {
                        bool flag = num5 > 1;
                        bool flag2 = num6 > 1;
                        if (!flag && !flag2)
                        {
                            Debug.LogWarning((object)("Sprite center area is already minimal: " + assetPath));
                            return false;
                        }
                        int num7 = (flag ? (num + 1 + num3) : width);
                        int num8 = (flag2 ? (num2 + 1 + num4) : height);
                        int centerIndex = num + (num5 - 1) / 2;
                        int centerIndex2 = num2 + (num6 - 1) / 2;
                        Color32[] pixels = val2.GetPixels32();
                        Color32[] array = (Color32[])(object)new Color32[num7 * num8];
                        for (int i = 0; i < num8; i++)
                        {
                            int num9 = MapCollapsedAxisIndex(i, flag2, num2, num6, centerIndex2) * width;
                            int num10 = i * num7;
                            for (int j = 0; j < num7; j++)
                            {
                                int num11 = MapCollapsedAxisIndex(j, flag, num, num5, centerIndex);
                                array[num10 + j] = pixels[num9 + num11];
                            }
                        }
                        val3 = new Texture2D(num7, num8, (TextureFormat)4, false)
                        {
                            alphaIsTransparency = val2.alphaIsTransparency,
                            filterMode = ((Texture)val2).filterMode,
                            wrapMode = ((Texture)val2).wrapMode,
                            anisoLevel = ((Texture)val2).anisoLevel
                        };
                        val3.SetPixels32(array);
                        val3.Apply();
                        byte[] array2 = EncodeTextureBytes(assetPath, val3);
                        if (array2 != null && array2.Length != 0)
                        {
                            File.WriteAllBytes(assetFullPath, array2);
                            AssetDatabase.ImportAsset(assetPath, (ImportAssetOptions)8);
                            AssetImporter atPath2 = AssetImporter.GetAtPath(assetPath);
                            val = (TextureImporter)(object)((atPath2 is TextureImporter) ? atPath2 : null);
                            if ((Object)(object)val == (Object)null)
                            {
                                Debug.LogWarning((object)("TextureImporter reload failed: " + assetPath));
                                return false;
                            }
                            Vector4 val4 = default(Vector4);
                            val4 = new Vector4((float)num, (float)num2, (float)num3, (float)num4);
                            if (val.spriteBorder != val4)
                            {
                                val.spriteBorder = val4;
                                ((AssetImporter)val).SaveAndReimport();
                            }
                            Debug.Log((object)$"Crop Minimal 9-Slice success: {assetPath} ({width}x{height} -> {num7}x{num8})");
                            return true;
                        }
                        Debug.LogWarning((object)("Unsupported texture format for Crop Minimal 9-Slice: " + assetPath));
                        return false;
                    }
                    Debug.LogWarning((object)("Sprite Border exceeds texture size: " + assetPath));
                    return false;
                }
                finally
                {
                    if ((Object)(object)val2 != (Object)null)
                    {
                        Object.DestroyImmediate((Object)(object)val2);
                    }
                    if ((Object)(object)val3 != (Object)null)
                    {
                        Object.DestroyImmediate((Object)(object)val3);
                    }
                }
            }
            Debug.LogWarning((object)("Sprite Border is invalid or empty: " + assetPath));
            return false;
        }

        private static int MapCollapsedAxisIndex(int targetIndex, bool cropAxis, int stretchStart, int stretchLength, int centerIndex)
        {
            if (cropAxis)
            {
                if (targetIndex >= stretchStart)
                {
                    if (targetIndex == stretchStart)
                    {
                        return centerIndex;
                    }
                    return stretchStart + stretchLength + (targetIndex - stretchStart - 1);
                }
                return targetIndex;
            }
            return targetIndex;
        }

        private static byte[] EncodeTextureBytes(string assetPath, Texture2D texture)
        {
            switch (Path.GetExtension(assetPath)?.ToLowerInvariant())
            {
            case ".jpg":
            case ".jpeg":
                return ImageConversion.EncodeToJPG(texture, 100);
            default:
                return null;
            case ".png":
                return ImageConversion.EncodeToPNG(texture);
            }
        }

        private static string GetAssetFullPath(string assetPath)
        {
            string text = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(text))
            {
                return assetPath;
            }
            return Path.GetFullPath(Path.Combine(text, assetPath));
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static RightClickExtension GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
