namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEditor.Compilation;

    /// <summary>
    /// Executes the reviewed cleanup payload inside the current Unity Editor
    /// without starting uLoop. The payload renderer remains shared with uLoop
    /// so both backends execute the same validated Unity operations.
    /// </summary>
    internal static class PsdHierarchyNativePayloadExecutor
    {
        internal const string NativePayloadNamespace = "PsdLayoutTool2";
        internal const string NativePayloadTypeName = "NativeCleanupPayload";
        internal const string NativePayloadMethodName = "Execute";
        internal const string NativePayloadDirectory = "Library/PSDLayoutTool2/NativeCleanupPayloads";
        internal const string RendererRelativePath = ".agents/skills/prefab-hierarchy-cleanup/scripts/render_prefab_cleanup.py";

        internal static string WrapPayloadSource(string payloadSource)
        {
            if (string.IsNullOrWhiteSpace(payloadSource))
                throw new ArgumentException("Native cleanup payload is empty.", nameof(payloadSource));

            var usingLines = new List<string>();
            var bodyLines = new List<string>();
            bool collectingUsings = true;
            using (var reader = new StringReader(payloadSource))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (collectingUsings && (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("using ", StringComparison.Ordinal)))
                    {
                        if (!string.IsNullOrWhiteSpace(line)) usingLines.Add(line);
                        continue;
                    }

                    collectingUsings = false;
                    bodyLines.Add(line);
                }
            }

            var builder = new StringBuilder(payloadSource.Length + 256);
            foreach (string line in usingLines) builder.AppendLine(line);
            builder.AppendLine("using Object = UnityEngine.Object;");
            builder.AppendLine("namespace " + NativePayloadNamespace);
            builder.AppendLine("{");
            builder.AppendLine("    public static class " + NativePayloadTypeName);
            builder.AppendLine("    {");
            builder.AppendLine("        public static string " + NativePayloadMethodName + "()");
            builder.AppendLine("        {");
            foreach (string line in bodyLines) builder.AppendLine("            " + line);
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        internal static string BuildNativePayloadDirectory(string projectRoot)
        {
            return Path.Combine(Path.GetFullPath(projectRoot ?? string.Empty), NativePayloadDirectory.Replace('/', Path.DirectorySeparatorChar));
        }

        internal static string BuildNativePayloadPath(string projectRoot, string id, string extension)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Payload id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(extension)) throw new ArgumentException("Payload extension is required.", nameof(extension));
            return Path.Combine(BuildNativePayloadDirectory(projectRoot), id + extension);
        }

        internal static async Task<PsdHierarchyNativePayloadResult> ExecuteAsync(
            string projectRoot,
            string skillFullPath,
            string planJson,
            string mode)
        {
            return await RenderCompileAndMaybeInvokeAsync(
                projectRoot,
                skillFullPath,
                planJson,
                mode,
                true);
        }

        internal static async Task<PsdHierarchyNativePayloadResult> CompileOnlyAsync(
            string projectRoot,
            string skillFullPath,
            string planJson,
            string mode)
        {
            return await RenderCompileAndMaybeInvokeAsync(
                projectRoot,
                skillFullPath,
                planJson,
                mode,
                false);
        }

        private static async Task<PsdHierarchyNativePayloadResult> RenderCompileAndMaybeInvokeAsync(
            string projectRoot,
            string skillFullPath,
            string planJson,
            string mode,
            bool invokePayload)
        {
            if (string.IsNullOrWhiteSpace(projectRoot)) return PsdHierarchyNativePayloadResult.Failure("Native payload execution requires the Unity project root.");
            if (string.IsNullOrWhiteSpace(planJson)) return PsdHierarchyNativePayloadResult.Failure("Native payload execution received an empty plan.");

            string id = Guid.NewGuid().ToString("N");
            string directory = BuildNativePayloadDirectory(projectRoot);
            string planPath = BuildNativePayloadPath(projectRoot, id, ".plan.json");
            string renderedPath = BuildNativePayloadPath(projectRoot, id, ".payload.cs");
            string sourcePath = BuildNativePayloadPath(projectRoot, id, ".compiled.cs");
            string assemblyPath = BuildNativePayloadPath(projectRoot, id, ".dll");
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(planPath, planJson, new UTF8Encoding(false));
                string rendererPath = ResolveRendererPath(projectRoot, skillFullPath);
                if (!File.Exists(rendererPath)) return PsdHierarchyNativePayloadResult.Failure("Prefab cleanup payload renderer was not found: " + rendererPath);

                PsdHierarchyNativePayloadResult renderResult = await RenderPayloadAsync(rendererPath, planPath, renderedPath, mode);
                if (!renderResult.success) return renderResult;

                File.WriteAllText(sourcePath, WrapPayloadSource(File.ReadAllText(renderedPath, Encoding.UTF8)), new UTF8Encoding(false));
                return await CompileAndInvokeAsync(sourcePath, assemblyPath, invokePayload);
            }
            catch (Exception exception)
            {
                return PsdHierarchyNativePayloadResult.Failure("Native payload execution failed: " + exception.Message);
            }
            finally
            {
                DeleteFile(planPath);
                DeleteFile(renderedPath);
                DeleteFile(sourcePath);
                DeleteFile(assemblyPath);
                DeleteFile(Path.ChangeExtension(assemblyPath, ".pdb"));
            }
        }

        private static async Task<PsdHierarchyNativePayloadResult> RenderPayloadAsync(string rendererPath, string planPath, string renderedPath, string mode)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = Quote(rendererPath) + " --plan " + Quote(planPath) + " --mode " + Quote(mode) + " --output " + Quote(renderedPath),
                WorkingDirectory = Path.GetDirectoryName(rendererPath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = Encoding.Default,
            };

            using (Process process = Process.Start(startInfo))
            {
                if (process == null) return PsdHierarchyNativePayloadResult.Failure("Could not start the Prefab cleanup payload renderer.");
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit());
                string output = (await outputTask).Trim();
                string error = (await errorTask).Trim();
                if (process.ExitCode != 0 || !File.Exists(renderedPath))
                {
                    string detail = string.IsNullOrWhiteSpace(error) ? output : error;
                    return PsdHierarchyNativePayloadResult.Failure("Native payload rendering failed: " + (string.IsNullOrWhiteSpace(detail) ? "renderer returned no detail" : detail));
                }
                return PsdHierarchyNativePayloadResult.Success(output);
            }
        }

        private static Task<PsdHierarchyNativePayloadResult> CompileAndInvokeAsync(
            string sourcePath,
            string assemblyPath,
            bool invokePayload)
        {
            var completion = new TaskCompletionSource<PsdHierarchyNativePayloadResult>();
            var builder = new AssemblyBuilder(assemblyPath, new[] { sourcePath })
            {
                flags = AssemblyBuilderFlags.EditorAssembly,
                referencesOptions = ReferencesOptions.UseEngineModules,
            };
            builder.buildFinished += (_, messages) =>
            {
                CompilerMessage[] diagnostics = messages ?? Array.Empty<CompilerMessage>();
                CompilerMessage[] errors = diagnostics.Where(message => message.type == CompilerMessageType.Error).ToArray();
                if (errors.Length > 0)
                {
                    completion.TrySetResult(PsdHierarchyNativePayloadResult.Failure("Native payload compilation failed: " + FormatDiagnostics(errors)));
                    return;
                }

                if (!invokePayload)
                {
                    completion.TrySetResult(PsdHierarchyNativePayloadResult.Success("Native payload compilation succeeded."));
                    return;
                }

                try
                {
                    System.Reflection.Assembly assembly = System.Reflection.Assembly.Load(File.ReadAllBytes(assemblyPath));
                    Type payloadType = assembly.GetType(NativePayloadNamespace + "." + NativePayloadTypeName, true);
                    MethodInfo execute = payloadType.GetMethod(NativePayloadMethodName, BindingFlags.Public | BindingFlags.Static);
                    if (execute == null) throw new MissingMethodException(payloadType.FullName, NativePayloadMethodName);
                    completion.TrySetResult(PsdHierarchyNativePayloadResult.Success(execute.Invoke(null, null) as string ?? string.Empty));
                }
                catch (TargetInvocationException exception)
                {
                    completion.TrySetResult(PsdHierarchyNativePayloadResult.Failure("Native payload execution failed: " + (exception.InnerException ?? exception).Message));
                }
                catch (Exception exception)
                {
                    completion.TrySetResult(PsdHierarchyNativePayloadResult.Failure("Native payload execution failed: " + exception.Message));
                }
            };

            if (!builder.Build()) completion.TrySetResult(PsdHierarchyNativePayloadResult.Failure("Native payload compilation could not be started."));
            return completion.Task;
        }

        private static string ResolveRendererPath(string projectRoot, string skillFullPath)
        {
            if (!string.IsNullOrWhiteSpace(skillFullPath))
            {
                string skillDirectory = Path.GetDirectoryName(skillFullPath);
                if (!string.IsNullOrEmpty(skillDirectory))
                {
                    string candidate = Path.Combine(skillDirectory, "scripts", "render_prefab_cleanup.py");
                    if (File.Exists(candidate)) return candidate;
                }
            }

            PsdHierarchyChatContextBuilder.TryResolvePackageFilePath(
                projectRoot,
                FindSourceScriptAssetPath(),
                RendererRelativePath,
                out string resolved);
            return resolved;
        }

        private static string FindSourceScriptAssetPath()
        {
            foreach (string guid in AssetDatabase.FindAssets("PsdHierarchyNativePayloadExecutor t:Script"))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (assetPath.EndsWith("/PsdHierarchyNativePayloadExecutor.cs", StringComparison.OrdinalIgnoreCase))
                    return assetPath;
            }
            return string.Empty;
        }

        private static string FormatDiagnostics(IEnumerable<CompilerMessage> messages)
        {
            return string.Join(Environment.NewLine, messages.Select(message => message.file + "(" + message.line + "," + message.column + "): " + message.message));
        }

        private static string Quote(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";

        private static void DeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try { File.Delete(path); } catch { }
        }
    }

    internal readonly struct PsdHierarchyNativePayloadResult
    {
        internal readonly bool success;
        internal readonly string message;

        private PsdHierarchyNativePayloadResult(bool success, string message)
        {
            this.success = success;
            this.message = message ?? string.Empty;
        }

        internal static PsdHierarchyNativePayloadResult Success(string message) => new PsdHierarchyNativePayloadResult(true, message);
        internal static PsdHierarchyNativePayloadResult Failure(string message) => new PsdHierarchyNativePayloadResult(false, message);
    }
}
