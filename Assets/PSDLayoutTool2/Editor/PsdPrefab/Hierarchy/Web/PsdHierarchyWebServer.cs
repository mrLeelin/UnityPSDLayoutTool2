namespace PsdLayoutTool2.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>Minimal HTTP/1.1 listener intentionally limited to local PSD workbench traffic.</summary>
    internal sealed class PsdHierarchyWebServer : IDisposable
    {
        private const int MaxRequestLineBytes = 4 * 1024;
        private const int MaxHeaderBytes = 32 * 1024;
        private const int MaxBodyBytes = 1024 * 1024;
        private readonly object gate = new object();
        private readonly TcpListener listener;
        private readonly PsdHierarchyWebRouter router;
        private readonly Task acceptLoop;
        private TcpClient activeClient;
        private bool disposed;

        public PsdHierarchyWebServer(PsdHierarchyWebRouter router)
        {
            if (router == null) throw new ArgumentNullException(nameof(router));
            this.router = router;
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
            acceptLoop = Task.Run((Func<Task>)AcceptLoopAsync);
        }

        public int port { get; private set; }

        public void Dispose()
        {
            TcpClient client;
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                client = activeClient;
            }
            try { listener.Stop(); } catch (SocketException) { }
            try { client?.Close(); } catch (SocketException) { }
            try { acceptLoop.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
        }

        private async Task AcceptLoopAsync()
        {
            while (true)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync().ConfigureAwait(false); }
                catch (ObjectDisposedException) { return; }
                catch (SocketException)
                {
                    lock (gate) { if (disposed) return; }
                    continue;
                }
                lock (gate)
                {
                    if (disposed) { client.Close(); return; }
                    activeClient = client;
                }
                try { await ProcessClientAsync(client).ConfigureAwait(false); }
                finally
                {
                    client.Close();
                    lock (gate) { if (ReferenceEquals(activeClient, client)) activeClient = null; }
                }
            }
        }

        private async Task ProcessClientAsync(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                PsdHierarchyWebRequest request;
                int errorStatus;
                if (!TryReadRequest(stream, out request, out errorStatus))
                {
                    await WriteResponseAsync(stream, PsdHierarchyWebResponse.Empty(errorStatus)).ConfigureAwait(false);
                    return;
                }
                await WriteResponseAsync(stream, router.Route(request)).ConfigureAwait(false);
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
        }

        private bool TryReadRequest(NetworkStream stream, out PsdHierarchyWebRequest request, out int errorStatus)
        {
            request = null;
            errorStatus = 400;
            string requestLine;
            if (!TryReadLine(stream, MaxRequestLineBytes, out requestLine, out errorStatus)) return false;
            string[] requestParts = requestLine.Split(' ');
            if (requestParts.Length != 3 || requestParts[1].Length == 0 || requestParts[1][0] != '/' ||
                !string.Equals(requestParts[2], "HTTP/1.1", StringComparison.Ordinal)) return false;
            if (requestParts[0] != "GET" && requestParts[0] != "POST") { errorStatus = 405; return false; }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int headerBytes = 0;
            while (true)
            {
                string line;
                int lineStatus;
                if (!TryReadLine(stream, MaxHeaderBytes - headerBytes, out line, out lineStatus))
                {
                    errorStatus = lineStatus;
                    return false;
                }
                headerBytes += Encoding.ASCII.GetByteCount(line) + 2;
                if (headerBytes > MaxHeaderBytes) { errorStatus = 413; return false; }
                if (line.Length == 0) break;
                int colon = line.IndexOf(':');
                if (colon <= 0 || headers.ContainsKey(line.Substring(0, colon))) return false;
                headers.Add(line.Substring(0, colon), line.Substring(colon + 1).Trim());
            }
            if (!IsExpectedHost(headers, port)) return false;
            string contentLengthValue;
            int contentLength = 0;
            if (headers.TryGetValue("Content-Length", out contentLengthValue) &&
                (!int.TryParse(contentLengthValue, NumberStyles.None, CultureInfo.InvariantCulture, out contentLength) || contentLength < 0)) return false;
            if (headers.ContainsKey("Transfer-Encoding")) return false;
            if (contentLength > MaxBodyBytes) { errorStatus = 413; return false; }
            byte[] body = new byte[contentLength];
            int received = 0;
            while (received < body.Length)
            {
                int read = stream.Read(body, received, body.Length - received);
                if (read == 0) return false;
                received += read;
            }
            request = new PsdHierarchyWebRequest(requestParts[0], requestParts[1], headers, body);
            return true;
        }

        private static bool IsExpectedHost(Dictionary<string, string> headers, int port)
        {
            string host;
            if (!headers.TryGetValue("Host", out host)) return false;
            return string.Equals(host, "127.0.0.1:" + port, StringComparison.Ordinal) ||
                string.Equals(host, "localhost:" + port, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryReadLine(NetworkStream stream, int maximumBytes, out string line, out int errorStatus)
        {
            line = null;
            errorStatus = 400;
            if (maximumBytes < 0) { errorStatus = 413; return false; }
            var bytes = new List<byte>();
            while (true)
            {
                int value = stream.ReadByte();
                if (value < 0) return false;
                if (bytes.Count >= maximumBytes) { errorStatus = 413; return false; }
                bytes.Add((byte)value);
                if (value == '\n')
                {
                    if (bytes.Count < 2 || bytes[bytes.Count - 2] != '\r') return false;
                    bytes.RemoveAt(bytes.Count - 1);
                    bytes.RemoveAt(bytes.Count - 1);
                    line = Encoding.ASCII.GetString(bytes.ToArray());
                    return true;
                }
            }
        }

        private static async Task WriteResponseAsync(NetworkStream stream, PsdHierarchyWebResponse response)
        {
            if (response == null) response = PsdHierarchyWebResponse.Empty(500);
            string statusText = response.statusCode == 200 ? "OK" : response.statusCode == 400 ? "Bad Request" :
                response.statusCode == 401 ? "Unauthorized" : response.statusCode == 404 ? "Not Found" :
                response.statusCode == 405 ? "Method Not Allowed" : response.statusCode == 413 ? "Payload Too Large" : "Internal Server Error";
            string headers = "HTTP/1.1 " + response.statusCode + " " + statusText + "\r\nContent-Type: " +
                response.contentType + "\r\nContent-Length: " + response.body.Length + "\r\nConnection: close\r\n" +
                (response.statusCode == 405 ? "Allow: GET, POST\r\n" : string.Empty) + "\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length).ConfigureAwait(false);
            if (response.body.Length > 0) await stream.WriteAsync(response.body, 0, response.body.Length).ConfigureAwait(false);
        }
    }
}
