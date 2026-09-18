namespace PsdLayoutTool2
{
    using System;
    using System.IO;
    using Newtonsoft.Json;
    using UnityEngine;

    /// <summary>
    /// 本机个人配置：AI CLI、预览端口、九宫格可视化等「每人不一样」的字段。
    /// 存在工程根 UserSettings/ 下，不进 Assets、不提交 git，与共享的
    /// PsdLayoutProjectSettings.asset 分离，避免同事之间互相覆盖。
    /// </summary>
    internal static class PsdLayoutLocalUserSettings
    {
        internal const int CurrentVersion = 1;

        private const string RelativeDirectory = "UserSettings/PsdLayoutTool2";
        private const string RelativePath = RelativeDirectory + "/user-settings.json";

        private static Data cached;
        private static bool cacheLoaded;
        private static string cachedPath = string.Empty;

        /// <summary>单测可注入临时路径，避免写工程真实 UserSettings。</summary>
        internal static string PathOverride { get; set; }

        [Serializable]
        internal sealed class Data
        {
            public int version = CurrentVersion;
            public AiData ai = new AiData();
            public int previewServerPort = PsdCommonAssetPreviewSettings.DefaultPort;
            public bool showNineSliceImageMarkers = PsdLayoutProjectNineSliceSettings.DefaultShowImageMarkers;
        }

        [Serializable]
        internal sealed class AiData
        {
            public int provider = (int)PsdHierarchyAiProvider.None;
            public string customModel = string.Empty;
            public string reasoningEffort = string.Empty;
            public string customEndpoint = string.Empty;
        }

        /// <summary>工程根：优先 Assets 父目录（与 watcher/session 一致），单测无 Editor 时回退 CWD。</summary>
        private static string ResolveProjectRoot()
        {
            try
            {
                string parent = Directory.GetParent(Application.dataPath)?.FullName;
                if (!string.IsNullOrEmpty(parent))
                {
                    return parent;
                }
            }
            catch (Exception)
            {
                // 单测 / 非 Editor 环境访问 Application.dataPath 会失败。
            }

            return Directory.GetCurrentDirectory();
        }

        /// <summary>工程根下的绝对路径；单测可用 PathOverride 指到临时目录。</summary>
        internal static string FilePath =>
            string.IsNullOrEmpty(PathOverride)
                ? Path.GetFullPath(Path.Combine(ResolveProjectRoot(), RelativePath))
                : Path.GetFullPath(PathOverride);

        internal static Data Load()
        {
            string path = FilePath;
            if (cacheLoaded && string.Equals(path, cachedPath, StringComparison.OrdinalIgnoreCase) && cached != null)
            {
                return Clone(cached);
            }

            cachedPath = path;
            cacheLoaded = true;
            cached = ReadFromDisk(path) ?? new Data();
            // 返回副本：调用方 Set 失败时不应污染缓存。
            return Clone(cached);
        }

        /// <summary>文件存在且能解析出有效 Data 时返回 true；缺失或损坏返回 false（供迁移判断）。</summary>
        internal static bool TryLoadExisting(out Data data)
        {
            string path = FilePath;
            data = ReadFromDisk(path);
            if (data == null)
            {
                return false;
            }

            cachedPath = path;
            cacheLoaded = true;
            cached = Clone(data);
            data = Clone(cached);
            return true;
        }

        /// <summary>写盘。字段已在调用方校验；失败返回 false 并打日志。</summary>
        internal static bool Save(Data data)
        {
            return Save(data, out _);
        }

        /// <summary>写盘，并把可直接展示给用户的错误文案返回给上层。</summary>
        internal static bool Save(Data data, out string error)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            Data snapshot = Clone(data);
            snapshot.version = CurrentVersion;
            string path = FilePath;
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, JsonConvert.SerializeObject(snapshot, Formatting.Indented));
            }
            catch (Exception exception)
            {
                error = "保存个人配置失败（" + path + "）：" + exception.Message;
                Debug.LogError("[PSDLayoutTool2] " + error);
                return false;
            }

            cached = snapshot;
            cacheLoaded = true;
            cachedPath = path;
            error = string.Empty;
            return true;
        }

        private static Data Clone(Data source)
        {
            if (source == null)
            {
                return new Data();
            }

            return new Data
            {
                version = source.version,
                ai = source.ai == null
                    ? new AiData()
                    : new AiData
                    {
                        provider = source.ai.provider,
                        customModel = source.ai.customModel,
                        reasoningEffort = source.ai.reasoningEffort,
                        customEndpoint = source.ai.customEndpoint,
                    },
                previewServerPort = source.previewServerPort,
                showNineSliceImageMarkers = source.showNineSliceImageMarkers,
            };
        }

        /// <summary>测试或切换工程根后清掉内存缓存，下次 Load 重读磁盘。</summary>
        internal static void ResetCacheForTests()
        {
            cached = null;
            cacheLoaded = false;
            cachedPath = string.Empty;
            PathOverride = null;
        }

        private static Data ReadFromDisk(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                Data data = JsonConvert.DeserializeObject<Data>(json);
                if (data == null)
                {
                    return null;
                }

                if (data.ai == null)
                {
                    data.ai = new AiData();
                }

                if (!Enum.IsDefined(typeof(PsdHierarchyAiProvider), data.ai.provider))
                {
                    data.ai.provider = (int)PsdHierarchyAiProvider.None;
                }

                data.ai.customModel = data.ai.customModel ?? string.Empty;
                data.ai.reasoningEffort = data.ai.reasoningEffort ?? string.Empty;
                data.ai.customEndpoint = data.ai.customEndpoint ?? string.Empty;
                if (data.previewServerPort < 1 || data.previewServerPort > 65535)
                {
                    data.previewServerPort = PsdCommonAssetPreviewSettings.DefaultPort;
                }

                return data;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[PSDLayoutTool2] 个人配置解析失败，本次使用默认值（" + path + "）：" + exception.Message);
                return null;
            }
        }
    }
}
