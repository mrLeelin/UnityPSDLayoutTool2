namespace PsdLayoutTool2
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// 监听 Library/PsdHierarchyTerminal 下的 *.apply 哨兵：
    /// 终端/外部 AI 在人工审核通过后写入该文件，Unity 自动校验并应用对应计划。
    /// 结果会写回同前缀 *.apply-result.json，终端 AI 可读取成功/失败原因并自行修订计划。
    ///
    /// 协议版本 2（ADR 0001/0002、规格 A12-A16）：
    /// - 会话描述与回执新写入使用版本 2，读取端校验版本，旧会话要求重新生成。
    /// - 业务写入前先把哨兵改名为 *.applying 抢占请求，并记录执行标识、计划内容哈希与
    ///   目标 Prefab 指纹；哨兵离开待执行集合是唯一信号，域重载/崩溃/改名失败都不会
    ///   重复执行同一个已接受的请求。
    /// - 计划内容在抢占时一次性读取并哈希，之后的文件替换不改变已接受请求的内容。
    /// - 抢占后没有终态的请求只做只读核验并报告不确定，绝不自动重试、回滚或继续写入。
    /// </summary>
    internal static class PsdHierarchyTerminalApplyWatcher
    {
        internal const int CurrentProtocolVersion = 2;

        private const string TerminalFolderName = "PsdHierarchyTerminal";
        private const string ApplyExtension = ".apply";
        private const string ApplyingExtension = ".applying";
        private const string AppliedExtension = ".applied";
        private const string FailedExtension = ".apply-failed";
        private const string UncertainExtension = ".apply-uncertain";
        private const string ResultSuffix = ".apply-result.json";
        private const string SessionSuffix = ".session.json";
        private const double PollIntervalSeconds = 0.5d;

        // 回执状态：只有 applied 代表写完并核验通过（规格 A13）。
        internal const string StatusApplied = "applied";
        internal const string StatusRejected = "rejected";
        internal const string StatusPartial = "partial";
        internal const string StatusUncertain = "uncertain";

        private static double nextPollTime;
        private static bool isApplying;
        private static string inFlightApplyPath = string.Empty;

        [Serializable]
        internal sealed class SessionRecord
        {
            public int version = CurrentProtocolVersion;
            public string sessionId = string.Empty;
            public string sourcePsdAssetPath = string.Empty;
            public string targetPrefabPath = string.Empty;
            public string planPath = string.Empty;
            public string reviewPath = string.Empty;
        }

        /// <summary>抢占记录：写入计划内容哈希与执行标识，重载后据此判断不确定请求。</summary>
        [Serializable]
        internal sealed class ApplyClaimRecord
        {
            public int version = CurrentProtocolVersion;
            public string sessionId = string.Empty;
            public string executionId = string.Empty;
            public string planPath = string.Empty;
            public string planHash = string.Empty;
            public string sourcePsdAssetPath = string.Empty;
            public string targetPrefabPath = string.Empty;
            public string targetFingerprintBefore = string.Empty;
            public string claimedAtUtc = string.Empty;
        }

        /// <summary>写给终端 AI 的回执；失败时 message 即校验/执行错误全文。</summary>
        [Serializable]
        internal sealed class ApplyResultRecord
        {
            public int version = CurrentProtocolVersion;
            public bool success;
            public string status = string.Empty;
            public string stage = string.Empty;
            public string message = string.Empty;
            public string sessionId = string.Empty;
            public string executionId = string.Empty;
            public string planPath = string.Empty;
            public string planHash = string.Empty;
            public string sourcePsdAssetPath = string.Empty;
            public string targetPrefabPath = string.Empty;
            public string finishedAtUtc = string.Empty;
        }

        [InitializeOnLoadMethod]
        private static void Install()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (isApplying)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < nextPollTime)
            {
                return;
            }

            nextPollTime = EditorApplication.timeSinceStartup + PollIntervalSeconds;

            // 编译、导入与 PlayMode 切换期间不启动新的应用（规格 A15）。
            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            RecoverInterruptedClaims();

            string applyPath = FindNewestPendingApply();
            if (string.IsNullOrEmpty(applyPath))
            {
                return;
            }

            if (!TryClaim(applyPath, out string claimPath))
            {
                return;
            }

            isApplying = true;
            inFlightApplyPath = claimPath;
            _ = ApplyAsync(applyPath, claimPath);
        }

        /// <summary>终端目录（绝对路径）：真实会话、哨兵与回执都放在这里。</summary>
        internal static string TerminalDirectoryPath => TerminalDirectory();

        /// <summary>
        /// 测试入口：跳过轮询节流，立即消费一个待执行哨兵并等待这次应用结束。
        /// 返回是否真的发起了一次应用（没有待执行哨兵或占用失败时为 false，且不写入任何内容）。
        /// </summary>
        internal static async Task<bool> RunPendingApplyOnceForTestsAsync()
        {
            if (isApplying)
            {
                return false;
            }

            RecoverInterruptedClaims();

            string applyPath = FindNewestPendingApply();
            if (string.IsNullOrEmpty(applyPath))
            {
                return false;
            }

            if (!TryClaim(applyPath, out string claimPath))
            {
                return false;
            }

            isApplying = true;
            inFlightApplyPath = claimPath;
            try
            {
                await ApplyAsync(applyPath, claimPath);
            }
            finally
            {
                isApplying = false;
                inFlightApplyPath = string.Empty;
            }

            return true;
        }

        private static string FindNewestPendingApply()
        {
            string directory = TerminalDirectory();
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return null;
            }

            try
            {
                return Directory
                    .GetFiles(directory, "*" + ApplyExtension)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>抢占：哨兵改名为 *.applying。失败即不执行，避免任何重复写入。</summary>
        private static bool TryClaim(string applyPath, out string claimPath)
        {
            claimPath = BuildClaimPath(applyPath);
            try
            {
                if (!File.Exists(applyPath))
                {
                    return false;
                }

                DeleteIfExists(claimPath);
                File.Move(applyPath, claimPath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[PSDLayoutTool2] 无法占用 Apply 哨兵，本次不执行任何写入：" +
                    applyPath + "：" + exception.Message);
                return false;
            }
        }

        /// <summary>
        /// 域重载/崩溃后残留的 *.applying 只有两种可能：写入已完成但没有终态标记，或写入过程
        /// 被打断。两者都只做只读核验并报告不确定，不自动重试、不回滚、不继续写入。
        /// </summary>
        private static void RecoverInterruptedClaims()
        {
            string directory = TerminalDirectory();
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return;
            }

            string[] claims;
            try
            {
                claims = Directory.GetFiles(directory, "*" + ApplyingExtension);
            }
            catch (Exception)
            {
                return;
            }

            foreach (string claimPath in claims)
            {
                try
                {
                    ApplyClaimRecord claim = ReadClaim(claimPath);
                    string targetFingerprintAfter = ResolveTargetFingerprint(claim.targetPrefabPath);
                    bool wroteSomething =
                        !string.IsNullOrEmpty(claim.targetFingerprintBefore) &&
                        !string.Equals(claim.targetFingerprintBefore, targetFingerprintAfter, StringComparison.Ordinal);

                    var record = new ApplyResultRecord
                    {
                        version = CurrentProtocolVersion,
                        success = false,
                        status = StatusUncertain,
                        stage = "recovery",
                        message = wroteSomething
                            ? "Unity 在上一次应用过程中被中断，目标 Prefab 的磁盘内容已经变化。" +
                              "无法判断整理是否完整完成，也不会自动重试或回滚。" +
                              "请核验 Prefab 后基于当前层级重新分析并生成新计划。"
                            : "Unity 在上一次应用过程中被中断，目标 Prefab 的磁盘内容没有变化，" +
                              "但无法确认计划是否已执行完毕。不会自动重试；请核验后重新生成计划。",
                        sessionId = claim.sessionId,
                        executionId = claim.executionId,
                        planPath = claim.planPath,
                        planHash = claim.planHash,
                        sourcePsdAssetPath = claim.sourcePsdAssetPath,
                        targetPrefabPath = claim.targetPrefabPath,
                        finishedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    };
                    WriteResult(ResolveResultPath(claimPath), record);
                    Debug.LogError("[PSDLayoutTool2] 检测到被中断的 Apply 请求，已写入不确定回执：" + claimPath);
                    TryMove(claimPath, StripStateExtension(claimPath) + UncertainExtension);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[PSDLayoutTool2] 恢复被中断的 Apply 请求失败：" + exception.Message);
                }
            }
        }

        private static ApplyClaimRecord ReadClaim(string claimPath)
        {
            try
            {
                string json = File.ReadAllText(claimPath, Encoding.UTF8);
                ApplyClaimRecord claim = string.IsNullOrWhiteSpace(json)
                    ? null
                    : JsonConvert.DeserializeObject<ApplyClaimRecord>(json);
                if (claim != null)
                {
                    return claim;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PSDLayoutTool2] 抢占记录无法解析：" + claimPath + "：" + exception.Message);
            }

            return new ApplyClaimRecord
            {
                sessionId = Path.GetFileNameWithoutExtension(claimPath),
            };
        }

        private static string ResolveTargetFingerprint(string targetPrefabAssetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPrefabAssetPath))
            {
                return string.Empty;
            }

            try
            {
                string fullPath = GetProjectFullPath(targetPrefabAssetPath);
                return File.Exists(fullPath)
                    ? PsdHierarchyChatContextBuilder.ComputeFileFingerprint(fullPath)
                    : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string GetProjectFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException("无法解析 Unity 工程根目录。");
            }

            return Path.GetFullPath(Path.Combine(
                projectRoot,
                (assetPath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string TerminalDirectory()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                {
                    return null;
                }

                return Path.Combine(projectRoot, "Library", TerminalFolderName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static string BuildResultPath(string applyPath)
        {
            if (string.IsNullOrWhiteSpace(applyPath))
            {
                throw new ArgumentException("Apply 路径不能为空。", nameof(applyPath));
            }

            if (applyPath.EndsWith(ApplyExtension, StringComparison.OrdinalIgnoreCase))
            {
                return applyPath.Substring(0, applyPath.Length - ApplyExtension.Length) + ResultSuffix;
            }

            return applyPath + ResultSuffix;
        }

        /// <summary>与 Apply 哨兵同前缀的会话描述文件路径。</summary>
        internal static string BuildSessionPath(string applyPath)
        {
            if (string.IsNullOrWhiteSpace(applyPath))
            {
                throw new ArgumentException("Apply 路径不能为空。", nameof(applyPath));
            }

            return StripStateExtension(applyPath) + SessionSuffix;
        }

        /// <summary>与 Apply 哨兵同前缀的抢占文件路径。</summary>
        internal static string BuildClaimPath(string applyPath)
        {
            if (string.IsNullOrWhiteSpace(applyPath))
            {
                throw new ArgumentException("Apply 路径不能为空。", nameof(applyPath));
            }

            if (applyPath.EndsWith(ApplyExtension, StringComparison.OrdinalIgnoreCase))
            {
                return applyPath.Substring(0, applyPath.Length - ApplyExtension.Length) + ApplyingExtension;
            }

            return applyPath + ApplyingExtension;
        }

        /// <summary>任意哨兵状态路径 → 回执路径。</summary>
        internal static string ResolveResultPath(string anyStatePath)
        {
            if (string.IsNullOrWhiteSpace(anyStatePath))
            {
                throw new ArgumentException("哨兵路径不能为空。", nameof(anyStatePath));
            }

            return StripStateExtension(anyStatePath) + ResultSuffix;
        }

        internal static string ComputePlanHash(string planJson)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(planJson ?? string.Empty));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                {
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static async Task ApplyAsync(string applyPath, string claimPath)
        {
            SessionRecord session = null;
            ApplyClaimRecord claim = null;
            try
            {
                // 新一轮应用前清掉旧回执，避免 AI 读到上一次结果。
                DeleteIfExists(BuildResultPath(applyPath));

                if (!TryLoadSession(applyPath, out session, out string error))
                {
                    FailApply(applyPath, claimPath, session, null, StatusRejected, "session", error);
                    return;
                }

                string planPath = ResolvePlanPath(applyPath, session, out string planError);
                if (string.IsNullOrEmpty(planPath))
                {
                    FailApply(applyPath, claimPath, session, null, StatusRejected, "plan", planError);
                    return;
                }

                // 抢占时一次性读取计划内容：之后计划文件被替换也不改变本次已接受的请求。
                string planJson = File.ReadAllText(planPath, Encoding.UTF8);
                claim = new ApplyClaimRecord
                {
                    version = CurrentProtocolVersion,
                    sessionId = session.sessionId,
                    executionId = Guid.NewGuid().ToString("N"),
                    planPath = planPath,
                    planHash = ComputePlanHash(planJson),
                    sourcePsdAssetPath = session.sourcePsdAssetPath,
                    targetPrefabPath = session.targetPrefabPath,
                    targetFingerprintBefore = ResolveTargetFingerprint(session.targetPrefabPath),
                    claimedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                };
                File.WriteAllText(claimPath, JsonConvert.SerializeObject(claim, Formatting.Indented), new UTF8Encoding(false));

                if (!PsdHierarchyChatContextBuilder.TryCreate(
                        session.sourcePsdAssetPath,
                        session.targetPrefabPath,
                        out PsdHierarchyChatContext context,
                        out error))
                {
                    FailApply(applyPath, claimPath, session, claim, StatusRejected, "context",
                        "无法创建整理上下文：" + error);
                    return;
                }

                PsdHierarchyChatCleanupExecutionResult result =
                    await PsdHierarchyChatCleanupExecution.ApplyConfirmedAsync(context, planJson);

                CompleteApply(applyPath, claimPath, session, claim, result);
            }
            catch (Exception exception)
            {
                FailApply(applyPath, claimPath, session, claim, StatusUncertain, "exception",
                    "应用过程中发生异常，无法确认结果：" + exception.Message);
            }
            finally
            {
                isApplying = false;
                inFlightApplyPath = string.Empty;
            }
        }

        private static string ResolvePlanPath(string applyPath, SessionRecord session, out string error)
        {
            error = string.Empty;
            if (!string.IsNullOrWhiteSpace(session.planPath) && File.Exists(session.planPath))
            {
                return session.planPath;
            }

            string siblingPlan = StripStateExtension(applyPath) + ".plan.json";
            if (File.Exists(siblingPlan))
            {
                return siblingPlan;
            }

            error = "找不到与 Apply 哨兵对应的计划文件：" + applyPath;
            return string.Empty;
        }

        private static bool TryLoadSession(string applyPath, out SessionRecord session, out string error)
        {
            session = null;
            error = string.Empty;
            string fileName = Path.GetFileName(applyPath);
            if (!fileName.EndsWith(ApplyExtension, StringComparison.OrdinalIgnoreCase))
            {
                error = "不是 Apply 哨兵文件：" + applyPath;
                return false;
            }

            string sessionId = fileName.Substring(0, fileName.Length - ApplyExtension.Length);
            string sessionPath = StripStateExtension(applyPath) + SessionSuffix;
            if (!File.Exists(sessionPath))
            {
                error = "缺少会话描述文件，无法确定要应用的 PSD/Prefab：" + sessionPath;
                return false;
            }

            try
            {
                session = JsonConvert.DeserializeObject<SessionRecord>(File.ReadAllText(sessionPath, Encoding.UTF8));
            }
            catch (Exception exception)
            {
                error = "解析会话描述失败：" + exception.Message;
                return false;
            }

            if (session == null)
            {
                error = "会话描述为空：" + sessionPath;
                return false;
            }

            // 读取端校验独立协议版本：旧 v1 会话必须重新生成，不能静默执行。
            if (session.version != CurrentProtocolVersion)
            {
                int actualVersion = session.version;
                session = null;
                error = "会话描述版本为 " + actualVersion + "，当前只接受版本 " +
                        CurrentProtocolVersion + " 的终端会话；请重新发起一次 AI 整理以生成新会话。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(session.sourcePsdAssetPath) ||
                string.IsNullOrWhiteSpace(session.targetPrefabPath))
            {
                error = "会话描述缺少 sourcePsdAssetPath / targetPrefabPath。";
                session = null;
                return false;
            }

            if (string.IsNullOrWhiteSpace(session.sessionId))
            {
                session.sessionId = sessionId;
            }

            return true;
        }

        private static void CompleteApply(
            string applyPath,
            string claimPath,
            SessionRecord session,
            ApplyClaimRecord claim,
            PsdHierarchyChatCleanupExecutionResult result)
        {
            string status;
            bool success = false;
            switch (result.state)
            {
                case PsdHierarchyCleanupExecutionState.Success:
                    status = StatusApplied;
                    success = true;
                    break;
                case PsdHierarchyCleanupExecutionState.Partial:
                    status = StatusPartial;
                    break;
                case PsdHierarchyCleanupExecutionState.Uncertain:
                    status = StatusUncertain;
                    break;
                default:
                    status = StatusRejected;
                    break;
            }

            string message = string.IsNullOrWhiteSpace(result.message) ? StatusMessage(status) : result.message;
            // 回执必须写到 *.apply-result.json：写到哨兵路径会让 AI 永远读不到结果，
            // 而且会在待执行集合里重新制造一个 .apply 文件，导致同一请求被再次执行。
            WriteResult(BuildResultPath(applyPath), BuildRecord(session, claim, success, status, result.stage, message));

            if (success)
            {
                Debug.Log(
                    "[PSDLayoutTool2] AI 计划已自动应用：" + session.sourcePsdAssetPath +
                    "\n计划：" + claim.planPath +
                    "\n回执：" + BuildResultPath(applyPath));
                TryMove(claimPath, StripStateExtension(claimPath) + AppliedExtension);
                return;
            }

            Debug.LogError("[PSDLayoutTool2] 自动应用 AI 计划未成功（" + status + "）：" + message);
            TryMove(
                claimPath,
                StripStateExtension(claimPath) + (status == StatusUncertain ? UncertainExtension : FailedExtension));
            if (status != StatusRejected)
            {
                ShowFailureDialog(message, BuildResultPath(applyPath));
            }
        }

        private static string StatusMessage(string status)
        {
            switch (status)
            {
                case StatusApplied:
                    return "Prefab 已更新并通过核验。";
                case StatusPartial:
                    return "Prefab 已保存，但保存后的核验没有通过。请核验实际结果，不要直接重复应用。";
                case StatusUncertain:
                    return "无法确定写入结果，请核验实际结果，不要直接重复应用。";
                default:
                    return "计划在写入前被拒绝，业务资源没有变化。";
            }
        }

        private static void FailApply(
            string applyPath,
            string claimPath,
            SessionRecord session,
            ApplyClaimRecord claim,
            string status,
            string stage,
            string message)
        {
            string reason = string.IsNullOrWhiteSpace(message) ? "未知错误。" : message;
            WriteResult(BuildResultPath(applyPath), BuildRecord(session, claim, false, status, stage, reason));

            Debug.LogError("[PSDLayoutTool2] 自动应用 AI 计划未成功（" + status + "/" + stage + "）：" + reason);
            try
            {
                File.WriteAllText(applyPath + ".error", reason, new UTF8Encoding(false));
            }
            catch (Exception)
            {
                // 写错误旁路失败不影响主流程。
            }

            // 拒绝与不确定都必须移出待执行集合：同一请求不会被再次执行。
            TryMove(
                claimPath,
                StripStateExtension(claimPath) + (status == StatusUncertain ? UncertainExtension : FailedExtension));

            // 写入前拒绝可安全修订后重来，不弹窗；已经可能写入的情况必须让人看到。
            if (status != StatusRejected)
            {
                ShowFailureDialog(reason, BuildResultPath(applyPath));
            }
        }

        private static ApplyResultRecord BuildRecord(
            SessionRecord session,
            ApplyClaimRecord claim,
            bool success,
            string status,
            string stage,
            string message)
        {
            return new ApplyResultRecord
            {
                version = CurrentProtocolVersion,
                success = success,
                status = status ?? string.Empty,
                stage = stage ?? string.Empty,
                message = message ?? string.Empty,
                sessionId = claim?.sessionId ?? session?.sessionId ?? string.Empty,
                executionId = claim?.executionId ?? string.Empty,
                planPath = claim?.planPath ?? session?.planPath ?? string.Empty,
                planHash = claim?.planHash ?? string.Empty,
                sourcePsdAssetPath = claim?.sourcePsdAssetPath ?? session?.sourcePsdAssetPath ?? string.Empty,
                targetPrefabPath = claim?.targetPrefabPath ?? session?.targetPrefabPath ?? string.Empty,
                finishedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            };
        }

        private static void ShowFailureDialog(string message, string resultPath)
        {
            if (Application.isBatchMode)
            {
                return;
            }

            EditorUtility.DisplayDialog(
                "PSDLayoutTool2",
                "自动应用 AI 计划未成功：\n" + message +
                "\n\n回执已写给终端 AI：\n" + resultPath +
                "\n\n请先核验当前 Prefab，再决定是否重新生成计划。",
                "确定");
        }

        private static void WriteResult(string resultPath, ApplyResultRecord record)
        {
            try
            {
                File.WriteAllText(
                    resultPath,
                    JsonConvert.SerializeObject(record, Formatting.Indented),
                    new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PSDLayoutTool2] 写入 Apply 回执失败：" + exception.Message);
            }
        }

        private static void DeleteIfExists(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // 忽略：旧回执删不掉时覆盖写仍可用。
            }
        }

        /// <summary>去掉 .apply/.applying/.applied/.apply-failed/.apply-uncertain 后缀。</summary>
        private static string StripStateExtension(string path)
        {
            string[] suffixes =
            {
                ApplyingExtension,
                AppliedExtension,
                UncertainExtension,
                FailedExtension,
                ApplyExtension,
            };
            foreach (string suffix in suffixes)
            {
                if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return path.Substring(0, path.Length - suffix.Length);
                }
            }

            return path;
        }

        private static void TryMove(string fromPath, string toPath)
        {
            try
            {
                if (!File.Exists(fromPath))
                {
                    return;
                }

                DeleteIfExists(toPath);
                File.Move(fromPath, toPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PSDLayoutTool2] 处理 Apply 哨兵文件失败：" + exception.Message);
            }
        }

        /// <summary>单测可观察：是否正在应用。</summary>
        internal static bool IsApplying => isApplying;

        /// <summary>单测可观察：当前处理中的抢占文件路径。</summary>
        internal static string InFlightApplyPath => inFlightApplyPath;
    }
}
