namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using PsdLayoutTool2.Editor;

    public sealed class PsdHierarchyWebApiTests
    {
        [Test]
        public void OpenUrlKeepsTheTokenInTheFragment()
        {
            using (var session = new PsdHierarchyWebSession(
                       "session123", "secret456789012345", "guid", "Assets/A.psd",
                       Path.Combine(Path.GetTempPath(), "PsdHierarchyWebApiTests", Guid.NewGuid().ToString("N")),
                       null))
            {
                string url = PsdHierarchyWebWorkbench.BuildOpenUrl(49152, session);

                Assert.That(url, Is.EqualTo(
                    "http://127.0.0.1:49152/open/session123#token=secret456789012345"));
                StringAssert.DoesNotContain("?token=", url);
            }
        }

        [Test]
        public void Api_ReturnsAuthenticatedSessionStatusAndBoundedClientErrors()
        {
            using (var session = new PsdHierarchyWebSession(
                       "known", "token", "guid", "Assets/A.psd",
                       Path.Combine(Path.GetTempPath(), "PsdHierarchyWebApiTests", Guid.NewGuid().ToString("N")),
                       null))
            {
                Type apiType = typeof(PsdHierarchyWebController).Assembly.GetType(
                    "PsdLayoutTool2.Editor.PsdHierarchyWebApi");
                Assert.That(apiType, Is.Not.Null, "The loopback server needs a concrete controller API adapter.");
                object api = Activator.CreateInstance(
                    apiType,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new object[] { new PsdHierarchyWebController(new ImmediateMainThread()) },
                    null);
                MethodInfo handle = apiType.GetMethod(
                    "Handle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(handle, Is.Not.Null);

                PsdHierarchyWebResponse info = Invoke(handle, api, Request("GET", "/session/known"), session);
                PsdHierarchyWebResponse status = Invoke(handle, api, Request("GET", "/session/known/status"), session);
                PsdHierarchyWebResponse malformed = Invoke(
                    handle,
                    api,
                    Request("POST", "/session/known/refine", "{"),
                    session);

                Assert.That(info.statusCode, Is.EqualTo(200));
                Assert.That(JObject.Parse(Encoding.UTF8.GetString(info.body))["sessionId"].Value<string>(),
                    Is.EqualTo("known"));
                Assert.That(status.statusCode, Is.EqualTo(200));
                Assert.That(JObject.Parse(Encoding.UTF8.GetString(status.body))["status"].Value<string>(),
                    Is.EqualTo("idle"));
                Assert.That(malformed.statusCode, Is.EqualTo(400));
                Assert.That(Encoding.UTF8.GetString(malformed.body).Length, Is.LessThan(1200));
            }
        }

        private static PsdHierarchyWebResponse Invoke(
            MethodInfo handle,
            object api,
            PsdHierarchyWebRequest request,
            PsdHierarchyWebSession session)
        {
            return (PsdHierarchyWebResponse)handle.Invoke(api, new object[] { request, session });
        }

        private static PsdHierarchyWebRequest Request(string method, string path, string body = "")
        {
            return new PsdHierarchyWebRequest(
                method,
                path,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Encoding.UTF8.GetBytes(body));
        }

        private sealed class ImmediateMainThread : IPsdHierarchyWebMainThread
        {
            public System.Threading.Tasks.Task InvokeAsync(
                Func<System.Threading.Tasks.Task> action) { return action(); }

            public System.Threading.Tasks.Task<TResult> InvokeAsync<TResult>(Func<TResult> action)
            {
                return System.Threading.Tasks.Task.FromResult(action());
            }
        }
    }
}
