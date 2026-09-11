using System;
using PsdObjectArrayNamespace;
using PsdAliasValueNamespace;
using PsdOffsetReferenceNamespace;
using PsdBinaryReaderNamespace;
using PsdPropertyReferenceNamespace;
using PsdClassValueNamespace;
using PsdDescriptorReaderNamespace;
using PsdReferenceValueNamespace;
using PsdEnumValueNamespace;
using PsdUnitFloatNamespace;
using PropertyCollectionNamespace;
using PsdListValueNamespace;

namespace PsdDescriptorValueReaderNamespace
{
    internal sealed class PsdDescriptorValueReader
    {
        internal static PsdDescriptorValueReader s_ObfuscationSentinel;

        public static object ReadValue(object text, object psdBinaryReader)
        {
            return text switch
            {
                "obj " => new PsdReferenceValue((PsdBinaryReader)psdBinaryReader), 
                "tdta" => ReadRawData(psdBinaryReader), 
                "UntF" => new PsdUnitFloat((PsdBinaryReader)psdBinaryReader), 
                "doub" => ((PsdBinaryReader)psdBinaryReader).ReadDouble(), 
                "indx" => ReadIndexReference(psdBinaryReader), 
                "type" => new PsdClassValue((PsdBinaryReader)psdBinaryReader), 
                "Objc" => new PsdDescriptorReader((PsdBinaryReader)psdBinaryReader, false), 
                "ObAr" => new PsdObjectArray((PsdBinaryReader)psdBinaryReader), 
                "TEXT" => ((PsdBinaryReader)psdBinaryReader).ReadUnicodeString(), 
                "VlLs" => new PsdListValue((PsdBinaryReader)psdBinaryReader), 
                "Idnt" => ReadIdentifierReference(psdBinaryReader), 
                "long" => ((PsdBinaryReader)psdBinaryReader).ReadInt32(), 
                "name" => ReadNameReference(psdBinaryReader), 
                "enum" => new PsdEnumValue((PsdBinaryReader)psdBinaryReader), 
                "prop" => new PsdPropertyReference((PsdBinaryReader)psdBinaryReader), 
                "Clss" => new PsdClassValue((PsdBinaryReader)psdBinaryReader), 
                "bool" => ((PsdBinaryReader)psdBinaryReader).ReadBoolean(), 
                "alis" => new PsdAliasValue((PsdBinaryReader)psdBinaryReader), 
                "GlbO" => new PsdDescriptorReader((PsdBinaryReader)psdBinaryReader, false), 
                "GlbC" => new PsdClassValue((PsdBinaryReader)psdBinaryReader), 
                "Enmr" => new PsdEnumValue((PsdBinaryReader)psdBinaryReader), 
                "comp" => ((PsdBinaryReader)psdBinaryReader).ReadInt64(), 
                "rele" => new PsdOffsetReference((PsdBinaryReader)psdBinaryReader), 
                _ => throw new NotSupportedException((string)text), 
            };
        }

        private static PropertyCollection ReadRawData(object value)
        {
            PropertyCollection value2 = new PropertyCollection(1);
            int num = ((PsdBinaryReader)value).ReadInt32();
            value2.Add("Data", ((PsdBinaryReader)value).ReadBytes(num));
            return value2;
        }

        private static PropertyCollection ReadIdentifierReference(object value)
        {
            return new PropertyCollection(3)
            {
                {
                    "Name",
                    ((PsdBinaryReader)value).ReadUnicodeString()
                },
                {
                    "ClassID",
                    ((PsdBinaryReader)value).ReadDescriptorId()
                },
                {
                    "Identifier",
                    ((PsdBinaryReader)value).ReadInt32()
                }
            };
        }

        private static PropertyCollection ReadIndexReference(object value)
        {
            return new PropertyCollection(3)
            {
                {
                    "Name",
                    ((PsdBinaryReader)value).ReadUnicodeString()
                },
                {
                    "ClassID",
                    ((PsdBinaryReader)value).ReadDescriptorId()
                },
                {
                    "Index",
                    ((PsdBinaryReader)value).ReadInt32()
                }
            };
        }

        private static PropertyCollection ReadNameReference(object value)
        {
            return new PropertyCollection(3)
            {
                {
                    "Name",
                    ((PsdBinaryReader)value).ReadUnicodeString()
                },
                {
                    "ClassID",
                    ((PsdBinaryReader)value).ReadDescriptorId()
                },
                {
                    "Value",
                    ((PsdBinaryReader)value).ReadUnicodeString()
                }
            };
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdDescriptorValueReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
