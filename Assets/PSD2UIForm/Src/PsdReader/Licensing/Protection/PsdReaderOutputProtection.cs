using System;
using System.Text;
using PsdReaderLicenseServiceNamespace;
using RenderProtectionDescriptorNamespace;
using RenderProtectionProfileNamespace;
using cn.efunstudio.psdreader.PsdParser;
using LicenseCryptoUtilityNamespace;
using RenderProtectionSessionNamespace;

namespace cn.efunstudio.psdreader
{
    public sealed class PsdReaderOutputProtection
    {
        [Serializable]
        private sealed class ProtectedAssetSeal
        {
            public int SchemaVersion = 1;

            public string DocumentType = "ProtectedAssetSeal";

            public string VendorId = "efunstudio";

            public string ProductId = "psd2ugui";

            public string OutputFamilyId = string.Empty;

            public string OutputPath = string.Empty;

            public string SourcePath = string.Empty;

            public string LookupId = string.Empty;

            public string LicenseId = string.Empty;

            public int LicenseRevision;

            public string ClockHighWaterUtc = string.Empty;

            public string ValidationTranscriptHead = string.Empty;

            public string LedgerHash = string.Empty;

            public string ProjectIdentity = string.Empty;

            public string DeviceIdentity = string.Empty;

            public long GeneratedAtUtcTicks;

            public string GeneratedAtUtc = string.Empty;

            public string ReservedText01 = string.Empty;

            public string ReservedText02 = string.Empty;

            public string ReservedText03 = string.Empty;

            public string ReservedText04 = string.Empty;

            public string ReservedText05 = string.Empty;

            public string ReservedText06 = string.Empty;

            public string ReservedText07 = string.Empty;

            public string ReservedText08 = string.Empty;

            public bool IsAuthorized;

            public string ReservedText09 = string.Empty;

            private static ProtectedAssetSeal s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static ProtectedAssetSeal GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        internal static PsdReaderOutputProtection s_ObfuscationSentinel;

        internal static RenderProtectionSession CreateProtectionSession(int value2, int value3, int value4, int value5, int value6, object value7, object value8, bool enabled = false)
        {
            RenderProtectionDescriptor value = ResolveProtectionDescriptor(value2, value3, value4, value5, value6, value7, value8, enabled);
            if (value == null || !value.HasActiveProtection())
            {
                return null;
            }
            return new RenderProtectionSession(value, value2, value3, value4, value5, value6);
        }

        internal static string ComputeProtectionFingerprint(int value2, int value3, int value4, int value5, int value6, object value7, object value8, bool enabled = true)
        {
            RenderProtectionDescriptor value = ResolveProtectionDescriptor(value2, value3, value4, value5, value6, value7, value8, enabled);
            object obj;
            if (value != null)
            {
                obj = value.GetProtectionFingerprint();
                if (obj != null)
                {
                    goto IL_0026;
                }
            }
            else
            {
                obj = null;
            }
            obj = string.Empty;
            goto IL_0026;
            IL_0026:
            return (string)obj;
        }

        internal static void FinalizeProtectedRender(object psdRenderedImage, int value = 0)
        {
            if (psdRenderedImage != null && !((PsdRenderedImage)psdRenderedImage).IsEmpty)
            {
                PsdReaderLicenseService.MarkGenerationUsed();
            }
        }

        private static RenderProtectionDescriptor ResolveProtectionDescriptor(int value, int value2, int value3, int value4, int value5, object value6, object value7, bool enabled)
        {
            RenderProtectionProfile value8 = (enabled ? PsdReaderLicenseService.GetRefreshedOutputProtectionContext() : PsdReaderLicenseService.GetOutputProtectionContext());
            RenderProtectionDescriptor value9 = PsdReaderLicenseService.CreateEnabledProtectionDescriptor(value8, value6, value7, value5, value, value2, value3, value4);
            if (!PsdReaderLicenseService.MatchesEnabledProtectionDescriptor(value8, value9, value5, value, value2, value3, value4))
            {
                return PsdReaderLicenseService.CreateDisabledProtectionDescriptor(value8, value6, value7, value5, value, value2, value3, value4);
            }
            return value9;
        }

        internal static string ComputeOutputFamilyFingerprint(object renderProtectionProfile, object value, object value2, object value3, object value4, object value5, object value6)
        {
            if (renderProtectionProfile == null)
            {
                return string.Empty;
            }
            return ComputeSealFingerprint(BuildProtectedAssetSeal(renderProtectionProfile, string.Empty, value4, value5), ((RenderProtectionProfile)renderProtectionProfile).GetProtectionKeyHex(), value, value2, value3, value6);
        }

        private static ProtectedAssetSeal BuildProtectedAssetSeal(object value, object value2, object value3, object value4)
        {
            DateTime dateTime = ((value == null || ((RenderProtectionProfile)value).GetGeneratedAtUtcTicks() <= 0L) ? DateTime.UtcNow : new DateTime(((RenderProtectionProfile)value).GetGeneratedAtUtcTicks(), DateTimeKind.Utc));
            DateTime dateTime2 = ((value == null || !(((RenderProtectionProfile)value).GetClockHighWaterUtc() > DateTime.MinValue)) ? dateTime : ((RenderProtectionProfile)value).GetClockHighWaterUtc());
            ProtectedAssetSeal obj = new ProtectedAssetSeal
            {
                OutputFamilyId = NormalizeIdentifier(value3),
                OutputPath = NormalizeOutputPath(value4),
                SourcePath = NormalizeSourcePath(value2)
            };
            object obj2;
            if (value != null)
            {
                obj2 = ((RenderProtectionProfile)value).GetLookupId();
                if (obj2 != null)
                {
                    goto IL_0089;
                }
            }
            else
            {
                obj2 = null;
            }
            obj2 = string.Empty;
            goto IL_0089;
            IL_00e3:
            object obj3;
            obj.ValidationTranscriptHead = (string)obj3;
            object obj4;
            if (value != null)
            {
                obj4 = ((RenderProtectionProfile)value).GetLedgerHash();
                if (obj4 != null)
                {
                    goto IL_00fe;
                }
            }
            else
            {
                obj4 = null;
            }
            obj4 = string.Empty;
            goto IL_00fe;
            IL_0119:
            object obj5;
            obj.ProjectIdentity = (string)obj5;
            object obj6;
            if (value == null)
            {
                obj6 = null;
            }
            else
            {
                obj6 = ((RenderProtectionProfile)value).GetDeviceIdentity();
                if (obj6 != null)
                {
                    goto IL_0134;
                }
            }
            obj6 = string.Empty;
            goto IL_0134;
            IL_00a4:
            object obj7;
            obj.LicenseId = (string)obj7;
            obj.LicenseRevision = ((RenderProtectionProfile)value)?.GetRevision() ?? 0;
            obj.ClockHighWaterUtc = dateTime2.ToString("O");
            if (value != null)
            {
                obj3 = ((RenderProtectionProfile)value).GetValidationTranscriptHead();
                if (obj3 != null)
                {
                    goto IL_00e3;
                }
            }
            else
            {
                obj3 = null;
            }
            obj3 = string.Empty;
            goto IL_00e3;
            IL_0134:
            obj.DeviceIdentity = (string)obj6;
            obj.GeneratedAtUtcTicks = dateTime.Ticks;
            obj.GeneratedAtUtc = dateTime.ToString("O");
            obj.IsAuthorized = ((RenderProtectionProfile)value)?.GetIsAuthorized() ?? false;
            return obj;
            IL_00fe:
            obj.LedgerHash = (string)obj4;
            if (value == null)
            {
                obj5 = null;
            }
            else
            {
                obj5 = ((RenderProtectionProfile)value).GetProjectIdentity();
                if (obj5 != null)
                {
                    goto IL_0119;
                }
            }
            obj5 = string.Empty;
            goto IL_0119;
            IL_0089:
            obj.LookupId = (string)obj2;
            if (value == null)
            {
                obj7 = null;
            }
            else
            {
                obj7 = ((RenderProtectionProfile)value).GetLicenseId();
                if (obj7 != null)
                {
                    goto IL_00a4;
                }
            }
            obj7 = string.Empty;
            goto IL_00a4;
        }

        private static string ComputeSealFingerprint(object value, object value2, object value3, object value4, object value5, object value6)
        {
            if (value != null)
            {
                string text = string.Join("|", "psd2ugui", "OutputFamily", LicenseCryptoUtility.GetCryptoVersion(), LicenseCryptoUtility.NormalizeHex(((ProtectedAssetSeal)value).LookupId), LicenseCryptoUtility.NormalizeHex(((ProtectedAssetSeal)value).LicenseId), ((ProtectedAssetSeal)value).LicenseRevision.ToString(), LicenseCryptoUtility.NormalizeHex(((ProtectedAssetSeal)value).ProjectIdentity), LicenseCryptoUtility.NormalizeHex(((ProtectedAssetSeal)value).ClockHighWaterUtc), NormalizeIdentifier(value3), NormalizeIdentifier(value4), NormalizeIdentifier(value5), LicenseCryptoUtility.NormalizeHex(((ProtectedAssetSeal)value).LedgerHash), NormalizeIdentifier(((ProtectedAssetSeal)value).OutputFamilyId), NormalizeOutputPath(((ProtectedAssetSeal)value).OutputPath), LicenseCryptoUtility.NormalizeHex(value6), LicenseCryptoUtility.NormalizeHex(((ProtectedAssetSeal)value).ValidationTranscriptHead), LicenseCryptoUtility.NormalizeHex(((ProtectedAssetSeal)value).DeviceIdentity), (!((ProtectedAssetSeal)value).IsAuthorized) ? "0" : "1");
                return LicenseCryptoUtility.BytesToHex(LicenseCryptoUtility.ComputeStringHmacSha256(DeriveSealSigningKey(value2, value), text));
            }
            return string.Empty;
        }

        private static byte[] DeriveSealSigningKey(object value, object value2)
        {
            byte[] array = LicenseCryptoUtility.HexToBytes(value);
            if (array == null || array.Length == 0)
            {
                array = LicenseCryptoUtility.ComputeStringSha256(value ?? string.Empty);
            }
            return LicenseCryptoUtility.ComputeStringHmacSha256(array, string.Format("{0}|OutputKey|{1}|{2}|{3}|{4}|{5}|{6}", "psd2ugui", LicenseCryptoUtility.GetCryptoVersion(), LicenseCryptoUtility.NormalizeHex(((ProtectedAssetSeal)value2)?.LookupId), LicenseCryptoUtility.NormalizeHex(((ProtectedAssetSeal)value2)?.LicenseId), ((ProtectedAssetSeal)value2)?.LicenseRevision ?? 0, LicenseCryptoUtility.NormalizeHex(((ProtectedAssetSeal)value2)?.ProjectIdentity), LicenseCryptoUtility.NormalizeHex(((ProtectedAssetSeal)value2)?.DeviceIdentity)));
        }

        private static string NormalizeSourcePath(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return string.Empty;
            }
            return ((string)value).Replace("\\", "/").Trim();
        }

        private static string NormalizeOutputPath(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                return ((string)value).Replace("\\", "/").Trim();
            }
            return string.Empty;
        }

        private static string NormalizeIdentifier(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                StringBuilder stringBuilder = new StringBuilder(((string)value).Length);
                for (int i = 0; i < ((string)value).Length; i++)
                {
                    char c = char.ToLowerInvariant(((string)value)[i]);
                    if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-')
                    {
                        stringBuilder.Append(c);
                    }
                }
                return stringBuilder.ToString();
            }
            return string.Empty;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderOutputProtection GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
