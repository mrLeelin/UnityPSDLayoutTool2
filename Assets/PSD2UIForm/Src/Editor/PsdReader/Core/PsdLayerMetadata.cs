using System;
using System.Runtime.CompilerServices;
using LayerAdditionalInfoReaderNamespace;
using LayerBlendingRangesReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using LayerMaskReaderNamespace;
using PsdPropertyExtensionsNamespace;

namespace PsdLayerMetadataNamespace
{
    internal class PsdLayerMetadata
    {
        private readonly LayerMaskReader _maskReader;

        private readonly LayerBlendingRangesReader _blendingRangesReader;

        private readonly LayerAdditionalInfoReader _additionalInfoReader;

        private readonly string _name;

        private SectionType _sectionType;

        private Guid _smartObjectId;

        internal static PsdLayerMetadata s_ObfuscationSentinel;

        public SectionType SectionType => _sectionType;

        public string Name => _name;

        public LayerMask Mask => _maskReader.Value;

        public IProperties Resources => _additionalInfoReader.Value;

        public PsdLayerMetadata(LayerMaskReader layerMaskReader, LayerBlendingRangesReader layerBlendingRangesReader, LayerAdditionalInfoReader layerAdditionalInfoReader, string text)
        {
            _maskReader = layerMaskReader;
            _blendingRangesReader = layerBlendingRangesReader;
            _additionalInfoReader = layerAdditionalInfoReader;
            _name = text;
            _additionalInfoReader.TryGetProperty(ref _name, "luni.Name");
            if (!_additionalInfoReader.TryGetProperty(ref _sectionType, "lsct.SectionType"))
            {
                _additionalInfoReader.TryGetProperty(ref _sectionType, "lsdk.SectionType");
            }
            if (_additionalInfoReader.Contains("SoLd.Idnt"))
            {
                _smartObjectId = _additionalInfoReader.GetGuid("SoLd.Idnt");
            }
            else if (_additionalInfoReader.Contains("SoLE.Idnt"))
            {
                _smartObjectId = _additionalInfoReader.GetGuid("SoLE.Idnt");
            }
        }

        [SpecialName]
        public Guid GetSmartObjectId()
        {
            return _smartObjectId;
        }

        [SpecialName]
        public object GetBlendingRanges()
        {
            return _blendingRangesReader.Value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdLayerMetadata GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
