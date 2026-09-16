namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Resolves the source PSD behind a generated PNG so the Hierarchy nine-slice
    /// marker can open the PSD layer editor instead of the single-texture fallback.
    ///
    /// Resolution order (first hit wins):
    ///   1. The source PSD recorded in the PNG meta during export.
    ///   2. The export folder convention:
    ///      &lt;psdDir&gt;/&lt;psdName&gt;/Texture/&lt;file&gt;.png  ->  &lt;psdDir&gt;/&lt;psdName&gt;.psd
    ///      A candidate that also exposes the layer id wins; another existing candidate
    ///      is kept as a weak fallback, because the export folder is named after its PSD
    ///      even when that layer is no longer listed (hidden, renamed or re-created).
    ///   3. A project-wide scan matching the stable Photoshop layer id.
    ///
    /// Step 3 only accepts a PSD that really exposes that layer id, so an unrelated
    /// project PSD is never opened. When nothing matches, the caller keeps its
    /// PNG-only fallback.
    /// </summary>
    internal static class PsdNineSliceSourcePsdResolver
    {
        private static readonly Dictionary<string, HashSet<uint>> LayerIdsByPsdPath =
            new Dictionary<string, HashSet<uint>>(StringComparer.Ordinal);

        private static readonly Dictionary<string, string> PsdPathByPngPath =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Finds the PSD that owns <paramref name="layerId"/> for a generated PNG.
        /// </summary>
        /// <param name="pngAssetPath">Project-relative path of the generated PNG.</param>
        /// <param name="layerId">Stable Photoshop layer id recorded on the PNG meta.</param>
        /// <param name="recordedSourcePsdPath">Source PSD recorded on the PNG meta, if any.</param>
        /// <param name="psdAssetPath">Resolved project-relative PSD path.</param>
        internal static bool TryResolve(string pngAssetPath, uint layerId, string recordedSourcePsdPath, out string psdAssetPath)
        {
            psdAssetPath = null;
            string pngPath = NormalizeAssetPath(pngAssetPath);
            if (string.IsNullOrEmpty(pngPath) || layerId == 0U)
            {
                return false;
            }

            string cacheKey = pngPath + "|" + layerId;
            string cached;
            if (PsdPathByPngPath.TryGetValue(cacheKey, out cached) && IsLoadablePsd(cached))
            {
                psdAssetPath = cached;
                return true;
            }

            string recorded = NormalizeAssetPath(recordedSourcePsdPath);
            if (IsLoadablePsd(recorded))
            {
                // The recorded path is authoritative: export wrote it, and trusting it
                // keeps the common click free of a PSD parse.
                psdAssetPath = recorded;
                return true;
            }

            string weakCandidate = null;
            foreach (string candidate in EnumerateConventionCandidates(pngPath))
            {
                if (ContainsLayer(candidate, layerId))
                {
                    return Accept(cacheKey, candidate, out psdAssetPath);
                }

                // The export folder is named after its PSD, so an existing candidate is
                // the right document even when the layer id no longer shows up in the
                // visible layer list (hidden, re-created or renamed layer). Keep it for
                // after the strict search instead of dropping to the PNG editor.
                if (weakCandidate == null && IsLoadablePsd(candidate))
                {
                    weakCandidate = candidate;
                }
            }

            foreach (string candidate in EnumerateProjectPsdPaths(pngPath))
            {
                if (ContainsLayer(candidate, layerId))
                {
                    return Accept(cacheKey, candidate, out psdAssetPath);
                }
            }

            if (weakCandidate != null)
            {
                return Accept(cacheKey, weakCandidate, out psdAssetPath);
            }

            return false;
        }

        /// <summary>
        /// Export folder convention candidates, most likely first. Pure path math with
        /// no AssetDatabase access, so the ordering stays unit-testable.
        /// </summary>
        internal static IEnumerable<string> EnumerateConventionCandidates(string pngAssetPath)
        {
            string pngPath = NormalizeAssetPath(pngAssetPath);
            if (string.IsNullOrEmpty(pngPath))
            {
                yield break;
            }

            string directory = ParentFolder(pngPath);
            if (string.IsNullOrEmpty(directory))
            {
                yield break;
            }

            // ".../7日任务拆分/Texture/x.png" -> ".../7日任务拆分", which is where the
            // exporter puts "<psdName>.psd" next to its own output folder.
            string root = LeafName(directory).Equals("Texture", StringComparison.OrdinalIgnoreCase)
                ? ParentFolder(directory)
                : directory;
            if (string.IsNullOrEmpty(root))
            {
                yield break;
            }

            yield return root + ".psd";
            string name = LeafName(root);
            if (!string.IsNullOrEmpty(name))
            {
                yield return root + "/" + name + ".psd";
            }
        }

        /// <summary>
        /// Every PSD asset in the project, ordered so that files sharing the longest
        /// folder prefix with the PNG are tried first. This keeps the usual layout
        /// (PSD next to its export folder) cheap while still covering projects that
        /// export into a fixed output path.
        /// </summary>
        private static IEnumerable<string> EnumerateProjectPsdPaths(string pngAssetPath)
        {
            string pngFolder = ParentFolder(pngAssetPath);
            string[] assetPaths = AssetDatabase.GetAllAssetPaths();
            var candidates = new List<string>(assetPaths.Length);
            for (int index = 0; index < assetPaths.Length; index++)
            {
                if (IsPsdAssetPath(assetPaths[index]))
                {
                    candidates.Add(assetPaths[index]);
                }
            }

            candidates.Sort((left, right) =>
                SharedPrefixLength(ParentFolder(right), pngFolder).CompareTo(
                    SharedPrefixLength(ParentFolder(left), pngFolder)));

            return candidates;
        }

        private static bool Accept(string cacheKey, string psdAssetPath, out string resolved)
        {
            resolved = psdAssetPath;
            PsdPathByPngPath[cacheKey] = psdAssetPath;
            return true;
        }

        /// <summary>
        /// Checks whether a PSD exposes the layer id the editor window can select.
        /// </summary>
        private static bool ContainsLayer(string psdAssetPath, uint layerId)
        {
            if (layerId == 0U || !IsLoadablePsd(psdAssetPath))
            {
                return false;
            }

            HashSet<uint> layerIds;
            if (!LayerIdsByPsdPath.TryGetValue(psdAssetPath, out layerIds))
            {
                layerIds = BuildLayerIdIndex(psdAssetPath);
            }

            return layerIds.Contains(layerId);
        }

        /// <summary>
        /// Indexes the layer ids the editor window can actually select. A PSD that
        /// fails to parse is cached as empty so one broken file cannot make every
        /// later click pay for it again.
        /// </summary>
        private static HashSet<uint> BuildLayerIdIndex(string psdAssetPath)
        {
            var layerIds = new HashSet<uint>();
            try
            {
                using (PsdNineSlicePsdLayerSession session = PsdNineSlicePsdLayerSession.Open(psdAssetPath))
                {
                    for (int index = 0; index < session.Layers.Count; index++)
                    {
                        uint layerId = session.Layers[index].LayerId;
                        if (layerId != 0U)
                        {
                            layerIds.Add(layerId);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PsdLayoutTool2] Could not index PSD layers for the 9-slice lookup: " +
                    psdAssetPath + " - " + exception.Message);
            }

            LayerIdsByPsdPath[psdAssetPath] = layerIds;
            return layerIds;
        }

        private static bool IsLoadablePsd(string assetPath)
        {
            return IsPsdAssetPath(assetPath) && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null;
        }

        private static bool IsPsdAssetPath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                assetPath.EndsWith(".psd", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAssetPath(string assetPath)
        {
            return string.IsNullOrWhiteSpace(assetPath)
                ? string.Empty
                : assetPath.Trim().Replace('\\', '/').TrimEnd('/');
        }

        private static string ParentFolder(string assetPath)
        {
            int separator = assetPath.LastIndexOf('/');
            return separator <= 0 ? string.Empty : assetPath.Substring(0, separator);
        }

        private static string LeafName(string assetPath)
        {
            int separator = assetPath.LastIndexOf('/');
            return separator < 0 ? assetPath : assetPath.Substring(separator + 1);
        }

        private static int SharedPrefixLength(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return 0;
            }

            int limit = Math.Min(left.Length, right.Length);
            int length = 0;
            while (length < limit &&
                char.ToUpperInvariant(left[length]) == char.ToUpperInvariant(right[length]))
            {
                length++;
            }

            return length;
        }
    }
}
