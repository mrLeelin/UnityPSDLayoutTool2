namespace PsdLayoutTool2.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;
    using PackageInfo = UnityEditor.PackageManager.PackageInfo;

    internal sealed class PsdHierarchyWebStaticAsset
    {
        public PsdHierarchyWebStaticAsset(string contentType, byte[] bytes)
        {
            this.contentType = contentType;
            this.bytes = bytes;
        }

        public string contentType { get; private set; }
        public byte[] bytes { get; private set; }
    }

    internal static class PsdHierarchyWebStaticAssets
    {
        private const string StaticRelativePath = "Editor/PsdPrefab/Hierarchy/Web/Static";
        private static readonly object Gate = new object();
        private static Dictionary<string, PsdHierarchyWebStaticAsset> cachedAssets;

        public static void WarmUp()
        {
            var loaded = new Dictionary<string, PsdHierarchyWebStaticAsset>(StringComparer.Ordinal);
            foreach (string route in new[] { "/", "/organizer.css", "/organizer.js" })
                loaded.Add(route, Load(route));
            lock (Gate) cachedAssets = loaded;
        }

        public static PsdHierarchyWebStaticAsset Resolve(string route)
        {
            lock (Gate)
            {
                PsdHierarchyWebStaticAsset cached;
                if (cachedAssets != null && cachedAssets.TryGetValue(route, out cached)) return cached;
                if (cachedAssets != null) return null;
            }
            return Load(route);
        }

        private static PsdHierarchyWebStaticAsset Load(string route)
        {
            string fileName;
            string contentType;
            switch (route)
            {
                case "/":
                    fileName = "index.html";
                    contentType = "text/html; charset=utf-8";
                    break;
                case "/organizer.css":
                    fileName = "organizer.css";
                    contentType = "text/css; charset=utf-8";
                    break;
                case "/organizer.js":
                    fileName = "organizer.js";
                    contentType = "text/javascript; charset=utf-8";
                    break;
                default:
                    return null;
            }

            string root = ResolvePackageContentRoot();
            if (string.IsNullOrEmpty(root)) return null;
            string path = Path.Combine(root, StaticRelativePath, fileName);
            return File.Exists(path) ? new PsdHierarchyWebStaticAsset(contentType, File.ReadAllBytes(path)) : null;
        }

        private static string ResolvePackageContentRoot()
        {
            PackageInfo package = PackageInfo.FindForAssembly(typeof(PsdHierarchyWebStaticAssets).Assembly);
            if (package != null && !string.IsNullOrEmpty(package.resolvedPath))
            {
                string packagedContent = Path.Combine(package.resolvedPath, "Assets", "PSDLayoutTool2");
                if (Directory.Exists(packagedContent)) return packagedContent;
                if (Directory.Exists(Path.Combine(package.resolvedPath, "Editor"))) return package.resolvedPath;
            }

            string[] assemblyDefinitions = AssetDatabase.FindAssets("PsdLayoutTool2.Editor t:AssemblyDefinitionAsset");
            foreach (string guid in assemblyDefinitions)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetFileName(assetPath), "PsdLayoutTool2.Editor.asmdef", StringComparison.Ordinal))
                    continue;
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                string editorDirectory = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(projectRoot, assetPath)));
                return editorDirectory == null ? null : Directory.GetParent(editorDirectory)?.FullName;
            }
            return null;
        }
    }
}
