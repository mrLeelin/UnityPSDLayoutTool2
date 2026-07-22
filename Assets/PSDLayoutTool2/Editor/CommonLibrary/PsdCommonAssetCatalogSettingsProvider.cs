namespace PsdLayoutTool2
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Project Settings UI for generating and inspecting the Common Asset Catalog.
    /// </summary>
    internal static class PsdCommonAssetCatalogSettingsProvider
    {
        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/PSD Layout Tool/Common Asset Catalog", SettingsScope.Project)
            {
                label = "PSD Layout Tool - Common Asset Catalog",
                guiHandler = DrawGui,
                keywords = new HashSet<string>(new[] { "PSD", "Common", "Prefab", "Texture", "Catalog" })
            };
        }

        private static void DrawGui(string searchContext)
        {
            PsdCommonAssetCatalog catalog = PsdCommonAssetCatalog.Load();
            EditorGUILayout.HelpBox(
                "Refresh scans the entire project for asset names beginning with Common_Prefab_ and Common_Texture_, then stores direct GUID references in the catalog. PSD import reads this catalog only.",
                MessageType.Info);
            if (GUILayout.Button(catalog == null ? "Generate Common Asset Catalog" : "Refresh Common Asset Catalog"))
            {
                catalog = PsdCommonAssetCatalog.CreateOrRefresh();
                Selection.activeObject = catalog;
            }

            if (catalog == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Catalog Status", catalog.needsRefresh ? "Out of date - generate/refresh required" : "Ready (auto-updates Common_* asset changes)");
            EditorGUILayout.LabelField("Prefabs", catalog.prefabs.Count.ToString());
            EditorGUILayout.LabelField("Textures / Sprites", catalog.textures.Count.ToString());
            if (GUILayout.Button("Select Catalog Asset"))
            {
                Selection.activeObject = catalog;
            }
        }
    }
}
