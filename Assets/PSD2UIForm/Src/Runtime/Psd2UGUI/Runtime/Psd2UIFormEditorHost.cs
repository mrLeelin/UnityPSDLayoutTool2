using System;
using System.Collections.Generic;
using PsdTextStyleInfoNamespace;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
	/// <summary>
	/// 运行期程序集与编辑器实现之间的唯一通道。
	///
	/// 背景：PsdLayerNode 与各 UIHelper 既是"挂在生成物上的运行期组件"（必须在播放器构建中存在），
	/// 又承载生成期/导出期逻辑（必须调用编辑器侧的程序集，例如 UGUIParser / Psd2UIFormConverter）。
	/// 两个方向不可能同时成立，因此把这些调用统一收敛到这个运行期侧定义的接口上：
	/// 运行期代码只依赖接口，编辑器程序集提供实现（<c>Psd2UIFormEditorHostAdapter</c>）并在加载时注册。
	///
	/// 约定：接口不得出现编辑器侧程序集的类型（否则运行期程序集仍会被迫引用编辑器程序集）。
	/// </summary>
	internal interface IPsd2UIFormEditorHost
	{
		// ---- 生成期布局 / 样式（原 UGUIParser） ----

		void ApplyNodeRectToUI(object node, object target, bool applyPosition = true, bool applySizeX = true, bool applySizeY = true, int padding = 0);

		Sprite ApplyImageSprite(PsdLayerNode node, Image image);

		Sprite ExportAndLoadSprite(object node, bool requiresBorder = false);

		Texture2D ExportAndLoadTexture(object node);

		void ApplyTMPTextStyle(object node, object target);

		PsdTextStyleInfo ApplyLegacyTextStyle(object node, object target);

		GUIType GetDefaultImageType();

		UGUIParseRule FindRule(GUIType uiType);

		Color GetRepresentativeColor(object node, Color fallbackColor);

		// ---- 名称 / 标签（原 UGUIParser 实例方法；Instance 为空时返回安全默认值） ----

		bool IsChineseNameConversionEnabled();

		string RemoveRecognizedLayerTags(string text);

		string BuildLayerObjectName(string name, int index);

		GUIType ApplyForcedTMPType(GUIType uiType);

		Type GetHelperComponentType(GUIType uiType);

		string GetSharedAssetsOutputDirectory();

		string GetSharedPrefabOutputDirectory();

		// ---- 生成期宿主（原 Psd2UIFormConverter） ----

		bool HasConverterInstance();

		// Psd2UIFormConverter（运行期壳）的编辑器侧逻辑挂接/摘除，
		// 由壳的 OnEnable/OnDestroy 触发，保证拆分后触发时机与拆分前完全一致。
		void AttachConverter(Psd2UIFormConverter converter);

		void DetachConverter(Psd2UIFormConverter converter);

		// 壳被销毁时的彻底清理（摘回调 + 释放 PSD 文档）。
		// 与 DetachConverter 分开：Detach 会在 Inspector 失去选中/PrefabStage 关闭时发生，
		// 那时不能释放文档，否则仍然存在的 PsdLayerNode 首次渲染会 NRE。
		void DisposeConverter(Psd2UIFormConverter converter);

		// 壳的 OnDrawGizmos 转发：Gizmos 只能由 MonoBehaviour 消息触发，
		// 而绘制体依赖 Selection 等编辑器 API，因此实现在编辑器侧。
		void DrawConverterGizmos(Psd2UIFormConverter converter);

		// 壳的 Start 转发（原 MonoBehaviour.Start 的行为：无条件重试加载文档）。
		// 必须与 Attach 分开：CreateConverterRoot 之后 psdAssetPath 才被赋值，
		// 若在 Attach 里就无条件加载会误报“源文档不存在”。
		void LoadConverterDocument(Psd2UIFormConverter converter);

		void RefreshAllHelperComponents();

		string GetSourcePsdAssetPath();

		string GetPsdAssetChangeTime();

		PsdLayerNode FindNodeByReferenceKey(string key);

		// ---- 导出 / 预览 / 采样（原 PsdTextureAssetUtility / PsdLayerPreviewCache / PsdLayerRenderer 渲染管线） ----

		string ExportImageAsset(PsdLayerNode node, bool enabled, string exportDirectory, string fileName, bool lowerCaseName, bool requiresBorder, bool skipAssetReference);

		bool MatchesTextureExportMode(string assetPath, bool preferHighBitDepth);

		Texture2D AcquirePreviewTexture(string cacheKey, PsdLayerNode node);

		bool ReleasePreviewTexture(string cacheKey);

		bool TryGetRenderedRepresentativeColor(PsdLayerNode node, out Color color);

		// ---- 产品授权（原 PsdReaderProductAccess） ----

		bool IsPsdReaderProductAvailable();
	}

	/// <summary>
	/// <see cref="IPsd2UIFormEditorHost"/> 的注册点。
	/// 编辑器程序集在加载时（<c>[InitializeOnLoadMethod]</c>）写入实现；播放器构建中始终为 null，
	/// 因此运行期调用点必须使用 <c>?.</c> 或先判空。
	/// </summary>
	internal static class Psd2UIFormEditorHost
	{
		private static IPsd2UIFormEditorHost _current;

		internal static IPsd2UIFormEditorHost Current
		{
			get { return _current; }
			set { _current = value; }
		}

		internal static bool IsRegistered
		{
			get { return _current != null; }
		}
	}
}
