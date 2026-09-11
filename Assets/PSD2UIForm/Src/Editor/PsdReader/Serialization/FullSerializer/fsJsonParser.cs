using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace cn.efunstudio.psdreader.FullSerializer
{
    public class fsJsonParser
    {
        private int _start;

        private string _input;

        private readonly StringBuilder _cachedStringBuilder = new StringBuilder(256);

        private static fsJsonParser s_ObfuscationSentinel;

        private fsResult MakeFailure(string message)
        {
            int num = Math.Max(0, _start - 20);
            int length = Math.Min(50, _input.Length - num);
            return fsResult.Fail("Error while parsing: " + message + "; context = <" + _input.Substring(num, length) + ">");
        }

        private bool TryMoveNext()
        {
            if (_start >= _input.Length)
            {
                return false;
            }
            _start++;
            return true;
        }

        private bool HasValue()
        {
            return HasValue(0);
        }

        private bool HasValue(int offset)
        {
            if (_start + offset < 0)
            {
                return false;
            }
            return _start + offset < _input.Length;
        }

        private char Character()
        {
            return Character(0);
        }

        private char Character(int offset)
        {
            return _input[_start + offset];
        }

        private void SkipSpace()
        {
            while (HasValue())
            {
                if (!char.IsWhiteSpace(Character()))
                {
                    if (!HasValue(1) || Character(0) != '/')
                    {
                        break;
                    }
                    if (Character(1) == '/')
                    {
                        while (HasValue() && !Environment.NewLine.Contains(Character().ToString() ?? ""))
                        {
                            TryMoveNext();
                        }
                    }
                    else
                    {
                        if (Character(1) != '*')
                        {
                            continue;
                        }
                        TryMoveNext();
                        TryMoveNext();
                        while (HasValue(1))
                        {
                            if (Character(0) != '*' || Character(1) != '/')
                            {
                                TryMoveNext();
                                continue;
                            }
                            TryMoveNext();
                            TryMoveNext();
                            TryMoveNext();
                            break;
                        }
                    }
                }
                else
                {
                    TryMoveNext();
                }
            }
        }

        private bool IsHex(char c)
        {
            if ((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))
            {
                return true;
            }
            if (c < 'A')
            {
                return false;
            }
            return c <= 'F';
        }

        private uint ParseSingleChar(char c1, uint multipliyer)
        {
            uint result = 0u;
            if (c1 >= '0' && c1 <= '9')
            {
                result = (uint)(c1 - 48) * multipliyer;
            }
            else if (c1 >= 'A' && c1 <= 'F')
            {
                result = (uint)(c1 - 65 + 10) * multipliyer;
            }
            else if (c1 >= 'a' && c1 <= 'f')
            {
                result = (uint)(c1 - 97 + 10) * multipliyer;
            }
            return result;
        }

        private uint ParseUnicode(char c1, char c2, char c3, char c4)
        {
            uint num = ParseSingleChar(c1, 4096u);
            uint num2 = ParseSingleChar(c2, 256u);
            uint num3 = ParseSingleChar(c3, 16u);
            uint num4 = ParseSingleChar(c4, 1u);
            return num + num2 + num3 + num4;
        }

        private fsResult TryUnescapeChar(out char escaped)
        {
            TryMoveNext();
            if (!HasValue())
            {
                escaped = ' ';
                return MakeFailure("Unexpected end of input after \\");
            }
            switch (Character())
            {
            case '/':
                TryMoveNext();
                escaped = '/';
                return fsResult.Success;
            case '"':
                TryMoveNext();
                escaped = '"';
                return fsResult.Success;
            case '\\':
                TryMoveNext();
                escaped = '\\';
                return fsResult.Success;
            case '0':
                TryMoveNext();
                escaped = '\0';
                return fsResult.Success;
            case 'r':
                TryMoveNext();
                escaped = '\r';
                return fsResult.Success;
            case 't':
                TryMoveNext();
                escaped = '\t';
                return fsResult.Success;
            case 'u':
                TryMoveNext();
                if (IsHex(Character(0)) && IsHex(Character(1)) && IsHex(Character(2)) && IsHex(Character(3)))
                {
                    uint num = ParseUnicode(Character(0), Character(1), Character(2), Character(3));
                    TryMoveNext();
                    TryMoveNext();
                    TryMoveNext();
                    TryMoveNext();
                    escaped = (char)num;
                    return fsResult.Success;
                }
                escaped = '\0';
                return MakeFailure($"invalid escape sequence '\\u{Character(0)}{Character(1)}{Character(2)}{Character(3)}'\n");
            case 'n':
                TryMoveNext();
                escaped = '\n';
                return fsResult.Success;
            case 'f':
                TryMoveNext();
                escaped = '\f';
                return fsResult.Success;
            default:
                escaped = '\0';
                return MakeFailure($"Invalid escape sequence \\{Character()}");
            case 'b':
                TryMoveNext();
                escaped = '\b';
                return fsResult.Success;
            case 'a':
                TryMoveNext();
                escaped = '\a';
                return fsResult.Success;
            }
        }

        private fsResult TryParseExact(string content)
        {
            for (int i = 0; i < content.Length; i++)
            {
                if (Character() == content[i])
                {
                    if (!TryMoveNext())
                    {
                        return MakeFailure("Unexpected end of content when parsing " + content);
                    }
                    continue;
                }
                return MakeFailure("Expected " + content[i]);
            }
            return fsResult.Success;
        }

        private fsResult TryParseTrue(out fsData data)
        {
            fsResult result = TryParseExact("true");
            if (!result.Succeeded)
            {
                data = null;
                return result;
            }
            data = new fsData(boolean: true);
            return fsResult.Success;
        }

        private fsResult TryParseFalse(out fsData data)
        {
            fsResult result = TryParseExact("false");
            if (result.Succeeded)
            {
                data = new fsData(boolean: false);
                return fsResult.Success;
            }
            data = null;
            return result;
        }

        private fsResult TryParseNull(out fsData data)
        {
            fsResult result = TryParseExact("null");
            if (!result.Succeeded)
            {
                data = null;
                return result;
            }
            data = new fsData();
            return fsResult.Success;
        }

        private bool IsSeparator(char c)
        {
            if (!char.IsWhiteSpace(c) && c != ',' && c != '}')
            {
                return c == ']';
            }
            return true;
        }

        private fsResult TryParseNumber(out fsData data)
        {
            int start = _start;
            while (TryMoveNext() && HasValue() && !IsSeparator(Character()))
            {
            }
            string text = _input.Substring(start, _start - start);
            if (!text.Contains(".") && !text.Contains("e") && !text.Contains("E"))
            {
                switch (text)
                {
                default:
                {
                    if (!long.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                    {
                        data = null;
                        return MakeFailure("Bad Int64 format with " + text);
                    }
                    data = new fsData(result);
                    return fsResult.Success;
                }
                case "Infinity":
                case "-Infinity":
                case "NaN":
                    break;
                }
            }
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var result2))
            {
                data = new fsData(result2);
                return fsResult.Success;
            }
            data = null;
            return MakeFailure("Bad double format with " + text);
        }

        private fsResult TryParseString(out string str)
        {
            _cachedStringBuilder.Length = 0;
            if (Character() == '"' && TryMoveNext())
            {
                while (HasValue() && Character() != '"')
                {
                    char c = Character();
                    if (c == '\\')
                    {
                        char escaped;
                        fsResult result = TryUnescapeChar(out escaped);
                        if (result.Failed)
                        {
                            str = string.Empty;
                            return result;
                        }
                        _cachedStringBuilder.Append(escaped);
                    }
                    else
                    {
                        _cachedStringBuilder.Append(c);
                        if (!TryMoveNext())
                        {
                            str = string.Empty;
                            return MakeFailure("Unexpected end of input when reading a string");
                        }
                    }
                }
                if (HasValue() && Character() == '"' && TryMoveNext())
                {
                    str = _cachedStringBuilder.ToString();
                    return fsResult.Success;
                }
                str = string.Empty;
                return MakeFailure("No closing \" when parsing a string");
            }
            str = string.Empty;
            return MakeFailure("Expected initial \" when parsing a string");
        }

        private fsResult TryParseArray(out fsData arr)
        {
            if (Character() != '[')
            {
                arr = null;
                return MakeFailure("Expected initial [ when parsing an array");
            }
            if (TryMoveNext())
            {
                SkipSpace();
                List<fsData> list = new List<fsData>();
                while (HasValue() && Character() != ']')
                {
                    fsData data;
                    fsResult result = RunParse(out data);
                    if (!result.Failed)
                    {
                        list.Add(data);
                        SkipSpace();
                        if (HasValue() && Character() == ',')
                        {
                            if (!TryMoveNext())
                            {
                                break;
                            }
                            SkipSpace();
                        }
                        continue;
                    }
                    arr = null;
                    return result;
                }
                if (HasValue() && Character() == ']' && TryMoveNext())
                {
                    arr = new fsData(list);
                    return fsResult.Success;
                }
                arr = null;
                return MakeFailure("No closing ] for array");
            }
            arr = null;
            return MakeFailure("Unexpected end of input when parsing an array");
        }

        private fsResult TryParseObject(out fsData obj)
        {
            if (Character() != '{')
            {
                obj = null;
                return MakeFailure("Expected initial { when parsing an object");
            }
            if (!TryMoveNext())
            {
                obj = null;
                return MakeFailure("Unexpected end of input when parsing an object");
            }
            SkipSpace();
            Dictionary<string, fsData> dictionary = new Dictionary<string, fsData>((!fsGlobalConfig.IsCaseSensitive) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            fsResult result;
            while (true)
            {
                if (HasValue() && Character() != '}')
                {
                    SkipSpace();
                    result = TryParseString(out var str);
                    if (result.Failed)
                    {
                        break;
                    }
                    SkipSpace();
                    if (!HasValue() || Character() != ':' || !TryMoveNext())
                    {
                        obj = null;
                        return MakeFailure("Expected : after key \"" + str + "\"");
                    }
                    SkipSpace();
                    result = RunParse(out var data);
                    if (result.Failed)
                    {
                        obj = null;
                        return result;
                    }
                    dictionary.Add(str, data);
                    SkipSpace();
                    if (!HasValue() || Character() != ',')
                    {
                        continue;
                    }
                    if (TryMoveNext())
                    {
                        SkipSpace();
                        continue;
                    }
                }
                if (HasValue() && Character() == '}' && TryMoveNext())
                {
                    obj = new fsData(dictionary);
                    return fsResult.Success;
                }
                obj = null;
                return MakeFailure("No closing } for object");
            }
            obj = null;
            return result;
        }

        private fsResult RunParse(out fsData data)
        {
            SkipSpace();
            if (HasValue())
            {
                switch (Character())
                {
                case 'n':
                    return TryParseNull(out data);
                case 'f':
                    return TryParseFalse(out data);
                case '{':
                    return TryParseObject(out data);
                case 't':
                    return TryParseTrue(out data);
                case '"':
                {
                    string str;
                    fsResult result = TryParseString(out str);
                    if (!result.Failed)
                    {
                        data = new fsData(str);
                        return fsResult.Success;
                    }
                    data = null;
                    return result;
                }
                default:
                    data = null;
                    return MakeFailure("unable to parse; invalid token \"" + Character() + "\"");
                case '[':
                    return TryParseArray(out data);
                case '+':
                case '-':
                case '.':
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                case 'I':
                case 'N':
                    return TryParseNumber(out data);
                }
            }
            data = null;
            return MakeFailure("Unexpected end of input");
        }

        public static fsResult Parse(string input, out fsData data)
        {
            if (string.IsNullOrEmpty(input))
            {
                data = null;
                return fsResult.Fail("No input");
            }
            return new fsJsonParser(input).RunParse(out data);
        }

        public static fsData Parse(string input)
        {
            Parse(input, out var data).AssertSuccess();
            return data;
        }

        private fsJsonParser(string input)
        {
            _input = input;
            _start = 0;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static fsJsonParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
