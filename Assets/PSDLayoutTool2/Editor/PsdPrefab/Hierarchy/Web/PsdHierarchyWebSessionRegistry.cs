namespace PsdLayoutTool2.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Owns live web sessions and the narrowly-scoped temporary directory root.
    /// </summary>
    internal sealed class PsdHierarchyWebSessionRegistry : IDisposable
    {
        private static readonly TimeSpan StaleAge = TimeSpan.FromDays(7);
        private readonly object gate = new object();
        private readonly Dictionary<string, PsdHierarchyWebSession> sessions =
            new Dictionary<string, PsdHierarchyWebSession>(StringComparer.Ordinal);
        private readonly Func<DateTime> utcNow;
        private readonly Action<string> deleteDirectory;
        private bool disposed;

        public PsdHierarchyWebSessionRegistry(string rootDirectory, Func<DateTime> utcNow)
            : this(rootDirectory, utcNow, path => Directory.Delete(path, true))
        {
        }

        public PsdHierarchyWebSessionRegistry(
            string rootDirectory,
            Func<DateTime> utcNow,
            Action<string> deleteDirectory)
        {
            if (utcNow == null) throw new ArgumentNullException(nameof(utcNow));
            if (deleteDirectory == null) throw new ArgumentNullException(nameof(deleteDirectory));
            root = CanonicalizeRoot(rootDirectory);
            this.utcNow = utcNow;
            this.deleteDirectory = deleteDirectory;
        }

        public string root { get; private set; }

        public PsdHierarchyWebSession GetOrCreate(
            string sourcePsdGuid,
            string sourcePsdPath,
            PsdHierarchyOrganizerPreviewModel previewModel)
        {
            if (string.IsNullOrWhiteSpace(sourcePsdGuid))
                throw new ArgumentException("PSD GUID is required.", nameof(sourcePsdGuid));
            if (string.IsNullOrWhiteSpace(sourcePsdPath))
                throw new ArgumentException("PSD path is required.", nameof(sourcePsdPath));

            lock (gate)
            {
                ThrowIfDisposed();
                PsdHierarchyWebSession existing;
                if (sessions.TryGetValue(sourcePsdGuid, out existing))
                {
                    existing.ReplacePreview(previewModel);
                    return existing;
                }

                string sessionId = PsdHierarchyWebSession.CreateSecret(16);
                string directory = Path.Combine(root, sessionId);
                EnsureDirectChild(directory);
                if (Directory.Exists(directory) && IsReparsePoint(directory))
                    throw new IOException("Session directory must not be a reparse point.");
                Directory.CreateDirectory(directory);
                if (IsReparsePoint(directory))
                    throw new IOException("Session directory must not be a reparse point.");
                var session = new PsdHierarchyWebSession(
                    sessionId,
                    PsdHierarchyWebSession.CreateSecret(32),
                    sourcePsdGuid,
                    sourcePsdPath,
                    directory,
                    previewModel);
                sessions.Add(sourcePsdGuid, session);
                return session;
            }
        }

        public void CleanupStaleDirectories()
        {
            ThrowIfDisposed();
            DateTime cutoff = utcNow().ToUniversalTime().Subtract(StaleAge);
            foreach (string directory in Directory.EnumerateDirectories(root).ToArray())
            {
                try
                {
                    if (!IsRecognizedSessionDirectory(directory) ||
                        Directory.GetLastWriteTimeUtc(directory) >= cutoff)
                        continue;
                    EnsureDirectChild(directory);
                    if (IsReparsePoint(directory)) continue;
                    deleteDirectory(directory);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                foreach (PsdHierarchyWebSession session in sessions.Values) session.Dispose();
                sessions.Clear();
            }
        }

        private static string CanonicalizeRoot(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("Session root is required.", nameof(rootDirectory));
            string canonical = Path.GetFullPath(rootDirectory);
            string trimmed = canonical.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string volumeRoot = Path.GetPathRoot(canonical)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(trimmed, volumeRoot, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Session root must not be a filesystem root.");
            Directory.CreateDirectory(canonical);
            EnsureNoReparsePoints(canonical);
            return trimmed;
        }

        private void EnsureDirectChild(string path)
        {
            string canonical = Path.GetFullPath(path);
            string parent = Path.GetDirectoryName(canonical);
            if (!string.Equals(parent, root, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Session directory escaped the configured root.");
        }

        private bool IsRecognizedSessionDirectory(string path)
        {
            string name = Path.GetFileName(path);
            if (name.Length < 16 || name.Length > 64 || (name.Length & 1) != 0) return false;
            return name.All(character =>
                (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'));
        }

        private static bool IsReparsePoint(string path)
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }

        private static void EnsureNoReparsePoints(string path)
        {
            var current = new DirectoryInfo(path);
            while (current != null)
            {
                if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Session root path must not traverse a reparse point.");
                current = current.Parent;
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(PsdHierarchyWebSessionRegistry));
        }
    }
}
