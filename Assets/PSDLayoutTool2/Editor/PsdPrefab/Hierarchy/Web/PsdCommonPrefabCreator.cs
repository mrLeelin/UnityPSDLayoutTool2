namespace PsdLayoutTool2.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using TMPro;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    internal static class PsdCommonPrefabCreator
    {
        public static void Create(
            string targetPrefabPath,
            string sourcePsdGuid,
            IReadOnlyList<PsdHierarchyWebPrefabCandidateDto> candidates)
        {
            if (string.IsNullOrWhiteSpace(targetPrefabPath))
                throw new ArgumentException("Target Prefab path is required.", nameof(targetPrefabPath));
            if (string.IsNullOrWhiteSpace(sourcePsdGuid))
                throw new ArgumentException("Source PSD GUID is required.", nameof(sourcePsdGuid));
            if (candidates == null || candidates.Count == 0)
                throw new ArgumentException("At least one Prefab candidate is required.", nameof(candidates));

            string profilePath = PsdPrefabTransactionalSave.GetProfilePath(targetPrefabPath, sourcePsdGuid);
            PsdHierarchyProfile persistedProfile =
                PsdPrefabTransactionalSave.ResolveBoundProfileForImport(profilePath, targetPrefabPath);
            if (persistedProfile == null)
                throw new InvalidOperationException("The target Prefab has no bound hierarchy Profile.");

            GameObject loadedRoot = PrefabUtility.LoadPrefabContents(targetPrefabPath);
            PsdHierarchyProfile workingProfile = UnityEngine.Object.Instantiate(persistedProfile);
            var createdAssetPaths = new List<string>();
            try
            {
                Dictionary<long, Transform> loadedByLocalId = ResolveLoadedByLocalId(targetPrefabPath, loadedRoot);
                Dictionary<string, RectTransform> generatedByStableId = ResolveProfileNodes(
                    workingProfile.nodes, loadedByLocalId);
                Dictionary<string, RectTransform> groupsByKey = ResolveProfileGroups(
                    workingProfile.groups, loadedByLocalId);
                var replacementByObject = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
                var replacements = new List<Replacement>();
                var claimedStableIds = new HashSet<string>(StringComparer.Ordinal);

                foreach (PsdHierarchyWebPrefabCandidateDto candidate in candidates)
                {
                    List<string> instanceIds = (candidate.instanceStableIds ?? new List<string>())
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
                    if (instanceIds.Count == 0)
                        throw new InvalidOperationException("A selected Prefab candidate has no instances.");
                    if (instanceIds.Any(value => !claimedStableIds.Add(value)))
                        throw new InvalidOperationException("Selected Prefab candidates overlap.");

                    List<RectTransform> instanceRoots = instanceIds.Select(stableId =>
                    {
                        RectTransform value;
                        if (!generatedByStableId.TryGetValue(stableId, out value))
                            throw new InvalidOperationException(
                                "The selected Prefab instance is missing from the bound Profile: " + stableId);
                        if (value == loadedRoot.transform)
                            throw new InvalidOperationException("The target Prefab root cannot become a common Prefab.");
                        return value;
                    }).ToList();

                    string commonPath = CreateCommonAssetPath(targetPrefabPath, candidate, instanceRoots[0]);
                    GameObject saved = PrefabUtility.SaveAsPrefabAsset(instanceRoots[0].gameObject, commonPath);
                    if (saved == null)
                        throw new InvalidOperationException("Unity failed to save the common Prefab: " + commonPath);
                    createdAssetPaths.Add(commonPath);

                    foreach (RectTransform oldRoot in instanceRoots)
                    {
                        GameObject newRootObject = PrefabUtility.InstantiatePrefab(saved, loadedRoot.scene) as GameObject;
                        if (newRootObject == null)
                            throw new InvalidOperationException("Unity failed to instantiate the common Prefab.");
                        Transform parent = oldRoot.parent;
                        int siblingIndex = oldRoot.GetSiblingIndex();
                        newRootObject.transform.SetParent(parent, false);
                        newRootObject.name = oldRoot.name;
                        BuildReplacementMap(oldRoot, newRootObject.transform, replacementByObject);
                        CopyTransformTree(oldRoot, newRootObject.transform);
                        CopyInstanceOverrides(oldRoot, newRootObject.transform, replacementByObject);
                        replacements.Add(new Replacement(oldRoot, newRootObject.transform, parent, siblingIndex));
                    }
                }

                foreach (Replacement replacement in replacements)
                    UnityEngine.Object.DestroyImmediate(replacement.oldRoot.gameObject);
                foreach (Replacement replacement in replacements
                             .OrderBy(value => value.parent.GetInstanceID())
                             .ThenBy(value => value.siblingIndex))
                    replacement.newRoot.SetSiblingIndex(replacement.siblingIndex);

                RemapProfileTargets(generatedByStableId, replacementByObject);
                RemapProfileTargets(groupsByKey, replacementByObject);
                PsdPrefabTransactionalSave.Save(
                    targetPrefabPath,
                    loadedRoot,
                    profilePath,
                    workingProfile,
                    generatedByStableId,
                    groupsByKey,
                    null,
                    null);
                createdAssetPaths.Clear();
            }
            catch
            {
                foreach (string path in createdAssetPaths) AssetDatabase.DeleteAsset(path);
                throw;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(workingProfile);
                PrefabUtility.UnloadPrefabContents(loadedRoot);
            }
        }

        private static Dictionary<long, Transform> ResolveLoadedByLocalId(
            string prefabPath,
            GameObject loadedRoot)
        {
            GameObject persistentRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (persistentRoot == null) throw new InvalidOperationException("Target Prefab cannot be loaded.");
            Transform[] persistent = persistentRoot.GetComponentsInChildren<Transform>(true);
            Transform[] loaded = loadedRoot.GetComponentsInChildren<Transform>(true);
            if (persistent.Length != loaded.Length)
                throw new InvalidOperationException("Loaded Prefab contents do not match the target asset.");
            var result = new Dictionary<long, Transform>();
            for (int index = 0; index < persistent.Length; index++)
            {
                string guid;
                long localId;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        persistent[index].gameObject, out guid, out localId) || localId <= 0L)
                    continue;
                if (!result.TryAdd(localId, loaded[index]))
                    throw new InvalidOperationException("Target Prefab contains a duplicate local file ID.");
            }
            return result;
        }

        private static Dictionary<string, RectTransform> ResolveProfileNodes(
            IEnumerable<PsdHierarchyProfileNode> records,
            IReadOnlyDictionary<long, Transform> loadedByLocalId)
        {
            var result = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            foreach (PsdHierarchyProfileNode record in records ?? Enumerable.Empty<PsdHierarchyProfileNode>())
            {
                Transform target;
                if (record == null || record.ownership != PsdHierarchyNodeOwnership.Generated ||
                    record.localFileId <= 0L || !loadedByLocalId.TryGetValue(record.localFileId, out target))
                    continue;
                RectTransform rect = target as RectTransform;
                if (rect == null || !result.TryAdd(record.stableId, rect))
                    throw new InvalidOperationException("The hierarchy Profile contains an invalid generated node.");
            }
            return result;
        }

        private static Dictionary<string, RectTransform> ResolveProfileGroups(
            IEnumerable<PsdHierarchyProfileGroup> records,
            IReadOnlyDictionary<long, Transform> loadedByLocalId)
        {
            var result = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            foreach (PsdHierarchyProfileGroup record in records ?? Enumerable.Empty<PsdHierarchyProfileGroup>())
            {
                Transform target;
                if (record == null || record.localFileId <= 0L ||
                    !loadedByLocalId.TryGetValue(record.localFileId, out target)) continue;
                RectTransform rect = target as RectTransform;
                if (rect == null || !result.TryAdd(record.key, rect))
                    throw new InvalidOperationException("The hierarchy Profile contains an invalid organizer group.");
            }
            return result;
        }

        private static string CreateCommonAssetPath(
            string targetPrefabPath,
            PsdHierarchyWebPrefabCandidateDto candidate,
            RectTransform representative)
        {
            string targetDirectory = Path.GetDirectoryName(targetPrefabPath).Replace('\\', '/');
            string targetName = Path.GetFileNameWithoutExtension(targetPrefabPath);
            string folder = targetDirectory + "/" + targetName + "_CommonPrefabs";
            EnsureAssetFolder(folder);
            string name = SanitizeFileName(candidate.proposedName);
            if (string.IsNullOrEmpty(name)) name = SanitizeFileName(representative.name);
            if (string.IsNullOrEmpty(name)) name = "CommonPrefab";
            string path = folder + "/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                throw new InvalidOperationException("A common Prefab already exists at: " + path);
            return path;
        }

        private static void EnsureAssetFolder(string path)
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains(".."))
                throw new InvalidOperationException("Common Prefab output must stay inside Assets.");
            string current = "Assets";
            foreach (string segment in path.Substring("Assets/".Length).Split('/'))
            {
                if (string.IsNullOrEmpty(segment)) continue;
                string next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next) && string.IsNullOrEmpty(AssetDatabase.CreateFolder(current, segment)))
                    throw new InvalidOperationException("Unable to create common Prefab folder: " + next);
                current = next;
            }
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
            return new string((value ?? string.Empty).Trim().Where(character => !invalid.Contains(character)).ToArray());
        }

        private static void BuildReplacementMap(
            Transform oldTransform,
            Transform newTransform,
            IDictionary<UnityEngine.Object, UnityEngine.Object> map)
        {
            if (oldTransform.childCount != newTransform.childCount)
                throw new InvalidOperationException("Common Prefab instances have incompatible child structures.");
            map.Add(oldTransform.gameObject, newTransform.gameObject);
            map.Add(oldTransform, newTransform);
            Component[] oldComponents = oldTransform.GetComponents<Component>()
                .Where(component => component != null && !(component is Transform)).ToArray();
            Component[] newComponents = newTransform.GetComponents<Component>()
                .Where(component => component != null && !(component is Transform)).ToArray();
            if (oldComponents.Length != newComponents.Length)
                throw new InvalidOperationException("Common Prefab instances have incompatible components.");
            for (int index = 0; index < oldComponents.Length; index++)
            {
                if (oldComponents[index].GetType() != newComponents[index].GetType())
                    throw new InvalidOperationException("Common Prefab instances have incompatible component types.");
                map.Add(oldComponents[index], newComponents[index]);
            }
            for (int index = 0; index < oldTransform.childCount; index++)
                BuildReplacementMap(oldTransform.GetChild(index), newTransform.GetChild(index), map);
        }

        private static void CopyTransformTree(Transform oldTransform, Transform newTransform)
        {
            CopyTransform(oldTransform, newTransform);
            for (int index = 0; index < oldTransform.childCount; index++)
                CopyTransformTree(oldTransform.GetChild(index), newTransform.GetChild(index));
        }

        private static void CopyTransform(Transform source, Transform target)
        {
            RectTransform sourceRect = source as RectTransform;
            RectTransform targetRect = target as RectTransform;
            if ((sourceRect == null) != (targetRect == null))
                throw new InvalidOperationException("Common Prefab instances have incompatible transform types.");
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
            if (sourceRect == null) return;
            targetRect.anchorMin = sourceRect.anchorMin;
            targetRect.anchorMax = sourceRect.anchorMax;
            targetRect.pivot = sourceRect.pivot;
            targetRect.sizeDelta = sourceRect.sizeDelta;
            targetRect.anchoredPosition3D = sourceRect.anchoredPosition3D;
        }

        private static void CopyInstanceOverrides(
            Transform source,
            Transform target,
            IReadOnlyDictionary<UnityEngine.Object, UnityEngine.Object> replacements)
        {
            foreach (Component sourceComponent in source.GetComponents<Component>())
            {
                if (sourceComponent == null || sourceComponent is Transform || sourceComponent is CanvasRenderer)
                    continue;
                Component targetComponent = (Component)replacements[sourceComponent];
                if (sourceComponent is Image sourceImage && targetComponent is Image targetImage)
                {
                    targetImage.sprite = sourceImage.sprite;
                    targetImage.overrideSprite = sourceImage.overrideSprite;
                    targetImage.color = sourceImage.color;
                    targetImage.material = sourceImage.material;
                    targetImage.raycastTarget = sourceImage.raycastTarget;
                    targetImage.maskable = sourceImage.maskable;
                    targetImage.type = sourceImage.type;
                    targetImage.preserveAspect = sourceImage.preserveAspect;
                    targetImage.fillCenter = sourceImage.fillCenter;
                    targetImage.fillMethod = sourceImage.fillMethod;
                    targetImage.fillAmount = sourceImage.fillAmount;
                    targetImage.fillClockwise = sourceImage.fillClockwise;
                    targetImage.fillOrigin = sourceImage.fillOrigin;
                    targetImage.useSpriteMesh = sourceImage.useSpriteMesh;
                    targetImage.pixelsPerUnitMultiplier = sourceImage.pixelsPerUnitMultiplier;
                }
                else if (sourceComponent is Text sourceText && targetComponent is Text targetText)
                {
                    targetText.text = sourceText.text;
                    targetText.font = sourceText.font;
                    targetText.fontSize = sourceText.fontSize;
                    targetText.fontStyle = sourceText.fontStyle;
                    targetText.alignment = sourceText.alignment;
                    targetText.color = sourceText.color;
                    targetText.material = sourceText.material;
                    targetText.raycastTarget = sourceText.raycastTarget;
                    targetText.lineSpacing = sourceText.lineSpacing;
                    targetText.supportRichText = sourceText.supportRichText;
                    targetText.resizeTextForBestFit = sourceText.resizeTextForBestFit;
                    targetText.resizeTextMinSize = sourceText.resizeTextMinSize;
                    targetText.resizeTextMaxSize = sourceText.resizeTextMaxSize;
                }
                else if (sourceComponent is TMP_Text sourceTmp && targetComponent is TMP_Text targetTmp)
                {
                    targetTmp.text = sourceTmp.text;
                    targetTmp.font = sourceTmp.font;
                    targetTmp.fontSharedMaterial = sourceTmp.fontSharedMaterial;
                    targetTmp.fontSize = sourceTmp.fontSize;
                    targetTmp.fontStyle = sourceTmp.fontStyle;
                    targetTmp.alignment = sourceTmp.alignment;
                    targetTmp.color = sourceTmp.color;
                    targetTmp.enableAutoSizing = sourceTmp.enableAutoSizing;
                    targetTmp.fontSizeMin = sourceTmp.fontSizeMin;
                    targetTmp.fontSizeMax = sourceTmp.fontSizeMax;
                    targetTmp.richText = sourceTmp.richText;
                    targetTmp.raycastTarget = sourceTmp.raycastTarget;
                }
                else if (sourceComponent is Button sourceButton && targetComponent is Button targetButton)
                {
                    targetButton.interactable = sourceButton.interactable;
                    targetButton.transition = sourceButton.transition;
                    targetButton.colors = sourceButton.colors;
                    targetButton.spriteState = sourceButton.spriteState;
                    targetButton.animationTriggers = sourceButton.animationTriggers;
                    targetButton.navigation = RemapNavigation(sourceButton.navigation, replacements);
                    UnityEngine.Object mappedGraphic;
                    if (sourceButton.targetGraphic != null &&
                        replacements.TryGetValue(sourceButton.targetGraphic, out mappedGraphic))
                        targetButton.targetGraphic = mappedGraphic as Graphic;
                }
                else if (sourceComponent is Shadow sourceShadow && targetComponent is Shadow targetShadow)
                {
                    targetShadow.effectColor = sourceShadow.effectColor;
                    targetShadow.effectDistance = sourceShadow.effectDistance;
                    targetShadow.useGraphicAlpha = sourceShadow.useGraphicAlpha;
                }
                else if (sourceComponent is AspectRatioFitter sourceAspect &&
                         targetComponent is AspectRatioFitter targetAspect)
                {
                    targetAspect.aspectMode = sourceAspect.aspectMode;
                    targetAspect.aspectRatio = sourceAspect.aspectRatio;
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(targetComponent);
            }
            for (int index = 0; index < source.childCount; index++)
                CopyInstanceOverrides(source.GetChild(index), target.GetChild(index), replacements);
        }

        private static Navigation RemapNavigation(
            Navigation source,
            IReadOnlyDictionary<UnityEngine.Object, UnityEngine.Object> replacements)
        {
            source.selectOnUp = RemapSelectable(source.selectOnUp, replacements);
            source.selectOnDown = RemapSelectable(source.selectOnDown, replacements);
            source.selectOnLeft = RemapSelectable(source.selectOnLeft, replacements);
            source.selectOnRight = RemapSelectable(source.selectOnRight, replacements);
            return source;
        }

        private static Selectable RemapSelectable(
            Selectable source,
            IReadOnlyDictionary<UnityEngine.Object, UnityEngine.Object> replacements)
        {
            UnityEngine.Object replacement;
            return source != null && replacements.TryGetValue(source, out replacement)
                ? replacement as Selectable
                : source;
        }

        private static void RemapProfileTargets(
            IDictionary<string, RectTransform> targets,
            IReadOnlyDictionary<UnityEngine.Object, UnityEngine.Object> replacements)
        {
            foreach (string key in targets.Keys.ToArray())
            {
                UnityEngine.Object replacement;
                if (replacements.TryGetValue(targets[key], out replacement))
                    targets[key] = replacement as RectTransform;
            }
        }

        private sealed class Replacement
        {
            public Replacement(Transform oldRoot, Transform newRoot, Transform parent, int siblingIndex)
            {
                this.oldRoot = oldRoot;
                this.newRoot = newRoot;
                this.parent = parent;
                this.siblingIndex = siblingIndex;
            }

            public readonly Transform oldRoot;
            public readonly Transform newRoot;
            public readonly Transform parent;
            public readonly int siblingIndex;
        }
    }
}
