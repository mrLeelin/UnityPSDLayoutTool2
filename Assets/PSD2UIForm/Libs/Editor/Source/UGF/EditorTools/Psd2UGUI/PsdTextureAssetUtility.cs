using System;
using System.Collections.Generic;
using System.IO;
using PsdProtectionGuards;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using cn.efunstudio.psdreader;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
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

		internal static object ywUIMrZK9h2jKZvE4OCJ;

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

		internal static bool ISWKoSZKmvLUimoRpBCI()
		{
			return ywUIMrZK9h2jKZvE4OCJ == null;
		}

		internal static object TqTI4XZKo8dWLI2qXGhj()
		{
			return ywUIMrZK9h2jKZvE4OCJ;
		}
	}

	private const string ExportModeKey = "Psd2UIExportMode";

	private static readonly PlatformSpec[] ManagedPlatforms;

	internal static byte[] EncodePng(PsdRenderedImage rendered)
	{
		if (rendered != null && !rendered.IsEmpty)
		{
			if (!rendered.IsHighBitDepth)
			{
				return ImageConversion.EncodeArrayToPNG(rendered.Rgba32, GraphicsFormat.R8G8B8A8_UNorm, (uint)rendered.Width, (uint)rendered.Height);
			}
			ushort[] rgba = rendered.Rgba64;
			byte[] array = new byte[rgba.Length * 2];
			Buffer.BlockCopy(rgba, 0, array, 0, array.Length);
			return ImageConversion.EncodeArrayToPNG(array, GraphicsFormat.R16G16B16A16_UNorm, (uint)rendered.Width, (uint)rendered.Height);
		}
		return null;
	}

	internal static byte[] EncodePng(Texture2D texture)
	{
		if (texture == null)
		{
			return null;
		}
		if (texture.format != TextureFormat.RGBA64)
		{
			return texture.EncodeToPNG();
		}
		return ImageConversion.EncodeArrayToPNG(texture.GetRawTextureData<byte>().ToArray(), GraphicsFormat.R16G16B16A16_UNorm, (uint)texture.width, (uint)texture.height);
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

	internal static bool MatchesExportMode(string assetPath, bool preferHighBitDepth)
	{
		if (!string.IsNullOrWhiteSpace(assetPath) && File.Exists(assetPath))
		{
			TextureImporter textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
			if (textureImporter == null)
			{
				return false;
			}
			string b = BuildExportModeTag(preferHighBitDepth);
			if (!TryExtractExportModeTag(textureImporter.userData, out var tag))
			{
				return false;
			}
			return string.Equals(tag, b, StringComparison.Ordinal);
		}
		return false;
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
		if (!PsdReaderProductAccess.IsAvailable)
		{
			return 0;
		}
		return 1;
	}

	private static string MergeExportModeTag(string userData, string tag)
	{
		if (string.IsNullOrWhiteSpace(userData))
		{
			return tag;
		}
		if (TryExtractExportModeTag(userData, out var tag2))
		{
			return userData.Replace(tag2, tag);
		}
		return userData + "\n" + tag;
	}

	private static bool TryExtractExportModeTag(string userData, out string tag)
	{
		tag = null;
		if (!string.IsNullOrWhiteSpace(userData))
		{
			string[] array = userData.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i].Trim();
				if (text.StartsWith("Psd2UIExportMode:", StringComparison.Ordinal))
				{
					tag = text;
					return true;
				}
			}
			return false;
		}
		return false;
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
