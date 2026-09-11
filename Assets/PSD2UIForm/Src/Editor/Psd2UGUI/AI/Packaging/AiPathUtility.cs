using System;
using System.IO;
using Psd2UIFormPluginPathResolverNamespace;

namespace AiPathUtilityNamespace
{
    internal sealed class AiPathUtility
    {
        internal static AiPathUtility s_ObfuscationSentinel;

        internal static string ResolvePath(object path, object path2)
        {
            if (string.IsNullOrWhiteSpace((string)path2))
            {
                return string.Empty;
            }
            if (!Path.IsPathRooted((string)path2))
            {
                return Path.Combine((string)path, (string)path2);
            }
            return (string)path2;
        }

        internal static string ResolvePluginRelativePath(object value, object path)
        {
            if (string.IsNullOrWhiteSpace((string)path))
            {
                return string.Empty;
            }
            string text = GetPluginAssetRoot();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return ResolvePath(value, CombineAssetPath(text, path));
            }
            return string.Empty;
        }

        internal static string BuildPluginRelativePath(object text)
        {
            if (!string.IsNullOrWhiteSpace((string)text))
            {
                return "PSD2UIForm/" + ((string)text).Replace("\\", "/").TrimStart('/');
            }
            return "PSD2UIForm";
        }

        internal static string SanitizePsdName(object name)
        {
            string text = Path.GetFileName(string.IsNullOrWhiteSpace((string)name) ? string.Empty : ((string)name).Trim().Replace("\\", "/"));
            if (string.IsNullOrWhiteSpace(text))
            {
                text = "psd";
            }
            char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
            char[] array = text.ToCharArray();
            for (int i = 0; i < array.Length; i++)
            {
                char c = array[i];
                if (c <= '\u001f' || Array.IndexOf(invalidFileNameChars, c) >= 0)
                {
                    array[i] = '_';
                }
            }
            string text2 = new string(array).Trim();
            if (!string.IsNullOrWhiteSpace(text2))
            {
                return text2;
            }
            return "psd";
        }

        internal static string GetPluginAssetRoot()
        {
            return Psd2UIFormPluginPathResolver.GetPluginAssetRoot();
        }

        private static string CombineAssetPath(object value, object value2)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                if (!string.IsNullOrWhiteSpace((string)value2))
                {
                    return ((string)value).TrimEnd(new char[2] { '/', '\\' }) + "/" + ((string)value2).Replace("\\", "/").TrimStart('/');
                }
                return (string)(value ?? string.Empty);
            }
            return (string)(value2 ?? string.Empty);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiPathUtility GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
