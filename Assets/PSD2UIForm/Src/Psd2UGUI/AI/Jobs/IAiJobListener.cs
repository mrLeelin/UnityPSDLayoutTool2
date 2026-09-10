using UGF.EditorTools.Psd2UGUI;

namespace IAiJobListenerNamespace
{
    internal interface IAiJobListener
    {
        void OnJobCompleted(AiJobContext jobContext);

        void OnJobFailed(AiJobContext jobContext, string errorMessage);
    }
}
