namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net.Sockets;
    using System.Text;
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

        private static PsdHierarchyWebServer CreateServer()
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
                            : PsdHierarchyWebResponse.Json("{\"ok\":true}")));
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
