namespace PsdLayoutTool2
{
    using System;
    using System.IO;

    public sealed class ClaudeCliHierarchyRunner : IPsdHierarchyAiRunner
    {
        private readonly CodexCliHierarchyRunner implementation;

        public ClaudeCliHierarchyRunner()
            : this(new SystemHierarchyProcessAdapter(),
                () => ResolveDefaultExecutable(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
                Path.Combine("Temp", "PSDLayoutTool2", "Hierarchy"),
                new PsdHierarchyAiConnectionSnapshot(PsdHierarchyAiConnectionMode.Default, string.Empty),
                null, string.Empty)
        {
        }

        internal static string ResolveDefaultExecutable(string roamingAppData)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT &&
                !string.IsNullOrWhiteSpace(roamingAppData))
            {
                string executablePath = Path.Combine(
                    roamingAppData,
                    "npm",
                    "node_modules",
                    "@anthropic-ai",
                    "claude-code",
                    "bin",
                    "claude.exe");
                if (File.Exists(executablePath)) return Path.GetFullPath(executablePath);
            }

            return "claude";
        }

        internal ClaudeCliHierarchyRunner(
            IHierarchyProcessAdapter processAdapter,
            Func<string> executableResolver,
            string packageRoot,
            PsdHierarchyAiConnectionSnapshot connection,
            IPsdAiSecretStore secretStore,
            string projectIdentity)
        {
            implementation = new CodexCliHierarchyRunner(
                processAdapter, executableResolver, packageRoot,
                path => Directory.CreateDirectory(path), () => Array.Empty<string>(),
                PsdHierarchyAiProvider.Claude, connection, secretStore, projectIdentity);
        }

        public System.Threading.Tasks.Task<PsdHierarchyAiRunResult> RunAsync(
            PsdHierarchyAiRunRequest request,
            System.Threading.CancellationToken cancellationToken)
        {
            return implementation.RunAsync(request, cancellationToken);
        }
    }
}
