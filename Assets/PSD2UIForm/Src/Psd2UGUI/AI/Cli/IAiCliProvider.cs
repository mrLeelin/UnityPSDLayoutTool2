using System.Runtime.CompilerServices;
using UGF.EditorTools.Psd2UGUI;

namespace IAiCliProviderNamespace
{
    internal interface IAiCliProvider
    {
        [SpecialName]
        string GetProviderId();

        [SpecialName]
        AiProviderCapabilities GetCapabilities();

        void ExecuteJob(AiJobContext jobContext, AiAnalysisRequest analysisRequest);
    }
}
