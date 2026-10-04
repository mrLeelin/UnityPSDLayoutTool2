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
        ///
        /// 这里只是**兜底**：运行时优先用 PsdHierarchyAiCliModelCatalog 读各 CLI 缓存在家目录里的
        /// 真实目录（Codex/Grok/Pi 都有）。本机实测这份静态表和实际情况完全对不上
        /// （codex 实际是 gpt-6.1-sol、grok 实际是 grok-4.6），只在读不到本机目录时才会用到它。
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

        /// <summary>其他 CLI 的目录读取器（见 PsdHierarchyAiCliModelCatalog）也要用同一套家目录规则。</summary>
        internal static string AgentDirectory()
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

        /// <summary>缓存的键：大小 + 修改时间。文件没变就复用上次结果（包括「解析失败」这个结论）。</summary>
        internal static string DescribeFile(string path)
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

        internal static string ReadAllTextOrNull(string path)
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

        internal static JObject ParseObjectOrNull(string json)
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

    /// <summary>
    /// 某个 CLI 的候选目录：模型名 + 有效思考档位 + 本机默认值 + 数据来源。
    /// 模型名是开放集合（永远只是建议，输入框必须允许手填）；档位是封闭集合（列出来就是全集）。
    /// </summary>
    internal readonly struct PsdHierarchyAiCliCatalog
    {
        internal PsdHierarchyAiCliCatalog(
            string[] models,
            string[] effortLevels,
            string defaultModel,
            string origin,
            bool isLocal)
        {
            this.models = models ?? Empty;
            this.effortLevels = effortLevels ?? Empty;
            this.defaultModel = defaultModel ?? string.Empty;
            this.origin = origin ?? string.Empty;
            this.isLocal = isLocal;
        }

        private static readonly string[] Empty = new string[0];

        internal readonly string[] models;
        internal readonly string[] effortLevels;
        internal readonly string defaultModel;

        /// <summary>给人读的来源说明，页面直接显示，例如「本机 ~/.codex/models_cache.json」。</summary>
        internal readonly string origin;

        /// <summary>true = 真读了本机文件；false = 只能用内置示例，那份列表必然和用户实际用的对不上。</summary>
        internal readonly bool isLocal;
    }

    /// <summary>
    /// 各 CLI 的「本机模型目录」读取器。
    ///
    /// 为什么需要它：四个 CLI 里**只有 pi 有命令行式的模型列表**，另外三个都没有这种命令，
    /// 但它们都会把自己那份真实目录缓存在家目录里：
    ///   Claude : ~/.claude/settings.json 的 env.ANTHROPIC_DEFAULT_*_MODEL[_NAME]
    ///   Codex  : ~/.codex/models_cache.json（服务端下发的完整目录）+ ~/.codex/config.toml 的 model
    ///   Grok   : ~/.grok/models_cache.json + ~/.grok/config.toml 的 [models] default
    ///   Pi     : ~/.pi/agent/models.json（由 PsdHierarchyAiPiCatalog 解析）
    ///
    /// 只读 pi 那一份的后果是实测过的：本机 codex 实际用 gpt-6.1-sol、grok 实际用 grok-4.6，
    /// 而静态示例里写的是 gpt-5 / grok-4，**一个都不存在** —— 于是「拉取模型名」无论怎么写都不对，
    /// 因为数据根本没去拿。这里改成四个都读本机，读不到才回退示例，并把来源如实告诉页面。
    ///
    /// 只读文件、不联网、不需要 API Key；读写都只碰纯 .NET API，因此在监听线程调用也安全。
    /// </summary>
    internal static class PsdHierarchyAiCliModelCatalog
    {
        /// <summary>档位的规范顺序：下拉与提示文案都按它排序，避免各处顺序不一致。</summary>
        private static readonly string[] EffortOrder =
        {
            "off", "none", "minimal", "low", "medium", "high", "xhigh", "max", "ultra",
        };

        private static readonly PsdHierarchyAiCliCatalog EmptyCatalog =
            new PsdHierarchyAiCliCatalog(null, null, string.Empty, string.Empty, false);

        // BuildConfigJson 每 0.4 秒跑一次，不能每次都读盘解析；/models 还可能从监听线程进来现算。
        // 所以缓存要加锁：两个线程都可能第一次走到这里。
        private static readonly object sync = new object();
        private static readonly Dictionary<int, Slot> slots = new Dictionary<int, Slot>();

        private sealed class Slot
        {
            internal string key = string.Empty;
            internal bool loaded;
            internal PsdHierarchyAiCliCatalog value;
        }

        internal static PsdHierarchyAiCliCatalog Resolve(PsdHierarchyAiProvider provider)
        {
            if (provider == PsdHierarchyAiProvider.None)
            {
                return EmptyCatalog;
            }

            lock (sync)
            {
                int id = (int)provider;
                if (!slots.TryGetValue(id, out Slot slot))
                {
                    slot = new Slot();
                    slots[id] = slot;
                }

                string key = BuildCacheKey(provider);
                if (!slot.loaded || !string.Equals(key, slot.key, StringComparison.Ordinal))
                {
                    slot.key = key;
                    slot.loaded = true;
                    slot.value = Load(provider);
                }

                return slot.value;
            }
        }

        private static string BuildCacheKey(PsdHierarchyAiProvider provider)
        {
            switch (provider)
            {
                case PsdHierarchyAiProvider.Claude:
                    return PsdHierarchyAiPiCatalog.DescribeFile(ClaudeSettingsPath());
                case PsdHierarchyAiProvider.Codex:
                    return PsdHierarchyAiPiCatalog.DescribeFile(CodexCatalogPath()) + "|" +
                           PsdHierarchyAiPiCatalog.DescribeFile(CodexConfigPath());
                case PsdHierarchyAiProvider.Grok:
                    return PsdHierarchyAiPiCatalog.DescribeFile(GrokCatalogPath()) + "|" +
                           PsdHierarchyAiPiCatalog.DescribeFile(GrokConfigPath());
                case PsdHierarchyAiProvider.Pi:
                    return PsdHierarchyAiPiCatalog.DescribeFile(PiSettingsPath()) + "|" +
                           PsdHierarchyAiPiCatalog.DescribeFile(PiModelsPath());
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 逐字段回退：本机目录给了模型没给档位（Claude 就是这样），档位仍用静态全集，
        /// 而不是整份丢掉退回示例。
        /// </summary>
        private static PsdHierarchyAiCliCatalog Load(PsdHierarchyAiProvider provider)
        {
            PsdHierarchyAiCliCatalog local = ReadLocal(provider);
            if (!local.isLocal)
            {
                return new PsdHierarchyAiCliCatalog(
                    FallbackModels(provider), FallbackEfforts(provider),
                    string.Empty, BuiltInOrigin(provider), false);
            }

            return new PsdHierarchyAiCliCatalog(
                local.models.Length > 0 ? local.models : FallbackModels(provider),
                local.effortLevels.Length > 0 ? local.effortLevels : FallbackEfforts(provider),
                local.defaultModel,
                local.origin,
                true);
        }

        private static PsdHierarchyAiCliCatalog ReadLocal(PsdHierarchyAiProvider provider)
        {
            switch (provider)
            {
                case PsdHierarchyAiProvider.Claude:
                    return ReadClaude();
                case PsdHierarchyAiProvider.Codex:
                    return ReadCodex();
                case PsdHierarchyAiProvider.Grok:
                    return ReadGrok();
                case PsdHierarchyAiProvider.Pi:
                    return ReadPi();
                default:
                    return EmptyCatalog;
            }
        }

        /// <summary>
        /// Claude Code 没有列模型的命令，但它自己的配置里就写着当前用的模型。
        /// 值可能是别名（sonnet）也可能是完整 id（claude-sonnet-5），还可能带 claude-code
        /// 自己的质量后缀 [1M] —— 那个后缀只有它自己认，所以统一剥掉（见 NormalizeModelId）。
        /// </summary>
        private static PsdHierarchyAiCliCatalog ReadClaude()
        {
            JObject settings = PsdHierarchyAiPiCatalog.ParseObjectOrNull(
                PsdHierarchyAiPiCatalog.ReadAllTextOrNull(ClaudeSettingsPath()));
            if (settings == null)
            {
                return EmptyCatalog;
            }

            JToken env = settings["env"];
            var models = new List<string>();

            // ANTHROPIC_MODEL 是本机真正生效的那个；没有再退回顶层 model（通常是个别名）。
            AddModel(models, env?["ANTHROPIC_MODEL"]?.ToString());
            string defaultModel = models.Count > 0 ? models[0] : string.Empty;
            AddModel(models, settings["model"]?.ToString());
            if (string.IsNullOrEmpty(defaultModel) && models.Count > 0)
            {
                defaultModel = models[0];
            }

            // 走中转（cc-switch 这类）时，三个档位会各自映射到一个具体模型名上。
            // _NAME 是纯模型名，_MODEL 可能带 [1M] 后缀，两边都收、去重后自然只剩 _NAME。
            string[] tiers = { "OPUS", "SONNET", "HAIKU", "FABLE" };
            foreach (string tier in tiers)
            {
                AddModel(models, env?["ANTHROPIC_DEFAULT_" + tier + "_MODEL_NAME"]?.ToString());
            }

            foreach (string tier in tiers)
            {
                AddModel(models, env?["ANTHROPIC_DEFAULT_" + tier + "_MODEL"]?.ToString());
            }

            if (models.Count == 0)
            {
                return EmptyCatalog;
            }

            // Claude 的 --effort 档位没有本机来源，回退静态全集（effortLevels 留空即触发）。
            return new PsdHierarchyAiCliCatalog(
                models.ToArray(), null, defaultModel, "本机 ~/.claude/settings.json", true);
        }

        /// <summary>
        /// Codex 把服务端下发的完整模型目录缓存成 models_cache.json，每项自带
        /// visibility（hidden 的不该列）与它真正支持的档位。当前默认模型在 config.toml 顶层的 model 上。
        /// </summary>
        private static PsdHierarchyAiCliCatalog ReadCodex()
        {
            JObject cache = PsdHierarchyAiPiCatalog.ParseObjectOrNull(
                PsdHierarchyAiPiCatalog.ReadAllTextOrNull(CodexCatalogPath()));
            if (cache == null || !(cache["models"] is JArray entries))
            {
                return EmptyCatalog;
            }

            var models = new List<string>();
            var efforts = new List<string>();
            foreach (JToken token in entries)
            {
                if (!(token is JObject model))
                {
                    continue;
                }

                if (string.Equals(model.Value<string>("visibility"), "hidden", StringComparison.OrdinalIgnoreCase) ||
                    model.Value<bool?>("supported_in_api") == false)
                {
                    continue;
                }

                string slug = NormalizeModelId(model.Value<string>("slug"));
                if (slug.Length == 0 || models.Contains(slug))
                {
                    continue;
                }

                models.Add(slug);
                AddEfforts(efforts, model["supported_reasoning_levels"], "effort");
            }

            if (models.Count == 0)
            {
                return EmptyCatalog;
            }

            string defaultModel = NormalizeModelId(ReadTomlValue(CodexConfigPath(), string.Empty, "model"));
            if (!models.Contains(defaultModel))
            {
                // 配置里写的模型不在目录里（换过 provider 之类）就不要假装它是候选。
                defaultModel = string.Empty;
            }

            // 取并集而不是只取默认模型的档位：目录里各模型档位差别很大（有的只有 none，
            // 有的多出 ultra），只取一个会把别的合法档位挡掉。并集只会多列、不会少列。
            return new PsdHierarchyAiCliCatalog(
                models.ToArray(),
                efforts.Count > 0 ? OrderEfforts(efforts) : null,
                defaultModel,
                "本机 ~/.codex/models_cache.json",
                true);
        }

        /// <summary>
        /// Grok 的目录同样缓存在 models_cache.json 里（models 是对象，取 info.id）；
        /// 当前默认模型在 config.toml 的 [models] default 上。
        /// </summary>
        private static PsdHierarchyAiCliCatalog ReadGrok()
        {
            JObject cache = PsdHierarchyAiPiCatalog.ParseObjectOrNull(
                PsdHierarchyAiPiCatalog.ReadAllTextOrNull(GrokCatalogPath()));
            if (cache == null || !(cache["models"] is JObject entries))
            {
                return EmptyCatalog;
            }

            var models = new List<string>();
            var efforts = new List<string>();
            foreach (JProperty property in entries.Properties())
            {
                if (!(property.Value is JObject entry))
                {
                    continue;
                }

                JObject info = entry["info"] as JObject;
                if (info?.Value<bool?>("hidden") == true)
                {
                    continue;
                }

                string id = NormalizeModelId(info?.Value<string>("id") ?? property.Name);
                if (id.Length == 0 || models.Contains(id))
                {
                    continue;
                }

                models.Add(id);
                AddEfforts(efforts, info?["reasoning_efforts"], "id");
            }

            if (models.Count == 0)
            {
                return EmptyCatalog;
            }

            string defaultModel = NormalizeModelId(ReadTomlValue(GrokConfigPath(), "models", "default"));
            if (!models.Contains(defaultModel))
            {
                defaultModel = string.Empty;
            }

            return new PsdHierarchyAiCliCatalog(
                models.ToArray(),
                efforts.Count > 0 ? OrderEfforts(efforts) : null,
                defaultModel,
                "本机 ~/.grok/models_cache.json",
                true);
        }

        private static PsdHierarchyAiCliCatalog ReadPi()
        {
            if (!PsdHierarchyAiPiCatalog.TryLoad(out string[] models, out string[] levels, out string defaultModel))
            {
                return EmptyCatalog;
            }

            return new PsdHierarchyAiCliCatalog(
                models, levels, defaultModel, "本机 ~/.pi/agent/models.json", true);
        }

        private static string[] FallbackModels(PsdHierarchyAiProvider provider)
        {
            return PsdHierarchyAiCliDiscovery.TryGetSupported(provider, out PsdHierarchyAiCliDescriptor descriptor)
                ? descriptor.modelSuggestions
                : new string[0];
        }

        private static string[] FallbackEfforts(PsdHierarchyAiProvider provider)
        {
            return PsdHierarchyAiCliDiscovery.TryGetSupported(provider, out PsdHierarchyAiCliDescriptor descriptor)
                ? descriptor.reasoningEffortLevels
                : new string[0];
        }

        private static string BuiltInOrigin(PsdHierarchyAiProvider provider)
        {
            switch (provider)
            {
                case PsdHierarchyAiProvider.Claude:
                    return "内置示例（未找到 ~/.claude/settings.json）";
                case PsdHierarchyAiProvider.Codex:
                    return "内置示例（未找到 ~/.codex/models_cache.json）";
                case PsdHierarchyAiProvider.Grok:
                    return "内置示例（未找到 ~/.grok/models_cache.json）";
                case PsdHierarchyAiProvider.Pi:
                    return "内置示例（未找到 ~/.pi/agent/models.json）";
                default:
                    return "内置示例";
            }
        }

        /// <summary>
        /// 把候选压成一行提示：最多列 4 个，其余用「等 N 个」带过。
        /// 真实目录可能有十几项（本机 codex 就是 15 个），全列出来在提示行里就是一堵字墙。
        /// </summary>
        internal static string Summarize(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return string.Empty;
            }

            const int head = 4;
            if (values.Length <= head)
            {
                return string.Join("、", values);
            }

            var shown = new string[head];
            Array.Copy(values, shown, head);
            return string.Join("、", shown) + " 等 " + values.Length + " 个";
        }

        /// <summary>
        /// 剥掉 claude-code 自己的质量后缀与首尾空白：claude-sonnet-5[1M] → claude-sonnet-5。
        /// 后缀是它内部写法，原样传给 --model 或自定义 API 只会变成不认识的模型名。
        /// </summary>
        internal static string NormalizeModelId(string raw)
        {
            string value = (raw ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                return string.Empty;
            }

            int bracket = value.IndexOf('[');
            if (bracket > 0 && value.EndsWith("]", StringComparison.Ordinal))
            {
                value = value.Substring(0, bracket).Trim();
            }

            return value;
        }

        private static void AddModel(List<string> models, string raw)
        {
            string value = NormalizeModelId(raw);
            if (value.Length == 0 || models.Contains(value))
            {
                return;
            }

            models.Add(value);
        }

        /// <summary>档位节点可能是 ["low","high"]，也可能是 [{"effort":"low"},…] / [{"id":"low"},…]。</summary>
        private static void AddEfforts(List<string> efforts, JToken levels, string fieldName)
        {
            if (!(levels is JArray array))
            {
                return;
            }

            foreach (JToken token in array)
            {
                string effort = token is JObject item ? item.Value<string>(fieldName) : token.ToString();
                AddEffort(efforts, effort);
            }
        }

        private static void AddEffort(List<string> efforts, string raw)
        {
            string value = (raw ?? string.Empty).Trim();
            if (value.Length == 0 || efforts.Contains(value))
            {
                return;
            }

            efforts.Add(value);
        }

        /// <summary>按 EffortOrder 排序；表外的值原样排在末尾，CLI 加了新档位也不会被悄悄丢掉。</summary>
        private static string[] OrderEfforts(List<string> values)
        {
            var ordered = new List<string>();
            foreach (string known in EffortOrder)
            {
                if (values.Contains(known))
                {
                    ordered.Add(known);
                }
            }

            foreach (string value in values)
            {
                if (!ordered.Contains(value))
                {
                    ordered.Add(value);
                }
            }

            return ordered.ToArray();
        }

        /// <summary>
        /// 极简 TOML 取值：只认「section 下的 key = 值」这一种形态，够读 config.toml 里的
        /// model / default 两个字符串键。不引完整 TOML 解析器 —— 为了两个字符串不值得加依赖。
        /// section 传空串表示只看第一个 [table] 之前的那段（顶层键）。
        /// </summary>
        private static string ReadTomlValue(string path, string section, string key)
        {
            string text = PsdHierarchyAiPiCatalog.ReadAllTextOrNull(path);
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            bool topLevel = string.IsNullOrEmpty(section);
            bool inSection = topLevel;
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                if (line[0] == '[')
                {
                    inSection = topLevel ? false : line.Trim('[', ']').Trim() == section;
                    continue;
                }

                if (!inSection)
                {
                    continue;
                }

                int equals = line.IndexOf('=');
                if (equals <= 0 || line.Substring(0, equals).Trim() != key)
                {
                    continue;
                }

                return UnquoteToml(line.Substring(equals + 1));
            }

            return string.Empty;
        }

        private static string UnquoteToml(string raw)
        {
            string value = (raw ?? string.Empty).Trim();
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\''))
            {
                int end = value.IndexOf(value[0], 1);
                return end > 0 ? value.Substring(1, end - 1).Trim() : value.Trim(value[0]).Trim();
            }

            // 裸值：行尾注释要切掉（引号里的 # 属于值本身，上面那个分支已经处理）。
            int hash = value.IndexOf('#');
            if (hash >= 0)
            {
                value = value.Substring(0, hash);
            }

            return value.Trim();
        }

        private static string HomeDirectory()
        {
            // 不用 Application.platform：那是 Unity 的 ECall，脱离编辑器（单测 / 反射探针）会抛
            // 「ECall methods must be packaged into a system module」。
            string home = Environment.GetEnvironmentVariable("USERPROFILE");
            if (string.IsNullOrEmpty(home))
            {
                home = Environment.GetEnvironmentVariable("HOME");
            }

            return home ?? string.Empty;
        }

        private static string ClaudeSettingsPath() => Path.Combine(HomeDirectory(), ".claude", "settings.json");
        private static string CodexCatalogPath() => Path.Combine(HomeDirectory(), ".codex", "models_cache.json");
        private static string CodexConfigPath() => Path.Combine(HomeDirectory(), ".codex", "config.toml");
        private static string GrokCatalogPath() => Path.Combine(HomeDirectory(), ".grok", "models_cache.json");
        private static string GrokConfigPath() => Path.Combine(HomeDirectory(), ".grok", "config.toml");
        private static string PiSettingsPath() => Path.Combine(PsdHierarchyAiPiCatalog.AgentDirectory(), "settings.json");
        private static string PiModelsPath() => Path.Combine(PsdHierarchyAiPiCatalog.AgentDirectory(), "models.json");
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
