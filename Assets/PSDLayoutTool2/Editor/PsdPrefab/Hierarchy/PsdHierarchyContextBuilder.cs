namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Converts the normalized PSD model plus a separately inspected Prefab
    /// hierarchy into the planner's read-only context. Pixel fingerprints and
    /// channel bytes are intentionally not copied into the request contract.
    /// </summary>
    public static class PsdHierarchyContextBuilder
    {
        public static PsdHierarchyRequest Build(
            PsdPrefabDocumentModel document,
            IEnumerable<PsdHierarchyPrefabNodeMetadata> prefabHierarchy,
            IEnumerable<PsdHierarchyPreviewReference> previews = null)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            List<PsdHierarchyPrefabNodeMetadata> prefabNodes = (prefabHierarchy ??
                    Enumerable.Empty<PsdHierarchyPrefabNodeMetadata>())
                .Where(node => node != null)
                .Select(ClonePrefabNode)
                .ToList();
            Dictionary<string, PsdHierarchyPrefabNodeMetadata> prefabByStableId = prefabNodes
                .Where(node => !string.IsNullOrEmpty(node.stableId))
                .GroupBy(node => node.stableId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            var request = new PsdHierarchyRequest
            {
                schemaVersion = PsdHierarchyRequest.CurrentSchemaVersion,
                sourceFingerprint = document.sourceFingerprint ?? string.Empty,
                documentWidth = document.width,
                documentHeight = document.height,
                currentPrefabHierarchy = prefabNodes,
                previews = (previews ?? Enumerable.Empty<PsdHierarchyPreviewReference>())
                    .Where(preview => preview != null)
                    .Select(ClonePreview)
                    .ToList()
            };

            foreach (PsdPrefabNodeModel source in document.nodes ?? new List<PsdPrefabNodeModel>())
            {
                if (source == null)
                {
                    continue;
                }

                PsdHierarchyPrefabNodeMetadata prefabNode;
                prefabByStableId.TryGetValue(source.stableId ?? string.Empty, out prefabNode);
                request.nodes.Add(new PsdHierarchyRequestNode
                {
                    stableId = source.stableId ?? string.Empty,
                    originalName = source.name ?? string.Empty,
                    kind = source.kind.ToString(),
                    parentStableId = source.parentStableId ?? string.Empty,
                    siblingIndex = source.siblingIndex,
                    rectangle = new PsdHierarchyRectangle
                    {
                        x = source.bounds.x,
                        y = source.bounds.y,
                        width = source.bounds.width,
                        height = source.bounds.height
                    },
                    hasProjectComponents = prefabNode != null && prefabNode.hasProjectComponents,
                    isProtectedBoundary = prefabNode != null && prefabNode.isProtectedBoundary,
                    protectedBoundaryStableId = prefabNode != null
                        ? prefabNode.protectedBoundaryStableId ?? string.Empty
                        : string.Empty
                });
            }

            return request;
        }

        private static PsdHierarchyPrefabNodeMetadata ClonePrefabNode(PsdHierarchyPrefabNodeMetadata source)
        {
            return new PsdHierarchyPrefabNodeMetadata
            {
                stableId = source.stableId ?? string.Empty,
                parentStableId = source.parentStableId ?? string.Empty,
                siblingIndex = source.siblingIndex,
                hierarchyPath = source.hierarchyPath ?? string.Empty,
                componentTypes = new List<string>(source.componentTypes ?? new List<string>()),
                hasProjectComponents = source.hasProjectComponents,
                isProtectedBoundary = source.isProtectedBoundary,
                protectedBoundaryStableId = source.protectedBoundaryStableId ?? string.Empty
            };
        }

        private static PsdHierarchyPreviewReference ClonePreview(PsdHierarchyPreviewReference source)
        {
            return new PsdHierarchyPreviewReference
            {
                key = source.key ?? string.Empty,
                kind = source.kind ?? string.Empty,
                crop = source.crop
            };
        }
    }
}
