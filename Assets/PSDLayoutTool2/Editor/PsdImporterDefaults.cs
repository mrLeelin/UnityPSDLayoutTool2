namespace PsdLayoutTool2
{
    /// <summary>
    /// Centralizes first-use importer defaults while preserving explicit user choices.
    /// </summary>
    public static class PsdImporterDefaults
    {
        public static bool ResolveUseUnityUI(bool hasSavedValue, bool savedValue)
        {
            return hasSavedValue ? savedValue : true;
        }
    }
}
