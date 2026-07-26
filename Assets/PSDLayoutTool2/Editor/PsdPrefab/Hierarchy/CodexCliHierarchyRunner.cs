namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

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
        private readonly Func<IEnumerable<string>> mcpServerNamesResolver;
        private readonly PsdHierarchyAiProvider provider;
        private readonly PsdHierarchyAiConnectionSnapshot connection;
        private readonly IPsdAiSecretStore secretStore;
        private readonly string projectIdentity;

        public CodexCliHierarchyRunner()
            : this(new SystemHierarchyProcessAdapter(),
                () => ResolveDefaultExecutable(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
                Path.Combine("Temp", "PSDLayoutTool2", "Hierarchy"),
                path => Directory.CreateDirectory(path),
                () => ResolveConfiguredMcpServerNames(ResolveDefaultConfigPath()),
                PsdHierarchyAiProvider.Codex,
                new PsdHierarchyAiConnectionSnapshot(PsdHierarchyAiConnectionMode.Default, string.Empty),
                null,
                string.Empty)
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
            : this(processAdapter, executableResolver, packageRoot,
                path => Directory.CreateDirectory(path), () => Array.Empty<string>(),
                PsdHierarchyAiProvider.Codex,
                new PsdHierarchyAiConnectionSnapshot(PsdHierarchyAiConnectionMode.Default, string.Empty),
                null,
                string.Empty)
        {
        }

        public CodexCliHierarchyRunner(
            IHierarchyProcessAdapter processAdapter,
            Func<string> executableResolver,
            string packageRoot,
            Action<string> packageDirectoryCreator)
            : this(processAdapter, executableResolver, packageRoot,
                packageDirectoryCreator, () => Array.Empty<string>(),
                PsdHierarchyAiProvider.Codex,
                new PsdHierarchyAiConnectionSnapshot(PsdHierarchyAiConnectionMode.Default, string.Empty),
                null,
                string.Empty)
        {
        }

        internal CodexCliHierarchyRunner(
            IHierarchyProcessAdapter processAdapter,
            Func<string> executableResolver,
            string packageRoot,
            PsdHierarchyAiConnectionSnapshot connection,
            IPsdAiSecretStore secretStore,
            string projectIdentity)
            : this(processAdapter, executableResolver, packageRoot,
                path => Directory.CreateDirectory(path), () => Array.Empty<string>(),
                PsdHierarchyAiProvider.Codex, connection, secretStore, projectIdentity)
        {
        }

        internal CodexCliHierarchyRunner(
            IHierarchyProcessAdapter processAdapter,
            Func<string> executableResolver,
            string packageRoot,
            Action<string> packageDirectoryCreator,
            Func<IEnumerable<string>> mcpServerNamesResolver)
            : this(processAdapter, executableResolver, packageRoot, packageDirectoryCreator,
                mcpServerNamesResolver, PsdHierarchyAiProvider.Codex,
                new PsdHierarchyAiConnectionSnapshot(PsdHierarchyAiConnectionMode.Default, string.Empty),
                null, string.Empty)
        {
        }

        internal CodexCliHierarchyRunner(
            IHierarchyProcessAdapter processAdapter,
            Func<string> executableResolver,
            string packageRoot,
            Action<string> packageDirectoryCreator,
            Func<IEnumerable<string>> mcpServerNamesResolver,
            PsdHierarchyAiProvider provider,
            PsdHierarchyAiConnectionSnapshot connection,
            IPsdAiSecretStore secretStore,
            string projectIdentity)
        {
            this.processAdapter = processAdapter ?? throw new ArgumentNullException("processAdapter");
            this.executableResolver = executableResolver ?? throw new ArgumentNullException("executableResolver");
            this.packageRoot = string.IsNullOrWhiteSpace(packageRoot)
                ? throw new ArgumentException("Package root is required.", "packageRoot")
                : packageRoot;
            this.packageDirectoryCreator = packageDirectoryCreator ?? throw new ArgumentNullException("packageDirectoryCreator");
            this.mcpServerNamesResolver = mcpServerNamesResolver ?? throw new ArgumentNullException("mcpServerNamesResolver");
            if (provider != PsdHierarchyAiProvider.Codex && provider != PsdHierarchyAiProvider.Claude)
                throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported hierarchy AI provider.");
            this.provider = provider;
            this.connection = connection;
            this.secretStore = secretStore;
            this.projectIdentity = projectIdentity ?? string.Empty;
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
                string credential = null;
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

                    Dictionary<string, string> childEnvironment = ResolveChildEnvironment(out credential);

                    var invocation = new PsdHierarchyProcessInvocation
                    {
                        executable = executableResolver(),
                        workingDirectory = packagePath,
                        standardInput = prompt,
                        OutputPath = outputPath,
                        useShellExecute = false,
                        providerName = ProviderName,
                        childEnvironment = childEnvironment,
                        arguments = BuildInvocationArguments(schemaPath, outputPath)
                    };

                    PsdHierarchyProcessResult processResult = await processAdapter
                        .RunAsync(invocation, NormalizeTimeout(runRequest.timeout), cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (processResult == null)
                    {
                        return Failed(ProviderName + " process stage returned no result.", packagePath, null, credential);
                    }

                    if (processResult.outputLimitExceeded)
                    {
                        return Failed(ProviderName + " output stage exceeded the bounded output quota.", packagePath, processResult, credential);
                    }

                    if (processResult.timedOut)
                    {
                        return Failed(ProviderName + " process stage timed out.", packagePath, processResult, credential);
                    }

                    if (processResult.exitCode != 0)
                    {
                        string detail = string.IsNullOrWhiteSpace(processResult.standardError)
                            ? processResult.error
                            : processResult.standardError;
                        if (ContainsUsageLimit(detail))
                        {
                            detail = ProviderName + " 账号已达到使用额度上限。请在额度恢复后，或更换可用账号/套餐后点击“重新分析”。";
                        }
                        return Failed(ProviderName + " process stage failed (exit " + processResult.exitCode + "): " + detail,
                            packagePath, processResult, credential);
                    }

                    string json;
                    if (provider == PsdHierarchyAiProvider.Codex && !File.Exists(outputPath))
                    {
                        return Failed("Codex output stage is missing the hierarchy plan.", packagePath, processResult, credential);
                    }

                    if (provider == PsdHierarchyAiProvider.Codex &&
                        new FileInfo(outputPath).Length > PsdHierarchyContractLimits.MaxJsonUtf8Bytes)
                    {
                        return Failed(ProviderName + " output stage exceeds the UTF-8 byte limit.", packagePath, processResult, credential);
                    }

                    try
                    {
                        if (provider == PsdHierarchyAiProvider.Codex)
                        {
                            using (var outputReader = new StreamReader(outputPath, Encoding.UTF8, true, 4096))
                            {
                                json = await PsdHierarchyBoundedTextReader.ReadAsync(
                                    outputReader, PsdHierarchyContractLimits.MaxJsonCharacters, cancellationToken);
                            }
                        }
                        else json = ExtractClaudeStructuredOutput(processResult.standardOutput);
                        json = BindRequestIdentityJson(json, runRequest.request);
                        PsdHierarchyPlan plan = PsdHierarchyPlanJson.Parse(json);
                        BindRequestIdentity(plan, runRequest.request);
                        CompleteMissingFocusedDecisions(plan, runRequest);
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
                            standardOutput = SanitizeProviderDiagnostic(processResult.standardOutput, credential),
                            standardError = SanitizeProviderDiagnostic(processResult.standardError, credential),
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
                        return Failed(ProviderName + " validation stage rejected the hierarchy plan: " + exception.Message,
                            packagePath, processResult, credential);
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
                    return Failed("Unable to run " + ProviderName + " hierarchy planner: " + exception.Message,
                        packagePath, null, credential);
                }
                finally
                {
                    operationLock.Dispose();
                    ReleaseLockFile(lockPath);
                }
            }
        }

        internal static readonly string PlanSchema =
            "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"schemaVersion\",\"sourcePsdGuid\",\"sourceFingerprint\",\"contentFingerprint\",\"structureFingerprint\",\"geometryFingerprint\",\"groups\",\"renames\"]," +
            "\"properties\":{\"schemaVersion\":{\"type\":\"integer\",\"const\":1},\"sourcePsdGuid\":{\"type\":\"string\"},\"sourceFingerprint\":{\"type\":\"string\"},\"contentFingerprint\":{\"type\":\"string\"},\"structureFingerprint\":{\"type\":\"string\"},\"geometryFingerprint\":{\"type\":\"string\"}," +
            "\"groups\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"key\",\"parentKey\",\"memberStableIds\",\"displayName\",\"evidence\",\"confidence\"],\"properties\":{\"key\":{\"type\":\"string\"},\"parentKey\":{\"type\":\"string\"},\"memberStableIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}},\"displayName\":{\"type\":\"string\"},\"evidence\":{\"type\":\"string\"},\"confidence\":{\"type\":\"number\",\"minimum\":0,\"maximum\":1}}}}," +
            "\"renames\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"stableId\",\"name\",\"evidence\",\"confidence\"],\"properties\":{\"stableId\":{\"type\":\"string\"},\"name\":{\"type\":\"string\"},\"evidence\":{\"type\":\"string\"},\"confidence\":{\"type\":\"number\",\"minimum\":0,\"maximum\":1}}}}}}";

        private static string BuildPrompt(string targetPrefabPath, string requestPath, string focusPath)
        {
            return "You are a read-only PSD hierarchy planner. You have no permission to write Unity Assets, Prefabs, Profiles, materials, or project files. " +
                   "Read the bounded request JSON at " + requestPath + " and the scope/ancestor graph at " + focusPath + ". " +
                   "Copy sourcePsdGuid, sourceFingerprint, contentFingerprint, structureFingerprint, and geometryFingerprint exactly from request.json; they are immutable request identity, not planning output. " +
                   "Modify only IDs and existing group keys listed as modifiable. Return only a plan matching plan.schema.json. " +
                   "Never return, expand, reparent, or add children under keys listed in immutableGroupKeys. " +
                   "Keys in requiredAncestorGroupKeys are fixed ancestors: do not return or remove them, but modifiable child groups may remain under them. " +
                   "You may create new group keys when the modifiable IDs need a new semantic container; use a unique ASCII key and include only modifiable IDs in that new group. " +
                   "Infer semantic maintenance boundaries before visual layer categories: a group should represent a candidate independent Prefab only when its members are likely to be reused, repeated, independently scripted, independently animated, or independently maintained. " +
                   "For a candidate Prefab, keep its related background, text, icons, and interaction layers together under that semantic container; use Background, Content, Interaction, or Decoration only as child groups inside it when useful. " +
                   "Do not create a top-level group merely because layers share a rendering type such as text or background. If no independent maintenance boundary is supported by the request evidence, preserve a simple semantic group or leave the nodes ungrouped. " +
                   "Prefer contiguous sibling ranges. A semantic container may include non-adjacent siblings only when every crossed sibling is geometrically disjoint from the moved later members; Unity validates this rule. Never reorder overlapping visuals. " +
                   "Every modifiable ID in focus.json requires an explicit decision: either include it in an allowed group or add a rename. " +
                   "The optional instruction field in focus.json is user guidance only. Follow it only when it remains inside every immutable, scope, ordering, and output-schema constraint. " +
                   "If it should remain ungrouped, add an identity rename whose name exactly equals that node's originalName in request.json; never return empty groups and renames while modifiable IDs exist. " +
                   "The target shown for evidence only is '" + SanitizePromptValue(targetPrefabPath) + "'. " +
                   "Do not propose commands, code, material edits, deletions, or any field outside the schema.";
        }

        private static void BindRequestIdentity(PsdHierarchyPlan plan, PsdHierarchyRequest request)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (request == null) throw new ArgumentNullException(nameof(request));
            plan.sourcePsdGuid = request.sourcePsdGuid;
            plan.sourceFingerprint = request.sourceFingerprint;
            plan.contentFingerprint = request.contentFingerprint;
            plan.structureFingerprint = request.structureFingerprint;
            plan.geometryFingerprint = request.geometryFingerprint;
        }

        private static string BindRequestIdentityJson(string json, PsdHierarchyRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException exception)
            {
                throw new PsdHierarchyPlanFormatException(
                    "Hierarchy plan JSON is invalid before identity binding: " + exception.Message, exception);
            }

            // These fields identify the trusted input package, not an AI decision.
            // Replace them before strict parsing so an omitted model echo cannot
            // reject an otherwise valid plan.
            root["sourcePsdGuid"] = request.sourcePsdGuid;
            root["sourceFingerprint"] = request.sourceFingerprint;
            root["contentFingerprint"] = request.contentFingerprint;
            root["structureFingerprint"] = request.structureFingerprint;
            root["geometryFingerprint"] = request.geometryFingerprint;
            return root.ToString(Formatting.None);
        }

        private static void CompleteMissingFocusedDecisions(
            PsdHierarchyPlan plan,
            PsdHierarchyAiRunRequest runRequest)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (runRequest == null || runRequest.request == null) return;

            var modifiableIds = new HashSet<string>(
                runRequest.modifiableStableIds ?? new List<string>(),
                StringComparer.Ordinal);
            if (modifiableIds.Count == 0) return;

            var decidedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (PsdHierarchyPlanGroup group in plan.groups ?? new List<PsdHierarchyPlanGroup>())
            {
                if (group == null) continue;
                foreach (string memberId in group.memberStableIds ?? new List<string>())
                {
                    if (modifiableIds.Contains(memberId)) decidedIds.Add(memberId);
                }
            }
            foreach (PsdHierarchyPlanRename rename in plan.renames ?? new List<PsdHierarchyPlanRename>())
            {
                if (rename != null && modifiableIds.Contains(rename.stableId))
                    decidedIds.Add(rename.stableId);
            }

            if (plan.renames == null) plan.renames = new List<PsdHierarchyPlanRename>();
            foreach (PsdHierarchyRequestNode node in runRequest.request.nodes ?? new List<PsdHierarchyRequestNode>())
            {
                if (node == null || !modifiableIds.Contains(node.stableId) || decidedIds.Contains(node.stableId))
                    continue;
                plan.renames.Add(new PsdHierarchyPlanRename
                {
                    stableId = node.stableId,
                    name = node.originalName ?? string.Empty,
                    evidence = "Planner omitted a focused decision; preserve the source name.",
                    confidence = 1d
                });
            }
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

        private PsdHierarchyAiRunResult Failed(
            string error,
            string packagePath,
            PsdHierarchyProcessResult processResult,
            string credential)
        {
            return new PsdHierarchyAiRunResult
            {
                succeeded = false,
                error = SanitizeProviderDiagnostic(error, credential),
                standardOutput = processResult != null ? SanitizeProviderDiagnostic(processResult.standardOutput, credential) : string.Empty,
                standardError = processResult != null ? SanitizeProviderDiagnostic(processResult.standardError, credential) : string.Empty,
                requestPackagePath = packagePath,
                offlinePackageAvailable = HasCompleteOfflinePackage(packagePath)
            };
        }

        private static bool ContainsUsageLimit(string value)
        {
            return !string.IsNullOrEmpty(value) &&
                   (value.IndexOf("usage limit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal static List<string> ResolveConfiguredMcpServerNames(string configPath)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath)) return new List<string>();
            var pattern = new Regex(
                "^\\s*\\[\\s*mcp_servers\\.(?:(?<bare>[A-Za-z0-9_-]+)|\\\"(?<quoted>[A-Za-z0-9_-]+)\\\")\\s*\\]\\s*(?:#.*)?$",
                RegexOptions.CultureInvariant);
            foreach (string line in File.ReadLines(configPath))
            {
                Match match = pattern.Match(line ?? string.Empty);
                if (!match.Success) continue;
                string name = match.Groups["bare"].Success
                    ? match.Groups["bare"].Value
                    : match.Groups["quoted"].Value;
                if (!string.IsNullOrEmpty(name)) result.Add(name);
            }
            var names = new List<string>(result);
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        private static string ResolveDefaultConfigPath()
        {
            string codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
            if (string.IsNullOrWhiteSpace(codexHome))
                codexHome = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
            return Path.Combine(codexHome, "config.toml");
        }

        private List<string> BuildInvocationArguments(
            string schemaPath,
            string outputPath)
        {
            if (provider == PsdHierarchyAiProvider.Claude)
            {
                return new List<string>
                {
                    "--print", "--output-format", "json", "--json-schema", PlanSchema,
                    "--no-session-persistence", "--tools", string.Empty
                };
            }

            var arguments = new List<string>
            {
                "exec", "--sandbox", "read-only", "--ephemeral", "--ignore-rules",
                "--disable", "plugins", "--disable", "hooks"
            };
            foreach (string name in mcpServerNamesResolver() ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(name) ||
                    !Regex.IsMatch(name, "^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant)) continue;
                arguments.Add("-c");
                arguments.Add("mcp_servers." + name + ".enabled=false");
            }
            arguments.Add("--output-schema");
            arguments.Add(schemaPath);
            arguments.Add("-o");
            arguments.Add(outputPath);
            arguments.Add("-");
            return arguments;
        }

        private string ProviderName => provider == PsdHierarchyAiProvider.Claude ? "Claude" : "Codex";

        private Dictionary<string, string> ResolveChildEnvironment(out string credential)
        {
            credential = null;
            var environment = new Dictionary<string, string>(StringComparer.Ordinal);
            if (connection.mode == PsdHierarchyAiConnectionMode.Default) return environment;
            if (connection.mode != PsdHierarchyAiConnectionMode.Custom)
                throw new InvalidOperationException("Unsupported hierarchy AI connection mode.");
            if (!PsdHierarchyAiConnectionSettings.TryValidateBaseUrl(connection.baseUrl, out string error))
                throw new InvalidOperationException(error);
            if (secretStore == null || !secretStore.TryRead(projectIdentity, provider, out credential) ||
                string.IsNullOrWhiteSpace(credential))
                throw new InvalidOperationException(ProviderName + " custom credential is unavailable.");

            if (provider == PsdHierarchyAiProvider.Claude)
            {
                environment["ANTHROPIC_BASE_URL"] = connection.baseUrl;
                environment["ANTHROPIC_AUTH_TOKEN"] = credential;
            }
            else
            {
                environment["OPENAI_BASE_URL"] = connection.baseUrl;
                environment["OPENAI_API_KEY"] = credential;
            }
            return environment;
        }

        private static string ExtractClaudeStructuredOutput(string envelope)
        {
            if (string.IsNullOrWhiteSpace(envelope))
                throw new PsdHierarchyPlanFormatException("Claude returned an empty JSON envelope.");
            JObject root;
            try { root = JObject.Parse(envelope); }
            catch (JsonException exception)
            {
                throw new PsdHierarchyPlanFormatException("Claude returned an invalid JSON envelope: " + exception.Message);
            }
            JToken structured = root["structured_output"];
            if (structured == null || structured.Type != JTokenType.Object)
                throw new PsdHierarchyPlanFormatException("Claude JSON envelope has no structured_output object.");
            return structured.ToString(Formatting.None);
        }

        private string SanitizeProviderDiagnostic(string value, string credential)
        {
            const int MaxDiagnosticCharacters = 4096;
            string sanitized = value ?? string.Empty;
            if (!string.IsNullOrEmpty(credential)) sanitized = sanitized.Replace(credential, "[REDACTED]");
            if (!string.IsNullOrWhiteSpace(connection.baseUrl))
                sanitized = sanitized.Replace(connection.baseUrl, "[REDACTED_URL]");
            sanitized = Regex.Replace(sanitized,
                "(?i)(Authorization\\s*:\\s*(?:Bearer\\s+)?)[^\\s,;]+", "$1[REDACTED]",
                RegexOptions.CultureInvariant);
            sanitized = Regex.Replace(sanitized,
                "(?i)(Bearer\\s+)[A-Za-z0-9._~+/-]+=*", "$1[REDACTED]",
                RegexOptions.CultureInvariant);
            sanitized = Regex.Replace(sanitized,
                "(?i)(https?://)[^/@\\s]+@", "$1[REDACTED]@",
                RegexOptions.CultureInvariant);
            sanitized = Regex.Replace(sanitized,
                "(?i)(https?://[^\\s?#]+)\\?[^\\s#]+", "$1?[REDACTED_QUERY]",
                RegexOptions.CultureInvariant);
            sanitized = Regex.Replace(sanitized,
                "(?i)([?&](?:key|api[_-]?key|token|access[_-]?token|auth|authorization)=)[^&#\\s]+",
                "$1[REDACTED]", RegexOptions.CultureInvariant);
            if (sanitized.Length > MaxDiagnosticCharacters)
            {
                const string suffix = "...[truncated]";
                sanitized = sanitized.Substring(0, MaxDiagnosticCharacters - suffix.Length) + suffix;
            }
            return sanitized;
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
                instruction = request.instruction ?? string.Empty,
                modifiableStableIds = request.modifiableStableIds ?? new List<string>(),
                contextStableIds = request.contextStableIds ?? new List<string>(),
                modifiableGroupKeys = request.modifiableGroupKeys ?? new List<string>(),
                scopeOwnedGroupKeys = request.scopeOwnedGroupKeys ?? new List<string>(),
                hybridGroupKeys = request.hybridGroupKeys ?? new List<string>(),
                readonlyNeighborGroupKeys = request.readonlyNeighborGroupKeys ?? new List<string>(),
                structuralDependentGroupKeys = request.structuralDependentGroupKeys ?? new List<string>(),
                immutableGroupKeys = request.immutableGroupKeys ?? new List<string>(),
                requiredAncestorGroupKeys = request.requiredAncestorGroupKeys ?? new List<string>(),
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
                    throw new InvalidOperationException("Hierarchy AI process did not start.");
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
                                    "Hierarchy AI process cancelled after termination request.",
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
        /// Hierarchy AI CLIs read their prompt from stdin as UTF-8. ProcessStartInfo otherwise
        /// uses the Windows active code page, which corrupts Chinese PSD names
        /// and can make the provider reject the input before planning starts.
        /// </summary>
        internal static ProcessStartInfo CreateStartInfo(PsdHierarchyProcessInvocation invocation)
        {
            if (invocation == null) throw new ArgumentNullException("invocation");
            var startInfo = new ProcessStartInfo
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
            foreach (KeyValuePair<string, string> pair in invocation.childEnvironment ??
                new Dictionary<string, string>())
            {
                startInfo.EnvironmentVariables[pair.Key] = pair.Value ?? string.Empty;
            }
            return startInfo;
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
