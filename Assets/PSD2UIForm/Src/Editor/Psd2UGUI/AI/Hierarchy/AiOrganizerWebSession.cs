using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AiHierarchyAnalysisOrchestratorNamespace;
using AiJobFileStoreNamespace;
using AiPathUtilityNamespace;
using IAiJobListenerNamespace;
using UnityEditor;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable] internal sealed class AiOrganizerWebCommand
    {
        public string sessionId, action, feedback, nodeId, destination;
        public int revision, version;
    }

    [Serializable] internal sealed class AiOrganizerWebNode
    {
        public string id, name, type;
        public int depth;
        public RectData bounds;
    }

    [Serializable] internal sealed class AiOrganizerWebVersion
    {
        public int id;
        public string label;
    }

    [Serializable] internal sealed class AiOrganizerWebMessage
    {
        public string role, text;
    }

    [Serializable] internal sealed class AiOrganizerWebState
    {
        public string sessionId, sourceName, destination, status = "准备就绪", error = "", previewUrl = "", publishedPath = "";
        public string lastRequestId = "", commandError = "";
        public int revision, activeVersion;
        public bool running, canApply, sourceChanged;
        public List<AiOrganizerWebNode> nodes = new List<AiOrganizerWebNode>();
        public List<AiOrganizerWebVersion> versions = new List<AiOrganizerWebVersion>();
        public List<AiOrganizerWebMessage> messages = new List<AiOrganizerWebMessage>();
    }

    /// <summary>主线程拥有整理会话；HTTP 层只读取快照、排队命令。</summary>
    internal sealed class AiOrganizerWebSession : IAiJobListener, IDisposable
    {
        internal readonly AiOrganizerWebState State = new AiOrganizerWebState();
        internal byte[] PreviewPng { get; private set; }
        internal Psd2UIFormConverter Source { get; }
        readonly Dictionary<int, AiPatchDocument> _plans = new Dictionary<int, AiPatchDocument>();
        AiJobContext _job;
        AiAnalysisPackageDocument _package;
        AiPatchDocument _patch;
        AiOrganizerPreview _preview;
        string _fingerprint, _sourcePath, _pendingLabel;
        byte[] _sourceBytes;
        bool _disposed;
        int _nextVersion;

        internal AiOrganizerWebSession(Psd2UIFormConverter source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Source = source;
            State.sessionId = Guid.NewGuid().ToString("N");
            State.sourceName = source.uiFormName;
            string output = ScriptableSingleton<Psd2UIFormSettings>.Instance.UIFormOutputDir;
            if (string.IsNullOrEmpty(output) || !AssetDatabase.IsValidFolder(output)) output = "Assets";
            State.destination = AssetDatabase.GenerateUniqueAssetPath(output.TrimEnd('/') + "/" + source.uiFormName + "_Organized");
            AddMessage("assistant", "点击开始整理。AI 将分析当前界面并自动生成 Unity 预览；之后直接描述修改意见即可。版本保留在本次编辑器会话中，重新编译或关闭 Unity 会结束会话。");
        }

        internal void Execute(AiOrganizerWebCommand command)
        {
            RequireCurrent(State, command);
            State.error = "";
            switch (command.action)
            {
                case "start":
                case "revise":
                    if (!string.IsNullOrEmpty(State.publishedPath)) throw new InvalidOperationException("此会话已采用结果，请从编辑树重新打开工作台。");
                    if (command.action == "revise" && _patch == null) throw new InvalidOperationException("请先完成首次整理。");
                    Start(command); break;
                case "cancel":
                    var job = _job; _job = null;
                    State.running = false; State.revision++;
                    if (job != null) AiHierarchyAnalysisOrchestrator.CancelOrganizerJob(job);
                    State.status = "已取消，保留上一版";
                    State.canApply = _preview != null && !State.sourceChanged;
                    break;
                case "restore":
                    CheckInput();
                    if (!_plans.TryGetValue(command.version, out var plan)) throw new InvalidOperationException("版本不存在。");
                    BuildCandidate(Clone(plan), _package);
                    State.activeVersion = command.version; State.revision++;
                    State.status = "已恢复 V" + command.version;
                    AddMessage("assistant", State.status + "，接下来的修改会基于这个版本。");
                    break;
                case "apply":
                    CheckInput();
                    if (_preview == null || !State.canApply) throw new InvalidOperationException("请先生成可用预览。");
                    _preview.Publish(Psd2UIFormConverterEditor.GetOrCreate(Source));
                    State.publishedPath = _preview.TargetPath;
                    _preview.Dispose(); _preview = null;
                    State.canApply = false; State.revision++; State.status = "已采用并保存";
                    AddMessage("assistant", "已保存到 " + State.publishedPath + "。源编辑树也已按当前方案更新。");
                    break;
                default: throw new InvalidOperationException("未知操作。");
            }
        }

        internal static void RequireCurrent(AiOrganizerWebState state, AiOrganizerWebCommand command)
        {
            if (command == null || command.sessionId != state.sessionId || command.revision != state.revision)
                throw new InvalidOperationException("页面版本已过期，请等待刷新后重试。");
            if (state.running && command.action != "cancel") throw new InvalidOperationException("AI 正在处理，请等待完成或取消。");
            if (!state.running && command.action == "cancel") throw new InvalidOperationException("当前没有运行中的任务。");
            if (!string.IsNullOrEmpty(state.publishedPath)) throw new InvalidOperationException("此会话已采用结果，请重新打开工作台。");
        }

        void Start(AiOrganizerWebCommand command)
        {
            bool revise = _patch != null;
            if (revise) CheckInput();
            string feedback = (command.feedback ?? "").Trim();
            if (revise && feedback.Length == 0) throw new InvalidOperationException("请描述希望怎样修改。");
            if (!string.IsNullOrEmpty(command.nodeId))
            {
                var node = State.nodes.SingleOrDefault(n => n.id == command.nodeId);
                if (node == null) throw new InvalidOperationException("选中节点已不存在，请重新选择。");
                feedback = "选中节点 ID: " + node.id + "，名称: " + node.name + "\n" + feedback;
            }
            if (!revise)
            {
                CaptureInput();
            }
            if (!string.IsNullOrWhiteSpace(command.destination) && command.destination != State.destination)
            {
                if (revise) throw new InvalidOperationException("整理开始后不能修改输出目录，请重新打开会话。");
                State.destination = command.destination.Trim();
            }
            AiOrganizerPreview.ValidateDestination(State.destination);
            string project = Directory.GetParent(Application.dataPath).FullName;
            string instructions = File.ReadAllText(AiPathUtility.ResolvePluginRelativePath(project, "AIPrompts/UIOrganizer/SKILL.md"));
            instructions += revise ? AiOrganizerPlanEditing.RevisionInstructions(_patch, feedback) : "\nUser organization requirements:\n" + feedback;
            instructions += "\nExecute this organization task now using the supplied snapshot and images. This is an automated, non-interactive request; do not ask for confirmation. " +
                "Return the complete combined recognition JSON, not a readiness message. Preserving the existing hierarchy does not mean omitting owners: independent Image/Text controls still need their existing nodes as owner carriers.";
            _pendingLabel = revise ? feedback : "AI 首次整理";
            AddMessage("user", feedback.Length == 0 ? "开始整理整个界面" : feedback);
            State.running = true; State.canApply = false; State.revision++; State.status = "AI 正在分析";
            try
            {
                if (!AiHierarchyAnalysisOrchestrator.StartRecognitionJob(Psd2UIFormConverterEditor.GetOrCreate(Source), out string error,
                    this, true, instructions, job => { _job = job; if (!revise) _fingerprint = PsdExtractionSourceFingerprint.Capture(Source.gameObject); })) throw new InvalidOperationException(error);
            }
            catch (Exception ex) { Fail(ex.Message); }
        }

        public void OnJobCompleted(AiJobContext job)
        {
            if (_disposed || !State.running || _job == null || job.JobId != _job.JobId) return;
            try
            {
                CheckInput(); State.status = "Unity 正在生成完整预览";
                if (!AiJobFileStore.TryReadJson<AiRecognitionCombinedResultDocument>(job.RecognitionCombinedPath, out var combined) || combined == null || combined.organizerVersion != "1.0")
                    throw new InvalidOperationException("AI 未返回完整整理协议，请查看 Unity 任务日志。");
                if (!AiJobFileStore.TryReadJson<AiPatchDocument>(job.PatchPath, out var patch) ||
                    !AiJobFileStore.TryReadJson<AiAnalysisPackageDocument>(job.AnalysisPackagePath, out var package))
                    throw new InvalidOperationException("缺少整理方案或分析快照。");
                AcceptResult(patch, package, _pendingLabel);
                State.running = false; _job = null;
            }
            catch (Exception ex) { Fail(ex.Message); }
        }

        // 与真实 AI 回调共用这条路径，便于用保存的结果验证预览、版本和失败保留。
        internal void AcceptResult(AiPatchDocument patch, AiAnalysisPackageDocument package, string label)
        {
            CheckInput();
            if (_package != null && package.treeHash != _package.treeHash)
                throw new InvalidOperationException("分析快照已变化，不能将修订结果混入旧会话。");
            BuildCandidate(patch, package);
            int id = ++_nextVersion;
            _plans.Add(id, Clone(patch));
            State.versions.Add(new AiOrganizerWebVersion { id = id, label = label.Length > 55 ? label.Substring(0, 55) + "…" : label });
            State.activeVersion = id; State.revision++;
            State.status = "V" + id + " 预览已生成";
            AddMessage("assistant", "V" + id + " 已生成：" + patch.operations.Count + " 项整理操作，" + (patch.components?.Count ?? 0) + " 组公共组件。结构与提取校验已通过，请查看真实画面；可以继续描述修改。");
        }

        void BuildCandidate(AiPatchDocument patch, AiAnalysisPackageDocument package)
        {
            AiOrganizerPreview candidate = null;
            try
            {
                var nodes = AiOrganizerPlanEditing.Tree(package, patch).Select(n => new AiOrganizerWebNode { id = n.Id, name = n.Name, type = n.Type, depth = n.Depth }).ToList();
                candidate = AiOrganizerPreview.Build(Psd2UIFormConverterEditor.GetOrCreate(Source), patch, package, State.destination, _fingerprint, _sourceBytes);
                byte[] png = AiOrganizerWebPreview.Capture(candidate, package, nodes);
                _preview?.Dispose(); _preview = candidate; candidate = null;
                _patch = Clone(patch); _package = package;
                PreviewPng = png; State.nodes = nodes; State.canApply = true;
                State.previewUrl = "/preview.png?v=" + Guid.NewGuid().ToString("N");
            }
            finally { candidate?.Dispose(); }
        }

        internal void CaptureInput()
        {
            _sourcePath = PsdCommonPrefabPersistence.SourcePath(Source.gameObject);
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (string.IsNullOrEmpty(_sourcePath) || (stage != null && stage.assetPath == _sourcePath && stage.scene.isDirty))
                throw new InvalidOperationException("请先保存 PSD 编辑树，再开始整理。");
            _sourceBytes = File.ReadAllBytes(_sourcePath);
            _fingerprint = PsdExtractionSourceFingerprint.Capture(Source.gameObject);
            State.sourceChanged = false;
        }

        internal void CheckInput()
        {
            if (Source == null || string.IsNullOrEmpty(_sourcePath) ||
                !File.Exists(_sourcePath) || !_sourceBytes.SequenceEqual(File.ReadAllBytes(_sourcePath)) ||
                _fingerprint != PsdExtractionSourceFingerprint.Capture(Source.gameObject))
            {
                State.sourceChanged = true; State.canApply = false;
                throw new InvalidOperationException("源编辑树或配置已变化。请从 Unity 重新打开工作台并分析。");
            }
        }

        internal void Tick()
        {
            if (State.running && _job != null && AiJobFileStore.TryReadJson<AiJobStatusDocument>(_job.StatusPath, out var status) && status != null)
                State.status = status.stage + " · " + status.detail;
        }

        internal void Fail(string error)
        {
            State.running = false; _job = null; State.error = error; State.status = "未完成，已保留上一版";
            State.canApply = _preview != null && !State.sourceChanged; State.revision++;
            AddMessage("assistant", error);
        }

        public void OnJobFailed(AiJobContext job, string message)
        {
            if (!_disposed && State.running && _job != null && job.JobId == _job.JobId) Fail(message);
        }

        void AddMessage(string role, string text) { State.messages.Add(new AiOrganizerWebMessage { role = role, text = text }); }
        static AiPatchDocument Clone(AiPatchDocument value) { return JsonUtility.FromJson<AiPatchDocument>(JsonUtility.ToJson(value)); }

        public void Dispose()
        {
            _disposed = true;
            if (State.running && _job != null) AiHierarchyAnalysisOrchestrator.CancelOrganizerJob(_job);
            State.running = false; _job = null;
            _preview?.Dispose(); _preview = null;
        }
    }
}
