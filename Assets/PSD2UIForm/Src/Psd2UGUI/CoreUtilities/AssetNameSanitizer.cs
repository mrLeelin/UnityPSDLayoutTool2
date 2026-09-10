using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AssetNameSanitizerNamespace
{
    internal sealed class AssetNameSanitizer
    {
        private static readonly Regex s_RepeatedUnderscoreRegex;

        private static readonly HashSet<string> s_ReservedWindowsNames;

        private static readonly char[] s_InvalidFileNameChars;

        private static readonly Encoding s_ChineseEncoding;

        private static readonly int[] s_PinyinCodeThresholds;

        private static readonly string[] s_PinyinSyllables;

        private static AssetNameSanitizer s_ObfuscationSentinel;

        static AssetNameSanitizer()
        {
            s_RepeatedUnderscoreRegex = new Regex("_+", RegexOptions.Compiled);
            s_ReservedWindowsNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6",
                "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7",
                "LPT8", "LPT9"
            };
            s_InvalidFileNameChars = Path.GetInvalidFileNameChars();
            s_PinyinCodeThresholds = new int[396]
            {
                -20319, -20317, -20304, -20295, -20292, -20283, -20265, -20257, -20242, -20230,
                -20051, -20036, -20032, -20026, -20002, -19990, -19986, -19982, -19976, -19805,
                -19784, -19775, -19774, -19763, -19756, -19751, -19746, -19741, -19739, -19728,
                -19725, -19715, -19540, -19531, -19525, -19515, -19500, -19484, -19479, -19467,
                -19289, -19288, -19281, -19275, -19270, -19263, -19261, -19249, -19243, -19242,
                -19238, -19235, -19227, -19224, -19218, -19212, -19038, -19023, -19018, -19006,
                -19003, -18996, -18977, -18961, -18952, -18783, -18774, -18773, -18763, -18756,
                -18741, -18735, -18731, -18722, -18710, -18697, -18696, -18526, -18518, -18501,
                -18490, -18478, -18463, -18448, -18447, -18446, -18239, -18237, -18231, -18220,
                -18211, -18201, -18184, -18183, -18181, -18012, -17997, -17988, -17970, -17964,
                -17961, -17950, -17947, -17931, -17928, -17922, -17759, -17752, -17733, -17730,
                -17721, -17703, -17701, -17697, -17692, -17683, -17676, -17496, -17487, -17482,
                -17468, -17454, -17433, -17427, -17417, -17202, -17185, -16983, -16970, -16942,
                -16915, -16733, -16708, -16706, -16689, -16664, -16657, -16647, -16474, -16470,
                -16465, -16459, -16452, -16448, -16433, -16429, -16427, -16423, -16419, -16412,
                -16407, -16403, -16401, -16393, -16220, -16216, -16212, -16205, -16202, -16187,
                -16180, -16171, -16169, -16158, -16155, -15959, -15958, -15944, -15933, -15920,
                -15915, -15903, -15889, -15878, -15707, -15701, -15681, -15667, -15661, -15659,
                -15652, -15640, -15631, -15625, -15454, -15448, -15436, -15435, -15419, -15416,
                -15408, -15394, -15385, -15377, -15375, -15369, -15363, -15362, -15183, -15180,
                -15165, -15158, -15153, -15150, -15149, -15144, -15143, -15141, -15140, -15139,
                -15128, -15121, -15119, -15117, -15110, -15109, -14941, -14937, -14933, -14930,
                -14929, -14928, -14926, -14922, -14921, -14914, -14908, -14902, -14894, -14889,
                -14882, -14873, -14871, -14857, -14678, -14674, -14670, -14668, -14663, -14654,
                -14645, -14630, -14594, -14429, -14407, -14399, -14384, -14379, -14368, -14355,
                -14353, -14345, -14170, -14159, -14151, -14149, -14145, -14140, -14137, -14135,
                -14125, -14123, -14122, -14112, -14109, -14099, -14097, -14094, -14092, -14090,
                -14087, -14083, -13917, -13914, -13910, -13907, -13906, -13905, -13896, -13894,
                -13878, -13870, -13859, -13847, -13831, -13658, -13611, -13601, -13406, -13404,
                -13400, -13398, -13395, -13391, -13387, -13383, -13367, -13359, -13356, -13343,
                -13340, -13329, -13326, -13318, -13147, -13138, -13120, -13107, -13096, -13095,
                -13091, -13076, -13068, -13063, -13060, -12888, -12875, -12871, -12860, -12858,
                -12852, -12849, -12838, -12831, -12829, -12812, -12802, -12607, -12597, -12594,
                -12585, -12556, -12359, -12346, -12320, -12300, -12120, -12099, -12089, -12074,
                -12067, -12058, -12039, -11867, -11861, -11847, -11831, -11798, -11781, -11604,
                -11589, -11536, -11358, -11340, -11339, -11324, -11303, -11097, -11077, -11067,
                -11055, -11052, -11045, -11041, -11038, -11024, -11020, -11019, -11018, -11014,
                -10838, -10832, -10815, -10800, -10790, -10780, -10764, -10587, -10544, -10533,
                -10519, -10331, -10329, -10328, -10322, -10315, -10309, -10307, -10296, -10281,
                -10274, -10270, -10262, -10260, -10256, -10254
            };
            s_PinyinSyllables = new string[396]
            {
                "a", "ai", "an", "ang", "ao", "ba", "bai", "ban", "bang", "bao",
                "bei", "ben", "beng", "bi", "bian", "biao", "bie", "bin", "bing", "bo",
                "bu", "ca", "cai", "can", "cang", "cao", "ce", "ceng", "cha", "chai",
                "chan", "chang", "chao", "che", "chen", "cheng", "chi", "chong", "chou", "chu",
                "chuai", "chuan", "chuang", "chui", "chun", "chuo", "ci", "cong", "cou", "cu",
                "cuan", "cui", "cun", "cuo", "da", "dai", "dan", "dang", "dao", "de",
                "deng", "di", "dian", "diao", "die", "ding", "diu", "dong", "dou", "du",
                "duan", "dui", "dun", "duo", "e", "en", "er", "fa", "fan", "fang",
                "fei", "fen", "feng", "fo", "fou", "fu", "ga", "gai", "gan", "gang",
                "gao", "ge", "gei", "gen", "geng", "gong", "gou", "gu", "gua", "guai",
                "guan", "guang", "gui", "gun", "guo", "ha", "hai", "han", "hang", "hao",
                "he", "hei", "hen", "heng", "hong", "hou", "hu", "hua", "huai", "huan",
                "huang", "hui", "hun", "huo", "ji", "jia", "jian", "jiang", "jiao", "jie",
                "jin", "jing", "jiong", "jiu", "ju", "juan", "jue", "jun", "ka", "kai",
                "kan", "kang", "kao", "ke", "ken", "keng", "kong", "kou", "ku", "kua",
                "kuai", "kuan", "kuang", "kui", "kun", "kuo", "la", "lai", "lan", "lang",
                "lao", "le", "lei", "leng", "li", "lia", "lian", "liang", "liao", "lie",
                "lin", "ling", "liu", "long", "lou", "lu", "lv", "luan", "lue", "lun",
                "luo", "ma", "mai", "man", "mang", "mao", "me", "mei", "men", "meng",
                "mi", "mian", "miao", "mie", "min", "ming", "miu", "mo", "mou", "mu",
                "na", "nai", "nan", "nang", "nao", "ne", "nei", "nen", "neng", "ni",
                "nian", "niang", "niao", "nie", "nin", "ning", "niu", "nong", "nu", "nv",
                "nuan", "nue", "nuo", "o", "ou", "pa", "pai", "pan", "pang", "pao",
                "pei", "pen", "peng", "pi", "pian", "piao", "pie", "pin", "ping", "po",
                "pu", "qi", "qia", "qian", "qiang", "qiao", "qie", "qin", "qing", "qiong",
                "qiu", "qu", "quan", "que", "qun", "ran", "rang", "rao", "re", "ren",
                "reng", "ri", "rong", "rou", "ru", "ruan", "rui", "run", "ruo", "sa",
                "sai", "san", "sang", "sao", "se", "sen", "seng", "sha", "shai", "shan",
                "shang", "shao", "she", "shen", "sheng", "shi", "shou", "shu", "shua", "shuai",
                "shuan", "shuang", "shui", "shun", "shuo", "si", "song", "sou", "su", "suan",
                "sui", "sun", "suo", "ta", "tai", "tan", "tang", "tao", "te", "teng",
                "ti", "tian", "tiao", "tie", "ting", "tong", "tou", "tu", "tuan", "tui",
                "tun", "tuo", "wa", "wai", "wan", "wang", "wei", "wen", "weng", "wo",
                "wu", "xi", "xia", "xian", "xiang", "xiao", "xie", "xin", "xing", "xiong",
                "xiu", "xu", "xuan", "xue", "xun", "ya", "yan", "yang", "yao", "ye",
                "yi", "yin", "ying", "yo", "yong", "you", "yu", "yuan", "yue", "yun",
                "za", "zai", "zan", "zang", "zao", "ze", "zei", "zen", "zeng", "zha",
                "zhai", "zhan", "zhang", "zhao", "zhe", "zhen", "zheng", "zhi", "zhong", "zhou",
                "zhu", "zhua", "zhuai", "zhuan", "zhuang", "zhui", "zhun", "zhuo", "zi", "zong",
                "zou", "zu", "zuan", "zui", "zun", "zuo"
            };
            try
            {
                s_ChineseEncoding = Encoding.GetEncoding("GB2312");
            }
            catch
            {
                try
                {
                    s_ChineseEncoding = Encoding.GetEncoding(936);
                }
                catch
                {
                    s_ChineseEncoding = Encoding.UTF8;
                }
            }
        }

        internal static string TransliterateChinese(object text)
        {
            if (!string.IsNullOrEmpty((string)text))
            {
                StringBuilder stringBuilder = new StringBuilder(((string)text).Length * 2);
                for (int i = 0; i < ((string)text).Length; i++)
                {
                    char c = ((string)text)[i];
                    if (IsChineseCharacter(c))
                    {
                        stringBuilder.Append(GetPinyinInitial(c));
                    }
                    else
                    {
                        stringBuilder.Append(c);
                    }
                }
                return stringBuilder.ToString();
            }
            return string.Empty;
        }

        internal static string SanitizeReferenceName(object value)
        {
            return SanitizeNameCore(value, true);
        }

        internal static string SanitizeRelativePath(object path)
        {
            if (!string.IsNullOrWhiteSpace((string)path))
            {
                string text = ((string)path).Replace("\\", "/").Trim();
                if (string.IsNullOrEmpty(text))
                {
                    return string.Empty;
                }
                string[] array = text.Split(new char[1] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (array != null && array.Length != 0)
                {
                    List<string> list = new List<string>(array.Length);
                    string[] array2 = array;
                    for (int i = 0; i < array2.Length; i++)
                    {
                        string text2 = SanitizeNameCore(array2[i], false);
                        if (!string.IsNullOrWhiteSpace(text2))
                        {
                            list.Add(text2);
                        }
                    }
                    if (list.Count <= 0)
                    {
                        return string.Empty;
                    }
                    return string.Join("/", list);
                }
                return string.Empty;
            }
            return string.Empty;
        }

        internal static string SanitizeName(object value)
        {
            return SanitizeNameCore(value, false);
        }

        internal static string GetNormalizedFileName(object name)
        {
            if (!string.IsNullOrWhiteSpace((string)name))
            {
                string text = ((string)name).Replace("\\", "/").TrimEnd('/');
                if (string.IsNullOrEmpty(text))
                {
                    return string.Empty;
                }
                string fileName = Path.GetFileName(text);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    return text;
                }
                return fileName;
            }
            return string.Empty;
        }

        private static string SanitizeNameCore(object value, bool enabled)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return string.Empty;
            }
            string text = ((string)value).Trim();
            if (enabled)
            {
                if (string.IsNullOrEmpty("refp ") || !text.StartsWith("refp ", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty("ref ") && text.StartsWith("ref ", StringComparison.OrdinalIgnoreCase))
                    {
                        text = text.Substring("ref ".Length).TrimStart();
                    }
                }
                else
                {
                    text = text.Substring("refp ".Length).TrimStart();
                }
            }
            StringBuilder stringBuilder = new StringBuilder(text.Length);
            string text2 = text;
            foreach (char c in text2)
            {
                if (!IsSafeFileNameCharacter(c))
                {
                    if (!char.IsWhiteSpace(c) && !char.IsControl(c) && Array.IndexOf(s_InvalidFileNameChars, c) < 0)
                    {
                        stringBuilder.Append('_');
                    }
                    else
                    {
                        stringBuilder.Append('_');
                    }
                }
                else
                {
                    stringBuilder.Append(c);
                }
            }
            string text3 = s_RepeatedUnderscoreRegex.Replace(stringBuilder.ToString(), "_");
            text3 = text3.Trim('_', '.');
            if (text3.Length > 64)
            {
                text3 = text3.Substring(0, 64);
            }
            if (s_ReservedWindowsNames.Contains(text3))
            {
                text3 = "_" + text3;
            }
            return text3;
        }

        private static string GetPinyinInitial(char value)
        {
            int num;
            if (s_ChineseEncoding == null)
            {
                num = value;
                return num.ToString("x4");
            }
            byte[] bytes = s_ChineseEncoding.GetBytes(new char[1] { value });
            if (bytes.Length != 2)
            {
                return value.ToString();
            }
            int num2 = (bytes[0] << 8) | bytes[1];
            num2 -= 65536;
            if (num2 > 0 && num2 < 160)
            {
                return value.ToString();
            }
            int num3 = s_PinyinCodeThresholds.Length - 1;
            while (num3 >= 0)
            {
                if (num2 < s_PinyinCodeThresholds[num3])
                {
                    num3--;
                    continue;
                }
                return s_PinyinSyllables[num3];
            }
            num = value;
            return "u" + num.ToString("x4");
        }

        private static bool IsChineseCharacter(char value)
        {
            if (value < '一')
            {
                return false;
            }
            return value <= '鿿';
        }

        private static bool IsSafeFileNameCharacter(char value)
        {
            if (!char.IsLetterOrDigit(value) && value != '_' && value != '-')
            {
                return value == '.';
            }
            return true;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AssetNameSanitizer GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
