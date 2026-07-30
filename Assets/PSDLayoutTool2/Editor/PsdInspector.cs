namespace PsdLayoutTool2
{
    using System;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// A custom Inspector to allow PSD files to be turned into prefabs and separate textures per layer.
    /// </summary>
    /// <remarks>
    /// Unity isn't able to draw the custom inspector for a TextureImporter (even if calling the base
    /// method or calling DrawDefaultInspector).  It comes out as just a generic, hard to use mess of GUI
    /// items.  To add in the buttons we want without disrupting the normal GUI for TextureImporter, we have
    /// to do some reflection "magic".
    /// Thanks to DeadNinja: http://forum.unity3d.com/threads/custom-textureimporterinspector.260833/
    /// </remarks>
    [CustomEditor(typeof(TextureImporter))]
    public class PsdInspector : UnityEditor.Editor
    {
        /// <summary>
        /// Supported inspector display languages.
        /// </summary>
        private enum InspectorLanguage
        {
            /// <summary>
            /// Chinese UI.
            /// </summary>
            Chinese = 0,

            /// <summary>
            /// English UI.
            /// </summary>
            English = 1
        }

        /// <summary>
        /// EditorPrefs key for selecting Canvas/Unity UI output mode.
        /// </summary>
        private const string UseUnityUIPrefKey = "PsdLayoutTool2.UseUnityUI";

        /// <summary>
        /// EditorPrefs key for auto anchor by name.
        /// </summary>
        private const string AutoAnchorByNamePrefKey = "PsdLayoutTool2.EnableAutoAnchorByName";

        /// <summary>
        /// EditorPrefs key for default global root anchoring.
        /// </summary>
        private const string RootGlobalAnchorPrefKey = "PsdLayoutTool2.RootUseGlobalAnchorByDefault";

        /// <summary>
        /// EditorPrefs key for using TextMeshProUGUI for PSD text layers.
        /// </summary>
        private const string TextMeshProEnabledPrefKey = "PsdLayoutTool2.UseTextMeshPro";

        /// <summary>
        /// EditorPrefs key for inspector display language.
        /// </summary>
        private const string LanguagePrefKey = "PsdLayoutTool2.InspectorLanguage";

        /// <summary>
        /// EditorPrefs key for showing Unity's built-in texture importer inspector inside the PSD inspector.
        /// </summary>
        private const string ShowNativeInspectorPrefKey = "PsdLayoutTool2.ShowNativeTextureImporterInspector";

#if UNITY_2021_3_OR_NEWER && !UNITY_2022_1_OR_NEWER
        /// <summary>
        /// Unity 2021.3 can hang inside TextureImporterInspector.OnInspectorGUI when it is nested by reflection.
        /// </summary>
        private const bool DefaultShowNativeInspector = false;

        /// <summary>
        /// Do not persist the dangerous expanded state in the affected Unity version.
        /// </summary>
        private const bool PersistNativeInspectorFoldout = false;
#else
        /// <summary>
        /// Keep the historical behavior outside the Unity 2021.3 compatibility path.
        /// </summary>
        private const bool DefaultShowNativeInspector = true;

        /// <summary>
        /// Persist the foldout state where the native inspector path is not known to hang.
        /// </summary>
        private const bool PersistNativeInspectorFoldout = true;
#endif

        /// <summary>
        /// Language options displayed in dropdown.
        /// </summary>
        private static readonly string[] LanguageOptions = { "中文", "English" };

        /// <summary>
        /// The native Unity editor used to render the <see cref="TextureImporter"/>'s Inspector.
        /// </summary>
        private UnityEditor.Editor nativeEditor;

        /// <summary>
        /// The style used to draw the section header text.
        /// </summary>
        private GUIStyle guiStyle;

        /// <summary>
        /// Whether to draw Unity's native texture importer settings for PSD assets.
        /// </summary>
        private bool showNativeInspector;

        /// <summary>
        /// Prevents repeatedly trying to create an internal Unity editor when reflection fails.
        /// </summary>
        private bool nativeEditorCreationFailed;

        /// <summary>
        /// Current inspector display language.
        /// </summary>
        private static InspectorLanguage CurrentLanguage { get; set; } = InspectorLanguage.Chinese;

        /// <summary>
        /// Called by Unity when any Texture file is first clicked on and the Inspector is populated.
        /// </summary>
        public void OnEnable()
        {
            showNativeInspector = PersistNativeInspectorFoldout
                ? EditorPrefs.GetBool(ShowNativeInspectorPrefKey, DefaultShowNativeInspector)
                : DefaultShowNativeInspector;

            PsdImporter.ApplyProjectOutputSettings(PsdLayoutProjectSettings.instance.ResolveOutputSettings());

            bool hasSavedUseUnityUI = EditorPrefs.HasKey(UseUnityUIPrefKey);
            PsdImporter.UseUnityUI = PsdImporterDefaults.ResolveUseUnityUI(
                hasSavedUseUnityUI,
                EditorPrefs.GetBool(UseUnityUIPrefKey, true));

            // Target Canvas and target-canvas scaling are intentionally not exposed by this Inspector.
            // Clear legacy preferences so an older UI selection cannot affect new generation runs.
            PsdImporter.TargetCanvasPath = string.Empty;
            PsdImporter.ScaleToTargetCanvas = false;
            PsdImporter.PreserveAspectWhenScalingToCanvas = true;

            if (EditorPrefs.HasKey(AutoAnchorByNamePrefKey))
            {
                PsdImporter.EnableAutoAnchorByName = EditorPrefs.GetBool(AutoAnchorByNamePrefKey, true);
            }

            if (EditorPrefs.HasKey(RootGlobalAnchorPrefKey))
            {
                PsdImporter.RootUseGlobalAnchorByDefault = EditorPrefs.GetBool(RootGlobalAnchorPrefKey, true);
            }

            if (EditorPrefs.HasKey(TextMeshProEnabledPrefKey))
            {
                PsdImporter.UseTextMeshPro = EditorPrefs.GetBool(TextMeshProEnabledPrefKey, true);
            }

            if (EditorPrefs.HasKey(LanguagePrefKey))
            {
                CurrentLanguage = (InspectorLanguage)EditorPrefs.GetInt(LanguagePrefKey, (int)InspectorLanguage.Chinese);
            }

            if (!IsPsdTarget() || showNativeInspector)
            {
                EnsureNativeEditor();
            }
        }

        /// <summary>
        /// Called when Unity destroys this custom Inspector.
        /// </summary>
        public void OnDisable()
        {
            DisposeNativeEditor();
        }

        /// <summary>
        /// Draws the Inspector GUI for the TextureImporter.
        /// Normal Texture files should appear as they normally do, however PSD files will have additional items.
        /// </summary>
        public override void OnInspectorGUI()
        {
            EnsureHeaderStyle();

            TextureImporter importer = target as TextureImporter;
            if (importer != null)
            {
                // check if it is a PSD file selected
                string assetPath = importer.assetPath;

                if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".psd", StringComparison.OrdinalIgnoreCase))
                {
                    PsdImporter.ApplyProjectOutputSettings(PsdLayoutProjectSettings.instance.ResolveOutputSettings());
                    GUIContent languageLabel = LocalizedContent(
                        "界面语言",
                        "Inspector Language",
                        "切换此插件 Inspector 的显示语言。",
                        "Switch the display language for this plugin inspector.");
                    int languageIndex = EditorGUILayout.Popup(languageLabel, (int)CurrentLanguage, LanguageOptions);
                    if (languageIndex != (int)CurrentLanguage)
                    {
                        CurrentLanguage = (InspectorLanguage)languageIndex;
                        EditorPrefs.SetInt(LanguagePrefKey, languageIndex);
                    }

                    GUILayout.Label(Localize("<b>PSD 布局工具 2</b>", "<b>PSD Layout Tool 2</b>"), guiStyle, GUILayout.Height(23));

                    EditorGUI.BeginChangeCheck();
                    GUIContent useUnityUILabel = LocalizedContent(
                        "使用 Unity UI",
                        "Use Unity UI",
                        "开启后生成 Canvas/Image/Text/Button 等 UI 对象。\n关闭后生成 SpriteRenderer/TextMesh 等普通场景对象。",
                        "When enabled, generates Canvas/Image/Text/Button UI objects.\nWhen disabled, generates regular scene objects like SpriteRenderer/TextMesh.");
                    PsdImporter.UseUnityUI = EditorGUILayout.Toggle(useUnityUILabel, PsdImporter.UseUnityUI);

                    if (PsdImporter.UseUnityUI)
                    {
                        GUIContent autoAnchorLabel = LocalizedContent(
                            "按名称自动设置锚点",
                            "Auto Anchor By Name",
                            "当图层或文件夹名称以 左上、左下、右上、右下、中间、左中、右中、上中、下中、上、下、左、右、全局 开头时，自动设置 UI 锚点。",
                            "Automatically sets UI anchors when a layer or folder name starts with 左上, 左下, 右上, 右下, 中间, 左中, 右中, 上中, 下中, 上, 下, 左, 右, or 全局.");
                        PsdImporter.EnableAutoAnchorByName = EditorGUILayout.Toggle(autoAnchorLabel, PsdImporter.EnableAutoAnchorByName);

                        GUIContent rootGlobalLabel = LocalizedContent(
                            "ROOT 默认使用全局锚点",
                            "Root Uses Global By Default",
                            "开启后，最外层导入根节点会自动全拉伸到父 Canvas，四边距为 0。",
                            "When enabled, the outermost generated root stretches to the parent canvas with zero margins.");
                        PsdImporter.RootUseGlobalAnchorByDefault = EditorGUILayout.Toggle(rootGlobalLabel, PsdImporter.RootUseGlobalAnchorByDefault);

                        GUIContent textMeshProEnabledLabel = LocalizedContent(
                            "使用 TMP 文本",
                            "Use TextMeshPro",
                            "启用后，PSD 文字会生成 TextMeshProUGUI，并使用下面选择的 TMP_FontAsset。关闭后回退到 Unity UI Text。",
                            "When enabled, PSD text layers use TextMeshProUGUI and the selected TMP_FontAsset. Disable to fall back to Unity UI Text.");
                        PsdImporter.UseTextMeshPro = EditorGUILayout.Toggle(textMeshProEnabledLabel, PsdImporter.UseTextMeshPro);

                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorPrefs.SetBool(UseUnityUIPrefKey, PsdImporter.UseUnityUI);
                        EditorPrefs.SetBool(AutoAnchorByNamePrefKey, PsdImporter.EnableAutoAnchorByName);
                        EditorPrefs.SetBool(RootGlobalAnchorPrefKey, PsdImporter.RootUseGlobalAnchorByDefault);
                        EditorPrefs.SetBool(TextMeshProEnabledPrefKey, PsdImporter.UseTextMeshPro);
                    }

                    EditorGUILayout.HelpBox(
                        Localize(
                            "提示：标签匹配不区分大小写。|Button 仅在启用 Unity UI 时生效，|Animation 仅在非 UI 模式生效。\n命名前缀必须写在名称开头，例如：左上关闭按钮、全局背景。\n上/下/左/右 会按单点锚点处理，不会做边缘拉伸；全局 会让 UI 节点四边距为 0，其中图片会额外按比例覆盖父节点。\n如果文件夹本身带锚点前缀，则其中没有前缀的子项会默认继承父级锚点。\n所有导入生成的 Unity UI Image 都会默认开启 Image.preserveAspect。",
                            "Tip: Tag matching is case-insensitive. |Button only works when Unity UI is enabled, and |Animation only works in non-UI mode.\nAnchor prefixes must be written at the start of the name, for example: 左上CloseButton or 全局Background.\n上/下/左/右 use point anchors instead of edge stretch; 全局 gives zero margins, and images additionally cover the parent while keeping aspect.\nIf a folder has an anchor prefix, child items without their own prefix inherit the parent's anchor.\nAll generated Unity UI Images enable Image.preserveAspect by default."),
                        MessageType.Info);

                    if (GUILayout.Button(
                            new GUIContent(
                                Localize("打开全局配置", "Open Global Settings"),
                                Localize(
                                    "选择项目中的 PSDLayoutProjectSettings 配置资产。输出规则、字体、材质和公共资源前缀都在该资产的 Inspector 中编辑。",
                                    "Selects the project PSDLayoutProjectSettings asset. Output rules, fonts, materials, and Common asset prefixes are edited in that asset Inspector.")),
                            GUILayout.Height(24)))
                    {
                        PsdLayoutProjectSettingsAsset.OpenInInspector();
                    }

                    string hierarchyTargetPath;
                    string hierarchyUnavailableReason;
                    bool hierarchyOrganizerAvailable = PsdHierarchyOrganizerEntry.TryResolvePrefabAvailability(
                        assetPath,
                        PsdImporter.OutputMode,
                        PsdImporter.OutputFolderName,
                        PsdImporter.PrefabMode,
                        path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null,
                        out hierarchyTargetPath,
                        out hierarchyUnavailableReason);

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(Localize("打开日志目录", "Open Log Folder")))
                    {
                        PsdLogger.RevealLogFolder();
                    }

                    if (GUILayout.Button(Localize("定位最新日志", "Reveal Latest Log")))
                    {
                        PsdLogger.RevealLatestLog();
                    }

                    EditorGUILayout.EndHorizontal();

                    if (GUILayout.Button(Localize("打开九宫图工具", "Open 9-Slice Tool")))
                    {
                        PsdNineSliceWindow.Open(AssetDatabase.GetAssetPath(Selection.activeObject));
                    }

                    if (GUILayout.Button(Localize("全量生成预制体", "Full Generate Prefab")))
                    {
                        GeneratePrefabWithMissingProfileRecovery(assetPath);
                    }

                    if (ShouldShowIncrementalUpdateButton(
                            PsdImporter.IsIncrementalPrefabUpdateAvailable(assetPath)) &&
                        GUILayout.Button(Localize("增量更新（保留整理）", "Incremental Update (Preserve Organization)")))
                    {
                        PsdImporter.UpdatePrefabIncrementally(assetPath);
                    }

                    EditorGUILayout.BeginHorizontal();
                    using (new EditorGUI.DisabledScope(!hierarchyOrganizerAvailable))
                    {
                        if (GUILayout.Button(
                                new GUIContent(
                                    Localize("定位 Prefab", "Ping Prefab"),
                                    Localize(
                                        "在 Project 窗口中高亮此 PSD 当前关联的 Prefab，保持当前 PSD Inspector 不变。",
                                        "Highlights this PSD's currently associated Prefab in the Project window without changing the current PSD Inspector.")),
                                GUILayout.Height(24)))
                        {
                            TryPingPrefab(hierarchyTargetPath);
                        }
                    }

                    if (hierarchyOrganizerAvailable && GUILayout.Button(
                            new GUIContent(
                                PsdHierarchyOrganizerEntry.AiButtonLabel,
                                "在 Unity 编辑器中打开 AI 对话窗口，并把整理技能与当前目标 Prefab 发送给 AI。"),
                            GUILayout.Height(24)))
                    {
                        string chatError;
                        if (!PsdHierarchyOrganizerEntry.TryOpenChat(assetPath, out chatError))
                        {
                            EditorUtility.DisplayDialog("PSDLayoutTool2", chatError, "确定");
                        }
                    }

                    EditorGUILayout.EndHorizontal();

                    GUILayout.Space(3);

                    GUILayout.Box(string.Empty, GUILayout.Height(1), GUILayout.MaxWidth(Screen.width - 30));

                    GUILayout.Space(3);

                    GUILayout.Label(Localize("<b>Unity 纹理导入设置</b>", "<b>Unity Texture Import Settings</b>"), guiStyle, GUILayout.Height(23));

                    DrawNativeInspectorFoldout();
                }
                else
                {
                    // It is a "normal" Texture, not a PSD
                    DrawNativeTextureInspector();
                }
            }

            // Unfortunately we cant hide the ImportedObject section because the interal InspectorWindow checks via
            // "if (editor is AssetImporterEditor)" and all flags that this check sets are method local variables
            // so aside from direct patching UnityEditor.dll, reflection cannot be used here.

            // Therefore we just move the ImportedObject section out of view
            ////GUILayout.Space(2048);
        }

        /// <summary>
        /// Localizes text based on current inspector language.
        /// </summary>
        /// <param name="chinese">Chinese text.</param>
        /// <param name="english">English text.</param>
        /// <returns>Localized text.</returns>
        private static void GeneratePrefabWithMissingProfileRecovery(string assetPath)
        {
            if (!PsdImporter.IsMissingHierarchyProfileRecoveryEligible(assetPath))
            {
                PsdImporter.GeneratePrefab(assetPath);
                return;
            }

            ConfirmAndRecoverMissingProfile(assetPath);
        }

        internal static bool ShouldShowIncrementalUpdateButton(bool isIncrementalEligible)
        {
            return isIncrementalEligible;
        }

        /// <summary>
        /// Highlights an associated Prefab in the Project window without
        /// changing the active selection, so the PSD Inspector remains open.
        /// </summary>
        internal static bool TryPingPrefab(string prefabPath)
        {
            UnityEngine.Object prefab = AssetDatabase.LoadMainAssetAtPath(prefabPath);
            if (prefab == null)
            {
                return false;
            }

            EditorGUIUtility.PingObject(prefab);
            return true;
        }

        private static void ConfirmAndRecoverMissingProfile(string assetPath)
        {
            if (!EditorUtility.DisplayDialog(
                    "PSDLayoutTool2",
                    Localize(
                        "这会归档失效的层级或清理回放 Profile，并以全新 Prefab 重新生成。旧 Profile 的本地 ID 不能用于新 Prefab；完成后需要再次整理层级。是否继续？",
                        "This archives the orphaned hierarchy or cleanup replay Profile and regenerates a new Prefab. The old Profile local IDs cannot be reused; organize the new Prefab again afterward. Continue?"),
                    Localize("归档并重新生成", "Archive and Regenerate"),
                    Localize("取消", "Cancel")))
                return;

            RecoverMissingProfileAndGeneratePrefab(assetPath);
        }

        private static void RecoverMissingProfileAndGeneratePrefab(string assetPath)
        {
            try
            {
                string archivedProfilePath;
                string failureReason;
                if (!PsdImporter.TryRecoverMissingHierarchyProfileAndGeneratePrefab(
                        assetPath, out archivedProfilePath, out failureReason))
                {
                    EditorUtility.DisplayDialog("PSDLayoutTool2", failureReason, Localize("确定", "OK"));
                    return;
                }

                EditorUtility.DisplayDialog(
                    "PSDLayoutTool2",
                    Localize(
                        "已归档失效 Profile：\n" + archivedProfilePath + "\n\n已开始重新生成 Prefab。完成后请重新整理层级。",
                        "Archived orphaned Profile:\n" + archivedProfilePath + "\n\nPrefab regeneration has started. Organize the hierarchy again when it completes."),
                    Localize("确定", "OK"));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("PSDLayoutTool2", exception.Message, Localize("确定", "OK"));
            }
        }

        private static string Localize(string chinese, string english)
        {
            return CurrentLanguage == InspectorLanguage.English ? english : chinese;
        }

        /// <summary>
        /// Creates localized GUI content with tooltip.
        /// </summary>
        /// <param name="chineseText">Chinese text.</param>
        /// <param name="englishText">English text.</param>
        /// <param name="chineseTooltip">Chinese tooltip.</param>
        /// <param name="englishTooltip">English tooltip.</param>
        /// <returns>Localized GUI content.</returns>
        private static GUIContent LocalizedContent(string chineseText, string englishText, string chineseTooltip, string englishTooltip)
        {
            return new GUIContent(Localize(chineseText, englishText), Localize(chineseTooltip, englishTooltip));
        }

        /// <summary>
        /// Ensures the header GUI style exists even during editor initialization edge cases.
        /// </summary>
        private void EnsureHeaderStyle()
        {
            if (guiStyle != null)
            {
                return;
            }

            GUIStyle baseStyle = EditorStyles.label;
            guiStyle = baseStyle != null ? new GUIStyle(baseStyle) : new GUIStyle();
            guiStyle.richText = true;
            guiStyle.fontSize = 14;
        }

        /// <summary>
        /// Draws the opt-in Unity texture importer settings section for PSD assets.
        /// </summary>
        private void DrawNativeInspectorFoldout()
        {
            bool newShowNativeInspector = EditorGUILayout.Foldout(
                showNativeInspector,
                Localize("显示 Unity 原生纹理设置", "Show Unity Texture Settings"),
                true);

            if (newShowNativeInspector != showNativeInspector)
            {
                showNativeInspector = newShowNativeInspector;
                if (PersistNativeInspectorFoldout)
                {
                    EditorPrefs.SetBool(ShowNativeInspectorPrefKey, showNativeInspector);
                }

                if (!showNativeInspector)
                {
                    DisposeNativeEditor();
                }
            }

            if (!showNativeInspector)
            {
                EditorGUILayout.HelpBox(
                    Localize(
                        "已默认收起 Unity 原生 TextureImporter 面板，以避开 Unity 2021.3 在嵌套绘制该面板时可能出现的长时间 Hold on。上方 PSD Layout Tool 2 按钮不依赖此面板。",
                        "Unity's native TextureImporter panel is collapsed by default to avoid a possible long Hold on in Unity 2021.3 when that internal panel is drawn through a nested editor. The PSD Layout Tool 2 buttons above do not depend on it."),
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                Localize(
                    "如果 Unity 在此区域长时间显示 Hold on，请重新选择该 PSD 后保持此区域收起。",
                    "If Unity shows a long Hold on in this section, reselect the PSD and keep this section collapsed."),
                MessageType.Warning);

            DrawNativeTextureInspector();
        }

        /// <summary>
        /// Draws Unity's built-in texture importer inspector, falling back to the generic inspector if reflection fails.
        /// </summary>
        private void DrawNativeTextureInspector()
        {
            if (EnsureNativeEditor())
            {
                nativeEditor.OnInspectorGUI();
                return;
            }

            DrawDefaultInspector();
        }

        /// <summary>
        /// Creates Unity's built-in texture importer inspector on demand.
        /// </summary>
        /// <returns>True if the native editor is available; otherwise false.</returns>
        private bool EnsureNativeEditor()
        {
            if (nativeEditor != null)
            {
                return true;
            }

            if (nativeEditorCreationFailed)
            {
                return false;
            }

            Type type = Type.GetType("UnityEditor.TextureImporterInspector, UnityEditor");
            if (type == null)
            {
                nativeEditorCreationFailed = true;
                return false;
            }

            try
            {
                nativeEditor = CreateEditor(target, type);
            }
            catch (Exception exception)
            {
                nativeEditorCreationFailed = true;
                Debug.LogWarning("PSDLayoutTool2 failed to create Unity's TextureImporterInspector: " + exception.Message);
            }

            return nativeEditor != null;
        }

        /// <summary>
        /// Destroys the reflected native editor to avoid keeping stale importer state alive.
        /// </summary>
        private void DisposeNativeEditor()
        {
            if (nativeEditor == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(nativeEditor);
            nativeEditor = null;
        }

        /// <summary>
        /// Returns whether the current target is a PSD texture importer.
        /// </summary>
        /// <returns>True for PSD importers; otherwise false.</returns>
        private bool IsPsdTarget()
        {
            TextureImporter importer = target as TextureImporter;
            string assetPath = importer != null ? importer.assetPath : string.Empty;
            return !string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".psd", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Finds a scene canvas by hierarchy path.
        /// </summary>
        /// <param name="path">Hierarchy path in the form "Root/Child".</param>
        /// <returns>Matching canvas if found; otherwise null.</returns>
        private static UnityEngine.Canvas FindCanvasByHierarchyPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            UnityEngine.Canvas[] canvases = FindAllCanvases();
            foreach (UnityEngine.Canvas canvas in canvases)
            {
                if (GetHierarchyPath(canvas.transform) == path)
                {
                    return canvas;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds all canvases in the loaded scene(s), using the newest available Unity API.
        /// </summary>
        /// <returns>Array of canvases.</returns>
        private static UnityEngine.Canvas[] FindAllCanvases()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>(FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType<UnityEngine.Canvas>();
#endif
        }

        /// <summary>
        /// Builds a hierarchy path for a transform in the form "Root/Child/SubChild".
        /// </summary>
        /// <param name="transform">Target transform.</param>
        /// <returns>Hierarchy path string.</returns>
        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}

