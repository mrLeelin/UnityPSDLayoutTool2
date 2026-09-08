using System;
using PsdProtectionGuards;
using PsdPreviewProtection;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace cn.efunstudio.psdreader.PsdParser
{

public sealed class PsdRenderedImage
{
	private readonly int left;

	private readonly int top;

	private readonly int width;

	private readonly int height;

	private readonly int bitDepth;

	private readonly byte[] m_Rgba32;

	private readonly ushort[] m_Rgba64;

	private readonly ProtectedPreviewRenderer protectionSession;

	private bool protectionApplied;

	public int Left => left;

	public int Top => top;

	public int Width => width;

	public int Height => height;

	public int Right => Left + Width;

	public int Bottom => Top + Height;

	public int BitDepth => bitDepth;

	public bool IsHighBitDepth => bitDepth == 16;

	public byte[] Rgba32 => GetExportRgba32();

	public ushort[] Rgba64 => GetExportRgba64();

	internal byte[] RawRgba32 => m_Rgba32;

	internal ushort[] RawRgba64 => m_Rgba64;

	public bool IsEmpty
	{
		get
		{
			if (Width > 0 && Height > 0)
			{
				if (IsHighBitDepth)
				{
					return m_Rgba64.Length == 0;
				}
				return m_Rgba32.Length == 0;
			}
			return true;
		}
	}

	public PsdRenderedImage(int left, int top, int width, int height, string layerPathKey, string normalizedBounds, int securitySalt = 0, bool enableProtection = true, bool isPreviewRender = false, int bitDepth = 8)
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		this.left = left;
		this.top = top;
		this.width = Math.Max(0, width);
		this.height = Math.Max(0, height);
		this.bitDepth = ((bitDepth == 16) ? 16 : 8);
		if (this.bitDepth == 16)
		{
			m_Rgba64 = new ushort[this.width * this.height * 4];
			m_Rgba32 = Array.Empty<byte>();
		}
		else
		{
			m_Rgba32 = new byte[this.width * this.height * 4];
			m_Rgba64 = Array.Empty<ushort>();
		}
		protectionSession = (enableProtection ? PsdReaderOutputProtection.EX0re7VEWt(this.left, this.top, this.width, this.height, securitySalt, layerPathKey, normalizedBounds, isPreviewRender) : null);
	}

	public byte[] CreateAlphaMask()
	{
		EnsureProtectionApplied();
		byte[] array = new byte[Width * Height];
		for (int i = 0; i < Height; i++)
		{
			for (int j = 0; j < Width; j++)
			{
				int num = i * Width + j;
				int pixelOffset = GetPixelOffset(j, i);
				array[num] = ((!IsHighBitDepth) ? m_Rgba32[pixelOffset + 3] : PsdBinaryDataUtilities.ConvertUInt16ToByte(m_Rgba64[pixelOffset + 3]));
			}
		}
		return array;
	}

	internal ushort[] CreateAlphaMask16()
	{
		EnsureProtectionApplied();
		ushort[] array = new ushort[Width * Height];
		for (int i = 0; i < Height; i++)
		{
			for (int j = 0; j < Width; j++)
			{
				int num = i * Width + j;
				int pixelOffset = GetPixelOffset(j, i);
				array[num] = ((!IsHighBitDepth) ? PsdBinaryDataUtilities.ConvertByteToUInt16(m_Rgba32[pixelOffset + 3]) : m_Rgba64[pixelOffset + 3]);
			}
		}
		return array;
	}

	internal void MultiplyOpacity(float opacity)
	{
		if (IsEmpty)
		{
			return;
		}
		opacity = Math.Min(1f, Math.Max(0f, opacity));
		if (opacity >= 0.9999f)
		{
			return;
		}
		if (!IsHighBitDepth)
		{
			for (int i = 3; i < m_Rgba32.Length; i += 4)
			{
				m_Rgba32[i] = PsdLayerRenderer.MultiplyAlpha(m_Rgba32[i], opacity);
			}
		}
		else
		{
			for (int j = 3; j < m_Rgba64.Length; j += 4)
			{
				m_Rgba64[j] = PsdLayerRenderer.MultiplyAlpha(m_Rgba64[j], opacity);
			}
		}
	}

	internal void ApplyProtection()
	{
		EnsureProtectionApplied();
	}

	internal byte[] GetExportRgba32()
	{
		EnsureProtectionApplied();
		if (IsHighBitDepth)
		{
			byte[] array = new byte[m_Rgba64.Length];
			for (int i = 0; i < m_Rgba64.Length; i++)
			{
				array[i] = PsdBinaryDataUtilities.ConvertUInt16ToByte(m_Rgba64[i]);
			}
			return array;
		}
		return m_Rgba32;
	}

	internal ushort[] GetExportRgba64()
	{
		EnsureProtectionApplied();
		if (IsHighBitDepth)
		{
			return m_Rgba64;
		}
		ushort[] array = new ushort[m_Rgba32.Length];
		for (int i = 0; i < m_Rgba32.Length; i++)
		{
			array[i] = PsdBinaryDataUtilities.ConvertByteToUInt16(m_Rgba32[i]);
		}
		return array;
	}

	private void EnsureProtectionApplied()
	{
		if (protectionApplied || protectionSession == null || IsEmpty)
		{
			return;
		}
		if (IsHighBitDepth)
		{
			byte[] array = CreateProxyRgba32FromRaw16();
			protectionSession.RVLso9nLBq(array, width, height);
			for (int i = 0; i < array.Length; i++)
			{
				byte b = array[i];
				if (b != PsdBinaryDataUtilities.ConvertUInt16ToByte(m_Rgba64[i]))
				{
					m_Rgba64[i] = PsdBinaryDataUtilities.ConvertByteToUInt16(b);
				}
			}
		}
		else
		{
			protectionSession.RVLso9nLBq(m_Rgba32, width, height);
		}
		protectionApplied = true;
	}

	private byte[] CreateProxyRgba32FromRaw16()
	{
		byte[] array = new byte[m_Rgba64.Length];
		for (int i = 0; i < m_Rgba64.Length; i++)
		{
			array[i] = PsdBinaryDataUtilities.ConvertUInt16ToByte(m_Rgba64[i]);
		}
		return array;
	}

	internal int GetPixelOffset(int x, int yTopDown)
	{
		return ((Height - 1 - yTopDown) * Width + x) * 4;
	}

	internal void SetPixel(int x, int yTopDown, byte r, byte g, byte b, byte a)
	{
		int pixelOffset = GetPixelOffset(x, yTopDown);
		m_Rgba32[pixelOffset] = r;
		m_Rgba32[pixelOffset + 1] = g;
		m_Rgba32[pixelOffset + 2] = b;
		m_Rgba32[pixelOffset + 3] = a;
	}

	internal void SetPixel(int x, int yTopDown, ushort r, ushort g, ushort b, ushort a)
	{
		int pixelOffset = GetPixelOffset(x, yTopDown);
		m_Rgba64[pixelOffset] = r;
		m_Rgba64[pixelOffset + 1] = g;
		m_Rgba64[pixelOffset + 2] = b;
		m_Rgba64[pixelOffset + 3] = a;
	}

	internal void MultiplyPixelAlpha(int x, int yTopDown, byte alphaMask)
	{
		int pixelOffset = GetPixelOffset(x, yTopDown);
		m_Rgba32[pixelOffset + 3] = PsdLayerRenderer.MultiplyAlpha(m_Rgba32[pixelOffset + 3], alphaMask);
	}

	internal void MultiplyPixelAlpha(int x, int yTopDown, ushort alphaMask)
	{
		int pixelOffset = GetPixelOffset(x, yTopDown);
		m_Rgba64[pixelOffset + 3] = PsdLayerRenderer.MultiplyAlpha(m_Rgba64[pixelOffset + 3], alphaMask);
	}

	internal void CompositePixel(int x, int yTopDown, byte srcR, byte srcG, byte srcB, byte srcA)
	{
		if (srcA > 0)
		{
			int pixelOffset = GetPixelOffset(x, yTopDown);
			byte b = m_Rgba32[pixelOffset];
			byte b2 = m_Rgba32[pixelOffset + 1];
			byte b3 = m_Rgba32[pixelOffset + 2];
			byte num = m_Rgba32[pixelOffset + 3];
			float num2 = (float)(int)srcA / 255f;
			float num3 = (float)(int)num / 255f;
			float num4 = num2 + num3 * (1f - num2);
			if (num4 <= 0f)
			{
				m_Rgba32[pixelOffset] = 0;
				m_Rgba32[pixelOffset + 1] = 0;
				m_Rgba32[pixelOffset + 2] = 0;
				m_Rgba32[pixelOffset + 3] = 0;
				return;
			}
			float num5 = (float)(int)srcR / 255f * num2 + (float)(int)b / 255f * num3 * (1f - num2);
			float num6 = (float)(int)srcG / 255f * num2 + (float)(int)b2 / 255f * num3 * (1f - num2);
			float num7 = (float)(int)srcB / 255f * num2 + (float)(int)b3 / 255f * num3 * (1f - num2);
			byte b4 = ClampToByte(num5 / num4);
			byte b5 = ClampToByte(num6 / num4);
			byte b6 = ClampToByte(num7 / num4);
			byte b7 = ClampToByte(num4);
			m_Rgba32[pixelOffset] = b4;
			m_Rgba32[pixelOffset + 1] = b5;
			m_Rgba32[pixelOffset + 2] = b6;
			m_Rgba32[pixelOffset + 3] = b7;
		}
	}

	internal void CompositePixel(int x, int yTopDown, ushort srcR, ushort srcG, ushort srcB, ushort srcA)
	{
		if (srcA > 0)
		{
			int pixelOffset = GetPixelOffset(x, yTopDown);
			ushort num = m_Rgba64[pixelOffset];
			ushort num2 = m_Rgba64[pixelOffset + 1];
			ushort num3 = m_Rgba64[pixelOffset + 2];
			ushort num4 = m_Rgba64[pixelOffset + 3];
			float num5 = (float)(int)srcA / 65535f;
			float num6 = (float)(int)num4 / 65535f;
			float num7 = num5 + num6 * (1f - num5);
			if (num7 <= 0f)
			{
				m_Rgba64[pixelOffset] = 0;
				m_Rgba64[pixelOffset + 1] = 0;
				m_Rgba64[pixelOffset + 2] = 0;
				m_Rgba64[pixelOffset + 3] = 0;
			}
			else
			{
				float num8 = (float)(int)srcR / 65535f * num5 + (float)(int)num / 65535f * num6 * (1f - num5);
				float num9 = (float)(int)srcG / 65535f * num5 + (float)(int)num2 / 65535f * num6 * (1f - num5);
				float num10 = (float)(int)srcB / 65535f * num5 + (float)(int)num3 / 65535f * num6 * (1f - num5);
				m_Rgba64[pixelOffset] = ClampToUInt16(num8 / num7);
				m_Rgba64[pixelOffset + 1] = ClampToUInt16(num9 / num7);
				m_Rgba64[pixelOffset + 2] = ClampToUInt16(num10 / num7);
				m_Rgba64[pixelOffset + 3] = ClampToUInt16(num7);
			}
		}
	}

	private static byte ClampToByte(float value)
	{
		value = Math.Min(1f, Math.Max(0f, value));
		return (byte)Math.Round(value * 255f, MidpointRounding.AwayFromZero);
	}

	private static ushort ClampToUInt16(float value)
	{
		value = Math.Min(1f, Math.Max(0f, value));
		return (ushort)Math.Round(value * 65535f, MidpointRounding.AwayFromZero);
	}
}
}
