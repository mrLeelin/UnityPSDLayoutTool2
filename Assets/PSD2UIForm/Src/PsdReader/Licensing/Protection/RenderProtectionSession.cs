using System;
using System.Collections.Generic;
using WatermarkTextRasterizerNamespace;
using WatermarkMaskNamespace;
using UnityEngine;
using Object = UnityEngine.Object;
using RenderProtectionFlagsNamespace;
using RenderProtectionDescriptorNamespace;

namespace RenderProtectionSessionNamespace
{
    internal sealed class RenderProtectionSession
    {
        private readonly struct WatermarkPlacement
        {
            internal readonly float OriginX;

            internal readonly float OriginY;

            internal readonly int PixelMinX;

            internal readonly int PixelMaxX;

            internal readonly int PixelMinY;

            internal readonly int PixelMaxY;

            private static object s_ObfuscationSentinel;

            internal WatermarkPlacement(float value, float value2, int value3, int value4, int value5, int value6)
            {
                OriginX = value;
                OriginY = value2;
                PixelMinX = value3;
                PixelMaxX = value4;
                PixelMinY = value5;
                PixelMaxY = value6;
            }

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static object GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private readonly RenderProtectionDescriptor _descriptor;

        private readonly int _originX;

        private readonly int _originY;

        private readonly int _width;

        private readonly int _height;

        private readonly int _securitySalt;

        private readonly float _jitteredCenterX;

        private readonly float _jitteredCenterY;

        private readonly string _primaryWatermarkText;

        private readonly string _brandWatermarkText;

        private readonly float _primaryWatermarkScale;

        private readonly float _brandWatermarkBaseScale;

        private readonly float _primaryWatermarkWidth;

        private readonly float _brandWatermarkWidth;

        private readonly float _primaryWatermarkHeight;

        private readonly float _brandWatermarkHeight;

        private readonly float _primaryWatermarkRowSpacing;

        private readonly float _primaryWatermarkColumnGap;

        private readonly float _primaryWatermarkShear;

        private readonly float _brandWatermarkShear;

        private readonly float _primaryWatermarkRowSkew;

        private readonly float _primaryWatermarkPatternOffset;

        private readonly float _brandBoundsLeft;

        private readonly float _brandBoundsTop;

        private readonly float _brandBoundsRight;

        private readonly float _brandBoundsBottom;

        private readonly float _brandHorizontalClearance;

        private readonly float _brandVerticalClearance;

        private readonly Color32 _lightProtectionColor;

        private readonly Color32 _darkProtectionColor;

        private readonly Color32 _accentProtectionColor;

        private readonly Color32 _opaqueWhiteColor;

        private readonly bool _isPrimaryWatermarkEnabled;

        private readonly bool _isBrandWatermarkEnabled;

        private readonly bool _isDiagonalOverlayEnabled;

        private readonly bool _isAlphaNoiseEnabled;

        private readonly bool _isEdgeEmphasisEnabled;

        private readonly bool _usesOpaqueDiagonalOverlay;

        private readonly WatermarkMask _primaryWatermarkMask;

        private readonly WatermarkMask _brandWatermarkMask;

        private readonly WatermarkPlacement[] _primaryWatermarkPlacements;

        private readonly float _centerX;

        private readonly float _centerY;

        private readonly float _diagonalLength;

        private readonly float _shortSideLength;

        private readonly float _maxX;

        private readonly float _maxY;

        private readonly float _primaryDiagonalRadius;

        private readonly float _secondaryDiagonalRadius;

        private readonly float _dashedDiagonalRadius;

        private readonly float _primaryDashPeriod;

        private readonly float _secondaryDashPeriod;

        private static RenderProtectionSession s_ObfuscationSentinel;

        internal RenderProtectionSession(RenderProtectionDescriptor renderProtectionDescriptor, int value, int value2, int value3, int value4, int value5)
        {
            _descriptor = renderProtectionDescriptor ?? new RenderProtectionDescriptor();
            _originX = value;
            _originY = value2;
            _width = Math.Max(0, value3);
            _height = Math.Max(0, value4);
            _securitySalt = value5;
            _primaryWatermarkText = "试用版";
            _brandWatermarkText = "efunstudio.cn";
            _isPrimaryWatermarkEnabled = (_descriptor.GetProtectionFlags() & (RenderProtectionFlags)1) == (RenderProtectionFlags)1;
            _isBrandWatermarkEnabled = (_descriptor.GetProtectionFlags() & (RenderProtectionFlags)2) == (RenderProtectionFlags)2;
            _isDiagonalOverlayEnabled = (_descriptor.GetProtectionFlags() & (RenderProtectionFlags)16) == (RenderProtectionFlags)16;
            _isAlphaNoiseEnabled = _descriptor.GetAlphaNoiseStrength() > 0;
            _isEdgeEmphasisEnabled = (_descriptor.GetProtectionFlags() & (RenderProtectionFlags)128) == (RenderProtectionFlags)128;
            _usesOpaqueDiagonalOverlay = _isDiagonalOverlayEnabled;
            _centerX = (float)(_width - 1) * 0.5f;
            _centerY = (float)(_height - 1) * 0.5f;
            _diagonalLength = Mathf.Sqrt((float)(_width * _width + _height * _height));
            _shortSideLength = Math.Max(1f, Math.Min(Math.Max(1, _width), Math.Max(1, _height)));
            _maxX = Math.Max(1f, (float)_width - 1f);
            _maxY = Math.Max(1f, (float)_height - 1f);
            float num = Math.Max(24f, _shortSideLength);
            float val = WatermarkTextRasterizer.MeasureTextWidth(_primaryWatermarkText, 1f);
            float val2 = WatermarkTextRasterizer.MeasureTextHeight(_primaryWatermarkText, 1f);
            float val3 = WatermarkTextRasterizer.MeasureTextWidth(_brandWatermarkText, 1f);
            float val4 = WatermarkTextRasterizer.MeasureTextHeight(_brandWatermarkText, 1f);
            float val5 = Mathf.Clamp(num / (23.5f + (float)(int)_descriptor.GetSecondaryLayoutVariant() * 0.64f), 1.25f, 7.2f);
            float val6 = Math.Max(0.95f, Math.Min(Math.Max(44f, (float)_width * 0.72f) / Math.Max(1f, val), Math.Max(18f, (float)_height * 0.24f) / Math.Max(1f, val2)));
            _primaryWatermarkScale = Mathf.Clamp(Math.Min(val5, val6), 0.95f, 7.2f);
            float val7 = Math.Max(1.4f, Math.Min(Math.Max(72f, (float)_width * 1.28f) / Math.Max(1f, val3), Math.Max(22f, (float)_height * 0.3f) / Math.Max(1f, val4)));
            _brandWatermarkBaseScale = Mathf.Clamp(Math.Min(_primaryWatermarkScale * 1.14f, val7), 1.4f, 10.5f);
            _primaryWatermarkWidth = WatermarkTextRasterizer.MeasureTextWidth(_primaryWatermarkText, _primaryWatermarkScale);
            _primaryWatermarkHeight = WatermarkTextRasterizer.MeasureTextHeight(_primaryWatermarkText, _primaryWatermarkScale);
            float num2 = _primaryWatermarkHeight;
            float num3 = Math.Min(_brandWatermarkBaseScale * 2f, (float)_width / Math.Max(1f, val3));
            _brandWatermarkWidth = WatermarkTextRasterizer.MeasureTextWidth(_brandWatermarkText, num3);
            _brandWatermarkHeight = WatermarkTextRasterizer.MeasureTextHeight(_brandWatermarkText, num3);
            float num4 = Math.Max(_width, _height);
            _primaryWatermarkRowSpacing = Mathf.Max(num2 * 1.15f, Math.Max(16f, num4 * 0.38f));
            _primaryWatermarkColumnGap = Mathf.Max(_primaryWatermarkWidth * 0.08f, Math.Max(6f, (float)_width * 0.02f));
            float num5 = (float)_width / (float)Math.Max(1, _height);
            float num6 = (((_descriptor.GetStableSeed() & 1) != 0) ? (-1f) : 1f);
            _primaryWatermarkShear = num6 * ((num5 >= 1.4f) ? 0.22f : 0.16f) + NormalizeByteToSignedUnit((byte)((_descriptor.GetStableSeed() >> 16) & 0xFF)) * 0.06f;
            _brandWatermarkShear = (0f - num6) * 0.12f + NormalizeByteToSignedUnit((byte)((_descriptor.GetSessionSeed() >> 16) & 0xFF)) * 0.05f;
            _primaryWatermarkRowSkew = _primaryWatermarkWidth * (0.18f + (float)(_descriptor.GetPrimaryLayoutVariant() - 5) * 0.025f);
            _primaryWatermarkPatternOffset = 0f;
            float num7 = NormalizeByteToSignedUnit((byte)(_descriptor.GetPositionJitterSeed() ^ (_descriptor.GetStableSeed() & 0xFF))) * Math.Max(4f, (float)_width * 0.12f);
            float num8 = NormalizeByteToSignedUnit((byte)(_descriptor.GetPositionJitterSeed() ^ ((_descriptor.GetStableSeed() >> 8) & 0xFF))) * Math.Max(4f, (float)_height * 0.08f);
            float num9 = NormalizeByteToSignedUnit((byte)(_descriptor.GetSessionSeed() & 0xFF)) * Math.Max(2f, (float)_width * 0.025f);
            float num10 = NormalizeByteToSignedUnit((byte)((_descriptor.GetSessionSeed() >> 8) & 0xFF)) * Math.Max(2f, (float)_height * 0.02f);
            _jitteredCenterX = (float)(_width - 1) * 0.5f + num7 + num9;
            _jitteredCenterY = (float)(_height - 1) * 0.5f + num8 + num10;
            byte b = (byte)(168 + ((_descriptor.GetStableSeed() >> 3) & 0x1F));
            byte b2 = (byte)(152 + ((_descriptor.GetSessionSeed() >> 5) & 0x2F));
            byte b3 = (byte)(92 + ((_descriptor.GetStableSeed() >> 11) & 0x3F));
            _lightProtectionColor = new Color32((byte)248, (byte)248, (byte)248, byte.MaxValue);
            _darkProtectionColor = new Color32((byte)16, (byte)16, (byte)16, byte.MaxValue);
            _accentProtectionColor = new Color32(b, b2, b3, byte.MaxValue);
            _opaqueWhiteColor = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
            _primaryDiagonalRadius = Math.Max(7f, _shortSideLength * (0.06f + (float)(int)_descriptor.GetContrastStrength() / 255f * 0.03f));
            _secondaryDiagonalRadius = _primaryDiagonalRadius * (0.82f + (float)(int)_descriptor.GetEdgeStrength() / 255f * 0.16f);
            _dashedDiagonalRadius = Math.Max(4.5f, _shortSideLength * (0.038f + (float)(int)_descriptor.GetBrandWatermarkStrength() / 255f * 0.022f));
            _primaryDashPeriod = Math.Max(14f, _shortSideLength * (0.16f + (float)(_descriptor.GetPrimaryLayoutVariant() % 5) * 0.018f));
            _secondaryDashPeriod = Math.Max(14f, _shortSideLength * (0.16f + (float)(_descriptor.GetSecondaryLayoutVariant() % 5) * 0.018f));
            _primaryWatermarkMask = (_isPrimaryWatermarkEnabled ? WatermarkTextRasterizer.CreateWatermarkMask(_primaryWatermarkText, _primaryWatermarkScale) : WatermarkMask.GetEmpty());
            _brandWatermarkMask = ((!_isBrandWatermarkEnabled) ? WatermarkMask.GetEmpty() : WatermarkTextRasterizer.CreateWatermarkMask(_brandWatermarkText, num3));
            CalculateBrandWatermarkBounds(_width, _height, out _brandBoundsLeft, out _brandBoundsTop, out _brandBoundsRight, out _brandBoundsBottom);
            _brandHorizontalClearance = Math.Max(10f, _brandWatermarkWidth * 0.18f);
            _brandVerticalClearance = Math.Max(8f, _brandWatermarkHeight * 0.2f);
            _primaryWatermarkPlacements = BuildPrimaryWatermarkPlacements();
        }

        internal void ApplyProtectionToRgba32(byte[] bytes, int value, int value2)
        {
            if (bytes != null && value > 0 && value2 > 0 && _descriptor.HasActiveProtection())
            {
                long num = (long)value * (long)value2 * 4L;
                if (num > 0L && num <= bytes.Length)
                {
                    ApplyPrimaryWatermark(bytes, value, value2);
                    ApplyBrandWatermark(bytes, value, value2);
                }
            }
        }

        private WatermarkPlacement[] BuildPrimaryWatermarkPlacements()
        {
            if (_isPrimaryWatermarkEnabled && _primaryWatermarkMask.HasCoverage() && !(_primaryWatermarkWidth <= 0f) && !(_primaryWatermarkHeight <= 0f) && _width > 0 && _height > 0)
            {
                float num = 0.70710677f;
                float num2 = 0.70710677f;
                float num3 = -0.70710677f;
                float num4 = num;
                float num5 = Math.Max(_primaryWatermarkHeight * 1.15f, _primaryWatermarkRowSpacing);
                float num6 = _primaryWatermarkWidth + Math.Max(_primaryWatermarkScale * 2f, _primaryWatermarkColumnGap);
                if (!(num5 <= 0.001f) && num6 > 0.001f)
                {
                    float num7 = float.MaxValue;
                    float num8 = float.MinValue;
                    float num9 = float.MaxValue;
                    float num10 = float.MinValue;
                    float num11 = _centerX;
                    float num12 = _centerY;
                    float[] array = new float[2]
                    {
                        0f,
                        Math.Max(0f, (float)_width - 1f)
                    };
                    float[] array2 = new float[2]
                    {
                        0f,
                        Math.Max(0f, (float)_height - 1f)
                    };
                    for (int i = 0; i < array2.Length; i++)
                    {
                        for (int j = 0; j < array.Length; j++)
                        {
                            float num13 = array[j] - num11;
                            float num14 = array2[i] - num12;
                            float val = num13 * num + num14 * num2;
                            float val2 = num13 * num3 + num14 * num4;
                            num7 = Math.Min(num7, val);
                            num8 = Math.Max(num8, val);
                            num9 = Math.Min(num9, val2);
                            num10 = Math.Max(num10, val2);
                        }
                    }
                    float num15 = _primaryWatermarkHeight + Math.Abs(_primaryWatermarkRowSkew) + Math.Max(4f, _primaryWatermarkScale * 1.5f);
                    float num16 = _primaryWatermarkWidth + Math.Abs(_primaryWatermarkShear) * _primaryWatermarkHeight + Math.Max(4f, _primaryWatermarkScale * 1.5f);
                    bool flag;
                    float num17 = ((!(flag = _isBrandWatermarkEnabled && _brandWatermarkMask.HasCoverage())) ? 0f : (_brandBoundsLeft - _brandHorizontalClearance));
                    float num18 = ((!flag) ? (-1f) : (_brandBoundsRight + _brandHorizontalClearance));
                    float num19 = ((!flag) ? 0f : (_brandBoundsTop - _brandVerticalClearance));
                    float num20 = ((!flag) ? (-1f) : (_brandBoundsBottom + _brandVerticalClearance));
                    int num21 = (int)Math.Floor((num9 - num15) / num5) - 2;
                    int num22 = (int)Math.Ceiling((num10 + num15) / num5) + 2;
                    int num23 = (int)Math.Floor((num7 - num16) / num6) - 2;
                    int num24 = (int)Math.Ceiling((num8 + num16) / num6) + 2;
                    float num25 = Math.Abs(_primaryWatermarkShear) * _primaryWatermarkHeight * 0.5f;
                    List<WatermarkPlacement> list = new List<WatermarkPlacement>();
                    for (int k = num21; k <= num22; k++)
                    {
                        float num26 = (float)k * num5;
                        if (k != 0)
                        {
                            float num27 = (GenerateDeterministicNoise(_descriptor.GetStableSeed() ^ 0x23B1, k, _securitySalt, 1) - 0.5f) * Math.Max(1f, _primaryWatermarkScale * 0.65f);
                            num26 += (float)k * _primaryWatermarkRowSkew * 0.14f + num27;
                        }
                        float num28 = num11 + num3 * num26;
                        float num29 = num12 + num4 * num26;
                        for (int l = num23; l <= num24; l++)
                        {
                            float num30 = (float)l * num6;
                            if (l != 0 || k != 0)
                            {
                                float num31 = (GenerateDeterministicNoise(_descriptor.GetSessionSeed() ^ 0xBDE3, l, k, 1) - 0.5f) * Math.Max(0.75f, _primaryWatermarkScale * (0.32f + Math.Abs(_primaryWatermarkShear) * 0.85f));
                                num30 += num31 + (float)k * _primaryWatermarkRowSkew * 0.08f;
                            }
                            float num32 = num28 + num * num30;
                            float num33 = num29 + num2 * num30;
                            float num34 = num32 - _primaryWatermarkWidth * 0.5f;
                            float num35 = num33 - _primaryWatermarkHeight * 0.5f;
                            int num36 = Math.Max(0, (int)Math.Floor(num34 - num25) - 1);
                            int num37 = Math.Min(_width - 1, (int)Math.Ceiling(num34 + _primaryWatermarkWidth + num25) + 1);
                            int num38 = Math.Max(0, (int)Math.Floor(num35) - 1);
                            int num39 = Math.Min(_height - 1, (int)Math.Ceiling(num35 + _primaryWatermarkHeight) + 1);
                            if (num36 <= num37 && num38 <= num39 && (!flag || (float)num37 < num17 || (float)num36 > num18 || (float)num39 < num19 || (float)num38 > num20))
                            {
                                list.Add(new WatermarkPlacement(num34, num35, num36, num37, num38, num39));
                            }
                        }
                    }
                    if (list.Count <= 0)
                    {
                        return Array.Empty<WatermarkPlacement>();
                    }
                    return list.ToArray();
                }
                return Array.Empty<WatermarkPlacement>();
            }
            return Array.Empty<WatermarkPlacement>();
        }

        private void CalculateBrandWatermarkBounds(int value, int value2, out float result, out float result2, out float result3, out float result4)
        {
            result = 0f;
            result2 = 0f;
            result3 = 0f;
            result4 = 0f;
            if (value > 0 && value2 > 0 && _isBrandWatermarkEnabled && _brandWatermarkMask.HasCoverage() && !(_brandWatermarkWidth <= 0f) && _brandWatermarkHeight > 0f)
            {
                result = Mathf.Max(0f, ((float)value - _brandWatermarkWidth) * 0.5f);
                result2 = Mathf.Max(0f, ((float)value2 - _brandWatermarkHeight) * 0.5f);
                result3 = Math.Min(value, result + _brandWatermarkWidth);
                result4 = Math.Min(value2, result2 + _brandWatermarkHeight);
            }
        }

        private void ApplyPrimaryWatermark(byte[] bytes, int value, int value2)
        {
            if (bytes == null || value <= 0 || value2 <= 0 || !_isPrimaryWatermarkEnabled || !_primaryWatermarkMask.HasCoverage() || _primaryWatermarkPlacements == null || _primaryWatermarkPlacements.Length == 0 || _primaryWatermarkWidth <= 0f || !(_primaryWatermarkHeight > 0f))
            {
                return;
            }
            float num = (float)(int)_descriptor.GetPrimaryWatermarkStrength() / 255f * 0.96f;
            if (num <= 0f)
            {
                return;
            }
            for (int i = 0; i < _primaryWatermarkPlacements.Length; i++)
            {
                WatermarkPlacement value3 = _primaryWatermarkPlacements[i];
                for (int j = value3.PixelMinY; j <= value3.PixelMaxY; j++)
                {
                    int num2 = (value2 - 1 - j) * value * 4;
                    float num3 = (float)j - value3.OriginY;
                    float num4 = (num3 - _primaryWatermarkHeight * 0.5f) * _primaryWatermarkShear;
                    for (int k = value3.PixelMinX; k <= value3.PixelMaxX; k++)
                    {
                        int num5 = num2 + k * 4;
                        byte b = bytes[num5 + 3];
                        if (b == 0 || (_isBrandWatermarkEnabled && (float)k >= _brandBoundsLeft - _brandHorizontalClearance && (float)k <= _brandBoundsRight + _brandHorizontalClearance && (float)j >= _brandBoundsTop - _brandVerticalClearance && (float)j <= _brandBoundsBottom + _brandVerticalClearance))
                        {
                            continue;
                        }
                        float num6 = (float)k - value3.OriginX - num4;
                        if (num6 < -1f || num6 > _primaryWatermarkWidth + 1f || num3 < -1f || num3 > _primaryWatermarkHeight + 1f)
                        {
                            continue;
                        }
                        float num7 = _primaryWatermarkMask.SampleCoverage(num6, num3);
                        float num8 = CalculateMaskEdgeCoverage(_primaryWatermarkMask, num6, num3, num7);
                        if (num7 <= 0f && num8 <= 0f)
                        {
                            continue;
                        }
                        float num9 = MathF.Sqrt(MathF.Max(0f, (float)(int)b / 255f));
                        float num10 = num7 * num * num9;
                        float num11 = num8 * GetEdgeEmphasisStrength() * num9;
                        if (!(num10 <= 0f) || !(num11 <= 0f))
                        {
                            byte b2 = bytes[num5];
                            byte b3 = bytes[num5 + 1];
                            byte b4 = bytes[num5 + 2];
                            if (num11 > 0f)
                            {
                                BlendRgb(ref b2, ref b3, ref b4, GetInverseContrastingProtectionColor(b2, b3, b4), num11);
                            }
                            if (num10 > 0f)
                            {
                                BlendRgb(ref b2, ref b3, ref b4, GetContrastingProtectionColor(b2, b3, b4), num10);
                            }
                            bytes[num5] = b2;
                            bytes[num5 + 1] = b3;
                            bytes[num5 + 2] = b4;
                        }
                    }
                }
            }
        }

        private void ApplyBrandWatermark(byte[] bytes, int value, int value2)
        {
            if (bytes == null || value <= 0 || value2 <= 0 || !_isBrandWatermarkEnabled || !_brandWatermarkMask.HasCoverage() || _brandWatermarkWidth <= 0f || !(_brandWatermarkHeight > 0f))
            {
                return;
            }
            float num = _brandBoundsLeft;
            float brandBoundsTop = _brandBoundsTop;
            float num2 = Math.Abs(_brandWatermarkShear) * _brandWatermarkHeight * 0.5f;
            int num3 = Math.Max(0, (int)Math.Floor(num - num2) - 1);
            int num4 = Math.Min(value - 1, (int)Math.Ceiling(_brandBoundsRight) + 1);
            int num5 = Math.Max(0, (int)Math.Floor(brandBoundsTop) - 1);
            int num6 = Math.Min(value2 - 1, (int)Math.Ceiling(brandBoundsTop + _brandWatermarkHeight) + 1);
            if (num3 > num4 || num5 > num6)
            {
                return;
            }
            float num7 = (float)(int)_descriptor.GetBrandWatermarkStrength() / 255f * 0.96f;
            if (num7 <= 0f)
            {
                return;
            }
            for (int i = num5; i <= num6; i++)
            {
                int num8 = (value2 - 1 - i) * value * 4;
                float num9 = (float)i - brandBoundsTop;
                float num10 = (num9 - _brandWatermarkHeight * 0.5f) * _brandWatermarkShear;
                for (int j = num3; j <= num4; j++)
                {
                    int num11 = num8 + j * 4;
                    byte b = bytes[num11];
                    byte b2 = bytes[num11 + 1];
                    byte b3 = bytes[num11 + 2];
                    byte b4 = bytes[num11 + 3];
                    if (b4 == 0)
                    {
                        continue;
                    }
                    float num12 = (float)j - num - num10;
                    if (num12 < -1f || num12 > _brandWatermarkWidth + 1f || num9 < -1f || num9 > _brandWatermarkHeight + 1f)
                    {
                        continue;
                    }
                    float num13 = _brandWatermarkMask.SampleCoverage(num12, num9);
                    float num14 = CalculateMaskEdgeCoverage(_brandWatermarkMask, num12, num9, num13);
                    if (num13 <= 0f && num14 <= 0f)
                    {
                        continue;
                    }
                    float num15 = MathF.Sqrt(MathF.Max(0f, (float)(int)b4 / 255f));
                    float num16 = num13 * num7 * num15;
                    float num17 = num14 * GetEdgeEmphasisStrength() * num15;
                    if (!(num16 <= 0f) || !(num17 <= 0f))
                    {
                        if (num17 > 0f)
                        {
                            BlendRgb(ref b, ref b2, ref b3, GetInverseContrastingProtectionColor(b, b2, b3), num17);
                        }
                        if (num16 > 0f)
                        {
                            BlendRgb(ref b, ref b2, ref b3, GetContrastingProtectionColor(b, b2, b3), num16);
                        }
                        bytes[num11] = b;
                        bytes[num11 + 1] = b2;
                        bytes[num11 + 2] = b3;
                        bytes[num11 + 3] = b4;
                    }
                }
            }
        }

        private float GetEdgeEmphasisStrength()
        {
            if (_isEdgeEmphasisEnabled)
            {
                return (float)(int)_descriptor.GetEdgeStrength() / 255f * 0.82f;
            }
            return 0f;
        }

        private static float CalculateMaskEdgeCoverage(object value, float value2, float value3, float value4)
        {
            if (value != null && ((WatermarkMask)value).HasCoverage())
            {
                float num = Math.Max(Math.Max(((WatermarkMask)value).SampleCoverage(value2 - 1f, value3), ((WatermarkMask)value).SampleCoverage(value2 + 1f, value3)), Math.Max(((WatermarkMask)value).SampleCoverage(value2, value3 - 1f), ((WatermarkMask)value).SampleCoverage(value2, value3 + 1f)));
                return Mathf.Clamp01(num - value4 + ((!(num > 0f) || value4 <= 0f) ? 0f : 0.18f));
            }
            return 0f;
        }

        private byte[] BuildPrimaryWatermarkCoverageMask(int value, int value2)
        {
            if (_isPrimaryWatermarkEnabled && value > 0 && value2 > 0)
            {
                long num = (long)value * (long)value2;
                if (num > 0L && num <= 2147483647L)
                {
                    byte[] array = new byte[(int)num];
                    for (int i = 0; i < value2; i++)
                    {
                        for (int j = 0; j < value; j++)
                        {
                            int num2 = i * value + j;
                            array[num2] = CoverageToByte(SamplePrimaryWatermarkCoverage(j, i));
                        }
                    }
                    return array;
                }
                return null;
            }
            return null;
        }

        internal void ApplyStrongProtectionEffectsToPixel(ref byte value, ref byte value2, ref byte value3, ref byte value4, int value5, int value6)
        {
            ApplyProtectionEffectsToPixel(ref value, ref value2, ref value3, ref value4, value5, value6, 1f, 0.52f, 0.68f);
        }

        internal void ApplySubtleProtectionEffectsToPixel(ref byte value, ref byte value2, ref byte value3, ref byte value4, int value5, int value6)
        {
            ApplyProtectionEffectsToPixel(ref value, ref value2, ref value3, ref value4, value5, value6, 0.36f, 0.78f, 0.42f);
        }

        internal void ApplyAlphaNoise(ref byte value, int value2, int value3)
        {
            if (!_isAlphaNoiseEnabled)
            {
                return;
            }
            float num = CalculateCrossDiagonalCoverage(value2, value3);
            if (!(num * 1.08f <= 0f))
            {
                float num2 = num;
                float num3 = CalculateDashedDiagonalCoverage(value2, value3, num * 1.02f);
                float num4 = MathF.Sqrt(MathF.Max(0f, (float)(int)value / 255f)) * MathF.Max(MathF.Max(num2 * 0.52f, num * 0.3f), num3 * 0.28f) * ((float)(int)_descriptor.GetAlphaNoiseStrength() / 255f) * 0.58f;
                if (!(num4 <= 0f))
                {
                    int num5 = (int)Math.Round((GenerateDeterministicNoise(_descriptor.GetSessionSeed() ^ _descriptor.GetStableSeed(), _originX + value2, _originY + value3, 23) - 0.5f) * 2f * (float)(int)_descriptor.GetAlphaNoiseStrength() * num4, MidpointRounding.AwayFromZero);
                    value = ClampToByte(value + num5);
                }
            }
        }

        private void ApplyProtectionEffectsToPixel(ref byte value, ref byte value2, ref byte value3, ref byte value4, int value5, int value6, float value7, float value8, float value9)
        {
            float num = (_usesOpaqueDiagonalOverlay ? CalculateCrossDiagonalCoverage(value5, value6) : 0f);
            float num2 = num * 1.1f;
            float num3 = (_isEdgeEmphasisEnabled ? num : 0f);
            float num4 = (_usesOpaqueDiagonalOverlay ? CalculateDashedDiagonalCoverage(value5, value6, num * 1.04f) : 0f);
            float num5 = MathF.Sqrt(MathF.Max(0f, (float)(int)value4 / 255f));
            float num6 = num5 * num2 * value7 * ((float)(int)_descriptor.GetPrimaryWatermarkStrength() / 255f);
            float num7 = num5 * MathF.Max(num3 * 1f, num * 0.34f) * value8 * ((float)(int)_descriptor.GetEdgeStrength() / 255f);
            float num8 = num5 * num4 * value9 * ((float)(int)_descriptor.GetBrandWatermarkStrength() / 255f) * 0.96f;
            if (num6 > 0f)
            {
                Color32 val = (_usesOpaqueDiagonalOverlay ? _opaqueWhiteColor : GetContrastingProtectionColor(value, value2, value3));
                float num9 = num6 * (0.42f + (float)(int)_descriptor.GetContrastStrength() / 255f * 0.68f);
                BlendRgb(ref value, ref value2, ref value3, val, num9);
            }
            if (num7 > 0f)
            {
                BlendRgb(ref value, ref value2, ref value3, _usesOpaqueDiagonalOverlay ? _opaqueWhiteColor : GetInverseContrastingProtectionColor(value, value2, value3), num7);
            }
            if (num8 > 0f)
            {
                BlendRgb(ref value, ref value2, ref value3, (!_usesOpaqueDiagonalOverlay) ? _accentProtectionColor : _opaqueWhiteColor, num8);
            }
            float num10 = num7 * 0.34f + num8 * 0.2f;
            if (num10 > 0f && _isAlphaNoiseEnabled)
            {
                int num11 = (int)Math.Round((GenerateDeterministicNoise(_descriptor.GetStableSeed(), _originX + value5, _originY + value6, 41) - 0.5f) * 2f * (float)(int)_descriptor.GetAlphaNoiseStrength() * num10, MidpointRounding.AwayFromZero);
                value4 = ClampToByte(value4 + num11);
            }
        }

        private void ApplyAlphaWeightedProtectionWithMask(ref byte value, ref byte value2, ref byte value3, ref byte value4, int value5, int value6, byte[] bytes, int value7, int value8)
        {
            float num = (float)(int)value4 / 255f;
            float num2 = 0.36f + num * 0.64f;
            float num3 = 0.78f - num * 0.26f;
            float num4 = 0.42f + num * 0.26f;
            ApplyProtectionEffectsToPixelWithMask(ref value, ref value2, ref value3, ref value4, value5, value6, num2, num3, num4, bytes, value7, value8);
        }

        private void ApplyAlphaWeightedProtection(ref byte value, ref byte value2, ref byte value3, ref byte value4, int value5, int value6)
        {
            float num = (float)(int)value4 / 255f;
            float num2 = 0.36f + num * 0.64f;
            float num3 = 0.78f - num * 0.26f;
            float num4 = 0.42f + num * 0.26f;
            ApplyProtectionEffectsToPixel(ref value, ref value2, ref value3, ref value4, value5, value6, num2, num3, num4);
        }

        private void ApplyProtectionEffectsToPixelWithMask(ref byte value, ref byte value2, ref byte value3, ref byte value4, int value5, int value6, float value7, float value8, float value9, byte[] bytes, int value10, int value11)
        {
            float num = (_usesOpaqueDiagonalOverlay ? CalculateCrossDiagonalCoverage(value5, value6) : 0f);
            float num2 = num * 1.1f;
            float num3 = ((!_isEdgeEmphasisEnabled) ? 0f : num);
            float num4 = (_usesOpaqueDiagonalOverlay ? CalculateDashedDiagonalCoverage(value5, value6, num * 1.04f) : 0f);
            float num5 = MathF.Sqrt(MathF.Max(0f, (float)(int)value4 / 255f));
            float num6 = num5 * num2 * value7 * ((float)(int)_descriptor.GetPrimaryWatermarkStrength() / 255f);
            float num7 = num5 * MathF.Max(num3 * 1f, num * 0.34f) * value8 * ((float)(int)_descriptor.GetEdgeStrength() / 255f);
            float num8 = num5 * num4 * value9 * ((float)(int)_descriptor.GetBrandWatermarkStrength() / 255f) * 0.96f;
            if (num6 > 0f)
            {
                Color32 val = ((!_usesOpaqueDiagonalOverlay) ? GetContrastingProtectionColor(value, value2, value3) : _opaqueWhiteColor);
                float num9 = num6 * (0.42f + (float)(int)_descriptor.GetContrastStrength() / 255f * 0.68f);
                BlendRgb(ref value, ref value2, ref value3, val, num9);
            }
            if (num7 > 0f)
            {
                BlendRgb(ref value, ref value2, ref value3, _usesOpaqueDiagonalOverlay ? _opaqueWhiteColor : GetInverseContrastingProtectionColor(value, value2, value3), num7);
            }
            if (num8 > 0f)
            {
                BlendRgb(ref value, ref value2, ref value3, _usesOpaqueDiagonalOverlay ? _opaqueWhiteColor : _accentProtectionColor, num8);
            }
            float num10 = num7 * 0.34f + num8 * 0.2f;
            if (num10 > 0f && _isAlphaNoiseEnabled)
            {
                int num11 = (int)Math.Round((GenerateDeterministicNoise(_descriptor.GetStableSeed(), _originX + value5, _originY + value6, 41) - 0.5f) * 2f * (float)(int)_descriptor.GetAlphaNoiseStrength() * num10, MidpointRounding.AwayFromZero);
                value4 = ClampToByte(value4 + num11);
            }
        }

        private static byte CoverageToByte(float value)
        {
            value = Math.Min(1f, Math.Max(0f, value));
            return (byte)Math.Round(value * 255f, MidpointRounding.AwayFromZero);
        }

        private float SampleCoverageMask(byte[] bytes, int value, int value2, int value3, int value4)
        {
            if (_isPrimaryWatermarkEnabled && bytes != null && value > 0 && value2 > 0 && value3 >= 0 && value4 >= 0 && value3 < value && value4 < value2)
            {
                int num = value4 * value + value3;
                if (num >= 0 && num < bytes.Length)
                {
                    return (float)(int)bytes[num] / 255f;
                }
                return 0f;
            }
            return 0f;
        }

        private float CalculateCoverageMaskEdge(byte[] bytes, int value, int value2, int value3, int value4, float value5)
        {
            if (!_isEdgeEmphasisEnabled)
            {
                return 0f;
            }
            float num = Math.Max(Math.Max(SampleCoverageMask(bytes, value, value2, value3 - 1, value4), SampleCoverageMask(bytes, value, value2, value3 + 1, value4)), Math.Max(SampleCoverageMask(bytes, value, value2, value3, value4 - 1), SampleCoverageMask(bytes, value, value2, value3, value4 + 1)));
            return Mathf.Clamp01(num - value5 + ((!(num > 0f) || value5 <= 0f) ? 0f : 0.18f));
        }

        private float SamplePrimaryWatermarkCoverage(int value, int value2)
        {
            if (!_isPrimaryWatermarkEnabled)
            {
                return 0f;
            }
            return SampleRepeatedWatermarkCoverage(_primaryWatermarkMask, _primaryWatermarkText, _primaryWatermarkScale, _primaryWatermarkWidth, _primaryWatermarkHeight, _primaryWatermarkRowSpacing, _primaryWatermarkColumnGap, _primaryWatermarkShear, _primaryWatermarkRowSkew, _primaryWatermarkPatternOffset, value, value2, 1, false);
        }

        private float CalculatePrimaryWatermarkEdgeCoverage(int value, int value2, float value3)
        {
            if (!_isEdgeEmphasisEnabled)
            {
                return 0f;
            }
            float num = Math.Max(Math.Max(SamplePrimaryWatermarkCoverage(value - 1, value2), SamplePrimaryWatermarkCoverage(value + 1, value2)), Math.Max(SamplePrimaryWatermarkCoverage(value, value2 - 1), SamplePrimaryWatermarkCoverage(value, value2 + 1)));
            return Mathf.Clamp01(num - value3 + ((!(num > 0f) || value3 <= 0f) ? 0f : 0.18f));
        }

        private float CalculateCrossDiagonalCoverage(int value, int value2)
        {
            if (_usesOpaqueDiagonalOverlay)
            {
                float num = CalculateLineCoverage(value, value2, 0f, 0f, _maxX, _maxY, _primaryDiagonalRadius);
                float num2 = CalculateLineCoverage(value, value2, _maxX, 0f, 0f, _maxY, _secondaryDiagonalRadius);
                return Mathf.Clamp01(Mathf.Max(num, num2));
            }
            return 0f;
        }

        private float CalculateDashedDiagonalCoverage(int value, int value2, float value3)
        {
            if (value3 <= 0f)
            {
                return 0f;
            }
            float num = 0f;
            float num3;
            float num4;
            float num2 = CalculateLineSegmentCoverage(value, value2, _maxX, 0f, 0f, _maxY, _dashedDiagonalRadius * 0.94f, out num3, out num4);
            if (num2 > 0f && _isDiagonalOverlayEnabled)
            {
                float num5 = ((PositiveModulo(num3 + (float)((_descriptor.GetSessionSeed() >> 4) & 0x3F), _secondaryDashPeriod) <= _secondaryDashPeriod * 0.56f) ? 1f : 0f);
                num = Math.Max(num, num2 * num5 * 0.84f);
            }
            return num * value3;
        }

        private static float CalculateLineCoverage(float value, float value2, float value3, float value4, float value5, float value6, float value7)
        {
            float num;
            float num2;
            return CalculateLineSegmentCoverage(value, value2, value3, value4, value5, value6, value7, out num, out num2);
        }

        private static float CalculateLineSegmentCoverage(float value, float value2, float value3, float value4, float value5, float value6, float value7, out float result, out float result2)
        {
            result = 0f;
            result2 = 0f;
            if (value7 <= 0f)
            {
                return 0f;
            }
            float num = value5 - value3;
            float num2 = value6 - value4;
            float num3 = num * num + num2 * num2;
            if (num3 <= 0.0001f)
            {
                return 0f;
            }
            result2 = Mathf.Sqrt(num3);
            float num4 = ((value - value3) * num + (value2 - value4) * num2) / num3;
            num4 = Mathf.Clamp01(num4);
            result = result2 * num4;
            float num5 = value3 + num * num4;
            float num6 = value4 + num2 * num4;
            float num7 = Mathf.Sqrt((value - num5) * (value - num5) + (value2 - num6) * (value2 - num6));
            return 1f - Mathf.Clamp01(num7 / value7);
        }

        private float SampleRepeatedWatermarkCoverage(WatermarkMask value, string text, float value2, float value3, float value4, float value5, float value6, float value7, float value8, float value9, int value10, int value11, int value12, bool enabled)
        {
            if (!string.IsNullOrEmpty(text) && !(value2 <= 0f) && !(value3 <= 0f) && !(value4 <= 0f) && value.HasCoverage() && _width > 0 && _height > 0)
            {
                float num = ((!enabled) ? 0.70710677f : (-0.70710677f));
                float num2 = 0.70710677f;
                float num3 = -0.70710677f;
                float num4 = num;
                float num5 = _centerX + num3 * value9;
                float num6 = _centerY + num4 * value9;
                float num7 = (float)value10 - num5;
                float num8 = (float)value11 - num6;
                float num9 = num7 * num + num8 * num2;
                float num10 = num7 * num3 + num8 * num4;
                float num11 = value3 + Math.Max(value2 * 2f, value6);
                if (num11 <= 0.001f)
                {
                    return 0f;
                }
                float num12 = Math.Max(value4 * 1.15f, value5);
                int num13 = ((num12 < _diagonalLength * 0.72f) ? 1 : 0);
                int num14 = (int)Math.Round(num9 / num11, MidpointRounding.AwayFromZero);
                int num15 = ((num13 > 0) ? ((int)Math.Round(num10 / num12, MidpointRounding.AwayFromZero)) : 0);
                float num16 = 0f;
                for (int i = num15 - num13; i <= num15 + num13; i++)
                {
                    float num17 = (float)i * num12;
                    if (i != 0)
                    {
                        float num18 = (GenerateDeterministicNoise(_descriptor.GetStableSeed() ^ (value12 * 9137), i, _securitySalt, value12) - 0.5f) * Math.Max(1f, value2 * 0.65f);
                        num17 += (float)i * value8 * 0.14f + num18;
                    }
                    for (int j = num14 - 1; j <= num14 + 1; j++)
                    {
                        float num19 = (float)j * num11;
                        if (j != 0 || i != 0)
                        {
                            float num20 = (GenerateDeterministicNoise(_descriptor.GetSessionSeed() ^ (value12 * 48611), j, i, value12) - 0.5f) * Math.Max(0.75f, value2 * (0.32f + Math.Abs(value7) * 0.85f));
                            num19 += num20 + (float)i * value8 * 0.08f;
                        }
                        float num21 = num5 + num * num19 + num3 * num17;
                        float num22 = num6 + num2 * num19 + num4 * num17;
                        float num23 = (float)value10 - (num21 - value3 * 0.5f);
                        float num24 = (float)value11 - (num22 - value4 * 0.5f);
                        if (!(num23 < -1f) && !(num23 > value3 + 1f) && !(num24 < -1f) && !(num24 > value4 + 1f))
                        {
                            float num25 = value.SampleCoverage(num23, num24);
                            if (num25 > num16)
                            {
                                num16 = num25;
                            }
                        }
                    }
                }
                return num16;
            }
            return 0f;
        }

        private Color32 GetContrastingProtectionColor(byte value, byte value2, byte value3)
        {
            if (!(CalculateLuminance(value, value2, value3) >= 0.58f))
            {
                return _lightProtectionColor;
            }
            return _darkProtectionColor;
        }

        private Color32 GetInverseContrastingProtectionColor(byte value, byte value2, byte value3)
        {
            if (!(CalculateLuminance(value, value2, value3) >= 0.58f))
            {
                return _darkProtectionColor;
            }
            return _lightProtectionColor;
        }

        private static float NormalizeByteToSignedUnit(byte value)
        {
            return (float)(int)value / 127.5f - 1f;
        }

        private static float CalculateLuminance(byte value, byte value2, byte value3)
        {
            return ((float)(int)value * 0.2126f + (float)(int)value2 * 0.7152f + (float)(int)value3 * 0.0722f) / 255f;
        }

        private static float GenerateDeterministicNoise(int value, int value2, int value3, int value4)
        {
            int num = value ^ (value2 * 73856093) ^ (value3 * 19349663) ^ (value4 * 83492791);
            int num2 = (int)((uint)num ^ ((uint)num >> 16)) * -2048144789;
            int num3 = (int)((uint)num2 ^ ((uint)num2 >> 13)) * -1028477387;
            return (float)(((uint)num3 ^ ((uint)num3 >> 16)) & 0xFFFF) / 65535f;
        }

        private static void BlendRgb(ref byte value, ref byte value2, ref byte value3, Color32 value4, float value5)
        {
            value = LerpByte(value, value4.r, value5);
            value2 = LerpByte(value2, value4.g, value5);
            value3 = LerpByte(value3, value4.b, value5);
        }

        private static byte LerpByte(byte value, byte value2, float value3)
        {
            if (value3 <= 0f)
            {
                return value;
            }
            if (value3 >= 1f)
            {
                return value2;
            }
            return (byte)Math.Round((float)(int)value + (float)(value2 - value) * value3, MidpointRounding.AwayFromZero);
        }

        private static byte ClampToByte(int value)
        {
            return (byte)Math.Max(0, Math.Min(255, value));
        }

        private static float PositiveModulo(float value, float value2)
        {
            if (value2 <= 0f)
            {
                return 0f;
            }
            float num = value % value2;
            if (!(num < 0f))
            {
                return num;
            }
            return num + value2;
        }

        private static int PositiveModulo(int value, int value2)
        {
            if (value2 > 0)
            {
                int num = value % value2;
                if (num < 0)
                {
                    return num + value2;
                }
                return num;
            }
            return 0;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static RenderProtectionSession GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
