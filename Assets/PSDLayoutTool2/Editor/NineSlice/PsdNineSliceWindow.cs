namespace PsdLayoutTool2
{
    using System;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Unity-native nine-slice editor. PNG assets retain the previous
    /// crop-and-apply workflow while a PSD path opens the artist-facing layer
    /// browser and persists manual decisions in the PSD asset meta data.
    /// </summary>
    public sealed class PsdNineSliceWindow : EditorWindow
    {
        private enum EditorMode
        {
            Png,
            Psd
        }

        private enum DragGuide
        {
            None,
            Left,
            Top,
            Right,
            Bottom
        }

        private EditorMode mode;
        private string assetPath;
        private PsdNineSliceInference inference;
        private PsdNineSlicePsdLayerSession psdSession;
        private Vector2 layerScroll;
        private string layerSearchText = string.Empty;
        private int selectedLayerIndex = -1;
        private bool nineSliceEnabled;
        private bool hasManualOverride;
        private PsdNineSliceBorder editableBorder;
        private DragGuide activeDrag;
        private bool autoSavePending;
        private double autoSaveDeadline;
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
        /// Opens the editor for a generated PNG or for all visible raster
        /// layers in a PSD selected from the PSD Layout Tool inspector.
        /// </summary>
        public static void Open(string path)
        {
            PsdNineSliceWindow window = GetWindow<PsdNineSliceWindow>(true, "PSD 9-Slice", true);
            window.ClosePsdSession();
            window.assetPath = path ?? string.Empty;
            window.inference = null;
            window.selectedLayerIndex = -1;
            window.activeDrag = DragGuide.None;
            window.statusIsError = false;

            if (window.assetPath.EndsWith(".psd", StringComparison.OrdinalIgnoreCase))
            {
                window.mode = EditorMode.Psd;
                try
                {
                    window.psdSession = PsdNineSlicePsdLayerSession.Open(window.assetPath);
                    window.status = window.psdSession.Layers.Count == 0
                        ? "This PSD has no visible raster layers with pixels."
                        : "Select an image, then drag the four cyan guides or type exact pixel values.";
                    if (window.psdSession.Layers.Count > 0)
                    {
                        window.SelectPsdLayer(0);
                    }
                }
                catch (Exception exception)
                {
                    window.status = "Unable to open PSD: " + exception.Message;
                    window.statusIsError = true;
                }

                window.minSize = new Vector2(760, 460);
            }
            else
            {
                window.mode = EditorMode.Png;
                window.assetPath = window.assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? window.assetPath : string.Empty;
                window.status = "Click Analyze to create a candidate. Nothing is written until Apply.";
                window.minSize = new Vector2(360, 300);
            }

            window.Show();
        }

        private void OnDisable()
        {
            EditorApplication.update -= ProcessPendingAutoSave;
            ClosePsdSession();
        }

        private void OnEnable()
        {
            EditorApplication.update -= ProcessPendingAutoSave;
            EditorApplication.update += ProcessPendingAutoSave;
        }

        private void OnGUI()
        {
            if (mode == EditorMode.Psd)
            {
                DrawPsdEditor();
                return;
            }

            DrawPngEditor();
        }

        private void DrawPsdEditor()
        {
            EditorGUILayout.LabelField("PSD 9-Slice Editor", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(assetPath, EditorStyles.wordWrappedMiniLabel);
            if (psdSession == null)
            {
                DrawStatus();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawPsdLayerList();
            DrawPsdLayerEditor();
            EditorGUILayout.EndHorizontal();
            DrawStatus();
        }

        private void DrawPsdLayerList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(285));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Visible image layers", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            layerSearchText = EditorGUILayout.TextField(layerSearchText ?? string.Empty, EditorStyles.textField);
            if (GUILayout.Button("Clear", GUILayout.Width(48)))
            {
                layerSearchText = string.Empty;
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();
            layerScroll = EditorGUILayout.BeginScrollView(layerScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            int visibleCount = 0;
            for (int index = 0; index < psdSession.Layers.Count; index++)
            {
                PsdNineSlicePsdLayerEntry entry = psdSession.Layers[index];
                if (!MatchesLayerSearch(entry, layerSearchText))
                {
                    continue;
                }

                visibleCount++;
                string prefix = new string(' ', Mathf.Clamp(entry.Depth, 0, 8) * 2);
                string id = entry.LayerId == 0U ? "no layer id" : "#" + entry.LayerId;
                string label = prefix + entry.DisplayName + "  [" + Mathf.RoundToInt(entry.Rect.width) + "x" + Mathf.RoundToInt(entry.Rect.height) + "]  " + id;
                bool selected = index == selectedLayerIndex;
                if (GUILayout.Toggle(selected, label, "Button", GUILayout.ExpandWidth(true)) && !selected)
                {
                    SelectPsdLayer(index);
                }
            }

            if (visibleCount == 0)
            {
                EditorGUILayout.HelpBox("No image layer matches the search.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static bool MatchesLayerSearch(PsdNineSlicePsdLayerEntry entry, string query)
        {
            if (entry == null || string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            string compactQuery = query.Replace(" ", string.Empty).Trim();
            if (compactQuery.Length == 0)
            {
                return true;
            }

            string layerId = entry.LayerId == 0U ? string.Empty : entry.LayerId.ToString();
            return IsFuzzyMatch(entry.DisplayName, compactQuery) || IsFuzzyMatch(layerId, compactQuery);
        }

        private static bool IsFuzzyMatch(string source, string query)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(query))
            {
                return false;
            }

            int queryIndex = 0;
            for (int index = 0; index < source.Length && queryIndex < query.Length; index++)
            {
                if (char.ToUpperInvariant(source[index]) == char.ToUpperInvariant(query[queryIndex]))
                {
                    queryIndex++;
                }
            }

            return queryIndex == query.Length;
        }

        private void DrawPsdLayerEditor()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            PsdNineSlicePsdLayerEntry entry = GetSelectedPsdLayer();
            if (entry == null)
            {
                EditorGUILayout.HelpBox("Select a visible image layer on the left.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField(entry.DisplayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Photoshop layer " + (entry.LayerId == 0U ? "has no stable ID" : entry.LayerId.ToString()) + " · " + Mathf.RoundToInt(entry.Rect.width) + " x " + Mathf.RoundToInt(entry.Rect.height) + " PSD pixels", EditorStyles.wordWrappedMiniLabel);
            Texture2D preview = psdSession.GetPreview(entry);
            Rect previewRect = GUILayoutUtility.GetRect(260f, 300f, GUILayout.ExpandWidth(true), GUILayout.Height(300f));
            if (preview == null)
            {
                EditorGUI.HelpBox(previewRect, "Unity could not decode this PSD layer.", MessageType.Warning);
            }
            else
            {
                Rect imageRect = GetAspectFitRect(previewRect, preview.width, preview.height);
                DrawTransparentPreview(imageRect, preview);
                DrawNineSliceGuides(imageRect, preview.width, preview.height);
                HandleGuideDrag(imageRect, preview.width, preview.height);
            }

            EditorGUILayout.BeginHorizontal();
            bool enabled = EditorGUILayout.ToggleLeft("Use manual 9-slice override", nineSliceEnabled, GUILayout.Width(210));
            if (enabled != nineSliceEnabled)
            {
                nineSliceEnabled = enabled;
                ScheduleAutoSave();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(hasManualOverride ? "Manual override" : "No manual override", EditorStyles.miniLabel, GUILayout.Width(105));
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginDisabledGroup(!nineSliceEnabled || preview == null);
            GUILayout.Space(4f);
            if (DrawBorderFields(preview == null ? 0 : preview.width, preview == null ? 0 : preview.height))
            {
                ScheduleAutoSave();
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Use automatic candidate"))
            {
                UseAutomaticCandidate(preview);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.HelpBox(
                "Checked: this layer uses the manual left, top, right, bottom pixels. Unchecked: automatic PSD naming/XMP rules are used. Changes save to this PSD asset's .meta automatically after you stop editing.",
                MessageType.None);
            EditorGUILayout.EndVertical();
        }

        private bool DrawBorderFields(int width, int height)
        {
            if (editableBorder == null)
            {
                editableBorder = CreateDefaultBorder(width, height);
            }

            EditorGUILayout.BeginHorizontal();
            int left = EditorGUILayout.IntField("Left", editableBorder.Left);
            int right = EditorGUILayout.IntField("Right", editableBorder.Right);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            int top = EditorGUILayout.IntField("Top", editableBorder.Top);
            int bottom = EditorGUILayout.IntField("Bottom", editableBorder.Bottom);
            EditorGUILayout.EndHorizontal();
            PsdNineSliceBorder next = ClampBorder(new PsdNineSliceBorder(left, top, right, bottom), width, height);
            if (!BordersEqual(next, editableBorder))
            {
                editableBorder = next;
                hasManualOverride = true;
                return true;
            }

            return false;
        }

        private void DrawNineSliceGuides(Rect imageRect, int width, int height)
        {
            if (!nineSliceEnabled || editableBorder == null || width <= 0 || height <= 0)
            {
                return;
            }

            Color old = Handles.color;
            Handles.color = new Color(0.1f, 0.85f, 1f, 0.95f);
            float left = imageRect.xMin + (imageRect.width * editableBorder.Left / width);
            float right = imageRect.xMax - (imageRect.width * editableBorder.Right / width);
            float top = imageRect.yMin + (imageRect.height * editableBorder.Top / height);
            float bottom = imageRect.yMax - (imageRect.height * editableBorder.Bottom / height);
            Handles.DrawLine(new Vector3(left, imageRect.yMin), new Vector3(left, imageRect.yMax));
            Handles.DrawLine(new Vector3(right, imageRect.yMin), new Vector3(right, imageRect.yMax));
            Handles.DrawLine(new Vector3(imageRect.xMin, top), new Vector3(imageRect.xMax, top));
            Handles.DrawLine(new Vector3(imageRect.xMin, bottom), new Vector3(imageRect.xMax, bottom));
            Handles.color = old;
        }

        private void HandleGuideDrag(Rect imageRect, int width, int height)
        {
            if (!nineSliceEnabled || editableBorder == null || Event.current == null)
            {
                return;
            }

            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && imageRect.Contains(evt.mousePosition))
            {
                activeDrag = FindClosestGuide(imageRect, width, height, evt.mousePosition);
                if (activeDrag != DragGuide.None)
                {
                    evt.Use();
                }
            }
            else if (evt.type == EventType.MouseDrag && activeDrag != DragGuide.None)
            {
                editableBorder = BorderFromDrag(activeDrag, imageRect, width, height, evt.mousePosition, editableBorder);
                hasManualOverride = true;
                Repaint();
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && activeDrag != DragGuide.None)
            {
                activeDrag = DragGuide.None;
                ScheduleAutoSave();
                evt.Use();
            }
        }

        private void UseAutomaticCandidate(Texture2D preview)
        {
            PsdNineSliceInference candidate;
            string error;
            if (!PsdNineSliceTextureProcessor.TryAnalyze(preview, out candidate, out error))
            {
                status = error;
                statusIsError = true;
                return;
            }

            inference = candidate;
            editableBorder = candidate.Border;
            nineSliceEnabled = true;
            hasManualOverride = true;
            ScheduleAutoSave();
            status = "Automatic candidate applied. Drag the cyan guides to refine it; the .meta is saved automatically after you stop.";
            statusIsError = false;
        }

        private void SaveCurrentOverride(PsdNineSlicePsdLayerEntry entry, Texture2D preview)
        {
            if (entry.LayerId == 0U)
            {
                status = "This PSD layer has no stable Photoshop layer ID, so its override cannot survive incremental import.";
                statusIsError = true;
                return;
            }

            if (nineSliceEnabled && (preview == null || editableBorder == null || !editableBorder.IsValidFor(preview.width, preview.height)))
            {
                status = "The four borders must leave at least a two-pixel center in the original PSD layer.";
                statusIsError = true;
                return;
            }

            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
            {
                status = "Unity could not access the selected PSD importer.";
                statusIsError = true;
                return;
            }

            importer.userData = nineSliceEnabled
                ? PsdNineSliceOverrideStore.Write(importer.userData, entry.LayerId, true, editableBorder)
                : PsdNineSliceOverrideStore.Remove(importer.userData, entry.LayerId);
            AssetDatabase.WriteImportSettingsIfDirty(assetPath);
            hasManualOverride = nineSliceEnabled;
            status = nineSliceEnabled
                ? "Saved manual 9-slice override. The next PSD-to-Prefab import uses it first."
                : "Manual override removed. The next PSD-to-Prefab import uses PSD naming/XMP automatic rules.";
            statusIsError = false;
        }

        private void ScheduleAutoSave()
        {
            if (mode != EditorMode.Psd || psdSession == null || GetSelectedPsdLayer() == null)
            {
                return;
            }

            autoSavePending = true;
            autoSaveDeadline = EditorApplication.timeSinceStartup + 0.35d;
        }

        private void ProcessPendingAutoSave()
        {
            if (!autoSavePending || EditorApplication.timeSinceStartup < autoSaveDeadline)
            {
                return;
            }

            autoSavePending = false;
            PsdNineSlicePsdLayerEntry entry = GetSelectedPsdLayer();
            Texture2D preview = psdSession == null || entry == null ? null : psdSession.GetPreview(entry);
            if (entry != null)
            {
                SaveCurrentOverride(entry, preview);
            }
        }

        private void SelectPsdLayer(int index)
        {
            selectedLayerIndex = index;
            inference = null;
            LoadSelectedPsdLayerState();
            Repaint();
        }

        private void LoadSelectedPsdLayerState()
        {
            PsdNineSlicePsdLayerEntry entry = GetSelectedPsdLayer();
            if (entry == null)
            {
                return;
            }

            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            PsdNineSliceOverride saved;
            if (importer != null && PsdNineSliceOverrideStore.TryGet(importer.userData, entry.LayerId, out saved) && saved.Enabled)
            {
                hasManualOverride = true;
                nineSliceEnabled = true;
                editableBorder = saved.Border;
                return;
            }

            hasManualOverride = false;
            PsdNineSliceNameRule rule;
            if (PsdNineSliceNameRules.TryParse(entry.DisplayName, out rule))
            {
                nineSliceEnabled = false;
                editableBorder = rule.ExplicitBorder ?? CreateDefaultBorder(Mathf.RoundToInt(entry.Rect.width), Mathf.RoundToInt(entry.Rect.height));
            }
            else
            {
                nineSliceEnabled = false;
                editableBorder = CreateDefaultBorder(Mathf.RoundToInt(entry.Rect.width), Mathf.RoundToInt(entry.Rect.height));
            }
        }

        private PsdNineSlicePsdLayerEntry GetSelectedPsdLayer()
        {
            return psdSession != null && selectedLayerIndex >= 0 && selectedLayerIndex < psdSession.Layers.Count
                ? psdSession.Layers[selectedLayerIndex]
                : null;
        }

        private static Rect GetAspectFitRect(Rect available, int width, int height)
        {
            if (width <= 0 || height <= 0 || available.width <= 0f || available.height <= 0f)
            {
                return available;
            }

            float sourceAspect = width / (float)height;
            float availableAspect = available.width / available.height;
            if (sourceAspect > availableAspect)
            {
                float heightFit = available.width / sourceAspect;
                return new Rect(available.x, available.y + ((available.height - heightFit) * 0.5f), available.width, heightFit);
            }

            float widthFit = available.height * sourceAspect;
            return new Rect(available.x + ((available.width - widthFit) * 0.5f), available.y, widthFit, available.height);
        }

        /// <summary>
        /// Draws layer pixels like a PNG preview. Photoshop preserves RGB data
        /// beneath transparent pixels, so drawing the texture without alpha
        /// blending incorrectly exposes that hidden matte color.
        /// </summary>
        private static void DrawTransparentPreview(Rect rect, Texture2D texture)
        {
            const float cellSize = 12f;
            Color light = new Color(0.76f, 0.76f, 0.76f, 1f);
            Color dark = new Color(0.58f, 0.58f, 0.58f, 1f);
            int columns = Mathf.CeilToInt(rect.width / cellSize);
            int rows = Mathf.CeilToInt(rect.height / cellSize);
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    EditorGUI.DrawRect(
                        new Rect(rect.x + (column * cellSize), rect.y + (row * cellSize), cellSize, cellSize),
                        ((row + column) & 1) == 0 ? light : dark);
                }
            }

            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
        }

        private static PsdNineSliceBorder CreateDefaultBorder(int width, int height)
        {
            int horizontal = Mathf.Max(0, Mathf.Min(Mathf.Max(0, width - 2) / 4, 16));
            int vertical = Mathf.Max(0, Mathf.Min(Mathf.Max(0, height - 2) / 4, 16));
            return new PsdNineSliceBorder(horizontal, vertical, horizontal, vertical);
        }

        private static PsdNineSliceBorder ClampBorder(PsdNineSliceBorder border, int width, int height)
        {
            int left = Mathf.Clamp(border.Left, 0, Mathf.Max(0, width - 2));
            int right = Mathf.Clamp(border.Right, 0, Mathf.Max(0, width - left - 2));
            int top = Mathf.Clamp(border.Top, 0, Mathf.Max(0, height - 2));
            int bottom = Mathf.Clamp(border.Bottom, 0, Mathf.Max(0, height - top - 2));
            return new PsdNineSliceBorder(left, top, right, bottom);
        }

        private static bool BordersEqual(PsdNineSliceBorder left, PsdNineSliceBorder right)
        {
            return left == right || (left != null && right != null && left.Left == right.Left && left.Top == right.Top && left.Right == right.Right && left.Bottom == right.Bottom);
        }

        private DragGuide FindClosestGuide(Rect imageRect, int width, int height, Vector2 mouse)
        {
            const float threshold = 8f;
            if (editableBorder == null || width <= 0 || height <= 0)
            {
                return DragGuide.None;
            }

            float bestDistance = float.MaxValue;
            DragGuide best = DragGuide.None;
            TrySelectGuide(DragGuide.Left, Mathf.Abs(mouse.x - (imageRect.xMin + (imageRect.width * editableBorder.Left / width))), ref bestDistance, ref best);
            TrySelectGuide(DragGuide.Right, Mathf.Abs(mouse.x - (imageRect.xMax - (imageRect.width * editableBorder.Right / width))), ref bestDistance, ref best);
            TrySelectGuide(DragGuide.Top, Mathf.Abs(mouse.y - (imageRect.yMin + (imageRect.height * editableBorder.Top / height))), ref bestDistance, ref best);
            TrySelectGuide(DragGuide.Bottom, Mathf.Abs(mouse.y - (imageRect.yMax - (imageRect.height * editableBorder.Bottom / height))), ref bestDistance, ref best);
            return bestDistance < threshold ? best : DragGuide.None;
        }

        private static void TrySelectGuide(DragGuide guide, float distance, ref float bestDistance, ref DragGuide best)
        {
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = guide;
            }
        }

        private PsdNineSliceBorder BorderFromDrag(DragGuide guide, Rect imageRect, int width, int height, Vector2 mouse, PsdNineSliceBorder current)
        {
            int left = current.Left;
            int top = current.Top;
            int right = current.Right;
            int bottom = current.Bottom;
            switch (guide)
            {
                case DragGuide.Left: left = Mathf.RoundToInt((mouse.x - imageRect.xMin) / imageRect.width * width); break;
                case DragGuide.Right: right = Mathf.RoundToInt((imageRect.xMax - mouse.x) / imageRect.width * width); break;
                case DragGuide.Top: top = Mathf.RoundToInt((mouse.y - imageRect.yMin) / imageRect.height * height); break;
                case DragGuide.Bottom: bottom = Mathf.RoundToInt((imageRect.yMax - mouse.y) / imageRect.height * height); break;
            }

            return ClampBorder(new PsdNineSliceBorder(left, top, right, bottom), width, height);
        }

        private void DrawPngEditor()
        {
            EditorGUILayout.LabelField("Unity 9-Slice", EditorStyles.boldLabel);
            Texture2D selectedSource = (Texture2D)EditorGUILayout.ObjectField("Generated PNG", AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath), typeof(Texture2D), false);
            string selectedPath = AssetDatabase.GetAssetPath(selectedSource);
            if (!string.Equals(assetPath, selectedPath, StringComparison.Ordinal))
            {
                assetPath = selectedPath;
                inference = null;
                status = "Click Analyze to create a candidate. Nothing is written until Apply.";
                statusIsError = false;
            }

            EditorGUILayout.LabelField("Source PNG", assetPath ?? "No PNG selected", EditorStyles.wordWrappedMiniLabel);
            Texture2D source = selectedSource;
            if (source != null)
            {
                EditorGUI.DrawPreviewTexture(GUILayoutUtility.GetRect(220, 130, GUILayout.ExpandWidth(true)), source, null, ScaleMode.ScaleToFit);
            }

            if (GUILayout.Button("Analyze pixels")) AnalyzePng();
            if (inference != null)
            {
                int left = EditorGUILayout.IntField("Left", inference.Border.Left);
                int top = EditorGUILayout.IntField("Top", inference.Border.Top);
                int right = EditorGUILayout.IntField("Right", inference.Border.Right);
                int bottom = EditorGUILayout.IntField("Bottom", inference.Border.Bottom);
                if (left != inference.Border.Left || top != inference.Border.Top || right != inference.Border.Right || bottom != inference.Border.Bottom)
                    inference = new PsdNineSliceInference(new PsdNineSliceBorder(left, top, right, bottom), PsdNineSliceConfidence.High, "user-confirmed");
                if (GUILayout.Button("Apply border and crop PNG")) ApplyPng();
            }

            DrawStatus();
        }

        private void AnalyzePng()
        {
            string error;
            if (PsdNineSliceTextureProcessor.TryAnalyze(assetPath, out inference, out error))
            {
                status = "Candidate is ready. Review the four values, then Apply.";
                statusIsError = false;
            }
            else { status = error; statusIsError = true; }
        }

        private void ApplyPng()
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
            if (!PsdNineSliceTextureProcessor.TryCropAndPersist(assetPath, importer, layerId, inference.Border, out error)) { status = error; statusIsError = true; return; }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.spriteBorder = PsdNineSliceTextureProcessor.ToUnityBorder(inference.Border);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            status = "Applied. Future PSD imports reuse this crop only while the source pixels remain unchanged.";
            statusIsError = false;
        }

        private void DrawStatus()
        {
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusIsError ? MessageType.Error : MessageType.Info);
        }

        private void ClosePsdSession()
        {
            if (psdSession != null) psdSession.Dispose();
            psdSession = null;
        }
    }
}
