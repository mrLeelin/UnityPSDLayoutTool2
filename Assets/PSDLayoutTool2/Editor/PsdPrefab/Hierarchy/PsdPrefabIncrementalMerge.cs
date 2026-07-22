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
        public static List<PsdHierarchyPrefabNodeMetadata> BuildProfilePrefabMetadata(
            string prefabPath,
            PsdHierarchyProfile profile)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (root == null) throw new PsdPrefabIncrementalMergeException("Existing target Prefab was not found.");
            var byLocalId = new Dictionary<long, RectTransform>();
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                string guid;
                long localId;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(rect.gameObject, out guid, out localId) && localId > 0L)
                    byLocalId[localId] = rect;
            }
            var stableIdByTransform = new Dictionary<Transform, string>();
            foreach (PsdHierarchyProfileNode record in profile.nodes ?? new List<PsdHierarchyProfileNode>())
            {
                RectTransform rect;
                if (record != null && record.ownership == PsdHierarchyNodeOwnership.Generated &&
                    record.localFileId > 0L && byLocalId.TryGetValue(record.localFileId, out rect))
                    stableIdByTransform[rect] = record.stableId;
            }
            var result = new List<PsdHierarchyPrefabNodeMetadata>();
            foreach (KeyValuePair<Transform, string> pair in stableIdByTransform.OrderBy(pair => pair.Value, StringComparer.Ordinal))
            {
                string parentStableId = string.Empty;
                string protectedBoundaryStableId = string.Empty;
                for (Transform cursor = pair.Key.parent; cursor != null; cursor = cursor.parent)
                {
                    string ancestorId;
                    if (stableIdByTransform.TryGetValue(cursor, out ancestorId))
                    {
                        if (string.IsNullOrEmpty(parentStableId)) parentStableId = ancestorId;
                        if (IsProtectedBoundary(cursor.gameObject))
                        {
                            protectedBoundaryStableId = ancestorId;
                            break;
                        }
                    }
                }
                result.Add(new PsdHierarchyPrefabNodeMetadata
                {
                    stableId = pair.Value,
                    parentStableId = parentStableId,
                    siblingIndex = pair.Key.GetSiblingIndex(),
                    hierarchyPath = HierarchyPath(pair.Key, root.transform),
                    componentTypes = pair.Key.GetComponents<Component>()
                        .Select(component => component == null ? "<missing>" : component.GetType().AssemblyQualifiedName).ToList(),
                    hasProjectComponents = HasProjectComponents(
                        pair.Key as RectTransform,
                        (profile.nodes ?? new List<PsdHierarchyProfileNode>())
                        .First(node => node != null && string.Equals(node.stableId, pair.Value, StringComparison.Ordinal))
                        .importerOwnedComponentTypes),
                    isProtectedBoundary = IsProtectedBoundary(pair.Key.gameObject),
                    protectedBoundaryStableId = IsProtectedBoundary(pair.Key.gameObject) ? pair.Value : protectedBoundaryStableId
                });
            }
            return result;
        }

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
            var created = new List<RectTransform>();
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
                    created.Add(target);
                }
            }
            try
            {
                var targetByCandidate = candidateByStableId.ToDictionary(
                    pair => pair.Value, pair => result.generatedByStableId[pair.Key]);
                foreach (KeyValuePair<string, RectTransform> pair in candidateByStableId)
                {
                    PsdHierarchyProfileNode record = recordsByStableId[pair.Key];
                    ValidateComponentSynchronization(existingContents.transform, pair.Value,
                        result.generatedByStableId[pair.Key], record, targetByCandidate);
                }
                foreach (KeyValuePair<string, RectTransform> pair in candidateByStableId)
                {
                    PsdHierarchyProfileNode record = recordsByStableId[pair.Key];
                    SynchronizeImporterOwnedComponents(existingContents.transform, pair.Value,
                        result.generatedByStableId[pair.Key], record, targetByCandidate);
                    CopyImporterOwnedValues(pair.Value, result.generatedByStableId[pair.Key]);
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
            catch
            {
                for (int index = created.Count - 1; index >= 0; index--)
                    if (created[index] != null) UnityEngine.Object.DestroyImmediate(created[index].gameObject);
                throw;
            }
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
            if (exact != null && IsAdoptionEvidenceEqual(candidate, exact) && !HasUnexpectedProjectComponents(exact, candidate))
                return exact;

            // Names and resource references are useful diagnostics, but never
            // sufficient when source hierarchy/sibling evidence does not agree.
            int visualMatches = existingRoot.GetComponentsInChildren<RectTransform>(true)
                .Where(value => value != existingRoot && !HasUnexpectedProjectComponents(value, candidate))
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
            if (sourceText != null && (sourceText.font != targetText.font ||
                                       sourceText.fontSharedMaterial != targetText.fontSharedMaterial ||
                                       !string.Equals(sourceText.text, targetText.text, StringComparison.Ordinal) ||
                                       sourceText.fontStyle != targetText.fontStyle)) return false;
            return LegacyEvidenceEqual(source, target);
        }

        private static bool LegacyEvidenceEqual(RectTransform source, RectTransform target)
        {
            Text sourceText = source.GetComponent<Text>();
            Text targetText = target.GetComponent<Text>();
            if ((sourceText == null) != (targetText == null)) return false;
            if (sourceText != null && (!string.Equals(sourceText.text, targetText.text, StringComparison.Ordinal) ||
                                       sourceText.font != targetText.font || sourceText.material != targetText.material ||
                                       sourceText.fontStyle != targetText.fontStyle || sourceText.fontSize != targetText.fontSize ||
                                       sourceText.alignment != targetText.alignment || sourceText.color != targetText.color)) return false;
            foreach (Type effectType in new[] { typeof(Shadow), typeof(Outline) })
            {
                Shadow sourceEffect = GetExactComponent(source, effectType) as Shadow;
                Shadow targetEffect = GetExactComponent(target, effectType) as Shadow;
                if ((sourceEffect == null) != (targetEffect == null)) return false;
                if (sourceEffect != null && (sourceEffect.effectColor != targetEffect.effectColor ||
                                             sourceEffect.effectDistance != targetEffect.effectDistance ||
                                             sourceEffect.useGraphicAlpha != targetEffect.useGraphicAlpha)) return false;
            }
            AspectRatioFitter sourceAspect = source.GetComponent<AspectRatioFitter>();
            AspectRatioFitter targetAspect = target.GetComponent<AspectRatioFitter>();
            if ((sourceAspect == null) != (targetAspect == null)) return false;
            if (sourceAspect != null && (sourceAspect.aspectMode != targetAspect.aspectMode ||
                                         sourceAspect.aspectRatio != targetAspect.aspectRatio)) return false;
            Button sourceButton = source.GetComponent<Button>();
            Button targetButton = target.GetComponent<Button>();
            return (sourceButton == null) == (targetButton == null) &&
                   (sourceButton == null || (sourceButton.transition == targetButton.transition &&
                                             sourceButton.interactable == targetButton.interactable &&
                                             targetButton.onClick.GetPersistentEventCount() == 0));
        }

        private static bool HasProjectComponents(RectTransform target, IEnumerable<string> importerOwnedTypes)
        {
            var owned = new HashSet<string>(importerOwnedTypes ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            return target.GetComponents<Component>().Any(component =>
                !(component is RectTransform) && !(component is CanvasRenderer) &&
                (!IsImporterOwnedType(component.GetType()) || !owned.Contains(ComponentTypeName(component.GetType()))));
        }

        private static bool HasUnexpectedProjectComponents(RectTransform target, RectTransform candidate)
        {
            var candidateTypes = new HashSet<string>(CandidateOwnedTypeNames(candidate), StringComparer.Ordinal);
            Button targetButton = target.GetComponent<Button>();
            Button candidateButton = candidate.GetComponent<Button>();
            if (targetButton != null && candidateButton != null &&
                targetButton.onClick.GetPersistentEventCount() > candidateButton.onClick.GetPersistentEventCount()) return true;
            return target.GetComponents<Component>().Any(component =>
                !(component is RectTransform) && !(component is CanvasRenderer) &&
                (!IsImporterOwnedType(component.GetType()) || !candidateTypes.Contains(ComponentTypeName(component.GetType()))));
        }

        private static bool IsProtectedBoundary(GameObject target)
        {
            return target.GetComponent<Canvas>() != null || target.GetComponent<Mask>() != null ||
                   target.GetComponent<RectMask2D>() != null || target.GetComponent<Selectable>() != null ||
                   target.GetComponent<Animator>() != null;
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
            foreach (Component component in candidate.GetComponents<Component>())
            {
                Type type = component.GetType();
                if (IsImporterOwnedType(type) && GetExactComponent(target, type) == null)
                    target.gameObject.AddComponent(type);
            }
            return target;
        }

        private static void ValidateComponentSynchronization(
            Transform root,
            RectTransform source,
            RectTransform target,
            PsdHierarchyProfileNode record,
            IReadOnlyDictionary<RectTransform, RectTransform> targetByCandidate)
        {
            List<string> current = CandidateOwnedTypeNames(source);
            var previous = new HashSet<string>((record.importerOwnedComponentTypes ?? new List<string>())
                .Where(IsImporterOwnedTypeName), StringComparer.Ordinal);
            foreach (string stale in previous.Where(type => !current.Contains(type)))
            {
                Component component = target.GetComponents<Component>()
                    .FirstOrDefault(value => value != null && string.Equals(ComponentTypeName(value.GetType()), stale, StringComparison.Ordinal));
                if (component != null) ValidateSafeComponentRemoval(root, component);
            }
            if (previous.Count > 0)
            {
                foreach (string added in current.Where(type => !previous.Contains(type)))
                {
                    Type type = ResolveImporterOwnedType(added);
                    if (type != null && GetExactComponent(target, type) != null)
                        throw new PsdPrefabIncrementalMergeException(
                            "Cannot claim a project-owned component as importer-owned: " + added);
                }
            }
            Button sourceButton = source.GetComponent<Button>();
            if (sourceButton != null)
            {
                ResolveMappedGraphic(sourceButton.targetGraphic, targetByCandidate);
                ValidateNavigationMappings(sourceButton.navigation, targetByCandidate);
            }
        }

        private static void SynchronizeImporterOwnedComponents(
            Transform root,
            RectTransform source,
            RectTransform target,
            PsdHierarchyProfileNode record,
            IReadOnlyDictionary<RectTransform, RectTransform> targetByCandidate)
        {
            List<string> current = CandidateOwnedTypeNames(source);
            var previous = new HashSet<string>((record.importerOwnedComponentTypes ?? new List<string>())
                .Where(IsImporterOwnedTypeName), StringComparer.Ordinal);
            foreach (string stale in previous.Where(type => !current.Contains(type)).ToArray())
            {
                Type type = ResolveImporterOwnedType(stale);
                Component component = type == null ? null : GetExactComponent(target, type);
                if (component != null) UnityEngine.Object.DestroyImmediate(component);
            }
            foreach (Component sourceComponent in source.GetComponents<Component>())
            {
                Type type = sourceComponent.GetType();
                if (IsImporterOwnedType(type) && GetExactComponent(target, type) == null)
                    target.gameObject.AddComponent(type);
            }
            CopyLegacyText(source.GetComponent<Text>(), target.GetComponent<Text>());
            CopyShadow(GetExactComponent(source, typeof(Shadow)) as Shadow,
                GetExactComponent(target, typeof(Shadow)) as Shadow);
            CopyShadow(source.GetComponent<Outline>(), target.GetComponent<Outline>());
            CopyAspect(source.GetComponent<AspectRatioFitter>(), target.GetComponent<AspectRatioFitter>());
            CopyButton(source.GetComponent<Button>(), target.GetComponent<Button>(), targetByCandidate);
            record.importerOwnedComponentTypes = current;
        }

        private static List<string> CandidateOwnedTypeNames(RectTransform source)
        {
            return source.GetComponents<Component>().Where(component => component != null && IsImporterOwnedType(component.GetType()))
                .Select(component => ComponentTypeName(component.GetType())).Distinct(StringComparer.Ordinal).ToList();
        }

        private static bool IsImporterOwnedType(Type type)
        {
            return type == typeof(Image) || type == typeof(TextMeshProUGUI) || type == typeof(Text) ||
                   type == typeof(Outline) || type == typeof(Shadow) || type == typeof(AspectRatioFitter) ||
                   type == typeof(Button);
        }

        private static bool IsImporterOwnedTypeName(string name)
        {
            return ResolveImporterOwnedType(name) != null;
        }

        private static Type ResolveImporterOwnedType(string name)
        {
            return new[] { typeof(Image), typeof(TextMeshProUGUI), typeof(Text), typeof(Outline), typeof(Shadow),
                typeof(AspectRatioFitter), typeof(Button) }
                .FirstOrDefault(type => string.Equals(ComponentTypeName(type), name, StringComparison.Ordinal));
        }

        private static string ComponentTypeName(Type type)
        {
            return type == null ? string.Empty : type.FullName;
        }

        private static Component GetExactComponent(Component target, Type type)
        {
            return target == null ? null : target.GetComponents<Component>()
                .FirstOrDefault(component => component != null && component.GetType() == type);
        }

        private static void ValidateSafeComponentRemoval(Transform root, Component target)
        {
            Button button = target as Button;
            if (button != null && button.onClick.GetPersistentEventCount() > 0)
                throw new PsdPrefabIncrementalMergeException(
                    "Cannot remove importer-owned Button because it contains project onClick events.");
            foreach (Component owner in root.GetComponentsInChildren<Component>(true))
            {
                if (owner == null || owner == target) continue;
                var serialized = new SerializedObject(owner);
                SerializedProperty property = serialized.GetIterator();
                // Next (rather than NextVisible) includes hidden serialized
                // fields. Passing true on every iteration descends through
                // arrays, lists and nested serializable objects as well as
                // their direct owner fields.
                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue == target)
                        throw new PsdPrefabIncrementalMergeException(
                            "Cannot remove importer-owned component because project serialized data still references it.");
                }
            }
        }

        private static void CopyLegacyText(Text source, Text target)
        {
            if (source == null || target == null) return;
            target.text = source.text;
            target.font = source.font;
            target.fontStyle = source.fontStyle;
            target.fontSize = source.fontSize;
            target.lineSpacing = source.lineSpacing;
            target.supportRichText = source.supportRichText;
            target.alignment = source.alignment;
            target.alignByGeometry = source.alignByGeometry;
            target.horizontalOverflow = source.horizontalOverflow;
            target.verticalOverflow = source.verticalOverflow;
            target.resizeTextForBestFit = source.resizeTextForBestFit;
            target.resizeTextMinSize = source.resizeTextMinSize;
            target.resizeTextMaxSize = source.resizeTextMaxSize;
            target.color = source.color;
            target.material = source.material;
            target.raycastTarget = source.raycastTarget;
            target.maskable = source.maskable;
        }

        private static void CopyShadow(Shadow source, Shadow target)
        {
            if (source == null || target == null) return;
            target.effectColor = source.effectColor;
            target.effectDistance = source.effectDistance;
            target.useGraphicAlpha = source.useGraphicAlpha;
        }

        private static void CopyAspect(AspectRatioFitter source, AspectRatioFitter target)
        {
            if (source == null || target == null) return;
            target.aspectMode = source.aspectMode;
            target.aspectRatio = source.aspectRatio;
        }

        private static void CopyButton(
            Button source,
            Button target,
            IReadOnlyDictionary<RectTransform, RectTransform> targetByCandidate)
        {
            if (source == null || target == null) return;
            target.targetGraphic = ResolveMappedGraphic(source.targetGraphic, targetByCandidate);
            target.transition = source.transition;
            target.colors = source.colors;
            target.spriteState = source.spriteState;
            target.interactable = source.interactable;
            Navigation navigation = source.navigation;
            navigation.selectOnUp = ResolveMappedSelectable(navigation.selectOnUp, targetByCandidate);
            navigation.selectOnDown = ResolveMappedSelectable(navigation.selectOnDown, targetByCandidate);
            navigation.selectOnLeft = ResolveMappedSelectable(navigation.selectOnLeft, targetByCandidate);
            navigation.selectOnRight = ResolveMappedSelectable(navigation.selectOnRight, targetByCandidate);
            target.navigation = navigation;
            AnimationTriggers sourceTriggers = source.animationTriggers;
            AnimationTriggers targetTriggers = target.animationTriggers;
            targetTriggers.normalTrigger = sourceTriggers.normalTrigger;
            targetTriggers.highlightedTrigger = sourceTriggers.highlightedTrigger;
            targetTriggers.pressedTrigger = sourceTriggers.pressedTrigger;
            targetTriggers.selectedTrigger = sourceTriggers.selectedTrigger;
            targetTriggers.disabledTrigger = sourceTriggers.disabledTrigger;
        }

        private static void ValidateNavigationMappings(
            Navigation navigation,
            IReadOnlyDictionary<RectTransform, RectTransform> targetByCandidate)
        {
            ResolveMappedSelectable(navigation.selectOnUp, targetByCandidate);
            ResolveMappedSelectable(navigation.selectOnDown, targetByCandidate);
            ResolveMappedSelectable(navigation.selectOnLeft, targetByCandidate);
            ResolveMappedSelectable(navigation.selectOnRight, targetByCandidate);
        }

        private static Graphic ResolveMappedGraphic(
            Graphic source,
            IReadOnlyDictionary<RectTransform, RectTransform> targetByCandidate)
        {
            if (source == null) return null;
            RectTransform mapped;
            if (!targetByCandidate.TryGetValue(source.transform as RectTransform, out mapped))
                throw new PsdPrefabIncrementalMergeException("Button targetGraphic is outside the generated registry.");
            Graphic result = mapped.GetComponent(source.GetType()) as Graphic;
            if (result == null) throw new PsdPrefabIncrementalMergeException("Button targetGraphic type cannot be mapped.");
            return result;
        }

        private static Selectable ResolveMappedSelectable(
            Selectable source,
            IReadOnlyDictionary<RectTransform, RectTransform> targetByCandidate)
        {
            if (source == null) return null;
            RectTransform mapped;
            if (!targetByCandidate.TryGetValue(source.transform as RectTransform, out mapped))
                throw new PsdPrefabIncrementalMergeException("Button navigation references an object outside the generated registry.");
            Selectable result = mapped.GetComponent(source.GetType()) as Selectable;
            if (result == null) throw new PsdPrefabIncrementalMergeException("Button navigation type cannot be mapped.");
            return result;
        }

        private static void CopyImporterOwnedValues(RectTransform source, RectTransform target)
        {
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
