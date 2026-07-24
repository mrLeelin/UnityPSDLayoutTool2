namespace PsdLayoutTool2.Editor
{
    using System;
    using System.Collections.Generic;

    /// <summary>Routes already-parsed loopback requests without touching Unity APIs.</summary>
    internal sealed class PsdHierarchyWebRouter
    {
        private readonly Func<string, PsdHierarchyWebSession> findSession;
        private readonly Func<PsdHierarchyWebRequest, PsdHierarchyWebSession, PsdHierarchyWebResponse> handleSession;

        public PsdHierarchyWebRouter(
            Func<string, PsdHierarchyWebSession> findSession,
            Func<PsdHierarchyWebRequest, PsdHierarchyWebSession, PsdHierarchyWebResponse> handleSession)
        {
            if (findSession == null) throw new ArgumentNullException(nameof(findSession));
            if (handleSession == null) throw new ArgumentNullException(nameof(handleSession));
            this.findSession = findSession;
            this.handleSession = handleSession;
        }

        public PsdHierarchyWebResponse Route(PsdHierarchyWebRequest request)
        {
            if (request == null || !TryGetSessionId(request.path, out string sessionId))
                return PsdHierarchyWebResponse.Empty(404);

            PsdHierarchyWebSession session = findSession(sessionId);
            if (session == null) return PsdHierarchyWebResponse.Empty(404);
            string providedToken;
            if (!request.headers.TryGetValue("X-PSD-Session-Token", out providedToken) ||
                !TokensMatch(session.token, providedToken))
                return PsdHierarchyWebResponse.Empty(401);

            return handleSession(request, session) ?? PsdHierarchyWebResponse.Empty(404);
        }

        private static bool TryGetSessionId(string path, out string sessionId)
        {
            sessionId = null;
            if (string.IsNullOrEmpty(path) || path.IndexOf('?') >= 0 || path.IndexOf('#') >= 0 ||
                path.IndexOf('\\') >= 0 || path.IndexOf('%') >= 0) return false;
            string[] segments = path.Split('/');
            if (segments.Length < 4 || segments[0].Length != 0 ||
                !string.Equals(segments[1], "sessions", StringComparison.Ordinal) ||
                string.IsNullOrEmpty(segments[2])) return false;
            for (int index = 1; index < segments.Length; index++)
                if (segments[index] == "." || segments[index] == "..") return false;
            sessionId = segments[2];
            return true;
        }

        private static bool TokensMatch(string expected, string actual)
        {
            if (expected == null || actual == null) return false;
            int maximumLength = Math.Max(expected.Length, actual.Length);
            int difference = expected.Length ^ actual.Length;
            for (int index = 0; index < maximumLength; index++)
            {
                int left = index < expected.Length ? expected[index] : 0;
                int right = index < actual.Length ? actual[index] : 0;
                difference |= left ^ right;
            }
            return difference == 0;
        }
    }

    internal sealed class PsdHierarchyWebRequest
    {
        public PsdHierarchyWebRequest(string method, string path, Dictionary<string, string> headers, byte[] body)
        {
            this.method = method;
            this.path = path;
            this.headers = headers;
            this.body = body;
        }

        public string method { get; private set; }
        public string path { get; private set; }
        public Dictionary<string, string> headers { get; private set; }
        public byte[] body { get; private set; }
    }
}
