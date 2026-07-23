namespace PsdLayoutTool2
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Describes whether an identifier can safely survive a later PSD import.
    /// Only Photoshop's non-zero layer ID is durable. A fallback ID is useful in
    /// the current preview, but rename/reorder operations can change it.
    /// </summary>
    public enum PsdStableLayerIdStability
    {
        NativeStable,
        FallbackUnstable
    }

    public struct PsdStableLayerId
    {
        public string value;
        public PsdStableLayerIdStability stability;

        public bool canPersist
        {
            get { return stability == PsdStableLayerIdStability.NativeStable; }
        }
    }

    /// <summary>
    /// Centralizes PSD layer identity so model building and hierarchy profiles
    /// never silently disagree about which IDs are safe to persist.
    /// </summary>
    public static class PsdStableLayerIdUtility
    {
        public static PsdStableLayerId Create(uint nativeLayerId, string parentId, int siblingIndex, string layerName)
        {
            if (nativeLayerId != 0U)
            {
                return new PsdStableLayerId
                {
                    value = nativeLayerId.ToString(CultureInfo.InvariantCulture),
                    stability = PsdStableLayerIdStability.NativeStable
                };
            }

            // This diagnostic fallback intentionally includes location and name.
            // It must never be stored in a Profile because both inputs are editable.
            string source = (parentId ?? string.Empty) + "/" + siblingIndex + "/" + (layerName ?? string.Empty);
            return new PsdStableLayerId
            {
                value = "fallback_" + ComputeFnv1a(source),
                stability = PsdStableLayerIdStability.FallbackUnstable
            };
        }

        public static bool IsPersistable(string stableId)
        {
            uint nativeId;
            return uint.TryParse(stableId, NumberStyles.None, CultureInfo.InvariantCulture, out nativeId) && nativeId != 0U;
        }

        internal static string ComputeFnv1a(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char character in value ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }

                return hash.ToString("x8", CultureInfo.InvariantCulture);
            }
        }
    }
}
