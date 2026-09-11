using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using cn.efunstudio.psdreader.PsdParser;
using cn.efunstudio.psdreader.Reconstructor;
using Object = UnityEngine.Object;

namespace cn.efunstudio.psdreader
{
    internal class PsdImporter
    {
        private const string DOC_ROOT = "DOCUMENT_ROOT";

        private const float DefaultSourcePixelsPerUnit = 100f;

        internal static PsdImporter s_ObfuscationSentinel;

        private static bool IsSupportedSourceFile(string filepath)
        {
            if (!string.IsNullOrEmpty(filepath))
            {
                if (!filepath.EndsWith(".psd", StringComparison.OrdinalIgnoreCase))
                {
                    return filepath.EndsWith(".psb", StringComparison.OrdinalIgnoreCase);
                }
                return true;
            }
            return false;
        }

        private static TextureImporterSettings GetSourceTextureImportSettings(string filepath, out float sourcePixelsPerUnit)
        {
            TextureImporterSettings val = new TextureImporterSettings();
            sourcePixelsPerUnit = 100f;
            AssetImporter atPath = AssetImporter.GetAtPath(filepath);
            TextureImporter val2 = (TextureImporter)(object)((atPath is TextureImporter) ? atPath : null);
            if (!((Object)(object)val2 != (Object)null))
            {
                val.spritePixelsPerUnit = 100f;
                val.filterMode = (FilterMode)1;
                val.wrapMode = (TextureWrapMode)1;
                val.textureType = (TextureImporterType)8;
                val.spriteMode = 1;
                val.mipmapEnabled = false;
                val.alphaIsTransparency = true;
                val.npotScale = (TextureImporterNPOTScale)0;
                return val;
            }
            val2.ReadTextureSettings(val);
            sourcePixelsPerUnit = val2.spritePixelsPerUnit;
            return val;
        }

        private static string GetPsdFilepath(Object psdFile)
        {
            string assetPath = AssetDatabase.GetAssetPath(psdFile);
            if (string.IsNullOrEmpty(assetPath))
            {
                return string.Empty;
            }
            if (!IsSupportedSourceFile(assetPath))
            {
                return string.Empty;
            }
            return assetPath;
        }

        private static IEnumerator ParseLayers(PsdLayer[] layers, bool doYield, Action<PsdLayer, int[]> onLayer, Action onComplete, int[] parentIndex = null)
        {
            for (int i = layers.Length - 1; i >= 0; i--)
            {
                int[] layerIndex = parentIndex;
                if (layerIndex != null)
                {
                    int num = layerIndex.Length;
                    Array.Resize(ref layerIndex, num + 1);
                    layerIndex[num] = i;
                }
                else
                {
                    layerIndex = new int[1] { i };
                }
                PsdLayer layer = layers[i];
                if (layer != null)
                {
                    onLayer?.Invoke(layer, layerIndex);
                    if (doYield)
                    {
                        yield return null;
                    }
                    if (layer.Childs.Length != 0)
                    {
                        yield return EditorCoroutineRunner.StartCoroutine(ParseLayers(layer.Childs, doYield, onLayer, null, layerIndex));
                    }
                }
            }
            onComplete?.Invoke();
        }

        public static void BuildImportLayerData(Object file, ImportUserData importSettings, Action<ImportLayerData, DisplayLayerData> callback)
        {
            string psdFilepath = GetPsdFilepath(file);
            if (string.IsNullOrEmpty(psdFilepath))
            {
                if (callback != null)
                {
                    callback(null, null);
                }
                return;
            }
            using PsdDocument psdDocument = PsdDocument.Create(psdFilepath);
            ImportLayerData docImportData = new ImportLayerData
            {
                name = "DOCUMENT_ROOT",
                indexId = new int[1] { -1 },
                Childs = new List<ImportLayerData>()
            };
            DisplayLayerData docDisplayData = new DisplayLayerData
            {
                indexId = new int[1] { -1 },
                Childs = new List<DisplayLayerData>()
            };
            EditorCoroutineRunner.StartCoroutine(ParseLayers(psdDocument.Childs, doYield: false, delegate(PsdLayer layer, int[] indexId)
            {
                string text = "";
                ImportLayerData importLayerData = docImportData;
                DisplayLayerData displayLayerData = docDisplayData;
                if (indexId.Length > 1)
                {
                    for (int i = 0; i < indexId.Length - 1; i++)
                    {
                        int index = indexId[i];
                        importLayerData = importLayerData.Childs[index];
                        displayLayerData = displayLayerData.Childs[index];
                        if (!string.IsNullOrEmpty(text))
                        {
                            text += "/";
                        }
                        text += importLayerData.name;
                    }
                }
                if (!string.IsNullOrEmpty(text))
                {
                    text += "/";
                }
                text += layer.Name;
                ImportLayerData value = new ImportLayerData
                {
                    name = layer.Name,
                    path = text,
                    indexId = indexId,
                    import = layer.IsVisible,
                    useDefaults = true,
                    Alignment = importSettings.DefaultAlignment,
                    Pivot = importSettings.DefaultPivot,
                    ScaleFactor = importSettings.ScaleFactor,
                    Childs = new List<ImportLayerData>()
                };
                DisplayLayerData value2 = new DisplayLayerData
                {
                    indexId = indexId,
                    isVisible = layer.IsVisible,
                    isGroup = (layer.Childs.Length != 0),
                    isOpen = layer.IsFolderOpen
                };
                int num = indexId[indexId.Length - 1];
                int num2 = num + 1;
                while (importLayerData.Childs.Count < num2)
                {
                    importLayerData.Childs.Add(null);
                }
                importLayerData.Childs[num] = value;
                while (displayLayerData.Childs.Count < num2)
                {
                    displayLayerData.Childs.Add(null);
                }
                displayLayerData.Childs[num] = value2;
            }, delegate
            {
                if (callback != null)
                {
                    callback(docImportData, docDisplayData);
                }
            }));
        }

        private static PsdLayer GetPsdLayerByIndex(PsdDocument psdDoc, int[] layerIdx)
        {
            if (psdDoc != null && layerIdx != null && layerIdx.Length != 0)
            {
                PsdLayer[] childs = psdDoc.Childs;
                PsdLayer psdLayer = null;
                foreach (int num in layerIdx)
                {
                    if (childs != null && num >= 0 && num < childs.Length)
                    {
                        psdLayer = childs[num];
                        if (psdLayer != null)
                        {
                            childs = psdLayer.Childs;
                            continue;
                        }
                        return null;
                    }
                    return null;
                }
                return psdLayer;
            }
            return null;
        }

        public static Texture2D GetLayerTexture(Object psdFile, int[] layerIdx)
        {
            ImportLayerData setting = new ImportLayerData
            {
                Alignment = (SpriteAlignment)0,
                Pivot = new Vector2(0.5f, 0.5f),
                ScaleFactor = ScaleFactor.Full,
                Childs = new List<ImportLayerData>(),
                import = true,
                indexId = layerIdx
            };
            return GetLayerTexture(psdFile, setting);
        }

        public static Texture2D GetLayerTexture(Object psdFile, ImportLayerData setting)
        {
            string psdFilepath = GetPsdFilepath(psdFile);
            if (!string.IsNullOrEmpty(psdFilepath))
            {
                using (PsdDocument psdDoc = PsdDocument.Create(psdFilepath))
                {
                    PsdLayer psdLayerByIndex = GetPsdLayerByIndex(psdDoc, setting.indexId);
                    return GetLayerTexture(psdDoc, psdLayerByIndex, setting);
                }
            }
            return null;
        }

        private static Texture2D GetLayerTexture(PsdDocument psdDoc, PsdLayer psdLayer, ImportLayerData setting)
        {
            if (psdLayer == null)
            {
                return null;
            }
            Texture2D val = GetTexture(psdLayer);
            if (setting.ScaleFactor != ScaleFactor.Full)
            {
                int mipLevel = ((setting.ScaleFactor == ScaleFactor.Half) ? 1 : 2);
                val = ScaleTextureByMipmap(val, mipLevel);
            }
            return val;
        }

        private static Texture2D GetTexture(PsdLayer layer)
        {
            PsdRenderedImage psdRenderedImage = layer.Render();
            if (psdRenderedImage != null && !psdRenderedImage.IsEmpty)
            {
                TextureFormat val = (TextureFormat)(psdRenderedImage.IsHighBitDepth ? 74 : 4);
                Texture2D val2 = new Texture2D(psdRenderedImage.Width, psdRenderedImage.Height, val, true);
                if (!psdRenderedImage.IsHighBitDepth)
                {
                    val2.LoadRawTextureData(psdRenderedImage.GetExportRgba32());
                }
                else
                {
                    ushort[] exportRgba = psdRenderedImage.GetExportRgba64();
                    byte[] array = new byte[exportRgba.Length * 2];
                    Buffer.BlockCopy(exportRgba, 0, array, 0, array.Length);
                    val2.LoadRawTextureData(array);
                }
                val2.Apply(true, false);
                return val2;
            }
            return null;
        }

        private static Texture2D ScaleTextureByMipmap(Texture2D tex, int mipLevel)
        {
            if (mipLevel >= 0 && mipLevel <= 2)
            {
                int num = Mathf.RoundToInt((float)(((Texture)tex).width / (mipLevel * 2)));
                int num2 = Mathf.RoundToInt((float)(((Texture)tex).height / (mipLevel * 2)));
                TextureFormat val = (TextureFormat)(((int)tex.format == 74) ? 74 : 4);
                Texture2D val2 = new Texture2D(num, num2, val, false);
                if ((int)val == 74)
                {
                    val2.SetPixels(tex.GetPixels(mipLevel));
                }
                else
                {
                    val2.SetPixels32(tex.GetPixels32(mipLevel));
                }
                val2.Apply();
                return val2;
            }
            return null;
        }

        public static void ImportLayersUI(Object psdFile, ImportUserData importSettings, List<int[]> layerIndices)
        {
            int total = layerIndices.Count;
            EditorCoroutineRunner.StartCoroutineWithUI(ImportCoroutine(psdFile, importSettings, layerIndices, doYield: true, delegate(int current, ImportLayerData layer)
            {
                string label = $"[{current}/{total}] Layer: {layer.name}";
                float percent = (float)current / (float)total;
                EditorCoroutineRunner.UpdateUI(label, percent);
            }), "Importing PSD Layers", isCancelable: true);
        }

        public static void ImportLayers(Object psdFile, ImportUserData importSettings, List<int[]> layerIndices, Action<List<Sprite>> callback = null)
        {
            EditorCoroutineRunner.StartCoroutine(ImportCoroutine(psdFile, importSettings, layerIndices, doYield: false, null, callback));
        }

        private static IEnumerator ImportCoroutine(Object psdFile, ImportUserData importSettings, List<int[]> layerIndices, bool doYield = false, Action<int, ImportLayerData> layerCallback = null, Action<List<Sprite>> completeCallback = null)
        {
            string psdFilepath = GetPsdFilepath(psdFile);
            if (!string.IsNullOrEmpty(psdFilepath))
            {
                if (string.IsNullOrEmpty(importSettings.TargetDirectory))
                {
                    importSettings.TargetDirectory = psdFilepath.Substring(0, psdFilepath.LastIndexOf("/"));
                }
                float sourcePixelsPerUnit;
                TextureImporterSettings psdUnityImport = GetSourceTextureImportSettings(psdFilepath, out sourcePixelsPerUnit);
                int importCurrent = 0;
                List<Sprite> sprites = new List<Sprite>();
                using (PsdDocument psd = PsdDocument.Create(psdFilepath))
                {
                    foreach (int[] layerIndex in layerIndices)
                    {
                        ImportLayerData layerData = importSettings.GetLayerData(layerIndex);
                        if (layerData != null)
                        {
                            layerCallback?.Invoke(importCurrent, layerData);
                            Sprite item = ImportLayer(psd, importSettings, layerData, psdUnityImport);
                            sprites.Add(item);
                            importCurrent++;
                            if (doYield)
                            {
                                yield return null;
                            }
                        }
                    }
                }
                completeCallback?.Invoke(sprites);
            }
            else
            {
                completeCallback?.Invoke(null);
            }
        }

        private static Sprite ImportLayer(PsdDocument psdDoc, ImportUserData importSettings, ImportLayerData layerSettings, TextureImporterSettings psdUnityImport)
        {
            if (layerSettings != null)
            {
                PsdLayer psdLayerByIndex = GetPsdLayerByIndex(psdDoc, layerSettings.indexId);
                Texture2D layerTexture = GetLayerTexture(psdDoc, psdLayerByIndex, layerSettings);
                if ((Object)(object)layerTexture == (Object)null)
                {
                    return null;
                }
                return SaveAsset(psdLayerByIndex, psdUnityImport, layerTexture, importSettings, layerSettings);
            }
            return null;
        }

        private static Sprite SaveAsset(PsdLayer psdLayer, TextureImporterSettings psdUnityImport, Texture2D texture, ImportUserData importSettings, ImportLayerData layerSettings)
        {
            string dir;
            string filePath = GetFilePath(psdLayer, importSettings, out dir);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                string[] array = dir.Split('/', StringSplitOptions.None);
                string text = array[0];
                foreach (string item in array.Skip(1))
                {
                    string text2 = $"{text}/{item}";
                    if (!AssetDatabase.IsValidFolder(text2))
                    {
                        AssetDatabase.CreateFolder(text, item);
                    }
                    text = text2;
                }
            }
            float num = psdUnityImport.spritePixelsPerUnit;
            switch (layerSettings.ScaleFactor)
            {
            case ScaleFactor.Quarter:
                num /= 4f;
                break;
            case ScaleFactor.Half:
                num /= 2f;
                break;
            }
            bool preferHighBitDepth = (Object)(object)texture != (Object)null && (int)texture.format == 74;
            try
            {
                byte[] array2 = PsdTextureAssetUtility.EncodePng(texture);
                if (array2 == null || array2.Length == 0)
                {
                    return null;
                }
                File.WriteAllBytes(filePath, array2);
            }
            finally
            {
                if ((Object)(object)texture != (Object)null)
                {
                    Object.DestroyImmediate((Object)(object)texture);
                }
            }
            AssetDatabase.ImportAsset(filePath, (ImportAssetOptions)1);
            Texture2D dirty = AssetDatabase.LoadAssetAtPath<Texture2D>(filePath);
            TextureImporter val = (TextureImporter)AssetImporter.GetAtPath(filePath);
            TextureImporterSettings val2 = new TextureImporterSettings();
            val.ReadTextureSettings(val2);
            val2.spriteAlignment = (int)layerSettings.Alignment;
            val2.spritePivot = layerSettings.Pivot;
            val2.spritePixelsPerUnit = num;
            val2.filterMode = psdUnityImport.filterMode;
            val2.wrapMode = psdUnityImport.wrapMode;
            val2.textureType = (TextureImporterType)8;
            val2.spriteMode = 1;
            val2.mipmapEnabled = false;
            val2.alphaIsTransparency = true;
            val2.npotScale = (TextureImporterNPOTScale)0;
            val.SetTextureSettings(val2);
            PsdTextureAssetUtility.ApplyPrecisionImportSettings(val, preferHighBitDepth);
            EditorUtility.SetDirty((Object)(object)dirty);
            AssetDatabase.WriteImportSettingsIfDirty(filePath);
            AssetDatabase.ImportAsset(filePath, (ImportAssetOptions)1);
            return (Sprite)AssetDatabase.LoadAssetAtPath(filePath, typeof(Sprite));
        }

        public static string GetFilePath(PsdLayer layer, ImportUserData importSettings, out string dir)
        {
            string text = string.Format("{0}{1}", layer.Name, ".png");
            string text2 = "";
            if (importSettings.fileNaming != NamingConvention.LayerNameOnly)
            {
                bool flag = importSettings.fileNaming == NamingConvention.CreateGroupFolders;
                PsdLayer parent = layer.Parent;
                while (parent != null)
                {
                    if (flag)
                    {
                        text2 = ((!string.IsNullOrEmpty(text2)) ? $"{parent.Name}/{text2}" : parent.Name);
                    }
                    else
                    {
                        text = $"{parent.Name}_{text}";
                    }
                    parent = parent.Parent;
                    if (importSettings.groupMode == GroupMode.ParentOnly)
                    {
                        break;
                    }
                }
            }
            string text3 = importSettings.TargetDirectory;
            if (!string.IsNullOrEmpty(text2))
            {
                text3 = $"{text3}/{text2}";
            }
            text3 = SanitizeString(text3, Path.GetInvalidPathChars());
            text = SanitizeString(text, Path.GetInvalidFileNameChars());
            string result = $"{text3}/{text}";
            dir = text3;
            return result;
        }

        private static bool ShouldExportHighBitDepth(PsdLayer layer)
        {
            if (layer != null && layer.Document != null && layer.Document.Depth == 16)
            {
                return true; // 已删除 License：默认完全授权
            }
            return false;
        }

        private static Sprite LoadSpriteAssetAtPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }
            Sprite val = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if ((Object)(object)val != (Object)null)
            {
                return val;
            }
            return AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().FirstOrDefault();
        }

        private static string SanitizeString(string text, char[] cleanChars)
        {
            text = string.Join("_", text.Split(cleanChars));
            text = new string(text.Select((char c) => char.IsWhiteSpace(c) ? '_' : c).ToArray());
            return text;
        }

        private static ReconstructData GetReconstructData(PsdDocument psdDoc, string psdPath, Vector2 documentPivot, ImportUserData importSettings, ImportLayerData reconstructRoot)
        {
            float sourcePixelsPerUnit;
            TextureImporterSettings psdUnityImport = GetSourceTextureImportSettings(psdPath, out sourcePixelsPerUnit);
            Vector2 docSize = default(Vector2);
            docSize = new Vector2((float)psdDoc.Width, (float)psdDoc.Height);
            ReconstructData data = new ReconstructData(docSize, documentPivot, sourcePixelsPerUnit);
            reconstructRoot.Iterate(delegate(ImportLayerData layer)
            {
                if (layer.import)
                {
                    PsdLayer psdLayerByIndex = GetPsdLayerByIndex(psdDoc, layer.indexId);
                    Rect val = default(Rect);
                    val.xMin = psdLayerByIndex.Left;
                    val.xMax = psdLayerByIndex.Right;
                    val.yMin = psdDoc.Height - psdLayerByIndex.Bottom;
                    val.yMax = psdDoc.Height - psdLayerByIndex.Top;
                    Rect value = val;
                    data.layerBoundsIndex.Add(layer.indexId, value);
                    string dir;
                    string filePath = GetFilePath(psdLayerByIndex, importSettings, out dir);
                    Sprite val2 = LoadSpriteAssetAtPath(filePath);
                    if ((Object)(object)val2 == (Object)null)
                    {
                        val2 = ImportLayer(psdDoc, importSettings, layer, psdUnityImport);
                    }
                    Vector2 anchor = Vector2.zero;
                    if ((Object)(object)val2 != (Object)null)
                    {
                        TextureImporter val3 = (TextureImporter)AssetImporter.GetAtPath(filePath);
                        if ((Object)(object)val3 != (Object)null)
                        {
                            TextureImporterSettings val4 = new TextureImporterSettings();
                            val3.ReadTextureSettings(val4);
                            anchor = ((val4.spriteAlignment != 9) ? AlignmentToPivot((SpriteAlignment)val4.spriteAlignment) : val4.spritePivot);
                        }
                        else
                        {
                            val = val2.rect;
                            if (val.width > 0f)
                            {
                                val = val2.rect;
                                if (val.height > 0f)
                                {
                                    float x = val2.pivot.x;
                                    val = val2.rect;
                                    float num = x / val.width;
                                    float y = val2.pivot.y;
                                    val = val2.rect;
                                    anchor = new Vector2(num, y / val.height);
                                }
                            }
                        }
                    }
                    data.AddSprite(layer.indexId, val2, anchor);
                }
            }, (ImportLayerData checkGroup) => checkGroup.import);
            return data;
        }

        public static void Reconstruct(Object psdFile, ImportUserData importSettings, ImportLayerData reconstructRoot, Vector2 documentPivot, IReconstructor reconstructor)
        {
            string psdFilepath = GetPsdFilepath(psdFile);
            if (string.IsNullOrEmpty(psdFilepath))
            {
                return;
            }
            using PsdDocument psdDoc = PsdDocument.Create(psdFilepath);
            ReconstructData reconstructData = GetReconstructData(psdDoc, psdFilepath, documentPivot, importSettings, reconstructRoot);
            GameObject val = reconstructor.Reconstruct(reconstructRoot, reconstructData, Selection.activeGameObject);
            if ((Object)(object)val != (Object)null)
            {
                EditorGUIUtility.PingObject((Object)(object)val);
                Selection.activeGameObject = val;
            }
        }

        public static Vector2 AlignmentToPivot(SpriteAlignment spriteAlignment)
        {
            Vector2 zero = Vector2.zero;
            switch ((int)spriteAlignment)
            {
            case 1:
            case 2:
            case 3:
                zero.y = 1f;
                break;
            case 0:
            case 4:
            case 5:
                zero.y = 0.5f;
                break;
            case 6:
            case 7:
            case 8:
                zero.y = 0f;
                break;
            }
            switch ((int)spriteAlignment)
            {
            case 1:
            case 4:
            case 6:
                zero.x = 0f;
                break;
            case 0:
            case 2:
            case 7:
                zero.x = 0.5f;
                break;
            case 3:
            case 5:
            case 8:
                zero.x = 1f;
                break;
            }
            return zero;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdImporter GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
