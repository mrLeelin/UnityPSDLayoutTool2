namespace PsdLayoutTool2
{
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Versioned project configuration for folders that expose Common_* assets.
    /// </summary>
    public sealed class PsdCommonAssetLibrarySettings : ScriptableObject
    {
        public const string AssetPath = "Assets/PSDLayoutTool2Settings/PsdCommonAssetLibrary.asset";
        public List<DefaultAsset> prefabRoots = new List<DefaultAsset>();
        public List<DefaultAsset> textureRoots = new List<DefaultAsset>();

        public static PsdCommonAssetLibrarySettings Load()
        {
            return AssetDatabase.LoadAssetAtPath<PsdCommonAssetLibrarySettings>(AssetPath);
        }

        public static PsdCommonAssetLibrarySettings CreateDefault()
        {
            PsdCommonAssetLibrarySettings existing = Load();
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder("Assets/PSDLayoutTool2Settings");
            EnsureFolder("Assets/UI/Common/Prefabs");
            EnsureFolder("Assets/UI/Common/Textures");

            PsdCommonAssetLibrarySettings settings = CreateInstance<PsdCommonAssetLibrarySettings>();
            settings.prefabRoots.Add(AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/UI/Common/Prefabs"));
            settings.textureRoots.Add(AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/UI/Common/Textures"));
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        public IEnumerable<string> GetRootPaths(PsdCommonAssetKind kind)
        {
            List<DefaultAsset> roots = kind == PsdCommonAssetKind.Prefab ? prefabRoots : textureRoots;
            foreach (DefaultAsset root in roots)
            {
                string path = root != null ? AssetDatabase.GetAssetPath(root) : string.Empty;
                if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
                {
                    yield return path;
                }
            }
        }

        public bool IsPathUnderConfiguredRoot(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            foreach (string root in GetRootPaths(PsdCommonAssetKind.Prefab))
            {
                if (IsPathInsideRoot(assetPath, root))
                {
                    return true;
                }
            }

            foreach (string root in GetRootPaths(PsdCommonAssetKind.Texture))
            {
                if (IsPathInsideRoot(assetPath, root))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPathInsideRoot(string assetPath, string root)
        {
            string normalizedAssetPath = assetPath.Replace('\\', '/').TrimEnd('/');
            string normalizedRoot = root.Replace('\\', '/').TrimEnd('/');
            return normalizedAssetPath.StartsWith(normalizedRoot + "/", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedAssetPath, normalizedRoot, System.StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
