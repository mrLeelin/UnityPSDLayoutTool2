using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace PsdLicensing
{

internal static class LicenseStoragePaths
{
	private static string _pluginAssetDirectory;

	private static string _pluginPhysicalDirectory;

	[SpecialName]
	internal static string GetProtectorDataDirectory()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eFunStudioProtector");
	}

	[SpecialName]
	internal static string GetLicenseCacheDirectory()
	{
		return Path.Combine(GetProtectorDataDirectory(), "Licenses", "psd2ugui");
	}

	[SpecialName]
	internal static string GetLicenseCachePath()
	{
		return Path.Combine(GetLicenseCacheDirectory(), "license.cache.bin");
	}

	[SpecialName]
	internal static string GetProjectRootDirectory()
	{
		return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
	}

	[SpecialName]
	internal static string GetPluginAssetDirectory()
	{
		return _pluginAssetDirectory ?? (_pluginAssetDirectory = FindPluginAssetDirectory());
	}

	[SpecialName]
	internal static string GetProjectLicenseAssetPath()
	{
		return GetPluginAssetDirectory() + "/psd2ugui.project-license";
	}

	[SpecialName]
	internal static string GetPluginPhysicalDirectory()
	{
		return _pluginPhysicalDirectory ?? (_pluginPhysicalDirectory = ResolveAssetPhysicalPath(GetPluginAssetDirectory()));
	}

	[SpecialName]
	internal static string GetProjectLicensePhysicalPath()
	{
		return Path.Combine(GetPluginPhysicalDirectory(), "psd2ugui.project-license");
	}

	private static string FindPluginAssetDirectory()
	{
		string text = FindPluginRootFromAssetName("PSDReader.asmdef");
		if (string.IsNullOrWhiteSpace(text))
		{
			text = FindPluginRootFromAssetName("PSDReader.dll");
			if (string.IsNullOrWhiteSpace(text))
			{
				if (AssetDatabase.IsValidFolder("Assets/Plugins/PSD2UIForm"))
				{
					return "Assets/Plugins/PSD2UIForm";
				}
				return "Assets";
			}
			return text;
		}
		return text;
	}

	private static string FindPluginRootFromAssetName(object P_0)
	{
		if (!string.IsNullOrWhiteSpace((string)P_0))
		{
			string[] array = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension((string)P_0));
			int num = 0;
			string text2;
			while (true)
			{
				if (num < array.Length)
				{
					string text = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(array[num]));
					if (string.Equals(Path.GetFileName(text), (string)P_0, StringComparison.OrdinalIgnoreCase))
					{
						text2 = FindPluginRootAncestor(text);
						if (!string.IsNullOrWhiteSpace(text2))
						{
							break;
						}
					}
					num++;
					continue;
				}
				return string.Empty;
			}
			return text2;
		}
		return string.Empty;
	}

	private static string FindPluginRootAncestor(object P_0)
	{
		string text = NormalizeAssetPath(Path.GetDirectoryName((string)P_0));
		while (true)
		{
			if (!string.IsNullOrWhiteSpace(text))
			{
				if (string.Equals(Path.GetFileName(text), "PSD2UIForm", StringComparison.OrdinalIgnoreCase))
				{
					break;
				}
				string text2 = NormalizeAssetPath(Path.GetDirectoryName(text));
				if (!string.IsNullOrWhiteSpace(text2) && !string.Equals(text2, text, StringComparison.OrdinalIgnoreCase))
				{
					text = text2;
					continue;
				}
			}
			return string.Empty;
		}
		return text;
	}

	private static string ResolveAssetPhysicalPath(object P_0)
	{
		string text = NormalizeAssetPath(P_0);
		if (!string.IsNullOrWhiteSpace(text))
		{
			if (!Path.IsPathRooted(text))
			{
				if (text.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
				{
					UnityEditor.PackageManager.PackageInfo packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(text);
					if (packageInfo != null && !string.IsNullOrWhiteSpace(packageInfo.resolvedPath))
					{
						string text2 = NormalizeAssetPath(packageInfo.assetPath);
						string text3 = ((text.Length > text2.Length) ? text.Substring(text2.Length).TrimStart('/') : string.Empty);
						if (string.IsNullOrWhiteSpace(text3))
						{
							return packageInfo.resolvedPath;
						}
						return Path.GetFullPath(Path.Combine(packageInfo.resolvedPath, text3.Replace('/', Path.DirectorySeparatorChar)));
					}
				}
				return Path.GetFullPath(Path.Combine(GetProjectRootDirectory(), text.Replace('/', Path.DirectorySeparatorChar)));
			}
			return text;
		}
		return GetProjectRootDirectory();
	}

	private static string NormalizeAssetPath(object P_0)
	{
		if (string.IsNullOrWhiteSpace((string)P_0))
		{
			return string.Empty;
		}
		return ((string)P_0).Replace('\\', '/').Trim();
	}
}
}
