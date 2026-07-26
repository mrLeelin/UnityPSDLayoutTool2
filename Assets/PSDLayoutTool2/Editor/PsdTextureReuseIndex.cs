namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Preserves exact-content reuse across all names, then adds conservative
    /// visual reuse for textures that share the same semantic PSD name.
    /// </summary>
    public sealed class PsdTextureReuseIndex
    {
        private readonly Dictionary<string, Candidate> exactCandidates =
            new Dictionary<string, Candidate>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Candidate>> candidatesByBaseName =
            new Dictionary<string, List<Candidate>>(StringComparer.OrdinalIgnoreCase);

        public bool TryFind(
            string baseName,
            string contentHash,
            string borderContract,
            int width,
            int height,
            Color32[] pixels,
            out string existingPath)
        {
            existingPath = string.Empty;
            Candidate candidate;
            if (exactCandidates.TryGetValue(BuildExactKey(contentHash, borderContract), out candidate))
            {
                existingPath = candidate.Path;
                return true;
            }

            List<Candidate> sameNameCandidates;
            if (!candidatesByBaseName.TryGetValue(NormalizeBaseName(baseName), out sameNameCandidates))
            {
                return false;
            }

            for (int i = 0; i < sameNameCandidates.Count; i++)
            {
                candidate = sameNameCandidates[i];
                if (string.Equals(candidate.BorderContract, borderContract, StringComparison.Ordinal) &&
                    PsdTextureVisualMatcher.AreEquivalent(
                        candidate.Width,
                        candidate.Height,
                        candidate.Pixels,
                        width,
                        height,
                        pixels))
                {
                    existingPath = candidate.Path;
                    return true;
                }
            }

            return false;
        }

        public void Add(
            string baseName,
            string contentHash,
            string borderContract,
            int width,
            int height,
            Color32[] pixels,
            string path)
        {
            var candidate = new Candidate(
                borderContract,
                width,
                height,
                pixels != null ? (Color32[])pixels.Clone() : null,
                path);
            string exactKey = BuildExactKey(contentHash, borderContract);
            if (!exactCandidates.ContainsKey(exactKey))
            {
                exactCandidates.Add(exactKey, candidate);
            }

            string normalizedName = NormalizeBaseName(baseName);
            List<Candidate> sameNameCandidates;
            if (!candidatesByBaseName.TryGetValue(normalizedName, out sameNameCandidates))
            {
                sameNameCandidates = new List<Candidate>();
                candidatesByBaseName.Add(normalizedName, sameNameCandidates);
            }

            sameNameCandidates.Add(candidate);
        }

        private static string BuildExactKey(string contentHash, string borderContract)
        {
            return (contentHash ?? string.Empty) + "|" + (borderContract ?? string.Empty);
        }

        private static string NormalizeBaseName(string baseName)
        {
            return (baseName ?? string.Empty).Trim();
        }

        private sealed class Candidate
        {
            public Candidate(string borderContract, int width, int height, Color32[] pixels, string path)
            {
                BorderContract = borderContract ?? string.Empty;
                Width = width;
                Height = height;
                Pixels = pixels;
                Path = path ?? string.Empty;
            }

            public string BorderContract { get; private set; }
            public int Width { get; private set; }
            public int Height { get; private set; }
            public Color32[] Pixels { get; private set; }
            public string Path { get; private set; }
        }
    }
}
