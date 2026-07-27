namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text;
    using UnityEditor;
    using UnityEngine;

    internal enum PsdHierarchyAiProvider
    {
        Claude,
        Codex,
    }

    internal enum PsdHierarchyAiConnectionMode
    {
        LocalCli,
        CustomApi,
    }

    internal readonly struct PsdHierarchyAiCliDescriptor
    {
        internal PsdHierarchyAiCliDescriptor(PsdHierarchyAiProvider provider, string displayName, string executablePath)
        {
            this.provider = provider;
            this.displayName = displayName ?? string.Empty;
            this.executablePath = executablePath ?? string.Empty;
        }

        internal readonly PsdHierarchyAiProvider provider;
        internal readonly string displayName;
        internal readonly string executablePath;
    }

    internal static class PsdHierarchyAiCliDiscovery
    {
        private static readonly PsdHierarchyAiCliDescriptor[] SupportedClis =
        {
            new PsdHierarchyAiCliDescriptor(PsdHierarchyAiProvider.Claude, "Claude", "claude"),
            new PsdHierarchyAiCliDescriptor(PsdHierarchyAiProvider.Codex, "Codex", "codex"),
        };

        internal static IReadOnlyList<PsdHierarchyAiCliDescriptor> FindInstalled()
        {
            return FindInstalled(Environment.GetEnvironmentVariable("PATH"), File.Exists);
        }

        internal static IReadOnlyList<PsdHierarchyAiCliDescriptor> FindInstalled(
            string searchPath,
            Func<string, bool> fileExists)
        {
            if (fileExists == null) throw new ArgumentNullException(nameof(fileExists));

            var installed = new List<PsdHierarchyAiCliDescriptor>();
            foreach (PsdHierarchyAiCliDescriptor supported in SupportedClis)
            {
                string executablePath = FindExecutable(supported.executablePath, searchPath, fileExists);
                if (!string.IsNullOrEmpty(executablePath))
                {
                    installed.Add(new PsdHierarchyAiCliDescriptor(
                        supported.provider,
                        supported.displayName,
                        executablePath));
                }
            }

            return installed;
        }

        internal static bool TryGetInstalled(
            PsdHierarchyAiProvider provider,
            out PsdHierarchyAiCliDescriptor descriptor)
        {
            IReadOnlyList<PsdHierarchyAiCliDescriptor> installed = FindInstalled();
            for (int index = 0; index < installed.Count; index++)
            {
                if (installed[index].provider == provider)
                {
                    descriptor = installed[index];
                    return true;
                }
            }

            descriptor = default(PsdHierarchyAiCliDescriptor);
            return false;
        }

        private static string FindExecutable(string command, string searchPath, Func<string, bool> fileExists)
        {
            if (string.IsNullOrWhiteSpace(searchPath))
            {
                return string.Empty;
            }

            string[] extensions = Application.platform == RuntimePlatform.WindowsEditor
                ? new[] { ".cmd", ".exe", ".bat", string.Empty }
                : new[] { string.Empty };
            string[] folders = searchPath.Split(Path.PathSeparator);
            foreach (string rawFolder in folders)
            {
                string folder = (rawFolder ?? string.Empty).Trim().Trim('"');
                if (string.IsNullOrEmpty(folder))
                {
                    continue;
                }

                foreach (string extension in extensions)
                {
                    string candidate = Path.Combine(folder, command + extension);
                    if (fileExists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
            }

            return string.Empty;
        }
    }

    internal readonly struct PsdHierarchyAiSettingsSnapshot
    {
        internal PsdHierarchyAiSettingsSnapshot(
            PsdHierarchyAiProvider provider,
            PsdHierarchyAiConnectionMode connectionMode,
            string customEndpoint,
            string customModel)
        {
            this.provider = provider;
            this.connectionMode = connectionMode;
            this.customEndpoint = customEndpoint ?? string.Empty;
            this.customModel = customModel ?? string.Empty;
        }

        internal readonly PsdHierarchyAiProvider provider;
        internal readonly PsdHierarchyAiConnectionMode connectionMode;
        internal readonly string customEndpoint;
        internal readonly string customModel;

        internal string ResolveEndpoint()
        {
            return string.IsNullOrWhiteSpace(customEndpoint)
                ? PsdHierarchyChatClient.DefaultEndpoint(provider)
                : customEndpoint.Trim();
        }

        internal string ResolveModel()
        {
            return string.IsNullOrWhiteSpace(customModel)
                ? PsdHierarchyChatClient.DefaultModel(provider)
                : customModel.Trim();
        }

        internal bool TryValidate(out string error)
        {
            if (provider != PsdHierarchyAiProvider.Claude && provider != PsdHierarchyAiProvider.Codex)
            {
                error = "选择的 AI 不受支持。";
                return false;
            }

            if (connectionMode != PsdHierarchyAiConnectionMode.LocalCli &&
                connectionMode != PsdHierarchyAiConnectionMode.CustomApi)
            {
                error = "选择的连接方式不受支持。";
                return false;
            }

            if (connectionMode == PsdHierarchyAiConnectionMode.CustomApi)
            {
                if (!Uri.TryCreate(ResolveEndpoint(), UriKind.Absolute, out Uri endpoint) ||
                    (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
                {
                    error = "自定义 API 地址必须是 http 或 https 的完整地址。";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(ResolveModel()))
                {
                    error = "请填写自定义 API 的模型名称。";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    internal sealed class PsdHierarchyAiSettings
    {
        [SerializeField]
        private PsdHierarchyAiProvider provider = PsdHierarchyAiProvider.Claude;

        [SerializeField]
        private PsdHierarchyAiConnectionMode connectionMode = PsdHierarchyAiConnectionMode.LocalCli;

        [SerializeField]
        private string customEndpoint = string.Empty;

        [SerializeField]
        private string customModel = string.Empty;

        internal PsdHierarchyAiSettingsSnapshot Resolve()
        {
            return new PsdHierarchyAiSettingsSnapshot(provider, connectionMode, customEndpoint, customModel);
        }

        internal bool Set(
            PsdHierarchyAiProvider newProvider,
            PsdHierarchyAiConnectionMode newConnectionMode,
            string newCustomEndpoint,
            string newCustomModel)
        {
            var candidate = new PsdHierarchyAiSettingsSnapshot(
                newProvider,
                newConnectionMode,
                (newCustomEndpoint ?? string.Empty).Trim(),
                (newCustomModel ?? string.Empty).Trim());
            if (!candidate.TryValidate(out string error))
            {
                throw new ArgumentException(error);
            }

            if (provider == candidate.provider &&
                connectionMode == candidate.connectionMode &&
                string.Equals(customEndpoint, candidate.customEndpoint, StringComparison.Ordinal) &&
                string.Equals(customModel, candidate.customModel, StringComparison.Ordinal))
            {
                return false;
            }

            provider = candidate.provider;
            connectionMode = candidate.connectionMode;
            customEndpoint = candidate.customEndpoint;
            customModel = candidate.customModel;
            return true;
        }
    }

    internal sealed class PsdHierarchyAiSecretStore
    {
        private const string StoragePrefix = "PsdLayoutTool2.HierarchyAi.v1";

        internal bool HasApiKey(string projectRoot, PsdHierarchyAiProvider provider)
        {
            return EditorPrefs.HasKey(BuildStorageKey(projectRoot, provider));
        }

        internal bool TryReadApiKey(string projectRoot, PsdHierarchyAiProvider provider, out string apiKey)
        {
            apiKey = string.Empty;
            string storageKey = BuildStorageKey(projectRoot, provider);
            if (!EditorPrefs.HasKey(storageKey))
            {
                return false;
            }

            try
            {
                byte[] encrypted = null;
                byte[] plaintext = null;
                try
                {
                    encrypted = Convert.FromBase64String(EditorPrefs.GetString(storageKey, string.Empty));
                    plaintext = TransformWithCurrentUser(encrypted, false);
                    apiKey = Encoding.UTF8.GetString(plaintext);
                }
                finally
                {
                    if (encrypted != null)
                    {
                        Array.Clear(encrypted, 0, encrypted.Length);
                    }

                    if (plaintext != null)
                    {
                        Array.Clear(plaintext, 0, plaintext.Length);
                    }
                }
                return !string.IsNullOrWhiteSpace(apiKey);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("无法读取本机加密 API Key：" + exception.Message, exception);
            }
        }

        internal void SaveApiKey(string projectRoot, PsdHierarchyAiProvider provider, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API Key 不能为空。", nameof(apiKey));
            }

            byte[] plaintext = Encoding.UTF8.GetBytes(apiKey.Trim());
            byte[] encrypted = null;
            try
            {
                encrypted = TransformWithCurrentUser(plaintext, true);
                EditorPrefs.SetString(BuildStorageKey(projectRoot, provider), Convert.ToBase64String(encrypted));
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("无法保存本机加密 API Key：" + exception.Message, exception);
            }
            finally
            {
                Array.Clear(plaintext, 0, plaintext.Length);
                if (encrypted != null)
                {
                    Array.Clear(encrypted, 0, encrypted.Length);
                }
            }
        }

        internal void ClearApiKey(string projectRoot, PsdHierarchyAiProvider provider)
        {
            EditorPrefs.DeleteKey(BuildStorageKey(projectRoot, provider));
        }

        private static string BuildStorageKey(string projectRoot, PsdHierarchyAiProvider provider)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("项目根目录不能为空。", nameof(projectRoot));
            }

            string identity = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            byte[] bytes = Encoding.UTF8.GetBytes(identity.ToUpperInvariant());
            byte[] hash;
            using (SHA256 algorithm = SHA256.Create())
            {
                hash = algorithm.ComputeHash(bytes);
            }

            try
            {
                var builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("x2"));
                }

                return StoragePrefix + "." + builder + "." + provider;
            }
            finally
            {
                Array.Clear(bytes, 0, bytes.Length);
                Array.Clear(hash, 0, hash.Length);
            }
        }

        private static byte[] TransformWithCurrentUser(byte[] input, bool protect)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                throw new PlatformNotSupportedException("本机 API Key 加密目前仅支持 Windows。");
            }

            if (input == null || input.Length == 0)
            {
                throw new ArgumentException("加密数据不能为空。", nameof(input));
            }

            var inputBlob = new DataBlob();
            var outputBlob = new DataBlob();
            IntPtr description = IntPtr.Zero;
            try
            {
                inputBlob.Length = input.Length;
                inputBlob.Data = Marshal.AllocHGlobal(input.Length);
                Marshal.Copy(input, 0, inputBlob.Data, input.Length);
                bool success = protect
                    ? CryptProtectData(
                        ref inputBlob,
                        null,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        CryptProtectUiForbidden,
                        out outputBlob)
                    : CryptUnprotectData(
                        ref inputBlob,
                        out description,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        CryptProtectUiForbidden,
                        out outputBlob);
                if (!success)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                var output = new byte[outputBlob.Length];
                Marshal.Copy(outputBlob.Data, output, 0, outputBlob.Length);
                return output;
            }
            finally
            {
                if (inputBlob.Data != IntPtr.Zero)
                {
                    ZeroUnmanagedMemory(inputBlob.Data, inputBlob.Length);
                    Marshal.FreeHGlobal(inputBlob.Data);
                }

                if (outputBlob.Data != IntPtr.Zero)
                {
                    ZeroUnmanagedMemory(outputBlob.Data, outputBlob.Length);
                    LocalFree(outputBlob.Data);
                }

                if (description != IntPtr.Zero)
                {
                    LocalFree(description);
                }
            }
        }

        private static void ZeroUnmanagedMemory(IntPtr address, int length)
        {
            for (int index = 0; index < length; index++)
            {
                Marshal.WriteByte(address, index, 0);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int Length;
            public IntPtr Data;
        }

        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptProtectData(
            ref DataBlob dataIn,
            string dataDescription,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStructure,
            int flags,
            out DataBlob dataOut);

        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptUnprotectData(
            ref DataBlob dataIn,
            out IntPtr dataDescription,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStructure,
            int flags,
            out DataBlob dataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        private const int CryptProtectUiForbidden = 0x1;
    }
}
