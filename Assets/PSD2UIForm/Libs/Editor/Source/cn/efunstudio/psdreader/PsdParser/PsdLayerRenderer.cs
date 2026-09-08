using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PsdLicensing;
using UnityEngine;
using PsdBinaryUtilities;
using PsdLicenseEditor;

namespace cn.efunstudio.psdreader.PsdParser
{

public static class PsdLayerRenderer
{
	public static PsdRenderedImage RenderPreview(this PsdDocument document, bool includeHiddenLayers = false, bool applyClippingMasks = true)
	{
		float previewScale = ResolvePreviewRenderScale(document?.Width ?? 0, document?.Height ?? 0);
		return RenderDocument(document, includeHiddenLayers, applyClippingMasks, isPreviewRender: true, previewScale);
	}

	public static PsdRenderedImage Render(this PsdDocument document, bool includeHiddenLayers = false, bool applyClippingMasks = true)
	{
		return RenderDocument(document, includeHiddenLayers, applyClippingMasks, isPreviewRender: false, 1f);
	}

	public static PsdRenderedImage RenderPreview(this PsdLayer layer, bool includeHiddenLayers = false, bool applyClippingMasks = true)
	{
		float previewScale = ResolvePreviewRenderScale(layer?.Width ?? 0, layer?.Height ?? 0);
		return RenderLayer(layer, includeHiddenLayers, applyClippingMasks, isPreviewRender: true, previewScale);
	}

	public static PsdRenderedImage Render(this PsdLayer layer, bool includeHiddenLayers = false, bool applyClippingMasks = true)
	{
		return RenderLayer(layer, includeHiddenLayers, applyClippingMasks, isPreviewRender: false, 1f);
	}

	internal static string ComputePreviewProtectionFingerprint(this PsdLayer layer)
	{
		if (layer != null)
		{
			return PsdReaderOutputProtection.pgArWc2aJD(layer.Left, layer.Top, layer.Width, layer.Height, ComputeLayerWatermarkSalt(layer), BuildLayerPathKey(layer), BuildLayerNormalizedBounds(layer));
		}
		return string.Empty;
	}

	public static byte MultiplyAlpha(byte alpha, float opacity)
	{
		opacity = Math.Min(1f, Math.Max(0f, opacity));
		return (byte)Math.Round((float)(int)alpha * opacity, MidpointRounding.AwayFromZero);
	}

	public static ushort MultiplyAlpha(ushort alpha, float opacity)
	{
		opacity = Math.Min(1f, Math.Max(0f, opacity));
		return (ushort)Math.Round((float)(int)alpha * opacity, MidpointRounding.AwayFromZero);
	}

	public static byte MultiplyAlpha(byte alpha, byte alphaMask)
	{
		return (byte)((alpha * alphaMask + 127) / 255);
	}

	public static ushort MultiplyAlpha(ushort alpha, ushort alphaMask)
	{
		return (ushort)((uint)(alpha * alphaMask + 32767) / 65535u);
	}

	private static PsdRenderedImage RenderDocument(PsdDocument document, bool includeHiddenLayers, bool applyClippingMasks, bool isPreviewRender, float previewScale)
	{
		if (document == null)
		{
			return null;
		}
		using ((!isPreviewRender) ? EnterRenderBuildScope() : null)
		{
			int num = ScalePreviewDimension(document.Width, previewScale);
			int num2 = ScalePreviewDimension(document.Height, previewScale);
			int num3 = ResolveRenderBitDepth(document.Depth, isPreviewRender);
			PsdRenderedImage psdRenderedImage = new PsdRenderedImage(0, 0, num, num2, BuildDocumentPathKey(document), BuildNormalizedBounds(0, 0, num, num2, num, num2), ComputeDocumentWatermarkSalt(document), enableProtection: true, isPreviewRender, num3);
			ComposeLayers(psdRenderedImage, document.Childs.OfType<PsdLayer>().ToArray(), includeHiddenLayers, applyClippingMasks, isPreviewRender, previewScale, num3);
			psdRenderedImage.ApplyProtection();
			if (!isPreviewRender)
			{
				PsdReaderOutputProtection.MNLriQIjAl(psdRenderedImage, ComputeDocumentWatermarkSalt(document));
			}
			return psdRenderedImage;
		}
	}

	private static PsdRenderedImage RenderLayer(PsdLayer layer, bool includeHiddenLayers, bool applyClippingMasks, bool isPreviewRender, float previewScale)
	{
		if (layer == null)
		{
			return null;
		}
		using (isPreviewRender ? null : EnterRenderBuildScope())
		{
			int renderBitDepth = ResolveRenderBitDepth(layer.Depth, isPreviewRender);
			PsdRenderedImage psdRenderedImage = RenderInternal(layer, includeHiddenLayers, applyClippingMasks, isRootCall: true, isPreviewRender, previewScale, renderBitDepth);
			psdRenderedImage?.ApplyProtection();
			if (!isPreviewRender)
			{
				PsdReaderOutputProtection.MNLriQIjAl(psdRenderedImage, ComputeLayerWatermarkSalt(layer));
			}
			return psdRenderedImage;
		}
	}

	private static IDisposable EnterRenderBuildScope()
	{
		PsdLicenseService.GetStatusSnapshot();
		return EditorLicenseOperationScope.BeginOperation();
	}

	private static PsdRenderedImage RenderGroup(PsdLayer layer, bool includeHiddenLayers, bool applyClippingMasks, bool enableProtection, bool isPreviewRender, float previewScale, int renderBitDepth)
	{
		int left = ScalePreviewCoordinate(layer.Left, previewScale);
		int top = ScalePreviewCoordinate(layer.Top, previewScale);
		int num = ScalePreviewDimension(layer.Width, previewScale);
		int num2 = ScalePreviewDimension(layer.Height, previewScale);
		PsdRenderedImage psdRenderedImage = new PsdRenderedImage(left, top, num, num2, BuildLayerPathKey(layer), BuildNormalizedBounds(left, top, num, num2, num, num2), ComputeLayerWatermarkSalt(layer), enableProtection, isPreviewRender, renderBitDepth);
		ComposeLayers(psdRenderedImage, layer.Childs, includeHiddenLayers, applyClippingMasks, isPreviewRender, previewScale, renderBitDepth);
		psdRenderedImage.MultiplyOpacity(layer.Opacity);
		return psdRenderedImage;
	}

	private static void ComposeLayers(PsdRenderedImage canvas, PsdLayer[] layers, bool includeHiddenLayers, bool applyClippingMasks, bool isPreviewRender, float previewScale, int renderBitDepth)
	{
		if (canvas == null || canvas.IsEmpty || layers == null || layers.Length == 0)
		{
			return;
		}
		byte[] array = null;
		ushort[] array2 = null;
		foreach (PsdLayer psdLayer in layers)
		{
			if (psdLayer == null || (!includeHiddenLayers && !psdLayer.IsVisible))
			{
				continue;
			}
			PsdRenderedImage psdRenderedImage = RenderInternal(psdLayer, includeHiddenLayers, applyClippingMasks, isRootCall: false, isPreviewRender, previewScale, renderBitDepth);
			if (psdRenderedImage == null || psdRenderedImage.IsEmpty)
			{
				continue;
			}
			if (applyClippingMasks && psdLayer.IsClipping)
			{
				if (!canvas.IsHighBitDepth)
				{
					if (array != null)
					{
						ApplyClippingMask(psdRenderedImage, array, canvas.Left, canvas.Top, canvas.Width, canvas.Height);
					}
				}
				else if (array2 != null)
				{
					ApplyClippingMask(psdRenderedImage, array2, canvas.Left, canvas.Top, canvas.Width, canvas.Height);
				}
			}
			CompositeImage(canvas, psdRenderedImage);
			if (!psdLayer.IsClipping)
			{
				if (!canvas.IsHighBitDepth)
				{
					array = BuildPlacedAlphaMask(psdRenderedImage, canvas.Left, canvas.Top, canvas.Width, canvas.Height);
					array2 = null;
				}
				else
				{
					array2 = BuildPlacedAlphaMask16(psdRenderedImage, canvas.Left, canvas.Top, canvas.Width, canvas.Height);
					array = null;
				}
			}
		}
	}

	private static PsdRenderedImage RenderLeaf(PsdLayer layer, bool enableProtection, bool isPreviewRender, float previewScale, int renderBitDepth)
	{
		if (layer != null && layer.HasImage && layer.Width > 0 && layer.Height > 0)
		{
			Channel channel = layer.Channels.FirstOrDefault((Channel item) => item.Type == ChannelType.Red);
			Channel channel2 = layer.Channels.FirstOrDefault((Channel item) => item.Type == ChannelType.Green);
			Channel channel3 = layer.Channels.FirstOrDefault((Channel item) => item.Type == ChannelType.Blue);
			Channel channel4 = layer.Channels.FirstOrDefault((Channel item) => item.Type == ChannelType.Alpha);
			Channel channel5 = layer.Channels.FirstOrDefault((Channel item) => item.Type == ChannelType.Mask);
			if (channel != null && channel2 != null && channel3 != null)
			{
				int left = ScalePreviewCoordinate(layer.Left, previewScale);
				int top = ScalePreviewCoordinate(layer.Top, previewScale);
				int num = ScalePreviewDimension(layer.Width, previewScale);
				int num2 = ScalePreviewDimension(layer.Height, previewScale);
				PsdRenderedImage psdRenderedImage = new PsdRenderedImage(left, top, num, num2, BuildLayerPathKey(layer), BuildNormalizedBounds(left, top, num, num2, num, num2), ComputeLayerWatermarkSalt(layer), enableProtection, isPreviewRender, renderBitDepth);
				LayerMask layerMask = ((layer.Records == null) ? null : layer.Records.Mask);
				float opacity = layer.Opacity;
				bool flag = renderBitDepth >= 16;
				for (int num3 = 0; num3 < num2; num3++)
				{
					int num4 = ((!isPreviewRender) ? num3 : ScalePreviewSampleCoordinate(num3, previewScale, layer.Height));
					for (int num5 = 0; num5 < num; num5++)
					{
						int num6 = ((!isPreviewRender) ? num5 : ScalePreviewSampleCoordinate(num5, previewScale, layer.Width));
						int num7 = num4 * layer.Width + num6;
						if (!flag)
						{
							byte alpha = ((channel4 != null) ? channel4.Data[num7] : byte.MaxValue);
							alpha = MultiplyAlpha(alpha, opacity);
							if (channel5 != null && layerMask != null && channel5.Data != null && channel5.Data.Length != 0)
							{
								int num8 = layer.Left + num6;
								int num9 = layer.Top + num4;
								if (num8 >= layerMask.Left && num8 < layerMask.Right && num9 >= layerMask.Top && num9 < layerMask.Bottom)
								{
									int num10 = num8 - layerMask.Left;
									int num11 = (num9 - layerMask.Top) * layerMask.Width + num10;
									alpha = (byte)((num11 >= 0 && num11 < channel5.Data.Length) ? MultiplyAlpha(alpha, channel5.Data[num11]) : 0);
								}
								else
								{
									alpha = 0;
								}
							}
							psdRenderedImage.SetPixel(num5, num3, channel.Data[num7], channel2.Data[num7], channel3.Data[num7], alpha);
							continue;
						}
						ushort alpha2 = ((channel4 == null) ? ushort.MaxValue : ReadChannelSample16(channel4, num7));
						alpha2 = MultiplyAlpha(alpha2, opacity);
						if (channel5 != null && layerMask != null && ((channel5.Data16 != null && channel5.Data16.Length != 0) || (channel5.Data != null && channel5.Data.Length != 0)))
						{
							int num12 = layer.Left + num6;
							int num13 = layer.Top + num4;
							if (num12 >= layerMask.Left && num12 < layerMask.Right && num13 >= layerMask.Top && num13 < layerMask.Bottom)
							{
								int num14 = num12 - layerMask.Left;
								int index = (num13 - layerMask.Top) * layerMask.Width + num14;
								alpha2 = (ushort)(TryReadChannelSample16(channel5, index, out var value) ? MultiplyAlpha(alpha2, value) : 0);
							}
							else
							{
								alpha2 = 0;
							}
						}
						psdRenderedImage.SetPixel(num5, num3, ReadChannelSample16(channel, num7), ReadChannelSample16(channel2, num7), ReadChannelSample16(channel3, num7), alpha2);
					}
				}
				return psdRenderedImage;
			}
			return null;
		}
		return null;
	}

	private static PsdRenderedImage RenderInternal(PsdLayer layer, bool includeHiddenLayers, bool applyClippingMasks, bool isRootCall, bool isPreviewRender, float previewScale, int renderBitDepth)
	{
		if (layer == null)
		{
			return null;
		}
		if (layer.IsGroup)
		{
			return RenderGroup(layer, includeHiddenLayers, applyClippingMasks, isRootCall, isPreviewRender, previewScale, renderBitDepth);
		}
		return RenderLeaf(layer, isRootCall, isPreviewRender, previewScale, renderBitDepth);
	}

	private static int ComputeDocumentWatermarkSalt(PsdDocument document)
	{
		if (document != null)
		{
			int num = 31;
			num = 0xFDD ^ document.Width;
			num = (num * 197) ^ document.Height;
			PsdLayer[] childs = document.Childs;
			if (childs != null)
			{
				num = (num * 17) ^ childs.Length;
			}
			return num;
		}
		return 0;
	}

	private static int ComputeLayerWatermarkSalt(PsdLayer layer)
	{
		if (layer == null)
		{
			return 0;
		}
		int num = 23;
		num = 0xBC5 ^ layer.Left;
		num = (num * 197) ^ layer.Top;
		num = (num * 31) ^ layer.Width;
		num = (num * 17) ^ layer.Height;
		string name = layer.Name;
		if (!string.IsNullOrEmpty(name))
		{
			for (int i = 0; i < name.Length; i++)
			{
				num = (num * 33) ^ name[i];
			}
		}
		return num;
	}

	private static string BuildDocumentPathKey(PsdDocument document)
	{
		if (document == null)
		{
			return "document[0]";
		}
		return $"document[{document.Width}x{document.Height}]";
	}

	private static string BuildLayerPathKey(PsdLayer layer)
	{
		if (layer == null)
		{
			return "layer[0]";
		}
		StringBuilder stringBuilder = new StringBuilder(128);
		Stack<string> stack = new Stack<string>();
		for (PsdLayer psdLayer = layer; psdLayer != null; psdLayer = psdLayer.Parent)
		{
			stack.Push($"{NormalizePathSegment(psdLayer.Name)}[{ResolveSiblingOrdinal(psdLayer)}]");
		}
		stringBuilder.Append(BuildDocumentPathKey(layer.Document));
		while (stack.Count > 0)
		{
			stringBuilder.Append('/');
			stringBuilder.Append(stack.Pop());
		}
		return stringBuilder.ToString();
	}

	private static int ResolveSiblingOrdinal(PsdLayer layer)
	{
		if (layer == null)
		{
			return 0;
		}
		PsdLayer[] array = ((layer.Parent != null) ? layer.Parent.Childs : layer.Document?.Childs?.OfType<PsdLayer>().ToArray());
		if (array != null)
		{
			int num = 0;
			while (true)
			{
				if (num < array.Length)
				{
					if (array[num] == layer)
					{
						break;
					}
					num++;
					continue;
				}
				return 0;
			}
			return num;
		}
		return 0;
	}

	private static string NormalizePathSegment(string rawName)
	{
		if (!string.IsNullOrWhiteSpace(rawName))
		{
			string text = rawName.Trim();
			StringBuilder stringBuilder = new StringBuilder(text.Length);
			for (int i = 0; i < text.Length; i++)
			{
				char c = char.ToLowerInvariant(text[i]);
				if (char.IsLetterOrDigit(c))
				{
					stringBuilder.Append(c);
				}
				else if (c == '_' || c == '-' || char.IsWhiteSpace(c))
				{
					stringBuilder.Append('_');
				}
			}
			if (stringBuilder.Length <= 0)
			{
				string text2 = LicenseCryptography.ComputeStringSha256Hex(text);
				if (string.IsNullOrEmpty(text2))
				{
					return "layer";
				}
				return "layer_" + text2.Substring(0, Math.Min(10, text2.Length));
			}
			return stringBuilder.ToString();
		}
		return "layer";
	}

	private static string BuildLayerNormalizedBounds(PsdLayer layer)
	{
		if (layer != null)
		{
			int canvasWidth = ((layer.Document == null) ? layer.Width : layer.Document.Width);
			int canvasHeight = ((layer.Document == null) ? layer.Height : layer.Document.Height);
			return BuildNormalizedBounds(layer.Left, layer.Top, layer.Width, layer.Height, canvasWidth, canvasHeight);
		}
		return "0:0:0:0";
	}

	private static string BuildNormalizedBounds(int left, int top, int width, int height, int canvasWidth, int canvasHeight)
	{
		int maxValue = Math.Max(1, canvasWidth);
		int maxValue2 = Math.Max(1, canvasHeight);
		int value = left + Math.Max(0, width);
		int value2 = top + Math.Max(0, height);
		return QuantizeNormalized(left, maxValue) + ":" + QuantizeNormalized(top, maxValue2) + ":" + QuantizeNormalized(value, maxValue) + ":" + QuantizeNormalized(value2, maxValue2);
	}

	private static int QuantizeNormalized(int value, int maxValue)
	{
		return (int)Math.Round(Math.Max(0.0, Math.Min(1.0, (double)value / (double)Math.Max(1, maxValue))) * 10000.0, MidpointRounding.AwayFromZero);
	}

	private static float ResolvePreviewRenderScale(int width, int height)
	{
		int num = Math.Max(Math.Max(0, width), Math.Max(0, height));
		if (num <= 0)
		{
			return 0.5f;
		}
		return Math.Min(0.5f, 1024f / (float)num);
	}

	private static int ScalePreviewDimension(int value, float scale)
	{
		if (value <= 0)
		{
			return 0;
		}
		if (scale >= 0.9999f)
		{
			return value;
		}
		return Math.Max(1, Mathf.RoundToInt((float)value * scale));
	}

	private static int ScalePreviewCoordinate(int value, float scale)
	{
		if (scale >= 0.9999f)
		{
			return value;
		}
		return Mathf.RoundToInt((float)value * scale);
	}

	private static int ScalePreviewSampleCoordinate(int value, float scale, int sourceLimit)
	{
		if (sourceLimit <= 0)
		{
			return 0;
		}
		if (scale >= 0.9999f)
		{
			return Mathf.Clamp(value, 0, sourceLimit - 1);
		}
		return Mathf.Clamp(Mathf.FloorToInt((float)value / scale), 0, sourceLimit - 1);
	}

	private static int ResolveRenderBitDepth(int sourceBitDepth, bool isPreviewRender)
	{
		if (!isPreviewRender && sourceBitDepth == 16)
		{
			if (PsdLicenseService.GetPreviewLicenseState().GetLicenseValid())
			{
				return 16;
			}
			return 8;
		}
		return 8;
	}

	private static void CompositeImage(PsdRenderedImage destination, PsdRenderedImage source)
	{
		if (destination == null || source == null || destination.IsEmpty || source.IsEmpty)
		{
			return;
		}
		if (destination.IsHighBitDepth)
		{
			CompositeImage16(destination, source);
			return;
		}
		int num = source.Left - destination.Left;
		int num2 = source.Top - destination.Top;
		for (int i = 0; i < source.Height; i++)
		{
			int num3 = num2 + i;
			if (num3 < 0 || num3 >= destination.Height)
			{
				continue;
			}
			for (int j = 0; j < source.Width; j++)
			{
				int num4 = num + j;
				if (num4 >= 0 && num4 < destination.Width)
				{
					int pixelOffset = source.GetPixelOffset(j, i);
					byte b = ReadRenderedSample8(source, pixelOffset + 3);
					if (b > 0)
					{
						destination.CompositePixel(num4, num3, ReadRenderedSample8(source, pixelOffset), ReadRenderedSample8(source, pixelOffset + 1), ReadRenderedSample8(source, pixelOffset + 2), b);
					}
				}
			}
		}
	}

	private static void CompositeImage16(PsdRenderedImage destination, PsdRenderedImage source)
	{
		int num = source.Left - destination.Left;
		int num2 = source.Top - destination.Top;
		for (int i = 0; i < source.Height; i++)
		{
			int num3 = num2 + i;
			if (num3 < 0 || num3 >= destination.Height)
			{
				continue;
			}
			for (int j = 0; j < source.Width; j++)
			{
				int num4 = num + j;
				if (num4 >= 0 && num4 < destination.Width)
				{
					int pixelOffset = source.GetPixelOffset(j, i);
					ushort num5 = ReadRenderedSample16(source, pixelOffset + 3);
					if (num5 > 0)
					{
						destination.CompositePixel(num4, num3, ReadRenderedSample16(source, pixelOffset), ReadRenderedSample16(source, pixelOffset + 1), ReadRenderedSample16(source, pixelOffset + 2), num5);
					}
				}
			}
		}
	}

	private static byte[] BuildPlacedAlphaMask(PsdRenderedImage source, int destinationLeft, int destinationTop, int destinationWidth, int destinationHeight)
	{
		byte[] array = new byte[destinationWidth * destinationHeight];
		int num = source.Left - destinationLeft;
		int num2 = source.Top - destinationTop;
		for (int i = 0; i < source.Height; i++)
		{
			int num3 = num2 + i;
			if (num3 < 0 || num3 >= destinationHeight)
			{
				continue;
			}
			for (int j = 0; j < source.Width; j++)
			{
				int num4 = num + j;
				if (num4 >= 0 && num4 < destinationWidth)
				{
					int pixelOffset = source.GetPixelOffset(j, i);
					array[num3 * destinationWidth + num4] = ReadRenderedSample8(source, pixelOffset + 3);
				}
			}
		}
		return array;
	}

	private static ushort[] BuildPlacedAlphaMask16(PsdRenderedImage source, int destinationLeft, int destinationTop, int destinationWidth, int destinationHeight)
	{
		ushort[] array = new ushort[destinationWidth * destinationHeight];
		int num = source.Left - destinationLeft;
		int num2 = source.Top - destinationTop;
		for (int i = 0; i < source.Height; i++)
		{
			int num3 = num2 + i;
			if (num3 < 0 || num3 >= destinationHeight)
			{
				continue;
			}
			for (int j = 0; j < source.Width; j++)
			{
				int num4 = num + j;
				if (num4 >= 0 && num4 < destinationWidth)
				{
					int pixelOffset = source.GetPixelOffset(j, i);
					array[num3 * destinationWidth + num4] = ReadRenderedSample16(source, pixelOffset + 3);
				}
			}
		}
		return array;
	}

	private static void ApplyClippingMask(PsdRenderedImage source, byte[] clippingMask, int clippingLeft, int clippingTop, int clippingWidth, int clippingHeight)
	{
		if (source == null || source.IsEmpty || clippingMask == null)
		{
			return;
		}
		int num = source.Left - clippingLeft;
		int num2 = source.Top - clippingTop;
		for (int i = 0; i < source.Height; i++)
		{
			int num3 = num2 + i;
			if (num3 < 0 || num3 >= clippingHeight)
			{
				continue;
			}
			for (int j = 0; j < source.Width; j++)
			{
				int num4 = num + j;
				if (num4 >= 0 && num4 < clippingWidth)
				{
					byte alphaMask = clippingMask[num3 * clippingWidth + num4];
					source.MultiplyPixelAlpha(j, i, alphaMask);
				}
			}
		}
	}

	private static void ApplyClippingMask(PsdRenderedImage source, ushort[] clippingMask, int clippingLeft, int clippingTop, int clippingWidth, int clippingHeight)
	{
		if (source == null || source.IsEmpty || clippingMask == null)
		{
			return;
		}
		int num = source.Left - clippingLeft;
		int num2 = source.Top - clippingTop;
		for (int i = 0; i < source.Height; i++)
		{
			int num3 = num2 + i;
			if (num3 < 0 || num3 >= clippingHeight)
			{
				continue;
			}
			for (int j = 0; j < source.Width; j++)
			{
				int num4 = num + j;
				if (num4 >= 0 && num4 < clippingWidth)
				{
					ushort alphaMask = clippingMask[num3 * clippingWidth + num4];
					source.MultiplyPixelAlpha(j, i, alphaMask);
				}
			}
		}
	}

	private static ushort ReadChannelSample16(Channel channel, int index)
	{
		if (channel == null)
		{
			return 0;
		}
		if (channel.Data16 != null && index >= 0 && index < channel.Data16.Length)
		{
			return channel.Data16[index];
		}
		if (channel.Data != null && index >= 0 && index < channel.Data.Length)
		{
			return PsdBinaryDataUtilities.ConvertByteToUInt16(channel.Data[index]);
		}
		return 0;
	}

	private static bool TryReadChannelSample16(Channel channel, int index, out ushort value)
	{
		value = 0;
		if (channel != null)
		{
			if (channel.Data16 != null && index >= 0 && index < channel.Data16.Length)
			{
				value = channel.Data16[index];
				return true;
			}
			if (channel.Data != null && index >= 0 && index < channel.Data.Length)
			{
				value = PsdBinaryDataUtilities.ConvertByteToUInt16(channel.Data[index]);
				return true;
			}
			return false;
		}
		return false;
	}

	private static byte ReadRenderedSample8(PsdRenderedImage image, int index)
	{
		if (image != null)
		{
			if (image.RawRgba32 != null && index >= 0 && index < image.RawRgba32.Length)
			{
				return image.RawRgba32[index];
			}
			if (image.RawRgba64 != null && index >= 0 && index < image.RawRgba64.Length)
			{
				return PsdBinaryDataUtilities.ConvertUInt16ToByte(image.RawRgba64[index]);
			}
			return 0;
		}
		return 0;
	}

	private static ushort ReadRenderedSample16(PsdRenderedImage image, int index)
	{
		if (image != null)
		{
			if (image.RawRgba64 != null && index >= 0 && index < image.RawRgba64.Length)
			{
				return image.RawRgba64[index];
			}
			if (image.RawRgba32 != null && index >= 0 && index < image.RawRgba32.Length)
			{
				return PsdBinaryDataUtilities.ConvertByteToUInt16(image.RawRgba32[index]);
			}
			return 0;
		}
		return 0;
	}
}
}
