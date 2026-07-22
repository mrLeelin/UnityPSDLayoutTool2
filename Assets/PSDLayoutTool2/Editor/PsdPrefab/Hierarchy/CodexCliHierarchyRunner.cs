namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Produces a bounded request package and invokes Codex as a read-only,
    /// ephemeral planner. Successful packages are removed; failed packages are
    /// retained explicitly so users can run/import the request offline.
    /// </summary>
    public sealed class CodexCliHierarchyRunner : IPsdHierarchyAiRunner
    {
        private readonly IHierarchyProcessAdapter processAdapter;
        private readonly Func<string> executableResolver;
        private readonly string packageRoot;

        public CodexCliHierarchyRunner()
            : this(new SystemHierarchyProcessAdapter(), () => "codex",
                Path.Combine("Temp", "PSDLayoutTool2", "Hierarchy"))
        {
        }

        public CodexCliHierarchyRunner(
            IHierarchyProcessAdapter processAdapter,
            Func<string> executableResolver,
            string packageRoot)
        {
            this.processAdapter = processAdapter ?? throw new ArgumentNullException("processAdapter");
            this.executableResolver = executableResolver ?? throw new ArgumentNullException("executableResolver");
            this.packageRoot = string.IsNullOrWhiteSpace(packageRoot)
                ? throw new ArgumentException("Package root is required.", "packageRoot")
                : packageRoot;
        }

        public async Task<PsdHierarchyAiRunResult> RunAsync(
            PsdHierarchyAiRunRequest runRequest,
            CancellationToken cancellationToken)
        {
            if (runRequest == null || runRequest.request == null)
            {
                throw new ArgumentNullException("runRequest");
            }

            cancellationToken.ThrowIfCancellationRequested();
            string operationId = ValidateOperationId(runRequest.operationId);
            string packagePath = Path.GetFullPath(Path.Combine(packageRoot, operationId));
            EnsureChildPath(Path.GetFullPath(packageRoot), packagePath);
            Directory.CreateDirectory(packagePath);

            string requestPath = Path.Combine(packagePath, "request.json");
            string schemaPath = Path.Combine(packagePath, "plan.schema.json");
            string promptPath = Path.Combine(packagePath, "prompt.txt");
            string outputPath = Path.Combine(packagePath, "plan.json");
            string prompt = BuildPrompt(runRequest.targetPrefabPath, requestPath);

            try
            {
                File.WriteAllText(requestPath, PsdHierarchyPlanJson.SerializeRequest(runRequest.request), new UTF8Encoding(false));
                File.WriteAllText(schemaPath, PlanSchema, new UTF8Encoding(false));
                File.WriteAllText(promptPath, prompt, new UTF8Encoding(false));

                var invocation = new PsdHierarchyProcessInvocation
                {
                    executable = executableResolver(),
                    workingDirectory = packagePath,
                    standardInput = prompt,
                    OutputPath = outputPath,
                    useShellExecute = false,
                    arguments = new List<string>
                    {
                        "exec", "--sandbox", "read-only", "--ephemeral",
                        "--output-schema", schemaPath, "-o", outputPath, "-"
                    }
                };

                PsdHierarchyProcessResult processResult = await processAdapter
                    .RunAsync(invocation, NormalizeTimeout(runRequest.timeout), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (processResult == null)
                {
                    return Failed("Codex process returned no result.", packagePath, null);
                }

                if (processResult.timedOut)
                {
                    return Failed("Codex hierarchy planning timeout (process timed out).", packagePath, processResult);
                }

                if (processResult.exitCode != 0)
                {
                    string detail = string.IsNullOrWhiteSpace(processResult.standardError)
                        ? processResult.error
                        : processResult.standardError;
                    return Failed("Codex hierarchy planning failed (exit " + processResult.exitCode + "): " + detail,
                        packagePath, processResult);
                }

                if (!File.Exists(outputPath))
                {
                    return Failed("Codex hierarchy plan output is missing.", packagePath, processResult);
                }

                if (new FileInfo(outputPath).Length > PsdHierarchyContractLimits.MaxJsonUtf8Bytes)
                {
                    return Failed("Hierarchy plan exceeds the UTF-8 byte limit.", packagePath, processResult);
                }

                try
                {
                    PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(File.ReadAllText(outputPath, Encoding.UTF8));
                    PsdHierarchyPlanValidator.Validate(plan, runRequest.request);
                    var success = new PsdHierarchyAiRunResult
                    {
                        succeeded = true,
                        plan = plan,
                        standardOutput = processResult.standardOutput ?? string.Empty,
                        standardError = processResult.standardError ?? string.Empty,
                        requestPackagePath = packagePath
                    };
                    DeletePackage(packagePath);
                    return success;
                }
                catch (Exception exception) when (
                    exception is PsdHierarchyPlanFormatException ||
                    exception is PsdHierarchyPlanValidationException)
                {
                    return Failed("Hierarchy plan was rejected: " + exception.Message, packagePath, processResult);
                }
            }
            catch (OperationCanceledException)
            {
                DeletePackage(packagePath);
                throw;
            }
            catch (Exception exception)
            {
                // Startup/offline failures retain the complete bounded package.
                return Failed("Unable to run Codex hierarchy planner: " + exception.Message, packagePath, null);
            }
        }

        private static readonly string PlanSchema =
            "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"schemaVersion\",\"sourcePsdGuid\",\"sourceFingerprint\",\"contentFingerprint\",\"structureFingerprint\",\"geometryFingerprint\",\"groups\",\"renames\"]," +
            "\"properties\":{\"schemaVersion\":{\"const\":1},\"sourcePsdGuid\":{\"type\":\"string\"},\"sourceFingerprint\":{\"type\":\"string\"},\"contentFingerprint\":{\"type\":\"string\"},\"structureFingerprint\":{\"type\":\"string\"},\"geometryFingerprint\":{\"type\":\"string\"}," +
            "\"groups\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"key\",\"parentKey\",\"memberStableIds\",\"displayName\",\"evidence\",\"confidence\"],\"properties\":{\"key\":{\"type\":\"string\"},\"parentKey\":{\"type\":\"string\"},\"memberStableIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}},\"displayName\":{\"type\":\"string\"},\"evidence\":{\"type\":\"string\"},\"confidence\":{\"type\":\"number\",\"minimum\":0,\"maximum\":1}}}}," +
            "\"renames\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"stableId\",\"name\",\"evidence\",\"confidence\"],\"properties\":{\"stableId\":{\"type\":\"string\"},\"name\":{\"type\":\"string\"},\"evidence\":{\"type\":\"string\"},\"confidence\":{\"type\":\"number\",\"minimum\":0,\"maximum\":1}}}}}}";

        private static string BuildPrompt(string targetPrefabPath, string requestPath)
        {
            return "You are a read-only PSD hierarchy planner. You have no permission to write Unity Assets, Prefabs, Profiles, materials, or project files. " +
                   "Read the bounded request JSON at " + requestPath + ". Return only a plan matching plan.schema.json. " +
                   "The target shown for evidence only is '" + (targetPrefabPath ?? string.Empty).Replace("'", "") + "'. " +
                   "Do not propose commands, code, material edits, deletions, or any field outside the schema.";
        }

        private static TimeSpan NormalizeTimeout(TimeSpan value)
        {
            if (value <= TimeSpan.Zero)
            {
                return TimeSpan.FromMinutes(2);
            }
            return value > TimeSpan.FromMinutes(10) ? TimeSpan.FromMinutes(10) : value;
        }

        private static string ValidateOperationId(string operationId)
        {
            if (string.IsNullOrWhiteSpace(operationId) || operationId.Length > 128)
            {
                throw new ArgumentException("A bounded operation ID is required.", "operationId");
            }

            foreach (char character in operationId)
            {
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
                {
                    throw new ArgumentException("Operation ID contains an unsafe character.", "operationId");
                }
            }
            return operationId;
        }

        private static void EnsureChildPath(string root, string child)
        {
            string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!child.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Request package escaped its Temp root.");
            }
        }

        private static PsdHierarchyAiRunResult Failed(
            string error,
            string packagePath,
            PsdHierarchyProcessResult processResult)
        {
            return new PsdHierarchyAiRunResult
            {
                succeeded = false,
                error = error ?? string.Empty,
                standardOutput = processResult != null ? processResult.standardOutput ?? string.Empty : string.Empty,
                standardError = processResult != null ? processResult.standardError ?? string.Empty : string.Empty,
                requestPackagePath = packagePath,
                offlinePackageAvailable = Directory.Exists(packagePath)
            };
        }

        private static void DeletePackage(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }

    /// <summary>
    /// Direct process adapter: no cmd.exe/PowerShell, redirected streams, and a
    /// linked timeout that kills the child before returning control to Unity.
    /// </summary>
    public sealed class SystemHierarchyProcessAdapter : IHierarchyProcessAdapter
    {
        public async Task<PsdHierarchyProcessResult> RunAsync(
            PsdHierarchyProcessInvocation invocation,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (invocation == null)
            {
                throw new ArgumentNullException("invocation");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = invocation.executable,
                Arguments = JoinArguments(invocation.arguments),
                WorkingDirectory = invocation.workingDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true })
            using (var timeoutSource = new CancellationTokenSource(timeout))
            using (var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token))
            {
                var completion = new TaskCompletionSource<int>();
                process.Exited += (sender, args) => completion.TrySetResult(process.ExitCode);
                if (!process.Start())
                {
                    throw new InvalidOperationException("Codex process did not start.");
                }

                Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                Task<string> stderr = process.StandardError.ReadToEndAsync();
                await process.StandardInput.WriteAsync(invocation.standardInput ?? string.Empty);
                process.StandardInput.Close();

                using (linkedSource.Token.Register(() => completion.TrySetCanceled()))
                {
                    try
                    {
                        int exitCode = await completion.Task;
                        return new PsdHierarchyProcessResult
                        {
                            exitCode = exitCode,
                            standardOutput = await stdout,
                            standardError = await stderr
                        };
                    }
                    catch (TaskCanceledException)
                    {
                        TryKill(process);
                        if (cancellationToken.IsCancellationRequested)
                        {
                            throw new OperationCanceledException(cancellationToken);
                        }

                        return new PsdHierarchyProcessResult
                        {
                            timedOut = true,
                            wasKilled = true,
                            error = "Process timeout."
                        };
                    }
                }
            }
        }

        private static string JoinArguments(IEnumerable<string> arguments)
        {
            var builder = new StringBuilder();
            foreach (string argument in arguments ?? Array.Empty<string>())
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }
                builder.Append(QuoteArgument(argument));
            }
            return builder.ToString();
        }

        private static string QuoteArgument(string argument)
        {
            argument = argument ?? string.Empty;
            if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
            {
                return argument;
            }

            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char character in argument)
            {
                if (character == '\\')
                {
                    slashes++;
                    continue;
                }
                if (character == '"')
                {
                    result.Append('\\', slashes * 2 + 1);
                    result.Append('"');
                    slashes = 0;
                    continue;
                }
                result.Append('\\', slashes);
                slashes = 0;
                result.Append(character);
            }
            result.Append('\\', slashes * 2);
            result.Append('"');
            return result.ToString();
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
            }
            catch (InvalidOperationException)
            {
                // Process exited between the observation and kill attempt.
            }
        }
    }
}
