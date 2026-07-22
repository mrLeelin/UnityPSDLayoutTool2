namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TMPro;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    public sealed class PsdPrefabIncrementalMergeException : InvalidOperationException
    {
        public PsdPrefabIncrementalMergeException(string message) : base(message) { }
    }

    public sealed class PsdPrefabIncrementalMergeResult
    {
        public readonly Dictionary<string, RectTransform> generatedByStableId =
            new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        public readonly Dictionary<string, RectTransform> groupsByKey =
            new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        public readonly List<string> pendingMissingStableIds = new List<string>();
    }

    /// <summary>
    /// Copies only importer-owned UI values from a freshly exported candidate
    /// into the already loaded target Prefab. Existing GameObjects are never
    /// replaced: their local file IDs, project components, business children
    /// and references therefore keep their identity.
    /// </summary>
    public static class PsdPrefabIncrementalMerge
    {
        public static PsdPrefabIncrementalMergeResult Merge(
            string prefabPath,
            GameObject existingContents,
            GameObject candidateRoot,
            IReadOnlyDictionary<string, RectTransform> candidateByStableId,
            PsdHierarchyProfile profile,
            PsdHierarchyPlan plan)
        {
            if (string.IsNullOrEmpty(prefabPath)) throw new ArgumentException("Prefab path is required.", "prefabPath");
            if (existingContents == null) throw new ArgumentNullException("existingContents");
            if (candidateRoot == null) throw new ArgumentNullException("candidateRoot");
            if (candidateByStableId == null) throw new ArgumentNullException("candidateByStableId");
            if (profile == null) throw new ArgumentNullException("profile");
            if (plan == null) throw new ArgumentNullException("plan");
            if (!profile.CheckSchema().canApply)
                throw new PsdPrefabIncrementalMergeException("The hierarchy Profile schema cannot be applied.");

            ValidateCandidateRegistry(candidateByStableId);
            var recordsByStableId = (profile.nodes ?? new List<PsdHierarchyProfileNode>())
                .Where(node => node != null).ToDictionary(node => node.stableId, StringComparer.Ordinal);
            var profileStableIds = new HashSet<string>(recordsByStableId.Keys, StringComparer.Ordinal);
            string unownedCandidateId = candidateByStableId.Keys.FirstOrDefault(id => !profileStableIds.Contains(id));
            if (!string.IsNullOrEmpty(unownedCandidateId))
                throw new PsdPrefabIncrementalMergeException(
                    "Candidate PSD layer is not classified by the hierarchy Profile: '" + unownedCandidateId + "'.");
            Dictionary<long, RectTransform> existingByLocalId = ResolveLoadedObjectsByLocalId(prefabPath, existingContents);
            Dictionary<RectTransform, long> localIdByExisting = existingByLocalId.ToDictionary(pair => pair.Value, pair => pair.Key);
            var result = new PsdPrefabIncrementalMergeResult();

            // Profile identity is the sole ownership source after adoption.
            // lastKnownPath is intentionally excluded from matching.
            foreach (PsdHierarchyProfileNode record in (profile.nodes ?? new List<PsdHierarchyProfileNode>())
                         .Where(value => value != null && PsdStableLayerIdUtility.IsPersistable(value.stableId)))
            {
                if (record.ownership == PsdHierarchyNodeOwnership.Unknown)
                    throw new PsdPrefabIncrementalMergeException(
                        "Hierarchy Profile ownership requires explicit adoption or migration for PSD layer '" + record.stableId + "'.");
                if (record.ownership == PsdHierarchyNodeOwnership.NotEmitted) continue;
                if (record.localFileId <= 0L && !record.pendingCreation)
                {
                    RectTransform candidate;
                    if (!candidateByStableId.TryGetValue(record.stableId, out candidate))
                        throw new PsdPrefabIncrementalMergeException(
                            "Generated adoption candidate is missing for PSD layer '" + record.stableId + "'.");
                    RectTransform adopted = AdoptDeterministically(
                        candidateRoot.transform as RectTransform, existingContents.transform as RectTransform, candidate);
                    long adoptedLocalId;
                    if (!localIdByExisting.TryGetValue(adopted, out adoptedLocalId))
                        throw new PsdPrefabIncrementalMergeException("Adopted object has no persistent local file ID.");
                    record.localFileId = adoptedLocalId;
                    record.lastKnownPath = HierarchyPath(adopted, existingContents.transform);
                    result.generatedByStableId.Add(record.stableId, adopted);
                    continue;
                }
                if (record.pendingCreation)
                {
                    if (record.localFileId > 0L || !candidateByStableId.ContainsKey(record.stableId))
                        throw new PsdPrefabIncrementalMergeException(
                            "Pending generated identity is inconsistent for PSD layer '" + record.stableId + "'.");
                    continue;
                }
                RectTransform retained;
                if (!existingByLocalId.TryGetValue(record.localFileId, out retained))
                    throw new PsdPrefabIncrementalMergeException(
                        "The recorded generated object is missing for PSD layer '" + record.stableId + "'.");
                if (!result.generatedByStableId.TryAdd(record.stableId, retained))
                    throw new PsdPrefabIncrementalMergeException("The Profile contains a duplicate PSD layer identity.");
            }

            foreach (PsdHierarchyProfileGroup group in profile.groups ?? new List<PsdHierarchyProfileGroup>())
            {
                if (group == null || group.localFileId <= 0L) continue;
                RectTransform retained;
                if (!existingByLocalId.TryGetValue(group.localFileId, out retained))
                    throw new PsdPrefabIncrementalMergeException(
                        "The recorded organizer group is missing: '" + group.key + "'.");
                if (!result.groupsByKey.TryAdd(group.key, retained))
                    throw new PsdPrefabIncrementalMergeException("The Profile contains a duplicate organizer group key.");
            }

            Dictionary<RectTransform, string> candidateIds = candidateByStableId.ToDictionary(pair => pair.Value, pair => pair.Key);
            foreach (KeyValuePair<string, RectTransform> pair in candidateByStableId
                         .OrderBy(pair => Depth(pair.Value)))
            {
                RectTransform target;
                if (!result.generatedByStableId.TryGetValue(pair.Key, out target))
                {
                    RectTransform parent = ResolveNewParent(
                        pair.Value, candidateRoot.transform as RectTransform, existingContents.transform as RectTransform,
                        candidateIds, result.generatedByStableId);
                    target = CreateGeneratedObject(pair.Value, parent);
                    result.generatedByStableId.Add(pair.Key, target);
                }
                CopyImporterOwnedValues(pair.Value, target);
            }

            foreach (string stableId in result.generatedByStableId.Keys
                         .Where(id => !candidateByStableId.ContainsKey(id)).OrderBy(id => id, StringComparer.Ordinal))
                result.pendingMissingStableIds.Add(stableId);

            RectTransform existingRoot = existingContents.transform as RectTransform;
            if (existingRoot == null)
                throw new PsdPrefabIncrementalMergeException("Incremental hierarchy merge requires a RectTransform root.");
            PsdHierarchyApplyResult apply = PsdHierarchyApplier.Apply(
                existingRoot, plan, result.generatedByStableId, result.groupsByKey);
            result.groupsByKey.Clear();
            foreach (KeyValuePair<string, RectTransform> pair in apply.groupsByKey) result.groupsByKey.Add(pair.Key, pair.Value);
            return result;
        }

        private static RectTransform AdoptDeterministically(
            RectTransform candidateRoot,
            RectTransform existingRoot,
            RectTransform candidate)
        {
            if (candidateRoot == null || existingRoot == null)
                throw new PsdPrefabIncrementalMergeException("First adoption requires RectTransform roots.");
            int[] indexPath = SiblingIndexPath(candidate, candidateRoot);
            RectTransform exact = FollowSiblingIndexPath(existingRoot, indexPath);
            if (exact != null && IsAdoptionEvidenceEqual(candidate, exact) && !HasProjectComponents(exact))
                return exact;

            // Names and resource references are useful diagnostics, but never
            // sufficient when source hierarchy/sibling evidence does not agree.
            int visualMatches = existingRoot.GetComponentsInChildren<RectTransform>(true)
                .Where(value => value != existingRoot && !HasProjectComponents(value))
                .Count(value => IsVisualEvidenceEqual(candidate, value));
            throw new PsdPrefabIncrementalMergeException(visualMatches > 1
                ? "First adoption is ambiguous: multiple same-name/resource objects match '" + candidate.name + "'."
                : "First adoption has no unique full hierarchy match for '" + candidate.name + "'.");
        }

        private static bool IsAdoptionEvidenceEqual(RectTransform source, RectTransform target)
        {
            return IsVisualEvidenceEqual(source, target) &&
                   source.GetSiblingIndex() == target.GetSiblingIndex() &&
                   source.anchorMin == target.anchorMin && source.anchorMax == target.anchorMax &&
                   source.pivot == target.pivot && source.anchoredPosition3D == target.anchoredPosition3D &&
                   source.sizeDelta == target.sizeDelta && source.localRotation == target.localRotation &&
                   source.localScale == target.localScale;
        }

        private static bool IsVisualEvidenceEqual(RectTransform source, RectTransform target)
        {
            if (!string.Equals(source.name, target.name, StringComparison.Ordinal) ||
                source.gameObject.activeSelf != target.gameObject.activeSelf) return false;
            Image sourceImage = source.GetComponent<Image>();
            Image targetImage = target.GetComponent<Image>();
            if ((sourceImage == null) != (targetImage == null)) return false;
            if (sourceImage != null && (sourceImage.sprite != targetImage.sprite ||
                                       sourceImage.material != targetImage.material ||
                                       sourceImage.type != targetImage.type || sourceImage.color != targetImage.color)) return false;
            TextMeshProUGUI sourceText = source.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI targetText = target.GetComponent<TextMeshProUGUI>();
            if ((sourceText == null) != (targetText == null)) return false;
            return sourceText == null || (sourceText.font == targetText.font &&
                                          sourceText.fontSharedMaterial == targetText.fontSharedMaterial &&
                                          string.Equals(sourceText.text, targetText.text, StringComparison.Ordinal) &&
                                          sourceText.fontStyle == targetText.fontStyle);
        }

        private static bool HasProjectComponents(RectTransform target)
        {
            return target.GetComponents<Component>().Any(component =>
                !(component is RectTransform) && !(component is CanvasRenderer) &&
                !(component is Image) && !(component is TextMeshProUGUI) &&
                !(component is BaseMeshEffect) && !(component is AspectRatioFitter));
        }

        private static int[] SiblingIndexPath(Transform target, Transform root)
        {
            var indices = new Stack<int>();
            for (Transform cursor = target; cursor != null && cursor != root; cursor = cursor.parent)
                indices.Push(cursor.GetSiblingIndex());
            return indices.ToArray();
        }

        private static RectTransform FollowSiblingIndexPath(RectTransform root, IEnumerable<int> indices)
        {
            Transform cursor = root;
            foreach (int index in indices)
            {
                if (index < 0 || index >= cursor.childCount) return null;
                cursor = cursor.GetChild(index);
            }
            return cursor as RectTransform;
        }

        private static string HierarchyPath(Transform target, Transform root)
        {
            var names = new Stack<string>();
            for (Transform cursor = target; cursor != null; cursor = cursor.parent)
            {
                names.Push(cursor.name);
                if (cursor == root) break;
            }
            return string.Join("/", names.ToArray());
        }

        private static Dictionary<long, RectTransform> ResolveLoadedObjectsByLocalId(string prefabPath, GameObject loadedRoot)
        {
            GameObject persistentRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (persistentRoot == null) throw new PsdPrefabIncrementalMergeException("Existing target Prefab was not found.");
            Transform[] persistent = persistentRoot.GetComponentsInChildren<Transform>(true);
            Transform[] loaded = loadedRoot.GetComponentsInChildren<Transform>(true);
            if (persistent.Length != loaded.Length)
                throw new PsdPrefabIncrementalMergeException("Loaded Prefab contents do not match the target asset hierarchy.");

            var result = new Dictionary<long, RectTransform>();
            for (int index = 0; index < persistent.Length; index++)
            {
                string guid;
                long localId;
                RectTransform loadedRect = loaded[index] as RectTransform;
                if (loadedRect == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        persistent[index].gameObject, out guid, out localId) || localId <= 0L) continue;
                if (!result.TryAdd(localId, loadedRect))
                    throw new PsdPrefabIncrementalMergeException("Target Prefab contains a duplicate local file ID.");
            }
            return result;
        }

        private static RectTransform ResolveNewParent(
            RectTransform candidate,
            RectTransform candidateRoot,
            RectTransform existingRoot,
            Dictionary<RectTransform, string> candidateIds,
            Dictionary<string, RectTransform> existingByStableId)
        {
            RectTransform candidateParent = candidate.parent as RectTransform;
            if (candidateParent == null || candidateParent == candidateRoot) return existingRoot;
            string parentStableId;
            RectTransform targetParent;
            if (!candidateIds.TryGetValue(candidateParent, out parentStableId) ||
                !existingByStableId.TryGetValue(parentStableId, out targetParent))
                throw new PsdPrefabIncrementalMergeException(
                    "A new generated object has an unstable or missing generated parent: '" + candidate.name + "'.");
            return targetParent;
        }

        private static RectTransform CreateGeneratedObject(RectTransform candidate, RectTransform parent)
        {
            if (parent == null) throw new PsdPrefabIncrementalMergeException("A new generated object has no target parent.");
            var target = new GameObject(candidate.name, typeof(RectTransform)).GetComponent<RectTransform>();
            target.SetParent(parent, false);
            if (candidate.GetComponent<CanvasRenderer>() != null) target.gameObject.AddComponent<CanvasRenderer>();
            if (candidate.GetComponent<Image>() != null) target.gameObject.AddComponent<Image>();
            if (candidate.GetComponent<TextMeshProUGUI>() != null) target.gameObject.AddComponent<TextMeshProUGUI>();
            return target;
        }

        private static void CopyImporterOwnedValues(RectTransform source, RectTransform target)
        {
            bool sourceHasImage = source.GetComponent<Image>() != null;
            bool targetHasImage = target.GetComponent<Image>() != null;
            bool sourceHasText = source.GetComponent<TextMeshProUGUI>() != null;
            bool targetHasText = target.GetComponent<TextMeshProUGUI>() != null;
            if ((targetHasImage || targetHasText) &&
                (sourceHasImage != targetHasImage || sourceHasText != targetHasText))
                throw new PsdPrefabIncrementalMergeException(
                    "Generated visual component type changed for retained object '" + target.name + "'.");

            target.gameObject.name = source.gameObject.name;
            target.gameObject.SetActive(source.gameObject.activeSelf);
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition3D = source.anchoredPosition3D;
            target.sizeDelta = source.sizeDelta;
            target.offsetMin = source.offsetMin;
            target.offsetMax = source.offsetMax;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;

            Image sourceImage = source.GetComponent<Image>();
            Image targetImage = target.GetComponent<Image>();
            if (sourceImage != null)
            {
                if (targetImage == null) targetImage = target.gameObject.AddComponent<Image>();
                targetImage.sprite = sourceImage.sprite;
                targetImage.overrideSprite = sourceImage.overrideSprite;
                targetImage.color = sourceImage.color;
                targetImage.material = sourceImage.material;
                targetImage.raycastTarget = sourceImage.raycastTarget;
                targetImage.raycastPadding = sourceImage.raycastPadding;
                targetImage.maskable = sourceImage.maskable;
                targetImage.type = sourceImage.type;
                targetImage.preserveAspect = sourceImage.preserveAspect;
                targetImage.fillCenter = sourceImage.fillCenter;
                targetImage.fillMethod = sourceImage.fillMethod;
                targetImage.fillAmount = sourceImage.fillAmount;
                targetImage.fillClockwise = sourceImage.fillClockwise;
                targetImage.fillOrigin = sourceImage.fillOrigin;
                targetImage.pixelsPerUnitMultiplier = sourceImage.pixelsPerUnitMultiplier;
            }

            TextMeshProUGUI sourceText = source.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI targetText = target.GetComponent<TextMeshProUGUI>();
            if (sourceText == null) return;
            if (targetText == null) targetText = target.gameObject.AddComponent<TextMeshProUGUI>();
            targetText.text = sourceText.text;
            targetText.font = sourceText.font;
            targetText.fontSharedMaterial = sourceText.fontSharedMaterial;
            targetText.fontSize = sourceText.fontSize;
            targetText.fontStyle = sourceText.fontStyle;
            targetText.color = sourceText.color;
            targetText.alignment = sourceText.alignment;
            targetText.richText = sourceText.richText;
            targetText.textWrappingMode = sourceText.textWrappingMode;
            targetText.overflowMode = sourceText.overflowMode;
            targetText.raycastTarget = sourceText.raycastTarget;
            targetText.characterSpacing = sourceText.characterSpacing;
            targetText.wordSpacing = sourceText.wordSpacing;
            targetText.lineSpacing = sourceText.lineSpacing;
            targetText.paragraphSpacing = sourceText.paragraphSpacing;
            targetText.enableAutoSizing = sourceText.enableAutoSizing;
            targetText.fontSizeMin = sourceText.fontSizeMin;
            targetText.fontSizeMax = sourceText.fontSizeMax;
            targetText.margin = sourceText.margin;
        }

        private static void ValidateCandidateRegistry(IReadOnlyDictionary<string, RectTransform> registry)
        {
            var seen = new HashSet<RectTransform>();
            foreach (KeyValuePair<string, RectTransform> pair in registry)
            {
                if (!PsdStableLayerIdUtility.IsPersistable(pair.Key) || pair.Value == null || !seen.Add(pair.Value))
                    throw new PsdPrefabIncrementalMergeException("Candidate registry must contain unique native PSD identities.");
            }
        }

        private static int Depth(Transform value)
        {
            int depth = 0;
            for (Transform cursor = value; cursor != null; cursor = cursor.parent) depth++;
            return depth;
        }
    }
}
