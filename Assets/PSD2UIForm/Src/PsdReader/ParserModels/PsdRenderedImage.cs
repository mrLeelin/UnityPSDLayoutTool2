using System;
using PsdBinaryUtilityNamespace;
using RenderProtectionSessionNamespace;

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

        private readonly RenderProtectionSession protectionSession;

        private bool protectionApplied;

        private static PsdRenderedImage s_ObfuscationSentinel;

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
            protectionSession = (enableProtection ? PsdReaderOutputProtection.CreateProtectionSession(this.left, this.top, this.width, this.height, securitySalt, layerPathKey, normalizedBounds, isPreviewRender) : null);
        }

        public byte[] CreateAlphaMask()
        {
            EnsureProtectionApplied();
            byte[] alphaMask = new byte[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int maskIndex = y * Width + x;
                    int pixelOffset = GetPixelOffset(x, y);
                    alphaMask[maskIndex] = (IsHighBitDepth ? PsdBinaryUtility.ConvertUInt16ToByte(m_Rgba64[pixelOffset + 3]) : m_Rgba32[pixelOffset + 3]);
                }
            }
            return alphaMask;
        }

        internal ushort[] CreateAlphaMask16()
        {
            EnsureProtectionApplied();
            ushort[] alphaMask = new ushort[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int maskIndex = y * Width + x;
                    int pixelOffset = GetPixelOffset(x, y);
                    alphaMask[maskIndex] = ((!IsHighBitDepth) ? PsdBinaryUtility.ConvertByteToUInt16(m_Rgba32[pixelOffset + 3]) : m_Rgba64[pixelOffset + 3]);
                }
            }
            return alphaMask;
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
            if (IsHighBitDepth)
            {
                for (int alphaIndex = 3; alphaIndex < m_Rgba64.Length; alphaIndex += 4)
                {
                    m_Rgba64[alphaIndex] = PsdLayerRenderer.MultiplyAlpha(m_Rgba64[alphaIndex], opacity);
                }
            }
            else
            {
                for (int alphaIndex = 3; alphaIndex < m_Rgba32.Length; alphaIndex += 4)
                {
                    m_Rgba32[alphaIndex] = PsdLayerRenderer.MultiplyAlpha(m_Rgba32[alphaIndex], opacity);
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
            if (!IsHighBitDepth)
            {
                return m_Rgba32;
            }
            byte[] rgba32 = new byte[m_Rgba64.Length];
            for (int sampleIndex = 0; sampleIndex < m_Rgba64.Length; sampleIndex++)
            {
                rgba32[sampleIndex] = PsdBinaryUtility.ConvertUInt16ToByte(m_Rgba64[sampleIndex]);
            }
            return rgba32;
        }

        internal ushort[] GetExportRgba64()
        {
            EnsureProtectionApplied();
            if (IsHighBitDepth)
            {
                return m_Rgba64;
            }
            ushort[] rgba64 = new ushort[m_Rgba32.Length];
            for (int sampleIndex = 0; sampleIndex < m_Rgba32.Length; sampleIndex++)
            {
                rgba64[sampleIndex] = PsdBinaryUtility.ConvertByteToUInt16(m_Rgba32[sampleIndex]);
            }
            return rgba64;
        }

        private void EnsureProtectionApplied()
        {
            if (protectionApplied || protectionSession == null || IsEmpty)
            {
                return;
            }
            if (!IsHighBitDepth)
            {
                protectionSession.ApplyProtectionToRgba32(m_Rgba32, width, height);
            }
            else
            {
                byte[] proxyRgba32 = CreateProxyRgba32FromRaw16();
                protectionSession.ApplyProtectionToRgba32(proxyRgba32, width, height);
                for (int sampleIndex = 0; sampleIndex < proxyRgba32.Length; sampleIndex++)
                {
                    byte protectedSample = proxyRgba32[sampleIndex];
                    if (protectedSample != PsdBinaryUtility.ConvertUInt16ToByte(m_Rgba64[sampleIndex]))
                    {
                        m_Rgba64[sampleIndex] = PsdBinaryUtility.ConvertByteToUInt16(protectedSample);
                    }
                }
            }
            protectionApplied = true;
        }

        private byte[] CreateProxyRgba32FromRaw16()
        {
            byte[] proxyRgba32 = new byte[m_Rgba64.Length];
            for (int sampleIndex = 0; sampleIndex < m_Rgba64.Length; sampleIndex++)
            {
                proxyRgba32[sampleIndex] = PsdBinaryUtility.ConvertUInt16ToByte(m_Rgba64[sampleIndex]);
            }
            return proxyRgba32;
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
                byte destinationR = m_Rgba32[pixelOffset];
                byte destinationG = m_Rgba32[pixelOffset + 1];
                byte destinationB = m_Rgba32[pixelOffset + 2];
                byte destinationA = m_Rgba32[pixelOffset + 3];
                float sourceAlpha = (float)(int)srcA / 255f;
                float destinationAlpha = (float)(int)destinationA / 255f;
                float outputAlpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);
                if (outputAlpha <= 0f)
                {
                    m_Rgba32[pixelOffset] = 0;
                    m_Rgba32[pixelOffset + 1] = 0;
                    m_Rgba32[pixelOffset + 2] = 0;
                    m_Rgba32[pixelOffset + 3] = 0;
                    return;
                }
                float outputPremultipliedR = (float)(int)srcR / 255f * sourceAlpha + (float)(int)destinationR / 255f * destinationAlpha * (1f - sourceAlpha);
                float outputPremultipliedG = (float)(int)srcG / 255f * sourceAlpha + (float)(int)destinationG / 255f * destinationAlpha * (1f - sourceAlpha);
                float outputPremultipliedB = (float)(int)srcB / 255f * sourceAlpha + (float)(int)destinationB / 255f * destinationAlpha * (1f - sourceAlpha);
                byte outputR = ClampToByte(outputPremultipliedR / outputAlpha);
                byte outputG = ClampToByte(outputPremultipliedG / outputAlpha);
                byte outputB = ClampToByte(outputPremultipliedB / outputAlpha);
                byte outputA = ClampToByte(outputAlpha);
                m_Rgba32[pixelOffset] = outputR;
                m_Rgba32[pixelOffset + 1] = outputG;
                m_Rgba32[pixelOffset + 2] = outputB;
                m_Rgba32[pixelOffset + 3] = outputA;
            }
        }

        internal void CompositePixel(int x, int yTopDown, ushort srcR, ushort srcG, ushort srcB, ushort srcA)
        {
            if (srcA > 0)
            {
                int pixelOffset = GetPixelOffset(x, yTopDown);
                ushort destinationR = m_Rgba64[pixelOffset];
                ushort destinationG = m_Rgba64[pixelOffset + 1];
                ushort destinationB = m_Rgba64[pixelOffset + 2];
                ushort destinationA = m_Rgba64[pixelOffset + 3];
                float sourceAlpha = (float)(int)srcA / 65535f;
                float destinationAlpha = (float)(int)destinationA / 65535f;
                float outputAlpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);
                if (outputAlpha <= 0f)
                {
                    m_Rgba64[pixelOffset] = 0;
                    m_Rgba64[pixelOffset + 1] = 0;
                    m_Rgba64[pixelOffset + 2] = 0;
                    m_Rgba64[pixelOffset + 3] = 0;
                }
                else
                {
                    float outputPremultipliedR = (float)(int)srcR / 65535f * sourceAlpha + (float)(int)destinationR / 65535f * destinationAlpha * (1f - sourceAlpha);
                    float outputPremultipliedG = (float)(int)srcG / 65535f * sourceAlpha + (float)(int)destinationG / 65535f * destinationAlpha * (1f - sourceAlpha);
                    float outputPremultipliedB = (float)(int)srcB / 65535f * sourceAlpha + (float)(int)destinationB / 65535f * destinationAlpha * (1f - sourceAlpha);
                    m_Rgba64[pixelOffset] = ClampToUInt16(outputPremultipliedR / outputAlpha);
                    m_Rgba64[pixelOffset + 1] = ClampToUInt16(outputPremultipliedG / outputAlpha);
                    m_Rgba64[pixelOffset + 2] = ClampToUInt16(outputPremultipliedB / outputAlpha);
                    m_Rgba64[pixelOffset + 3] = ClampToUInt16(outputAlpha);
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

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdRenderedImage GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
