namespace PsdLayoutTool2
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using Newtonsoft.Json;

    internal readonly struct PsdHierarchyPlanWorkspaceStoreResult
    {
        internal PsdHierarchyPlanWorkspaceStoreResult(bool success, string path, string error)
        {
            this.success = success;
            this.path = path ?? string.Empty;
            this.error = error ?? string.Empty;
        }

        internal readonly bool success;
        internal readonly string path;
        internal readonly string error;
    }

    internal static class PsdHierarchyPlanWorkspaceStore
    {
        private const string RelativeDirectory = "Library/PSDLayoutTool2/PlanWorkspaces";
        private static readonly Regex UnsafePathSegmentRegex = new Regex(
            "[^A-Za-z0-9_-]+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        internal static PsdHierarchyPlanWorkspaceStoreResult TrySave(
            string projectRoot,
            PsdHierarchyPlanWorkspace workspace)
        {
            if (workspace == null)
            {
                return new PsdHierarchyPlanWorkspaceStoreResult(false, string.Empty, "缺少可导出的计划工作区。");
            }

            try
            {
                string root = Path.GetFullPath(projectRoot ?? string.Empty);
                string fingerprint = SanitizePathSegment(workspace.snapshotFingerprint);
                string directory = Path.Combine(
                    root,
                    RelativeDirectory.Replace('/', Path.DirectorySeparatorChar),
                    fingerprint);
                Directory.CreateDirectory(directory);

                string filename = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'") + "-" +
                                  Guid.NewGuid().ToString("N").Substring(0, 8) + ".json";
                string path = Path.Combine(directory, filename);
                string json = JsonConvert.SerializeObject(workspace, Formatting.Indented);
                File.WriteAllText(path, json, new UTF8Encoding(false));
                return new PsdHierarchyPlanWorkspaceStoreResult(true, path, string.Empty);
            }
            catch (Exception exception)
            {
                return new PsdHierarchyPlanWorkspaceStoreResult(false, string.Empty, exception.Message);
            }
        }

        private static string SanitizePathSegment(string value)
        {
            string sanitized = UnsafePathSegmentRegex.Replace(value ?? string.Empty, "_").Trim('_');
            return string.IsNullOrWhiteSpace(sanitized) ? "unknown-snapshot" : sanitized;
        }
    }
}
