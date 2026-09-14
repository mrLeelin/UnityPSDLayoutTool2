using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using ReadOnlyFieldAttributeNamespace;
using PsdLayerExtensionsNamespace;
using AssetNameSanitizerNamespace;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using cn.efunstudio.psdreader.PsdParser;
using AiLocalHierarchyNormalizerNamespace;
using PathCompatibilityUtilityNamespace;
using UGF.EditorTools.Psd2UGUI.NineSlice;

namespace UGF.EditorTools.Psd2UGUI
{
    /// <summary>
    /// PSD 生成物的生成期逻辑（Editor 程序集）。
    ///
    /// 拆分后 MonoBehaviour 身份与序列化状态都在运行期壳 <see cref="Psd2UIFormConverter"/> 上，
    /// 本类只持有该壳的引用，并把壳的序列化字段与组件成员转发进来，因此原方法体基本无需改动。
    /// 挂接/摘除/绘制 Gizmos 由壳的同名消息经 Psd2UIFormEditorHost 门面触发，
    /// 保证触发时机与拆分前完全一致。
    /// </summary>
    internal sealed class Psd2UIFormConverterEditor
    {
        private readonly Psd2UIFormConverter _owner;

        private bool _attached;

        private static Psd2UIFormConverterEditor s_Instance;

        // 按壳实例 ID 索引：OnDestroy 期间 Unity 的 == 已把 owner 判为 null，
        // 那时 GetOrCreate 的存在性判断会失败，Detach 就退订不到回调。
        private static readonly Dictionary<int, Psd2UIFormConverterEditor> s_byOwnerInstanceId = new Dictionary<int, Psd2UIFormConverterEditor>();

        internal Psd2UIFormConverterEditor(Psd2UIFormConverter owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        internal Psd2UIFormConverter Owner => _owner;

        /// <summary>
        /// 当前场景/Stage 中那个 converter 壳对应的逻辑对象。
        /// 按壳实例缓存，使 _psdDocument 与引用索引等实例状态不会每次访问被重建。
        /// 壳被销毁后 Unity 的 == 会把它判为 null，因此这里返回 null。
        /// </summary>
        internal static Psd2UIFormConverterEditor Instance => GetOrCreate(Psd2UIFormConverter.Instance);

        internal static Psd2UIFormConverterEditor GetOrCreate(Psd2UIFormConverter owner)
        {
            if ((Object)(object)owner == (Object)null)
            {
                s_Instance = null;
                return null;
            }
            Psd2UIFormConverterEditor editor = s_Instance;
            if (editor == null || (Object)(object)editor._owner != (Object)(object)owner)
            {
                editor = new Psd2UIFormConverterEditor(owner);
                s_Instance = editor;
            }
            s_byOwnerInstanceId[owner.GetInstanceID()] = editor;
            return editor;
        }

        /// <summary>
        /// Detach 专用：按实例 ID 找到已挂接的逻辑对象，不依赖 "壳是否仍然存活" 的判断，
        /// 因为 OnDestroy 阶段壳已经被 Unity 判为 null。
        /// </summary>
        internal static Psd2UIFormConverterEditor GetAttached(Psd2UIFormConverter owner)
        {
            if ((Object)owner == (Object)null)
            {
                return null;
            }
            Psd2UIFormConverterEditor editor;
            if (s_byOwnerInstanceId.TryGetValue(owner.GetInstanceID(), out editor) && (Object)(object)editor._owner == (Object)(object)owner)
            {
                return editor;
            }
            return null;
        }

        // ---- 壳的组件成员转发 ----

        internal GameObject gameObject => _owner.gameObject;

        internal Transform transform => _owner.transform;

        internal T GetComponent<T>()
        {
            return _owner.GetComponent<T>();
        }

        internal T[] GetComponents<T>()
        {
            return _owner.GetComponents<T>();
        }

        internal T[] GetComponentsInChildren<T>()
        {
            return _owner.GetComponentsInChildren<T>();
        }

        internal T[] GetComponentsInChildren<T>(bool includeInactive)
        {
            return _owner.GetComponentsInChildren<T>(includeInactive);
        }

        internal bool TryGetComponent<T>(out T component)
        {
            return _owner.TryGetComponent<T>(out component);
        }

        // ---- 壳的序列化字段转发（字段名与拆分前一致，序列化不变） ----

        internal string psdAssetChangeTime
        {
            get { return _owner.psdAssetChangeTime; }
            set { _owner.psdAssetChangeTime = value; }
        }

        internal string uiFormName
        {
            get { return _owner.uiFormName; }
            set { _owner.uiFormName = value; }
        }

        internal Sprite psdAsset
        {
            get { return _owner.psdAsset; }
            set { _owner.psdAsset = value; }
        }

        internal Sprite previewSprite
        {
            get { return _owner.previewSprite; }
            set { _owner.previewSprite = value; }
        }

        internal string psdAssetPath
        {
            get { return _owner.psdAssetPath; }
            set { _owner.psdAssetPath = value; }
        }

        internal bool drawLayerRectGizmos
        {
            get { return _owner.drawLayerRectGizmos; }
            set { _owner.drawLayerRectGizmos = value; }
        }

        internal Color drawLayerRectGizmosColor
        {
            get { return _owner.drawLayerRectGizmosColor; }
            set { _owner.drawLayerRectGizmosColor = value; }
        }

        internal bool preferSmallestLayerOnScenePick
        {
            get { return _owner.preferSmallestLayerOnScenePick; }
            set { _owner.preferSmallestLayerOnScenePick = value; }
        }

        internal List<GeneratedMetadataSerializedEntry> generatedMetadataEntries
        {
            get { return _owner.generatedMetadataEntries; }
            set { _owner.generatedMetadataEntries = value; }
        }

        [Serializable]
        private sealed class GeneratedMetadataCollection
        {
            public List<GeneratedMetadataEntry> Entries = new List<GeneratedMetadataEntry>();

            internal static GeneratedMetadataCollection s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static GeneratedMetadataCollection GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private sealed class GeneratedKeySnapshot
        {
            public string Key;

            public string TypeKey;

            public bool IsContainer;

            public List<int> SiblingIndexPath;

            internal static GeneratedKeySnapshot s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static GeneratedKeySnapshot GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private sealed class NodeTypeSnapshot
        {
            public GUIType UIType;

            private static NodeTypeSnapshot s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static NodeTypeSnapshot GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private sealed class RenderedLayerEntry
        {
            internal PsdLayer Layer;

            internal PsdRenderedImage RenderedImage;

            internal static RenderedLayerEntry s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static RenderedLayerEntry GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [CompilerGenerated]
        private sealed class PrefabGenerationScope
        {
            public List<PsdLayerNode> PrefabReferenceRoots;

            public string ScopedNodePath;

            private static PrefabGenerationScope s_ObfuscationSentinel;

            internal bool ContainsTransform(Transform transform)
            {
                if (!((Object)(object)transform == (Object)null) && PrefabReferenceRoots.Count != 0)
                {
                    foreach (PsdLayerNode item in PrefabReferenceRoots)
                    {
                        if (!((Object)(object)item == (Object)null) && ((Object)(object)transform == (Object)(object)((Component)item).transform || transform.IsChildOf(((Component)item).transform)))
                        {
                            return true;
                        }
                    }
                    return false;
                }
                return false;
            }

            internal bool IsNodeOutsidePrefabReferenceRoots(PsdLayerNode node)
            {
                if (!((Object)(object)node != (Object)null))
                {
                    return false;
                }
                return !ContainsTransform(((Component)node).transform);
            }

            internal bool IsGeneratedKeyOutsideScope(PsdGeneratedKey generatedKey)
            {
                return !IsKeyWithinScope(generatedKey.Key, ScopedNodePath);
            }

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static PrefabGenerationScope GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [CompilerGenerated]
        private sealed class ReferencedPrefabExportScope
        {
            public List<PsdLayerNode> NestedPrefabReferenceRoots;

            internal static ReferencedPrefabExportScope s_ObfuscationSentinel;

            internal bool ContainsTransform(Transform transform)
            {
                if (!((Object)(object)transform == (Object)null) && NestedPrefabReferenceRoots.Count != 0)
                {
                    foreach (PsdLayerNode item in NestedPrefabReferenceRoots)
                    {
                        if (!((Object)(object)item == (Object)null) && ((Object)(object)transform == (Object)(object)((Component)item).transform || transform.IsChildOf(((Component)item).transform)))
                        {
                            return true;
                        }
                    }
                    return false;
                }
                return false;
            }

            internal bool IsNodeOutsidePrefabReferenceRoots(PsdLayerNode node)
            {
                if (!((Object)(object)node != (Object)null))
                {
                    return false;
                }
                return !ContainsTransform(((Component)node).transform);
            }

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static ReferencedPrefabExportScope GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private PsdDocument _psdDocument;

        private GUIStyle uiTypeLabelStyle;

        private readonly Dictionary<string, PsdLayerNode> _nodeByReferenceKey = new Dictionary<string, PsdLayerNode>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> _exportedPathByNodeKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, GameObject> _prefabAssetByReferenceKey = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _referencedAssetKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _prefabExportsInProgress = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [SpecialName]
        internal bool IsDocumentLoaded()
        {
            return _psdDocument != null;
        }

        [SpecialName]
        internal PsdDocument GetPsdDocument()
        {
            return _psdDocument;
        }

        [SpecialName]
        internal string GetSourcePsdAssetPath()
        {
            if ((Object)(object)psdAsset != (Object)null)
            {
                string assetPath = AssetDatabase.GetAssetPath((Object)(object)psdAsset);
                if (!string.IsNullOrWhiteSpace(assetPath))
                {
                    return assetPath;
                }
            }
            if (string.IsNullOrWhiteSpace(psdAssetPath))
            {
                return null;
            }
            return psdAssetPath;
        }

        [SpecialName]
        internal string GetUIFormName()
        {
            return uiFormName;
        }

        [SpecialName]
        internal Sprite GetPreviewSprite()
        {
            if (!((Object)(object)psdAsset != (Object)null))
            {
                return previewSprite;
            }
            return psdAsset;
        }

        [SpecialName]
        internal Vector2Int GetDocumentSize()
        {
            if (_psdDocument != null)
            {
                return new Vector2Int(_psdDocument.Width, _psdDocument.Height);
            }
            return Vector2Int.zero;
        }

        internal void Attach()
        {
            if (!_attached)
            {
                _attached = true;
                uiTypeLabelStyle = new GUIStyle();
                uiTypeLabelStyle.fontSize = 13;
                uiTypeLabelStyle.fontStyle = (FontStyle)3;
                Color textColor = default(Color);
                ColorUtility.TryParseHtmlString("#7ED994", out textColor);
                uiTypeLabelStyle.normal.textColor = textColor;
                SceneView.duringSceneGui += OnSceneGUI;
                EditorApplication.hierarchyWindowItemOnGUI = (EditorApplication.HierarchyWindowItemCallback)Delegate.Combine((Delegate)(object)EditorApplication.hierarchyWindowItemOnGUI, (Delegate)new EditorApplication.HierarchyWindowItemCallback(OnHierarchyWindowItemGUI));
            }
            // 回调订阅只需一次，但文档必须补加载：
            // 壳是先生成（psdAssetPath 为空）后绑定 PSD 的，先 Attach 再绑定是常见顺序，
            // 不补加载的话 Inspector 会一直显示"请打开Prefab…"。
            if (_psdDocument == null && !string.IsNullOrWhiteSpace(GetSourcePsdAssetPath()))
            {
                LoadDocumentAndRebindNodes();
            }
        }

        /// <summary>原 MonoBehaviour.Start 的行为，由壳的 Start 经门面转发，保持时机与拆分前一致。</summary>
        internal void LoadDocument()
        {
            LoadDocumentAndRebindNodes();
        }

        internal void DrawGizmos()
        {
            if (!drawLayerRectGizmos)
            {
                return;
            }
            PsdLayerNode[] componentsInChildren = GetComponentsInChildren<PsdLayerNode>();
            PsdLayerNode psdLayerNode = null;
            Gizmos.color = drawLayerRectGizmosColor;
            GameObject activeGameObject = Selection.activeGameObject;
            PsdLayerNode[] array = componentsInChildren;
            Rect val;
            foreach (PsdLayerNode psdLayerNode2 in array)
            {
                if ((Object)(object)((Component)psdLayerNode2).gameObject == (Object)(object)activeGameObject)
                {
                    psdLayerNode = psdLayerNode2;
                }
                else if (psdLayerNode2.ShouldExportImage())
                {
                    val = psdLayerNode2.GetLayerRect();
                    Vector3 val2 = (Vector2)(val.position * 0.01f);
                    val = psdLayerNode2.GetLayerRect();
                    Gizmos.DrawWireCube(val2, (Vector2)(val.size * 0.01f));
                }
            }
            if ((Object)(object)psdLayerNode != (Object)null)
            {
                Gizmos.color = Color.green;
                val = psdLayerNode.GetLayerRect();
                Vector3 val3 = (Vector2)(val.position * 0.01f);
                val = psdLayerNode.GetLayerRect();
                Gizmos.DrawWireCube(val3, (Vector2)(val.size * 0.01f));
            }
        }

        private void OnSceneGUI(SceneView view)
        {
            if ((Object)(object)_owner == (Object)null)
            {
                if (_attached)
                {
                    Detach();
                }
                return;
            }
            Event current = Event.current;
            if (current == null || (int)current.type != 1 || current.button != 0)
            {
                return;
            }
            Ray val = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            Plane val2 = default(Plane);
            val2 = new Plane(Vector3.forward, Vector3.zero);
            float num = default(float);
            if (val2.Raycast(val, out num))
            {
                Vector3 point = val.GetPoint(num);
                PsdLayerNode psdLayerNode = (preferSmallestLayerOnScenePick ? FindSmallestNodeAtPoint(point, this.transform) : FindTopmostNodeAtPoint(point, this.transform));
                if ((Object)(object)psdLayerNode != (Object)null)
                {
                    Selection.activeGameObject = ((Component)psdLayerNode).gameObject;
                    EditorGUIUtility.PingObject((Object)(object)((Component)psdLayerNode).gameObject);
                    Event.current.Use();
                }
            }
        }

        private static PsdLayerNode FindTopmostNodeAtPoint(Vector3 vector, object value)
        {
            if (!((Object)value == (Object)null))
            {
                if (!IsSceneObjectPickable(((Component)value).gameObject))
                {
                    return null;
                }
                int num = ((Transform)value).childCount - 1;
                PsdLayerNode psdLayerNode;
                while (true)
                {
                    if (num >= 0)
                    {
                        Transform child = ((Transform)value).GetChild(num);
                        psdLayerNode = FindTopmostNodeAtPoint(vector, child);
                        if ((Object)(object)psdLayerNode != (Object)null)
                        {
                            break;
                        }
                        num--;
                        continue;
                    }
                    PsdLayerNode component = ((Component)value).GetComponent<PsdLayerNode>();
                    if ((Object)(object)component != (Object)null && ContainsWorldPoint(vector, component.GetLayerRect()))
                    {
                        return component;
                    }
                    return null;
                }
                return psdLayerNode;
            }
            return null;
        }

        private static PsdLayerNode FindSmallestNodeAtPoint(Vector3 vector, object value)
        {
            PsdLayerNode result = null;
            float num = float.MaxValue;
            FindSmallestNodeAtPointRecursive(vector, value, ref result, ref num);
            return result;
        }

        private static void FindSmallestNodeAtPointRecursive(Vector3 vector, object value, ref PsdLayerNode layerNode, ref float value2)
        {
            if ((Object)value == (Object)null || !IsSceneObjectPickable(((Component)value).gameObject))
            {
                return;
            }
            for (int num = ((Transform)value).childCount - 1; num >= 0; num--)
            {
                FindSmallestNodeAtPointRecursive(vector, ((Transform)value).GetChild(num), ref layerNode, ref value2);
            }
            PsdLayerNode component = ((Component)value).GetComponent<PsdLayerNode>();
            if (!((Object)(object)component == (Object)null) && ContainsWorldPoint(vector, component.GetLayerRect()))
            {
                float num2 = CalculateRectArea(component.GetLayerRect());
                if (num2 < value2)
                {
                    value2 = num2;
                    layerNode = component;
                }
            }
        }

        private static float CalculateRectArea(Rect rect)
        {
            Vector2 size = rect.size;
            return size.x * size.y;
        }

        private static bool IsSceneObjectPickable(object value)
        {
            if (!((Object)value == (Object)null) && ((GameObject)value).activeInHierarchy)
            {
                if ((((Object)value).hideFlags & HideFlags.HideInHierarchy) == 0)
                {
                    return !UnityEditor.ScriptableSingleton<SceneVisibilityManager>.instance.IsHidden((GameObject)value);
                }
                return false;
            }
            return false;
        }

        private static bool ContainsWorldPoint(Vector3 vector, Rect rect)
        {
            Vector2 val = rect.position * 0.01f;
            Vector2 val2 = rect.size * 0.01f * 0.5f;
            Vector2 val3 = val - val2;
            Vector2 val4 = val + val2;
            if (vector.x >= val3.x && vector.x <= val4.x && vector.y >= val3.y)
            {
                return vector.y <= val4.y;
            }
            return false;
        }

        private static GameObject HierarchyIdToGameObject(int instanceId)
        {
            Object obj = EditorUtility.InstanceIDToObject(instanceId);
            return (GameObject)(object)((obj is GameObject) ? obj : null);
        }

        private static int GetObjectInstanceId(object value)
        {
            return ((Object)value).GetInstanceID();
        }

        private void OnHierarchyWindowItemGUI(int value, Rect rect)
        {
            if ((Object)(object)_owner == (Object)null)
            {
                if (_attached)
                {
                    Detach();
                }
                return;
            }
            if (Event.current == null)
            {
                return;
            }
            GameObject val = HierarchyIdToGameObject(value);
            PsdLayerNode layerNode = default(PsdLayerNode);
            if ((Object)(object)val == (Object)null || (Object)(object)val == (Object)(object)gameObject || !val.TryGetComponent<PsdLayerNode>(out layerNode))
            {
                return;
            }
            Rect val2 = rect;
            val2.x = 35f;
            val2.width = 10f;
            Undo.RecordObject((Object)(object)layerNode, "Change Export Image");
            EditorGUI.BeginChangeCheck();
            layerNode.markToExport = EditorGUI.Toggle(val2, layerNode.markToExport);
            if (EditorGUI.EndChangeCheck())
            {
                if (Selection.gameObjects.Length > 1)
                {
                    SetExportImageTg(Selection.gameObjects, layerNode.markToExport);
                }
                EditorUtility.SetDirty((Object)(object)layerNode);
            }
            val2.width = Mathf.Clamp(rect.xMax * 0.2f, 100f, 200f);
            val2.x = rect.xMax - val2.width;
            if (EditorGUI.DropdownButton(val2, new GUIContent(layerNode.UIType.ToString()), (FocusType)2))
            {
                BuildUITypeMenu(layerNode, delegate(GUIType selectUIType)
                {
                    GameObject[] gameObjects = Selection.gameObjects;
                    if (gameObjects.Length > 1)
                    {
                        bool flag = false;
                        GameObject[] array = gameObjects;
                        foreach (GameObject obj in array)
                        {
                            PsdLayerNode psdLayerNode2 = ((obj == null) ? null : obj.GetComponent<PsdLayerNode>());
                            if ((Object)(object)psdLayerNode2 != (Object)null)
                            {
                                psdLayerNode2.SetUIType(selectUIType, false);
                                EditorUtility.SetDirty((Object)(object)psdLayerNode2);
                                flag = true;
                            }
                        }
                        if (flag)
                        {
                            RefreshAllHelperComponents();
                        }
                    }
                    else
                    {
                        layerNode.SetUIType(selectUIType);
                    }
                }).ShowAsContext();
            }
            // 引用按钮（"ref xxx"）的宽度；没有引用时按 0 计算 —— 九宫格标识要贴在它左侧。
            GUIContent referenceContent = new GUIContent(((Object)layerNode).name);
            bool hasReference = layerNode.HasAssetReference() || layerNode.HasPrefabReference();
            float num = hasReference ? Mathf.Min(GUI.skin.button.CalcSize(referenceContent).x + 8f, 100f) : 0f;

            DrawNineSliceToggle(layerNode, rect, val2.x - num - 6f);

            if (!hasReference)
            {
                return;
            }

            if (!GUI.Button(new Rect(rect.xMax - val2.width - num, rect.y, num, rect.height), referenceContent))
            {
                return;
            }
            if (!layerNode.HasAssetReference() || !layerNode.TryResolveReferencedNode(out var psdLayerNode))
            {
                if (!layerNode.HasAssetReference() || !layerNode.TryResolveReferencedAsset(out var activeObject))
                {
                    if (layerNode.HasPrefabReference() && layerNode.TryResolveReferencedPrefab(out var activeObject2))
                    {
                        Selection.activeObject = (Object)(object)activeObject2;
                    }
                }
                else
                {
                    Selection.activeObject = activeObject;
                }
            }
            else
            {
                Selection.activeGameObject = ((Component)psdLayerNode).gameObject;
            }
        }

        private GenericMenu BuildUITypeMenu(PsdLayerNode layerNode, Action<GUIType> callback)
        {
            Array values = Enum.GetValues(typeof(GUIType));
            GenericMenu val = new GenericMenu();
            foreach (GUIType menuUiType in values)
            {
                string text = (UGUIParser.IsPrimaryUIType(menuUiType) ? menuUiType.ToString() : menuUiType.ToString().Replace('_', '/'));
                val.AddItem(new GUIContent(text), menuUiType.Equals(layerNode.UIType), (GenericMenu.MenuFunction)delegate
                {
                    callback(menuUiType);
                });
            }
            return val;
        }

        // ——————————————————————————————————————————————————————————————
        // 九宫格标识（Hierarchy 行内）
        //
        // UIType 为 Image / Background 的节点，会在 UIType 下拉框左侧多出一颗九宫格开关：
        //   暗色 = 未启用手动九宫格；亮色 = 已启用。
        //   左键 → 打开九宫格设置窗口（与 PSDLayoutTool2 的九宫格窗口一致）；
        //   Ctrl/Shift + 左键 → 直接开关（开启时自动推断一次边距）；
        //   右键 → 推断 / 启用 / 禁用 / 清除。
        // 状态本体存在 PsdLayerNode 上，随 Prefab 序列化。
        // ——————————————————————————————————————————————————————————————

        private static GUIStyle s_NineSliceIndicatorStyle;

        private void DrawNineSliceToggle(PsdLayerNode layerNode, Rect rowRect, float rightEdge)
        {
            if (!Psd2UiNineSliceNodeState.IsCandidate(layerNode))
            {
                return;
            }

            // 点击区域保持在当前行内，图形另留边距，避免相邻行连成一条。
            float size = Mathf.Min(rowRect.height, 18f);
            Rect indicatorRect = new Rect(
                rightEdge - size,
                rowRect.y + ((rowRect.height - size) * 0.5f),
                size,
                size);

            // 面板太窄时不再挤占名字区域
            if (indicatorRect.x < 44f)
            {
                return;
            }

            layerNode = Psd2UiNineSliceNodeState.ResolveSourceNode(layerNode);
            bool enabled = layerNode.NineSliceEnabled;
            DrawNineSliceGlyph(indicatorRect, enabled);

            if (s_NineSliceIndicatorStyle == null)
            {
                s_NineSliceIndicatorStyle = new GUIStyle(GUIStyle.none)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 9,
                    fontStyle = FontStyle.Bold,
                    clipping = TextClipping.Clip
                };
            }

            GUIContent content = new GUIContent(string.Empty, BuildNineSliceTooltip(layerNode, enabled));

            Event current = Event.current;
            // 右键：GUI.Button 只吃左键，右键菜单得自己判事件（ContextClick 或 MouseUp+右键）
            bool isContextClick = current != null && indicatorRect.Contains(current.mousePosition) &&
                (current.type == EventType.ContextClick || (current.type == EventType.MouseUp && current.button == 1));
            if (isContextClick)
            {
                BuildNineSliceMenu(layerNode).ShowAsContext();
                current.Use();
                return;
            }

            if (!GUI.Button(indicatorRect, content, s_NineSliceIndicatorStyle))
            {
                return;
            }

            if (current != null && (current.control || current.shift || current.command))
            {
                Psd2UiNineSliceNodeState.Toggle(layerNode);
            }
            else
            {
                Psd2UiNineSliceWindow.Open(layerNode);
            }

            if (current != null)
            {
                current.Use();
            }
        }

        private static string BuildNineSliceTooltip(PsdLayerNode layerNode, bool enabled)
        {
            string header = enabled
                ? "九宫格：已启用（边距 左" + layerNode.nineSliceLeft + " 上" + layerNode.nineSliceTop +
                  " 右" + layerNode.nineSliceRight + " 下" + layerNode.nineSliceBottom + "）"
                : "九宫格：未启用";
            return header + "\n左键：打开九宫格设置　Ctrl/Shift+左键：直接开关　右键：更多操作";
        }

        /// <summary>无底板的细线九宫格，按物理像素对齐。</summary>
        private static void DrawNineSliceGlyph(Rect rect, bool enabled)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            bool dark = EditorGUIUtility.isProSkin;
            Color glyph = enabled
                ? (dark ? new Color(0.42f, 0.78f, 0.57f) : new Color(0.16f, 0.48f, 0.28f))
                : (dark ? new Color(0.56f, 0.56f, 0.56f) : new Color(0.43f, 0.43f, 0.43f));
            float pixels = EditorGUIUtility.pixelsPerPoint;
            float thickness = 1f / pixels;
            float size = Mathf.Min(12f, rect.height - 4f);
            float cell = Mathf.Max(1f, Mathf.Floor((size * pixels - 1f) / 3f)) / pixels;
            float extent = cell * 3f + thickness;
            float left = Mathf.Round((rect.center.x - extent * 0.5f) * pixels) / pixels;
            float top = Mathf.Round((rect.center.y - extent * 0.5f) * pixels) / pixels;

            for (int i = 0; i <= 3; i++)
            {
                EditorGUI.DrawRect(new Rect(left + i * cell, top, thickness, extent), glyph);
                EditorGUI.DrawRect(new Rect(left, top + i * cell, extent, thickness), glyph);
            }
        }

        private static GenericMenu BuildNineSliceMenu(PsdLayerNode layerNode)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("打开九宫格设置…"), false, delegate { Psd2UiNineSliceWindow.Open(layerNode); });
            menu.AddSeparator(string.Empty);

            if (layerNode.NineSliceEnabled)
            {
                menu.AddItem(new GUIContent("关闭九宫格"), true, delegate
                {
                    Psd2UiNineSliceNodeState.SetEnabled(layerNode, false, "关闭九宫格");
                });
            }
            else
            {
                menu.AddItem(new GUIContent("启用九宫格（自动推断边距）"), false, delegate
                {
                    EnableNineSliceWithInference(layerNode);
                });
                // GenericMenu 没有 (content, on, disabled, func) 这个重载，想置灰只能用 AddDisabledItem
                if (layerNode.HasNineSliceBorder)
                {
                    menu.AddItem(new GUIContent("启用九宫格（沿用已记录边距）"), false, delegate
                    {
                        Psd2UiNineSliceNodeState.SetEnabled(layerNode, true, "启用九宫格");
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("启用九宫格（沿用已记录边距）"));
                }
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("按当前图像重新推断边距"), false, delegate
            {
                AutoInferNineSliceBorder(layerNode);
            });
            menu.AddItem(new GUIContent("清除该节点的九宫格数据"), false, delegate
            {
                Psd2UiNineSliceNodeState.Clear(layerNode);
            });
            return menu;
        }

        private static void EnableNineSliceWithInference(PsdLayerNode layerNode)
        {
            string method;
            string error;
            if (Psd2UiNineSliceNodeState.TryAutoFillBorder(layerNode, out method, out error))
            {
                Debug.Log("[Psd2UIForm] 九宫格已启用，边距自动推断（" + method + "）。");
            }
            else
            {
                Debug.LogWarning("[Psd2UIForm] 九宫格自动推断失败，先用零边距启用：" + error);
            }

            Psd2UiNineSliceNodeState.SetEnabled(layerNode, true, "启用九宫格");
        }

        private static void AutoInferNineSliceBorder(PsdLayerNode layerNode)
        {
            string method;
            string error;
            if (Psd2UiNineSliceNodeState.TryAutoFillBorder(layerNode, out method, out error))
            {
                Debug.Log("[Psd2UIForm] 九宫格边距重新推断完成（" + method + "）：左" + layerNode.nineSliceLeft +
                    " 上" + layerNode.nineSliceTop + " 右" + layerNode.nineSliceRight + " 下" + layerNode.nineSliceBottom);
                return;
            }

            Debug.LogWarning("[Psd2UIForm] 九宫格边距推断失败：" + error);
        }

        private void SetExportImageTg(GameObject[] gameObjects, bool enabled)
        {
            GameObject[] array = gameObjects.Where((GameObject item) => (Object)(object)((item != null) ? item.GetComponent<PsdLayerNode>() : null) != (Object)null).ToArray();
            for (int num = 0; num < array.Length; num++)
            {
                array[num].GetComponent<PsdLayerNode>().markToExport = enabled;
            }
        }

        internal void Detach()
        {
            if (_attached)
            {
                _attached = false;
                SceneView.duringSceneGui -= OnSceneGUI;
                EditorApplication.hierarchyWindowItemOnGUI = (EditorApplication.HierarchyWindowItemCallback)Delegate.Remove((Delegate)(object)EditorApplication.hierarchyWindowItemOnGUI, (Delegate)new EditorApplication.HierarchyWindowItemCallback(OnHierarchyWindowItemGUI));
            }
            // 注意：这里刻意**不**释放 _psdDocument。
            // 生成物里的 PsdLayerNode 一直持有该文档的 PsdLayer（预览渲染依赖它），
            // 而 Detach 会在 PrefabStage 关闭时发生，
            // 此时释放文档会让仍然存在的 PsdLayerNode 在首次渲染时 NRE。
            // （Inspector 失去选中已不再 Detach，否则 Hierarchy 勾选框/UIType 下拉会消失。）
            // 真正释放走 Dispose()（壳销毁时）。
        }

        /// <summary>壳被销毁时的彻底清理：先摘除回调，再释放 PSD 文档与缓存。</summary>
        internal void Dispose()
        {
            Detach();
            if (_psdDocument != null)
            {
                _psdDocument.Dispose();
                _psdDocument = null;
            }
            _nodeByReferenceKey.Clear();
            _exportedPathByNodeKey.Clear();
            _prefabAssetByReferenceKey.Clear();
            _referencedAssetKeys.Clear();
            _prefabExportsInProgress.Clear();
            s_byOwnerInstanceId.Remove(_owner.GetInstanceID());
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        private void LoadDocumentAndRebindNodes()
        {
            if (_psdDocument == null)
            {
                if (!File.Exists(GetSourcePsdAssetPath()))
                {
                    Debug.LogError((object)("刷新节点绑定图层失败! 源文档不存在:" + GetSourcePsdAssetPath()));
                    return;
                }
                try
                {
                    EditorUtility.DisplayProgressBar("文件加载中", $"正在读取源文档:{GetSourcePsdAssetPath()}\n文件过大会影响读取速度,建议通过栅格化图层减小文档大小", 0.5f);
                    _psdDocument = PsdDocument.Create(GetSourcePsdAssetPath());
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    return;
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
            PsdLayerNode[] componentsInChildren = GetComponentsInChildren<PsdLayerNode>(true);
            for (int i = 0; i < componentsInChildren.Length; i++)
            {
                componentsInChildren[i].RebindPsdDocument(_psdDocument);
            }
            RefreshPreviewSprite(_psdDocument);
            SyncPreviewSpriteRenderer();
        }

        [MenuItem("Assets/Psd2UIForm Editor", priority = 0)]
        private static void Psd2UIFormPrefabMenu()
        {
            if (Selection.activeObject == (Object)null)
            {
                return;
            }
            string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (IsPsdOrPsbPath(assetPath))
            {
                string text = BuildEditorPrefabPath(assetPath);
                if (File.Exists(text))
                {
                    OpenEditorPrefab(text);
                }
                else if (CreateOrUpdateEditorPrefabFromPsd(assetPath))
                {
                    OpenEditorPrefab(text);
                }
            }
            else
            {
                Debug.LogWarning((object)("选择的文件(" + assetPath + ")不是PSD/PSB格式, 工具只支持PSD/PSB转换为UIForm"));
            }
        }

        [MenuItem("Assets/Psd2UIForm Editor", true)]
        private static bool ValidatePsd2UIFormPrefabMenu()
        {
            if (Selection.activeObject != (Object)null && IsPsdOrPsbPath(AssetDatabase.GetAssetPath(Selection.activeObject)))
            {
                return true;
            }
            return false;
        }

        internal bool HasSourceAssetChanged()
        {
            string text = GetSourcePsdAssetPath();
            if (!string.IsNullOrWhiteSpace(text))
            {
                string strB = GetFileTimestampUtc(text);
                return psdAssetChangeTime.CompareTo(strB) != 0;
            }
            return false;
        }

        private static string GetFileTimestampUtc(object value)
        {
            return new FileInfo((string)value).LastWriteTimeUtc.ToString("yyyyMMddHHmmss");
        }

        internal static void OpenEditorPrefab(object value)
        {
            OpenPrefabStage(value);
        }

        private static void OpenPrefabStage(object value)
        {
            GUID val = AssetDatabase.GUIDFromAssetPath((string)value);
            PrefabStageUtility.OpenPrefab((string)((!val.Empty()) ? AssetDatabase.GUIDToAssetPath(val) : value));
        }

        internal static bool CreateOrUpdateEditorPrefabFromPsd(object text3, Psd2UIFormConverterEditor value2 = null, bool enabled = false, bool enabled2 = true)
        {
            text3 = NormalizeAssetPath(text3);
            if (!string.IsNullOrWhiteSpace((string)text3) && File.Exists((string)text3))
            {
                AssetImporter atPath = AssetImporter.GetAtPath((string)text3);
                TextureImporter val = (TextureImporter)(object)((atPath is TextureImporter) ? atPath : null);
                if ((Object)(object)val != (Object)null)
                {
                    if ((int)val.textureType == 8 && (int)val.spriteImportMode == 1)
                    {
                        AssetDatabase.ImportAsset((string)text3, (ImportAssetOptions)8);
                    }
                    else
                    {
                        val.textureType = (TextureImporterType)8;
                        val.spriteImportMode = (SpriteImportMode)1;
                        val.mipmapEnabled = false;
                        val.alphaIsTransparency = true;
                        ((AssetImporter)val).SaveAndReimport();
                    }
                }
                string text = BuildEditorPrefabPath(text3);
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);
                bool flag = value2 == null;
                if (value2 == null)
                {
                    Psd2UIFormConverterEditor value = Psd2UIFormConverterEditor.GetOrCreate(CreateConverterRoot(fileNameWithoutExtension));
                    if (value != null)
                    {
                        value.psdAssetChangeTime = GetFileTimestampUtc(text3);
                        value.SetSourcePsdAsset((string)text3);
                        if (RebuildLayerNodesFromPsd(text3, value, enabled))
                        {
                            value.gameObject.name = Path.GetFileNameWithoutExtension(text);
                            bool flag2 = default(bool);
                            PrefabUtility.SaveAsPrefabAsset(value.gameObject, text, out flag2);
                            if (flag)
                            {
                                Object.DestroyImmediate((Object)(object)value.gameObject);
                            }
                            AssetDatabase.Refresh();
                            Stage currentStage = StageUtility.GetCurrentStage();
                            string text2 = (((object)currentStage == null) ? null : currentStage.assetPath);
                            bool flag3 = !string.IsNullOrWhiteSpace(text2) && AssetDatabase.GUIDFromAssetPath(text2) == AssetDatabase.GUIDFromAssetPath(text);
                            if (flag2 && enabled2 && !flag3)
                            {
                                OpenPrefabStage(text);
                            }
                            return flag2;
                        }
                        if (flag)
                        {
                            Object.DestroyImmediate((Object)(object)value.gameObject);
                        }
                        return false;
                    }
                    Debug.LogError((object)"解析PSD失败: 无法创建根节点组件 Psd2UIFormConverter");
                    return false;
                }
                return RebuildLayerNodesFromPsd(text3, value2, enabled);
            }
            Debug.LogError((object)("Error: 源文档不存在:" + (string)text3));
            return false;
        }

        private static bool RebuildLayerNodesFromPsd(object value, Psd2UIFormConverterEditor value2, bool enabled = false)
        {
            EditorUtility.DisplayProgressBar("解析PSD", "正在解析" + (string)value, 0f);
            Dictionary<string, NodeTypeSnapshot> dictionary = (enabled ? value2.CaptureNodeTypeSnapshots() : null);
            try
            {
                using (PsdDocument psdDocument = PsdDocument.Create((string)value))
                {
                    PsdLayer[] array = psdDocument.Childs ?? Array.Empty<PsdLayer>();
                    if (array.Length == 0)
                    {
                        Debug.LogError((object)("解析PSD失败: PSD未包含可解析的图层树。文件: " + (string)value));
                        return false;
                    }
                    for (int num = value2.transform.childCount - 1; num >= 0; num--)
                    {
                        Object.DestroyImmediate((Object)(object)value2.transform.GetChild(num).gameObject, true);
                    }
                    int num2 = psdDocument.GetLayerCount();
                    int num3 = 0;
                    int num4 = 0;
                    foreach (PsdLayer psdLayer in array)
                    {
                        if (psdLayer != null)
                        {
                            BuildLayerNodeTreeRecursive(psdLayer, value2.transform, ref num4, ref num3, num2);
                        }
                    }
                    value2.RefreshPreviewSprite(psdDocument, (string)value);
                }
                value2.psdAssetChangeTime = GetFileTimestampUtc(value);
                if (value2._psdDocument != null)
                {
                    value2._psdDocument.Dispose();
                    value2._psdDocument = null;
                }
                value2.LoadDocumentAndRebindNodes();
                if (enabled)
                {
                    value2.RestoreNodeTypeSnapshots(dictionary);
                }
                value2.NormalizeGroupGenerationState();
                PsdLayerNode[] componentsInChildren = value2.GetComponentsInChildren<PsdLayerNode>(true);
                for (int j = 0; j < componentsInChildren.Length; j++)
                {
                    componentsInChildren[j].SynchronizeHelperComponent();
                }
                EditorUtility.SetDirty((Object)(object)value2.gameObject);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void SetSourcePsdAsset(string text)
        {
            psdAssetPath = NormalizeAssetPath(text);
            psdAsset = LoadSpriteAtAssetPath(psdAssetPath);
            previewSprite = (((Object)(object)psdAsset != (Object)null || !IsPsbPath(psdAssetPath)) ? null : LoadGeneratedPreviewSprite(psdAssetPath));
            if (string.IsNullOrWhiteSpace(ScriptableSingleton<Psd2UIFormSettings>.Instance.UIImagesOutputDir))
            {
                ScriptableSingleton<Psd2UIFormSettings>.Instance.UIImagesOutputDir = Path.GetDirectoryName(psdAssetPath);
            }
            if (string.IsNullOrWhiteSpace(uiFormName))
            {
                uiFormName = (((Object)(object)psdAsset != (Object)null) ? ((Object)psdAsset).name : Path.GetFileNameWithoutExtension(psdAssetPath));
            }
        }

        private static string BuildEditorPrefabPath(object value)
        {
            return Path.Combine(Path.GetDirectoryName((string)value), Path.GetFileNameWithoutExtension((string)value) + "_UIFormEditor.prefab");
        }

        private static string NormalizeAssetPath(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                string text = ((string)value).Replace("\\", "/").Trim();
                if (Path.IsPathRooted(text))
                {
                    text = PathCompatibilityUtility.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, text).Replace("\\", "/");
                }
                return text;
            }
            return null;
        }

        private static bool IsPsdOrPsbPath(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return false;
            }
            string extension = Path.GetExtension((string)value);
            if (!extension.Equals(".psd", StringComparison.OrdinalIgnoreCase))
            {
                return extension.Equals(".psb", StringComparison.OrdinalIgnoreCase);
            }
            return true;
        }

        private static bool IsPsbPath(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                return Path.GetExtension((string)value).Equals(".psb", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private static Sprite LoadSpriteAtAssetPath(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return null;
            }
            Sprite val = AssetDatabase.LoadAssetAtPath<Sprite>((string)value);
            if (!((Object)(object)val != (Object)null))
            {
                Object[] array = AssetDatabase.LoadAllAssetsAtPath((string)value);
                if (array != null)
                {
                    Object[] array2 = array;
                    int num = 0;
                    Sprite val2;
                    while (true)
                    {
                        if (num >= array2.Length)
                        {
                            return null;
                        }
                        Object obj = array2[num];
                        val2 = (Sprite)(object)((obj is Sprite) ? obj : null);
                        if ((object)val2 != null)
                        {
                            break;
                        }
                        num++;
                    }
                    return val2;
                }
                return null;
            }
            return val;
        }

        private static string BuildPreviewAssetPath(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return null;
            }
            string directoryName = Path.GetDirectoryName((string)value);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension((string)value);
            if (!string.IsNullOrWhiteSpace(directoryName) && !string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                return Path.Combine(directoryName, fileNameWithoutExtension + "_Preview.png").Replace("\\", "/");
            }
            return null;
        }

        private static string BuildPreviewImporterTag(object value, object value2)
        {
            return "Psd2UIFormPreview:v2:" + (string)value2 + ":" + (string)value;
        }

        private static string AssetPathToAbsolutePath(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return null;
            }
            string text = NormalizeAssetPath(value);
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (Path.IsPathRooted(text))
                {
                    return text;
                }
                return Path.Combine(Directory.GetParent(Application.dataPath).FullName, text.Replace("/", "\\"));
            }
            return null;
        }

        private static Sprite LoadGeneratedPreviewSprite(object value)
        {
            return PsdLayerNode.LoadSpriteAtPath(BuildPreviewAssetPath(value));
        }

        private static bool IsGeneratedPreviewCurrent(object value, object value2, object value3)
        {
            AssetImporter atPath = AssetImporter.GetAtPath((string)value);
            TextureImporter val = (TextureImporter)(object)((atPath is TextureImporter) ? atPath : null);
            if (!((Object)(object)val == (Object)null))
            {
                return string.Equals(((AssetImporter)val).userData, BuildPreviewImporterTag(value2, value3), StringComparison.Ordinal);
            }
            return false;
        }

        private static byte[] RenderDocumentPreviewPng(object value)
        {
            int num;
            int num2;
            int num3;
            int num4;
            return RenderDocumentPreviewPng(value, out num, out num2, out num3, out num4);
        }

        internal static byte[] RenderPreviewPngFromAssetPath(object path, out int result, out int result2, out int result3, out int result4)
        {
            result = 0;
            result2 = 0;
            result3 = 0;
            result4 = 0;
            path = NormalizeAssetPath(path);
            if (string.IsNullOrWhiteSpace((string)path))
            {
                return null;
            }
            using PsdDocument psdDocument = PsdDocument.Create((string)path);
            return RenderDocumentPreviewPng(psdDocument, out result, out result2, out result3, out result4);
        }

        internal static byte[] RenderDocumentPreviewPng(object value, out int result, out int result2, out int result3, out int result4)
        {
            result = 0;
            result2 = 0;
            result3 = 0;
            result4 = 0;
            if (value == null)
            {
                return null;
            }
            List<RenderedLayerEntry> list = CollectVisibleRenderedLayers(value);
            CalculatePreviewCompositeBounds(value, list, out result, out result2, out result3, out result4);
            bool flag = false;
            for (int i = 0; i < list.Count; i++)
            {
                PsdRenderedImage psdRenderedImage = ((list[i] != null) ? list[i].RenderedImage : null);
                if (psdRenderedImage != null && psdRenderedImage.IsHighBitDepth)
                {
                    flag = true;
                    break;
                }
            }
            Texture2D val = null;
            try
            {
                val = CompositeRenderedLayers(list, result, result2, result3, result4, flag);
                return PsdTextureAssetUtility.EncodePng(val);
            }
            finally
            {
                if ((Object)(object)val != (Object)null)
                {
                    Object.DestroyImmediate((Object)(object)val);
                }
            }
        }

        private static List<RenderedLayerEntry> CollectVisibleRenderedLayers(object value)
        {
            List<RenderedLayerEntry> list = new List<RenderedLayerEntry>();
            PsdLayer[] childs = ((PsdDocument)value).Childs;
            if (childs != null && childs.Length != 0)
            {
                foreach (PsdLayer psdLayer in childs)
                {
                    if (psdLayer != null && psdLayer.IsVisible)
                    {
                        PsdRenderedImage psdRenderedImage = psdLayer.Render();
                        if (psdRenderedImage != null && !psdRenderedImage.IsEmpty)
                        {
                            list.Add(new RenderedLayerEntry
                            {
                                Layer = psdLayer,
                                RenderedImage = psdRenderedImage
                            });
                        }
                    }
                }
                return list;
            }
            return list;
        }

        private static void CalculatePreviewCompositeBounds(object value, List<RenderedLayerEntry> values, out int result, out int result2, out int result3, out int result4)
        {
            int num = 0;
            int num2 = 0;
            int num3 = ((PsdDocument)value)?.Width ?? 0;
            int num4 = ((PsdDocument)value)?.Height ?? 0;
            if (values != null)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    PsdRenderedImage psdRenderedImage = values[i]?.RenderedImage;
                    if (psdRenderedImage != null && !psdRenderedImage.IsEmpty)
                    {
                        if (psdRenderedImage.Left < num)
                        {
                            num = psdRenderedImage.Left;
                        }
                        if (psdRenderedImage.Top < num2)
                        {
                            num2 = psdRenderedImage.Top;
                        }
                        if (psdRenderedImage.Right > num3)
                        {
                            num3 = psdRenderedImage.Right;
                        }
                        if (psdRenderedImage.Bottom > num4)
                        {
                            num4 = psdRenderedImage.Bottom;
                        }
                    }
                }
            }
            int num5 = Math.Max(0, ((PsdDocument)value)?.Width ?? 0);
            int num6 = Math.Max(0, ((PsdDocument)value)?.Height ?? 0);
            int num7 = Math.Max(Math.Max(0, -num), Math.Max(0, num3 - num5));
            int num8 = Math.Max(Math.Max(0, -num2), Math.Max(0, num4 - num6));
            result = -num7;
            result2 = -num8;
            result3 = num5 + (num7 << 1);
            result4 = num6 + (num8 << 1);
        }

        private static Texture2D CompositeRenderedLayers(List<RenderedLayerEntry> values, int value, int value2, int value3, int value4, bool enabled)
        {
            value3 = Math.Max(1, value3);
            value4 = Math.Max(1, value4);
            if (enabled)
            {
                ushort[] array = new ushort[value3 * value4 * 4];
                ushort[] array2 = null;
                if (values != null)
                {
                    for (int i = 0; i < values.Count; i++)
                    {
                        RenderedLayerEntry value5 = values[i];
                        PsdRenderedImage psdRenderedImage = value5?.RenderedImage;
                        if (psdRenderedImage != null && !psdRenderedImage.IsEmpty)
                        {
                            bool flag = value5.Layer != null && value5.Layer.IsClipping;
                            CompositeRenderedLayer16Bit(array, value, value2, value3, value4, psdRenderedImage, flag ? array2 : null);
                            if (!flag)
                            {
                                array2 = BuildLayerAlphaMask16Bit(psdRenderedImage, value, value2, value3, value4);
                            }
                        }
                    }
                }
                byte[] array3 = new byte[array.Length * 2];
                Buffer.BlockCopy(array, 0, array3, 0, array3.Length);
                Texture2D val = new Texture2D(value3, value4, (TextureFormat)74, false, false);
                val.LoadRawTextureData(array3);
                val.Apply(false, false);
                return val;
            }
            byte[] array4 = new byte[value3 * value4 * 4];
            byte[] array5 = null;
            if (values != null)
            {
                for (int j = 0; j < values.Count; j++)
                {
                    RenderedLayerEntry value6 = values[j];
                    PsdRenderedImage psdRenderedImage2 = value6?.RenderedImage;
                    if (psdRenderedImage2 != null && !psdRenderedImage2.IsEmpty)
                    {
                        bool flag2 = value6.Layer != null && value6.Layer.IsClipping;
                        CompositeRenderedLayer8Bit(array4, value, value2, value3, value4, psdRenderedImage2, flag2 ? array5 : null);
                        if (!flag2)
                        {
                            array5 = BuildLayerAlphaMask8Bit(psdRenderedImage2, value, value2, value3, value4);
                        }
                    }
                }
            }
            Texture2D val2 = new Texture2D(value3, value4, (TextureFormat)4, false, false);
            val2.LoadRawTextureData(array4);
            val2.Apply(false, false);
            return val2;
        }

        private static void CompositeRenderedLayer8Bit(object value, int value2, int value3, int value4, int value5, object value6, object value7)
        {
            if (value == null || value6 == null || ((PsdRenderedImage)value6).IsEmpty)
            {
                return;
            }
            byte[] rgba = ((PsdRenderedImage)value6).Rgba32;
            int num = ((PsdRenderedImage)value6).Left - value2;
            int num2 = ((PsdRenderedImage)value6).Top - value3;
            for (int i = 0; i < ((PsdRenderedImage)value6).Height; i++)
            {
                int num3 = num2 + i;
                if (num3 < 0 || num3 >= value5)
                {
                    continue;
                }
                for (int j = 0; j < ((PsdRenderedImage)value6).Width; j++)
                {
                    int num4 = num + j;
                    if (num4 < 0 || num4 >= value4)
                    {
                        continue;
                    }
                    int num5 = GetFlippedRgbaIndex(((PsdRenderedImage)value6).Width, ((PsdRenderedImage)value6).Height, j, i);
                    byte b = rgba[num5 + 3];
                    if (b <= 0)
                    {
                        continue;
                    }
                    if (value7 != null)
                    {
                        b = PsdLayerRenderer.MultiplyAlpha(b, ((byte[])value7)[num3 * value4 + num4]);
                        if (b <= 0)
                        {
                            continue;
                        }
                    }
                    CompositePixel8Bit(value, value4, value5, num4, num3, rgba[num5], rgba[num5 + 1], rgba[num5 + 2], b);
                }
            }
        }

        private static void CompositeRenderedLayer16Bit(object value, int value2, int value3, int value4, int value5, object value6, object value7)
        {
            if (value == null || value6 == null || ((PsdRenderedImage)value6).IsEmpty)
            {
                return;
            }
            ushort[] rgba = ((PsdRenderedImage)value6).Rgba64;
            int num = ((PsdRenderedImage)value6).Left - value2;
            int num2 = ((PsdRenderedImage)value6).Top - value3;
            for (int i = 0; i < ((PsdRenderedImage)value6).Height; i++)
            {
                int num3 = num2 + i;
                if (num3 < 0 || num3 >= value5)
                {
                    continue;
                }
                for (int j = 0; j < ((PsdRenderedImage)value6).Width; j++)
                {
                    int num4 = num + j;
                    if (num4 < 0 || num4 >= value4)
                    {
                        continue;
                    }
                    int num5 = GetFlippedRgbaIndex(((PsdRenderedImage)value6).Width, ((PsdRenderedImage)value6).Height, j, i);
                    ushort num6 = rgba[num5 + 3];
                    if (num6 <= 0)
                    {
                        continue;
                    }
                    if (value7 != null)
                    {
                        num6 = PsdLayerRenderer.MultiplyAlpha(num6, ((ushort[])value7)[num3 * value4 + num4]);
                        if (num6 <= 0)
                        {
                            continue;
                        }
                    }
                    CompositePixel16Bit(value, value4, value5, num4, num3, rgba[num5], rgba[num5 + 1], rgba[num5 + 2], num6);
                }
            }
        }

        private static byte[] BuildLayerAlphaMask8Bit(object value, int value2, int value3, int value4, int value5)
        {
            byte[] array = new byte[value4 * value5];
            if (value != null && !((PsdRenderedImage)value).IsEmpty)
            {
                byte[] rgba = ((PsdRenderedImage)value).Rgba32;
                int num = ((PsdRenderedImage)value).Left - value2;
                int num2 = ((PsdRenderedImage)value).Top - value3;
                for (int i = 0; i < ((PsdRenderedImage)value).Height; i++)
                {
                    int num3 = num2 + i;
                    if (num3 < 0 || num3 >= value5)
                    {
                        continue;
                    }
                    for (int j = 0; j < ((PsdRenderedImage)value).Width; j++)
                    {
                        int num4 = num + j;
                        if (num4 >= 0 && num4 < value4)
                        {
                            int num5 = GetFlippedRgbaIndex(((PsdRenderedImage)value).Width, ((PsdRenderedImage)value).Height, j, i);
                            array[num3 * value4 + num4] = rgba[num5 + 3];
                        }
                    }
                }
                return array;
            }
            return array;
        }

        private static ushort[] BuildLayerAlphaMask16Bit(object value, int value2, int value3, int value4, int value5)
        {
            ushort[] array = new ushort[value4 * value5];
            if (value != null && !((PsdRenderedImage)value).IsEmpty)
            {
                ushort[] rgba = ((PsdRenderedImage)value).Rgba64;
                int num = ((PsdRenderedImage)value).Left - value2;
                int num2 = ((PsdRenderedImage)value).Top - value3;
                for (int i = 0; i < ((PsdRenderedImage)value).Height; i++)
                {
                    int num3 = num2 + i;
                    if (num3 < 0 || num3 >= value5)
                    {
                        continue;
                    }
                    for (int j = 0; j < ((PsdRenderedImage)value).Width; j++)
                    {
                        int num4 = num + j;
                        if (num4 >= 0 && num4 < value4)
                        {
                            int num5 = GetFlippedRgbaIndex(((PsdRenderedImage)value).Width, ((PsdRenderedImage)value).Height, j, i);
                            array[num3 * value4 + num4] = rgba[num5 + 3];
                        }
                    }
                }
                return array;
            }
            return array;
        }

        private static void CompositePixel8Bit(object value, int value2, int value3, int value4, int value5, byte value6, byte value7, byte value8, byte value9)
        {
            int num = GetFlippedRgbaIndex(value2, value3, value4, value5);
            byte b = ((byte[])value)[num];
            byte b2 = ((byte[])value)[num + 1];
            byte b3 = ((byte[])value)[num + 2];
            byte num2 = ((byte[])value)[num + 3];
            float num3 = (float)(int)value9 / 255f;
            float num4 = (float)(int)num2 / 255f;
            float num5 = num3 + num4 * (1f - num3);
            if (num5 <= 0f)
            {
                ((sbyte[])value)[num] = 0;
                ((sbyte[])value)[num + 1] = 0;
                ((sbyte[])value)[num + 2] = 0;
                ((sbyte[])value)[num + 3] = 0;
            }
            else
            {
                float num6 = (float)(int)value6 / 255f * num3 + (float)(int)b / 255f * num4 * (1f - num3);
                float num7 = (float)(int)value7 / 255f * num3 + (float)(int)b2 / 255f * num4 * (1f - num3);
                float num8 = (float)(int)value8 / 255f * num3 + (float)(int)b3 / 255f * num4 * (1f - num3);
                ((sbyte[])value)[num] = (sbyte)ToByteChannel(num6 / num5);
                ((sbyte[])value)[num + 1] = (sbyte)ToByteChannel(num7 / num5);
                ((sbyte[])value)[num + 2] = (sbyte)ToByteChannel(num8 / num5);
                ((sbyte[])value)[num + 3] = (sbyte)ToByteChannel(num5);
            }
        }

        private static void CompositePixel16Bit(object value, int value2, int value3, int value4, int value5, ushort value6, ushort value7, ushort value8, ushort value9)
        {
            int num = GetFlippedRgbaIndex(value2, value3, value4, value5);
            ushort num2 = ((ushort[])value)[num];
            ushort num3 = ((ushort[])value)[num + 1];
            ushort num4 = ((ushort[])value)[num + 2];
            ushort num5 = ((ushort[])value)[num + 3];
            float num6 = (float)(int)value9 / 65535f;
            float num7 = (float)(int)num5 / 65535f;
            float num8 = num6 + num7 * (1f - num6);
            if (num8 <= 0f)
            {
                ((short[])value)[num] = 0;
                ((short[])value)[num + 1] = 0;
                ((short[])value)[num + 2] = 0;
                ((short[])value)[num + 3] = 0;
            }
            else
            {
                float num9 = (float)(int)value6 / 65535f * num6 + (float)(int)num2 / 65535f * num7 * (1f - num6);
                float num10 = (float)(int)value7 / 65535f * num6 + (float)(int)num3 / 65535f * num7 * (1f - num6);
                float num11 = (float)(int)value8 / 65535f * num6 + (float)(int)num4 / 65535f * num7 * (1f - num6);
                ((short[])value)[num] = (short)ToUShortChannel(num9 / num8);
                ((short[])value)[num + 1] = (short)ToUShortChannel(num10 / num8);
                ((short[])value)[num + 2] = (short)ToUShortChannel(num11 / num8);
                ((short[])value)[num + 3] = (short)ToUShortChannel(num8);
            }
        }

        private static int GetFlippedRgbaIndex(int value, int value2, int value3, int value4)
        {
            return ((value2 - 1 - value4) * value + value3) * 4;
        }

        private static byte ToByteChannel(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        }

        private static ushort ToUShortChannel(float value)
        {
            return (ushort)Mathf.Clamp(Mathf.RoundToInt(value * 65535f), 0, 65535);
        }

        private static Sprite LoadOrGenerateDocumentPreviewSprite(object value, PsdDocument psdDocument = null)
        {
            value = NormalizeAssetPath(value);
            string text = AssetPathToAbsolutePath(value);
            if (!string.IsNullOrWhiteSpace((string)value) && !string.IsNullOrWhiteSpace(text) && File.Exists(text))
            {
                string text2 = GetFileTimestampUtc(value);
                string text3 = BuildPreviewAssetPath(value);
                if (!string.IsNullOrWhiteSpace(text3))
                {
                    string text4 = AssetPathToAbsolutePath(text3);
                    if (!string.IsNullOrWhiteSpace(text4) && File.Exists(text4) && IsGeneratedPreviewCurrent(text3, value, text2))
                    {
                        return PsdLayerNode.LoadSpriteAtPath(text3);
                    }
                    bool flag = psdDocument == null;
                    try
                    {
                        if (psdDocument == null)
                        {
                            psdDocument = PsdDocument.Create((string)value);
                        }
                        byte[] array = RenderDocumentPreviewPng(psdDocument);
                        if (array != null && array.Length != 0)
                        {
                            if (!string.IsNullOrWhiteSpace(text4))
                            {
                                File.WriteAllBytes(text4, array);
                                AssetDatabase.ImportAsset(text3, (ImportAssetOptions)9);
                                AssetImporter atPath = AssetImporter.GetAtPath(text3);
                                TextureImporter val = (TextureImporter)(object)((atPath is TextureImporter) ? atPath : null);
                                if ((Object)(object)val != (Object)null)
                                {
                                    val.textureType = (TextureImporterType)8;
                                    val.spriteImportMode = (SpriteImportMode)1;
                                    val.alphaSource = (TextureImporterAlphaSource)1;
                                    val.alphaIsTransparency = true;
                                    val.mipmapEnabled = false;
                                    val.npotScale = (TextureImporterNPOTScale)0;
                                    val.textureCompression = (TextureImporterCompression)0;
                                    val.wrapMode = (TextureWrapMode)1;
                                    val.filterMode = (FilterMode)1;
                                    ((AssetImporter)val).userData = BuildPreviewImporterTag(value, text2);
                                    ((AssetImporter)val).SaveAndReimport();
                                }
                                return PsdLayerNode.LoadSpriteAtPath(text3);
                            }
                            return null;
                        }
                        return null;
                    }
                    catch (Exception arg)
                    {
                        Debug.LogWarning((object)$"生成源文档预览图失败: {value}\n{arg}");
                        return null;
                    }
                    finally
                    {
                        if (flag)
                        {
                            psdDocument?.Dispose();
                        }
                    }
                }
                return null;
            }
            return null;
        }

        private void RefreshPreviewSprite(PsdDocument psdDocument, string text = null)
        {
            text = (string.IsNullOrWhiteSpace(text) ? GetSourcePsdAssetPath() : NormalizeAssetPath(text));
            Sprite obj = previewSprite;
            if (!((Object)(object)psdAsset != (Object)null) && IsPsbPath(text))
            {
                previewSprite = LoadOrGenerateDocumentPreviewSprite(text, psdDocument);
            }
            else
            {
                previewSprite = null;
            }
            if ((Object)(object)obj != (Object)(object)previewSprite)
            {
                EditorUtility.SetDirty((Object)(object)_owner);
            }
        }

        private void SyncPreviewSpriteRenderer()
        {
            (gameObject.GetComponent<SpriteRenderer>() ?? gameObject.AddComponent<SpriteRenderer>()).sprite = GetPreviewSprite();
        }

        private static Psd2UIFormConverter CreateConverterRoot(object value)
        {
            GameObject val = new GameObject((string)value, new Type[1] { typeof(RectTransform) });
            val.gameObject.tag = "EditorOnly";
            val.transform.localPosition = Vector3.zero;
            val.transform.localRotation = Quaternion.identity;
            val.transform.localScale = Vector3.one;
            Psd2UIFormConverter value2 = val.AddComponent<Psd2UIFormConverter>();
            if ((Object)(object)value2 == (Object)null)
            {
                Object.DestroyImmediate((Object)(object)val);
            }
            return value2;
        }

        private static void BuildLayerNodeTreeRecursive(object value, object value2, ref int value3, ref int value4, int value5)
        {
            if (value == null || (Object)value2 == (Object)null)
            {
                return;
            }
            value4++;
            EditorUtility.DisplayProgressBar($"解析PSD({value4}/{Mathf.Max(1, value5)})", "正在解析图层:" + value.GetLayerName(), (value5 > 0) ? ((float)value4 / (float)value5) : 1f);
            int num = (((PsdLayer)value).IsGroup ? (value3 + value.GetLayerRecordSpan() - 1) : value3);
            PsdLayerNode psdLayerNode = CreateLayerNode(value, num);
            Transform nodeTransform = ((Component)psdLayerNode).transform;
            Transform parentTransform = (Transform)value2;
            // new GameObject() 创建在主场景；Prefab Stage 里 SetParent 前必须先移到父节点所在场景
            if (nodeTransform.gameObject.scene != parentTransform.gameObject.scene)
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(nodeTransform.gameObject, parentTransform.gameObject.scene);
            }
            nodeTransform.SetParent(parentTransform);
            ((Component)psdLayerNode).transform.localPosition = Vector3.zero;
            if (((PsdLayer)value).Childs != null && ((PsdLayer)value).Childs.Length != 0)
            {
                int num2 = value3 + 1;
                for (int i = 0; i < ((PsdLayer)value).Childs.Length; i++)
                {
                    PsdLayer psdLayer = ((PsdLayer)value).Childs[i];
                    if (psdLayer != null)
                    {
                        BuildLayerNodeTreeRecursive(psdLayer, ((Component)psdLayerNode).transform, ref num2, ref value4, value5);
                    }
                }
            }
            value3 += value.GetLayerRecordSpan();
        }

        private static PsdLayerNode CreateLayerNode(object value, int value2)
        {
            string text = value.GetLayerName();
            text = UGUIParser.Instance.BuildLayerObjectName(text, value2);
            GameObject val = new GameObject(text, new Type[1] { typeof(RectTransform) });
            val.gameObject.tag = "EditorOnly";
            val.transform.localPosition = Vector3.zero;
            val.transform.localRotation = Quaternion.identity;
            val.transform.localScale = Vector3.one;
            PsdLayerNode psdLayerNode = val.AddComponent<PsdLayerNode>();
            psdLayerNode.BindPsdLayerIndex = value2;
            InitializeLayerNodeFromPsd(psdLayerNode, value);
            return psdLayerNode;
        }

        private static void InitializeLayerNodeFromPsd(object value, object value2)
        {
            if (value2 != null)
            {
                PsdLayerType psdLayerType = value2.GetLayerType();
                ((PsdLayerNode)value).SetSourceLayerName(value2.GetLayerName());
                ((PsdLayerNode)value).BindPsdLayer((PsdLayer)value2);
                ((PsdLayerNode)value).SetCollapseAndTextSource(false, -1);
                if (UGUIParser.Instance.TryResolveRuleForNode((PsdLayerNode)value, out var uGUIParseRule))
                {
                    ((PsdLayerNode)value).SetUIType(uGUIParseRule.UIType, false);
                }
                bool flag = psdLayerType == PsdLayerType.TextLayer && ((PsdLayerNode)value).UIType.ToString().EndsWith("Text") && ((PsdLayerNode)value).UIType != GUIType.FillColor;
                bool flag2 = psdLayerType == PsdLayerType.FillLayer || ((PsdLayerNode)value).UIType == GUIType.FillColor;
                ((PsdLayerNode)value).markToExport = psdLayerType != PsdLayerType.LayerGroup && !flag && !flag2;
                ((Component)value).gameObject.SetActive(((PsdLayer)value2).IsVisible);
                if (((PsdLayerNode)value).HasAssetReference() || ((PsdLayerNode)value).HasPrefabReference())
                {
                    ((PsdLayerNode)value).markToExport = false;
                }
            }
        }

        internal void NormalizeGroupGenerationState()
        {
            PsdLayerNode[] componentsInChildren = GetComponentsInChildren<PsdLayerNode>(true);
            if (componentsInChildren == null || componentsInChildren.Length == 0)
            {
                return;
            }
            PsdLayerNode[] array = componentsInChildren;
            foreach (PsdLayerNode psdLayerNode in array)
            {
                if ((Object)(object)psdLayerNode == (Object)null)
                {
                    continue;
                }
                psdLayerNode.SetCollapseAndTextSource(false, -1);
                if (psdLayerNode.LayerType != PsdLayerType.LayerGroup)
                {
                    continue;
                }
                if (!IsTextType(psdLayerNode.UIType) && !IsTextRoleType(psdLayerNode.UIType))
                {
                    if (!IsImageLikeType(psdLayerNode.UIType) && !IsAuxiliaryVisualType(psdLayerNode.UIType))
                    {
                        psdLayerNode.markToExport = false;
                        EditorUtility.SetDirty((Object)(object)psdLayerNode);
                    }
                    else
                    {
                        psdLayerNode.SetCollapseAndTextSource(true, -1);
                        psdLayerNode.markToExport = !psdLayerNode.HasAssetReference() && !psdLayerNode.HasPrefabReference();
                        EditorUtility.SetDirty((Object)(object)psdLayerNode);
                    }
                    continue;
                }
                PsdLayerNode psdLayerNode2 = FindTextSourceNode(psdLayerNode);
                if ((Object)(object)psdLayerNode2 != (Object)null)
                {
                    psdLayerNode.SetCollapseAndTextSource(true, psdLayerNode2.BindPsdLayerIndex);
                    psdLayerNode.markToExport = false;
                }
                else
                {
                    psdLayerNode.SetUIType(GUIType.Panel, false);
                    psdLayerNode.markToExport = false;
                }
                EditorUtility.SetDirty((Object)(object)psdLayerNode);
            }
        }

        internal void RefreshAllHelperComponents()
        {
            NormalizeGroupGenerationState();
            PsdLayerNode[] componentsInChildren = GetComponentsInChildren<PsdLayerNode>(true);
            if (componentsInChildren == null || componentsInChildren.Length == 0)
            {
                return;
            }
            foreach (PsdLayerNode psdLayerNode in componentsInChildren)
            {
                if (!((Object)(object)psdLayerNode == (Object)null))
                {
                    psdLayerNode.SynchronizeHelperComponent();
                }
            }
        }

        internal bool RunLocalNormalization(bool enabled, out string result)
        {
            result = "未执行本地归一化。";
            if (!AiLocalHierarchyNormalizer.TryNormalizeHierarchy(this, enabled, out var value))
            {
                result = ((value == null) ? "本地归一化失败。" : value.BuildSummary());
                return false;
            }
            result = ((value != null) ? value.BuildSummary() : "本地归一化完成。");
            if (value != null)
            {
                for (int i = 0; i < value.Actions.Count; i++)
                {
                    Debug.Log((object)("[PSD2UIForm.Normalize] " + value.Actions[i]));
                }
                for (int j = 0; j < value.Warnings.Count; j++)
                {
                    Debug.LogWarning((object)("[PSD2UIForm.Normalize] " + value.Warnings[j]));
                }
            }
            return true;
        }

        private static PsdLayerNode FindTextSourceNode(object value)
        {
            PsdLayerNode layerNode = (PsdLayerNode)value;
            if (!((Object)(object)layerNode == (Object)null))
            {
                PsdLayerNode[] array = (from node in ((Component)layerNode).GetComponentsInChildren<PsdLayerNode>(true)
                    where (Object)(object)node != (Object)null && (Object)(object)node != (Object)(object)layerNode && node.LayerType == PsdLayerType.TextLayer && ((Component)node).gameObject.activeSelf
                    orderby node.BindPsdLayerIndex
                    select node).ToArray();
                if (array.Length != 0)
                {
                    return array.FirstOrDefault((PsdLayerNode node) => IsTextType(node.UIType) || IsTextRoleType(node.UIType)) ?? array[0];
                }
                return null;
            }
            return null;
        }

        private static bool IsImageLikeType(GUIType uiType)
        {
            if (uiType != GUIType.Image && uiType != GUIType.RawImage)
            {
                return uiType == GUIType.Mask;
            }
            return true;
        }

        private static bool IsTextType(GUIType uiType)
        {
            if (uiType != GUIType.Text)
            {
                return uiType == GUIType.TMPText;
            }
            return true;
        }

        private static bool IsTextRoleType(GUIType uiType)
        {
            switch (uiType)
            {
            default:
                return false;
            case GUIType.Button_Text:
            case GUIType.Dropdown_Label:
            case GUIType.InputField_Placeholder:
            case GUIType.InputField_Text:
            case GUIType.Toggle_Label:
                return true;
            }
        }

        private static bool IsAuxiliaryVisualType(GUIType uiType)
        {
            switch (uiType)
            {
            default:
                return false;
            case GUIType.Background:
            case GUIType.Button_Highlight:
            case GUIType.Button_Press:
            case GUIType.Button_Select:
            case GUIType.Button_Disable:
            case GUIType.Dropdown_Arrow:
            case GUIType.Toggle_Checkmark:
            case GUIType.Slider_Fill:
            case GUIType.Slider_Handle:
            case GUIType.ScrollView_Viewport:
            case GUIType.ScrollView_HorizontalBarBG:
            case GUIType.ScrollView_HorizontalBar:
            case GUIType.ScrollView_VerticalBarBG:
            case GUIType.ScrollView_VerticalBar:
                return true;
            }
        }

        internal void ExportMarkedLayerImages()
        {
            RebuildReferenceCaches();
            IEnumerable<PsdLayerNode> enumerable = from node in GetComponentsInChildren<PsdLayerNode>()
                where node.ShouldExportImage()
                select node;
            string path = GetImageExportDirectory();
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            int num = 0;
            int num2 = enumerable.Count();
            foreach (PsdLayerNode item in enumerable)
            {
                string text = item.ExportImageAsset();
                if (text == null)
                {
                    Debug.LogWarning((object)$"导出图层[name:{((Object)item).name}, layerIdx:{item.BindPsdLayerIndex}]图片失败!");
                }
                num++;
                EditorUtility.DisplayProgressBar($"导出进度({num}/{num2})", "导出UI图片:" + text, (float)num / (float)num2);
            }
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }

        internal void ExportUIFormPrefab(PsdLayerNode layerNode = null)
        {
            if (!ScriptableSingleton<Psd2UIFormSettings>.Instance.UseUIFormOutputDir || !string.IsNullOrWhiteSpace(ScriptableSingleton<Psd2UIFormSettings>.Instance.UIFormOutputDir))
            {
                Transform transform = this.transform;
                if ((Object)(object)layerNode != (Object)null)
                {
                    transform = ((Component)layerNode).transform;
                }
                if (!ScriptableSingleton<Psd2UIFormSettings>.Instance.UseUIFormOutputDir)
                {
                    string text = ((!string.IsNullOrWhiteSpace(ScriptableSingleton<Psd2UIFormSettings>.Instance.LastUIFormOutputDir)) ? ScriptableSingleton<Psd2UIFormSettings>.Instance.LastUIFormOutputDir : "Assets");
                    string text2 = EditorUtility.SaveFolderPanel("保存目录", text, (string)null);
                    if (!string.IsNullOrWhiteSpace(text2))
                    {
                        if (!text2.StartsWith("Assets/"))
                        {
                            text2 = PathCompatibilityUtility.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, text2);
                        }
                        ScriptableSingleton<Psd2UIFormSettings>.Instance.LastUIFormOutputDir = text2;
                        GenerateAndSaveUIFormPrefab(transform, text2);
                    }
                }
                else
                {
                    GenerateAndSaveUIFormPrefab(transform, ScriptableSingleton<Psd2UIFormSettings>.Instance.UIFormOutputDir);
                }
            }
            else
            {
                Debug.LogError((object)("生成UIForm失败! UIForm导出路径为空:" + ScriptableSingleton<Psd2UIFormSettings>.Instance.UIFormOutputDir));
            }
        }

        internal void ExportReusablePrefab(PsdLayerNode layerNode)
        {
            if ((Object)(object)layerNode == (Object)null)
            {
                Debug.LogError((object)"导出prefab失败: 目标节点为空");
                return;
            }
            if ((Object)(object)UGUIParser.Instance == (Object)null)
            {
                Debug.LogError((object)"导出prefab失败: UGUIParser配置未找到");
                return;
            }
            if (string.IsNullOrWhiteSpace(UGUIParser.Instance.GetSharedPrefabOutputDirectory()))
            {
                Debug.LogError((object)("导出prefab失败! 复用prefab导出路径为空:" + UGUIParser.Instance.GetSharedPrefabOutputDirectory()));
                return;
            }
            string text = NormalizeProjectAssetPath(UGUIParser.Instance.GetSharedPrefabOutputDirectory());
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogError((object)("导出prefab失败! 复用prefab导出路径无效:" + UGUIParser.Instance.GetSharedPrefabOutputDirectory()));
                return;
            }
            if (!Directory.Exists(text))
            {
                try
                {
                    Directory.CreateDirectory(text);
                    AssetDatabase.Refresh();
                }
                catch (Exception ex)
                {
                    Debug.LogError((object)("创建复用prefab导出目录失败:" + ex.Message));
                    return;
                }
            }
            string text2 = AssetNameSanitizer.SanitizeReferenceName(((Object)layerNode).name);
            if (string.IsNullOrWhiteSpace(text2))
            {
                text2 = "PsdLayerPrefab";
            }
            string text3 = Path.Combine(text, text2 + ".prefab").Replace("\\", "/");
            if (!File.Exists(text3) || EditorUtility.DisplayDialog("警告", "prefab文件已存在, 是否覆盖:" + text3, "覆盖生成", "取消生成"))
            {
                PsdLayerNode component = ((Component)layerNode).GetComponent<PsdLayerNode>();
                bool flag = (Object)(object)component != (Object)null && component.HasPrefabReference() && string.Equals(component.GetPrefabReferenceKey(), text2, StringComparison.OrdinalIgnoreCase);
                GameObject val = ExportReferencedPrefab(layerNode, text3, text2, flag);
                if ((Object)(object)val != (Object)null)
                {
                    Selection.activeGameObject = val;
                }
            }
        }

        internal bool GenerateAndSaveUIFormPrefab(Transform transform3, string text4)
        {
            PrefabGenerationScope value = new PrefabGenerationScope();
            if (string.IsNullOrWhiteSpace(uiFormName))
            {
                Debug.LogError((object)"导出UI Prefab失败: UI Form Name为空, 请填写UI Form Name.");
                return false;
            }
            string text = Path.Combine(text4, uiFormName + ".prefab").Replace('\\', '/');
            byte[] previousPrefab = File.Exists(text) ? File.ReadAllBytes(text) : null;
            byte[] previousPrefabMeta = File.Exists(text + ".meta") ? File.ReadAllBytes(text + ".meta") : null;
            try
            {
                // Check durable extraction identities before any delete, image export, or overwrite.
                if (PsdCommonPrefabPersistence.TryReuse(_owner.gameObject, text, out var extractedAsset))
                {
                    Selection.activeGameObject = extractedAsset;
                    Debug.Log("输入未变化，已保留公共 Prefab 和嵌套实例：" + text);
                    return true;
                }
                PsdCommonPrefabPersistence.ValidateGeneration(_owner.gameObject, text);
            }
            catch (InvalidOperationException ex)
            {
                Debug.LogError("公共 Prefab 生成冲突：" + ex.Message);
                return false;
            }
            if (!string.IsNullOrWhiteSpace(text4) && !Directory.Exists(text4))
            {
                try
                {
                    Directory.CreateDirectory(text4);
                    AssetDatabase.Refresh();
                }
                catch (Exception ex)
                {
                    Debug.LogError((object)("导出UI prefab失败:" + ex.Message));
                    return false;
                }
            }
            LoadDocumentAndRebindNodes();
            if ((Object)(object)transform3 == (Object)(object)_owner.transform && File.Exists(text))
            {
                switch (EditorUtility.DisplayDialogComplex("警告", "prefab文件已存在, 请选择生成方式:" + text, "覆盖生成(不丢失引用)", "取消", "重新生成"))
                {
                case 2:
                    if (!AssetDatabase.DeleteAsset(text))
                    {
                        Debug.LogError((object)("重新生成UIForm失败: 删除旧prefab失败:" + text));
                        return false;
                    }
                    AssetDatabase.Refresh();
                    break;
                case 1:
                    return false;
                }
            }
            PsdLayerNode[] componentsInChildren = ((Component)transform3).GetComponentsInChildren<PsdLayerNode>(true);
            value.PrefabReferenceRoots = new List<PsdLayerNode>();
            PsdLayerNode[] array = componentsInChildren.Where((PsdLayerNode node) => (Object)(object)node != (Object)null && node.HasPrefabReference()).ToArray();
            PsdLayerNode component = ((Component)transform3).GetComponent<PsdLayerNode>();
            PsdLayerNode[] array2;
            if ((Object)(object)component != (Object)null && component.HasPrefabReference())
            {
                value.PrefabReferenceRoots.Add(component);
            }
            else if (array.Length != 0)
            {
                array2 = array;
                foreach (PsdLayerNode psdLayerNode in array2)
                {
                    if ((Object)(object)psdLayerNode == (Object)null)
                    {
                        continue;
                    }
                    bool flag = false;
                    Transform parent = ((Component)psdLayerNode).transform.parent;
                    while ((Object)(object)parent != (Object)null && (Object)(object)parent != (Object)(object)transform3)
                    {
                        PsdLayerNode component2 = ((Component)parent).GetComponent<PsdLayerNode>();
                        if (!((Object)(object)component2 != (Object)null) || !component2.HasPrefabReference())
                        {
                            parent = parent.parent;
                            continue;
                        }
                        flag = true;
                        break;
                    }
                    if (!flag)
                    {
                        value.PrefabReferenceRoots.Add(psdLayerNode);
                    }
                }
            }
            value.PrefabReferenceRoots.Sort(CompareLayerNodesByHierarchyOrder);
            PsdLayerNode[] array3 = componentsInChildren.Where((PsdLayerNode node) => (Object)(object)node != (Object)null && !value.ContainsTransform(((Component)node).transform)).ToArray();
            NormalizeLayerNodeTypesAndHelpers(array3);
            RebuildReferenceCaches();
            _referencedAssetKeys.Clear();
            array2 = array3;
            foreach (PsdLayerNode psdLayerNode2 in array2)
            {
                if (psdLayerNode2.HasAssetReference() && !string.IsNullOrEmpty(psdLayerNode2.GetAssetReferenceKey()))
                {
                    _referencedAssetKeys.Add(psdLayerNode2.GetAssetReferenceKey());
                }
            }
            ExportReferencedAssets(array3);
            UIHelperBase[] array4 = GetAvailableUIHelpers(transform3);
            if (array4 != null && array4.Length != 0 && value.PrefabReferenceRoots.Count > 0)
            {
                for (int num2 = array4.Length - 1; num2 >= 0; num2--)
                {
                    UIHelperBase uIHelperBase = array4[num2];
                    if (!((Object)(object)uIHelperBase == (Object)null) && !((Object)(object)uIHelperBase.GetLayerNode() == (Object)null) && value.ContainsTransform(((Component)uIHelperBase).transform))
                    {
                        ArrayUtility.RemoveAt<UIHelperBase>(ref array4, num2);
                    }
                }
            }
            if ((array4 == null || array4.Length < 1) && value.PrefabReferenceRoots.Count < 1)
            {
                Debug.LogError((object)"生成UIForm失败: 节点树中没有可生成的 UI 控件（无 UIHelper 组件且无引用 Prefab）。请检查图层 UI 类型是否已正确设置，或先执行「解析psd图层」。");
                return false;
            }
            GameObject val = AssetDatabase.LoadAssetAtPath<GameObject>(text);
            GameObject val2;
            if ((Object)(object)val == (Object)null)
            {
                if ((Object)(object)UGUIParser.Instance.GetUIFormTemplate() == (Object)null)
                {
                    Debug.LogError((object)"生成UIForm失败: UIFormTemplate为空");
                    return false;
                }
                val2 = Object.Instantiate<GameObject>(UGUIParser.Instance.GetUIFormTemplate(), Vector3.zero, Quaternion.identity);
                val2.transform.localScale = Vector3.one;
            }
            else
            {
                val2 = Object.Instantiate<GameObject>(val);
                RestoreGeneratedKeyMetadata(text, val2);
            }
            ((Object)val2).name = uiFormName;
            HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
            value.ScopedNodePath = null;
            if ((Object)(object)transform3 != (Object)(object)_owner.transform)
            {
                value.ScopedNodePath = BuildNormalizedNodePath(((Component)transform3).gameObject, this.transform);
                if (!string.IsNullOrEmpty(value.ScopedNodePath))
                {
                    CollectGeneratedKeyIdentities(val2.transform, hashSet, (PsdGeneratedKey generatedKey) => !IsKeyWithinScope(generatedKey.Key, value.ScopedNodePath));
                }
            }
            Vector3 val3 = Vector3.zero;
            RectTransform component3 = val2.GetComponent<RectTransform>();
            if ((Object)(object)component3 != (Object)null)
            {
                val3 = (Vector2)(component3.anchoredPosition);
            }
            int num3 = 0;
            int num4 = array4.Length;
            List<UIHelperBase> list = new List<UIHelperBase>(num4);
            List<GameObject> list2 = new List<GameObject>(num4);
            UIHelperBase[] array5 = array4;
            foreach (UIHelperBase uIHelperBase2 in array5)
            {
                if (!((Object)(object)uIHelperBase2 == (Object)null) && !((Object)(object)uIHelperBase2.GetLayerNode() == (Object)null))
                {
                    EditorUtility.DisplayProgressBar($"生成UIFrom:({num3++}/{num4})", "正在生成UI元素:" + ((Object)uIHelperBase2).name, (float)num3 / (float)num4);
                    string text2 = BuildNormalizedNodePath(((Component)uIHelperBase2).gameObject, this.transform);
                    string[] array7;
                    string[] array6 = GetAncestorContainerPaths(((Component)uIHelperBase2).gameObject, this.transform, out array7);
                    GameObject val4 = ResolveOrCreateContainerHierarchy(val2, array6, array7, hashSet);
                    GameObject val5 = FindReusableGeneratedObject(val4.transform, uIHelperBase2, text2);
                    GameObject val6 = uIHelperBase2.CreateOrUpdateUIRoot(val5);
                    if (!((Object)(object)val6 == (Object)null))
                    {
                        ApplyGeneratedKey(val6, text2, uIHelperBase2.GetLayerNode().UIType.ToString(), false, hashSet);
                        RemoveRedundantButtonVisualDependencies(val6, uIHelperBase2, this.transform);
                        TagButtonTextDependencies(val6, uIHelperBase2, this.transform, hashSet);
                        val6.transform.SetParent(val4.transform, true);
                        CopySiblingIndex(val6.transform, ((Component)uIHelperBase2).transform);
                        Transform transform = val6.transform;
                        transform.position += val3;
                        val6.transform.localScale = Vector3.one;
                        uIHelperBase2.OnUIParented(val6);
                        list.Add(uIHelperBase2);
                        list2.Add(val6);
                    }
                }
            }
            if (value.PrefabReferenceRoots.Count > 0)
            {
                int num5 = 0;
                int count = value.PrefabReferenceRoots.Count;
                foreach (PsdLayerNode item in value.PrefabReferenceRoots)
                {
                    if ((Object)(object)item == (Object)null || !item.HasPrefabReference())
                    {
                        continue;
                    }
                    EditorUtility.DisplayProgressBar($"生成UIFrom-引用prefab:({num5++}/{count})", "正在实例化prefab:" + item.GetPrefabReferenceName(), (float)num5 / (float)count);
                    if (!TryResolveOrExportReferencedPrefab(item, out var val7) || (Object)(object)val7 == (Object)null)
                    {
                        Debug.LogWarning((object)("引用prefab未找到且导出失败: " + item.GetPrefabReferenceName()));
                        continue;
                    }
                    string text3 = BuildNormalizedNodePath(((Component)item).gameObject, this.transform);
                    string[] array9;
                    string[] array8 = GetAncestorContainerPaths(((Component)item).gameObject, this.transform, out array9);
                    GameObject val8 = ResolveOrCreateContainerHierarchy(val2, array8, array9, hashSet);
                    GameObject val9 = FindReusablePrefabReferenceInstance(val8.transform, item, text3, val7);
                    if ((Object)(object)val9 == (Object)null)
                    {
                        Object obj = PrefabUtility.InstantiatePrefab((Object)(object)val7);
                        val9 = (GameObject)(object)((obj is GameObject) ? obj : null);
                        if ((Object)(object)val9 == (Object)null)
                        {
                            val9 = Object.Instantiate<GameObject>(val7);
                        }
                    }
                    ((Object)val9).name = (string.IsNullOrEmpty(item.GetPrefabReferenceKey()) ? ((Object)val7).name : PsdLayerNode.GetNormalizedFileNameOrFallback(item.GetPrefabReferenceKey(), ((Object)val7).name));
                    val9.transform.localRotation = Quaternion.identity;
                    val9.transform.localScale = Vector3.one;
                    RectTransform component4 = val9.GetComponent<RectTransform>();
                    if (!((Object)(object)component4 != (Object)null))
                    {
                        Debug.LogWarning((object)("引用prefab缺少RectTransform: " + ((Object)val7).name));
                    }
                    else
                    {
                        UGUIParser.ApplyNodeRectToUI(item, component4);
                    }
                    ApplyGeneratedKey(val9, text3, "__PrefabRef", false, hashSet);
                    val9.transform.SetParent(val8.transform, true);
                    CopySiblingIndex(val9.transform, ((Component)item).transform);
                    Transform transform2 = val9.transform;
                    transform2.position += val3;
                    val9.transform.localScale = Vector3.one;
                }
            }
            NotifyGeneratedHierarchyReady(list, list2);
            RemoveUIStringKeyComponents(val2);
            RemoveStaleGeneratedNodes(val2.transform, hashSet);
            FitGeneratedContainersToChildren(val2.transform);
            List<GeneratedKeySnapshot> list3 = CaptureGeneratedKeySnapshots(val2);
            RemoveGeneratedKeyComponents(val2);
            ((Object)val2).name = Path.GetFileNameWithoutExtension(text);
            GameObject val10;
            try
            {
                val10 = PrefabUtility.SaveAsPrefabAsset(val2, text);
                if (val10 == null) throw new InvalidOperationException("保存 UI Prefab 失败。");
                PsdCommonPrefabPersistence.RecordGeneration(transform3.gameObject, text);
            }
            catch (Exception ex)
            {
                if (previousPrefab == null) AssetDatabase.DeleteAsset(text);
                else
                {
                    File.WriteAllBytes(text, previousPrefab);
                    if (previousPrefabMeta != null) File.WriteAllBytes(text + ".meta", previousPrefabMeta);
                    AssetDatabase.ImportAsset(text, ImportAssetOptions.ForceUpdate);
                }
                Object.DestroyImmediate(val2);
                EditorUtility.ClearProgressBar();
                Debug.LogError("保存 UI 或抽取来源失败，已恢复目标 Prefab：" + ex.Message);
                return false;
            }
            if ((Object)(object)val10 != (Object)null)
            {
                SaveGeneratedKeyMetadata(text, val10, list3, value.ScopedNodePath);
                Object.DestroyImmediate((Object)(object)val2);
                Selection.activeGameObject = val10;
            }
            EditorUtility.ClearProgressBar();
            return true;
        }

        private GameObject ResolveOrCreateContainerHierarchy(GameObject gameObject, string[] texts, string[] texts2, HashSet<string> texts3)
        {
            GameObject val = gameObject;
            if (texts != null && texts2 != null)
            {
                for (int i = 0; i < texts.Length; i++)
                {
                    string text = texts[i];
                    string text2 = texts2[i];
                    string text3 = PsdLayerNode.NormalizeLayerObjectName(text2);
                    GameObject val2 = FindDirectChildByGeneratedKey(val.transform, text, false);
                    bool flag = (Object)(object)val2 != (Object)null && IsGeneratedKeyPreserved(texts3, val2.GetComponent<PsdGeneratedKey>());
                    if ((Object)(object)val2 == (Object)null)
                    {
                        val2 = FindDirectChildByGeneratedKeyAndType(val.transform, text, "__Container", true);
                    }
                    if ((Object)(object)val2 == (Object)null)
                    {
                        val2 = FindReusableUnkeyedChild(val.transform, text3, true);
                    }
                    if ((Object)(object)val2 == (Object)null && !string.Equals(text2, text3, StringComparison.Ordinal))
                    {
                        val2 = FindReusableUnkeyedChild(val.transform, text2, true);
                    }
                    if ((Object)(object)val2 == (Object)null)
                    {
                        val2 = new GameObject(text3, new Type[1] { typeof(RectTransform) });
                        val2.layer = UnityEngine.LayerMask.NameToLayer("UI");
                        val2.transform.SetParent(val.transform, false);
                        val2.transform.localPosition = Vector3.zero;
                        val2.transform.localRotation = Quaternion.identity;
                        val2.transform.localScale = Vector3.one;
                    }
                    ((Object)val2).name = text3;
                    if (!flag)
                    {
                        if ((Object)(object)val2.GetComponent<PsdGeneratedKey>() != (Object)null)
                        {
                            RemoveGeneratedUIComponents(val2);
                        }
                        ApplyGeneratedKey(val2, text, "__Container", true, texts3);
                    }
                    val = val2;
                }
            }
            return val;
        }

        private static bool IsGeneratedKeyPreserved(HashSet<string> texts, object value)
        {
            if (texts != null && !((Object)value == (Object)null))
            {
                return texts.Contains(BuildGeneratedKeyIdentity(((PsdGeneratedKey)value).Key, ((PsdGeneratedKey)value).GetTypeKey(), ((PsdGeneratedKey)value).IsContainer()));
            }
            return false;
        }

        private static void CopySiblingIndex(object value, object value2)
        {
            if (!((Object)value == (Object)null) && !((Object)value2 == (Object)null) && !((Object)(object)((Transform)value).parent == (Object)null))
            {
                ((Transform)value).SetSiblingIndex(((Transform)value2).GetSiblingIndex());
            }
        }

        private static void NotifyGeneratedHierarchyReady(List<UIHelperBase> values, List<GameObject> gameObjects)
        {
            if (values == null || gameObjects == null)
            {
                return;
            }
            int num = Mathf.Min(values.Count, gameObjects.Count);
            for (int i = 0; i < num; i++)
            {
                UIHelperBase uIHelperBase = values[i];
                GameObject val = gameObjects[i];
                if (!((Object)(object)uIHelperBase == (Object)null) && !((Object)(object)val == (Object)null))
                {
                    uIHelperBase.OnGeneratedHierarchyReady(val);
                }
            }
        }

        private static string BuildGeneratedKeyIdentity(object value, object value2, bool enabled)
        {
            return ((!enabled) ? "N" : "C") + ":" + (string)value2 + ":" + (string)value;
        }

        private static string BuildGeneratedMetadataPath(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                string directoryName = Path.GetDirectoryName((string)value);
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension((string)value);
                if (!string.IsNullOrWhiteSpace(directoryName) && !string.IsNullOrWhiteSpace(fileNameWithoutExtension))
                {
                    return Path.Combine(directoryName, fileNameWithoutExtension + ".psd2uiform.generated.json").Replace("\\", "/");
                }
                return null;
            }
            return null;
        }

        private static string NormalizeMetadataPath(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return null;
            }
            return ((string)value).Replace("\\", "/").Trim();
        }

        private static bool MetadataPathsEqual(object value, object value2)
        {
            string text = NormalizeMetadataPath(value);
            string text2 = NormalizeMetadataPath(value2);
            if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(text2))
            {
                return string.Equals(text, text2, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private GeneratedMetadataSerializedEntry FindSerializedMetadataEntry(string text)
        {
            if (generatedMetadataEntries != null && generatedMetadataEntries.Count >= 1)
            {
                int num = 0;
                GeneratedMetadataSerializedEntry generatedMetadataSerializedEntry;
                while (true)
                {
                    if (num < generatedMetadataEntries.Count)
                    {
                        generatedMetadataSerializedEntry = generatedMetadataEntries[num];
                        if (generatedMetadataSerializedEntry != null && !string.IsNullOrWhiteSpace(generatedMetadataSerializedEntry.PrefabAssetPath) && MetadataPathsEqual(generatedMetadataSerializedEntry.PrefabAssetPath, text))
                        {
                            break;
                        }
                        num++;
                        continue;
                    }
                    return null;
                }
                return generatedMetadataSerializedEntry;
            }
            return null;
        }

        private static List<GeneratedMetadataEntry> SanitizeMetadataEntries(IEnumerable<GeneratedMetadataEntry> values)
        {
            List<GeneratedMetadataEntry> list = new List<GeneratedMetadataEntry>();
            if (values == null)
            {
                return list;
            }
            foreach (GeneratedMetadataEntry item in values)
            {
                if (item != null && !string.IsNullOrWhiteSpace(item.GlobalObjectId) && !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.TypeKey))
                {
                    list.Add(new GeneratedMetadataEntry
                    {
                        GlobalObjectId = item.GlobalObjectId,
                        Key = item.Key,
                        TypeKey = item.TypeKey,
                        IsContainer = item.IsContainer
                    });
                }
            }
            return list;
        }

        private static List<GeneratedMetadataEntry> ParseGeneratedMetadataJson(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return new List<GeneratedMetadataEntry>();
            }
            try
            {
                return SanitizeMetadataEntries(JsonUtility.FromJson<GeneratedMetadataCollection>((string)value)?.Entries);
            }
            catch
            {
                return new List<GeneratedMetadataEntry>();
            }
        }

        private void NormalizeSerializedMetadataEntries()
        {
            if (generatedMetadataEntries != null)
            {
                Dictionary<string, GeneratedMetadataSerializedEntry> dictionary = new Dictionary<string, GeneratedMetadataSerializedEntry>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < generatedMetadataEntries.Count; i++)
                {
                    GeneratedMetadataSerializedEntry generatedMetadataSerializedEntry = generatedMetadataEntries[i];
                    string text = NormalizeMetadataPath(generatedMetadataSerializedEntry?.PrefabAssetPath);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        List<GeneratedMetadataEntry> list = SanitizeMetadataEntries(generatedMetadataSerializedEntry.Entries);
                        if (list.Count < 1)
                        {
                            list = ParseGeneratedMetadataJson(generatedMetadataSerializedEntry.Json);
                        }
                        if (list.Count >= 1)
                        {
                            dictionary[text] = new GeneratedMetadataSerializedEntry
                            {
                                PrefabAssetPath = text,
                                Json = null,
                                Entries = list
                            };
                        }
                    }
                }
                generatedMetadataEntries = dictionary.Values.OrderBy((GeneratedMetadataSerializedEntry item) => item.PrefabAssetPath, StringComparer.OrdinalIgnoreCase).ToList();
            }
            else
            {
                generatedMetadataEntries = new List<GeneratedMetadataSerializedEntry>();
            }
        }

        private void PersistMetadataChanges()
        {
            EditorUtility.SetDirty((Object)(object)_owner);
            PrefabUtility.RecordPrefabInstancePropertyModifications((Object)(object)_owner);
            if ((Object)(object)gameObject != (Object)null)
            {
                Scene scene = gameObject.scene;
                if (scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
            }
            AssetDatabase.SaveAssets();
        }

        internal string GetMetadataOwnerPath()
        {
            string assetPath = AssetDatabase.GetAssetPath((Object)(object)gameObject);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return "<Scene Object>";
            }
            return assetPath;
        }

        internal string[] GetGeneratedMetadataPrefabPaths()
        {
            if (generatedMetadataEntries == null || generatedMetadataEntries.Count < 1)
            {
                return Array.Empty<string>();
            }
            return (from item in generatedMetadataEntries
                where item != null && !string.IsNullOrWhiteSpace(item.PrefabAssetPath) && item.Entries != null && item.Entries.Count > 0
                select NormalizeMetadataPath(item.PrefabAssetPath) into item
                where !string.IsNullOrWhiteSpace(item)
                select item).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy((string item) => item, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal string GetGeneratedMetadataJson(string json)
        {
            GeneratedMetadataSerializedEntry generatedMetadataSerializedEntry = FindSerializedMetadataEntry(json);
            if (generatedMetadataSerializedEntry != null && generatedMetadataSerializedEntry.Entries != null && generatedMetadataSerializedEntry.Entries.Count >= 1)
            {
                return JsonUtility.ToJson((object)new GeneratedMetadataCollection
                {
                    Entries = SanitizeMetadataEntries(generatedMetadataSerializedEntry.Entries)
                }, true);
            }
            return string.Empty;
        }

        private List<GeneratedMetadataEntry> LoadGeneratedMetadataEntries(string text3)
        {
            GeneratedMetadataSerializedEntry generatedMetadataSerializedEntry = FindSerializedMetadataEntry(text3);
            if (generatedMetadataSerializedEntry != null && generatedMetadataSerializedEntry.Entries != null && generatedMetadataSerializedEntry.Entries.Count > 0)
            {
                return SanitizeMetadataEntries(generatedMetadataSerializedEntry.Entries);
            }
            if (generatedMetadataSerializedEntry != null && !string.IsNullOrWhiteSpace(generatedMetadataSerializedEntry.Json))
            {
                List<GeneratedMetadataEntry> list = ParseGeneratedMetadataJson(generatedMetadataSerializedEntry.Json);
                if (list.Count > 0)
                {
                    SaveGeneratedMetadataEntries(text3, list);
                    return list;
                }
            }
            string text = BuildGeneratedMetadataPath(text3);
            if (!string.IsNullOrWhiteSpace(text) && File.Exists(text))
            {
                string text2 = File.ReadAllText(text);
                if (!string.IsNullOrWhiteSpace(text2))
                {
                    List<GeneratedMetadataEntry> list2 = ParseGeneratedMetadataJson(text2);
                    if (list2.Count > 0)
                    {
                        SaveGeneratedMetadataEntries(text3, list2);
                        return list2;
                    }
                }
            }
            return null;
        }

        private void SaveGeneratedMetadataEntries(string text3, List<GeneratedMetadataEntry> values)
        {
            string text = NormalizeMetadataPath(text3);
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogWarning((object)("保存生成节点元数据失败: target为空, target=" + text3));
                return;
            }
            if (generatedMetadataEntries == null)
            {
                generatedMetadataEntries = new List<GeneratedMetadataSerializedEntry>();
            }
            GeneratedMetadataSerializedEntry generatedMetadataSerializedEntry = FindSerializedMetadataEntry(text);
            List<GeneratedMetadataEntry> list = SanitizeMetadataEntries(values);
            if (list.Count < 1)
            {
                if (generatedMetadataSerializedEntry != null)
                {
                    generatedMetadataEntries.Remove(generatedMetadataSerializedEntry);
                }
            }
            else
            {
                if (generatedMetadataSerializedEntry == null)
                {
                    generatedMetadataSerializedEntry = new GeneratedMetadataSerializedEntry();
                    generatedMetadataEntries.Add(generatedMetadataSerializedEntry);
                }
                generatedMetadataSerializedEntry.PrefabAssetPath = text;
                generatedMetadataSerializedEntry.Entries = list;
            }
            NormalizeSerializedMetadataEntries();
            PersistMetadataChanges();
            string text2 = BuildGeneratedMetadataPath(text3);
            if (!string.IsNullOrWhiteSpace(text2) && File.Exists(text2))
            {
                AssetDatabase.DeleteAsset(text2);
            }
        }

        private List<GeneratedKeySnapshot> CaptureGeneratedKeySnapshots(GameObject gameObject)
        {
            List<GeneratedKeySnapshot> list = new List<GeneratedKeySnapshot>();
            if ((Object)(object)gameObject == (Object)null)
            {
                return list;
            }
            PsdGeneratedKey[] componentsInChildren = gameObject.GetComponentsInChildren<PsdGeneratedKey>(true);
            Transform transform = gameObject.transform;
            PsdGeneratedKey[] array = componentsInChildren;
            foreach (PsdGeneratedKey psdGeneratedKey in array)
            {
                if (!((Object)(object)psdGeneratedKey == (Object)null))
                {
                    List<int> list2 = BuildSiblingIndexPath(((Component)psdGeneratedKey).transform, transform);
                    if (list2 != null)
                    {
                        list.Add(new GeneratedKeySnapshot
                        {
                            Key = psdGeneratedKey.Key,
                            TypeKey = psdGeneratedKey.GetTypeKey(),
                            IsContainer = psdGeneratedKey.IsContainer(),
                            SiblingIndexPath = list2
                        });
                    }
                }
            }
            return list;
        }

        private static List<int> BuildSiblingIndexPath(object value, object value2)
        {
            if (!((Object)value == (Object)null) && !((Object)value2 == (Object)null) && ((Transform)value).IsChildOf((Transform)value2))
            {
                List<int> list = new List<int>(8);
                Transform val = (Transform)value;
                while ((Object)(object)val != (Object)null && (Object)(object)val != (Object)value2)
                {
                    list.Insert(0, val.GetSiblingIndex());
                    val = val.parent;
                }
                if (!((Object)(object)val == (Object)value2))
                {
                    return null;
                }
                return list;
            }
            return null;
        }

        private static GameObject ResolveObjectBySiblingIndexPath(object value, List<int> values)
        {
            if (!((Object)value == (Object)null) && values != null)
            {
                Transform val = ((GameObject)value).transform;
                int num = 0;
                while (true)
                {
                    if (num < values.Count)
                    {
                        int num2 = values[num];
                        if (num2 < 0 || num2 >= val.childCount)
                        {
                            break;
                        }
                        val = val.GetChild(num2);
                        num++;
                        continue;
                    }
                    if (!((Object)(object)val != (Object)null))
                    {
                        return null;
                    }
                    return ((Component)val).gameObject;
                }
                return null;
            }
            return null;
        }

        private static void RemoveGeneratedKeyComponents(object value)
        {
            if ((Object)value == (Object)null)
            {
                return;
            }
            PsdGeneratedKey[] componentsInChildren = ((GameObject)value).GetComponentsInChildren<PsdGeneratedKey>(true);
            for (int num = componentsInChildren.Length - 1; num >= 0; num--)
            {
                if ((Object)(object)componentsInChildren[num] != (Object)null)
                {
                    Object.DestroyImmediate((Object)(object)componentsInChildren[num]);
                }
            }
        }

        private void RestoreGeneratedKeyMetadata(string text2, GameObject gameObject)
        {
            if ((Object)(object)gameObject == (Object)null)
            {
                return;
            }
            List<GeneratedMetadataEntry> list = LoadGeneratedMetadataEntries(text2);
            if (list == null || list.Count < 1)
            {
                return;
            }
            try
            {
                Dictionary<string, GameObject> dictionary = new Dictionary<string, GameObject>(StringComparer.Ordinal);
                Transform[] componentsInChildren = gameObject.GetComponentsInChildren<Transform>(true);
                foreach (Transform val in componentsInChildren)
                {
                    if ((Object)(object)val == (Object)null)
                    {
                        continue;
                    }
                    GameObject correspondingObjectFromSource = PrefabUtility.GetCorrespondingObjectFromSource<GameObject>(((Component)val).gameObject);
                    if (!((Object)(object)correspondingObjectFromSource == (Object)null))
                    {
                        string text = GlobalObjectId.GetGlobalObjectIdSlow((Object)(object)correspondingObjectFromSource).ToString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            dictionary[text] = ((Component)val).gameObject;
                        }
                    }
                }
                foreach (GeneratedMetadataEntry item in list)
                {
                    if (item != null && !string.IsNullOrEmpty(item.GlobalObjectId) && dictionary.TryGetValue(item.GlobalObjectId, out var value) && !((Object)(object)value == (Object)null))
                    {
                        ApplyGeneratedKey(value, item.Key, item.TypeKey, item.IsContainer);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning((object)("恢复生成节点元数据失败: " + text2 + "\n" + ex.Message));
            }
        }

        private void SaveGeneratedKeyMetadata(string text2, GameObject gameObject, List<GeneratedKeySnapshot> values, string text3 = null)
        {
            try
            {
                if (!((Object)(object)gameObject == (Object)null) && values != null && values.Count >= 1)
                {
                    List<GeneratedMetadataEntry> list = new List<GeneratedMetadataEntry>();
                    foreach (GeneratedKeySnapshot item2 in values)
                    {
                        if (item2 == null || item2.SiblingIndexPath == null)
                        {
                            continue;
                        }
                        GameObject val = ResolveObjectBySiblingIndexPath(gameObject, item2.SiblingIndexPath);
                        if (!((Object)(object)val == (Object)null))
                        {
                            string text = GlobalObjectId.GetGlobalObjectIdSlow((Object)(object)val).ToString();
                            if (!string.IsNullOrEmpty(text))
                            {
                                list.Add(new GeneratedMetadataEntry
                                {
                                    GlobalObjectId = text,
                                    Key = item2.Key,
                                    TypeKey = item2.TypeKey,
                                    IsContainer = item2.IsContainer
                                });
                            }
                        }
                    }
                    if (list.Count < 1)
                    {
                        SaveGeneratedMetadataEntries(text2, null);
                        return;
                    }
                    if (!string.IsNullOrEmpty(text3))
                    {
                        List<GeneratedMetadataEntry> list2 = LoadGeneratedMetadataEntries(text2);
                        if (list2 != null && list2.Count > 0)
                        {
                            HashSet<string> hashSet = new HashSet<string>(list.Select((GeneratedMetadataEntry entry) => BuildGeneratedKeyIdentity(entry.Key, entry.TypeKey, entry.IsContainer)), StringComparer.Ordinal);
                            foreach (GeneratedMetadataEntry item3 in list2)
                            {
                                if (item3 != null && !IsKeyWithinScope(item3.Key, text3))
                                {
                                    string item = BuildGeneratedKeyIdentity(item3.Key, item3.TypeKey, item3.IsContainer);
                                    if (hashSet.Add(item))
                                    {
                                        list.Add(item3);
                                    }
                                }
                            }
                        }
                    }
                    SaveGeneratedMetadataEntries(text2, list);
                }
                else
                {
                    SaveGeneratedMetadataEntries(text2, null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning((object)("保存生成节点元数据失败: " + text2 + "\n" + ex.Message));
            }
        }

        private static bool IsKeyWithinScope(object value, object value2)
        {
            if (string.IsNullOrEmpty((string)value) || string.IsNullOrEmpty((string)value2))
            {
                return false;
            }
            if (string.Equals((string)value, (string)value2, StringComparison.Ordinal))
            {
                return true;
            }
            return ((string)value).StartsWith((string)value2 + "/", StringComparison.Ordinal);
        }

        private void CollectGeneratedKeyIdentities(Transform transform, HashSet<string> texts, Func<PsdGeneratedKey, bool> callback)
        {
            if ((Object)(object)transform == (Object)null || texts == null)
            {
                return;
            }
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (!((Object)(object)child == (Object)null))
                {
                    PsdGeneratedKey component = ((Component)child).GetComponent<PsdGeneratedKey>();
                    if ((Object)(object)component != (Object)null && (callback == null || callback(component)))
                    {
                        texts.Add(BuildGeneratedKeyIdentity(component.Key, component.GetTypeKey(), component.IsContainer()));
                    }
                    CollectGeneratedKeyIdentities(child, texts, callback);
                }
            }
        }

        private static Type GetPrimaryUIComponentType(object value)
        {
            if ((Object)value == (Object)null)
            {
                return null;
            }
            Component[] components = ((GameObject)value).GetComponents<Component>();
            foreach (Component val in components)
            {
                if (!((Object)(object)val == (Object)null) && !IsInfrastructureComponentType(((object)val).GetType()) && (val is ScrollRect || val is Selectable || val is Mask))
                {
                    return ((object)val).GetType();
                }
            }
            foreach (Component val2 in components)
            {
                if (!((Object)(object)val2 == (Object)null) && !IsInfrastructureComponentType(((object)val2).GetType()) && val2 is Graphic)
                {
                    return ((object)val2).GetType();
                }
            }
            foreach (Component val3 in components)
            {
                if (!((Object)(object)val3 == (Object)null))
                {
                    Type type = ((object)val3).GetType();
                    if (!IsInfrastructureComponentType(type))
                    {
                        return type;
                    }
                }
            }
            return null;
        }

        private static bool IsInfrastructureComponentType(Type type)
        {
            if (!(type == typeof(Transform)) && !(type == typeof(RectTransform)) && !(type == typeof(CanvasRenderer)) && !(type == typeof(UIStringKey)))
            {
                return type == typeof(PsdGeneratedKey);
            }
            return true;
        }

        private static bool IsCompatibleWithUITypePrefab(object value, GUIType uiType)
        {
            UGUIParseRule uGUIParseRule = UGUIParser.Instance?.FindRule(uiType);
            if (uGUIParseRule != null && !((Object)value == (Object)null))
            {
                if (!((Object)(object)uGUIParseRule.UIPrefab == (Object)null))
                {
                    Type type = GetPrimaryUIComponentType(uGUIParseRule.UIPrefab);
                    Type type2 = GetPrimaryUIComponentType(value);
                    if (!(type == null) && !(type2 == null))
                    {
                        return type == type2;
                    }
                    return false;
                }
                return (Object)(object)((GameObject)value).GetComponent<RectTransform>() != (Object)null;
            }
            return false;
        }

        private GameObject FindDirectChildByGeneratedKeyAndType(Transform transform, string text, string text2, bool enabled)
        {
            if (!((Object)(object)transform == (Object)null) && !string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(text2))
            {
                foreach (Transform item in transform)
                {
                    Transform val = item;
                    if (!((Object)(object)val == (Object)null))
                    {
                        PsdGeneratedKey component = ((Component)val).GetComponent<PsdGeneratedKey>();
                        if (!((Object)(object)component == (Object)null) && component.IsContainer() == enabled && string.Equals(component.GetTypeKey(), text2, StringComparison.Ordinal) && string.Equals(component.Key, text, StringComparison.Ordinal))
                        {
                            return ((Component)val).gameObject;
                        }
                    }
                }
                return null;
            }
            return null;
        }

        private GameObject FindDirectChildByGeneratedKey(Transform transform, string text, bool enabled)
        {
            if (!((Object)(object)transform == (Object)null) && !string.IsNullOrEmpty(text))
            {
                foreach (Transform item in transform)
                {
                    Transform val = item;
                    if (!((Object)(object)val == (Object)null))
                    {
                        PsdGeneratedKey component = ((Component)val).GetComponent<PsdGeneratedKey>();
                        if (!((Object)(object)component == (Object)null) && component.IsContainer() == enabled && string.Equals(component.Key, text, StringComparison.Ordinal))
                        {
                            return ((Component)val).gameObject;
                        }
                    }
                }
                return null;
            }
            return null;
        }

        private GameObject FindReusableUnkeyedChild(Transform transform, string text, bool enabled, GUIType? value = null)
        {
            if (!((Object)(object)transform == (Object)null) && !string.IsNullOrWhiteSpace(text))
            {
                GameObject result = null;
                int num = 0;
                {
                    foreach (Transform item in transform)
                    {
                        Transform val = item;
                        if ((Object)(object)val == (Object)null || !string.Equals(((Object)val).name, text, StringComparison.Ordinal) || (Object)(object)((Component)val).GetComponent<PsdGeneratedKey>() != (Object)null)
                        {
                            continue;
                        }
                        if (enabled)
                        {
                            if (GetPrimaryUIComponentType(((Component)val).gameObject) != null)
                            {
                                continue;
                            }
                        }
                        else if (value.HasValue && !IsCompatibleWithUITypePrefab(((Component)val).gameObject, value.Value))
                        {
                            continue;
                        }
                        result = ((Component)val).gameObject;
                        num++;
                        if (num > 1)
                        {
                            return null;
                        }
                    }
                    return result;
                }
            }
            return null;
        }

        private void ApplyGeneratedKey(GameObject gameObject, string text, string text2, bool enabled, HashSet<string> texts = null)
        {
            if (!((Object)(object)gameObject == (Object)null) && !string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(text2))
            {
                PsdGeneratedKey obj = gameObject.GetComponent<PsdGeneratedKey>() ?? gameObject.AddComponent<PsdGeneratedKey>();
                obj.Key = text;
                obj.SetTypeKey(text2);
                obj.SetIsContainer(enabled);
                texts?.Add(BuildGeneratedKeyIdentity(text, text2, enabled));
            }
        }

        private static void RemoveUIStringKeyComponents(object value)
        {
            if ((Object)value == (Object)null)
            {
                return;
            }
            UIStringKey[] componentsInChildren = ((GameObject)value).GetComponentsInChildren<UIStringKey>(true);
            for (int num = componentsInChildren.Length - 1; num >= 0; num--)
            {
                if ((Object)(object)componentsInChildren[num] != (Object)null)
                {
                    Object.DestroyImmediate((Object)(object)componentsInChildren[num]);
                }
            }
        }

        private void RemoveStaleGeneratedNodes(Transform transform, HashSet<string> texts)
        {
            if ((Object)(object)transform == (Object)null || texts == null)
            {
                return;
            }
            for (int num = transform.childCount - 1; num >= 0; num--)
            {
                Transform child = transform.GetChild(num);
                if ((Object)(object)child == (Object)null)
                {
                    continue;
                }
                PsdGeneratedKey component = ((Component)child).GetComponent<PsdGeneratedKey>();
                if ((Object)(object)component != (Object)null)
                {
                    string item = BuildGeneratedKeyIdentity(component.Key, component.GetTypeKey(), component.IsContainer());
                    if (!texts.Contains(item))
                    {
                        Object.DestroyImmediate((Object)(object)((Component)child).gameObject);
                        continue;
                    }
                }
                RemoveStaleGeneratedNodes(child, texts);
            }
        }

        internal string BuildNormalizedNodePath(GameObject gameObject, Transform transform)
        {
            if (!((Object)(object)gameObject == (Object)null))
            {
                if (!((Object)(object)gameObject.GetComponent<PsdLayerNode>() == (Object)null))
                {
                    List<string> list = new List<string>();
                    Transform val = gameObject.transform;
                    while ((Object)(object)val != (Object)null && (Object)(object)val != (Object)(object)transform)
                    {
                        PsdLayerNode component = ((Component)val).GetComponent<PsdLayerNode>();
                        if ((Object)(object)component != (Object)null)
                        {
                            string text = BuildUniqueNodeKeySegment(component);
                            if (!string.IsNullOrEmpty(text))
                            {
                                list.Insert(0, text);
                            }
                        }
                        val = val.parent;
                    }
                    if (list.Count > 0)
                    {
                        return string.Join("/", list);
                    }
                    return null;
                }
                return null;
            }
            return null;
        }

        private string[] GetAncestorContainerPaths(GameObject gameObject, Transform transform, out string[] result)
        {
            result = null;
            if (!((Object)(object)gameObject == (Object)null) && !((Object)(object)transform == (Object)null))
            {
                if (!((Object)(object)gameObject.transform == (Object)(object)transform))
                {
                    if (!((Object)(object)gameObject.transform.parent == (Object)null))
                    {
                        if (gameObject.transform.IsChildOf(transform))
                        {
                            if (!((Object)(object)gameObject.transform.parent == (Object)(object)transform))
                            {
                                List<PsdLayerNode> list = new List<PsdLayerNode>();
                                Transform parent = gameObject.transform.parent;
                                while ((Object)(object)parent != (Object)null && (Object)(object)parent != (Object)(object)transform)
                                {
                                    PsdLayerNode component = ((Component)parent).GetComponent<PsdLayerNode>();
                                    if ((Object)(object)component != (Object)null)
                                    {
                                        list.Insert(0, component);
                                    }
                                    parent = parent.parent;
                                }
                                if (list.Count < 1)
                                {
                                    return null;
                                }
                                result = list.Select((PsdLayerNode item) => ((Object)((Component)item).gameObject).name).ToArray();
                                return list.Select((PsdLayerNode item) => BuildNormalizedNodePath(((Component)item).gameObject, transform)).ToArray();
                            }
                            return null;
                        }
                        return null;
                    }
                    return null;
                }
                return null;
            }
            return null;
        }

        private Dictionary<string, NodeTypeSnapshot> CaptureNodeTypeSnapshots()
        {
            Dictionary<string, NodeTypeSnapshot> dictionary = new Dictionary<string, NodeTypeSnapshot>(StringComparer.OrdinalIgnoreCase);
            PsdLayerNode[] componentsInChildren = GetComponentsInChildren<PsdLayerNode>(true);
            foreach (PsdLayerNode psdLayerNode in componentsInChildren)
            {
                string text = BuildSourceLayerIdentityPath(psdLayerNode);
                if (!string.IsNullOrEmpty(text) && !dictionary.ContainsKey(text))
                {
                    dictionary.Add(text, new NodeTypeSnapshot
                    {
                        UIType = psdLayerNode.UIType
                    });
                }
            }
            return dictionary;
        }

        private void RestoreNodeTypeSnapshots(Dictionary<string, NodeTypeSnapshot> lookup)
        {
            if (lookup == null || lookup.Count < 1)
            {
                return;
            }
            PsdLayerNode[] componentsInChildren = GetComponentsInChildren<PsdLayerNode>(true);
            foreach (PsdLayerNode psdLayerNode in componentsInChildren)
            {
                string text = BuildSourceLayerIdentityPath(psdLayerNode);
                if (!string.IsNullOrEmpty(text) && lookup.TryGetValue(text, out var value))
                {
                    psdLayerNode.SetUIType(value.UIType, false);
                }
            }
        }

        private string BuildSourceLayerIdentityPath(PsdLayerNode layerNode)
        {
            if (!((Object)(object)layerNode == (Object)null))
            {
                List<string> list = new List<string>();
                Transform val = ((Component)layerNode).transform;
                while ((Object)(object)val != (Object)null && (Object)(object)val != (Object)(object)_owner.transform)
                {
                    PsdLayerNode component = ((Component)val).GetComponent<PsdLayerNode>();
                    if ((Object)(object)component != (Object)null)
                    {
                        string text = BuildUniqueSourceNameSegment(component);
                        if (!string.IsNullOrEmpty(text))
                        {
                            list.Insert(0, text);
                        }
                    }
                    val = val.parent;
                }
                if (list.Count > 0)
                {
                    return string.Join("/", list);
                }
                return null;
            }
            return null;
        }

        private string BuildUniqueSourceNameSegment(PsdLayerNode layerNode)
        {
            if ((Object)(object)layerNode == (Object)null)
            {
                return null;
            }
            string text = GetNormalizedSourceLayerName(layerNode);
            if (!string.IsNullOrEmpty(text))
            {
                int num = 0;
                Transform parent = ((Component)layerNode).transform.parent;
                if ((Object)(object)parent != (Object)null)
                {
                    for (int i = 0; i < parent.childCount; i++)
                    {
                        Transform child = parent.GetChild(i);
                        PsdLayerNode psdLayerNode = ((!((Object)(object)child != (Object)null)) ? null : ((Component)child).GetComponent<PsdLayerNode>());
                        if (!((Object)(object)psdLayerNode == (Object)null))
                        {
                            if ((object)psdLayerNode == layerNode)
                            {
                                break;
                            }
                            if (string.Equals(GetNormalizedSourceLayerName(psdLayerNode), text, StringComparison.OrdinalIgnoreCase))
                            {
                                num++;
                            }
                        }
                    }
                }
                if (num <= 0)
                {
                    return text;
                }
                return $"{text}[{num}]";
            }
            return null;
        }

        private string GetNormalizedSourceLayerName(PsdLayerNode layerNode)
        {
            if ((Object)(object)layerNode == (Object)null)
            {
                return null;
            }
            object obj;
            if (string.IsNullOrWhiteSpace(layerNode.GetSourceLayerName()))
            {
                GameObject gameObject = ((Component)layerNode).gameObject;
                obj = (((object)gameObject != null) ? ((Object)gameObject).name : null);
            }
            else
            {
                obj = layerNode.GetSourceLayerName();
            }
            string text = (string)obj;
            UGUIParser uGUIParser = UGUIParser.Instance;
            if ((Object)(object)uGUIParser != (Object)null)
            {
                text = uGUIParser.RemoveRecognizedLayerTags(text);
            }
            return PsdLayerNode.NormalizeNodeReferenceKey(text, true);
        }

        private string BuildUniqueNodeKeySegment(PsdLayerNode layerNode)
        {
            if ((Object)(object)layerNode == (Object)null)
            {
                return null;
            }
            string text = layerNode.GetNormalizedNodeKey();
            if (!string.IsNullOrEmpty(text))
            {
                int num = 0;
                Transform parent = ((Component)layerNode).transform.parent;
                if ((Object)(object)parent != (Object)null)
                {
                    for (int i = 0; i < parent.childCount; i++)
                    {
                        Transform child = parent.GetChild(i);
                        PsdLayerNode psdLayerNode = ((!((Object)(object)child != (Object)null)) ? null : ((Component)child).GetComponent<PsdLayerNode>());
                        if (!((Object)(object)psdLayerNode == (Object)null))
                        {
                            if ((object)psdLayerNode == layerNode)
                            {
                                break;
                            }
                            if (string.Equals(psdLayerNode.GetNormalizedNodeKey(), text, StringComparison.OrdinalIgnoreCase))
                            {
                                num++;
                            }
                        }
                    }
                }
                if (num > 0)
                {
                    return $"{text}[{num}]";
                }
                return text;
            }
            return null;
        }

        private GameObject FindReusableGeneratedObject(Transform transform, UIHelperBase value, string text2)
        {
            if (!((Object)(object)transform == (Object)null) && !((Object)(object)value == (Object)null) && !((Object)(object)value.GetLayerNode() == (Object)null) && !string.IsNullOrEmpty(text2))
            {
                string text = value.GetLayerNode().UIType.ToString();
                GameObject val = FindDirectChildByGeneratedKeyAndType(transform, text2, text, false);
                if ((Object)(object)val != (Object)null)
                {
                    return val;
                }
                GameObject val2 = FindDirectChildByGeneratedKey(transform, text2, false);
                if (!((Object)(object)val2 != (Object)null))
                {
                    GameObject val3 = FindDirectChildByGeneratedKeyAndType(transform, text2, "__Container", true);
                    if (!((Object)(object)val3 != (Object)null))
                    {
                        return null;
                    }
                    return val3;
                }
                return val2;
            }
            return null;
        }

        private void TagButtonTextDependencies(GameObject gameObject, UIHelperBase value, Transform transform, HashSet<string> texts)
        {
            if ((Object)(object)gameObject == (Object)null || (Object)(object)value == (Object)null || (Object)(object)transform == (Object)null || texts == null || !SupportsButtonTextDependencies(value))
            {
                return;
            }
            PsdLayerNode[] dependencies = value.GetDependencies();
            if (dependencies == null || dependencies.Length < 1)
            {
                return;
            }
            foreach (PsdLayerNode psdLayerNode in dependencies)
            {
                if (!((Object)(object)psdLayerNode == (Object)null) && IsButtonTextType(psdLayerNode.UIType))
                {
                    string text = BuildNormalizedNodePath(((Component)psdLayerNode).gameObject, transform);
                    GameObject val = FindGeneratedObjectByKey(gameObject.transform, text, false);
                    if (!((Object)(object)val == (Object)null))
                    {
                        ApplyGeneratedKey(val, text, psdLayerNode.UIType.ToString(), false, texts);
                    }
                }
            }
        }

        private void RemoveRedundantButtonVisualDependencies(GameObject gameObject, UIHelperBase value, Transform transform)
        {
            if ((Object)(object)gameObject == (Object)null || (Object)(object)value == (Object)null || (Object)(object)transform == (Object)null || !SupportsButtonVisualDependencies(value))
            {
                return;
            }
            PsdLayerNode[] dependencies = value.GetDependencies();
            if (dependencies == null || dependencies.Length < 1)
            {
                return;
            }
            foreach (PsdLayerNode psdLayerNode in dependencies)
            {
                if (!((Object)(object)psdLayerNode == (Object)null) && IsButtonVisualType(psdLayerNode.UIType))
                {
                    string text = BuildNormalizedNodePath(((Component)psdLayerNode).gameObject, transform);
                    GameObject val = FindGeneratedObjectByKey(gameObject.transform, text, false);
                    if ((Object)(object)val == (Object)null)
                    {
                        val = FindDescendantByName(gameObject.transform, psdLayerNode.GetGeneratedObjectName());
                    }
                    if (!((Object)(object)val == (Object)null) && !((Object)(object)val == (Object)(object)gameObject))
                    {
                        Object.DestroyImmediate((Object)(object)val);
                    }
                }
            }
        }

        private static bool SupportsButtonTextDependencies(object value)
        {
            if (value is ButtonHelper)
            {
                return true;
            }
            return value is TMPButtonHelper;
        }

        private static bool SupportsButtonVisualDependencies(object value)
        {
            if (value is ButtonHelper)
            {
                return true;
            }
            return value is TMPButtonHelper;
        }

        private static bool IsButtonTextType(GUIType uiType)
        {
            if (uiType != GUIType.Button_Text && uiType != GUIType.Text)
            {
                return uiType == GUIType.TMPText;
            }
            return true;
        }

        private static bool IsButtonVisualType(GUIType uiType)
        {
            if (uiType != GUIType.Background && uiType != GUIType.Image && uiType != GUIType.RawImage && uiType != GUIType.Button_Highlight && uiType != GUIType.Button_Press && uiType != GUIType.Button_Select)
            {
                return uiType == GUIType.Button_Disable;
            }
            return true;
        }

        private static GameObject FindGeneratedObjectByKey(object value, object value2, bool enabled)
        {
            if (!((Object)value == (Object)null) && !string.IsNullOrEmpty((string)value2))
            {
                PsdGeneratedKey[] componentsInChildren = ((Component)value).GetComponentsInChildren<PsdGeneratedKey>(true);
                int num = 0;
                PsdGeneratedKey psdGeneratedKey;
                while (true)
                {
                    if (num < componentsInChildren.Length)
                    {
                        psdGeneratedKey = componentsInChildren[num];
                        if (!((Object)(object)psdGeneratedKey == (Object)null) && psdGeneratedKey.IsContainer() == enabled && string.Equals(psdGeneratedKey.Key, (string)value2, StringComparison.Ordinal))
                        {
                            break;
                        }
                        num++;
                        continue;
                    }
                    return null;
                }
                return ((Component)psdGeneratedKey).gameObject;
            }
            return null;
        }

        private static GameObject FindDescendantByName(object value, object value2)
        {
            if (!((Object)value == (Object)null) && !string.IsNullOrEmpty((string)value2))
            {
                for (int i = 0; i < ((Transform)value).childCount; i++)
                {
                    Transform child = ((Transform)value).GetChild(i);
                    if (!((Object)(object)child == (Object)null))
                    {
                        if (string.Equals(((Object)child).name, (string)value2, StringComparison.Ordinal))
                        {
                            return ((Component)child).gameObject;
                        }
                        GameObject val = FindDescendantByName(child, value2);
                        if ((Object)(object)val != (Object)null)
                        {
                            return val;
                        }
                    }
                }
                return null;
            }
            return null;
        }

        private GameObject FindReusablePrefabReferenceInstance(Transform transform, PsdLayerNode layerNode, string text2, GameObject gameObject)
        {
            if (!((Object)(object)transform == (Object)null) && !((Object)(object)layerNode == (Object)null) && !string.IsNullOrEmpty(text2))
            {
                GameObject val = FindDirectChildByGeneratedKeyAndType(transform, text2, "__PrefabRef", false);
                if ((Object)(object)val != (Object)null)
                {
                    if ((Object)(object)PrefabUtility.GetCorrespondingObjectFromSource<GameObject>(val) == (Object)(object)gameObject)
                    {
                        return val;
                    }
                    Object.DestroyImmediate((Object)(object)val);
                }
                GameObject val2 = FindDirectChildByGeneratedKey(transform, text2, false);
                if ((Object)(object)val2 != (Object)null)
                {
                    Object.DestroyImmediate((Object)(object)val2);
                }
                string text = ((!string.IsNullOrEmpty(layerNode.GetPrefabReferenceKey())) ? PsdLayerNode.GetNormalizedFileNameOrFallback(layerNode.GetPrefabReferenceKey(), ((Object)((Component)layerNode).gameObject).name) : ((Object)((Component)layerNode).gameObject).name);
                GameObject val3 = FindReusableUnkeyedChild(transform, text, false);
                if (!((Object)(object)val3 == (Object)null))
                {
                    if ((Object)(object)PrefabUtility.GetCorrespondingObjectFromSource<GameObject>(val3) == (Object)(object)gameObject)
                    {
                        return val3;
                    }
                    return null;
                }
                return null;
            }
            return null;
        }

        private void RebuildReferenceCaches(PsdLayerNode[] layerNodes = null)
        {
            _nodeByReferenceKey.Clear();
            _referencedAssetKeys.Clear();
            if (layerNodes == null || layerNodes.Length == 0)
            {
                layerNodes = GetComponentsInChildren<PsdLayerNode>(true);
            }
            PsdLayerNode[] array = layerNodes;
            foreach (PsdLayerNode psdLayerNode in array)
            {
                string text = psdLayerNode.GetNormalizedNodeKey();
                if (!string.IsNullOrEmpty(text) && !_nodeByReferenceKey.ContainsKey(text))
                {
                    _nodeByReferenceKey.Add(text, psdLayerNode);
                }
            }
            array = layerNodes;
            foreach (PsdLayerNode psdLayerNode2 in array)
            {
                if (psdLayerNode2.HasAssetReference() && !string.IsNullOrEmpty(psdLayerNode2.GetAssetReferenceKey()))
                {
                    _referencedAssetKeys.Add(psdLayerNode2.GetAssetReferenceKey());
                }
            }
        }

        private void ExportReferencedAssets(PsdLayerNode[] layerNodes)
        {
            _exportedPathByNodeKey.Clear();
            if (layerNodes == null || layerNodes.Length == 0 || !TryGetSharedAssetOutputDirectory(out var text))
            {
                return;
            }
            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PsdLayerNode psdLayerNode in layerNodes)
            {
                if (psdLayerNode.HasAssetReference() && !string.IsNullOrEmpty(psdLayerNode.GetAssetReferenceKey()))
                {
                    hashSet.Add(psdLayerNode.GetAssetReferenceKey());
                }
            }
            foreach (string item in hashSet)
            {
                if (_nodeByReferenceKey.TryGetValue(item, out var value))
                {
                    string text2 = value.ExportImageAsset(true, text, item, false, true);
                    if (!string.IsNullOrEmpty(text2))
                    {
                        CacheExportPath(item, text2);
                    }
                }
            }
        }

        private static bool EnsureAssetDirectoryExists(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                string text = Path.GetDirectoryName((string)value)?.Replace("\\", "/");
                if (string.IsNullOrWhiteSpace(text))
                {
                    return false;
                }
                if (Directory.Exists(text))
                {
                    return true;
                }
                try
                {
                    Directory.CreateDirectory(text);
                    AssetDatabase.Refresh();
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError((object)("创建资源目录失败:" + ex.Message + "\n" + text));
                    return false;
                }
            }
            return false;
        }

        private bool TryGetSharedAssetOutputDirectory(out string result)
        {
            result = null;
            string text = UGUIParser.Instance?.GetSharedAssetsOutputDirectory();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            result = NormalizeProjectAssetPath(text);
            if (!string.IsNullOrWhiteSpace(result))
            {
                if (!Directory.Exists(result))
                {
                    try
                    {
                        Directory.CreateDirectory(result);
                        AssetDatabase.Refresh();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError((object)("创建共享资源目录失败:" + ex.Message));
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        private bool TryGetSharedPrefabOutputDirectory(out string result)
        {
            result = null;
            string text = UGUIParser.Instance?.GetSharedPrefabOutputDirectory();
            if (!string.IsNullOrWhiteSpace(text))
            {
                result = NormalizeProjectAssetPath(text);
                if (!string.IsNullOrWhiteSpace(result))
                {
                    if (!Directory.Exists(result))
                    {
                        try
                        {
                            Directory.CreateDirectory(result);
                            AssetDatabase.Refresh();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError((object)("创建共享Prefab目录失败:" + ex.Message));
                            return false;
                        }
                    }
                    return true;
                }
                return false;
            }
            return false;
        }

        private string NormalizeProjectAssetPath(string text2)
        {
            if (string.IsNullOrWhiteSpace(text2))
            {
                return null;
            }
            string text = text2.Replace("\\", "/").Trim();
            if (Path.IsPathRooted(text))
            {
                text = PathCompatibilityUtility.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, text).Replace("\\", "/");
            }
            if (string.Equals(text, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                return "Assets";
            }
            if (!text.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            return text.Replace("\\", "/");
        }

        private bool TryResolveOrExportReferencedPrefab(PsdLayerNode layerNode, out GameObject result)
        {
            result = null;
            if (!((Object)(object)layerNode == (Object)null) && !string.IsNullOrWhiteSpace(layerNode.GetPrefabReferenceKey()))
            {
                string text = layerNode.GetPrefabReferenceKey();
                if (_prefabAssetByReferenceKey.TryGetValue(text, out var value) && (Object)(object)value != (Object)null)
                {
                    result = value;
                    return true;
                }
                if (layerNode.TryResolveReferencedPrefab(out result) && (Object)(object)result != (Object)null)
                {
                    _prefabAssetByReferenceKey[text] = result;
                    return true;
                }
                if (TryGetSharedPrefabOutputDirectory(out var path))
                {
                    string text2 = Path.Combine(path, text + ".prefab").Replace("\\", "/");
                    if (!EnsureAssetDirectoryExists(text2))
                    {
                        return false;
                    }
                    if (File.Exists(text2))
                    {
                        AssetDatabase.ImportAsset(text2);
                        result = AssetDatabase.LoadAssetAtPath<GameObject>(text2);
                        if ((Object)(object)result != (Object)null)
                        {
                            _prefabAssetByReferenceKey[text] = result;
                            return true;
                        }
                        Debug.LogWarning((object)("引用prefab文件存在但无法加载: " + text2));
                    }
                    if (!_prefabExportsInProgress.Contains(text))
                    {
                        _prefabExportsInProgress.Add(text);
                        try
                        {
                            string text3 = PsdLayerNode.GetNormalizedFileNameOrFallback(text, ((Object)layerNode).name);
                            result = ExportReferencedPrefab(layerNode, text2, text3, true);
                            if (!((Object)(object)result != (Object)null))
                            {
                                return false;
                            }
                            _prefabAssetByReferenceKey[text] = result;
                            return true;
                        }
                        finally
                        {
                            _prefabExportsInProgress.Remove(text);
                        }
                    }
                    Debug.LogWarning((object)("检测到循环引用prefab, 已跳过导出: " + text));
                    return false;
                }
                Debug.LogWarning((object)("SharedPrefabOutput未配置, 无法复用prefab:" + ((Object)layerNode).name));
                return false;
            }
            return false;
        }

        private GameObject ExportReferencedPrefab(PsdLayerNode layerNode, string text3, string text4, bool enabled)
        {
            ReferencedPrefabExportScope value = new ReferencedPrefabExportScope();
            if ((Object)(object)layerNode == (Object)null)
            {
                return null;
            }
            if (string.IsNullOrWhiteSpace(text3) || string.IsNullOrWhiteSpace(text4))
            {
                return null;
            }
            LoadDocumentAndRebindNodes();
            Transform transform = ((Component)layerNode).transform;
            PsdLayerNode[] componentsInChildren = ((Component)transform).GetComponentsInChildren<PsdLayerNode>(true);
            value.NestedPrefabReferenceRoots = new List<PsdLayerNode>();
            PsdLayerNode component = ((Component)transform).GetComponent<PsdLayerNode>();
            PsdLayerNode[] array = componentsInChildren.Where((PsdLayerNode node) => (Object)(object)node != (Object)null && node.HasPrefabReference()).ToArray();
            PsdLayerNode[] array2;
            if (!enabled && (Object)(object)component != (Object)null && component.HasPrefabReference())
            {
                value.NestedPrefabReferenceRoots.Add(component);
            }
            else if (array.Length != 0)
            {
                array2 = array;
                foreach (PsdLayerNode psdLayerNode in array2)
                {
                    if ((Object)(object)psdLayerNode == (Object)null || (enabled && (Object)(object)psdLayerNode == (Object)(object)component))
                    {
                        continue;
                    }
                    bool flag = false;
                    Transform parent = ((Component)psdLayerNode).transform.parent;
                    while ((Object)(object)parent != (Object)null && (Object)(object)parent != (Object)(object)transform)
                    {
                        PsdLayerNode component2 = ((Component)parent).GetComponent<PsdLayerNode>();
                        if (!((Object)(object)component2 != (Object)null) || !component2.HasPrefabReference() || (enabled && !((Object)(object)component2 != (Object)(object)component)))
                        {
                            parent = parent.parent;
                            continue;
                        }
                        flag = true;
                        break;
                    }
                    if (!flag)
                    {
                        value.NestedPrefabReferenceRoots.Add(psdLayerNode);
                    }
                }
            }
            value.NestedPrefabReferenceRoots.Sort(CompareLayerNodesByHierarchyOrder);
            PsdLayerNode[] array3 = componentsInChildren.Where((PsdLayerNode node) => (Object)(object)node != (Object)null && !value.ContainsTransform(((Component)node).transform)).ToArray();
            NormalizeLayerNodeTypesAndHelpers(array3);
            RebuildReferenceCaches();
            _referencedAssetKeys.Clear();
            array2 = array3;
            foreach (PsdLayerNode psdLayerNode2 in array2)
            {
                if (psdLayerNode2.HasAssetReference() && !string.IsNullOrEmpty(psdLayerNode2.GetAssetReferenceKey()))
                {
                    _referencedAssetKeys.Add(psdLayerNode2.GetAssetReferenceKey());
                }
            }
            ExportReferencedAssets(array3);
            UIHelperBase[] array4 = GetAvailableUIHelpers(transform);
            if (array4 != null && array4.Length != 0 && value.NestedPrefabReferenceRoots.Count > 0)
            {
                for (int num2 = array4.Length - 1; num2 >= 0; num2--)
                {
                    UIHelperBase uIHelperBase = array4[num2];
                    if (!((Object)(object)uIHelperBase == (Object)null) && !((Object)(object)uIHelperBase.GetLayerNode() == (Object)null) && value.ContainsTransform(((Component)uIHelperBase).transform))
                    {
                        ArrayUtility.RemoveAt<UIHelperBase>(ref array4, num2);
                    }
                }
            }
            if ((array4 == null || array4.Length < 1) && value.NestedPrefabReferenceRoots.Count < 1)
            {
                Debug.LogWarning((object)("导出prefab失败: 未找到可生成的UI节点:" + ((Object)layerNode).name));
                return null;
            }
            GameObject val = null;
            GameObject val2 = AssetDatabase.LoadAssetAtPath<GameObject>(text3);
            UIHelperBase component3 = ((Component)transform).GetComponent<UIHelperBase>();
            bool flag2 = (Object)(object)component3 != (Object)null && (Object)(object)component3.GetLayerNode() != (Object)null && component3.GetLayerNode().IsPrimaryUIType() && component3.GetLayerNode().UIType != GUIType.Null;
            try
            {
                if ((Object)(object)val2 != (Object)null)
                {
                    val = Object.Instantiate<GameObject>(val2);
                    RestoreGeneratedKeyMetadata(text3, val);
                }
                Vector3 val3 = Vector3.zero;
                if (flag2)
                {
                    if ((Object)(object)val != (Object)null)
                    {
                        val = component3.CreateOrUpdateUIRoot(val);
                    }
                    if ((Object)(object)val == (Object)null)
                    {
                        val = component3.CreateOrUpdateUIRoot();
                    }
                    if ((Object)(object)val == (Object)null)
                    {
                        flag2 = false;
                    }
                }
                if (!flag2)
                {
                    if ((Object)(object)val != (Object)null && (Object)(object)val.GetComponent<RectTransform>() != (Object)null)
                    {
                        RemoveGeneratedUIComponents(val);
                    }
                    else
                    {
                        if ((Object)(object)val != (Object)null)
                        {
                            Object.DestroyImmediate((Object)(object)val);
                        }
                        val = new GameObject(text4, new Type[1] { typeof(RectTransform) });
                    }
                    ((Object)val).name = text4;
                    val.layer = UnityEngine.LayerMask.NameToLayer("UI");
                    val.transform.localPosition = Vector3.zero;
                    val.transform.localRotation = Quaternion.identity;
                    val.transform.localScale = Vector3.one;
                    UGUIParser.ApplyNodeRectToUI(layerNode, val.transform);
                }
                else
                {
                    ((Object)val).name = text4;
                    val.layer = UnityEngine.LayerMask.NameToLayer("UI");
                    val3 = val.transform.position;
                    val.transform.localPosition = Vector3.zero;
                    val.transform.localRotation = Quaternion.identity;
                    val.transform.localScale = Vector3.one;
                }
                HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
                int num3 = 0;
                int num4 = array4.Length;
                List<UIHelperBase> list = new List<UIHelperBase>(num4);
                List<GameObject> list2 = new List<GameObject>(num4);
                UIHelperBase[] array5 = array4;
                foreach (UIHelperBase uIHelperBase2 in array5)
                {
                    if ((Object)(object)uIHelperBase2 == (Object)null || (Object)(object)uIHelperBase2.GetLayerNode() == (Object)null || (flag2 && (Object)(object)uIHelperBase2 == (Object)(object)component3))
                    {
                        continue;
                    }
                    EditorUtility.DisplayProgressBar($"生成prefab:({num3++}/{num4})", "正在生成UI元素:" + ((Object)uIHelperBase2).name, (float)num3 / (float)num4);
                    string text = BuildNormalizedNodePath(((Component)uIHelperBase2).gameObject, transform);
                    string[] array7;
                    string[] array6 = GetAncestorContainerPaths(((Component)uIHelperBase2).gameObject, transform, out array7);
                    GameObject val4 = ResolveOrCreateContainerHierarchy(val, array6, array7, hashSet);
                    GameObject val5 = FindReusableGeneratedObject(val4.transform, uIHelperBase2, text);
                    GameObject val6 = uIHelperBase2.CreateOrUpdateUIRoot(val5);
                    if (!((Object)(object)val6 == (Object)null))
                    {
                        ApplyGeneratedKey(val6, text, uIHelperBase2.GetLayerNode().UIType.ToString(), false, hashSet);
                        RemoveRedundantButtonVisualDependencies(val6, uIHelperBase2, transform);
                        TagButtonTextDependencies(val6, uIHelperBase2, transform, hashSet);
                        val6.transform.SetParent(val4.transform, true);
                        CopySiblingIndex(val6.transform, ((Component)uIHelperBase2).transform);
                        if (flag2)
                        {
                            Transform transform2 = val6.transform;
                            transform2.position -= val3;
                        }
                        val6.transform.localScale = Vector3.one;
                        uIHelperBase2.OnUIParented(val6);
                        list.Add(uIHelperBase2);
                        list2.Add(val6);
                    }
                }
                if (value.NestedPrefabReferenceRoots.Count > 0)
                {
                    int num5 = 0;
                    int count = value.NestedPrefabReferenceRoots.Count;
                    foreach (PsdLayerNode item in value.NestedPrefabReferenceRoots)
                    {
                        if ((Object)(object)item == (Object)null || !item.HasPrefabReference())
                        {
                            continue;
                        }
                        EditorUtility.DisplayProgressBar($"生成prefab-引用prefab:({num5++}/{count})", "正在实例化prefab:" + item.GetPrefabReferenceName(), (float)num5 / (float)count);
                        if (TryResolveOrExportReferencedPrefab(item, out var val7) && !((Object)(object)val7 == (Object)null))
                        {
                            string text2 = BuildNormalizedNodePath(((Component)item).gameObject, transform);
                            string[] array9;
                            string[] array8 = GetAncestorContainerPaths(((Component)item).gameObject, transform, out array9);
                            GameObject val8 = ResolveOrCreateContainerHierarchy(val, array8, array9, hashSet);
                            GameObject val9 = FindReusablePrefabReferenceInstance(val8.transform, item, text2, val7);
                            if ((Object)(object)val9 == (Object)null)
                            {
                                Object obj = PrefabUtility.InstantiatePrefab((Object)(object)val7);
                                val9 = (GameObject)(object)((obj is GameObject) ? obj : null);
                                if ((Object)(object)val9 == (Object)null)
                                {
                                    val9 = Object.Instantiate<GameObject>(val7);
                                }
                            }
                            ((Object)val9).name = (string.IsNullOrEmpty(item.GetPrefabReferenceKey()) ? ((Object)val7).name : PsdLayerNode.GetNormalizedFileNameOrFallback(item.GetPrefabReferenceKey(), ((Object)val7).name));
                            val9.transform.localRotation = Quaternion.identity;
                            val9.transform.localScale = Vector3.one;
                            RectTransform component4 = val9.GetComponent<RectTransform>();
                            if ((Object)(object)component4 != (Object)null)
                            {
                                UGUIParser.ApplyNodeRectToUI(item, component4);
                            }
                            else
                            {
                                Debug.LogWarning((object)("引用prefab缺少RectTransform: " + ((Object)val7).name));
                            }
                            ApplyGeneratedKey(val9, text2, "__PrefabRef", false, hashSet);
                            val9.transform.SetParent(val8.transform, true);
                            CopySiblingIndex(val9.transform, ((Component)item).transform);
                            if (flag2)
                            {
                                Transform transform3 = val9.transform;
                                transform3.position -= val3;
                            }
                            val9.transform.localScale = Vector3.one;
                        }
                        else
                        {
                            Debug.LogWarning((object)("引用prefab未找到且导出失败: " + item.GetPrefabReferenceName()));
                        }
                    }
                }
                NotifyGeneratedHierarchyReady(list, list2);
                RemoveUIStringKeyComponents(val);
                RemoveStaleGeneratedNodes(val.transform, hashSet);
                FitGeneratedContainersToChildren(val.transform);
                List<GeneratedKeySnapshot> list3 = CaptureGeneratedKeySnapshots(val);
                RemoveGeneratedKeyComponents(val);
                ((Object)val).name = Path.GetFileNameWithoutExtension(text3);
                GameObject val10 = PrefabUtility.SaveAsPrefabAsset(val, text3);
                if ((Object)(object)val10 != (Object)null)
                {
                    SaveGeneratedKeyMetadata(text3, val10, list3);
                }
                return val10;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if ((Object)(object)val != (Object)null)
                {
                    Object.DestroyImmediate((Object)(object)val);
                }
            }
        }

        private static void RemoveGeneratedUIComponents(object value)
        {
            if ((Object)value == (Object)null)
            {
                return;
            }
            Component[] components = ((GameObject)value).GetComponents<Component>();
            for (int num = components.Length - 1; num >= 0; num--)
            {
                Component val = components[num];
                if (!((Object)(object)val == (Object)null))
                {
                    Type type = ((object)val).GetType();
                    if (!IsInfrastructureComponentType(type) && IsRemovableUIComponentType(type))
                    {
                        Object.DestroyImmediate((Object)(object)val);
                    }
                }
            }
        }

        private static bool IsRemovableUIComponentType(Type type)
        {
            if (!typeof(Selectable).IsAssignableFrom(type) && !typeof(Graphic).IsAssignableFrom(type) && !(type == typeof(Mask)) && !(type == typeof(ScrollRect)))
            {
                return type == typeof(CanvasRenderer);
            }
            return true;
        }

        internal string ResolveOrExportReferencedImage(PsdLayerNode layerNode, bool enabled)
        {
            if (!((Object)(object)layerNode == (Object)null) && !string.IsNullOrEmpty(layerNode.GetAssetReferenceKey()))
            {
                if (TryGetCachedExportPath(layerNode, out var text))
                {
                    return text.Replace("\\", "/");
                }
                if (!TryGetSharedAssetOutputDirectory(out var text2))
                {
                    Debug.LogWarning((object)("SharedAssetsOutput未配置, 无法复用图层:" + ((Object)layerNode).name));
                    return null;
                }
                bool flag = layerNode.IsHighBitDepthSource();
                string text3 = PsdLayerNode.GetExistingExportPath(text2, layerNode.GetAssetReferenceKey(), flag);
                if (string.IsNullOrWhiteSpace(text3))
                {
                    RebuildReferenceCaches();
                    if (_nodeByReferenceKey.TryGetValue(layerNode.GetAssetReferenceKey(), out var value) && !((Object)(object)value == (Object)null))
                    {
                        string text4 = value.ExportImageAsset(true, text2, layerNode.GetAssetReferenceKey(), false, enabled);
                        if (string.IsNullOrEmpty(text4))
                        {
                            return null;
                        }
                        CacheExportPath(layerNode.GetAssetReferenceKey(), text4);
                        return text4;
                    }
                    string text5 = layerNode.ExportImageAsset(true, text2, layerNode.GetAssetReferenceKey(), false, enabled, true);
                    if (!string.IsNullOrEmpty(text5))
                    {
                        CacheExportPath(layerNode.GetAssetReferenceKey(), text5);
                        return text5;
                    }
                    Debug.LogWarning((object)("引用图片未找到且导出失败: " + ((Object)layerNode).name + " -> " + layerNode.GetAssetReferenceName()));
                    return null;
                }
                CacheExportPath(layerNode.GetAssetReferenceKey(), text3);
                return text3;
            }
            return null;
        }

        internal bool TryGetCachedExportPath(PsdLayerNode layerNode, out string result)
        {
            result = null;
            if (!((Object)(object)layerNode == (Object)null))
            {
                string text = layerNode.GetNormalizedNodeKey();
                if (string.IsNullOrEmpty(text))
                {
                    return false;
                }
                if (_exportedPathByNodeKey.TryGetValue(text, out var value))
                {
                    if (!PsdTextureAssetUtility.MatchesExportMode(value, layerNode.IsHighBitDepthSource()))
                    {
                        _exportedPathByNodeKey.Remove(text);
                        return false;
                    }
                    if (File.Exists(value))
                    {
                        result = value.Replace("\\", "/");
                        return true;
                    }
                    _exportedPathByNodeKey.Remove(text);
                }
                return false;
            }
            return false;
        }

        internal bool TryGetSharedExportTarget(PsdLayerNode layerNode, out string result, out string result2)
        {
            result = null;
            result2 = null;
            RebuildReferenceCaches();
            if ((Object)(object)layerNode == (Object)null)
            {
                return false;
            }
            string text = layerNode.GetNormalizedNodeKey();
            if (!string.IsNullOrEmpty(text) && _referencedAssetKeys.Contains(text))
            {
                if (TryGetSharedAssetOutputDirectory(out var text2))
                {
                    result = text2;
                    result2 = text;
                    return true;
                }
                return false;
            }
            return false;
        }

        internal void CacheExportPath(string path, string path2)
        {
            if (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(path2))
            {
                _exportedPathByNodeKey[path] = path2.Replace("\\", "/");
            }
        }

        internal PsdLayerNode FindNodeByReferenceKey(string key)
        {
            RebuildReferenceCaches();
            if (!string.IsNullOrEmpty(key))
            {
                _nodeByReferenceKey.TryGetValue(key, out var value);
                return value;
            }
            return null;
        }

        private void FitGeneratedContainersToChildren(Transform transform)
        {
            if ((Object)(object)transform == (Object)null)
            {
                return;
            }
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (!((Object)(object)child == (Object)null))
                {
                    FitGeneratedContainersToChildren(child);
                }
            }
            if (IsGeneratedContainerTransform(transform))
            {
                FitContainerToChildren((transform is RectTransform) ? transform : null);
            }
        }

        private static bool IsGeneratedContainerTransform(object value)
        {
            if (!((Object)value == (Object)null))
            {
                PsdGeneratedKey component = ((Component)value).GetComponent<PsdGeneratedKey>();
                if ((Object)(object)component != (Object)null && component.IsContainer())
                {
                    return string.Equals(component.GetTypeKey(), "__Container", StringComparison.Ordinal);
                }
                return false;
            }
            return false;
        }

        private static void FitContainerToChildren(object value)
        {
            if ((Object)value == (Object)null)
            {
                return;
            }
            List<RectTransform> list = new List<RectTransform>();
            for (int i = 0; i < ((Transform)value).childCount; i++)
            {
                Transform child = ((Transform)value).GetChild(i);
                RectTransform val = (RectTransform)(object)((child is RectTransform) ? child : null);
                if (!((Object)(object)val == (Object)null))
                {
                    CenterAnchorsPreservingWorldBounds(val);
                    list.Add(val);
                }
            }
            if (list.Count >= 1 && TryCalculateCombinedWorldBounds(list, out var val2))
            {
                Vector3[] array = (Vector3[])(object)new Vector3[list.Count];
                for (int j = 0; j < list.Count; j++)
                {
                    array[j] = ((Transform)list[j]).position;
                }
                ((RectTransform)value).anchorMin = new Vector2(0.5f, 0.5f);
                ((RectTransform)value).anchorMax = new Vector2(0.5f, 0.5f);
                ((RectTransform)value).pivot = new Vector2(0.5f, 0.5f);
                ApplyWorldBoundsToRectTransform(value, val2);
                for (int k = 0; k < list.Count; k++)
                {
                    ((Transform)list[k]).position = array[k];
                }
            }
        }

        private static void CenterAnchorsPreservingWorldBounds(object value)
        {
            if (!((Object)value == (Object)null) && TryGetWorldBounds(value, out var val))
            {
                ((RectTransform)value).anchorMin = new Vector2(0.5f, 0.5f);
                ((RectTransform)value).anchorMax = new Vector2(0.5f, 0.5f);
                ApplyWorldBoundsToRectTransform(value, val);
            }
        }

        private static bool TryCalculateCombinedWorldBounds(List<RectTransform> rectTransforms, out Rect result)
        {
            result = default(Rect);
            if (rectTransforms != null && rectTransforms.Count >= 1)
            {
                bool flag = false;
                float num = 0f;
                float num2 = 0f;
                float num3 = 0f;
                float num4 = 0f;
                for (int i = 0; i < rectTransforms.Count; i++)
                {
                    if (TryGetWorldBounds(rectTransforms[i], out var val))
                    {
                        if (!flag)
                        {
                            num = val.xMin;
                            num2 = val.yMin;
                            num3 = val.xMax;
                            num4 = val.yMax;
                            flag = true;
                        }
                        else
                        {
                            num = Mathf.Min(num, val.xMin);
                            num2 = Mathf.Min(num2, val.yMin);
                            num3 = Mathf.Max(num3, val.xMax);
                            num4 = Mathf.Max(num4, val.yMax);
                        }
                    }
                }
                if (!flag)
                {
                    return false;
                }
                result = Rect.MinMaxRect(num, num2, num3, num4);
                return true;
            }
            return false;
        }

        private static bool TryGetWorldBounds(object value, out Rect result)
        {
            result = default(Rect);
            if (!((Object)value == (Object)null))
            {
                Vector3[] array = (Vector3[])(object)new Vector3[4];
                ((RectTransform)value).GetWorldCorners(array);
                float num = array[0].x;
                float num2 = array[0].y;
                float num3 = array[0].x;
                float num4 = array[0].y;
                for (int i = 1; i < array.Length; i++)
                {
                    Vector3 val = array[i];
                    num = Mathf.Min(num, val.x);
                    num2 = Mathf.Min(num2, val.y);
                    num3 = Mathf.Max(num3, val.x);
                    num4 = Mathf.Max(num4, val.y);
                }
                result = Rect.MinMaxRect(num, num2, num3, num4);
                return true;
            }
            return false;
        }

        private static void ApplyWorldBoundsToRectTransform(object value, Rect rect2)
        {
            if (!((Object)value == (Object)null))
            {
                ((RectTransform)value).SetSizeWithCurrentAnchors((RectTransform.Axis)0, rect2.width);
                ((RectTransform)value).SetSizeWithCurrentAnchors((RectTransform.Axis)1, rect2.height);
                Rect rect = ((RectTransform)value).rect;
                Vector2 size = rect.size;
                Vector2 val = (((RectTransform)value).pivot - Vector2.one * 0.5f) * size;
                ((Transform)value).position = new Vector3(rect2.center.x + val.x, rect2.center.y + val.y, ((Transform)value).position.z);
            }
        }

        private UIHelperBase[] GetAvailableUIHelpers(Transform transform)
        {
            UIHelperBase[] componentsInChildren = ((Component)transform).GetComponentsInChildren<UIHelperBase>();
            componentsInChildren = componentsInChildren.Where((UIHelperBase ui) => (Object)(object)ui != (Object)null && (Object)(object)ui.GetLayerNode() != (Object)null && ui.GetLayerNode().IsPrimaryUIType()).ToArray();
            for (int num = 0; num < componentsInChildren.Length; num++)
            {
                if ((Object)(object)componentsInChildren[num] != (Object)null)
                {
                    componentsInChildren[num].ParseAndAttachUIElements();
                }
            }
            componentsInChildren = ((Component)transform).GetComponentsInChildren<UIHelperBase>();
            componentsInChildren = componentsInChildren.Where((UIHelperBase ui) => (Object)(object)ui != (Object)null && (Object)(object)ui.GetLayerNode() != (Object)null && ui.GetLayerNode().IsPrimaryUIType()).ToArray();
            ResolveHelperDependencyClaims(componentsInChildren);
            List<Transform> list = new List<Transform>();
            PsdLayerNode[] componentsInChildren2 = ((Component)transform).GetComponentsInChildren<PsdLayerNode>(true);
            foreach (PsdLayerNode psdLayerNode in componentsInChildren2)
            {
                if ((Object)(object)psdLayerNode != (Object)null && psdLayerNode.ShouldCollapseChildrenForGeneration())
                {
                    list.Add(((Component)psdLayerNode).transform);
                }
            }
            HashSet<int> hashSet = new HashSet<int>();
            UIHelperBase[] array = componentsInChildren;
            foreach (UIHelperBase uIHelperBase in array)
            {
                if ((Object)(object)uIHelperBase == (Object)null)
                {
                    continue;
                }
                PsdLayerNode[] dependencies = uIHelperBase.GetDependencies();
                if (dependencies == null)
                {
                    continue;
                }
                componentsInChildren2 = dependencies;
                foreach (PsdLayerNode psdLayerNode2 in componentsInChildren2)
                {
                    if (!((Object)(object)psdLayerNode2 == (Object)null))
                    {
                        int item = GetObjectInstanceId(((Component)psdLayerNode2).gameObject);
                        hashSet.Add(item);
                        if (psdLayerNode2.ShouldCollapseChildrenForGeneration())
                        {
                            list.Add(((Component)psdLayerNode2).transform);
                        }
                    }
                }
            }
            for (int num4 = componentsInChildren.Length - 1; num4 >= 0; num4--)
            {
                UIHelperBase uIHelperBase2 = componentsInChildren[num4];
                if (!((Object)(object)uIHelperBase2 == (Object)null))
                {
                    if (hashSet.Contains(GetObjectInstanceId(((Component)uIHelperBase2).gameObject)) || IsDescendantOfAnyTransform(((Component)uIHelperBase2).transform, list))
                    {
                        ArrayUtility.RemoveAt<UIHelperBase>(ref componentsInChildren, num4);
                    }
                }
                else
                {
                    ArrayUtility.RemoveAt<UIHelperBase>(ref componentsInChildren, num4);
                }
            }
            Array.Sort(componentsInChildren, CompareUIHelpersByHierarchyOrder);
            return componentsInChildren;
        }

        private void NormalizeLayerNodeTypesAndHelpers(PsdLayerNode[] layerNodes)
        {
            UGUIParser uGUIParser = UGUIParser.Instance;
            if ((Object)(object)uGUIParser == (Object)null || layerNodes == null || layerNodes.Length == 0)
            {
                return;
            }
            foreach (PsdLayerNode psdLayerNode in layerNodes)
            {
                if (!((Object)(object)psdLayerNode == (Object)null))
                {
                    GUIType gUIType = uGUIParser.ApplyForcedTMPType(psdLayerNode.UIType);
                    if (gUIType != psdLayerNode.UIType)
                    {
                        psdLayerNode.SetUIType(gUIType, false);
                    }
                }
            }
            NormalizeGroupGenerationState();
            foreach (PsdLayerNode psdLayerNode2 in layerNodes)
            {
                if (!((Object)(object)psdLayerNode2 == (Object)null))
                {
                    psdLayerNode2.SynchronizeHelperComponent();
                }
            }
        }

        private static void ResolveHelperDependencyClaims(object value2)
        {
            if (value2 == null || ((Array)value2).Length < 1)
            {
                return;
            }
            Dictionary<int, HashSet<int>> dictionary = new Dictionary<int, HashSet<int>>();
            for (int i = 0; i < ((Array)value2).Length; i++)
            {
                UIHelperBase uIHelperBase = (UIHelperBase)((object[])value2)[i];
                if ((Object)(object)uIHelperBase == (Object)null)
                {
                    continue;
                }
                PsdLayerNode[] dependencies = uIHelperBase.GetDependencies();
                if (dependencies == null)
                {
                    continue;
                }
                int instanceID = ((Object)uIHelperBase).GetInstanceID();
                foreach (PsdLayerNode psdLayerNode in dependencies)
                {
                    if ((Object)(object)psdLayerNode == (Object)null)
                    {
                        continue;
                    }
                    int num = UIHelperBase.GetLayerNodeGameObjectId(psdLayerNode);
                    if (num != 0)
                    {
                        if (!dictionary.TryGetValue(num, out var value))
                        {
                            value = new HashSet<int>();
                            dictionary.Add(num, value);
                        }
                        value.Add(instanceID);
                    }
                }
            }
            for (int k = 0; k < ((Array)value2).Length; k++)
            {
                ((UIHelperBase)((object[])value2)[k])?.ResolveDependencyClaims(dictionary);
            }
        }

        private static int CompareUIHelpersByHierarchyOrder(object value, object value2)
        {
            return CompareTransformsByHierarchyOrder((!((Object)value != (Object)null)) ? null : ((Component)value).transform, (!((Object)value2 != (Object)null)) ? null : ((Component)value2).transform);
        }

        private static int CompareLayerNodesByHierarchyOrder(object value, object value2)
        {
            return CompareTransformsByHierarchyOrder((!((Object)value != (Object)null)) ? null : ((Component)value).transform, ((Object)value2 != (Object)null) ? ((Component)value2).transform : null);
        }

        private static int CompareTransformsByHierarchyOrder(object value, object value2)
        {
            if ((Object)value == (Object)value2)
            {
                return 0;
            }
            if ((Object)value == (Object)null)
            {
                return 1;
            }
            if ((Object)value2 == (Object)null)
            {
                return -1;
            }
            List<int> list = BuildAbsoluteSiblingIndexPath(value);
            List<int> list2 = BuildAbsoluteSiblingIndexPath(value2);
            int num = Mathf.Min(list.Count, list2.Count);
            for (int i = 0; i < num; i++)
            {
                int num2 = list[i].CompareTo(list2[i]);
                if (num2 != 0)
                {
                    return num2;
                }
            }
            return list.Count.CompareTo(list2.Count);
        }

        private static List<int> BuildAbsoluteSiblingIndexPath(object value)
        {
            List<int> list = new List<int>(8);
            while ((Object)value != (Object)null)
            {
                list.Insert(0, ((Transform)value).GetSiblingIndex());
                value = ((Transform)value).parent;
            }
            return list;
        }

        private static bool IsDescendantOfAnyTransform(object value, List<Transform> transforms)
        {
            if (!((Object)value == (Object)null) && transforms != null && transforms.Count != 0)
            {
                int num = 0;
                while (true)
                {
                    if (num < transforms.Count)
                    {
                        Transform val = transforms[num];
                        if (!((Object)(object)val == (Object)null) && !((Object)value == (Object)(object)val) && ((Transform)value).IsChildOf(val))
                        {
                            break;
                        }
                        num++;
                        continue;
                    }
                    return false;
                }
                return true;
            }
            return false;
        }

        internal static void ConvertTexturesType(object texts, bool enabled = true, bool enabled2 = false)
        {
            string[] array = (string[])texts;
            for (int i = 0; i < array.Length; i++)
            {
                string text = array[i];
                AssetImporter atPath = AssetImporter.GetAtPath(text);
                TextureImporter val = (TextureImporter)(object)((atPath is TextureImporter) ? atPath : null);
                if (!((Object)(object)val == (Object)null))
                {
                    if (!enabled)
                    {
                        val.textureType = (TextureImporterType)0;
                        val.textureShape = (TextureImporterShape)1;
                        val.alphaSource = (TextureImporterAlphaSource)1;
                        val.alphaIsTransparency = true;
                        val.mipmapEnabled = false;
                        val.npotScale = (TextureImporterNPOTScale)0;
                    }
                    else
                    {
                        val.textureType = (TextureImporterType)8;
                        val.spriteImportMode = (SpriteImportMode)1;
                        val.alphaSource = (TextureImporterAlphaSource)1;
                        val.alphaIsTransparency = true;
                        val.mipmapEnabled = false;
                    }
                    PsdTextureAssetUtility.ApplyPrecisionImportSettings(val, enabled2);
                    ((AssetImporter)val).SaveAndReimport();
                }
                else
                {
                    Debug.LogError((object)("TextureImporter为空:" + text));
                }
            }
        }

        internal static void EnsureNineSliceBorder(string texturePath, string layerName = null)
        {
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            // Ensure readable
            bool wasReadable = importer.isReadable;
            if (!wasReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            // Load texture
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture != null && importer.spriteBorder == Vector4.zero)
            {
                // Use new algorithm with layer name
                Vector4 border = UGUIParser.CalculateNineSliceBorder(texture, layerName);

                if (border != Vector4.zero)
                {
                    importer.spriteBorder = border;
                    importer.SaveAndReimport();

                    // Optional cropping
                    if (ScriptableSingleton<Psd2UIFormSettings>.Instance.AutoCropMinimalNineSlice)
                    {
                        TryCropMinimalNineSlice(texturePath, layerName);
                    }
                }
            }

            // Restore readable state
            if (!wasReadable)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// Backward compatibility: keep old function signature.
        /// </summary>
        internal static void EnsureNineSliceBorder(object text)
        {
            EnsureNineSliceBorder((string)text, layerName: null);
        }

        /// <summary>
        /// 节点级九宫格应用。
        ///
        /// 节点勾选了手动九宫格（Hierarchy 行上那颗九宫格标识亮着）时，
        /// 用节点记录的四个边距直接覆盖 TextureImporter.spriteBorder，
        /// 不再走"图层名标签 → 像素推断"；否则行为与原来完全一致。
        /// </summary>
        internal static void EnsureNineSliceBorder(string texturePath, PsdLayerNode node)
        {
            node = Psd2UiNineSliceNodeState.ResolveSourceNode(node);
            if (node == null)
            {
                EnsureNineSliceBorder(texturePath, (string)null);
                return;
            }

            if (!node.NineSliceEnabled)
            {
                EnsureNineSliceBorder(texturePath, node.GetSourceLayerName());
                return;
            }

            string error;
            if (Psd2UiNineSliceNodeState.ApplyBorderToImportedSprite(
                    node, texturePath, Psd2UiNineSliceNodeState.GetBorder(node), out error))
            {
                Debug.Log("[Psd2UIForm] 九宫格：使用节点手动边距 左" + node.nineSliceLeft + " 上" + node.nineSliceTop +
                    " 右" + node.nineSliceRight + " 下" + node.nineSliceBottom + " → " + texturePath);
            }
            else
            {
                Debug.LogWarning("[Psd2UIForm] 九宫格：手动边距写入失败 - " + error);
            }
        }

        /// <summary>
        /// Crop texture to minimal nine-slice size using PSDLayoutTool2's cropper.
        /// </summary>
        private static bool TryCropMinimalNineSlice(string texturePath, string layerName)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                return false;
            }

            // Parse naming rules
            Psd2UiNineSliceNameRule rule = null;
            if (!string.IsNullOrEmpty(layerName))
            {
                // Compatible with old tags
                if (layerName.Contains("|sliced") || layerName.Contains("|九宫格"))
                {
                    rule = new Psd2UiNineSliceNameRule(Psd2UiNineSliceMode.NineSlice, null);
                }
                else
                {
                    Psd2UiNineSliceNameRules.TryParse(layerName, out rule);
                }
            }

            if (rule == null)
            {
                rule = new Psd2UiNineSliceNameRule(Psd2UiNineSliceMode.NineSlice, null);
            }

            // Convert to raster
            Psd2UiNineSliceRaster raster = UGUIParser.TextureToRaster(texture);

            // Use cropping processor
            Psd2UiNineSliceRaster croppedRaster;
            Psd2UiNineSliceBorder border;
            string reason;

            if (Psd2UiNineSliceAutoProcessor.TryProcessRaster(raster, rule, out croppedRaster, out border, out reason))
            {
                // Convert back to Texture2D
                Texture2D croppedTexture = new Texture2D(
                    croppedRaster.Width,
                    croppedRaster.Height,
                    TextureFormat.RGBA32,
                    false);

                Color32[] croppedPixels = new Color32[croppedRaster.Width * croppedRaster.Height];
                for (int i = 0; i < croppedPixels.Length; i++)
                {
                    croppedPixels[i] = new Color32(
                        croppedRaster.Pixels[i * 4 + 0],
                        croppedRaster.Pixels[i * 4 + 1],
                        croppedRaster.Pixels[i * 4 + 2],
                        croppedRaster.Pixels[i * 4 + 3]);
                }

                croppedTexture.SetPixels32(croppedPixels);
                byte[] pngBytes = croppedTexture.EncodeToPNG();
                Object.DestroyImmediate(croppedTexture);

                // Write file
                File.WriteAllBytes(texturePath, pngBytes);
                AssetDatabase.Refresh();

                // Update border
                TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (importer != null)
                {
                    importer.spriteBorder = new Vector4(
                        border.Left,
                        border.Bottom,
                        border.Right,
                        border.Top);
                    importer.SaveAndReimport();
                }

                Debug.Log($"[Psd2UI NineSlice Crop] {texturePath}: Success");
                return true;
            }

            Debug.LogWarning($"[Psd2UI NineSlice Crop] {texturePath}: {reason}");
            return false;
        }

        internal static bool CompressImageFile(object value)
        {
            return false;
        }

        internal string GetImageExportDirectory()
        {
            return Path.Combine(ScriptableSingleton<Psd2UIFormSettings>.Instance.UIImagesOutputDir, uiFormName);
        }

    }
}
