using System;
using System.Collections.Generic;
using AiJobFileStoreNamespace;
using AiPatchValidatorNamespace;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;

using Object = UnityEngine.Object;
namespace AiOwnerScopeManifestBuilderNamespace
{
    internal sealed class AiOwnerScopeManifestBuilder
    {
        private static AiOwnerScopeManifestBuilder s_ObfuscationSentinel;

        internal bool TryBuildOwnerScopeManifest(AiJobContext aiJobContext, AiAnalysisPackageDocument aiAnalysisPackageDocument, AiMainTypeResultDocument aiMainTypeResultDocument, out AiOwnerScopeManifestDocument result, out string result2)
        {
            result = null;
            result2 = null;
            if (aiJobContext != null && aiAnalysisPackageDocument != null && aiAnalysisPackageDocument.document != null && aiMainTypeResultDocument != null)
            {
                try
                {
                    Dictionary<string, AiAnalysisNodeEntry> dictionary = BuildNodeLookup(aiAnalysisPackageDocument.nodes);
                    result = new AiOwnerScopeManifestDocument
                    {
                        version = "2.0",
                        treeHash = aiAnalysisPackageDocument.treeHash
                    };
                    int width = aiAnalysisPackageDocument.document.width;
                    int height = aiAnalysisPackageDocument.document.height;
                    for (int i = 0; i < aiMainTypeResultDocument.nodes.Count; i++)
                    {
                        AiMainTypeEntry aiMainTypeEntry = aiMainTypeResultDocument.nodes[i];
                        if (aiMainTypeEntry != null && AiPatchValidator.TryParsePatchUiType(aiMainTypeEntry.predictedUIType, out var gUIType) && UGUIParser.IsCompositeControlType(gUIType) && dictionary.TryGetValue(aiMainTypeEntry.targetId, out var value) && value != null && value.rect != null)
                        {
                            Rect val = ToUnityRect(value.rect);
                            if (!(val.width <= 0f) && !(val.height <= 0f))
                            {
                                Rect val2 = ExpandAndClampRect(val, width, height, 24);
                                result.scopes.Add(new AiOwnerScopeEntry
                                {
                                    ownerId = aiMainTypeEntry.targetId,
                                    ownerShortId = (value.shortId ?? string.Empty),
                                    ownerType = gUIType.ToString(),
                                    rect = new RectData
                                    {
                                        x = val2.x,
                                        y = val2.y,
                                        w = val2.width,
                                        h = val2.height
                                    },
                                    scopeImagePath = string.Empty,
                                    annotatedScopeImagePath = string.Empty,
                                    candidateNodeIds = CollectCandidateNodeIds(aiAnalysisPackageDocument.nodes, dictionary, aiMainTypeEntry.targetId, val, val2)
                                });
                            }
                        }
                    }
                    AiJobFileStore.WriteJsonAtomic(aiJobContext.OwnerScopeManifestPath, result);
                    return true;
                }
                catch (Exception ex)
                {
                    result2 = "Failed to build owner scopes: " + ex.Message;
                    return false;
                }
            }
            result2 = "Owner scope build context is invalid.";
            return false;
        }

        private static Dictionary<string, AiAnalysisNodeEntry> BuildNodeLookup(List<AiAnalysisNodeEntry> values)
        {
            Dictionary<string, AiAnalysisNodeEntry> dictionary = new Dictionary<string, AiAnalysisNodeEntry>(StringComparer.OrdinalIgnoreCase);
            if (values == null)
            {
                return dictionary;
            }
            for (int i = 0; i < values.Count; i++)
            {
                AiAnalysisNodeEntry aiAnalysisNodeEntry = values[i];
                if (aiAnalysisNodeEntry != null && !string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.id))
                {
                    dictionary[aiAnalysisNodeEntry.id] = aiAnalysisNodeEntry;
                }
            }
            return dictionary;
        }

        private static string[] CollectCandidateNodeIds(List<AiAnalysisNodeEntry> values, Dictionary<string, AiAnalysisNodeEntry> lookup, object value, Rect rect, Rect rect2)
        {
            List<string> list = new List<string>(16);
            if (values == null)
            {
                return list.ToArray();
            }
            float num = Mathf.Max(1f, rect.width * rect.height);
            for (int i = 0; i < values.Count; i++)
            {
                AiAnalysisNodeEntry aiAnalysisNodeEntry = values[i];
                if (aiAnalysisNodeEntry == null || string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.id) || string.Equals(aiAnalysisNodeEntry.id, (string)value, StringComparison.OrdinalIgnoreCase) || aiAnalysisNodeEntry.rect == null)
                {
                    continue;
                }
                Rect val = ToUnityRect(aiAnalysisNodeEntry.rect);
                if (val.width <= 0f || val.height <= 0f || IsAncestorOf(lookup, aiAnalysisNodeEntry.id, value))
                {
                    continue;
                }
                if (IsDescendantOf(lookup, aiAnalysisNodeEntry.id, value))
                {
                    list.Add(aiAnalysisNodeEntry.id);
                    continue;
                }
                Vector2 center = val.center;
                float num2 = val.width * val.height;
                if ((rect2.Contains(center) || rect2.Overlaps(val)) && !(num2 > num * 1.2f) && (CalculateMinimumAreaOverlapRatio(rect, val) >= 0.12f || rect.Contains(center)))
                {
                    list.Add(aiAnalysisNodeEntry.id);
                }
            }
            return list.ToArray();
        }

        private static Rect ToUnityRect(object value)
        {
            if (value != null)
            {
                return new Rect(((RectData)value).x, ((RectData)value).y, ((RectData)value).w, ((RectData)value).h);
            }
            return Rect.zero;
        }

        private static Rect ExpandAndClampRect(Rect rect, int value, int value2, int value3)
        {
            float num = Mathf.Max(0f, rect.xMin - (float)value3);
            float num2 = Mathf.Max(0f, rect.yMin - (float)value3);
            float num3 = Mathf.Min((float)value, rect.xMax + (float)value3);
            float num4 = Mathf.Min((float)value2, rect.yMax + (float)value3);
            return Rect.MinMaxRect(num, num2, num3, num4);
        }

        private static bool IsDescendantOf(Dictionary<string, AiAnalysisNodeEntry> lookup, object value, object value2)
        {
            return IsDescendantOfCore(lookup, value, value2);
        }

        private static bool IsAncestorOf(Dictionary<string, AiAnalysisNodeEntry> lookup, object value, object value2)
        {
            return IsDescendantOfCore(lookup, value2, value);
        }

        private static bool IsDescendantOfCore(Dictionary<string, AiAnalysisNodeEntry> lookup, object value2, object value3)
        {
            if (lookup != null && !string.IsNullOrWhiteSpace((string)value2) && !string.IsNullOrWhiteSpace((string)value3))
            {
                string key = (string)value2;
                int num = 0;
                while (true)
                {
                    if (num < 128)
                    {
                        if (!lookup.TryGetValue(key, out var value) || value == null || string.IsNullOrWhiteSpace(value.parentId) || string.Equals(value.parentId, "root", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }
                        if (!string.Equals(value.parentId, (string)value3, StringComparison.OrdinalIgnoreCase))
                        {
                            key = value.parentId;
                            num++;
                            continue;
                        }
                        return true;
                    }
                    return false;
                }
                return false;
            }
            return false;
        }

        private static float CalculateMinimumAreaOverlapRatio(Rect rect, Rect rect2)
        {
            float num = Mathf.Max(0f, Mathf.Min(rect.xMax, rect2.xMax) - Mathf.Max(rect.xMin, rect2.xMin));
            float num2 = Mathf.Max(0f, Mathf.Min(rect.yMax, rect2.yMax) - Mathf.Max(rect.yMin, rect2.yMin));
            float num3 = num * num2;
            if (num3 <= 0f)
            {
                return 0f;
            }
            float num4 = Mathf.Max(1f, Mathf.Min(rect.width * rect.height, rect2.width * rect2.height));
            return num3 / num4;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiOwnerScopeManifestBuilder GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
