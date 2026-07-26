namespace PsdLayoutTool2
{
    using System;
    using UnityEngine;

    internal readonly struct PsdHierarchyAiConnectionSnapshot
    {
        internal PsdHierarchyAiConnectionSnapshot(PsdHierarchyAiConnectionMode mode, string baseUrl)
        {
            this.mode = mode;
            this.baseUrl = baseUrl ?? string.Empty;
        }

        internal readonly PsdHierarchyAiConnectionMode mode;
        internal readonly string baseUrl;
    }

    internal readonly struct PsdHierarchyAiSettingsSnapshot
    {
        internal PsdHierarchyAiSettingsSnapshot(
            PsdHierarchyAiProvider provider,
            PsdHierarchyAiConnectionSnapshot codex,
            PsdHierarchyAiConnectionSnapshot claude)
        {
            this.provider = provider;
            this.codex = codex;
            this.claude = claude;
        }

        internal readonly PsdHierarchyAiProvider provider;
        internal readonly PsdHierarchyAiConnectionSnapshot codex;
        internal readonly PsdHierarchyAiConnectionSnapshot claude;

        internal PsdHierarchyAiConnectionSnapshot activeConnection
        {
            get
            {
                switch (provider)
                {
                    case PsdHierarchyAiProvider.Codex:
                        return codex;
                    case PsdHierarchyAiProvider.Claude:
                        return claude;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(provider),
                            provider,
                            "Unsupported hierarchy AI provider.");
                }
            }
        }
    }

    [Serializable]
    internal sealed class PsdHierarchyAiConnectionSettings
    {
        [SerializeField]
        private PsdHierarchyAiConnectionMode mode = PsdHierarchyAiConnectionMode.Default;

        [SerializeField]
        private string baseUrl = string.Empty;

        internal bool SetMode(PsdHierarchyAiConnectionMode value)
        {
            if (value != PsdHierarchyAiConnectionMode.Default &&
                value != PsdHierarchyAiConnectionMode.Custom)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Unsupported hierarchy AI connection mode.");
            }

            if (mode == value)
            {
                return false;
            }

            mode = value;
            return true;
        }

        internal bool SetBaseUrl(string value)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (string.Equals(baseUrl, normalized, StringComparison.Ordinal))
            {
                return false;
            }

            baseUrl = normalized;
            return true;
        }

        internal PsdHierarchyAiConnectionSnapshot Resolve()
        {
            if (mode != PsdHierarchyAiConnectionMode.Default &&
                mode != PsdHierarchyAiConnectionMode.Custom)
            {
                throw new InvalidOperationException(
                    "Serialized hierarchy AI connection mode is unsupported: " + mode + ".");
            }

            return new PsdHierarchyAiConnectionSnapshot(mode, (baseUrl ?? string.Empty).Trim());
        }

        internal static bool TryValidateBaseUrl(string value, out string error)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                error = "API base URL is required.";
                return false;
            }

            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri uri))
            {
                error = "API base URL must be an absolute URL.";
                return false;
            }

            if (string.IsNullOrEmpty(uri.Host))
            {
                error = "API base URL must include a host.";
                return false;
            }

            if (!string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                error = "API base URL cannot contain credentials, a query, or a fragment.";
                return false;
            }

            if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                error = string.Empty;
                return true;
            }

            if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.IsLoopback)
            {
                error = string.Empty;
                return true;
            }

            error = "API base URL must use HTTPS, except for loopback HTTP endpoints.";
            return false;
        }
    }
}
