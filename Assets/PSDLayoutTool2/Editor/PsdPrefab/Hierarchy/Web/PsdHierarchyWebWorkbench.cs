namespace PsdLayoutTool2.Editor
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    internal static class PsdHierarchyWebWorkbench
    {
        private static readonly object Gate = new object();
        private static PsdHierarchyWebSessionRegistry registry;
        private static PsdHierarchyWebMainThread mainThread;
        private static PsdHierarchyWebServer server;
        private static bool shuttingDown;

        static PsdHierarchyWebWorkbench()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting += Shutdown;
        }

        public static async void Open(
            PsdHierarchyOrganizerInput input,
            Action<PsdHierarchyPlan> applyHandler)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (applyHandler == null) throw new ArgumentNullException(nameof(applyHandler));
            try
            {
                EnsureStarted();
                PsdHierarchyWebSession session = await registry.GetOrCreateAsync(
                    input.sourcePsdGuid,
                    input.sourcePsdPath,
                    input.previewModel,
                    applyHandler);
                await mainThread.InvokeAsync(() =>
                {
                    PsdHierarchyCompositePreviewWriter.Write(input.sourcePsdPath, session.directory);
                    Application.OpenURL(BuildOpenUrl(server.port, session));
                    return Task.CompletedTask;
                });
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("PSDLayoutTool2", exception.Message, "OK");
            }
        }

        internal static string BuildOpenUrl(int port, PsdHierarchyWebSession session)
        {
            if (port <= 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            if (session == null) throw new ArgumentNullException(nameof(session));
            return "http://127.0.0.1:" + port + "/open/" + session.sessionId + "#token=" + session.token;
        }

        private static void EnsureStarted()
        {
            lock (Gate)
            {
                if (shuttingDown) throw new InvalidOperationException("The Unity Editor is shutting down.");
                if (server != null) return;
                mainThread = new PsdHierarchyWebMainThread();
                PsdHierarchyWebStaticAssets.WarmUp();
                registry = new PsdHierarchyWebSessionRegistry(
                    Path.Combine(Path.GetTempPath(), "PsdLayoutTool2", "HierarchyWebSessions"),
                    () => DateTime.UtcNow);
                registry.CleanupStaleDirectories();
                var controller = new PsdHierarchyWebController(mainThread);
                var api = new PsdHierarchyWebApi(controller);
                server = new PsdHierarchyWebServer(
                    new PsdHierarchyWebRouter(registry.FindBySessionId, api.Handle),
                    TimeSpan.FromSeconds(60),
                    diagnostic => Debug.LogError(
                        "PSD hierarchy web server error: " + diagnostic.exceptionType + Environment.NewLine +
                        diagnostic.stackTrace));
            }
        }

        private static void Shutdown()
        {
            lock (Gate)
            {
                if (shuttingDown) return;
                shuttingDown = true;
                server?.Dispose();
                registry?.Dispose();
                server = null;
                registry = null;
                mainThread = null;
            }
        }
    }
}
