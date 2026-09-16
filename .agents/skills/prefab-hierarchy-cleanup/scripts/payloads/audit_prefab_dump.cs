// Read-only payload: dump one Prefab's complete tree WITH per-node invariants, so a before/after
// pair can be compared offline by scripts/audit_prefab_preservation.py.
//
// This file is a TEMPLATE. The caller (audit_prefab_preservation.py --mode capture) substitutes
// the JSON-quoted asset path into the placeholder on the `var prefabPath` line below, writes the
// result to a temp file, and runs it through the project's Unity Pipeline CLI (`eval_file`).
//
// Contract for eval_file payloads in this project (SKILL.md -> Project-verified engine realities R5):
// top-level statements only, NO `using` directives (they fail to compile with "Identifier expected"),
// UnityEngine / UnityEditor / System / System.Collections.Generic are implicitly available,
// UnityEngine.UI.* must be fully qualified, and `return`ing a string hands it back to the CLI.
//
// Everything here is READ-ONLY: LoadPrefabContents + UnloadPrefabContents, never SaveAsPrefabAsset.
var prefabPath = "ASSET_PATH_PLACEHOLDER";
var root = PrefabUtility.LoadPrefabContents(prefabPath);
if (root == null) throw new System.InvalidOperationException("prefab did not load: " + prefabPath);
try
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("DUMP_BEGIN");

    System.Action<Transform, string> walk = null;
    walk = (t, prefix) =>
    {
        var path = string.IsNullOrEmpty(prefix) ? t.name : prefix + "/" + t.name;

        // Non-Transform component type names, shortened for the common UI types.
        var comps = new System.Collections.Generic.List<string>();
        foreach (var c in t.GetComponents<Component>())
        {
            if (c == null) { comps.Add("MISSING"); continue; }
            var n = c.GetType().FullName;
            if (n == "UnityEngine.Transform" || n == "UnityEngine.RectTransform") continue;
            if (n == "UnityEngine.CanvasRenderer") { comps.Add("CanvasRenderer"); continue; }
            if (n == "UnityEngine.UI.Image") { comps.Add("Image"); continue; }
            if (n == "TMPro.TextMeshProUGUI") { comps.Add("TMP"); continue; }
            if (n == "UnityEngine.CanvasGroup") { comps.Add("CanvasGroup"); continue; }
            if (n == "UnityEngine.Animator") { comps.Add("Animator"); continue; }
            comps.Add(n);
        }
        comps.Sort(System.StringComparer.Ordinal);

        // Sprite identity = texture asset path + sprite name (a texture holds several sprites).
        var sprite = string.Empty;
        var img = t.GetComponent<UnityEngine.UI.Image>();
        if (img != null)
        {
            sprite = img.sprite == null
                ? "MISSING_SPRITE"
                : AssetDatabase.GetAssetPath(img.sprite.texture) + "#" + img.sprite.name;
        }

        // TMP text with escapes collapsed to one line; font identity by asset path.
        var text = string.Empty;
        var font = string.Empty;
        var tmp = t.GetComponent<TMPro.TextMeshProUGUI>();
        if (tmp != null)
        {
            text = (tmp.text ?? string.Empty).Replace("\r", "").Replace("\n", "\\n").Replace("\t", " ");
            if (tmp.font != null) font = AssetDatabase.GetAssetPath(tmp.font);
        }

        // World AABB (all four corners) - the same convention the Unity chat snapshot uses, so a
        // rotated rect is not mis-measured the way corner[0]/corner[2] would be.
        var rect = t as RectTransform;
        var wr = string.Empty;
        var rt = string.Empty;
        if (rect != null)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var minX = Mathf.Min(Mathf.Min(corners[0].x, corners[1].x), Mathf.Min(corners[2].x, corners[3].x));
            var minY = Mathf.Min(Mathf.Min(corners[0].y, corners[1].y), Mathf.Min(corners[2].y, corners[3].y));
            var maxX = Mathf.Max(Mathf.Max(corners[0].x, corners[1].x), Mathf.Max(corners[2].x, corners[3].x));
            var maxY = Mathf.Max(Mathf.Max(corners[0].y, corners[1].y), Mathf.Max(corners[2].y, corners[3].y));
            wr = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0:F2},{1:F2},{2:F2},{3:F2}", minX, minY, maxX, maxY);
            // Reparent-invariant RectTransform values: these must survive any hierarchy move.
            rt = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0:F3},{1:F3}|{2:F3},{3:F3}|{4:F3},{5:F3}|{6:F3},{7:F3}|{8:F3},{9:F3},{10:F3}|{11:F3}",
                rect.sizeDelta.x, rect.sizeDelta.y,
                rect.anchorMin.x, rect.anchorMin.y,
                rect.anchorMax.x, rect.anchorMax.y,
                rect.pivot.x, rect.pivot.y,
                rect.localScale.x, rect.localScale.y, rect.localScale.z,
                rect.localRotation.eulerAngles.z);
        }

        var isRoot = PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject);
        var src = isRoot ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) : string.Empty;
        sb.AppendLine(string.Join("\t", new string[]
        {
            "NODE", path, t.childCount.ToString(), t.gameObject.activeSelf ? "1" : "0",
            (isRoot ? "1" : "0"), src, string.Join(",", comps), sprite, text, font, wr, rt
        }));

        for (var i = 0; i < t.childCount; i++) walk(t.GetChild(i), path);
    };
    walk(root.transform, string.Empty);

    // Target-owned images with a missing Sprite reference are a hard failure for the owner of this
    // Prefab; nested-instance content is reported separately (its owner is the other asset).
    var missingOwn = 0;
    var missingNested = 0;
    foreach (var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
    {
        if (image.sprite != null && image.sprite.texture != null) continue;
        var insideInstance = false;
        for (var c = image.transform; c != null && c != root.transform; c = c.parent)
        {
            if (PrefabUtility.IsAnyPrefabInstanceRoot(c.gameObject)) { insideInstance = true; break; }
        }
        if (insideInstance) missingNested++; else missingOwn++;
    }
    sb.AppendLine("MISSING_SPRITES_OWN " + missingOwn);
    sb.AppendLine("MISSING_SPRITES_NESTED " + missingNested);
    sb.AppendLine("DUMP_END");
    return sb.ToString();
}
finally
{
    PrefabUtility.UnloadPrefabContents(root);
}
