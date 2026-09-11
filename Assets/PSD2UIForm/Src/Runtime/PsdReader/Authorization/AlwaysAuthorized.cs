using System;

namespace cn.efunstudio.psdreader.Authorization
{
    // License/auth system removed. Defaults to fully authorized.
    internal static class AlwaysAuthorized
    {
        public static bool IsAuthorized => true;

        public static bool HasMainFeature => true;

        private sealed class NoopScope : IDisposable
        {
            public void Dispose()
            {
            }
        }

        public static IDisposable CreateNoopScope()
        {
            return new NoopScope();
        }

        public static string Sha256Hex(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text));
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
