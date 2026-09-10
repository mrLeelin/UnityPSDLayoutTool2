// =============================================================================
//  __UnityEditorStubs.cs
//  プレイヤービルド専用の UnityEditor 互換スタブ。
//
//  このアセンブリ (cn.efunstudio.psd2ugui) は「全プラットフォーム」対象であり、
//  MonoBehaviour (Psd2UIFormConverter / PsdLayerNode) を GameObject にアタッチ
//  できるようにするために、エディタ専用でない扱いにする必要がある。
//  一方で PSD→UGUI 変換ロジックは本質的にエディタ時操作であり、
//  ランタイム型の本体には UnityEditor API 呼び出しが含まれる。
//
//  Unity のプレイヤーコンパイルパスでは UnityEditor は参照できないため、
//  ここで最小限のダミー型を UnityEditor 名前空間に供給し、
//  「コンパイルだけは通る」状態にする（これらの経路はプレイヤー実行時に走らない）。
//  エディタパスでは本物の UnityEditor が使われ、このファイルは丸ごと除外される。
// =============================================================================
#if !UNITY_EDITOR
using System;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityObject = UnityEngine.Object;

namespace UnityEditor
{
    // --- 属性 -----------------------------------------------------------------
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class MenuItemAttribute : Attribute
    {
        public int priority;
        public MenuItemAttribute(string itemName) { }
        public MenuItemAttribute(string itemName, bool isValidateFunction) { }
        public MenuItemAttribute(string itemName, bool isValidateFunction, int priority) { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class CanEditMultipleObjectsAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class InitializeOnLoadAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class CustomEditorAttribute : Attribute
    {
        public CustomEditorAttribute(Type inspectedType) { }
        public CustomEditorAttribute(Type inspectedType, bool editorForChildClasses) { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class CustomPropertyDrawerAttribute : Attribute
    {
        public CustomPropertyDrawerAttribute(Type type) { }
        public CustomPropertyDrawerAttribute(Type type, bool useForChildren) { }
    }

    public enum MessageType
    {
        None = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    public class EditorWindow : ScriptableObject
    {
        public Vector2 minSize;
        public Vector2 maxSize;
        public GUIContent titleContent;

        public static T GetWindow<T>() where T : EditorWindow => null;
        public static T GetWindow<T>(bool utility, string title) where T : EditorWindow => null;
        public static T GetWindow<T>(bool utility, string title, bool focus) where T : EditorWindow => null;
        public void Show() { }
        public void ShowUtility() { }
        public void Close() { }
        public void Repaint() { }
        public void Focus() { }
    }

    public class Editor : ScriptableObject
    {
        public UnityObject target;
        public UnityObject[] targets = Array.Empty<UnityObject>();
        public SerializedObject serializedObject;

        public virtual void OnInspectorGUI() { }
        public virtual bool HasPreviewGUI() => false;
        public virtual void OnPreviewGUI(Rect r, GUIStyle background) { }
        public virtual string GetInfoString() => string.Empty;
        public void Repaint() { }
    }

    public class PropertyDrawer
    {
        public virtual float GetPropertyHeight(SerializedProperty property, GUIContent label) => 0f;
        public virtual void OnGUI(Rect position, SerializedProperty property, GUIContent label) { }
    }

    public class SerializedObject
    {
        public SerializedObject(UnityObject obj) { }
        public SerializedObject(UnityObject[] objs) { }
        public UnityObject targetObject => null;
        public UnityObject[] targetObjects => Array.Empty<UnityObject>();
        public SerializedProperty FindProperty(string propertyPath) => null;
        public void Update() { }
        public bool ApplyModifiedProperties() => false;
        public bool ApplyModifiedPropertiesWithoutUndo() => false;
    }

    public class SerializedProperty
    {
        public bool isArray;
        public int arraySize;
        public bool boolValue;
        public int intValue;
        public int enumValueIndex;
        public float floatValue;
        public string stringValue;
        public UnityObject objectReferenceValue;

        public SerializedProperty FindPropertyRelative(string relativePropertyPath) => null;
        public SerializedProperty GetArrayElementAtIndex(int index) => null;
        public void ClearArray() { }
        public void InsertArrayElementAtIndex(int index) { }
    }

    public class PopupWindowContent
    {
        public EditorWindow editorWindow;

        public virtual Vector2 GetWindowSize() => default;
        public virtual void OnGUI(Rect rect) { }
    }

    public static class PopupWindow
    {
        public static void Show(Rect activatorRect, PopupWindowContent windowContent) { }
    }

    public class AssetPostprocessor { }

    // --- AssetDatabase --------------------------------------------------------
    public static class AssetDatabase
    {
        public static T LoadAssetAtPath<T>(string assetPath) where T : UnityObject => null;
        public static UnityObject LoadAssetAtPath(string assetPath, Type type) => null;
        public static UnityObject LoadMainAssetAtPath(string assetPath) => null;
        public static string GUIDToAssetPath(string guid) => string.Empty;
        public static string GUIDToAssetPath(GUID guid) => string.Empty;
        public static string AssetPathToGUID(string path) => string.Empty;
        public static GUID GUIDFromAssetPath(string path) => default;
        public static string[] FindAssets(string filter) => Array.Empty<string>();
        public static string[] FindAssets(string filter, string[] searchInFolders) => Array.Empty<string>();
        public static string GetAssetPath(UnityObject assetObject) => string.Empty;
        public static string GetAssetPath(int instanceID) => string.Empty;
        public static void CreateAsset(UnityObject asset, string path) { }
        public static void AddObjectToAsset(UnityObject objectToAdd, UnityObject assetObject) { }
        public static void AddObjectToAsset(UnityObject objectToAdd, string path) { }
        public static void SaveAssets() { }
        public static void SaveAssetIfDirty(UnityObject obj) { }
        public static void Refresh() { }
        public static void Refresh(ImportAssetOptions options) { }
        public static void ImportAsset(string path) { }
        public static void ImportAsset(string path, ImportAssetOptions options) { }
        public static string GenerateUniqueAssetPath(string path) => path;
        public static bool CopyAsset(string path, string newPath) => false;
        public static bool DeleteAsset(string path) => false;
        public static string MoveAsset(string oldPath, string newPath) => string.Empty;
        public static string ValidateMoveAsset(string oldPath, string newPath) => string.Empty;
        public static bool Contains(UnityObject obj) => false;
        public static bool Contains(int instanceID) => false;
        public static void StartAssetEditing() { }
        public static void StopAssetEditing() { }
        public static string[] GetDependencies(string pathName) => Array.Empty<string>();
        public static string[] GetDependencies(string pathName, bool recursive) => Array.Empty<string>();
        public static bool CreateFolder(string parentFolder, string newFolderName) => false;
        public static bool IsValidFolder(string path) => false;
        public static UnityObject[] LoadAllAssetsAtPath(string assetPath) => Array.Empty<UnityObject>();
        public static UnityObject[] LoadAllAssetRepresentationsAtPath(string assetPath) => Array.Empty<UnityObject>();
        public static void SetLabels(UnityObject obj, string[] labels) { }
        public static string[] GetLabels(UnityObject obj) => Array.Empty<string>();
        public static bool TryGetGUIDAndLocalFileIdentifier(UnityObject obj, out string guid, out long localId) { guid = string.Empty; localId = 0; return false; }
        public static bool WriteImportSettingsIfDirty(string assetPath) => false;
    }

    [Flags]
    public enum ImportAssetOptions
    {
        Default = 0,
        ForceUpdate = 1,
        ForceSynchronousImport = 8,
        ImportRecursive = 256,
        DontDownloadFromCacheServer = 8192,
        ForceUncompressedImport = 16384
    }

    // --- EditorUtility --------------------------------------------------------
    public static class EditorUtility
    {
        public static void SetDirty(UnityObject target) { }
        public static bool DisplayDialog(string title, string message, string ok) => false;
        public static bool DisplayDialog(string title, string message, string ok, string cancel) => false;
        public static int DisplayDialogComplex(string title, string message, string ok, string cancel, string alt) => 0;
        public static void DisplayProgressBar(string title, string info, float progress) { }
        public static bool DisplayCancelableProgressBar(string title, string info, float progress) => false;
        public static void ClearProgressBar() { }
        public static string OpenFolderPanel(string title, string folder, string defaultName) => string.Empty;
        public static string OpenFilePanel(string title, string directory, string extension) => string.Empty;
        public static string SaveFolderPanel(string title, string folder, string defaultName) => string.Empty;
        public static string SaveFilePanel(string title, string directory, string defaultName, string extension) => string.Empty;
        public static bool IsPersistent(UnityObject target) => false;
        public static void SetSelectedRenderState(Renderer renderer, EditorSelectedRenderState renderState) { }
        public static void CopySerialized(UnityObject source, UnityObject dest) { }
        public static void CopySerializedIfDifferent(UnityObject source, UnityObject dest) { }
        public static string FormatBytes(long bytes) => string.Empty;
        public static void UnloadUnusedAssetsImmediate() { }
        public static UnityObject InstanceIDToObject(int instanceID) => null;
        public static void RevealInFinder(string path) { }
    }

    public enum EditorSelectedRenderState { Hidden = 0, Wireframe = 1, Highlight = 2 }

    // --- Selection ------------------------------------------------------------
    public static class Selection
    {
        public static UnityObject activeObject;
        public static GameObject activeGameObject;
        public static Transform activeTransform;
        public static int activeInstanceID;
        public static UnityObject[] objects = Array.Empty<UnityObject>();
        public static GameObject[] gameObjects = Array.Empty<GameObject>();
        public static Transform[] transforms = Array.Empty<Transform>();
        public static string[] assetGUIDs = Array.Empty<string>();
        public static Transform[] GetTransforms(SelectionMode mode) => Array.Empty<Transform>();
        public static bool Contains(UnityObject o) => false;
        public static bool Contains(int instanceID) => false;
    }

    [Flags]
    public enum SelectionMode { Unfiltered = 0, TopLevel = 1, Deep = 2, ExcludePrefab = 4, Editable = 8, Assets = 16, DeepAssets = 32 }

    // --- Undo -----------------------------------------------------------------
    public static class Undo
    {
        public static void RecordObject(UnityObject objectToUndo, string name) { }
        public static void RecordObjects(UnityObject[] objectsToUndo, string name) { }
        public static void RegisterCreatedObjectUndo(UnityObject objectToUndo, string name) { }
        public static void RegisterCompleteObjectUndo(UnityObject objectToUndo, string name) { }
        public static void RegisterFullObjectHierarchyUndo(UnityObject objectToUndo, string name) { }
        public static void DestroyObjectImmediate(UnityObject objectToUndo) { }
        public static T AddComponent<T>(GameObject gameObject) where T : Component => null;
        public static Component AddComponent(GameObject gameObject, Type type) => null;
        public static void SetTransformParent(Transform transform, Transform newParent, string name) { }
        public static void CollapseUndoOperations(int groupIndex) { }
        public static int GetCurrentGroup() => 0;
        public static void SetCurrentGroupName(string name) { }
        public static void IncrementCurrentGroup() { }
        public static void FlushUndoRecordObjects() { }
    }

    // --- PrefabUtility --------------------------------------------------------
    public static class PrefabUtility
    {
        public static GameObject SaveAsPrefabAsset(GameObject instanceRoot, string assetPath) => null;
        public static GameObject SaveAsPrefabAsset(GameObject instanceRoot, string assetPath, out bool success) { success = false; return null; }
        public static GameObject SaveAsPrefabAssetAndConnect(GameObject instanceRoot, string assetPath, InteractionMode action) => null;
        public static GameObject InstantiatePrefab(UnityObject assetComponentOrGameObject) => null;
        public static GameObject GetCorrespondingObjectFromSource(GameObject componentOrGameObject) => null;
        public static T GetCorrespondingObjectFromSource<T>(T componentOrGameObject) where T : UnityObject => null;
        public static GameObject GetOutermostPrefabInstanceRoot(UnityObject componentOrGameObject) => null;
        public static bool IsPartOfPrefabAsset(UnityObject componentOrGameObject) => false;
        public static bool IsPartOfPrefabInstance(UnityObject componentOrGameObject) => false;
        public static bool IsAnyPrefabInstanceRoot(GameObject gameObject) => false;
        public static void UnpackPrefabInstance(GameObject instanceRoot, PrefabUnpackMode unpackMode, InteractionMode action) { }
        public static PrefabAssetType GetPrefabAssetType(UnityObject componentOrGameObject) => PrefabAssetType.NotAPrefab;
        public static string GetPrefabAssetPathOfNearestInstanceRoot(UnityObject componentOrGameObject) => string.Empty;
        public static void ApplyPrefabInstance(GameObject instanceRoot, InteractionMode action) { }
        public static void RecordPrefabInstancePropertyModifications(UnityObject targetObject) { }
    }

    public enum InteractionMode { AutomatedAction = 0, UserAction = 1 }
    public enum PrefabUnpackMode { OutermostRoot = 0, Completely = 1 }
    public enum PrefabAssetType { NotAPrefab = 0, Regular = 1, Model = 2, Variant = 3, MissingAsset = 4 }

    // --- SceneView ------------------------------------------------------------
    public class SceneView
    {
        public static event Action<SceneView> duringSceneGui { add { } remove { } }
        public static SceneView lastActiveSceneView;
        public static SceneView currentDrawingSceneView;
        public Camera camera;
        public bool in2DMode;
        public void Repaint() { }
        public static void RepaintAll() { }
        public void Focus() { }
        public void FrameSelected() { }
    }

    // --- GenericMenu ----------------------------------------------------------
    public class GenericMenu
    {
        public delegate void MenuFunction();
        public delegate void MenuFunction2(object userData);
        public void AddItem(GUIContent content, bool on, MenuFunction func) { }
        public void AddItem(GUIContent content, bool on, MenuFunction2 func, object userData) { }
        public void AddDisabledItem(GUIContent content) { }
        public void AddDisabledItem(GUIContent content, bool on) { }
        public void AddSeparator(string path) { }
        public int GetItemCount() => 0;
        public void ShowAsContext() { }
        public void DropDown(Rect position) { }
    }

    // --- その他よく参照されるユーティリティ -------------------------------------
    public enum PlayModeStateChange { EnteredEditMode = 0, ExitingEditMode = 1, EnteredPlayMode = 2, ExitingPlayMode = 3 }

    public static class AssemblyReloadEvents
    {
        public delegate void AssemblyReloadCallback();
        public static event AssemblyReloadCallback beforeAssemblyReload { add { } remove { } }
        public static event AssemblyReloadCallback afterAssemblyReload { add { } remove { } }
    }

    public static class EditorApplication
    {
        public delegate void CallbackFunction();
        public delegate void HierarchyWindowItemCallback(int instanceID, Rect selectionRect);
        public static CallbackFunction update;
        public static CallbackFunction delayCall;
        public static HierarchyWindowItemCallback hierarchyWindowItemOnGUI;
        public static Action quitting;
        public static Action<PlayModeStateChange> playModeStateChanged;
        public static bool isPlaying;
        public static bool isPlayingOrWillChangePlaymode;
        public static bool isCompiling;
        public static bool isUpdating;
        public static void Beep() { }
        public static void ExecuteMenuItem(string menuItemPath) { }
        public static void RepaintHierarchyWindow() { }
        public static void RepaintProjectWindow() { }
        public static void QueuePlayerLoopUpdate() { }
    }

    public static class EditorGUIUtility
    {
        public static float pixelsPerPoint => 1f;
        public static float singleLineHeight => 18f;
        public static float currentViewWidth => 0f;
        public static string systemCopyBuffer { get; set; }
        public static Texture2D whiteTexture => null;
        public static void PingObject(UnityObject obj) { }
        public static void PingObject(int targetInstanceID) { }
        public static GUIContent IconContent(string name) => new GUIContent();
        public static GUIContent TrTextContent(string text, string tooltip = null) => new GUIContent();
        public static Texture2D FindTexture(string name) => null;
    }

    // --- テクスチャ/アセットインポータ関連 enum -------------------------------
    public enum TextureImporterType { Default = 0, NormalMap = 1, GUI = 2, Sprite = 8, Cursor = 7, Cookie = 4, Lightmap = 6, SingleChannel = 10, Shadowmask = 11, DirectionalLightmap = 12 }
    public enum SpriteImportMode { None = 0, Single = 1, Multiple = 2, Polygon = 3 }
    public enum TextureImporterAlphaSource { None = 0, FromInput = 1, FromGrayScale = 2 }
    public enum TextureImporterNPOTScale { None = 0, ToNearest = 1, ToLarger = 2, ToSmaller = 3 }
    public enum TextureImporterShape { Texture2D = 1, TextureCube = 2, Texture2DArray = 4, Texture3D = 8 }
    public enum TextureImporterCompression { Uncompressed = 0, Compressed = 1, CompressedHQ = 2, CompressedLQ = 3 }
    public enum TextureImporterFormat { Automatic = -1, RGBA32 = 4, ARGB32 = 5, RGBA64 = 76 }

    public enum BuildTarget { NoTarget = -2, StandaloneWindows = 5, StandaloneWindows64 = 19, Android = 13, iOS = 9 }
    public enum BuildTargetGroup { Unknown = 0, Standalone = 1, iOS = 4, Android = 7 }

    public static class BuildPipeline
    {
        public static bool IsBuildTargetSupported(BuildTargetGroup buildTargetGroup, BuildTarget target) => false;
        public static BuildTargetGroup GetBuildTargetGroup(BuildTarget target) => BuildTargetGroup.Unknown;
        public static string GetBuildTargetName(BuildTarget target) => string.Empty;
    }

    // --- AssetImporter / TextureImporter / TrueTypeFontImporter ---------------
    public class AssetImporter : UnityObject
    {
        public string assetPath;
        public string userData;
        public string assetBundleName;
        public string assetBundleVariant;
        public static AssetImporter GetAtPath(string path) => null;
        public void SaveAndReimport() { }
    }

    public sealed class TextureImporterSettings
    {
        public TextureImporterType textureType;
        public SpriteImportMode spriteImportMode;
        public bool readable;
        public bool mipmapEnabled;
        public bool alphaIsTransparency;
        public TextureImporterAlphaSource alphaSource;
        public TextureImporterNPOTScale npotScale;
        public FilterMode filterMode;
        public TextureWrapMode wrapMode;
        public float spritePixelsPerUnit;
        public int spriteExtrude;
        public int spriteMode;
        public int spriteAlignment;
        public Vector2 spritePivot;
        public void CopyTo(TextureImporterSettings target) { }
    }

    public sealed class TextureImporterPlatformSettings
    {
        public string name;
        public bool overridden;
        public TextureImporterFormat format;
        public int maxTextureSize;
        public bool allowsAlphaSplitting;
        public TextureImporterCompression textureCompression;
    }

    public class TextureImporter : AssetImporter
    {
        public TextureImporterType textureType;
        public SpriteImportMode spriteImportMode;
        public bool isReadable;
        public bool mipmapEnabled;
        public bool alphaIsTransparency;
        public TextureImporterAlphaSource alphaSource;
        public TextureImporterNPOTScale npotScale;
        public TextureImporterShape textureShape;
        public TextureImporterCompression textureCompression;
        public FilterMode filterMode;
        public TextureWrapMode wrapMode;
        public float spritePixelsPerUnit;
        public Vector4 spriteBorder;
        public int maxTextureSize;
        public void ReadTextureSettings(TextureImporterSettings dest) { }
        public void SetTextureSettings(TextureImporterSettings src) { }
        public TextureImporterPlatformSettings GetPlatformTextureSettings(string platform) => new TextureImporterPlatformSettings();
        public void SetPlatformTextureSettings(TextureImporterPlatformSettings platformSettings) { }
        public bool GetSourceTextureWidthAndHeight(out int width, out int height) { width = 0; height = 0; return false; }
        public static bool IsDefaultPlatformTextureFormatValid(TextureImporterType textureType, TextureImporterFormat format) => false;
        public static bool IsPlatformTextureFormatValid(TextureImporterType textureType, BuildTarget target, TextureImporterFormat format) => false;
    }

    public class TrueTypeFontImporter : AssetImporter
    {
        public int fontSize;
        public Font[] fontReferences;
        public string fontTTFName;
        public bool includeFontData;
    }

    // --- MonoScript -----------------------------------------------------------
    public class MonoScript : TextAsset
    {
        public static MonoScript FromScriptableObject(ScriptableObject scriptableObject) => null;
        public static MonoScript FromMonoBehaviour(MonoBehaviour behaviour) => null;
        public Type GetClass() => null;
    }

    // --- ArrayUtility ---------------------------------------------------------
    public static class ArrayUtility
    {
        public static void Add<T>(ref T[] array, T item) { }
        public static void AddRange<T>(ref T[] array, T[] items) { }
        public static void Remove<T>(ref T[] array, T item) { }
        public static void RemoveAt<T>(ref T[] array, int index) { }
        public static void Insert<T>(ref T[] array, int index, T item) { }
        public static void Clear<T>(ref T[] array) { }
        public static bool Contains<T>(T[] array, T item) => false;
        public static int IndexOf<T>(T[] array, T item) => -1;
        public static int FindIndex<T>(T[] array, Predicate<T> match) => -1;
    }

    // --- EditorPrefs ----------------------------------------------------------
    public static class EditorPrefs
    {
        public static bool GetBool(string key, bool defaultValue = false) => defaultValue;
        public static void SetBool(string key, bool value) { }
        public static int GetInt(string key, int defaultValue = 0) => defaultValue;
        public static void SetInt(string key, int value) { }
        public static float GetFloat(string key, float defaultValue = 0f) => defaultValue;
        public static void SetFloat(string key, float value) { }
        public static string GetString(string key, string defaultValue = "") => defaultValue;
        public static void SetString(string key, string value) { }
        public static bool HasKey(string key) => false;
        public static void DeleteKey(string key) { }
    }

    // --- HandleUtility --------------------------------------------------------
    public static class HandleUtility
    {
        public static Ray GUIPointToWorldRay(Vector2 position) => default;
        public static Vector2 WorldToGUIPoint(Vector3 world) => default;
        public static float GetHandleSize(Vector3 position) => 1f;
        public static int nearestControl;
    }

    // --- GUID / GlobalObjectId ------------------------------------------------
    public struct GUID
    {
        public GUID(string hexRepresentation) { }
        public bool Empty() => true;
        public static bool operator ==(GUID lhs, GUID rhs) => true;
        public static bool operator !=(GUID lhs, GUID rhs) => false;
        public override bool Equals(object obj) => obj is GUID;
        public override int GetHashCode() => 0;
        public override string ToString() => string.Empty;
    }

    public struct GlobalObjectId
    {
        public static GlobalObjectId GetGlobalObjectIdSlow(UnityObject targetObject) => default;
        public static bool TryParse(string stringValue, out GlobalObjectId id) { id = default; return false; }
        public override string ToString() => string.Empty;
    }

    public class ScriptableSingleton<T> : ScriptableObject where T : ScriptableSingleton<T>
    {
        public static T instance => null;
    }

    public class SceneVisibilityManager : ScriptableSingleton<SceneVisibilityManager>
    {
        public bool IsHidden(GameObject gameObject) => false;
    }

    public static class EditorGUILayout
    {
        public sealed class HorizontalScope : IDisposable
        {
            public HorizontalScope(params GUILayoutOption[] options) { }
            public void Dispose() { }
        }

        public sealed class VerticalScope : IDisposable
        {
            public VerticalScope(params GUILayoutOption[] options) { }
            public VerticalScope(GUIStyle style, params GUILayoutOption[] options) { }
            public void Dispose() { }
        }

        public static void BeginHorizontal(params GUILayoutOption[] options) { }
        public static void EndHorizontal() { }
        public static void BeginVertical(GUIStyle style, params GUILayoutOption[] options) { }
        public static void EndVertical() { }
        public static Vector2 BeginScrollView(Vector2 scrollPosition, params GUILayoutOption[] options) => scrollPosition;
        public static void EndScrollView() { }
        public static void LabelField(string label, params GUILayoutOption[] options) { }
        public static void LabelField(string label, GUIStyle style, params GUILayoutOption[] options) { }
        public static void LabelField(string label, string label2, params GUILayoutOption[] options) { }
        public static void HelpBox(string message, MessageType type) { }
        public static void Space() { }
        public static void Space(float pixels) { }
        public static void PrefixLabel(GUIContent label) { }
        public static string PasswordField(string label, string password, params GUILayoutOption[] options) => password;
        public static string TextField(string text, params GUILayoutOption[] options) => text;
        public static string TextField(string label, string text, params GUILayoutOption[] options) => text;
        public static string TextArea(string text, params GUILayoutOption[] options) => text;
        public static int IntField(string label, int value, params GUILayoutOption[] options) => value;
        public static int IntSlider(string label, int value, int leftValue, int rightValue, params GUILayoutOption[] options) => value;
        public static int IntPopup(string label, int selectedValue, string[] displayedOptions, int[] optionValues, params GUILayoutOption[] options) => selectedValue;
        public static bool ToggleLeft(string label, bool value, params GUILayoutOption[] options) => value;
        public static Enum EnumPopup(string label, Enum selected, params GUILayoutOption[] options) => selected;
        public static bool Foldout(bool foldout, string content, bool toggleOnLabelClick) => foldout;
        public static int Popup(string label, int selectedIndex, string[] displayedOptions, params GUILayoutOption[] options) => selectedIndex;
        public static void PropertyField(SerializedProperty property, GUIContent label, params GUILayoutOption[] options) { }
        public static Vector2 Vector2Field(string label, Vector2 value, params GUILayoutOption[] options) => value;
        public static Color ColorField(string label, Color value, params GUILayoutOption[] options) => value;
        public static Rect GetControlRect(bool hasLabel, float height, params GUILayoutOption[] options) => default;
    }

    // --- MonoScript / IMGUI 補助 ----------------------------------------------
    public static class EditorGUI
    {
        public sealed class DisabledScope : IDisposable
        {
            public DisabledScope(bool disabled) { }
            public void Dispose() { }
        }

        public static int indentLevel;
        public static void BeginChangeCheck() { }
        public static bool EndChangeCheck() => false;
        public static void BeginDisabledGroup(bool disabled) { }
        public static void EndDisabledGroup() { }
        public static bool Toggle(Rect position, bool value) => value;
        public static bool Toggle(Rect position, string label, bool value) => value;
        public static bool Toggle(Rect position, GUIContent label, bool value) => value;
        public static bool DropdownButton(Rect position, GUIContent content, FocusType focusType) => false;
        public static bool DropdownButton(Rect position, GUIContent content, FocusType focusType, GUIStyle style) => false;
        public static void DrawRect(Rect rect, Color color) { }
        public static Rect IndentedRect(Rect source) => source;
        public static Rect PrefixLabel(Rect totalPosition, GUIContent label) => totalPosition;
        public static float GetPropertyHeight(SerializedProperty property, GUIContent label, bool includeChildren) => 0f;
        public static bool PropertyField(Rect position, SerializedProperty property, GUIContent label, bool includeChildren) => false;
    }

    public static class EditorStyles
    {
        public static GUIStyle boldLabel => null;
        public static GUIStyle helpBox => null;
        public static GUIStyle miniButton => null;
        public static GUIStyle popup => null;
        public static GUIStyle wordWrappedLabel => null;
        public static GUIStyle label => null;
        public static GUIStyle textField => null;
        public static GUIStyle foldout => null;
    }
}

namespace UnityEditorInternal
{
    using UnityEngine;

    public static class InternalEditorUtility
    {
        public static UnityEngine.Object[] LoadSerializedFileAndForget(string path) => System.Array.Empty<UnityEngine.Object>();
        public static void SaveToSerializedFileAndForget(UnityEngine.Object[] obj, string path, bool allowTextSerialization) { }
        public static void RepaintAllViews() { }
    }

    public static class ComponentUtility
    {
        public static bool CopyComponent(Component component) => false;
        public static bool PasteComponentAsNew(GameObject go) => false;
        public static bool PasteComponentValues(Component component) => false;
        public static bool MoveComponentUp(Component component) => false;
        public static bool MoveComponentDown(Component component) => false;
    }
}

namespace UnityEditor.SceneManagement
{
    using UnityEngine.SceneManagement;

    public static class EditorSceneManager
    {
        public static bool MarkSceneDirty(Scene scene) => false;
        public static void MarkAllScenesDirty() { }
        public static Scene GetActiveScene() => default;
    }

    public class PrefabStage
    {
        public GameObject prefabContentsRoot;
        public string assetPath;
        public Scene scene;
        public UnityEngine.SceneManagement.Scene GetScene() => default;
    }

    public static class PrefabStageUtility
    {
        public static PrefabStage GetCurrentPrefabStage() => null;
        public static PrefabStage GetPrefabStage(GameObject gameObject) => null;
        public static PrefabStage OpenPrefab(string prefabAssetPath) => null;
    }

    public class Stage
    {
        public string assetPath;
    }

    public static class StageUtility
    {
        public static Stage GetCurrentStage() => null;
        public static void GoToMainStage() { }
        public static void PlaceGameObjectInCurrentStage(GameObject gameObject) { }
    }
}

namespace UnityEditor.PackageManager
{
    public class PackageInfo
    {
        public string name;
        public string displayName;
        public string version;
        public string assetPath;
        public string resolvedPath;
        public static PackageInfo FindForAssetPath(string assetPath) => null;
        public static PackageInfo FindForAssembly(System.Reflection.Assembly assembly) => null;
    }
}
#endif
