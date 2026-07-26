namespace PsdLayoutTool2
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text;
    using UnityEditor;

    internal interface IPsdProtectedDataAdapter
    {
        byte[] Protect(byte[] plaintext);

        byte[] Unprotect(byte[] protectedData);
    }

    internal interface IPsdLocalValueStore
    {
        bool HasValue(string name);

        bool TryRead(string name, out string value);

        void Save(string name, string value);

        void Clear(string name);
    }

    internal sealed class PsdAiSecretStoreException : InvalidOperationException
    {
        public PsdAiSecretStoreException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    internal sealed class PsdAiSecretStore : IPsdAiSecretStore
    {
        private const string StoragePrefix = "PsdLayoutTool2.AiSecret.v1";
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly IPsdLocalValueStore localValueStore;
        private readonly IPsdProtectedDataAdapter protectedDataAdapter;

        public PsdAiSecretStore()
            : this(new EditorPrefsLocalValueStore(), new WindowsDpapiProtectedDataAdapter())
        {
        }

        internal PsdAiSecretStore(
            IPsdLocalValueStore localValueStore,
            IPsdProtectedDataAdapter protectedDataAdapter)
        {
            this.localValueStore = localValueStore ?? throw new ArgumentNullException(nameof(localValueStore));
            this.protectedDataAdapter =
                protectedDataAdapter ?? throw new ArgumentNullException(nameof(protectedDataAdapter));
        }

        public bool HasSavedCredential(
            string projectIdentity,
            PsdHierarchyAiProvider provider)
        {
            string storageName = BuildStorageName(projectIdentity, provider);
            try
            {
                return localValueStore.HasValue(storageName);
            }
            catch (Exception exception)
            {
                throw CreateStoreException("check", exception);
            }
        }

        public bool TryRead(
            string projectIdentity,
            PsdHierarchyAiProvider provider,
            out string key)
        {
            key = string.Empty;
            string storageName = BuildStorageName(projectIdentity, provider);
            string serializedValue;
            try
            {
                if (!localValueStore.TryRead(storageName, out serializedValue))
                {
                    return false;
                }
            }
            catch (Exception exception)
            {
                throw CreateStoreException("read", exception);
            }

            byte[] protectedBytes = null;
            byte[] plaintextBytes = null;
            try
            {
                protectedBytes = Convert.FromBase64String(serializedValue);
                plaintextBytes = protectedDataAdapter.Unprotect(protectedBytes);
                if (plaintextBytes == null || plaintextBytes.Length == 0)
                {
                    throw new InvalidDataException("The protected value contains no credential.");
                }

                key = StrictUtf8.GetString(plaintextBytes);
                if (string.IsNullOrWhiteSpace(key))
                {
                    key = string.Empty;
                    throw new InvalidDataException("The protected value contains no credential.");
                }

                return true;
            }
            catch (Exception exception)
            {
                key = string.Empty;
                throw CreateStoreException("decrypt", exception);
            }
            finally
            {
                ClearBytes(protectedBytes);
                ClearBytes(plaintextBytes);
            }
        }

        public void Save(
            string projectIdentity,
            PsdHierarchyAiProvider provider,
            string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("An AI credential is required.", nameof(key));
            }

            string storageName = BuildStorageName(projectIdentity, provider);
            byte[] plaintextBytes = null;
            byte[] protectedBytes = null;
            try
            {
                plaintextBytes = StrictUtf8.GetBytes(key);
                protectedBytes = protectedDataAdapter.Protect(plaintextBytes);
                if (protectedBytes == null || protectedBytes.Length == 0)
                {
                    throw new InvalidDataException("Credential protection returned no data.");
                }

                localValueStore.Save(storageName, Convert.ToBase64String(protectedBytes));
            }
            catch (Exception exception)
            {
                throw CreateStoreException("save", exception);
            }
            finally
            {
                ClearBytes(plaintextBytes);
                ClearBytes(protectedBytes);
            }
        }

        public void Clear(string projectIdentity, PsdHierarchyAiProvider provider)
        {
            string storageName = BuildStorageName(projectIdentity, provider);
            try
            {
                localValueStore.Clear(storageName);
            }
            catch (Exception exception)
            {
                throw CreateStoreException("clear", exception);
            }
        }

        private static string BuildStorageName(
            string projectIdentity,
            PsdHierarchyAiProvider provider)
        {
            ValidateProvider(provider);
            string normalizedIdentity = NormalizeProjectIdentity(projectIdentity);
            byte[] identityBytes = null;
            byte[] identityHash = null;
            try
            {
                identityBytes = StrictUtf8.GetBytes(normalizedIdentity);
                using (SHA256 sha256 = SHA256.Create())
                {
                    identityHash = sha256.ComputeHash(identityBytes);
                }

                var hashText = new StringBuilder(identityHash.Length * 2);
                for (int index = 0; index < identityHash.Length; index++)
                {
                    hashText.Append(identityHash[index].ToString("x2"));
                }

                return StoragePrefix + "." + hashText + "." + provider;
            }
            finally
            {
                ClearBytes(identityBytes);
                ClearBytes(identityHash);
            }
        }

        private static string NormalizeProjectIdentity(string projectIdentity)
        {
            if (string.IsNullOrWhiteSpace(projectIdentity))
            {
                throw new ArgumentException("A project identity is required.", nameof(projectIdentity));
            }

            string normalized = Path.GetFullPath(projectIdentity.Trim())
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            string root = Path.GetPathRoot(normalized) ?? string.Empty;
            while (normalized.Length > root.Length &&
                   (normalized.EndsWith("\\", StringComparison.Ordinal) ||
                    normalized.EndsWith("/", StringComparison.Ordinal)))
            {
                normalized = normalized.Substring(0, normalized.Length - 1);
            }

            return normalized.ToUpperInvariant();
        }

        private static void ValidateProvider(PsdHierarchyAiProvider provider)
        {
            switch (provider)
            {
                case PsdHierarchyAiProvider.Codex:
                case PsdHierarchyAiProvider.Claude:
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported AI provider.");
            }
        }

        private static PsdAiSecretStoreException CreateStoreException(
            string operation,
            Exception innerException)
        {
            return new PsdAiSecretStoreException(
                "Unable to " + operation +
                " the local AI credential. Verify that Windows user data protection is available and retry.",
                innerException);
        }

        private static void ClearBytes(byte[] bytes)
        {
            if (bytes != null)
            {
                Array.Clear(bytes, 0, bytes.Length);
            }
        }
    }

    internal sealed class EditorPrefsLocalValueStore : IPsdLocalValueStore
    {
        public bool HasValue(string name)
        {
            return EditorPrefs.HasKey(name);
        }

        public bool TryRead(string name, out string value)
        {
            if (!EditorPrefs.HasKey(name))
            {
                value = string.Empty;
                return false;
            }

            value = EditorPrefs.GetString(name, string.Empty);
            return true;
        }

        public void Save(string name, string value)
        {
            EditorPrefs.SetString(name, value);
        }

        public void Clear(string name)
        {
            EditorPrefs.DeleteKey(name);
        }
    }

    internal sealed class WindowsDpapiProtectedDataAdapter : IPsdProtectedDataAdapter
    {
        private const int CryptProtectUiForbidden = 0x1;

        public byte[] Protect(byte[] plaintext)
        {
            EnsureWindows();
            return Transform(plaintext, true);
        }

        public byte[] Unprotect(byte[] protectedData)
        {
            EnsureWindows();
            return Transform(protectedData, false);
        }

        private static byte[] Transform(byte[] input, bool protect)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (input.Length == 0)
            {
                throw new ArgumentException("Protected data cannot be empty.", nameof(input));
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

        private static void EnsureWindows()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                throw new PlatformNotSupportedException(
                    "Windows DPAPI is required to protect local AI credentials.");
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
    }
}
