using System;
using System.Collections.Generic;
using System.Linq;
using PsdSectionReaderNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using PsdLayerTraversalExtensionsNamespace;

namespace LayerInfoSectionReaderNamespace
{
    internal class LayerInfoSectionReader : PsdSectionReader<PsdLayer[]>
    {
        private static LayerInfoSectionReader s_ObfuscationSentinel;

        public LayerInfoSectionReader(PsdBinaryReader psdBinaryReader, PsdDocument psdDocument)
            : base(psdBinaryReader, true, (object)psdDocument)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out PsdLayer[] result)
        {
            result = ReadLayers(reader, context as PsdDocument);
        }

        public static PsdLayer[] BuildLayerHierarchy(object psdLayer, object psdLayers)
        {
            Stack<PsdLayer> stack = new Stack<PsdLayer>();
            List<PsdLayer> list = new List<PsdLayer>();
            Dictionary<PsdLayer, List<PsdLayer>> dictionary = new Dictionary<PsdLayer, List<PsdLayer>>();
            foreach (PsdLayer item in ((IEnumerable<PsdLayer>)psdLayers).Reverse())
            {
                if (item.SectionType == SectionType.Divider)
                {
                    psdLayer = ((stack.Count <= 0) ? null : stack.Pop());
                    continue;
                }
                if (psdLayer == null)
                {
                    list.Insert(0, item);
                }
                else
                {
                    if (!dictionary.ContainsKey((PsdLayer)psdLayer))
                    {
                        dictionary.Add((PsdLayer)psdLayer, new List<PsdLayer>());
                    }
                    dictionary[(PsdLayer)psdLayer].Insert(0, item);
                    item.Parent = (PsdLayer)psdLayer;
                }
                if (item.SectionType == SectionType.Opend || item.SectionType == SectionType.Closed)
                {
                    stack.Push((PsdLayer)psdLayer);
                    psdLayer = item;
                }
            }
            foreach (KeyValuePair<PsdLayer, List<PsdLayer>> item2 in dictionary)
            {
                item2.Key.Childs = item2.Value.ToArray();
            }
            return list.ToArray();
        }

        internal static PsdLayer[] ReadLayers(object value, object psdDocument)
        {
            int num = Math.Abs((int)((PsdBinaryReader)value).ReadInt16());
            PsdLayer[] array = new PsdLayer[num];
            for (int i = 0; i < num; i++)
            {
                array[i] = new PsdLayer((PsdBinaryReader)value, (PsdDocument)psdDocument);
            }
            PsdLayer[] array2 = array;
            for (int j = 0; j < array2.Length; j++)
            {
                array2[j].ReadChannels((PsdBinaryReader)value);
            }
            array = BuildLayerHierarchy(null, array);
            foreach (PsdLayer item in array.SelectMany((PsdLayer item) => item.EnumerateDescendantsAndSelf()).Reverse())
            {
                item.ComputeBounds();
            }
            return array;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LayerInfoSectionReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
