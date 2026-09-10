using System.Runtime.CompilerServices;
using EmbeddedLinkedLayerNamespace;

namespace EmbeddedLinkedLayerCollectionNamespace
{
    internal class EmbeddedLinkedLayerCollection
    {
        [CompilerGenerated]
        private EmbeddedLinkedLayer[] _layers;

        internal static EmbeddedLinkedLayerCollection s_ObfuscationSentinel;

        [SpecialName]
        [CompilerGenerated]
        public EmbeddedLinkedLayer[] GetLayers()
        {
            return _layers;
        }

        [SpecialName]
        [CompilerGenerated]
        public void SetLayers(EmbeddedLinkedLayer[] embeddedLinkedLayers)
        {
            _layers = embeddedLinkedLayers;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static EmbeddedLinkedLayerCollection GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
