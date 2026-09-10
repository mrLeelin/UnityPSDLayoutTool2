using System.Runtime.CompilerServices;
using RenderProtectionFlagsNamespace;

namespace RenderProtectionDescriptorNamespace
{
    internal sealed class RenderProtectionDescriptor
    {
        [CompilerGenerated]
        private int _version = 1;

        [CompilerGenerated]
        private int _stableSeed;

        [CompilerGenerated]
        private int _sessionSeed;

        [CompilerGenerated]
        private byte _primaryWatermarkStrength;

        [CompilerGenerated]
        private byte _brandWatermarkStrength;

        [CompilerGenerated]
        private byte _primaryLayoutVariant;

        [CompilerGenerated]
        private byte _secondaryLayoutVariant;

        [CompilerGenerated]
        private byte _alphaNoiseStrength;

        [CompilerGenerated]
        private byte _contrastStrength;

        [CompilerGenerated]
        private byte _edgeStrength;

        [CompilerGenerated]
        private byte _positionJitterSeed;

        [CompilerGenerated]
        private RenderProtectionFlags _protectionFlags;

        [CompilerGenerated]
        private string _layerPathKey = string.Empty;

        [CompilerGenerated]
        private string _normalizedBounds = string.Empty;

        [CompilerGenerated]
        private string _descriptorMacHex = string.Empty;

        [CompilerGenerated]
        private string _protectionFingerprint = string.Empty;

        private static RenderProtectionDescriptor s_ObfuscationSentinel;

        internal int Version
        {
            [CompilerGenerated]
            get
            {
                return _version;
            }
            [CompilerGenerated]
            set
            {
                _version = value;
            }
        }

        [SpecialName]
        [CompilerGenerated]
        internal int GetStableSeed()
        {
            return _stableSeed;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetStableSeed(int value)
        {
            _stableSeed = value;
        }

        [SpecialName]
        [CompilerGenerated]
        internal int GetSessionSeed()
        {
            return _sessionSeed;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetSessionSeed(int value)
        {
            _sessionSeed = value;
        }

        [SpecialName]
        [CompilerGenerated]
        internal byte GetPrimaryWatermarkStrength()
        {
            return _primaryWatermarkStrength;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetPrimaryWatermarkStrength(byte value)
        {
            _primaryWatermarkStrength = value;
        }

        [SpecialName]
        [CompilerGenerated]
        internal byte GetBrandWatermarkStrength()
        {
            return _brandWatermarkStrength;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetBrandWatermarkStrength(byte value)
        {
            _brandWatermarkStrength = value;
        }

        [SpecialName]
        [CompilerGenerated]
        internal byte GetPrimaryLayoutVariant()
        {
            return _primaryLayoutVariant;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetPrimaryLayoutVariant(byte value)
        {
            _primaryLayoutVariant = value;
        }

        [SpecialName]
        [CompilerGenerated]
        internal byte GetSecondaryLayoutVariant()
        {
            return _secondaryLayoutVariant;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetSecondaryLayoutVariant(byte value)
        {
            _secondaryLayoutVariant = value;
        }

        [SpecialName]
        [CompilerGenerated]
        internal byte GetAlphaNoiseStrength()
        {
            return _alphaNoiseStrength;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetAlphaNoiseStrength(byte value)
        {
            _alphaNoiseStrength = value;
        }

        [SpecialName]
        [CompilerGenerated]
        internal byte GetContrastStrength()
        {
            return _contrastStrength;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetContrastStrength(byte value)
        {
            _contrastStrength = value;
        }

        [SpecialName]
        [CompilerGenerated]
        internal byte GetEdgeStrength()
        {
            return _edgeStrength;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetEdgeStrength(byte value)
        {
            _edgeStrength = value;
        }

        [SpecialName]
        [CompilerGenerated]
        internal byte GetPositionJitterSeed()
        {
            return _positionJitterSeed;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetPositionJitterSeed(byte value)
        {
            _positionJitterSeed = value;
        }

        [SpecialName]
        [CompilerGenerated]
        internal RenderProtectionFlags GetProtectionFlags()
        {
            return _protectionFlags;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetProtectionFlags(RenderProtectionFlags renderProtectionFlags)
        {
            _protectionFlags = renderProtectionFlags;
        }

        [SpecialName]
        [CompilerGenerated]
        internal string GetLayerPathKey()
        {
            return _layerPathKey;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetLayerPathKey(string path)
        {
            _layerPathKey = path;
        }

        [SpecialName]
        [CompilerGenerated]
        internal string GetNormalizedBounds()
        {
            return _normalizedBounds;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetNormalizedBounds(string text)
        {
            _normalizedBounds = text;
        }

        [SpecialName]
        [CompilerGenerated]
        internal string GetDescriptorMacHex()
        {
            return _descriptorMacHex;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetDescriptorMacHex(string text)
        {
            _descriptorMacHex = text;
        }

        [SpecialName]
        [CompilerGenerated]
        internal string GetProtectionFingerprint()
        {
            return _protectionFingerprint;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetProtectionFingerprint(string text)
        {
            _protectionFingerprint = text;
        }

        [SpecialName]
        internal bool HasActiveProtection()
        {
            if (GetProtectionFlags() != 0)
            {
                if (GetPrimaryWatermarkStrength() <= 0 && GetBrandWatermarkStrength() <= 0 && GetAlphaNoiseStrength() <= 0)
                {
                    return GetEdgeStrength() > 0;
                }
                return true;
            }
            return false;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static RenderProtectionDescriptor GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
