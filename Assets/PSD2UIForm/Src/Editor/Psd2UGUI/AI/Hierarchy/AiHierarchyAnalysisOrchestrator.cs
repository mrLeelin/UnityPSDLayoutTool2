using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using IAiCliProviderNamespace;
using LayerNodeIdUtilityNamespace;
using AiAnalysisPackageBuilderNamespace;
using AiPatchResultLoaderNamespace;
using AiPatchApplierNamespace;
using AiJobFileStoreNamespace;
using AiRecognitionResultParserNamespace;
using AiCliProviderFactoryNamespace;
using AiPatchValidatorNamespace;
using AiRecognitionInputPackageWriterNamespace;
using AiResultDocumentKindNamespace;
using UGF.EditorTools.Psd2UGUI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using IAiJobListenerNamespace;
using AiJobStateNamespace;
using AiJobManagerNamespace;
using AiPatchLocalNormalizerNamespace;
using AiPathUtilityNamespace;
using AiCliArtifactUtilityNamespace;
using AiPatchPlannerNamespace;

namespace AiHierarchyAnalysisOrchestratorNamespace
{
    internal sealed class AiHierarchyAnalysisOrchestrator
    {
        private sealed class TemporaryHierarchyNormalizationScope : IDisposable
        {
            private sealed class DetachedNodeRecord
            {
                public Transform _transform;

                public Transform _originalParent;

                public string _originalParentNodePath;

                public int _originalSiblingIndex;

                public int _depth;

                internal static DetachedNodeRecord s_ObfuscationSentinel;

                internal static bool IsObfuscationSentinelNull()
                {
                    return s_ObfuscationSentinel == null;
                }

                internal static DetachedNodeRecord GetObfuscationSentinel()
                {
                    return s_ObfuscationSentinel;
                }
            }

            private readonly Psd2UIFormConverterEditor _converter;

            private readonly AiJobContext _jobContext;

            private readonly GameObject _stashObject;

            private readonly List<DetachedNodeRecord> _detachedNodes;

            private bool _isDisposed;

            internal static TemporaryHierarchyNormalizationScope s_ObfuscationSentinel;

            private TemporaryHierarchyNormalizationScope(Psd2UIFormConverterEditor value, AiJobContext value2, GameObject gameObject, List<DetachedNodeRecord> values)
            {
                _converter = value;
                _jobContext = value2;
                _stashObject = gameObject;
                _detachedNodes = values;
            }

            internal static TemporaryHierarchyNormalizationScope DetachInactiveSourceBoundNodes(object value2, object aiJobContext)
            {
                if (Psd2UIFormTargetCompat.IsNull(value2))
                {
                    return null;
                }
                PsdLayerNode[] componentsInChildren = Psd2UIFormTargetCompat.GameObjectOf(value2).GetComponentsInChildren<PsdLayerNode>(true);
                if (componentsInChildren != null && componentsInChildren.Length >= 1)
                {
                    List<DetachedNodeRecord> list = null;
                    foreach (PsdLayerNode psdLayerNode in componentsInChildren)
                    {
                        if (!Psd2UIFormTargetCompat.IsNull(psdLayerNode) && !((Object)(object)Psd2UIFormTargetCompat.TransformOf(psdLayerNode) == (Object)null) && !((Object)(object)Psd2UIFormTargetCompat.GameObjectOf(psdLayerNode) == (Object)null) && !Psd2UIFormTargetCompat.GameObjectOf(psdLayerNode).activeSelf && psdLayerNode.BindPsdLayerIndex >= 0 && !HasInactiveSourceBoundAncestor(Psd2UIFormTargetCompat.TransformOf(psdLayerNode), Psd2UIFormTargetCompat.TransformOf(value2)))
                        {
                            if (list == null)
                            {
                                list = new List<DetachedNodeRecord>(8);
                            }
                            list.Add(new DetachedNodeRecord
                            {
                                _transform = Psd2UIFormTargetCompat.TransformOf(psdLayerNode),
                                _originalParent = Psd2UIFormTargetCompat.TransformOf(psdLayerNode).parent,
                                _originalParentNodePath = GetParentNodePath(value2, Psd2UIFormTargetCompat.TransformOf(psdLayerNode).parent),
                                _originalSiblingIndex = Psd2UIFormTargetCompat.TransformOf(psdLayerNode).GetSiblingIndex(),
                                _depth = GetTransformDepth(Psd2UIFormTargetCompat.TransformOf(psdLayerNode))
                            });
                        }
                    }
                    if (list != null && list.Count >= 1)
                    {
                        list.Sort(delegate(DetachedNodeRecord left, DetachedNodeRecord right)
                        {
                            int num2 = left._depth.CompareTo(right._depth);
                            return (num2 == 0) ? left._originalSiblingIndex.CompareTo(right._originalSiblingIndex) : num2;
                        });
                        GameObject val = new GameObject("__PSD2UIForm_AI_InactiveNodeStash");
                        ((Object)val).hideFlags = (HideFlags)61;
                        for (int num = 0; num < list.Count; num++)
                        {
                            DetachedNodeRecord value = list[num];
                            if (!((Object)(object)value?._transform == (Object)null))
                            {
                                value._transform.SetParent(val.transform, true);
                            }
                        }
                        AiJobFileStore.LogDebug(aiJobContext, $"Temporarily detached {list.Count} inactive source-bound node roots before local structure normalization.");
                        return new TemporaryHierarchyNormalizationScope((Psd2UIFormConverterEditor)value2, (AiJobContext)aiJobContext, val, list);
                    }
                    return null;
                }
                return null;
            }

            public void Dispose()
            {
                RestoreDetachedNodes();
            }

            internal void RestoreDetachedNodes()
            {
                if (_isDisposed)
                {
                    return;
                }
                _isDisposed = true;
                if (_detachedNodes != null)
                {
                    _detachedNodes.Sort(delegate(DetachedNodeRecord left, DetachedNodeRecord right)
                    {
                        int num3 = left._depth.CompareTo(right._depth);
                        return (num3 != 0) ? num3 : left._originalSiblingIndex.CompareTo(right._originalSiblingIndex);
                    });
                    int num = 0;
                    for (int num2 = 0; num2 < _detachedNodes.Count; num2++)
                    {
                        DetachedNodeRecord value = _detachedNodes[num2];
                        if ((Object)(object)value?._transform == (Object)null)
                        {
                            continue;
                        }
                        Transform val = ResolveOriginalParent(value);
                        if (!Psd2UIFormTargetCompat.IsNull(val))
                        {
                            value._transform.SetParent(val, true);
                            value._transform.SetSiblingIndex(Mathf.Clamp(value._originalSiblingIndex, 0, Mathf.Max(0, val.childCount - 1)));
                            if ((Object)(object)((Component)value._transform).gameObject != (Object)null && ((Component)value._transform).gameObject.activeSelf)
                            {
                                ((Component)value._transform).gameObject.SetActive(false);
                            }
                            num++;
                        }
                    }
                    if (num > 0)
                    {
                        AiJobFileStore.LogDebug(_jobContext, $"Restored {num} inactive source-bound node roots after local structure normalization.");
                    }
                }
                if (!Psd2UIFormTargetCompat.IsNull(_stashObject))
                {
                    Object.DestroyImmediate((Object)(object)_stashObject);
                }
            }

            private Transform ResolveOriginalParent(DetachedNodeRecord value)
            {
                if (value != null)
                {
                    if (!((Object)(object)value._originalParent != (Object)null))
                    {
                        Transform val = FindTransformByNodePath(_converter, value._originalParentNodePath);
                        if (!(!Psd2UIFormTargetCompat.IsNull(val)))
                        {
                            if (!(!Psd2UIFormTargetCompat.IsNull(_converter)))
                            {
                                return null;
                            }
                            return _converter.transform;
                        }
                        return val;
                    }
                    return value._originalParent;
                }
                if (!Psd2UIFormTargetCompat.IsNull(_converter))
                {
                    return _converter.transform;
                }
                return null;
            }

            private static bool HasInactiveSourceBoundAncestor(object value, object value2)
            {
                if (Psd2UIFormTargetCompat.IsNull(value))
                {
                    return false;
                }
                Transform parent = ((Transform)value).parent;
                while (!Psd2UIFormTargetCompat.IsNull(parent) && (Object)(object)parent != (Object)value2)
                {
                    PsdLayerNode component = Psd2UIFormTargetCompat.GameObjectOf(parent).GetComponent<PsdLayerNode>();
                    if (!(!Psd2UIFormTargetCompat.IsNull(component)) || Psd2UIFormTargetCompat.GameObjectOf(component).activeSelf || component.BindPsdLayerIndex < 0)
                    {
                        parent = parent.parent;
                        continue;
                    }
                    return true;
                }
                return false;
            }

            private static int GetTransformDepth(object value)
            {
                int num = 0;
                Transform val = (Transform)value;
                while (!Psd2UIFormTargetCompat.IsNull(val))
                {
                    num++;
                    val = val.parent;
                }
                return num;
            }

            private static string GetParentNodePath(object value, object value2)
            {
                if (!Psd2UIFormTargetCompat.IsNull(value) && !Psd2UIFormTargetCompat.IsNull(value2))
                {
                    PsdLayerNode component = Psd2UIFormTargetCompat.GameObjectOf(value2).GetComponent<PsdLayerNode>();
                    if (!(!Psd2UIFormTargetCompat.IsNull(component)))
                    {
                        return string.Empty;
                    }
                    return LayerNodeIdUtility.GetStableNodeId(value, component);
                }
                return string.Empty;
            }

            private static Transform FindTransformByNodePath(object value, object value2)
            {
                if (!Psd2UIFormTargetCompat.IsNull(value) && !string.IsNullOrWhiteSpace((string)value2))
                {
                    PsdLayerNode[] componentsInChildren = Psd2UIFormTargetCompat.GameObjectOf(value).GetComponentsInChildren<PsdLayerNode>(true);
                    int num = 0;
                    PsdLayerNode psdLayerNode;
                    while (true)
                    {
                        if (num < componentsInChildren.Length)
                        {
                            psdLayerNode = componentsInChildren[num];
                            if (!Psd2UIFormTargetCompat.IsNull(psdLayerNode) && !((Object)(object)Psd2UIFormTargetCompat.TransformOf(psdLayerNode) == (Object)null) && string.Equals(LayerNodeIdUtility.GetStableNodeId(value, psdLayerNode), (string)value2, StringComparison.OrdinalIgnoreCase))
                            {
                                break;
                            }
                            num++;
                            continue;
                        }
                        return null;
                    }
                    return Psd2UIFormTargetCompat.TransformOf(psdLayerNode);
                }
                return null;
            }

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static TemporaryHierarchyNormalizationScope GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private sealed class ConverterAiJobListener : IAiJobListener
        {
            private readonly Psd2UIFormConverterEditor _converter;

            internal static ConverterAiJobListener s_ObfuscationSentinel;

            internal ConverterAiJobListener(Psd2UIFormConverterEditor value)
            {
                _converter = value;
            }

            public void OnJobCompleted(AiJobContext aiJobContext)
            {
                if (Psd2UIFormTargetCompat.IsNull(_converter))
                {
                    return;
                }
                if (!TryLoadLatestPatch(_converter, aiJobContext, out var aiPatchDocument, out var text))
                {
                    EditorUtility.DisplayDialog("AI修正失败", text, "确定");
                    return;
                }
                string text2 = BuildCompletionMessage(aiPatchDocument, aiJobContext);
                string text3;
                if (aiPatchDocument == null || aiPatchDocument.operations == null || aiPatchDocument.operations.Count < 1)
                {
                    EditorUtility.DisplayDialog("AI识别完成", text2, "确定");
                }
                else if (EditorUtility.DisplayDialog("AI识别完成", text2, "应用识别结果", "取消") && !TryApplyPatch(_converter, aiJobContext, aiPatchDocument, true, out text3))
                {
                    EditorUtility.DisplayDialog("AI修正失败", text3, "确定");
                }
            }

            public void OnJobFailed(AiJobContext aiJobContext, string text)
            {
                if (string.IsNullOrWhiteSpace(text) || text.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    EditorUtility.DisplayDialog("AI修正失败", string.IsNullOrWhiteSpace(text) ? "未知错误" : text, "确定");
                }
                else
                {
                    EditorUtility.DisplayDialog("AI任务已取消", "当前 AI 自动识别修正任务已取消。", "确定");
                }
            }

            private static string BuildCompletionMessage(object value, object value2)
            {
                int valueOrDefault = (((AiPatchDocument)value)?.analysis?.Count).GetValueOrDefault();
                int valueOrDefault2 = (((AiPatchDocument)value)?.operations?.Count).GetValueOrDefault();
                float num = 0f;
                float num2 = 1f;
                int num3 = 0;
                if (((AiPatchDocument)value)?.analysis != null)
                {
                    for (int i = 0; i < ((AiPatchDocument)value).analysis.Count; i++)
                    {
                        AiAuditEntry aiAuditEntry = ((AiPatchDocument)value).analysis[i];
                        if (aiAuditEntry != null && (string.Equals(aiAuditEntry.semanticKind, "owner", StringComparison.Ordinal) || string.Equals(aiAuditEntry.semanticKind, "role", StringComparison.Ordinal)))
                        {
                            num += aiAuditEntry.confidence;
                            num2 = Math.Min(num2, aiAuditEntry.confidence);
                            num3++;
                        }
                    }
                }
                float num4 = ((num3 > 0) ? (num / (float)num3) : 0f);
                if (num3 <= 0)
                {
                    num2 = 0f;
                }
                return "AI 已完成识别。\n\n" + $"识别节点数: {valueOrDefault}\n" + $"修正操作数: {valueOrDefault2}\n" + $"平均置信度: {num4 * 100f:0.#}%\n" + $"最低置信度: {num2 * 100f:0.#}%\n" + "以上置信度由 AI 自行评估，仅供参考。\n\n应用结果时将自动执行本地结构归一化。\n\n结果文件:\n" + ((AiJobContext)value2).PatchPath + "\n\n" + ((valueOrDefault2 > 0) ? "是否应用本次 AI 修正结果？" : "AI 未发现需要应用的结构修正。");
            }

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static ConverterAiJobListener GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private static readonly AiJobManager _jobManager = new AiJobManager();

        internal static AiHierarchyAnalysisOrchestrator s_ObfuscationSentinel;

        internal static bool StartRecognitionJob(object value2, out string result, IAiJobListener listener = null, bool background = false,
            string organizerInstructions = null, Action<AiJobContext> created = null)
        {
            result = null;
            if (Psd2UIFormTargetCompat.IsNull(value2))
            {
                result = "\ufffd";
                return false;
            }
            UGUIParser uGUIParser = UGUIParser.Instance;
            if (Psd2UIFormTargetCompat.IsNull(uGUIParser))
            {
                result = "Psd2UIFormConfig 未加载。";
                return false;
            }
            if (_jobManager.HasActiveJobs())
            {
                result = "已有 AI 任务正在执行，请等待当前任务完成后再试。";
                return false;
            }
            IAiCliProvider cliProvider = AiCliProviderFactory.GetConfiguredProvider(uGUIParser);
            if (cliProvider != null)
            {
                string text2 = Directory.GetParent(Application.dataPath).FullName;
                string text = GetAiJobsRootPath(text2);
                AiJobContext aiJobContext = _jobManager.CreateJobContext(cliProvider.GetProviderId(), text, ((Psd2UIFormConverterEditor)value2).GetSourcePsdAssetPath());
                if (!new AiAnalysisPackageBuilder().TryBuildAnalysisPackage((Psd2UIFormConverterEditor)value2, aiJobContext, out var treeHash, out result))
                {
                    return false;
                }
                AiJobMetaDocument value = new AiJobMetaDocument
                {
                    jobId = aiJobContext.JobId,
                    providerId = cliProvider.GetProviderId(),
                    createdAtUtc = DateTime.UtcNow.ToString("o"),
                    projectPath = text2.Replace("\\", "/"),
                    psdAssetPath = (((Psd2UIFormConverterEditor)value2).GetSourcePsdAssetPath() ?? string.Empty),
                    converterPath = (AssetDatabase.GetAssetPath((Object)(object)Psd2UIFormTargetCompat.GameObjectOf(value2)) ?? string.Empty),
                    analysisPackageVersion = "4.0",
                    recognitionCombinedVersion = "2.0",
                    mainTypeVersion = "2.0",
                    childRelationVersion = "2.0",
                    structuralVersion = "1.0",
                    patchVersion = "2.0",
                    treeHash = treeHash,
                    previewHash = string.Empty
                };
                string text3 = GetRecognitionPromptPath(text2);
                if (organizerInstructions != null)
                {
                    string organizerPrompt = Path.Combine(aiJobContext.RequestDirectory, "organizer-task.md");
                    File.WriteAllText(organizerPrompt, File.ReadAllText(text3) + "\n\n" + organizerInstructions);
                    text3 = organizerPrompt;
                }
                bool flag = !background && ShouldUseVisibleCliExecution(cliProvider, uGUIParser.GetAiProviderConfig());
                AiJobFileStore.WriteJsonAtomic(aiJobContext.MetaPath, value);
                AiJobFileStore.LogDebug(aiJobContext, "Workflow prepared. provider=" + cliProvider.GetProviderId() + ", projectRoot=" + text2 + ", recognitionPrompt=" + text3);
                created?.Invoke(aiJobContext);
                _jobManager.StartJob(aiJobContext, cliProvider, listener ?? new ConverterAiJobListener((Psd2UIFormConverterEditor)value2), delegate(AiJobContext jobContext, CancellationToken cancellationToken)
                {
                    ExecuteRecognitionWorkflow(jobContext, cliProvider, text2, cancellationToken, text3, flag);
                }, !background);
                return true;
            }
            result = "当前 AI Provider 配置无效。";
            return false;
        }

        internal static void CancelOrganizerJob(AiJobContext job) { if (job != null) _jobManager.CancelJob(job.JobId); }

        internal static bool TryApplyLatestPatchWithUndo(object value, out string result)
        {
            return TryApplyLatestPatch(value, true, out result);
        }

        internal static bool TryApplyLatestPatch(object value, bool enabled, out string result)
        {
            result = null;
            if (!Psd2UIFormTargetCompat.IsNull(value))
            {
                if (!_jobManager.HasActiveJobs())
                {
                    AiJobContext aiJobContext = CreateJobContextForSourceAsset(Directory.GetParent(Application.dataPath).FullName, ((Psd2UIFormConverterEditor)value).GetSourcePsdAssetPath());
                    if (TryLoadLatestPatch(value, aiJobContext, out var aiPatchDocument, out result))
                    {
                        if (aiPatchDocument != null && aiPatchDocument.operations != null && aiPatchDocument.operations.Count >= 1)
                        {
                            return TryApplyPatch(value, aiJobContext, aiPatchDocument, enabled, out result);
                        }
                        result = "当前 AI 结果没有需要应用的修正操作。";
                        return false;
                    }
                    return false;
                }
                result = "已有 AI 任务正在执行，请等待当前任务完成后再试。";
                return false;
            }
            result = "\ufffd";
            return false;
        }

        internal static bool TryRunBlockingWorkflow(object unityObject, bool enabled, bool enabled2, out AiJobContext result, out string result2, string text2 = null)
        {
            result = null;
            result2 = null;
            if (!Psd2UIFormTargetCompat.IsNull(unityObject))
            {
                UGUIParser uGUIParser = UGUIParser.Instance;
                if (Psd2UIFormTargetCompat.IsNull(uGUIParser))
                {
                    result2 = "Psd2UIFormConfig 未加载。";
                    return false;
                }
                if (!_jobManager.HasActiveJobs())
                {
                    IAiCliProvider aiCliProvider = (string.IsNullOrWhiteSpace(text2) ? AiCliProviderFactory.GetConfiguredProvider(uGUIParser) : AiCliProviderFactory.CreateProviderById(text2));
                    if (aiCliProvider == null)
                    {
                        result2 = ((!string.IsNullOrWhiteSpace(text2)) ? ("AI Provider override 无效: " + text2) : "当前 AI Provider 配置无效。");
                        return false;
                    }
                    if (TryPrepareBlockingWorkflow(unityObject, aiCliProvider, out result, out var text, out var aiAnalysisPackageDocument, out var _, out result2))
                    {
                        string text3 = GetRecognitionPromptPath(text);
                        bool flag = ShouldUseVisibleCliExecution(aiCliProvider, uGUIParser.GetAiProviderConfig());
                        AiJobFileStore.LogDebug(result, "Blocking workflow prepared. provider=" + aiCliProvider.GetProviderId() + ", projectRoot=" + text + ", recognitionPrompt=" + text3);
                        DateTime utcNow = DateTime.UtcNow;
                        try
                        {
                            AiJobFileStore.WriteJobStatus(result, (AiJobState)2, "Blocking AI workflow started.");
                            ExecuteRecognitionWorkflow(result, aiCliProvider, text, CancellationToken.None, text3, flag, aiAnalysisPackageDocument);
                            if (enabled)
                            {
                                if (!TryLoadLatestPatch(unityObject, result, out var aiPatchDocument, out result2))
                                {
                                    WriteBlockingWorkflowFailure(result, utcNow, result2);
                                    return false;
                                }
                                if (!TryApplyPatch(unityObject, result, aiPatchDocument, enabled2, out result2))
                                {
                                    WriteBlockingWorkflowFailure(result, utcNow, result2);
                                    return false;
                                }
                            }
                            WriteBlockingWorkflowSuccess(result, utcNow);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            result2 = ex.Message;
                            AiJobFileStore.WriteTextAtomic(result.ErrorPath, ex.ToString());
                            WriteBlockingWorkflowFailure(result, utcNow, result2);
                            AiJobFileStore.LogDebug(result, $"Blocking AI workflow failed. exception={ex}");
                            return false;
                        }
                    }
                    return false;
                }
                result2 = "已有 AI 任务正在执行，请等待当前任务完成后再试。";
                return false;
            }
            result2 = "\ufffd";
            return false;
        }

        private static string GetAiJobsRootPath(object value)
        {
            return AiPathUtility.ResolvePath(value, "Library/Psd2UIForm/AiJobs");
        }

        private static AiJobContext CreateJobContextForSourceAsset(object value, object value2)
        {
            string text = AiJobManager.GetJobDirectoryPath(GetAiJobsRootPath(value), value2);
            string text2 = Path.Combine(text, "request");
            string text3 = Path.Combine(text, "response");
            return new AiJobContext
            {
                JobDirectory = text,
                RequestDirectory = text2,
                ResponseDirectory = text3,
                MetaPath = Path.Combine(text, "meta.json"),
                AnalysisPackagePath = Path.Combine(text2, "analysis-package.json"),
                RecognitionCombinedPath = Path.Combine(text3, "combined-recognition.json"),
                RecognitionCombinedTempPath = Path.Combine(text3, "combined-recognition.json.tmp"),
                MainTypePath = Path.Combine(text3, "main-type.json"),
                MainTypeTempPath = Path.Combine(text3, "main-type.json.tmp"),
                ChildRelationPath = Path.Combine(text3, "child-relation.json"),
                ChildRelationTempPath = Path.Combine(text3, "child-relation.json.tmp"),
                StructuralPath = Path.Combine(text3, "structural.json"),
                StructuralTempPath = Path.Combine(text3, "structural.json.tmp"),
                OwnerScopeManifestPath = Path.Combine(text2, "owner-scope-manifest.json"),
                RecognitionInputManifestPath = Path.Combine(text2, "recognition-input", "manifest.json"),
                RecognitionNodeShardDirectory = Path.Combine(text2, "recognition-input", "nodes"),
                PromptPath = Path.Combine(text2, "prompt.md"),
                StatusPath = Path.Combine(text3, "status.json"),
                ResultPath = Path.Combine(text3, "result.json"),
                PatchPath = Path.Combine(text3, "patch.json"),
                PatchTempPath = Path.Combine(text3, "patch.json.tmp"),
                RawOutputPath = Path.Combine(text3, "raw-output.txt"),
                CompletedPath = Path.Combine(text3, "Completed"),
                StreamOutputPath = Path.Combine(text3, "stream-output.jsonl"),
                VisibleCliCompletionPath = Path.Combine(text3, "visible-cli-completed.json"),
                ErrorPath = Path.Combine(text3, "error.txt"),
                DebugLogPath = Path.Combine(text3, "debug-log.txt")
            };
        }

        private static string GetRecognitionPromptPath(object value)
        {
            return AiPathUtility.ResolvePluginRelativePath(value, "AIPrompts/TaskPrompt.md");
        }

        private static string GetMainTypePromptPath(object value)
        {
            return AiPathUtility.ResolvePluginRelativePath(value, "AIPrompts/MainTypePrompt.md");
        }

        private static string GetChildRelationPromptPath(object value)
        {
            return AiPathUtility.ResolvePluginRelativePath(value, "AIPrompts/ChildRelationPrompt.md");
        }

        private static string GetCombinedRecognitionPromptPath(object value)
        {
            return Path.Combine(((AiJobContext)value).RequestDirectory, "prompt-combined-recognition.md");
        }

        private static string GetMainTypePreparedPromptPath(object value)
        {
            return Path.Combine(((AiJobContext)value).RequestDirectory, "prompt-main-type.md");
        }

        private static string GetChildRelationPreparedPromptPath(object value)
        {
            return Path.Combine(((AiJobContext)value).RequestDirectory, "prompt-child-relation.md");
        }

        private static string[] GetAvailablePreviewImagePaths(object value, object value2)
        {
            if (!TryReadAnalysisPackage(value, out var aiAnalysisPackageDocument))
            {
                return Array.Empty<string>();
            }
            List<string> list = new List<string>(2);
            AddPreviewImagePathIfAvailable(value, list, value2, aiAnalysisPackageDocument.document.annotatedPreviewImagePath);
            if (list.Count == 0)
            {
                AddPreviewImagePathIfAvailable(value, list, value2, aiAnalysisPackageDocument.document.previewImagePath);
            }
            return list.ToArray();
        }

        private static bool TryReadAnalysisPackage(object value, out AiAnalysisPackageDocument result)
        {
            result = null;
            if (value != null && !string.IsNullOrWhiteSpace(((AiJobContext)value).AnalysisPackagePath) && AiJobFileStore.TryReadJson<AiAnalysisPackageDocument>(((AiJobContext)value).AnalysisPackagePath, out result) && result != null)
            {
                return result.document != null;
            }
            return false;
        }

        private static AiPromptTag[] BuildRecognitionPromptTags(object value, object value2, params AiPromptTag[] extraTags)
        {
            if (!TryReadAnalysisPackage(value, out var aiAnalysisPackageDocument))
            {
                return extraTags ?? Array.Empty<AiPromptTag>();
            }
            List<AiPromptTag> list = new List<AiPromptTag>(8 + ((extraTags != null) ? extraTags.Length : 0))
            {
                new AiPromptTag
                {
                    key = "[RECOGNITION_INPUT_MANIFEST_PATH]",
                    value = NormalizePromptPath(((AiJobContext)value).RecognitionInputManifestPath)
                },
                new AiPromptTag
                {
                    key = "[RECOGNITION_NODE_SHARD_GLOB_PATH]",
                    value = CombinePromptGlobPath(((AiJobContext)value).RecognitionNodeShardDirectory, "*.json")
                },
                new AiPromptTag
                {
                    key = "[PREVIEW_IMAGE_PATH]",
                    value = ResolvePromptImagePath(value, value2, aiAnalysisPackageDocument.document.previewImagePath)
                },
                new AiPromptTag
                {
                    key = "[ANNOTATED_PREVIEW_IMAGE_PATH]",
                    value = ResolvePromptImagePath(value, value2, aiAnalysisPackageDocument.document.annotatedPreviewImagePath)
                },
                new AiPromptTag
                {
                    key = "[NODE_ATLAS_GLOB_PATH]",
                    value = ResolvePromptImageGlob(value, value2, aiAnalysisPackageDocument.document.nodeAtlasDirectoryPath)
                },
                new AiPromptTag
                {
                    key = "[NODE_PREVIEW_GLOB_PATH]",
                    value = ResolvePromptImageGlob(value, value2, aiAnalysisPackageDocument.document.nodePreviewDirectoryPath)
                }
            };
            if (extraTags != null && extraTags.Length != 0)
            {
                foreach (AiPromptTag aiPromptTag in extraTags)
                {
                    if (aiPromptTag != null && !string.IsNullOrWhiteSpace(aiPromptTag.key))
                    {
                        list.Add(aiPromptTag);
                    }
                }
            }
            return list.ToArray();
        }

        private static string ResolvePromptImagePath(object value, object value2, object value3)
        {
            return NormalizePromptPath(ResolveAnalysisAssetPath((value != null) ? ((AiJobContext)value).JobDirectory : string.Empty, value2, value3));
        }

        private static string ResolvePromptImageGlob(object value, object value2, object value3)
        {
            return CombinePromptGlobPath(ResolveAnalysisAssetPath((value == null) ? string.Empty : ((AiJobContext)value).JobDirectory, value2, value3), "*.png");
        }

        private static string CombinePromptGlobPath(object value, object value2)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return string.Empty;
            }
            string text = NormalizePromptPath(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }
            string text2 = (string)(string.IsNullOrWhiteSpace((string)value2) ? "*" : value2);
            return text.TrimEnd('/', '\\') + "/" + text2.TrimStart('/', '\\');
        }

        private static string NormalizePromptPath(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return string.Empty;
            }
            try
            {
                return Path.GetFullPath((string)value).Replace("\\", "/");
            }
            catch
            {
                return ((string)value).Replace("\\", "/");
            }
        }

        private static void AddPreviewImagePathIfAvailable(object value, List<string> texts, object value2, object value3)
        {
            if (value == null || texts == null || string.IsNullOrWhiteSpace((string)value3))
            {
                return;
            }
            string text = ResolveAnalysisAssetPath(((AiJobContext)value).JobDirectory, value2, value3);
            if (string.IsNullOrWhiteSpace(text) || !File.Exists(text))
            {
                return;
            }
            int num = 0;
            while (true)
            {
                if (num < texts.Count)
                {
                    if (!string.Equals(texts[num], text, StringComparison.OrdinalIgnoreCase))
                    {
                        num++;
                        continue;
                    }
                    break;
                }
                texts.Add(text);
                break;
            }
        }

        private static string ResolveAnalysisAssetPath(object value, object value2, object value3)
        {
            if (!string.IsNullOrWhiteSpace((string)value3))
            {
                if (Path.IsPathRooted((string)value3))
                {
                    return (string)value3;
                }
                if (!((string)value3).StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) && !((string)value3).StartsWith("Library/", StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFullPath(Path.Combine((string)value, (string)value3));
                }
                return Path.Combine((string)value2, (string)value3);
            }
            return string.Empty;
        }

        private static bool TryPrepareBlockingWorkflow(object value, object value2, out AiJobContext result, out string result2, out AiAnalysisPackageDocument result3, out string result4, out string result5)
        {
            result = null;
            result2 = null;
            result3 = null;
            result4 = string.Empty;
            result5 = null;
            try
            {
                result2 = Directory.GetParent(Application.dataPath).FullName;
                string text = GetAiJobsRootPath(result2);
                AiJobManager aiJobManager = new AiJobManager();
                result = aiJobManager.CreateJobContext(((IAiCliProvider)value2).GetProviderId(), text, ((Psd2UIFormConverterEditor)value).GetSourcePsdAssetPath());
                if (!new AiAnalysisPackageBuilder().TryBuildAnalysisPackage((Psd2UIFormConverterEditor)value, result, out result4, out result5))
                {
                    return false;
                }
                AiJobMetaDocument value3 = new AiJobMetaDocument
                {
                    jobId = result.JobId,
                    providerId = ((IAiCliProvider)value2).GetProviderId(),
                    createdAtUtc = DateTime.UtcNow.ToString("o"),
                    projectPath = result2.Replace("\\", "/"),
                    psdAssetPath = (((Psd2UIFormConverterEditor)value).GetSourcePsdAssetPath() ?? string.Empty),
                    converterPath = (AssetDatabase.GetAssetPath((Object)(object)Psd2UIFormTargetCompat.GameObjectOf(value)) ?? string.Empty),
                    analysisPackageVersion = "4.0",
                    recognitionCombinedVersion = "2.0",
                    mainTypeVersion = "2.0",
                    childRelationVersion = "2.0",
                    structuralVersion = "1.0",
                    patchVersion = "2.0",
                    treeHash = result4,
                    previewHash = string.Empty
                };
                AiJobFileStore.WriteJsonAtomic(result.MetaPath, value3);
                if (!AiJobFileStore.TryReadJson<AiAnalysisPackageDocument>(result.AnalysisPackagePath, out result3) || result3 == null)
                {
                    result5 = "AI 分析包读取失败。";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                result5 = "Failed to prepare AI workflow context: " + ex.Message;
                return false;
            }
        }

        private static void ExecuteRecognitionWorkflow(object value, object value2, object value3, CancellationToken cancellationToken, object value4, bool enabled, AiAnalysisPackageDocument value5 = null)
        {
            if (value5 != null || (AiJobFileStore.TryReadJson<AiAnalysisPackageDocument>(((AiJobContext)value).AnalysisPackagePath, out value5) && value5 != null))
            {
                string[] imageInputPaths = GetAvailablePreviewImagePaths(value, value3);
                ClearPreviousWorkflowArtifacts(value);
                BuildRecognitionInputArtifacts(value, value5, value3);
                PrepareCombinedRecognitionPrompt(value, value3, value4);
                string text = GetStageRawOutputPath(value, "recognition");
                string outputCompletedPath = GetStageCompletedMarkerPath(value, "recognition");
                string streamOutputPath = GetStageStreamOutputPath(value, "recognition");
                string visibleCliCompletionPath = GetStageVisibleCliCompletionPath(value, "recognition");
                AiJobFileStore.UpdateJobStage(value, "AI正在识别UI元素类型");
                AiJobFileStore.UpdateJobDetail(value, "AI任务进程: UI元素识别阶段执行中");
                AiAnalysisRequest aiAnalysisRequest = new AiAnalysisRequest
                {
                    StageName = "recognition-combined",
                    StageDisplayName = "AI正在识别UI元素类型",
                    ResultDocumentKind = (AiResultDocumentKind)5,
                    AnalysisPackageJsonPath = ((AiJobContext)value).RecognitionInputManifestPath,
                    PromptTemplatePath = GetCombinedRecognitionPromptPath(value),
                    PromptTags = null,
                    OutputJsonPath = ((AiJobContext)value).RecognitionCombinedPath,
                    OutputTempJsonPath = ((AiJobContext)value).RecognitionCombinedTempPath,
                    OutputRawTextPath = text,
                    OutputCompletedPath = outputCompletedPath,
                    StreamOutputPath = streamOutputPath,
                    VisibleCliCompletionPath = visibleCliCompletionPath,
                    WorkingDirectory = (string)value3,
                    ImageInputPaths = imageInputPaths,
                    AllowVisibleCliExecution = enabled,
                    RequireExplicitOutputJsonFile = true,
                    DisableOutputRecovery = true,
                    CancellationToken = cancellationToken
                };
                AiRecognitionResultParser value6 = new AiRecognitionResultParser();
                AiJobFileStore.LogDebug(value, $"Stage combined-recognition begin. prompt={aiAnalysisRequest.PromptTemplatePath}, output={aiAnalysisRequest.OutputJsonPath}, visibleCli={aiAnalysisRequest.AllowVisibleCliExecution}");
                ((IAiCliProvider)value2).ExecuteJob((AiJobContext)value, aiAnalysisRequest);
                CopyFileIfExists(text, ((AiJobContext)value).RawOutputPath);
                FinalizeRecognitionArtifact(value, (AiResultDocumentKind)5, ((AiJobContext)value).RecognitionCombinedTempPath, ((AiJobContext)value).RecognitionCombinedPath, "combined-recognition");
                AiJobFileStore.UpdateJobDetail(value, "AI任务进程: 读取 owner-first 识别语义图");
                if (value6.TryLoadCombinedRecognitionResult((AiJobContext)value, value5, out var aiRecognitionCombinedResultDocument, out var message))
                {
                    if (!value6.TrySplitCombinedRecognitionResult(value5, aiRecognitionCombinedResultDocument, out var aiMainTypeResultDocument, out var aiChildRelationResultDocument, out var message2))
                    {
                        throw new InvalidOperationException(message2);
                    }
                    AiJobFileStore.WriteJsonAtomic(((AiJobContext)value).MainTypePath, aiMainTypeResultDocument);
                    AiJobFileStore.WriteJsonAtomic(((AiJobContext)value).ChildRelationPath, aiChildRelationResultDocument);
                    AiJobFileStore.LogDebug(value, $"Combined recognition split completed. nodeCount={((aiMainTypeResultDocument.nodes != null) ? aiMainTypeResultDocument.nodes.Count : 0)}, relationCount={((aiChildRelationResultDocument.relations != null) ? aiChildRelationResultDocument.relations.Count : 0)}");
                    AiJobFileStore.UpdateJobStage(value, "AI识别完成, 开始本地结构矫正与Patch合成");
                    AiJobFileStore.UpdateJobDetail(value, "AI识别完成, 开始本地结构矫正与Patch合成");
                    if (new AiPatchPlanner().TryBuildPatchFromCombinedRecognition(value5, aiRecognitionCombinedResultDocument, out var aiPatchDocument, out var message3))
                    {
                        if (aiPatchDocument != null && aiPatchDocument.analysis != null && aiPatchDocument.analysis.Count >= 1)
                        {
                            AiJobFileStore.WriteJsonAtomic(((AiJobContext)value).PatchPath, aiPatchDocument);
                            AiJobFileStore.LogDebug(value, $"Local patch synthesized. analysisCount={((aiPatchDocument.analysis != null) ? aiPatchDocument.analysis.Count : 0)}, operationCount={((aiPatchDocument.operations != null) ? aiPatchDocument.operations.Count : 0)}");
                            return;
                        }
                        throw new InvalidOperationException("AI 识别结果无效：未生成任何分析项，通常表示识别结果 JSON 协议不匹配或关键 owner/role 识别失败。");
                    }
                    throw new InvalidOperationException(message3);
                }
                throw new InvalidOperationException(message);
            }
            throw new InvalidOperationException("AI 分析包读取失败。");
        }

        private static void BuildRecognitionInputArtifacts(object value, object value2, object value3)
        {
            if (!new AiRecognitionInputPackageWriter().TryWriteRecognitionInputPackage((AiJobContext)value, (AiAnalysisPackageDocument)value2, (string)value3, out var message))
            {
                throw new InvalidOperationException(message);
            }
            AiJobFileStore.LogDebug(value, "Recognition input built. manifest=" + ((AiJobContext)value).RecognitionInputManifestPath + ", nodeShards=" + ((AiJobContext)value).RecognitionNodeShardDirectory);
        }

        private static void PrepareCombinedRecognitionPrompt(object value, object value2, object value3)
        {
            RenderPromptTemplateToFile(value3, GetCombinedRecognitionPromptPath(value), BuildRecognitionPromptTags(value, value2, new AiPromptTag
            {
                key = "[RECOGNITION_COMBINED_VERSION]",
                value = "2.0"
            }));
        }

        private static void PrepareMainTypePrompt(object value, object value2)
        {
            RenderPromptTemplateToFile(GetMainTypePromptPath(value2), GetMainTypePreparedPromptPath(value), BuildRecognitionPromptTags(value, value2, new AiPromptTag
            {
                key = "[MAIN_TYPE_VERSION]",
                value = "2.0"
            }));
        }

        private static void PrepareChildRelationPrompt(object value, object value2)
        {
            RenderPromptTemplateToFile(GetChildRelationPromptPath(value2), GetChildRelationPreparedPromptPath(value), BuildRecognitionPromptTags(value, value2, new AiPromptTag
            {
                key = "[MAIN_TYPE_RESULT_PATH]",
                value = NormalizePromptPath(((AiJobContext)value).MainTypePath)
            }, new AiPromptTag
            {
                key = "[OWNER_SCOPE_MANIFEST_PATH]",
                value = NormalizePromptPath(((AiJobContext)value).OwnerScopeManifestPath)
            }, new AiPromptTag
            {
                key = "[CHILD_RELATION_VERSION]",
                value = "2.0"
            }));
        }

        private static void RenderPromptTemplateToFile(object value, object value2, object value3)
        {
            AiAnalysisRequest aiAnalysisRequest = new AiAnalysisRequest
            {
                PromptTags = (AiPromptTag[])value3
            };
            string text = AiCliArtifactUtility.BuildPrompt(value, aiAnalysisRequest);
            AiJobFileStore.WriteTextAtomic(value2, text);
        }

        private static void ClearPreviousWorkflowArtifacts(object value)
        {
            if (value != null)
            {
                DeleteFileIfExistsQuietly(((AiJobContext)value).MainTypePath);
                DeleteFileIfExistsQuietly(((AiJobContext)value).MainTypeTempPath);
                DeleteFileIfExistsQuietly(((AiJobContext)value).ChildRelationPath);
                DeleteFileIfExistsQuietly(((AiJobContext)value).ChildRelationTempPath);
                DeleteFileIfExistsQuietly(((AiJobContext)value).RecognitionCombinedPath);
                DeleteFileIfExistsQuietly(((AiJobContext)value).RecognitionCombinedTempPath);
                DeleteFileIfExistsQuietly(((AiJobContext)value).StructuralPath);
                DeleteFileIfExistsQuietly(((AiJobContext)value).StructuralTempPath);
                DeleteFileIfExistsQuietly(((AiJobContext)value).PatchPath);
                DeleteFileIfExistsQuietly(((AiJobContext)value).PatchTempPath);
                DeleteFileIfExistsQuietly(((AiJobContext)value).OwnerScopeManifestPath);
                DeleteFileIfExistsQuietly(((AiJobContext)value).RecognitionInputManifestPath);
                DeleteFileIfExistsQuietly(((AiJobContext)value).RawOutputPath);
                DeleteFileIfExistsQuietly(((AiJobContext)value).CompletedPath);
                DeleteFileIfExistsQuietly(((AiJobContext)value).StreamOutputPath);
                DeleteFileIfExistsQuietly(((AiJobContext)value).VisibleCliCompletionPath);
                DeleteFileIfExistsQuietly(GetStageRawOutputPath(value, "recognition"));
                DeleteFileIfExistsQuietly(GetStageCompletedMarkerPath(value, "recognition"));
                DeleteFileIfExistsQuietly(GetStageStreamOutputPath(value, "recognition"));
                DeleteFileIfExistsQuietly(GetStageVisibleCliCompletionPath(value, "recognition"));
                DeleteFileIfExistsQuietly(GetStageRawOutputPath(value, "main-type"));
                DeleteFileIfExistsQuietly(GetStageCompletedMarkerPath(value, "main-type"));
                DeleteFileIfExistsQuietly(GetStageStreamOutputPath(value, "main-type"));
                DeleteFileIfExistsQuietly(GetStageVisibleCliCompletionPath(value, "main-type"));
                DeleteFileIfExistsQuietly(GetStageRawOutputPath(value, "child-relation"));
                DeleteFileIfExistsQuietly(GetStageCompletedMarkerPath(value, "child-relation"));
                DeleteFileIfExistsQuietly(GetStageStreamOutputPath(value, "child-relation"));
                DeleteFileIfExistsQuietly(GetStageVisibleCliCompletionPath(value, "child-relation"));
                DeleteFileIfExistsQuietly(((AiJobContext)value).PromptPath);
                DeleteFileIfExistsQuietly(GetCombinedRecognitionPromptPath(value));
                DeleteFileIfExistsQuietly(GetMainTypePreparedPromptPath(value));
                DeleteFileIfExistsQuietly(GetChildRelationPreparedPromptPath(value));
                DeleteDirectoryIfExistsQuietly(((AiJobContext)value).RecognitionNodeShardDirectory);
            }
        }

        private static bool ShouldUseVisibleCliExecution(object value, object value2)
        {
            if (value != null && (value2 == null || ((AiProviderConfig)value2).showCliWindow) && ((IAiCliProvider)value).GetCapabilities() != null && ((IAiCliProvider)value).GetCapabilities().UsesVisibleCliExecution)
            {
                return Environment.OSVersion.Platform == PlatformID.Win32NT;
            }
            return false;
        }

        private static void FinalizeRecognitionArtifact(object value, AiResultDocumentKind value2, object value3, object value4, object value5)
        {
            if (!AiCliArtifactUtility.TryFinalizeResultFile(new AiAnalysisRequest
            {
                ResultDocumentKind = value2,
                OutputJsonPath = (string)value4,
                OutputTempJsonPath = (string)value3,
                RequireExplicitOutputJsonFile = true,
                DisableOutputRecovery = true
            }, out var text))
            {
                throw new InvalidOperationException("Failed to finalize " + (string)value5 + " result: " + text);
            }
            AiJobFileStore.LogDebug(value, "Unified recognition artifact committed. kind=" + (string)value5 + ", temp=" + (string)value3 + ", final=" + (string)value4);
        }

        private static void DeleteFileIfExistsQuietly(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && File.Exists((string)value))
            {
                try
                {
                    AiJobFileStore.DeleteFileIfExists(value);
                }
                catch
                {
                }
            }
        }

        private static void DeleteDirectoryIfExistsQuietly(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && Directory.Exists((string)value))
            {
                try
                {
                    Directory.Delete((string)value, recursive: true);
                }
                catch
                {
                }
            }
        }

        private static void CopyFileIfExists(object value, object value2)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && !string.IsNullOrWhiteSpace((string)value2) && File.Exists((string)value))
            {
                try
                {
                    AiJobFileStore.EnsureDirectory(Path.GetDirectoryName((string)value2));
                    File.Copy((string)value, (string)value2, overwrite: true);
                }
                catch
                {
                }
            }
        }

        private static string GetStageRawOutputPath(object value, object value2)
        {
            return BuildStageResponsePath(value, "raw-output.", value2, ".txt");
        }

        private static string GetStageCompletedMarkerPath(object value, object value2)
        {
            return BuildStageResponsePath(value, "completed.", value2, string.Empty);
        }

        private static string GetStageStreamOutputPath(object value, object value2)
        {
            return BuildStageResponsePath(value, "stream-output.", value2, ".jsonl");
        }

        private static string GetStageVisibleCliCompletionPath(object value, object value2)
        {
            return BuildStageResponsePath(value, "visible-cli-completed.", value2, ".json");
        }

        private static string BuildStageResponsePath(object value, object value2, object value3, object value4)
        {
            if (value != null && !string.IsNullOrWhiteSpace(((AiJobContext)value).ResponseDirectory))
            {
                string path = (string)value2 + (string)(value3 ?? string.Empty) + (string)(value4 ?? string.Empty);
                return Path.Combine(((AiJobContext)value).ResponseDirectory, path);
            }
            return string.Empty;
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

        private static bool TryLoadLatestPatch(object value, object value2, out AiPatchDocument result, out string result2)
        {
            result = null;
            result2 = null;
            if (!HasRecognitionArtifacts(value2) || TryRebuildPatchFromRecognitionArtifacts(value2, out result, out result2))
            {
                if (!new AiPatchResultLoader().TryLoadPatch((AiJobContext)value2, out result, out result2))
                {
                    return false;
                }
                if (result != null)
                {
                    if (result.analysis != null && result.analysis.Count >= 1)
                    {
                        string text = string.Empty;
                        string text2 = string.Empty;
                        if (AiJobFileStore.TryReadJson<AiAnalysisPackageDocument>(((AiJobContext)value2).AnalysisPackagePath, out var aiAnalysisPackageDocument) && aiAnalysisPackageDocument != null)
                        {
                            text = aiAnalysisPackageDocument.treeHash;
                        }
                        if (AiJobFileStore.TryReadJson<AiJobMetaDocument>(((AiJobContext)value2).MetaPath, out var aiJobMetaDocument) && aiJobMetaDocument != null)
                        {
                            if (string.IsNullOrWhiteSpace(text))
                            {
                                text = aiJobMetaDocument.treeHash;
                            }
                            if (string.IsNullOrWhiteSpace(text2))
                            {
                                text2 = aiJobMetaDocument.psdAssetPath;
                            }
                        }
                        if (string.IsNullOrWhiteSpace(text) || string.Equals(text, result.treeHash, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrWhiteSpace(text2) && !AreAssetPathsEqual(text2, ((Psd2UIFormConverterEditor)value).GetSourcePsdAssetPath()))
                            {
                                result2 = "AI 结果属于 PSD '" + text2 + "'，当前面板绑定 PSD 为 '" + ((Psd2UIFormConverterEditor)value).GetSourcePsdAssetPath() + "'。请切换到对应面板或重新执行 AI 自动识别修正。";
                                result = null;
                                return false;
                            }
                            return true;
                        }
                        result2 = "AI 结果与当前任务请求不匹配，请重新执行 AI 自动识别修正。";
                        result = null;
                        return false;
                    }
                    result2 = "AI 识别结果无效：Patch 未生成任何分析项，通常表示识别结果 JSON 协议不匹配。";
                    result = null;
                    return false;
                }
                result2 = "Patch 文件为空。";
                return false;
            }
            return false;
        }

        private static bool HasRecognitionArtifacts(object value)
        {
            if (value != null && !string.IsNullOrWhiteSpace(((AiJobContext)value).AnalysisPackagePath) && File.Exists(((AiJobContext)value).AnalysisPackagePath))
            {
                if (!string.IsNullOrWhiteSpace(((AiJobContext)value).RecognitionCombinedPath) && File.Exists(((AiJobContext)value).RecognitionCombinedPath))
                {
                    return true;
                }
                if (!string.IsNullOrWhiteSpace(((AiJobContext)value).MainTypePath) && File.Exists(((AiJobContext)value).MainTypePath) && !string.IsNullOrWhiteSpace(((AiJobContext)value).ChildRelationPath))
                {
                    return File.Exists(((AiJobContext)value).ChildRelationPath);
                }
                return false;
            }
            return false;
        }

        private static bool TryRebuildPatchFromRecognitionArtifacts(object value, out AiPatchDocument result, out string result2)
        {
            result = null;
            result2 = null;
            if (value != null)
            {
                if (!AiJobFileStore.TryReadJson<AiAnalysisPackageDocument>(((AiJobContext)value).AnalysisPackagePath, out var aiAnalysisPackageDocument) || aiAnalysisPackageDocument == null)
                {
                    result2 = "AI analysis package not found: " + ((AiJobContext)value).AnalysisPackagePath;
                    return false;
                }
                AiRecognitionResultParser value2 = new AiRecognitionResultParser();
                AiPatchPlanner aiPatchPlanner = new AiPatchPlanner();
                if (string.IsNullOrWhiteSpace(((AiJobContext)value).RecognitionCombinedPath) || !File.Exists(((AiJobContext)value).RecognitionCombinedPath))
                {
                    if (value2.TryLoadMainTypeResult((AiJobContext)value, aiAnalysisPackageDocument, out var aiMainTypeResultDocument, out result2))
                    {
                        if (value2.TryLoadChildRelationResult((AiJobContext)value, aiAnalysisPackageDocument, out var aiChildRelationResultDocument, out result2))
                        {
                            if (!aiPatchPlanner.TryBuildPatchFromLegacyRecognition(aiAnalysisPackageDocument, aiMainTypeResultDocument, aiChildRelationResultDocument, out result, out result2))
                            {
                                return false;
                            }
                            AiJobFileStore.WriteJsonAtomic(((AiJobContext)value).PatchPath, result);
                            AiJobFileStore.LogDebug(value, "Patch rebuilt from latest main-type.json and child-relation.json before apply.");
                            return true;
                        }
                        return false;
                    }
                    return false;
                }
                if (value2.TryLoadCombinedRecognitionResult((AiJobContext)value, aiAnalysisPackageDocument, out var aiRecognitionCombinedResultDocument, out result2))
                {
                    if (!aiPatchPlanner.TryBuildPatchFromCombinedRecognition(aiAnalysisPackageDocument, aiRecognitionCombinedResultDocument, out result, out result2))
                    {
                        return false;
                    }
                    AiJobFileStore.WriteJsonAtomic(((AiJobContext)value).PatchPath, result);
                    AiJobFileStore.LogDebug(value, "Patch rebuilt from latest combined-recognition.json before apply.");
                    return true;
                }
                return false;
            }
            result2 = "AI job context is null.";
            return false;
        }

        private static bool AreAssetPathsEqual(object value, object value2)
        {
            return string.Equals(NormalizeAssetPath(value), NormalizeAssetPath(value2), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAssetPath(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return string.Empty;
            }
            return ((string)value).Trim().Replace("\\", "/");
        }

        private static bool TryApplyPatch(object value, object value2, object value3, bool enabled, out string result)
        {
            if (!ValidateHierarchyAgainstAnalysisPackage(value, value2, out result))
            {
                return false;
            }
            bool flag = AiPatchLocalNormalizer.NormalizePatchDocument(value3);
            if (value2 != null && !string.IsNullOrWhiteSpace(((AiJobContext)value2).AnalysisPackagePath) && AiJobFileStore.TryReadJson<AiAnalysisPackageDocument>(((AiJobContext)value2).AnalysisPackagePath, out var aiAnalysisPackageDocument) && aiAnalysisPackageDocument != null)
            {
                AiPatchLocalNormalizer.RepairAnalysisOwnership(value3, aiAnalysisPackageDocument);
                if (!new AiPatchValidator().ValidatePatch((AiPatchDocument)value3, aiAnalysisPackageDocument, out result))
                {
                    string text = result;
                    AiJobFileStore.LogDebug(value2, "Patch package validation warning; applying valid operations anyway. error=" + text);
                    Debug.LogWarning((object)("[PSD2UIForm.AI] Patch 语义校验未完全通过，将继续应用可执行的正确项。error=" + text));
                    result = null;
                }
            }
            else if (flag && value2 != null)
            {
                AiJobFileStore.WriteJsonAtomic(((AiJobContext)value2).PatchPath, (AiPatchDocument)value3);
            }
            AiPatchLocalNormalizer.AddMissingSetUiTypeOperations(value, value3);
            if (new AiPatchApplier().ApplyPatch((Psd2UIFormConverterEditor)value, (AiPatchDocument)value3, out result))
            {
                if (!enabled)
                {
                    return true;
                }
                using (TemporaryHierarchyNormalizationScope.DetachInactiveSourceBoundNodes(value, value2))
                {
                    if (!((Psd2UIFormConverterEditor)value).RunLocalNormalization(true, out string text2))
                    {
                        result = text2;
                        return false;
                    }
                    if (!string.IsNullOrWhiteSpace(text2))
                    {
                        Debug.Log((object)("[PSD2UIForm.AI] " + text2.Replace("\r\n", " | ").Replace("\r", " | ").Replace("\n", " | ")));
                    }
                }
                return true;
            }
            return false;
        }

        private static bool TryApplyPatchWithNormalization(object value, object value2, out string result)
        {
            return TryApplyPatch(value, null, value2, true, out result);
        }

        private static void WriteBlockingWorkflowSuccess(object value, DateTime value2)
        {
            if (value != null)
            {
                AiJobResultDocument aiJobResultDocument = new AiJobResultDocument
                {
                    jobId = ((AiJobContext)value).JobId,
                    providerId = ((AiJobContext)value).ProviderId,
                    success = true,
                    completedAtUtc = DateTime.UtcNow.ToString("o"),
                    durationMs = (long)(DateTime.UtcNow - value2).TotalMilliseconds,
                    rawOutputFile = MakeRelativePath(((AiJobContext)value).JobDirectory, ((AiJobContext)value).RawOutputPath),
                    patchFile = MakeRelativePath(((AiJobContext)value).JobDirectory, ((AiJobContext)value).PatchPath),
                    errorFile = string.Empty
                };
                AiJobFileStore.WriteJsonAtomic(((AiJobContext)value).ResultPath, aiJobResultDocument);
                AiJobFileStore.WriteJobStatus(value, (AiJobState)3, "Blocking AI workflow completed successfully.");
                AiJobFileStore.LogDebug(value, $"Blocking AI workflow completed. durationMs={aiJobResultDocument.durationMs}, patchPath={((AiJobContext)value).PatchPath}");
            }
        }

        private static void WriteBlockingWorkflowFailure(object value, DateTime value2, object value3)
        {
            if (value != null)
            {
                AiJobResultDocument value4 = new AiJobResultDocument
                {
                    jobId = ((AiJobContext)value).JobId,
                    providerId = ((AiJobContext)value).ProviderId,
                    success = false,
                    completedAtUtc = DateTime.UtcNow.ToString("o"),
                    durationMs = (long)(DateTime.UtcNow - value2).TotalMilliseconds,
                    rawOutputFile = MakeRelativePath(((AiJobContext)value).JobDirectory, ((AiJobContext)value).RawOutputPath),
                    patchFile = string.Empty,
                    errorFile = (File.Exists(((AiJobContext)value).ErrorPath) ? MakeRelativePath(((AiJobContext)value).JobDirectory, ((AiJobContext)value).ErrorPath) : string.Empty)
                };
                AiJobFileStore.WriteJsonAtomic(((AiJobContext)value).ResultPath, value4);
                AiJobFileStore.WriteJobStatus(value, (AiJobState)4, string.IsNullOrWhiteSpace((string)value3) ? "Blocking AI workflow failed." : value3);
            }
        }

        private static bool ValidateHierarchyAgainstAnalysisPackage(object value2, object value3, out string result)
        {
            result = null;
            if (!Psd2UIFormTargetCompat.IsNull(value2) && value3 != null && !string.IsNullOrWhiteSpace(((AiJobContext)value3).AnalysisPackagePath))
            {
                if (AiJobFileStore.TryReadJson<AiAnalysisPackageDocument>(((AiJobContext)value3).AnalysisPackagePath, out var aiAnalysisPackageDocument) && aiAnalysisPackageDocument != null && aiAnalysisPackageDocument.nodes != null)
                {
                    PsdLayerNode[] componentsInChildren = Psd2UIFormTargetCompat.GameObjectOf(value2).GetComponentsInChildren<PsdLayerNode>(true);
                    Dictionary<string, PsdLayerNode> dictionary = new Dictionary<string, PsdLayerNode>(StringComparer.OrdinalIgnoreCase);
                    if (componentsInChildren != null)
                    {
                        foreach (PsdLayerNode psdLayerNode in componentsInChildren)
                        {
                            string text = LayerNodeIdUtility.GetStableNodeId(value2, psdLayerNode);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                dictionary[text] = psdLayerNode;
                            }
                        }
                    }
                    List<string> list = new List<string>();
                    for (int j = 0; j < aiAnalysisPackageDocument.nodes.Count; j++)
                    {
                        AiAnalysisNodeEntry aiAnalysisNodeEntry = aiAnalysisPackageDocument.nodes[j];
                        if (aiAnalysisNodeEntry == null || string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.id))
                        {
                            continue;
                        }
                        if (!dictionary.TryGetValue(aiAnalysisNodeEntry.id, out var value) || Psd2UIFormTargetCompat.IsNull(value))
                        {
                            list.Add("'" + aiAnalysisNodeEntry.id + "' 已不存在");
                            continue;
                        }
                        string text2 = GetParentNodeId(value2, Psd2UIFormTargetCompat.TransformOf(value).parent);
                        string text3 = (string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.uiType) ? GUIType.Null.ToString() : aiAnalysisNodeEntry.uiType);
                        string text4 = aiAnalysisNodeEntry.name ?? string.Empty;
                        if (!string.Equals(text2, aiAnalysisNodeEntry.parentId, StringComparison.OrdinalIgnoreCase) || !string.Equals(value.UIType.ToString(), text3, StringComparison.Ordinal) || !string.Equals(((Object)Psd2UIFormTargetCompat.GameObjectOf(value)).name ?? string.Empty, text4, StringComparison.Ordinal))
                        {
                            list.Add($"'{aiAnalysisNodeEntry.id}' (name={text4}→{((Object)value).name}, parent={aiAnalysisNodeEntry.parentId}→{text2}, uiType={text3}→{value.UIType})");
                        }
                    }
                    if (list.Count > 0)
                    {
                        result = string.Format("节点树已变化 ({0} 个节点差异，应用仍继续): {1}", list.Count, string.Join("; ", list.Take(5)));
                    }
                    return true;
                }
                return true;
            }
            return true;
        }

        private static string GetParentNodeId(object value, object value2)
        {
            if (!Psd2UIFormTargetCompat.IsNull(value) && !Psd2UIFormTargetCompat.IsNull(value2) && !((Object)value2 == (Object)(object)Psd2UIFormTargetCompat.TransformOf(value)))
            {
                PsdLayerNode component = Psd2UIFormTargetCompat.GameObjectOf(value2).GetComponent<PsdLayerNode>();
                if (!Psd2UIFormTargetCompat.IsNull(component))
                {
                    return LayerNodeIdUtility.GetStableNodeId(value, component);
                }
                return "root";
            }
            return "root";
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiHierarchyAnalysisOrchestrator GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
