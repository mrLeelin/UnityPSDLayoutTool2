using System.Collections.Generic;
using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using PsdDescriptorReaderNamespace;
using AdditionalLayerInfoSectionNamespace;
using PropertyCollectionNamespace;

namespace SlicesResourceParserNamespace
{
    [AdditionalLayerInfoParserAttribute("1050", DisplayName = "Slices")]
    internal class SlicesResourceParser : AdditionalLayerInfoSection
    {
        private static SlicesResourceParser s_ObfuscationSentinel;

        public SlicesResourceParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            PropertyCollection value = new PropertyCollection();
            if (reader.ReadInt32() == 6)
            {
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadUnicodeString();
                int num = reader.ReadInt32();
                List<IProperties> list = new List<IProperties>(num);
                for (int i = 0; i < num; i++)
                {
                    list.Add(ReadLegacySlice(reader));
                }
            }
            object[] obj = ((IProperties)new PsdDescriptorReader(reader))["slices.Items[0]"] as object[];
            List<IProperties> list2 = new List<IProperties>(obj.Length);
            object[] array = obj;
            foreach (object obj2 in array)
            {
                list2.Add(ConvertDescriptorSlice(obj2 as IProperties));
            }
            value["Items"] = list2.ToArray();
            result = value;
        }

        private static PropertyCollection ReadLegacySlice(object value)
        {
            PropertyCollection obj = new PropertyCollection
            {
                ["ID"] = ((PsdBinaryReader)value).ReadInt32(),
                ["GroupID"] = ((PsdBinaryReader)value).ReadInt32()
            };
            if (((PsdBinaryReader)value).ReadInt32() == 1)
            {
                ((PsdBinaryReader)value).ReadInt32();
            }
            obj["Name"] = ((PsdBinaryReader)value).ReadUnicodeString();
            ((PsdBinaryReader)value).ReadInt32();
            obj["Left"] = ((PsdBinaryReader)value).ReadInt32();
            obj["Top"] = ((PsdBinaryReader)value).ReadInt32();
            obj["Right"] = ((PsdBinaryReader)value).ReadInt32();
            obj["Bottom"] = ((PsdBinaryReader)value).ReadInt32();
            obj["Url"] = ((PsdBinaryReader)value).ReadUnicodeString();
            obj["Target"] = ((PsdBinaryReader)value).ReadUnicodeString();
            obj["Message"] = ((PsdBinaryReader)value).ReadUnicodeString();
            obj["AltTag"] = ((PsdBinaryReader)value).ReadUnicodeString();
            ((PsdBinaryReader)value).ReadBoolean();
            ((PsdBinaryReader)value).ReadUnicodeString();
            obj["HorzAlign"] = ((PsdBinaryReader)value).ReadInt32();
            obj["VertAlign"] = ((PsdBinaryReader)value).ReadInt32();
            obj["Alpha"] = ((PsdBinaryReader)value).ReadByte();
            obj["Red"] = ((PsdBinaryReader)value).ReadByte();
            obj["Green"] = ((PsdBinaryReader)value).ReadByte();
            obj["Blue"] = ((PsdBinaryReader)value).ReadByte();
            return obj;
        }

        private static PropertyCollection ConvertDescriptorSlice(object value)
        {
            PropertyCollection value2 = new PropertyCollection();
            value2["ID"] = (int)((IProperties)value)["sliceID"];
            value2["GroupID"] = (int)((IProperties)value)["groupID"];
            if (((IProperties)value).Contains("Nm"))
            {
                value2["Name"] = ((IProperties)value)["Nm"] as string;
            }
            value2["Left"] = (int)((IProperties)value)["bounds.Left"];
            value2["Top"] = (int)((IProperties)value)["bounds.Top"];
            value2["Right"] = (int)((IProperties)value)["bounds.Rght"];
            value2["Bottom"] = (int)((IProperties)value)["bounds.Btom"];
            value2["Url"] = ((IProperties)value)["url"] as string;
            value2["Target"] = ((IProperties)value)["null"] as string;
            value2["Message"] = ((IProperties)value)["Msge"] as string;
            value2["AltTag"] = ((IProperties)value)["altTag"] as string;
            if (((IProperties)value).Contains("bgColor"))
            {
                value2["Alpha"] = (byte)(int)((IProperties)value)["bgColor.alpha"];
                value2["Red"] = (byte)(int)((IProperties)value)["bgColor.Rd"];
                value2["Green"] = (byte)(int)((IProperties)value)["bgColor.Grn"];
                value2["Blue"] = (byte)(int)((IProperties)value)["bgColor.Bl"];
            }
            return value2;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static SlicesResourceParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
