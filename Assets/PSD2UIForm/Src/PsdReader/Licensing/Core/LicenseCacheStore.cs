using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;
using cn.efunstudio.psdreader;
using LicenseCryptoUtilityNamespace;
using LicensePathProviderNamespace;

namespace LicenseCacheStoreNamespace
{
    internal sealed class LicenseCacheStore
    {
        private static LicenseCacheStore s_ObfuscationSentinel;

        internal PsdReaderLicenseCacheDocument Load()
        {
            string path = LicensePathProvider.GetLicenseCacheFilePath();
            if (File.Exists(path))
            {
                try
                {
                    string text = GetDeviceEncryptionKey();
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return null;
                    }
                    if (LicenseCryptoUtility.TryDecryptLicenseCachePayload(File.ReadAllBytes(path), text, out var bytes))
                    {
                        string text2 = Encoding.UTF8.GetString(bytes);
                        if (!string.IsNullOrWhiteSpace(text2))
                        {
                            PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument = JsonUtility.FromJson<PsdReaderLicenseCacheDocument>(text2);
                            if (psdReaderLicenseCacheDocument != null && psdReaderLicenseCacheDocument.Schema == 1)
                            {
                                return psdReaderLicenseCacheDocument;
                            }
                            Clear();
                            return null;
                        }
                        return null;
                    }
                    Clear();
                    return null;
                }
                catch (Exception)
                {
                    Clear();
                    return null;
                }
            }
            return null;
        }

        internal void Save(PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument)
        {
            if (psdReaderLicenseCacheDocument == null)
            {
                return;
            }
            string text = GetDeviceEncryptionKey();
            if (!string.IsNullOrWhiteSpace(text))
            {
                Directory.CreateDirectory(LicensePathProvider.GetLicenseCacheDirectory());
                string s = JsonUtility.ToJson((object)psdReaderLicenseCacheDocument, true);
                byte[] array = LicenseCryptoUtility.EncryptLicenseCachePayload(Encoding.UTF8.GetBytes(s), text);
                if (array != null && array.Length != 0)
                {
                    File.WriteAllBytes(LicensePathProvider.GetLicenseCacheFilePath(), array);
                }
            }
        }

        internal void Clear()
        {
            string path = LicensePathProvider.GetLicenseCacheFilePath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            string path2 = LicensePathProvider.GetLicenseCacheDirectory();
            if (Directory.Exists(path2) && !Directory.EnumerateFileSystemEntries(path2).Any())
            {
                Directory.Delete(path2);
            }
        }

        private static string GetDeviceEncryptionKey()
        {
            return LicenseCryptoUtility.ComputeDeviceIdHash(SystemInfo.deviceUniqueIdentifier);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LicenseCacheStore GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
