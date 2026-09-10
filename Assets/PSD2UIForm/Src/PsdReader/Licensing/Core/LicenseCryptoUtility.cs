using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using LicenseExceptionFactoryNamespace;
using UnityEngine;
using Object = UnityEngine.Object;
using cn.efunstudio.psdreader;

namespace LicenseCryptoUtilityNamespace
{
    internal sealed class LicenseCryptoUtility
    {
        private static readonly byte[] _rsaEncryptionOidValue = new byte[9] { 42, 134, 72, 134, 247, 13, 1, 1, 1 };

        private static readonly uint[] _embeddedMasterKeyPartA = new uint[8] { 1775658578u, 2358843861u, 1718708087u, 2657958266u, 2848839017u, 3219125356u, 2915019234u, 4221418734u };

        private static readonly uint[] _embeddedMasterKeyPartB = new uint[8] { 1945739969u, 1544636587u, 2195160652u, 2443463463u, 786195971u, 4213299385u, 277204594u, 1740235791u };

        private static string _cachedAssemblyFingerprint = string.Empty;

        internal static LicenseCryptoUtility s_ObfuscationSentinel;

        [SpecialName]
        internal static string GetCryptoVersion()
        {
            return "v1";
        }

        internal static string ExtractDigits(object text)
        {
            if (!string.IsNullOrWhiteSpace((string)text))
            {
                StringBuilder stringBuilder = new StringBuilder(((string)text).Length);
                for (int i = 0; i < ((string)text).Length; i++)
                {
                    if (char.IsDigit(((string)text)[i]))
                    {
                        stringBuilder.Append(((string)text)[i]);
                    }
                }
                return stringBuilder.ToString();
            }
            return string.Empty;
        }

        internal static string NormalizeIdentifier(object id)
        {
            if (!string.IsNullOrWhiteSpace((string)id))
            {
                StringBuilder stringBuilder = new StringBuilder(((string)id).Length);
                for (int i = 0; i < ((string)id).Length; i++)
                {
                    char c = char.ToLowerInvariant(((string)id)[i]);
                    if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-')
                    {
                        stringBuilder.Append(c);
                    }
                }
                return stringBuilder.ToString();
            }
            return string.Empty;
        }

        internal static string NormalizeHex(object text)
        {
            if (!string.IsNullOrWhiteSpace((string)text))
            {
                StringBuilder stringBuilder = new StringBuilder(((string)text).Length);
                for (int i = 0; i < ((string)text).Length; i++)
                {
                    char c = char.ToLowerInvariant(((string)text)[i]);
                    if ((c >= 'a' && c <= 'f') || (c >= '0' && c <= '9'))
                    {
                        stringBuilder.Append(c);
                    }
                }
                return stringBuilder.ToString();
            }
            return string.Empty;
        }

        internal static string TrimOrEmpty(object text)
        {
            if (string.IsNullOrWhiteSpace((string)text))
            {
                return string.Empty;
            }
            return ((string)text).Trim();
        }

        internal static string ComputeDeviceIdHash(object value)
        {
            string text = TrimOrEmpty(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }
            return ComputeStringSha256Hex("GPUAnim|DeviceId|" + GetCryptoVersion() + "|" + text);
        }

        internal static string ComputeOrderLookupId(object id)
        {
            return ComputeStringSha256Hex("EFunStudio|License|" + GetCryptoVersion() + "|psd2ugui|" + (string)id);
        }

        internal static string ComputeStringSha256Hex(object text)
        {
            using SHA256 sHA = SHA256.Create();
            return BytesToHex(sHA.ComputeHash(Encoding.UTF8.GetBytes((string)(text ?? string.Empty))));
        }

        internal static string ComputeBytesSha256Hex(object bytes)
        {
            using SHA256 sHA = SHA256.Create();
            return BytesToHex(sHA.ComputeHash((byte[])(bytes ?? Array.Empty<byte>())));
        }

        internal static byte[] ComputeStringSha256(object text)
        {
            using SHA256 sHA = SHA256.Create();
            return sHA.ComputeHash(Encoding.UTF8.GetBytes((string)(text ?? string.Empty)));
        }

        internal static byte[] ComputeStringHmacSha256(object bytes, object text)
        {
            using HMACSHA256 hMACSHA = new HMACSHA256((byte[])(bytes ?? Array.Empty<byte>()));
            return hMACSHA.ComputeHash(Encoding.UTF8.GetBytes((string)(text ?? string.Empty)));
        }

        internal static byte[] ComputeHmacSha256(object bytes, object bytes2)
        {
            using HMACSHA256 hMACSHA = new HMACSHA256((byte[])(bytes ?? Array.Empty<byte>()));
            return hMACSHA.ComputeHash((byte[])(bytes2 ?? Array.Empty<byte>()));
        }

        internal static byte[] GetEmbeddedMasterKey()
        {
            byte[] array = new byte[_embeddedMasterKeyPartA.Length * 4];
            for (int i = 0; i < _embeddedMasterKeyPartA.Length; i++)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(_embeddedMasterKeyPartA[i] ^ _embeddedMasterKeyPartB[i]), 0, array, i * 4, 4);
            }
            return array;
        }

        internal static string ComputeAssemblyFingerprint()
        {
            Assembly assembly;
            object obj;
            if (string.IsNullOrWhiteSpace(_cachedAssemblyFingerprint))
            {
                assembly = typeof(LicenseCryptoUtility).Assembly;
                if ((object)assembly != null)
                {
                    AssemblyName name = assembly.GetName();
                    if (name != null)
                    {
                        obj = name.Name;
                        if (obj != null)
                        {
                            goto IL_0043;
                        }
                    }
                    else
                    {
                        obj = null;
                    }
                }
                else
                {
                    obj = null;
                }
                obj = string.Empty;
                goto IL_0043;
            }
            return _cachedAssemblyFingerprint;
            IL_0074:
            object obj2;
            string text = (string)obj2;
            object obj3;
            if ((object)assembly != null)
            {
                Module manifestModule = assembly.ManifestModule;
                if ((object)manifestModule == null)
                {
                    obj3 = null;
                }
                else
                {
                    obj3 = manifestModule.Name;
                    if (obj3 != null)
                    {
                        goto IL_0099;
                    }
                }
            }
            else
            {
                obj3 = null;
            }
            obj3 = string.Empty;
            goto IL_0099;
            IL_0099:
            string text2 = (string)obj3;
            object obj4;
            if ((object)assembly != null)
            {
                Module manifestModule2 = assembly.ManifestModule;
                if ((object)manifestModule2 == null)
                {
                    obj4 = null;
                }
                else
                {
                    obj4 = manifestModule2.ModuleVersionId.ToString("N");
                    if (obj4 != null)
                    {
                        goto IL_00cc;
                    }
                }
            }
            else
            {
                obj4 = null;
            }
            obj4 = string.Empty;
            goto IL_00cc;
            IL_00cc:
            string text3 = (string)obj4;
            string text4 = string.Empty;
            try
            {
                object obj5;
                if ((object)assembly != null)
                {
                    obj5 = assembly.Location;
                    if (obj5 != null)
                    {
                        goto IL_00ec;
                    }
                }
                else
                {
                    obj5 = null;
                }
                obj5 = string.Empty;
                goto IL_00ec;
                IL_00ec:
                string text5 = (string)obj5;
                if (!string.IsNullOrWhiteSpace(text5) && File.Exists(text5))
                {
                    text4 = ComputeBytesSha256Hex(File.ReadAllBytes(text5));
                }
            }
            catch
            {
                text4 = string.Empty;
            }
            string text6;
            _cachedAssemblyFingerprint = ComputeStringSha256Hex("psd2ugui|AssemblyFingerprint|" + GetCryptoVersion() + "|" + text6 + "|" + text + "|" + text2 + "|" + text3 + "|" + text4);
            return _cachedAssemblyFingerprint;
            IL_0043:
            text6 = (string)obj;
            if ((object)assembly == null)
            {
                obj2 = null;
            }
            else
            {
                AssemblyName name2 = assembly.GetName();
                if (name2 != null)
                {
                    Version version = name2.Version;
                    if ((object)version == null)
                    {
                        obj2 = null;
                    }
                    else
                    {
                        obj2 = version.ToString();
                        if (obj2 != null)
                        {
                            goto IL_0074;
                        }
                    }
                }
                else
                {
                    obj2 = null;
                }
            }
            obj2 = string.Empty;
            goto IL_0074;
        }

        internal static string ComputeProjectScopeHash(object value)
        {
            return ComputeStringSha256Hex("psd2ugui|ProjectScope|" + GetCryptoVersion() + "|" + NormalizeScope(value));
        }

        internal static byte[] DeriveRepositoryDocumentKey(object value)
        {
            return ComputeStringHmacSha256(GetEmbeddedMasterKey(), "EFunStudio|RepoDoc|" + GetCryptoVersion() + "|" + NormalizeScope(value));
        }

        internal static byte[] DeriveLicenseAccessKey(object value, object value2)
        {
            return ComputeStringHmacSha256(GetEmbeddedMasterKey(), "EFunStudio|LicenseAccess|" + GetCryptoVersion() + "|psd2ugui|" + ExtractDigits(value) + "|" + NormalizeHex(value2));
        }

        internal static byte[] DeriveLicensePayloadKey(object value, object value2)
        {
            return ComputeStringHmacSha256(value2, "EFunStudio|LicensePayload|" + GetCryptoVersion() + "|psd2ugui|" + NormalizeHex(value));
        }

        internal static byte[] DeriveOfflineLeaseKey(object value, object value2)
        {
            return ComputeStringHmacSha256(value2, "EFunStudio|OfflineLease|" + GetCryptoVersion() + "|psd2ugui|" + NormalizeHex(value));
        }

        internal static string Base64UrlEncode(object bytes)
        {
            return Convert.ToBase64String((byte[])(bytes ?? Array.Empty<byte>())).TrimEnd('=').Replace('+', '-')
                .Replace('/', '_');
        }

        internal static byte[] Base64UrlDecode(object url)
        {
            if (!string.IsNullOrWhiteSpace((string)url))
            {
                string text = ((string)url).Replace('-', '+').Replace('_', '/');
                int num = (4 - text.Length % 4) & 3;
                if (num > 0)
                {
                    text = text.PadRight(text.Length + num, '=');
                }
                return Convert.FromBase64String(text);
            }
            return Array.Empty<byte>();
        }

        internal static string ComputeLicenseEntitlementFingerprint(object psdReaderLicensePayloadDocument, int value, int value2, int value3, object value4, object value5, object value6)
        {
            if (psdReaderLicensePayloadDocument == null)
            {
                return ComputeStringSha256Hex(string.Format("{0}|null|{1}|{2}|{3}", "psd2ugui", Math.Max(1, value), Math.Max(1, value2), Math.Max(1, value3)));
            }
            return ComputeStringSha256Hex(string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}", "psd2ugui", ((PsdReaderLicensePayloadDocument)psdReaderLicensePayloadDocument).LicenseId, ((PsdReaderLicensePayloadDocument)psdReaderLicensePayloadDocument).LookupId, ((PsdReaderLicensePayloadDocument)psdReaderLicensePayloadDocument).Revision, ((PsdReaderLicensePayloadDocument)psdReaderLicensePayloadDocument).IssuedUtcTicks, ((PsdReaderLicensePayloadDocument)psdReaderLicensePayloadDocument).SupportUntilUtcTicks, ((PsdReaderLicensePayloadDocument)psdReaderLicensePayloadDocument).FeatureMask, Math.Max(1, value), Math.Max(1, value2), Math.Max(1, value3), value4 ?? string.Empty, NormalizeHex(value5), BytesToHex(((PsdReaderLicensePayloadDocument)psdReaderLicensePayloadDocument).GrantSeed), BytesToHex(value6)));
        }

        internal static string ComputeLicenseCacheProof(object psdReaderLicenseCacheDocument, object value, DateTime dateTime3, DateTime dateTime4)
        {
            if (psdReaderLicenseCacheDocument != null)
            {
                DateTime dateTime = EnsureUtc(dateTime3);
                DateTime dateTime2 = EnsureUtc(dateTime4);
                string text = ComputeStringSha256Hex((((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).MetaEnvelopeBase64 ?? string.Empty) + "|" + (((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).LicenseEnvelopeBase64 ?? string.Empty) + "|" + (((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).OfflineLeaseEnvelopeBase64 ?? string.Empty) + "|" + (((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).DeviceShardEnvelopeBase64 ?? string.Empty));
                return ComputeStringSha256Hex(string.Format("{0}|CacheProof|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}|{14}|{15}|{16}|{17}|{18}", "psd2ugui", GetCryptoVersion(), NormalizeHex(((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).LookupId), NormalizeHex(((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).LicenseId), ((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).StatusCode, ((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).Revision, ((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).FeatureMask, ((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).CatalogMask, Math.Max(1, ((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).CurrentMajorVersion), Math.Max(1, ((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).MaxMajorVersion), Math.Max(1, ((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).LocalCacheTimeoutDays), Math.Max(1, ((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).MetaRevision), NormalizeIdentifier(((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).ActiveKid), NormalizeIdentifier(((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).ActivationSource), NormalizeHex(((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).StateSaltHex), NormalizeHex(value), text, dateTime.Ticks, dateTime2.Ticks));
            }
            return string.Empty;
        }

        internal static string ComputeValidationTranscriptHead(object psdReaderLicenseCacheDocument, object value, DateTime dateTime3, DateTime dateTime4, object value2)
        {
            if (psdReaderLicenseCacheDocument == null)
            {
                return string.Empty;
            }
            DateTime dateTime = EnsureUtc(dateTime3);
            DateTime dateTime2 = EnsureUtc(dateTime4);
            string text = NormalizeHex(value2);
            string text2 = ComputeStringSha256Hex((((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).MetaEnvelopeBase64 ?? string.Empty) + "|" + (((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).LicenseEnvelopeBase64 ?? string.Empty) + "|" + (((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).OfflineLeaseEnvelopeBase64 ?? string.Empty) + "|" + (((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).DeviceShardEnvelopeBase64 ?? string.Empty));
            return ComputeStringSha256Hex(string.Format("{0}|TranscriptHead|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}|{14}|{15}|{16}|{17}|{18}|{19}", "psd2ugui", GetCryptoVersion(), text, NormalizeHex(((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).LookupId), NormalizeHex(((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).LicenseId), ((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).StatusCode, ((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).Revision, ((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).FeatureMask, ((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).CatalogMask, Math.Max(1, ((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).CurrentMajorVersion), Math.Max(1, ((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).MaxMajorVersion), Math.Max(1, ((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).LocalCacheTimeoutDays), Math.Max(1, ((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).MetaRevision), NormalizeIdentifier(((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).ActiveKid), NormalizeIdentifier(((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).ActivationSource), NormalizeHex(((PsdReaderLicenseCacheDocument)psdReaderLicenseCacheDocument).StateSaltHex), NormalizeHex(value), text2, dateTime.Ticks, dateTime2.Ticks));
        }

        internal static string EncryptClockWatermark(DateTime dateTime3, DateTime dateTime4, object text, object text2, object text3)
        {
            if (!string.IsNullOrWhiteSpace((string)text3) && !string.IsNullOrWhiteSpace((string)text) && !string.IsNullOrWhiteSpace((string)text2))
            {
                DateTime dateTime = EnsureUtc(dateTime3);
                DateTime dateTime2 = EnsureUtc(dateTime4);
                byte[] array;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using BinaryWriter binaryWriter = new BinaryWriter(memoryStream, Encoding.UTF8, leaveOpen: true);
                    binaryWriter.Write(827805008u);
                    binaryWriter.Write(1);
                    binaryWriter.Write(dateTime.Ticks);
                    binaryWriter.Write(dateTime2.Ticks);
                    WriteLengthPrefixedString(binaryWriter, NormalizeHex(text));
                    WriteLengthPrefixedString(binaryWriter, NormalizeHex(text2));
                    binaryWriter.Flush();
                    array = memoryStream.ToArray();
                }
                byte[] array3;
                byte[] array2 = EncryptAesCbc(array, DeriveClockWatermarkEncryptionKey(text3), out array3);
                byte[] array4;
                using (MemoryStream memoryStream2 = new MemoryStream())
                {
                    using BinaryWriter binaryWriter2 = new BinaryWriter(memoryStream2, Encoding.UTF8, leaveOpen: true);
                    binaryWriter2.Write(827804995u);
                    binaryWriter2.Write(1);
                    WriteLengthPrefixedBytes(binaryWriter2, array3);
                    WriteLengthPrefixedBytes(binaryWriter2, array2);
                    binaryWriter2.Flush();
                    array4 = memoryStream2.ToArray();
                }
                byte[] array5 = ComputeHmacSha256(DeriveClockWatermarkMacKey(text3), array4);
                using MemoryStream memoryStream3 = new MemoryStream();
                using (BinaryWriter binaryWriter3 = new BinaryWriter(memoryStream3, Encoding.UTF8, leaveOpen: true))
                {
                    binaryWriter3.Write(array4);
                    WriteLengthPrefixedBytes(binaryWriter3, array5);
                    binaryWriter3.Flush();
                }
                return Base64UrlEncode(memoryStream3.ToArray());
            }
            return string.Empty;
        }

        internal static bool TryDecryptClockWatermark(object text, object text2, out DateTime result, out DateTime result2, out string result3, out string result4)
        {
            result = DateTime.MinValue;
            result2 = DateTime.MinValue;
            result3 = string.Empty;
            result4 = string.Empty;
            if (!string.IsNullOrWhiteSpace((string)text) && !string.IsNullOrWhiteSpace((string)text2))
            {
                try
                {
                    byte[] array = Base64UrlDecode(text);
                    if (array != null && array.Length != 0)
                    {
                        using (MemoryStream memoryStream = new MemoryStream(array, writable: false))
                        {
                            using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
                            uint num = binaryReader.ReadUInt32();
                            int num2 = binaryReader.ReadInt32();
                            if (num == 827804995 && num2 == 1)
                            {
                                byte[] array2 = ReadLengthPrefixedBytes(binaryReader, 64);
                                byte[] array3 = ReadLengthPrefixedBytes(binaryReader, 1048576);
                                byte[] array4 = ReadLengthPrefixedBytes(binaryReader, 128);
                                if (array4.Length == 0 || memoryStream.Position != memoryStream.Length)
                                {
                                    return false;
                                }
                                int num3 = array.Length - array4.Length - 4;
                                if (num3 <= 0)
                                {
                                    return false;
                                }
                                byte[] array5 = new byte[num3];
                                Buffer.BlockCopy(array, 0, array5, 0, array5.Length);
                                if (ByteArraysEqual(ComputeHmacSha256(DeriveClockWatermarkMacKey(text2), array5), array4))
                                {
                                    using MemoryStream memoryStream2 = new MemoryStream(DecryptAesCbc(array3, DeriveClockWatermarkEncryptionKey(text2), array2), writable: false);
                                    using BinaryReader binaryReader2 = new BinaryReader(memoryStream2, Encoding.UTF8, leaveOpen: false);
                                    uint num4 = binaryReader2.ReadUInt32();
                                    int num5 = binaryReader2.ReadInt32();
                                    if (num4 == 827805008 && num5 == 1)
                                    {
                                        long ticks = binaryReader2.ReadInt64();
                                        long ticks2 = binaryReader2.ReadInt64();
                                        result3 = NormalizeHex(ReadLengthPrefixedString(binaryReader2));
                                        result4 = NormalizeHex(ReadLengthPrefixedString(binaryReader2));
                                        if (memoryStream2.Position != memoryStream2.Length)
                                        {
                                            return false;
                                        }
                                        result = new DateTime(ticks, DateTimeKind.Utc);
                                        result2 = new DateTime(ticks2, DateTimeKind.Utc);
                                        return !string.IsNullOrWhiteSpace(result3) && !string.IsNullOrWhiteSpace(result4);
                                    }
                                    return false;
                                }
                                return false;
                            }
                            return false;
                        }
                    }
                    return false;
                }
                catch
                {
                    result = DateTime.MinValue;
                    result2 = DateTime.MinValue;
                    result3 = string.Empty;
                    result4 = string.Empty;
                    return false;
                }
            }
            return false;
        }

        internal static int MixIntegrityWords(object value)
        {
            if (value != null && ((Array)value).Length != 0)
            {
                int num = 1595565015;
                for (int i = 0; i < ((Array)value).Length; i++)
                {
                    int num2 = ((int[])value)[i] ^ -1640531527 ^ (i * 73244475);
                    num = (num << 5) | (int)((uint)num >> 27);
                    num = num ^ num2 ^ (num >> 3);
                }
                return num;
            }
            return 0;
        }

        internal static string DecodeUtf8(object bytes)
        {
            return Encoding.UTF8.GetString((byte[])(bytes ?? Array.Empty<byte>()));
        }

        internal static DateTime ParseUtcOrDefault(object utcText, DateTime defaultValue)
        {
            if (DateTime.TryParse((string)utcText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedUtc))
            {
                return parsedUtc.ToUniversalTime();
            }
            return defaultValue;
        }

        internal static string BytesToHex(object value)
        {
            if (value != null && ((Array)value).Length != 0)
            {
                char[] array = new char[((Array)value).Length * 2];
                for (int i = 0; i < ((Array)value).Length; i++)
                {
                    byte b = ((byte[])value)[i];
                    array[i * 2] = "0123456789abcdef"[b >> 4];
                    array[i * 2 + 1] = "0123456789abcdef"[b & 0xF];
                }
                return new string(array);
            }
            return string.Empty;
        }

        internal static byte[] HexToBytes(object value)
        {
            string text = NormalizeHex(value);
            if (!string.IsNullOrWhiteSpace(text) && (text.Length & 1) == 0)
            {
                byte[] array = new byte[text.Length / 2];
                int num = 0;
                while (true)
                {
                    if (num < array.Length)
                    {
                        int num2 = ParseHexNibble(text[num * 2]);
                        int num3 = ParseHexNibble(text[num * 2 + 1]);
                        if (num2 < 0 || num3 < 0)
                        {
                            break;
                        }
                        array[num] = (byte)((num2 << 4) | num3);
                        num++;
                        continue;
                    }
                    return array;
                }
                return Array.Empty<byte>();
            }
            return Array.Empty<byte>();
        }

        internal static string EncryptDeviceBoundSecret(object value, object value2)
        {
            byte[] array2;
            byte[] array = EncryptAesCbc(value, DeriveDeviceBoundSecretKey(value2), out array2);
            return Base64UrlEncode(array2) + "." + Base64UrlEncode(array);
        }

        internal static bool TryDecryptDeviceBoundSecret(object text, object value, out byte[] result)
        {
            result = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace((string)text))
            {
                return false;
            }
            string[] array = ((string)text).Split('.', StringSplitOptions.None);
            if (array.Length != 2)
            {
                return false;
            }
            try
            {
                byte[] array2 = Base64UrlDecode(array[0]);
                byte[] array3 = Base64UrlDecode(array[1]);
                result = DecryptAesCbc(array3, DeriveDeviceBoundSecretKey(value), array2);
                return result.Length != 0;
            }
            catch
            {
                result = Array.Empty<byte>();
                return false;
            }
        }

        internal static byte[] EncryptLicenseCachePayload(object array5, object text)
        {
            if (array5 != null && ((Array)array5).Length != 0 && !string.IsNullOrWhiteSpace((string)text))
            {
                byte[] array2;
                byte[] array = EncryptAesCbc(array5, DeriveLicenseCacheEncryptionKey(text), out array2);
                byte[] array3;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using BinaryWriter binaryWriter = new BinaryWriter(memoryStream, Encoding.UTF8, leaveOpen: true);
                    binaryWriter.Write(826360645u);
                    binaryWriter.Write(1);
                    WriteLengthPrefixedBytes(binaryWriter, array2);
                    WriteLengthPrefixedBytes(binaryWriter, array);
                    binaryWriter.Flush();
                    array3 = memoryStream.ToArray();
                }
                byte[] array4 = ComputeHmacSha256(DeriveLicenseCacheMacKey(text), array3);
                using MemoryStream memoryStream2 = new MemoryStream();
                using (BinaryWriter binaryWriter2 = new BinaryWriter(memoryStream2, Encoding.UTF8, leaveOpen: true))
                {
                    binaryWriter2.Write(array3);
                    WriteLengthPrefixedBytes(binaryWriter2, array4);
                    binaryWriter2.Flush();
                }
                return memoryStream2.ToArray();
            }
            return Array.Empty<byte>();
        }

        internal static bool TryDecryptLicenseCachePayload(object value, object text, out byte[] result)
        {
            result = Array.Empty<byte>();
            if (value == null || ((Array)value).Length == 0 || string.IsNullOrWhiteSpace((string)text))
            {
                return false;
            }
            try
            {
                using MemoryStream memoryStream = new MemoryStream((byte[])value, writable: false);
                using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
                uint num = binaryReader.ReadUInt32();
                int num2 = binaryReader.ReadInt32();
                if (num == 826360645 && num2 == 1)
                {
                    byte[] array = ReadLengthPrefixedBytes(binaryReader, 64);
                    byte[] array2 = ReadLengthPrefixedBytes(binaryReader, 16777216);
                    byte[] array3 = ReadLengthPrefixedBytes(binaryReader, 128);
                    if (array3.Length == 0 || memoryStream.Position != memoryStream.Length)
                    {
                        return false;
                    }
                    int num3 = ((Array)value).Length - array3.Length - 4;
                    if (num3 <= 0)
                    {
                        return false;
                    }
                    byte[] array4 = new byte[num3];
                    Buffer.BlockCopy((Array)value, 0, array4, 0, array4.Length);
                    if (!ByteArraysEqual(ComputeHmacSha256(DeriveLicenseCacheMacKey(text), array4), array3))
                    {
                        return false;
                    }
                    result = DecryptAesCbc(array2, DeriveLicenseCacheEncryptionKey(text), array);
                    return result.Length != 0;
                }
                return false;
            }
            catch
            {
                result = Array.Empty<byte>();
                return false;
            }
        }

        internal static byte[] SerializeProjectLicenseBundle(object value)
        {
            if (!TryNormalizeProjectLicenseBundle(value, out var psdReaderProjectLicenseBundleDocument))
            {
                return Array.Empty<byte>();
            }
            psdReaderProjectLicenseBundleDocument.BundleSeal = ComputeProjectLicenseBundleSeal(psdReaderProjectLicenseBundleDocument);
            byte[] array;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using BinaryWriter binaryWriter = new BinaryWriter(memoryStream, Encoding.UTF8, leaveOpen: true);
                binaryWriter.Write(827081296u);
                binaryWriter.Write(2);
                WriteLengthPrefixedString(binaryWriter, psdReaderProjectLicenseBundleDocument.VendorCode);
                WriteLengthPrefixedString(binaryWriter, psdReaderProjectLicenseBundleDocument.ProductCode);
                WriteLengthPrefixedString(binaryWriter, psdReaderProjectLicenseBundleDocument.LookupId);
                WriteLengthPrefixedString(binaryWriter, psdReaderProjectLicenseBundleDocument.LicenseId);
                WriteLengthPrefixedString(binaryWriter, psdReaderProjectLicenseBundleDocument.MetaEnvelopeBase64);
                WriteLengthPrefixedString(binaryWriter, psdReaderProjectLicenseBundleDocument.LicenseEnvelopeBase64);
                WriteLengthPrefixedString(binaryWriter, psdReaderProjectLicenseBundleDocument.OfflineLeaseEnvelopeBase64);
                WriteLengthPrefixedString(binaryWriter, psdReaderProjectLicenseBundleDocument.WrappedLicenseAccessKey);
                WriteLengthPrefixedString(binaryWriter, psdReaderProjectLicenseBundleDocument.WrappedOrderIdCipher);
                binaryWriter.Write(psdReaderProjectLicenseBundleDocument.ExportIssuedUtcTicks);
                binaryWriter.Write(psdReaderProjectLicenseBundleDocument.ExportExpiresUtcTicks);
                WriteLengthPrefixedString(binaryWriter, psdReaderProjectLicenseBundleDocument.BundleSeal);
                binaryWriter.Flush();
                array = memoryStream.ToArray();
            }
            byte[] array3;
            byte[] array2 = EncryptAesCbc(array, DeriveProjectLicenseEncryptionKey(), out array3);
            byte[] array4;
            using (MemoryStream memoryStream2 = new MemoryStream())
            {
                using BinaryWriter binaryWriter2 = new BinaryWriter(memoryStream2, Encoding.UTF8, leaveOpen: true);
                binaryWriter2.Write(827084871u);
                binaryWriter2.Write(2);
                WriteLengthPrefixedBytes(binaryWriter2, array3);
                WriteLengthPrefixedBytes(binaryWriter2, array2);
                binaryWriter2.Flush();
                array4 = memoryStream2.ToArray();
            }
            byte[] array5 = ComputeHmacSha256(DeriveProjectLicenseMacKey(), array4);
            using MemoryStream memoryStream3 = new MemoryStream();
            using (BinaryWriter binaryWriter3 = new BinaryWriter(memoryStream3, Encoding.UTF8, leaveOpen: true))
            {
                binaryWriter3.Write(array4);
                WriteLengthPrefixedBytes(binaryWriter3, array5);
                binaryWriter3.Flush();
            }
            return memoryStream3.ToArray();
        }

        internal static bool TryDeserializeProjectLicenseBundle(object value, out PsdReaderProjectLicenseBundleDocument result)
        {
            result = null;
            if (value == null || ((Array)value).Length == 0)
            {
                return false;
            }
            try
            {
                using MemoryStream memoryStream = new MemoryStream((byte[])value, writable: false);
                using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
                uint num = binaryReader.ReadUInt32();
                int num2 = binaryReader.ReadInt32();
                if (num == 827084871 && num2 == 2)
                {
                    byte[] array = ReadLengthPrefixedBytes(binaryReader, 64);
                    byte[] array2 = ReadLengthPrefixedBytes(binaryReader, 4194304);
                    byte[] array3 = ReadLengthPrefixedBytes(binaryReader, 128);
                    if (array3.Length == 0 || memoryStream.Position != memoryStream.Length)
                    {
                        return false;
                    }
                    int num3 = ((Array)value).Length - array3.Length - 4;
                    if (num3 <= 0)
                    {
                        return false;
                    }
                    byte[] array4 = new byte[num3];
                    Buffer.BlockCopy((Array)value, 0, array4, 0, array4.Length);
                    if (ByteArraysEqual(ComputeHmacSha256(DeriveProjectLicenseMacKey(), array4), array3))
                    {
                        using (MemoryStream memoryStream2 = new MemoryStream(DecryptAesCbc(array2, DeriveProjectLicenseEncryptionKey(), array), writable: false))
                        {
                            using BinaryReader binaryReader2 = new BinaryReader(memoryStream2, Encoding.UTF8, leaveOpen: false);
                            uint num4 = binaryReader2.ReadUInt32();
                            int num5 = binaryReader2.ReadInt32();
                            if (num4 == 827081296 && num5 == 2)
                            {
                                PsdReaderProjectLicenseBundleDocument psdReaderProjectLicenseBundleDocument = new PsdReaderProjectLicenseBundleDocument
                                {
                                    Schema = num5,
                                    VendorCode = ReadLengthPrefixedString(binaryReader2),
                                    ProductCode = ReadLengthPrefixedString(binaryReader2),
                                    LookupId = ReadLengthPrefixedString(binaryReader2),
                                    LicenseId = ReadLengthPrefixedString(binaryReader2),
                                    MetaEnvelopeBase64 = ReadLengthPrefixedString(binaryReader2),
                                    LicenseEnvelopeBase64 = ReadLengthPrefixedString(binaryReader2),
                                    OfflineLeaseEnvelopeBase64 = ReadLengthPrefixedString(binaryReader2),
                                    WrappedLicenseAccessKey = ReadLengthPrefixedString(binaryReader2),
                                    WrappedOrderIdCipher = ReadLengthPrefixedString(binaryReader2),
                                    ExportIssuedUtcTicks = binaryReader2.ReadInt64(),
                                    ExportExpiresUtcTicks = binaryReader2.ReadInt64(),
                                    BundleSeal = ReadLengthPrefixedString(binaryReader2)
                                };
                                psdReaderProjectLicenseBundleDocument.ExportIssuedUtc = ((psdReaderProjectLicenseBundleDocument.ExportIssuedUtcTicks > 0L) ? new DateTime(psdReaderProjectLicenseBundleDocument.ExportIssuedUtcTicks, DateTimeKind.Utc).ToString("O") : string.Empty);
                                psdReaderProjectLicenseBundleDocument.ExportExpiresUtc = ((psdReaderProjectLicenseBundleDocument.ExportExpiresUtcTicks <= 0L) ? string.Empty : new DateTime(psdReaderProjectLicenseBundleDocument.ExportExpiresUtcTicks, DateTimeKind.Utc).ToString("O"));
                                if (memoryStream2.Position == memoryStream2.Length && TryNormalizeProjectLicenseBundle(psdReaderProjectLicenseBundleDocument, out var psdReaderProjectLicenseBundleDocument2))
                                {
                                    if (string.Equals(ComputeProjectLicenseBundleSeal(psdReaderProjectLicenseBundleDocument2), psdReaderProjectLicenseBundleDocument2.BundleSeal, StringComparison.Ordinal))
                                    {
                                        result = psdReaderProjectLicenseBundleDocument2;
                                        return true;
                                    }
                                    return false;
                                }
                                return false;
                            }
                            return false;
                        }
                    }
                    return false;
                }
                return false;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        internal static string WrapProjectLicenseSecret(object bytes, object value, DateTime dateTime2)
        {
            byte[] array = (byte[])(bytes ?? Array.Empty<byte>());
            string text = NormalizeHex(value);
            DateTime dateTime = EnsureUtc(dateTime2);
            if (array.Length != 0 && !string.IsNullOrWhiteSpace(text) && !(dateTime <= DateTime.MinValue))
            {
                byte[] array3;
                byte[] array2 = EncryptAesCbc(array, DeriveProjectLicenseWrapEncryptionKey(text, dateTime.Ticks), out array3);
                byte[] array4;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using BinaryWriter binaryWriter = new BinaryWriter(memoryStream, Encoding.UTF8, leaveOpen: true);
                    binaryWriter.Write(827015511u);
                    binaryWriter.Write(1);
                    WriteLengthPrefixedBytes(binaryWriter, array3);
                    WriteLengthPrefixedBytes(binaryWriter, array2);
                    binaryWriter.Flush();
                    array4 = memoryStream.ToArray();
                }
                byte[] array5 = ComputeHmacSha256(DeriveProjectLicenseWrapMacKey(text, dateTime.Ticks), array4);
                using MemoryStream memoryStream2 = new MemoryStream();
                using (BinaryWriter binaryWriter2 = new BinaryWriter(memoryStream2, Encoding.UTF8, leaveOpen: true))
                {
                    binaryWriter2.Write(array4);
                    WriteLengthPrefixedBytes(binaryWriter2, array5);
                    binaryWriter2.Flush();
                }
                return Base64UrlEncode(memoryStream2.ToArray());
            }
            return string.Empty;
        }

        internal static bool TryUnwrapProjectLicenseSecret(object text2, object value, DateTime dateTime2, out byte[] result)
        {
            result = Array.Empty<byte>();
            string text = NormalizeHex(value);
            DateTime dateTime = EnsureUtc(dateTime2);
            if (!string.IsNullOrWhiteSpace((string)text2) && !string.IsNullOrWhiteSpace(text) && !(dateTime <= DateTime.MinValue))
            {
                try
                {
                    byte[] array = Base64UrlDecode(text2);
                    using MemoryStream memoryStream = new MemoryStream(array, writable: false);
                    using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
                    uint num = binaryReader.ReadUInt32();
                    int num2 = binaryReader.ReadInt32();
                    if (num == 827015511 && num2 == 1)
                    {
                        byte[] array2 = ReadLengthPrefixedBytes(binaryReader, 64);
                        byte[] array3 = ReadLengthPrefixedBytes(binaryReader, 1024);
                        byte[] array4 = ReadLengthPrefixedBytes(binaryReader, 128);
                        if (array4.Length == 0 || memoryStream.Position != memoryStream.Length)
                        {
                            return false;
                        }
                        int num3 = array.Length - array4.Length - 4;
                        if (num3 > 0)
                        {
                            byte[] array5 = new byte[num3];
                            Buffer.BlockCopy(array, 0, array5, 0, array5.Length);
                            if (ByteArraysEqual(ComputeHmacSha256(DeriveProjectLicenseWrapMacKey(text, dateTime.Ticks), array5), array4))
                            {
                                result = DecryptAesCbc(array3, DeriveProjectLicenseWrapEncryptionKey(text, dateTime.Ticks), array2);
                                return result.Length != 0;
                            }
                            return false;
                        }
                        return false;
                    }
                    return false;
                }
                catch
                {
                    result = Array.Empty<byte>();
                    return false;
                }
            }
            return false;
        }

        internal static string ComputeProjectLicenseBundleSeal(object value)
        {
            if (!TryNormalizeProjectLicenseBundle(value, out var psdReaderProjectLicenseBundleDocument))
            {
                return string.Empty;
            }
            string text = ComputeStringSha256Hex(psdReaderProjectLicenseBundleDocument.MetaEnvelopeBase64 + "|" + psdReaderProjectLicenseBundleDocument.LicenseEnvelopeBase64 + "|" + psdReaderProjectLicenseBundleDocument.OfflineLeaseEnvelopeBase64);
            string text2 = $"{psdReaderProjectLicenseBundleDocument.VendorCode}|{psdReaderProjectLicenseBundleDocument.ProductCode}|{NormalizeHex(psdReaderProjectLicenseBundleDocument.LookupId)}|{NormalizeHex(psdReaderProjectLicenseBundleDocument.LicenseId)}|{NormalizeHex(psdReaderProjectLicenseBundleDocument.WrappedLicenseAccessKey)}|{NormalizeHex(psdReaderProjectLicenseBundleDocument.WrappedOrderIdCipher)}|{text}|{psdReaderProjectLicenseBundleDocument.ExportIssuedUtcTicks}|{psdReaderProjectLicenseBundleDocument.ExportExpiresUtcTicks}";
            return BytesToHex(ComputeStringHmacSha256(DeriveProjectLicenseSealKey(), text2));
        }

        private static bool TryNormalizeProjectLicenseBundle(object value, out PsdReaderProjectLicenseBundleDocument result)
        {
            result = null;
            if (value == null)
            {
                return false;
            }
            DateTime dateTime = ((((PsdReaderProjectLicenseBundleDocument)value).ExportIssuedUtcTicks > 0L) ? new DateTime(((PsdReaderProjectLicenseBundleDocument)value).ExportIssuedUtcTicks, DateTimeKind.Utc) : ParseUtcOrDefault(((PsdReaderProjectLicenseBundleDocument)value).ExportIssuedUtc, DateTime.MinValue));
            DateTime dateTime2 = ((((PsdReaderProjectLicenseBundleDocument)value).ExportExpiresUtcTicks > 0L) ? new DateTime(((PsdReaderProjectLicenseBundleDocument)value).ExportExpiresUtcTicks, DateTimeKind.Utc) : ParseUtcOrDefault(((PsdReaderProjectLicenseBundleDocument)value).ExportExpiresUtc, DateTime.MinValue));
            if (dateTime <= DateTime.MinValue || dateTime2 <= dateTime)
            {
                return false;
            }
            if (((PsdReaderProjectLicenseBundleDocument)value).Schema != 2)
            {
                return false;
            }
            string text = NormalizeHex(((PsdReaderProjectLicenseBundleDocument)value).LookupId);
            string text2 = (((PsdReaderProjectLicenseBundleDocument)value).LicenseId ?? string.Empty).Trim();
            string text3 = (((PsdReaderProjectLicenseBundleDocument)value).MetaEnvelopeBase64 ?? string.Empty).Trim();
            string text4 = (((PsdReaderProjectLicenseBundleDocument)value).LicenseEnvelopeBase64 ?? string.Empty).Trim();
            string text5 = (((PsdReaderProjectLicenseBundleDocument)value).OfflineLeaseEnvelopeBase64 ?? string.Empty).Trim();
            string text6 = (((PsdReaderProjectLicenseBundleDocument)value).WrappedLicenseAccessKey ?? string.Empty).Trim();
            string text7 = (((PsdReaderProjectLicenseBundleDocument)value).WrappedOrderIdCipher ?? string.Empty).Trim();
            if (string.Equals(((PsdReaderProjectLicenseBundleDocument)value).VendorCode, "efunstudio", StringComparison.Ordinal) && string.Equals(((PsdReaderProjectLicenseBundleDocument)value).ProductCode, "psd2ugui", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(text2) && !string.IsNullOrWhiteSpace(text3) && !string.IsNullOrWhiteSpace(text4) && !string.IsNullOrWhiteSpace(text5) && !string.IsNullOrWhiteSpace(text6) && !string.IsNullOrWhiteSpace(text7))
            {
                result = new PsdReaderProjectLicenseBundleDocument
                {
                    Schema = 2,
                    VendorCode = "efunstudio",
                    ProductCode = "psd2ugui",
                    LookupId = text,
                    LicenseId = text2,
                    MetaEnvelopeBase64 = text3,
                    LicenseEnvelopeBase64 = text4,
                    OfflineLeaseEnvelopeBase64 = text5,
                    WrappedLicenseAccessKey = text6,
                    WrappedOrderIdCipher = text7,
                    ExportIssuedUtcTicks = dateTime.Ticks,
                    ExportIssuedUtc = dateTime.ToString("O"),
                    ExportExpiresUtcTicks = dateTime2.Ticks,
                    ExportExpiresUtc = dateTime2.ToString("O"),
                    BundleSeal = NormalizeHex(((PsdReaderProjectLicenseBundleDocument)value).BundleSeal)
                };
                return true;
            }
            return false;
        }

        internal static bool TryReadEncryptedSignedEnvelope(object value, object value2, object value3, out PsdReaderSignedEnvelopeDocument result)
        {
            result = null;
            if (value == null || ((Array)value).Length < 17)
            {
                return false;
            }
            try
            {
                using MemoryStream memoryStream = new MemoryStream((byte[])value, writable: false);
                using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: true);
                if (binaryReader.ReadByte() == 69 && binaryReader.ReadByte() == 70 && binaryReader.ReadByte() == 83 && binaryReader.ReadByte() == 66)
                {
                    if (binaryReader.ReadByte() != 1)
                    {
                        return false;
                    }
                    int num = binaryReader.ReadInt32();
                    int num2 = binaryReader.ReadInt32();
                    if (num > 0 && num <= 64 && num2 > 0 && num2 <= ((Array)value).Length)
                    {
                        byte[] array = binaryReader.ReadBytes(num);
                        byte[] array2 = binaryReader.ReadBytes(num2);
                        if (array.Length == num && array2.Length == num2 && memoryStream.Position == memoryStream.Length)
                        {
                            using MemoryStream memoryStream2 = new MemoryStream(DecryptAesCbc(array2, DeriveBinaryEnvelopeKey(value2, value3), array), writable: false);
                            using BinaryReader binaryReader2 = new BinaryReader(memoryStream2, Encoding.UTF8, leaveOpen: true);
                            result = new PsdReaderSignedEnvelopeDocument
                            {
                                Schema = binaryReader2.ReadInt32(),
                                Kind = ReadLengthPrefixedString(binaryReader2),
                                Kid = ReadLengthPrefixedString(binaryReader2),
                                Alg = ReadLengthPrefixedString(binaryReader2),
                                Enc = ReadLengthPrefixedString(binaryReader2),
                                Kdf = ReadLengthPrefixedString(binaryReader2),
                                Scope = ReadLengthPrefixedString(binaryReader2),
                                Iv = ReadLengthPrefixedString(binaryReader2),
                                Payload = ReadLengthPrefixedString(binaryReader2),
                                Sig = ReadLengthPrefixedString(binaryReader2)
                            };
                            return memoryStream2.Position == memoryStream2.Length;
                        }
                        return false;
                    }
                    return false;
                }
                return false;
            }
            catch (Exception)
            {
                result = null;
                return false;
            }
        }

        internal static bool TryVerifyAndDecryptEnvelope(object text, object psdReaderSignedEnvelopeDocument, object text2, object value, out byte[] result)
        {
            result = Array.Empty<byte>();
            if (!string.IsNullOrWhiteSpace((string)text) && psdReaderSignedEnvelopeDocument != null && !string.IsNullOrWhiteSpace(((PsdReaderSignedEnvelopeDocument)psdReaderSignedEnvelopeDocument).Payload) && !string.IsNullOrWhiteSpace(((PsdReaderSignedEnvelopeDocument)psdReaderSignedEnvelopeDocument).Sig) && !string.IsNullOrWhiteSpace(((PsdReaderSignedEnvelopeDocument)psdReaderSignedEnvelopeDocument).Iv))
            {
                if (((PsdReaderSignedEnvelopeDocument)psdReaderSignedEnvelopeDocument).Schema == 1 && string.Equals(((PsdReaderSignedEnvelopeDocument)psdReaderSignedEnvelopeDocument).Kind, "SecureEnvelope", StringComparison.Ordinal) && string.Equals(((PsdReaderSignedEnvelopeDocument)psdReaderSignedEnvelopeDocument).Alg, "RS256", StringComparison.Ordinal) && string.Equals(((PsdReaderSignedEnvelopeDocument)psdReaderSignedEnvelopeDocument).Enc, "AES256-CBC", StringComparison.Ordinal) && string.Equals(((PsdReaderSignedEnvelopeDocument)psdReaderSignedEnvelopeDocument).Kdf, "EFUN-HMACSHA256-V1", StringComparison.Ordinal))
                {
                    if (!string.IsNullOrWhiteSpace((string)text2) && !string.Equals((string)text2, ((PsdReaderSignedEnvelopeDocument)psdReaderSignedEnvelopeDocument).Scope ?? string.Empty, StringComparison.Ordinal))
                    {
                        return false;
                    }
                    try
                    {
                        byte[] signature = Base64UrlDecode(((PsdReaderSignedEnvelopeDocument)psdReaderSignedEnvelopeDocument).Sig);
                        using RSA rSA = RSA.Create();
                        ImportRsaPublicKeyFromPem(rSA, text);
                        if (!rSA.VerifyData(BuildEnvelopeSigningPayload(psdReaderSignedEnvelopeDocument), signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                        {
                            return false;
                        }
                        byte[] array = Base64UrlDecode(((PsdReaderSignedEnvelopeDocument)psdReaderSignedEnvelopeDocument).Iv);
                        byte[] array2 = Base64UrlDecode(((PsdReaderSignedEnvelopeDocument)psdReaderSignedEnvelopeDocument).Payload);
                        result = DecryptAesCbc(array2, value, array);
                        return result.Length != 0;
                    }
                    catch (Exception)
                    {
                        result = Array.Empty<byte>();
                        return false;
                    }
                }
                return false;
            }
            return false;
        }

        private static void ImportRsaPublicKeyFromPem(object value, object value2)
        {
            if (value == null)
            {
                throw LicenseExceptionFactory.CreateArgumentNullException();
            }
            byte[] array = DecodePemBlock(value2, "PUBLIC KEY");
            ((RSA)value).ImportParameters(ParseRsaSubjectPublicKeyInfo(array));
        }

        private static RSAParameters ParseRsaSubjectPublicKeyInfo(object value)
        {
            int num = 0;
            int num2 = ReadDerElementEnd(value, ref num, 48);
            int num3 = ReadDerElementEnd(value, ref num, 48);
            if (ByteArraysEqual(ReadDerElement(value, ref num, 6), _rsaEncryptionOidValue))
            {
                if (num < num3 && ((byte[])value)[num] == 5 && ReadDerElementEnd(value, ref num, 5) != num)
                {
                    throw LicenseExceptionFactory.CreateInvalidOperationException();
                }
                num = num3;
                byte[] array = ReadDerBitString(value, ref num);
                if (num != num2)
                {
                    throw LicenseExceptionFactory.CreateInvalidOperationException();
                }
                int num4 = 0;
                int num5 = ReadDerElementEnd(array, ref num4, 48);
                byte[] modulus = TrimLeadingZeroBytes(ReadDerElement(array, ref num4, 2));
                byte[] exponent = TrimLeadingZeroBytes(ReadDerElement(array, ref num4, 2));
                if (num4 != num5)
                {
                    throw LicenseExceptionFactory.CreateInvalidOperationException();
                }
                return new RSAParameters
                {
                    Modulus = modulus,
                    Exponent = exponent
                };
            }
            throw LicenseExceptionFactory.CreateInvalidOperationException();
        }

        private static int ReadDerElementEnd(object value, ref int value2, byte value3)
        {
            if (value != null && value2 < ((Array)value).Length)
            {
                if (((byte[])value)[value2++] != value3)
                {
                    throw LicenseExceptionFactory.CreateInvalidOperationException();
                }
                int num = ReadDerLength(value, ref value2);
                int num2 = value2 + num;
                if (num < 0 || num2 < value2 || num2 > ((Array)value).Length)
                {
                    throw LicenseExceptionFactory.CreateInvalidOperationException();
                }
                return num2;
            }
            throw LicenseExceptionFactory.CreateInvalidOperationException();
        }

        private static int ReadDerLength(object value, ref int value2)
        {
            if (value != null && value2 < ((Array)value).Length)
            {
                byte b = ((byte[])value)[value2++];
                if ((b & 0x80) == 0)
                {
                    return b;
                }
                int num = b & 0x7F;
                if (num > 0 && num <= 4 && value2 + num <= ((Array)value).Length)
                {
                    int num2 = 0;
                    for (int i = 0; i < num; i++)
                    {
                        num2 = (num2 << 8) | ((byte[])value)[value2++];
                    }
                    return num2;
                }
                throw LicenseExceptionFactory.CreateInvalidOperationException();
            }
            throw LicenseExceptionFactory.CreateInvalidOperationException();
        }

        private static byte[] ReadDerElement(object value, ref int value2, byte value3)
        {
            int num = ReadDerElementEnd(value, ref value2, value3);
            int num2 = value2;
            int num3 = num - num2;
            byte[] array = new byte[num3];
            Buffer.BlockCopy((Array)value, num2, array, 0, num3);
            value2 = num;
            return array;
        }

        private static byte[] ReadDerBitString(object value, ref int value2)
        {
            int num = ReadDerElementEnd(value, ref value2, 3);
            if (value2 < num)
            {
                if (((byte[])value)[value2++] != 0)
                {
                    throw LicenseExceptionFactory.CreateInvalidOperationException();
                }
                int num2 = num - value2;
                byte[] array = new byte[num2];
                Buffer.BlockCopy((Array)value, value2, array, 0, num2);
                value2 = num;
                return array;
            }
            throw LicenseExceptionFactory.CreateInvalidOperationException();
        }

        private static byte[] TrimLeadingZeroBytes(object value)
        {
            if (value != null && ((Array)value).Length != 0)
            {
                int i;
                for (i = 0; i < ((Array)value).Length - 1 && ((byte[])value)[i] == 0; i++)
                {
                }
                if (i != 0)
                {
                    byte[] array = new byte[((Array)value).Length - i];
                    Buffer.BlockCopy((Array)value, i, array, 0, array.Length);
                    return array;
                }
                return (byte[])value;
            }
            throw LicenseExceptionFactory.CreateInvalidOperationException();
        }

        private static bool ByteArraysEqual(object value, object value2)
        {
            if (value == value2)
            {
                return true;
            }
            if (value != null && value2 != null && ((Array)value).Length == ((Array)value2).Length)
            {
                for (int i = 0; i < ((Array)value).Length; i++)
                {
                    if (((byte[])value)[i] != ((byte[])value2)[i])
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        private static byte[] DecodePemBlock(object value2, object value3)
        {
            string text = "-----BEGIN " + (string)value3 + "-----";
            string value = "-----END " + (string)value3 + "-----";
            int num = ((string)value2).IndexOf(text, StringComparison.Ordinal);
            int num2 = ((string)value2).IndexOf(value, StringComparison.Ordinal);
            if (num < 0 || num2 <= num)
            {
                throw LicenseExceptionFactory.CreateInvalidOperationException();
            }
            return Convert.FromBase64String(((string)value2).Substring(num + text.Length, num2 - num - text.Length).Replace("\r", string.Empty).Replace("\n", string.Empty)
                .Trim());
        }

        private static byte[] BuildEnvelopeSigningPayload(object value)
        {
            string[] obj = new string[17]
            {
                $"schema:{((PsdReaderSignedEnvelopeDocument)value)?.Schema ?? 0}\n",
                "kind:",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            };
            object obj2;
            if (value == null)
            {
                obj2 = null;
            }
            else
            {
                obj2 = ((PsdReaderSignedEnvelopeDocument)value).Kind;
                if (obj2 != null)
                {
                    goto IL_0044;
                }
            }
            obj2 = string.Empty;
            goto IL_0044;
            IL_0064:
            object obj3;
            obj[4] = (string)obj3;
            obj[5] = "\nalg:";
            object obj4;
            if (value == null)
            {
                obj4 = null;
            }
            else
            {
                obj4 = ((PsdReaderSignedEnvelopeDocument)value).Alg;
                if (obj4 != null)
                {
                    goto IL_0084;
                }
            }
            obj4 = string.Empty;
            goto IL_0084;
            IL_0084:
            obj[6] = (string)obj4;
            obj[7] = "\nenc:";
            object obj5;
            if (value != null)
            {
                obj5 = ((PsdReaderSignedEnvelopeDocument)value).Enc;
                if (obj5 != null)
                {
                    goto IL_00a4;
                }
            }
            else
            {
                obj5 = null;
            }
            obj5 = string.Empty;
            goto IL_00a4;
            IL_00c6:
            object obj6;
            obj[10] = (string)obj6;
            obj[11] = "\nscope:";
            object obj7;
            if (value != null)
            {
                obj7 = ((PsdReaderSignedEnvelopeDocument)value).Scope;
                if (obj7 != null)
                {
                    goto IL_00e8;
                }
            }
            else
            {
                obj7 = null;
            }
            obj7 = string.Empty;
            goto IL_00e8;
            IL_012c:
            object obj8;
            obj[16] = (string)obj8;
            string s = string.Concat(obj);
            return Encoding.UTF8.GetBytes(s);
            IL_00a4:
            obj[8] = (string)obj5;
            obj[9] = "\nkdf:";
            if (value != null)
            {
                obj6 = ((PsdReaderSignedEnvelopeDocument)value).Kdf;
                if (obj6 != null)
                {
                    goto IL_00c6;
                }
            }
            else
            {
                obj6 = null;
            }
            obj6 = string.Empty;
            goto IL_00c6;
            IL_00e8:
            obj[12] = (string)obj7;
            obj[13] = "\niv:";
            object obj9;
            if (value != null)
            {
                obj9 = ((PsdReaderSignedEnvelopeDocument)value).Iv;
                if (obj9 != null)
                {
                    goto IL_010a;
                }
            }
            else
            {
                obj9 = null;
            }
            obj9 = string.Empty;
            goto IL_010a;
            IL_0044:
            obj[2] = (string)obj2;
            obj[3] = "\nkid:";
            if (value != null)
            {
                obj3 = ((PsdReaderSignedEnvelopeDocument)value).Kid;
                if (obj3 != null)
                {
                    goto IL_0064;
                }
            }
            else
            {
                obj3 = null;
            }
            obj3 = string.Empty;
            goto IL_0064;
            IL_010a:
            obj[14] = (string)obj9;
            obj[15] = "\npayload:";
            if (value == null)
            {
                obj8 = null;
            }
            else
            {
                obj8 = ((PsdReaderSignedEnvelopeDocument)value).Payload;
                if (obj8 != null)
                {
                    goto IL_012c;
                }
            }
            obj8 = string.Empty;
            goto IL_012c;
        }

        private static byte[] EncryptAesCbc(object value, object value2, out byte[] result)
        {
            using Aes aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = NormalizeAes256Key(value2);
            aes.GenerateIV();
            result = aes.IV;
            using ICryptoTransform cryptoTransform = aes.CreateEncryptor();
            return cryptoTransform.TransformFinalBlock((byte[])(value ?? Array.Empty<byte>()), 0, (value != null) ? ((Array)value).Length : 0);
        }

        private static byte[] DecryptAesCbc(object value, object value2, object value3)
        {
            using Aes aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = NormalizeAes256Key(value2);
            aes.IV = (byte[])(value3 ?? Array.Empty<byte>());
            using ICryptoTransform cryptoTransform = aes.CreateDecryptor();
            return cryptoTransform.TransformFinalBlock((byte[])(value ?? Array.Empty<byte>()), 0, (value != null) ? ((Array)value).Length : 0);
        }

        private static byte[] DeriveBinaryEnvelopeKey(object value, object value2)
        {
            return ComputeStringHmacSha256(value ?? Array.Empty<byte>(), "EFunStudio|BinaryEnvelope|v1|" + NormalizeScope(value2));
        }

        private static string ReadLengthPrefixedString(object value)
        {
            int num = ((BinaryReader)value).ReadInt32();
            if (num >= 0 && num <= 16777216)
            {
                byte[] array = ((BinaryReader)value).ReadBytes(num);
                if (array.Length != num)
                {
                    throw LicenseExceptionFactory.CreateEndOfStreamException();
                }
                return Encoding.UTF8.GetString(array);
            }
            throw LicenseExceptionFactory.CreateInvalidOperationException();
        }

        private static byte[] ReadLengthPrefixedBytes(object value, int value2)
        {
            int num = ((BinaryReader)value).ReadInt32();
            if (num >= 0 && num <= Mathf.Max(1, value2))
            {
                byte[] array = ((BinaryReader)value).ReadBytes(num);
                if (array.Length != num)
                {
                    throw LicenseExceptionFactory.CreateEndOfStreamException();
                }
                return array;
            }
            throw LicenseExceptionFactory.CreateInvalidOperationException();
        }

        private static void WriteLengthPrefixedString(object value, object value2)
        {
            byte[] bytes = Encoding.UTF8.GetBytes((string)(value2 ?? string.Empty));
            ((BinaryWriter)value).Write(bytes.Length);
            ((BinaryWriter)value).Write(bytes);
        }

        private static void WriteLengthPrefixedBytes(object value, object value2)
        {
            byte[] array = (byte[])(value2 ?? Array.Empty<byte>());
            ((BinaryWriter)value).Write(array.Length);
            ((BinaryWriter)value).Write(array);
        }

        private static byte[] DeriveDeviceBoundSecretKey(object value)
        {
            return ComputeStringHmacSha256(GetEmbeddedMasterKey(), "EFunStudio|CacheLocal|v3|psd2ugui|" + NormalizeHex(value));
        }

        private static byte[] DeriveLicenseCacheEncryptionKey(object value)
        {
            return ComputeStringHmacSha256(GetEmbeddedMasterKey(), "EFunStudio|CacheStore|v1|psd2ugui|enc|" + NormalizeHex(value));
        }

        private static byte[] DeriveLicenseCacheMacKey(object value)
        {
            return ComputeStringHmacSha256(GetEmbeddedMasterKey(), "EFunStudio|CacheStore|v1|psd2ugui|mac|" + NormalizeHex(value));
        }

        private static byte[] DeriveProjectLicenseEncryptionKey()
        {
            return ComputeStringHmacSha256(GetEmbeddedMasterKey(), "EFunStudio|ProjectLicense|" + GetCryptoVersion() + "|psd2ugui|enc");
        }

        private static byte[] DeriveProjectLicenseMacKey()
        {
            return ComputeStringHmacSha256(GetEmbeddedMasterKey(), "EFunStudio|ProjectLicense|" + GetCryptoVersion() + "|psd2ugui|mac");
        }

        private static byte[] DeriveProjectLicenseSealKey()
        {
            return ComputeStringHmacSha256(GetEmbeddedMasterKey(), "EFunStudio|ProjectLicense|" + GetCryptoVersion() + "|psd2ugui|seal");
        }

        private static byte[] DeriveProjectLicenseWrapEncryptionKey(object value, long value2)
        {
            return ComputeStringHmacSha256(GetEmbeddedMasterKey(), string.Format("EFunStudio|ProjectLicense|{0}|{1}|wrap|enc|{2}|{3}", GetCryptoVersion(), "psd2ugui", NormalizeHex(value), value2));
        }

        private static byte[] DeriveProjectLicenseWrapMacKey(object value, long value2)
        {
            return ComputeStringHmacSha256(GetEmbeddedMasterKey(), string.Format("EFunStudio|ProjectLicense|{0}|{1}|wrap|mac|{2}|{3}", GetCryptoVersion(), "psd2ugui", NormalizeHex(value), value2));
        }

        private static byte[] DeriveClockWatermarkEncryptionKey(object value)
        {
            return ComputeStringHmacSha256(GetEmbeddedMasterKey(), "EFunStudio|ClockWatermark|" + GetCryptoVersion() + "|psd2ugui|enc|" + NormalizeHex(value));
        }

        private static byte[] DeriveClockWatermarkMacKey(object value)
        {
            return ComputeStringHmacSha256(GetEmbeddedMasterKey(), "EFunStudio|ClockWatermark|" + GetCryptoVersion() + "|psd2ugui|mac|" + NormalizeHex(value));
        }

        private static int ParseHexNibble(char value)
        {
            if (value >= '0' && value <= '9')
            {
                return value - 48;
            }
            if (value < 'a' || value > 'f')
            {
                return -1;
            }
            return value - 97 + 10;
        }

        private static byte[] NormalizeAes256Key(object value)
        {
            byte[] array = new byte[32];
            if (value != null && ((Array)value).Length != 0)
            {
                if (((Array)value).Length < array.Length)
                {
                    for (int i = 0; i < array.Length; i += ((Array)value).Length)
                    {
                        int count = Math.Min(((Array)value).Length, array.Length - i);
                        Buffer.BlockCopy((Array)value, 0, array, i, count);
                    }
                    return array;
                }
                Buffer.BlockCopy((Array)value, 0, array, 0, array.Length);
                return array;
            }
            return array;
        }

        private static string NormalizeScope(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                StringBuilder stringBuilder = new StringBuilder(((string)value).Length);
                for (int i = 0; i < ((string)value).Length; i++)
                {
                    char c = char.ToLowerInvariant(((string)value)[i]);
                    if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == ':' || c == '/' || c == '.')
                    {
                        stringBuilder.Append(c);
                    }
                }
                if (stringBuilder.Length == 0)
                {
                    return "default";
                }
                return stringBuilder.ToString();
            }
            return "default";
        }

        private static DateTime EnsureUtc(DateTime value)
        {
            if (value.Kind != DateTimeKind.Utc)
            {
                return value.ToUniversalTime();
            }
            return value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LicenseCryptoUtility GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
