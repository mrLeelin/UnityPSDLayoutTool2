using System;
using System.Collections.Generic;
using PsdLicensing;
using PsdProtectionGuards;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader
{

internal static class PsdTextureAssetUtility
{
	private readonly struct PlatformSpec
	{
		internal static readonly PlatformSpec Default;

		internal readonly string Name;

		internal readonly BuildTargetGroup Group;

		internal readonly BuildTarget Target;

		internal readonly bool IsDefaultPlatform;

		internal static object Vqiq9YZvjuJOYbshp6mJ;

		internal PlatformSpec(string name)
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			Name = name;
			Group = BuildTargetGroup.Unknown;
			Target = BuildTarget.NoTarget;
			IsDefaultPlatform = true;
		}

		internal PlatformSpec(string name, BuildTargetGroup group, BuildTarget target)
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			Name = name;
			Group = group;
			Target = target;
			IsDefaultPlatform = false;
		}

		static PlatformSpec()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			Default = new PlatformSpec("DefaultTexturePlatform");
		}

		internal static bool BRwQk5ZvYN29NAy1iZmO()
		{
			return Vqiq9YZvjuJOYbshp6mJ == null;
		}

		internal static object e9MVjnZvtHN5oZm3uCJe()
		{
			return Vqiq9YZvjuJOYbshp6mJ;
		}
	}

	private const string ExportModeKey = "Psd2UIExportMode";

	private static readonly PlatformSpec[] ManagedPlatforms;

	internal static byte[] EncodePng(Texture2D texture)
	{
		if (!(texture == null))
		{
			if (texture.format != TextureFormat.RGBA64)
			{
				return texture.EncodeToPNG();
			}
			return ImageConversion.EncodeArrayToPNG(texture.GetRawTextureData<byte>().ToArray(), GraphicsFormat.R16G16B16A16_UNorm, (uint)texture.width, (uint)texture.height);
		}
		return null;
	}

	internal static void ApplyPrecisionImportSettings(TextureImporter importer, bool preferHighBitDepth)
	{
		if (importer == null)
		{
			return;
		}
		if (preferHighBitDepth)
		{
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			for (int i = 0; i < ManagedPlatforms.Length; i++)
			{
				ApplyHighBitDepthOverrideIfSupported(importer, ManagedPlatforms[i]);
			}
		}
		importer.userData = MergeExportModeTag(importer.userData, BuildExportModeTag(preferHighBitDepth));
	}

	private static void ApplyHighBitDepthOverrideIfSupported(TextureImporter importer, PlatformSpec platform)
	{
		if (SupportsHighBitDepthFormat(importer.textureType, platform))
		{
			ApplyPlatformOverride(importer, platform.Name, TextureImporterFormat.RGBA64, overridden: true);
		}
	}

	private static bool SupportsHighBitDepthFormat(TextureImporterType textureType, PlatformSpec platform)
	{
		if (!platform.IsDefaultPlatform)
		{
			if (!BuildPipeline.IsBuildTargetSupported(platform.Group, platform.Target))
			{
				return false;
			}
			return TextureImporter.IsPlatformTextureFormatValid(textureType, platform.Target, TextureImporterFormat.RGBA64);
		}
		return TextureImporter.IsDefaultPlatformTextureFormatValid(textureType, TextureImporterFormat.RGBA64);
	}

	private static void ApplyPlatformOverride(TextureImporter importer, string platformName, TextureImporterFormat format, bool overridden)
	{
		TextureImporterPlatformSettings platformTextureSettings = importer.GetPlatformTextureSettings(platformName);
		platformTextureSettings.name = platformName;
		platformTextureSettings.overridden = overridden;
		platformTextureSettings.format = format;
		platformTextureSettings.maxTextureSize = Math.Max(platformTextureSettings.maxTextureSize, 16384);
		platformTextureSettings.allowsAlphaSplitting = false;
		platformTextureSettings.textureCompression = TextureImporterCompression.Uncompressed;
		importer.SetPlatformTextureSettings(platformTextureSettings);
	}

	private static string BuildExportModeTag(bool preferHighBitDepth)
	{
		return string.Format("{0}:{1}:{2}", "Psd2UIExportMode", preferHighBitDepth ? "16" : "8", ResolveAuthorizationStamp());
	}

	private static int ResolveAuthorizationStamp()
	{
		if (!PsdLicenseService.GetPreviewLicenseState().GetLicenseValid())
		{
			return 0;
		}
		return 1;
	}

	private static string MergeExportModeTag(string userData, string tag)
	{
		if (!string.IsNullOrWhiteSpace(userData))
		{
			string[] array = userData.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			int num = 0;
			string text;
			while (true)
			{
				if (num < array.Length)
				{
					text = array[num].Trim();
					if (text.StartsWith("Psd2UIExportMode:", StringComparison.Ordinal))
					{
						break;
					}
					num++;
					continue;
				}
				return userData + "\n" + tag;
			}
			return userData.Replace(text, tag);
		}
		return tag;
	}

	private static PlatformSpec[] CollectManagedPlatforms()
	{
		List<PlatformSpec> list = new List<PlatformSpec>(8) { PlatformSpec.Default };
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal) { PlatformSpec.Default.Name };
		Array values = Enum.GetValues(typeof(BuildTarget));
		for (int i = 0; i < values.Length; i++)
		{
			BuildTarget buildTarget = (BuildTarget)values.GetValue(i);
			if (buildTarget == BuildTarget.NoTarget)
			{
				continue;
			}
			BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
			if (buildTargetGroup != BuildTargetGroup.Unknown && BuildPipeline.IsBuildTargetSupported(buildTargetGroup, buildTarget))
			{
				string buildTargetName = BuildPipeline.GetBuildTargetName(buildTarget);
				if (!string.IsNullOrWhiteSpace(buildTargetName) && hashSet.Add(buildTargetName))
				{
					list.Add(new PlatformSpec(buildTargetName, buildTargetGroup, buildTarget));
				}
			}
		}
		return list.ToArray();
	}

	static PsdTextureAssetUtility()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		ManagedPlatforms = CollectManagedPlatforms();
	}
}
}
