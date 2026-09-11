using System;
using System.Collections.Generic;
using UnityEngine;

using Object = UnityEngine.Object;
namespace UGF.EditorTools.Psd2UGUI
{
    public sealed class PsdLayerPreviewCache
    {
        private sealed class CacheEntry
        {
            public Texture2D Texture;

            public int ReferenceCount;

            private static CacheEntry s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static CacheEntry GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private static readonly object s_SyncRoot = new object();

        private static readonly Dictionary<string, CacheEntry> s_Entries = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);

        internal static PsdLayerPreviewCache s_ObfuscationSentinel;

        public static Texture2D Acquire(string cacheKey, Func<Texture2D> textureFactory)
        {
            if (!string.IsNullOrWhiteSpace(cacheKey) && textureFactory != null)
            {
                lock (s_SyncRoot)
                {
                    if (s_Entries.TryGetValue(cacheKey, out var value))
                    {
                        if (value != null && (Object)(object)value.Texture != (Object)null)
                        {
                            value.ReferenceCount++;
                            return value.Texture;
                        }
                        s_Entries.Remove(cacheKey);
                    }
                    Texture2D val = textureFactory();
                    if ((Object)(object)val == (Object)null)
                    {
                        return null;
                    }
                    s_Entries[cacheKey] = new CacheEntry
                    {
                        Texture = val,
                        ReferenceCount = 1
                    };
                    return val;
                }
            }
            return textureFactory?.Invoke();
        }

        public static bool Release(string cacheKey)
        {
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return false;
            }
            Texture2D val = null;
            lock (s_SyncRoot)
            {
                if (!s_Entries.TryGetValue(cacheKey, out var value) || value == null)
                {
                    return false;
                }
                value.ReferenceCount--;
                if (value.ReferenceCount > 0)
                {
                    return true;
                }
                val = value.Texture;
                s_Entries.Remove(cacheKey);
            }
            if ((Object)(object)val != (Object)null)
            {
                Object.DestroyImmediate((Object)(object)val);
            }
            return true;
        }

        public static void Clear()
        {
            Texture2D[] array;
            lock (s_SyncRoot)
            {
                if (s_Entries.Count == 0)
                {
                    return;
                }
                List<Texture2D> list = new List<Texture2D>(s_Entries.Count);
                foreach (CacheEntry value in s_Entries.Values)
                {
                    if ((Object)(object)value?.Texture != (Object)null)
                    {
                        list.Add(value.Texture);
                    }
                }
                s_Entries.Clear();
                array = list.ToArray();
            }
            for (int i = 0; i < array.Length; i++)
            {
                Object.DestroyImmediate((Object)(object)array[i]);
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdLayerPreviewCache GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
