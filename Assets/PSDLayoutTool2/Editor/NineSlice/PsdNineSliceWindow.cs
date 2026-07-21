namespace PsdLayoutTool2
{
    using System;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Small Unity-native confirmation surface for a single generated PSD PNG.
    /// </summary>
    public sealed class PsdNineSliceWindow : EditorWindow
    {
        private string assetPath;
        private PsdNineSliceInference inference;
        private string status;
        private bool statusIsError;

        [MenuItem("Assets/PSD Layout/Analyze 9-Slice", true)]
        private static bool ValidateOpenForSelection()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return !string.IsNullOrEmpty(path) && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        }

        [MenuItem("Assets/PSD Layout/Analyze 9-Slice")]
        private static void OpenForSelection()
        {
            Open(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        /// <summary>
        /// Opens the tool for one generated PNG asset.
        /// </summary>
        public static void Open(string path)
        {
            PsdNineSliceWindow window = GetWindow<PsdNineSliceWindow>(true, "PSD 9-Slice", true);
            window.assetPath = !string.IsNullOrEmpty(path) && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? path
                : string.Empty;
            window.inference = null;
            window.status = "Click Analyze to create a candidate. Nothing is written until Apply.";
            window.statusIsError = false;
            window.minSize = new Vector2(360, 300);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Unity 9-Slice", EditorStyles.boldLabel);
            Texture2D selectedSource = (Texture2D)EditorGUILayout.ObjectField(
                "Generated PNG",
                AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath),
                typeof(Texture2D),
                false);
            string selectedPath = AssetDatabase.GetAssetPath(selectedSource);
            if (!string.Equals(assetPath, selectedPath, StringComparison.Ordinal))
            {
                assetPath = selectedPath;
                inference = null;
                status = "Click Analyze to create a candidate. Nothing is written until Apply.";
                statusIsError = false;
            }

            EditorGUILayout.LabelField("Source PNG", assetPath ?? "No PNG selected", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(6);

            Texture2D source = selectedSource;
            if (source != null)
            {
                Rect previewRect = GUILayoutUtility.GetRect(220, 130, GUILayout.ExpandWidth(true));
                EditorGUI.DrawPreviewTexture(previewRect, source, null, ScaleMode.ScaleToFit);
            }

            if (GUILayout.Button("Analyze pixels"))
            {
                Analyze();
            }

            if (inference != null)
            {
                GUILayout.Space(6);
                EditorGUILayout.LabelField("Candidate", inference.Method + " / " + inference.Confidence);
                int left = EditorGUILayout.IntField("Left", inference.Border.Left);
                int top = EditorGUILayout.IntField("Top", inference.Border.Top);
                int right = EditorGUILayout.IntField("Right", inference.Border.Right);
                int bottom = EditorGUILayout.IntField("Bottom", inference.Border.Bottom);
                if (left != inference.Border.Left || top != inference.Border.Top || right != inference.Border.Right || bottom != inference.Border.Bottom)
                {
                    inference = new PsdNineSliceInference(
                        new PsdNineSliceBorder(left, top, right, bottom),
                        PsdNineSliceConfidence.High,
                        "user-confirmed");
                }

                EditorGUILayout.HelpBox(
                    "Apply replaces this generated PNG with its minimum 9-slice image, stores the source fingerprint in its .meta userData, and sets Sprite Border.",
                    MessageType.Info);
                if (GUILayout.Button("Apply border and crop PNG"))
                {
                    Apply();
                }
            }

            if (!string.IsNullOrEmpty(status))
            {
                EditorGUILayout.HelpBox(status, statusIsError ? MessageType.Error : MessageType.Info);
            }
        }

        private void Analyze()
        {
            string error;
            if (PsdNineSliceTextureProcessor.TryAnalyze(assetPath, out inference, out error))
            {
                status = "Candidate is ready. Review the four values, then Apply.";
                statusIsError = false;
            }
            else
            {
                status = error;
                statusIsError = true;
            }
        }

        private void Apply()
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            uint layerId;
            if (importer == null || !PsdNineSliceAssetState.TryReadLayerIdentity(importer.userData, out layerId))
            {
                status = "This PNG has no PSD layer identity yet. Export the PSD layers once, then reopen this tool from the generated PNG.";
                statusIsError = true;
                return;
            }

            string error;
            if (!PsdNineSliceTextureProcessor.TryCropAndPersist(assetPath, importer, layerId, inference.Border, out error))
            {
                status = error;
                statusIsError = true;
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.spriteBorder = PsdNineSliceTextureProcessor.ToUnityBorder(inference.Border);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            status = "Applied. Future PSD imports reuse this crop only while the source pixels remain unchanged.";
            statusIsError = false;
        }
    }
}
