using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.PackageManager;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;

using Object = UnityEngine.Object;
namespace LicensePathProviderNamespace
{
    internal sealed class LicensePathProvider
    {
        private static string _cachedPluginAssetRoot;

        private static string _cachedPluginAbsoluteDirectory;

        private static LicensePathProvider s_ObfuscationSentinel;

        [SpecialName]
        internal static string GetUserDataRoot()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eFunStudioProtector");
        }

        [SpecialName]
        internal static string GetLicenseCacheDirectory()
        {
            return Path.Combine(GetUserDataRoot(), "Licenses", "psd2ugui");
        }

        [SpecialName]
        internal static string GetLicenseCacheFilePath()
        {
            return Path.Combine(GetLicenseCacheDirectory(), "license.cache.bin");
        }

        [SpecialName]
        internal static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        [SpecialName]
        internal static string GetPluginAssetRoot()
        {
            return _cachedPluginAssetRoot ?? (_cachedPluginAssetRoot = ResolvePluginAssetRoot());
        }

        [SpecialName]
        internal static string GetProjectLicenseAssetPath()
        {
            return GetPluginAssetRoot() + "/psd2ugui.project-license";
        }

        [SpecialName]
        internal static string GetPluginAbsoluteDirectory()
        {
            return _cachedPluginAbsoluteDirectory ?? (_cachedPluginAbsoluteDirectory = ConvertAssetPathToAbsolutePath(GetPluginAssetRoot()));
        }

        [SpecialName]
        internal static string GetProjectLicenseAbsolutePath()
        {
            return Path.Combine(GetPluginAbsoluteDirectory(), "psd2ugui.project-license");
        }

        private static string ResolvePluginAssetRoot()
        {
            string text = FindAssetRootByFileName("PSDReader.asmdef");
            if (string.IsNullOrWhiteSpace(text))
            {
                text = FindPluginRootFromDll();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
                text = FindAssetRootByFileName("PSDReader.dll");
                if (string.IsNullOrWhiteSpace(text))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/Plugins/PSD2UIForm"))
                    {
                        return "Assets";
                    }
                    return "Assets/Plugins/PSD2UIForm";
                }
                return text;
            }
            return text;
        }

        private static string FindPluginRootFromDll()
        {
            string[] array = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension("cn.efunstudio.psd2ugui.dll"));
            int num = 0;
            string text;
            while (true)
            {
                if (num < array.Length)
                {
                    string path = NormalizePath(AssetDatabase.GUIDToAssetPath(array[num]));
                    if (string.Equals(Path.GetFileName(path), "cn.efunstudio.psd2ugui.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        text = NormalizePath(Path.GetDirectoryName(NormalizePath(Path.GetDirectoryName(path))));
                        if (IsPluginRootFolder(text))
                        {
                            break;
                        }
                    }
                    num++;
                    continue;
                }
                return string.Empty;
            }
            return text;
        }

        private static string FindAssetRootByFileName(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return string.Empty;
            }
            string[] array = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension((string)value));
            for (int i = 0; i < array.Length; i++)
            {
                string text = NormalizePath(AssetDatabase.GUIDToAssetPath(array[i]));
                if (string.Equals(Path.GetFileName(text), (string)value, StringComparison.OrdinalIgnoreCase))
                {
                    string text2 = FindContainingPluginDirectory(text);
                    if (!string.IsNullOrWhiteSpace(text2))
                    {
                        return text2;
                    }
                }
            }
            return string.Empty;
        }

        private static bool IsPluginRootFolder(object value)
        {
            string text = NormalizePath(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            if (!AssetDatabase.IsValidFolder(text + "/PSDReader") && !AssetDatabase.IsValidFolder(text + "/Scripts"))
            {
                return AssetDatabase.IsValidFolder(text + "/AIPrompts");
            }
            return true;
        }

        private static string FindContainingPluginDirectory(object value)
        {
            string text = NormalizePath(Path.GetDirectoryName((string)value));
            while (!string.IsNullOrWhiteSpace(text))
            {
                if (!string.Equals(Path.GetFileName(text), "PSD2UIForm", StringComparison.OrdinalIgnoreCase))
                {
                    string text2 = NormalizePath(Path.GetDirectoryName(text));
                    if (string.IsNullOrWhiteSpace(text2) || string.Equals(text2, text, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                    text = text2;
                    continue;
                }
                return text;
            }
            return string.Empty;
        }

        private static string ConvertAssetPathToAbsolutePath(object value)
        {
            string text = NormalizePath(value);
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (!Path.IsPathRooted(text))
                {
                    if (text.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
                    {
                        PackageInfo val = PackageInfo.FindForAssetPath(text);
                        if (val != null && !string.IsNullOrWhiteSpace(val.resolvedPath))
                        {
                            string text2 = NormalizePath(val.assetPath);
                            string text3 = ((text.Length <= text2.Length) ? string.Empty : text.Substring(text2.Length).TrimStart('/'));
                            if (!string.IsNullOrWhiteSpace(text3))
                            {
                                return Path.GetFullPath(Path.Combine(val.resolvedPath, text3.Replace('/', Path.DirectorySeparatorChar)));
                            }
                            return val.resolvedPath;
                        }
                    }
                    return Path.GetFullPath(Path.Combine(GetProjectRoot(), text.Replace('/', Path.DirectorySeparatorChar)));
                }
                return text;
            }
            return GetProjectRoot();
        }

        private static string NormalizePath(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return string.Empty;
            }
            return ((string)value).Replace('\\', '/').Trim();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LicensePathProvider GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
