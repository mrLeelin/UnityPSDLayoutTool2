namespace PsdLayoutTool2.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class PsdHierarchyWebDiagnostic
    {
        public readonly string exceptionType;
        public readonly string stackTrace;

        public PsdHierarchyWebDiagnostic(Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            exceptionType = exception.GetType().FullName ?? exception.GetType().Name;
            stackTrace = exception.StackTrace ?? string.Empty;
        }
    }

    /// <summary>Minimal HTTP/1.1 listener intentionally limited to local PSD workbench traffic.</summary>
    internal sealed class PsdHierarchyWebServer : IDisposable
    {
        // Both line limits include the trailing CRLF so a 4 KiB request line is 4096 wire bytes.
        private const int MaxRequestLineBytes = 4 * 1024;
        // Header bytes include every header line's CRLF and the terminating empty line's CRLF.
        private const int MaxHeaderBytes = 32 * 1024;
        private const int MaxBodyBytes = 1024 * 1024;
        private const int MaxConcurrentClients = 16;
        private static readonly TimeSpan MinimumRequestDeadline = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan MaximumRequestDeadline = TimeSpan.FromSeconds(60);
        private readonly object gate = new object();
        private readonly TcpListener listener;
        private readonly PsdHierarchyWebRouter router;
        private readonly TimeSpan requestDeadline;
        private readonly Action<PsdHierarchyWebDiagnostic> errorSink;
        private readonly Action<CancellationToken> processingProbe;
        private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
        private readonly Task acceptLoop;
        private readonly HashSet<TcpClient> activeClients = new HashSet<TcpClient>();
        private readonly HashSet<Task> clientTasks = new HashSet<Task>();
        private int activeDeadlineCountValue;
        private bool disposed;

        public PsdHierarchyWebServer(PsdHierarchyWebRouter router)
            : this(router, TimeSpan.FromSeconds(10), null)
        {
        }

        public PsdHierarchyWebServer(PsdHierarchyWebRouter router, TimeSpan requestDeadline, Action<PsdHierarchyWebDiagnostic> errorSink)
            : this(router, requestDeadline, errorSink, null)
        {
        }

        internal PsdHierarchyWebServer(
            PsdHierarchyWebRouter router,
            TimeSpan requestDeadline,
            Action<PsdHierarchyWebDiagnostic> errorSink,
            Action<CancellationToken> processingProbe)
        {
            if (router == null) throw new ArgumentNullException(nameof(router));
            if (requestDeadline < MinimumRequestDeadline || requestDeadline > MaximumRequestDeadline)
                throw new ArgumentOutOfRangeException(nameof(requestDeadline));
            this.router = router;
            this.requestDeadline = requestDeadline;
            this.errorSink = errorSink ?? ReportToTrace;
            this.processingProbe = processingProbe;
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
            acceptLoop = Task.Run((Func<Task>)AcceptLoopAsync);
        }

        public int port { get; private set; }
        internal int activeDeadlineCount { get { return Volatile.Read(ref activeDeadlineCountValue); } }

        public void Dispose()
        {
            TcpClient[] clients;
            Task[] tasks;
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                clients = new List<TcpClient>(activeClients).ToArray();
                tasks = new List<Task>(clientTasks).ToArray();
            }
            shutdown.Cancel();
            try { listener.Stop(); } catch (SocketException) { }
            foreach (TcpClient client in clients)
                try { client.Close(); } catch (SocketException) { }
            try { acceptLoop.Wait(TimeSpan.FromSeconds(2)); }
            catch (AggregateException exception) { Report(exception); }
            try { Task.WhenAll(tasks).Wait(TimeSpan.FromSeconds(2)); }
            catch (AggregateException exception) { Report(exception); }
            finally { shutdown.Dispose(); }
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
                    if (activeClients.Count >= MaxConcurrentClients)
                    {
                        client.Close();
                        continue;
                    }
                    activeClients.Add(client);
                }
                Task task = HandleClientAsync(client);
                lock (gate) clientTasks.Add(task);
                _ = task.ContinueWith(completed =>
                {
                    lock (gate) clientTasks.Remove(completed);
                    if (completed.IsFaulted) Report(completed.Exception);
                }, TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try { await ProcessClientWithinDeadlineAsync(client).ConfigureAwait(false); }
            finally
            {
                client.Close();
                lock (gate) activeClients.Remove(client);
            }
        }

        private async Task ProcessClientWithinDeadlineAsync(TcpClient client)
        {
            using (var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token))
            {
                requestCancellation.CancelAfter(requestDeadline);
                Interlocked.Increment(ref activeDeadlineCountValue);
                try
                {
                    // Parsing uses blocking stream reads, so schedule it before arming the deadline race.
                    Task process = Task.Run((Func<Task>)(() => ProcessClientAsync(client, requestCancellation.Token)));
                    Task cancellation = Task.Delay(Timeout.InfiniteTimeSpan, requestCancellation.Token);
                    Task completed = await Task.WhenAny(process, cancellation).ConfigureAwait(false);
                    if (completed == process)
                    {
                        try { await process.ConfigureAwait(false); }
                        catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested || shutdown.IsCancellationRequested)
                        {
                            // Deadline and shutdown cancellation are normal per-client terminal states.
                        }
                        return;
                    }

                    try { client.Close(); } catch (SocketException) { }
                    ObserveFault(process);
                }
                finally
                {
                    requestCancellation.Cancel();
                    Interlocked.Decrement(ref activeDeadlineCountValue);
                }
            }
        }

        private async Task ProcessClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            try
            {
                processingProbe?.Invoke(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                NetworkStream stream = client.GetStream();
                PsdHierarchyWebRequest request;
                int errorStatus;
                if (!TryReadRequest(stream, cancellationToken, out request, out errorStatus))
                {
                    await WriteResponseAsync(stream, PsdHierarchyWebResponse.Empty(errorStatus), cancellationToken).ConfigureAwait(false);
                    CompleteResponse(client);
                    return;
                }
                PsdHierarchyWebResponse response;
                try { response = router.Route(request); }
                catch (Exception exception)
                {
                    ReportRouteFailure(exception);
                    await WriteResponseAsync(stream, PsdHierarchyWebResponse.Json(500, "{\"error\":\"internal_error\"}"), cancellationToken)
                        .ConfigureAwait(false);
                    CompleteResponse(client);
                    return;
                }
                await WriteResponseAsync(stream, response, cancellationToken).ConfigureAwait(false);
                CompleteResponse(client);
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
        }

        private bool TryReadRequest(NetworkStream stream, CancellationToken cancellationToken, out PsdHierarchyWebRequest request, out int errorStatus)
        {
            request = null;
            errorStatus = 400;
            string requestLine;
            if (!TryReadLine(stream, cancellationToken, MaxRequestLineBytes, out requestLine, out errorStatus)) return false;
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
                if (!TryReadLine(stream, cancellationToken, MaxHeaderBytes - headerBytes, out line, out lineStatus))
                {
                    errorStatus = lineStatus;
                    return false;
                }
                headerBytes += Encoding.ASCII.GetByteCount(line) + 2;
                if (headerBytes > MaxHeaderBytes) { errorStatus = 413; return false; }
                if (line.Length == 0) break;
                int colon = line.IndexOf(':');
                if (colon <= 0 || !IsFieldName(line, colon) || headers.ContainsKey(line.Substring(0, colon))) return false;
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
                cancellationToken.ThrowIfCancellationRequested();
                int read = stream.Read(body, received, body.Length - received);
                if (read == 0) return false;
                received += read;
            }
            request = new PsdHierarchyWebRequest(requestParts[0], requestParts[1], headers, body);
            return true;
        }

        private static bool IsFieldName(string line, int colon)
        {
            for (int index = 0; index < colon; index++)
            {
                char character = line[index];
                if (!((character >= '0' && character <= '9') || (character >= 'A' && character <= 'Z') ||
                    (character >= 'a' && character <= 'z') || "!#$%&'*+-.^_`|~".IndexOf(character) >= 0)) return false;
            }
            return true;
        }

        private static bool IsExpectedHost(Dictionary<string, string> headers, int port)
        {
            string host;
            if (!headers.TryGetValue("Host", out host)) return false;
            return string.Equals(host, "127.0.0.1:" + port, StringComparison.Ordinal) ||
                string.Equals(host, "localhost:" + port, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryReadLine(NetworkStream stream, CancellationToken cancellationToken, int maximumBytes, out string line, out int errorStatus)
        {
            line = null;
            errorStatus = 400;
            if (maximumBytes < 0) { errorStatus = 413; return false; }
            var bytes = new List<byte>();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int value = stream.ReadByte();
                if (value < 0) return false;
                bytes.Add((byte)value);
                if (bytes.Count > maximumBytes) { errorStatus = 413; return false; }
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

        private static async Task WriteResponseAsync(NetworkStream stream, PsdHierarchyWebResponse response, CancellationToken cancellationToken)
        {
            if (response == null) response = PsdHierarchyWebResponse.Empty(500);
            string statusText = response.statusCode == 200 ? "OK" : response.statusCode == 400 ? "Bad Request" :
                response.statusCode == 401 ? "Unauthorized" : response.statusCode == 404 ? "Not Found" :
                response.statusCode == 405 ? "Method Not Allowed" : response.statusCode == 413 ? "Payload Too Large" : "Internal Server Error";
            string headers = "HTTP/1.1 " + response.statusCode + " " + statusText + "\r\nContent-Type: " +
                response.contentType + "\r\nContent-Length: " + response.body.Length + "\r\nConnection: close\r\n" +
                (response.statusCode == 405 ? "Allow: GET, POST\r\n" : string.Empty) + "\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
            cancellationToken.ThrowIfCancellationRequested();
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length).ConfigureAwait(false);
            if (response.body.Length > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await stream.WriteAsync(response.body, 0, response.body.Length).ConfigureAwait(false);
            }
        }

        private static void CompleteResponse(TcpClient client)
        {
            try
            {
                client.LingerState = new LingerOption(true, 1);
                client.Client.Shutdown(SocketShutdown.Send);
            }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
        }

        private void ReportRouteFailure(Exception exception)
        {
            Report(exception);
        }

        private void ObserveFault(Task task)
        {
            task.ContinueWith(completed =>
            {
                if (completed.IsFaulted) Report(completed.Exception);
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private void Report(Exception exception)
        {
            try { errorSink?.Invoke(new PsdHierarchyWebDiagnostic(exception)); } catch { }
        }

        private static void ReportToTrace(PsdHierarchyWebDiagnostic diagnostic)
        {
            Trace.WriteLine("PsdHierarchyWebServer exception type: " + diagnostic.exceptionType);
            Trace.WriteLine(diagnostic.stackTrace);
        }
    }
}
