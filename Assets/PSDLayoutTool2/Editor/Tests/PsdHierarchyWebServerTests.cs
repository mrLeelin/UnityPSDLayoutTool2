namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net.Sockets;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using PsdLayoutTool2.Editor;

    public sealed class PsdHierarchyWebServerTests
    {
        [Test]
        public async Task Server_BindsLoopbackOnRandomPort_AndClosesConnection()
        {
            using (var server = CreateServer())
            {
                Assert.That(server.port, Is.GreaterThan(0));
                Assert.That(server.port, Is.Not.EqualTo(80));
                RawResponse response = await SendAsync(server.port, Request(server.port, "/sessions/known/data", "token"));
                Assert.That(response.statusCode, Is.EqualTo(200));
                Assert.That(response.headers["Connection"], Is.EqualTo("close"));
            }
        }

        [TestCase(null)]
        [TestCase("wrong")]
        public async Task SessionRoute_RejectsMissingOrWrongToken(string token)
        {
            using (var server = CreateServer())
            {
                RawResponse response = await SendAsync(server.port, Request(server.port, "/sessions/known/data", token));
                Assert.That(response.statusCode, Is.EqualTo(401));
            }
        }

        [TestCase(null)]
        [TestCase("wrong")]
        public async Task UnknownSession_ReturnsNotFoundBeforeTokenValidation(string token)
        {
            using (var server = CreateServer())
            {
                RawResponse response = await SendAsync(server.port, Request(server.port, "/sessions/missing/data", token));
                Assert.That(response.statusCode, Is.EqualTo(404));
            }
        }

        [Test]
        public async Task Server_RejectsInvalidOrMultipleHostHeaders()
        {
            using (var server = CreateServer())
            {
                RawResponse invalid = await SendAsync(server.port,
                    "GET /sessions/known/data HTTP/1.1\r\nHost: example.com\r\nX-PSD-Session-Token: token\r\n\r\n");
                RawResponse multiple = await SendAsync(server.port,
                    "GET /sessions/known/data HTTP/1.1\r\nHost: localhost:" + server.port +
                    "\r\nHost: 127.0.0.1:" + server.port + "\r\nX-PSD-Session-Token: token\r\n\r\n");
                Assert.That(invalid.statusCode, Is.EqualTo(400));
                Assert.That(multiple.statusCode, Is.EqualTo(400));
            }
        }

        [Test]
        public async Task Server_RejectsInvalidHeaderSyntaxAndAmbiguousFraming()
        {
            using (var server = CreateServer())
            {
                string prefix = "GET /sessions/known/data HTTP/1.1\r\n";
                string host = "Host: localhost:" + server.port + "\r\nX-PSD-Session-Token: token\r\n";
                string[] invalid =
                {
                    prefix + "Host : localhost:" + server.port + "\r\nX-PSD-Session-Token: token\r\n\r\n",
                    prefix + host + "Content-Length : 0\r\n\r\n",
                    prefix + host + "Transfer-Encoding : chunked\r\n\r\n",
                    prefix + host + "Content-Length: 0\r\nContent-Length: 0\r\n\r\n",
                    prefix + host + "Content-Length: 0\r\nContent-Length: 1\r\n\r\n",
                    prefix + host + "Content-Length: 0\r\nTransfer-Encoding: chunked\r\n\r\n",
                    prefix + host + "Bad Header: value\r\n\r\n"
                };
                foreach (string request in invalid)
                    Assert.That((await SendAsync(server.port, request)).statusCode, Is.EqualTo(400));
            }
        }

        [Test]
        public async Task Server_ReturnsNotFoundForUnknownSessionTraversalAndRoute()
        {
            using (var server = CreateServer())
            {
                RawResponse unknownSession = await SendAsync(server.port, Request(server.port, "/sessions/missing/data", "token"));
                RawResponse traversal = await SendAsync(server.port, Request(server.port, "/sessions/known/../secret", "token"));
                RawResponse unknownRoute = await SendAsync(server.port, Request(server.port, "/not-a-route", "token"));
                Assert.That(unknownSession.statusCode, Is.EqualTo(404));
                Assert.That(traversal.statusCode, Is.EqualTo(404));
                Assert.That(unknownRoute.statusCode, Is.EqualTo(404));
            }
        }

        [Test]
        public async Task Server_WritesExactJsonAndPngResponses()
        {
            using (var server = CreateServer())
            {
                RawResponse json = await SendAsync(server.port, Request(server.port, "/sessions/known/data", "token"));
                RawResponse png = await SendAsync(server.port, Request(server.port, "/sessions/known/preview.png", "token"));
                Assert.That(json.headers["Content-Type"], Is.EqualTo("application/json; charset=utf-8"));
                Assert.That(Encoding.UTF8.GetString(json.body), Is.EqualTo("{\"ok\":true}"));
                Assert.That(png.headers["Content-Type"], Is.EqualTo("image/png"));
                CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71 }, png.body);
                Assert.That(json.headers["Content-Length"], Is.EqualTo(json.body.Length.ToString()));
                Assert.That(png.headers["Content-Length"], Is.EqualTo(png.body.Length.ToString()));
            }
        }

        [Test]
        public async Task Server_DeliversAllowedPostBodyToTheMatchedSessionRoute()
        {
            using (var server = CreateServer())
            {
                const string body = "post-body";
                string request = "POST /sessions/known/echo HTTP/1.1\r\nHost: localhost:" + server.port +
                    "\r\nX-PSD-Session-Token: token\r\nContent-Length: " + body.Length + "\r\n\r\n" + body;
                RawResponse response = await SendAsync(server.port, request);

                Assert.That(response.statusCode, Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(response.body),
                    Is.EqualTo("{\"delivery\":\"POST|/sessions/known/echo|post-body\"}"));
            }
        }

        [Test]
        public async Task Server_AcceptsExactWireLimitsAndRejectsOneByteOver()
        {
            using (var server = CreateServer())
            {
                string requestLine = "GET /" + new string('a', 4096 - "GET / HTTP/1.1\r\n".Length) + " HTTP/1.1\r\n";
                RawResponse exactLine = await SendAsync(server.port, requestLine + "Host: localhost:" + server.port + "\r\n\r\n");
                RawResponse overLine = await SendAsync(server.port, "GET /" +
                    new string('a', 4097 - "GET / HTTP/1.1\r\n".Length) + " HTTP/1.1\r\nHost: localhost:" + server.port + "\r\n\r\n");

                string headerPrefix = "Host: localhost:" + server.port + "\r\nX-PSD-Session-Token: token\r\nX-Pad: ";
                string exactHeaders = headerPrefix + new string('a', 32768 - headerPrefix.Length - 4) + "\r\n\r\n";
                RawResponse exactHeader = await SendAsync(server.port, "GET /sessions/known/data HTTP/1.1\r\n" + exactHeaders);
                RawResponse overHeader = await SendAsync(server.port, "GET /sessions/known/data HTTP/1.1\r\n" +
                    headerPrefix + new string('a', 32769 - headerPrefix.Length - 4) + "\r\n\r\n");

                string body = new string('b', 1024 * 1024);
                RawResponse exactBody = await SendAsync(server.port, "POST /sessions/known/body-size HTTP/1.1\r\nHost: localhost:" +
                    server.port + "\r\nX-PSD-Session-Token: token\r\nContent-Length: " + body.Length + "\r\n\r\n" + body);
                RawResponse overBody = await SendAsync(server.port, "POST /sessions/known/body-size HTTP/1.1\r\nHost: localhost:" +
                    server.port + "\r\nX-PSD-Session-Token: token\r\nContent-Length: 1048577\r\n\r\n");

                Assert.That(exactLine.statusCode, Is.EqualTo(404));
                Assert.That(overLine.statusCode, Is.EqualTo(413));
                Assert.That(exactHeader.statusCode, Is.EqualTo(200));
                Assert.That(overHeader.statusCode, Is.EqualTo(413));
                Assert.That(exactBody.statusCode, Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(exactBody.body), Is.EqualTo("{\"bytes\":1048576}"));
                Assert.That(overBody.statusCode, Is.EqualTo(413));
            }
        }

        [Test]
        public async Task Server_RejectsUnsupportedMalformedAndOversizedRequests()
        {
            using (var server = CreateServer())
            {
                RawResponse method = await SendAsync(server.port, "PUT / HTTP/1.1\r\nHost: localhost:" + server.port + "\r\n\r\n");
                RawResponse malformed = await SendAsync(server.port, "GET / HTTP/1.0\r\nHost: localhost:" + server.port + "\r\n\r\n");
                RawResponse body = await SendAsync(server.port, "POST / HTTP/1.1\r\nHost: localhost:" + server.port +
                    "\r\nContent-Length: 1048577\r\n\r\n");
                RawResponse line = await SendAsync(server.port, "GET /" + new string('a', 4096) + " HTTP/1.1\r\nHost: localhost:" + server.port + "\r\n\r\n");
                RawResponse headers = await SendAsync(server.port, "GET / HTTP/1.1\r\nHost: localhost:" + server.port +
                    "\r\nX-Pad: " + new string('a', 32768) + "\r\n\r\n");
                Assert.That(method.statusCode, Is.EqualTo(405));
                Assert.That(malformed.statusCode, Is.EqualTo(400));
                Assert.That(body.statusCode, Is.EqualTo(413));
                Assert.That(line.statusCode, Is.EqualTo(413));
                Assert.That(headers.statusCode, Is.EqualTo(413));
            }
        }

        [Test]
        public void Server_DisposeIsIdempotent()
        {
            var server = CreateServer();
            Assert.DoesNotThrow(() => server.Dispose());
            Assert.DoesNotThrow(() => server.Dispose());
        }

        [TestCase("GET /sessions/known/data HTTP/1.1\r\n")]
        [TestCase("GET /sessions/known/data HTTP/1.1\r\nHost: localhost:1\r\n")]
        [TestCase("POST /sessions/known/data HTTP/1.1\r\nHost: localhost:1\r\nContent-Length: 4\r\n\r\na")]
        public async Task Server_TimesOutPartialRequestsAndAcceptsTheNextClient(string partialRequest)
        {
            using (var server = CreateServer(TimeSpan.FromMilliseconds(100)))
            using (var client = await ConnectAndWriteAsync(server.port, partialRequest.Replace("localhost:1", "localhost:" + server.port)))
            {
                await WaitForCloseAsync(client);
                Assert.That((await SendAsync(server.port, Request(server.port, "/sessions/known/data", "token"))).statusCode,
                    Is.EqualTo(200));
            }
        }

        [Test]
        public async Task Server_DisposeClosesAClientDuringRead()
        {
            var server = CreateServer(TimeSpan.FromSeconds(10));
            try
            {
                using (var client = await ConnectAndWriteAsync(server.port, "GET /sessions/known/data HTTP/1.1\r\n"))
                {
                    server.Dispose();
                    await WaitForCloseAsync(client);
                }
            }
            finally { server.Dispose(); }
        }

        [Test]
        public async Task Server_ReleasesDeadlineStateAfterRepeatedHealthyRequests()
        {
            using (var server = CreateServer())
            {
                for (int index = 0; index < 12; index++)
                    Assert.That((await SendAsync(server.port, Request(server.port, "/sessions/known/data", "token"))).statusCode,
                        Is.EqualTo(200));
                Assert.That(server.activeDeadlineCount, Is.EqualTo(0));
            }
        }

        [Test]
        public async Task Server_ContainsThrowingHandlersAndContinuesAcceptingClients()
        {
            int calls = 0;
            var errors = new List<Exception>();
            using (var server = new PsdHierarchyWebServer(new PsdHierarchyWebRouter(
                id => id == "known" ? new PsdHierarchyWebSession("known", "token", "guid", "Assets/A.psd", "C:/temp/session", null) : null,
                (request, session) =>
                {
                    calls++;
                    if (calls == 1) throw new InvalidOperationException("token and body must not be logged");
                    return PsdHierarchyWebResponse.Json("{\"healthy\":true}");
                }), TimeSpan.FromSeconds(1), errors.Add))
            {
                RawResponse failed = await SendAsync(server.port, Request(server.port, "/sessions/known/data", "token"));
                RawResponse healthy = await SendAsync(server.port, Request(server.port, "/sessions/known/data", "token"));
                Assert.That(failed.statusCode, Is.EqualTo(500));
                Assert.That(Encoding.UTF8.GetString(failed.body), Is.EqualTo("{\"error\":\"internal_error\"}"));
                Assert.That(healthy.statusCode, Is.EqualTo(200));
                Assert.That(errors.Count, Is.EqualTo(1));
                StringAssert.DoesNotContain("token", errors[0].Message);
            }
        }

        [Test]
        public async Task Server_ContainsCooperativeDeadlineCancellationAndRecovers()
        {
            int probeCalls = 0;
            using (var server = new PsdHierarchyWebServer(new PsdHierarchyWebRouter(
                id => id == "known" ? new PsdHierarchyWebSession("known", "token", "guid", "Assets/A.psd", "C:/temp/session", null) : null,
                (request, session) => PsdHierarchyWebResponse.Json("{\"ok\":true}")), TimeSpan.FromMilliseconds(100), null,
                token => { if (Interlocked.Increment(ref probeCalls) == 1) token.WaitHandle.WaitOne(); }))
            using (var stalled = await ConnectAndWriteAsync(server.port, "GET /sessions/known/data HTTP/1.1\r\n"))
            {
                await WaitForCloseAsync(stalled);
                Assert.That((await SendAsync(server.port, Request(server.port, "/sessions/known/data", "token"))).statusCode,
                    Is.EqualTo(200));
            }
        }

        [Test]
        public async Task Server_DefaultDiagnosticsUseTraceWithoutSecretText()
        {
            var output = new StringWriter();
            var listener = new TextWriterTraceListener(output);
            Trace.Listeners.Add(listener);
            try
            {
                using (var server = new PsdHierarchyWebServer(new PsdHierarchyWebRouter(
                    id => id == "known" ? new PsdHierarchyWebSession("known", "token", "guid", "Assets/A.psd", "C:/temp/session", null) : null,
                    (request, session) => { throw new InvalidOperationException("secret-token-and-body"); })))
                {
                    Assert.That((await SendAsync(server.port, Request(server.port, "/sessions/known/data", "token"))).statusCode,
                        Is.EqualTo(500));
                }
                listener.Flush();
                StringAssert.Contains(typeof(InvalidOperationException).FullName, output.ToString());
                StringAssert.DoesNotContain("secret-token-and-body", output.ToString());
            }
            finally
            {
                Trace.Listeners.Remove(listener);
                listener.Dispose();
                output.Dispose();
            }
        }

        private static PsdHierarchyWebServer CreateServer(TimeSpan? deadline = null)
        {
            return new PsdHierarchyWebServer(new PsdHierarchyWebRouter(
                id => id == "known" ? new PsdHierarchyWebSession("known", "token", "guid", "Assets/A.psd", "C:/temp/session", null) : null,
                (request, session) => request.path.EndsWith("preview.png", StringComparison.Ordinal)
                    ? PsdHierarchyWebResponse.Png(new byte[] { 137, 80, 78, 71 })
                    : request.path.EndsWith("echo", StringComparison.Ordinal)
                        ? PsdHierarchyWebResponse.Json("{\"delivery\":\"" + request.method + "|" + request.path + "|" +
                            Encoding.UTF8.GetString(request.body) + "\"}")
                        : request.path.EndsWith("body-size", StringComparison.Ordinal)
                            ? PsdHierarchyWebResponse.Json("{\"bytes\":" + request.body.Length + "}")
                            : PsdHierarchyWebResponse.Json("{\"ok\":true}")), deadline ?? TimeSpan.FromSeconds(1), null);
        }

        private static async Task<TcpClient> ConnectAndWriteAsync(int port, string request)
        {
            var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            byte[] bytes = Encoding.ASCII.GetBytes(request);
            await client.GetStream().WriteAsync(bytes, 0, bytes.Length);
            return client;
        }

        private static async Task WaitForCloseAsync(TcpClient client)
        {
            var buffer = new byte[1];
            Task<int> read = client.GetStream().ReadAsync(buffer, 0, buffer.Length);
            Task completed = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.That(completed, Is.SameAs(read), "Server did not close the stalled request.");
            Assert.That(await read, Is.EqualTo(0));
        }

        private static string Request(int port, string path, string token)
        {
            return "GET " + path + " HTTP/1.1\r\nHost: localhost:" + port +
                (token == null ? string.Empty : "\r\nX-PSD-Session-Token: " + token) + "\r\n\r\n";
        }

        private static async Task<RawResponse> SendAsync(int port, string request)
        {
            using (var client = new TcpClient())
            {
                await client.ConnectAsync("127.0.0.1", port);
                NetworkStream stream = client.GetStream();
                byte[] requestBytes = Encoding.ASCII.GetBytes(request);
                await stream.WriteAsync(requestBytes, 0, requestBytes.Length);
                var bytes = new List<byte>();
                var buffer = new byte[4096];
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    bytes.AddRange(new ArraySegment<byte>(buffer, 0, read));
                return RawResponse.Parse(bytes.ToArray());
            }
        }

        private sealed class RawResponse
        {
            public int statusCode;
            public Dictionary<string, string> headers;
            public byte[] body;

            public static RawResponse Parse(byte[] bytes)
            {
                byte[] separator = Encoding.ASCII.GetBytes("\r\n\r\n");
                int start = IndexOf(bytes, separator);
                string[] lines = Encoding.ASCII.GetString(bytes, 0, start).Split(new[] { "\r\n" }, StringSplitOptions.None);
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int index = 1; index < lines.Length; index++)
                {
                    int colon = lines[index].IndexOf(':');
                    headers.Add(lines[index].Substring(0, colon), lines[index].Substring(colon + 1).Trim());
                }
                var body = new byte[bytes.Length - start - separator.Length];
                Buffer.BlockCopy(bytes, start + separator.Length, body, 0, body.Length);
                return new RawResponse { statusCode = int.Parse(lines[0].Split(' ')[1]), headers = headers, body = body };
            }

            private static int IndexOf(byte[] value, byte[] pattern)
            {
                for (int index = 0; index <= value.Length - pattern.Length; index++)
                {
                    int offset = 0;
                    while (offset < pattern.Length && value[index + offset] == pattern[offset]) offset++;
                    if (offset == pattern.Length) return index;
                }
                throw new AssertionException("No HTTP header terminator.");
            }
        }
    }
}
