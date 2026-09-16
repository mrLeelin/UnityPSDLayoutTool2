using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiProviderConnectionSettings
    {
        public bool useCustomApi;

        public string customApiUrl = string.Empty;
    }
}
