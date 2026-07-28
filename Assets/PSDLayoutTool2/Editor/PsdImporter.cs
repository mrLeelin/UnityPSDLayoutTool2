namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
    using PhotoshopFile;
    using UnityEditor;
    using UnityEditorInternal;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;
    using TMPro;

    /// <summary>
    /// Handles all of the importing for a PSD file (exporting textures, creating prefabs, etc).
    /// </summary>
    public static class PsdImporter
    {
        /// <summary>
        /// Controls where generated assets are saved.
        /// </summary>
        public enum OutputDirectoryMode
        {
            /// <summary>
            /// Save generated files into a subfolder next to the PSD.
            /// </summary>
            PsdDirectory,

            /// <summary>
            /// Save generated files into a subfolder under the Assets root.
            /// </summary>
            AssetsRoot,

            /// <summary>
            /// Allow generated asset types to use independently configured output folders.
            /// </summary>
            FixedPath
        }

        /// <summary>
        /// Controls where the generated prefab is saved.
        /// </summary>
        public enum PrefabOutputMode
        {
            /// <summary>
            /// Save the prefab next to the generated output folder (default).
            /// </summary>
            SiblingToOutputFolder,

            /// <summary>
            /// Save the prefab inside the generated output folder.
            /// </summary>
            InsideOutputFolder,

            /// <summary>
            /// Save the prefab into the explicitly configured Assets folder.
            /// </summary>
            CustomPath
        }

        public enum SpriteAtlasVersion
        {
            V1,
            V2
        }

        /// <summary>
        /// The current file path to use to save layers as .png files
        /// </summary>
        private static string currentPath;

        /// <summary>
        /// The <see cref="GameObject"/> representing the root PSD layer.  It contains all of the other layers as children GameObjects.
        /// </summary>
        private static GameObject rootPsdGameObject;

        /// <summary>
        /// The top-level object that should be saved as prefab or destroyed after import.
        /// </summary>
        private static GameObject importRootGameObject;

        /// <summary>
        /// The <see cref="GameObject"/> representing the current group (folder) we are processing.
        /// </summary>
        private static GameObject currentGroupGameObject;

        /// <summary>
        /// The current UI layout context used to place child RectTransforms.
        /// </summary>
        private static UiLayoutContext currentGroupLayoutContext;

        /// <summary>
        /// Embedded PSD 9-slice borders keyed by Photoshop's stable <c>lyid</c>.
        /// Values use Unity's left, bottom, right, top order.
        /// </summary>
        private static Dictionary<uint, Vector4> currentNineSliceBordersByLayerId;

        /// <summary>
        /// Artist-confirmed decisions saved from the PSD nine-slice editor.
        /// They are read once from the PSD asset meta data for the active
        /// import and take precedence over all authoring tags and XMP values.
        /// </summary>
        private static Dictionary<uint, PsdNineSliceOverride> currentManualNineSliceOverridesByLayerId;

        /// <summary>
        /// Borders calculated from bare PSD name tags while PNGs are generated.
        /// They only live for this import run; the PSD name remains the source
        /// of truth for future incremental updates.
        /// </summary>
        private static Dictionary<Layer, Vector4> currentAutomaticNineSliceBordersByLayer;
        private static HashSet<Layer> currentAutomaticNineSliceBordersInTargetCoordinates;

        /// <summary>
        /// Whether the embedded manifest is the authoritative 9-slice source
        /// for the current import. Older manifests retain layer-name fallback.
        /// </summary>
        private static bool useEmbeddedNineSliceMetadata;

        /// <summary>
        /// The current depth (Z axis position) that sprites will be placed on.  It is initialized to the MaximumDepth ("back" depth) and it is automatically
        /// decremented as the PSD file is processed, back to front.
        /// </summary>
        private static float currentDepth;

        /// <summary>
        /// The amount that the depth decrements for each layer.  This is automatically calculated from the number of layers in the PSD file and the MaximumDepth.
        /// </summary>
        private static float depthStep;

        /// <summary>
        /// Deterministic render order used for SpriteRenderer/TextMesh so layer order does not depend on camera angle.
        /// </summary>
        private static int currentSortingOrder;

        /// <summary>
        /// Stores explicit update selections for the active import run.
        /// If null or disabled, existing files are overwritten as before.
        /// </summary>
        private static HashSet<string> selectedUpdatePathsForCurrentImport;

        /// <summary>
        /// Indicates whether current import should respect explicit overwrite selections.
        /// </summary>
        private static bool useExplicitUpdateSelection;

        /// <summary>
        /// Prevents opening multiple conflict-selection dialogs at the same time.
        /// </summary>
        private static bool isConflictSelectionDialogOpen;

        /// <summary>
        /// Stores resolved import metadata for layers in the current import run.
        /// </summary>
        private static Dictionary<Layer, LayerImportInfo> currentLayerInfos;

        /// <summary>
        /// Generated Unity UI leaves keyed by Photoshop's durable layer ID for
        /// the active import only. The hierarchy organizer consumes a copy of
        /// this registry when Task 6 has loaded the existing target Prefab;
        /// this importer never applies a plan to, or saves, the temporary
        /// candidate hierarchy directly.
        /// </summary>
        private static Dictionary<string, RectTransform> currentGeneratedUiNodesByStableId;

        /// <summary>
        /// Total number of layers to export (for progress bar).
        /// </summary>
        private static int progressTotalLayers;

        /// <summary>
        /// Number of layers exported so far (for progress bar).
        /// </summary>
        private static int progressExportedLayers;

        /// <summary>
        /// Cached set of invalid filesystem characters for generated file and folder names.
        /// </summary>
        private static readonly HashSet<char> InvalidGeneratedNameChars = new HashSet<char>(
            Path.GetInvalidFileNameChars().Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' }));

        /// <summary>
        /// Reserved DOS device names that cannot be used as generated file or folder names on Windows.
        /// </summary>
        private static readonly HashSet<string> ReservedGeneratedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON",
            "PRN",
            "AUX",
            "NUL",
            "COM1",
            "COM2",
            "COM3",
            "COM4",
            "COM5",
            "COM6",
            "COM7",
            "COM8",
            "COM9",
            "LPT1",
            "LPT2",
            "LPT3",
            "LPT4",
            "LPT5",
            "LPT6",
            "LPT7",
            "LPT8",
            "LPT9"
        };

        /// <summary>
        /// Parses the PSD authoring tag in the order left, top, right, bottom.
        /// Both pipe tags and bracket tags are accepted so older PSD naming
        /// conventions can remain stable.
        /// </summary>
        private const string NineSliceTagPattern =
            @"(?:\|9slice\s*=\s*|\[9slice\s*:\s*)([0-9]+(?:\.[0-9]+)?)\s*,\s*([0-9]+(?:\.[0-9]+)?)\s*,\s*([0-9]+(?:\.[0-9]+)?)\s*,\s*([0-9]+(?:\.[0-9]+)?)\s*\]?";

        /// <summary>
        /// Represents how a button-group child should be interpreted.
        /// </summary>
        private enum ButtonChildRole
        {
            None,
            Default,
            Pressed,
            Highlighted,
            Disabled,
            TextImage
        }

        /// <summary>
        /// Supported anchor presets parsed from layer or folder names.
        /// </summary>
        private enum AnchorNamePreset
        {
            None,
            Global,
            TopLeft,
            BottomLeft,
            TopRight,
            BottomRight,
            Center,
            LeftMiddle,
            RightMiddle,
            TopMiddle,
            BottomMiddle
        }

        /// <summary>
        /// Describes how one parent RectTransform maps PSD space into its local space.
        /// </summary>
        private struct UiLayoutContext
        {
            /// <summary>
            /// Gets or sets the PSD-space rectangle represented by this layout context.
            /// </summary>
            public Rect PsdReferenceRect { get; set; }

            /// <summary>
            /// Gets or sets the full local rect size of the current parent RectTransform.
            /// </summary>
            public Vector2 LocalRectSize { get; set; }

            /// <summary>
            /// Gets or sets the PSD content display rect within the parent local space.
            /// </summary>
            public Rect LocalDisplayRect { get; set; }
        }

        /// <summary>
        /// Stores resolved import metadata for one PSD layer.
        /// </summary>
        private sealed class LayerImportInfo
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="LayerImportInfo"/> class.
            /// </summary>
            /// <param name="layer">PSD layer.</param>
            public LayerImportInfo(Layer layer)
            {
                Layer = layer;
            }

            /// <summary>
            /// Gets the source PSD layer.
            /// </summary>
            public Layer Layer { get; private set; }

            /// <summary>
            /// Gets or sets the resolved parent info.
            /// </summary>
            public LayerImportInfo Parent { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether this layer is visible after inheriting parent visibility.
            /// </summary>
            public bool EffectiveVisible { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether this layer behaves like a folder/group.
            /// </summary>
            public bool IsFolderLike { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether this layer is a |Button group.
            /// </summary>
            public bool IsButtonGroup { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether this layer is a |Animation group.
            /// </summary>
            public bool IsAnimationGroup { get; set; }

            /// <summary>
            /// Gets or sets the parsed button-child role when parent is a button group.
            /// </summary>
            public ButtonChildRole ButtonRole { get; set; }

            /// <summary>
            /// Gets or sets the unique stable name for this layer among siblings.
            /// </summary>
            public string UniqueSelfName { get; set; }

            /// <summary>
            /// Gets or sets the unique stable texture/file base name in the current output directory.
            /// </summary>
            public string UniqueTextureName { get; set; }

            /// <summary>
            /// Gets or sets the parsed animation frame rate.
            /// </summary>
            public float AnimationFps { get; set; }

            /// <summary>
            /// Gets or sets the parsed anchor preset from the source layer name.
            /// </summary>
            public AnchorNamePreset AnchorPreset { get; set; }

            /// <summary>
            /// Gets or sets the explicitly parsed anchor preset from the source layer name before inheritance.
            /// </summary>
            public AnchorNamePreset ExplicitAnchorPreset { get; set; }

            /// <summary>
            /// Gets or sets the resolved layout rect used for UI placement.
            /// </summary>
            public Rect LayoutRect { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether <see cref="LayoutRect"/> contains a usable rect.
            /// </summary>
            public bool HasLayoutRect { get; set; }
        }

        /// <summary>
        /// Initializes static members of the <see cref="PsdImporter"/> class.
        /// </summary>
        static PsdImporter()
        {
            MaximumDepth = 10;
            PixelsToUnits = 100;
            UseUnityUI = PsdImporterDefaults.ResolveUseUnityUI(false, false);
            UseTextMeshPro = true;
            OutputMode = OutputDirectoryMode.PsdDirectory;
            OutputFolderName = string.Empty;
            FixedOutputPath = string.Empty;
            AtlasOutputPath = string.Empty;
            TextureOutputPath = string.Empty;
            PrefabOutputPath = string.Empty;
            PrefabMode = PrefabOutputMode.SiblingToOutputFolder;
            AtlasVersion = SpriteAtlasVersion.V1;
            ScaleToTargetCanvas = false;
            PreserveAspectWhenScalingToCanvas = true;
            EnableAutoAnchorByName = true;
            RootUseGlobalAnchorByDefault = true;
        }

        /// <summary>
        /// Gets or sets the maximum depth.  This is where along the Z axis the back will be, with the front being at 0.
        /// </summary>
        public static float MaximumDepth { get; set; }

        /// <summary>
        /// Gets or sets the number of pixels per Unity unit value.  Defaults to 100 (which matches Unity's Sprite default).
        /// </summary>
        public static float PixelsToUnits { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use the Unity 4.6+ UI system or not.
        /// </summary>
        public static bool UseUnityUI { get; set; }

        /// <summary>
        /// Gets or sets whether Unity UI text layers are generated as TextMeshProUGUI.
        /// </summary>
        public static bool UseTextMeshPro { get; set; }

        /// <summary>
        /// Gets or sets the project-selected TMP font asset for generated PSD text.
        /// </summary>
        public static TMP_FontAsset TextMeshProFont { get; set; }

        /// <summary>
        /// Gets or sets the optional base TMP material used to create text-style materials.
        /// </summary>
        public static Material TextMeshProBaseMaterial { get; set; }

        private static bool tmpFontFallbackWarningEmitted;
        private static bool tmpBaseMaterialFallbackWarningEmitted;
        private static Dictionary<string, TMP_FontAsset> currentTmpFontFallbacksByPsdName;
        private static Dictionary<string, string> currentPngPathByContentHash;
        private static PsdTextureReuseIndex currentTextureReuseIndex;
        private static HashSet<string> currentPendingRedundantTexturePaths;
        private static string currentOutputRootDirectory;

        /// <summary>
        /// Gets or sets the hierarchy path of the target canvas to align generated UI under.
        /// Empty means creating a dedicated world-space canvas as before.
        /// </summary>
        public static string TargetCanvasPath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the generated UI should be scaled to the selected target canvas size.
        /// </summary>
        public static bool ScaleToTargetCanvas { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether scaling to target canvas should preserve PSD aspect ratio.
        /// </summary>
        public static bool PreserveAspectWhenScalingToCanvas { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether UI anchors should be inferred from layer names.
        /// </summary>
        public static bool EnableAutoAnchorByName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the generated UI root should default to a global stretch anchor.
        /// </summary>
        public static bool RootUseGlobalAnchorByDefault { get; set; }

        /// <summary>
        /// Gets or sets the generated files output mode.
        /// </summary>
        public static OutputDirectoryMode OutputMode { get; set; }

        /// <summary>
        /// Gets or sets the output folder name. Empty means using the PSD file name.
        /// </summary>
        public static string OutputFolderName { get; set; }

        /// <summary>
        /// Retained for serialized-settings compatibility. Generated output no longer uses a shared fixed root.
        /// </summary>
        public static string FixedOutputPath { get; set; }

        /// <summary>
        /// Gets or sets the generated atlas folder path under Assets.
        /// Empty means the conventional Atlas folder under the generated root.
        /// </summary>
        public static string AtlasOutputPath { get; set; }

        /// <summary>
        /// Gets or sets the generated texture folder path under Assets.
        /// Empty means the conventional Texture folder under the generated root.
        /// </summary>
        public static string TextureOutputPath { get; set; }

        /// <summary>
        /// Gets or sets the generated Prefab folder path under Assets.
        /// Empty means the conventional Prefab folder under the generated root.
        /// </summary>
        public static string PrefabOutputPath { get; set; }

        /// <summary>
        /// Gets or sets where the prefab is generated.
        /// </summary>
        public static PrefabOutputMode PrefabMode { get; set; }

        /// <summary>
        /// Gets or sets the Sprite Atlas asset format used for generated atlases.
        /// </summary>
        public static SpriteAtlasVersion AtlasVersion { get; set; }

        /// <summary>
        /// Resolves the exact generated Prefab selected by explicit import settings.
        /// The calculated path is returned even when no Prefab exists there, allowing
        /// callers to report the missing configured target without searching elsewhere.
        /// </summary>
        /// <param name="psdAssetPath">PSD asset path relative to the Unity project.</param>
        /// <param name="outputMode">Configured generated-output location.</param>
        /// <param name="outputFolderName">Configured generated-output folder name.</param>
        /// <param name="prefabMode">Configured Prefab location.</param>
        /// <param name="prefabAssetPath">Calculated configured Prefab path.</param>
        /// <returns>True only when a Prefab exists at the calculated path.</returns>
        public static bool TryResolveGeneratedPrefabPath(
            string psdAssetPath,
            OutputDirectoryMode outputMode,
            string outputFolderName,
            PrefabOutputMode prefabMode,
            out string prefabAssetPath)
        {
            return TryResolveGeneratedPrefabPath(
                psdAssetPath,
                outputMode,
                outputFolderName,
                string.Empty,
                prefabMode,
                string.Empty,
                out prefabAssetPath);
        }

        public static bool TryResolveGeneratedPrefabPath(
            string psdAssetPath,
            OutputDirectoryMode outputMode,
            string outputFolderName,
            string fixedOutputPath,
            PrefabOutputMode prefabMode,
            string prefabOutputPath,
            out string prefabAssetPath)
        {
            if (!PsdGeneratedPrefabPathResolver.TryResolve(
                    psdAssetPath,
                    outputMode,
                    outputFolderName,
                    fixedOutputPath,
                    prefabOutputPath,
                    prefabMode,
                    out prefabAssetPath))
            {
                return false;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath) != null;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the import process should create <see cref="GameObject"/>s in the scene.
        /// </summary>
        private static bool LayoutInScene { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the import process should create a prefab in the project's assets.
        /// </summary>
        private static bool CreatePrefab { get; set; }

        /// <summary>
        /// Gets or sets the size (in pixels) of the entire PSD canvas.
        /// </summary>
        private static Vector2 CanvasSize { get; set; }

        /// <summary>
        /// Gets or sets the name of the current 
        /// </summary>
        private static string PsdName { get; set; }

        /// <summary>
        /// Gets or sets the Unity 4.6+ UI canvas.
        /// </summary>
        private static GameObject Canvas { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether UI elements should use anchored-position placement for target canvas alignment.
        /// </summary>
        private static bool UseTargetCanvasCoordinates { get; set; }

        /// <summary>
        /// Gets or sets the reference canvas size used for target-canvas coordinate mapping.
        /// </summary>
        private static Vector2 TargetCanvasSize { get; set; }

        /// <summary>
        /// Gets or sets the current <see cref="PsdFile"/> that is being imported.
        /// </summary>
        ////private static PsdFile CurrentPsdFile { get; set; }

        /// <summary>
        /// Exports each of the art layers in the PSD file as separate textures (.png files) in the project's assets.
        /// </summary>
        /// <param name="assetPath">The path of to the .psd file relative to the project.</param>
        public static void ExportLayersAsTextures(string assetPath)
        {
            LayoutInScene = false;
            CreatePrefab = false;
            Import(assetPath);
        }

        /// <summary>
        /// Lays out sprites in the current scene to match the PSD's layout.  Each layer is exported as Sprite-type textures in the project's assets.
        /// </summary>
        /// <param name="assetPath">The path of to the .psd file relative to the project.</param>
        public static void LayoutInCurrentScene(string assetPath)
        {
            LayoutInScene = true;
            CreatePrefab = false;
            Import(assetPath);
        }

        /// <summary>
        /// Generates a prefab consisting of sprites laid out to match the PSD's layout. Each layer is exported as Sprite-type textures in the project's assets.
        /// </summary>
        /// <param name="assetPath">The path of to the .psd file relative to the project.</param>
        public static void GeneratePrefab(string assetPath)
        {
            LayoutInScene = false;
            CreatePrefab = true;
            Import(assetPath);
        }

        /// <summary>
        /// Explicitly resets an orphaned hierarchy Profile after its recorded
        /// Prefab and the currently configured output Prefab are both missing.
        /// The Profile is archived first; the subsequent import is a new
        /// generation and must be organized again before incremental updates.
        /// </summary>
        public static bool TryRecoverMissingHierarchyProfileAndGeneratePrefab(
            string assetPath,
            out string archivedProfilePath,
            out string failureReason)
        {
            archivedProfilePath = string.Empty;
            failureReason = string.Empty;
            if (string.IsNullOrEmpty(assetPath))
            {
                failureReason = "PSD asset path is required.";
                return false;
            }

            string normalizedAssetPath = assetPath.Replace('\\', '/');
            string sourceGuid = AssetDatabase.AssetPathToGUID(normalizedAssetPath);
            string prefabPath;
            if (string.IsNullOrEmpty(sourceGuid) ||
                !PsdGeneratedPrefabPathResolver.TryResolve(
                    normalizedAssetPath,
                    OutputMode,
                    OutputFolderName,
                    FixedOutputPath,
                    PrefabOutputPath,
                    PrefabMode,
                    out prefabPath))
            {
                failureReason = "Cannot resolve the PSD identity or configured Prefab output path.";
                return false;
            }

            string profilePath = PsdPrefabTransactionalSave.GetProfilePath(prefabPath, sourceGuid);
            if (!PsdPrefabTransactionalSave.TryArchiveProfileForMissingTargetRecovery(
                    profilePath, prefabPath, out archivedProfilePath, out failureReason))
                return false;

            GeneratePrefab(normalizedAssetPath);
            return true;
        }

        /// <summary>
        /// Checks whether the active Profile for a PSD is an orphan whose
        /// recorded target and configured output Prefab are both absent.
        /// </summary>
        public static bool IsMissingHierarchyProfileRecoveryEligible(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;

            string normalizedAssetPath = assetPath.Replace('\\', '/');
            string sourceGuid = AssetDatabase.AssetPathToGUID(normalizedAssetPath);
            string prefabPath;
            if (string.IsNullOrEmpty(sourceGuid) ||
                !PsdGeneratedPrefabPathResolver.TryResolve(
                    normalizedAssetPath,
                    OutputMode,
                    OutputFolderName,
                    FixedOutputPath,
                    PrefabOutputPath,
                    PrefabMode,
                    out prefabPath))
                return false;

            string profilePath = PsdPrefabTransactionalSave.GetProfilePath(prefabPath, sourceGuid);
            return PsdPrefabTransactionalSave.IsMissingTargetRecoveryEligible(profilePath, prefabPath);
        }

        /// <summary>
        /// Gets a readable label for the active import mode.
        /// </summary>
        /// <returns>Import mode label.</returns>
        private static string GetImportModeName()
        {
            if (CreatePrefab)
            {
                return UseUnityUI ? "Generate Prefab (Unity UI)" : "Generate Prefab (Scene Objects)";
            }

            if (LayoutInScene)
            {
                return UseUnityUI ? "Layout In Current Scene (Unity UI)" : "Layout In Current Scene (Scene Objects)";
            }

            return "Export Layers As Textures";
        }

        /// <summary>
        /// Imports a Photoshop document (.psd) file at the given path.
        /// </summary>
        /// <param name="asset">The path of to the .psd file relative to the project.</param>
        private static void Import(string asset)
        {
            Import(asset, null, false);
        }

        /// <summary>
        /// Imports a Photoshop document (.psd) file at the given path with optional preselected conflict handling.
        /// </summary>
        /// <param name="asset">The path of to the .psd file relative to the project.</param>
        /// <param name="forcedSelection">Preselected conflict actions. Null means no explicit selection.</param>
        /// <param name="skipConflictPrompt">True to bypass conflict prompts and apply <paramref name="forcedSelection"/> directly.</param>
        private static void Import(string asset, ImportConflictSelection forcedSelection, bool skipConflictPrompt)
        {
            PsdLogger.BeginImportSession(asset, GetImportModeName(), skipConflictPrompt);
            string sessionResult = "Completed";
            try
            {
                PsdLogger.Step("Initialize import state");
                currentDepth = MaximumDepth;
                currentSortingOrder = 0;
                UseTargetCanvasCoordinates = false;
                currentLayerInfos = null;
                currentNineSliceBordersByLayerId = new Dictionary<uint, Vector4>();
                currentManualNineSliceOverridesByLayerId = new Dictionary<uint, PsdNineSliceOverride>();
                currentAutomaticNineSliceBordersByLayer = new Dictionary<Layer, Vector4>();
                currentAutomaticNineSliceBordersInTargetCoordinates = new HashSet<Layer>();
                useEmbeddedNineSliceMetadata = false;
                tmpFontFallbackWarningEmitted = false;
                tmpBaseMaterialFallbackWarningEmitted = false;
                PsdLayoutProjectFontSnapshot projectFontSettings =
                    PsdLayoutProjectSettings.instance.ResolveFontSettings();
                ApplyProjectFontSettings(projectFontSettings);
                LogProjectFontSettingsWarnings(projectFontSettings);
                ApplyProjectOutputSettings(PsdLayoutProjectSettings.instance.ResolveOutputSettings());
                currentTmpFontFallbacksByPsdName = new Dictionary<string, TMP_FontAsset>(StringComparer.OrdinalIgnoreCase);
                currentPngPathByContentHash = new Dictionary<string, string>(StringComparer.Ordinal);
                currentTextureReuseIndex = new PsdTextureReuseIndex();
                currentPendingRedundantTexturePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                currentOutputRootDirectory = string.Empty;
                string normalizedAssetPath = asset.Replace('\\', '/');
                string fullPath = Path.Combine(GetFullProjectPath(), normalizedAssetPath);

                PsdLogger.Step("Read PSD file: " + fullPath);
                LogPsdFilePreflight(fullPath);
                PsdFile psd = new PsdFile(fullPath);
                ConfigureManualNineSliceOverrides(normalizedAssetPath);
                CanvasSize = new Vector2(psd.Width, psd.Height);
                TargetCanvasSize = CanvasSize;
                PsdLogger.Info("PSD loaded. Size=" + psd.Width + "x" + psd.Height + ", layers=" + psd.Layers.Count);
                if (psd.EmbeddedLayoutManifest != null)
                {
                    PsdLogger.Info(
                        "Embedded PSD layout manifest loaded. fingerprint=" +
                        psd.EmbeddedLayoutManifest.documentFingerprint +
                        ", manifestLayers=" + psd.EmbeddedLayoutManifest.layers.Length);
                }
                else
                {
                    PsdLogger.Info("No embedded PSD layout manifest found. Falling back to native PSD parsing.");
                }

                ConfigureEmbeddedNineSliceBorders(psd.EmbeddedLayoutManifest);

                PsdPrefabDocumentModel sourceModel = PsdPrefabModelBuilder.Build(psd);

                // Set the depth step based on the layer count.  If there are no layers, default to 0.1f.
                depthStep = psd.Layers.Count != 0 ? MaximumDepth / psd.Layers.Count : 0.1f;

                PsdName = Path.GetFileNameWithoutExtension(normalizedAssetPath);

                string outputRelativePath;
                if (!PsdGeneratedPrefabPathResolver.TryResolveOutputRoot(
                        normalizedAssetPath,
                        OutputMode,
                        OutputFolderName,
                        FixedOutputPath,
                        out outputRelativePath))
                {
                    throw new InvalidOperationException("Cannot resolve the generated output path for PSD asset: " + normalizedAssetPath);
                }

                string outputFullPath = Path.Combine(GetFullProjectPath(), outputRelativePath.Replace('/', Path.DirectorySeparatorChar));
                string atlasRelativePath;
                string textureRelativePath;
                string prefabFolderRelativePath;
                if (!PsdGeneratedPrefabPathResolver.TryResolveContentFolders(
                        normalizedAssetPath,
                        OutputMode,
                        OutputFolderName,
                        FixedOutputPath,
                        AtlasOutputPath,
                        TextureOutputPath,
                        PrefabOutputPath,
                        out atlasRelativePath,
                        out textureRelativePath,
                        out prefabFolderRelativePath))
                {
                    throw new InvalidOperationException("Cannot resolve generated content folders for PSD asset: " + normalizedAssetPath);
                }

                string prefabRelativePath = string.Empty;
                if (CreatePrefab &&
                    !PsdGeneratedPrefabPathResolver.TryResolve(
                        normalizedAssetPath,
                        OutputMode,
                        OutputFolderName,
                        FixedOutputPath,
                        PrefabOutputPath,
                        PrefabMode,
                        out prefabRelativePath))
                {
                    throw new InvalidOperationException("Cannot resolve the generated Prefab path for PSD asset: " + normalizedAssetPath);
                }
                PsdLogger.Info("Output relative path: " + outputRelativePath);
                PsdLogger.Info("Output full path: " + outputFullPath);
                PsdLogger.Info("Atlas folder: " + atlasRelativePath);
                PsdLogger.Info("Texture folder: " + textureRelativePath);
                PsdLogger.Info("Prefab folder: " + prefabFolderRelativePath);
                if (!string.IsNullOrEmpty(prefabRelativePath))
                {
                    PsdLogger.Info("Prefab path: " + prefabRelativePath);
                }

                // Resolve Profile ownership before conflict analysis or any
                // stale-file deletion. A known Profile with a missing target is
                // an error, never permission to fall back to whole-tree save.
                PsdHierarchyProfile boundHierarchyProfile = null;
                PsdHierarchyCleanupReplayProfile boundCleanupReplayProfile = null;
                string sourceGuid = string.Empty;
                if (CreatePrefab && !string.IsNullOrEmpty(prefabRelativePath))
                {
                    sourceGuid = AssetDatabase.AssetPathToGUID(normalizedAssetPath);
                    boundHierarchyProfile = ResolveHierarchyProfileBeforePrefabImport(
                        sourceGuid, prefabRelativePath, UseUnityUI);
                    boundCleanupReplayProfile = PsdHierarchyCleanupReplayProfile.Load(
                        prefabRelativePath,
                        sourceGuid);
                }

                var conversionContext = new PsdPrefabConversionContext
                {
                    source = sourceModel,
                    previous = boundHierarchyProfile != null
                        ? boundHierarchyProfile.BuildPreviousDocument()
                        : null
                };
                PsdPrefabConversionPlan conversionPlan = new PsdPrefabConversionPipeline().CreatePlan(conversionContext);
                PsdLogger.Info(
                    "Conversion plan created. nodes=" + sourceModel.nodes.Count +
                    ", added=" + conversionPlan.Count(PsdPrefabChangeKind.Added) +
                    ", updated=" + conversionPlan.Count(PsdPrefabChangeKind.Updated) +
                    ", removed=" + conversionPlan.Count(PsdPrefabChangeKind.Removed));

                if (CreatePrefab && IsTargetPrefabOpenInPrefabMode(prefabRelativePath))
                {
                    SchedulePrefabModeExitAndResumeImport(
                        asset,
                        forcedSelection,
                        skipConflictPrompt,
                        prefabRelativePath);
                    sessionResult = "Waiting for target Prefab Mode to close";
                    currentLayerInfos = null;
                    return;
                }

                PsdLogger.Step("Build layer tree");
                List<Layer> tree = BuildLayerTree(psd.Layers) ?? new List<Layer>();
                currentLayerInfos = BuildLayerImportInfoMap(tree);
                ValidateCommonLibraryReferences(tree);
                bool hasVisibleRuntimeObjects = HasVisibleRuntimeContent(tree);
                PsdLogger.Info("Layer tree root count=" + tree.Count + ", hasVisibleRuntimeObjects=" + hasVisibleRuntimeObjects);

                PsdLogger.Step("Analyze existing generated targets");
                ImportConflictAnalysis conflictAnalysis = AnalyzeImportConflicts(
                    tree,
                    outputRelativePath,
                    outputFullPath,
                    textureRelativePath,
                    prefabRelativePath,
                    hasVisibleRuntimeObjects);
                var protectedCleanupReplayPaths = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                if (boundCleanupReplayProfile != null)
                {
                    if (!boundCleanupReplayProfile.TryGetProtectedRenameTargets(
                            sourceGuid,
                            prefabRelativePath,
                            out IReadOnlyList<string> protectedAssetPaths,
                            out string protectedAssetError))
                        throw new InvalidOperationException(
                            "Cleanup replay asset protection could not be verified: " +
                            protectedAssetError);
                    foreach (string protectedAssetPath in protectedAssetPaths)
                    {
                        string protectedFullPath = NormalizePath(Path.Combine(
                            GetFullProjectPath(),
                            protectedAssetPath.Replace('/', Path.DirectorySeparatorChar)));
                        protectedCleanupReplayPaths.Add(protectedFullPath);
                        conflictAnalysis.DeletedPaths.RemoveAll(path =>
                            string.Equals(
                                NormalizePath(path),
                                protectedFullPath,
                                StringComparison.OrdinalIgnoreCase));
                    }
                }
                PsdLogger.Info(
                    "Conflict analysis: hasExistingTargets=" + conflictAnalysis.HasExistingTargets +
                    ", sameName=" + conflictAnalysis.SameNamePaths.Count +
                    ", stale=" + conflictAnalysis.DeletedPaths.Count +
                    ", hasSelectableEntries=" + conflictAnalysis.HasSelectableEntries);

                ImportConflictSelection effectiveSelection = forcedSelection;
                if (!skipConflictPrompt && conflictAnalysis.HasExistingTargets)
                {
                    PsdLogger.Step("Prompt user to update existing targets");
                    bool updateExistingFiles = PromptForUpdatingExistingFiles(conflictAnalysis);
                    PsdLogger.Info("Update existing targets answer: " + updateExistingFiles);
                    if (!updateExistingFiles)
                    {
                        sessionResult = "Canceled by user at existing-target prompt";
                        currentLayerInfos = null;
                        return;
                    }

                    if (conflictAnalysis.HasSelectableEntries)
                    {
                        if (isConflictSelectionDialogOpen)
                        {
                            PsdLogger.Warning("Skipped import because a conflict selection window is already open.");
                            EditorUtility.DisplayDialog(
                                "PSDLayoutTool2",
                                "已有一个更新/删除确认窗口正在打开，请先完成该操作。",
                                "确定");
                            sessionResult = "Skipped because conflict selection window is already open";
                            currentLayerInfos = null;
                            return;
                        }

                        isConflictSelectionDialogOpen = true;
                        ImportConflictSelection defaultSelection = CreateDefaultConflictSelection(conflictAnalysis);
                        PsdLogger.Step("Open conflict selection window");
                        ImportConflictSelectionWindow.ShowDialog(
                            conflictAnalysis,
                            defaultSelection,
                            selection =>
                            {
                                isConflictSelectionDialogOpen = false;
                                if (selection == null || !selection.Confirmed)
                                {
                                    return;
                                }

                                Import(asset, selection, true);
                            });
                        sessionResult = "Waiting for conflict selection";
                        currentLayerInfos = null;
                        return;
                    }

                    effectiveSelection = CreateDefaultConflictSelection(conflictAnalysis);
                }

                ConfigureCurrentImportSelection(effectiveSelection);
                if (effectiveSelection != null)
                {
                    PsdLogger.Info(
                        "Effective conflict selection: update=" + effectiveSelection.PathsToUpdate.Count +
                        ", delete=" + effectiveSelection.PathsToDelete.Count);
                }

                currentPath = outputFullPath;
                currentOutputRootDirectory = outputFullPath;
                PsdLogger.Step("Create output directories under: " + currentPath);
                Directory.CreateDirectory(currentPath);
                Directory.CreateDirectory(Path.Combine(
                    GetFullProjectPath(), textureRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (CreatePrefab)
                {
                    Directory.CreateDirectory(Path.Combine(
                        GetFullProjectPath(), atlasRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                    Directory.CreateDirectory(Path.Combine(
                        GetFullProjectPath(), prefabFolderRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                }

                if (effectiveSelection != null)
                {
                    effectiveSelection.PathsToDelete.ExceptWith(protectedCleanupReplayPaths);
                    if (boundHierarchyProfile != null &&
                        effectiveSelection.PathsToDelete.Contains(NormalizePath(conflictAnalysis.PrefabFullPath)))
                        throw new InvalidOperationException(
                            "Cannot delete a Prefab that is bound to an incremental hierarchy Profile.");
                    PsdLogger.Step("Delete selected stale files: " + effectiveSelection.PathsToDelete.Count);
                    DeleteSelectedFiles(
                        effectiveSelection.PathsToDelete,
                        outputFullPath,
                        textureRelativePath,
                        conflictAnalysis.PrefabFullPath);
                }

                rootPsdGameObject = null;
                importRootGameObject = null;
                currentGroupGameObject = null;
                currentGroupLayoutContext = default(UiLayoutContext);
                BeginGeneratedUiNodeRegistry(UseUnityUI);

                if ((LayoutInScene || CreatePrefab) && hasVisibleRuntimeObjects)
                {
                    if (UseUnityUI)
                    {
                        PsdLogger.Step("Create or resolve Unity UI root");
                        CreateUIEventSystem();
                        Canvas targetCanvas = ResolveTargetCanvas();
                        if (targetCanvas != null)
                        {
                            UseTargetCanvasCoordinates = true;
                            TargetCanvasSize = GetTargetCanvasRectSize(targetCanvas);
                            PsdLogger.Info("Using target canvas: " + GetHierarchyPath(targetCanvas.transform) + ", size=" + TargetCanvasSize);
                            rootPsdGameObject = new GameObject(PsdName, typeof(RectTransform));
                            RectTransform rootRect = rootPsdGameObject.GetComponent<RectTransform>();
                            rootRect.SetParent(targetCanvas.transform, false);
                            currentGroupLayoutContext = ApplyRootUILayout(rootRect);
                            importRootGameObject = rootPsdGameObject;
                        }
                        else
                        {
                            PsdLogger.Info("No target canvas found. Creating a dedicated UI canvas.");
                            CreateUICanvas();
                            rootPsdGameObject = new GameObject(PsdName, typeof(RectTransform));
                            RectTransform rootRect = rootPsdGameObject.GetComponent<RectTransform>();
                            rootRect.SetParent(Canvas.transform, false);
                            currentGroupLayoutContext = ApplyRootUILayout(rootRect);
                            importRootGameObject = Canvas;
                        }
                    }
                    else
                    {
                        PsdLogger.Step("Create scene root object: " + PsdName);
                        rootPsdGameObject = new GameObject(PsdName);
                        importRootGameObject = rootPsdGameObject;
                    }

                    currentGroupGameObject = rootPsdGameObject;
                }

                PsdLogger.Step("Export layer tree");
                progressTotalLayers = CountAllLayers(tree);
                progressExportedLayers = 0;
                ExportTree(tree);

                if (CreatePrefab && importRootGameObject != null)
                {
                    if (ShouldSavePrefab(prefabRelativePath))
                    {
                        PsdLogger.Step("Save prefab: " + prefabRelativePath);
                        EditorUtility.DisplayProgressBar("PSD Layout Tool 2", "保存 Prefab...", 0.95f);
                        bool replayStaged = PsdHierarchyCleanupReplayCoordinator.TryStageAndSchedule(
                            normalizedAssetPath,
                            prefabRelativePath,
                            importRootGameObject,
                            out string replayStageError);
                        if (!replayStaged && !string.IsNullOrEmpty(replayStageError))
                        {
                            throw new InvalidOperationException(
                                "Cleanup replay could not be staged; the existing organized Prefab was kept unchanged. " +
                                replayStageError);
                        }
                        if (!replayStaged && !TrySaveIncrementalHierarchyPrefab(
                                normalizedAssetPath,
                                sourceModel,
                                conversionPlan.changes,
                                prefabRelativePath,
                                importRootGameObject,
                                CaptureGeneratedUiNodeRegistry()))
                        {
                            // Absence of a hierarchy Profile deliberately keeps
                            // the established importer behavior. Once a valid
                            // Profile exists, failures are never downgraded to
                            // this destructive whole-candidate save path.
                            PrefabUtility.SaveAsPrefabAsset(importRootGameObject, prefabRelativePath);
                        }
                    }
                    else
                    {
                        PsdLogger.Info("Skip prefab save because overwrite selection does not allow it: " + prefabRelativePath);
                    }

                    if (!LayoutInScene && importRootGameObject != null)
                    {
                        // if we are not flagged to layout in the scene, delete the GameObject used to generate the prefab
                        UnityEngine.Object.DestroyImmediate(importRootGameObject);
                    }
                }

                PsdLogger.Step("Refresh AssetDatabase");
                EditorUtility.DisplayProgressBar("PSD Layout Tool 2", "刷新 AssetDatabase...", 0.98f);
                AssetDatabase.ImportAsset(outputRelativePath, ImportAssetOptions.ForceSynchronousImport);
                if (!string.Equals(outputRelativePath, textureRelativePath, StringComparison.OrdinalIgnoreCase))
                {
                    AssetDatabase.ImportAsset(textureRelativePath, ImportAssetOptions.ForceSynchronousImport);
                }
                FinalizeRedundantTextureCleanup(prefabRelativePath);
                if (CreatePrefab)
                {
                    string atlasExtension = AtlasVersion == SpriteAtlasVersion.V2
                        ? ".spriteatlasv2"
                        : ".spriteatlas";
                    string atlasAssetName = Path.GetFileNameWithoutExtension(prefabRelativePath) + atlasExtension;
                    string atlasAssetPath = atlasRelativePath.TrimEnd('/') + "/" + atlasAssetName;
                    PsdLogger.Step("Create or update SpriteAtlas " + AtlasVersion + ": " + atlasAssetPath);
                    PsdGeneratedSpriteAtlas.CreateOrUpdate(atlasAssetPath, textureRelativePath, AtlasVersion);
                    if (AtlasVersion == SpriteAtlasVersion.V2 &&
                        EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2 &&
                        EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2Build)
                    {
                        PsdLogger.Warning(
                            "SpriteAtlas V2 was created, but Sprite Packer Mode is not Sprite Atlas V2. " +
                            "Enable it in Project Settings > Editor when atlas packing is required.");
                    }
                }
            }
            catch (Exception exception)
            {
                sessionResult = "Failed";
                PsdLogger.Exception("Import failed while processing asset: " + asset, exception);
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                ClearCurrentImportSelection();
                currentLayerInfos = null;
                EndGeneratedUiNodeRegistry();
                PsdLogger.EndImportSession(sessionResult);
            }
        }

        /// <summary>
        /// Compares generated texture outputs against existing files to compute update/delete candidates.
        /// </summary>
        /// <param name="tree">Layer tree for current PSD import.</param>
        /// <param name="outputRelativePath">Output directory relative to project.</param>
        /// <param name="outputFullPath">Output directory absolute path.</param>
        /// <param name="prefabRelativePath">Prefab path relative to project.</param>
        /// <returns>Conflict analysis data for this import run.</returns>
        private static ImportConflictAnalysis AnalyzeImportConflicts(
            List<Layer> tree,
            string outputRelativePath,
            string outputFullPath,
            string textureRelativePath,
            string prefabRelativePath,
            bool hasVisibleRuntimeObjects)
        {
            ImportConflictAnalysis analysis = new ImportConflictAnalysis();
            analysis.OutputRelativePath = outputRelativePath;
            analysis.OutputFullPath = NormalizePath(outputFullPath);
            analysis.PrefabRelativePath = prefabRelativePath;
            if (!string.IsNullOrEmpty(prefabRelativePath))
            {
                analysis.PrefabFullPath = NormalizePath(
                    Path.Combine(GetFullProjectPath(), prefabRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            }

            analysis.HasExistingOutputDirectory = Directory.Exists(outputFullPath);
            analysis.HasExistingPrefab = !string.IsNullOrEmpty(analysis.PrefabFullPath) && File.Exists(analysis.PrefabFullPath);

            HashSet<string> generatedAssetPaths = CollectExpectedGeneratedAssetPaths(
                tree,
                outputFullPath,
                analysis.PrefabFullPath,
                hasVisibleRuntimeObjects);
            HashSet<string> existingGeneratedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> generatedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NormalizePath(outputFullPath),
                NormalizePath(Path.Combine(GetFullProjectPath(), textureRelativePath.Replace('/', Path.DirectorySeparatorChar)))
            };
            foreach (string generatedDirectory in generatedDirectories)
            {
                if (!Directory.Exists(generatedDirectory))
                {
                    continue;
                }

                string[] existingFiles = Directory.GetFiles(generatedDirectory, "*.*", SearchOption.AllDirectories);
                foreach (string existingFile in existingFiles)
                {
                    string extension = Path.GetExtension(existingFile);
                    if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(extension, ".anim", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(extension, ".controller", StringComparison.OrdinalIgnoreCase))
                    {
                        existingGeneratedPaths.Add(NormalizePath(existingFile));
                    }
                }
            }

            if (analysis.HasExistingPrefab)
            {
                existingGeneratedPaths.Add(NormalizePath(analysis.PrefabFullPath));
            }

            foreach (string existingFile in existingGeneratedPaths)
            {
                if (generatedAssetPaths.Contains(existingFile))
                {
                    analysis.SameNamePaths.Add(existingFile);
                }
                else
                {
                    analysis.DeletedPaths.Add(existingFile);
                }
            }

            List<string> sortedSameNamePaths = analysis.SameNamePaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => ToDisplayPath(path), StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<string> sortedDeletedPaths = analysis.DeletedPaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => ToDisplayPath(path), StringComparer.OrdinalIgnoreCase)
                .ToList();

            analysis.SameNamePaths.Clear();
            analysis.SameNamePaths.AddRange(sortedSameNamePaths);
            analysis.DeletedPaths.Clear();
            analysis.DeletedPaths.AddRange(sortedDeletedPaths);

            return analysis;
        }

        /// <summary>
        /// Creates the default conflict selection, which updates same-name files and deletes stale files.
        /// </summary>
        /// <param name="analysis">Current conflict analysis.</param>
        /// <returns>Default conflict selection.</returns>
        private static ImportConflictSelection CreateDefaultConflictSelection(ImportConflictAnalysis analysis)
        {
            ImportConflictSelection selection = new ImportConflictSelection
            {
                Confirmed = true
            };

            foreach (string path in analysis.SameNamePaths)
            {
                if (!string.Equals(Path.GetExtension(path), ".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    selection.PathsToUpdate.Add(NormalizePath(path));
                }
            }

            foreach (string path in analysis.DeletedPaths)
            {
                selection.PathsToDelete.Add(NormalizePath(path));
            }

            return selection;
        }

        /// <summary>
        /// Prompts the user to confirm whether existing targets should be updated.
        /// </summary>
        /// <param name="analysis">Current conflict analysis.</param>
        /// <returns>True if user wants to continue updating; otherwise false.</returns>
        private static bool PromptForUpdatingExistingFiles(ImportConflictAnalysis analysis)
        {
            StringBuilder messageBuilder = new StringBuilder();
            messageBuilder.AppendLine("检测到已有同名导入目标：");
            if (analysis.HasExistingOutputDirectory)
            {
                messageBuilder.AppendLine("输出目录: " + analysis.OutputRelativePath);
            }

            if (analysis.HasExistingPrefab)
            {
                messageBuilder.AppendLine("预制体: " + analysis.PrefabRelativePath);
            }

            messageBuilder.AppendLine();
            messageBuilder.AppendLine("是否要更新现有文件？");

            return EditorUtility.DisplayDialog(
                "PSDLayoutTool2",
                messageBuilder.ToString(),
                "更新",
                "取消");
        }

        /// <summary>
        /// Configures overwrite-selection state for the active import run.
        /// </summary>
        /// <param name="selection">Selected update/delete actions for this run.</param>
        private static void ConfigureCurrentImportSelection(ImportConflictSelection selection)
        {
            useExplicitUpdateSelection = selection != null;
            selectedUpdatePathsForCurrentImport = selection != null
                ? new HashSet<string>(selection.PathsToUpdate, StringComparer.OrdinalIgnoreCase)
                : null;
        }

        /// <summary>
        /// Clears overwrite-selection state after an import run ends.
        /// </summary>
        private static void ClearCurrentImportSelection()
        {
            useExplicitUpdateSelection = false;
            selectedUpdatePathsForCurrentImport = null;
        }

        /// <summary>
        /// Deletes selected stale generated files and their meta files.
        /// </summary>
        /// <param name="pathsToDelete">Files selected for deletion.</param>
        /// <param name="outputFullPath">Import output root path.</param>
        /// <param name="prefabFullPath">Resolved prefab full path, if any.</param>
        private static void DeleteSelectedFiles(
            HashSet<string> pathsToDelete,
            string outputFullPath,
            string textureRelativePath,
            string prefabFullPath)
        {
            if (pathsToDelete == null || pathsToDelete.Count == 0)
            {
                return;
            }

            string normalizedRoot = NormalizePath(outputFullPath).TrimEnd('/');
            string normalizedTextureRoot = NormalizePath(Path.Combine(
                GetFullProjectPath(),
                textureRelativePath.Replace('/', Path.DirectorySeparatorChar))).TrimEnd('/');
            string normalizedPrefabPath = string.IsNullOrEmpty(prefabFullPath) ? string.Empty : NormalizePath(prefabFullPath);

            foreach (string selectedPath in pathsToDelete)
            {
                string normalizedPath = NormalizePath(selectedPath);
                if (!IsPathInsideDirectory(normalizedPath, normalizedRoot) &&
                    !IsPathInsideDirectory(normalizedPath, normalizedTextureRoot) &&
                    !string.Equals(normalizedPath, normalizedPrefabPath, StringComparison.OrdinalIgnoreCase))
                {
                    PsdLogger.Warning("Skip deleting path outside generated targets: " + normalizedPath);
                    continue;
                }

                PsdLogger.Info("Delete generated file: " + normalizedPath);
                DeleteFileWithMeta(normalizedPath);
            }

            if (Directory.Exists(outputFullPath))
            {
                DeleteEmptySubDirectories(outputFullPath);
            }

            string textureDirectory = Path.Combine(
                GetFullProjectPath(),
                textureRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(textureDirectory) &&
                !string.Equals(
                    NormalizePath(textureDirectory),
                    NormalizePath(outputFullPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                DeleteEmptySubDirectories(textureDirectory);
            }
        }

        /// <summary>
        /// Determines whether the current import run can overwrite an existing generated file.
        /// </summary>
        /// <param name="filePath">Absolute path to the generated file.</param>
        /// <returns>True if writing is allowed; otherwise false.</returns>
        private static bool ShouldOverwriteExistingGeneratedFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return true;
            }

            if (!useExplicitUpdateSelection)
            {
                return true;
            }

            bool canOverwrite = selectedUpdatePathsForCurrentImport != null &&
                selectedUpdatePathsForCurrentImport.Contains(NormalizePath(filePath));
            if (!canOverwrite)
            {
                PsdLogger.Info("Skip existing generated file because it was not selected for update: " + filePath);
            }

            return canOverwrite;
        }

        /// <summary>
        /// Prepares an asset path for CreateAsset by honoring overwrite selection and deleting the old asset when allowed.
        /// </summary>
        /// <param name="assetRelativePath">Asset path relative to project root.</param>
        /// <returns>True if a new asset should be created at this path; otherwise false.</returns>
        private static bool PrepareAssetPathForCreate(string assetRelativePath)
        {
            string assetFullPath = NormalizePath(
                Path.Combine(GetFullProjectPath(), assetRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!ShouldOverwriteExistingGeneratedFile(assetFullPath))
            {
                return false;
            }

            if (File.Exists(assetFullPath))
            {
                PsdLogger.Info("Delete existing asset before recreating it: " + assetRelativePath);
                if (!AssetDatabase.DeleteAsset(assetRelativePath))
                {
                    DeleteFileWithMeta(assetFullPath);
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether the prefab should be saved for this import run.
        /// </summary>
        /// <param name="prefabRelativePath">Prefab path relative to project.</param>
        /// <returns>True if prefab should be created/updated; otherwise false.</returns>
        private static bool ShouldSavePrefab(string prefabRelativePath)
        {
            if (string.IsNullOrEmpty(prefabRelativePath))
            {
                return false;
            }

            string prefabFullPath = NormalizePath(
                Path.Combine(GetFullProjectPath(), prefabRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            return ShouldOverwriteExistingGeneratedFile(prefabFullPath);
        }

        /// <summary>
        /// Checks whether the Prefab that will be overwritten is the active Prefab stage.
        /// </summary>
        /// <param name="prefabRelativePath">Prefab path relative to the project.</param>
        /// <returns>True when the target Prefab is currently open in Prefab Mode.</returns>
        private static bool IsTargetPrefabOpenInPrefabMode(string prefabRelativePath)
        {
            if (string.IsNullOrEmpty(prefabRelativePath))
            {
                return false;
            }

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null || string.IsNullOrEmpty(prefabStage.assetPath))
            {
                return false;
            }

            return string.Equals(
                NormalizePath(prefabStage.assetPath),
                NormalizePath(prefabRelativePath),
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Defers Prefab Mode exit until after the current Inspector callback returns, then
        /// resumes the import after Unity has completed the stage transition.
        /// </summary>
        /// <param name="prefabRelativePath">Prefab path relative to the project.</param>
        private static void SchedulePrefabModeExitAndResumeImport(
            string asset,
            ImportConflictSelection forcedSelection,
            bool skipConflictPrompt,
            string prefabRelativePath)
        {
            EditorApplication.delayCall += () =>
            {
                CloseTargetPrefabStageIfOpen(prefabRelativePath);
                EditorApplication.delayCall += () => Import(asset, forcedSelection, skipConflictPrompt);
            };
        }

        /// <summary>
        /// Exits the target Prefab Mode before the importer overwrites the generated Prefab.
        /// PSD is the source of truth, so staged Prefab edits are discarded deliberately.
        /// </summary>
        /// <param name="prefabRelativePath">Prefab path relative to the project.</param>
        private static void CloseTargetPrefabStageIfOpen(string prefabRelativePath)
        {
            if (string.IsNullOrEmpty(prefabRelativePath))
            {
                return;
            }

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null || string.IsNullOrEmpty(prefabStage.assetPath))
            {
                return;
            }

            bool isTargetPrefab = string.Equals(
                NormalizePath(prefabStage.assetPath),
                NormalizePath(prefabRelativePath),
                StringComparison.OrdinalIgnoreCase);
            if (!isTargetPrefab)
            {
                return;
            }

            // Prevent Unity from opening its unsaved-stage confirmation dialog while leaving
            // Prefab Mode. The next import writes a fresh PSD-authoritative Prefab to disk.
            prefabStage.ClearDirtiness();
            StageUtility.GoToMainStage();
            PsdLogger.Info("Closed target Prefab Mode before overwrite: " + prefabRelativePath);
        }

        /// <summary>
        /// Collects all generated asset paths for the current import.
        /// </summary>
        /// <param name="tree">Layer tree for the PSD.</param>
        /// <param name="outputFullPath">Output root directory path.</param>
        /// <param name="prefabFullPath">Resolved prefab full path.</param>
        /// <param name="hasVisibleRuntimeObjects">Whether runtime content will be generated.</param>
        /// <returns>Set of absolute generated asset paths.</returns>
        private static HashSet<string> CollectExpectedGeneratedAssetPaths(
            List<Layer> tree,
            string outputFullPath,
            string prefabFullPath,
            bool hasVisibleRuntimeObjects)
        {
            HashSet<string> expectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (tree != null)
            {
                for (int i = tree.Count - 1; i >= 0; i--)
                {
                    CollectExpectedGeneratedAssetPathsForLayer(tree[i], outputFullPath, outputFullPath, expectedPaths);
                }
            }

            if (CreatePrefab && hasVisibleRuntimeObjects && !string.IsNullOrEmpty(prefabFullPath))
            {
                expectedPaths.Add(NormalizePath(prefabFullPath));
            }

            return expectedPaths;
        }

        /// <summary>
        /// Recursively collects generated asset paths for one layer.
        /// </summary>
        /// <param name="layer">Layer to inspect.</param>
        /// <param name="currentDirectory">Current output directory for this layer.</param>
        /// <param name="result">Destination set for generated assets.</param>
        private static void CollectExpectedGeneratedAssetPathsForLayer(
            Layer layer,
            string outputRootDirectory,
            string currentDirectory,
            HashSet<string> result)
        {
            LayerImportInfo info = GetLayerInfo(layer);
            if (info == null)
            {
                return;
            }

            PsdCommonAssetReference commonReference;
            if (PsdCommonAssetNameParser.TryParse(layer.Name, out commonReference))
            {
                return;
            }

            if (info.IsButtonGroup)
            {
                CollectExpectedGeneratedAssetPathsForButtonGroup(layer, outputRootDirectory, currentDirectory, result);
                return;
            }

            if (DoesLayerCreateOutputDirectory(info))
            {
                string childDirectory = Path.Combine(currentDirectory, GetOutputFolderName(layer));
                for (int i = layer.Children.Count - 1; i >= 0; i--)
                {
                    CollectExpectedGeneratedAssetPathsForLayer(layer.Children[i], outputRootDirectory, childDirectory, result);
                }

                if ((LayoutInScene || CreatePrefab) &&
                    info.EffectiveVisible &&
                    info.IsAnimationGroup &&
                    !UseUnityUI &&
                    GetVisibleAnimationFrameLayers(layer).Count > 0)
                {
                    string assetBaseName = GetOutputFolderName(layer);
                    result.Add(NormalizePath(Path.Combine(childDirectory, assetBaseName + ".anim")));
                    result.Add(NormalizePath(Path.Combine(childDirectory, assetBaseName + ".controller")));
                }

                return;
            }

            if (ShouldLayerEmitTextureFile(info))
            {
                string texturePath = GetTextureOutputPath(outputRootDirectory, layer);
                result.Add(NormalizePath(texturePath));
            }
        }

        /// <summary>
        /// Collects generated asset paths produced by a button group.
        /// </summary>
        /// <param name="layer">Button group layer.</param>
        /// <param name="currentDirectory">Current output directory.</param>
        /// <param name="result">Destination set for generated assets.</param>
        private static void CollectExpectedGeneratedAssetPathsForButtonGroup(
            Layer layer,
            string outputRootDirectory,
            string currentDirectory,
            HashSet<string> result)
        {
            foreach (Layer child in layer.Children)
            {
                LayerImportInfo childInfo = GetLayerInfo(child);
                if (!ShouldButtonGroupChildEmitTexture(childInfo))
                {
                    continue;
                }

                string path = GetTextureOutputPath(outputRootDirectory, child);
                result.Add(NormalizePath(path));
            }
        }

        /// <summary>
        /// Resolves the source-GUID Profile before any Prefab conflict,
        /// deletion or save operation. A Profile created for the Unity UI
        /// incremental workflow cannot safely fall back to scene-object mode.
        /// </summary>
        public static PsdHierarchyProfile ResolveHierarchyProfileBeforePrefabImport(
            string sourcePsdGuid,
            string prefabPath,
            bool useUnityUI)
        {
            string profilePath = PsdPrefabTransactionalSave.GetProfilePath(prefabPath, sourcePsdGuid);
            PsdHierarchyProfile profile = PsdPrefabTransactionalSave.ResolveBoundProfileForImport(
                profilePath, prefabPath);
            if (profile != null && !useUnityUI)
                throw new InvalidOperationException(
                    "Hierarchy Profile incremental import is unsupported in Scene Objects mode. " +
                    "Switch back to Unity UI mode before importing this PSD: " + profilePath);
            return profile;
        }

        /// <summary>
        /// Applies a persisted hierarchy Profile to the exact configured target
        /// Prefab. Returning false means no Profile was adopted and preserves
        /// the original importer save behavior; a stale or ambiguous Profile
        /// throws so it cannot silently overwrite project-owned work.
        /// </summary>
        private static bool TrySaveIncrementalHierarchyPrefab(
            string sourcePsdPath,
            PsdPrefabDocumentModel sourceModel,
            IReadOnlyCollection<PsdPrefabNodeChange> conversionChanges,
            string prefabPath,
            GameObject candidateRoot,
            IReadOnlyDictionary<string, RectTransform> candidateRegistry)
        {
            if (string.IsNullOrEmpty(prefabPath))
                return false;

            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePsdPath);
            string profilePath = PsdPrefabTransactionalSave.GetProfilePath(prefabPath, sourceGuid);
            PsdHierarchyProfile persisted = ResolveHierarchyProfileBeforePrefabImport(
                sourceGuid, prefabPath, UseUnityUI);
            if (!UseUnityUI) return false;
            if (persisted == null) return false;
            if (persisted != null && !persisted.CheckSchema().canApply)
                throw new InvalidOperationException("Hierarchy Profile schema is stale or unsupported: " + profilePath);
            if (persisted != null && !string.Equals(persisted.sourcePsdGuid, sourceGuid, StringComparison.Ordinal))
                throw new InvalidOperationException("Hierarchy Profile belongs to a different PSD: " + profilePath);

            PsdHierarchyProfile working = null;
            GameObject existingContents = null;
            try
            {
                PsdHierarchyReconciliationResult reconciliation = null;
                PsdHierarchyPlan plan;
                working = UnityEngine.Object.Instantiate(persisted);
                reconciliation = working.Reconcile(sourceModel);
                if (reconciliation.requiresReplan || reconciliation.unsortedNewStableIds.Count > 0 ||
                    reconciliation.unsortedUnstableIds.Count > 0)
                    throw new InvalidOperationException(
                        "Hierarchy Profile no longer matches the PSD. Generate a new Prefab before importing again.");
                plan = CreatePlanFromProfile(working, sourceGuid);

                // Ownership is determined by what this importer invocation
                // actually emitted, never inferred from PSD visibility/name.
                // Missing historical records are untouched and remain pending.
                foreach (string pendingEmission in working.UpdateImporterOwnership(sourceModel, candidateRegistry.Keys))
                    if (reconciliation != null && !reconciliation.pendingMissingStableIds.Contains(pendingEmission))
                        reconciliation.pendingMissingStableIds.Add(pendingEmission);

                // A plan derived from the persisted Profile must clear the same
                // membership, protected-boundary and rename-protection rules as
                // a freshly planned one. Reconcile has already decided staleness
                // per node, so only the document fingerprint gate is skipped;
                // without this a stale Profile could rename or reparent a node
                // that gained project-owned components since it was planned.
                if (reconciliation != null)
                {
                    PsdHierarchyRequest reconciledRequest = PsdHierarchyContextBuilder.Build(
                        sourceModel,
                        PsdPrefabIncrementalMerge.BuildProfilePrefabMetadata(prefabPath, working),
                        sourceGuid);
                    if (reconciliation.geometryValidationStableIds.Count > 0)
                    {
                        if (reconciliation.pendingMissingStableIds.Count > 0)
                            throw new InvalidOperationException(
                                "Geometry validation cannot reuse a plan while generated nodes are pending. Generate a new Prefab before importing again.");
                        try
                        {
                            PsdHierarchyPlanValidator.ValidateGeometryReuse(plan, reconciledRequest);
                            working.AcceptValidatedGeometry(sourceModel, reconciliation.geometryValidationStableIds);
                        }
                        catch (PsdHierarchyPlanValidationException exception)
                        {
                            throw new InvalidOperationException(
                                "Geometry-only reuse failed deterministic hierarchy validation. Generate a new Prefab before importing again.", exception);
                        }
                    }
                    else
                    {
                        try
                        {
                            PsdHierarchyPlanValidator.ValidateReconciledPlan(plan, reconciledRequest);
                        }
                        catch (PsdHierarchyPlanValidationException exception)
                        {
                            throw new InvalidOperationException(
                                "The persisted hierarchy Profile no longer passes deterministic validation " +
                                "against the current PSD and Prefab. Generate a new Prefab before importing again.", exception);
                        }
                    }
                }

                existingContents = PrefabUtility.LoadPrefabContents(prefabPath);
                var importerValueSyncStableIds = new HashSet<string>(
                    (conversionChanges ?? Array.Empty<PsdPrefabNodeChange>())
                    .Where(change => change != null && change.kind == PsdPrefabChangeKind.Updated)
                    .Select(change => change.stableId)
                    .Where(PsdStableLayerIdUtility.IsPersistable),
                    StringComparer.Ordinal);
                if (reconciliation != null)
                {
                    importerValueSyncStableIds.UnionWith(reconciliation.contentOnlyStableIds);
                    importerValueSyncStableIds.UnionWith(reconciliation.geometryValidationStableIds);
                }
                PsdPrefabIncrementalMergeResult merge = PsdPrefabIncrementalMerge.Merge(
                    prefabPath, existingContents, candidateRoot, candidateRegistry, working,
                    persisted != null
                        ? persisted.groups
                        : Enumerable.Empty<PsdHierarchyProfileGroup>(),
                    plan,
                    importerValueSyncStableIds);
                PsdPrefabTransactionalSave.Save(
                    prefabPath, existingContents, profilePath, working,
                    merge.generatedByStableId, merge.groupsByKey,
                    Array.Empty<string>(), null, persisted == null);
                return true;
            }
            finally
            {
                if (existingContents != null) PrefabUtility.UnloadPrefabContents(existingContents);
                if (working != null) UnityEngine.Object.DestroyImmediate(working);
            }
        }

        private static PsdHierarchyPlan CreatePlanFromProfile(
            PsdHierarchyProfile profile,
            string sourceGuid)
        {
            var plan = new PsdHierarchyPlan
            {
                schemaVersion = PsdHierarchyPlan.CurrentSchemaVersion,
                sourcePsdGuid = sourceGuid,
                sourceFingerprint = profile.sourceFingerprint,
                contentFingerprint = profile.sourceContentFingerprint,
                structureFingerprint = profile.sourceStructureFingerprint,
                geometryFingerprint = profile.sourceGeometryFingerprint
            };
            foreach (PsdHierarchyProfileGroup source in profile.groups ?? new List<PsdHierarchyProfileGroup>())
            {
                plan.groups.Add(new PsdHierarchyPlanGroup
                {
                    key = source.key,
                    parentKey = source.parentKey,
                    displayName = source.displayName,
                    memberStableIds = new List<string>(source.stableLayerIds ?? new List<string>()),
                    evidence = "Persisted validated hierarchy Profile",
                    confidence = 1d
                });
            }
            foreach (PsdHierarchyProfileRename source in profile.renames ?? new List<PsdHierarchyProfileRename>())
            {
                plan.renames.Add(new PsdHierarchyPlanRename
                {
                    stableId = source.stableId,
                    name = source.name,
                    evidence = "Persisted validated hierarchy Profile",
                    confidence = 1d
                });
            }
            return plan;
        }

        /// <summary>
        /// Converts full file paths to normalized display paths.
        /// </summary>
        /// <param name="fullPath">Absolute file path.</param>
        /// <returns>Path relative to project where possible.</returns>
        private static string ToDisplayPath(string fullPath)
        {
            string normalizedFullPath = NormalizePath(fullPath);
            string projectPath = NormalizePath(GetFullProjectPath()).TrimEnd('/');
            if (normalizedFullPath.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedFullPath.Substring(projectPath.Length).TrimStart('/');
            }

            return normalizedFullPath;
        }

        /// <summary>
        /// Normalizes a path for case-insensitive comparison.
        /// </summary>
        /// <param name="path">Path to normalize.</param>
        /// <returns>Normalized absolute path using forward slashes.</returns>
        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/');
        }

        /// <summary>
        /// Logs basic file and header details before the full PSD parser runs.
        /// </summary>
        /// <param name="fullPath">Absolute PSD file path.</param>
        private static void LogPsdFilePreflight(string fullPath)
        {
            FileInfo fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists)
            {
                PsdLogger.Warning("PSD file does not exist before open: " + fullPath);
                return;
            }

            PsdLogger.Info("PSD file size: " + FormatFileSize(fileInfo.Length));
            if (fileInfo.Length > int.MaxValue)
            {
                PsdLogger.Warning("PSD file is larger than 2 GB; this parser uses 32-bit lengths and may not support it.");
            }

            using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BinaryReverseReader reader = new BinaryReverseReader(stream))
            {
                if (stream.Length < 26)
                {
                    PsdLogger.Warning("PSD file is shorter than the required 26-byte header.");
                    return;
                }

                string signature = new string(reader.ReadChars(4));
                short version = reader.ReadInt16();
                reader.BaseStream.Position += 6L;
                short channelCount = reader.ReadInt16();
                int height = reader.ReadInt32();
                int width = reader.ReadInt32();
                short depth = reader.ReadInt16();
                ColorModes colorMode = (ColorModes)reader.ReadInt16();

                PsdLogger.Info(
                    "PSD header: signature=" + signature +
                    ", version=" + version +
                    ", size=" + width + "x" + height +
                    ", channels=" + channelCount +
                    ", depth=" + depth +
                    ", colorMode=" + colorMode);

                if (signature != "8BPS")
                {
                    PsdLogger.Warning("Unsupported PSD signature: " + signature);
                }

                if (version != 1)
                {
                    PsdLogger.Warning("Unsupported PSD version: " + version + ". PSB/large document files use version 2 and are not supported.");
                }

                if (depth != 1 && depth != 8 && depth != 16)
                {
                    PsdLogger.Warning("Unsupported PSD bit depth: " + depth);
                }
            }
        }

        private static string FormatFileSize(long bytes)
        {
            double megabytes = bytes / 1024d / 1024d;
            return string.Format("{0:0.##} MB ({1} bytes)", megabytes, bytes);
        }

        /// <summary>
        /// Returns whether the given path is under the specified root directory.
        /// </summary>
        /// <param name="path">Path to check.</param>
        /// <param name="rootDirectory">Root directory path.</param>
        /// <returns>True if inside root; otherwise false.</returns>
        private static bool IsPathInsideDirectory(string path, string rootDirectory)
        {
            string normalizedRoot = rootDirectory.TrimEnd('/');
            string normalizedPath = path.TrimEnd('/');
            return normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Deletes a file and its meta file if they exist.
        /// </summary>
        /// <param name="filePath">Absolute file path.</param>
        private static void DeleteFileWithMeta(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                string metaPath = filePath + ".meta";
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(string.Format("Failed to delete file '{0}': {1}", filePath, ex.Message));
            }
        }

        /// <summary>
        /// Deletes empty subdirectories under the specified root directory.
        /// </summary>
        /// <param name="rootDirectory">Root directory to clean.</param>
        private static void DeleteEmptySubDirectories(string rootDirectory)
        {
            if (!Directory.Exists(rootDirectory))
            {
                return;
            }

            foreach (string subDirectory in Directory.GetDirectories(rootDirectory))
            {
                DeleteEmptySubDirectories(subDirectory);

                bool hasFiles = Directory.GetFiles(subDirectory).Length > 0;
                bool hasDirectories = Directory.GetDirectories(subDirectory).Length > 0;
                if (hasFiles || hasDirectories)
                {
                    continue;
                }

                try
                {
                    Directory.Delete(subDirectory);
                    string metaPath = subDirectory + ".meta";
                    if (File.Exists(metaPath))
                    {
                        File.Delete(metaPath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(string.Format("Failed to remove directory '{0}': {1}", subDirectory, ex.Message));
                }
            }
        }

        /// <summary>
        /// Resolves cached import metadata for a layer.
        /// </summary>
        /// <param name="layer">PSD layer.</param>
        /// <returns>Layer metadata if available.</returns>
        private static LayerImportInfo GetLayerInfo(Layer layer)
        {
            if (layer == null || currentLayerInfos == null)
            {
                return null;
            }

            LayerImportInfo info;
            return currentLayerInfos.TryGetValue(layer, out info) ? info : null;
        }

        /// <summary>
        /// Builds import metadata for the current layer tree.
        /// </summary>
        /// <param name="tree">Top-level layer tree.</param>
        /// <returns>Metadata keyed by PSD layer instance.</returns>
        private static Dictionary<Layer, LayerImportInfo> BuildLayerImportInfoMap(List<Layer> tree)
        {
            Dictionary<Layer, LayerImportInfo> infoMap = new Dictionary<Layer, LayerImportInfo>();
            if (tree == null)
            {
                return infoMap;
            }

            foreach (Layer layer in tree)
            {
                CreateLayerImportInfo(layer, null, true, infoMap);
            }

            AssignUniqueSelfNamesRecursively(tree, infoMap);
            AssignUniqueTextureNamesForScope(tree, infoMap);
            return infoMap;
        }

        /// <summary>
        /// Creates import metadata for one layer and its descendants.
        /// </summary>
        /// <param name="layer">Layer to inspect.</param>
        /// <param name="parent">Parent metadata.</param>
        /// <param name="parentVisible">Inherited parent visibility.</param>
        /// <param name="infoMap">Destination map.</param>
        private static void CreateLayerImportInfo(
            Layer layer,
            LayerImportInfo parent,
            bool parentVisible,
            Dictionary<Layer, LayerImportInfo> infoMap)
        {
            LayerImportInfo info = new LayerImportInfo(layer)
            {
                Parent = parent,
                EffectiveVisible = parentVisible && layer.Visible,
                IsFolderLike = layer.Children.Count > 0 || layer.Rect.width == 0,
                AnimationFps = GetAnimationFps(layer.Name)
            };

            info.IsButtonGroup = info.IsFolderLike && layer.Name.ContainsIgnoreCase("|Button");
            info.IsAnimationGroup = info.IsFolderLike && layer.Name.ContainsIgnoreCase("|Animation");
            info.ButtonRole = parent != null && parent.IsButtonGroup ? GetButtonChildRole(layer) : ButtonChildRole.None;
            info.ExplicitAnchorPreset = ParseAnchorPreset(GetAnchorParsingName(info));
            info.AnchorPreset = ResolveAnchorPreset(info);

            infoMap[layer] = info;

            foreach (Layer child in layer.Children)
            {
                CreateLayerImportInfo(child, info, info.EffectiveVisible, infoMap);
            }

            Rect layoutRect;
            info.HasLayoutRect = TryResolveLayerLayoutRect(info, infoMap, out layoutRect);
            info.LayoutRect = layoutRect;
        }

        /// <summary>
        /// Resolves the effective layout rect used to place one layer in Unity UI.
        /// </summary>
        /// <param name="info">Layer metadata.</param>
        /// <param name="infoMap">Layer metadata map.</param>
        /// <param name="layoutRect">Resolved rect when available.</param>
        /// <returns>True when a valid layout rect exists.</returns>
        private static bool TryResolveLayerLayoutRect(
            LayerImportInfo info,
            Dictionary<Layer, LayerImportInfo> infoMap,
            out Rect layoutRect)
        {
            layoutRect = default(Rect);
            if (info == null || info.Layer == null)
            {
                return false;
            }

            if (!info.IsFolderLike)
            {
                if (info.Layer.Rect.width > 0f && info.Layer.Rect.height > 0f)
                {
                    layoutRect = info.Layer.Rect;
                    return true;
                }

                return false;
            }

            bool hasBounds = false;
            Rect combinedRect = default(Rect);
            foreach (Layer child in info.Layer.Children)
            {
                LayerImportInfo childInfo;
                if (!infoMap.TryGetValue(child, out childInfo) || childInfo == null || !childInfo.EffectiveVisible || !childInfo.HasLayoutRect)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedRect = childInfo.LayoutRect;
                    hasBounds = true;
                }
                else
                {
                    combinedRect = CombineRects(combinedRect, childInfo.LayoutRect);
                }
            }

            if (hasBounds)
            {
                layoutRect = combinedRect;
                return true;
            }

            if (info.Layer.Rect.width > 0f && info.Layer.Rect.height > 0f)
            {
                layoutRect = info.Layer.Rect;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Combines two rects into one bounding rect.
        /// </summary>
        /// <param name="first">First rect.</param>
        /// <param name="second">Second rect.</param>
        /// <returns>Bounding rect containing both inputs.</returns>
        private static Rect CombineRects(Rect first, Rect second)
        {
            float xMin = Mathf.Min(first.xMin, second.xMin);
            float yMin = Mathf.Min(first.yMin, second.yMin);
            float xMax = Mathf.Max(first.xMax, second.xMax);
            float yMax = Mathf.Max(first.yMax, second.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>
        /// Assigns stable unique names among siblings for all layers.
        /// </summary>
        /// <param name="siblings">Sibling layers.</param>
        /// <param name="infoMap">Layer metadata map.</param>
        private static void AssignUniqueSelfNamesRecursively(List<Layer> siblings, Dictionary<Layer, LayerImportInfo> infoMap)
        {
            if (siblings == null || siblings.Count == 0)
            {
                return;
            }

            List<LayerImportInfo> siblingInfos = siblings.Select(layer => infoMap[layer]).ToList();
            AssignUniqueNames(
                siblingInfos,
                GetStableSelfBaseName,
                (info, uniqueName) => info.UniqueSelfName = uniqueName,
                "Layer");

            foreach (Layer sibling in siblings)
            {
                AssignUniqueSelfNamesRecursively(sibling.Children, infoMap);
            }
        }

        /// <summary>
        /// Assigns unique texture/file names inside one output directory scope.
        /// </summary>
        /// <param name="siblings">Sibling layers that share one output directory scope.</param>
        /// <param name="infoMap">Layer metadata map.</param>
        private static void AssignUniqueTextureNamesForScope(List<Layer> siblings, Dictionary<Layer, LayerImportInfo> infoMap)
        {
            if (siblings == null || siblings.Count == 0)
            {
                return;
            }

            List<LayerImportInfo> fileEmitters = CollectFileEmittersForScope(siblings, infoMap);
            AssignUniqueNames(
                fileEmitters,
                GetPreferredTextureBaseName,
                (info, uniqueName) => info.UniqueTextureName = uniqueName,
                "Layer");

            foreach (Layer sibling in siblings)
            {
                LayerImportInfo info = infoMap[sibling];
                if (DoesLayerCreateOutputDirectory(info))
                {
                    AssignUniqueTextureNamesForScope(sibling.Children, infoMap);
                }
            }
        }

        /// <summary>
        /// Collects all layers that export texture files in the current output directory.
        /// </summary>
        /// <param name="siblings">Sibling layers in the current scope.</param>
        /// <param name="infoMap">Layer metadata map.</param>
        /// <returns>Ordered file emitters for the current directory.</returns>
        private static List<LayerImportInfo> CollectFileEmittersForScope(List<Layer> siblings, Dictionary<Layer, LayerImportInfo> infoMap)
        {
            List<LayerImportInfo> emitters = new List<LayerImportInfo>();

            foreach (Layer sibling in siblings)
            {
                LayerImportInfo info = infoMap[sibling];
                if (info.IsButtonGroup)
                {
                    foreach (Layer child in sibling.Children)
                    {
                        LayerImportInfo childInfo = infoMap[child];
                        if (ShouldButtonGroupChildEmitTexture(childInfo))
                        {
                            emitters.Add(childInfo);
                        }
                    }

                    continue;
                }

                if (!info.IsFolderLike && ShouldLayerEmitTextureFile(info))
                {
                    emitters.Add(info);
                }
            }

            return emitters;
        }

        /// <summary>
        /// Assigns unique suffixes like _2/_3 while preserving the first occurrence.
        /// </summary>
        /// <typeparam name="T">Item type.</typeparam>
        /// <param name="items">Items in stable order.</param>
        /// <param name="baseNameSelector">Gets the base name for one item.</param>
        /// <param name="assign">Applies the resolved unique name.</param>
        /// <param name="fallbackBaseName">Fallback when the base name is empty.</param>
        private static void AssignUniqueNames<T>(
            IEnumerable<T> items,
            Func<T, string> baseNameSelector,
            Action<T, string> assign,
            string fallbackBaseName)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (T item in items)
            {
                string baseName = SanitizeStableName(baseNameSelector(item), fallbackBaseName);
                int currentCount;
                counts.TryGetValue(baseName, out currentCount);
                currentCount++;
                counts[baseName] = currentCount;

                assign(item, currentCount == 1 ? baseName : string.Format("{0}_{1}", baseName, currentCount));
            }
        }

        /// <summary>
        /// Gets the stable sibling-name base for a layer.
        /// </summary>
        /// <param name="info">Layer metadata.</param>
        /// <returns>Tag-stripped, sanitized base name.</returns>
        private static string GetStableSelfBaseName(LayerImportInfo info)
        {
            if (info == null)
            {
                return "Layer";
            }

            if (info.IsAnimationGroup)
            {
                return SanitizeStableName(GetAnimationLayerBaseName(info.Layer.Name), "Animation");
            }

            if (info.IsButtonGroup)
            {
                return SanitizeStableName(RemoveTagIgnoreCase(info.Layer.Name, "|Button"), "Button");
            }

            if (info.Parent != null && info.Parent.IsButtonGroup)
            {
                return SanitizeStableName(GetButtonChildBaseName(info.Layer), info.Layer.IsTextLayer ? "Text" : "Layer");
            }

            return SanitizeStableName(info.Layer.Name, info.IsFolderLike ? "Folder" : "Layer");
        }

        /// <summary>
        /// Gets the preferred texture base name inside the current output directory.
        /// </summary>
        /// <param name="info">Layer metadata.</param>
        /// <returns>Preferred texture base name.</returns>
        private static string GetPreferredTextureBaseName(LayerImportInfo info)
        {
            if (info == null)
            {
                return "Layer";
            }

            if (info.Parent != null && info.Parent.IsButtonGroup)
            {
                string parentName = info.Parent.UniqueSelfName ?? GetStableSelfBaseName(info.Parent);
                string childName = info.UniqueSelfName ?? GetStableSelfBaseName(info);
                return SanitizeStableName(string.Format("{0}_{1}", parentName, childName), "Layer");
            }

            return info.UniqueSelfName ?? GetStableSelfBaseName(info);
        }

        /// <summary>
        /// Gets the base name used for animation folders/assets.
        /// </summary>
        /// <param name="name">Original layer name.</param>
        /// <returns>Animation base name.</returns>
        private static string GetAnimationLayerBaseName(string name)
        {
            string strippedName = RemoveAnimationTags(name);
            string[] nameParts = strippedName.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            string baseName = nameParts.Length > 0 ? nameParts[0] : strippedName;
            return string.IsNullOrWhiteSpace(baseName) ? "Animation" : baseName.Trim();
        }

        /// <summary>
        /// Removes animation-related tags from a layer name.
        /// </summary>
        /// <param name="name">Layer name.</param>
        /// <returns>Name without animation tags.</returns>
        private static string RemoveAnimationTags(string name)
        {
            string strippedName = RemoveTagIgnoreCase(name, "|Animation");
            return Regex.Replace(strippedName, "\\|FPS=[^|]+", string.Empty, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Parses animation FPS from the layer name.
        /// </summary>
        /// <param name="name">Layer name.</param>
        /// <returns>Frame rate, defaulting to 30 when unspecified.</returns>
        private static float GetAnimationFps(string name)
        {
            float fps = 30f;
            if (string.IsNullOrEmpty(name))
            {
                return fps;
            }

            string[] args = name.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string arg in args)
            {
                if (!arg.StartsWith("FPS=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                float parsedFps;
                if (float.TryParse(arg.Substring(4), out parsedFps))
                {
                    fps = parsedFps;
                }
                else
                {
                    Debug.LogError(string.Format("Unable to parse FPS: \"{0}\"", arg));
                }

                break;
            }

            return fps;
        }

        /// <summary>
        /// Resolves the parsed anchor preset for one layer.
        /// </summary>
        /// <param name="info">Layer metadata.</param>
        /// <returns>Parsed preset or <see cref="AnchorNamePreset.None"/> when no prefix applies.</returns>
        private static AnchorNamePreset ResolveAnchorPreset(LayerImportInfo info)
        {
            if (!EnableAutoAnchorByName || info == null)
            {
                return AnchorNamePreset.None;
            }

            if (info.ExplicitAnchorPreset != AnchorNamePreset.None)
            {
                return info.ExplicitAnchorPreset;
            }

            if (info.Parent != null &&
                info.Parent.IsFolderLike &&
                info.Parent.AnchorPreset != AnchorNamePreset.None)
            {
                return info.Parent.AnchorPreset;
            }

            return AnchorNamePreset.None;
        }

        /// <summary>
        /// Gets the source name used for anchor-prefix parsing.
        /// </summary>
        /// <param name="info">Layer metadata.</param>
        /// <returns>Name without tool tags.</returns>
        private static string GetAnchorParsingName(LayerImportInfo info)
        {
            if (info == null || info.Layer == null || string.IsNullOrEmpty(info.Layer.Name))
            {
                return string.Empty;
            }

            string name = info.Layer.Name;
            if (info.IsAnimationGroup)
            {
                return GetAnimationLayerBaseName(name);
            }

            if (info.IsButtonGroup)
            {
                return RemoveTagIgnoreCase(name, "|Button");
            }

            if (info.Parent != null && info.Parent.IsButtonGroup)
            {
                return GetButtonChildBaseName(info.Layer);
            }

            int pipeIndex = name.IndexOf('|');
            return pipeIndex >= 0 ? name.Substring(0, pipeIndex) : name;
        }

        /// <summary>
        /// Parses a layer-name prefix into an anchor preset.
        /// </summary>
        /// <param name="name">Layer or folder name without tool tags.</param>
        /// <returns>Resolved anchor preset.</returns>
        private static AnchorNamePreset ParseAnchorPreset(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return AnchorNamePreset.None;
            }

            string trimmedName = name.TrimStart();
            if (trimmedName.StartsWith("全局", StringComparison.OrdinalIgnoreCase))
            {
                return AnchorNamePreset.Global;
            }

            if (trimmedName.StartsWith("左上", StringComparison.OrdinalIgnoreCase))
            {
                return AnchorNamePreset.TopLeft;
            }

            if (trimmedName.StartsWith("左下", StringComparison.OrdinalIgnoreCase))
            {
                return AnchorNamePreset.BottomLeft;
            }

            if (trimmedName.StartsWith("右上", StringComparison.OrdinalIgnoreCase))
            {
                return AnchorNamePreset.TopRight;
            }

            if (trimmedName.StartsWith("右下", StringComparison.OrdinalIgnoreCase))
            {
                return AnchorNamePreset.BottomRight;
            }

            if (trimmedName.StartsWith("中间", StringComparison.OrdinalIgnoreCase))
            {
                return AnchorNamePreset.Center;
            }

            if (trimmedName.StartsWith("左中", StringComparison.OrdinalIgnoreCase))
            {
                return AnchorNamePreset.LeftMiddle;
            }

            if (trimmedName.StartsWith("右中", StringComparison.OrdinalIgnoreCase))
            {
                return AnchorNamePreset.RightMiddle;
            }

            if (trimmedName.StartsWith("上中", StringComparison.OrdinalIgnoreCase))
            {
                return AnchorNamePreset.TopMiddle;
            }

            if (trimmedName.StartsWith("下中", StringComparison.OrdinalIgnoreCase))
            {
                return AnchorNamePreset.BottomMiddle;
            }

            if (trimmedName.StartsWith("上", StringComparison.OrdinalIgnoreCase))
            {
                return AnchorNamePreset.TopMiddle;
            }

            if (trimmedName.StartsWith("下", StringComparison.OrdinalIgnoreCase))
            {
                return AnchorNamePreset.BottomMiddle;
            }

            if (trimmedName.StartsWith("左", StringComparison.OrdinalIgnoreCase))
            {
                return AnchorNamePreset.LeftMiddle;
            }

            if (trimmedName.StartsWith("右", StringComparison.OrdinalIgnoreCase))
            {
                return AnchorNamePreset.RightMiddle;
            }

            return AnchorNamePreset.None;
        }

        /// <summary>
        /// Gets the role of a button child layer.
        /// </summary>
        /// <param name="layer">Button child layer.</param>
        /// <returns>Resolved button role.</returns>
        private static ButtonChildRole GetButtonChildRole(Layer layer)
        {
            if (layer == null)
            {
                return ButtonChildRole.None;
            }

            if (layer.Name.ContainsIgnoreCase("|Disabled"))
            {
                return ButtonChildRole.Disabled;
            }

            if (layer.Name.ContainsIgnoreCase("|Highlighted"))
            {
                return ButtonChildRole.Highlighted;
            }

            if (layer.Name.ContainsIgnoreCase("|Pressed"))
            {
                return ButtonChildRole.Pressed;
            }

            if (layer.Name.ContainsIgnoreCase("|Default") ||
                layer.Name.ContainsIgnoreCase("|Enabled") ||
                layer.Name.ContainsIgnoreCase("|Normal") ||
                layer.Name.ContainsIgnoreCase("|Up"))
            {
                return ButtonChildRole.Default;
            }

            if (layer.Name.ContainsIgnoreCase("|Text") && !layer.IsTextLayer)
            {
                return ButtonChildRole.TextImage;
            }

            return ButtonChildRole.None;
        }

        /// <summary>
        /// Gets a button child name with button-state tags removed.
        /// </summary>
        /// <param name="layer">Button child layer.</param>
        /// <returns>Tag-stripped base name.</returns>
        private static string GetButtonChildBaseName(Layer layer)
        {
            string name = layer != null ? layer.Name : string.Empty;
            name = RemoveTagIgnoreCase(name, "|Disabled");
            name = RemoveTagIgnoreCase(name, "|Highlighted");
            name = RemoveTagIgnoreCase(name, "|Pressed");
            name = RemoveTagIgnoreCase(name, "|Default");
            name = RemoveTagIgnoreCase(name, "|Enabled");
            name = RemoveTagIgnoreCase(name, "|Normal");
            name = RemoveTagIgnoreCase(name, "|Up");

            if (layer != null && !layer.IsTextLayer)
            {
                name = RemoveTagIgnoreCase(name, "|Text");
            }

            return name;
        }

        /// <summary>
        /// Removes one tag from a name without case sensitivity.
        /// </summary>
        /// <param name="name">Source string.</param>
        /// <param name="tag">Tag to remove.</param>
        /// <returns>Updated string.</returns>
        private static string RemoveTagIgnoreCase(string name, string tag)
        {
            return string.IsNullOrEmpty(name) ? string.Empty : name.ReplaceIgnoreCase(tag, string.Empty);
        }

        /// <summary>
        /// Converts a name into a stable filesystem-safe identifier without extra logging.
        /// </summary>
        /// <param name="name">Raw name.</param>
        /// <param name="fallbackName">Fallback when the name is empty.</param>
        /// <returns>Sanitized stable name.</returns>
        private static string SanitizeStableName(string name, string fallbackName)
        {
            string sourceName = string.IsNullOrWhiteSpace(name) ? fallbackName : name.Trim();
            sourceName = RemoveNineSliceTag(sourceName);
            string safeName = MakeNameSafeSilently(sourceName);
            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = MakeNameSafeSilently(fallbackName);
            }

            return string.IsNullOrWhiteSpace(safeName) ? fallbackName : safeName;
        }

        /// <summary>
        /// Removes the optional 9-slice authoring tag before creating stable
        /// runtime names and generated asset paths.
        /// </summary>
        /// <param name="name">Raw PSD layer name.</param>
        /// <returns>Name without the 9-slice tag.</returns>
        private static string RemoveNineSliceTag(string name)
        {
            return PsdNineSliceNameRules.RemoveTag(name);
        }

        /// <summary>
        /// Converts a name into a filesystem-safe identifier without logging.
        /// </summary>
        /// <param name="name">Name to sanitize.</param>
        /// <returns>Sanitized name.</returns>
        private static string MakeNameSafeSilently(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            string trimmedName = name.Trim();
            StringBuilder builder = new StringBuilder(trimmedName.Length);
            foreach (char currentChar in trimmedName)
            {
                builder.Append(InvalidGeneratedNameChars.Contains(currentChar) || char.IsControl(currentChar) ? '_' : currentChar);
            }

            string sanitized = builder.ToString().Trim().TrimEnd('.');
            while (sanitized.EndsWith(" ", StringComparison.Ordinal))
            {
                sanitized = sanitized.Substring(0, sanitized.Length - 1);
            }

            if (ReservedGeneratedNames.Contains(sanitized))
            {
                sanitized += "_";
            }

            return sanitized;
        }

        /// <summary>
        /// Returns true if the layer creates a dedicated output subdirectory.
        /// </summary>
        /// <param name="info">Layer metadata.</param>
        /// <returns>True if the layer writes into a dedicated subdirectory.</returns>
        private static bool DoesLayerCreateOutputDirectory(LayerImportInfo info)
        {
            return info != null && info.IsFolderLike && !info.IsButtonGroup;
        }

        /// <summary>
        /// Returns true if the layer exports its own texture file.
        /// </summary>
        /// <param name="info">Layer metadata.</param>
        /// <returns>True if the layer exports a texture file.</returns>
        private static bool ShouldLayerEmitTextureFile(LayerImportInfo info)
        {
            if (info == null || info.IsFolderLike || info.Layer.Rect.width <= 0 || info.Layer.Rect.height <= 0)
            {
                return false;
            }

            PsdCommonAssetReference commonReference;
            if (PsdCommonAssetNameParser.TryParse(info.Layer.Name, out commonReference))
            {
                return false;
            }

            if (!info.Layer.IsTextLayer)
            {
                return true;
            }

            return !info.EffectiveVisible;
        }

        /// <summary>
        /// Returns true when a layer should be exported without a generated
        /// runtime object. Prefab generation intentionally excludes this path:
        /// its generated PNGs would have no Prefab dependency.
        /// </summary>
        private static bool ShouldExportTextureOnly(LayerImportInfo info)
        {
            return !CreatePrefab && ShouldLayerEmitTextureFile(info);
        }

        /// <summary>
        /// Returns true if a button child should export a texture file.
        /// </summary>
        /// <param name="childInfo">Button child metadata.</param>
        /// <returns>True if a texture should be exported.</returns>
        private static bool ShouldButtonGroupChildEmitTexture(LayerImportInfo childInfo)
        {
            if (childInfo == null || childInfo.IsFolderLike)
            {
                return false;
            }

            if (childInfo.ButtonRole != ButtonChildRole.None)
            {
                return !childInfo.Layer.IsTextLayer || !childInfo.EffectiveVisible;
            }

            return !childInfo.EffectiveVisible && ShouldLayerEmitTextureFile(childInfo);
        }

        /// <summary>
        /// Returns true if runtime generation already creates this button child's texture.
        /// </summary>
        /// <param name="childInfo">Button child metadata.</param>
        /// <returns>True if runtime creation already handles the texture export.</returns>
        private static bool IsButtonChildHandledByRuntime(LayerImportInfo childInfo)
        {
            return childInfo != null &&
                childInfo.EffectiveVisible &&
                childInfo.ButtonRole != ButtonChildRole.None &&
                !childInfo.Layer.IsTextLayer;
        }

        /// <summary>
        /// Gets all visible frame layers for an animation group.
        /// </summary>
        /// <param name="animationLayer">Animation group layer.</param>
        /// <returns>Visible art-layer frames in order.</returns>
        private static List<Layer> GetVisibleAnimationFrameLayers(Layer animationLayer)
        {
            List<Layer> frames = new List<Layer>();
            if (animationLayer == null)
            {
                return frames;
            }

            foreach (Layer child in animationLayer.Children)
            {
                LayerImportInfo childInfo = GetLayerInfo(child);
                if (childInfo == null || !childInfo.EffectiveVisible || childInfo.IsFolderLike || child.IsTextLayer)
                {
                    continue;
                }

                if (child.Rect.width <= 0 || child.Rect.height <= 0)
                {
                    continue;
                }

                frames.Add(child);
            }

            return frames;
        }

        /// <summary>
        /// Returns true if a button group still has visible runtime content after filtering hidden layers.
        /// </summary>
        /// <param name="buttonLayer">Button group layer.</param>
        /// <returns>True if the button object should be created.</returns>
        private static bool HasVisibleButtonRuntimeContent(Layer buttonLayer)
        {
            if (buttonLayer == null)
            {
                return false;
            }

            foreach (Layer child in buttonLayer.Children)
            {
                if (IsButtonChildHandledByRuntime(GetLayerInfo(child)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if any visible runtime content exists in the tree.
        /// </summary>
        /// <param name="tree">Top-level tree.</param>
        /// <returns>True if scene/prefab objects should be created.</returns>
        private static bool HasVisibleRuntimeContent(List<Layer> tree)
        {
            if (!(LayoutInScene || CreatePrefab) || tree == null)
            {
                return false;
            }

            foreach (Layer layer in tree)
            {
                if (HasVisibleRuntimeContent(layer))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if a layer or any descendants create visible runtime content.
        /// </summary>
        /// <param name="layer">Layer to inspect.</param>
        /// <returns>True if runtime content exists.</returns>
        private static bool HasVisibleRuntimeContent(Layer layer)
        {
            LayerImportInfo info = GetLayerInfo(layer);
            if (info == null || !info.EffectiveVisible)
            {
                return false;
            }

            if (info.IsButtonGroup)
            {
                return UseUnityUI && HasVisibleButtonRuntimeContent(layer);
            }

            if (info.IsAnimationGroup)
            {
                return !UseUnityUI && GetVisibleAnimationFrameLayers(layer).Count > 0;
            }

            if (!info.IsFolderLike)
            {
                return true;
            }

            foreach (Layer child in layer.Children)
            {
                if (HasVisibleRuntimeContent(child))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the runtime object name for a layer.
        /// </summary>
        /// <param name="layer">Layer to inspect.</param>
        /// <returns>Resolved runtime name.</returns>
        private static string GetRuntimeObjectName(Layer layer)
        {
            PsdCommonAssetReference commonReference;
            if (layer != null && PsdCommonAssetNameParser.TryParse(layer.Name, out commonReference))
            {
                return MakeNameSafe(commonReference.Key);
            }

            LayerImportInfo info = GetLayerInfo(layer);
            if (info == null)
            {
                return MakeNameSafe(layer != null ? layer.Name : "Layer");
            }

            if (info.Parent != null &&
                info.Parent.IsButtonGroup &&
                info.ButtonRole == ButtonChildRole.TextImage &&
                !string.IsNullOrEmpty(info.UniqueTextureName))
            {
                return info.UniqueTextureName;
            }

            return string.IsNullOrEmpty(info.UniqueSelfName)
                ? SanitizeStableName(info.Layer.Name, info.IsFolderLike ? "Folder" : "Layer")
                : info.UniqueSelfName;
        }

        /// <summary>
        /// Gets the output folder name for a folder-like layer.
        /// </summary>
        /// <param name="layer">Layer to inspect.</param>
        /// <returns>Resolved folder name.</returns>
        private static string GetOutputFolderName(Layer layer)
        {
            LayerImportInfo info = GetLayerInfo(layer);
            return info != null && !string.IsNullOrEmpty(info.UniqueSelfName)
                ? SanitizeStableName(info.UniqueSelfName, "Folder")
                : MakeNameSafe(layer.Name);
        }

        /// <summary>
        /// Gets the texture base name for a layer.
        /// </summary>
        /// <param name="layer">Layer to inspect.</param>
        /// <returns>Resolved texture base name.</returns>
        private static string GetTextureBaseName(Layer layer)
        {
            LayerImportInfo info = GetLayerInfo(layer);
            if (info != null && !string.IsNullOrEmpty(info.UniqueTextureName))
            {
                return SanitizeStableName(info.UniqueTextureName, layer != null && layer.IsTextLayer ? "Text" : "Layer");
            }

            if (info != null && !string.IsNullOrEmpty(info.UniqueSelfName))
            {
                return SanitizeStableName(info.UniqueSelfName, layer != null && layer.IsTextLayer ? "Text" : "Layer");
            }

            return MakeNameSafe(layer.Name);
        }

        /// <summary>
        /// Stores analyzed import conflicts for a single PSD import.
        /// </summary>
        private sealed class ImportConflictAnalysis
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ImportConflictAnalysis"/> class.
            /// </summary>
            public ImportConflictAnalysis()
            {
                SameNamePaths = new List<string>();
                DeletedPaths = new List<string>();
            }

            /// <summary>
            /// Gets or sets a value indicating whether the output folder already exists.
            /// </summary>
            public bool HasExistingOutputDirectory { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the target prefab already exists.
            /// </summary>
            public bool HasExistingPrefab { get; set; }

            /// <summary>
            /// Gets or sets the output folder path relative to project.
            /// </summary>
            public string OutputRelativePath { get; set; }

            /// <summary>
            /// Gets or sets the output folder path as a normalized full path.
            /// </summary>
            public string OutputFullPath { get; set; }

            /// <summary>
            /// Gets or sets the prefab path relative to project.
            /// </summary>
            public string PrefabRelativePath { get; set; }

            /// <summary>
            /// Gets or sets the prefab path as a normalized full path.
            /// </summary>
            public string PrefabFullPath { get; set; }

            /// <summary>
            /// Gets same-name files that can be updated.
            /// </summary>
            public List<string> SameNamePaths { get; private set; }

            /// <summary>
            /// Gets stale files that can be deleted.
            /// </summary>
            public List<string> DeletedPaths { get; private set; }

            /// <summary>
            /// Gets a value indicating whether any existing import target was found.
            /// </summary>
            public bool HasExistingTargets
            {
                get
                {
                    return HasExistingOutputDirectory || HasExistingPrefab;
                }
            }

            /// <summary>
            /// Gets a value indicating whether there are selectable entries for update/delete.
            /// </summary>
            public bool HasSelectableEntries
            {
                get
                {
                    return SameNamePaths.Count > 0 || DeletedPaths.Count > 0;
                }
            }
        }

        /// <summary>
        /// Stores user-selected update/delete operations for an import run.
        /// </summary>
        private sealed class ImportConflictSelection
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ImportConflictSelection"/> class.
            /// </summary>
            public ImportConflictSelection()
            {
                PathsToUpdate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                PathsToDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            /// <summary>
            /// Gets or sets a value indicating whether the selection is confirmed by user.
            /// </summary>
            public bool Confirmed { get; set; }

            /// <summary>
            /// Gets files selected for overwrite/update.
            /// </summary>
            public HashSet<string> PathsToUpdate { get; private set; }

            /// <summary>
            /// Gets files selected for deletion.
            /// </summary>
            public HashSet<string> PathsToDelete { get; private set; }
        }

        /// <summary>
        /// UI entry representing a selectable file operation.
        /// </summary>
        private sealed class ConflictPathOption
        {
            /// <summary>
            /// Gets or sets the normalized full file path.
            /// </summary>
            public string FullPath { get; set; }

            /// <summary>
            /// Gets or sets the display path shown in the dialog.
            /// </summary>
            public string DisplayPath { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether this entry is selected.
            /// </summary>
            public bool Selected { get; set; }
        }

        /// <summary>
        /// Selection window used to choose which same-name files to update and which stale files to delete.
        /// </summary>
        private sealed class ImportConflictSelectionWindow : EditorWindow
        {
            /// <summary>
            /// Same-name options.
            /// </summary>
            private readonly List<ConflictPathOption> updateOptions = new List<ConflictPathOption>();

            /// <summary>
            /// Stale-file options.
            /// </summary>
            private readonly List<ConflictPathOption> deleteOptions = new List<ConflictPathOption>();

            /// <summary>
            /// Scroll position for list rendering.
            /// </summary>
            private Vector2 scrollPosition;

            /// <summary>
            /// Callback fired once dialog is closed.
            /// </summary>
            private Action<ImportConflictSelection> onClose;

            /// <summary>
            /// Guards against invoking callback more than once.
            /// </summary>
            private bool callbackSent;

            /// <summary>
            /// Opens the conflict selection window.
            /// </summary>
            /// <param name="analysis">Conflict analysis data.</param>
            /// <param name="defaultSelection">Default checked entries.</param>
            /// <param name="onCloseCallback">Callback invoked on confirm/cancel.</param>
            public static void ShowDialog(
                ImportConflictAnalysis analysis,
                ImportConflictSelection defaultSelection,
                Action<ImportConflictSelection> onCloseCallback)
            {
                ImportConflictSelectionWindow window = CreateInstance<ImportConflictSelectionWindow>();
                window.titleContent = new GUIContent("PSD 更新与删除");
                window.minSize = new Vector2(760f, 420f);
                window.Initialize(analysis, defaultSelection, onCloseCallback);
                window.ShowUtility();
                window.Focus();
            }

            /// <summary>
            /// Initializes this window with selectable entries.
            /// </summary>
            /// <param name="analysis">Conflict analysis data.</param>
            /// <param name="defaultSelection">Default checked entries.</param>
            /// <param name="onCloseCallback">Callback invoked on confirm/cancel.</param>
            private void Initialize(
                ImportConflictAnalysis analysis,
                ImportConflictSelection defaultSelection,
                Action<ImportConflictSelection> onCloseCallback)
            {
                onClose = onCloseCallback;

                HashSet<string> defaultUpdates = defaultSelection != null
                    ? defaultSelection.PathsToUpdate
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                HashSet<string> defaultDeletes = defaultSelection != null
                    ? defaultSelection.PathsToDelete
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (string sameNamePath in analysis.SameNamePaths)
                {
                    string normalizedPath = NormalizePath(sameNamePath);
                    updateOptions.Add(new ConflictPathOption
                    {
                        FullPath = normalizedPath,
                        DisplayPath = ToDisplayPath(normalizedPath),
                        Selected = defaultUpdates.Contains(normalizedPath)
                    });
                }

                foreach (string stalePath in analysis.DeletedPaths)
                {
                    string normalizedPath = NormalizePath(stalePath);
                    deleteOptions.Add(new ConflictPathOption
                    {
                        FullPath = normalizedPath,
                        DisplayPath = ToDisplayPath(normalizedPath),
                        Selected = defaultDeletes.Contains(normalizedPath)
                    });
                }
            }

            /// <summary>
            /// Draws window GUI.
            /// </summary>
            private void OnGUI()
            {
                EditorGUILayout.HelpBox(
                    "勾选“同名文件”会覆盖现有文件；勾选“删除文件”会移除旧文件。未勾选项将保持不变。",
                    MessageType.Info);

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                DrawOptionsSection("同名文件（勾选后更新）", updateOptions, "没有同名文件。");
                GUILayout.Space(8f);
                DrawOptionsSection("删除文件（勾选后删除）", deleteOptions, "没有可删除文件。");
                EditorGUILayout.EndScrollView();

                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("取消", GUILayout.Height(28f)))
                {
                    CloseWithCancel();
                }

                if (GUILayout.Button("确定", GUILayout.Height(28f)))
                {
                    CloseWithSelection();
                }

                EditorGUILayout.EndHorizontal();
            }

            /// <summary>
            /// Ensures cancellation callback is emitted when window is closed directly.
            /// </summary>
            private void OnDestroy()
            {
                if (!callbackSent)
                {
                    NotifyClose(new ImportConflictSelection { Confirmed = false });
                }
            }

            /// <summary>
            /// Draws one selectable section.
            /// </summary>
            /// <param name="title">Section title.</param>
            /// <param name="options">Selectable options.</param>
            /// <param name="emptyMessage">Message shown when no options exist.</param>
            private static void DrawOptionsSection(string title, List<ConflictPathOption> options, string emptyMessage)
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                if (options.Count == 0)
                {
                    EditorGUILayout.LabelField(emptyMessage);
                    return;
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("全选", GUILayout.Width(70f)))
                {
                    SetSelection(options, true);
                }

                if (GUILayout.Button("全不选", GUILayout.Width(70f)))
                {
                    SetSelection(options, false);
                }

                EditorGUILayout.EndHorizontal();

                foreach (ConflictPathOption option in options)
                {
                    option.Selected = EditorGUILayout.ToggleLeft(option.DisplayPath, option.Selected);
                }
            }

            /// <summary>
            /// Sets selection state for all options in one section.
            /// </summary>
            /// <param name="options">Options to update.</param>
            /// <param name="selected">Target selected state.</param>
            private static void SetSelection(List<ConflictPathOption> options, bool selected)
            {
                foreach (ConflictPathOption option in options)
                {
                    option.Selected = selected;
                }
            }

            /// <summary>
            /// Closes window and emits confirmed selection.
            /// </summary>
            private void CloseWithSelection()
            {
                ImportConflictSelection selection = new ImportConflictSelection
                {
                    Confirmed = true
                };

                foreach (ConflictPathOption option in updateOptions.Where(option => option.Selected))
                {
                    selection.PathsToUpdate.Add(option.FullPath);
                }

                foreach (ConflictPathOption option in deleteOptions.Where(option => option.Selected))
                {
                    selection.PathsToDelete.Add(option.FullPath);
                }

                NotifyClose(selection);
                Close();
            }

            /// <summary>
            /// Closes window and emits cancellation.
            /// </summary>
            private void CloseWithCancel()
            {
                NotifyClose(new ImportConflictSelection { Confirmed = false });
                Close();
            }

            /// <summary>
            /// Emits close callback once.
            /// </summary>
            /// <param name="selection">Selection result.</param>
            private void NotifyClose(ImportConflictSelection selection)
            {
                if (callbackSent)
                {
                    return;
                }

                callbackSent = true;
                Action<ImportConflictSelection> callback = onClose;
                onClose = null;
                if (callback != null)
                {
                    callback(selection);
                }
            }
        }

        /// <summary>
        /// Resolves the configured target canvas in the current scene.
        /// </summary>
        /// <returns>The matching canvas if found; otherwise null.</returns>
        private static Canvas ResolveTargetCanvas()
        {
            if (string.IsNullOrEmpty(TargetCanvasPath))
            {
                return null;
            }

            Canvas[] canvases = FindAllCanvases();
            foreach (Canvas canvas in canvases)
            {
                if (GetHierarchyPath(canvas.transform) == TargetCanvasPath)
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
        private static Canvas[] FindAllCanvases()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType<Canvas>();
#endif
        }

        /// <summary>
        /// Gets the rect size of the target canvas if possible; otherwise falls back to PSD canvas size.
        /// </summary>
        /// <param name="targetCanvas">The target canvas.</param>
        /// <returns>Canvas rect size for mapping.</returns>
        private static Vector2 GetTargetCanvasRectSize(Canvas targetCanvas)
        {
            if (targetCanvas == null)
            {
                return CanvasSize;
            }

            CanvasScaler scaler = GetCanvasScaler(targetCanvas);
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                Vector2 referenceResolution = scaler.referenceResolution;
                if (referenceResolution.x > 0 && referenceResolution.y > 0)
                {
                    // For Scale With Screen Size, layout authoring coordinates are based on reference resolution.
                    return referenceResolution;
                }
            }

            RectTransform canvasRectTransform = targetCanvas.transform as RectTransform;
            if (canvasRectTransform == null)
            {
                return CanvasSize;
            }

            Rect rect = canvasRectTransform.rect;
            if (rect.width <= 0 || rect.height <= 0)
            {
                return CanvasSize;
            }

            return rect.size;
        }

        /// <summary>
        /// Gets the most relevant <see cref="CanvasScaler"/> for a target canvas.
        /// </summary>
        /// <param name="targetCanvas">The target canvas.</param>
        /// <returns>The canvas scaler if found; otherwise null.</returns>
        private static CanvasScaler GetCanvasScaler(Canvas targetCanvas)
        {
            if (targetCanvas == null)
            {
                return null;
            }

            CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                return scaler;
            }

            Canvas rootCanvas = targetCanvas.rootCanvas;
            return rootCanvas != null ? rootCanvas.GetComponent<CanvasScaler>() : null;
        }

        /// <summary>
        /// Gets a hierarchy path for the given transform in the form "Root/Child/SubChild".
        /// </summary>
        /// <param name="transform">The transform to build a path for.</param>
        /// <returns>The hierarchy path string.</returns>
        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            List<string> pathParts = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                pathParts.Add(current.name);
                current = current.parent;
            }

            pathParts.Reverse();
            return string.Join("/", pathParts.ToArray());
        }

        /// <summary>
        /// Constructs a tree collection based on the PSD layer groups from the raw list of layers.
        /// </summary>
        /// <param name="flatLayers">The flat list of all layers.</param>
        /// <returns>The layers reorganized into a tree structure based on the layer groups.</returns>
        private static List<Layer> BuildLayerTree(List<Layer> flatLayers)
        {
            // There is no tree to create if there are no layers
            if (flatLayers == null)
            {
                return null;
            }

            // PSD layers are stored backwards (with End Groups before Start Groups), so we must reverse them
            flatLayers.Reverse();

            List<Layer> tree = new List<Layer>();
            Layer currentGroupLayer = null;
            Stack<Layer> previousLayers = new Stack<Layer>();

            foreach (Layer layer in flatLayers)
            {
                if (IsEndGroup(layer) && currentGroupLayer != null)
                {
                    if (previousLayers.Count > 0)
                    {
                        Layer previousLayer = previousLayers.Pop();
                        previousLayer.Children.Add(currentGroupLayer);
                        currentGroupLayer = previousLayer;
                    }
                    else
                    {
                        tree.Add(currentGroupLayer);
                        currentGroupLayer = null;
                    }
                }
                else if (IsStartGroup(layer))
                {
                    // push the current layer
                    if (currentGroupLayer != null)
                    {
                        previousLayers.Push(currentGroupLayer);
                    }

                    currentGroupLayer = layer;
                }
                else if (layer.Rect.width != 0 && layer.Rect.height != 0)
                {
                    // It must be a text layer or image layer
                    if (currentGroupLayer != null)
                    {
                        currentGroupLayer.Children.Add(layer);
                    }
                    else
                    {
                        tree.Add(layer);
                    }
                }
            }

            // flush all remaining unclosed groups into the tree
            while (currentGroupLayer != null)
            {
                if (currentGroupLayer.Children.Count > 0)
                {
                    tree.Add(currentGroupLayer);
                }

                currentGroupLayer = previousLayers.Count > 0 ? previousLayers.Pop() : null;
            }

            return tree;
        }

        /// <summary>
        /// Fixes any layer names that would cause problems.
        /// </summary>
        /// <param name="name">The name of the layer</param>
        /// <returns>The fixed layer name</returns>
        private static string MakeNameSafe(string name)
        {
            string newName = MakeNameSafeSilently(name);

            if (name != newName)
            {
                Debug.Log(string.Format("Layer name \"{0}\" was changed to \"{1}\"", name, newName));
            }

            return newName;
        }

        /// <summary>
        /// Returns true if the given <see cref="Layer"/> is marking the start of a layer group.
        /// Uses the 'lsct' section divider tag when available (type 1 or 2); falls back to the
        /// pixel-data-irrelevant flag for all other cases (original behaviour).
        /// </summary>
        /// <param name="layer">The <see cref="Layer"/> to check if it's the start of a group</param>
        /// <returns>True if the layer starts a group, otherwise false.</returns>
        private static bool IsStartGroup(Layer layer)
        {
            // SectionType 3 (bounding) marks the end of a group, never a start.
            if (layer.IsGroupEnd)
            {
                return false;
            }

            return layer.IsGroupStart || layer.IsPixelDataIrrelevant;
        }

        /// <summary>
        /// Returns true if the given <see cref="Layer"/> is marking the end of a layer group.
        /// </summary>
        /// <param name="layer">The <see cref="Layer"/> to check if it's the end of a group.</param>
        /// <returns>True if the layer ends a group, otherwise false.</returns>
        private static bool IsEndGroup(Layer layer)
        {
            return layer.Name.Contains("</Layer set>") ||
                layer.Name.Contains("</Layer group>") ||
                (layer.Name == " copy" && layer.Rect.height == 0);
        }

        /// <summary>
        /// Gets full path to the current Unity project. In the form "C:/Project/".
        /// </summary>
        /// <returns>The full path to the current Unity project.</returns>
        private static string GetFullProjectPath()
        {
            string projectDirectory = Application.dataPath;

            // remove the Assets folder from the end since each imported asset has it already in its local path
            if (projectDirectory.EndsWith("Assets"))
            {
                projectDirectory = projectDirectory.Remove(projectDirectory.Length - "Assets".Length);
            }

            return projectDirectory;
        }

        /// <summary>
        /// Gets the relative path of a full path to an asset.
        /// </summary>
        /// <param name="fullPath">The full path to the asset.</param>
        /// <returns>The relative path to the asset.</returns>
        private static string GetRelativePath(string fullPath)
        {
            return fullPath.Replace(GetFullProjectPath(), string.Empty).Replace('\\', '/');
        }

        /// <summary>
        /// Builds a compact layer description for diagnostic logs.
        /// </summary>
        /// <param name="layer">Layer to describe.</param>
        /// <returns>Readable layer description.</returns>
        private static string DescribeLayerForLog(Layer layer)
        {
            if (layer == null)
            {
                return "<null layer>";
            }

            string name = string.IsNullOrEmpty(layer.Name) ? "<unnamed>" : layer.Name.Replace('\r', ' ').Replace('\n', ' ');
            Rect rect = layer.Rect;
            return "\"" + name + "\"" +
                " rect=(" + rect.x + "," + rect.y + "," + rect.width + "x" + rect.height + ")" +
                " children=" + layer.Children.Count +
                " text=" + layer.IsTextLayer +
                " visible=" + layer.Visible +
                " opacity=" + layer.Opacity;
        }

        #region Layer Exporting Methods

        /// <summary>
        /// Counts all layers recursively in the tree (including groups).
        /// </summary>
        private static int CountAllLayers(List<Layer> tree)
        {
            int count = 0;
            foreach (Layer layer in tree)
            {
                count++;
                if (layer.Children.Count > 0)
                {
                    count += CountAllLayers(layer.Children);
                }
            }
            return count;
        }

        /// <summary>
        /// Updates the progress bar with the current layer name.
        /// </summary>
        private static void UpdateExportProgress(string layerName)
        {
            progressExportedLayers++;
            float progress = progressTotalLayers > 0
                ? (float)progressExportedLayers / progressTotalLayers
                : 0f;
            EditorUtility.DisplayProgressBar(
                "PSD Layout Tool 2",
                string.Format("导出图层 ({0}/{1}): {2}", progressExportedLayers, progressTotalLayers, layerName),
                progress);
        }

        /// <summary>
        /// Processes and saves the layer tree.
        /// </summary>
        /// <param name="tree">The layer tree to export.</param>
        private static void ExportTree(List<Layer> tree)
        {
            // we must go through the tree in reverse order since Unity draws from back to front, but PSDs are stored front to back
            for (int i = tree.Count - 1; i >= 0; i--)
            {
                ExportLayer(tree[i]);
            }
        }

        /// <summary>
        /// Exports a single layer from the tree.
        /// </summary>
        /// <param name="layer">The layer to export.</param>
        private static void ExportLayer(Layer layer)
        {
            UpdateExportProgress(layer.Name);
            PsdLogger.Step("Export layer: " + DescribeLayerForLog(layer));
            LayerImportInfo info = GetLayerInfo(layer);
            if (info == null)
            {
                PsdLogger.Warning("Skip layer because no import info was found: " + DescribeLayerForLog(layer));
                return;
            }

            PsdCommonAssetReference commonReference;
            if (PsdCommonAssetNameParser.TryParse(layer.Name, out commonReference))
            {
                ExportCommonLayer(layer, info, commonReference);
                return;
            }

            if (info.IsFolderLike)
            {
                ExportFolderLayer(layer);
            }
            else
            {
                ExportArtLayer(layer);
            }
        }

        /// <summary>
        /// Exports a <see cref="Layer"/> that is a folder containing child layers.
        /// </summary>
        /// <param name="layer">The layer that is a folder.</param>
        private static void ExportFolderLayer(Layer layer)
        {
            LayerImportInfo info = GetLayerInfo(layer);
            if (info == null)
            {
                return;
            }

            if (info.IsButtonGroup)
            {
                PsdLogger.Info("Process button group: " + DescribeLayerForLog(layer));
                bool createRuntimeButton =
                    (LayoutInScene || CreatePrefab) &&
                    UseUnityUI &&
                    info.EffectiveVisible &&
                    HasVisibleButtonRuntimeContent(layer);

                if (createRuntimeButton)
                {
                    CreateUIButton(layer);
                }

                foreach (Layer child in layer.Children)
                {
                    LayerImportInfo childInfo = GetLayerInfo(child);
                    if (!ShouldButtonGroupChildEmitTexture(childInfo))
                    {
                        continue;
                    }

                    if (createRuntimeButton && IsButtonChildHandledByRuntime(childInfo))
                    {
                        continue;
                    }

                    ExportLayerTexturesOnly(child);
                }

                return;
            }

            if (info.IsAnimationGroup)
            {
                PsdLogger.Info("Process animation group: " + DescribeLayerForLog(layer));
                string oldPath = currentPath;
                GameObject oldGroupObject = currentGroupGameObject;
                List<Layer> visibleFrames = GetVisibleAnimationFrameLayers(layer);
                bool createRuntimeAnimation =
                    (LayoutInScene || CreatePrefab) &&
                    !UseUnityUI &&
                    info.EffectiveVisible &&
                    visibleFrames.Count > 0;

                currentPath = Path.Combine(currentPath, GetOutputFolderName(layer));
                PsdLogger.Info("Create animation output directory: " + currentPath);
                Directory.CreateDirectory(currentPath);

                if (createRuntimeAnimation)
                {
                    CreateAnimation(layer);
                }

                HashSet<Layer> runtimeFrames = new HashSet<Layer>(visibleFrames);
                foreach (Layer child in layer.Children)
                {
                    if (createRuntimeAnimation && runtimeFrames.Contains(child))
                    {
                        continue;
                    }

                    ExportLayerTexturesOnly(child);
                }

                currentPath = oldPath;
                currentGroupGameObject = oldGroupObject;
                return;
            }

            // it is a "normal" folder layer that contains children layers
            string oldDirectory = currentPath;
            GameObject oldGroup = currentGroupGameObject;
            UiLayoutContext oldLayoutContext = currentGroupLayoutContext;

            currentPath = Path.Combine(currentPath, GetOutputFolderName(layer));
            PsdLogger.Info("Enter PSD group for runtime hierarchy: " + currentPath);

            bool createGroupObject =
                (LayoutInScene || CreatePrefab) &&
                info.EffectiveVisible &&
                HasVisibleRuntimeContent(layer);

            if (createGroupObject)
            {
                if (UseUnityUI)
                {
                    currentGroupGameObject = new GameObject(GetRuntimeObjectName(layer), typeof(RectTransform));
                    RectTransform groupTransform = currentGroupGameObject.GetComponent<RectTransform>();
                    if (oldGroup != null)
                    {
                        groupTransform.SetParent(oldGroup.transform, false);
                    }

                    currentGroupLayoutContext = ApplyLayerUILayout(groupTransform, layer, info.AnchorPreset);
                    RegisterGeneratedUiNode(layer, groupTransform);
                }
                else
                {
                    currentGroupGameObject = new GameObject(GetRuntimeObjectName(layer));
                    if (oldGroup != null)
                    {
                        currentGroupGameObject.transform.parent = oldGroup.transform;
                    }
                }
            }

            ExportTree(layer.Children);

            currentPath = oldDirectory;
            currentGroupGameObject = oldGroup;
            currentGroupLayoutContext = oldLayoutContext;
        }

        /// <summary>
        /// Checks if the string contains the given string, while ignoring any casing.
        /// </summary>
        /// <param name="source">The source string to check.</param>
        /// <param name="toCheck">The string to search for in the source string.</param>
        /// <returns>True if the string contains the search string, otherwise false.</returns>
        private static bool ContainsIgnoreCase(this string source, string toCheck)
        {
            return source.IndexOf(toCheck, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Replaces any instance of the given string in this string with the given string.
        /// </summary>
        /// <param name="str">The string to replace sections in.</param>
        /// <param name="oldValue">The string to search for.</param>
        /// <param name="newValue">The string to replace the search string with.</param>
        /// <returns>The replaced string.</returns>
        private static string ReplaceIgnoreCase(this string str, string oldValue, string newValue)
        {
            StringBuilder sb = new StringBuilder();

            int previousIndex = 0;
            int index = str.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
            while (index != -1)
            {
                sb.Append(str.Substring(previousIndex, index - previousIndex));
                sb.Append(newValue);
                index += oldValue.Length;

                previousIndex = index;
                index = str.IndexOf(oldValue, index, StringComparison.OrdinalIgnoreCase);
            }

            sb.Append(str.Substring(previousIndex));

            return sb.ToString();
        }

        /// <summary>
        /// Exports an art layer as an image file and sprite.  It can also generate text meshes from text layers.
        /// </summary>
        /// <param name="layer">The art layer to export.</param>
        private static void ExportArtLayer(Layer layer)
        {
            LayerImportInfo info = GetLayerInfo(layer);
            if (info == null)
            {
                PsdLogger.Warning("Skip art layer because no import info was found: " + DescribeLayerForLog(layer));
                return;
            }

            bool createRuntimeObject = (LayoutInScene || CreatePrefab) && info.EffectiveVisible;
            bool exportTextureOnly = ShouldExportTextureOnly(info);
            PsdLogger.Info(
                "Art layer decision: createRuntimeObject=" + createRuntimeObject +
                ", exportTextureOnly=" + exportTextureOnly +
                ", layer=" + DescribeLayerForLog(layer));

            if (!layer.IsTextLayer)
            {
                if (createRuntimeObject)
                {
                    // create a sprite from the layer to lay it out in the scene
                    if (!UseUnityUI)
                    {
                        CreateSpriteGameObject(layer);
                    }
                    else
                    {
                        CreateUIImage(layer);
                    }
                }
                else if (exportTextureOnly)
                {
                    CreateTextureAssetWithoutGameObject(layer);
                }
            }
            else
            {
                // it is a text layer
                if (createRuntimeObject)
                {
                    // create text mesh
                    if (!UseUnityUI)
                    {
                        CreateTextGameObject(layer);
                    }
                    else
                    {
                        CreateUIText(layer);
                    }
                }
                else if (exportTextureOnly)
                {
                    CreateTextureAssetWithoutGameObject(layer);
                }
            }
        }

        /// <summary>
        /// Exports only generated assets for a layer subtree without creating runtime objects.
        /// </summary>
        /// <param name="layer">Layer to export.</param>
        private static void ExportLayerTexturesOnly(Layer layer)
        {
            // A Prefab import owns only the sprites referenced by the generated
            // hierarchy, Button states, or animation. This traversal exists for
            // texture-only and scene-layout imports, so it must not create
            // orphaned PNGs during Prefab generation.
            if (CreatePrefab)
            {
                return;
            }

            LayerImportInfo info = GetLayerInfo(layer);
            if (info == null)
            {
                PsdLogger.Warning("Skip texture-only layer because no import info was found: " + DescribeLayerForLog(layer));
                return;
            }

            if (info.IsButtonGroup)
            {
                foreach (Layer child in layer.Children)
                {
                    if (ShouldButtonGroupChildEmitTexture(GetLayerInfo(child)))
                    {
                        ExportLayerTexturesOnly(child);
                    }
                }

                return;
            }

            if (DoesLayerCreateOutputDirectory(info))
            {
                string oldPath = currentPath;
                currentPath = Path.Combine(currentPath, GetOutputFolderName(layer));
                PsdLogger.Info("Traverse texture-only PSD group: " + currentPath);

                foreach (Layer child in layer.Children)
                {
                    ExportLayerTexturesOnly(child);
                }

                currentPath = oldPath;
                return;
            }

            if (ShouldLayerEmitTextureFile(info) || ShouldButtonGroupChildEmitTexture(info))
            {
                CreateTextureAssetWithoutGameObject(layer);
            }
        }

        /// <summary>
        /// Saves the given <see cref="Layer"/> as a PNG on the hard drive.
        /// </summary>
        /// <param name="layer">The <see cref="Layer"/> to save as a PNG.</param>
        /// <returns>The filepath to the created PNG file.</returns>
        private static string CreatePNG(Layer layer, bool allowTextLayer = false)
        {
            string file = string.Empty;

            if (layer.Children.Count == 0 && layer.Rect.width > 0 && layer.Rect.height > 0 && (!layer.IsTextLayer || allowTextLayer))
            {
                file = GetTextureOutputPath(currentOutputRootDirectory, layer);
                if (!ShouldOverwriteExistingGeneratedFile(file))
                {
                    PsdLogger.Info("Skip PNG write by overwrite selection: " + file + " | " + DescribeLayerForLog(layer));
                    return file;
                }

                // decode the layer into a texture
                PsdLogger.Step("Decode layer image: " + DescribeLayerForLog(layer));
                Texture2D texture = ImageDecoder.DecodeImage(layer);
                if (texture == null)
                {
                    PsdLogger.Warning("Skip PNG because the PSD layer could not be decoded: " + DescribeLayerForLog(layer));
                    return string.Empty;
                }

                try
                {
                    byte[] png = texture.EncodeToPNG();
                    if (png == null || png.Length == 0)
                    {
                        PsdLogger.Warning("Skip PNG because Unity could not encode the PSD layer: " + DescribeLayerForLog(layer));
                        return string.Empty;
                    }

                    PsdNineSliceNameRule nineSliceRule;
                    if (TryGetNineSliceConversionRule(layer, out nineSliceRule))
                    {
                        byte[] originalPng = png;
                        byte[] processedPng;
                        PsdNineSliceBorder appliedBorder;
                        string reason;
                        bool processed = PsdNineSliceUnityAutoProcessor.TryProcess(
                            texture,
                            nineSliceRule,
                            GetTargetCanvasScaleX(),
                            GetTargetCanvasScaleY(),
                            out processedPng,
                            out appliedBorder,
                            out reason);
                        if (!processed && nineSliceRule.HasExplicitBorder && !HasEnabledManualNineSliceOverride(layer))
                        {
                            PsdNineSliceNameRule fallbackRule;
                            if (PsdNineSliceNameRules.TryParse(layer.Name, out fallbackRule) &&
                                !fallbackRule.HasExplicitBorder)
                            {
                                processed = PsdNineSliceUnityAutoProcessor.TryProcess(
                                    texture,
                                    fallbackRule,
                                    GetTargetCanvasScaleX(),
                                    GetTargetCanvasScaleY(),
                                    out processedPng,
                                    out appliedBorder,
                                    out reason);
                                if (processed)
                                {
                                    nineSliceRule = fallbackRule;
                                    PsdLogger.Info(
                                        "Ignored incompatible explicit 9-slice metadata and used the PSD name rule. Layer: " +
                                        DescribeLayerForLog(layer));
                                }
                            }
                        }

                        if (processed)
                        {
                            png = processedPng;
                            RegisterAutomaticNineSliceBorder(
                                layer,
                                PsdNineSliceTextureProcessor.ToUnityBorder(appliedBorder),
                                UseTargetCanvasCoordinates && ScaleToTargetCanvas);
                            PsdLogger.Info(
                                "Auto 9-slice conversion from PSD name. mode=" + nineSliceRule.Mode +
                                ", border(left,top,right,bottom)=" +
                                appliedBorder.Left + "," + appliedBorder.Top + "," +
                                appliedBorder.Right + "," + appliedBorder.Bottom +
                                ", layer=" + DescribeLayerForLog(layer) +
                                (string.IsNullOrEmpty(reason) ? string.Empty : ", " + reason));
                        }
                        else
                        {
                            png = originalPng;
                            ClearAutomaticNineSliceBorder(layer);
                            PsdLogger.Warning(
                                "PSD 9-slice tag kept the original PNG because analysis failed. " +
                                reason + " Layer: " + DescribeLayerForLog(layer));
                        }
                    }
                    else
                    {
                        ClearAutomaticNineSliceBorder(layer);
                    }

                    if (png == null || png.Length == 0)
                    {
                        PsdLogger.Warning("Skip PNG because the processed PSD layer produced no encoded bytes: " + DescribeLayerForLog(layer));
                        return string.Empty;
                    }

                    int pngWidth;
                    int pngHeight;
                    Color32[] pngPixels;
                    string pngContentHash = ComputePngContentHash(png, out pngWidth, out pngHeight, out pngPixels);
                    string borderContract = GetTextureBorderContract(layer);
                    string contentHash = pngContentHash + "|" + borderContract;
                    string existingFile;
                    bool exactMatch = currentPngPathByContentHash.TryGetValue(contentHash, out existingFile);
                    bool visualMatch = !exactMatch && currentTextureReuseIndex.TryFind(
                        GetTextureBaseName(layer),
                        pngContentHash,
                        borderContract,
                        pngWidth,
                        pngHeight,
                        pngPixels,
                        out existingFile);
                    if (exactMatch || visualMatch)
                    {
                        PsdLogger.Info(
                            (exactMatch
                                ? "Reuse identical PNG instead of exporting a duplicate: "
                                : "Reuse same semantic-name PNG instead of exporting a duplicate: ") + file +
                            " -> " + existingFile + " | " + DescribeLayerForLog(layer));
                        if (!string.Equals(file, existingFile, StringComparison.OrdinalIgnoreCase) &&
                            ShouldOverwriteExistingGeneratedFile(file))
                        {
                            currentPendingRedundantTexturePaths.Add(file);
                        }
                        return existingFile;
                    }

                    PsdLogger.Step("Write PNG: " + file);
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    File.WriteAllBytes(file, png);
                    currentPngPathByContentHash[contentHash] = file;
                    currentTextureReuseIndex.Add(
                        GetTextureBaseName(layer),
                        pngContentHash,
                        borderContract,
                        pngWidth,
                        pngHeight,
                        pngPixels,
                        file);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
            else
            {
                PsdLogger.Info("Skip PNG for non-exportable layer: " + DescribeLayerForLog(layer) + ", allowTextLayer=" + allowTextLayer);
            }

            return file;
        }

        private static string GetTextureOutputPath(string outputRootDirectory, Layer layer)
        {
            string textureDirectory = ResolveTextureOutputDirectory(outputRootDirectory);
            string textureName = GetTextureBaseName(layer) + "_" + layer.Id + ".png";
            return Path.Combine(textureDirectory, textureName);
        }

        private static string ResolveTextureOutputDirectory(string outputRootDirectory)
        {
            if (!string.IsNullOrEmpty(TextureOutputPath))
            {
                return Path.Combine(
                    GetFullProjectPath(),
                    TextureOutputPath.Replace('/', Path.DirectorySeparatorChar));
            }

            return Path.Combine(outputRootDirectory, "Texture");
        }

        private static void FinalizeRedundantTextureCleanup(string prefabRelativePath)
        {
            if (currentPendingRedundantTexturePaths == null || currentPendingRedundantTexturePaths.Count == 0)
            {
                return;
            }

            HashSet<string> prefabDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (CreatePrefab && !string.IsNullOrEmpty(prefabRelativePath))
            {
                GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabRelativePath);
                if (savedPrefab == null)
                {
                    PsdLogger.Warning("Keep redundant PNGs because the generated Prefab could not be reloaded: " + prefabRelativePath);
                    return;
                }

                string[] dependencies = AssetDatabase.GetDependencies(prefabRelativePath, true);
                for (int i = 0; i < dependencies.Length; i++)
                {
                    prefabDependencies.Add(dependencies[i]);
                }
            }

            int deletedCount = 0;
            foreach (string redundantPath in currentPendingRedundantTexturePaths)
            {
                string assetPath = ToProjectAssetPath(redundantPath);
                if (string.IsNullOrEmpty(assetPath) || prefabDependencies.Contains(assetPath))
                {
                    PsdLogger.Warning("Keep redundant PNG because the saved Prefab still references it: " + redundantPath);
                    continue;
                }

                DeleteFileWithMeta(redundantPath);
                deletedCount++;
            }

            if (deletedCount > 0)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                PsdLogger.Info("Deleted redundant PNGs after Prefab dependency validation: " + deletedCount);
            }
        }

        private static string ToProjectAssetPath(string fullPath)
        {
            string normalizedFullPath = NormalizePath(fullPath);
            string projectRoot = NormalizePath(GetFullProjectPath()).TrimEnd('/');
            if (!normalizedFullPath.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return normalizedFullPath.Substring(projectRoot.Length + 1);
        }

        private static string GetTextureBorderContract(Layer layer)
        {
            Vector4 border;
            if (!TryGetNineSliceBorder(layer, out border))
            {
                return "ordinary";
            }

            return "nine:" +
                border.x.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                border.y.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                border.z.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                border.w.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string ComputePngContentHash(byte[] png)
        {
            int width;
            int height;
            Color32[] pixels;
            return ComputePngContentHash(png, out width, out height, out pixels);
        }

        private static string ComputePngContentHash(
            byte[] png,
            out int width,
            out int height,
            out Color32[] pixels)
        {
            width = 0;
            height = 0;
            pixels = null;
            Texture2D decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!decoded.LoadImage(png, false))
                {
                    return ComputeRawHash(png);
                }

                width = decoded.width;
                height = decoded.height;
                pixels = decoded.GetPixels32();
                byte[] canonical = new byte[8 + pixels.Length * 4];
                Buffer.BlockCopy(BitConverter.GetBytes(decoded.width), 0, canonical, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(decoded.height), 0, canonical, 4, 4);
                for (int i = 0; i < pixels.Length; i++)
                {
                    int offset = 8 + i * 4;
                    canonical[offset] = pixels[i].a == 0 ? (byte)0 : pixels[i].r;
                    canonical[offset + 1] = pixels[i].a == 0 ? (byte)0 : pixels[i].g;
                    canonical[offset + 2] = pixels[i].a == 0 ? (byte)0 : pixels[i].b;
                    canonical[offset + 3] = pixels[i].a;
                }

                return ComputeRawHash(canonical);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(decoded);
            }
        }

        private static string ComputeRawHash(byte[] bytes)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] hash = algorithm.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        /// <summary>
        /// Exports a texture asset without creating a scene or prefab object.
        /// </summary>
        /// <param name="layer">Layer to export.</param>
        private static void CreateTextureAssetWithoutGameObject(Layer layer)
        {
            string file = CreatePNG(layer, true);
            if (!string.IsNullOrEmpty(file) && (LayoutInScene || CreatePrefab))
            {
                ImportSprite(GetRelativePath(file), PsdName, layer);
            }
        }

        /// <summary>
        /// Creates a <see cref="Sprite"/> from the given <see cref="Layer"/>.
        /// </summary>
        /// <param name="layer">The <see cref="Layer"/> to use to create a <see cref="Sprite"/>.</param>
        /// <returns>The created <see cref="Sprite"/> object.</returns>
        private static Sprite CreateSprite(Layer layer)
        {
            return CreateSprite(layer, PsdName);
        }

        /// <summary>
        /// Creates a <see cref="Sprite"/> from the given <see cref="Layer"/>.
        /// </summary>
        /// <param name="layer">The <see cref="Layer"/> to use to create a <see cref="Sprite"/>.</param>
        /// <param name="packingTag">The tag used for Unity's atlas packer.</param>
        /// <returns>The created <see cref="Sprite"/> object.</returns>
        private static Sprite CreateSprite(Layer layer, string packingTag)
        {
            Sprite sprite = null;

            if (layer.Children.Count == 0 && layer.Rect.width > 0)
            {
                string file = CreatePNG(layer);
                if (!string.IsNullOrEmpty(file))
                {
                    sprite = ImportSprite(GetRelativePath(file), packingTag, layer);
                }
            }

            return sprite;
        }

        /// <summary>
        /// Imports the <see cref="Sprite"/> at the given path, relative to the Unity project. For example "Assets/Textures/texture.png".
        /// </summary>
        /// <param name="relativePathToSprite">The path to the sprite, relative to the Unity project "Assets/Textures/texture.png".</param>
        /// <param name="packingTag">The tag to use for Unity's atlas packing.</param>
        /// <param name="layer">The PSD layer that owns the generated texture.</param>
        /// <returns>The imported image as a <see cref="Sprite"/> object.</returns>
        private static Sprite ImportSprite(string relativePathToSprite, string packingTag, Layer layer)
        {
            _ = packingTag;
            relativePathToSprite = relativePathToSprite.Replace('\\', '/');
            PsdLogger.Step("Import sprite asset before applying settings: " + relativePathToSprite);
            AssetDatabase.ImportAsset(relativePathToSprite, ImportAssetOptions.ForceUpdate);

            // change the importer to make the texture a sprite
            TextureImporter textureImporter = AssetImporter.GetAtPath(relativePathToSprite) as TextureImporter;
            if (textureImporter != null)
            {
                PsdLogger.Info("Apply TextureImporter sprite settings: " + relativePathToSprite);
                textureImporter.textureType = TextureImporterType.Sprite;
                textureImporter.mipmapEnabled = false;
                // PSD layer color bytes are authored for display. The project
                // uses Linear rendering, so these generated UI sprites must
                // retain sRGB sampling instead of depending on importer defaults.
                textureImporter.sRGBTexture = true;
                textureImporter.spriteImportMode = SpriteImportMode.Single;
                textureImporter.spritePivot = new Vector2(0.5f, 0.5f);
                textureImporter.maxTextureSize = 2048;
                textureImporter.npotScale = PsdNineSliceImportPolicy.GeneratedTextureNpotScale;
                textureImporter.spritePixelsPerUnit = PixelsToUnits;

                if (layer != null && layer.Id != 0U)
                {
                    textureImporter.userData = PsdNineSliceAssetState.WriteLayerIdentity(textureImporter.userData, layer.Id);
                }

                Vector4 nineSliceBorder;
                int generatedWidth;
                int generatedHeight;
                textureImporter.GetSourceTextureWidthAndHeight(out generatedWidth, out generatedHeight);
                if (TryGetNineSliceBorder(layer, out nineSliceBorder) &&
                    IsNineSliceBorderValidForGeneratedTexture(generatedWidth, generatedHeight, nineSliceBorder, relativePathToSprite))
                {
                    Vector4 canvasBorder = IsAutomaticNineSliceBorderInTargetCoordinates(layer)
                        ? nineSliceBorder
                        : ScaleNineSliceBorderForTargetCanvas(nineSliceBorder);
                    // Unity expects (left, bottom, right, top), while the PSD
                    // tag uses the more author-friendly (left, top, right, bottom).
                    textureImporter.spriteBorder = canvasBorder;
                    PsdLogger.Info(
                        "Apply 9-slice border (left,bottom,right,top), raw=" +
                        nineSliceBorder + ", canvas=" + canvasBorder + ": " + relativePathToSprite);
                }
                else
                {
                    // Name/XMP metadata is now the only source of automatic
                    // nine-slice. Do not revive stale manual recipes from a
                    // TextureImporter meta file during an incremental update.
                    textureImporter.spriteBorder = Vector4.zero;
                    bool isUntaggedLayer = PsdNineSliceImportPolicy.ShouldClearUntaggedBorder(
                        layer != null ? layer.Name : string.Empty);
                    PsdLogger.Info(
                        (isUntaggedLayer
                            ? "Cleared stale 9-slice border for untagged PSD layer: "
                            : "Cleared unresolved PSD 9-slice border: ") +
                        relativePathToSprite);
                }
            }
            else
            {
                PsdLogger.Warning("TextureImporter was not found for generated sprite: " + relativePathToSprite);
            }

            PsdLogger.Step("Reimport sprite asset after applying settings: " + relativePathToSprite);
            AssetDatabase.ImportAsset(relativePathToSprite, ImportAssetOptions.ForceUpdate);

            Sprite sprite = (Sprite)AssetDatabase.LoadAssetAtPath(relativePathToSprite, typeof(Sprite));
            if (sprite == null)
            {
                PsdLogger.Warning("Sprite load returned null: " + relativePathToSprite);
            }
            else
            {
                PsdLogger.Info("Sprite loaded: " + sprite.name + " from " + relativePathToSprite);
            }

            return sprite;
        }

        /// <summary>
        /// Resolves a font for text layers, preferring the PSD font and falling back to common CJK fonts.
        /// </summary>
        /// <param name="layer">The text layer.</param>
        /// <returns>A usable Unity font.</returns>
        private static Font GetFontForLayer(Layer layer)
        {
            List<string> fontCandidates = new List<string>();
            if (!string.IsNullOrEmpty(layer.FontName))
            {
                fontCandidates.Add(layer.FontName.Trim());

                // Unity does not expose a stable API for enumerating all mounted
                // OS fonts. CreateDynamicFontFromOSFont will resolve the exact
                // mounted family name when it is available, so keep the PSD name
                // as the first candidate and use the fallback list below.
            }

            fontCandidates.Add("Microsoft YaHei");
            fontCandidates.Add("SimHei");
            fontCandidates.Add("SimSun");
            fontCandidates.Add("PingFang SC");
            fontCandidates.Add("Heiti SC");
            fontCandidates.Add("Noto Sans CJK SC");
            fontCandidates.Add("Arial Unicode MS");
            fontCandidates.Add("Arial");

            foreach (string fontName in fontCandidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(fontName))
                {
                    continue;
                }

                try
                {
                    Font font = Font.CreateDynamicFontFromOSFont(fontName, Mathf.Max(1, Mathf.CeilToInt(layer.FontSize)));
                    if (font != null)
                    {
                        return font;
                    }
                }
                catch
                {
                    // Ignore unavailable fonts and try the next candidate.
                }
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        /// <summary>Normalizes a Photoshop or OS font display name for matching.</summary>
        private static string NormalizeFontName(string fontName)
        {
            if (string.IsNullOrEmpty(fontName))
            {
                return string.Empty;
            }

            string normalized = fontName.Trim().Replace("-", " ").Replace("_", " ");
            normalized = Regex.Replace(normalized, "\\s+", " ");
            normalized = Regex.Replace(normalized, "\\s+(Regular|Normal|Book|系|标准|Std)$", string.Empty, RegexOptions.IgnoreCase);
            return normalized;
        }

        /// <summary>
        /// Creates a <see cref="GameObject"/> with a <see cref="TextMesh"/> from the given <see cref="Layer"/>.
        /// </summary>
        /// <param name="layer">The <see cref="Layer"/> to create a <see cref="TextMesh"/> from.</param>
        private static void CreateTextGameObject(Layer layer)
        {
            Color color = ApplyLayerOpacity(layer.FillColor, layer);

            float x = layer.Rect.x / PixelsToUnits;
            float y = layer.Rect.y / PixelsToUnits;
            y = (CanvasSize.y / PixelsToUnits) - y;
            float width = layer.Rect.width / PixelsToUnits;
            float height = layer.Rect.height / PixelsToUnits;

            GameObject gameObject = new GameObject(GetRuntimeObjectName(layer));
            gameObject.transform.position = new Vector3(x + (width / 2), y - (height / 2), currentDepth);
            gameObject.transform.parent = currentGroupGameObject.transform;

            currentDepth -= depthStep;

            Font font = GetFontForLayer(layer);

            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.material = font.material;
            meshRenderer.sortingOrder = currentSortingOrder++;

            TextMesh textMesh = gameObject.AddComponent<TextMesh>();
            textMesh.text = layer.Text;
            textMesh.font = font;
            textMesh.fontSize = 0;
            textMesh.characterSize = layer.FontSize / PixelsToUnits;
            textMesh.color = color;
            textMesh.anchor = TextAnchor.MiddleCenter;

            switch (layer.Justification)
            {
                case TextJustification.Left:
                    textMesh.alignment = TextAlignment.Left;
                    break;
                case TextJustification.Right:
                    textMesh.alignment = TextAlignment.Right;
                    break;
                case TextJustification.Center:
                    textMesh.alignment = TextAlignment.Center;
                    break;
            }
        }

        /// <summary>
        /// Creates a <see cref="GameObject"/> with a sprite from the given <see cref="Layer"/>
        /// </summary>
        /// <param name="layer">The <see cref="Layer"/> to create the sprite from.</param>
        /// <returns>The <see cref="SpriteRenderer"/> component attached to the new sprite <see cref="GameObject"/>.</returns>
        private static SpriteRenderer CreateSpriteGameObject(Layer layer)
        {
            float x = layer.Rect.x / PixelsToUnits;
            float y = layer.Rect.y / PixelsToUnits;
            y = (CanvasSize.y / PixelsToUnits) - y;
            float width = layer.Rect.width / PixelsToUnits;
            float height = layer.Rect.height / PixelsToUnits;

            GameObject gameObject = new GameObject(GetRuntimeObjectName(layer));
            gameObject.transform.position = new Vector3(x + (width / 2), y - (height / 2), currentDepth);
            gameObject.transform.parent = currentGroupGameObject.transform;

            currentDepth -= depthStep;

            SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = CreateSprite(layer);
            spriteRenderer.sortingOrder = currentSortingOrder++;
            return spriteRenderer;
        }

        /// <summary>
        /// Creates a Unity sprite animation from the given <see cref="Layer"/> that is a group layer.  It grabs all of the children art
        /// layers and uses them as the frames of the animation.
        /// </summary>
        /// <param name="layer">The group <see cref="Layer"/> to use to create the sprite animation.</param>
        private static void CreateAnimation(Layer layer)
        {
            LayerImportInfo info = GetLayerInfo(layer);
            if (info == null)
            {
                return;
            }

            List<Sprite> frames = new List<Sprite>();
            List<Layer> visibleFrames = GetVisibleAnimationFrameLayers(layer);
            if (visibleFrames.Count == 0)
            {
                return;
            }

            string animationAssetName = GetOutputFolderName(layer);
            float fps = info.AnimationFps;

            Layer firstChild = visibleFrames[0];
            SpriteRenderer spriteRenderer = CreateSpriteGameObject(firstChild);
            spriteRenderer.name = GetRuntimeObjectName(layer);

            foreach (Layer child in visibleFrames)
            {
                Sprite frame = CreateSprite(child, animationAssetName);
                if (frame != null)
                {
                    frames.Add(frame);
                }
            }

            if (frames.Count == 0)
            {
                UnityEngine.Object.DestroyImmediate(spriteRenderer.gameObject);
                return;
            }

            spriteRenderer.sprite = frames[0];

#if UNITY_5_3_OR_NEWER
            // Create Animator Controller with an Animation Clip
            UnityEditor.Animations.AnimatorController controller = new UnityEditor.Animations.AnimatorController();
            controller.AddLayer("Base Layer");

            UnityEditor.Animations.AnimatorControllerLayer controllerLayer = controller.layers[0];
            UnityEditor.Animations.AnimatorState state = controllerLayer.stateMachine.AddState(animationAssetName);
            state.motion = CreateSpriteAnimationClip(animationAssetName, frames, fps);

            string controllerPath = GetRelativePath(currentPath) + "/" + animationAssetName + ".controller";
            RuntimeAnimatorController runtimeController = controller;
            if (PrepareAssetPathForCreate(controllerPath))
            {
                AssetDatabase.CreateAsset(controller, controllerPath);
            }
            else
            {
                RuntimeAnimatorController existingController =
                    AssetDatabase.LoadAssetAtPath(controllerPath, typeof(RuntimeAnimatorController)) as RuntimeAnimatorController;
                if (existingController != null)
                {
                    runtimeController = existingController;
                }
            }
#else // Unity 4
            // Create Animator Controller with an Animation Clip
            UnityEditor.Animations.AnimatorController controller = new UnityEditor.Animations.AnimatorController();
            UnityEditor.Animations.AnimatorControllerLayer controllerLayer = controller.AddLayer("Base Layer");

            UnityEditor.Animations.AnimatorState state = controllerLayer.stateMachine.AddState(animationAssetName);
            state.SetAnimationClip(CreateSpriteAnimationClip(animationAssetName, frames, fps));

            string controllerPath = GetRelativePath(currentPath) + "/" + animationAssetName + ".controller";
            RuntimeAnimatorController runtimeController = controller;
            if (PrepareAssetPathForCreate(controllerPath))
            {
                AssetDatabase.CreateAsset(controller, controllerPath);
            }
            else
            {
                RuntimeAnimatorController existingController =
                    AssetDatabase.LoadAssetAtPath(controllerPath, typeof(RuntimeAnimatorController)) as RuntimeAnimatorController;
                if (existingController != null)
                {
                    runtimeController = existingController;
                }
            }
#endif

            // Add an Animator and assign it the controller
            Animator animator = spriteRenderer.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = runtimeController;
        }

        /// <summary>
        /// Creates an <see cref="AnimationClip"/> of a sprite animation using the given <see cref="Sprite"/> frames and frames per second.
        /// </summary>
        /// <param name="name">The name of the animation to create.</param>
        /// <param name="sprites">The list of <see cref="Sprite"/> objects making up the frames of the animation.</param>
        /// <param name="fps">The frames per second for the animation.</param>
        /// <returns>The newly constructed <see cref="AnimationClip"/></returns>
        private static AnimationClip CreateSpriteAnimationClip(string name, IList<Sprite> sprites, float fps)
        {
            float frameLength = 1f / fps;

            AnimationClip clip = new AnimationClip();
            clip.name = name;
            clip.frameRate = fps;
            clip.wrapMode = WrapMode.Loop;

            // The AnimationClipSettings cannot be set in Unity (as of 4.6) and must be editted via SerializedProperty
            // from: http://forum.unity3d.com/threads/can-mecanim-animation-clip-properties-be-edited-in-script.251772/
            SerializedObject serializedClip = new SerializedObject(clip);
            SerializedProperty serializedSettings = serializedClip.FindProperty("m_AnimationClipSettings");
            serializedSettings.FindPropertyRelative("m_LoopTime").boolValue = true;
            serializedClip.ApplyModifiedProperties();

            EditorCurveBinding curveBinding = new EditorCurveBinding();
            curveBinding.type = typeof(SpriteRenderer);
            curveBinding.propertyName = "m_Sprite";

            ObjectReferenceKeyframe[] keyFrames = new ObjectReferenceKeyframe[sprites.Count];

            for (int i = 0; i < sprites.Count; i++)
            {
                ObjectReferenceKeyframe kf = new ObjectReferenceKeyframe();
                kf.time = i * frameLength;
                kf.value = sprites[i];
                keyFrames[i] = kf;
            }

#if UNITY_5_3_OR_NEWER
            AnimationUtility.SetObjectReferenceCurve(clip, curveBinding, keyFrames);
#else // Unity 4
            AnimationUtility.SetAnimationType(clip, ModelImporterAnimationType.Generic);
            AnimationUtility.SetObjectReferenceCurve(clip, curveBinding, keyFrames);

            clip.ValidateIfRetargetable(true);
#endif

            string clipPath = GetRelativePath(currentPath) + "/" + name + ".anim";
            if (PrepareAssetPathForCreate(clipPath))
            {
                AssetDatabase.CreateAsset(clip, clipPath);
                return clip;
            }

            AnimationClip existingClip = AssetDatabase.LoadAssetAtPath(clipPath, typeof(AnimationClip)) as AnimationClip;
            if (existingClip != null)
            {
                return existingClip;
            }

            return clip;
        }

        #endregion

        #region Unity UI
        /// <summary>
        /// Creates the Unity UI event system game object that handles all input.
        /// </summary>
        private static void CreateUIEventSystem()
        {
            if (!GameObject.Find("EventSystem"))
            {
                GameObject gameObject = new GameObject("EventSystem");
                gameObject.AddComponent<EventSystem>();
                gameObject.AddComponent<StandaloneInputModule>();
            }
        }

        /// <summary>
        /// Creates a Unity UI <see cref="Canvas"/>.
        /// </summary>
        private static void CreateUICanvas()
        {
            Canvas = new GameObject(PsdName);

            Canvas canvas = Canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform transform = Canvas.GetComponent<RectTransform>();
            transform.sizeDelta = CanvasSize;

            CanvasScaler scaler = Canvas.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = PixelsToUnits;
            scaler.referencePixelsPerUnit = PixelsToUnits;

            Canvas.AddComponent<GraphicRaycaster>();
        }

        /// <summary>
        /// Creates a Unity UI <see cref="UnityEngine.UI.Image"/> <see cref="GameObject"/> with a <see cref="Sprite"/> from a PSD <see cref="Layer"/>.
        /// </summary>
        /// <param name="layer">The <see cref="Layer"/> to use to create the UI Image.</param>
        /// <returns>The newly constructed Image object.</returns>
        private static Image CreateUIImage(Layer layer)
        {
            LayerImportInfo info = GetLayerInfo(layer);
            AnchorNamePreset preset = info != null ? info.AnchorPreset : AnchorNamePreset.None;

            GameObject uiObject = new GameObject(GetRuntimeObjectName(layer), typeof(RectTransform));
            uiObject.transform.SetParent(currentGroupGameObject.transform, false);

            RectTransform uiTransform = uiObject.GetComponent<RectTransform>();
            ApplyLayerUILayout(uiTransform, layer, preset);
            RegisterGeneratedUiNode(layer, uiTransform);

            Image uiImage = uiObject.AddComponent<Image>();
            uiImage.sprite = CreateSprite(layer);
            ApplyImageLayoutBehavior(uiImage, preset);
            ApplyNineSliceImageBehavior(uiImage, layer);
            return uiImage;
        }

        /// <summary>
        /// Creates a Unity UI <see cref="UnityEngine.UI.Text"/> <see cref="GameObject"/> with the text from a PSD <see cref="Layer"/>.
        /// </summary>
        /// <param name="layer">The <see cref="Layer"/> used to create the <see cref="UnityEngine.UI.Text"/> from.</param>
        private static void CreateUIText(Layer layer)
        {
            LayerImportInfo info = GetLayerInfo(layer);
            AnchorNamePreset preset = info != null ? info.AnchorPreset : AnchorNamePreset.None;

            Color color = ApplyLayerOpacity(layer.FillColor, layer);

            GameObject uiObject = new GameObject(GetRuntimeObjectName(layer), typeof(RectTransform));
            uiObject.transform.SetParent(currentGroupGameObject.transform, false);

            RectTransform uiTransform = uiObject.GetComponent<RectTransform>();
            ApplyLayerUILayout(uiTransform, layer, preset);
            RegisterGeneratedUiNode(layer, uiTransform);

            if (UseTextMeshPro)
            {
                CreateTextMeshProComponent(uiObject, layer, preset);
                return;
            }

            Font font = GetFontForLayer(layer);

            Text textUI = uiObject.AddComponent<Text>();
            textUI.text = GetLegacyUiText(layer.Text, layer.TextStyle);
            textUI.font = font;

            float fontSize = GetUIFontSize(layer);
            textUI.fontSize = Mathf.Max(1, Mathf.RoundToInt(fontSize));

            textUI.color = color;
            textUI.alignment = TextAnchor.MiddleCenter;
            textUI.horizontalOverflow = HorizontalWrapMode.Overflow;
            textUI.verticalOverflow = VerticalWrapMode.Overflow;
            textUI.resizeTextForBestFit = false;
            textUI.raycastTarget = false;

            ApplyTextStyle(textUI, layer);

            switch (layer.Justification)
            {
                case TextJustification.Left:
                    textUI.alignment = TextAnchor.MiddleLeft;
                    break;
                case TextJustification.Right:
                    textUI.alignment = TextAnchor.MiddleRight;
                    break;
                case TextJustification.Center:
                    textUI.alignment = TextAnchor.MiddleCenter;
                    break;
            }
        }

        /// <summary>
        /// Creates a TextMeshProUGUI component and applies the selected font and
        /// style material. The material is cached by a stable style signature so
        /// incremental PSD imports do not create duplicate materials.
        /// </summary>
        private static void CreateTextMeshProComponent(GameObject uiObject, Layer layer, AnchorNamePreset preset)
        {
            TextMeshProUGUI textUI = uiObject.AddComponent<TextMeshProUGUI>();
            textUI.text = layer.Text ?? string.Empty;
            textUI.font = ResolveUsableTextMeshProFont(layer);
            textUI.fontSize = Mathf.Max(1f, GetUIFontSize(layer));
            textUI.characterHorizontalScale = ResolveTextTransform(layer).CharacterHorizontalScale;
            textUI.color = ApplyLayerOpacity(layer.FillColor, layer);
            textUI.enableWordWrapping = false;
            textUI.overflowMode = TextOverflowModes.Overflow;
            textUI.raycastTarget = false;
            textUI.richText = false;
            textUI.alignment = GetTextMeshProAlignment(layer.Justification);
            ApplyTextMeshProCapitalization(textUI, layer.TextStyle);

            if (textUI.font == null)
            {
                PsdLogger.Warning("No TMP font asset is configured and Unity has no default TMP font. layer=" + GetRuntimeObjectName(layer));
            }
            else
            {
                if (TextMeshProBaseMaterial != null &&
                    !PsdPrefabTextMaterialFactory.IsCompatibleWithFont(TextMeshProBaseMaterial, textUI.font) &&
                    !tmpBaseMaterialFallbackWarningEmitted)
                {
                    tmpBaseMaterialFallbackWarningEmitted = true;
                    PsdLogger.Warning(
                        "Configured TMP base material is incompatible with the resolved font atlas; " +
                        "using the font material instead. material=" + TextMeshProBaseMaterial.name +
                        ", font=" + textUI.font.name);
                }

                PsdPrefabTextModel textModel = BuildTextModel(layer);
                Material material = PsdPrefabTextMaterialFactory.GetOrCreate(
                    textModel,
                    textUI.font,
                    TextMeshProBaseMaterial);
                if (material != null)
                {
                    textUI.fontSharedMaterial = material;
                }
            }

            ApplyTextMeshProLineHeight(textUI, layer);
            RefreshTextMeshProRendering(textUI);
        }

        /// <summary>
        /// Rebuilds TMP material references after the importer has assigned the
        /// final font, material, spacing, and font size. This is especially
        /// important for glyphs supplied by fallback font assets because TMP
        /// creates their sub-materials while generating the mesh.
        /// </summary>
        private static void RefreshTextMeshProRendering(TextMeshProUGUI textUI)
        {
            if (textUI == null || textUI.font == null)
            {
                return;
            }

            textUI.UpdateMeshPadding();
            textUI.SetMaterialDirty();
            textUI.SetVerticesDirty();
            textUI.SetLayoutDirty();
            textUI.ForceMeshUpdate(true, true);
            EditorUtility.SetDirty(textUI);
        }

        private static TMP_FontAsset ResolveUsableTextMeshProFont(Layer layer)
        {
            TMP_FontAsset configured = TextMeshProFont;
            if (IsUsableTextMeshProFont(configured))
            {
                return configured;
            }

            string psdFontName = layer == null ? string.Empty : layer.FontName;
            TMP_FontAsset matched = ResolveProjectTextMeshProFont(psdFontName);
            if (matched != null)
            {
                if (!tmpFontFallbackWarningEmitted)
                {
                    tmpFontFallbackWarningEmitted = true;
                    PsdLogger.Warning(
                        "Configured TMP font is unusable; matched the PSD font in the project instead. configured=" +
                        (configured == null ? "<none>" : configured.name) + ", psd=" + psdFontName +
                        ", matched=" + matched.name);
                }

                return matched;
            }

            TMP_FontAsset fallback = TMP_Settings.defaultFontAsset;
            if (configured != null && !tmpFontFallbackWarningEmitted)
            {
                tmpFontFallbackWarningEmitted = true;
                PsdLogger.Warning(
                    "Configured TMP font has no usable atlas/material; using TMP default font for this import. " +
                    "Regenerate or select a valid TMP Font Asset. configured=" + configured.name +
                    ", fallback=" + (fallback == null ? "<none>" : fallback.name));
            }

            return fallback;
        }

        internal static void ApplyProjectFontSettings(PsdLayoutProjectFontSnapshot settings)
        {
            TextMeshProFont = settings.font;
            TextMeshProBaseMaterial = settings.baseMaterial;
        }

        internal static void ApplyProjectOutputSettings(PsdLayoutProjectOutputSnapshot settings)
        {
            OutputMode = settings.outputMode;
            OutputFolderName = settings.outputFolderName;
            FixedOutputPath = settings.fixedOutputPath;
            AtlasOutputPath = settings.atlasOutputPath;
            TextureOutputPath = settings.textureOutputPath;
            PrefabOutputPath = settings.prefabOutputPath;
            PrefabMode = settings.prefabMode;
            AtlasVersion = settings.spriteAtlasVersion;
        }

        private static void LogProjectFontSettingsWarnings(PsdLayoutProjectFontSnapshot settings)
        {
            if (settings.fontStatus == PsdProjectAssetStatus.Missing)
            {
                PsdLogger.Warning(
                    "The project-wide TMP font reference is missing or invalid; " +
                    "using the TMP default font when available. guid=" + settings.fontGuid);
            }

            if (settings.materialStatus == PsdProjectAssetStatus.Missing)
            {
                PsdLogger.Warning(
                    "The project-wide TMP base material reference is missing or invalid; " +
                    "using the resolved font material. guid=" + settings.materialGuid);
            }
        }

        private static bool IsUsableTextMeshProFont(TMP_FontAsset font)
        {
            return PsdTmpFontAssetPolicy.IsUsable(font);
        }

        private static TMP_FontAsset ResolveProjectTextMeshProFont(string psdFontName)
        {
            if (string.IsNullOrEmpty(psdFontName))
            {
                return null;
            }

            if (currentTmpFontFallbacksByPsdName != null &&
                currentTmpFontFallbacksByPsdName.TryGetValue(psdFontName, out TMP_FontAsset cached))
            {
                return cached;
            }

            TMP_FontAsset matched = null;
            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
            {
                TMP_FontAsset candidate = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (IsUsableTextMeshProFont(candidate) &&
                    PsdTextFontNameMatcher.IsMatch(
                        psdFontName,
                        candidate.name,
                        candidate.sourceFontFile == null ? string.Empty : candidate.sourceFontFile.name))
                {
                    matched = candidate;
                    break;
                }
            }

            if (currentTmpFontFallbacksByPsdName != null)
            {
                currentTmpFontFallbacksByPsdName[psdFontName] = matched;
            }

            return matched;
        }

        private static TextAlignmentOptions GetTextMeshProAlignment(TextJustification justification)
        {
            switch (justification)
            {
                case TextJustification.Left:
                    return TextAlignmentOptions.MidlineLeft;
                case TextJustification.Right:
                    return TextAlignmentOptions.MidlineRight;
                case TextJustification.Center:
                default:
                    return TextAlignmentOptions.Midline;
            }
        }

        private static void ApplyTextMeshProCapitalization(TextMeshProUGUI textUI, PsdTextStyle style)
        {
            if (textUI == null || style == null)
            {
                return;
            }

            if (style.Capitalization == PsdTextCapitalization.AllCaps)
            {
                textUI.fontStyle |= FontStyles.UpperCase;
            }
            else if (style.Capitalization == PsdTextCapitalization.SmallCaps)
            {
                textUI.fontStyle |= FontStyles.SmallCaps;
            }
        }

        private static string GetLegacyUiText(string text, PsdTextStyle style)
        {
            if (string.IsNullOrEmpty(text) || style == null ||
                (style.Capitalization != PsdTextCapitalization.AllCaps &&
                 style.Capitalization != PsdTextCapitalization.SmallCaps))
            {
                return text ?? string.Empty;
            }

            return text.ToUpperInvariant();
        }

        private static void ApplyTextMeshProLineHeight(TextMeshProUGUI textUI, Layer layer)
        {
            if (textUI == null || layer == null || layer.TextStyle == null || layer.FontSize <= 0f)
            {
                return;
            }

            // Both LineHeight and FontSize are in PSD document pixels (unscaled).
            // The ratio between them is scale-independent, so the result is a
            // correct TMP lineSpacing percentage regardless of canvas scaling.
            textUI.lineSpacing = ((layer.TextStyle.LineHeight / layer.FontSize) - 1f) * 100f;
        }

        private static PsdPrefabTextModel BuildTextModel(Layer layer)
        {
            PsdTextStyle style = layer.TextStyle ?? PsdTextStyle.CreateDefault(layer.FontSize);
            float shadowSoftness;
            float shadowDilate;
            PsdTextEffectConversion.SplitShadowBlur(
                style.ShadowBlur,
                style.ShadowChoke,
                out shadowSoftness,
                out shadowDilate);
            Vector2 shadowOffset = PsdTextEffectConversion.ConvertShadowOffset(
                style.ShadowAngle,
                style.ShadowDistance);
            return new PsdPrefabTextModel
            {
                contents = layer.Text ?? string.Empty,
                fontFamily = layer.FontName ?? string.Empty,
                fontSize = style.Transform.EffectiveFontSize(layer.FontSize),
                characterHorizontalScale = style.Transform.CharacterHorizontalScale,
                fillColor = layer.FillColor,
                lineHeight = style.LineHeight,
                effect = new PsdPrefabTextEffectModel
                {
                    hasOutline = style.StrokeEnabled,
                    outlineColor = style.StrokeColor,
                    outlineWidth = style.StrokeWidth,
                    hasShadow = style.ShadowEnabled,
                    shadowColor = style.ShadowColor,
                    shadowOffsetX = shadowOffset.x,
                    shadowOffsetY = shadowOffset.y,
                    shadowSoftness = shadowSoftness,
                    shadowDilate = shadowDilate
                }
            };
        }

        /// <summary>
        /// Applies normalized PSD text effects to the generated UI text. Unity's
        /// built-in Outline/Shadow are used so the result remains editable in
        /// the generated Prefab and does not require nine-slice assets.
        /// </summary>
        private static void ApplyTextStyle(Text textUI, Layer layer)
        {
            if (textUI == null || layer == null || layer.TextStyle == null)
            {
                return;
            }

            float scale = GetTargetCanvasUniformScale();
            PsdTextStyle style = layer.TextStyle;
            if (style.LineHeight > 0f && layer.FontSize > 0f)
            {
                textUI.lineSpacing = Mathf.Max(0.01f, style.LineHeight / layer.FontSize);
            }

            if (style.StrokeEnabled && style.StrokeWidth > 0f)
            {
                Outline outline = textUI.gameObject.AddComponent<Outline>();
                float width = Mathf.Max(0.01f, style.StrokeWidth * scale);
                outline.effectDistance = new Vector2(width, width);
                outline.effectColor = ApplyLayerOpacity(style.StrokeColor, layer);
                outline.useGraphicAlpha = true;
            }

            if (style.ShadowEnabled)
            {
                Shadow shadow = textUI.gameObject.AddComponent<Shadow>();
                float distance = Mathf.Max(0f, style.ShadowDistance * scale);
                shadow.effectDistance = PsdTextEffectConversion.ConvertShadowOffset(style.ShadowAngle, distance);
                shadow.effectColor = ApplyLayerOpacity(style.ShadowColor, layer);
                shadow.useGraphicAlpha = true;

                if (style.ShadowBlur > 0f)
                {
                    PsdLogger.Warning("PSD text shadow blur is approximated by Unity UI Shadow; layer=" + GetRuntimeObjectName(layer));
                }
            }
        }

        /// <summary>
        /// Creates a <see cref="UnityEngine.UI.Button"/> from the given <see cref="Layer"/>.
        /// </summary>
        /// <param name="layer">The Layer to create the Button from.</param>
        private static void CreateUIButton(Layer layer)
        {
            LayerImportInfo info = GetLayerInfo(layer);
            AnchorNamePreset buttonPreset = info != null ? info.AnchorPreset : AnchorNamePreset.None;

            // create an empty Image object with a Button behavior attached
            Image image = CreateUIImage(layer);
            Button button = image.gameObject.AddComponent<Button>();
            UiLayoutContext buttonLayoutContext = GetChildUILayoutContext(layer, buttonPreset, GetLayerLayoutRect(layer));

            // look through the children for a clip rect
            ////Rectangle? clipRect = null;
            ////foreach (Layer child in layer.Children)
            ////{
            ////    if (child.Name.ContainsIgnoreCase("|ClipRect"))
            ////    {
            ////        clipRect = child.Rect;
            ////    }
            ////}

            // look through the children for the sprite states
            foreach (Layer child in layer.Children)
            {
                LayerImportInfo childInfo = GetLayerInfo(child);
                if (childInfo == null || !childInfo.EffectiveVisible)
                {
                    continue;
                }

                if (childInfo.ButtonRole == ButtonChildRole.Disabled)
                {
                    button.transition = Selectable.Transition.SpriteSwap;

                    SpriteState spriteState = button.spriteState;
                    spriteState.disabledSprite = CreateSprite(child);
                    button.spriteState = spriteState;
                }
                else if (childInfo.ButtonRole == ButtonChildRole.Highlighted)
                {
                    button.transition = Selectable.Transition.SpriteSwap;

                    SpriteState spriteState = button.spriteState;
                    spriteState.highlightedSprite = CreateSprite(child);
                    button.spriteState = spriteState;
                }
                else if (childInfo.ButtonRole == ButtonChildRole.Pressed)
                {
                    button.transition = Selectable.Transition.SpriteSwap;

                    SpriteState spriteState = button.spriteState;
                    spriteState.pressedSprite = CreateSprite(child);
                    button.spriteState = spriteState;
                }
                else if (childInfo.ButtonRole == ButtonChildRole.Default)
                {
                    image.sprite = CreateSprite(child);
                    ApplyImageLayoutBehavior(image, buttonPreset);
                    ApplyNineSliceImageBehavior(image, child);
                    button.targetGraphic = image;
                }
                else if (childInfo.ButtonRole == ButtonChildRole.TextImage)
                {
                    GameObject oldGroupObject = currentGroupGameObject;
                    UiLayoutContext oldLayoutContext = currentGroupLayoutContext;
                    currentGroupGameObject = button.gameObject;
                    currentGroupLayoutContext = buttonLayoutContext;

                    // If the "text" is a normal art layer, create an Image object from the "text"
                    CreateUIImage(child);

                    currentGroupGameObject = oldGroupObject;
                    currentGroupLayoutContext = oldLayoutContext;
                }

                if (child.IsTextLayer)
                {
                    // TODO: Create a child text game object
                }
            }
        }

        /// <summary>
        /// Applies the configured layout to the generated PSD root object.
        /// </summary>
        /// <param name="rootTransform">The root RectTransform.</param>
        /// <returns>Resolved root layout context.</returns>
        private static UiLayoutContext ApplyRootUILayout(RectTransform rootTransform)
        {
            Vector2 rootRectSize = GetRootRectSize();
            // A direct 1:1 PSD import must keep a root with the authored PSD
            // dimensions. Stretching it to an unrelated target Canvas silently
            // changes every child layout and crops tall PSDs. Global stretch is
            // only appropriate when the user explicitly chose Canvas scaling.
            if (RootUseGlobalAnchorByDefault && (!UseTargetCanvasCoordinates || ScaleToTargetCanvas))
            {
                ApplyStretchLayout(rootTransform);
            }
            else
            {
                rootTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rootTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rootTransform.pivot = new Vector2(0.5f, 0.5f);
                rootTransform.anchoredPosition = Vector2.zero;
                rootTransform.sizeDelta = rootRectSize;
            }

            return new UiLayoutContext
            {
                PsdReferenceRect = new Rect(0f, 0f, CanvasSize.x, CanvasSize.y),
                LocalRectSize = rootRectSize,
                LocalDisplayRect = GetCenteredRect(GetRootDisplaySize(rootRectSize))
            };
        }

        /// <summary>
        /// Applies the configured layout to one generated UI node.
        /// </summary>
        /// <param name="transform">The RectTransform to place.</param>
        /// <param name="layer">Source PSD layer.</param>
        /// <param name="preset">Resolved anchor preset.</param>
        /// <returns>Resolved child layout context.</returns>
        private static UiLayoutContext ApplyLayerUILayout(RectTransform transform, Layer layer, AnchorNamePreset preset)
        {
            Rect layoutRect = GetLayerLayoutRect(layer);
            AnchorNamePreset effectivePreset = NormalizePointAnchorPreset(preset);
            UiLayoutContext childContext = GetChildUILayoutContext(layer, preset, layoutRect);

            if (IsGlobalAnchorPreset(preset))
            {
                ApplyStretchLayout(transform);
                return childContext;
            }

            Vector2 anchor = GetAnchorVector(effectivePreset);
            transform.anchorMin = anchor;
            transform.anchorMax = anchor;
            transform.pivot = anchor;
            transform.anchoredPosition = GetAnchoredPositionForLayer(layoutRect, currentGroupLayoutContext, effectivePreset);
            transform.sizeDelta = childContext.LocalRectSize;
            return childContext;
        }

        /// <summary>
        /// Records the generated object identity without adding a marker
        /// component to the Prefab. Only native non-zero Photoshop layer IDs
        /// are durable enough for incremental hierarchy ownership. A zero-ID
        /// fallback is intentionally omitted so rename/reorder cannot silently
        /// bind an old plan to the wrong object.
        /// </summary>
        private static void RegisterGeneratedUiNode(Layer layer, RectTransform transform)
        {
            if (layer == null)
            {
                return;
            }

            RegisterGeneratedUiNode(layer.Id, transform);
        }

        internal static void RegisterGeneratedUiNode(uint layerId, RectTransform transform)
        {
            if (transform == null || currentGeneratedUiNodesByStableId == null || layerId == 0U) return;
            string stableId = layerId.ToString(CultureInfo.InvariantCulture);
            RectTransform existing;
            if (currentGeneratedUiNodesByStableId.TryGetValue(stableId, out existing) && existing != transform)
            {
                throw new InvalidOperationException(
                    "Duplicate durable PSD layer ID '" + stableId + "' generated more than one primary UI object.");
            }

            if (currentGeneratedUiNodesByStableId.Any(pair =>
                    !string.Equals(pair.Key, stableId, StringComparison.Ordinal) && pair.Value == transform))
            {
                throw new InvalidOperationException(
                    "Different durable PSD layer IDs cannot map to the same generated UI object.");
            }

            currentGeneratedUiNodesByStableId[stableId] = transform;
        }

        internal static void BeginGeneratedUiNodeRegistry(bool enabled)
        {
            currentGeneratedUiNodesByStableId = enabled
                ? new Dictionary<string, RectTransform>(StringComparer.Ordinal)
                : null;
        }

        internal static void EndGeneratedUiNodeRegistry()
        {
            currentGeneratedUiNodesByStableId = null;
        }

        /// <summary>
        /// Returns a detached view of the current import registry. The method is
        /// an inert integration seam: it performs no hierarchy mutation and no
        /// Prefab save. Task 6 may call it only while its transactional merge is
        /// synchronizing candidate values into the loaded existing Prefab.
        /// </summary>
        internal static Dictionary<string, RectTransform> CaptureGeneratedUiNodeRegistry()
        {
            return currentGeneratedUiNodesByStableId == null
                ? new Dictionary<string, RectTransform>(StringComparer.Ordinal)
                : new Dictionary<string, RectTransform>(currentGeneratedUiNodesByStableId, StringComparer.Ordinal);
        }

        /// <summary>
        /// Applies stretch anchors with zero offsets to a RectTransform.
        /// </summary>
        /// <param name="transform">Target RectTransform.</param>
        private static void ApplyStretchLayout(RectTransform transform)
        {
            transform.anchorMin = Vector2.zero;
            transform.anchorMax = Vector2.one;
            transform.pivot = new Vector2(0.5f, 0.5f);
            transform.anchoredPosition = Vector2.zero;
            transform.sizeDelta = Vector2.zero;
            transform.offsetMin = Vector2.zero;
            transform.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Gets the child UI layout context produced by one layer.
        /// </summary>
        /// <param name="layer">Source PSD layer.</param>
        /// <param name="preset">Resolved anchor preset.</param>
        /// <returns>Layout context for the layer's children.</returns>
        private static UiLayoutContext GetChildUILayoutContext(Layer layer, AnchorNamePreset preset, Rect layoutRect)
        {
            if (IsGlobalAnchorPreset(preset))
            {
                return currentGroupLayoutContext;
            }

            Vector2 childSize = GetUiLayerSize(layoutRect);
            return new UiLayoutContext
            {
                PsdReferenceRect = layoutRect,
                LocalRectSize = childSize,
                LocalDisplayRect = GetCenteredRect(childSize)
            };
        }

        /// <summary>
        /// Applies the default image preserve-aspect behavior for generated UI images.
        /// </summary>
        /// <param name="image">The generated image.</param>
        /// <param name="preset">Resolved anchor preset.</param>
        private static void ApplyImageLayoutBehavior(Image image, AnchorNamePreset preset)
        {
            if (image == null)
            {
                return;
            }

            image.type = Image.Type.Simple;
            image.fillCenter = true;
            image.preserveAspect = true;

            AspectRatioFitter fitter = image.GetComponent<AspectRatioFitter>();
            if (!IsGlobalAnchorPreset(preset) || image.sprite == null || image.sprite.rect.height <= 0f)
            {
                if (fitter != null)
                {
                    UnityEngine.Object.DestroyImmediate(fitter);
                }

                return;
            }

            if (fitter == null)
            {
                fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            }

            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
        }

        /// <summary>
        /// Applies Figma-style 9-slice behavior to a generated UI image.
        /// Corners keep their source size, edges stretch on one axis, and the
        /// center stretches on both axes.  The sprite importer already carries
        /// the matching pixel border before this method is called.
        /// </summary>
        /// <param name="image">Generated UI image.</param>
        /// <param name="layer">PSD layer that authored the 9-slice tag.</param>
        private static void ApplyNineSliceImageBehavior(Image image, Layer layer)
        {
            if (image == null || image.sprite == null)
            {
                return;
            }

            Vector4 border = image.sprite.border;
            if (!PsdNineSliceImportPolicy.HasSpriteBorder(border.x, border.y, border.z, border.w))
            {
                return;
            }

            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.preserveAspect = false;

            // AspectRatioFitter would force the whole image to keep its aspect
            // ratio and would therefore defeat independent 9-slice stretching.
            AspectRatioFitter fitter = image.GetComponent<AspectRatioFitter>();
            if (fitter != null)
            {
                UnityEngine.Object.DestroyImmediate(fitter);
            }
        }

        /// <summary>
        /// Validates every Common_* import contract before generated output is
        /// changed, so a missing public asset cannot create a partial prefab.
        /// </summary>
        private static void ValidateCommonLibraryReferences(IEnumerable<Layer> layers)
        {
            foreach (Layer layer in layers)
            {
                PsdCommonAssetReference reference;
                if (PsdCommonAssetNameParser.TryParse(layer.Name, out reference))
                {
                    if (reference.Kind == PsdCommonAssetKind.Texture && layer.Children.Count > 0)
                    {
                        PsdCommonAssetNamingSnapshot naming =
                            PsdLayoutProjectSettings.instance.ResolveCommonAssetNaming();
                        throw new InvalidOperationException(
                            naming.texturePrefix + " layers must be leaf art layers. Invalid layer: " +
                            DescribeLayerForLog(layer));
                    }

                    UnityEngine.Object asset;
                    string error;
                    if (!PsdCommonAssetResolver.TryResolve(reference, out asset, out error))
                    {
                        throw new InvalidOperationException(error + " Layer: " + DescribeLayerForLog(layer));
                    }

                    // A common prefab replaces the complete PSD subtree; its
                    // child names are implementation details of the source PSD
                    // and must not be independently validated or emitted.
                    if (reference.Kind == PsdCommonAssetKind.Prefab)
                    {
                        continue;
                    }
                }

                if (layer.Children != null && layer.Children.Count > 0)
                {
                    ValidateCommonLibraryReferences(layer.Children);
                }
            }
        }

        /// <summary>
        /// Creates the runtime representation for a hard Common_* PSD rule and
        /// intentionally suppresses normal PNG export for the layer subtree.
        /// </summary>
        private static void ExportCommonLayer(
            Layer layer,
            LayerImportInfo info,
            PsdCommonAssetReference reference)
        {
            if (!(LayoutInScene || CreatePrefab) || !info.EffectiveVisible)
            {
                PsdLogger.Info("Skip hidden Common layer runtime output: " + DescribeLayerForLog(layer));
                return;
            }

            UnityEngine.Object asset;
            string error;
            if (!PsdCommonAssetResolver.TryResolve(reference, out asset, out error))
            {
                throw new InvalidOperationException(error + " Layer: " + DescribeLayerForLog(layer));
            }

            if (reference.Kind == PsdCommonAssetKind.Prefab)
            {
                GameObject prefab = asset as GameObject;
                if (prefab == null)
                {
                    throw new InvalidOperationException("Resolved Common Prefab is not a GameObject: " + reference.Key);
                }

                CreateCommonPrefabInstance(layer, info, prefab);
                return;
            }

            Sprite sprite = asset as Sprite;
            if (sprite == null)
            {
                throw new InvalidOperationException("Resolved Common Texture is not a Sprite: " + reference.Key);
            }

            if (UseUnityUI)
            {
                CreateCommonUIImage(layer, info, sprite);
            }
            else
            {
                CreateCommonSpriteGameObject(layer, sprite);
            }
        }

        private static void CreateCommonPrefabInstance(Layer layer, LayerImportInfo info, GameObject prefab)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("Unable to instantiate Common Prefab: " + prefab.name);
            }

            instance.name = GetRuntimeObjectName(layer);
            if (UseUnityUI)
            {
                RectTransform rectTransform = instance.GetComponent<RectTransform>();
                if (rectTransform == null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                    throw new InvalidOperationException("Common Prefab must have a RectTransform when Use Unity UI is enabled: " + prefab.name);
                }

                rectTransform.SetParent(currentGroupGameObject.transform, false);
                ApplyLayerUILayout(rectTransform, layer, info.AnchorPreset);
                RegisterGeneratedUiNode(layer, rectTransform);
                if (PsdCommonPrefabVisualFallbackPolicy.RequiresSourceVisualFallback(HasRenderableCommonPrefabVisual(instance)))
                {
                    Image fallback = CreateCommonPrefabSourceFallback(layer);
                    fallback.gameObject.name = GetRuntimeObjectName(layer) + "__PsdFallback";
                    fallback.transform.SetSiblingIndex(instance.transform.GetSiblingIndex());
                    PsdLogger.Warning(
                        "Common Prefab has no renderable visual; retained PSD layer visual as fallback: " +
                        DescribeLayerForLog(layer));
                }

                return;
            }

            instance.transform.SetParent(currentGroupGameObject.transform, false);
            ApplyCommonWorldPosition(instance.transform, layer);
        }

        /// <summary>
        /// Creates the source visual used only when a Common Prefab resolves but
        /// has no visible graphics. Prefer the PSD merged crop, because its
        /// pixels represent Photoshop's final result; fall back to the raw
        /// layer image when the document format cannot provide that crop.
        /// </summary>
        private static Image CreateCommonPrefabSourceFallback(Layer layer)
        {
            Sprite sprite = CreateMergedFallbackSprite(layer) ?? CreateSprite(layer);
            GameObject uiObject = new GameObject(GetRuntimeObjectName(layer), typeof(RectTransform));
            uiObject.transform.SetParent(currentGroupGameObject.transform, false);

            LayerImportInfo info = GetLayerInfo(layer);
            AnchorNamePreset preset = info != null ? info.AnchorPreset : AnchorNamePreset.None;
            RectTransform transform = uiObject.GetComponent<RectTransform>();
            ApplyLayerUILayout(transform, layer, preset);

            Image image = uiObject.AddComponent<Image>();
            image.sprite = sprite;
            ApplyImageLayoutBehavior(image, preset);
            return image;
        }

        /// <summary>
        /// Exports a cropped PSD merged-image fallback Sprite for an unusable
        /// Common Prefab.
        /// </summary>
        private static Sprite CreateMergedFallbackSprite(Layer layer)
        {
            Texture2D texture = ImageDecoder.DecodeMergedImageCrop(layer);
            if (texture == null)
            {
                return null;
            }

            try
            {
                string textureDirectory = ResolveTextureOutputDirectory(currentOutputRootDirectory);
                string file = Path.Combine(
                    textureDirectory,
                    GetTextureBaseName(layer) + "_" + layer.Id + "__MergedFallback.png");
                byte[] png = texture.EncodeToPNG();
                if (png == null || png.Length == 0)
                {
                    return null;
                }

                // This is an importer-owned recovery asset, not an artist-editable
                // output. Always replace it so an incremental import cannot retain
                // an outdated merged crop after PSD or importer changes.
                Directory.CreateDirectory(textureDirectory);
                File.WriteAllBytes(file, png);
                return ImportSprite(GetRelativePath(file), PsdName, null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Determines whether a resolved Common Prefab already contains a
        /// visible visual asset. Components without a Sprite or Texture cannot
        /// reproduce the PSD layer and therefore need a source-image fallback.
        /// </summary>
        private static bool HasRenderableCommonPrefabVisual(GameObject instance)
        {
            foreach (Image image in instance.GetComponentsInChildren<Image>(true))
            {
                if (image.enabled && image.sprite != null && image.color.a > 0f)
                {
                    return true;
                }
            }

            foreach (RawImage image in instance.GetComponentsInChildren<RawImage>(true))
            {
                if (image.enabled && image.texture != null && image.color.a > 0f)
                {
                    return true;
                }
            }

            foreach (SpriteRenderer spriteRenderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (spriteRenderer.enabled && spriteRenderer.sprite != null && spriteRenderer.color.a > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        private static void CreateCommonUIImage(Layer layer, LayerImportInfo info, Sprite sprite)
        {
            GameObject uiObject = new GameObject(GetRuntimeObjectName(layer), typeof(RectTransform));
            uiObject.transform.SetParent(currentGroupGameObject.transform, false);
            RectTransform transform = uiObject.GetComponent<RectTransform>();
            ApplyLayerUILayout(transform, layer, info.AnchorPreset);
            RegisterGeneratedUiNode(layer, transform);

            Image image = uiObject.AddComponent<Image>();
            image.sprite = sprite;
            ApplyCommonTextureVisualTransform(transform, layer, sprite);
            ApplyImageLayoutBehavior(image, info.AnchorPreset);
            ApplyNineSliceImageBehavior(image, layer);
        }

        /// <summary>
        /// Reconstructs the visual transform baked into a PSD Common_Texture
        /// layer. Normal PSD exports retain that transform in their pixels;
        /// a public replacement Sprite needs the equivalent RectTransform
        /// rotation and scale applied explicitly.
        /// </summary>
        private static void ApplyCommonTextureVisualTransform(RectTransform transform, Layer layer, Sprite sprite)
        {
            if (transform == null || layer == null || sprite == null) return;

            Texture2D sourceTexture = ImageDecoder.DecodeImage(layer);
            Color32[] replacementPixels;
            if (sourceTexture == null || !TryReadSpritePixels(sprite, out replacementPixels))
            {
                if (sourceTexture != null) UnityEngine.Object.DestroyImmediate(sourceTexture);
                return;
            }

            try
            {
                PsdCommonVisualTransformMatcher.Result match;
                if (!PsdCommonVisualTransformMatcher.TryMatch(
                        (int)sprite.rect.width, (int)sprite.rect.height, replacementPixels,
                        sourceTexture.width, sourceTexture.height, sourceTexture.GetPixels32(), out match))
                {
                    return;
                }

                transform.localRotation = Quaternion.Euler(0f, 0f, match.RotationDegrees);
                Vector2 nativeSize = new Vector2(sprite.rect.width * match.Scale, sprite.rect.height * match.Scale);
                transform.sizeDelta = nativeSize;
                PsdLogger.Info("Recovered Common Texture transform. layer=" + DescribeLayerForLog(layer) +
                    ", rotation=" + match.RotationDegrees.ToString("F2", CultureInfo.InvariantCulture) +
                    ", scale=" + match.Scale.ToString("F3", CultureInfo.InvariantCulture));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sourceTexture);
            }
        }

        private static bool TryReadSpritePixels(Sprite sprite, out Color32[] pixels)
        {
            pixels = null;
            if (sprite == null || sprite.texture == null) return false;
            RenderTexture temporary = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                Graphics.Blit(sprite.texture, temporary);
                RenderTexture.active = temporary;
                copy = new Texture2D(sprite.texture.width, sprite.texture.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, copy.width, copy.height), 0, 0);
                copy.Apply(false, false);
                int width = Mathf.RoundToInt(sprite.rect.width);
                int height = Mathf.RoundToInt(sprite.rect.height);
                Color32[] texturePixels = copy.GetPixels32();
                pixels = new Color32[width * height];
                int startX = Mathf.RoundToInt(sprite.rect.x);
                int startY = Mathf.RoundToInt(sprite.rect.y);
                for (int y = 0; y < height; y++)
                {
                    Array.Copy(texturePixels, ((startY + y) * copy.width) + startX, pixels, y * width, width);
                }
                return pixels.Length == width * height;
            }
            catch (UnityException)
            {
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
                if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        private static void CreateCommonSpriteGameObject(Layer layer, Sprite sprite)
        {
            GameObject gameObject = new GameObject(GetRuntimeObjectName(layer));
            gameObject.transform.SetParent(currentGroupGameObject.transform, false);
            ApplyCommonWorldPosition(gameObject.transform, layer);

            SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = currentSortingOrder++;
        }

        private static void ApplyCommonWorldPosition(Transform transform, Layer layer)
        {
            float x = layer.Rect.x / PixelsToUnits;
            float y = (CanvasSize.y - layer.Rect.y) / PixelsToUnits;
            float width = layer.Rect.width / PixelsToUnits;
            float height = layer.Rect.height / PixelsToUnits;
            transform.localPosition = new Vector3(x + (width / 2f), y - (height / 2f), currentDepth);
            currentDepth -= depthStep;
        }

        /// <summary>
        /// Resolves embedded 9-slice metadata, then falls back to the legacy
        /// PSD layer-name tag for documents written by older plugin versions.
        /// </summary>
        /// <param name="layer">PSD layer.</param>
        /// <param name="border">Unity border in left, bottom, right, top order.</param>
        /// <returns>True when the layer has a valid 9-slice tag.</returns>
        private static bool TryGetNineSliceBorder(Layer layer, out Vector4 border)
        {
            border = Vector4.zero;
            if (layer == null)
            {
                return false;
            }

            PsdNineSliceOverride manualOverride;
            if (TryGetManualNineSliceOverride(layer, out manualOverride))
            {
                // A manual disabled decision explicitly suppresses every
                // automatic source. An enabled decision may use a border only
                // after the PNG processing pass registered the matching crop.
                if (!manualOverride.Enabled)
                {
                    return false;
                }

                return currentAutomaticNineSliceBordersByLayer != null &&
                    currentAutomaticNineSliceBordersByLayer.TryGetValue(layer, out border) &&
                    IsNineSliceBorderValid(layer, border, "manual PSD editor");
            }

            if (currentAutomaticNineSliceBordersByLayer != null &&
                currentAutomaticNineSliceBordersByLayer.TryGetValue(layer, out border))
            {
                return IsNineSliceBorderValid(layer, border, "PSD name auto-analysis");
            }

            if (layer.Id != 0U &&
                currentNineSliceBordersByLayerId != null &&
                currentNineSliceBordersByLayerId.TryGetValue(layer.Id, out border))
            {
                return IsNineSliceBorderValid(layer, border, "embedded XMP");
            }

            if (useEmbeddedNineSliceMetadata)
            {
                return false;
            }

            if (string.IsNullOrEmpty(layer.Name))
            {
                return false;
            }

            Match match = Regex.Match(layer.Name, NineSliceTagPattern, RegexOptions.IgnoreCase);
            if (!match.Success || match.Groups.Count < 5)
            {
                return false;
            }

            float left;
            float top;
            float right;
            float bottom;
            if (!float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out left) ||
                !float.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out top) ||
                !float.TryParse(match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out right) ||
                !float.TryParse(match.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out bottom))
            {
                return false;
            }

            border = new Vector4(left, bottom, right, top);
            return IsNineSliceBorderValid(layer, border, "layer-name fallback: " + match.Value);
        }

        /// <summary>
        /// Resolves the automatic PNG conversion requested by an embedded
        /// border, legacy explicit name tag, or the new bare name tags.
        /// </summary>
        private static bool TryGetNineSliceConversionRule(Layer layer, out PsdNineSliceNameRule rule)
        {
            rule = null;
            PsdNineSliceOverride manualOverride;
            if (TryGetManualNineSliceOverride(layer, out manualOverride))
            {
                if (!manualOverride.Enabled)
                {
                    return false;
                }

                rule = new PsdNineSliceNameRule(PsdNineSliceMode.NineSlice, manualOverride.Border);
                return true;
            }

            // A PSD may retain an older XMP border that was inferred by a previous
            // exporter version.  A current authoring name is the explicit intent
            // for this import: its numeric form supplies the border directly and
            // its bare jiugong form requests a fresh pixel analysis in Unity.
            // Only use embedded data when the layer has no authoring tag.
            if (layer != null && PsdNineSliceNameRules.TryParse(layer.Name, out rule))
            {
                return true;
            }

            Vector4 explicitBorder;
            if (TryGetNineSliceBorder(layer, out explicitBorder))
            {
                rule = new PsdNineSliceNameRule(
                    PsdNineSliceMode.NineSlice,
                    new PsdNineSliceBorder(
                        Mathf.RoundToInt(explicitBorder.x),
                        Mathf.RoundToInt(explicitBorder.w),
                        Mathf.RoundToInt(explicitBorder.z),
                        Mathf.RoundToInt(explicitBorder.y)));
                return true;
            }

            return false;
        }

        private static void ConfigureManualNineSliceOverrides(string assetPath)
        {
            if (currentManualNineSliceOverridesByLayerId == null)
            {
                currentManualNineSliceOverridesByLayerId = new Dictionary<uint, PsdNineSliceOverride>();
            }

            currentManualNineSliceOverridesByLayerId.Clear();
            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
            {
                return;
            }

            currentManualNineSliceOverridesByLayerId = PsdNineSliceOverrideStore.ReadAll(importer.userData);
            if (currentManualNineSliceOverridesByLayerId.Count > 0)
            {
                PsdLogger.Info("Loaded " + currentManualNineSliceOverridesByLayerId.Count + " manual PSD 9-slice override(s) from .meta userData.");
            }
        }

        private static bool TryGetManualNineSliceOverride(Layer layer, out PsdNineSliceOverride value)
        {
            value = null;
            return layer != null && layer.Id != 0U && currentManualNineSliceOverridesByLayerId != null &&
                currentManualNineSliceOverridesByLayerId.TryGetValue(layer.Id, out value);
        }

        private static bool HasEnabledManualNineSliceOverride(Layer layer)
        {
            PsdNineSliceOverride value;
            return TryGetManualNineSliceOverride(layer, out value) && value.Enabled;
        }

        private static void RegisterAutomaticNineSliceBorder(Layer layer, Vector4 border, bool isInTargetCoordinates)
        {
            if (layer == null)
            {
                return;
            }

            if (currentAutomaticNineSliceBordersByLayer == null)
            {
                currentAutomaticNineSliceBordersByLayer = new Dictionary<Layer, Vector4>();
            }

            currentAutomaticNineSliceBordersByLayer[layer] = border;
            if (isInTargetCoordinates)
            {
                currentAutomaticNineSliceBordersInTargetCoordinates.Add(layer);
            }
            else
            {
                currentAutomaticNineSliceBordersInTargetCoordinates.Remove(layer);
            }
        }

        private static bool IsAutomaticNineSliceBorderInTargetCoordinates(Layer layer)
        {
            return layer != null && currentAutomaticNineSliceBordersInTargetCoordinates != null &&
                currentAutomaticNineSliceBordersInTargetCoordinates.Contains(layer);
        }

        private static void ClearAutomaticNineSliceBorder(Layer layer)
        {
            if (layer != null && currentAutomaticNineSliceBordersByLayer != null)
            {
                currentAutomaticNineSliceBordersByLayer.Remove(layer);
                if (currentAutomaticNineSliceBordersInTargetCoordinates != null)
                {
                    currentAutomaticNineSliceBordersInTargetCoordinates.Remove(layer);
                }
            }
        }

        /// <summary>
        /// Reads per-layer 9-slice values from the PSD's embedded XMP manifest.
        /// The Photoshop layer ID is the stable join key between this metadata and
        /// the native layer records used by the texture exporter.
        /// </summary>
        /// <param name="manifest">Optional embedded PSD layout manifest.</param>
        private static void ConfigureEmbeddedNineSliceBorders(PsdEmbeddedLayoutManifest manifest)
        {
            if (currentNineSliceBordersByLayerId == null)
            {
                currentNineSliceBordersByLayerId = new Dictionary<uint, Vector4>();
            }

            currentNineSliceBordersByLayerId.Clear();
            useEmbeddedNineSliceMetadata = manifest != null &&
                manifest.IsUsable &&
                manifest.nineSliceSchemaVersion >= 1;
            if (manifest == null || !manifest.IsUsable || manifest.layers == null)
            {
                return;
            }

            foreach (PsdEmbeddedLayoutLayer sourceLayer in manifest.layers)
            {
                if (sourceLayer == null || sourceLayer.nineSlice == null || !sourceLayer.nineSlice.enabled)
                {
                    continue;
                }

                uint layerId;
                if (!uint.TryParse(
                        sourceLayer.layerId,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out layerId) ||
                    layerId == 0U)
                {
                    PsdLogger.Warning("Ignore embedded 9-slice data with invalid layerId: " + sourceLayer.layerId);
                    continue;
                }

                currentNineSliceBordersByLayerId[layerId] = new Vector4(
                    sourceLayer.nineSlice.left,
                    sourceLayer.nineSlice.bottom,
                    sourceLayer.nineSlice.right,
                    sourceLayer.nineSlice.top);
            }

            if (currentNineSliceBordersByLayerId.Count > 0)
            {
                PsdLogger.Info(
                    "Loaded embedded 9-slice data for " +
                    currentNineSliceBordersByLayerId.Count + " PSD layer(s).");
            }
        }

        /// <summary>
        /// Validates Unity-order border values against one native PSD layer.
        /// </summary>
        /// <param name="layer">PSD layer receiving the border.</param>
        /// <param name="border">Border in left, bottom, right, top order.</param>
        /// <param name="source">Human-readable metadata source for diagnostics.</param>
        /// <returns>True when the border can be applied to the layer texture.</returns>
        private static bool IsNineSliceBorderValid(Layer layer, Vector4 border, string source)
        {
            if (layer.Rect.width <= 0f || layer.Rect.height <= 0f ||
                border.x < 0f || border.y < 0f || border.z < 0f || border.w < 0f ||
                border.x + border.z > layer.Rect.width || border.y + border.w > layer.Rect.height ||
                border.x + border.z + PsdNineSliceBorder.MinimumStretchCenterPixels > layer.Rect.width ||
                border.y + border.w + PsdNineSliceBorder.MinimumStretchCenterPixels > layer.Rect.height)
            {
                PsdLogger.Warning(
                    "Ignore invalid 9-slice border for layer: " + DescribeLayerForLog(layer) +
                    ", source=" + source + ", border=" + border);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Prevents metadata expressed for an old PSD rectangle from being
        /// applied to the final PNG produced for this import.
        /// </summary>
        private static bool IsNineSliceBorderValidForGeneratedTexture(
            int width,
            int height,
            Vector4 border,
            string assetPath)
        {
            if (width <= 0 || height <= 0 ||
                border.x < 0f || border.y < 0f || border.z < 0f || border.w < 0f ||
                border.x + border.z + PsdNineSliceBorder.MinimumStretchCenterPixels > width ||
                border.y + border.w + PsdNineSliceBorder.MinimumStretchCenterPixels > height)
            {
                PsdLogger.Warning(
                    "Ignore invalid 9-slice border for generated PNG: " + assetPath +
                    ", texture=" + width + "x" + height +
                    ", border=" + border);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Keeps the Sprite border in the coordinate system used by the
        /// generated RectTransform when a target Canvas rescales PSD pixels.
        /// </summary>
        private static Vector4 ScaleNineSliceBorderForTargetCanvas(Vector4 border)
        {
            if (!UseTargetCanvasCoordinates)
            {
                return border;
            }

            PsdNineSliceBorder scaledBorder = new PsdNineSliceBorder(
                Mathf.RoundToInt(border.x),
                Mathf.RoundToInt(border.w),
                Mathf.RoundToInt(border.z),
                Mathf.RoundToInt(border.y)).Scale(
                GetTargetCanvasScaleX(),
                GetTargetCanvasScaleY());
            return PsdNineSliceTextureProcessor.ToUnityBorder(scaledBorder);
        }

        /// <summary>
        /// Gets scaled layer size for UI layout.
        /// </summary>
        /// <param name="rect">The PSD layer rectangle.</param>
        /// <returns>Scaled width and height.</returns>
        private static Vector2 GetUiLayerSize(Rect rect)
        {
            return new Vector2(rect.width * GetTargetCanvasScaleX(), rect.height * GetTargetCanvasScaleY());
        }

        /// <summary>
        /// Gets scale ratio for X axis when mapping PSD pixels to target canvas.
        /// </summary>
        /// <returns>Scale factor on X axis.</returns>
        private static float GetTargetCanvasScaleX()
        {
            if (!(UseTargetCanvasCoordinates && ScaleToTargetCanvas))
            {
                return 1f;
            }

            if (PreserveAspectWhenScalingToCanvas)
            {
                return GetTargetCanvasFitScale();
            }

            return CanvasSize.x > 0 ? TargetCanvasSize.x / CanvasSize.x : 1f;
        }

        /// <summary>
        /// Gets scale ratio for Y axis when mapping PSD pixels to target canvas.
        /// </summary>
        /// <returns>Scale factor on Y axis.</returns>
        private static float GetTargetCanvasScaleY()
        {
            if (!(UseTargetCanvasCoordinates && ScaleToTargetCanvas))
            {
                return 1f;
            }

            if (PreserveAspectWhenScalingToCanvas)
            {
                return GetTargetCanvasFitScale();
            }

            return CanvasSize.y > 0 ? TargetCanvasSize.y / CanvasSize.y : 1f;
        }

        /// <summary>
        /// Gets a uniform scale ratio for text/font scaling.
        /// </summary>
        /// <returns>Uniform scale ratio.</returns>
        private static float GetTargetCanvasUniformScale()
        {
            return Mathf.Min(GetTargetCanvasScaleX(), GetTargetCanvasScaleY());
        }

        /// <summary>
        /// Gets fit scale that preserves PSD aspect ratio inside the target canvas.
        /// </summary>
        /// <returns>Uniform fit scale.</returns>
        private static float GetTargetCanvasFitScale()
        {
            float scaleX = CanvasSize.x > 0 ? TargetCanvasSize.x / CanvasSize.x : 1f;
            float scaleY = CanvasSize.y > 0 ? TargetCanvasSize.y / CanvasSize.y : 1f;
            return Mathf.Min(scaleX, scaleY);
        }

        /// <summary>
        /// Gets root rect size for the generated PSD root under target canvas.
        /// </summary>
        /// <returns>Root rect size.</returns>
        private static Vector2 GetScaledRootSize()
        {
            if (!(UseTargetCanvasCoordinates && ScaleToTargetCanvas))
            {
                return CanvasSize;
            }

            if (!PreserveAspectWhenScalingToCanvas)
            {
                return TargetCanvasSize;
            }

            float fitScale = GetTargetCanvasFitScale();
            return CanvasSize * fitScale;
        }

        /// <summary>
        /// Gets the actual root RectTransform size.
        /// </summary>
        /// <returns>Root RectTransform size.</returns>
        private static Vector2 GetRootRectSize()
        {
            // A global-stretch root only takes the target Canvas dimensions
            // when the user explicitly requested Canvas scaling. In direct
            // 1:1 mode this must remain the authored PSD size.
            return UseTargetCanvasCoordinates && RootUseGlobalAnchorByDefault && ScaleToTargetCanvas
                ? TargetCanvasSize
                : GetScaledRootSize();
        }

        /// <summary>
        /// Gets the PSD content display size inside the current root RectTransform.
        /// </summary>
        /// <param name="rootRectSize">The actual root RectTransform size.</param>
        /// <returns>PSD content display size.</returns>
        private static Vector2 GetRootDisplaySize(Vector2 rootRectSize)
        {
            if (!UseTargetCanvasCoordinates)
            {
                return rootRectSize;
            }

            return GetScaledRootSize();
        }

        /// <summary>
        /// Gets the UI font size used by generated Unity UI text.
        /// </summary>
        /// <param name="layer">Source PSD text layer.</param>
        /// <returns>Scaled UI font size.</returns>
        private static float GetUIFontSize(Layer layer)
        {
            // PSD font size is in points (1pt = 1px at 72 DPI). Scale uniformly
            // by the same fit factor used for the PSD root so the text stays
            // proportional to both width and height, preventing overlap when the
            // target canvas aspect ratio differs from the PSD canvas.
            return ResolveTextTransform(layer).EffectiveFontSize(layer.FontSize) * GetTargetCanvasUniformScale();
        }

        private static PsdTextTransform ResolveTextTransform(Layer layer)
        {
            return layer != null && layer.TextStyle != null
                ? layer.TextStyle.Transform
                : PsdTextTransform.Identity;
        }

        /// <summary>
        /// Gets the effective layout rect for a layer, falling back to the raw PSD rect when needed.
        /// </summary>
        /// <param name="layer">Source PSD layer.</param>
        /// <returns>Resolved layout rect.</returns>
        private static Rect GetLayerLayoutRect(Layer layer)
        {
            LayerImportInfo info = GetLayerInfo(layer);
            if (info != null && info.HasLayoutRect)
            {
                return info.LayoutRect;
            }

            return layer != null ? layer.Rect : default(Rect);
        }

        /// <summary>
        /// Applies Photoshop layer opacity to a Unity color.
        /// </summary>
        /// <param name="color">Base color.</param>
        /// <param name="layer">Source PSD layer.</param>
        /// <returns>Color with layer opacity applied on alpha.</returns>
        private static Color ApplyLayerOpacity(Color color, Layer layer)
        {
            float layerOpacity = layer != null ? layer.Opacity / (float)byte.MaxValue : 1f;
            color.a = Mathf.Clamp01(color.a) * layerOpacity;
            return color;
        }

        /// <summary>
        /// Converts a PSD layer rect to a local anchored position relative to the current parent layout context.
        /// </summary>
        /// <param name="rect">The PSD layer rectangle.</param>
        /// <param name="parentContext">The current parent layout context.</param>
        /// <param name="preset">The anchor preset used by the child.</param>
        /// <returns>Local anchored position for the generated UI element.</returns>
        private static Vector2 GetAnchoredPositionForLayer(Rect rect, UiLayoutContext parentContext, AnchorNamePreset preset)
        {
            Vector2 localPoint = MapPsdPointToLocalSpace(GetPsdPresetPoint(rect, preset), parentContext);
            Vector2 anchorPoint = GetLocalPresetPoint(parentContext.LocalRectSize, preset);
            return localPoint - anchorPoint;
        }

        /// <summary>
        /// Maps a PSD point into the local coordinate space of the current parent RectTransform.
        /// </summary>
        /// <param name="psdPoint">PSD-space point.</param>
        /// <param name="context">Current UI layout context.</param>
        /// <returns>Local point in parent center-space coordinates.</returns>
        private static Vector2 MapPsdPointToLocalSpace(Vector2 psdPoint, UiLayoutContext context)
        {
            if (context.PsdReferenceRect.width <= 0f || context.PsdReferenceRect.height <= 0f)
            {
                return Vector2.zero;
            }

            float normalizedX = (psdPoint.x - context.PsdReferenceRect.xMin) / context.PsdReferenceRect.width;
            float normalizedY = (psdPoint.y - context.PsdReferenceRect.yMin) / context.PsdReferenceRect.height;

            float x = context.LocalDisplayRect.xMin + (normalizedX * context.LocalDisplayRect.width);
            float y = context.LocalDisplayRect.yMax - (normalizedY * context.LocalDisplayRect.height);
            return new Vector2(x, y);
        }

        /// <summary>
        /// Gets the PSD-space point for one anchor preset.
        /// </summary>
        /// <param name="rect">PSD-space rect.</param>
        /// <param name="preset">Anchor preset.</param>
        /// <returns>PSD-space anchor point.</returns>
        private static Vector2 GetPsdPresetPoint(Rect rect, AnchorNamePreset preset)
        {
            switch (NormalizePointAnchorPreset(preset))
            {
                case AnchorNamePreset.TopLeft:
                    return new Vector2(rect.xMin, rect.yMin);
                case AnchorNamePreset.BottomLeft:
                    return new Vector2(rect.xMin, rect.yMax);
                case AnchorNamePreset.TopRight:
                    return new Vector2(rect.xMax, rect.yMin);
                case AnchorNamePreset.BottomRight:
                    return new Vector2(rect.xMax, rect.yMax);
                case AnchorNamePreset.LeftMiddle:
                    return new Vector2(rect.xMin, rect.yMin + (rect.height * 0.5f));
                case AnchorNamePreset.RightMiddle:
                    return new Vector2(rect.xMax, rect.yMin + (rect.height * 0.5f));
                case AnchorNamePreset.TopMiddle:
                    return new Vector2(rect.xMin + (rect.width * 0.5f), rect.yMin);
                case AnchorNamePreset.BottomMiddle:
                    return new Vector2(rect.xMin + (rect.width * 0.5f), rect.yMax);
                case AnchorNamePreset.Center:
                default:
                    return rect.center;
            }
        }

        /// <summary>
        /// Gets the local-space anchor point for one anchor preset in a parent rect.
        /// </summary>
        /// <param name="size">Parent local rect size.</param>
        /// <param name="preset">Anchor preset.</param>
        /// <returns>Local-space anchor point.</returns>
        private static Vector2 GetLocalPresetPoint(Vector2 size, AnchorNamePreset preset)
        {
            Rect localRect = GetCenteredRect(size);
            switch (NormalizePointAnchorPreset(preset))
            {
                case AnchorNamePreset.TopLeft:
                    return new Vector2(localRect.xMin, localRect.yMax);
                case AnchorNamePreset.BottomLeft:
                    return new Vector2(localRect.xMin, localRect.yMin);
                case AnchorNamePreset.TopRight:
                    return new Vector2(localRect.xMax, localRect.yMax);
                case AnchorNamePreset.BottomRight:
                    return new Vector2(localRect.xMax, localRect.yMin);
                case AnchorNamePreset.LeftMiddle:
                    return new Vector2(localRect.xMin, 0f);
                case AnchorNamePreset.RightMiddle:
                    return new Vector2(localRect.xMax, 0f);
                case AnchorNamePreset.TopMiddle:
                    return new Vector2(0f, localRect.yMax);
                case AnchorNamePreset.BottomMiddle:
                    return new Vector2(0f, localRect.yMin);
                case AnchorNamePreset.Center:
                default:
                    return Vector2.zero;
            }
        }

        /// <summary>
        /// Gets the anchor vector used by RectTransform for a preset.
        /// </summary>
        /// <param name="preset">Anchor preset.</param>
        /// <returns>Unity anchor vector.</returns>
        private static Vector2 GetAnchorVector(AnchorNamePreset preset)
        {
            switch (NormalizePointAnchorPreset(preset))
            {
                case AnchorNamePreset.TopLeft:
                    return new Vector2(0f, 1f);
                case AnchorNamePreset.BottomLeft:
                    return new Vector2(0f, 0f);
                case AnchorNamePreset.TopRight:
                    return new Vector2(1f, 1f);
                case AnchorNamePreset.BottomRight:
                    return new Vector2(1f, 0f);
                case AnchorNamePreset.LeftMiddle:
                    return new Vector2(0f, 0.5f);
                case AnchorNamePreset.RightMiddle:
                    return new Vector2(1f, 0.5f);
                case AnchorNamePreset.TopMiddle:
                    return new Vector2(0.5f, 1f);
                case AnchorNamePreset.BottomMiddle:
                    return new Vector2(0.5f, 0f);
                case AnchorNamePreset.Center:
                default:
                    return new Vector2(0.5f, 0.5f);
            }
        }

        /// <summary>
        /// Normalizes a preset so regular point placement falls back to center.
        /// </summary>
        /// <param name="preset">Parsed preset.</param>
        /// <returns>Point-placement preset.</returns>
        private static AnchorNamePreset NormalizePointAnchorPreset(AnchorNamePreset preset)
        {
            return preset == AnchorNamePreset.None || preset == AnchorNamePreset.Global
                ? AnchorNamePreset.Center
                : preset;
        }

        /// <summary>
        /// Determines whether a preset represents global stretch anchoring.
        /// </summary>
        /// <param name="preset">Parsed preset.</param>
        /// <returns>True when the preset is global.</returns>
        private static bool IsGlobalAnchorPreset(AnchorNamePreset preset)
        {
            return preset == AnchorNamePreset.Global;
        }

        /// <summary>
        /// Creates a centered local rect for the given size.
        /// </summary>
        /// <param name="size">Rect size.</param>
        /// <returns>Centered rect.</returns>
        private static Rect GetCenteredRect(Vector2 size)
        {
            return new Rect(-size.x * 0.5f, -size.y * 0.5f, size.x, size.y);
        }
        #endregion
    }
}

