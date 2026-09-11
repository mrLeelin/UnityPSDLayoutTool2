using System;
using System.Collections.Generic;
using System.IO;
using AiJobFileStoreNamespace;
using UGF.EditorTools.Psd2UGUI;

namespace AiRecognitionInputPackageWriterNamespace
{
    internal sealed class AiRecognitionInputPackageWriter
    {
        internal static AiRecognitionInputPackageWriter s_ObfuscationSentinel;

        internal bool TryWriteRecognitionInputPackage(AiJobContext aiJobContext, AiAnalysisPackageDocument aiAnalysisPackageDocument, string text2, out string result)
        {
            result = null;
            if (aiJobContext != null && aiAnalysisPackageDocument != null && aiAnalysisPackageDocument.document != null)
            {
                if (!string.IsNullOrWhiteSpace(aiJobContext.RecognitionInputManifestPath) && !string.IsNullOrWhiteSpace(aiJobContext.RecognitionNodeShardDirectory))
                {
                    try
                    {
                        AiJobFileStore.EnsureDirectory(Path.GetDirectoryName(aiJobContext.RecognitionInputManifestPath));
                        AiJobFileStore.EnsureDirectory(aiJobContext.RecognitionNodeShardDirectory);
                        List<AiRecognitionInputNodeEntry> list = BuildInputNodeEntries(aiAnalysisPackageDocument.nodes);
                        int num = Math.Max(1, (list.Count + 64 - 1) / 64);
                        string[] array = new string[num];
                        for (int i = 0; i < num; i++)
                        {
                            int num2 = i * 64;
                            int num3 = Math.Min(64, list.Count - num2);
                            AiRecognitionNodeShardDocument aiRecognitionNodeShardDocument = new AiRecognitionNodeShardDocument
                            {
                                version = "1.0",
                                treeHash = (aiAnalysisPackageDocument.treeHash ?? string.Empty),
                                shardIndex = i,
                                totalShards = num
                            };
                            for (int j = 0; j < num3; j++)
                            {
                                aiRecognitionNodeShardDocument.nodes.Add(list[num2 + j]);
                            }
                            string text = Path.Combine(aiJobContext.RecognitionNodeShardDirectory, $"nodes-{i:000}.json");
                            AiJobFileStore.WriteJsonAtomic(text, aiRecognitionNodeShardDocument);
                            array[i] = MakeRelativePath(text2, text);
                        }
                        AiRecognitionInputManifestDocument value = new AiRecognitionInputManifestDocument
                        {
                            version = "1.0",
                            treeHash = (aiAnalysisPackageDocument.treeHash ?? string.Empty),
                            document = BuildInputDocumentInfo(aiAnalysisPackageDocument.document, list),
                            config = (aiAnalysisPackageDocument.config ?? new AiAnalysisConfigInfo()),
                            nodeCount = list.Count,
                            nodeShardPaths = array
                        };
                        AiJobFileStore.WriteJsonAtomic(aiJobContext.RecognitionInputManifestPath, value);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        result = "Failed to build recognition input: " + ex.Message;
                        return false;
                    }
                }
                result = "Recognition input output paths are invalid.";
                return false;
            }
            result = "Recognition input build context is invalid.";
            return false;
        }

        private static List<AiRecognitionInputNodeEntry> BuildInputNodeEntries(List<AiAnalysisNodeEntry> values)
        {
            List<AiRecognitionInputNodeEntry> list = new List<AiRecognitionInputNodeEntry>(values?.Count ?? 0);
            if (values == null)
            {
                return list;
            }
            for (int i = 0; i < values.Count; i++)
            {
                AiAnalysisNodeEntry aiAnalysisNodeEntry = values[i];
                if (aiAnalysisNodeEntry != null)
                {
                    list.Add(new AiRecognitionInputNodeEntry
                    {
                        id = (aiAnalysisNodeEntry.id ?? string.Empty),
                        shortId = (aiAnalysisNodeEntry.shortId ?? string.Empty),
                        parentId = (aiAnalysisNodeEntry.parentId ?? string.Empty),
                        childIds = CloneStringArray(aiAnalysisNodeEntry.childIds),
                        onlyChildId = (aiAnalysisNodeEntry.onlyChildId ?? string.Empty),
                        idPath = (aiAnalysisNodeEntry.idPath ?? string.Empty),
                        displayPath = (aiAnalysisNodeEntry.displayPath ?? string.Empty),
                        name = (aiAnalysisNodeEntry.name ?? string.Empty),
                        layerName = (aiAnalysisNodeEntry.layerName ?? string.Empty),
                        nameTokens = CloneStringArray(aiAnalysisNodeEntry.nameTokens),
                        layerType = (aiAnalysisNodeEntry.layerType ?? string.Empty),
                        isGroupLayer = aiAnalysisNodeEntry.isGroupLayer,
                        isTextLayer = aiAnalysisNodeEntry.isTextLayer,
                        isGeneratedNode = aiAnalysisNodeEntry.isGeneratedNode,
                        uiType = (aiAnalysisNodeEntry.uiType ?? string.Empty),
                        rect = CloneRect(aiAnalysisNodeEntry.rect),
                        previewFileName = Path.GetFileName(aiAnalysisNodeEntry.imageFile ?? string.Empty),
                        previewKind = (aiAnalysisNodeEntry.previewKind ?? string.Empty),
                        previewSourceId = (aiAnalysisNodeEntry.previewSourceId ?? string.Empty),
                        visualHash = (aiAnalysisNodeEntry.visualHash ?? string.Empty),
                        renderLeafCount = aiAnalysisNodeEntry.renderLeafCount,
                        atlas = ((aiAnalysisNodeEntry.atlas == null) ? null : new AiRecognitionInputAtlasRef
                        {
                            page = (aiAnalysisNodeEntry.atlas.page ?? string.Empty),
                            cell = aiAnalysisNodeEntry.atlas.cell,
                            label = (aiAnalysisNodeEntry.atlas.label ?? string.Empty)
                        }),
                        suffixMatch = (aiAnalysisNodeEntry.suffixMatch ?? string.Empty),
                        siblingIndex = aiAnalysisNodeEntry.siblingIndex,
                        childCount = aiAnalysisNodeEntry.childCount
                    });
                }
            }
            return list;
        }

        private static AiRecognitionInputDocumentInfo BuildInputDocumentInfo(object value, List<AiRecognitionInputNodeEntry> values)
        {
            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (values != null)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    AiRecognitionInputAtlasRef aiRecognitionInputAtlasRef = ((values[i] != null) ? values[i].atlas : null);
                    if (aiRecognitionInputAtlasRef != null && !string.IsNullOrWhiteSpace(aiRecognitionInputAtlasRef.page))
                    {
                        hashSet.Add(aiRecognitionInputAtlasRef.page);
                    }
                }
            }
            return new AiRecognitionInputDocumentInfo
            {
                previewImagePath = (((AiAnalysisDocumentInfo)value).previewImagePath ?? string.Empty),
                annotatedPreviewImagePath = (((AiAnalysisDocumentInfo)value).annotatedPreviewImagePath ?? string.Empty),
                nodePreviewDirectoryPath = (((AiAnalysisDocumentInfo)value).nodePreviewDirectoryPath ?? string.Empty),
                nodeAtlasDirectoryPath = (((AiAnalysisDocumentInfo)value).nodeAtlasDirectoryPath ?? string.Empty),
                atlasPageCount = hashSet.Count,
                width = ((AiAnalysisDocumentInfo)value).width,
                height = ((AiAnalysisDocumentInfo)value).height
            };
        }

        private static RectData CloneRect(object value)
        {
            if (value == null)
            {
                return null;
            }
            return new RectData
            {
                x = ((RectData)value).x,
                y = ((RectData)value).y,
                w = ((RectData)value).w,
                h = ((RectData)value).h
            };
        }

        private static string[] CloneStringArray(object value)
        {
            if (value == null || ((Array)value).Length < 1)
            {
                return Array.Empty<string>();
            }
            string[] array = new string[((Array)value).Length];
            Array.Copy((Array)value, array, ((Array)value).Length);
            return array;
        }

        private static string MakeRelativePath(object value, object value2)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && !string.IsNullOrWhiteSpace((string)value2))
            {
                try
                {
                    return Path.GetRelativePath((string)value, (string)value2).Replace("\\", "/");
                }
                catch
                {
                    return ((string)value2).Replace("\\", "/");
                }
            }
            return string.Empty;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiRecognitionInputPackageWriter GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
