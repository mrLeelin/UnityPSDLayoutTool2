using UGF.EditorTools.Psd2UGUI;
using UnityEditor;

namespace Psd2UIFormConfigAssetPostprocessorNamespace
{
    internal sealed class Psd2UIFormConfigAssetPostprocessor : AssetPostprocessor
    {
        internal static Psd2UIFormConfigAssetPostprocessor s_ObfuscationSentinel;

        private static void HandleRelevantAssetChanges(object value, object value2, object value3, object value4)
        {
            if (Psd2UIFormConfigRepair.ContainsRelevantAsset((string[])value) || Psd2UIFormConfigRepair.ContainsRelevantAsset((string[])value2) || Psd2UIFormConfigRepair.ContainsRelevantAsset((string[])value3) || Psd2UIFormConfigRepair.ContainsRelevantAsset((string[])value4))
            {
                Psd2UIFormConfigRepair.ScheduleEnsureConfigReady();
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static Psd2UIFormConfigAssetPostprocessor GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
