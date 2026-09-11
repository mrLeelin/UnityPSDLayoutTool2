using System;
using System.IO;
using System.Text;
using AiJobFileStoreNamespace;
using AiPatchValidatorNamespace;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;
using Object = UnityEngine.Object;
using AiPatchLocalNormalizerNamespace;
using AiCliArtifactUtilityNamespace;

namespace AiPatchResultLoaderNamespace
{
    internal sealed class AiPatchResultLoader
    {
        private static readonly UTF8Encoding _utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        internal static AiPatchResultLoader s_ObfuscationSentinel;

        internal bool TryLoadPatch(AiJobContext aiJobContext, out AiPatchDocument result, out string result2)
        {
            result = null;
            result2 = null;
            if (aiJobContext != null)
            {
                if (string.IsNullOrWhiteSpace(aiJobContext.PatchPath) || !File.Exists(aiJobContext.PatchPath))
                {
                    result2 = "Patch file not found: " + aiJobContext?.PatchPath;
                    return false;
                }
                try
                {
                    string text;
                    using (FileStream stream = new FileStream(aiJobContext.PatchPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        using StreamReader streamReader = new StreamReader(stream, _utf8NoBom);
                        text = streamReader.ReadToEnd();
                    }
                    if (!AiCliArtifactUtility.TryValidatePatchJson(text, out result2))
                    {
                        return false;
                    }
                    result = JsonUtility.FromJson<AiPatchDocument>(text);
                    if (result == null)
                    {
                        result2 = "Patch file is empty or invalid.";
                        return false;
                    }
                    AiAnalysisPackageDocument aiAnalysisPackageDocument = null;
                    if (!string.IsNullOrWhiteSpace(aiJobContext.AnalysisPackagePath) && File.Exists(aiJobContext.AnalysisPackagePath))
                    {
                        AiJobFileStore.TryReadJson<AiAnalysisPackageDocument>(aiJobContext.AnalysisPackagePath, out aiAnalysisPackageDocument);
                    }
                    bool num = AiPatchLocalNormalizer.NormalizePatchDocument(result);
                    bool flag;
                    if (flag = AiPatchValidator.NormalizeOperationOrder(result))
                    {
                        AiJobFileStore.LogDebug(aiJobContext, "Normalized patch operation order to create -> structure -> type -> cleanup.");
                        Debug.LogWarning((object)"[PSD2UIForm.AI] Patch operations were out of phase order and have been normalized locally.");
                    }
                    bool flag2;
                    if (flag2 = AiPatchLocalNormalizer.RepairAnalysisOwnership(result, aiAnalysisPackageDocument))
                    {
                        AiJobFileStore.LogDebug(aiJobContext, "Normalized ownerless semantic analysis entries.");
                        Debug.LogWarning((object)"[PSD2UIForm.AI] Patch 包含无 ownerId 的子控件语义，已按最近兼容 owner 或独立 Image 本地归一化。");
                    }
                    if (num || flag || flag2)
                    {
                        AiJobFileStore.WriteJsonAtomic(aiJobContext.PatchPath, result);
                    }
                    if (!new AiPatchValidator().ValidatePatch(result, aiAnalysisPackageDocument, out result2))
                    {
                        string text2 = result2;
                        AiJobFileStore.LogDebug(aiJobContext, "Patch validation warning; loading patch and letting applier skip invalid items. error=" + text2);
                        Debug.LogWarning((object)("[PSD2UIForm.AI] Patch 本地校验未完全通过，将继续加载并在应用时跳过无效项。error=" + text2));
                        result2 = null;
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    result2 = "Failed to parse patch file: " + ex.Message;
                    result = null;
                    return false;
                }
            }
            result2 = "AI job context is null.";
            return false;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiPatchResultLoader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
