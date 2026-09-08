using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using PsdProtectionGuards;
using PsdEditorAttributes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PsdLayerUtilities;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdPathUtilities;

namespace UGF.EditorTools.Psd2UGUI
{

[RequireComponent(typeof(SpriteRenderer))]
[ExecuteInEditMode]
public sealed class Psd2UIFormConverter : MonoBehaviour
{
	[Serializable]
	private sealed class GeneratedMetadataCollection
	{
		public List<GeneratedMetadataEntry> Entries;

		public GeneratedMetadataCollection()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			Entries = new List<GeneratedMetadataEntry>();
		}
	}

	[Serializable]
	private sealed class GeneratedMetadataEntry
	{
		public string GlobalObjectId;

		public string Key;

		public string TypeKey;

		public bool IsContainer;

		public GeneratedMetadataEntry()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}
	}

	[Serializable]
	private sealed class GeneratedMetadataSerializedEntry
	{
		public string PrefabAssetPath;

		[HideInInspector]
		public string Json;

		public List<GeneratedMetadataEntry> Entries;

		public GeneratedMetadataSerializedEntry()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			Entries = new List<GeneratedMetadataEntry>();
		}
	}

	private sealed class GeneratedNodeMetadataSnapshot
	{
		public GameObject GameObject;

		public string GeneratedKey;

		public string TypeKey;

		public bool IsContainer;

		public GeneratedNodeMetadataSnapshot()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}
	}

	private sealed class LayerUiTypeSnapshot
	{
		public GUIType UiType;

		public GUIType RoleUiType;

		public LayerUiTypeSnapshot()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}
	}

	private sealed class RenderedPreviewLayer
	{
		internal PsdLayer Layer;

		internal PsdRenderedImage RenderedImage;

		public RenderedPreviewLayer()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}
	}

	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass129_0
	{
		public Psd2UIFormConverter Converter;

		public Transform GenerationRoot;

		public _003C_003Ec__DisplayClass129_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal string SelectGeneratedNodePath(PsdLayerNode item)
		{
			return Converter.BuildGeneratedNodePath(item.gameObject, GenerationRoot);
		}
	}

	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass145_0
	{
		public List<PsdLayerNode> PrefabReferenceRoots;

		public _003C_003Ec__DisplayClass145_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal bool IsWithinPrefabReferenceRoot(Transform transform)
		{
			if (!(transform == null) && PrefabReferenceRoots.Count != 0)
			{
				foreach (PsdLayerNode item in PrefabReferenceRoots)
				{
					if (!(item == null) && (transform == item.transform || transform.IsChildOf(item.transform)))
					{
						return true;
					}
				}
				return false;
			}
			return false;
		}

		internal bool IsOutsidePrefabReferenceRoots(PsdLayerNode node)
		{
			if (node != null)
			{
				return !IsWithinPrefabReferenceRoot(node.transform);
			}
			return false;
		}
	}

	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass50_0
	{
		public PsdLayerNode SelectedLayerNode;

		public _003C_003Ec__DisplayClass50_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal void ApplySelectedUiType(GUIType selectUIType)
		{
			GameObject[] gameObjects = Selection.gameObjects;
			if (gameObjects.Length > 1)
			{
				GameObject[] array = gameObjects;
				for (int i = 0; i < array.Length; i++)
				{
					PsdLayerNode psdLayerNode = array[i]?.GetComponent<PsdLayerNode>();
					if (psdLayerNode != null)
					{
						psdLayerNode.SetUiType(selectUIType);
						EditorUtility.SetDirty(psdLayerNode);
					}
				}
			}
			else
			{
				SelectedLayerNode.SetUiType(selectUIType);
			}
		}
	}

	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass51_0
	{
		public Action<GUIType> OnUiTypeSelected;

		public _003C_003Ec__DisplayClass51_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}
	}

	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass51_1
	{
		public GUIType MenuUiType;

		public _003C_003Ec__DisplayClass51_0 SelectionCallbackContext;

		public _003C_003Ec__DisplayClass51_1()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal void InvokeUiTypeSelection()
		{
			SelectionCallbackContext.OnUiTypeSelected(MenuUiType);
		}
	}

	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass98_0
	{
		public List<PsdLayerNode> ExcludedPrefabReferenceRoots;

		public string PreservedGeneratedKeyPrefix;

		public _003C_003Ec__DisplayClass98_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal bool IsWithinExcludedPrefabReference(Transform transform)
		{
			if (!(transform == null) && ExcludedPrefabReferenceRoots.Count != 0)
			{
				foreach (PsdLayerNode item in ExcludedPrefabReferenceRoots)
				{
					if (!(item == null) && (transform == item.transform || transform.IsChildOf(item.transform)))
					{
						return true;
					}
				}
				return false;
			}
			return false;
		}

		internal bool IsOutsideExcludedPrefabReferences(PsdLayerNode node)
		{
			if (!(node != null))
			{
				return false;
			}
			return !IsWithinExcludedPrefabReference(node.transform);
		}

		internal bool IsOutsidePreservedKeyPrefix(PsdGeneratedKey generatedKey)
		{
			return !IsGeneratedKeyWithinPrefix(generatedKey.Key, PreservedGeneratedKeyPrefix);
		}
	}

	[CompilerGenerated]
	private static Psd2UIFormConverter _instance;

	[PsdReadOnlyAttribute]
	[SerializeField]
	internal string psdAssetChangeTime;

	[SerializeField]
	[Tooltip("UIForm名字")]
	private string uiFormName;

	[SerializeField]
	[Tooltip("关联的psd文件")]
	private Sprite psdAsset;

	[HideInInspector]
	[SerializeField]
	private Sprite previewSprite;

	[SerializeField]
	[HideInInspector]
	private string psdAssetPath;

	[SerializeField]
	[Header("Debug:")]
	private bool drawLayerRectGizmos = true;

	[SerializeField]
	private Color drawLayerRectGizmosColor = Color.gray;

	[SerializeField]
	[Tooltip("Scene点选时，优先选中命中区域内面积最小的图层节点")]
	private bool preferSmallestLayerOnScenePick = true;

	[HideInInspector]
	[SerializeField]
	private List<GeneratedMetadataSerializedEntry> generatedMetadataEntries = new List<GeneratedMetadataSerializedEntry>();

	private PsdDocument _document;

	private GUIStyle _hierarchyLabelStyle;

	private readonly Dictionary<string, PsdLayerNode> _layersByLookupKey = new Dictionary<string, PsdLayerNode>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, string> _exportedImagePathsByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, GameObject> _sharedPrefabsByKey = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

	private readonly HashSet<string> _referencedImageKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private readonly HashSet<string> _prefabExportsInProgress = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	internal static Psd2UIFormConverter Instance
	{
		[CompilerGenerated]
		get
		{
			return _instance;
		}
		[CompilerGenerated]
		private set
		{
			_instance = value;
		}
	}

	[SpecialName]
	internal bool HasLoadedDocument()
	{
		return _document != null;
	}

	[SpecialName]
	internal string GetPsdAssetPath()
	{
		if (psdAsset != null)
		{
			string assetPath = AssetDatabase.GetAssetPath(psdAsset);
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
	internal Sprite GetDisplaySprite()
	{
		if (!(psdAsset != null))
		{
			return previewSprite;
		}
		return psdAsset;
	}

	[SpecialName]
	internal Vector2Int GetDocumentPixelSize()
	{
		if (_document != null)
		{
			return new Vector2Int(_document.Width, _document.Height);
		}
		return Vector2Int.zero;
	}

	private void OnEnable()
	{
		Instance = this;
		_hierarchyLabelStyle = new GUIStyle();
		_hierarchyLabelStyle.fontSize = 13;
		_hierarchyLabelStyle.fontStyle = FontStyle.BoldAndItalic;
		ColorUtility.TryParseHtmlString("#7ED994", out var color);
		_hierarchyLabelStyle.normal.textColor = color;
		if (_document == null && !string.IsNullOrWhiteSpace(GetPsdAssetPath()))
		{
			ReloadDocumentAndRebindLayers();
		}
		SceneView.duringSceneGui += OnSceneGUI;
		EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItem;
	}

	private void Start()
	{
		ReloadDocumentAndRebindLayers();
	}

	private void OnDrawGizmos()
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
		foreach (PsdLayerNode psdLayerNode2 in array)
		{
			if (!(psdLayerNode2.gameObject == activeGameObject))
			{
				if (psdLayerNode2.ShouldExportImage())
				{
					Gizmos.DrawWireCube(psdLayerNode2.GetLayerBounds().position * 0.01f, psdLayerNode2.GetLayerBounds().size * 0.01f);
				}
			}
			else
			{
				psdLayerNode = psdLayerNode2;
			}
		}
		if (psdLayerNode != null)
		{
			Gizmos.color = Color.green;
			Gizmos.DrawWireCube(psdLayerNode.GetLayerBounds().position * 0.01f, psdLayerNode.GetLayerBounds().size * 0.01f);
		}
	}

	private void OnSceneGUI(SceneView view)
	{
		Event current = Event.current;
		if (current == null || current.type != EventType.MouseUp || current.button != 0)
		{
			return;
		}
		Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
		if (new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float enter))
		{
			Vector3 point = ray.GetPoint(enter);
			PsdLayerNode psdLayerNode = (preferSmallestLayerOnScenePick ? PickSmallestLayerAtPoint(point, base.transform) : PickTopmostLayerAtPoint(point, base.transform));
			if (psdLayerNode != null)
			{
				Selection.activeGameObject = psdLayerNode.gameObject;
				EditorGUIUtility.PingObject(psdLayerNode.gameObject);
				Event.current.Use();
			}
		}
	}

	private static PsdLayerNode PickTopmostLayerAtPoint(Vector3 P_0, object P_1)
	{
		if (!((UnityEngine.Object)P_1 == null))
		{
			if (!IsLayerPickableInScene(((Component)P_1).gameObject))
			{
				return null;
			}
			int num = ((Transform)P_1).childCount - 1;
			PsdLayerNode psdLayerNode;
			while (true)
			{
				if (num >= 0)
				{
					Transform child = ((Transform)P_1).GetChild(num);
					psdLayerNode = PickTopmostLayerAtPoint(P_0, child);
					if (psdLayerNode != null)
					{
						break;
					}
					num--;
					continue;
				}
				PsdLayerNode component = ((Component)P_1).GetComponent<PsdLayerNode>();
				if (component != null && ContainsScenePoint(P_0, component.GetLayerBounds()))
				{
					return component;
				}
				return null;
			}
			return psdLayerNode;
		}
		return null;
	}

	private static PsdLayerNode PickSmallestLayerAtPoint(Vector3 P_0, object P_1)
	{
		PsdLayerNode result = null;
		float num = float.MaxValue;
		AccumulateSmallestLayerHit(P_0, P_1, ref result, ref num);
		return result;
	}

	private static void AccumulateSmallestLayerHit(Vector3 P_0, object P_1, ref PsdLayerNode P_2, ref float P_3)
	{
		if ((UnityEngine.Object)P_1 == null || !IsLayerPickableInScene(((Component)P_1).gameObject))
		{
			return;
		}
		for (int num = ((Transform)P_1).childCount - 1; num >= 0; num--)
		{
			AccumulateSmallestLayerHit(P_0, ((Transform)P_1).GetChild(num), ref P_2, ref P_3);
		}
		PsdLayerNode component = ((Component)P_1).GetComponent<PsdLayerNode>();
		if (!(component == null) && ContainsScenePoint(P_0, component.GetLayerBounds()))
		{
			float num2 = GetRectArea(component.GetLayerBounds());
			if (num2 < P_3)
			{
				P_3 = num2;
				P_2 = component;
			}
		}
	}

	private static float GetRectArea(Rect P_0)
	{
		Vector2 size = P_0.size;
		return size.x * size.y;
	}

	private static bool IsLayerPickableInScene(object P_0)
	{
		if (!((UnityEngine.Object)P_0 == null) && ((GameObject)P_0).activeInHierarchy)
		{
			if ((((UnityEngine.Object)P_0).hideFlags & HideFlags.HideInHierarchy) == 0)
			{
				return !UnityEditor.ScriptableSingleton<SceneVisibilityManager>.instance.IsHidden((GameObject)P_0);
			}
			return false;
		}
		return false;
	}

	private static bool ContainsScenePoint(Vector3 P_0, Rect P_1)
	{
		Vector2 vector = P_1.position * 0.01f;
		Vector2 vector2 = P_1.size * 0.01f * 0.5f;
		Vector2 vector3 = vector - vector2;
		Vector2 vector4 = vector + vector2;
		if (P_0.x >= vector3.x && P_0.x <= vector4.x && P_0.y >= vector3.y)
		{
			return P_0.y <= vector4.y;
		}
		return false;
	}

	private static GameObject HierarchyIdToGameObject(EntityId entityId)
	{
		return EditorUtility.EntityIdToObject(entityId) as GameObject;
	}

	private static EntityId GetObjectEntityId(object P_0)
	{
		return ((UnityEngine.Object)P_0).GetEntityId();
	}

	private void OnHierarchyWindowItem(int instanceId, Rect selectionRect)
	{
		DrawLayerHierarchyControls((EntityId)instanceId, selectionRect);
	}

	private void DrawLayerHierarchyControls(EntityId P_0, Rect P_1)
	{
		_003C_003Ec__DisplayClass50_0 CS_0024_003C_003E8__locals18 = new _003C_003Ec__DisplayClass50_0();
		if (Event.current == null)
		{
			return;
		}
		GameObject gameObject = HierarchyIdToGameObject(P_0);
		if (gameObject == null || gameObject == base.gameObject || !gameObject.TryGetComponent<PsdLayerNode>(out CS_0024_003C_003E8__locals18.SelectedLayerNode))
		{
			return;
		}
		Rect position = P_1;
		position.x = 35f;
		position.width = 10f;
		Undo.RecordObject(CS_0024_003C_003E8__locals18.SelectedLayerNode, "Change Export Image");
		EditorGUI.BeginChangeCheck();
		CS_0024_003C_003E8__locals18.SelectedLayerNode.markToExport = EditorGUI.Toggle(position, CS_0024_003C_003E8__locals18.SelectedLayerNode.markToExport);
		if (EditorGUI.EndChangeCheck())
		{
			if (Selection.gameObjects.Length > 1)
			{
				SetSelectedLayersExportFlag(Selection.gameObjects, CS_0024_003C_003E8__locals18.SelectedLayerNode.markToExport);
			}
			EditorUtility.SetDirty(CS_0024_003C_003E8__locals18.SelectedLayerNode);
		}
		position.width = Mathf.Clamp(P_1.xMax * 0.2f, 100f, 200f);
		position.x = P_1.xMax - position.width;
		if (EditorGUI.DropdownButton(position, new GUIContent(CS_0024_003C_003E8__locals18.SelectedLayerNode.UIType.ToString()), FocusType.Passive))
		{
			ShowUiTypeSelectionMenu(CS_0024_003C_003E8__locals18.SelectedLayerNode, delegate(GUIType selectUIType)
			{
				GameObject[] gameObjects = Selection.gameObjects;
				if (gameObjects.Length > 1)
				{
					GameObject[] array = gameObjects;
					for (int i = 0; i < array.Length; i++)
					{
						PsdLayerNode psdLayerNode2 = array[i]?.GetComponent<PsdLayerNode>();
						if (psdLayerNode2 != null)
						{
							psdLayerNode2.SetUiType(selectUIType);
							EditorUtility.SetDirty(psdLayerNode2);
						}
					}
				}
				else
				{
					CS_0024_003C_003E8__locals18.SelectedLayerNode.SetUiType(selectUIType);
				}
			}).ShowAsContext();
		}
		if (!CS_0024_003C_003E8__locals18.SelectedLayerNode.HasImageReference() && !CS_0024_003C_003E8__locals18.SelectedLayerNode.HasPrefabReference())
		{
			return;
		}
		GUIContent content = new GUIContent(CS_0024_003C_003E8__locals18.SelectedLayerNode.name);
		float num = Mathf.Min(GUI.skin.button.CalcSize(content).x + 8f, 100f);
		if (GUI.Button(new Rect(P_1.xMax - position.width - num, P_1.y, num, P_1.height), content))
		{
			UnityEngine.Object activeObject;
			GameObject activeObject2;
			if (CS_0024_003C_003E8__locals18.SelectedLayerNode.HasImageReference() && CS_0024_003C_003E8__locals18.SelectedLayerNode.TryResolveReferencedLayer(out var psdLayerNode))
			{
				Selection.activeGameObject = psdLayerNode.gameObject;
			}
			else if (CS_0024_003C_003E8__locals18.SelectedLayerNode.HasImageReference() && CS_0024_003C_003E8__locals18.SelectedLayerNode.TryLoadReferencedImageAsset(out activeObject))
			{
				Selection.activeObject = activeObject;
			}
			else if (CS_0024_003C_003E8__locals18.SelectedLayerNode.HasPrefabReference() && CS_0024_003C_003E8__locals18.SelectedLayerNode.TryLoadReferencedPrefab(out activeObject2))
			{
				Selection.activeObject = activeObject2;
			}
		}
	}

	private GenericMenu ShowUiTypeSelectionMenu(PsdLayerNode P_0, Action<GUIType> P_1)
	{
		_003C_003Ec__DisplayClass51_0 _003C_003Ec__DisplayClass51_ = new _003C_003Ec__DisplayClass51_0();
		_003C_003Ec__DisplayClass51_.OnUiTypeSelected = P_1;
		Array values = Enum.GetValues(typeof(GUIType));
		GenericMenu genericMenu = new GenericMenu();
		IEnumerator enumerator = values.GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				_003C_003Ec__DisplayClass51_1 CS_0024_003C_003E8__locals8 = new _003C_003Ec__DisplayClass51_1();
				CS_0024_003C_003E8__locals8.SelectionCallbackContext = _003C_003Ec__DisplayClass51_;
				CS_0024_003C_003E8__locals8.MenuUiType = (GUIType)enumerator.Current;
				string text = (UGUIParser.IsPrimaryUiType(CS_0024_003C_003E8__locals8.MenuUiType) ? CS_0024_003C_003E8__locals8.MenuUiType.ToString() : CS_0024_003C_003E8__locals8.MenuUiType.ToString().Replace('_', '/'));
				genericMenu.AddItem(new GUIContent(text), CS_0024_003C_003E8__locals8.MenuUiType.Equals(P_0.UIType), delegate
				{
					CS_0024_003C_003E8__locals8.SelectionCallbackContext.OnUiTypeSelected(CS_0024_003C_003E8__locals8.MenuUiType);
				});
			}
			return genericMenu;
		}
		finally
		{
			IDisposable disposable = enumerator as IDisposable;
			if (disposable != null)
			{
				disposable.Dispose();
			}
		}
	}

	private void SetSelectedLayersExportFlag(GameObject[] P_0, bool P_1)
	{
		GameObject[] array = P_0.Where((GameObject item) => item?.GetComponent<PsdLayerNode>() != null).ToArray();
		for (int num = 0; num < array.Length; num++)
		{
			array[num].GetComponent<PsdLayerNode>().markToExport = P_1;
		}
	}

	private void OnDestroy()
	{
		SceneView.duringSceneGui -= OnSceneGUI;
		EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyWindowItem;
		if (_document != null)
		{
			_document.Dispose();
			_document = null;
		}
	}

	private void ReloadDocumentAndRebindLayers()
	{
		if (_document == null)
		{
			if (!File.Exists(GetPsdAssetPath()))
			{
				Debug.LogError("刷新节点绑定图层失败! 源文档不存在:" + GetPsdAssetPath());
				return;
			}
			try
			{
				EditorUtility.DisplayProgressBar("文件加载中", $"正在读取源文档:{GetPsdAssetPath()}\n文件过大会影响读取速度,建议通过栅格化图层减小文档大小", 0.5f);
				_document = PsdDocument.Create(GetPsdAssetPath());
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				return;
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}
		PsdLayerNode[] componentsInChildren = GetComponentsInChildren<PsdLayerNode>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].RebindLayerFromDocument(_document);
		}
		RefreshLargeDocumentPreview(_document);
		RefreshPreviewSpriteRenderer();
	}

	[MenuItem("Assets/Psd2UIForm Editor", priority = 0)]
	private static void Psd2UIFormPrefabMenu()
	{
		if (Selection.activeObject == null)
		{
			return;
		}
		string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
		if (IsPhotoshopDocumentPath(assetPath))
		{
			string text = GetUiFormEditorPrefabPath(assetPath);
			if (!File.Exists(text))
			{
				if (CreateUiFormEditorPrefabFromPsd(assetPath))
				{
					OpenUiFormEditorPrefab(text);
				}
			}
			else
			{
				OpenUiFormEditorPrefab(text);
			}
		}
		else
		{
			Debug.LogWarning("选择的文件(" + assetPath + ")不是PSD/PSB格式, 工具只支持PSD/PSB转换为UIForm");
		}
	}

	[MenuItem("Assets/Psd2UIForm Editor", true)]
	private static bool ValidatePsd2UIFormPrefabMenu()
	{
		if (Selection.activeObject != null && IsPhotoshopDocumentPath(AssetDatabase.GetAssetPath(Selection.activeObject)))
		{
			return true;
		}
		return false;
	}

	internal bool HasPsdAssetChanged()
	{
		string text = GetPsdAssetPath();
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		string strB = GetAssetModificationStamp(text);
		return psdAssetChangeTime.CompareTo(strB) != 0;
	}

	private static string GetAssetModificationStamp(object documentAssetPath)
	{
		return new FileInfo((string)documentAssetPath).LastWriteTimeUtc.ToString("yyyyMMddHHmmss");
	}

	internal static void OpenUiFormEditorPrefab(object prefabAssetPath)
	{
		OpenPrefabStageFromAssetPath(prefabAssetPath);
	}

	private static void OpenPrefabStageFromAssetPath(object prefabAssetPath)
	{
		GUID val = AssetDatabase.GUIDFromAssetPath((string)prefabAssetPath);
		PrefabStageUtility.OpenPrefab((string)(val.Empty() ? prefabAssetPath : AssetDatabase.GUIDToAssetPath(val)));
	}

	internal static bool CreateUiFormEditorPrefabFromPsd(object documentAssetPath, Psd2UIFormConverter existingConverter = null, bool preserveLayerUiTypes = false)
	{
		documentAssetPath = NormalizeProjectAssetPath(documentAssetPath);
		if (!string.IsNullOrWhiteSpace((string)documentAssetPath) && File.Exists((string)documentAssetPath))
		{
			TextureImporter sourceTextureImporter = AssetImporter.GetAtPath((string)documentAssetPath) as TextureImporter;
			if (sourceTextureImporter != null)
			{
				if (sourceTextureImporter.textureType == TextureImporterType.Sprite && sourceTextureImporter.spriteImportMode == SpriteImportMode.Single)
				{
					AssetDatabase.ImportAsset((string)documentAssetPath, ImportAssetOptions.ForceSynchronousImport);
				}
				else
				{
					sourceTextureImporter.textureType = TextureImporterType.Sprite;
					sourceTextureImporter.spriteImportMode = SpriteImportMode.Single;
					sourceTextureImporter.mipmapEnabled = false;
					sourceTextureImporter.alphaIsTransparency = true;
					sourceTextureImporter.SaveAndReimport();
				}
			}
			string converterPrefabPath = GetUiFormEditorPrefabPath(documentAssetPath);
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(converterPrefabPath);
			bool createdTemporaryRoot = existingConverter == null;
			if (existingConverter != null)
			{
				return RebuildLayerHierarchy(documentAssetPath, existingConverter, preserveLayerUiTypes);
			}
			Psd2UIFormConverter createdConverter = CreateConverterRoot(fileNameWithoutExtension);
			if (createdConverter == null)
			{
				Debug.LogError("解析PSD失败: 无法创建根节点组件 Psd2UIFormConverter");
				return false;
			}
			createdConverter.psdAssetChangeTime = GetAssetModificationStamp(documentAssetPath);
			createdConverter.ConfigurePsdSourceAsset((string)documentAssetPath);
			if (!RebuildLayerHierarchy(documentAssetPath, createdConverter, preserveLayerUiTypes))
			{
				if (createdTemporaryRoot)
				{
					UnityEngine.Object.DestroyImmediate(createdConverter.gameObject);
				}
				return false;
			}
			createdConverter.gameObject.name = Path.GetFileNameWithoutExtension(converterPrefabPath);
			PrefabUtility.SaveAsPrefabAsset(createdConverter.gameObject, converterPrefabPath, out var success);
			if (createdTemporaryRoot)
			{
				UnityEngine.Object.DestroyImmediate(createdConverter.gameObject);
			}
			AssetDatabase.Refresh();
			string openPrefabAssetPath = StageUtility.GetCurrentStage()?.assetPath;
			bool prefabAlreadyOpen = !string.IsNullOrWhiteSpace(openPrefabAssetPath) && AssetDatabase.GUIDFromAssetPath(openPrefabAssetPath) == AssetDatabase.GUIDFromAssetPath(converterPrefabPath);
			if (success && !prefabAlreadyOpen)
			{
				OpenPrefabStageFromAssetPath(converterPrefabPath);
			}
			return success;
		}
		Debug.LogError("Error: 源文档不存在:" + (string)documentAssetPath);
		return false;
	}

	private static bool RebuildLayerHierarchy(object documentAssetPath, object targetConverter, bool preserveLayerUiTypes = false)
	{
		EditorUtility.DisplayProgressBar("解析PSD", "正在解析" + (string)documentAssetPath, 0f);
		Dictionary<string, LayerUiTypeSnapshot> savedLayerUiTypes = (preserveLayerUiTypes ? ((Psd2UIFormConverter)targetConverter).CaptureLayerUiTypes() : null);
		try
		{
			using (PsdDocument loadedPsdDocument = PsdDocument.Create((string)documentAssetPath))
			{
				PsdLayer[] rootLayers = loadedPsdDocument.Childs ?? Array.Empty<PsdLayer>();
				if (rootLayers.Length == 0)
				{
					Debug.LogError("解析PSD失败: PSD未包含可解析的图层树。文件: " + (string)documentAssetPath);
					return false;
				}
				for (int num = ((Component)targetConverter).transform.childCount - 1; num >= 0; num--)
				{
					UnityEngine.Object.DestroyImmediate(((Component)targetConverter).transform.GetChild(num).gameObject);
				}
				int totalLayerCount = loadedPsdDocument.CountDocumentLayers();
				int processedLayerCount = 0;
				int nextLayerRecordIndex = 0;
				foreach (PsdLayer rootLayer in rootLayers)
				{
					if (rootLayer != null)
					{
						CreateLayerHierarchyRecursive(rootLayer, ((Component)targetConverter).transform, ref nextLayerRecordIndex, ref processedLayerCount, totalLayerCount);
					}
				}
				((Psd2UIFormConverter)targetConverter).RefreshLargeDocumentPreview(loadedPsdDocument, (string)documentAssetPath);
			}
			((Psd2UIFormConverter)targetConverter).psdAssetChangeTime = GetAssetModificationStamp(documentAssetPath);
			if (((Psd2UIFormConverter)targetConverter)._document != null)
			{
				((Psd2UIFormConverter)targetConverter)._document.Dispose();
				((Psd2UIFormConverter)targetConverter)._document = null;
			}
			((Psd2UIFormConverter)targetConverter).ReloadDocumentAndRebindLayers();
			if (preserveLayerUiTypes)
			{
				((Psd2UIFormConverter)targetConverter).RestoreLayerUiTypes(savedLayerUiTypes);
			}
			PsdLayerNode[] rebuiltLayerNodes = ((Component)targetConverter).GetComponentsInChildren<PsdLayerNode>(includeInactive: true);
			for (int j = 0; j < rebuiltLayerNodes.Length; j++)
			{
				rebuiltLayerNodes[j].RefreshUiHelperBindings();
			}
			EditorUtility.SetDirty(((Component)targetConverter).gameObject);
			return true;
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
			return false;
		}
		finally
		{
			EditorUtility.ClearProgressBar();
		}
	}

	private void ConfigurePsdSourceAsset(string documentAssetPath)
	{
		psdAssetPath = NormalizeProjectAssetPath(documentAssetPath);
		psdAsset = LoadPsdSpriteAsset(psdAssetPath);
		previewSprite = ((psdAsset != null || !IsLargePhotoshopDocumentPath(psdAssetPath)) ? null : LoadDocumentPreviewSprite(psdAssetPath));
		if (string.IsNullOrWhiteSpace(ScriptableSingleton<Psd2UIFormSettings>.Instance.UIImagesOutputDir))
		{
			ScriptableSingleton<Psd2UIFormSettings>.Instance.UIImagesOutputDir = Path.GetDirectoryName(psdAssetPath);
		}
		if (string.IsNullOrWhiteSpace(uiFormName))
		{
			uiFormName = ((psdAsset != null) ? psdAsset.name : Path.GetFileNameWithoutExtension(psdAssetPath));
		}
	}

	private static string GetUiFormEditorPrefabPath(object documentAssetPath)
	{
		return Path.Combine(Path.GetDirectoryName((string)documentAssetPath), Path.GetFileNameWithoutExtension((string)documentAssetPath) + "_UIFormEditor.prefab");
	}

	private static string NormalizeProjectAssetPath(object documentAssetPath)
	{
		if (string.IsNullOrWhiteSpace((string)documentAssetPath))
		{
			return null;
		}
		string text = ((string)documentAssetPath).Replace("\\", "/").Trim();
		if (Path.IsPathRooted(text))
		{
			text = RelativePathUtilities.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, text).Replace("\\", "/");
		}
		return text;
	}

	private static bool IsPhotoshopDocumentPath(object documentAssetPath)
	{
		if (string.IsNullOrWhiteSpace((string)documentAssetPath))
		{
			return false;
		}
		string extension = Path.GetExtension((string)documentAssetPath);
		if (extension.Equals(".psd", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		return extension.Equals(".psb", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsLargePhotoshopDocumentPath(object documentAssetPath)
	{
		if (!string.IsNullOrWhiteSpace((string)documentAssetPath))
		{
			return Path.GetExtension((string)documentAssetPath).Equals(".psb", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static Sprite LoadPsdSpriteAsset(object documentAssetPath)
	{
		if (!string.IsNullOrWhiteSpace((string)documentAssetPath))
		{
			Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>((string)documentAssetPath);
			if (!(sprite != null))
			{
				UnityEngine.Object[] array = AssetDatabase.LoadAllAssetsAtPath((string)documentAssetPath);
				if (array != null)
				{
					UnityEngine.Object[] array2 = array;
					for (int i = 0; i < array2.Length; i++)
					{
						if (array2[i] is Sprite result)
						{
							return result;
						}
					}
					return null;
				}
				return null;
			}
			return sprite;
		}
		return null;
	}

	private static string GetDocumentPreviewPngPath(object documentAssetPath)
	{
		if (string.IsNullOrWhiteSpace((string)documentAssetPath))
		{
			return null;
		}
		string directoryName = Path.GetDirectoryName((string)documentAssetPath);
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension((string)documentAssetPath);
		if (!string.IsNullOrWhiteSpace(directoryName) && !string.IsNullOrWhiteSpace(fileNameWithoutExtension))
		{
			return Path.Combine(directoryName, fileNameWithoutExtension + "_Preview.png").Replace("\\", "/");
		}
		return null;
	}

	private static string BuildPreviewImporterStamp(object documentAssetPath, object sourceWriteTimestamp)
	{
		return "Psd2UIFormPreview:v2:" + (string)sourceWriteTimestamp + ":" + (string)documentAssetPath;
	}

	private static string GetAbsoluteProjectAssetPath(object documentAssetPath)
	{
		if (!string.IsNullOrWhiteSpace((string)documentAssetPath))
		{
			string text = NormalizeProjectAssetPath(documentAssetPath);
			if (!string.IsNullOrWhiteSpace(text))
			{
				if (!Path.IsPathRooted(text))
				{
					return Path.Combine(Directory.GetParent(Application.dataPath).FullName, text.Replace("/", "\\"));
				}
				return text;
			}
			return null;
		}
		return null;
	}

	private static Sprite LoadDocumentPreviewSprite(object documentAssetPath)
	{
		return PsdLayerNode.LoadSpriteAsset(GetDocumentPreviewPngPath(documentAssetPath));
	}

	private static bool IsPreviewImporterStampCurrent(object previewAssetPath, object documentAssetPath, object sourceWriteTimestamp)
	{
		TextureImporter textureImporter = AssetImporter.GetAtPath((string)previewAssetPath) as TextureImporter;
		if (!(textureImporter == null))
		{
			return string.Equals(textureImporter.userData, BuildPreviewImporterStamp(documentAssetPath, sourceWriteTimestamp), StringComparison.Ordinal);
		}
		return false;
	}

	private static byte[] RenderDocumentPreviewPng(object document)
	{
		if (document == null)
		{
			return null;
		}
		List<RenderedPreviewLayer> renderedLayers = CollectRenderedPreviewLayers(document);
		CalculatePreviewCanvasBounds(document, renderedLayers, out var canvasLeft, out var canvasTop, out var canvasWidth, out var canvasHeight);
		bool requiresHighBitDepth = false;
		for (int i = 0; i < renderedLayers.Count; i++)
		{
			PsdRenderedImage renderedImage = ((renderedLayers[i] != null) ? renderedLayers[i].RenderedImage : null);
			if (renderedImage != null && renderedImage.IsHighBitDepth)
			{
				requiresHighBitDepth = true;
				break;
			}
		}
		Texture2D compositeTexture = null;
		try
		{
			compositeTexture = CompositePreviewTexture(renderedLayers, canvasLeft, canvasTop, canvasWidth, canvasHeight, requiresHighBitDepth);
			return PsdTextureAssetUtility.EncodePng(compositeTexture);
		}
		finally
		{
			if (compositeTexture != null)
			{
				UnityEngine.Object.DestroyImmediate(compositeTexture);
			}
		}
	}

	private static List<RenderedPreviewLayer> CollectRenderedPreviewLayers(object document)
	{
		List<RenderedPreviewLayer> renderedLayers = new List<RenderedPreviewLayer>();
		PsdLayer[] childs = ((PsdDocument)document).Childs;
		if (childs != null && childs.Length != 0)
		{
			foreach (PsdLayer childLayer in childs)
			{
				if (childLayer != null && childLayer.IsVisible)
				{
					PsdRenderedImage renderedImage = childLayer.Render();
					if (renderedImage != null && !renderedImage.IsEmpty)
					{
						renderedLayers.Add(new RenderedPreviewLayer
						{
							Layer = childLayer,
							RenderedImage = renderedImage
						});
					}
				}
			}
			return renderedLayers;
		}
		return renderedLayers;
	}

	private static void CalculatePreviewCanvasBounds(object document, List<RenderedPreviewLayer> renderedLayers, out int canvasLeft, out int canvasTop, out int canvasWidth, out int canvasHeight)
	{
		int minimumLeft = 0;
		int minimumTop = 0;
		int maximumRight = ((PsdDocument)document)?.Width ?? 0;
		int maximumBottom = ((PsdDocument)document)?.Height ?? 0;
		if (renderedLayers != null)
		{
			for (int i = 0; i < renderedLayers.Count; i++)
			{
				PsdRenderedImage renderedImage = renderedLayers[i]?.RenderedImage;
				if (renderedImage != null && !renderedImage.IsEmpty)
				{
					if (renderedImage.Left < minimumLeft)
					{
						minimumLeft = renderedImage.Left;
					}
					if (renderedImage.Top < minimumTop)
					{
						minimumTop = renderedImage.Top;
					}
					if (renderedImage.Right > maximumRight)
					{
						maximumRight = renderedImage.Right;
					}
					if (renderedImage.Bottom > maximumBottom)
					{
						maximumBottom = renderedImage.Bottom;
					}
				}
			}
		}
		int documentWidth = Math.Max(0, ((PsdDocument)document)?.Width ?? 0);
		int documentHeight = Math.Max(0, ((PsdDocument)document)?.Height ?? 0);
		int horizontalPadding = Math.Max(Math.Max(0, -minimumLeft), Math.Max(0, maximumRight - documentWidth));
		int verticalPadding = Math.Max(Math.Max(0, -minimumTop), Math.Max(0, maximumBottom - documentHeight));
		canvasLeft = -horizontalPadding;
		canvasTop = -verticalPadding;
		canvasWidth = documentWidth + (horizontalPadding << 1);
		canvasHeight = documentHeight + (verticalPadding << 1);
	}

	private static Texture2D CompositePreviewTexture(List<RenderedPreviewLayer> renderedLayers, int canvasLeft, int canvasTop, int canvasWidth, int canvasHeight, bool useHighBitDepth)
	{
		canvasWidth = Math.Max(1, canvasWidth);
		canvasHeight = Math.Max(1, canvasHeight);
		if (useHighBitDepth)
		{
			ushort[] highPrecisionCanvas = new ushort[canvasWidth * canvasHeight * 4];
			ushort[] highPrecisionClippingMask = null;
			if (renderedLayers != null)
			{
				for (int i = 0; i < renderedLayers.Count; i++)
				{
					RenderedPreviewLayer highPrecisionLayerEntry = renderedLayers[i];
					PsdRenderedImage highPrecisionLayerImage = highPrecisionLayerEntry?.RenderedImage;
					if (highPrecisionLayerImage != null && !highPrecisionLayerImage.IsEmpty)
					{
						bool isHighPrecisionClippingLayer = highPrecisionLayerEntry.Layer != null && highPrecisionLayerEntry.Layer.IsClipping;
						CompositePreviewLayerRgba64(highPrecisionCanvas, canvasLeft, canvasTop, canvasWidth, canvasHeight, highPrecisionLayerImage, isHighPrecisionClippingLayer ? highPrecisionClippingMask : null);
						if (!isHighPrecisionClippingLayer)
						{
							highPrecisionClippingMask = BuildClippingAlphaMask16(highPrecisionLayerImage, canvasLeft, canvasTop, canvasWidth, canvasHeight);
						}
					}
				}
			}
			byte[] highPrecisionTextureBytes = new byte[highPrecisionCanvas.Length * 2];
			Buffer.BlockCopy(highPrecisionCanvas, 0, highPrecisionTextureBytes, 0, highPrecisionTextureBytes.Length);
			Texture2D highPrecisionTexture = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA64, mipChain: false, linear: false);
			highPrecisionTexture.LoadRawTextureData(highPrecisionTextureBytes);
			highPrecisionTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			return highPrecisionTexture;
		}
		byte[] byteCanvas = new byte[canvasWidth * canvasHeight * 4];
		byte[] byteClippingMask = null;
		if (renderedLayers != null)
		{
			for (int j = 0; j < renderedLayers.Count; j++)
			{
				RenderedPreviewLayer byteLayerEntry = renderedLayers[j];
				PsdRenderedImage byteLayerImage = byteLayerEntry?.RenderedImage;
				if (byteLayerImage != null && !byteLayerImage.IsEmpty)
				{
					bool isByteClippingLayer = byteLayerEntry.Layer != null && byteLayerEntry.Layer.IsClipping;
					CompositePreviewLayerRgba32(byteCanvas, canvasLeft, canvasTop, canvasWidth, canvasHeight, byteLayerImage, isByteClippingLayer ? byteClippingMask : null);
					if (!isByteClippingLayer)
					{
						byteClippingMask = BuildClippingAlphaMask8(byteLayerImage, canvasLeft, canvasTop, canvasWidth, canvasHeight);
					}
				}
			}
		}
		Texture2D byteTexture = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, mipChain: false, linear: false);
		byteTexture.LoadRawTextureData(byteCanvas);
		byteTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
		return byteTexture;
	}

	private static void CompositePreviewLayerRgba32(object destinationPixels, int canvasLeft, int canvasTop, int canvasWidth, int canvasHeight, object renderedLayer, object clippingMask)
	{
		if (destinationPixels == null || renderedLayer == null || ((PsdRenderedImage)renderedLayer).IsEmpty)
		{
			return;
		}
		byte[] rgba = ((PsdRenderedImage)renderedLayer).Rgba32;
		int num = ((PsdRenderedImage)renderedLayer).Left - canvasLeft;
		int num2 = ((PsdRenderedImage)renderedLayer).Top - canvasTop;
		for (int i = 0; i < ((PsdRenderedImage)renderedLayer).Height; i++)
		{
			int num3 = num2 + i;
			if (num3 < 0 || num3 >= canvasHeight)
			{
				continue;
			}
			for (int j = 0; j < ((PsdRenderedImage)renderedLayer).Width; j++)
			{
				int num4 = num + j;
				if (num4 < 0 || num4 >= canvasWidth)
				{
					continue;
				}
				int num5 = GetBottomUpRgbaOffset(((PsdRenderedImage)renderedLayer).Width, ((PsdRenderedImage)renderedLayer).Height, j, i);
				byte b = rgba[num5 + 3];
				if (b <= 0)
				{
					continue;
				}
				if (clippingMask != null)
				{
					b = PsdLayerRenderer.MultiplyAlpha(b, ((byte[])clippingMask)[num3 * canvasWidth + num4]);
					if (b <= 0)
					{
						continue;
					}
				}
				BlendSourceOverPixel8(destinationPixels, canvasWidth, canvasHeight, num4, num3, rgba[num5], rgba[num5 + 1], rgba[num5 + 2], b);
			}
		}
	}

	private static void CompositePreviewLayerRgba64(object destinationPixels, int canvasLeft, int canvasTop, int canvasWidth, int canvasHeight, object renderedLayer, object clippingMask)
	{
		if (destinationPixels == null || renderedLayer == null || ((PsdRenderedImage)renderedLayer).IsEmpty)
		{
			return;
		}
		ushort[] rgba = ((PsdRenderedImage)renderedLayer).Rgba64;
		int num = ((PsdRenderedImage)renderedLayer).Left - canvasLeft;
		int num2 = ((PsdRenderedImage)renderedLayer).Top - canvasTop;
		for (int i = 0; i < ((PsdRenderedImage)renderedLayer).Height; i++)
		{
			int num3 = num2 + i;
			if (num3 < 0 || num3 >= canvasHeight)
			{
				continue;
			}
			for (int j = 0; j < ((PsdRenderedImage)renderedLayer).Width; j++)
			{
				int num4 = num + j;
				if (num4 < 0 || num4 >= canvasWidth)
				{
					continue;
				}
				int num5 = GetBottomUpRgbaOffset(((PsdRenderedImage)renderedLayer).Width, ((PsdRenderedImage)renderedLayer).Height, j, i);
				ushort num6 = rgba[num5 + 3];
				if (num6 <= 0)
				{
					continue;
				}
				if (clippingMask != null)
				{
					num6 = PsdLayerRenderer.MultiplyAlpha(num6, ((ushort[])clippingMask)[num3 * canvasWidth + num4]);
					if (num6 <= 0)
					{
						continue;
					}
				}
				BlendSourceOverPixel16(destinationPixels, canvasWidth, canvasHeight, num4, num3, rgba[num5], rgba[num5 + 1], rgba[num5 + 2], num6);
			}
		}
	}

	private static byte[] BuildClippingAlphaMask8(object renderedLayer, int canvasLeft, int canvasTop, int canvasWidth, int canvasHeight)
	{
		byte[] array = new byte[canvasWidth * canvasHeight];
		if (renderedLayer != null && !((PsdRenderedImage)renderedLayer).IsEmpty)
		{
			byte[] rgba = ((PsdRenderedImage)renderedLayer).Rgba32;
			int num = ((PsdRenderedImage)renderedLayer).Left - canvasLeft;
			int num2 = ((PsdRenderedImage)renderedLayer).Top - canvasTop;
			for (int i = 0; i < ((PsdRenderedImage)renderedLayer).Height; i++)
			{
				int num3 = num2 + i;
				if (num3 < 0 || num3 >= canvasHeight)
				{
					continue;
				}
				for (int j = 0; j < ((PsdRenderedImage)renderedLayer).Width; j++)
				{
					int num4 = num + j;
					if (num4 >= 0 && num4 < canvasWidth)
					{
						int num5 = GetBottomUpRgbaOffset(((PsdRenderedImage)renderedLayer).Width, ((PsdRenderedImage)renderedLayer).Height, j, i);
						array[num3 * canvasWidth + num4] = rgba[num5 + 3];
					}
				}
			}
			return array;
		}
		return array;
	}

	private static ushort[] BuildClippingAlphaMask16(object renderedLayer, int canvasLeft, int canvasTop, int canvasWidth, int canvasHeight)
	{
		ushort[] array = new ushort[canvasWidth * canvasHeight];
		if (renderedLayer != null && !((PsdRenderedImage)renderedLayer).IsEmpty)
		{
			ushort[] rgba = ((PsdRenderedImage)renderedLayer).Rgba64;
			int num = ((PsdRenderedImage)renderedLayer).Left - canvasLeft;
			int num2 = ((PsdRenderedImage)renderedLayer).Top - canvasTop;
			for (int i = 0; i < ((PsdRenderedImage)renderedLayer).Height; i++)
			{
				int num3 = num2 + i;
				if (num3 < 0 || num3 >= canvasHeight)
				{
					continue;
				}
				for (int j = 0; j < ((PsdRenderedImage)renderedLayer).Width; j++)
				{
					int num4 = num + j;
					if (num4 >= 0 && num4 < canvasWidth)
					{
						int num5 = GetBottomUpRgbaOffset(((PsdRenderedImage)renderedLayer).Width, ((PsdRenderedImage)renderedLayer).Height, j, i);
						array[num3 * canvasWidth + num4] = rgba[num5 + 3];
					}
				}
			}
			return array;
		}
		return array;
	}

	private static void BlendSourceOverPixel8(object destinationPixels, int canvasWidth, int canvasHeight, int pixelX, int pixelY, byte sourceRed, byte sourceGreen, byte sourceBlue, byte sourceAlpha)
	{
		int num = GetBottomUpRgbaOffset(canvasWidth, canvasHeight, pixelX, pixelY);
		byte b = ((byte[])destinationPixels)[num];
		byte b2 = ((byte[])destinationPixels)[num + 1];
		byte b3 = ((byte[])destinationPixels)[num + 2];
		byte num2 = ((byte[])destinationPixels)[num + 3];
		float num3 = (float)(int)sourceAlpha / 255f;
		float num4 = (float)(int)num2 / 255f;
		float num5 = num3 + num4 * (1f - num3);
		if (num5 <= 0f)
		{
			((sbyte[])destinationPixels)[num] = 0;
			((sbyte[])destinationPixels)[num + 1] = 0;
			((sbyte[])destinationPixels)[num + 2] = 0;
			((sbyte[])destinationPixels)[num + 3] = 0;
		}
		else
		{
			float num6 = (float)(int)sourceRed / 255f * num3 + (float)(int)b / 255f * num4 * (1f - num3);
			float num7 = (float)(int)sourceGreen / 255f * num3 + (float)(int)b2 / 255f * num4 * (1f - num3);
			float num8 = (float)(int)sourceBlue / 255f * num3 + (float)(int)b3 / 255f * num4 * (1f - num3);
			((sbyte[])destinationPixels)[num] = (sbyte)QuantizeNormalizedByte(num6 / num5);
			((sbyte[])destinationPixels)[num + 1] = (sbyte)QuantizeNormalizedByte(num7 / num5);
			((sbyte[])destinationPixels)[num + 2] = (sbyte)QuantizeNormalizedByte(num8 / num5);
			((sbyte[])destinationPixels)[num + 3] = (sbyte)QuantizeNormalizedByte(num5);
		}
	}

	private static void BlendSourceOverPixel16(object destinationPixels, int canvasWidth, int canvasHeight, int pixelX, int pixelY, ushort sourceRed, ushort sourceGreen, ushort sourceBlue, ushort sourceAlpha)
	{
		int num = GetBottomUpRgbaOffset(canvasWidth, canvasHeight, pixelX, pixelY);
		ushort num2 = ((ushort[])destinationPixels)[num];
		ushort num3 = ((ushort[])destinationPixels)[num + 1];
		ushort num4 = ((ushort[])destinationPixels)[num + 2];
		ushort num5 = ((ushort[])destinationPixels)[num + 3];
		float num6 = (float)(int)sourceAlpha / 65535f;
		float num7 = (float)(int)num5 / 65535f;
		float num8 = num6 + num7 * (1f - num6);
		if (num8 <= 0f)
		{
			((short[])destinationPixels)[num] = 0;
			((short[])destinationPixels)[num + 1] = 0;
			((short[])destinationPixels)[num + 2] = 0;
			((short[])destinationPixels)[num + 3] = 0;
		}
		else
		{
			float num9 = (float)(int)sourceRed / 65535f * num6 + (float)(int)num2 / 65535f * num7 * (1f - num6);
			float num10 = (float)(int)sourceGreen / 65535f * num6 + (float)(int)num3 / 65535f * num7 * (1f - num6);
			float num11 = (float)(int)sourceBlue / 65535f * num6 + (float)(int)num4 / 65535f * num7 * (1f - num6);
			((short[])destinationPixels)[num] = (short)QuantizeNormalizedUInt16(num9 / num8);
			((short[])destinationPixels)[num + 1] = (short)QuantizeNormalizedUInt16(num10 / num8);
			((short[])destinationPixels)[num + 2] = (short)QuantizeNormalizedUInt16(num11 / num8);
			((short[])destinationPixels)[num + 3] = (short)QuantizeNormalizedUInt16(num8);
		}
	}

	private static int GetBottomUpRgbaOffset(int imageWidth, int imageHeight, int pixelX, int pixelY)
	{
		return ((imageHeight - 1 - pixelY) * imageWidth + pixelX) * 4;
	}

	private static byte QuantizeNormalizedByte(float normalizedChannel)
	{
		return (byte)Mathf.Clamp(Mathf.RoundToInt(normalizedChannel * 255f), 0, 255);
	}

	private static ushort QuantizeNormalizedUInt16(float normalizedChannel)
	{
		return (ushort)Mathf.Clamp(Mathf.RoundToInt(normalizedChannel * 65535f), 0, 65535);
	}

	private static Sprite EnsureDocumentPreviewSprite(object documentAssetPath, PsdDocument loadedDocument = null)
	{
		documentAssetPath = NormalizeProjectAssetPath(documentAssetPath);
		string sourcePhysicalPath = GetAbsoluteProjectAssetPath(documentAssetPath);
		if (!string.IsNullOrWhiteSpace((string)documentAssetPath) && !string.IsNullOrWhiteSpace(sourcePhysicalPath) && File.Exists(sourcePhysicalPath))
		{
			string sourceWriteTimestamp = GetAssetModificationStamp(documentAssetPath);
			string previewAssetPath = GetDocumentPreviewPngPath(documentAssetPath);
			if (!string.IsNullOrWhiteSpace(previewAssetPath))
			{
				string previewPhysicalPath = GetAbsoluteProjectAssetPath(previewAssetPath);
				if (!string.IsNullOrWhiteSpace(previewPhysicalPath) && File.Exists(previewPhysicalPath) && IsPreviewImporterStampCurrent(previewAssetPath, documentAssetPath, sourceWriteTimestamp))
				{
					return PsdLayerNode.LoadSpriteAsset(previewAssetPath);
				}
				bool ownsLoadedDocument = loadedDocument == null;
				try
				{
					if (loadedDocument == null)
					{
						loadedDocument = PsdDocument.Create((string)documentAssetPath);
					}
					byte[] previewPngBytes = RenderDocumentPreviewPng(loadedDocument);
					if (previewPngBytes != null && previewPngBytes.Length != 0)
					{
						if (!string.IsNullOrWhiteSpace(previewPhysicalPath))
						{
							File.WriteAllBytes(previewPhysicalPath, previewPngBytes);
							AssetDatabase.ImportAsset(previewAssetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
							TextureImporter previewTextureImporter = AssetImporter.GetAtPath(previewAssetPath) as TextureImporter;
							if (previewTextureImporter != null)
							{
								previewTextureImporter.textureType = TextureImporterType.Sprite;
								previewTextureImporter.spriteImportMode = SpriteImportMode.Single;
								previewTextureImporter.alphaSource = TextureImporterAlphaSource.FromInput;
								previewTextureImporter.alphaIsTransparency = true;
								previewTextureImporter.mipmapEnabled = false;
								previewTextureImporter.npotScale = TextureImporterNPOTScale.None;
								previewTextureImporter.textureCompression = TextureImporterCompression.Uncompressed;
								previewTextureImporter.wrapMode = TextureWrapMode.Clamp;
								previewTextureImporter.filterMode = FilterMode.Bilinear;
								previewTextureImporter.userData = BuildPreviewImporterStamp(documentAssetPath, sourceWriteTimestamp);
								previewTextureImporter.SaveAndReimport();
							}
							return PsdLayerNode.LoadSpriteAsset(previewAssetPath);
						}
						return null;
					}
					return null;
				}
				catch (Exception arg)
				{
					Debug.LogWarning($"生成源文档预览图失败: {documentAssetPath}\n{arg}");
					return null;
				}
				finally
				{
					if (ownsLoadedDocument)
					{
						loadedDocument?.Dispose();
					}
				}
			}
			return null;
		}
		return null;
	}

	private void RefreshLargeDocumentPreview(PsdDocument loadedDocument, string documentAssetPath = null)
	{
		documentAssetPath = ((!string.IsNullOrWhiteSpace(documentAssetPath)) ? NormalizeProjectAssetPath(documentAssetPath) : GetPsdAssetPath());
		Sprite sprite = previewSprite;
		if (!(psdAsset != null) && IsLargePhotoshopDocumentPath(documentAssetPath))
		{
			previewSprite = EnsureDocumentPreviewSprite(documentAssetPath, loadedDocument);
		}
		else
		{
			previewSprite = null;
		}
		if (sprite != previewSprite)
		{
			EditorUtility.SetDirty(this);
		}
	}

	private void RefreshPreviewSpriteRenderer()
	{
		(base.gameObject.GetComponent<SpriteRenderer>() ?? base.gameObject.AddComponent<SpriteRenderer>()).sprite = GetDisplaySprite();
	}

	private static Psd2UIFormConverter CreateConverterRoot(object rootObjectName)
	{
		GameObject gameObject = new GameObject((string)rootObjectName, typeof(RectTransform));
		gameObject.gameObject.tag = "EditorOnly";
		gameObject.transform.localPosition = Vector3.zero;
		gameObject.transform.localRotation = Quaternion.identity;
		gameObject.transform.localScale = Vector3.one;
		Psd2UIFormConverter psd2UIFormConverter = gameObject.AddComponent<Psd2UIFormConverter>();
		if (psd2UIFormConverter == null)
		{
			UnityEngine.Object.DestroyImmediate(gameObject);
		}
		return psd2UIFormConverter;
	}

	private static void CreateLayerHierarchyRecursive(object sourceLayer, object parentTransform, ref int layerRecordIndex, ref int processedLayerCount, int totalLayerCount)
	{
		if (sourceLayer == null || (UnityEngine.Object)parentTransform == null)
		{
			return;
		}
		processedLayerCount++;
		EditorUtility.DisplayProgressBar($"解析PSD({processedLayerCount}/{Mathf.Max(1, totalLayerCount)})", "正在解析图层:" + sourceLayer.GetLayerName(), (totalLayerCount > 0) ? ((float)processedLayerCount / (float)totalLayerCount) : 1f);
		int boundRecordIndex = ((!((PsdLayer)sourceLayer).IsGroup) ? layerRecordIndex : (layerRecordIndex + sourceLayer.CountLayerRecords() - 1));
		PsdLayerNode createdLayerNode = CreatePsdLayerNode(sourceLayer, boundRecordIndex);
		createdLayerNode.transform.SetParent((Transform)parentTransform);
		createdLayerNode.transform.localPosition = Vector3.zero;
		if (((PsdLayer)sourceLayer).Childs != null && ((PsdLayer)sourceLayer).Childs.Length != 0)
		{
			int nextChildRecordIndex = layerRecordIndex + 1;
			for (int i = 0; i < ((PsdLayer)sourceLayer).Childs.Length; i++)
			{
				PsdLayer childLayer = ((PsdLayer)sourceLayer).Childs[i];
				if (childLayer != null)
				{
					CreateLayerHierarchyRecursive(childLayer, createdLayerNode.transform, ref nextChildRecordIndex, ref processedLayerCount, totalLayerCount);
				}
			}
		}
		layerRecordIndex += sourceLayer.CountLayerRecords();
	}

	private static PsdLayerNode CreatePsdLayerNode(object sourceLayer, int layerRecordIndex)
	{
		string nodeObjectName = sourceLayer.GetLayerName();
		nodeObjectName = UGUIParser.Instance.BuildEditorLayerName(nodeObjectName, layerRecordIndex);
		GameObject nodeObject = new GameObject(nodeObjectName, typeof(RectTransform));
		nodeObject.gameObject.tag = "EditorOnly";
		nodeObject.transform.localPosition = Vector3.zero;
		nodeObject.transform.localRotation = Quaternion.identity;
		nodeObject.transform.localScale = Vector3.one;
		PsdLayerNode createdLayerNode = nodeObject.AddComponent<PsdLayerNode>();
		createdLayerNode.BindPsdLayerIndex = layerRecordIndex;
		InitializePsdLayerNode(createdLayerNode, sourceLayer);
		return createdLayerNode;
	}

	private static void InitializePsdLayerNode(object layerNode, object sourceLayer)
	{
		if (sourceLayer != null)
		{
			PsdLayerType sourceLayerType = sourceLayer.GetUiLayerType();
			((PsdLayerNode)layerNode).SetSourceLayerName(sourceLayer.GetLayerName());
			((PsdLayerNode)layerNode).SetBoundPsdLayer((PsdLayer)sourceLayer);
			if (UGUIParser.Instance.TryMatchLayerRuleAndRole((PsdLayerNode)layerNode, out var matchedParseRule, out var matchedUiType))
			{
				((PsdLayerNode)layerNode).SetUiTypeAndRole(matchedParseRule.UIType, matchedUiType, false);
			}
			bool isNativeTextLayer = sourceLayerType == PsdLayerType.TextLayer && ((PsdLayerNode)layerNode).UIType.ToString().EndsWith("Text") && ((PsdLayerNode)layerNode).UIType != GUIType.FillColor;
			bool isFillColorLayer = sourceLayerType == PsdLayerType.FillLayer || ((PsdLayerNode)layerNode).UIType == GUIType.FillColor;
			((PsdLayerNode)layerNode).markToExport = sourceLayerType != PsdLayerType.LayerGroup && !isNativeTextLayer && !isFillColorLayer;
			((Component)layerNode).gameObject.SetActive(((PsdLayer)sourceLayer).IsVisible);
			if (((PsdLayerNode)layerNode).HasImageReference() || ((PsdLayerNode)layerNode).HasPrefabReference())
			{
				((PsdLayerNode)layerNode).markToExport = false;
			}
		}
	}

	internal void ExportMarkedLayerImages()
	{
		RebuildImageReferenceIndex();
		IEnumerable<PsdLayerNode> exportableLayers = from node in GetComponentsInChildren<PsdLayerNode>()
			where node.ShouldExportImage()
			select node;
		string imagesOutputDirectory = GetUiImageOutputDirectory();
		if (!Directory.Exists(imagesOutputDirectory))
		{
			Directory.CreateDirectory(imagesOutputDirectory);
		}
		int exportedLayerCount = 0;
		int totalExportLayerCount = exportableLayers.Count();
		foreach (PsdLayerNode item in exportableLayers)
		{
			string exportedImagePath = item.ExportLayerImage();
			if (exportedImagePath == null)
			{
				Debug.LogWarning($"导出图层[name:{item.name}, layerIdx:{item.BindPsdLayerIndex}]图片失败!");
			}
			exportedLayerCount++;
			EditorUtility.DisplayProgressBar($"导出进度({exportedLayerCount}/{totalExportLayerCount})", "导出UI图片:" + exportedImagePath, (float)exportedLayerCount / (float)totalExportLayerCount);
		}
		EditorUtility.ClearProgressBar();
		AssetDatabase.Refresh();
	}

	internal void ExportUiForm(PsdLayerNode selectedRootLayer = null)
	{
		if (ScriptableSingleton<Psd2UIFormSettings>.Instance.UseUIFormOutputDir && string.IsNullOrWhiteSpace(ScriptableSingleton<Psd2UIFormSettings>.Instance.UIFormOutputDir))
		{
			Debug.LogError("生成UIForm失败! UIForm导出路径为空:" + ScriptableSingleton<Psd2UIFormSettings>.Instance.UIFormOutputDir);
			return;
		}
		Transform transform = base.transform;
		if (selectedRootLayer != null)
		{
			transform = selectedRootLayer.transform;
		}
		if (!ScriptableSingleton<Psd2UIFormSettings>.Instance.UseUIFormOutputDir)
		{
			string folder = (string.IsNullOrWhiteSpace(ScriptableSingleton<Psd2UIFormSettings>.Instance.LastUIFormOutputDir) ? "Assets" : ScriptableSingleton<Psd2UIFormSettings>.Instance.LastUIFormOutputDir);
			string text = EditorUtility.SaveFolderPanel("保存目录", folder, null);
			if (!string.IsNullOrWhiteSpace(text))
			{
				if (!text.StartsWith("Assets/"))
				{
					text = RelativePathUtilities.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, text);
				}
				ScriptableSingleton<Psd2UIFormSettings>.Instance.LastUIFormOutputDir = text;
				GenerateAndSaveUiFormPrefab(transform, text);
			}
		}
		else
		{
			GenerateAndSaveUiFormPrefab(transform, ScriptableSingleton<Psd2UIFormSettings>.Instance.UIFormOutputDir);
		}
	}

	internal void ExportReusablePrefab(PsdLayerNode targetLayer)
	{
		if (targetLayer == null)
		{
			Debug.LogError("导出prefab失败: 目标节点为空");
		}
		else if (!(UGUIParser.Instance == null))
		{
			if (string.IsNullOrWhiteSpace(UGUIParser.Instance.GetSharedPrefabOutputDirectory()))
			{
				Debug.LogError("导出prefab失败! 复用prefab导出路径为空:" + UGUIParser.Instance.GetSharedPrefabOutputDirectory());
				return;
			}
			string text = NormalizeAssetsDirectory(UGUIParser.Instance.GetSharedPrefabOutputDirectory());
			if (!string.IsNullOrWhiteSpace(text))
			{
				if (!Directory.Exists(text))
				{
					try
					{
						Directory.CreateDirectory(text);
						AssetDatabase.Refresh();
					}
					catch (Exception ex)
					{
						Debug.LogError("创建复用prefab导出目录失败:" + ex.Message);
						return;
					}
				}
				string text2 = PsdAssetNameUtilities.SanitizeLayerAssetName(targetLayer.name);
				if (string.IsNullOrWhiteSpace(text2))
				{
					text2 = "PsdLayerPrefab";
				}
				string text3 = Path.Combine(text, text2 + ".prefab").Replace("\\", "/");
				if (!File.Exists(text3) || EditorUtility.DisplayDialog("警告", "prefab文件已存在, 是否覆盖:" + text3, "覆盖生成", "取消生成"))
				{
					PsdLayerNode component = targetLayer.GetComponent<PsdLayerNode>();
					bool flag = component != null && component.HasPrefabReference() && string.Equals(component.GetPrefabReferenceKey(), text2, StringComparison.OrdinalIgnoreCase);
					GameObject gameObject = GenerateReusablePrefab(targetLayer, text3, text2, flag);
					if (gameObject != null)
					{
						Selection.activeGameObject = gameObject;
					}
				}
			}
			else
			{
				Debug.LogError("导出prefab失败! 复用prefab导出路径无效:" + UGUIParser.Instance.GetSharedPrefabOutputDirectory());
			}
		}
		else
		{
			Debug.LogError("导出prefab失败: UGUIParser配置未找到");
		}
	}

	private bool GenerateAndSaveUiFormPrefab(Transform sourceRootTransform, string outputDirectory)
	{
		_003C_003Ec__DisplayClass98_0 CS_0024_003C_003E8__locals15 = new _003C_003Ec__DisplayClass98_0();
		if (!string.IsNullOrWhiteSpace(outputDirectory) && !Directory.Exists(outputDirectory))
		{
			try
			{
				Directory.CreateDirectory(outputDirectory);
				AssetDatabase.Refresh();
			}
			catch (Exception ex)
			{
				Debug.LogError("导出UI prefab失败:" + ex.Message);
				return false;
			}
		}
		if (!string.IsNullOrWhiteSpace(uiFormName))
		{
			string text = Path.Combine(outputDirectory, uiFormName + ".prefab");
			if (sourceRootTransform == base.transform && File.Exists(text) && !EditorUtility.DisplayDialog("警告", "prefab文件已存在, 是否覆盖:" + text, "覆盖生成", "取消生成"))
			{
				return false;
			}
			PsdLayerNode[] componentsInChildren = sourceRootTransform.GetComponentsInChildren<PsdLayerNode>(includeInactive: true);
			CS_0024_003C_003E8__locals15.ExcludedPrefabReferenceRoots = new List<PsdLayerNode>();
			PsdLayerNode[] array = componentsInChildren.Where((PsdLayerNode node) => node != null && node.HasPrefabReference()).ToArray();
			PsdLayerNode component = sourceRootTransform.GetComponent<PsdLayerNode>();
			PsdLayerNode[] array2;
			if (component != null && component.HasPrefabReference())
			{
				CS_0024_003C_003E8__locals15.ExcludedPrefabReferenceRoots.Add(component);
			}
			else if (array.Length != 0)
			{
				array2 = array;
				foreach (PsdLayerNode psdLayerNode in array2)
				{
					if (psdLayerNode == null)
					{
						continue;
					}
					bool flag = false;
					Transform parent = psdLayerNode.transform.parent;
					while (parent != null && parent != sourceRootTransform)
					{
						PsdLayerNode component2 = parent.GetComponent<PsdLayerNode>();
						if (!(component2 != null) || !component2.HasPrefabReference())
						{
							parent = parent.parent;
							continue;
						}
						flag = true;
						break;
					}
					if (!flag)
					{
						CS_0024_003C_003E8__locals15.ExcludedPrefabReferenceRoots.Add(psdLayerNode);
					}
				}
			}
			PsdLayerNode[] array3 = componentsInChildren.Where((PsdLayerNode node) => node != null && !CS_0024_003C_003E8__locals15.IsWithinExcludedPrefabReference(node.transform)).ToArray();
			RebuildImageReferenceIndex();
			_referencedImageKeys.Clear();
			array2 = array3;
			foreach (PsdLayerNode psdLayerNode2 in array2)
			{
				if (psdLayerNode2.HasImageReference() && !string.IsNullOrEmpty(psdLayerNode2.GetImageReferenceKey()))
				{
					_referencedImageKeys.Add(psdLayerNode2.GetImageReferenceKey());
				}
			}
			ExportReferencedImages(array3);
			UIHelperBase[] array4 = CollectIndependentUiHelpers(sourceRootTransform);
			if (array4 != null && array4.Length != 0 && CS_0024_003C_003E8__locals15.ExcludedPrefabReferenceRoots.Count > 0)
			{
				for (int num2 = array4.Length - 1; num2 >= 0; num2--)
				{
					UIHelperBase uIHelperBase = array4[num2];
					if (!(uIHelperBase == null) && !(uIHelperBase.GetLayerNode() == null) && CS_0024_003C_003E8__locals15.IsWithinExcludedPrefabReference(uIHelperBase.transform))
					{
						ArrayUtility.RemoveAt(ref array4, num2);
					}
				}
			}
			if ((array4 == null || array4.Length < 1) && CS_0024_003C_003E8__locals15.ExcludedPrefabReferenceRoots.Count < 1)
			{
				return false;
			}
			GameObject gameObject = AssetDatabase.LoadAssetAtPath<GameObject>(text);
			GameObject gameObject2;
			if (gameObject == null)
			{
				if (UGUIParser.Instance.GetUiFormTemplate() == null)
				{
					Debug.LogError("生成UIForm失败: UIFormTemplate为空");
					return false;
				}
				gameObject2 = UnityEngine.Object.Instantiate(UGUIParser.Instance.GetUiFormTemplate(), Vector3.zero, Quaternion.identity);
				gameObject2.transform.localScale = Vector3.one;
			}
			else
			{
				gameObject2 = UnityEngine.Object.Instantiate(gameObject);
				RestoreGeneratedKeyComponents(text, gameObject2);
			}
			gameObject2.name = uiFormName;
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			CS_0024_003C_003E8__locals15.PreservedGeneratedKeyPrefix = null;
			if (sourceRootTransform != base.transform)
			{
				CS_0024_003C_003E8__locals15.PreservedGeneratedKeyPrefix = BuildGeneratedNodePath(sourceRootTransform.gameObject, base.transform);
				if (!string.IsNullOrEmpty(CS_0024_003C_003E8__locals15.PreservedGeneratedKeyPrefix))
				{
					CollectGeneratedIdentities(gameObject2.transform, hashSet, (PsdGeneratedKey generatedKey) => !IsGeneratedKeyWithinPrefix(generatedKey.Key, CS_0024_003C_003E8__locals15.PreservedGeneratedKeyPrefix));
				}
			}
			Vector3 vector = Vector3.zero;
			RectTransform component3 = gameObject2.GetComponent<RectTransform>();
			if (component3 != null)
			{
				vector = component3.anchoredPosition;
			}
			int num3 = 0;
			int num4 = array4.Length;
			UIHelperBase[] array5 = array4;
			foreach (UIHelperBase uIHelperBase2 in array5)
			{
				if (!(uIHelperBase2 == null) && !(uIHelperBase2.GetLayerNode() == null))
				{
					EditorUtility.DisplayProgressBar($"生成UIFrom:({num3++}/{num4})", "正在生成UI元素:" + uIHelperBase2.name, (float)num3 / (float)num4);
					string text2 = BuildGeneratedNodePath(uIHelperBase2.gameObject, base.transform);
					string[] array7;
					string[] array6 = GetParentContainerPaths(uIHelperBase2.gameObject, base.transform, out array7);
					GameObject gameObject3 = EnsureGeneratedContainerPath(gameObject2, array6, array7, hashSet);
					GameObject gameObject4 = FindReusableGeneratedUiObject(gameObject3.transform, uIHelperBase2, text2);
					GameObject gameObject5 = uIHelperBase2.CreateOrUpdateUiObject(gameObject4);
					if (!(gameObject5 == null))
					{
						AssignGeneratedNodeIdentity(gameObject5, text2, uIHelperBase2.GetLayerNode().UIType.ToString(), false, hashSet);
						gameObject5.transform.SetParent(gameObject3.transform, worldPositionStays: true);
						gameObject5.transform.position += vector;
						gameObject5.transform.localScale = Vector3.one;
					}
				}
			}
			if (CS_0024_003C_003E8__locals15.ExcludedPrefabReferenceRoots.Count > 0)
			{
				int num5 = 0;
				int count = CS_0024_003C_003E8__locals15.ExcludedPrefabReferenceRoots.Count;
				foreach (PsdLayerNode item in CS_0024_003C_003E8__locals15.ExcludedPrefabReferenceRoots)
				{
					if (item == null || !item.HasPrefabReference())
					{
						continue;
					}
					EditorUtility.DisplayProgressBar($"生成UIFrom-引用prefab:({num5++}/{count})", "正在实例化prefab:" + item.GetRawPrefabReference(), (float)num5 / (float)count);
					if (TryEnsureReferencedPrefab(item, out var gameObject6) && !(gameObject6 == null))
					{
						string text3 = BuildGeneratedNodePath(item.gameObject, base.transform);
						string[] array9;
						string[] array8 = GetParentContainerPaths(item.gameObject, base.transform, out array9);
						GameObject gameObject7 = EnsureGeneratedContainerPath(gameObject2, array8, array9, hashSet);
						GameObject gameObject8 = FindReusablePrefabReferenceObject(gameObject7.transform, item, text3, gameObject6);
						if (gameObject8 == null)
						{
							gameObject8 = PrefabUtility.InstantiatePrefab(gameObject6) as GameObject;
							if (gameObject8 == null)
							{
								gameObject8 = UnityEngine.Object.Instantiate(gameObject6);
							}
						}
						gameObject8.name = (string.IsNullOrEmpty(item.GetPrefabReferenceKey()) ? gameObject6.name : PsdLayerNode.GetAssetLeafName(item.GetPrefabReferenceKey(), gameObject6.name));
						gameObject8.transform.localRotation = Quaternion.identity;
						gameObject8.transform.localScale = Vector3.one;
						RectTransform component4 = gameObject8.GetComponent<RectTransform>();
						if (component4 != null)
						{
							UGUIParser.ApplyLayerRectToUiElement(item, component4);
						}
						else
						{
							Debug.LogWarning("引用prefab缺少RectTransform: " + gameObject6.name);
						}
						AssignGeneratedNodeIdentity(gameObject8, text3, "__PrefabRef", false, hashSet);
						gameObject8.transform.SetParent(gameObject7.transform, worldPositionStays: true);
						gameObject8.transform.position += vector;
						gameObject8.transform.localScale = Vector3.one;
					}
					else
					{
						Debug.LogWarning("引用prefab未找到且导出失败: " + item.GetRawPrefabReference());
					}
				}
			}
			RemoveUiStringKeyComponents(gameObject2);
			RemoveStaleGeneratedChildren(gameObject2.transform, hashSet);
			ResizeGeneratedContainersRecursive(gameObject2.transform);
			List<GeneratedNodeMetadataSnapshot> list = CaptureGeneratedNodeMetadata(gameObject2);
			RemoveGeneratedKeyComponents(gameObject2);
			gameObject2.name = Path.GetFileNameWithoutExtension(text);
			GameObject gameObject9 = PrefabUtility.SaveAsPrefabAsset(gameObject2, text);
			if (gameObject9 != null)
			{
				PersistGeneratedNodeMetadata(text, list, CS_0024_003C_003E8__locals15.PreservedGeneratedKeyPrefix);
				UnityEngine.Object.DestroyImmediate(gameObject2);
				Selection.activeGameObject = gameObject9;
			}
			EditorUtility.ClearProgressBar();
			return true;
		}
		Debug.LogError("导出UI Prefab失败: UI Form Name为空, 请填写UI Form Name.");
		return false;
	}

	private GameObject EnsureGeneratedContainerPath(GameObject P_0, string[] P_1, string[] P_2, HashSet<string> P_3)
	{
		GameObject gameObject = P_0;
		if (P_1 != null && P_2 != null)
		{
			for (int i = 0; i < P_1.Length; i++)
			{
				string text = P_1[i];
				string text2 = P_2[i];
				string text3 = PsdLayerNode.BuildGeneratedObjectName(text2);
				GameObject gameObject2 = FindGeneratedChildByKey(gameObject.transform, text, false);
				bool flag;
				if (!(flag = gameObject2 != null))
				{
					gameObject2 = FindGeneratedChildByIdentity(gameObject.transform, text, "__Container", true);
				}
				if (gameObject2 == null)
				{
					gameObject2 = FindUniqueUnmarkedChild(gameObject.transform, text3, true);
				}
				if (gameObject2 == null && !string.Equals(text2, text3, StringComparison.Ordinal))
				{
					gameObject2 = FindUniqueUnmarkedChild(gameObject.transform, text2, true);
				}
				if (gameObject2 == null)
				{
					gameObject2 = new GameObject(text3, typeof(RectTransform));
					gameObject2.layer = UnityEngine.LayerMask.NameToLayer("UI");
					gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
					gameObject2.transform.localPosition = Vector3.zero;
					gameObject2.transform.localRotation = Quaternion.identity;
					gameObject2.transform.localScale = Vector3.one;
				}
				gameObject2.name = text3;
				if (!flag)
				{
					AssignGeneratedNodeIdentity(gameObject2, text, "__Container", true, P_3);
				}
				gameObject = gameObject2;
			}
		}
		return gameObject;
	}

	private static string BuildGeneratedIdentity(object generatedKey, object typeKey, bool isContainer)
	{
		return (isContainer ? "C" : "N") + ":" + (string)typeKey + ":" + (string)generatedKey;
	}

	private static string GetLegacyMetadataSidecarPath(object prefabAssetPath)
	{
		if (!string.IsNullOrWhiteSpace((string)prefabAssetPath))
		{
			string directoryName = Path.GetDirectoryName((string)prefabAssetPath);
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension((string)prefabAssetPath);
			if (!string.IsNullOrWhiteSpace(directoryName) && !string.IsNullOrWhiteSpace(fileNameWithoutExtension))
			{
				return Path.Combine(directoryName, fileNameWithoutExtension + ".psd2uiform.generated.json").Replace("\\", "/");
			}
			return null;
		}
		return null;
	}

	private static string NormalizeMetadataAssetPath(object assetPath)
	{
		if (!string.IsNullOrWhiteSpace((string)assetPath))
		{
			return ((string)assetPath).Replace("\\", "/").Trim();
		}
		return null;
	}

	private static bool MetadataAssetPathsEqual(object firstAssetPath, object secondAssetPath)
	{
		string text = NormalizeMetadataAssetPath(firstAssetPath);
		string text2 = NormalizeMetadataAssetPath(secondAssetPath);
		if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(text2))
		{
			return string.Equals(text, text2, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private GeneratedMetadataSerializedEntry FindSerializedMetadataEntry(string prefabAssetPath)
	{
		if (generatedMetadataEntries != null && generatedMetadataEntries.Count >= 1)
		{
			for (int i = 0; i < generatedMetadataEntries.Count; i++)
			{
				GeneratedMetadataSerializedEntry generatedMetadataSerializedEntry = generatedMetadataEntries[i];
				if (generatedMetadataSerializedEntry != null && !string.IsNullOrWhiteSpace(generatedMetadataSerializedEntry.PrefabAssetPath) && MetadataAssetPathsEqual(generatedMetadataSerializedEntry.PrefabAssetPath, prefabAssetPath))
				{
					return generatedMetadataSerializedEntry;
				}
			}
			return null;
		}
		return null;
	}

	private static List<GeneratedMetadataEntry> CloneValidGeneratedMetadata(IEnumerable<GeneratedMetadataEntry> metadataEntries)
	{
		List<GeneratedMetadataEntry> list = new List<GeneratedMetadataEntry>();
		if (metadataEntries == null)
		{
			return list;
		}
		foreach (GeneratedMetadataEntry item in metadataEntries)
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

	private static List<GeneratedMetadataEntry> DeserializeGeneratedMetadata(object metadataJson)
	{
		if (string.IsNullOrWhiteSpace((string)metadataJson))
		{
			return new List<GeneratedMetadataEntry>();
		}
		try
		{
			return CloneValidGeneratedMetadata(JsonUtility.FromJson<GeneratedMetadataCollection>((string)metadataJson)?.Entries);
		}
		catch
		{
			return new List<GeneratedMetadataEntry>();
		}
	}

	private void NormalizeSerializedMetadata()
	{
		if (generatedMetadataEntries == null)
		{
			generatedMetadataEntries = new List<GeneratedMetadataSerializedEntry>();
			return;
		}
		Dictionary<string, GeneratedMetadataSerializedEntry> dictionary = new Dictionary<string, GeneratedMetadataSerializedEntry>(StringComparer.OrdinalIgnoreCase);
		for (int i = 0; i < generatedMetadataEntries.Count; i++)
		{
			GeneratedMetadataSerializedEntry generatedMetadataSerializedEntry = generatedMetadataEntries[i];
			string text = NormalizeMetadataAssetPath(generatedMetadataSerializedEntry?.PrefabAssetPath);
			if (!string.IsNullOrWhiteSpace(text))
			{
				List<GeneratedMetadataEntry> list = CloneValidGeneratedMetadata(generatedMetadataSerializedEntry.Entries);
				if (list.Count < 1)
				{
					list = DeserializeGeneratedMetadata(generatedMetadataSerializedEntry.Json);
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

	private void SaveConverterMetadataChanges()
	{
		EditorUtility.SetDirty(this);
		PrefabUtility.RecordPrefabInstancePropertyModifications(this);
		if (base.gameObject != null && base.gameObject.scene.IsValid())
		{
			EditorSceneManager.MarkSceneDirty(base.gameObject.scene);
		}
		AssetDatabase.SaveAssets();
	}

	internal string GetConverterAssetDescription()
	{
		string assetPath = AssetDatabase.GetAssetPath(base.gameObject);
		if (string.IsNullOrWhiteSpace(assetPath))
		{
			return "<Scene Object>";
		}
		return assetPath;
	}

	internal string[] GetMetadataPrefabPaths()
	{
		if (generatedMetadataEntries != null && generatedMetadataEntries.Count >= 1)
		{
			return (from item in generatedMetadataEntries
				where item != null && !string.IsNullOrWhiteSpace(item.PrefabAssetPath) && item.Entries != null && item.Entries.Count > 0
				select NormalizeMetadataAssetPath(item.PrefabAssetPath) into item
				where !string.IsNullOrWhiteSpace(item)
				select item).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy((string item) => item, StringComparer.OrdinalIgnoreCase).ToArray();
		}
		return Array.Empty<string>();
	}

	internal string SerializePrefabMetadata(string P_0)
	{
		GeneratedMetadataSerializedEntry generatedMetadataSerializedEntry = FindSerializedMetadataEntry(P_0);
		if (generatedMetadataSerializedEntry != null && generatedMetadataSerializedEntry.Entries != null && generatedMetadataSerializedEntry.Entries.Count >= 1)
		{
			return JsonUtility.ToJson(new GeneratedMetadataCollection
			{
				Entries = CloneValidGeneratedMetadata(generatedMetadataSerializedEntry.Entries)
			}, prettyPrint: true);
		}
		return string.Empty;
	}

	private List<GeneratedMetadataEntry> LoadGeneratedMetadata(string P_0)
	{
		GeneratedMetadataSerializedEntry generatedMetadataSerializedEntry = FindSerializedMetadataEntry(P_0);
		if (generatedMetadataSerializedEntry != null && generatedMetadataSerializedEntry.Entries != null && generatedMetadataSerializedEntry.Entries.Count > 0)
		{
			return CloneValidGeneratedMetadata(generatedMetadataSerializedEntry.Entries);
		}
		if (generatedMetadataSerializedEntry != null && !string.IsNullOrWhiteSpace(generatedMetadataSerializedEntry.Json))
		{
			List<GeneratedMetadataEntry> list = DeserializeGeneratedMetadata(generatedMetadataSerializedEntry.Json);
			if (list.Count > 0)
			{
				StoreGeneratedMetadata(P_0, list);
				return list;
			}
		}
		string text = GetLegacyMetadataSidecarPath(P_0);
		if (!string.IsNullOrWhiteSpace(text) && File.Exists(text))
		{
			string text2 = File.ReadAllText(text);
			if (!string.IsNullOrWhiteSpace(text2))
			{
				List<GeneratedMetadataEntry> list2 = DeserializeGeneratedMetadata(text2);
				if (list2.Count > 0)
				{
					StoreGeneratedMetadata(P_0, list2);
					return list2;
				}
			}
		}
		return null;
	}

	private void StoreGeneratedMetadata(string P_0, List<GeneratedMetadataEntry> P_1)
	{
		string text = NormalizeMetadataAssetPath(P_0);
		if (string.IsNullOrWhiteSpace(text))
		{
			Debug.LogWarning("保存生成节点元数据失败: target为空, target=" + P_0);
			return;
		}
		if (generatedMetadataEntries == null)
		{
			generatedMetadataEntries = new List<GeneratedMetadataSerializedEntry>();
		}
		GeneratedMetadataSerializedEntry generatedMetadataSerializedEntry = FindSerializedMetadataEntry(text);
		List<GeneratedMetadataEntry> list = CloneValidGeneratedMetadata(P_1);
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
		NormalizeSerializedMetadata();
		SaveConverterMetadataChanges();
		string text2 = GetLegacyMetadataSidecarPath(P_0);
		if (!string.IsNullOrWhiteSpace(text2) && File.Exists(text2))
		{
			AssetDatabase.DeleteAsset(text2);
		}
	}

	private List<GeneratedNodeMetadataSnapshot> CaptureGeneratedNodeMetadata(GameObject P_0)
	{
		List<GeneratedNodeMetadataSnapshot> list = new List<GeneratedNodeMetadataSnapshot>();
		if (P_0 == null)
		{
			return list;
		}
		PsdGeneratedKey[] componentsInChildren = P_0.GetComponentsInChildren<PsdGeneratedKey>(includeInactive: true);
		foreach (PsdGeneratedKey psdGeneratedKey in componentsInChildren)
		{
			if (!(psdGeneratedKey == null))
			{
				list.Add(new GeneratedNodeMetadataSnapshot
				{
					GameObject = psdGeneratedKey.gameObject,
					GeneratedKey = psdGeneratedKey.Key,
					TypeKey = psdGeneratedKey.GetTypeKey(),
					IsContainer = psdGeneratedKey.GetIsContainer()
				});
			}
		}
		return list;
	}

	private static void RemoveGeneratedKeyComponents(object P_0)
	{
		if ((UnityEngine.Object)P_0 == null)
		{
			return;
		}
		PsdGeneratedKey[] componentsInChildren = ((GameObject)P_0).GetComponentsInChildren<PsdGeneratedKey>(includeInactive: true);
		for (int num = componentsInChildren.Length - 1; num >= 0; num--)
		{
			if (componentsInChildren[num] != null)
			{
				UnityEngine.Object.DestroyImmediate(componentsInChildren[num]);
			}
		}
	}

	private void RestoreGeneratedKeyComponents(string P_0, GameObject P_1)
	{
		if (P_1 == null)
		{
			return;
		}
		List<GeneratedMetadataEntry> list = LoadGeneratedMetadata(P_0);
		if (list == null || list.Count < 1)
		{
			return;
		}
		try
		{
			Dictionary<string, GameObject> dictionary = new Dictionary<string, GameObject>(StringComparer.Ordinal);
			Transform[] componentsInChildren = P_1.GetComponentsInChildren<Transform>(includeInactive: true);
			foreach (Transform transform in componentsInChildren)
			{
				if (transform == null)
				{
					continue;
				}
				GameObject correspondingObjectFromSource = PrefabUtility.GetCorrespondingObjectFromSource(transform.gameObject);
				if (!(correspondingObjectFromSource == null))
				{
					string text = GlobalObjectId.GetGlobalObjectIdSlow(correspondingObjectFromSource).ToString();
					if (!string.IsNullOrEmpty(text))
					{
						dictionary[text] = transform.gameObject;
					}
				}
			}
			foreach (GeneratedMetadataEntry item in list)
			{
				if (item != null && !string.IsNullOrEmpty(item.GlobalObjectId) && dictionary.TryGetValue(item.GlobalObjectId, out var value) && !(value == null))
				{
					AssignGeneratedNodeIdentity(value, item.Key, item.TypeKey, item.IsContainer);
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("恢复生成节点元数据失败: " + P_0 + "\n" + ex.Message);
		}
	}

	private void PersistGeneratedNodeMetadata(string P_0, List<GeneratedNodeMetadataSnapshot> P_1, string P_2 = null)
	{
		try
		{
			if (P_1 != null && P_1.Count >= 1)
			{
				List<GeneratedMetadataEntry> list = new List<GeneratedMetadataEntry>();
				foreach (GeneratedNodeMetadataSnapshot item2 in P_1)
				{
					if (item2 == null || item2.GameObject == null)
					{
						continue;
					}
					GameObject correspondingObjectFromSource = PrefabUtility.GetCorrespondingObjectFromSource(item2.GameObject);
					if (!(correspondingObjectFromSource == null))
					{
						string text = GlobalObjectId.GetGlobalObjectIdSlow(correspondingObjectFromSource).ToString();
						if (!string.IsNullOrEmpty(text))
						{
							list.Add(new GeneratedMetadataEntry
							{
								GlobalObjectId = text,
								Key = item2.GeneratedKey,
								TypeKey = item2.TypeKey,
								IsContainer = item2.IsContainer
							});
						}
					}
				}
				if (list.Count < 1)
				{
					StoreGeneratedMetadata(P_0, null);
					return;
				}
				if (!string.IsNullOrEmpty(P_2))
				{
					List<GeneratedMetadataEntry> list2 = LoadGeneratedMetadata(P_0);
					if (list2 != null && list2.Count > 0)
					{
						HashSet<string> hashSet = new HashSet<string>(list.Select((GeneratedMetadataEntry entry) => BuildGeneratedIdentity(entry.Key, entry.TypeKey, entry.IsContainer)), StringComparer.Ordinal);
						foreach (GeneratedMetadataEntry item3 in list2)
						{
							if (item3 != null && !IsGeneratedKeyWithinPrefix(item3.Key, P_2))
							{
								string item = BuildGeneratedIdentity(item3.Key, item3.TypeKey, item3.IsContainer);
								if (hashSet.Add(item))
								{
									list.Add(item3);
								}
							}
						}
					}
				}
				StoreGeneratedMetadata(P_0, list);
			}
			else
			{
				StoreGeneratedMetadata(P_0, null);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("保存生成节点元数据失败: " + P_0 + "\n" + ex.Message);
		}
	}

	private static bool IsGeneratedKeyWithinPrefix(object P_0, object P_1)
	{
		if (!string.IsNullOrEmpty((string)P_0) && !string.IsNullOrEmpty((string)P_1))
		{
			if (string.Equals((string)P_0, (string)P_1, StringComparison.Ordinal))
			{
				return true;
			}
			return ((string)P_0).StartsWith((string)P_1 + "/", StringComparison.Ordinal);
		}
		return false;
	}

	private void CollectGeneratedIdentities(Transform P_0, HashSet<string> P_1, Func<PsdGeneratedKey, bool> P_2)
	{
		if (P_0 == null || P_1 == null)
		{
			return;
		}
		for (int i = 0; i < P_0.childCount; i++)
		{
			Transform child = P_0.GetChild(i);
			if (!(child == null))
			{
				PsdGeneratedKey component = child.GetComponent<PsdGeneratedKey>();
				if (component != null && (P_2 == null || P_2(component)))
				{
					P_1.Add(BuildGeneratedIdentity(component.Key, component.GetTypeKey(), component.GetIsContainer()));
				}
				CollectGeneratedIdentities(child, P_1, P_2);
			}
		}
	}

	private static Type GetPrimaryUiComponentType(object P_0)
	{
		if ((UnityEngine.Object)P_0 == null)
		{
			return null;
		}
		Component[] components = ((GameObject)P_0).GetComponents<Component>();
		int num = 0;
		Type type;
		while (true)
		{
			if (num < components.Length)
			{
				Component component = components[num];
				if (!(component == null))
				{
					type = component.GetType();
					if (!(type == typeof(Transform)) && !(type == typeof(RectTransform)) && !(type == typeof(CanvasRenderer)) && !(type == typeof(UIStringKey)) && !(type == typeof(PsdGeneratedKey)))
					{
						break;
					}
				}
				num++;
				continue;
			}
			return null;
		}
		return type;
	}

	private static bool MatchesConfiguredPrefabType(object P_0, GUIType P_1)
	{
		UGUIParseRule uGUIParseRule = UGUIParser.Instance?.GetRuleForUiType(P_1);
		if (uGUIParseRule != null && !(uGUIParseRule.UIPrefab == null) && !((UnityEngine.Object)P_0 == null))
		{
			Type type = GetPrimaryUiComponentType(uGUIParseRule.UIPrefab);
			Type type2 = GetPrimaryUiComponentType(P_0);
			if (!(type == null) && !(type2 == null))
			{
				return type == type2;
			}
			return false;
		}
		return false;
	}

	private GameObject FindGeneratedChildByIdentity(Transform parentTransform, string generatedKey, string typeKey, bool isContainer)
	{
		if (!(parentTransform == null) && !string.IsNullOrEmpty(generatedKey) && !string.IsNullOrEmpty(typeKey))
		{
			foreach (Transform item in parentTransform)
			{
				if (!(item == null))
				{
					PsdGeneratedKey component = item.GetComponent<PsdGeneratedKey>();
					if (!(component == null) && component.GetIsContainer() == isContainer && string.Equals(component.GetTypeKey(), typeKey, StringComparison.Ordinal) && string.Equals(component.Key, generatedKey, StringComparison.Ordinal))
					{
						return item.gameObject;
					}
				}
			}
			return null;
		}
		return null;
	}

	private GameObject FindGeneratedChildByKey(Transform parentTransform, string generatedKey, bool isContainer)
	{
		if (!(parentTransform == null) && !string.IsNullOrEmpty(generatedKey))
		{
			foreach (Transform item in parentTransform)
			{
				if (!(item == null))
				{
					PsdGeneratedKey component = item.GetComponent<PsdGeneratedKey>();
					if (!(component == null) && component.GetIsContainer() == isContainer && string.Equals(component.Key, generatedKey, StringComparison.Ordinal))
					{
						return item.gameObject;
					}
				}
			}
			return null;
		}
		return null;
	}

	private GameObject FindUniqueUnmarkedChild(Transform parentTransform, string objectName, bool isContainer, GUIType? expectedUiType = null)
	{
		if (!(parentTransform == null) && !string.IsNullOrWhiteSpace(objectName))
		{
			GameObject result = null;
			int num = 0;
			{
				foreach (Transform item in parentTransform)
				{
					if (item == null || !string.Equals(item.name, objectName, StringComparison.Ordinal) || item.GetComponent<PsdGeneratedKey>() != null)
					{
						continue;
					}
					if (isContainer)
					{
						if (GetPrimaryUiComponentType(item.gameObject) != null)
						{
							continue;
						}
					}
					else if (expectedUiType.HasValue && !MatchesConfiguredPrefabType(item.gameObject, expectedUiType.Value))
					{
						continue;
					}
					result = item.gameObject;
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

	private void AssignGeneratedNodeIdentity(GameObject generatedObject, string generatedKey, string typeKey, bool isContainer, HashSet<string> retainedKeys = null)
	{
		if (!(generatedObject == null) && !string.IsNullOrEmpty(generatedKey) && !string.IsNullOrEmpty(typeKey))
		{
			PsdGeneratedKey obj = generatedObject.GetComponent<PsdGeneratedKey>() ?? generatedObject.AddComponent<PsdGeneratedKey>();
			obj.Key = generatedKey;
			obj.SetTypeKey(typeKey);
			obj.SetIsContainer(isContainer);
			retainedKeys?.Add(BuildGeneratedIdentity(generatedKey, typeKey, isContainer));
		}
	}

	private static void RemoveUiStringKeyComponents(object rootObject)
	{
		if ((UnityEngine.Object)rootObject == null)
		{
			return;
		}
		UIStringKey[] componentsInChildren = ((GameObject)rootObject).GetComponentsInChildren<UIStringKey>(includeInactive: true);
		for (int num = componentsInChildren.Length - 1; num >= 0; num--)
		{
			if (componentsInChildren[num] != null)
			{
				UnityEngine.Object.DestroyImmediate(componentsInChildren[num]);
			}
		}
	}

	private void RemoveStaleGeneratedChildren(Transform parentTransform, HashSet<string> retainedKeys)
	{
		if (parentTransform == null || retainedKeys == null)
		{
			return;
		}
		for (int num = parentTransform.childCount - 1; num >= 0; num--)
		{
			Transform child = parentTransform.GetChild(num);
			if (child == null)
			{
				continue;
			}
			PsdGeneratedKey component = child.GetComponent<PsdGeneratedKey>();
			if (component != null)
			{
				string item = BuildGeneratedIdentity(component.Key, component.GetTypeKey(), component.GetIsContainer());
				if (!retainedKeys.Contains(item))
				{
					UnityEngine.Object.DestroyImmediate(child.gameObject);
					continue;
				}
			}
			RemoveStaleGeneratedChildren(child, retainedKeys);
		}
	}

	private string BuildGeneratedNodePath(GameObject layerObject, Transform rootTransform)
	{
		if (layerObject == null)
		{
			return null;
		}
		if (!(layerObject.GetComponent<PsdLayerNode>() == null))
		{
			List<string> list = new List<string>();
			Transform parent = layerObject.transform;
			while (parent != null && parent != rootTransform)
			{
				PsdLayerNode component = parent.GetComponent<PsdLayerNode>();
				if (component != null)
				{
					string text = GetUniqueGeneratedLayerSegment(component);
					if (!string.IsNullOrEmpty(text))
					{
						list.Insert(0, text);
					}
				}
				parent = parent.parent;
			}
			if (list.Count <= 0)
			{
				return null;
			}
			return string.Join("/", list);
		}
		return null;
	}

	private string[] GetParentContainerPaths(GameObject layerObject, Transform rootTransform, out string[] ancestorObjectNames)
	{
		_003C_003Ec__DisplayClass129_0 CS_0024_003C_003E8__locals9 = new _003C_003Ec__DisplayClass129_0();
		CS_0024_003C_003E8__locals9.Converter = this;
		CS_0024_003C_003E8__locals9.GenerationRoot = rootTransform;
		ancestorObjectNames = null;
		if (!(layerObject == null) && !(CS_0024_003C_003E8__locals9.GenerationRoot == null))
		{
			if (layerObject.transform == CS_0024_003C_003E8__locals9.GenerationRoot)
			{
				return null;
			}
			if (layerObject.transform.parent == null)
			{
				return null;
			}
			if (layerObject.transform.IsChildOf(CS_0024_003C_003E8__locals9.GenerationRoot))
			{
				if (layerObject.transform.parent == CS_0024_003C_003E8__locals9.GenerationRoot)
				{
					return null;
				}
				List<PsdLayerNode> list = new List<PsdLayerNode>();
				Transform parent = layerObject.transform.parent;
				while (parent != null && parent != CS_0024_003C_003E8__locals9.GenerationRoot)
				{
					PsdLayerNode component = parent.GetComponent<PsdLayerNode>();
					if (component != null)
					{
						list.Insert(0, component);
					}
					parent = parent.parent;
				}
				if (list.Count >= 1)
				{
					ancestorObjectNames = list.Select((PsdLayerNode item) => item.gameObject.name).ToArray();
					return list.Select((PsdLayerNode item) => CS_0024_003C_003E8__locals9.Converter.BuildGeneratedNodePath(item.gameObject, CS_0024_003C_003E8__locals9.GenerationRoot)).ToArray();
				}
				return null;
			}
			return null;
		}
		return null;
	}

	private Dictionary<string, LayerUiTypeSnapshot> CaptureLayerUiTypes()
	{
		Dictionary<string, LayerUiTypeSnapshot> dictionary = new Dictionary<string, LayerUiTypeSnapshot>(StringComparer.OrdinalIgnoreCase);
		PsdLayerNode[] componentsInChildren = GetComponentsInChildren<PsdLayerNode>(includeInactive: true);
		foreach (PsdLayerNode psdLayerNode in componentsInChildren)
		{
			string text = BuildSourceLayerPath(psdLayerNode);
			if (!string.IsNullOrEmpty(text) && !dictionary.ContainsKey(text))
			{
				dictionary.Add(text, new LayerUiTypeSnapshot
				{
					UiType = psdLayerNode.UIType,
					RoleUiType = psdLayerNode.RoleUIType
				});
			}
		}
		return dictionary;
	}

	private void RestoreLayerUiTypes(Dictionary<string, LayerUiTypeSnapshot> P_0)
	{
		if (P_0 == null || P_0.Count < 1)
		{
			return;
		}
		PsdLayerNode[] componentsInChildren = GetComponentsInChildren<PsdLayerNode>(includeInactive: true);
		foreach (PsdLayerNode psdLayerNode in componentsInChildren)
		{
			string text = BuildSourceLayerPath(psdLayerNode);
			if (!string.IsNullOrEmpty(text) && P_0.TryGetValue(text, out var value))
			{
				psdLayerNode.SetUiTypeAndRole(value.UiType, value.RoleUiType, false);
			}
		}
	}

	private string BuildSourceLayerPath(PsdLayerNode P_0)
	{
		if (P_0 == null)
		{
			return null;
		}
		List<string> list = new List<string>();
		Transform parent = P_0.transform;
		while (parent != null && parent != base.transform)
		{
			PsdLayerNode component = parent.GetComponent<PsdLayerNode>();
			if (component != null)
			{
				string text = GetUniqueSourceLayerSegment(component);
				if (!string.IsNullOrEmpty(text))
				{
					list.Insert(0, text);
				}
			}
			parent = parent.parent;
		}
		if (list.Count > 0)
		{
			return string.Join("/", list);
		}
		return null;
	}

	private string GetUniqueSourceLayerSegment(PsdLayerNode P_0)
	{
		if (P_0 == null)
		{
			return null;
		}
		string text = GetSourceLayerLookupKey(P_0);
		if (!string.IsNullOrEmpty(text))
		{
			int num = 0;
			Transform parent = P_0.transform.parent;
			if (parent != null)
			{
				for (int i = 0; i < parent.childCount; i++)
				{
					Transform child = parent.GetChild(i);
					PsdLayerNode psdLayerNode = ((!(child != null)) ? null : child.GetComponent<PsdLayerNode>());
					if (!(psdLayerNode == null))
					{
						if ((object)psdLayerNode == P_0)
						{
							break;
						}
						if (string.Equals(GetSourceLayerLookupKey(psdLayerNode), text, StringComparison.OrdinalIgnoreCase))
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

	private string GetSourceLayerLookupKey(PsdLayerNode P_0)
	{
		if (!(P_0 == null))
		{
			string text = ((!string.IsNullOrWhiteSpace(P_0.GetSourceLayerName())) ? P_0.GetSourceLayerName() : P_0.gameObject?.name);
			UGUIParser uGUIParser = UGUIParser.Instance;
			if (uGUIParser != null)
			{
				text = uGUIParser.RemoveRecognizedLayerTags(text);
			}
			return PsdLayerNode.NormalizeLayerLookupKey(text, true);
		}
		return null;
	}

	private string GetUniqueGeneratedLayerSegment(PsdLayerNode P_0)
	{
		if (P_0 == null)
		{
			return null;
		}
		string text = P_0.GetGameObjectLookupKey();
		if (!string.IsNullOrEmpty(text))
		{
			int num = 0;
			Transform parent = P_0.transform.parent;
			if (parent != null)
			{
				for (int i = 0; i < parent.childCount; i++)
				{
					Transform child = parent.GetChild(i);
					PsdLayerNode psdLayerNode = ((child != null) ? child.GetComponent<PsdLayerNode>() : null);
					if (!(psdLayerNode == null))
					{
						if ((object)psdLayerNode == P_0)
						{
							break;
						}
						if (string.Equals(psdLayerNode.GetGameObjectLookupKey(), text, StringComparison.OrdinalIgnoreCase))
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

	private GameObject FindReusableGeneratedUiObject(Transform parentTransform, UIHelperBase uiHelper, string generatedKey)
	{
		if (!(parentTransform == null) && !(uiHelper == null) && !(uiHelper.GetLayerNode() == null) && !string.IsNullOrEmpty(generatedKey))
		{
			string text = uiHelper.GetLayerNode().UIType.ToString();
			GameObject gameObject = FindGeneratedChildByIdentity(parentTransform, generatedKey, text, false);
			if (gameObject != null)
			{
				return gameObject;
			}
			GameObject gameObject2 = FindGeneratedChildByKey(parentTransform, generatedKey, false);
			if (gameObject2 != null)
			{
				UnityEngine.Object.DestroyImmediate(gameObject2);
			}
			string text2 = uiHelper.GetLayerNode().GetGeneratedObjectName();
			GameObject gameObject3 = FindUniqueUnmarkedChild(parentTransform, text2, false, uiHelper.GetLayerNode().UIType);
			if (gameObject3 != null)
			{
				return gameObject3;
			}
			if (string.Equals(text2, uiHelper.name, StringComparison.Ordinal))
			{
				return null;
			}
			return FindUniqueUnmarkedChild(parentTransform, uiHelper.name, false, uiHelper.GetLayerNode().UIType);
		}
		return null;
	}

	private GameObject FindReusablePrefabReferenceObject(Transform parentTransform, PsdLayerNode layerNode, string generatedKey, GameObject sourcePrefab)
	{
		if (!(parentTransform == null) && !(layerNode == null) && !string.IsNullOrEmpty(generatedKey))
		{
			GameObject gameObject = FindGeneratedChildByIdentity(parentTransform, generatedKey, "__PrefabRef", false);
			if (gameObject != null)
			{
				if (PrefabUtility.GetCorrespondingObjectFromSource(gameObject) == sourcePrefab)
				{
					return gameObject;
				}
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
			GameObject gameObject2 = FindGeneratedChildByKey(parentTransform, generatedKey, false);
			if (gameObject2 != null)
			{
				UnityEngine.Object.DestroyImmediate(gameObject2);
			}
			string text = ((!string.IsNullOrEmpty(layerNode.GetPrefabReferenceKey())) ? PsdLayerNode.GetAssetLeafName(layerNode.GetPrefabReferenceKey(), layerNode.gameObject.name) : layerNode.gameObject.name);
			GameObject gameObject3 = FindUniqueUnmarkedChild(parentTransform, text, false);
			if (!(gameObject3 == null))
			{
				if (PrefabUtility.GetCorrespondingObjectFromSource(gameObject3) == sourcePrefab)
				{
					return gameObject3;
				}
				return null;
			}
			return null;
		}
		return null;
	}

	private void RebuildImageReferenceIndex(PsdLayerNode[] layerNodes = null)
	{
		_layersByLookupKey.Clear();
		_referencedImageKeys.Clear();
		if (layerNodes == null || layerNodes.Length == 0)
		{
			layerNodes = GetComponentsInChildren<PsdLayerNode>(includeInactive: true);
		}
		PsdLayerNode[] array = layerNodes;
		foreach (PsdLayerNode psdLayerNode in array)
		{
			string text = psdLayerNode.GetGameObjectLookupKey();
			if (!string.IsNullOrEmpty(text) && !_layersByLookupKey.ContainsKey(text))
			{
				_layersByLookupKey.Add(text, psdLayerNode);
			}
		}
		array = layerNodes;
		foreach (PsdLayerNode psdLayerNode2 in array)
		{
			if (psdLayerNode2.HasImageReference() && !string.IsNullOrEmpty(psdLayerNode2.GetImageReferenceKey()))
			{
				_referencedImageKeys.Add(psdLayerNode2.GetImageReferenceKey());
			}
		}
	}

	private void ExportReferencedImages(PsdLayerNode[] P_0)
	{
		_exportedImagePathsByKey.Clear();
		if (P_0 == null || P_0.Length == 0 || !TryEnsureSharedImageDirectory(out var text))
		{
			return;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (PsdLayerNode psdLayerNode in P_0)
		{
			if (psdLayerNode.HasImageReference() && !string.IsNullOrEmpty(psdLayerNode.GetImageReferenceKey()))
			{
				hashSet.Add(psdLayerNode.GetImageReferenceKey());
			}
		}
		foreach (string item in hashSet)
		{
			if (_layersByLookupKey.TryGetValue(item, out var value))
			{
				string text2 = value.ExportLayerImage(true, text, item, false, true);
				if (!string.IsNullOrEmpty(text2))
				{
					CacheImageExportPath(item, text2);
				}
			}
		}
	}

	private static bool EnsureAssetParentDirectory(object P_0)
	{
		if (string.IsNullOrWhiteSpace((string)P_0))
		{
			return false;
		}
		string text = Path.GetDirectoryName((string)P_0)?.Replace("\\", "/");
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
			Debug.LogError("创建资源目录失败:" + ex.Message + "\n" + text);
			return false;
		}
	}

	private bool TryEnsureSharedImageDirectory(out string P_0)
	{
		P_0 = null;
		string text = UGUIParser.Instance?.GetSharedImageOutputDirectory();
		if (!string.IsNullOrWhiteSpace(text))
		{
			P_0 = NormalizeAssetsDirectory(text);
			if (string.IsNullOrWhiteSpace(P_0))
			{
				return false;
			}
			if (!Directory.Exists(P_0))
			{
				try
				{
					Directory.CreateDirectory(P_0);
					AssetDatabase.Refresh();
				}
				catch (Exception ex)
				{
					Debug.LogError("创建共享资源目录失败:" + ex.Message);
					return false;
				}
			}
			return true;
		}
		return false;
	}

	private bool TryEnsureSharedPrefabDirectory(out string P_0)
	{
		P_0 = null;
		string text = UGUIParser.Instance?.GetSharedPrefabOutputDirectory();
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		P_0 = NormalizeAssetsDirectory(text);
		if (string.IsNullOrWhiteSpace(P_0))
		{
			return false;
		}
		if (!Directory.Exists(P_0))
		{
			try
			{
				Directory.CreateDirectory(P_0);
				AssetDatabase.Refresh();
			}
			catch (Exception ex)
			{
				Debug.LogError("创建共享Prefab目录失败:" + ex.Message);
				return false;
			}
		}
		return true;
	}

	private string NormalizeAssetsDirectory(string P_0)
	{
		if (!string.IsNullOrWhiteSpace(P_0))
		{
			string text = P_0.Replace("\\", "/").Trim();
			if (Path.IsPathRooted(text))
			{
				text = RelativePathUtilities.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, text).Replace("\\", "/");
			}
			if (string.Equals(text, "Assets", StringComparison.OrdinalIgnoreCase))
			{
				return "Assets";
			}
			if (text.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
			{
				return text.Replace("\\", "/");
			}
			return null;
		}
		return null;
	}

	private bool TryEnsureReferencedPrefab(PsdLayerNode P_0, out GameObject P_1)
	{
		P_1 = null;
		if (!(P_0 == null) && !string.IsNullOrWhiteSpace(P_0.GetPrefabReferenceKey()))
		{
			string text = P_0.GetPrefabReferenceKey();
			if (_sharedPrefabsByKey.TryGetValue(text, out var value) && value != null)
			{
				P_1 = value;
				return true;
			}
			if (P_0.TryLoadReferencedPrefab(out P_1) && P_1 != null)
			{
				_sharedPrefabsByKey[text] = P_1;
				return true;
			}
			if (!TryEnsureSharedPrefabDirectory(out var path))
			{
				Debug.LogWarning("SharedPrefabOutput未配置, 无法复用prefab:" + P_0.name);
				return false;
			}
			string text2 = Path.Combine(path, text + ".prefab").Replace("\\", "/");
			if (!EnsureAssetParentDirectory(text2))
			{
				return false;
			}
			if (File.Exists(text2))
			{
				AssetDatabase.ImportAsset(text2);
				P_1 = AssetDatabase.LoadAssetAtPath<GameObject>(text2);
				if (P_1 != null)
				{
					_sharedPrefabsByKey[text] = P_1;
					return true;
				}
				Debug.LogWarning("引用prefab文件存在但无法加载: " + text2);
			}
			if (_prefabExportsInProgress.Contains(text))
			{
				Debug.LogWarning("检测到循环引用prefab, 已跳过导出: " + text);
				return false;
			}
			_prefabExportsInProgress.Add(text);
			try
			{
				string text3 = PsdLayerNode.GetAssetLeafName(text, P_0.name);
				P_1 = GenerateReusablePrefab(P_0, text2, text3, true);
				if (P_1 != null)
				{
					_sharedPrefabsByKey[text] = P_1;
					return true;
				}
				return false;
			}
			finally
			{
				_prefabExportsInProgress.Remove(text);
			}
		}
		return false;
	}

	private GameObject GenerateReusablePrefab(PsdLayerNode P_0, string P_1, string P_2, bool P_3)
	{
		_003C_003Ec__DisplayClass145_0 CS_0024_003C_003E8__locals10 = new _003C_003Ec__DisplayClass145_0();
		if (P_0 == null)
		{
			return null;
		}
		if (!string.IsNullOrWhiteSpace(P_1) && !string.IsNullOrWhiteSpace(P_2))
		{
			Transform transform = P_0.transform;
			PsdLayerNode[] componentsInChildren = transform.GetComponentsInChildren<PsdLayerNode>(includeInactive: true);
			CS_0024_003C_003E8__locals10.PrefabReferenceRoots = new List<PsdLayerNode>();
			PsdLayerNode component = transform.GetComponent<PsdLayerNode>();
			PsdLayerNode[] array = componentsInChildren.Where((PsdLayerNode node) => node != null && node.HasPrefabReference()).ToArray();
			PsdLayerNode[] array2;
			if (!P_3 && component != null && component.HasPrefabReference())
			{
				CS_0024_003C_003E8__locals10.PrefabReferenceRoots.Add(component);
			}
			else if (array.Length != 0)
			{
				array2 = array;
				foreach (PsdLayerNode psdLayerNode in array2)
				{
					if (psdLayerNode == null || (P_3 && psdLayerNode == component))
					{
						continue;
					}
					bool flag = false;
					Transform parent = psdLayerNode.transform.parent;
					while (parent != null && parent != transform)
					{
						PsdLayerNode component2 = parent.GetComponent<PsdLayerNode>();
						if (!(component2 != null) || !component2.HasPrefabReference() || (P_3 && !(component2 != component)))
						{
							parent = parent.parent;
							continue;
						}
						flag = true;
						break;
					}
					if (!flag)
					{
						CS_0024_003C_003E8__locals10.PrefabReferenceRoots.Add(psdLayerNode);
					}
				}
			}
			PsdLayerNode[] array3 = componentsInChildren.Where((PsdLayerNode node) => node != null && !CS_0024_003C_003E8__locals10.IsWithinPrefabReferenceRoot(node.transform)).ToArray();
			RebuildImageReferenceIndex();
			_referencedImageKeys.Clear();
			array2 = array3;
			foreach (PsdLayerNode psdLayerNode2 in array2)
			{
				if (psdLayerNode2.HasImageReference() && !string.IsNullOrEmpty(psdLayerNode2.GetImageReferenceKey()))
				{
					_referencedImageKeys.Add(psdLayerNode2.GetImageReferenceKey());
				}
			}
			ExportReferencedImages(array3);
			UIHelperBase[] array4 = CollectIndependentUiHelpers(transform);
			if (array4 != null && array4.Length != 0 && CS_0024_003C_003E8__locals10.PrefabReferenceRoots.Count > 0)
			{
				for (int num2 = array4.Length - 1; num2 >= 0; num2--)
				{
					UIHelperBase uIHelperBase = array4[num2];
					if (!(uIHelperBase == null) && !(uIHelperBase.GetLayerNode() == null) && CS_0024_003C_003E8__locals10.IsWithinPrefabReferenceRoot(uIHelperBase.transform))
					{
						ArrayUtility.RemoveAt(ref array4, num2);
					}
				}
			}
			if ((array4 == null || array4.Length < 1) && CS_0024_003C_003E8__locals10.PrefabReferenceRoots.Count < 1)
			{
				Debug.LogWarning("导出prefab失败: 未找到可生成的UI节点:" + P_0.name);
				return null;
			}
			GameObject gameObject = null;
			GameObject gameObject2 = AssetDatabase.LoadAssetAtPath<GameObject>(P_1);
			UIHelperBase component3 = transform.GetComponent<UIHelperBase>();
			bool flag2 = component3 != null && component3.GetLayerNode() != null && component3.GetLayerNode().IsPrimaryUiElement() && component3.GetLayerNode().UIType != GUIType.Null;
			try
			{
				if (gameObject2 != null)
				{
					gameObject = UnityEngine.Object.Instantiate(gameObject2);
					RestoreGeneratedKeyComponents(P_1, gameObject);
				}
				Vector3 vector = Vector3.zero;
				if (flag2)
				{
					if (gameObject != null)
					{
						if (!MatchesConfiguredPrefabType(gameObject, component3.GetLayerNode().UIType))
						{
							UnityEngine.Object.DestroyImmediate(gameObject);
							gameObject = null;
						}
						else
						{
							gameObject = component3.CreateOrUpdateUiObject(gameObject);
						}
					}
					if (gameObject == null)
					{
						gameObject = component3.CreateOrUpdateUiObject();
					}
					if (gameObject == null)
					{
						flag2 = false;
					}
				}
				if (flag2)
				{
					gameObject.name = P_2;
					gameObject.layer = UnityEngine.LayerMask.NameToLayer("UI");
					vector = gameObject.transform.position;
					gameObject.transform.localPosition = Vector3.zero;
					gameObject.transform.localRotation = Quaternion.identity;
					gameObject.transform.localScale = Vector3.one;
				}
				else
				{
					if (!(gameObject != null) || !(gameObject.GetComponent<RectTransform>() != null) || !(GetPrimaryUiComponentType(gameObject) == null))
					{
						if (gameObject != null)
						{
							UnityEngine.Object.DestroyImmediate(gameObject);
						}
						gameObject = new GameObject(P_2, typeof(RectTransform));
					}
					gameObject.name = P_2;
					gameObject.layer = UnityEngine.LayerMask.NameToLayer("UI");
					gameObject.transform.localPosition = Vector3.zero;
					gameObject.transform.localRotation = Quaternion.identity;
					gameObject.transform.localScale = Vector3.one;
					UGUIParser.ApplyLayerRectToUiElement(P_0, gameObject.transform);
				}
				HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
				int num3 = 0;
				int num4 = array4.Length;
				UIHelperBase[] array5 = array4;
				foreach (UIHelperBase uIHelperBase2 in array5)
				{
					if (uIHelperBase2 == null || uIHelperBase2.GetLayerNode() == null || (flag2 && uIHelperBase2 == component3))
					{
						continue;
					}
					EditorUtility.DisplayProgressBar($"生成prefab:({num3++}/{num4})", "正在生成UI元素:" + uIHelperBase2.name, (float)num3 / (float)num4);
					string text = BuildGeneratedNodePath(uIHelperBase2.gameObject, transform);
					string[] array7;
					string[] array6 = GetParentContainerPaths(uIHelperBase2.gameObject, transform, out array7);
					GameObject gameObject3 = EnsureGeneratedContainerPath(gameObject, array6, array7, hashSet);
					GameObject gameObject4 = FindReusableGeneratedUiObject(gameObject3.transform, uIHelperBase2, text);
					GameObject gameObject5 = uIHelperBase2.CreateOrUpdateUiObject(gameObject4);
					if (!(gameObject5 == null))
					{
						AssignGeneratedNodeIdentity(gameObject5, text, uIHelperBase2.GetLayerNode().UIType.ToString(), false, hashSet);
						gameObject5.transform.SetParent(gameObject3.transform, worldPositionStays: true);
						if (flag2)
						{
							gameObject5.transform.position -= vector;
						}
						gameObject5.transform.localScale = Vector3.one;
					}
				}
				if (CS_0024_003C_003E8__locals10.PrefabReferenceRoots.Count > 0)
				{
					int num5 = 0;
					int count = CS_0024_003C_003E8__locals10.PrefabReferenceRoots.Count;
					foreach (PsdLayerNode item in CS_0024_003C_003E8__locals10.PrefabReferenceRoots)
					{
						if (item == null || !item.HasPrefabReference())
						{
							continue;
						}
						EditorUtility.DisplayProgressBar($"生成prefab-引用prefab:({num5++}/{count})", "正在实例化prefab:" + item.GetRawPrefabReference(), (float)num5 / (float)count);
						if (TryEnsureReferencedPrefab(item, out var gameObject6) && !(gameObject6 == null))
						{
							string text2 = BuildGeneratedNodePath(item.gameObject, transform);
							string[] array9;
							string[] array8 = GetParentContainerPaths(item.gameObject, transform, out array9);
							GameObject gameObject7 = EnsureGeneratedContainerPath(gameObject, array8, array9, hashSet);
							GameObject gameObject8 = FindReusablePrefabReferenceObject(gameObject7.transform, item, text2, gameObject6);
							if (gameObject8 == null)
							{
								gameObject8 = PrefabUtility.InstantiatePrefab(gameObject6) as GameObject;
								if (gameObject8 == null)
								{
									gameObject8 = UnityEngine.Object.Instantiate(gameObject6);
								}
							}
							gameObject8.name = ((!string.IsNullOrEmpty(item.GetPrefabReferenceKey())) ? PsdLayerNode.GetAssetLeafName(item.GetPrefabReferenceKey(), gameObject6.name) : gameObject6.name);
							gameObject8.transform.localRotation = Quaternion.identity;
							gameObject8.transform.localScale = Vector3.one;
							RectTransform component4 = gameObject8.GetComponent<RectTransform>();
							if (component4 != null)
							{
								UGUIParser.ApplyLayerRectToUiElement(item, component4);
							}
							else
							{
								Debug.LogWarning("引用prefab缺少RectTransform: " + gameObject6.name);
							}
							AssignGeneratedNodeIdentity(gameObject8, text2, "__PrefabRef", false, hashSet);
							gameObject8.transform.SetParent(gameObject7.transform, worldPositionStays: true);
							if (flag2)
							{
								gameObject8.transform.position -= vector;
							}
							gameObject8.transform.localScale = Vector3.one;
						}
						else
						{
							Debug.LogWarning("引用prefab未找到且导出失败: " + item.GetRawPrefabReference());
						}
					}
				}
				RemoveUiStringKeyComponents(gameObject);
				RemoveStaleGeneratedChildren(gameObject.transform, hashSet);
				ResizeGeneratedContainersRecursive(gameObject.transform);
				List<GeneratedNodeMetadataSnapshot> list = CaptureGeneratedNodeMetadata(gameObject);
				RemoveGeneratedKeyComponents(gameObject);
				gameObject.name = Path.GetFileNameWithoutExtension(P_1);
				GameObject obj = PrefabUtility.SaveAsPrefabAsset(gameObject, P_1);
				if (obj != null)
				{
					PersistGeneratedNodeMetadata(P_1, list);
				}
				return obj;
			}
			finally
			{
				EditorUtility.ClearProgressBar();
				if (gameObject != null)
				{
					UnityEngine.Object.DestroyImmediate(gameObject);
				}
			}
		}
		return null;
	}

	internal string EnsureReferencedImageExport(PsdLayerNode P_0, bool P_1)
	{
		if (!(P_0 == null) && !string.IsNullOrEmpty(P_0.GetImageReferenceKey()))
		{
			if (TryGetCachedImageExport(P_0, out var text))
			{
				return text.Replace("\\", "/");
			}
			if (TryEnsureSharedImageDirectory(out var text2))
			{
				bool flag = P_0.ShouldExportHighBitDepth();
				string text3 = PsdLayerNode.FindMatchingExportedImagePath(text2, P_0.GetImageReferenceKey(), flag);
				if (string.IsNullOrWhiteSpace(text3))
				{
					RebuildImageReferenceIndex();
					if (_layersByLookupKey.TryGetValue(P_0.GetImageReferenceKey(), out var value) && !(value == null))
					{
						string text4 = value.ExportLayerImage(true, text2, P_0.GetImageReferenceKey(), false, P_1);
						if (string.IsNullOrEmpty(text4))
						{
							return null;
						}
						CacheImageExportPath(P_0.GetImageReferenceKey(), text4);
						return text4;
					}
					string text5 = P_0.ExportLayerImage(true, text2, P_0.GetImageReferenceKey(), false, P_1, true);
					if (!string.IsNullOrEmpty(text5))
					{
						CacheImageExportPath(P_0.GetImageReferenceKey(), text5);
						return text5;
					}
					Debug.LogWarning("引用图片未找到且导出失败: " + P_0.name + " -> " + P_0.GetRawImageReference());
					return null;
				}
				CacheImageExportPath(P_0.GetImageReferenceKey(), text3);
				return text3;
			}
			Debug.LogWarning("SharedAssetsOutput未配置, 无法复用图层:" + P_0.name);
			return null;
		}
		return null;
	}

	internal bool TryGetCachedImageExport(PsdLayerNode layerNode, out string cachedImagePath)
	{
		cachedImagePath = null;
		if (!(layerNode == null))
		{
			string sharedReferenceKey = layerNode.GetGameObjectLookupKey();
			if (string.IsNullOrEmpty(sharedReferenceKey))
			{
				return false;
			}
			if (_exportedImagePathsByKey.TryGetValue(sharedReferenceKey, out var cachedExportPath))
			{
				if (!PsdTextureAssetUtility.MatchesExportMode(cachedExportPath, layerNode.ShouldExportHighBitDepth()))
				{
					_exportedImagePathsByKey.Remove(sharedReferenceKey);
					return false;
				}
				if (File.Exists(cachedExportPath))
				{
					cachedImagePath = cachedExportPath.Replace("\\", "/");
					return true;
				}
				_exportedImagePathsByKey.Remove(sharedReferenceKey);
			}
			return false;
		}
		return false;
	}

	internal bool TryGetSharedImageExportTarget(PsdLayerNode layerNode, out string sharedOutputDirectory, out string sharedAssetKey)
	{
		sharedOutputDirectory = null;
		sharedAssetKey = null;
		RebuildImageReferenceIndex();
		if (!(layerNode == null))
		{
			string text = layerNode.GetGameObjectLookupKey();
			if (!string.IsNullOrEmpty(text) && _referencedImageKeys.Contains(text))
			{
				if (TryEnsureSharedImageDirectory(out var text2))
				{
					sharedOutputDirectory = text2;
					sharedAssetKey = text;
					return true;
				}
				return false;
			}
			return false;
		}
		return false;
	}

	internal void CacheImageExportPath(string sharedAssetKey, string exportedImagePath)
	{
		if (!string.IsNullOrEmpty(sharedAssetKey) && !string.IsNullOrEmpty(exportedImagePath))
		{
			_exportedImagePathsByKey[sharedAssetKey] = exportedImagePath.Replace("\\", "/");
		}
	}

	internal PsdLayerNode FindLayerByLookupKey(string sharedAssetKey)
	{
		RebuildImageReferenceIndex();
		if (!string.IsNullOrEmpty(sharedAssetKey))
		{
			_layersByLookupKey.TryGetValue(sharedAssetKey, out var value);
			return value;
		}
		return null;
	}

	private void ResizeGeneratedContainersRecursive(Transform P_0)
	{
		if (P_0 == null)
		{
			return;
		}
		for (int i = 0; i < P_0.childCount; i++)
		{
			Transform child = P_0.GetChild(i);
			if (!(child == null))
			{
				ResizeGeneratedContainersRecursive(child);
			}
		}
		if (IsGeneratedContainer(P_0))
		{
			FitContainerToChildren(P_0 as RectTransform);
		}
	}

	private static bool IsGeneratedContainer(object P_0)
	{
		if (!((UnityEngine.Object)P_0 == null))
		{
			PsdGeneratedKey component = ((Component)P_0).GetComponent<PsdGeneratedKey>();
			if (component != null && component.GetIsContainer())
			{
				return string.Equals(component.GetTypeKey(), "__Container", StringComparison.Ordinal);
			}
			return false;
		}
		return false;
	}

	private static void FitContainerToChildren(object P_0)
	{
		if ((UnityEngine.Object)P_0 == null)
		{
			return;
		}
		List<RectTransform> list = new List<RectTransform>();
		for (int i = 0; i < ((Transform)P_0).childCount; i++)
		{
			RectTransform rectTransform = ((Transform)P_0).GetChild(i) as RectTransform;
			if (!(rectTransform == null))
			{
				CenterAnchorsPreservingWorldRect(rectTransform);
				list.Add(rectTransform);
			}
		}
		if (list.Count >= 1 && TryGetCombinedWorldRect(list, out var rect))
		{
			Vector3[] array = new Vector3[list.Count];
			for (int j = 0; j < list.Count; j++)
			{
				array[j] = list[j].position;
			}
			((RectTransform)P_0).anchorMin = new Vector2(0.5f, 0.5f);
			((RectTransform)P_0).anchorMax = new Vector2(0.5f, 0.5f);
			((RectTransform)P_0).pivot = new Vector2(0.5f, 0.5f);
			ApplyWorldRect(P_0, rect);
			for (int k = 0; k < list.Count; k++)
			{
				list[k].position = array[k];
			}
		}
	}

	private static void CenterAnchorsPreservingWorldRect(object targetRectTransform)
	{
		if (!((UnityEngine.Object)targetRectTransform == null) && TryGetWorldRect(targetRectTransform, out var rect))
		{
			((RectTransform)targetRectTransform).anchorMin = new Vector2(0.5f, 0.5f);
			((RectTransform)targetRectTransform).anchorMax = new Vector2(0.5f, 0.5f);
			ApplyWorldRect(targetRectTransform, rect);
		}
	}

	private static bool TryGetCombinedWorldRect(List<RectTransform> rectTransforms, out Rect combinedWorldRect)
	{
		combinedWorldRect = default(Rect);
		if (rectTransforms != null && rectTransforms.Count >= 1)
		{
			bool hasValidBounds = false;
			float minimumX = 0f;
			float minimumY = 0f;
			float maximumX = 0f;
			float maximumY = 0f;
			for (int i = 0; i < rectTransforms.Count; i++)
			{
				if (TryGetWorldRect(rectTransforms[i], out var childWorldRect))
				{
					if (!hasValidBounds)
					{
						minimumX = childWorldRect.xMin;
						minimumY = childWorldRect.yMin;
						maximumX = childWorldRect.xMax;
						maximumY = childWorldRect.yMax;
						hasValidBounds = true;
					}
					else
					{
						minimumX = Mathf.Min(minimumX, childWorldRect.xMin);
						minimumY = Mathf.Min(minimumY, childWorldRect.yMin);
						maximumX = Mathf.Max(maximumX, childWorldRect.xMax);
						maximumY = Mathf.Max(maximumY, childWorldRect.yMax);
					}
				}
			}
			if (!hasValidBounds)
			{
				return false;
			}
			combinedWorldRect = Rect.MinMaxRect(minimumX, minimumY, maximumX, maximumY);
			return true;
		}
		return false;
	}

	private static bool TryGetWorldRect(object targetRectTransform, out Rect worldRect)
	{
		worldRect = default(Rect);
		if (!((UnityEngine.Object)targetRectTransform == null))
		{
			Vector3[] worldCorners = new Vector3[4];
			((RectTransform)targetRectTransform).GetWorldCorners(worldCorners);
			float minimumX = worldCorners[0].x;
			float minimumY = worldCorners[0].y;
			float maximumX = worldCorners[0].x;
			float maximumY = worldCorners[0].y;
			for (int i = 1; i < worldCorners.Length; i++)
			{
				Vector3 worldCorner = worldCorners[i];
				minimumX = Mathf.Min(minimumX, worldCorner.x);
				minimumY = Mathf.Min(minimumY, worldCorner.y);
				maximumX = Mathf.Max(maximumX, worldCorner.x);
				maximumY = Mathf.Max(maximumY, worldCorner.y);
			}
			worldRect = Rect.MinMaxRect(minimumX, minimumY, maximumX, maximumY);
			return true;
		}
		return false;
	}

	private static void ApplyWorldRect(object targetRectTransform, Rect worldRect)
	{
		if (!((UnityEngine.Object)targetRectTransform == null))
		{
			((RectTransform)targetRectTransform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, worldRect.width);
			((RectTransform)targetRectTransform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, worldRect.height);
			Vector2 rectSize = ((RectTransform)targetRectTransform).rect.size;
			Vector2 pivotOffset = (((RectTransform)targetRectTransform).pivot - Vector2.one * 0.5f) * rectSize;
			((Transform)targetRectTransform).position = new Vector3(worldRect.center.x + pivotOffset.x, worldRect.center.y + pivotOffset.y, ((Transform)targetRectTransform).position.z);
		}
	}

	private UIHelperBase[] CollectIndependentUiHelpers(Transform rootTransform)
	{
		UIHelperBase[] componentsInChildren = rootTransform.GetComponentsInChildren<UIHelperBase>();
		componentsInChildren = componentsInChildren.Where((UIHelperBase ui) => ui != null && ui.GetLayerNode() != null && ui.GetLayerNode().IsPrimaryUiElement()).ToArray();
		HashSet<EntityId> hashSet = new HashSet<EntityId>();
		UIHelperBase[] array = componentsInChildren;
		for (int num = 0; num < array.Length; num++)
		{
			PsdLayerNode[] dependencies = array[num].GetDependencies();
			if (dependencies == null)
			{
				continue;
			}
			PsdLayerNode[] array2 = dependencies;
			foreach (PsdLayerNode psdLayerNode in array2)
			{
				if (!(psdLayerNode == null))
				{
					EntityId item = GetObjectEntityId(psdLayerNode.gameObject);
					hashSet.Add(item);
				}
			}
		}
		for (int num3 = componentsInChildren.Length - 1; num3 >= 0; num3--)
		{
			UIHelperBase uIHelperBase = componentsInChildren[num3];
			if (hashSet.Contains(GetObjectEntityId(uIHelperBase.gameObject)))
			{
				ArrayUtility.RemoveAt(ref componentsInChildren, num3);
			}
		}
		return componentsInChildren;
	}

	internal static void ConfigureExportedTextureImports(object textureAssetPaths, bool importAsSprite = true, bool preserveHighBitDepth = false)
	{
		for (int i = 0; i < ((Array)textureAssetPaths).Length; i++)
		{
			string text = (string)((object[])textureAssetPaths)[i];
			TextureImporter textureImporter = AssetImporter.GetAtPath(text) as TextureImporter;
			if (textureImporter == null)
			{
				Debug.LogError("TextureImporter为空:" + text);
				continue;
			}
			if (!importAsSprite)
			{
				textureImporter.textureType = TextureImporterType.Default;
				textureImporter.textureShape = TextureImporterShape.Texture2D;
				textureImporter.alphaSource = TextureImporterAlphaSource.FromInput;
				textureImporter.alphaIsTransparency = true;
				textureImporter.mipmapEnabled = false;
				textureImporter.npotScale = TextureImporterNPOTScale.None;
			}
			else
			{
				textureImporter.textureType = TextureImporterType.Sprite;
				textureImporter.spriteImportMode = SpriteImportMode.Single;
				textureImporter.alphaSource = TextureImporterAlphaSource.FromInput;
				textureImporter.alphaIsTransparency = true;
				textureImporter.mipmapEnabled = false;
			}
			PsdTextureAssetUtility.ApplyPrecisionImportSettings(textureImporter, preserveHighBitDepth);
			textureImporter.SaveAndReimport();
		}
	}

	internal static void EnsureSpriteNineSliceBorder(object P_0)
	{
		TextureImporter textureImporter = AssetImporter.GetAtPath((string)P_0) as TextureImporter;
		if (textureImporter == null)
		{
			Texture2D texture2D = AssetDatabase.LoadAssetAtPath<Texture2D>((string)P_0);
			Sprite sprite = PsdLayerNode.LoadSpriteAsset(P_0);
			if (!(texture2D == null) && !(sprite == null) && !(sprite.border != Vector4.zero))
			{
				Vector4 vector = UGUIParser.CalculateNineSliceBorder(texture2D, 0);
				if (!(vector == Vector4.zero))
				{
					PsdLayerNode.ReplaceEmbeddedSpriteAsset(P_0, texture2D, vector, out var _);
				}
			}
			return;
		}
		bool flag;
		if (flag = !textureImporter.isReadable)
		{
			textureImporter.isReadable = true;
			textureImporter.SaveAndReimport();
			textureImporter = AssetImporter.GetAtPath((string)P_0) as TextureImporter;
		}
		if (textureImporter == null)
		{
			return;
		}
		Sprite sprite3 = PsdLayerNode.LoadSpriteAsset(P_0);
		if (sprite3 != null && textureImporter.spriteBorder == Vector4.zero)
		{
			textureImporter.spriteBorder = UGUIParser.CalculateNineSliceBorder(sprite3.texture, 0);
			textureImporter.SaveAndReimport();
		}
		if (flag)
		{
			textureImporter = AssetImporter.GetAtPath((string)P_0) as TextureImporter;
			if (textureImporter != null)
			{
				textureImporter.isReadable = false;
				textureImporter.SaveAndReimport();
			}
		}
	}

	internal static bool nuYOPWMIyn(object P_0)
	{
		return false;
	}

	internal string GetUiImageOutputDirectory()
	{
		return Path.Combine(ScriptableSingleton<Psd2UIFormSettings>.Instance.UIImagesOutputDir, uiFormName);
	}

	public Psd2UIFormConverter()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private Psd2UIFormConverter(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
