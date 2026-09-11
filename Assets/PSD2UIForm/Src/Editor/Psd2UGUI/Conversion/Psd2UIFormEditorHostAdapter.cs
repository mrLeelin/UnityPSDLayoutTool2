using System;
using PsdTextStyleInfoNamespace;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using cn.efunstudio.psdreader;
using Object = UnityEngine.Object;

namespace UGF.EditorTools.Psd2UGUI
{
	/// <summary>
	/// <see cref="IPsd2UIFormEditorHost"/> 的编辑器侧实现：把运行期侧的调用转发到既有编辑器实现
	/// （UGUIParser 的生成期操作）。转发保持原调用语义不变，便于逐点替换调用位置。
	/// </summary>
	internal sealed class Psd2UIFormEditorHostAdapter : IPsd2UIFormEditorHost
	{
		public void ApplyNodeRectToUI(object node, object target, bool applyPosition = true, bool applySizeX = true, bool applySizeY = true, int padding = 0)
		{
			UGUIParser.ApplyNodeRectToUI(node, target, applyPosition, applySizeX, applySizeY, padding);
		}

		public Sprite ApplyImageSprite(PsdLayerNode node, Image image)
		{
			return UGUIParser.Instance.ApplyImageSprite(node, image);
		}

		public Sprite ExportAndLoadSprite(object node, bool requiresBorder = false)
		{
			return UGUIParser.ExportAndLoadSprite(node, requiresBorder);
		}

		public Texture2D ExportAndLoadTexture(object node)
		{
			return UGUIParser.ExportAndLoadTexture(node);
		}

		public void ApplyTMPTextStyle(object node, object target)
		{
			UGUIParser.ApplyTMPTextStyle(node, target);
		}

		public PsdTextStyleInfo ApplyLegacyTextStyle(object node, object target)
		{
			return UGUIParser.ApplyLegacyTextStyle(node, target);
		}

		public GUIType GetDefaultImageType()
		{
			return UGUIParser.Instance.GetDefaultImageType();
		}

		public UGUIParseRule FindRule(GUIType uiType)
		{
			return UGUIParser.Instance.FindRule(uiType);
		}

		public Color GetRepresentativeColor(object node, Color fallbackColor)
		{
			return UGUIParser.GetRepresentativeColor(node, fallbackColor);
		}

		public bool IsChineseNameConversionEnabled()
		{
			UGUIParser parser = UGUIParser.Instance;
			return (Object)(object)parser != (Object)null && parser.IsChineseNameConversionEnabled();
		}

		public string RemoveRecognizedLayerTags(string text)
		{
			UGUIParser parser = UGUIParser.Instance;
			if ((Object)(object)parser == (Object)null)
			{
				return text;
			}
			return parser.RemoveRecognizedLayerTags(text);
		}

		public string BuildLayerObjectName(string name, int index)
		{
			UGUIParser parser = UGUIParser.Instance;
			if ((Object)(object)parser == (Object)null)
			{
				return null;
			}
			return parser.BuildLayerObjectName(name, index);
		}

		public GUIType ApplyForcedTMPType(GUIType uiType)
		{
			UGUIParser parser = UGUIParser.Instance;
			if ((Object)(object)parser == (Object)null)
			{
				return uiType;
			}
			return parser.ApplyForcedTMPType(uiType);
		}

		public Type GetHelperComponentType(GUIType uiType)
		{
			UGUIParser parser = UGUIParser.Instance;
			if ((Object)(object)parser == (Object)null)
			{
				return null;
			}
			return parser.GetHelperComponentType(uiType);
		}

		public string GetSharedAssetsOutputDirectory()
		{
			UGUIParser parser = UGUIParser.Instance;
			if ((Object)(object)parser == (Object)null)
			{
				return null;
			}
			return parser.GetSharedAssetsOutputDirectory();
		}

		public string GetSharedPrefabOutputDirectory()
		{
			UGUIParser parser = UGUIParser.Instance;
			if ((Object)(object)parser == (Object)null)
			{
				return null;
			}
			return parser.GetSharedPrefabOutputDirectory();
		}

		public bool HasConverterInstance()
		{
			return (Object)(object)Psd2UIFormConverterEditor.Instance != (Object)null;
		}

		public void AttachConverter(Psd2UIFormConverter converter)
		{
			Psd2UIFormConverterEditor.GetOrCreate(converter)?.Attach();
		}

		public void DetachConverter(Psd2UIFormConverter converter)
		{
			// 不走 GetOrCreate：OnDestroy 阶段壳已被 Unity 判为 null。
			Psd2UIFormConverterEditor.GetAttached(converter)?.Detach();
		}

		public void DisposeConverter(Psd2UIFormConverter converter)
		{
			// OnDestroy 阶段壳已被 Unity 判为 null，只能按实例 ID 找回逻辑对象。
			Psd2UIFormConverterEditor.GetAttached(converter)?.Dispose();
		}

		public void DrawConverterGizmos(Psd2UIFormConverter converter)
		{
			Psd2UIFormConverterEditor.GetOrCreate(converter)?.DrawGizmos();
		}

		public void LoadConverterDocument(Psd2UIFormConverter converter)
		{
			Psd2UIFormConverterEditor.GetOrCreate(converter)?.LoadDocument();
		}

		public void RefreshAllHelperComponents()
		{
			Psd2UIFormConverterEditor converter = Psd2UIFormConverterEditor.Instance;
			if (converter != null)
			{
				converter.RefreshAllHelperComponents();
			}
		}

		public string GetSourcePsdAssetPath()
		{
			return Psd2UIFormConverterEditor.Instance?.GetSourcePsdAssetPath();
		}

		public string GetPsdAssetChangeTime()
		{
			Psd2UIFormConverterEditor converter = Psd2UIFormConverterEditor.Instance;
			if (converter == null)
			{
				return null;
			}
			return converter.psdAssetChangeTime;
		}

		public PsdLayerNode FindNodeByReferenceKey(string key)
		{
			Psd2UIFormConverterEditor converter = Psd2UIFormConverterEditor.Instance;
			if (converter == null)
			{
				return null;
			}
			return converter.FindNodeByReferenceKey(key);
		}

		public string ExportImageAsset(PsdLayerNode node, bool enabled, string exportDirectory, string fileName, bool lowerCaseName, bool requiresBorder, bool skipAssetReference)
		{
			return PsdLayerNodeEditorOps.ExportImageAsset(node, enabled, exportDirectory, fileName, lowerCaseName, requiresBorder, skipAssetReference);
		}

		public bool MatchesTextureExportMode(string assetPath, bool preferHighBitDepth)
		{
			return PsdTextureAssetUtility.MatchesExportMode(assetPath, preferHighBitDepth);
		}

		public Texture2D AcquirePreviewTexture(string cacheKey, PsdLayerNode node)
		{
			return PsdLayerPreviewCache.Acquire(cacheKey, () => PsdLayerNodeEditorOps.CreatePreviewTexture(node));
		}

		public bool ReleasePreviewTexture(string cacheKey)
		{
			return PsdLayerPreviewCache.Release(cacheKey);
		}

		public bool TryGetRenderedRepresentativeColor(PsdLayerNode node, out Color color)
		{
			return PsdLayerNodeEditorOps.TrySampleRepresentativeColor(PsdLayerNodeEditorOps.RenderNodeImage(node, true), out color);
		}

		public bool IsPsdReaderProductAvailable()
		{
			return PsdReaderProductAccess.IsAvailable;
		}
	}

	/// <summary>
	/// 编辑器加载时注册适配器（域重载后同样会执行），保证编辑器路径下 <see cref="Psd2UIFormEditorHost.Current"/> 始终可用。
	/// </summary>
	internal static class Psd2UIFormEditorHostBootstrap
	{
		[InitializeOnLoadMethod]
		private static void Register()
		{
			Psd2UIFormEditorHost.Current = new Psd2UIFormEditorHostAdapter();
		}
	}
}
