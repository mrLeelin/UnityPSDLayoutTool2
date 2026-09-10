using System.Collections.Generic;
using PsdBinaryReaderNamespace;
using PsdBinaryUtilityNamespace;
using PropertyCollectionNamespace;

namespace PsdObjectArrayNamespace
{
    internal class PsdObjectArray : PropertyCollection
    {
        internal static PsdObjectArray s_ObfuscationSentinel;

        public PsdObjectArray(PsdBinaryReader psdBinaryReader)
        {
            psdBinaryReader.ReadInt32();
            Add("Name", psdBinaryReader.ReadUnicodeString());
            Add("ClassID", psdBinaryReader.ReadDescriptorId());
            int num = psdBinaryReader.ReadInt32();
            List<PropertyCollection> list = new List<PropertyCollection>();
            for (int i = 0; i < num; i++)
            {
                PropertyCollection value = new PropertyCollection
                {
                    {
                        "Type1",
                        psdBinaryReader.ReadDescriptorId()
                    },
                    {
                        "EnumName",
                        psdBinaryReader.ReadSignature()
                    },
                    {
                        "Type2",
                        PsdBinaryUtility.ParseUnitType(psdBinaryReader.ReadSignature())
                    }
                };
                int num2 = psdBinaryReader.ReadInt32();
                value.Add("Values", psdBinaryReader.ReadDoubles(num2));
                list.Add(value);
            }
            Add("items", list.ToArray());
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdObjectArray GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
