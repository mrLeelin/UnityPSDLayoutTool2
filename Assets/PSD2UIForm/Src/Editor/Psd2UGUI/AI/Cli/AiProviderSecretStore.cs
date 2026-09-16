using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    /// <summary>API Key 仅保存为当前 Windows 用户可解密的本地 EditorPrefs 值，绝不序列化到 Unity asset。</summary>
    internal static class AiProviderSecretStore
    {
        private const string KeyPrefix = "PSD2UIForm.AiProviderKey.";

        internal static bool HasKey(AiProviderKind provider)
        {
            return !string.IsNullOrWhiteSpace(EditorPrefs.GetString(GetStorageKey(provider), string.Empty));
        }

        internal static void Save(AiProviderKind provider, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return;
            byte[] encrypted = Protect(Encoding.UTF8.GetBytes(apiKey.Trim()));
            EditorPrefs.SetString(GetStorageKey(provider), Convert.ToBase64String(encrypted));
        }

        internal static void Clear(AiProviderKind provider)
        {
            EditorPrefs.DeleteKey(GetStorageKey(provider));
        }

        internal static string ReadRequired(AiProviderKind provider)
        {
            string encoded = EditorPrefs.GetString(GetStorageKey(provider), string.Empty);
            if (string.IsNullOrWhiteSpace(encoded))
            {
                throw new InvalidOperationException("自定义 API 未配置 API Key，请在 PSD2UIForm 设置页面保存 API Key。");
            }
            try
            {
                return Encoding.UTF8.GetString(Unprotect(Convert.FromBase64String(encoded)));
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("无法读取本机加密保存的 API Key，请重新填写并保存。", exception);
            }
        }

        private static string GetStorageKey(AiProviderKind provider)
        {
            return KeyPrefix + GetProjectIdentity() + "." + (provider == AiProviderKind.ClaudeCodeCli ? "claude" : "codex");
        }

        private static byte[] Protect(byte[] data)
        {
            return Crypt(data, protect: true);
        }

        private static byte[] Unprotect(byte[] data)
        {
            return Crypt(data, protect: false);
        }

        private static byte[] Crypt(byte[] data, bool protect)
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                throw new PlatformNotSupportedException("当前平台不支持本机加密 API Key 存储。");
            }

            DataBlob input = new DataBlob(data);
            DataBlob output = new DataBlob();
            try
            {
                bool succeeded = protect
                    ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref output)
                    : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref output);
                if (!succeeded)
                {
                    throw new InvalidOperationException("Windows 无法加密或读取 API Key，错误码: " + Marshal.GetLastWin32Error());
                }

                byte[] result = new byte[output.cbData];
                Marshal.Copy(output.pbData, result, 0, result.Length);
                return result;
            }
            finally
            {
                if (input.pbData != IntPtr.Zero) Marshal.FreeHGlobal(input.pbData);
                if (output.pbData != IntPtr.Zero) LocalFree(output.pbData);
            }
        }

        private static string GetProjectIdentity()
        {
            string projectPath = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            using (SHA256 hash = SHA256.Create())
            {
                byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(projectPath.ToLowerInvariant()));
                return BitConverter.ToString(bytes, 0, 8).Replace("-", string.Empty);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DataBlob
        {
            public int cbData;
            public IntPtr pbData;

            public DataBlob(byte[] data)
            {
                cbData = data == null ? 0 : data.Length;
                pbData = cbData == 0 ? IntPtr.Zero : Marshal.AllocHGlobal(cbData);
                if (cbData > 0) Marshal.Copy(data, 0, pbData, cbData);
            }
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(ref DataBlob dataIn, string description, IntPtr optionalEntropy,
            IntPtr reserved, IntPtr promptStruct, int flags, ref DataBlob dataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr description, IntPtr optionalEntropy,
            IntPtr reserved, IntPtr promptStruct, int flags, ref DataBlob dataOut);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr memory);
    }
}
