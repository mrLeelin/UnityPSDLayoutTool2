using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using PsdProtectionGuards;
using PsdEditorAttributes;
using TMPro;
using UnityEditor;
using UnityEngine;
using PsdTextStyles;
using PsdLayerUtilities;
using cn.efunstudio.psdreader;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdPathUtilities;

namespace UGF.EditorTools.Psd2UGUI
{

[CanEditMultipleObjects]
[ExecuteInEditMode]
[DisallowMultipleComponent]
public sealed class PsdLayerNode : MonoBehaviour
{
	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass65_0
	{
		public PsdLayerNode SourceLayer;

		public bool LowercaseExportName;

		public string RequestedExportName;

		public _003C_003Ec__DisplayClass65_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal bool IsExportNameCandidate(PsdLayerNode node)
		{
			if (node != null)
			{
				if (!(node == SourceLayer))
				{
					return node.ShouldExportImage();
				}
				return true;
			}
			return false;
		}

		internal global::_003C_003Ef__AnonymousType0<PsdLayerNode, string> SelectNodeExportName(PsdLayerNode node)
		{
			return new global::_003C_003Ef__AnonymousType0<PsdLayerNode, string>(node, GetExportFileStem(node, LowercaseExportName));
		}

		internal bool MatchesRequestedExportName(global::_003C_003Ef__AnonymousType0<PsdLayerNode, string> entry)
		{
			return string.Equals(entry.Name, RequestedExportName, StringComparison.OrdinalIgnoreCase);
		}
	}

	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass89_0
	{
		public GUIType RequiredUiType;

		public _003C_003Ec__DisplayClass89_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal bool MatchesRequiredUiTypeOrRole(PsdLayerNode layer)
		{
			if (layer.UIType != RequiredUiType)
			{
				return layer.RoleUIType == RequiredUiType;
			}
			return true;
		}
	}

	[SerializeField]
	[PsdReadOnlyAttribute]
	internal int BindPsdLayerIndex = -1;

	[SerializeField]
	[PsdReadOnlyAttribute]
	private PsdLayerType mLayerType;

	[SerializeField]
	[PsdReadOnlyAttribute]
	private string sourceLayerName;

	[SerializeField]
	internal bool markToExport;

	[SerializeField]
	[HideInInspector]
	internal GUIType UIType;

	[SerializeField]
	[HideInInspector]
	internal GUIType RoleUIType;

	[CompilerGenerated]
	private Texture2D _previewTexture;

	[CompilerGenerated]
	private string _layerBoundsDescription;

	[CompilerGenerated]
	private Rect _layerBounds;

	private WeakReference<PsdLayerNode> _referencedLayerCache;

	private string _previewCacheKey;

	private PsdLayer _boundPsdLayer;

	internal Texture2D PreviewTexture
	{
		[CompilerGenerated]
		get
		{
			return _previewTexture;
		}
		[CompilerGenerated]
		private set
		{
			_previewTexture = value;
		}
	}

	internal PsdLayerType LayerType => mLayerType;

	private static EntityId GetLayerEntityId(object unityObject)
	{
		return ((UnityEngine.Object)unityObject).GetEntityId();
	}

	[SpecialName]
	[CompilerGenerated]
	internal string GetLayerBoundsDescription()
	{
		return _layerBoundsDescription;
	}

	[SpecialName]
	[CompilerGenerated]
	private void SetLayerBoundsDescription(string boundsDescription)
	{
		_layerBoundsDescription = boundsDescription;
	}

	[SpecialName]
	[CompilerGenerated]
	internal Rect GetLayerBounds()
	{
		return _layerBounds;
	}

	[SpecialName]
	[CompilerGenerated]
	private void SetLayerBounds(Rect layerBounds)
	{
		_layerBounds = layerBounds;
	}

	[SpecialName]
	internal string GetSourceLayerName()
	{
		return sourceLayerName;
	}

	[SpecialName]
	internal bool IsPrimaryUiElement()
	{
		return UGUIParser.IsPrimaryUiType(UIType);
	}

	[SpecialName]
	internal bool HasImageReference()
	{
		return !string.IsNullOrEmpty(GetImageReferenceKey());
	}

	[SpecialName]
	internal bool HasPrefabReference()
	{
		return !string.IsNullOrEmpty(GetPrefabReferenceKey());
	}

	[SpecialName]
	internal string GetImageReferenceKey()
	{
		if (TryReadImageReferenceDirective(out var text))
		{
			return NormalizeReferenceAssetKey(text);
		}
		return null;
	}

	[SpecialName]
	internal string GetRawImageReference()
	{
		if (!TryReadImageReferenceDirective(out var result))
		{
			return null;
		}
		return result;
	}

	[SpecialName]
	internal string GetPrefabReferenceKey()
	{
		if (!TryReadPrefabReferenceDirective(out var text))
		{
			return null;
		}
		return NormalizeReferenceAssetKey(text);
	}

	[SpecialName]
	internal string GetRawPrefabReference()
	{
		if (!TryReadPrefabReferenceDirective(out var result))
		{
			return null;
		}
		return result;
	}

	[SpecialName]
	internal string GetGameObjectLookupKey()
	{
		return NormalizeLayerLookupKey(base.gameObject?.name, true);
	}

	[SpecialName]
	internal bool ShouldExportHighBitDepth()
	{
		if (PsdReaderProductAccess.IsAvailable && GetBoundPsdLayer() != null && GetBoundPsdLayer().Document != null)
		{
			return GetBoundPsdLayer().Document.Depth == 16;
		}
		return false;
	}

	[SpecialName]
	internal PsdLayer GetBoundPsdLayer()
	{
		return _boundPsdLayer;
	}

	[SpecialName]
	internal void SetBoundPsdLayer(PsdLayer psdLayer)
	{
		_boundPsdLayer = psdLayer;
		if (_boundPsdLayer != null)
		{
			mLayerType = _boundPsdLayer.GetUiLayerType();
			SetLayerBounds(_boundPsdLayer.GetCenteredLayerRect());
			SetLayerBoundsDescription($"{GetLayerBounds()}");
		}
	}

	internal static string NormalizeLayerLookupKey(object layerName, bool P_1 = false)
	{
		if (string.IsNullOrWhiteSpace((string)layerName))
		{
			return null;
		}
		return ((!P_1) ? PsdAssetNameUtilities.SanitizeLayerAssetName(((string)layerName).Trim()) : PsdAssetNameUtilities.SanitizeFileName(((string)layerName).Trim())).ToLowerInvariant();
	}

	internal static string BuildGeneratedObjectName(object layerName, int layerIndex = -1)
	{
		string text = ((!string.IsNullOrWhiteSpace((string)layerName)) ? ((string)layerName).Trim() : string.Empty);
		UGUIParser uGUIParser = UGUIParser.Instance;
		if (uGUIParser != null && uGUIParser.ShouldTransliterateChineseNames() && !string.IsNullOrEmpty(text))
		{
			text = PsdAssetNameUtilities.TransliterateChineseToPinyin(text);
		}
		if (uGUIParser != null)
		{
			text = uGUIParser.RemoveRecognizedLayerTags(text);
		}
		text = PsdAssetNameUtilities.SanitizeLayerAssetName(text);
		if (string.IsNullOrEmpty(text))
		{
			if (layerIndex < 0)
			{
				return "PsdLayer";
			}
			return $"PsdLayer-{layerIndex}";
		}
		return text;
	}

	internal string GetGeneratedObjectName()
	{
		return BuildGeneratedObjectName(ResolveEffectiveLayerName(), BindPsdLayerIndex);
	}

	internal static string SanitizeLayerName(object layerName)
	{
		if (!string.IsNullOrWhiteSpace((string)layerName))
		{
			return PsdAssetNameUtilities.SanitizeLayerAssetName(((string)layerName).Trim());
		}
		return null;
	}

	internal static string NormalizeReferenceAssetKey(object referenceName)
	{
		if (string.IsNullOrWhiteSpace((string)referenceName))
		{
			return null;
		}
		string text = ((string)referenceName).Trim();
		UGUIParser uGUIParser = UGUIParser.Instance;
		if (uGUIParser != null && uGUIParser.ShouldTransliterateChineseNames())
		{
			text = PsdAssetNameUtilities.TransliterateChineseToPinyin(text);
		}
		string text2 = PsdAssetNameUtilities.SanitizeRelativeAssetPath(text);
		if (!string.IsNullOrWhiteSpace(text2))
		{
			return text2;
		}
		return null;
	}

	internal static string GetAssetLeafName(object layerPath, string defaultName = null)
	{
		string text = PsdAssetNameUtilities.GetFinalPathSegment(layerPath);
		if (string.IsNullOrWhiteSpace(text))
		{
			if (string.IsNullOrWhiteSpace(defaultName))
			{
				return "PsdLayer";
			}
			return defaultName;
		}
		return text;
	}

	internal void SetSourceLayerName(string layerName)
	{
		sourceLayerName = layerName;
	}

	private string ResolveEffectiveLayerName()
	{
		string text = base.gameObject?.name;
		UGUIParser uGUIParser = UGUIParser.Instance;
		if (!string.IsNullOrWhiteSpace(text) && uGUIParser != null)
		{
			if (!string.IsNullOrWhiteSpace(sourceLayerName))
			{
				string b = uGUIParser.BuildEditorLayerName(sourceLayerName, BindPsdLayerIndex);
				if (!string.Equals(text, b, StringComparison.Ordinal))
				{
					return text;
				}
			}
			else
			{
				string text2 = GetBoundPsdLayer()?.GetLayerName();
				if (!string.IsNullOrWhiteSpace(text2))
				{
					string b2 = uGUIParser.BuildEditorLayerName(text2, BindPsdLayerIndex);
					if (!string.Equals(text, b2, StringComparison.Ordinal))
					{
						return text;
					}
				}
			}
		}
		if (string.IsNullOrWhiteSpace(sourceLayerName))
		{
			string text3 = GetBoundPsdLayer()?.GetLayerName();
			if (string.IsNullOrWhiteSpace(text3))
			{
				return text;
			}
			return text3;
		}
		return sourceLayerName;
	}

	private bool TryReadImageReferenceDirective(out string referenceName)
	{
		referenceName = null;
		string text = ResolveEffectiveLayerName();
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		text = text.Trim();
		if (text.StartsWith("ref ", StringComparison.OrdinalIgnoreCase))
		{
			referenceName = text.Substring("ref ".Length).Trim();
			UGUIParser uGUIParser = UGUIParser.Instance;
			if (uGUIParser != null)
			{
				referenceName = uGUIParser.RemoveRecognizedLayerTags(referenceName);
			}
			return !string.IsNullOrEmpty(referenceName);
		}
		return false;
	}

	private bool TryReadPrefabReferenceDirective(out string prefabReferenceName)
	{
		prefabReferenceName = null;
		string text = ResolveEffectiveLayerName();
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		text = text.Trim();
		if (text.StartsWith("refp ", StringComparison.OrdinalIgnoreCase))
		{
			prefabReferenceName = text.Substring("refp ".Length).Trim();
			UGUIParser uGUIParser = UGUIParser.Instance;
			if (uGUIParser != null)
			{
				prefabReferenceName = uGUIParser.RemoveRecognizedLayerTags(prefabReferenceName);
			}
			return !string.IsNullOrEmpty(prefabReferenceName);
		}
		return false;
	}

	private void OnDestroy()
	{
		ReleasePreviewTexture();
	}

	internal void SetUiType(GUIType uiType, bool refreshHelpers = true)
	{
		UGUIParser uGUIParser = UGUIParser.Instance;
		UIType = ((!(uGUIParser != null)) ? uiType : uGUIParser.NormalizeUiTypeForBackend(uiType));
		RemoveUiHelpers();
		if (refreshHelpers)
		{
			RefreshUiHelperBindings(true);
		}
	}

	internal void SetUiTypeAndRole(GUIType uiType, GUIType roleUiType, bool refreshHelpers = true)
	{
		RoleUIType = roleUiType;
		SetUiType(uiType, refreshHelpers);
	}

	internal void RefreshUiHelperBindings(bool refreshParentHelper = false)
	{
		UGUIParser uGUIParser = UGUIParser.Instance;
		if (uGUIParser != null)
		{
			UIType = uGUIParser.NormalizeUiTypeForBackend(UIType);
		}
		if (UIType == GUIType.Null)
		{
			return;
		}
		Type type = UGUIParser.Instance.GetUiHelperType(UIType);
		if (type != null)
		{
			((base.gameObject.GetComponent(type) ?? base.gameObject.AddComponent(type)) as UIHelperBase).ParseAndAttachUIElements();
		}
		if (refreshParentHelper)
		{
			Transform parent = base.transform.parent;
			while (parent != null)
			{
				UIHelperBase component = parent.GetComponent<UIHelperBase>();
				if (!(component != null))
				{
					parent = parent.parent;
					continue;
				}
				component.ParseAndAttachUIElements();
				break;
			}
		}
		EditorUtility.SetDirty(this);
	}

	private void RemoveUiHelpers()
	{
		UIHelperBase[] components = GetComponents<UIHelperBase>();
		if (components != null)
		{
			UIHelperBase[] array = components;
			for (int i = 0; i < array.Length; i++)
			{
				UnityEngine.Object.DestroyImmediate(array[i]);
			}
		}
		EditorUtility.SetDirty(this);
	}

	internal bool ShouldExportImage()
	{
		if (UIType != GUIType.FillColor && LayerType != PsdLayerType.FillLayer)
		{
			if (base.gameObject.activeSelf)
			{
				return markToExport;
			}
			return false;
		}
		return false;
	}

	private static string NormalizeExportFileStem(object layerName, bool lowercase, object defaultName)
	{
		string text = PsdAssetNameUtilities.SanitizeLayerAssetName(layerName);
		if (string.IsNullOrWhiteSpace(text))
		{
			text = PsdAssetNameUtilities.SanitizeLayerAssetName(defaultName);
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "PsdLayer";
		}
		if (lowercase)
		{
			text = text.ToLowerInvariant();
		}
		return text;
	}

	private static string GetExportFileStem(object layerNode, bool lowercase)
	{
		if (!((UnityEngine.Object)layerNode == null))
		{
			return NormalizeExportFileStem(string.IsNullOrWhiteSpace(((UnityEngine.Object)layerNode).name) ? ((PsdLayerNode)layerNode).UIType.ToString() : ((UnityEngine.Object)layerNode).name, lowercase, ((PsdLayerNode)layerNode).UIType.ToString());
		}
		return "PsdLayer";
	}

	private string DisambiguateExportFileStem(Psd2UIFormConverter converter, string exportName, bool lowercase)
	{
		_003C_003Ec__DisplayClass65_0 exportNameContext = new _003C_003Ec__DisplayClass65_0();
		exportNameContext.SourceLayer = this;
		exportNameContext.LowercaseExportName = lowercase;
		exportNameContext.RequestedExportName = exportName;
		if (!(converter == null) && !string.IsNullOrWhiteSpace(exportNameContext.RequestedExportName))
		{
			PsdLayerNode[] componentsInChildren = converter.GetComponentsInChildren<PsdLayerNode>(includeInactive: true);
			if (componentsInChildren != null && componentsInChildren.Length != 0)
			{
				PsdLayerNode[] array = componentsInChildren.Where((PsdLayerNode node) => node != null && (node == exportNameContext.SourceLayer || node.ShouldExportImage())).ToArray();
				if (array.Length > 1)
				{
					var array2 = array.Select((PsdLayerNode node) => new
					{
						Node = node,
						Name = GetExportFileStem(node, exportNameContext.LowercaseExportName)
					}).ToArray();
					HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					var array3 = array2;
					foreach (var anon in array3)
					{
						hashSet.Add(anon.Name);
					}
					PsdLayerNode[] array4 = (from entry in array2
						where string.Equals(entry.Name, exportNameContext.RequestedExportName, StringComparison.OrdinalIgnoreCase)
						select entry.Node into node
						orderby node.BindPsdLayerIndex, GetLayerEntityId(node)
						select node).ToArray();
					if (array4.Length > 1)
					{
						int num2 = Array.IndexOf(array4, this);
						if (num2 > 0)
						{
							int num3 = 1;
							for (int num4 = 1; num4 <= num2; num4++)
							{
								for (; hashSet.Contains($"{exportNameContext.RequestedExportName}_{num3}"); num3++)
								{
								}
								if (num4 != num2)
								{
									num3++;
									continue;
								}
								return $"{exportNameContext.RequestedExportName}_{num3}";
							}
							return exportNameContext.RequestedExportName;
						}
						return exportNameContext.RequestedExportName;
					}
					return exportNameContext.RequestedExportName;
				}
				return exportNameContext.RequestedExportName;
			}
			return exportNameContext.RequestedExportName;
		}
		return exportNameContext.RequestedExportName;
	}

	private static string ResolveExportDirectoryAndFileStem(object exportDirectory, object relativeExportPath, bool lowercase, object defaultName, out string resolvedDirectory)
	{
		resolvedDirectory = (string)exportDirectory;
		if (!string.IsNullOrWhiteSpace((string)relativeExportPath))
		{
			string text = Path.ChangeExtension(((string)relativeExportPath).Trim().Replace("\\", "/"), null);
			text = PsdAssetNameUtilities.SanitizeRelativeAssetPath(text);
			if (!string.IsNullOrWhiteSpace(text))
			{
				if (lowercase)
				{
					text = text.ToLowerInvariant();
				}
				string text2 = Path.GetDirectoryName(text)?.Replace("\\", "/");
				if (!string.IsNullOrWhiteSpace(text2))
				{
					resolvedDirectory = Path.Combine((string)exportDirectory, text2).Replace("\\", "/");
				}
				return NormalizeExportFileStem(PsdAssetNameUtilities.GetFinalPathSegment(text), lowercase, defaultName);
			}
			return NormalizeExportFileStem(defaultName, lowercase, defaultName);
		}
		return NormalizeExportFileStem(defaultName, lowercase, defaultName);
	}

	internal string ExportLayerImage(bool forceSpriteImport = false, string exportDirectory = null, string relativeExportPath = null, bool lowercase = true, bool prepareNineSlice = false, bool ignoreReference = false)
	{
		Psd2UIFormConverter psd2UIFormConverter = Psd2UIFormConverter.Instance;
		if (psd2UIFormConverter != null && psd2UIFormConverter.TryGetCachedImageExport(this, out var result))
		{
			return result;
		}
		bool flag = false;
		string text = null;
		string text2 = exportDirectory;
		string text3 = relativeExportPath;
		if (psd2UIFormConverter != null && string.IsNullOrEmpty(text2) && !HasImageReference() && psd2UIFormConverter.TryGetSharedImageExportTarget(this, out var text4, out var text5))
		{
			text2 = text4;
			text3 = text5;
			flag = true;
			text = text5;
			forceSpriteImport = true;
		}
		if (!ignoreReference && HasImageReference() && psd2UIFormConverter != null)
		{
			return psd2UIFormConverter.EnsureReferencedImageExport(this, prepareNineSlice);
		}
		string result2 = null;
		if (UIType != GUIType.FillColor && LayerType != PsdLayerType.FillLayer)
		{
			if (GetBoundPsdLayer() != null)
			{
				PsdRenderedImage psdRenderedImage = GetBoundPsdLayer().Render();
				if (psdRenderedImage == null || psdRenderedImage.IsEmpty)
				{
					return null;
				}
				bool flag2 = UIType != GUIType.FillColor && UIType != GUIType.RawImage;
				text2 = (string.IsNullOrWhiteSpace(text2) ? Psd2UIFormConverter.Instance.GetUiImageOutputDirectory() : text2);
				if (!Directory.Exists(text2))
				{
					try
					{
						Directory.CreateDirectory(text2);
						AssetDatabase.Refresh();
					}
					catch (Exception)
					{
						return null;
					}
				}
				string text6 = ((!string.IsNullOrWhiteSpace(text3)) ? GetExportFileStem(this, lowercase) : DisambiguateExportFileStem(psd2UIFormConverter, GetExportFileStem(this, lowercase), lowercase));
				string text7 = ((!string.IsNullOrWhiteSpace(text3)) ? ResolveExportDirectoryAndFileStem(text2, text3, lowercase, text6, out text2) : text6);
				if (!Directory.Exists(text2))
				{
					try
					{
						Directory.CreateDirectory(text2);
						AssetDatabase.Refresh();
					}
					catch (Exception)
					{
						return null;
					}
				}
				string text8 = ".png";
				string text9 = Path.Combine(text2, text7 + text8).Replace("\\", "/");
				byte[] array = PsdTextureAssetUtility.EncodePng(psdRenderedImage);
				if (array == null || array.Length == 0)
				{
					return null;
				}
				File.WriteAllBytes(text9, array);
				result2 = text9;
				AssetDatabase.Refresh();
				Psd2UIFormConverter.ConfigureExportedTextureImports(new string[1] { text9 }, flag2 || forceSpriteImport, psdRenderedImage.IsHighBitDepth);
				if (prepareNineSlice)
				{
					Psd2UIFormConverter.EnsureSpriteNineSliceBorder(text9);
				}
				if (flag && psd2UIFormConverter != null)
				{
					psd2UIFormConverter.CacheImageExportPath(string.IsNullOrEmpty(text) ? GetGameObjectLookupKey() : text, text9);
				}
			}
			return result2;
		}
		return null;
	}

	internal bool EnsurePreviewTexture(bool forceRefresh = false)
	{
		string text = TryBuildPreviewCacheKey();
		bool flag2;
		bool flag = (flag2 = PreviewTexture != null) && !string.IsNullOrEmpty(_previewCacheKey) && string.Equals(_previewCacheKey, text, StringComparison.Ordinal);
		if (!(!forceRefresh && flag))
		{
			if (GetBoundPsdLayer() != null)
			{
				if (flag2 && (!flag || forceRefresh))
				{
					ReleasePreviewTexture();
				}
				PreviewTexture = PsdLayerPreviewCache.Acquire(text, RenderPreviewTexture);
				if (PreviewTexture != null)
				{
					_previewCacheKey = text;
				}
				return PreviewTexture != null;
			}
			ReleasePreviewTexture();
			return false;
		}
		return true;
	}

	private string TryBuildPreviewCacheKey()
	{
		if (GetBoundPsdLayer() != null)
		{
			try
			{
				return BuildPreviewCacheKey();
			}
			catch
			{
				return string.Empty;
			}
		}
		return string.Empty;
	}

	private string BuildPreviewCacheKey()
	{
		string text = NormalizePreviewCacheKeyPart(Psd2UIFormConverter.Instance?.GetPsdAssetPath());
		string text2 = NormalizePreviewCacheKeyPart(Psd2UIFormConverter.Instance?.psdAssetChangeTime);
		if (string.IsNullOrWhiteSpace(text2) && !string.IsNullOrWhiteSpace(text))
		{
			text2 = GetPsdFileModificationStamp(text);
		}
		string text3 = NormalizePreviewCacheKeyPart(GetBoundPsdLayer().GetPreviewProtectionFingerprint());
		return string.Join("|", "PsdLayerPreview", text, text2, text3, BindPsdLayerIndex.ToString(CultureInfo.InvariantCulture), NormalizePreviewCacheKeyPart(GetSourceLayerName()), GetBoundPsdLayer().Left.ToString(CultureInfo.InvariantCulture), GetBoundPsdLayer().Top.ToString(CultureInfo.InvariantCulture), GetBoundPsdLayer().Width.ToString(CultureInfo.InvariantCulture), GetBoundPsdLayer().Height.ToString(CultureInfo.InvariantCulture), (!GetBoundPsdLayer().IsGroup) ? "0" : "1", (!GetBoundPsdLayer().IsVisible) ? "0" : "1");
	}

	private static string NormalizePreviewCacheKeyPart(object P_0)
	{
		if (string.IsNullOrWhiteSpace((string)P_0))
		{
			return string.Empty;
		}
		return ((string)P_0).Trim().Replace("\\", "/").ToLowerInvariant();
	}

	private static string GetPsdFileModificationStamp(object P_0)
	{
		if (!string.IsNullOrWhiteSpace((string)P_0) && File.Exists((string)P_0))
		{
			return new FileInfo((string)P_0).LastWriteTimeUtc.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
		}
		return string.Empty;
	}

	private void ReleasePreviewTexture()
	{
		if (string.IsNullOrWhiteSpace(_previewCacheKey))
		{
			if (PreviewTexture != null)
			{
				UnityEngine.Object.DestroyImmediate(PreviewTexture);
			}
		}
		else if (!PsdLayerPreviewCache.Release(_previewCacheKey) && PreviewTexture != null)
		{
			UnityEngine.Object.DestroyImmediate(PreviewTexture);
		}
		PreviewTexture = null;
		_previewCacheKey = string.Empty;
	}

	internal Texture2D RenderPreviewTexture()
	{
		if (GetBoundPsdLayer() == null)
		{
			return null;
		}
		PsdRenderedImage psdRenderedImage = GetBoundPsdLayer().RenderPreview();
		if (psdRenderedImage != null && !psdRenderedImage.IsEmpty)
		{
			return CreateTextureFromRenderedImage(psdRenderedImage, true);
		}
		return null;
	}

	private static Texture2D CreateTextureFromRenderedImage(object renderedImage, bool hideAndDontSave)
	{
		if (renderedImage != null && !((PsdRenderedImage)renderedImage).IsEmpty)
		{
			TextureFormat textureFormat = (((PsdRenderedImage)renderedImage).IsHighBitDepth ? TextureFormat.RGBA64 : TextureFormat.RGBA32);
			Texture2D texture2D = new Texture2D(((PsdRenderedImage)renderedImage).Width, ((PsdRenderedImage)renderedImage).Height, textureFormat, mipChain: false);
			texture2D.hideFlags = (hideAndDontSave ? HideFlags.HideAndDontSave : HideFlags.None);
			texture2D.alphaIsTransparency = true;
			if (((PsdRenderedImage)renderedImage).IsHighBitDepth)
			{
				ushort[] rgba = ((PsdRenderedImage)renderedImage).Rgba64;
				byte[] array = new byte[rgba.Length * 2];
				Buffer.BlockCopy(rgba, 0, array, 0, array.Length);
				texture2D.LoadRawTextureData(array);
			}
			else
			{
				texture2D.LoadRawTextureData(((PsdRenderedImage)renderedImage).Rgba32);
			}
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			return texture2D;
		}
		return null;
	}

	internal static Sprite LoadSpriteAsset(object assetPath)
	{
		if (!string.IsNullOrWhiteSpace((string)assetPath))
		{
			Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>((string)assetPath);
			if (sprite != null)
			{
				return sprite;
			}
			return AssetDatabase.LoadAllAssetsAtPath((string)assetPath).OfType<Sprite>().FirstOrDefault();
		}
		return null;
	}

	internal static string FindMatchingExportedImagePath(object exportDirectory, object referenceName, bool highBitDepth = false)
	{
		if (!string.IsNullOrWhiteSpace((string)exportDirectory) && !string.IsNullOrWhiteSpace((string)referenceName))
		{
			string text = Path.Combine((string)exportDirectory, (string)referenceName + ".png").Replace("\\", "/");
			if (PsdTextureAssetUtility.MatchesExportMode(text, highBitDepth))
			{
				return text;
			}
			return null;
		}
		return null;
	}

	internal static bool ReplaceEmbeddedSpriteAsset(object assetPath, object texture, Vector4 spriteBorder, out Sprite createdSprite)
	{
		createdSprite = null;
		if (!string.IsNullOrWhiteSpace((string)assetPath) && !((UnityEngine.Object)texture == null))
		{
			Sprite[] array = AssetDatabase.LoadAllAssetsAtPath((string)assetPath).OfType<Sprite>().ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] != null)
				{
					UnityEngine.Object.DestroyImmediate(array[i], allowDestroyingAssets: true);
				}
			}
			createdSprite = Sprite.Create((Texture2D)texture, new Rect(0f, 0f, ((Texture)texture).width, ((Texture)texture).height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, spriteBorder);
			createdSprite.name = Path.GetFileNameWithoutExtension((string)assetPath);
			AssetDatabase.AddObjectToAsset(createdSprite, (string)assetPath);
			EditorUtility.SetDirty((UnityEngine.Object)texture);
			EditorUtility.SetDirty(createdSprite);
			AssetDatabase.SaveAssets();
			AssetDatabase.ImportAsset((string)assetPath, ImportAssetOptions.ForceUpdate);
			createdSprite = LoadSpriteAsset(assetPath);
			return createdSprite != null;
		}
		return false;
	}

	internal bool TryGetLayerFillColor(out Color layerColor)
	{
		layerColor = default(Color);
		if (GetBoundPsdLayer() == null)
		{
			return false;
		}
		if (GetBoundPsdLayer().TryGetStructuralLayerColor(out layerColor))
		{
			return true;
		}
		if (TrySampleDominantInteriorColor(GetBoundPsdLayer().RenderPreview(), out layerColor))
		{
			return true;
		}
		return false;
	}

	private static bool TrySampleDominantInteriorColor(object renderedImage, out Color dominantColor)
	{
		dominantColor = default(Color);
		if (renderedImage != null && !((PsdRenderedImage)renderedImage).IsEmpty && ((PsdRenderedImage)renderedImage).Rgba32 != null && ((PsdRenderedImage)renderedImage).Rgba32.Length >= 4)
		{
			int num = ((((PsdRenderedImage)renderedImage).Width > 1) ? Mathf.Max(1, Mathf.RoundToInt((float)((PsdRenderedImage)renderedImage).Width * 0.25f)) : 0);
			int num2 = ((((PsdRenderedImage)renderedImage).Height > 1) ? Mathf.Max(1, Mathf.RoundToInt((float)((PsdRenderedImage)renderedImage).Height * 0.25f)) : 0);
			int num3 = ((PsdRenderedImage)renderedImage).Width / 2;
			int num4 = ((PsdRenderedImage)renderedImage).Height / 2;
			uint[] array = new uint[4];
			int num5 = 0;
			if (TryReadOpaqueRgbaPixel(renderedImage, num3, Mathf.Clamp(num4 - num2, 0, ((PsdRenderedImage)renderedImage).Height - 1), out var num6))
			{
				array[num5++] = num6;
			}
			if (TryReadOpaqueRgbaPixel(renderedImage, num3, Mathf.Clamp(num4 + num2, 0, ((PsdRenderedImage)renderedImage).Height - 1), out num6))
			{
				array[num5++] = num6;
			}
			if (TryReadOpaqueRgbaPixel(renderedImage, Mathf.Clamp(num3 - num, 0, ((PsdRenderedImage)renderedImage).Width - 1), num4, out num6))
			{
				array[num5++] = num6;
			}
			if (TryReadOpaqueRgbaPixel(renderedImage, Mathf.Clamp(num3 + num, 0, ((PsdRenderedImage)renderedImage).Width - 1), num4, out num6))
			{
				array[num5++] = num6;
			}
			if (num5 <= 0)
			{
				return false;
			}
			uint num7 = array[0];
			int num8 = 1;
			for (int i = 0; i < num5; i++)
			{
				uint num9 = array[i];
				int num10 = 1;
				for (int j = i + 1; j < num5; j++)
				{
					if (array[j] == num9)
					{
						num10++;
					}
				}
				if (num10 > num8)
				{
					num8 = num10;
					num7 = num9;
				}
			}
			dominantColor = UnpackRgba32(num7);
			return true;
		}
		return false;
	}

	private static bool TryReadOpaqueRgbaPixel(object renderedImage, int pixelX, int pixelY, out uint packedRgba)
	{
		packedRgba = 0u;
		if (renderedImage != null && !((PsdRenderedImage)renderedImage).IsEmpty && ((PsdRenderedImage)renderedImage).Rgba32 != null && ((PsdRenderedImage)renderedImage).Rgba32.Length >= 4)
		{
			if (pixelX >= 0 && pixelY >= 0 && pixelX < ((PsdRenderedImage)renderedImage).Width && pixelY < ((PsdRenderedImage)renderedImage).Height)
			{
				int num = ((((PsdRenderedImage)renderedImage).Height - 1 - pixelY) * ((PsdRenderedImage)renderedImage).Width + pixelX) * 4;
				if (num >= 0 && num + 3 < ((PsdRenderedImage)renderedImage).Rgba32.Length)
				{
					byte b = ((PsdRenderedImage)renderedImage).Rgba32[num + 3];
					if (b == 0)
					{
						return false;
					}
					packedRgba = PackRgba32(((PsdRenderedImage)renderedImage).Rgba32[num], ((PsdRenderedImage)renderedImage).Rgba32[num + 1], ((PsdRenderedImage)renderedImage).Rgba32[num + 2], b);
					return true;
				}
				return false;
			}
			return false;
		}
		return false;
	}

	private static uint PackRgba32(byte red, byte green, byte blue, byte alpha)
	{
		return (uint)((red << 24) | (green << 16) | (blue << 8) | alpha);
	}

	private static Color UnpackRgba32(uint packedRgba)
	{
		return new Color32((byte)(packedRgba >> 24), (byte)(packedRgba >> 16), (byte)(packedRgba >> 8), (byte)packedRgba);
	}

	internal PsdLayerNode FindChildByUiType(GUIType uiType)
	{
		if (uiType != GUIType.Null)
		{
			return FindChildByUiTypeWithinElement(base.transform, uiType);
		}
		return null;
	}

	internal PsdLayerNode FindChildByUiTypePriority(params GUIType[] uiTps)
	{
		foreach (GUIType gUIType in uiTps)
		{
			PsdLayerNode psdLayerNode = FindChildByUiType(gUIType);
			if (psdLayerNode != null)
			{
				return psdLayerNode;
			}
		}
		return null;
	}

	internal bool TryLoadReferencedImageAsset(out UnityEngine.Object referencedAsset)
	{
		referencedAsset = null;
		if (!HasImageReference())
		{
			return false;
		}
		string text = FindMatchingExportedImagePath(UGUIParser.Instance?.GetSharedImageOutputDirectory(), GetImageReferenceKey(), ShouldExportHighBitDepth());
		if (!string.IsNullOrWhiteSpace(text))
		{
			referencedAsset = LoadSpriteAsset(text) ?? AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(text);
			return referencedAsset != null;
		}
		return false;
	}

	internal bool TryLoadReferencedPrefab(out GameObject referencedPrefab)
	{
		referencedPrefab = null;
		if (HasPrefabReference())
		{
			string text = UGUIParser.Instance?.GetSharedPrefabOutputDirectory();
			if (!string.IsNullOrWhiteSpace(text))
			{
				text = text.Replace("\\", "/");
				string assetPath = Path.Combine(text, GetPrefabReferenceKey() + ".prefab").Replace("\\", "/");
				referencedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
				if (referencedPrefab != null)
				{
					return true;
				}
				string text2 = PsdAssetNameUtilities.GetFinalPathSegment(GetPrefabReferenceKey());
				if (!string.IsNullOrWhiteSpace(text2))
				{
					string[] array = AssetDatabase.FindAssets(text2 + " t:prefab", new string[1] { text });
					if (array != null && array.Length != 0)
					{
						string[] array2 = array;
						int num = 0;
						while (true)
						{
							if (num < array2.Length)
							{
								string text3 = AssetDatabase.GUIDToAssetPath(array2[num]);
								if (!string.IsNullOrWhiteSpace(text3) && string.Equals(Path.GetFileNameWithoutExtension(text3), text2, StringComparison.OrdinalIgnoreCase))
								{
									referencedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(text3);
									if (referencedPrefab != null)
									{
										break;
									}
								}
								num++;
								continue;
							}
							string text4 = AssetDatabase.GUIDToAssetPath(array[0]);
							if (!string.IsNullOrWhiteSpace(text4))
							{
								referencedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(text4);
							}
							return referencedPrefab != null;
						}
						return true;
					}
					return false;
				}
				return false;
			}
			return false;
		}
		return false;
	}

	internal bool TryResolveReferencedLayer(out PsdLayerNode referencedLayer)
	{
		referencedLayer = null;
		if (!HasImageReference())
		{
			return false;
		}
		if (_referencedLayerCache != null && _referencedLayerCache.TryGetTarget(out referencedLayer) && referencedLayer != null)
		{
			return true;
		}
		Psd2UIFormConverter psd2UIFormConverter = Psd2UIFormConverter.Instance;
		if (!(psd2UIFormConverter == null))
		{
			referencedLayer = psd2UIFormConverter.FindLayerByLookupKey(GetImageReferenceKey());
			if (referencedLayer != null)
			{
				_referencedLayerCache = new WeakReference<PsdLayerNode>(referencedLayer);
				return true;
			}
			return false;
		}
		return false;
	}

	internal PsdLayerNode FindDescendantByUiTypeOrRole(GUIType uiType)
	{
		_003C_003Ec__DisplayClass89_0 uiTypeSearchContext = new _003C_003Ec__DisplayClass89_0();
		uiTypeSearchContext.RequiredUiType = uiType;
		PsdLayerNode[] componentsInChildren = GetComponentsInChildren<PsdLayerNode>(includeInactive: true);
		if (componentsInChildren != null && componentsInChildren.Length != 0)
		{
			return componentsInChildren.FirstOrDefault((PsdLayerNode layer) => layer.UIType == uiTypeSearchContext.RequiredUiType || layer.RoleUIType == uiTypeSearchContext.RequiredUiType);
		}
		return null;
	}

	private PsdLayerNode FindChildByUiTypeWithinElement(Transform parentTransform, GUIType uiType)
	{
		if (!(parentTransform == null))
		{
			int num = 0;
			PsdLayerNode psdLayerNode;
			while (true)
			{
				if (num < parentTransform.childCount)
				{
					Transform child = parentTransform.GetChild(num);
					PsdLayerNode component = child.GetComponent<PsdLayerNode>();
					if (component != null)
					{
						if (component.UIType == uiType || component.RoleUIType == uiType)
						{
							return component;
						}
						if (component != this && component.IsPrimaryUiElement() && component.UIType != GUIType.Null)
						{
							goto IL_0075;
						}
					}
					psdLayerNode = FindChildByUiTypeWithinElement(child, uiType);
					if (psdLayerNode != null)
					{
						break;
					}
					goto IL_0075;
				}
				return null;
				IL_0075:
				num++;
			}
			return psdLayerNode;
		}
		return null;
	}

	internal bool TryGetPsdTextLayerInfo(out PsdTextLayerInfo textLayerInfo)
	{
		textLayerInfo = null;
		if (GetBoundPsdLayer() != null)
		{
			return GetBoundPsdLayer().TryGetTextLayerInfo(out textLayerInfo);
		}
		return false;
	}

	internal void RebindLayerFromDocument(PsdDocument document)
	{
		if (document != null)
		{
			if (BindPsdLayerIndex >= 0)
			{
				SetBoundPsdLayer(document.FindLayerByRecordIndex(BindPsdLayerIndex));
			}
		}
		else
		{
			SetBoundPsdLayer(null);
		}
	}

	internal bool TryBuildTextStyleData(out PsdTextStyleInfo textStyle)
	{
		textStyle = default(PsdTextStyleInfo);
		if (!TryGetPsdTextLayerInfo(out var psdTextLayerInfo))
		{
			return false;
		}
		textStyle = new PsdTextStyleInfo
		{
			TextContent = null,
			FontSize = 0,
			AutoLineSpacing = true,
			CharacterSpacing = 0f,
			LineSpacing = 0f,
			TextColor = Color.white,
			FontStyle = FontStyle.Normal,
			TmpFontStyle = FontStyles.Normal,
			FontName = null,
			OutlineEnabled = false,
			OutlineColor = Color.clear,
			UguiOutlineSize = 0f,
			TmpOutlineSize = 0f,
			OutlineMode = PsdTextStyleInfo.TMPOutlineMode.Center,
			ShadowEnabled = false,
			InnerShadow = false,
			ShadowColor = Color.clear,
			ShadowOffset = Vector2.zero,
			ShadowSpread = 0f,
			ShadowSoftness = 0f,
			GlowEnabled = false,
			InnerGlow = false,
			GlowColor = Color.clear,
			GlowSize = 0f,
			GlowSpread = 0f,
			GlowOffset = 0f,
			GlowPower = 0f,
			BevelEnabled = false,
			InnerBevel = false,
			BevelSize = 0f,
			BevelDepth = 0f,
			BevelSoften = 0f,
			BevelAngle = 0f,
			BevelAltitude = 0f,
			BevelHighlightColor = Color.white,
			BevelHighlightOpacity = 1f,
			BevelShadowColor = Color.black,
			BevelShadowOpacity = 1f,
			GradientEnabled = false,
			GradientAngle = 0f,
			GradientReverse = false,
			GradientStyleKey = null,
			GradientBlendModeKey = null,
			GradientStops = null
		};
		textStyle.TextContent = psdTextLayerInfo.Text;
		textStyle.FontSize = Mathf.Max(1, Mathf.FloorToInt(psdTextLayerInfo.FontSize));
		textStyle.AutoLineSpacing = psdTextLayerInfo.AutoLeading || psdTextLayerInfo.Leading <= 0f;
		textStyle.TextColor = ConvertPsdColorToUnityColor(psdTextLayerInfo.Color);
		if (psdTextLayerInfo.FauxBold && psdTextLayerInfo.FauxItalic)
		{
			textStyle.FontStyle = FontStyle.BoldAndItalic;
		}
		else if (!psdTextLayerInfo.FauxBold)
		{
			if (!psdTextLayerInfo.FauxItalic)
			{
				textStyle.FontStyle = FontStyle.Normal;
			}
			else
			{
				textStyle.FontStyle = FontStyle.Italic;
			}
		}
		else
		{
			textStyle.FontStyle = FontStyle.Bold;
		}
		if (psdTextLayerInfo.FauxItalic)
		{
			textStyle.TmpFontStyle |= FontStyles.Italic;
		}
		if (psdTextLayerInfo.FauxBold)
		{
			textStyle.TmpFontStyle |= FontStyles.Bold;
		}
		if (psdTextLayerInfo.Underline)
		{
			textStyle.TmpFontStyle |= FontStyles.Underline;
		}
		if (psdTextLayerInfo.Strikethrough)
		{
			textStyle.TmpFontStyle |= FontStyles.Strikethrough;
		}
		textStyle.FontName = psdTextLayerInfo.FontName;
		textStyle.CharacterSpacing = psdTextLayerInfo.Tracking * 0.1f;
		textStyle.LineSpacing = psdTextLayerInfo.Leading;
		CopyPsdTextEffects(psdTextLayerInfo, ref textStyle);
		return true;
	}

	private static void CopyPsdTextEffects(object textLayerInfo, ref PsdTextStyleInfo textStyle)
	{
		if (textLayerInfo == null)
		{
			return;
		}
		if (((PsdTextLayerInfo)textLayerInfo).Shadow != null && ((PsdTextLayerInfo)textLayerInfo).Shadow.Enabled)
		{
			textStyle.ShadowEnabled = true;
			textStyle.InnerShadow = false;
			textStyle.ShadowColor = ConvertPsdColorToUnityColor(((PsdTextLayerInfo)textLayerInfo).Shadow.Color);
			textStyle.ShadowOffset = Quaternion.Euler(0f, 0f, ((PsdTextLayerInfo)textLayerInfo).Shadow.Angle) * (Vector2.left * ((PsdTextLayerInfo)textLayerInfo).Shadow.Distance);
			textStyle.ShadowSpread = Mathf.Clamp01(((PsdTextLayerInfo)textLayerInfo).Shadow.Spread);
			textStyle.ShadowSoftness = Mathf.Max(0f, ((PsdTextLayerInfo)textLayerInfo).Shadow.Blur);
		}
		else if (((PsdTextLayerInfo)textLayerInfo).InnerShadow != null && ((PsdTextLayerInfo)textLayerInfo).InnerShadow.Enabled)
		{
			textStyle.ShadowEnabled = true;
			textStyle.InnerShadow = true;
			textStyle.ShadowColor = ConvertPsdColorToUnityColor(((PsdTextLayerInfo)textLayerInfo).InnerShadow.Color);
			textStyle.ShadowOffset = Quaternion.Euler(0f, 0f, ((PsdTextLayerInfo)textLayerInfo).InnerShadow.Angle) * (Vector2.left * ((PsdTextLayerInfo)textLayerInfo).InnerShadow.Distance);
			textStyle.ShadowSpread = Mathf.Clamp01(((PsdTextLayerInfo)textLayerInfo).InnerShadow.Spread);
			textStyle.ShadowSoftness = Mathf.Max(0f, ((PsdTextLayerInfo)textLayerInfo).InnerShadow.Blur);
		}
		if (((PsdTextLayerInfo)textLayerInfo).Stroke != null && ((PsdTextLayerInfo)textLayerInfo).Stroke.Enabled)
		{
			textStyle.OutlineEnabled = true;
			textStyle.UguiOutlineSize = GetStrokeOutwardExtent(((PsdTextLayerInfo)textLayerInfo).Stroke);
			textStyle.TmpOutlineSize = Mathf.Max(0f, ((PsdTextLayerInfo)textLayerInfo).Stroke.Size);
			textStyle.OutlineMode = ConvertStrokePositionToTmpOutline(((PsdTextLayerInfo)textLayerInfo).Stroke.Position);
			textStyle.OutlineColor = ConvertPsdColorToUnityColor(((PsdTextLayerInfo)textLayerInfo).Stroke.Color);
		}
		PsdTextGlowInfo psdTextGlowInfo = ((((PsdTextLayerInfo)textLayerInfo).OuterGlow != null && ((PsdTextLayerInfo)textLayerInfo).OuterGlow.Enabled) ? ((PsdTextLayerInfo)textLayerInfo).OuterGlow : ((((PsdTextLayerInfo)textLayerInfo).InnerGlow == null || !((PsdTextLayerInfo)textLayerInfo).InnerGlow.Enabled) ? null : ((PsdTextLayerInfo)textLayerInfo).InnerGlow));
		if (psdTextGlowInfo != null)
		{
			textStyle.GlowEnabled = true;
			textStyle.InnerGlow = psdTextGlowInfo.Inner;
			textStyle.GlowColor = ConvertPsdColorToUnityColor(psdTextGlowInfo.Color);
			textStyle.GlowSize = Mathf.Max(0f, psdTextGlowInfo.Size);
			textStyle.GlowSpread = Mathf.Clamp01(psdTextGlowInfo.Spread);
			textStyle.GlowOffset = 0f;
			textStyle.GlowPower = 0.75f;
		}
		if (((PsdTextLayerInfo)textLayerInfo).Bevel != null && ((PsdTextLayerInfo)textLayerInfo).Bevel.Enabled)
		{
			textStyle.BevelEnabled = true;
			textStyle.InnerBevel = ((PsdTextLayerInfo)textLayerInfo).Bevel.Inner;
			textStyle.BevelSize = Mathf.Max(0f, ((PsdTextLayerInfo)textLayerInfo).Bevel.Size);
			textStyle.BevelDepth = Mathf.Max(0f, ((PsdTextLayerInfo)textLayerInfo).Bevel.Depth);
			textStyle.BevelSoften = Mathf.Max(0f, ((PsdTextLayerInfo)textLayerInfo).Bevel.Soften);
			textStyle.BevelAngle = ((PsdTextLayerInfo)textLayerInfo).Bevel.Angle;
			textStyle.BevelAltitude = Mathf.Clamp(((PsdTextLayerInfo)textLayerInfo).Bevel.Altitude, 0f, 90f);
			textStyle.BevelHighlightColor = ConvertPsdColorToUnityColor(((PsdTextLayerInfo)textLayerInfo).Bevel.HighlightColor);
			textStyle.BevelHighlightOpacity = Mathf.Clamp01(((PsdTextLayerInfo)textLayerInfo).Bevel.HighlightOpacity);
			textStyle.BevelShadowColor = ConvertPsdColorToUnityColor(((PsdTextLayerInfo)textLayerInfo).Bevel.ShadowColor);
			textStyle.BevelShadowOpacity = Mathf.Clamp01(((PsdTextLayerInfo)textLayerInfo).Bevel.ShadowOpacity);
		}
		if (((PsdTextLayerInfo)textLayerInfo).Gradient != null && ((PsdTextLayerInfo)textLayerInfo).Gradient.Enabled && ((PsdTextLayerInfo)textLayerInfo).Gradient.Stops != null && ((PsdTextLayerInfo)textLayerInfo).Gradient.Stops.Length >= 2)
		{
			PsdTextGradientStop[] stops = ((PsdTextLayerInfo)textLayerInfo).Gradient.Stops;
			PsdUiGradientStop[] array = new PsdUiGradientStop[stops.Length];
			for (int i = 0; i < stops.Length; i++)
			{
				PsdTextGradientStop psdTextGradientStop = stops[i];
				array[i] = new PsdUiGradientStop
				{
					Position = Mathf.Clamp01(psdTextGradientStop.Location),
					Color = ConvertPsdColorToUnityColor(psdTextGradientStop.Color)
				};
			}
			Array.Sort(array, (PsdUiGradientStop a, PsdUiGradientStop b) => a.Position.CompareTo(b.Position));
			textStyle.GradientEnabled = true;
			textStyle.GradientAngle = ((PsdTextLayerInfo)textLayerInfo).Gradient.Angle;
			textStyle.GradientReverse = ((PsdTextLayerInfo)textLayerInfo).Gradient.Reverse;
			textStyle.GradientStyleKey = ((PsdTextLayerInfo)textLayerInfo).Gradient.StyleKey;
			textStyle.GradientBlendModeKey = ((PsdTextLayerInfo)textLayerInfo).Gradient.BlendModeKey;
			textStyle.GradientStops = array;
		}
	}

	private static PsdTextStyleInfo.TMPOutlineMode ConvertStrokePositionToTmpOutline(PsdTextStrokePosition strokePosition)
	{
		return strokePosition switch
		{
			PsdTextStrokePosition.Center => PsdTextStyleInfo.TMPOutlineMode.Center, 
			PsdTextStrokePosition.Inside => PsdTextStyleInfo.TMPOutlineMode.Inside, 
			_ => PsdTextStyleInfo.TMPOutlineMode.Outside, 
		};
	}

	private static float GetStrokeOutwardExtent(object strokeInfo)
	{
		if (strokeInfo == null)
		{
			return 0f;
		}
		float num = Mathf.Max(0f, ((PsdTextStrokeInfo)strokeInfo).Size);
		return ((PsdTextStrokeInfo)strokeInfo).Position switch
		{
			PsdTextStrokePosition.Center => num * 0.5f, 
			PsdTextStrokePosition.Inside => 0f, 
			_ => num, 
		};
	}

	private static Color ConvertPsdColorToUnityColor(PsdColor psdColor)
	{
		return new Color((float)(int)psdColor.R / 255f, (float)(int)psdColor.G / 255f, (float)(int)psdColor.B / 255f, (float)(int)psdColor.A / 255f);
	}

	public PsdLayerNode()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private PsdLayerNode(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
