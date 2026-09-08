using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using PsdLicensing;
using PsdProtectionGuards;
using UnityEditor;
using UnityEngine;
using cn.efunstudio.psdreader.PsdParser;
using cn.efunstudio.psdreader.Reconstructor;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader
{

internal class PsdImporter
{
	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass13_0
	{
		public int total;

		public _003C_003Ec__DisplayClass13_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal void _003CImportLayersUI_003Eb__0(int current, ImportLayerData layer)
		{
			string label = $"[{current}/{total}] Layer: {layer.name}";
			float percent = (float)current / (float)total;
			EditorCoroutineRunner.UpdateUI(label, percent);
		}
	}

	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass22_0
	{
		public PsdDocument psdDoc;

		public ReconstructData data;

		public ImportUserData importSettings;

		public TextureImporterSettings psdUnityImport;

		public _003C_003Ec__DisplayClass22_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal void _003CGetReconstructData_003Eb__0(ImportLayerData layer)
		{
			if (!layer.import)
			{
				return;
			}
			PsdLayer psdLayerByIndex = GetPsdLayerByIndex(psdDoc, layer.indexId);
			Rect value = new Rect
			{
				xMin = psdLayerByIndex.Left,
				xMax = psdLayerByIndex.Right,
				yMin = psdDoc.Height - psdLayerByIndex.Bottom,
				yMax = psdDoc.Height - psdLayerByIndex.Top
			};
			data.layerBoundsIndex.Add(layer.indexId, value);
			string dir;
			string filePath = GetFilePath(psdLayerByIndex, importSettings, out dir);
			Sprite sprite = LoadSpriteAssetAtPath(filePath);
			if (sprite == null)
			{
				sprite = ImportLayer(psdDoc, importSettings, layer, psdUnityImport);
			}
			Vector2 anchor = Vector2.zero;
			if (sprite != null)
			{
				TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(filePath);
				if (!(textureImporter != null))
				{
					if (sprite.rect.width > 0f && sprite.rect.height > 0f)
					{
						anchor = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
					}
				}
				else
				{
					TextureImporterSettings textureImporterSettings = new TextureImporterSettings();
					textureImporter.ReadTextureSettings(textureImporterSettings);
					anchor = ((textureImporterSettings.spriteAlignment != 9) ? AlignmentToPivot((SpriteAlignment)textureImporterSettings.spriteAlignment) : textureImporterSettings.spritePivot);
				}
			}
			data.AddSprite(layer.indexId, sprite, anchor);
		}
	}

	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass6_0
	{
		public ImportUserData importSettings;

		public Action<ImportLayerData, DisplayLayerData> callback;

		public ImportLayerData docImportData;

		public DisplayLayerData docDisplayData;

		public _003C_003Ec__DisplayClass6_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal void _003CBuildImportLayerData_003Eb__0(PsdLayer layer, int[] indexId)
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
		}

		internal void _003CBuildImportLayerData_003Eb__1()
		{
			if (callback != null)
			{
				callback(docImportData, docDisplayData);
			}
		}
	}

	private const string DOC_ROOT = "DOCUMENT_ROOT";

	private const float DefaultSourcePixelsPerUnit = 100f;

	private static bool IsSupportedSourceFile(string filepath)
	{
		if (!string.IsNullOrEmpty(filepath))
		{
			if (filepath.EndsWith(".psd", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			return filepath.EndsWith(".psb", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static TextureImporterSettings GetSourceTextureImportSettings(string filepath, out float sourcePixelsPerUnit)
	{
		TextureImporterSettings textureImporterSettings = new TextureImporterSettings();
		sourcePixelsPerUnit = 100f;
		TextureImporter textureImporter = AssetImporter.GetAtPath(filepath) as TextureImporter;
		if (textureImporter != null)
		{
			textureImporter.ReadTextureSettings(textureImporterSettings);
			sourcePixelsPerUnit = textureImporter.spritePixelsPerUnit;
			return textureImporterSettings;
		}
		textureImporterSettings.spritePixelsPerUnit = 100f;
		textureImporterSettings.filterMode = FilterMode.Bilinear;
		textureImporterSettings.wrapMode = TextureWrapMode.Clamp;
		textureImporterSettings.textureType = TextureImporterType.Sprite;
		textureImporterSettings.spriteMode = 1;
		textureImporterSettings.mipmapEnabled = false;
		textureImporterSettings.alphaIsTransparency = true;
		textureImporterSettings.npotScale = TextureImporterNPOTScale.None;
		return textureImporterSettings;
	}

	private static string GetPsdFilepath(UnityEngine.Object psdFile)
	{
		string assetPath = AssetDatabase.GetAssetPath(psdFile);
		if (!string.IsNullOrEmpty(assetPath))
		{
			if (!IsSupportedSourceFile(assetPath))
			{
				return string.Empty;
			}
			return assetPath;
		}
		return string.Empty;
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

	public static void BuildImportLayerData(UnityEngine.Object file, ImportUserData importSettings, Action<ImportLayerData, DisplayLayerData> callback)
	{
		_003C_003Ec__DisplayClass6_0 CS_0024_003C_003E8__locals15 = new _003C_003Ec__DisplayClass6_0();
		CS_0024_003C_003E8__locals15.importSettings = importSettings;
		CS_0024_003C_003E8__locals15.callback = callback;
		string psdFilepath = GetPsdFilepath(file);
		if (string.IsNullOrEmpty(psdFilepath))
		{
			if (CS_0024_003C_003E8__locals15.callback != null)
			{
				CS_0024_003C_003E8__locals15.callback(null, null);
			}
			return;
		}
		using PsdDocument psdDocument = PsdDocument.Create(psdFilepath);
		CS_0024_003C_003E8__locals15.docImportData = new ImportLayerData
		{
			name = "DOCUMENT_ROOT",
			indexId = new int[1] { -1 },
			Childs = new List<ImportLayerData>()
		};
		CS_0024_003C_003E8__locals15.docDisplayData = new DisplayLayerData
		{
			indexId = new int[1] { -1 },
			Childs = new List<DisplayLayerData>()
		};
		EditorCoroutineRunner.StartCoroutine(ParseLayers(psdDocument.Childs, doYield: false, delegate(PsdLayer layer, int[] indexId)
		{
			string text = "";
			ImportLayerData importLayerData = CS_0024_003C_003E8__locals15.docImportData;
			DisplayLayerData displayLayerData = CS_0024_003C_003E8__locals15.docDisplayData;
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
				Alignment = CS_0024_003C_003E8__locals15.importSettings.DefaultAlignment,
				Pivot = CS_0024_003C_003E8__locals15.importSettings.DefaultPivot,
				ScaleFactor = CS_0024_003C_003E8__locals15.importSettings.ScaleFactor,
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
			if (CS_0024_003C_003E8__locals15.callback != null)
			{
				CS_0024_003C_003E8__locals15.callback(CS_0024_003C_003E8__locals15.docImportData, CS_0024_003C_003E8__locals15.docDisplayData);
			}
		}));
	}

	private static PsdLayer GetPsdLayerByIndex(PsdDocument psdDoc, int[] layerIdx)
	{
		if (psdDoc != null && layerIdx != null && layerIdx.Length != 0)
		{
			PsdLayer[] childs = psdDoc.Childs;
			PsdLayer psdLayer = null;
			int num = 0;
			while (true)
			{
				if (num < layerIdx.Length)
				{
					int num2 = layerIdx[num];
					if (childs == null || num2 < 0 || num2 >= childs.Length)
					{
						break;
					}
					psdLayer = childs[num2];
					if (psdLayer != null)
					{
						childs = psdLayer.Childs;
						num++;
						continue;
					}
					return null;
				}
				return psdLayer;
			}
			return null;
		}
		return null;
	}

	public static Texture2D GetLayerTexture(UnityEngine.Object psdFile, int[] layerIdx)
	{
		ImportLayerData setting = new ImportLayerData
		{
			Alignment = SpriteAlignment.Center,
			Pivot = new Vector2(0.5f, 0.5f),
			ScaleFactor = ScaleFactor.Full,
			Childs = new List<ImportLayerData>(),
			import = true,
			indexId = layerIdx
		};
		return GetLayerTexture(psdFile, setting);
	}

	public static Texture2D GetLayerTexture(UnityEngine.Object psdFile, ImportLayerData setting)
	{
		string psdFilepath = GetPsdFilepath(psdFile);
		if (!string.IsNullOrEmpty(psdFilepath))
		{
			Texture2D texture2D = null;
			using PsdDocument psdDoc = PsdDocument.Create(psdFilepath);
			PsdLayer psdLayerByIndex = GetPsdLayerByIndex(psdDoc, setting.indexId);
			return GetLayerTexture(psdDoc, psdLayerByIndex, setting);
		}
		return null;
	}

	private static Texture2D GetLayerTexture(PsdDocument psdDoc, PsdLayer psdLayer, ImportLayerData setting)
	{
		if (psdLayer != null)
		{
			Texture2D texture2D = GetTexture(psdLayer);
			if (setting.ScaleFactor != ScaleFactor.Full)
			{
				int mipLevel = ((setting.ScaleFactor == ScaleFactor.Half) ? 1 : 2);
				texture2D = ScaleTextureByMipmap(texture2D, mipLevel);
			}
			return texture2D;
		}
		return null;
	}

	private static Texture2D GetTexture(PsdLayer layer)
	{
		PsdRenderedImage psdRenderedImage = layer.Render();
		if (psdRenderedImage != null && !psdRenderedImage.IsEmpty)
		{
			TextureFormat textureFormat = ((!psdRenderedImage.IsHighBitDepth) ? TextureFormat.RGBA32 : TextureFormat.RGBA64);
			Texture2D texture2D = new Texture2D(psdRenderedImage.Width, psdRenderedImage.Height, textureFormat, mipChain: true);
			if (psdRenderedImage.IsHighBitDepth)
			{
				ushort[] exportRgba = psdRenderedImage.GetExportRgba64();
				byte[] array = new byte[exportRgba.Length * 2];
				Buffer.BlockCopy(exportRgba, 0, array, 0, array.Length);
				texture2D.LoadRawTextureData(array);
			}
			else
			{
				texture2D.LoadRawTextureData(psdRenderedImage.GetExportRgba32());
			}
			texture2D.Apply(updateMipmaps: true, makeNoLongerReadable: false);
			return texture2D;
		}
		return null;
	}

	private static Texture2D ScaleTextureByMipmap(Texture2D tex, int mipLevel)
	{
		if (mipLevel >= 0 && mipLevel <= 2)
		{
			int width = Mathf.RoundToInt(tex.width / (mipLevel * 2));
			int height = Mathf.RoundToInt(tex.height / (mipLevel * 2));
			TextureFormat textureFormat = ((tex.format == TextureFormat.RGBA64) ? TextureFormat.RGBA64 : TextureFormat.RGBA32);
			Texture2D texture2D = new Texture2D(width, height, textureFormat, mipChain: false);
			if (textureFormat == TextureFormat.RGBA64)
			{
				texture2D.SetPixels(tex.GetPixels(mipLevel));
			}
			else
			{
				texture2D.SetPixels32(tex.GetPixels32(mipLevel));
			}
			texture2D.Apply();
			return texture2D;
		}
		return null;
	}

	public static void ImportLayersUI(UnityEngine.Object psdFile, ImportUserData importSettings, List<int[]> layerIndices)
	{
		_003C_003Ec__DisplayClass13_0 CS_0024_003C_003E8__locals3 = new _003C_003Ec__DisplayClass13_0();
		CS_0024_003C_003E8__locals3.total = layerIndices.Count;
		EditorCoroutineRunner.StartCoroutineWithUI(ImportCoroutine(psdFile, importSettings, layerIndices, doYield: true, delegate(int current, ImportLayerData layer)
		{
			string label = $"[{current}/{CS_0024_003C_003E8__locals3.total}] Layer: {layer.name}";
			float percent = (float)current / (float)CS_0024_003C_003E8__locals3.total;
			EditorCoroutineRunner.UpdateUI(label, percent);
		}), "Importing PSD Layers", isCancelable: true);
	}

	public static void ImportLayers(UnityEngine.Object psdFile, ImportUserData importSettings, List<int[]> layerIndices, Action<List<Sprite>> callback = null)
	{
		EditorCoroutineRunner.StartCoroutine(ImportCoroutine(psdFile, importSettings, layerIndices, doYield: false, null, callback));
	}

	private static IEnumerator ImportCoroutine(UnityEngine.Object psdFile, ImportUserData importSettings, List<int[]> layerIndices, bool doYield = false, Action<int, ImportLayerData> layerCallback = null, Action<List<Sprite>> completeCallback = null)
	{
		string psdFilepath = GetPsdFilepath(psdFile);
		if (string.IsNullOrEmpty(psdFilepath))
		{
			completeCallback?.Invoke(null);
			yield break;
		}
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

	private static Sprite ImportLayer(PsdDocument psdDoc, ImportUserData importSettings, ImportLayerData layerSettings, TextureImporterSettings psdUnityImport)
	{
		if (layerSettings == null)
		{
			return null;
		}
		PsdLayer psdLayerByIndex = GetPsdLayerByIndex(psdDoc, layerSettings.indexId);
		Texture2D layerTexture = GetLayerTexture(psdDoc, psdLayerByIndex, layerSettings);
		if (!(layerTexture == null))
		{
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
		bool preferHighBitDepth = texture != null && texture.format == TextureFormat.RGBA64;
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
			if (texture != null)
			{
				UnityEngine.Object.DestroyImmediate(texture);
			}
		}
		AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate);
		Texture2D dirty = AssetDatabase.LoadAssetAtPath<Texture2D>(filePath);
		TextureImporter obj = (TextureImporter)AssetImporter.GetAtPath(filePath);
		TextureImporterSettings textureImporterSettings = new TextureImporterSettings();
		obj.ReadTextureSettings(textureImporterSettings);
		textureImporterSettings.spriteAlignment = (int)layerSettings.Alignment;
		textureImporterSettings.spritePivot = layerSettings.Pivot;
		textureImporterSettings.spritePixelsPerUnit = num;
		textureImporterSettings.filterMode = psdUnityImport.filterMode;
		textureImporterSettings.wrapMode = psdUnityImport.wrapMode;
		textureImporterSettings.textureType = TextureImporterType.Sprite;
		textureImporterSettings.spriteMode = 1;
		textureImporterSettings.mipmapEnabled = false;
		textureImporterSettings.alphaIsTransparency = true;
		textureImporterSettings.npotScale = TextureImporterNPOTScale.None;
		obj.SetTextureSettings(textureImporterSettings);
		PsdTextureAssetUtility.ApplyPrecisionImportSettings(obj, preferHighBitDepth);
		EditorUtility.SetDirty(dirty);
		AssetDatabase.WriteImportSettingsIfDirty(filePath);
		AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate);
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
					text2 = (string.IsNullOrEmpty(text2) ? parent.Name : $"{parent.Name}/{text2}");
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
			return PsdLicenseService.GetPreviewLicenseState().GetLicenseValid();
		}
		return false;
	}

	private static Sprite LoadSpriteAssetAtPath(string assetPath)
	{
		if (string.IsNullOrEmpty(assetPath))
		{
			return null;
		}
		Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
		if (sprite != null)
		{
			return sprite;
		}
		return AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().FirstOrDefault();
	}

	private static string SanitizeString(string text, char[] cleanChars)
	{
		text = string.Join("_", text.Split(cleanChars));
		text = new string(text.Select((char c) => (!char.IsWhiteSpace(c)) ? c : '_').ToArray());
		return text;
	}

	private static ReconstructData GetReconstructData(PsdDocument psdDoc, string psdPath, Vector2 documentPivot, ImportUserData importSettings, ImportLayerData reconstructRoot)
	{
		_003C_003Ec__DisplayClass22_0 CS_0024_003C_003E8__locals16 = new _003C_003Ec__DisplayClass22_0();
		CS_0024_003C_003E8__locals16.psdDoc = psdDoc;
		CS_0024_003C_003E8__locals16.importSettings = importSettings;
		CS_0024_003C_003E8__locals16.psdUnityImport = GetSourceTextureImportSettings(psdPath, out var sourcePixelsPerUnit);
		Vector2 docSize = new Vector2(CS_0024_003C_003E8__locals16.psdDoc.Width, CS_0024_003C_003E8__locals16.psdDoc.Height);
		CS_0024_003C_003E8__locals16.data = new ReconstructData(docSize, documentPivot, sourcePixelsPerUnit);
		reconstructRoot.Iterate(delegate(ImportLayerData layer)
		{
			if (layer.import)
			{
				PsdLayer psdLayerByIndex = GetPsdLayerByIndex(CS_0024_003C_003E8__locals16.psdDoc, layer.indexId);
				Rect value = new Rect
				{
					xMin = psdLayerByIndex.Left,
					xMax = psdLayerByIndex.Right,
					yMin = CS_0024_003C_003E8__locals16.psdDoc.Height - psdLayerByIndex.Bottom,
					yMax = CS_0024_003C_003E8__locals16.psdDoc.Height - psdLayerByIndex.Top
				};
				CS_0024_003C_003E8__locals16.data.layerBoundsIndex.Add(layer.indexId, value);
				string dir;
				string filePath = GetFilePath(psdLayerByIndex, CS_0024_003C_003E8__locals16.importSettings, out dir);
				Sprite sprite = LoadSpriteAssetAtPath(filePath);
				if (sprite == null)
				{
					sprite = ImportLayer(CS_0024_003C_003E8__locals16.psdDoc, CS_0024_003C_003E8__locals16.importSettings, layer, CS_0024_003C_003E8__locals16.psdUnityImport);
				}
				Vector2 anchor = Vector2.zero;
				if (sprite != null)
				{
					TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(filePath);
					if (!(textureImporter != null))
					{
						if (sprite.rect.width > 0f && sprite.rect.height > 0f)
						{
							anchor = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
						}
					}
					else
					{
						TextureImporterSettings textureImporterSettings = new TextureImporterSettings();
						textureImporter.ReadTextureSettings(textureImporterSettings);
						anchor = ((textureImporterSettings.spriteAlignment != 9) ? AlignmentToPivot((SpriteAlignment)textureImporterSettings.spriteAlignment) : textureImporterSettings.spritePivot);
					}
				}
				CS_0024_003C_003E8__locals16.data.AddSprite(layer.indexId, sprite, anchor);
			}
		}, (ImportLayerData checkGroup) => checkGroup.import);
		return CS_0024_003C_003E8__locals16.data;
	}

	public static void Reconstruct(UnityEngine.Object psdFile, ImportUserData importSettings, ImportLayerData reconstructRoot, Vector2 documentPivot, IReconstructor reconstructor)
	{
		string psdFilepath = GetPsdFilepath(psdFile);
		if (string.IsNullOrEmpty(psdFilepath))
		{
			return;
		}
		using PsdDocument psdDoc = PsdDocument.Create(psdFilepath);
		ReconstructData reconstructData = GetReconstructData(psdDoc, psdFilepath, documentPivot, importSettings, reconstructRoot);
		GameObject gameObject = reconstructor.Reconstruct(reconstructRoot, reconstructData, Selection.activeGameObject);
		if (gameObject != null)
		{
			EditorGUIUtility.PingObject(gameObject);
			Selection.activeGameObject = gameObject;
		}
	}

	public static Vector2 AlignmentToPivot(SpriteAlignment spriteAlignment)
	{
		Vector2 zero = Vector2.zero;
		switch (spriteAlignment)
		{
		case SpriteAlignment.TopLeft:
		case SpriteAlignment.TopCenter:
		case SpriteAlignment.TopRight:
			zero.y = 1f;
			break;
		case SpriteAlignment.Center:
		case SpriteAlignment.LeftCenter:
		case SpriteAlignment.RightCenter:
			zero.y = 0.5f;
			break;
		case SpriteAlignment.BottomLeft:
		case SpriteAlignment.BottomCenter:
		case SpriteAlignment.BottomRight:
			zero.y = 0f;
			break;
		}
		switch (spriteAlignment)
		{
		case SpriteAlignment.TopLeft:
		case SpriteAlignment.LeftCenter:
		case SpriteAlignment.BottomLeft:
			zero.x = 0f;
			break;
		case SpriteAlignment.Center:
		case SpriteAlignment.TopCenter:
		case SpriteAlignment.BottomCenter:
			zero.x = 0.5f;
			break;
		case SpriteAlignment.TopRight:
		case SpriteAlignment.RightCenter:
		case SpriteAlignment.BottomRight:
			zero.x = 1f;
			break;
		}
		return zero;
	}

	public PsdImporter()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}
