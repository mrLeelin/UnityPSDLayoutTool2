namespace PsdLayoutTool2.Editor
{
    using System;
    using System.Security.Cryptography;
    using System.Threading;

    /// <summary>
    /// In-memory ownership boundary for one PSD's loopback workbench state.
    /// Secrets and cancellation handles deliberately never enter a DTO or disk.
    /// </summary>
    internal sealed class PsdHierarchyWebSession : IDisposable
    {
        private readonly object gate = new object();
        private CancellationTokenSource cancellation;
        private PsdHierarchyWebOperationState operationValue = NewIdleOperation();
        private PsdHierarchyOrganizerPreviewModel previewModelValue;
        private bool disposed;

        public PsdHierarchyWebSession(
            string sessionId,
            string token,
            string sourcePsdGuid,
            string sourcePsdPath,
            string directory,
            PsdHierarchyOrganizerPreviewModel previewModel)
        {
            this.sessionId = Require(sessionId, "sessionId");
            this.token = Require(token, "token");
            this.sourcePsdGuid = Require(sourcePsdGuid, "sourcePsdGuid");
            this.sourcePsdPath = Require(sourcePsdPath, "sourcePsdPath");
            this.directory = Require(directory, "directory");
            previewModelValue = previewModel;
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
                if (cancellation != null)
                    throw new InvalidOperationException("A session operation is already running.");

                cancellation = new CancellationTokenSource();
                operationValue = new PsdHierarchyWebOperationState
                {
                    operationId = CreateSecret(12),
                    kind = kind,
                    status = PsdHierarchyWebOperationStatus.Running,
                    message = message ?? string.Empty
                };
                return new PsdHierarchyWebOperationLease(operationValue.operationId, cancellation.Token);
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
                cancellation.Cancel();
                cancellation.Dispose();
                cancellation = null;
                operationValue = NewIdleOperation(message);
            }
        }

        /// <summary>
        /// Runs synchronous work against the current preview while the session owns it.
        /// Callers must finish their model work inside this callback; they never receive a
        /// model reference that can outlive a replacement.
        /// </summary>
        public void UsePreview(Action<PsdHierarchyOrganizerPreviewModel> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            lock (gate)
            {
                ThrowIfDisposed();
                action(previewModelValue);
            }
        }

        public void ReplacePreview(PsdHierarchyOrganizerPreviewModel previewModel)
        {
            lock (gate)
            {
                ThrowIfDisposed();
                previewModelValue = previewModel;
            }
        }

        public PsdHierarchyWebSessionSnapshot Snapshot()
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return new PsdHierarchyWebSessionSnapshot(
                    sessionId, sourcePsdGuid, sourcePsdPath, directory, CloneOperation(operationValue));
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                if (cancellation != null)
                {
                    cancellation.Cancel();
                    cancellation.Dispose();
                    cancellation = null;
                }
                previewModelValue = null;
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

                cancellation.Dispose();
                cancellation = null;
                operationValue.status = status;
                operationValue.message = message ?? string.Empty;
            }
        }

        private bool IsCurrentOperation(PsdHierarchyWebOperationLease lease)
        {
            return !disposed &&
                lease != null &&
                cancellation != null &&
                string.Equals(operationValue.operationId, lease.operationId, StringComparison.Ordinal) &&
                cancellation.Token.Equals(lease.token);
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
    /// work that completes after cancellation cannot affect a later operation.
    /// </summary>
    internal sealed class PsdHierarchyWebOperationLease
    {
        internal PsdHierarchyWebOperationLease(string operationId, CancellationToken token)
        {
            this.operationId = operationId;
            this.token = token;
        }

        public string operationId { get; private set; }
        public CancellationToken token { get; private set; }
    }

    internal sealed class PsdHierarchyWebSessionSnapshot
    {
        internal PsdHierarchyWebSessionSnapshot(
            string sessionId,
            string sourcePsdGuid,
            string sourcePsdPath,
            string directory,
            PsdHierarchyWebOperationState operation)
        {
            this.sessionId = sessionId;
            this.sourcePsdGuid = sourcePsdGuid;
            this.sourcePsdPath = sourcePsdPath;
            this.directory = directory;
            this.operation = operation;
        }

        public string sessionId { get; private set; }
        public string sourcePsdGuid { get; private set; }
        public string sourcePsdPath { get; private set; }
        public string directory { get; private set; }
        public PsdHierarchyWebOperationState operation { get; private set; }
    }
}
