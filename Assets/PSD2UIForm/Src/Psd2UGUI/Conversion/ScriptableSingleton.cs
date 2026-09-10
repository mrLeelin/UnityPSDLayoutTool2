using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ScriptableSingletonPathAttributeNamespace;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

using Object = UnityEngine.Object;
namespace UGF.EditorTools.Psd2UGUI
{
    public abstract class ScriptableSingleton<T> : ScriptableObject where T : ScriptableObject
    {
        private static readonly Regex s_ScriptReferenceRegex = new Regex("^(\\s*m_Script:\\s*)\\{fileID:\\s*-?\\d+,\\s*guid:\\s*[0-9a-fA-F]+,\\s*type:\\s*\\d+\\}\\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex s_YamlFieldRegex = new Regex("^\\s{2}([A-Za-z_][A-Za-z0-9_]*):(?:\\s*(.*))?$", RegexOptions.Multiline | RegexOptions.Compiled);

        private static T s_Instance;

        private static object s_ObfuscationSentinel;

        internal static T Instance
        {
            get
            {
                if (!(bool)((Object)(object)s_Instance))
                {
                    LoadInstance();
                }
                return s_Instance;
            }
        }

        internal static T LoadInstance()
        {
            string filePath = GetFilePath();
            if (!string.IsNullOrEmpty(filePath))
            {
                bool flag = File.Exists(GetAbsolutePath(filePath));
                s_Instance = InternalEditorUtility.LoadSerializedFileAndForget(filePath).OfType<T>().FirstOrDefault();
                if (!(bool)((Object)(object)s_Instance))
                {
                    if (flag && TryLoadLegacyConfig(filePath, out var legacyInstance))
                    {
                        s_Instance = legacyInstance;
                        SaveInstance();
                        Debug.LogWarning((object)(typeof(T).Name + ": 检测到旧版配置反序列化失败，已自动迁移配置文件: " + filePath));
                    }
                    else
                    {
                        s_Instance = ScriptableObject.CreateInstance<T>();
                        if (flag)
                        {
                            SaveInstance();
                            Debug.LogWarning((object)(typeof(T).Name + ": 配置文件反序列化失败，已重建默认配置: " + filePath));
                        }
                    }
                }
            }
            else
            {
                Debug.LogError((object)"ScriptableSingleton: 请设置持久化存档路径！ ");
            }
            return s_Instance;
        }

        internal static void SaveInstance(bool enabled = true)
        {
            if (!(bool)((Object)(object)s_Instance))
            {
                return;
            }
            string filePath = GetFilePath();
            if (!string.IsNullOrEmpty(filePath))
            {
                string directoryName = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }
                Object[] array = (Object[])(object)new T[1] { s_Instance };
                InternalEditorUtility.SaveToSerializedFileAndForget(array, filePath, enabled);
            }
        }

        protected static string GetFilePath()
        {
            return typeof(T).GetCustomAttributes(inherit: true).Cast<ScriptableSingletonPathAttribute>().FirstOrDefault((ScriptableSingletonPathAttribute v) => v != null)?.RelativePath;
        }

        private static bool TryLoadLegacyConfig(string text2, out T result)
        {
            result = default(T);
            string text = GetAbsolutePath(text2);
            if (File.Exists(text))
            {
                if (TryLoadWithRepairedScriptReference(text, out result))
                {
                    return true;
                }
                return TryLoadFieldsFromYaml(text, out result);
            }
            return false;
        }

        private static bool TryLoadWithRepairedScriptReference(string text4, out T result)
        {
            result = default(T);
            if (!TryGetScriptReference(out var arg, out var num))
            {
                return false;
            }
            string text = File.ReadAllText(text4);
            if (!string.IsNullOrWhiteSpace(text))
            {
                Match match = s_ScriptReferenceRegex.Match(text);
                if (!match.Success)
                {
                    return false;
                }
                string text2 = $"{match.Groups[1].Value}{{fileID: {num}, guid: {arg}, type: 3}}";
                string contents = text.Substring(0, match.Index) + text2 + text.Substring(match.Index + match.Length);
                string text3 = Path.Combine(Path.GetTempPath(), $"{typeof(T).Name}_{Guid.NewGuid():N}.asset");
                try
                {
                    File.WriteAllText(text3, contents);
                    Object[] source = InternalEditorUtility.LoadSerializedFileAndForget(text3);
                    result = source.OfType<T>().FirstOrDefault();
                    return (Object)(object)result != (Object)null;
                }
                catch
                {
                    result = default(T);
                    return false;
                }
                finally
                {
                    if (File.Exists(text3))
                    {
                        File.Delete(text3);
                    }
                }
            }
            return false;
        }

        private static bool TryLoadFieldsFromYaml(string text, out T result)
        {
            result = ScriptableObject.CreateInstance<T>();
            try
            {
                Dictionary<string, string> dictionary = ParseYamlFields(File.ReadAllText(text));
                bool flag = false;
                foreach (FieldInfo item in GetSerializableFields())
                {
                    if (dictionary.TryGetValue(item.Name, out var value) && TryParseSerializedValue(value, item.FieldType, out var value2))
                    {
                        item.SetValue(result, value2);
                        flag = true;
                    }
                }
                if (flag)
                {
                    return true;
                }
                Object.DestroyImmediate((Object)(object)result);
                result = default(T);
                return false;
            }
            catch
            {
                if ((Object)(object)result != (Object)null)
                {
                    Object.DestroyImmediate((Object)(object)result);
                }
                result = default(T);
                return false;
            }
        }

        private static bool TryGetScriptReference(out string result, out long result2)
        {
            result = null;
            result2 = 0L;
            T val = ScriptableObject.CreateInstance<T>();
            try
            {
                MonoScript val2 = MonoScript.FromScriptableObject((ScriptableObject)(object)val);
                return (bool)((Object)(object)val2) && AssetDatabase.TryGetGUIDAndLocalFileIdentifier((Object)(object)val2, out result, out result2);
            }
            finally
            {
                Object.DestroyImmediate((Object)(object)val);
            }
        }

        private static Dictionary<string, string> ParseYamlFields(string text)
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match item in s_YamlFieldRegex.Matches(text))
            {
                dictionary[item.Groups[1].Value] = item.Groups[2].Value;
            }
            return dictionary;
        }

        private static IEnumerable<FieldInfo> GetSerializableFields()
        {
            return from field in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                where !field.IsStatic && !field.IsInitOnly && !field.IsLiteral && !field.IsNotSerialized && (field.IsPublic || ((MemberInfo)field).GetCustomAttribute<SerializeField>() != null)
                select field;
        }

        private static bool TryParseSerializedValue(string text2, Type type, out object result7)
        {
            string text = ((text2 == null) ? string.Empty : text2.Trim());
            if (!(type == typeof(string)))
            {
                if (!(type == typeof(bool)))
                {
                    long result5;
                    if (type == typeof(int))
                    {
                        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                        {
                            result7 = result;
                            return true;
                        }
                    }
                    else if (!(type == typeof(long)))
                    {
                        if (type == typeof(float))
                        {
                            if (float.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result2))
                            {
                                result7 = result2;
                                return true;
                            }
                        }
                        else if (type == typeof(double))
                        {
                            if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result3))
                            {
                                result7 = result3;
                                return true;
                            }
                        }
                        else if (type.IsEnum)
                        {
                            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result4))
                            {
                                result7 = Enum.ToObject(type, result4);
                                return true;
                            }
                            string value = DecodeYamlScalar(text);
                            if (!string.IsNullOrEmpty(value))
                            {
                                try
                                {
                                    result7 = Enum.Parse(type, value, ignoreCase: true);
                                    return true;
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                    else if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result5))
                    {
                        result7 = result5;
                        return true;
                    }
                }
                else
                {
                    if (text == "1")
                    {
                        result7 = true;
                        return true;
                    }
                    if (text == "0")
                    {
                        result7 = false;
                        return true;
                    }
                    if (bool.TryParse(text, out var result6))
                    {
                        result7 = result6;
                        return true;
                    }
                }
                result7 = null;
                return false;
            }
            result7 = DecodeYamlScalar(text);
            return true;
        }

        private static string DecodeYamlScalar(string text)
        {
            if (!string.IsNullOrEmpty(text) && !(text == "null") && !(text == "~"))
            {
                if (text.Length >= 2)
                {
                    if (text[0] == '"' && text[text.Length - 1] == '"')
                    {
                        return text.Substring(1, text.Length - 2).Replace("\\\\", "\\").Replace("\\\"", "\"")
                            .Replace("\\n", "\n")
                            .Replace("\\r", "\r")
                            .Replace("\\t", "\t");
                    }
                    if (text[0] == '\'' && text[text.Length - 1] == '\'')
                    {
                        return text.Substring(1, text.Length - 2).Replace("''", "'");
                    }
                }
                return text;
            }
            if (!(text == string.Empty))
            {
                return null;
            }
            return string.Empty;
        }

        private static string GetAbsolutePath(string text)
        {
            if (!Path.IsPathRooted(text))
            {
                return Path.GetFullPath(text);
            }
            return text;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static object GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
