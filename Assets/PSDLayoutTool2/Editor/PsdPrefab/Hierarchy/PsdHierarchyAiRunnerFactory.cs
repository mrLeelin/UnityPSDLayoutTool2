namespace PsdLayoutTool2
{
    using System;
    using System.IO;
    using UnityEngine;

    internal static class PsdHierarchyAiRunnerFactory
    {
        internal static IPsdHierarchyAiRunner CreateConfigured()
        {
            DirectoryInfo projectDirectory = Directory.GetParent(Application.dataPath);
            string projectIdentity = projectDirectory == null
                ? Application.dataPath
                : projectDirectory.FullName;
            return CreateConfigured(
                PsdLayoutProjectSettings.instance,
                new PsdAiSecretStore(),
                projectIdentity,
                new SystemHierarchyProcessAdapter(),
                Path.Combine("Temp", "PSDLayoutTool2", "Hierarchy"),
                () => CodexCliHierarchyRunner.ResolveDefaultExecutable(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
                () => ClaudeCliHierarchyRunner.ResolveDefaultExecutable(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)));
        }

        internal static IPsdHierarchyAiRunner CreateConfigured(
            PsdLayoutProjectSettings settings,
            IPsdAiSecretStore secretStore,
            string projectIdentity,
            IHierarchyProcessAdapter processAdapter,
            string packageRoot,
            Func<string> codexExecutableResolver,
            Func<string> claudeExecutableResolver)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return Create(
                settings.ResolveAiSettings(),
                secretStore,
                projectIdentity,
                processAdapter,
                packageRoot,
                codexExecutableResolver,
                claudeExecutableResolver);
        }

        internal static IPsdHierarchyAiRunner Create(
            PsdHierarchyAiSettingsSnapshot settings,
            IPsdAiSecretStore secretStore,
            string projectIdentity,
            IHierarchyProcessAdapter processAdapter,
            string packageRoot,
            Func<string> codexExecutableResolver,
            Func<string> claudeExecutableResolver)
        {
            switch (settings.provider)
            {
                case PsdHierarchyAiProvider.Codex:
                    return new CodexCliHierarchyRunner(processAdapter, codexExecutableResolver,
                        packageRoot, settings.codex, secretStore, projectIdentity);
                case PsdHierarchyAiProvider.Claude:
                    return new ClaudeCliHierarchyRunner(processAdapter, claudeExecutableResolver,
                        packageRoot, settings.claude, secretStore, projectIdentity);
                default:
                    throw new ArgumentOutOfRangeException(nameof(settings.provider), settings.provider,
                        "Unsupported hierarchy AI provider.");
            }
        }
    }
}
