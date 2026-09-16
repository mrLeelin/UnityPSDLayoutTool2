namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text;
    using Newtonsoft.Json.Linq;
    using UnityEditor;
    using UnityEngine;

    internal enum PsdHierarchyAiProvider
    {
        /// <summary>
        /// 未选择任何 AI 模型。默认值，用于「AI 整理尚未配置」的状态。
        /// 取 -1 是为了保留 Claude=0 / Codex=1 的既有序列化值，
        /// 否则老项目配置里的 provider 会被静默改写成别的模型。
        /// </summary>
        None = -1,

        Claude = 0,

        Codex = 1,

        Grok = 2,

        Pi = 3,
    }

    internal enum PsdHierarchyAiConnectionMode
    {
        LocalCli,
        CustomApi,
    }

    internal readonly struct PsdHierarchyAiCliDescriptor
    {
        internal PsdHierarchyAiCliDescriptor(
            PsdHierarchyAiProvider provider,
            string displayName,
            string executablePath,
            string[] modelSuggestions,
            string[] reasoningEffortLevels)
        {
            this.provider = provider;
            this.displayName = displayName ?? string.Empty;
            this.executablePath = executablePath ?? string.Empty;
            this.modelSuggestions = modelSuggestions ?? Empty;
            this.reasoningEffortLevels = reasoningEffortLevels ?? Empty;
        }

        private static readonly string[] Empty = new string[0];

        internal readonly PsdHierarchyAiProvider provider;
        internal readonly string displayName;
        internal readonly string executablePath;

        /// <summary>
        /// 模型名称的候选取值。**只用于界面的下拉建议，不参与校验**：
        /// 每个 CLI 支持的模型名是开放集合（新版本随时会加），所以这里列的是常见项，
        /// 输入框必须始终允许手填其它名字。
        /// </summary>
        internal readonly string[] modelSuggestions;

        /// <summary>
        /// 思考程度的候选取值。与模型名不同，这一列是**封闭枚举**：
        /// 档位会原样拼进命令行（见 PsdHierarchyChatClient.BuildReasoningEffortArguments），
        /// 写错不会报错、只会被 CLI 忽略，所以用下拉列全比让人手打更安全。
        /// 提示文案与网页下拉都由它派生，不再单独维护一份字符串。
        /// </summary>
        internal readonly string[] reasoningEffortLevels;

        /// <summary>模型名称输入框的占位提示，说明该 CLI 认什么样的取值。</summary>
        internal string defaultModelHint => Describe(modelSuggestions);

        /// <summary>思考程度输入框的占位提示，列出该 CLI 支持的档位。</summary>
        internal string reasoningEffortHint => Join(reasoningEffortLevels, " / ");

        private static string Join(string[] values, string separator)
        {
            return values == null || values.Length == 0
                ? string.Empty
                : string.Join(separator, values);
        }

        /// <summary>把候选模型名拼成「例如 a、b」这种占位提示。</summary>
        private static string Describe(string[] values)
        {
            return values == null || values.Length == 0
                ? string.Empty
                : "例如 " + string.Join("、", values);
        }
    }

    internal static class PsdHierarchyAiCliDiscovery
    {
        // 模型名是开放集合（只是下拉建议），思考程度是封闭集合（下拉即全集）。
        // 两组提示文案都由下面的数组派生，避免改了一处漏一处。
        private static readonly PsdHierarchyAiCliDescriptor[] SupportedClis =
        {
            new PsdHierarchyAiCliDescriptor(
                PsdHierarchyAiProvider.Claude,
                "Claude",
                "claude",
                new[] { "opus", "sonnet", "haiku", "claude-sonnet-5" },
                new[] { "low", "medium", "high", "xhigh", "max" }),
            new PsdHierarchyAiCliDescriptor(
                PsdHierarchyAiProvider.Codex,
                "Codex",
                "codex",
                new[] { "gpt-5", "gpt-5-codex", "gpt-5-mini" },
                new[] { "minimal", "low", "medium", "high" }),
            new PsdHierarchyAiCliDescriptor(
                PsdHierarchyAiProvider.Grok,
                "Grok",
                "grok",
                new[] { "grok-4", "grok-3", "grok-code-fast-1" },
                new[] { "low", "medium", "high" }),
            new PsdHierarchyAiCliDescriptor(
                PsdHierarchyAiProvider.Pi,
                "Pi",
                "pi",
                new[] { "openai/gpt-5", "anthropic/claude-sonnet-5" },
                new[] { "off", "minimal", "low", "medium", "high", "xhigh", "max" }),
        };

        internal static IReadOnlyList<PsdHierarchyAiCliDescriptor> FindInstalled()
        {
            return FindInstalled(Environment.GetEnvironmentVariable("PATH"), File.Exists);
        }

        internal static IReadOnlyList<PsdHierarchyAiCliDescriptor> FindInstalled(
            string searchPath,
            Func<string, bool> fileExists)
        {
            if (fileExists == null) throw new ArgumentNullException(nameof(fileExists));

            var installed = new List<PsdHierarchyAiCliDescriptor>();
            foreach (PsdHierarchyAiCliDescriptor supported in SupportedClis)
            {
                string executablePath = FindExecutable(supported.executablePath, searchPath, fileExists);
                if (!string.IsNullOrEmpty(executablePath))
                {
                    installed.Add(new PsdHierarchyAiCliDescriptor(
                        supported.provider,
                        supported.displayName,
                        executablePath,
                        supported.modelSuggestions,
                        supported.reasoningEffortLevels));
                }
            }

            return installed;
        }

        /// <summary>
        /// 返回该 provider 的支持信息（占位提示等），与是否安装无关。
        /// 未支持的 provider 返回 false。
        /// </summary>
        internal static bool TryGetSupported(
            PsdHierarchyAiProvider provider,
            out PsdHierarchyAiCliDescriptor descriptor)
        {
            for (int index = 0; index < SupportedClis.Length; index++)
            {
                if (SupportedClis[index].provider == provider)
                {
                    descriptor = SupportedClis[index];
                    return true;
                }
            }

            descriptor = default(PsdHierarchyAiCliDescriptor);
            return false;
        }

        internal static bool TryGetInstalled(
            PsdHierarchyAiProvider provider,
            out PsdHierarchyAiCliDescriptor descriptor)
        {
            IReadOnlyList<PsdHierarchyAiCliDescriptor> installed = FindInstalled();
            for (int index = 0; index < installed.Count; index++)
            {
                if (installed[index].provider == provider)
                {
                    descriptor = installed[index];
                    return true;
                }
            }

            descriptor = default(PsdHierarchyAiCliDescriptor);
            return false;
        }

        private static string FindExecutable(string command, string searchPath, Func<string, bool> fileExists)
        {
            if (string.IsNullOrWhiteSpace(searchPath))
            {
                return string.Empty;
            }

            string[] extensions = Application.platform == RuntimePlatform.WindowsEditor
                ? new[] { ".cmd", ".exe", ".bat", string.Empty }
                : new[] { string.Empty };
            string[] folders = searchPath.Split(Path.PathSeparator);
            foreach (string rawFolder in folders)
            {
                string folder = (rawFolder ?? string.Empty).Trim().Trim('"');
                if (string.IsNullOrEmpty(folder))
                {
                    continue;
                }

                foreach (string extension in extensions)
                {
                    string candidate = Path.Combine(folder, command + extension);
                    if (fileExists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Pi 的模型目录是「每台机器各不相同」的：它支持自定义 provider（例如用 cc-switch
    /// 配的 DeepSeek 中转），~/.pi/agent/models.json 里列的才是这台机器真正可用的模型。
    /// 静态写死的候选必然和用户实际用的对不上（实测本机就是 DeepSeek，而静态示例
    /// 写的是 openai / anthropic），所以 Pi 的下拉候选改为运行时读取：
    /// settings.json 给默认模型，models.json 给模型清单和各模型实际支持的思考档位
    /// （thinkingLevelMap 里映射为 null 的档位对该模型不起作用，直接不列）。
    /// 文件缺失或解析失败时回退到 SupportedClis 里的静态示例。
    /// </summary>
    internal static class PsdHierarchyAiPiCatalog
    {
        /// <summary>pi --thinking 接受的档位（pi --help 原文顺序），展示时按这个顺序排。</summary>
        private static readonly string[] EffortOrder =
        {
            "off", "minimal", "low", "medium", "high", "xhigh", "max",
        };

        private static readonly string[] NoValues = new string[0];

        // BuildConfigJson 每 0.4 秒跑一次，不能每次都读盘解析。
        // 以「两个文件的大小 + 修改时间」为缓存键：文件没变就复用上次结果（包括解析失败）。
        // 只在主线程调用（BuildConfigJson 与 Inspector 都是主线程），无需加锁。
        private static string cacheKey = string.Empty;
        private static bool cacheLoaded;
        private static string[] cachedModels = NoValues;
        private static string[] cachedLevels = NoValues;
        private static string cachedDefaultModel = string.Empty;

        /// <summary>
        /// 读本机 ~/.pi/agent/ 下的 settings.json 与 models.json。
        /// 返回 false 表示没有可用的目录信息（文件不存在/解析失败），调用方应使用静态兜底。
        /// modelSuggestions / effortLevels 可能单独为空：哪个为空就单独回退哪个。
        /// </summary>
        internal static bool TryLoad(
            out string[] modelSuggestions,
            out string[] effortLevels,
            out string defaultModel)
        {
            string agentDirectory = AgentDirectory();
            string settingsPath = Path.Combine(agentDirectory, "settings.json");
            string modelsPath = Path.Combine(agentDirectory, "models.json");
            string key = DescribeFile(settingsPath) + "|" + DescribeFile(modelsPath);
            if (!cacheLoaded || !string.Equals(key, cacheKey, StringComparison.Ordinal))
            {
                cacheLoaded = true;
                cacheKey = key;
                TryParse(
                    ReadAllTextOrNull(settingsPath),
                    ReadAllTextOrNull(modelsPath),
                    out cachedModels,
                    out cachedLevels,
                    out cachedDefaultModel);
            }

            modelSuggestions = cachedModels;
            effortLevels = cachedLevels;
            defaultModel = cachedDefaultModel;
            return cachedModels.Length > 0 || cachedLevels.Length > 0;
        }

        /// <summary>
        /// 纯解析：settings.json 与 models.json 的文本内容 → 候选模型 / 有效档位 / 默认模型。
        /// 档位取「默认模型」的 thinkingLevelMap；默认模型不在目录里时，
        /// 若所有模型的档位映射一致则采用该映射（例如两个 DeepSeek 模型都只有 high / max），
        /// 否则不给档位（回退静态全集）。
        /// </summary>
        internal static bool TryParse(
            string settingsJson,
            string modelsJson,
            out string[] modelSuggestions,
            out string[] effortLevels,
            out string defaultModel)
        {
            modelSuggestions = NoValues;
            effortLevels = NoValues;
            defaultModel = string.Empty;

            JObject models = ParseObjectOrNull(modelsJson);
            if (models == null)
            {
                return false;
            }

            JObject settings = ParseObjectOrNull(settingsJson);
            if (settings != null)
            {
                defaultModel = settings["defaultModel"]?.ToString() ?? string.Empty;
            }

            // 目录里所有模型的 id（保序去重）与各自的档位映射。
            var ids = new List<string>();
            var mapsById = new Dictionary<string, JObject>();
            var allMaps = new List<JObject>();
            if (models["providers"] is JObject providers)
            {
                foreach (JProperty providerProperty in providers.Properties())
                {
                    if (!(providerProperty.Value is JObject provider) ||
                        !(provider["models"] is JArray providerModels))
                    {
                        continue;
                    }

                    foreach (JToken entry in providerModels)
                    {
                        string id = (entry as JObject)?["id"]?.ToString();
                        if (string.IsNullOrEmpty(id) || ids.Contains(id))
                        {
                            continue;
                        }

                        ids.Add(id);
                        if ((entry as JObject)?["thinkingLevelMap"] is JObject map)
                        {
                            mapsById[id] = map;
                            allMaps.Add(map);
                        }
                    }
                }
            }

            if (ids.Count == 0)
            {
                return false;
            }

            var suggestions = new List<string>();
            if (!string.IsNullOrEmpty(defaultModel))
            {
                suggestions.Add(defaultModel);
            }

            foreach (string id in ids)
            {
                if (!suggestions.Contains(id))
                {
                    suggestions.Add(id);
                }
            }

            modelSuggestions = suggestions.ToArray();

            JObject chosenMap = null;
            if (!string.IsNullOrEmpty(defaultModel) && mapsById.TryGetValue(defaultModel, out JObject exactMap))
            {
                chosenMap = exactMap;
            }
            else if (AllMapsAgree(allMaps))
            {
                chosenMap = allMaps[0];
            }

            if (chosenMap != null)
            {
                effortLevels = LevelsFromMap(chosenMap);
            }

            return true;
        }

        /// <summary>档位映射 → 有效档位列表：只保留映射值非 null 的键，按 EffortOrder 排序。</summary>
        private static string[] LevelsFromMap(JObject map)
        {
            var enabled = new List<string>();
            foreach (string level in EffortOrder)
            {
                if (map[level] is JToken token && token.Type != JTokenType.Null)
                {
                    enabled.Add(level);
                }
            }

            return enabled.Count > 0 ? enabled.ToArray() : NoValues;
        }

        private static bool AllMapsAgree(List<JObject> maps)
        {
            if (maps.Count == 0)
            {
                return false;
            }

            string[] reference = LevelsFromMap(maps[0]);
            for (int index = 1; index < maps.Count; index++)
            {
                string[] other = LevelsFromMap(maps[index]);
                if (reference.Length != other.Length)
                {
                    return false;
                }

                for (int level = 0; level < reference.Length; level++)
                {
                    if (!string.Equals(reference[level], other[level], StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static string AgentDirectory()
        {
            // 不用 Application.platform：那是 Unity 的 ECall，脱离编辑器（单测 / 反射探针）会抛
            // 「ECall methods must be packaged into a system module」。USERPROFILE / HOME
            // 在 Windows 与 mac/linux 上各自总是存在，按这个顺序取就够。
            string home = Environment.GetEnvironmentVariable("USERPROFILE");
            if (string.IsNullOrEmpty(home))
            {
                home = Environment.GetEnvironmentVariable("HOME");
            }

            return Path.Combine(home ?? string.Empty, ".pi", "agent");
        }

        private static string DescribeFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return "missing";
                }

                var info = new FileInfo(path);
                return info.Length.ToString() + "@" + info.LastWriteTimeUtc.Ticks.ToString();
            }
            catch (Exception)
            {
                return "error";
            }
        }

        private static string ReadAllTextOrNull(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static JObject ParseObjectOrNull(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JObject.Parse(json);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    internal readonly struct PsdHierarchyAiSettingsSnapshot
    {
        internal PsdHierarchyAiSettingsSnapshot(
            PsdHierarchyAiProvider provider,
            string customEndpoint,
            string customModel,
            string reasoningEffort)
        {
            this.provider = provider;
            this.customEndpoint = customEndpoint ?? string.Empty;
            this.customModel = customModel ?? string.Empty;
            this.reasoningEffort = reasoningEffort ?? string.Empty;
        }

        internal readonly PsdHierarchyAiProvider provider;
        internal readonly string customEndpoint;
        internal readonly string customModel;
        internal readonly string reasoningEffort;

        /// <summary>是否已选择 AI 模型。未选择时 AI 整理不可用。</summary>
        internal bool isConfigured => provider != PsdHierarchyAiProvider.None;

        /// <summary>
        /// 连接方式不再单独落盘：API 地址留空即走本机 CLI，填了才走自定义 API。
        /// 这样设置面板只有一个「留空就是默认」的规则，不会出现地址为空却标记成自定义 API 的矛盾状态。
        /// </summary>
        internal PsdHierarchyAiConnectionMode connectionMode =>
            !isConfigured || string.IsNullOrWhiteSpace(customEndpoint)
                ? PsdHierarchyAiConnectionMode.LocalCli
                : PsdHierarchyAiConnectionMode.CustomApi;

        internal string ResolveEndpoint()
        {
            return string.IsNullOrWhiteSpace(customEndpoint)
                ? PsdHierarchyChatClient.DefaultEndpoint(provider)
                : customEndpoint.Trim();
        }

        internal string ResolveModel()
        {
            return string.IsNullOrWhiteSpace(customModel)
                ? PsdHierarchyChatClient.DefaultModel(provider)
                : customModel.Trim();
        }

        /// <summary>思考程度留空表示不传该参数，完全交给 CLI 自己的配置。</summary>
        internal string ResolveReasoningEffort() => (reasoningEffort ?? string.Empty).Trim();

        internal bool TryValidate(out string error)
        {
            // 「不启用」是一个合法且可保存的状态，不算校验失败。
            // 「尚未选择 AI 模型」的拦截放在真正要用 AI 的地方（连接校验与 AI整理入口）。
            if (provider == PsdHierarchyAiProvider.None)
            {
                error = string.Empty;
                return true;
            }

            if (!PsdHierarchyAiCliDiscovery.TryGetSupported(provider, out _))
            {
                error = "选择的 AI 不受支持。";
                return false;
            }

            // 思考程度会原样拼进命令行: Codex 走 -c 裸值形式，其余走带引号的参数。
            // 含空格或引号会让拼接结果在不同 shell 下行为不一致，直接拒绝比猜要好。
            string effort = (reasoningEffort ?? string.Empty).Trim();
            if (effort.IndexOfAny(new[] { ' ', '\t', '"', '\'' }) >= 0)
            {
                error = "思考程度不能包含空格或引号，请填写单个档位名，例如 high。";
                return false;
            }

            if (connectionMode == PsdHierarchyAiConnectionMode.CustomApi)
            {
                string endpoint = ResolveEndpoint();
                if (string.IsNullOrWhiteSpace(endpoint))
                {
                    error = "所选 AI 没有内置的官方 API 地址，请填写自定义 API 地址。";
                    return false;
                }

                if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri parsedEndpoint) ||
                    (parsedEndpoint.Scheme != Uri.UriSchemeHttp && parsedEndpoint.Scheme != Uri.UriSchemeHttps))
                {
                    error = "自定义 API 地址必须是 http 或 https 的完整地址。";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(ResolveModel()))
                {
                    error = "请填写自定义 API 的模型名称。";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    internal sealed class PsdHierarchyAiSettings
    {
        [SerializeField]
        private PsdHierarchyAiProvider provider = PsdHierarchyAiProvider.None;

        /// <summary>
        /// 历史字段：旧配置用它保存「自定义 API 的模型名」。
        /// 现在它同时充当本地 CLI 的模型参数，留空即不传 --model，用 CLI 自身配置。
        /// 保留字段名是为了让老配置里的值原样迁移过来。
        /// </summary>
        [SerializeField]
        private string customModel = string.Empty;

        [SerializeField]
        private string reasoningEffort = string.Empty;

        [SerializeField]
        private string customEndpoint = string.Empty;

        internal PsdHierarchyAiSettingsSnapshot Resolve()
        {
            return new PsdHierarchyAiSettingsSnapshot(provider, customEndpoint, customModel, reasoningEffort);
        }

        internal bool Set(
            PsdHierarchyAiProvider newProvider,
            string newCustomEndpoint,
            string newCustomModel,
            string newReasoningEffort)
        {
            var candidate = new PsdHierarchyAiSettingsSnapshot(
                newProvider,
                (newCustomEndpoint ?? string.Empty).Trim(),
                (newCustomModel ?? string.Empty).Trim(),
                (newReasoningEffort ?? string.Empty).Trim());
            if (!candidate.TryValidate(out string error))
            {
                throw new ArgumentException(error);
            }

            if (provider == candidate.provider &&
                string.Equals(customEndpoint, candidate.customEndpoint, StringComparison.Ordinal) &&
                string.Equals(customModel, candidate.customModel, StringComparison.Ordinal) &&
                string.Equals(reasoningEffort, candidate.reasoningEffort, StringComparison.Ordinal))
            {
                return false;
            }

            provider = candidate.provider;
            customEndpoint = candidate.customEndpoint;
            customModel = candidate.customModel;
            reasoningEffort = candidate.reasoningEffort;
            return true;
        }

        /// <summary>
        /// 关闭 AI 整理时使用：只改 provider，保留模型与地址，方便下次重新启用时不用重填。
        /// </summary>
        internal bool Clear()
        {
            if (provider == PsdHierarchyAiProvider.None)
            {
                return false;
            }

            provider = PsdHierarchyAiProvider.None;
            return true;
        }
    }

    internal sealed class PsdHierarchyAiSecretStore
    {
        private const string StoragePrefix = "PsdLayoutTool2.HierarchyAi.v1";

        internal bool HasApiKey(string projectRoot, PsdHierarchyAiProvider provider)
        {
            return EditorPrefs.HasKey(BuildStorageKey(projectRoot, provider));
        }

        internal bool TryReadApiKey(string projectRoot, PsdHierarchyAiProvider provider, out string apiKey)
        {
            apiKey = string.Empty;
            string storageKey = BuildStorageKey(projectRoot, provider);
            if (!EditorPrefs.HasKey(storageKey))
            {
                return false;
            }

            try
            {
                byte[] encrypted = null;
                byte[] plaintext = null;
                try
                {
                    encrypted = Convert.FromBase64String(EditorPrefs.GetString(storageKey, string.Empty));
                    plaintext = TransformWithCurrentUser(encrypted, false);
                    apiKey = Encoding.UTF8.GetString(plaintext);
                }
                finally
                {
                    if (encrypted != null)
                    {
                        Array.Clear(encrypted, 0, encrypted.Length);
                    }

                    if (plaintext != null)
                    {
                        Array.Clear(plaintext, 0, plaintext.Length);
                    }
                }
                return !string.IsNullOrWhiteSpace(apiKey);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("无法读取本机加密 API Key：" + exception.Message, exception);
            }
        }

        internal void SaveApiKey(string projectRoot, PsdHierarchyAiProvider provider, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API Key 不能为空。", nameof(apiKey));
            }

            byte[] plaintext = Encoding.UTF8.GetBytes(apiKey.Trim());
            byte[] encrypted = null;
            try
            {
                encrypted = TransformWithCurrentUser(plaintext, true);
                EditorPrefs.SetString(BuildStorageKey(projectRoot, provider), Convert.ToBase64String(encrypted));
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("无法保存本机加密 API Key：" + exception.Message, exception);
            }
            finally
            {
                Array.Clear(plaintext, 0, plaintext.Length);
                if (encrypted != null)
                {
                    Array.Clear(encrypted, 0, encrypted.Length);
                }
            }
        }

        internal void ClearApiKey(string projectRoot, PsdHierarchyAiProvider provider)
        {
            EditorPrefs.DeleteKey(BuildStorageKey(projectRoot, provider));
        }

        private static string BuildStorageKey(string projectRoot, PsdHierarchyAiProvider provider)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("项目根目录不能为空。", nameof(projectRoot));
            }

            string identity = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            byte[] bytes = Encoding.UTF8.GetBytes(identity.ToUpperInvariant());
            byte[] hash;
            using (SHA256 algorithm = SHA256.Create())
            {
                hash = algorithm.ComputeHash(bytes);
            }

            try
            {
                var builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("x2"));
                }

                return StoragePrefix + "." + builder + "." + provider;
            }
            finally
            {
                Array.Clear(bytes, 0, bytes.Length);
                Array.Clear(hash, 0, hash.Length);
            }
        }

        private static byte[] TransformWithCurrentUser(byte[] input, bool protect)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                throw new PlatformNotSupportedException("本机 API Key 加密目前仅支持 Windows。");
            }

            if (input == null || input.Length == 0)
            {
                throw new ArgumentException("加密数据不能为空。", nameof(input));
            }

            var inputBlob = new DataBlob();
            var outputBlob = new DataBlob();
            IntPtr description = IntPtr.Zero;
            try
            {
                inputBlob.Length = input.Length;
                inputBlob.Data = Marshal.AllocHGlobal(input.Length);
                Marshal.Copy(input, 0, inputBlob.Data, input.Length);
                bool success = protect
                    ? CryptProtectData(
                        ref inputBlob,
                        null,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        CryptProtectUiForbidden,
                        out outputBlob)
                    : CryptUnprotectData(
                        ref inputBlob,
                        out description,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        CryptProtectUiForbidden,
                        out outputBlob);
                if (!success)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                var output = new byte[outputBlob.Length];
                Marshal.Copy(outputBlob.Data, output, 0, outputBlob.Length);
                return output;
            }
            finally
            {
                if (inputBlob.Data != IntPtr.Zero)
                {
                    ZeroUnmanagedMemory(inputBlob.Data, inputBlob.Length);
                    Marshal.FreeHGlobal(inputBlob.Data);
                }

                if (outputBlob.Data != IntPtr.Zero)
                {
                    ZeroUnmanagedMemory(outputBlob.Data, outputBlob.Length);
                    LocalFree(outputBlob.Data);
                }

                if (description != IntPtr.Zero)
                {
                    LocalFree(description);
                }
            }
        }

        private static void ZeroUnmanagedMemory(IntPtr address, int length)
        {
            for (int index = 0; index < length; index++)
            {
                Marshal.WriteByte(address, index, 0);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int Length;
            public IntPtr Data;
        }

        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptProtectData(
            ref DataBlob dataIn,
            string dataDescription,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStructure,
            int flags,
            out DataBlob dataOut);

        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptUnprotectData(
            ref DataBlob dataIn,
            out IntPtr dataDescription,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStructure,
            int flags,
            out DataBlob dataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        private const int CryptProtectUiForbidden = 0x1;
    }
}
