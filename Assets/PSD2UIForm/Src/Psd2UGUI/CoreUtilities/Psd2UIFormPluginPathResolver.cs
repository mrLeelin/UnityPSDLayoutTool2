using System;
using System.IO;
using UGF.EditorTools.Psd2UGUI;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;
namespace Psd2UIFormPluginPathResolverNamespace
{
    internal sealed class Psd2UIFormPluginPathResolver
    {
        private static string s_PluginAssetRoot;

        private static string s_ProjectRoot;

        private static Psd2UIFormPluginPathResolver s_ObfuscationSentinel;

        internal static string GetPluginAssetRoot()
        {
            if (string.IsNullOrWhiteSpace(s_PluginAssetRoot))
            {
                if (!string.Equals(typeof(UGUIParser).Assembly.GetName().Name, "cn.efunstudio.psd2ugui", StringComparison.Ordinal) || !TryResolveFromScript(out s_PluginAssetRoot))
                {
                    if (TryResolveFromAssembly(out s_PluginAssetRoot))
                    {
                        return s_PluginAssetRoot;
                    }
                    return string.Empty;
                }
                return s_PluginAssetRoot;
            }
            return s_PluginAssetRoot;
        }

        private static bool TryResolveFromScript(out string result)
        {
            result = string.Empty;
            UGUIParser uGUIParser = ScriptableObject.CreateInstance<UGUIParser>();
            try
            {
                MonoScript val = MonoScript.FromScriptableObject((ScriptableObject)(object)uGUIParser);
                string text = ((!((Object)(object)val != (Object)null)) ? string.Empty : NormalizeAssetPath(AssetDatabase.GetAssetPath((Object)(object)val)));
                int num = text.IndexOf("/Scripts/", StringComparison.OrdinalIgnoreCase);
                if (num < 0)
                {
                    num = text.IndexOf("/Src/", StringComparison.OrdinalIgnoreCase);
                }
                if (num > 0)
                {
                    string text2 = text.Substring(0, num);
                    if (IsValidPluginRoot(text2))
                    {
                        result = text2;
                        return true;
                    }
                    return false;
                }
                return false;
            }
            finally
            {
                Object.DestroyImmediate((Object)(object)uGUIParser);
            }
        }

        private static bool TryResolveFromAssembly(out string result)
        {
            result = string.Empty;
            string[] array = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension("cn.efunstudio.psd2ugui.dll"));
            int num = 0;
            string text;
            while (true)
            {
                if (num < array.Length)
                {
                    string path = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(array[num]));
                    if (string.Equals(Path.GetFileName(path), "cn.efunstudio.psd2ugui.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        text = NormalizeAssetPath(Path.GetDirectoryName(NormalizeAssetPath(Path.GetDirectoryName(path))));
                        if (IsValidPluginRoot(text))
                        {
                            break;
                        }
                    }
                    num++;
                    continue;
                }
                return false;
            }
            result = text;
            return true;
        }

        private static bool IsValidPluginRoot(object value)
        {
            string text = NormalizeAssetPath(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            if (!AssetDatabase.IsValidFolder(text + "/AIPrompts") && !AssetDatabase.IsValidFolder(text + "/Scripts"))
            {
                return AssetDatabase.IsValidFolder(text + "/PSDReader");
            }
            return true;
        }

        private static string NormalizeAssetPath(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                return ((string)value).Replace("\\", "/").Trim();
            }
            return string.Empty;
        }

        internal static string CombinePluginAssetPath(object path)
        {
            string text = GetPluginAssetRoot();
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }
            if (!string.IsNullOrWhiteSpace((string)path))
            {
                return text.TrimEnd('/', '\\') + "/" + ((string)path).Replace("\\", "/").TrimStart('/');
            }
            return text;
        }

        internal static string GetPluginAbsolutePath(object value)
        {
            return AssetPathToAbsolutePath(CombinePluginAssetPath(value));
        }

        internal static string AssetPathToAbsolutePath(object path)
        {
            if (string.IsNullOrWhiteSpace((string)path))
            {
                return string.Empty;
            }
            string text = GetProjectRoot();
            if (string.IsNullOrWhiteSpace(text))
            {
                return (string)path;
            }
            return Path.GetFullPath(Path.Combine(text, ((string)path).Replace('/', Path.DirectorySeparatorChar)));
        }

        internal static string GetProjectRoot()
        {
            if (!string.IsNullOrWhiteSpace(s_ProjectRoot))
            {
                return s_ProjectRoot;
            }
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            object obj;
            if (parent != null)
            {
                obj = parent.FullName;
                if (obj != null)
                {
                    goto IL_0031;
                }
            }
            else
            {
                obj = null;
            }
            obj = string.Empty;
            goto IL_0031;
            IL_0031:
            s_ProjectRoot = (string)obj;
            return s_ProjectRoot;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static Psd2UIFormPluginPathResolver GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
