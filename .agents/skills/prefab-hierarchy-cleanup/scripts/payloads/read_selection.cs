// Read-only payload: dump the current Unity Editor selection.
// Contract for eval_file payloads in this project: TOP-LEVEL STATEMENTS ONLY.
// No `using` directives (they fail to compile), UnityEngine/UnityEditor/System/
// System.Collections.Generic are implicitly available, UnityEngine.UI.* must be qualified.
var sb = new System.Text.StringBuilder();
sb.AppendLine("SELECTION_BEGIN");
var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
sb.AppendLine("prefabStage=" + (stage == null ? "<none>" : stage.assetPath));
sb.AppendLine("activeScene=" + UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path);
sb.AppendLine("count=" + UnityEditor.Selection.objects.Length);

string PathOf(Transform t)
{
    var parts = new System.Collections.Generic.Stack<string>();
    for (var c = t; c != null; c = c.parent) parts.Push(c.name);
    return string.Join("/", parts.ToArray());
}

foreach (var obj in UnityEditor.Selection.objects)
{
    if (obj == null) { sb.AppendLine("OBJ\tnull"); continue; }
    var assetPath = AssetDatabase.GetAssetPath(obj);
    sb.AppendLine("OBJ\ttype=" + obj.GetType().Name + "\tname=" + obj.name + "\tasset=" +
                  (string.IsNullOrEmpty(assetPath) ? "<none>" : assetPath));
    var go = obj as GameObject;
    if (go == null) continue;
    var t = go.transform;
    var rect = go.GetComponent<RectTransform>();
    var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
    sb.AppendLine("GO\tpath=" + PathOf(t)
        + "\tscene=" + go.scene.name
        + "\tinstanceRoot=" + PrefabUtility.IsAnyPrefabInstanceRoot(go)
        + "\tpartOfInstance=" + PrefabUtility.IsPartOfPrefabInstance(go)
        + "\tnearestInstanceSrc=" + (PrefabUtility.IsPartOfPrefabInstance(go)
              ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) : "-")
        + "\tcorrespondingAsset=" + (src == null ? "-" : AssetDatabase.GetAssetPath(src))
        + "\tsibling=" + t.GetSiblingIndex() + "\tchildren=" + t.childCount
        + "\tactiveSelf=" + go.activeSelf + "\tactiveInHierarchy=" + go.activeInHierarchy);
    var comps = new System.Collections.Generic.List<string>();
    foreach (var c in go.GetComponents<Component>())
        comps.Add(c == null ? "<missing>" : c.GetType().Name);
    sb.AppendLine("COMPONENTS\t" + string.Join(",", comps.ToArray()));
    if (rect != null)
    {
        sb.AppendLine("RECT\tanchoredPosition=" + rect.anchoredPosition.x.ToString("0.###") + "," + rect.anchoredPosition.y.ToString("0.###")
            + "\tsizeDelta=" + rect.sizeDelta.x.ToString("0.###") + "," + rect.sizeDelta.y.ToString("0.###")
            + "\tanchorMin=" + rect.anchorMin.x.ToString("0.###") + "," + rect.anchorMin.y.ToString("0.###")
            + "\tanchorMax=" + rect.anchorMax.x.ToString("0.###") + "," + rect.anchorMax.y.ToString("0.###"));
        var world = new Vector3[4];
        rect.GetWorldCorners(world);
        sb.AppendLine("RECT_WORLD\tmin=" + world[0].x.ToString("0.###") + "," + world[0].y.ToString("0.###")
            + "\tmax=" + world[2].x.ToString("0.###") + "," + world[2].y.ToString("0.###"));
    }
    var tmp = go.GetComponent<TMPro.TextMeshProUGUI>();
    if (tmp != null) sb.AppendLine("TMP\ttext=" + tmp.text.Replace("\n", "\\n"));
    var img = go.GetComponent<UnityEngine.UI.Image>();
    if (img != null) sb.AppendLine("IMAGE\tsprite=" + (img.sprite == null ? "<none>" : AssetDatabase.GetAssetPath(img.sprite)));
}
sb.AppendLine("SELECTION_END");
return sb.ToString();
