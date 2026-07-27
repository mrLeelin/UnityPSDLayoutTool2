namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.Networking;

    internal readonly struct PsdHierarchyChatConnection
    {
        internal PsdHierarchyChatConnection(
            PsdHierarchyAiProvider provider,
            PsdHierarchyAiConnectionMode connectionMode,
            string cliExecutablePath,
            string endpoint,
            string model,
            string apiKey)
        {
            this.provider = provider;
            this.connectionMode = connectionMode;
            this.cliExecutablePath = cliExecutablePath ?? string.Empty;
            this.endpoint = endpoint ?? string.Empty;
            this.model = model ?? string.Empty;
            this.apiKey = apiKey ?? string.Empty;
        }

        internal readonly PsdHierarchyAiProvider provider;
        internal readonly PsdHierarchyAiConnectionMode connectionMode;
        internal readonly string cliExecutablePath;
        internal readonly string endpoint;
        internal readonly string model;
        internal readonly string apiKey;

        internal bool TryValidate(out string error)
        {
            if (provider != PsdHierarchyAiProvider.Claude && provider != PsdHierarchyAiProvider.Codex)
            {
                error = "选择的 AI 不受支持。";
                return false;
            }

            if (connectionMode == PsdHierarchyAiConnectionMode.LocalCli)
            {
                if (string.IsNullOrWhiteSpace(cliExecutablePath) || !File.Exists(cliExecutablePath))
                {
                    error = "所选 AI CLI 已不可用，请在全局配置中重新选择。";
                    return false;
                }

                error = string.Empty;
                return true;
            }

            if (connectionMode != PsdHierarchyAiConnectionMode.CustomApi)
            {
                error = "选择的 AI 连接方式不受支持。";
                return false;
            }

            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri parsedEndpoint) ||
                (parsedEndpoint.Scheme != Uri.UriSchemeHttp && parsedEndpoint.Scheme != Uri.UriSchemeHttps))
            {
                error = "请填写有效的自定义 API 地址。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(model))
            {
                error = "请填写模型名称。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                error = "请在全局配置中填写 API Key。";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    internal readonly struct PsdHierarchyChatMessage
    {
        internal PsdHierarchyChatMessage(string role, string content)
        {
            this.role = role ?? string.Empty;
            this.content = content ?? string.Empty;
        }

        internal readonly string role;
        internal readonly string content;
    }

    internal sealed class PsdHierarchyChatContext
    {
        internal PsdHierarchyChatContext(
            string projectRoot,
            string sourcePsdAssetPath,
            string targetPrefabAssetPath,
            string skillFullPath,
            string skillContent,
            string prefabContent)
        {
            this.projectRoot = projectRoot ?? string.Empty;
            this.sourcePsdAssetPath = sourcePsdAssetPath ?? string.Empty;
            this.targetPrefabAssetPath = targetPrefabAssetPath ?? string.Empty;
            this.skillFullPath = skillFullPath ?? string.Empty;
            this.skillContent = skillContent ?? string.Empty;
            this.prefabContent = prefabContent ?? string.Empty;
        }

        internal readonly string projectRoot;
        internal readonly string sourcePsdAssetPath;
        internal readonly string targetPrefabAssetPath;
        internal readonly string skillFullPath;
        internal readonly string skillContent;
        internal readonly string prefabContent;

        internal string BuildInstructions()
        {
            var builder = new StringBuilder();
            builder.AppendLine("You are assisting with a Unity Prefab hierarchy cleanup from inside the Unity Editor.");
            builder.AppendLine("The user supplied the exact cleanup skill and target Prefab below.");
            builder.AppendLine("Inspect first and provide a complete, reviewable plan. Do not claim to have edited a local asset: this chat has no local file-write capability.");
            builder.AppendLine("The only allowed output mode is in_place: output.assetPath must exactly equal the supplied target Prefab path.");
            builder.AppendLine("The target is already confirmed for in-place cleanup. Do not ask the user to choose an output mode or whether to create a new Prefab.");
            builder.AppendLine("Do not propose, create, copy, or offer a .cleaned.prefab or any other replacement Prefab. Any later approved cleanup must target the supplied Prefab in place while preserving visual layout, generated assets, bindings, and unrelated components.");
            builder.AppendLine("This chat request does not authorize component extraction. Do not propose or include componentExtractions, stateComponentExtractions, variantComponentExtractions, statefulComponentExtractions, Prefab/Common, or any nested Prefab.");
            builder.AppendLine("Return an auditable analysis summary, not private chain-of-thought. In Simplified Chinese, use exactly these sections: 分析摘要, 分组依据, 风险与保留项, 原地整理方案, 验证清单. Ground every claim in observable hierarchy, geometry, component, sibling-order, or repeated-structure evidence.");
            builder.AppendLine("Source PSD: " + sourcePsdAssetPath);
            builder.AppendLine("Target Prefab: " + targetPrefabAssetPath);
            builder.AppendLine();
            builder.AppendLine("===== BEGIN prefab-hierarchy-cleanup/SKILL.md =====");
            builder.AppendLine(skillContent);
            builder.AppendLine("===== END prefab-hierarchy-cleanup/SKILL.md =====");
            builder.AppendLine();
            builder.AppendLine("===== BEGIN TARGET PREFAB YAML =====");
            builder.AppendLine(prefabContent);
            builder.AppendLine("===== END TARGET PREFAB YAML =====");
            return builder.ToString();
        }
    }

    internal static class PsdHierarchyChatContextBuilder
    {
        internal const string DefaultSkillRelativePath =
            "Assets/UnityPSDLayoutTool2/.agents/skills/prefab-hierarchy-cleanup/SKILL.md";

        internal const long MaxContextFileBytes = 512 * 1024;

        internal static bool TryCreate(
            string sourcePsdAssetPath,
            string targetPrefabAssetPath,
            out PsdHierarchyChatContext context,
            out string error)
        {
            context = null;
            DirectoryInfo projectDirectory = Directory.GetParent(Application.dataPath);
            if (projectDirectory == null)
            {
                error = "无法解析 Unity 项目根目录。";
                return false;
            }

            string projectRoot = projectDirectory.FullName;
            string prefabAssetPath = NormalizeAssetPath(targetPrefabAssetPath);
            string prefabFullPath = ToFullPath(projectRoot, prefabAssetPath);
            if (!TryReadContextFile(prefabFullPath, "目标 Prefab", out string prefabContent, out error))
            {
                return false;
            }

            string skillFullPath = ToFullPath(projectRoot, DefaultSkillRelativePath);
            if (!TryReadContextFile(skillFullPath, "AI 整理技能", out string skillContent, out error))
            {
                return false;
            }

            context = new PsdHierarchyChatContext(
                projectRoot,
                NormalizeAssetPath(sourcePsdAssetPath),
                prefabAssetPath,
                skillFullPath,
                skillContent,
                prefabContent);
            error = string.Empty;
            return true;
        }

        private static bool TryReadContextFile(string fullPath, string label, out string content, out string error)
        {
            content = string.Empty;
            if (!File.Exists(fullPath))
            {
                error = label + "不存在：" + fullPath;
                return false;
            }

            var info = new FileInfo(fullPath);
            if (info.Length > MaxContextFileBytes)
            {
                error = label + "过大，不能直接发送给 AI（上限 " + MaxContextFileBytes + " 字节）：" + fullPath;
                return false;
            }

            try
            {
                content = File.ReadAllText(fullPath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(content))
                {
                    error = label + "为空：" + fullPath;
                    return false;
                }
            }
            catch (Exception exception)
            {
                error = "读取" + label + "失败：" + exception.Message;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static string ToFullPath(string projectRoot, string path)
        {
            string candidate = path ?? string.Empty;
            if (!Path.IsPathRooted(candidate))
            {
                candidate = Path.Combine(projectRoot, candidate.Replace('/', Path.DirectorySeparatorChar));
            }

            return Path.GetFullPath(candidate);
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }

    internal sealed class PsdHierarchyChatHttpRequest
    {
        private readonly Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal PsdHierarchyChatHttpRequest(string url, string body)
        {
            this.url = url ?? string.Empty;
            this.body = body ?? string.Empty;
        }

        internal readonly string url;
        internal readonly string body;

        internal void SetHeader(string name, string value)
        {
            headers[name] = value ?? string.Empty;
        }

        internal string GetHeader(string name)
        {
            return headers.TryGetValue(name, out string value) ? value : string.Empty;
        }

        internal IEnumerable<KeyValuePair<string, string>> Headers => headers;
    }

    internal readonly struct PsdHierarchyChatHttpResponse
    {
        internal PsdHierarchyChatHttpResponse(bool success, long statusCode, string body, string error)
        {
            this.success = success;
            this.statusCode = statusCode;
            this.body = body ?? string.Empty;
            this.error = error ?? string.Empty;
        }

        internal readonly bool success;
        internal readonly long statusCode;
        internal readonly string body;
        internal readonly string error;
    }

    internal readonly struct PsdHierarchyChatSendResult
    {
        internal PsdHierarchyChatSendResult(bool success, string message, string cliSessionId = "")
        {
            this.success = success;
            this.message = message ?? string.Empty;
            this.cliSessionId = cliSessionId ?? string.Empty;
        }

        internal readonly bool success;
        internal readonly string message;
        internal readonly string cliSessionId;
    }

    internal interface IPsdHierarchyChatTransport
    {
        Task<PsdHierarchyChatHttpResponse> SendAsync(PsdHierarchyChatHttpRequest request);
    }

    internal interface IPsdHierarchyCliChatTransport
    {
        Task<PsdHierarchyChatSendResult> SendAsync(
            PsdHierarchyChatContext context,
            PsdHierarchyChatConnection connection,
            IReadOnlyList<PsdHierarchyChatMessage> messages,
            string cliSessionId);
    }

    internal readonly struct PsdHierarchyCliInvocation
    {
        internal PsdHierarchyCliInvocation(
            string executablePath,
            string arguments,
            string workingDirectory,
            bool writePromptToStandardInput)
        {
            this.executablePath = executablePath ?? string.Empty;
            this.arguments = arguments ?? string.Empty;
            this.workingDirectory = workingDirectory ?? string.Empty;
            this.writePromptToStandardInput = writePromptToStandardInput;
        }

        internal readonly string executablePath;
        internal readonly string arguments;
        internal readonly string workingDirectory;
        internal readonly bool writePromptToStandardInput;
    }

    internal static class PsdHierarchyChatClient
    {
        internal const string OpenAiEndpoint = "https://api.openai.com/v1/responses";
        internal const string AnthropicEndpoint = "https://api.anthropic.com/v1/messages";
        private const int MaxClaudePromptCharacters = 6000;
        internal const string DefaultUserPrompt =
            "请按整理技能完整审查当前目标 Prefab，而不是只查看顶层或按名称猜测。\n" +
            "1. 结合 PSD 与 Prefab 的完整层级、节点几何、组件、同级顺序和重复结构，说明当前结构的主要问题。\n" +
            "2. 给出完整的原地整理后树形结构：每个新增语义容器、节点重命名、节点归属和保留顺序都要明确。\n" +
            "3. 对重复视觉单元按整体分组，不要把背景、文本、图标、锁等平铺到按类型命名的大容器中。\n" +
            "4. 标出无法安全推断、存在序列化引用风险或嵌套 Prefab 边界的节点，并说明保持不动的原因。\n" +
            "5. 列出应用前必须验证的布局、组件、引用、激活状态和资源命名不变量。\n" +
            "请严格按以下 5 个章节输出可审查的分析摘要，不要输出原始内部推理：\n" +
            "一、分析摘要：说明观察到的结构与主要问题。\n" +
            "二、分组依据：逐项说明几何、组件、同级顺序或重复结构证据。\n" +
            "三、风险与保留项：说明不移动或不改名节点的具体原因。\n" +
            "四、原地整理方案：给出完整目标树与每项调整。\n" +
            "五、验证清单：列出应用前后必须检查的不变量。\n" +
            "本次只原地整理当前目标 Prefab；不要新建、复制、抽取、嵌套或另存为任何 Prefab，也不要声称已经修改本地文件。";

        internal static PsdHierarchyChatHttpRequest BuildRequest(
            PsdHierarchyChatContext context,
            PsdHierarchyChatConnection connection,
            IReadOnlyList<PsdHierarchyChatMessage> messages)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!connection.TryValidate(out string validationError))
            {
                throw new ArgumentException(validationError, nameof(connection));
            }

            if (connection.connectionMode != PsdHierarchyAiConnectionMode.CustomApi)
            {
                throw new ArgumentException("仅自定义 API 连接可以构建 HTTP 请求。", nameof(connection));
            }

            return connection.provider == PsdHierarchyAiProvider.Codex
                ? BuildOpenAiRequest(context, connection, messages)
                : BuildAnthropicRequest(context, connection, messages);
        }

        internal static async Task<PsdHierarchyChatSendResult> SendAsync(
            PsdHierarchyChatContext context,
            PsdHierarchyChatConnection connection,
            IReadOnlyList<PsdHierarchyChatMessage> messages,
            IPsdHierarchyChatTransport transport = null,
            IPsdHierarchyCliChatTransport cliTransport = null)
        {
            return await SendWithCliSessionAsync(
                context,
                connection,
                messages,
                string.Empty,
                transport,
                cliTransport);
        }

        internal static async Task<PsdHierarchyChatSendResult> SendWithCliSessionAsync(
            PsdHierarchyChatContext context,
            PsdHierarchyChatConnection connection,
            IReadOnlyList<PsdHierarchyChatMessage> messages,
            string cliSessionId,
            IPsdHierarchyChatTransport transport = null,
            IPsdHierarchyCliChatTransport cliTransport = null)
        {
            if (!connection.TryValidate(out string validationError))
            {
                return new PsdHierarchyChatSendResult(false, validationError);
            }

            if (connection.connectionMode == PsdHierarchyAiConnectionMode.LocalCli)
            {
                try
                {
                    return await (cliTransport ?? new ProcessCliChatTransport()).SendAsync(
                        context,
                        connection,
                        messages,
                        cliSessionId);
                }
                catch (Exception exception)
                {
                    return new PsdHierarchyChatSendResult(false, "AI CLI 调用失败：" + exception.Message);
                }
            }

            PsdHierarchyChatHttpRequest request;
            try
            {
                request = BuildRequest(context, connection, messages);
            }
            catch (Exception exception)
            {
                return new PsdHierarchyChatSendResult(false, "无法构建 AI 请求：" + exception.Message);
            }

            PsdHierarchyChatHttpResponse response;
            try
            {
                response = await (transport ?? new UnityWebRequestChatTransport()).SendAsync(request);
            }
            catch (Exception exception)
            {
                return new PsdHierarchyChatSendResult(false, "AI 请求失败：" + exception.Message);
            }

            return ParseResponse(connection.provider, response);
        }

        internal static string DefaultEndpoint(PsdHierarchyAiProvider provider)
        {
            return provider == PsdHierarchyAiProvider.Codex ? OpenAiEndpoint : AnthropicEndpoint;
        }

        internal static string DefaultModel(PsdHierarchyAiProvider provider)
        {
            return provider == PsdHierarchyAiProvider.Codex ? "gpt-5" : "claude-sonnet-5";
        }

        internal static string GetProviderDisplayName(PsdHierarchyAiProvider provider)
        {
            return provider == PsdHierarchyAiProvider.Codex ? "Codex" : "Claude";
        }

        internal static string GetModelDisplayName(PsdHierarchyChatConnection connection)
        {
            return connection.connectionMode == PsdHierarchyAiConnectionMode.LocalCli
                ? "本地 CLI 默认"
                : connection.model.Trim();
        }

        internal static bool TryOpenInteractiveCli(
            PsdHierarchyChatConnection connection,
            string projectRoot,
            string cliSessionId,
            out string error)
        {
            if (connection.connectionMode != PsdHierarchyAiConnectionMode.LocalCli)
            {
                error = "当前会话使用自定义 API，不能打开本地 CLI。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(cliSessionId))
            {
                error = "当前对话尚未收到可恢复的 CLI 会话 ID。";
                return false;
            }

            if (!connection.TryValidate(out error))
            {
                return false;
            }

            try
            {
                PsdHierarchyCliInvocation invocation = CreateInteractiveCliInvocation(
                    connection,
                    projectRoot,
                    cliSessionId);
                Process.Start(new ProcessStartInfo
                {
                    FileName = invocation.executablePath,
                    Arguments = invocation.arguments,
                    WorkingDirectory = invocation.workingDirectory,
                    UseShellExecute = true,
                });
            }
            catch (Exception exception)
            {
                error = "打开本次对话的 CLI 失败：" + exception.Message;
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal static PsdHierarchyCliInvocation CreateCliInvocation(
            PsdHierarchyChatConnection connection,
            string workingDirectory)
        {
            return CreateCliInvocation(connection, workingDirectory, string.Empty, string.Empty, false);
        }

        internal static PsdHierarchyCliInvocation CreateCliInvocation(
            PsdHierarchyChatConnection connection,
            string workingDirectory,
            string prompt)
        {
            return CreateCliInvocation(connection, workingDirectory, prompt, string.Empty, false);
        }

        internal static PsdHierarchyCliInvocation CreateCliInvocation(
            PsdHierarchyChatConnection connection,
            string workingDirectory,
            string prompt,
            string cliSessionId,
            bool resumeCliSession)
        {
            if (connection.connectionMode != PsdHierarchyAiConnectionMode.LocalCli)
            {
                throw new ArgumentException("仅默认 CLI 连接可以构建 CLI 调用。", nameof(connection));
            }

            bool hasSessionId = !string.IsNullOrWhiteSpace(cliSessionId);
            if (resumeCliSession && !hasSessionId)
            {
                throw new ArgumentException("恢复 CLI 会话时必须提供会话 ID。", nameof(cliSessionId));
            }

            string sessionArguments = connection.provider == PsdHierarchyAiProvider.Claude && hasSessionId
                ? (resumeCliSession ? " --resume " : " --session-id ") + QuoteProcessArgument(cliSessionId)
                : string.Empty;
            if (connection.provider == PsdHierarchyAiProvider.Claude && !string.IsNullOrWhiteSpace(prompt))
            {
                string directClaudeExecutable = ResolveClaudeDirectExecutable(connection.cliExecutablePath);
                if (!string.IsNullOrEmpty(directClaudeExecutable))
                {
                    return new PsdHierarchyCliInvocation(
                        directClaudeExecutable,
                        "--print --output-format json --permission-mode dontAsk --safe-mode " +
                        "--tools Read --add-dir " + QuoteProcessArgument(workingDirectory) +
                        sessionArguments + " -- " + QuoteProcessArgument(prompt),
                        workingDirectory,
                        false);
                }
            }

            string arguments = connection.provider == PsdHierarchyAiProvider.Claude
                ? "--print --output-format json --permission-mode plan --safe-mode" + sessionArguments
                : resumeCliSession
                    ? "exec resume --json " + QuoteProcessArgument(cliSessionId) + " -"
                    : "exec --json --sandbox read-only -";
            string cliPath = connection.cliExecutablePath;
            if (cliPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                cliPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                string commandProcessor = Environment.GetEnvironmentVariable("ComSpec");
                if (string.IsNullOrWhiteSpace(commandProcessor))
                {
                    commandProcessor = "cmd.exe";
                }

                return new PsdHierarchyCliInvocation(
                    commandProcessor,
                    "/d /s /c \"\"" + cliPath.Replace("\"", "\"\"") + "\" " + arguments + "\"",
                    workingDirectory,
                    true);
            }

            return new PsdHierarchyCliInvocation(cliPath, arguments, workingDirectory, true);
        }

        internal static PsdHierarchyCliInvocation CreateInteractiveCliInvocation(
            PsdHierarchyChatConnection connection,
            string workingDirectory,
            string cliSessionId)
        {
            if (connection.connectionMode != PsdHierarchyAiConnectionMode.LocalCli)
            {
                throw new ArgumentException("仅本地 CLI 连接可以打开交互式会话。", nameof(connection));
            }

            if (string.IsNullOrWhiteSpace(cliSessionId))
            {
                throw new ArgumentException("恢复 CLI 会话时必须提供会话 ID。", nameof(cliSessionId));
            }

            string arguments = connection.provider == PsdHierarchyAiProvider.Claude
                ? "--resume " + QuoteProcessArgument(cliSessionId) +
                  " --permission-mode plan --safe-mode --add-dir " + QuoteProcessArgument(workingDirectory)
                : "-s read-only resume " + QuoteProcessArgument(cliSessionId);
            string commandProcessor = Environment.GetEnvironmentVariable("ComSpec");
            if (string.IsNullOrWhiteSpace(commandProcessor))
            {
                commandProcessor = "cmd.exe";
            }

            return new PsdHierarchyCliInvocation(
                commandProcessor,
                "/d /s /k \"\"" + connection.cliExecutablePath.Replace("\"", "\"\"") +
                "\" " + arguments + "\"",
                workingDirectory,
                false);
        }

        private static string ResolveClaudeDirectExecutable(string cliExecutablePath)
        {
            if (string.IsNullOrWhiteSpace(cliExecutablePath))
            {
                return string.Empty;
            }

            if (cliExecutablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return cliExecutablePath;
            }

            if (!cliExecutablePath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) &&
                !cliExecutablePath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            string npmDirectory = Path.GetDirectoryName(cliExecutablePath);
            return string.IsNullOrEmpty(npmDirectory)
                ? string.Empty
                : Path.Combine(
                    npmDirectory,
                    "node_modules",
                    "@anthropic-ai",
                    "claude-code",
                    "bin",
                    "claude.exe");
        }

        private static string QuoteProcessArgument(string value)
        {
            string input = value ?? string.Empty;
            var builder = new StringBuilder(input.Length + 2);
            builder.Append('"');
            int slashCount = 0;
            for (int index = 0; index < input.Length; index++)
            {
                char character = input[index];
                if (character == '\\')
                {
                    slashCount++;
                    continue;
                }

                if (character == '"')
                {
                    builder.Append('\\', slashCount * 2 + 1);
                    builder.Append(character);
                    slashCount = 0;
                    continue;
                }

                builder.Append('\\', slashCount);
                slashCount = 0;
                builder.Append(character);
            }

            builder.Append('\\', slashCount * 2);
            builder.Append('"');
            return builder.ToString();
        }

        internal static string ResolveUserPrompt(string prompt)
        {
            return string.IsNullOrWhiteSpace(prompt) ? DefaultUserPrompt : prompt.Trim();
        }

        internal static PsdHierarchyChatSendResult ParseResponse(
            PsdHierarchyAiProvider provider,
            PsdHierarchyChatHttpResponse response)
        {
            if (!response.success)
            {
                string apiError = TryExtractErrorMessage(response.body);
                string detail = string.IsNullOrEmpty(apiError) ? response.error : apiError;
                if (string.IsNullOrEmpty(detail))
                {
                    detail = "HTTP " + response.statusCode;
                }

                return new PsdHierarchyChatSendResult(false, "AI 请求失败：" + detail);
            }

            try
            {
                string text = provider == PsdHierarchyAiProvider.Codex
                    ? ExtractOpenAiText(response.body)
                    : ExtractAnthropicText(response.body);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return new PsdHierarchyChatSendResult(false, "AI 未返回可显示的文本。");
                }

                return new PsdHierarchyChatSendResult(true, text.Trim());
            }
            catch (Exception exception)
            {
                return new PsdHierarchyChatSendResult(false, "解析 AI 返回内容失败：" + exception.Message);
            }
        }

        private static PsdHierarchyChatHttpRequest BuildOpenAiRequest(
            PsdHierarchyChatContext context,
            PsdHierarchyChatConnection connection,
            IReadOnlyList<PsdHierarchyChatMessage> messages)
        {
            var request = new OpenAiRequest
            {
                model = connection.model.Trim(),
                instructions = context.BuildInstructions(),
                input = BuildOpenAiMessages(messages),
                store = false,
            };
            var httpRequest = new PsdHierarchyChatHttpRequest(connection.endpoint, JsonUtility.ToJson(request));
            httpRequest.SetHeader("Content-Type", "application/json");
            httpRequest.SetHeader("Authorization", "Bearer " + connection.apiKey.Trim());
            return httpRequest;
        }

        private static PsdHierarchyChatHttpRequest BuildAnthropicRequest(
            PsdHierarchyChatContext context,
            PsdHierarchyChatConnection connection,
            IReadOnlyList<PsdHierarchyChatMessage> messages)
        {
            var request = new AnthropicRequest
            {
                model = connection.model.Trim(),
                max_tokens = 4096,
                system = context.BuildInstructions(),
                messages = BuildAnthropicMessages(messages),
            };
            var httpRequest = new PsdHierarchyChatHttpRequest(connection.endpoint, JsonUtility.ToJson(request));
            httpRequest.SetHeader("Content-Type", "application/json");
            httpRequest.SetHeader("x-api-key", connection.apiKey.Trim());
            httpRequest.SetHeader("anthropic-version", "2023-06-01");
            return httpRequest;
        }

        private static OpenAiMessage[] BuildOpenAiMessages(IReadOnlyList<PsdHierarchyChatMessage> messages)
        {
            PsdHierarchyChatMessage[] normalized = NormalizeMessages(messages);
            var result = new OpenAiMessage[normalized.Length];
            for (int index = 0; index < normalized.Length; index++)
            {
                result[index] = new OpenAiMessage
                {
                    role = normalized[index].role,
                    content = normalized[index].content,
                };
            }

            return result;
        }

        private static AnthropicMessage[] BuildAnthropicMessages(IReadOnlyList<PsdHierarchyChatMessage> messages)
        {
            PsdHierarchyChatMessage[] normalized = NormalizeMessages(messages);
            var result = new AnthropicMessage[normalized.Length];
            for (int index = 0; index < normalized.Length; index++)
            {
                result[index] = new AnthropicMessage
                {
                    role = normalized[index].role,
                    content = normalized[index].content,
                };
            }

            return result;
        }

        private static PsdHierarchyChatMessage[] NormalizeMessages(IReadOnlyList<PsdHierarchyChatMessage> messages)
        {
            if (messages == null || messages.Count == 0)
            {
                return new[] { new PsdHierarchyChatMessage("user", DefaultUserPrompt) };
            }

            var result = new List<PsdHierarchyChatMessage>(messages.Count);
            for (int index = 0; index < messages.Count; index++)
            {
                PsdHierarchyChatMessage message = messages[index];
                if (string.IsNullOrWhiteSpace(message.content))
                {
                    continue;
                }

                string role = string.Equals(message.role, "assistant", StringComparison.OrdinalIgnoreCase)
                    ? "assistant"
                    : "user";
                string content = message.content.Trim();
                int previousIndex = result.Count - 1;
                if (previousIndex >= 0 && string.Equals(result[previousIndex].role, role, StringComparison.Ordinal))
                {
                    PsdHierarchyChatMessage previous = result[previousIndex];
                    result[previousIndex] = new PsdHierarchyChatMessage(
                        role,
                        previous.content + "\n\n" + content);
                    continue;
                }

                result.Add(new PsdHierarchyChatMessage(role, content));
            }

            return result.Count == 0
                ? new[] { new PsdHierarchyChatMessage("user", DefaultUserPrompt) }
                : result.ToArray();
        }

        private static string ExtractOpenAiText(string json)
        {
            OpenAiResponse response = JsonUtility.FromJson<OpenAiResponse>(json);
            if (response == null || response.output == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (OpenAiOutput output in response.output)
            {
                if (output == null || output.content == null)
                {
                    continue;
                }

                foreach (OpenAiContent content in output.content)
                {
                    if (content != null && !string.IsNullOrWhiteSpace(content.text))
                    {
                        if (builder.Length > 0)
                        {
                            builder.AppendLine();
                        }

                        builder.Append(content.text);
                    }
                }
            }

            return builder.ToString();
        }

        private static string ExtractAnthropicText(string json)
        {
            AnthropicResponse response = JsonUtility.FromJson<AnthropicResponse>(json);
            if (response == null || response.content == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (AnthropicContent content in response.content)
            {
                if (content != null && !string.IsNullOrWhiteSpace(content.text))
                {
                    if (builder.Length > 0)
                    {
                        builder.AppendLine();
                    }

                    builder.Append(content.text);
                }
            }

            return builder.ToString();
        }

        private static string TryExtractErrorMessage(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            try
            {
                ApiErrorEnvelope envelope = JsonUtility.FromJson<ApiErrorEnvelope>(json);
                return envelope != null && envelope.error != null ? envelope.error.message ?? string.Empty : string.Empty;
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
        }

        [Serializable]
        private sealed class OpenAiRequest
        {
            public string model;
            public string instructions;
            public OpenAiMessage[] input;
            public bool store;
        }

        [Serializable]
        private sealed class OpenAiMessage
        {
            public string role;
            public string content;
        }

        [Serializable]
        private sealed class AnthropicRequest
        {
            public string model;
            public int max_tokens;
            public string system;
            public AnthropicMessage[] messages;
        }

        [Serializable]
        private sealed class AnthropicMessage
        {
            public string role;
            public string content;
        }

        [Serializable]
        private sealed class OpenAiResponse
        {
            public OpenAiOutput[] output;
        }

        [Serializable]
        private sealed class OpenAiOutput
        {
            public OpenAiContent[] content;
        }

        [Serializable]
        private sealed class OpenAiContent
        {
            public string text;
        }

        [Serializable]
        private sealed class AnthropicResponse
        {
            public AnthropicContent[] content;
        }

        [Serializable]
        private sealed class AnthropicContent
        {
            public string text;
        }

        [Serializable]
        private sealed class ApiErrorEnvelope
        {
            public ApiError error;
        }

        [Serializable]
        private sealed class ApiError
        {
            public string message;
        }

        private sealed class UnityWebRequestChatTransport : IPsdHierarchyChatTransport
        {
            public async Task<PsdHierarchyChatHttpResponse> SendAsync(PsdHierarchyChatHttpRequest request)
            {
                using (var webRequest = new UnityWebRequest(request.url, UnityWebRequest.kHttpVerbPOST))
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.body));
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    foreach (KeyValuePair<string, string> header in request.Headers)
                    {
                        webRequest.SetRequestHeader(header.Key, header.Value);
                    }

                    UnityWebRequestAsyncOperation operation = webRequest.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    bool success = webRequest.result == UnityWebRequest.Result.Success;
                    return new PsdHierarchyChatHttpResponse(
                        success,
                        webRequest.responseCode,
                        webRequest.downloadHandler.text,
                        webRequest.error);
                }
            }
        }

        private sealed class ProcessCliChatTransport : IPsdHierarchyCliChatTransport
        {
            public async Task<PsdHierarchyChatSendResult> SendAsync(
                PsdHierarchyChatContext context,
                PsdHierarchyChatConnection connection,
                IReadOnlyList<PsdHierarchyChatMessage> messages,
                string cliSessionId)
            {
                bool resumeCliSession = !string.IsNullOrWhiteSpace(cliSessionId);
                string requestedSessionId = connection.provider == PsdHierarchyAiProvider.Claude && !resumeCliSession
                    ? Guid.NewGuid().ToString()
                    : cliSessionId;
                string prompt = resumeCliSession
                    ? LastUserMessage(messages)
                    : connection.provider == PsdHierarchyAiProvider.Claude
                        ? BuildClaudeDirectPrompt(context, messages)
                        : BuildCliPrompt(context, messages);
                PsdHierarchyCliInvocation invocation = CreateCliInvocation(
                    connection,
                    context.projectRoot,
                    prompt,
                    requestedSessionId,
                    resumeCliSession);
                var startInfo = new ProcessStartInfo
                {
                    FileName = invocation.executablePath,
                    Arguments = invocation.arguments,
                    WorkingDirectory = invocation.workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardInputEncoding = new UTF8Encoding(false),
                    StandardOutputEncoding = new UTF8Encoding(false),
                    StandardErrorEncoding = new UTF8Encoding(false),
                    CreateNoWindow = true,
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return new PsdHierarchyChatSendResult(false, "无法启动所选 AI CLI。" );
                    }

                    if (invocation.writePromptToStandardInput)
                    {
                        await process.StandardInput.WriteAsync(prompt);
                    }
                    process.StandardInput.Close();
                    Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                    Task<string> errorTask = process.StandardError.ReadToEndAsync();
                    await WaitForExitAsync(process);

                    string output = await outputTask;
                    string error = await errorTask;
                    if (process.ExitCode != 0)
                    {
                        return new PsdHierarchyChatSendResult(
                            false,
                            "AI CLI 返回错误：" + FirstNonEmptyLine(error, output, "退出码 " + process.ExitCode));
                    }

                    if (string.IsNullOrWhiteSpace(output))
                    {
                        return new PsdHierarchyChatSendResult(false, "AI CLI 未返回可显示的文本。" );
                    }

                    return ParseCliResponse(connection.provider, output, requestedSessionId);
                }
            }

            private static PsdHierarchyChatSendResult ParseCliResponse(
                PsdHierarchyAiProvider provider,
                string output,
                string fallbackSessionId)
            {
                return provider == PsdHierarchyAiProvider.Claude
                    ? ParseClaudeCliResponse(output, fallbackSessionId)
                    : ParseCodexCliResponse(output, fallbackSessionId);
            }

            private static PsdHierarchyChatSendResult ParseClaudeCliResponse(
                string output,
                string fallbackSessionId)
            {
                try
                {
                    ClaudeCliResult response = JsonUtility.FromJson<ClaudeCliResult>(output);
                    string message = response == null ? string.Empty : response.result;
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        return new PsdHierarchyChatSendResult(false, "Claude CLI 未返回可显示的文本。");
                    }

                    string sessionId = !string.IsNullOrWhiteSpace(response.session_id)
                        ? response.session_id
                        : fallbackSessionId;
                    if (string.IsNullOrWhiteSpace(sessionId))
                    {
                        return new PsdHierarchyChatSendResult(false, "Claude CLI 未返回可恢复的会话 ID。");
                    }

                    return new PsdHierarchyChatSendResult(true, message.Trim(), sessionId);
                }
                catch (ArgumentException exception)
                {
                    return new PsdHierarchyChatSendResult(false, "解析 Claude CLI 会话失败：" + exception.Message);
                }
            }

            private static PsdHierarchyChatSendResult ParseCodexCliResponse(
                string output,
                string fallbackSessionId)
            {
                string sessionId = fallbackSessionId;
                var message = new StringBuilder();
                using (var reader = new StringReader(output))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        try
                        {
                            CodexCliEvent cliEvent = JsonUtility.FromJson<CodexCliEvent>(line);
                            if (cliEvent == null)
                            {
                                continue;
                            }

                            if (string.Equals(cliEvent.type, "thread.started", StringComparison.Ordinal) &&
                                !string.IsNullOrWhiteSpace(cliEvent.thread_id))
                            {
                                sessionId = cliEvent.thread_id;
                            }

                            if (string.Equals(cliEvent.type, "item.completed", StringComparison.Ordinal) &&
                                cliEvent.item != null &&
                                string.Equals(cliEvent.item.type, "agent_message", StringComparison.Ordinal) &&
                                !string.IsNullOrWhiteSpace(cliEvent.item.text))
                            {
                                if (message.Length > 0)
                                {
                                    message.AppendLine();
                                }

                                message.Append(cliEvent.item.text);
                            }
                        }
                        catch (ArgumentException)
                        {
                            // Ignore non-event lines because Codex may write transport diagnostics to stdout.
                        }
                    }
                }

                if (message.Length == 0)
                {
                    return new PsdHierarchyChatSendResult(false, "Codex CLI 未返回可显示的文本。");
                }

                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    return new PsdHierarchyChatSendResult(false, "Codex CLI 未返回可恢复的会话 ID。");
                }

                return new PsdHierarchyChatSendResult(true, message.ToString().Trim(), sessionId);
            }

            private static async Task WaitForExitAsync(Process process)
            {
                while (!process.HasExited)
                {
                    await Task.Delay(50);
                }
            }

            private static string BuildCliPrompt(
                PsdHierarchyChatContext context,
                IReadOnlyList<PsdHierarchyChatMessage> messages)
            {
                var builder = new StringBuilder(context.BuildInstructions());
                builder.AppendLine();
                builder.AppendLine("===== BEGIN CHAT HISTORY =====");
                PsdHierarchyChatMessage[] normalized = NormalizeMessages(messages);
                foreach (PsdHierarchyChatMessage message in normalized)
                {
                    builder.AppendLine("[" + message.role + "]");
                    builder.AppendLine(message.content);
                }

                builder.AppendLine("===== END CHAT HISTORY =====");
                return builder.ToString();
            }

            [Serializable]
            private sealed class ClaudeCliResult
            {
                public string result;
                public string session_id;
            }

            [Serializable]
            private sealed class CodexCliEvent
            {
                public string type;
                public string thread_id;
                public CodexCliItem item;
            }

            [Serializable]
            private sealed class CodexCliItem
            {
                public string type;
                public string text;
            }

            private static string BuildClaudeDirectPrompt(
                PsdHierarchyChatContext context,
                IReadOnlyList<PsdHierarchyChatMessage> messages)
            {
                var builder = new StringBuilder();
                builder.AppendLine("You are reviewing one existing Unity Prefab hierarchy from inside a Unity Editor tool.");
                builder.AppendLine("Use the Read tool to inspect exactly these two files before answering:");
                builder.AppendLine("1. Cleanup skill: " + context.skillFullPath);
                builder.AppendLine(
                    "2. Target Prefab: " + Path.Combine(
                        context.projectRoot,
                        context.targetPrefabAssetPath.Replace('/', Path.DirectorySeparatorChar)));
                builder.AppendLine("Do not use any other tool. Do not edit, create, rename, or delete any file.");
                builder.AppendLine("Follow the cleanup skill. Return a complete, reviewable hierarchy-cleanup plan in Simplified Chinese.");
                builder.AppendLine("The plan must preserve visual layout, bindings, components, nested Prefab boundaries, and sibling order.");
                builder.AppendLine("The only permitted main-Prefab output is in_place at the exact Target Prefab path. Do not offer copy mode, a .cleaned.prefab, or any replacement Prefab.");
                builder.AppendLine("The target is already confirmed for in-place cleanup. Do not ask the user to choose an output mode or whether to create a new Prefab.");
                builder.AppendLine("This chat request authorizes no component extraction. Do not propose Prefab/Common, a nested component Prefab, or any component extraction field.");
                builder.AppendLine("Return an auditable analysis summary, not private chain-of-thought. In Simplified Chinese, use exactly these sections: 分析摘要, 分组依据, 风险与保留项, 原地整理方案, 验证清单. Ground every claim in observable hierarchy, geometry, component, sibling-order, or repeated-structure evidence.");
                builder.AppendLine("Do not claim that a local asset was changed.");
                builder.AppendLine("User request:");
                builder.Append(TrimPrompt(LastUserMessage(messages)));
                return builder.ToString();
            }

            private static string LastUserMessage(IReadOnlyList<PsdHierarchyChatMessage> messages)
            {
                PsdHierarchyChatMessage[] normalized = NormalizeMessages(messages);
                for (int index = normalized.Length - 1; index >= 0; index--)
                {
                    if (string.Equals(normalized[index].role, "user", StringComparison.Ordinal))
                    {
                        return normalized[index].content;
                    }
                }

                return DefaultUserPrompt;
            }

            private static string TrimPrompt(string prompt)
            {
                string value = prompt ?? string.Empty;
                if (value.Length <= MaxClaudePromptCharacters)
                {
                    return value;
                }

                return value.Substring(0, MaxClaudePromptCharacters) + "\n[后续追问已截断]";
            }

            private static string FirstNonEmptyLine(string first, string second, string fallback)
            {
                string[] candidates = { first, second };
                foreach (string candidate in candidates)
                {
                    if (string.IsNullOrWhiteSpace(candidate))
                    {
                        continue;
                    }

                    using (var reader = new StringReader(candidate))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                return line.Trim();
                            }
                        }
                    }
                }

                return fallback;
            }
        }
    }
}
