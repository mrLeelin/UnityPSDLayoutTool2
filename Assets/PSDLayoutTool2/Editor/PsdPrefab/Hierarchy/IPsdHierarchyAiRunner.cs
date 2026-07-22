namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// 只负责产生层级“建议”，不持有任何 Unity 写入能力。
    /// Keeping the planner behind this interface also lets EditMode tests prove
    /// incremental call counts without launching a real external process.
    /// </summary>
    public interface IPsdHierarchyAiRunner
    {
        Task<PsdHierarchyAiRunResult> RunAsync(
            PsdHierarchyAiRunRequest request,
            CancellationToken cancellationToken);
    }

    public sealed class PsdHierarchyAiRunRequest
    {
        public string operationId;
        public PsdHierarchyRequest request;
        public string targetPrefabPath;
        public TimeSpan timeout = TimeSpan.FromMinutes(2);
        public List<string> modifiableStableIds = new List<string>();
        public List<string> contextStableIds = new List<string>();
        public List<string> modifiableGroupKeys = new List<string>();
        public List<PsdHierarchyPlanGroup> baselineGroups = new List<PsdHierarchyPlanGroup>();
    }

    public sealed class PsdHierarchyAiRunResult
    {
        public bool succeeded;
        public PsdHierarchyPlan plan;
        public string error = string.Empty;
        public string standardOutput = string.Empty;
        public string standardError = string.Empty;
        public string requestPackagePath = string.Empty;
        public bool offlinePackageAvailable;
    }

    /// <summary>
    /// A process invocation is represented as discrete arguments. Callers never
    /// construct a shell command, so PSD names and paths cannot become commands.
    /// </summary>
    public sealed class PsdHierarchyProcessInvocation
    {
        public string executable;
        public List<string> arguments = new List<string>();
        public string workingDirectory;
        public string standardInput;
        public string OutputPath;
        public bool useShellExecute;
    }

    public sealed class PsdHierarchyProcessResult
    {
        public int exitCode;
        public string standardOutput = string.Empty;
        public string standardError = string.Empty;
        public string error = string.Empty;
        public bool timedOut;
        public bool wasKilled;
        public bool outputLimitExceeded;
        public bool processTreeKilled;
        public bool waitForExitSucceeded;
    }

    public sealed class PsdHierarchyProcessCancelledException : OperationCanceledException
    {
        public PsdHierarchyProcessCancelledException(
            string message,
            bool processTreeKilled,
            bool waitForExitSucceeded,
            CancellationToken cancellationToken)
            : base(message, cancellationToken)
        {
            this.processTreeKilled = processTreeKilled;
            this.waitForExitSucceeded = waitForExitSucceeded;
        }

        public bool processTreeKilled { get; private set; }
        public bool waitForExitSucceeded { get; private set; }
    }

    /// <summary>
    /// Injected boundary around System.Diagnostics.Process. Tests can simulate
    /// timeout, cancellation and CLI failures without spawning another process.
    /// </summary>
    public interface IHierarchyProcessAdapter
    {
        Task<PsdHierarchyProcessResult> RunAsync(
            PsdHierarchyProcessInvocation invocation,
            TimeSpan timeout,
            CancellationToken cancellationToken);
    }
}
