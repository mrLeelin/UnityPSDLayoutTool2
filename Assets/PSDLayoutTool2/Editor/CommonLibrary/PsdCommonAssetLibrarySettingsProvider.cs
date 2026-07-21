namespace PsdLayoutTool2
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Project Settings UI for Common_* public asset roots.
    /// </summary>
    internal static class PsdCommonAssetLibrarySettingsProvider
    {
        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/PSD Layout Tool/Common Asset Library", SettingsScope.Project)
            {
                label = "PSD Layout Tool - Common Asset Library",
                guiHandler = DrawGui,
                keywords = new HashSet<string>(new[] { "PSD", "Common", "Prefab", "Texture", "Sprite" })
            };
        }

        private static void DrawGui(string searchContext)
        {
            PsdCommonAssetLibrarySettings settings = PsdCommonAssetLibrarySettings.Load();
            if (settings == null)
            {
                EditorGUILayout.HelpBox(
                    "Create the project Common Asset Library before importing Common_Prefab_ or Common_Texture_ PSD layers.",
                    MessageType.Info);
                if (GUILayout.Button("Create Default Common Asset Library"))
                {
                    settings = PsdCommonAssetLibrarySettings.CreateDefault();
                    PsdCommonAssetResolver.Invalidate();
                    Selection.activeObject = settings;
                }

                return;
            }

            EditorGUILayout.HelpBox(
                "Only these folders are indexed. Common_Prefab_<Key> and Common_Texture_<Key> require exactly one matching asset name.",
                MessageType.Info);
            DrawFolderList("Prefab Roots", settings.prefabRoots);
            DrawFolderList("Texture / Sprite Roots", settings.textureRoots);

            if (GUI.changed)
            {
                EditorUtility.SetDirty(settings);
                PsdCommonAssetResolver.Invalidate();
            }
        }

        private static void DrawFolderList(string label, List<DefaultAsset> roots)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            for (int index = 0; index < roots.Count; index++)
            {
                EditorGUILayout.BeginHorizontal();
                roots[index] = (DefaultAsset)EditorGUILayout.ObjectField(roots[index], typeof(DefaultAsset), false);
                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    roots.RemoveAt(index);
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Folder", GUILayout.Width(110f)))
            {
                roots.Add(null);
            }

            EditorGUILayout.Space(8f);
        }
    }
}
