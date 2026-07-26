namespace PsdLayoutTool2
{
    internal interface IPsdAiSecretStore
    {
        bool HasSavedCredential(string projectIdentity, PsdHierarchyAiProvider provider);

        bool TryRead(string projectIdentity, PsdHierarchyAiProvider provider, out string key);

        void Save(string projectIdentity, PsdHierarchyAiProvider provider, string key);

        void Clear(string projectIdentity, PsdHierarchyAiProvider provider);
    }
}
