using System.Collections.Generic;
using PsdBinaryReaderNamespace;
using PropertyCollectionNamespace;
using PsdDescriptorValueReaderNamespace;

namespace PsdTypedValueListNamespace
{
    internal class PsdTypedValueList : PropertyCollection
    {
        internal static PsdTypedValueList s_ObfuscationSentinel;

        public PsdTypedValueList(PsdBinaryReader psdBinaryReader)
        {
            List<object> list = new List<object>();
            int num = psdBinaryReader.ReadInt32();
            for (int i = 0; i < num; i++)
            {
                object item = PsdDescriptorValueReader.ReadValue(psdBinaryReader.ReadSignature(), psdBinaryReader);
                list.Add(item);
            }
            Add("Items", list.ToArray());
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdTypedValueList GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
