namespace PsdLayoutTool2.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class PsdHierarchyWebController
    {
        private readonly object gate = new object();
        private readonly IPsdHierarchyWebMainThread mainThread;
        private readonly Dictionary<string, PsdHierarchyWebSnapshotDto> snapshots =
            new Dictionary<string, PsdHierarchyWebSnapshotDto>(StringComparer.Ordinal);

        public PsdHierarchyWebController(IPsdHierarchyWebMainThread mainThread)
        {
            this.mainThread = mainThread ?? throw new ArgumentNullException(nameof(mainThread));
        }

        public async Task<PsdHierarchyWebSessionDto> GetSessionAsync(PsdHierarchyWebSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            PsdHierarchyWebSessionSnapshot sessionSnapshot = session.Snapshot();
            return await session.UsePreviewAsync(model => mainThread.InvokeAsync(() =>
                new PsdHierarchyWebSessionDto
                {
                    sessionId = sessionSnapshot.sessionId,
                    sourcePsdName = Path.GetFileName(sessionSnapshot.sourcePsdPath),
                    targetPrefabName = model == null ? string.Empty : Path.GetFileName(model.targetPrefabPath),
                    canAnalyze = model != null && !model.isRunning &&
                                 string.IsNullOrEmpty(sessionSnapshot.resultingPrefabPath),
                    canApply = model != null && model.canApply &&
                               string.IsNullOrEmpty(sessionSnapshot.resultingPrefabPath),
                    canCreatePrefabs = !string.IsNullOrEmpty(sessionSnapshot.resultingPrefabPath),
                    phase = string.IsNullOrEmpty(sessionSnapshot.resultingPrefabPath) ? "organize" : "prefabReview",
                    resultingPrefabPath = sessionSnapshot.resultingPrefabPath,
                    operation = CloneOperation(sessionSnapshot.operation)
                }));
        }

        public async Task<PsdHierarchyWebSnapshotDto> GetSnapshotAsync(PsdHierarchyWebSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            lock (gate)
            {
                PsdHierarchyWebSnapshotDto snapshot;
                if (snapshots.TryGetValue(session.sessionId, out snapshot)) return snapshot;
            }
            return await RefreshSnapshotAsync(session);
        }

        public Task AnalyzeAsync(PsdHierarchyWebSession session)
        {
            return RunOperationAsync(
                session,
                PsdHierarchyWebOperationKind.Analyze,
                "AI is analyzing the complete PSD hierarchy.",
                (model, token) => model.ReplanAllUnlockedAsync(token));
        }

        public Task RefineAsync(PsdHierarchyWebSession session, PsdHierarchyWebRefineRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            List<string> stableIds = (request.stableIds ?? new List<string>())
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (stableIds.Count == 0) throw new ArgumentException("At least one stable ID is required.", nameof(request));
            return RunOperationAsync(
                session,
                PsdHierarchyWebOperationKind.Refine,
                "AI is reorganizing the selected PSD region.",
                (model, token) => model.RefineSelectionAsync(stableIds, request.instruction, token));
        }

        public Task AcceptAsync(PsdHierarchyWebSession session, PsdHierarchyWebAcceptRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            List<string> groupKeys = (request.groupKeys ?? new List<string>())
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (groupKeys.Count == 0) throw new ArgumentException("At least one group key is required.", nameof(request));
            return RunOperationAsync(
                session,
                PsdHierarchyWebOperationKind.Accept,
                request.isAccepted ? "Accepting selected groups." : "Unlocking selected groups.",
                (model, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    var available = new HashSet<string>(
                        (model.proposedPlan.groups ?? new List<PsdHierarchyPlanGroup>())
                            .Where(group => group != null)
                            .Select(group => group.key),
                        StringComparer.Ordinal);
                    if (groupKeys.Any(groupKey => !available.Contains(groupKey)))
                        throw new ArgumentException("An accepted group no longer exists.", nameof(request));
                    foreach (string groupKey in groupKeys) model.SetGroupAccepted(groupKey, request.isAccepted);
                    return Task.CompletedTask;
                });
        }

        public async Task ApplyAsync(PsdHierarchyWebSession session, PsdHierarchyWebApplyRequest request)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (request == null || !request.confirmed)
                throw new ArgumentException("Explicit apply confirmation is required.", nameof(request));
            if (session.Snapshot().operation.status == PsdHierarchyWebOperationStatus.Running)
                throw new InvalidOperationException("A session operation is already running.");

            PsdHierarchyPlan plan = await session.UsePreviewAsync(model => mainThread.InvokeAsync(() =>
            {
                if ((model.validationErrors ?? new List<string>()).Count > 0)
                    throw new InvalidOperationException(model.validationErrors[0]);
                var accepted = new HashSet<string>(model.acceptedGroupKeys, StringComparer.Ordinal);
                List<string> pendingGroups = (model.proposedPlan.groups ?? new List<PsdHierarchyPlanGroup>())
                    .Where(group => group != null && !accepted.Contains(group.key))
                    .Select(group => group.key)
                    .ToList();
                if (pendingGroups.Count > 0)
                    throw new InvalidOperationException("Accept every proposed group before applying the hierarchy.");
                PsdHierarchyPlan validated;
                string error;
                if (!model.TryCreateValidatedApplyPlan(out validated, out error))
                    throw new InvalidOperationException(string.IsNullOrEmpty(error)
                        ? "Hierarchy preview is not ready to apply."
                        : error);
                return validated;
            }));

            PsdHierarchyWebOperationLease lease = session.Start(
                PsdHierarchyWebOperationKind.Apply,
                "Applying validated naming and hierarchy changes.");
            try
            {
                await mainThread.InvokeAsync(() =>
                {
                    session.DispatchApply(plan);
                    return Task.CompletedTask;
                });
                string prefabPath = await session.UsePreviewAsync(model =>
                    mainThread.InvokeAsync(() => model.targetPrefabPath));
                session.RecordAppliedPrefab(prefabPath);
                await RefreshSnapshotAsync(session);
                session.Complete(lease, "Hierarchy applied. Review common Prefab candidates.");
            }
            catch (Exception exception)
            {
                session.Fail(lease, BoundedMessage(exception));
            }
            finally
            {
                lease.Dispose();
            }
        }

        public PsdHierarchyWebOperationState GetStatus(PsdHierarchyWebSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            return CloneOperation(session.Snapshot().operation);
        }

        private async Task RunOperationAsync(
            PsdHierarchyWebSession session,
            PsdHierarchyWebOperationKind kind,
            string message,
            Func<PsdHierarchyOrganizerPreviewModel, CancellationToken, Task> operation)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            PsdHierarchyWebOperationLease lease = session.Start(kind, message);
            try
            {
                await session.UsePreviewAsync(model =>
                    mainThread.InvokeAsync(() => operation(model, lease.token)));
                await RefreshSnapshotAsync(session);
                session.Complete(lease, "Operation completed.");
            }
            catch (OperationCanceledException) when (lease.token.IsCancellationRequested)
            {
                session.Cancel(lease);
            }
            catch (Exception exception)
            {
                session.Fail(lease, BoundedMessage(exception));
            }
            finally
            {
                lease.Dispose();
            }
        }

        private async Task<PsdHierarchyWebSnapshotDto> RefreshSnapshotAsync(PsdHierarchyWebSession session)
        {
            PsdHierarchyWebSnapshotDto snapshot = await session.UsePreviewAsync(model =>
                mainThread.InvokeAsync(() => PsdHierarchyWebSnapshotBuilder.Build(model)));
            lock (gate) snapshots[session.sessionId] = snapshot;
            return snapshot;
        }

        private static string BoundedMessage(Exception exception)
        {
            string message = exception == null || string.IsNullOrWhiteSpace(exception.Message)
                ? "The Unity operation failed."
                : exception.Message.Trim();
            return message.Length <= 1000 ? message : message.Substring(0, 1000);
        }

        private static PsdHierarchyWebOperationState CloneOperation(PsdHierarchyWebOperationState source)
        {
            source = source ?? new PsdHierarchyWebOperationState();
            return new PsdHierarchyWebOperationState
            {
                operationId = source.operationId,
                kind = source.kind,
                status = source.status,
                message = source.message
            };
        }
    }
}
