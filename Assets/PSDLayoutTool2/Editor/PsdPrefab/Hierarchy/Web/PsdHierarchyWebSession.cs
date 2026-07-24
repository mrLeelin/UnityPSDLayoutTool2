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

        internal PsdHierarchyOrganizerPreviewModel previewModel
        {
            get { lock (gate) { return previewModelValue; } }
        }

        public CancellationToken Start(PsdHierarchyWebOperationKind kind, string message)
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
                return cancellation.Token;
            }
        }

        public void Complete(string message)
        {
            Finish(PsdHierarchyWebOperationStatus.Succeeded, message);
        }

        public void Fail(string message)
        {
            Finish(PsdHierarchyWebOperationStatus.Failed, message);
        }

        public void Cancel(string message = "Cancelled.")
        {
            lock (gate)
            {
                ThrowIfDisposed();
                if (cancellation == null) return;
                cancellation.Cancel();
                cancellation.Dispose();
                cancellation = null;
                operationValue = NewIdleOperation(message);
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

        private void Finish(PsdHierarchyWebOperationStatus status, string message)
        {
            lock (gate)
            {
                ThrowIfDisposed();
                if (cancellation == null)
                    throw new InvalidOperationException("No session operation is running.");

                cancellation.Dispose();
                cancellation = null;
                operationValue.status = status;
                operationValue.message = message ?? string.Empty;
            }
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
