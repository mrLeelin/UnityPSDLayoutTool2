using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GlobalLayerMaskInfoReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using LayerInfoSectionReaderNamespace;
using PsdPropertyExtensionsNamespace;

namespace LayerAndMaskSectionDataNamespace
{
    internal class LayerAndMaskSectionData
    {
        private readonly LayerInfoSectionReader _layerInfo;

        private readonly GlobalLayerMaskInfoReader _globalLayerMaskInfo;

        private readonly IProperties _resources;

        private ILinkedLayer[] _linkedLayers;

        private static LayerAndMaskSectionData s_ObfuscationSentinel;

        public PsdLayer[] Layers
        {
            get
            {
                if (_layerInfo != null && _layerInfo.GetSectionLength() > 0L)
                {
                    return _layerInfo.Value ?? Array.Empty<PsdLayer>();
                }
                if (_resources != null)
                {
                    string[] array = new string[2] { "Lr16", "Lr32" };
                    foreach (string text in array)
                    {
                        if (_resources.ContainsProperty(text, "Layers"))
                        {
                            PsdLayer[] array2 = _resources.GetProperty<PsdLayer[]>(text, new string[1] { "Layers" });
                            if (array2 != null)
                            {
                                return array2;
                            }
                        }
                    }
                }
                return Array.Empty<PsdLayer>();
            }
        }

        public IProperties Resources => _resources;

        public LayerAndMaskSectionData(LayerInfoSectionReader layerInfoSectionReader, GlobalLayerMaskInfoReader globalLayerMaskInfoReader, IProperties properties)
        {
            _layerInfo = layerInfoSectionReader;
            _globalLayerMaskInfo = globalLayerMaskInfoReader;
            _resources = properties;
        }

        [SpecialName]
        public ILinkedLayer[] GetLinkedLayers()
        {
            if (_linkedLayers == null)
            {
                List<ILinkedLayer> list = new List<ILinkedLayer>();
                string[] array = new string[4] { "lnk2", "lnk3", "lnkD", "lnkE" };
                foreach (string text in array)
                {
                    if (_resources.Contains(text))
                    {
                        ILinkedLayer[] collection = _resources.GetProperty<ILinkedLayer[]>(text, new string[1] { "Items" });
                        list.AddRange(collection);
                    }
                }
                _linkedLayers = list.ToArray();
            }
            return _linkedLayers;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LayerAndMaskSectionData GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
