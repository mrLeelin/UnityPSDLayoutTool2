namespace PsdLayoutTool2.Editor
{
    using System;
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

        public static PsdHierarchyWebStaticAsset Resolve(string route)
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
