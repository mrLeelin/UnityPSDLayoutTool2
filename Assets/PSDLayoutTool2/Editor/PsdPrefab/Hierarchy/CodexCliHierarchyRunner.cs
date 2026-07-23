namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

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
        private readonly Action<string> packageDirectoryCreator;

        public CodexCliHierarchyRunner()
            : this(new SystemHierarchyProcessAdapter(),
                () => ResolveDefaultExecutable(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
                Path.Combine("Temp", "PSDLayoutTool2", "Hierarchy"))
        {
        }

        /// <summary>
        /// Unity inherits its PATH only when the Editor process starts. A global
        /// npm install performed later leaves a valid Windows shim outside that
        /// stale PATH, even though the same account can run <c>codex</c> in a
        /// newly opened terminal. Prefer the known npm shim when it exists and
        /// otherwise retain the portable PATH-based command fallback.
        /// </summary>
        internal static string ResolveDefaultExecutable(string roamingAppData)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT &&
                !string.IsNullOrWhiteSpace(roamingAppData))
            {
                string npmShim = Path.Combine(roamingAppData, "npm", "codex.cmd");
                if (File.Exists(npmShim)) return Path.GetFullPath(npmShim);
            }

            return "codex";
        }

        public CodexCliHierarchyRunner(
            IHierarchyProcessAdapter processAdapter,
            Func<string> executableResolver,
            string packageRoot)
            : this(processAdapter, executableResolver, packageRoot, path => Directory.CreateDirectory(path))
        {
        }

        public CodexCliHierarchyRunner(
            IHierarchyProcessAdapter processAdapter,
            Func<string> executableResolver,
            string packageRoot,
            Action<string> packageDirectoryCreator)
        {
            this.processAdapter = processAdapter ?? throw new ArgumentNullException("processAdapter");
            this.executableResolver = executableResolver ?? throw new ArgumentNullException("executableResolver");
            this.packageRoot = string.IsNullOrWhiteSpace(packageRoot)
                ? throw new ArgumentException("Package root is required.", "packageRoot")
                : packageRoot;
            this.packageDirectoryCreator = packageDirectoryCreator ?? throw new ArgumentNullException("packageDirectoryCreator");
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
            string rootPath = Path.GetFullPath(packageRoot);
            Directory.CreateDirectory(rootPath);
            string packagePath = Path.GetFullPath(Path.Combine(rootPath, operationId));
            string lockPath = Path.GetFullPath(Path.Combine(rootPath, "." + operationId + ".lock"));
            EnsureChildPath(rootPath, packagePath);
            EnsureChildPath(rootPath, lockPath);

            FileStream operationLock;
            try
            {
                operationLock = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            }
            catch (IOException)
            {
                return OwnershipRejected("Hierarchy operation is already in progress.", packagePath);
            }

            using (operationLock)
            {
                try
                {
                    if (Directory.Exists(packagePath))
                    {
                        return OwnershipRejected("Hierarchy operation package already exists; refusing stale output.", packagePath);
                    }

                    packageDirectoryCreator(packagePath);

                    string requestPath = Path.Combine(packagePath, "request.json");
                    string focusPath = Path.Combine(packagePath, "focus.json");
                    string schemaPath = Path.Combine(packagePath, "plan.schema.json");
                    string promptPath = Path.Combine(packagePath, "prompt.txt");
                    string outputPath = Path.Combine(packagePath, "plan.json");
                    string prompt = BuildPrompt(runRequest.targetPrefabPath, requestPath, focusPath);

                    File.WriteAllText(requestPath, PsdHierarchyPlanJson.SerializeRequest(runRequest.request), new UTF8Encoding(false));
                    string focusJson = SerializeFocus(runRequest);
                    if (focusJson.Length > PsdHierarchyContractLimits.MaxJsonCharacters ||
                        Encoding.UTF8.GetByteCount(focusJson) > PsdHierarchyContractLimits.MaxJsonUtf8Bytes)
                    {
                        throw new PsdHierarchyPlanFormatException("Focused hierarchy metadata exceeds the JSON quota.");
                    }
                    File.WriteAllText(focusPath, focusJson, new UTF8Encoding(false));
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

                    if (processResult.outputLimitExceeded)
                    {
                        return Failed("Codex stdout/stderr exceeded the bounded output quota.", packagePath, processResult);
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
                        string json;
                        using (var outputReader = new StreamReader(outputPath, Encoding.UTF8, true, 4096))
                        {
                            json = await PsdHierarchyBoundedTextReader.ReadAsync(
                                outputReader, PsdHierarchyContractLimits.MaxJsonCharacters, cancellationToken);
                        }
                        PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(json);
                        if ((runRequest.modifiableStableIds ?? new List<string>()).Count > 0)
                        {
                            PsdHierarchyFocusedPlanValidator.ValidatePartial(plan, runRequest);
                        }
                        else
                        {
                            PsdHierarchyPlanValidator.Validate(plan, runRequest.request);
                        }
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
                        exception is PsdHierarchyPlanValidationException ||
                        exception is PsdHierarchyOutputLimitException)
                    {
                        return Failed("Hierarchy plan was rejected: " + exception.Message, packagePath, processResult);
                    }
                }
                catch (PsdHierarchyProcessCancelledException exception)
                {
                    if (exception.processTreeKillConfirmed && exception.waitForExitSucceeded)
                    {
                        DeletePackage(packagePath);
                        throw;
                    }
                    throw new PsdHierarchyProcessTerminationException(
                        "Process tree termination was not confirmed; request package retained at " + packagePath + ".");
                }
                catch (OperationCanceledException)
                {
                    // No confirmed process exit means the package remains owned
                    // by the still-running/unknown child and must not be deleted.
                    throw;
                }
                catch (Exception exception)
                {
                    return Failed("Unable to run Codex hierarchy planner: " + exception.Message, packagePath, null);
                }
                finally
                {
                    operationLock.Dispose();
                    ReleaseLockFile(lockPath);
                }
            }
        }

        private static readonly string PlanSchema =
            "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"schemaVersion\",\"sourcePsdGuid\",\"sourceFingerprint\",\"contentFingerprint\",\"structureFingerprint\",\"geometryFingerprint\",\"groups\",\"renames\"]," +
            "\"properties\":{\"schemaVersion\":{\"type\":\"integer\",\"const\":1},\"sourcePsdGuid\":{\"type\":\"string\"},\"sourceFingerprint\":{\"type\":\"string\"},\"contentFingerprint\":{\"type\":\"string\"},\"structureFingerprint\":{\"type\":\"string\"},\"geometryFingerprint\":{\"type\":\"string\"}," +
            "\"groups\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"key\",\"parentKey\",\"memberStableIds\",\"displayName\",\"evidence\",\"confidence\"],\"properties\":{\"key\":{\"type\":\"string\"},\"parentKey\":{\"type\":\"string\"},\"memberStableIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}},\"displayName\":{\"type\":\"string\"},\"evidence\":{\"type\":\"string\"},\"confidence\":{\"type\":\"number\",\"minimum\":0,\"maximum\":1}}}}," +
            "\"renames\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"stableId\",\"name\",\"evidence\",\"confidence\"],\"properties\":{\"stableId\":{\"type\":\"string\"},\"name\":{\"type\":\"string\"},\"evidence\":{\"type\":\"string\"},\"confidence\":{\"type\":\"number\",\"minimum\":0,\"maximum\":1}}}}}}";

        private static string BuildPrompt(string targetPrefabPath, string requestPath, string focusPath)
        {
            return "You are a read-only PSD hierarchy planner. You have no permission to write Unity Assets, Prefabs, Profiles, materials, or project files. " +
                   "Read the bounded request JSON at " + requestPath + " and the scope/ancestor graph at " + focusPath + ". " +
                   "Modify only IDs and existing group keys listed as modifiable. Return only a plan matching plan.schema.json. " +
                   "Every modifiable ID in focus.json requires an explicit decision: either include it in an allowed group or add a rename. " +
                   "If it should remain ungrouped, add an identity rename whose name exactly equals that node's originalName in request.json; never return empty groups and renames while modifiable IDs exist. " +
                   "The target shown for evidence only is '" + SanitizePromptValue(targetPrefabPath) + "'. " +
                   "Do not propose commands, code, material edits, deletions, or any field outside the schema.";
        }

        private static string SanitizePromptValue(string value)
        {
            var result = new StringBuilder();
            foreach (char character in value ?? string.Empty)
            {
                if (!char.IsControl(character) && character != '\'') result.Append(character);
            }
            return result.ToString();
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
                offlinePackageAvailable = HasCompleteOfflinePackage(packagePath)
            };
        }

        private static PsdHierarchyAiRunResult OwnershipRejected(string error, string packagePath)
        {
            return new PsdHierarchyAiRunResult
            {
                succeeded = false,
                error = error,
                requestPackagePath = packagePath,
                offlinePackageAvailable = false
            };
        }

        private static string SerializeFocus(PsdHierarchyAiRunRequest request)
        {
            return JsonConvert.SerializeObject(new
            {
                modifiableStableIds = request.modifiableStableIds ?? new List<string>(),
                contextStableIds = request.contextStableIds ?? new List<string>(),
                modifiableGroupKeys = request.modifiableGroupKeys ?? new List<string>(),
                scopeOwnedGroupKeys = request.scopeOwnedGroupKeys ?? new List<string>(),
                hybridGroupKeys = request.hybridGroupKeys ?? new List<string>(),
                readonlyNeighborGroupKeys = request.readonlyNeighborGroupKeys ?? new List<string>(),
                structuralDependentGroupKeys = request.structuralDependentGroupKeys ?? new List<string>(),
                existingGroupKeys = request.existingGroupKeys ?? new List<string>(),
                baselineGroups = request.baselineGroups ?? new List<PsdHierarchyPlanGroup>()
            }, Formatting.None);
        }

        private static void ReleaseLockFile(string lockPath)
        {
            try
            {
                if (File.Exists(lockPath)) File.Delete(lockPath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                // A live foreign lock is never deleted. Our own lock is disposed
                // before this best-effort filesystem cleanup.
            }
        }

        private static bool HasCompleteOfflinePackage(string packagePath)
        {
            return Directory.Exists(packagePath) &&
                   File.Exists(Path.Combine(packagePath, "request.json")) &&
                   File.Exists(Path.Combine(packagePath, "focus.json")) &&
                   File.Exists(Path.Combine(packagePath, "plan.schema.json")) &&
                   File.Exists(Path.Combine(packagePath, "prompt.txt"));
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

            ProcessStartInfo startInfo = CreateStartInfo(invocation);

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

                Task<string> stdout = null;
                Task<string> stderr = null;
                try
                {
                    stdout = PsdHierarchyBoundedTextReader.ReadAsync(
                        process.StandardOutput, PsdHierarchyContractLimits.MaxJsonCharacters, linkedSource.Token);
                    stderr = PsdHierarchyBoundedTextReader.ReadAsync(
                        process.StandardError, PsdHierarchyContractLimits.MaxJsonCharacters, linkedSource.Token);
                    await process.StandardInput.WriteAsync(invocation.standardInput ?? string.Empty);
                    process.StandardInput.Close();

                    using (linkedSource.Token.Register(() => completion.TrySetCanceled()))
                    {
                        try
                        {
                            await PsdHierarchyProcessOutputMonitor.WaitAsync(completion.Task, stdout, stderr);
                            int exitCode = await completion.Task;
                            return new PsdHierarchyProcessResult
                            {
                                exitCode = exitCode,
                                standardOutput = await stdout,
                                standardError = await stderr
                            };
                        }
                        catch (PsdHierarchyOutputLimitException exception)
                        {
                            linkedSource.Cancel();
                            ProcessTerminationResult termination = TerminateProcessTreeAndWait(process);
                            if (termination.waitForExitSucceeded) await ObserveStreams(stdout, stderr);
                            return new PsdHierarchyProcessResult
                            {
                                outputLimitExceeded = true,
                                wasKilled = termination.killRequested,
                                processTreeKillConfirmed = termination.processTreeKillConfirmed,
                                waitForExitSucceeded = termination.waitForExitSucceeded,
                                error = exception.Message
                            };
                        }
                        catch (TaskCanceledException)
                        {
                            ProcessTerminationResult termination = TerminateProcessTreeAndWait(process);
                            if (termination.waitForExitSucceeded) await ObserveStreams(stdout, stderr);
                            if (cancellationToken.IsCancellationRequested)
                            {
                                throw new PsdHierarchyProcessCancelledException(
                                    "Codex process cancelled after termination request.",
                                    termination.processTreeKillConfirmed,
                                    termination.waitForExitSucceeded,
                                    cancellationToken);
                            }

                            return new PsdHierarchyProcessResult
                            {
                                timedOut = true,
                                wasKilled = termination.killRequested,
                                processTreeKillConfirmed = termination.processTreeKillConfirmed,
                                waitForExitSucceeded = termination.waitForExitSucceeded,
                                error = "Process timeout."
                            };
                        }
                    }
                }
                catch (PsdHierarchyProcessCancelledException)
                {
                    throw;
                }
                catch
                {
                    ProcessTerminationResult termination = TerminateProcessTreeAndWait(process);
                    if (termination.waitForExitSucceeded && stdout != null && stderr != null)
                    {
                        await ObserveStreams(stdout, stderr);
                    }
                    throw;
                }
            }
        }

        /// <summary>
        /// Codex reads its prompt from stdin as UTF-8. ProcessStartInfo otherwise
        /// uses the Windows active code page, which corrupts Chinese PSD names
        /// and can make Codex reject the input before planning starts.
        /// </summary>
        internal static ProcessStartInfo CreateStartInfo(PsdHierarchyProcessInvocation invocation)
        {
            if (invocation == null) throw new ArgumentNullException("invocation");
            return new ProcessStartInfo
            {
                FileName = invocation.executable,
                Arguments = JoinArguments(invocation.arguments),
                WorkingDirectory = invocation.workingDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false),
                CreateNoWindow = true
            };
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

        private static ProcessTerminationResult TerminateProcessTreeAndWait(Process process)
        {
            var result = new ProcessTerminationResult();
            try
            {
                if (process.HasExited)
                {
                    result.waitForExitSucceeded = true;
                    return result;
                }

                result.killRequested = true;
                if (PsdHierarchyProcessTreeStrategy.Select(Environment.OSVersion.Platform) ==
                    PsdHierarchyProcessTreeTerminationStrategy.WindowsTaskkill)
                {
                    try
                    {
                        using (var taskKill = Process.Start(new ProcessStartInfo
                        {
                            FileName = "taskkill.exe",
                            Arguments = "/PID " + process.Id + " /T /F",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }))
                        {
                            bool taskkillWaited = taskKill != null && taskKill.WaitForExit(5000);
                            int taskkillExitCode = taskkillWaited ? taskKill.ExitCode : -1;
                            result.processTreeKillConfirmed = PsdHierarchyTaskkillConfirmation.IsConfirmed(
                                taskkillWaited, taskkillExitCode);
                        }
                    }
                    catch (Exception)
                    {
                        // A failed taskkill attempt is never proof that the process tree exited.
                    }

                    if (!result.processTreeKillConfirmed)
                        TryUnconfirmedKill(process);
                }
                else
                {
                    TryUnconfirmedKill(process);
                }

                result.waitForExitSucceeded = process.WaitForExit(5000);
            }
            catch (Exception)
            {
                // Process exited between the observation and kill attempt.
                try { result.waitForExitSucceeded = process.HasExited || process.WaitForExit(5000); }
                catch (InvalidOperationException) { result.waitForExitSucceeded = true; }
            }
            return result;
        }

        private static void TryUnconfirmedKill(Process process)
        {
            try
            {
                var treeKill = typeof(Process).GetMethod("Kill", new[] { typeof(bool) });
                if (treeKill != null)
                    treeKill.Invoke(process, new object[] { true });
                else
                    process.Kill();
            }
            catch (Exception)
            {
                // Best effort only. This path never confirms full process-tree termination.
            }
        }

        private static async Task ObserveStreams(params Task<string>[] streams)
        {
            foreach (Task<string> stream in streams)
            {
                try { await stream; }
                catch (Exception) { /* Quota/cancellation was already recorded. */ }
            }
        }

        private struct ProcessTerminationResult
        {
            public bool killRequested;
            public bool processTreeKillConfirmed;
            public bool waitForExitSucceeded;
        }
    }

    public sealed class PsdHierarchyOutputLimitException : InvalidOperationException
    {
        public PsdHierarchyOutputLimitException(string message) : base(message) { }
    }

    /// <summary>Chunked reader used for stdout, stderr and imported JSON.</summary>
    public static class PsdHierarchyBoundedTextReader
    {
        public static string Read(TextReader reader, int maximumCharacters)
        {
            if (reader == null) throw new ArgumentNullException("reader");
            if (maximumCharacters < 0) throw new ArgumentOutOfRangeException("maximumCharacters");
            var value = new StringBuilder(Math.Min(maximumCharacters, 4096));
            var buffer = new char[4096];
            while (true)
            {
                int read = reader.Read(buffer, 0, buffer.Length);
                if (read == 0) return value.ToString();
                EnsureCapacity(value.Length, read, maximumCharacters);
                value.Append(buffer, 0, read);
            }
        }

        public static async Task<string> ReadAsync(
            TextReader reader,
            int maximumCharacters,
            CancellationToken cancellationToken)
        {
            if (reader == null) throw new ArgumentNullException("reader");
            if (maximumCharacters < 0) throw new ArgumentOutOfRangeException("maximumCharacters");
            var value = new StringBuilder(Math.Min(maximumCharacters, 4096));
            var buffer = new char[4096];
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = await reader.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0) return value.ToString();
                EnsureCapacity(value.Length, read, maximumCharacters);
                value.Append(buffer, 0, read);
            }
        }

        private static void EnsureCapacity(int current, int incoming, int maximumCharacters)
        {
            if (current > maximumCharacters - incoming)
                throw new PsdHierarchyOutputLimitException("Text output exceeds the character quota.");
        }
    }

    /// <summary>
    /// Completes as soon as the process exits or either stream faults. It never
    /// waits for both streams before surfacing one stream's quota violation.
    /// </summary>
    public static class PsdHierarchyProcessOutputMonitor
    {
        public static async Task WaitAsync(Task<int> exit, Task<string> stdout, Task<string> stderr)
        {
            if (exit == null || stdout == null || stderr == null) throw new ArgumentNullException("tasks");
            var remaining = new List<Task> { stdout, stderr };
            while (remaining.Count > 0)
            {
                var candidates = new List<Task>(remaining) { exit };
                Task completed = await Task.WhenAny(candidates);
                if (completed == exit)
                {
                    await exit;
                    return;
                }

                await completed; // Immediate quota/cancellation propagation.
                remaining.Remove(completed);
            }

            await exit;
        }
    }

    public enum PsdHierarchyProcessTreeTerminationStrategy
    {
        WindowsTaskkill,
        NonWindowsUnconfirmedKill
    }

    /// <summary>Selects the only platform path whose tree termination can be confirmed.</summary>
    public static class PsdHierarchyProcessTreeStrategy
    {
        public static PsdHierarchyProcessTreeTerminationStrategy Select(PlatformID platform)
        {
            return platform == PlatformID.Win32NT
                ? PsdHierarchyProcessTreeTerminationStrategy.WindowsTaskkill
                : PsdHierarchyProcessTreeTerminationStrategy.NonWindowsUnconfirmedKill;
        }
    }

    /// <summary>
    /// taskkill only confirms a Windows tree termination request when the helper
    /// itself completed and returned success. Parent exit alone is insufficient.
    /// </summary>
    public static class PsdHierarchyTaskkillConfirmation
    {
        public static bool IsConfirmed(bool waitForExitSucceeded, int exitCode)
        {
            return waitForExitSucceeded && exitCode == 0;
        }
    }
}
