using System.Collections;
using PsdBinaryReaderNamespace;
using PropertyCollectionNamespace;

namespace PsdEngineDataParserNamespace
{
    internal class PsdEngineDataParser : PropertyCollection
    {
        internal static PsdEngineDataParser s_ObfuscationSentinel;

        public PsdEngineDataParser(PsdBinaryReader psdBinaryReader)
        {
            psdBinaryReader.ReadInt32();
            psdBinaryReader.ValidateRepeatedChar('\n', 2);
            ReadPropertyBlock(psdBinaryReader, 0, this);
        }

        private void ReadPropertyBlock(PsdBinaryReader value2, int value3, PropertyCollection value4)
        {
            value2.ValidateRepeatedChar('\t', value3);
            switch (value2.ReadChar())
            {
            case ']':
                return;
            case '<':
                value2.ValidateChar('<');
                break;
            }
            value2.ValidateChar('\n');
            while (true)
            {
                value2.ValidateRepeatedChar('\t', value3);
                char c = value2.ReadChar();
                if (c == '>')
                {
                    break;
                }
                value2.ReadChar();
                string text = string.Empty;
                while (true)
                {
                    c = value2.ReadChar();
                    if (c == ' ' || c == '\n')
                    {
                        break;
                    }
                    text += c;
                }
                switch (c)
                {
                case '\n':
                {
                    PropertyCollection value5 = new PropertyCollection();
                    ReadPropertyBlock(value2, value3 + 1, value5);
                    if (value5.Count > 0)
                    {
                        value4.Add(text, value5);
                    }
                    value2.ValidateChar('\n');
                    break;
                }
                case ' ':
                {
                    object value = ReadValue(value2, value3 + 1);
                    value4.Add(text, value);
                    break;
                }
                }
            }
            value2.ValidateChar('>');
        }

        private object ReadValue(PsdBinaryReader value, int value2)
        {
            char c = value.ReadChar();
            switch (c)
            {
            case ']':
                return null;
            case '(':
            {
                string text2 = string.Empty;
                value.ReadInt16();
                while (true)
                {
                    char c2 = value.ReadChar();
                    if (c2 == ')')
                    {
                        break;
                    }
                    char c3 = value.ReadChar();
                    if (c3 == '\\')
                    {
                        c3 = value.ReadChar();
                    }
                    text2 = ((c3 != '\r') ? (text2 + (char)(((uint)c2 << 8) | c3)) : (text2 + "\n"));
                }
                value.ValidateChar('\n');
                return text2;
            }
            case '[':
            {
                ArrayList arrayList = new ArrayList();
                c = value.ReadChar();
                while (true)
                {
                    switch (c)
                    {
                    case '\n':
                    {
                        PropertyCollection value3 = new PropertyCollection();
                        ReadPropertyBlock(value, value2, value3);
                        value.ValidateChar('\n');
                        if (value3.Count != 0)
                        {
                            arrayList.Add(value3);
                            break;
                        }
                        return arrayList;
                    }
                    case ' ':
                    {
                        object obj = ReadValue(value, value2);
                        if (obj != null)
                        {
                            arrayList.Add(obj);
                            break;
                        }
                        value.ValidateChar('\n');
                        return arrayList;
                    }
                    }
                }
            }
            default:
            {
                string text = string.Empty;
                do
                {
                    text += c;
                    c = value.ReadChar();
                }
                while (c != '\n' && c != ' ');
                if (!int.TryParse(text, out var result))
                {
                    if (!float.TryParse(text, out var result2))
                    {
                        if (!bool.TryParse(text, out var result3))
                        {
                            return text;
                        }
                        return result3;
                    }
                    return result2;
                }
                return result;
            }
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdEngineDataParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
