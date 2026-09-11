using System.Collections.Generic;
using cn.efunstudio.psdreader.PsdParser;

namespace PsdLayerTraversalExtensionsNamespace
{
    internal static class PsdLayerTraversalExtensions
    {
        internal static IEnumerable<PsdLayer> EnumerateDescendantsAndSelf(this object psdLayer2)
        {
            yield return (PsdLayer)psdLayer2;
            PsdLayer[] childs = ((PsdLayer)psdLayer2).Childs;
            foreach (PsdLayer psdLayer in childs)
            {
                foreach (PsdLayer item in psdLayer.EnumerateDescendantsAndSelf())
                {
                    yield return item;
                }
            }
        }
    }
}
