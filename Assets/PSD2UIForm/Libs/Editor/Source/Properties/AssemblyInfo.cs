using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: Obfuscation(Feature = "rule regex: (-renaming) where (inherited_public) (namespace=UnityEditor; types=EditorWindow) (-typenames; methods=OnEnable|OnDisable|OnFocus|OnGUI|OnInspectorUpdate|CreateGUI; fields=m_.*)", Exclude = false)]
[assembly: Obfuscation(Feature = "rule regex: (-renaming) (namespace=UGF\\.EditorTools\\.Psd2UGUI; types=.*) (methods=Awake|OnAwake|Reset|Start|Update|FixedUpdate|LateUpdate|OnEnable|OnDisable|OnDestroy|OnApplicationFocus|OnApplicationPause|OnApplicationQuit|OnBeforeTransformParentChanged|OnTransformParentChanged|OnTransformChildrenChanged|OnRectTransformDimensionsChange|OnCanvasGroupChanged|OnCanvasHierarchyChanged|OnDidApplyAnimationProperties|OnGUI|OnValidate|OnFocus|OnLostFocus|OnSelectionChange|OnHierarchyChange|OnProjectChange|OnInspectorUpdate|OnInspectorGUI|OnSceneGUI|OnPreviewGUI|OnPreviewSettings|CreateGUI|CreateInspectorGUI|OnBeforeSerialize|OnAfterDeserialize|HasPreviewGUI|GetInfoString|GetPropertyHeight|OnDrawGizmos|OnDrawGizmosSelected|OnRenderObject|OnPostRender|OnPreRender|OnWillRenderObject|OnBecameVisible|OnBecameInvisible|OnAnimatorMove|OnAnimatorIK)", Exclude = false)]
[assembly: SuppressIldasm]
[assembly: Obfuscation(Feature = "rule regex: (-renaming) where (inherited_public) (namespace=UGF\\.EditorTools\\.Psd2UGUI; types=UIHelperBase) (-typenames)", Exclude = false)]
[assembly: AssemblyVersion("0.0.0.0")]
