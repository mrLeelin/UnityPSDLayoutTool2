namespace PsdLayoutTool2.Editor
{
    using System;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// In-memory ownership boundary for one PSD's loopback workbench state.
    /// Secrets and cancellation handles deliberately never enter a DTO or disk.
    /// </summary>
    internal sealed class PsdHierarchyWebSession : IDisposable
    {
        private readonly object gate = new object();
        private readonly SemaphoreSlim previewGate = new SemaphoreSlim(1, 1);
        private PsdHierarchyWebOperationLease currentLease;
        private PsdHierarchyWebOperationState operationValue = NewIdleOperation();
        private PsdHierarchyOrganizerPreviewModel previewModelValue;
        private readonly Action<PsdHierarchyPlan> applyHandler;
        private string resultingPrefabPathValue = string.Empty;
        private long previewGeneration;
        private bool disposed;

        public PsdHierarchyWebSession(
            string sessionId,
            string token,
            string sourcePsdGuid,
            string sourcePsdPath,
            string directory,
            PsdHierarchyOrganizerPreviewModel previewModel,
            Action<PsdHierarchyPlan> applyHandler = null)
        {
            this.sessionId = Require(sessionId, "sessionId");
            this.token = Require(token, "token");
            this.sourcePsdGuid = Require(sourcePsdGuid, "sourcePsdGuid");
            this.sourcePsdPath = Require(sourcePsdPath, "sourcePsdPath");
            this.directory = Require(directory, "directory");
            previewModelValue = previewModel;
            this.applyHandler = applyHandler;
        }

        public string sessionId { get; private set; }
        public string token { get; private set; }
        public string sourcePsdGuid { get; private set; }
        public string sourcePsdPath { get; private set; }
        public string directory { get; private set; }

        public PsdHierarchyWebOperationLease Start(PsdHierarchyWebOperationKind kind, string message)
        {
            if (kind == PsdHierarchyWebOperationKind.None)
                throw new ArgumentOutOfRangeException("kind");

            lock (gate)
            {
                ThrowIfDisposed();
                if (currentLease != null)
                    throw new InvalidOperationException("A session operation is already running.");

                operationValue = new PsdHierarchyWebOperationState
                {
                    operationId = CreateSecret(12),
                    kind = kind,
                    status = PsdHierarchyWebOperationStatus.Running,
                    message = message ?? string.Empty
                };
                currentLease = new PsdHierarchyWebOperationLease(operationValue.operationId);
                return currentLease;
            }
        }

        public void Complete(PsdHierarchyWebOperationLease lease, string message)
        {
            Finish(lease, PsdHierarchyWebOperationStatus.Succeeded, message);
        }

        public void Fail(PsdHierarchyWebOperationLease lease, string message)
        {
            Finish(lease, PsdHierarchyWebOperationStatus.Failed, message);
        }

        public void Cancel(PsdHierarchyWebOperationLease lease, string message = "Cancelled.")
        {
            lock (gate)
            {
                if (!IsCurrentOperation(lease)) return;
                currentLease.RequestCancellation();
                currentLease = null;
                operationValue = NewIdleOperation(message);
            }
        }

        /// <summary>
        /// Runs one complete asynchronous operation against the current preview. The delegate
        /// must represent the full operation; do not retain the model after its returned task
        /// completes. Replacement waits for this task before changing the active preview.
        /// </summary>
        public async Task UsePreviewAsync(Func<PsdHierarchyOrganizerPreviewModel, Task> operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            await UsePreviewAsync(async preview =>
            {
                await operation(preview).ConfigureAwait(false);
                return true;
            }).ConfigureAwait(false);
        }

        public async Task<TResult> UsePreviewAsync<TResult>(
            Func<PsdHierarchyOrganizerPreviewModel, Task<TResult>> operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            await previewGate.WaitAsync().ConfigureAwait(false);
            try
            {
                PsdHierarchyOrganizerPreviewModel preview;
                long generation;
                lock (gate)
                {
                    ThrowIfDisposed();
                    preview = previewModelValue;
                    generation = previewGeneration;
                }

                TResult result = await operation(preview).ConfigureAwait(false);
                lock (gate)
                {
                    if (disposed || generation != previewGeneration)
                        throw new InvalidOperationException("The preview changed before the operation completed.");
                }
                return result;
            }
            finally
            {
                previewGate.Release();
            }
        }

        public async Task ReplacePreviewAsync(PsdHierarchyOrganizerPreviewModel previewModel)
        {
            await previewGate.WaitAsync().ConfigureAwait(false);
            try
            {
                lock (gate)
                {
                    ThrowIfDisposed();
                    previewModelValue = previewModel;
                    previewGeneration++;
                }
            }
            finally
            {
                previewGate.Release();
            }
        }

        public PsdHierarchyWebSessionSnapshot Snapshot()
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return new PsdHierarchyWebSessionSnapshot(
                    sessionId, sourcePsdGuid, sourcePsdPath, directory,
                    resultingPrefabPathValue, CloneOperation(operationValue));
            }
        }

        public void DispatchApply(PsdHierarchyPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            Action<PsdHierarchyPlan> handler;
            lock (gate)
            {
                ThrowIfDisposed();
                handler = applyHandler;
            }
            if (handler == null) throw new InvalidOperationException("This workbench session has no hierarchy apply handler.");
            handler(plan);
        }

        public void RecordAppliedPrefab(string prefabPath)
        {
            lock (gate)
            {
                ThrowIfDisposed();
                resultingPrefabPathValue = Require(prefabPath, "prefabPath");
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                if (currentLease != null) currentLease.RequestCancellation();
                currentLease = null;
                previewModelValue = null;
                previewGeneration++;
            }
        }

        private void Finish(
            PsdHierarchyWebOperationLease lease,
            PsdHierarchyWebOperationStatus status,
            string message)
        {
            lock (gate)
            {
                if (!IsCurrentOperation(lease)) return;

                currentLease = null;
                operationValue.status = status;
                operationValue.message = message ?? string.Empty;
            }
        }

        private bool IsCurrentOperation(PsdHierarchyWebOperationLease lease)
        {
            return !disposed &&
                lease != null &&
                currentLease != null &&
                string.Equals(operationValue.operationId, lease.operationId, StringComparison.Ordinal) &&
                ReferenceEquals(currentLease, lease);
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(PsdHierarchyWebSession));
        }

        private static PsdHierarchyWebOperationState NewIdleOperation(string message = "")
        {
            return new PsdHierarchyWebOperationState { message = message ?? string.Empty };
        }

        private static PsdHierarchyWebOperationState CloneOperation(PsdHierarchyWebOperationState source)
        {
            return new PsdHierarchyWebOperationState
            {
                operationId = source.operationId,
                kind = source.kind,
                status = source.status,
                message = source.message
            };
        }

        internal static string CreateSecret(int byteCount)
        {
            var bytes = new byte[byteCount];
            RandomNumberGenerator.Fill(bytes);
            var characters = new char[bytes.Length * 2];
            const string Hex = "0123456789abcdef";
            for (int index = 0; index < bytes.Length; index++)
            {
                characters[index * 2] = Hex[bytes[index] >> 4];
                characters[index * 2 + 1] = Hex[bytes[index] & 15];
            }
            return new string(characters);
        }

        private static string Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", name);
            return value;
        }
    }

    /// <summary>
    /// Identifies one running operation. Terminal callbacks must present this lease so
    /// work that completes after cancellation cannot affect a later operation. The worker
    /// owns this lease and must release it from its finally block after it stops using token.
    /// </summary>
    internal sealed class PsdHierarchyWebOperationLease : IDisposable
    {
        private readonly object gate = new object();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private bool released;

        internal PsdHierarchyWebOperationLease(string operationId)
        {
            this.operationId = operationId;
            token = cancellation.Token;
        }

        public string operationId { get; private set; }
        public CancellationToken token { get; private set; }

        internal void RequestCancellation()
        {
            lock (gate)
            {
                if (!released) cancellation.Cancel();
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (released) return;
                released = true;
                cancellation.Dispose();
            }
        }
    }

    internal sealed class PsdHierarchyWebSessionSnapshot
    {
        internal PsdHierarchyWebSessionSnapshot(
            string sessionId,
            string sourcePsdGuid,
            string sourcePsdPath,
            string directory,
            string resultingPrefabPath,
            PsdHierarchyWebOperationState operation)
        {
            this.sessionId = sessionId;
            this.sourcePsdGuid = sourcePsdGuid;
            this.sourcePsdPath = sourcePsdPath;
            this.directory = directory;
            this.resultingPrefabPath = resultingPrefabPath;
            this.operation = operation;
        }

        public string sessionId { get; private set; }
        public string sourcePsdGuid { get; private set; }
        public string sourcePsdPath { get; private set; }
        public string directory { get; private set; }
        public string resultingPrefabPath { get; private set; }
        public PsdHierarchyWebOperationState operation { get; private set; }
    }
}
