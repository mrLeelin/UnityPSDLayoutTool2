namespace PsdLayoutTool2
{
    /// <summary>
    /// Decides whether a Common Prefab must retain the PSD layer's source
    /// visual because the resolved prefab cannot draw anything by itself.
    /// </summary>
    internal static class PsdCommonPrefabVisualFallbackPolicy
    {
        internal static bool RequiresSourceVisualFallback(bool hasRenderableVisual)
        {
            return !hasRenderableVisual;
        }
    }
}
