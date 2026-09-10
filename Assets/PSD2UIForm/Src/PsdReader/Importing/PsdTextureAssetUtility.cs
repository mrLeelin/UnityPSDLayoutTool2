using System;
using System.Collections.Generic;
using PsdReaderLicenseServiceNamespace;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Experimental.Rendering;

namespace cn.efunstudio.psdreader
{
    internal sealed class PsdTextureAssetUtility
    {
        private readonly struct PlatformSpec
        {
            internal static readonly PlatformSpec Default = new PlatformSpec("DefaultTexturePlatform");

            internal readonly string Name;

            internal readonly BuildTargetGroup Group;

            internal readonly BuildTarget Target;

            internal readonly bool IsDefaultPlatform;

            internal static object s_ObfuscationSentinel;

            internal PlatformSpec(string name)
            {
                Name = name;
                Group = (BuildTargetGroup)0;
                Target = (BuildTarget)(-2);
                IsDefaultPlatform = true;
            }

            internal PlatformSpec(string name, BuildTargetGroup group, BuildTarget target)
            {
                Name = name;
                Group = group;
                Target = target;
                IsDefaultPlatform = false;
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

        private const string ExportModeKey = "Psd2UIExportMode";

        private static readonly PlatformSpec[] ManagedPlatforms = CollectManagedPlatforms();

        private static PsdTextureAssetUtility s_ObfuscationSentinel;

        internal static byte[] EncodePng(Texture2D texture)
        {
            if ((Object)(object)texture == (Object)null)
            {
                return null;
            }
            if ((int)texture.format != 74)
            {
                return ImageConversion.EncodeToPNG(texture);
            }
            return ImageConversion.EncodeArrayToPNG((Array)texture.GetRawTextureData<byte>().ToArray(), (GraphicsFormat)24, (uint)((Texture)texture).width, (uint)((Texture)texture).height, 0u);
        }

        internal static void ApplyPrecisionImportSettings(TextureImporter importer, bool preferHighBitDepth)
        {
            if ((Object)(object)importer == (Object)null)
            {
                return;
            }
            if (preferHighBitDepth)
            {
                importer.textureCompression = (TextureImporterCompression)0;
                for (int i = 0; i < ManagedPlatforms.Length; i++)
                {
                    ApplyHighBitDepthOverrideIfSupported(importer, ManagedPlatforms[i]);
                }
            }
            ((AssetImporter)importer).userData = MergeExportModeTag(((AssetImporter)importer).userData, BuildExportModeTag(preferHighBitDepth));
        }

        private static void ApplyHighBitDepthOverrideIfSupported(TextureImporter importer, PlatformSpec platform)
        {
            if (SupportsHighBitDepthFormat(importer.textureType, platform))
            {
                ApplyPlatformOverride(importer, platform.Name, (TextureImporterFormat)74, overridden: true);
            }
        }

        private static bool SupportsHighBitDepthFormat(TextureImporterType textureType, PlatformSpec platform)
        {
            if (platform.IsDefaultPlatform)
            {
                return TextureImporter.IsDefaultPlatformTextureFormatValid(textureType, (TextureImporterFormat)74);
            }
            if (!BuildPipeline.IsBuildTargetSupported(platform.Group, platform.Target))
            {
                return false;
            }
            return TextureImporter.IsPlatformTextureFormatValid(textureType, platform.Target, (TextureImporterFormat)74);
        }

        private static void ApplyPlatformOverride(TextureImporter importer, string platformName, TextureImporterFormat format, bool overridden)
        {
            TextureImporterPlatformSettings platformTextureSettings = importer.GetPlatformTextureSettings(platformName);
            platformTextureSettings.name = platformName;
            platformTextureSettings.overridden = overridden;
            platformTextureSettings.format = format;
            platformTextureSettings.maxTextureSize = Math.Max(platformTextureSettings.maxTextureSize, 16384);
            platformTextureSettings.allowsAlphaSplitting = false;
            platformTextureSettings.textureCompression = (TextureImporterCompression)0;
            importer.SetPlatformTextureSettings(platformTextureSettings);
        }

        private static string BuildExportModeTag(bool preferHighBitDepth)
        {
            return string.Format("{0}:{1}:{2}", "Psd2UIExportMode", preferHighBitDepth ? "16" : "8", ResolveAuthorizationStamp());
        }

        private static int ResolveAuthorizationStamp()
        {
            if (!PsdReaderLicenseService.GetOutputProtectionContext().GetIsAuthorized())
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
                for (int i = 0; i < array.Length; i++)
                {
                    string text = array[i].Trim();
                    if (text.StartsWith("Psd2UIExportMode:", StringComparison.Ordinal))
                    {
                        return userData.Replace(text, tag);
                    }
                }
                return userData + "\n" + tag;
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
                BuildTarget val = (BuildTarget)values.GetValue(i);
                if ((int)val == -2)
                {
                    continue;
                }
                BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(val);
                if ((int)buildTargetGroup != 0 && BuildPipeline.IsBuildTargetSupported(buildTargetGroup, val))
                {
                    string buildTargetName = BuildPipeline.GetBuildTargetName(val);
                    if (!string.IsNullOrWhiteSpace(buildTargetName) && hashSet.Add(buildTargetName))
                    {
                        list.Add(new PlatformSpec(buildTargetName, buildTargetGroup, val));
                    }
                }
            }
            return list.ToArray();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdTextureAssetUtility GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
