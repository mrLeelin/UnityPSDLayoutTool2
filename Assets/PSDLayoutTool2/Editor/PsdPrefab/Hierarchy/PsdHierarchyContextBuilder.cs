namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

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
            IEnumerable<PsdHierarchyPreviewReference> previews = null,
            string sourcePsdGuid = "")
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            List<PsdPrefabNodeModel> sourceNodes = document.nodes ?? new List<PsdPrefabNodeModel>();
            if (sourceNodes.Count > PsdHierarchyContractLimits.MaxContextNodes)
            {
                throw new ArgumentException("Hierarchy context exceeds the node limit.", "document");
            }

            List<PsdHierarchyPrefabNodeMetadata> prefabNodes = ReadPrefabNodes(prefabHierarchy);
            Dictionary<string, PsdHierarchyPrefabNodeMetadata> prefabByStableId = BuildPrefabIndex(prefabNodes);
            List<PsdHierarchyPreviewReference> previewList = ReadPreviews(previews);

            var request = new PsdHierarchyRequest
            {
                schemaVersion = PsdHierarchyRequest.CurrentSchemaVersion,
                sourcePsdGuid = sourcePsdGuid ?? string.Empty,
                sourceFingerprint = document.sourceFingerprint ?? string.Empty,
                contentFingerprint = ComputeDocumentFacet(sourceNodes, PsdHierarchyFingerprints.Content, null),
                structureFingerprint = ComputeDocumentFacet(sourceNodes, PsdHierarchyFingerprints.Structure, null),
                geometryFingerprint = ComputeDocumentFacet(sourceNodes, PsdHierarchyFingerprints.Geometry,
                    document.width.ToString(CultureInfo.InvariantCulture) + "|" +
                    document.height.ToString(CultureInfo.InvariantCulture) + "|" +
                    document.resolution.ToString("R", CultureInfo.InvariantCulture)),
                documentWidth = document.width,
                documentHeight = document.height,
                currentPrefabHierarchy = prefabNodes,
                previews = previewList
            };

            EnsureLength(request.sourcePsdGuid, PsdHierarchyContractLimits.MaxSourceGuidLength, "source PSD GUID");
            EnsureLength(request.sourceFingerprint, PsdHierarchyContractLimits.MaxFingerprintLength, "source fingerprint");

            foreach (PsdPrefabNodeModel source in sourceNodes)
            {
                if (source == null)
                {
                    continue;
                }

                PsdHierarchyPrefabNodeMetadata prefabNode;
                prefabByStableId.TryGetValue(source.stableId ?? string.Empty, out prefabNode);
                EnsureLength(source.stableId, PsdHierarchyContractLimits.MaxIdentifierLength, "PSD stable ID");
                EnsureLength(source.parentStableId, PsdHierarchyContractLimits.MaxIdentifierLength, "PSD parent stable ID");
                EnsureLength(source.name, PsdHierarchyContractLimits.MaxNameLength, "PSD node name");
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

        private static List<PsdHierarchyPrefabNodeMetadata> ReadPrefabNodes(
            IEnumerable<PsdHierarchyPrefabNodeMetadata> source)
        {
            var result = new List<PsdHierarchyPrefabNodeMetadata>();
            int observedCount = 0;
            foreach (PsdHierarchyPrefabNodeMetadata node in source ?? Enumerable.Empty<PsdHierarchyPrefabNodeMetadata>())
            {
                observedCount++;
                if (observedCount > PsdHierarchyContractLimits.MaxPrefabMetadataNodes)
                {
                    throw new ArgumentException("Hierarchy context exceeds the Prefab metadata node limit.", "source");
                }

                if (node != null)
                {
                    ValidatePrefabNode(node);
                    result.Add(ClonePrefabNode(node));
                }
            }

            return result;
        }

        private static Dictionary<string, PsdHierarchyPrefabNodeMetadata> BuildPrefabIndex(
            IEnumerable<PsdHierarchyPrefabNodeMetadata> nodes)
        {
            var result = new Dictionary<string, PsdHierarchyPrefabNodeMetadata>(StringComparer.Ordinal);
            foreach (PsdHierarchyPrefabNodeMetadata node in nodes)
            {
                if (string.IsNullOrEmpty(node.stableId))
                {
                    continue;
                }

                if (!result.TryAdd(node.stableId, node))
                {
                    throw new ArgumentException("Duplicate Prefab metadata stable ID '" + node.stableId + "'.", "nodes");
                }
            }

            return result;
        }

        private static List<PsdHierarchyPreviewReference> ReadPreviews(
            IEnumerable<PsdHierarchyPreviewReference> source)
        {
            var result = new List<PsdHierarchyPreviewReference>();
            int observedCount = 0;
            foreach (PsdHierarchyPreviewReference preview in source ?? Enumerable.Empty<PsdHierarchyPreviewReference>())
            {
                observedCount++;
                if (observedCount > PsdHierarchyContractLimits.MaxPreviews)
                {
                    throw new ArgumentException("Hierarchy context exceeds the preview limit.", "source");
                }

                if (preview != null)
                {
                    EnsureLength(preview.key, PsdHierarchyContractLimits.MaxIdentifierLength, "preview key");
                    EnsureLength(preview.kind, PsdHierarchyContractLimits.MaxPreviewKindLength, "preview kind");
                    result.Add(ClonePreview(preview));
                }
            }

            return result;
        }

        private static void ValidatePrefabNode(PsdHierarchyPrefabNodeMetadata node)
        {
            EnsureLength(node.stableId, PsdHierarchyContractLimits.MaxIdentifierLength, "Prefab stable ID");
            EnsureLength(node.parentStableId, PsdHierarchyContractLimits.MaxIdentifierLength, "Prefab parent stable ID");
            EnsureLength(node.protectedBoundaryStableId, PsdHierarchyContractLimits.MaxIdentifierLength, "Prefab boundary ID");
            EnsureLength(node.hierarchyPath, PsdHierarchyContractLimits.MaxHierarchyPathLength, "Prefab hierarchy path");
            List<string> components = node.componentTypes ?? new List<string>();
            if (components.Count > PsdHierarchyContractLimits.MaxComponentTypesPerNode)
            {
                throw new ArgumentException("Prefab metadata exceeds the component type limit.", "node");
            }

            foreach (string component in components)
            {
                EnsureLength(component, PsdHierarchyContractLimits.MaxNameLength, "component type");
            }
        }

        private static void EnsureLength(string value, int maximum, string label)
        {
            if ((value ?? string.Empty).Length > maximum)
            {
                throw new ArgumentException(label + " exceeds the allowed length.", label);
            }
        }

        private static string ComputeDocumentFacet(
            IEnumerable<PsdPrefabNodeModel> nodes,
            Func<PsdPrefabNodeModel, string> fingerprint,
            string prefix)
        {
            var value = new StringBuilder(prefix ?? string.Empty);
            foreach (string item in nodes.Where(node => node != null)
                         .Select(node => (node.stableId ?? string.Empty) + ":" + fingerprint(node))
                         .OrderBy(item => item, StringComparer.Ordinal))
            {
                value.Append('|');
                value.Append(item.Length.ToString(CultureInfo.InvariantCulture));
                value.Append(':');
                value.Append(item);
            }

            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value.ToString()));
                var hex = new StringBuilder(bytes.Length * 2);
                foreach (byte item in bytes)
                {
                    hex.Append(item.ToString("x2", CultureInfo.InvariantCulture));
                }

                return hex.ToString();
            }
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
