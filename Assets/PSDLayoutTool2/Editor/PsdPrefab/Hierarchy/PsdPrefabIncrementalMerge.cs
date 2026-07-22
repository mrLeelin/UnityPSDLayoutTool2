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
            var profileStableIds = new HashSet<string>((profile.nodes ?? new List<PsdHierarchyProfileNode>())
                .Where(node => node != null).Select(node => node.stableId), StringComparer.Ordinal);
            string unownedCandidateId = candidateByStableId.Keys.FirstOrDefault(id => !profileStableIds.Contains(id));
            if (!string.IsNullOrEmpty(unownedCandidateId))
                throw new PsdPrefabIncrementalMergeException(
                    "Candidate PSD layer is not classified by the hierarchy Profile: '" + unownedCandidateId + "'.");
            Dictionary<long, RectTransform> existingByLocalId = ResolveLoadedObjectsByLocalId(prefabPath, existingContents);
            var result = new PsdPrefabIncrementalMergeResult();

            // Profile identity is the sole ownership source after adoption.
            // lastKnownPath is intentionally excluded from matching.
            foreach (PsdHierarchyProfileNode record in (profile.nodes ?? new List<PsdHierarchyProfileNode>())
                         .Where(value => value != null && PsdStableLayerIdUtility.IsPersistable(value.stableId)))
            {
                if (record.localFileId <= 0L && !record.pendingCreation)
                    throw new PsdPrefabIncrementalMergeException(
                        "Existing Prefab adoption is ambiguous for PSD layer '" + record.stableId + "'.");
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
