namespace PsdLayoutTool2.Editor
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    internal sealed class PsdHierarchyWebApi
    {
        private readonly PsdHierarchyWebController controller;

        public PsdHierarchyWebApi(PsdHierarchyWebController controller)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        public PsdHierarchyWebResponse Handle(
            PsdHierarchyWebRequest request,
            PsdHierarchyWebSession session)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (session == null) throw new ArgumentNullException(nameof(session));
            string suffix = GetSuffix(request.path, session.sessionId);
            try
            {
                if (string.Equals(request.method, "GET", StringComparison.Ordinal))
                    return HandleGet(suffix, session);
                if (string.Equals(request.method, "POST", StringComparison.Ordinal))
                    return HandlePost(suffix, request.body, session);
                return PsdHierarchyWebResponse.Empty(405);
            }
            catch (JsonException exception)
            {
                return Error(400, exception.Message);
            }
            catch (ArgumentException exception)
            {
                return Error(400, exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return Error(409, exception.Message);
            }
        }

        private PsdHierarchyWebResponse HandleGet(string suffix, PsdHierarchyWebSession session)
        {
            switch (suffix)
            {
                case "":
                    return Json(controller.GetSessionAsync(session).GetAwaiter().GetResult());
                case "/snapshot":
                    return Json(controller.GetSnapshotAsync(session).GetAwaiter().GetResult());
                case "/status":
                    return Json(controller.GetStatus(session));
                case "/prefab-candidates":
                    return Json(new
                    {
                        prefabCandidates = controller.GetSnapshotAsync(session).GetAwaiter().GetResult().prefabCandidates
                    });
                case "/composite.png":
                    string path = Path.Combine(session.directory, "composite.png");
                    return File.Exists(path)
                        ? PsdHierarchyWebResponse.Png(File.ReadAllBytes(path))
                        : PsdHierarchyWebResponse.Empty(404);
                default:
                    return PsdHierarchyWebResponse.Empty(404);
            }
        }

        private PsdHierarchyWebResponse HandlePost(
            string suffix,
            byte[] body,
            PsdHierarchyWebSession session)
        {
            switch (suffix)
            {
                case "/analyze":
                    StartBackground(controller.AnalyzeAsync(session));
                    break;
                case "/refine":
                    StartBackground(controller.RefineAsync(
                        session, Deserialize<PsdHierarchyWebRefineRequest>(body)));
                    break;
                case "/accept":
                    StartBackground(controller.AcceptAsync(
                        session, Deserialize<PsdHierarchyWebAcceptRequest>(body)));
                    break;
                case "/apply":
                    controller.ApplyAsync(
                        session, Deserialize<PsdHierarchyWebApplyRequest>(body)).GetAwaiter().GetResult();
                    break;
                case "/create-prefabs":
                    controller.CreatePrefabsAsync(
                        session, Deserialize<PsdHierarchyWebCreatePrefabsRequest>(body)).GetAwaiter().GetResult();
                    break;
                default:
                    return PsdHierarchyWebResponse.Empty(404);
            }
            return Json(controller.GetStatus(session));
        }

        private static void StartBackground(Task operation)
        {
            if (operation == null) throw new InvalidOperationException("Unity did not start the operation.");
            if (operation.IsCompleted)
            {
                operation.GetAwaiter().GetResult();
                return;
            }
            operation.ContinueWith(completed =>
            {
                if (completed.Exception != null)
                    Trace.WriteLine("PSD hierarchy web operation failed: " + completed.Exception.GetBaseException().GetType().FullName);
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private static T Deserialize<T>(byte[] body) where T : class
        {
            if (body == null || body.Length == 0)
                throw new ArgumentException("A JSON request body is required.");
            T value = JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(body));
            if (value == null) throw new ArgumentException("The JSON request body is invalid.");
            return value;
        }

        private static PsdHierarchyWebResponse Json(object value)
        {
            return PsdHierarchyWebResponse.Json(JsonConvert.SerializeObject(value));
        }

        private static PsdHierarchyWebResponse Error(int statusCode, string message)
        {
            message = string.IsNullOrWhiteSpace(message) ? "The request could not be completed." : message.Trim();
            if (message.Length > 1000) message = message.Substring(0, 1000);
            return PsdHierarchyWebResponse.Json(
                statusCode,
                JsonConvert.SerializeObject(new { error = message }));
        }

        private static string GetSuffix(string path, string sessionId)
        {
            string prefix = "/session/" + sessionId;
            if (!string.Equals(path, prefix, StringComparison.Ordinal) &&
                !path.StartsWith(prefix + "/", StringComparison.Ordinal))
                throw new ArgumentException("The request path does not match the session.");
            return path.Substring(prefix.Length);
        }
    }
}
