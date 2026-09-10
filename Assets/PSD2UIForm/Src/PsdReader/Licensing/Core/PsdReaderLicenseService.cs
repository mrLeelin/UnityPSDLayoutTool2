#define PSD2UIFORM_FORCE_AUTHORIZED_TEST

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LicenseCacheStoreNamespace;
using LicenseFeatureCatalogNamespace;
using VerifiedLicenseContextNamespace;
using LicenseStatusPresenterNamespace;
using LicenseResultCodeNamespace;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

using Object = UnityEngine.Object;
using LicenseRepositoryClientNamespace;
using RenderProtectionFlagsNamespace;
using RenderProtectionDescriptorNamespace;
using RenderProtectionProfileNamespace;
using cn.efunstudio.psdreader;
using DeviceFingerprintProviderNamespace;
using LicensePayloadBinaryReaderNamespace;
using LicenseClientDefaultsNamespace;
using LicenseAvailabilityStateNamespace;
using LicenseTelemetryClientNamespace;
using EditorLicenseUsageScopeManagerNamespace;
using PsdReaderLicenseStatusNamespace;
using LicenseCryptoUtilityNamespace;
using LicensePathProviderNamespace;

namespace PsdReaderLicenseServiceNamespace
{
    internal sealed class PsdReaderLicenseService
    {
        private enum EnvelopeFetchFailure : byte
        {
            // 保持原程序集的空成员表，仅记录数值语义：
            // 0 未分类；1 下载或网络失败；2 密钥标识不匹配；3 签名或解密失败；
            // 4 载荷反序列化失败；5 信封格式无效。
        }

        private enum ActivationSource : byte
        {
            // 保持原程序集的空成员表，仅记录数值语义：
            // 0 缓存刷新；1 订单号激活；2 项目授权文件激活。
        }

        private sealed class LicenseUsageScope : IDisposable
        {
            private bool _isDisposed;

            internal static LicenseUsageScope s_ObfuscationSentinel;

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }
                _isDisposed = true;
                lock (_syncRoot)
                {
                    if (_usageScopeDepth > 0)
                    {
                        _usageScopeDepth--;
                    }
                    if (_usageScopeDepth == 0)
                    {
                        FlushGenerationUsageTelemetry();
                        ResetUsageScopeState();
                    }
                }
            }

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static LicenseUsageScope GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private static readonly object _syncRoot = new object();

        private static readonly LicenseRepositoryClient _repositoryClient = new LicenseRepositoryClient();

        private static readonly LicenseCacheStore _licenseCacheStore = new LicenseCacheStore();

        private static readonly DeviceFingerprintProvider _deviceFingerprintProvider = new DeviceFingerprintProvider();

        private static readonly LicenseTelemetryClient _telemetryClient = new LicenseTelemetryClient();

        private static readonly HashSet<string> _trackedUsageEvents = new HashSet<string>(StringComparer.Ordinal);

        private static readonly HashSet<string> _trackedValidationEvents = new HashSet<string>(StringComparer.Ordinal);

        private static readonly string _telemetryVersionSuffix = BuildTelemetryVersionSuffix();

        private static PsdReaderLicenseStatus _currentStatus;

        private static PsdReaderLicenseCacheDocument _cachedLicense;

        private static PsdReaderLicenseClientConfigDocument _clientConfig;

        private static bool _isCacheLoaded;

        private static int _cachedFeatureMask = int.MinValue;

        private static int _usageScopeDepth;

        private static int _generationUseCount;

        private static string _cachedBuildLedgerHash = string.Empty;

        private static RenderProtectionProfile _cachedProtectionProfile;

        private static string _lastProjectLicenseFileStamp = string.Empty;

        private static string _lastProtectionRefreshKey = string.Empty;

        private static DateTime _lastProtectionRefreshAttemptUtc = DateTime.MinValue;

        private static string _lastNotifiedProtectionStateHash = string.Empty;

        internal static PsdReaderLicenseService s_ObfuscationSentinel;

        internal static PsdReaderLicenseStatus GetLicenseStatus()
        {
#if PSD2UIFORM_FORCE_AUTHORIZED_TEST
            // HACK: 临时完整模拟正常授权，用于验证授权状态、功能能力和无水印输出。
            DateTime verifiedAtUtc = DateTime.UtcNow;
            PsdReaderLicenseCacheDocument testCacheDocument = new PsdReaderLicenseCacheDocument
            {
                LookupId = "7E57A001",
                LicenseId = "7E57A002",
                StatusCode = 0,
                StatusText = "Active",
                Message = "授权有效。",
                Features = new string[1] { "Main" },
                FeatureMask = 1u,
                CatalogMask = 1u,
                LastVerifiedUtc = verifiedAtUtc.ToString("O"),
                SupportUntilUtc = verifiedAtUtc.AddYears(10).ToString("O"),
                Revision = 1,
                LastResultCode = (LicenseResultCode)1
            };
            lock (_syncRoot)
            {
                _currentStatus = CreateLicenseStatus((LicenseResultCode)1, "授权有效。", testCacheDocument);
                _cachedFeatureMask = 1;
                return CloneLicenseStatus(_currentStatus);
            }
#else
            AutoActivateProjectLicenseIfChanged();
            lock (_syncRoot)
            {
                EnsureCacheLoaded();
                return CloneLicenseStatus(_currentStatus ?? CreateLicenseStatus((LicenseResultCode)4, "尚未激活 Psd2UGUI 授权。"));
            }
#endif
        }

        internal static bool ActivateWithOrder(object orderId, out string message)
        {
            return TryActivateLicense(orderId, (ActivationSource)1, "order", out message);
        }

        private static bool TryActivateLicense(
            object orderId,
            ActivationSource activationSource,
            object activationSourceName,
            out string message)
        {
            string normalizedOrderId = LicenseCryptoUtility.ExtractDigits(orderId);
            if (normalizedOrderId.Length != 19)
            {
                message = "订单号必须是 19 位数字。";
                PsdReaderLicenseClientConfigDocument clientConfig;
                lock (_syncRoot)
                {
                    _currentStatus = CreateLicenseStatus((LicenseResultCode)3, message);
                    _cachedFeatureMask = 0;
                    clientConfig = BuildClientConfig();
                }
                TrackLicenseValidation(activationSource, clientConfig, normalizedOrderId, (LicenseResultCode)3, null);
                return false;
            }

            string lookupId = LicenseCryptoUtility.ComputeOrderLookupId(normalizedOrderId);
            byte[] licenseAccessKey = LicenseCryptoUtility.DeriveLicenseAccessKey(normalizedOrderId, lookupId);
            VerifiedLicenseContext verifiedContext;
            return TryRefreshLicenseContext(
                lookupId,
                licenseAccessKey,
                normalizedOrderId,
                true,
                activationSource,
                activationSourceName,
                out message,
                out verifiedContext);
        }

        internal static bool RefreshLicense(out string message)
        {
            lock (_syncRoot)
            {
                EnsureCacheLoaded();
                if (_cachedLicense == null || string.IsNullOrWhiteSpace(_cachedLicense.LookupId))
                {
                    message = "当前没有可刷新的授权缓存。";
                    _currentStatus = CreateLicenseStatus((LicenseResultCode)4, message);
                    _cachedFeatureMask = 0;
                    return false;
                }
            }

            VerifiedLicenseContext verifiedContext;
            return TryRefreshLicenseContext(
                _cachedLicense.LookupId,
                GetCachedLicenseAccessKey(),
                null,
                true,
                (ActivationSource)0,
                null,
                out message,
                out verifiedContext);
        }

        internal static bool HasProjectLicenseFile()
        {
            PsdReaderProjectLicenseBundleDocument psdReaderProjectLicenseBundleDocument;
            DateTime dateTime;
            string text;
            return TryGetProjectLicenseFile(out psdReaderProjectLicenseBundleDocument, out dateTime, out text);
        }

        internal static string GetProjectLicenseHint()
        {
            if (!TryGetProjectLicenseFile(out var _, out var _, out var _))
            {
                return "可将项目授权文件保存到插件目录：" + LicensePathProvider.GetProjectLicenseAssetPath();
            }
            return "已检测到插件目录授权文件：" + LicensePathProvider.GetProjectLicenseAssetPath() + "\n可直接使用授权文件激活";
        }

        internal static bool CanExportProjectLicense()
        {
            string text;
            return TryValidateProjectLicenseExport(out text);
        }

        internal static int GetDefaultProjectLicenseDays()
        {
            return 90;
        }

        internal static bool TryValidateProjectLicenseExport(out string result)
        {
            PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument;
            long num;
            return TryGetExportableLicenseCache(out psdReaderLicenseCacheDocument, out num, out result);
        }

        internal static bool ActivateFromProjectLicenseFile(out string message)
        {
            if (!TryActivateProjectLicenseFileCore(out var verifiedContext, out message, out var resultCode))
            {
                PsdReaderLicenseClientConfigDocument failedClientConfig;
                lock (_syncRoot)
                {
                    _currentStatus = CreateLicenseStatus((LicenseResultCode)4, message, _cachedLicense);
                    _cachedFeatureMask = 0;
                    failedClientConfig = BuildClientConfig();
                }
                TrackLicenseValidation((ActivationSource)2, failedClientConfig, null, resultCode, null);
                return false;
            }

            lock (_syncRoot)
            {
                _cachedLicense = verifiedContext.GetCacheDocument();
                _clientConfig = BuildClientConfig(verifiedContext.GetProductMetadata(), verifiedContext.GetCacheDocument());
                _licenseCacheStore.Save(_cachedLicense);
                _currentStatus = CreateLicenseStatus((LicenseResultCode)1, "授权已更新。", _cachedLicense);
                _cachedFeatureMask = int.MinValue;
            }
            NotifyProtectionStateChanged();
            TrackLicenseValidation((ActivationSource)2, _clientConfig, null, (LicenseResultCode)1, verifiedContext);
            message = "授权已更新。";
            return true;
        }

        internal static bool SaveProjectLicenseFile(int validityDays, out string message)
        {
            if (!TryBuildProjectLicenseBundle(validityDays, out var projectLicenseBundle, out message))
            {
                return false;
            }
            try
            {
                byte[] serializedBundle = LicenseCryptoUtility.SerializeProjectLicenseBundle(projectLicenseBundle);
                if (serializedBundle != null && serializedBundle.Length != 0)
                {
                    Directory.CreateDirectory(LicensePathProvider.GetPluginAbsoluteDirectory());
                    File.WriteAllBytes(LicensePathProvider.GetProjectLicenseAbsolutePath(), serializedBundle);
                    AssetDatabase.Refresh((ImportAssetOptions)8);
                    message = "项目授权文件已保存到：" + LicensePathProvider.GetProjectLicenseAssetPath();
                    return true;
                }
                message = "生成项目授权文件失败。";
                return false;
            }
            catch (Exception)
            {
                message = "写入项目授权文件失败。";
                return false;
            }
        }

        internal static IDisposable BeginUsageScope()
        {
            lock (_syncRoot)
            {
                if (_usageScopeDepth == 0)
                {
                    ResetUsageScopeState();
                }
                _usageScopeDepth++;
                return new LicenseUsageScope();
            }
        }

        internal static bool HasMainFeature()
        {
            return (GetFeatureMask() & 1) != 0;
        }

        internal static string ClearLicenseData()
        {
            string message = File.Exists(LicensePathProvider.GetProjectLicenseAbsolutePath()) && !DeleteProjectLicenseFile()
                ? "本地授权缓存已清除，但插件目录授权文件删除失败，请手动删除。"
                : "本地授权缓存和插件目录授权文件已清除。";
            lock (_syncRoot)
            {
                _licenseCacheStore.Clear();
                _cachedLicense = null;
                _clientConfig = null;
                _isCacheLoaded = true;
                _lastProjectLicenseFileStamp = string.Empty;
                _currentStatus = CreateLicenseStatus((LicenseResultCode)4, message);
                _cachedFeatureMask = 0;
            }
            NotifyProtectionStateChanged();
            return message;
        }

        internal static void TrackUsageEvent(object text2)
        {
            text2 = AppendTelemetryVersionSuffix(text2);
            if (string.IsNullOrWhiteSpace((string)text2))
            {
                return;
            }
            lock (_syncRoot)
            {
                if (!TryRegisterUsageEvent(text2))
                {
                    return;
                }
                EnsureCacheLoaded();
                PsdReaderLicenseClientConfigDocument psdReaderLicenseClientConfigDocument = BuildClientConfig();
                if (psdReaderLicenseClientConfigDocument != null)
                {
                    string text = ResolveUsageTelemetryId(GetCachedOrderId(), text2);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        _telemetryClient.TrackEvent(psdReaderLicenseClientConfigDocument, text, (string)text2);
                    }
                }
            }
        }

        internal static void MarkGenerationUsed()
        {
            lock (_syncRoot)
            {
                if (_usageScopeDepth > 0)
                {
                    _generationUseCount++;
                }
            }
        }

        private static string ResolveUsageTelemetryId(object value, object value2)
        {
            string text = LicenseCryptoUtility.ExtractDigits(value);
            if (text.Length == 19)
            {
                return text;
            }
            if (!IsTrialUsageEvent(value2))
            {
                return CreateErrorTelemetryId(text);
            }
            return CreateTrialTelemetryId();
        }

        private static bool IsTrialUsageEvent(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                return ((string)value).StartsWith("trial_", StringComparison.Ordinal);
            }
            return false;
        }

        private static string CreateTrialTelemetryId()
        {
            _deviceFingerprintProvider.GetDeviceFingerprintHash(out var text);
            text = LicenseCryptoUtility.NormalizeHex(text);
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }
            string text2 = LicenseCryptoUtility.NormalizeHex(LicenseCryptoUtility.ComputeStringSha256Hex("PsdReader|TrialTelemetry|v1|psd2ugui|" + text));
            if (text2.Length > 16)
            {
                text2 = text2.Substring(0, 16);
            }
            return "trial_" + text2;
        }

        private static string CreateErrorTelemetryId(object value)
        {
            string text = LicenseCryptoUtility.ExtractDigits(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                return "error_order";
            }
            return "error_" + text;
        }

        private static void FlushGenerationUsageTelemetry()
        {
            if (_generationUseCount > 0)
            {
                EnsureCacheLoaded();
                TrackUsageEvent((LicenseStatusPresenter.GetAvailabilityState(_currentStatus ?? CreateLicenseStatus((LicenseResultCode)4, "尚未激活 Psd2UGUI 授权。")) == (LicenseAvailabilityState)0) ? "generate" : "trial_generate");
            }
        }

        private static void ResetUsageScopeState()
        {
            _trackedUsageEvents.Clear();
            _trackedValidationEvents.Clear();
            _generationUseCount = 0;
            _cachedBuildLedgerHash = string.Empty;
            _cachedProtectionProfile = null;
        }

        private static bool TryRegisterUsageEvent(object value)
        {
            if (_usageScopeDepth <= 0)
            {
                return true;
            }
            return _trackedUsageEvents.Add((string)(value ?? string.Empty));
        }

        internal static RenderProtectionProfile GetOutputProtectionContext()
        {
            lock (_syncRoot)
            {
#if PSD2UIFORM_FORCE_AUTHORIZED_TEST
                if (_usageScopeDepth > 0 && _cachedProtectionProfile != null)
                {
                    return _cachedProtectionProfile;
                }
                DateTime generatedAtUtc = DateTime.UtcNow;
                RenderProtectionProfile testProtectionProfile = new RenderProtectionProfile();
                testProtectionProfile.SetProfileName("Main");
                testProtectionProfile.SetIsAuthorized(true);
                testProtectionProfile.SetProtectionKeyHex(LicenseCryptoUtility.ComputeStringSha256Hex("psd2ugui|AuthorizedTest|protection-key"));
                testProtectionProfile.SetLookupId("7E57A001");
                testProtectionProfile.SetLicenseId("7E57A002");
                testProtectionProfile.SetRevision(1);
                testProtectionProfile.SetValidationTranscriptHead(LicenseCryptoUtility.ComputeStringSha256Hex("psd2ugui|AuthorizedTest|transcript"));
                testProtectionProfile.SetProjectIdentity(LicenseCryptoUtility.ComputeStringSha256Hex("psd2ugui|AuthorizedTest|project"));
                testProtectionProfile.SetDeviceIdentity(LicenseCryptoUtility.ComputeStringSha256Hex("psd2ugui|AuthorizedTest|device"));
                testProtectionProfile.SetClockHighWaterUtc(generatedAtUtc);
                testProtectionProfile.SetGeneratedAtUtcTicks(generatedAtUtc.Ticks);
                testProtectionProfile.SetStatusCode(0);
                testProtectionProfile.SetSchemaVersion(1);
                testProtectionProfile.SetStatusMessage(string.Empty);
                testProtectionProfile.SetLedgerHash(ComputeProtectionLedgerHash(testProtectionProfile));
                testProtectionProfile.SetProtectionStateHash(ComputeProtectionStateHash(testProtectionProfile));
                if (_usageScopeDepth > 0)
                {
                    _cachedProtectionProfile = testProtectionProfile;
                }
                return testProtectionProfile;
#else
                if (_usageScopeDepth > 0 && _cachedProtectionProfile != null)
                {
                    return _cachedProtectionProfile;
                }
                EnsureCacheLoaded();
                VerifiedLicenseContext value = null;
                string text;
                if (ShouldUseProjectLicenseFile())
                {
                    TryLoadProjectLicenseContext(out value, out text);
                }
                if (value == null && HasCachedLicenseLookup())
                {
                    TryRefreshLicenseContext(_cachedLicense.LookupId, GetCachedLicenseAccessKey(), null, false, (ActivationSource)0, null, out text, out value);
                    if (value == null)
                    {
                        value = TryRestoreVerifiedContextFromCache(_cachedLicense);
                    }
                }
                RenderProtectionProfile value2 = ((value != null && TryValidateContextForFeature("Main", value, out value, out text)) ? CreateAuthorizedProtectionProfile(value) : CreateUnauthorizedProtectionProfile());
                if (_usageScopeDepth > 0)
                {
                    _cachedProtectionProfile = value2;
                }
                return value2;
#endif
            }
        }

        internal static RenderProtectionProfile GetRefreshedOutputProtectionContext()
        {
#if PSD2UIFORM_FORCE_AUTHORIZED_TEST
            return GetOutputProtectionContext();
#else
            if (TryCreateAuthorizedProtectionProfileFromCache(out var result))
            {
                return result;
            }
            RefreshProtectionProfileIfNeeded();
            if (TryCreateAuthorizedProtectionProfileFromCache(out result))
            {
                return result;
            }
            lock (_syncRoot)
            {
                EnsureCacheLoaded();
                return CreateUnauthorizedProtectionProfile();
            }
#endif
        }

        private static bool TryCreateAuthorizedProtectionProfileFromCache(out RenderProtectionProfile result)
        {
            result = null;
            lock (_syncRoot)
            {
                EnsureCacheLoaded();
                if (IsLicenseCacheValid(_cachedLicense))
                {
                    VerifiedLicenseContext value = TryRestoreVerifiedContextFromCache(_cachedLicense);
                    if (value == null)
                    {
                        return false;
                    }
                    result = CreateAuthorizedProtectionProfile(value);
                    return true;
                }
                return false;
            }
        }

        private static void RefreshProtectionProfileIfNeeded()
        {
            string text = string.Empty;
            byte[] array = null;
            bool flag = false;
            lock (_syncRoot)
            {
                EnsureCacheLoaded();
                if (IsLicenseCacheValid(_cachedLicense))
                {
                    return;
                }
                if (HasCachedLicenseLookup())
                {
                    text = _cachedLicense.LookupId;
                    array = GetCachedLicenseAccessKey();
                    if (array == null || array.Length == 0)
                    {
                        text = string.Empty;
                        array = null;
                    }
                }
                if (string.IsNullOrWhiteSpace(text))
                {
                    flag = HasProjectLicenseFile();
                }
                string text2 = (flag ? "project_file" : LicenseCryptoUtility.NormalizeHex(text));
                if (string.IsNullOrWhiteSpace(text2) || !ShouldAttemptProtectionRefresh(text2))
                {
                    return;
                }
                _lastProtectionRefreshKey = text2;
                _lastProtectionRefreshAttemptUtc = DateTime.UtcNow;
            }
            string text3;
            VerifiedLicenseContext value;
            if (!flag)
            {
                TryRefreshLicenseContext(text, array, null, false, (ActivationSource)0, null, out text3, out value);
            }
            else
            {
                TryLoadProjectLicenseContext(out value, out text3);
            }
        }

        private static bool ShouldAttemptProtectionRefresh(object value)
        {
            DateTime utcNow = DateTime.UtcNow;
            if (string.Equals(_lastProtectionRefreshKey, (string)value, StringComparison.Ordinal) && !(_lastProtectionRefreshAttemptUtc <= DateTime.MinValue))
            {
                return utcNow >= _lastProtectionRefreshAttemptUtc.AddMinutes(10.0);
            }
            return true;
        }

        internal static int GetFeatureMask()
        {
#if PSD2UIFORM_FORCE_AUTHORIZED_TEST
            _cachedFeatureMask = 1;
#endif
            if (_cachedFeatureMask != int.MinValue)
            {
                return _cachedFeatureMask;
            }
            lock (_syncRoot)
            {
                if (_cachedFeatureMask != int.MinValue)
                {
                    return _cachedFeatureMask;
                }
                try
                {
                    _cachedFeatureMask = (GetOutputProtectionContext().GetIsAuthorized() ? 1 : 0);
                }
                catch
                {
                    _cachedFeatureMask = 0;
                }
                return _cachedFeatureMask;
            }
        }

        internal static RenderProtectionDescriptor CreateEnabledProtectionDescriptor(object value, object value2, object value3, int value4, int value5, int value6, int value7, int value8)
        {
            return CreateProtectionDescriptor(value, value2, value3, value4, value5, value6, value7, value8, true);
        }

        internal static RenderProtectionDescriptor CreateDisabledProtectionDescriptor(object value, object value2, object value3, int value4, int value5, int value6, int value7, int value8)
        {
            return CreateProtectionDescriptor(value, value2, value3, value4, value5, value6, value7, value8, false);
        }

        internal static bool MatchesEnabledProtectionDescriptor(object value, object renderProtectionDescriptor, int value2, int value3, int value4, int value5, int value6)
        {
            if (renderProtectionDescriptor != null)
            {
                return AreProtectionDescriptorsEquivalent(CreateProtectionDescriptor(value, ((RenderProtectionDescriptor)renderProtectionDescriptor).GetLayerPathKey(), ((RenderProtectionDescriptor)renderProtectionDescriptor).GetNormalizedBounds(), value2, value3, value4, value5, value6, true), renderProtectionDescriptor);
            }
            return false;
        }

        private static RenderProtectionDescriptor CreateProtectionDescriptor(object value, object value2, object value3, int value4, int value5, int value6, int value7, int value8, bool enabled)
        {
            if (value == null)
            {
                value = new RenderProtectionProfile();
            }
            value2 = NormalizeProtectionPath(value2);
            value3 = NormalizeProtectionPath(value3);
            string text = LicenseCryptoUtility.ComputeStringSha256Hex(value2);
            string text2 = LicenseCryptoUtility.ComputeStringSha256Hex(value3);
            string text3 = string.Format("{0}|RenderProfile|stable|{1}|", "psd2ugui", 1) + $"{LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetLookupId())}|{LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetLicenseId())}|{((RenderProtectionProfile)value).GetRevision()}|" + LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetProjectIdentity()) + "|" + LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetDeviceIdentity()) + "|" + $"{text}|{text2}|{value5}|{value6}|{value7}|{value8}|{value4}";
            string text4 = string.Format("{0}|RenderProfile|session|{1}|", "psd2ugui", 1) + $"{LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetLedgerHash())}|{((RenderProtectionProfile)value).GetGeneratedAtUtcTicks()}|" + $"{LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetProtectionKeyHex())}|{text}|{text2}|{value4}";
            byte[] array = LicenseCryptoUtility.ComputeStringSha256(text3);
            byte[] array2 = LicenseCryptoUtility.ComputeStringHmacSha256(DeriveRenderProfileKey(value), text4);
            byte b = (byte)(104 + ReadByteOrDefault(array, 4, 0) % 72);
            byte b2 = (byte)(56 + ReadByteOrDefault(array, 5, 0) % 64);
            byte b3 = (byte)(24 + ReadByteOrDefault(array, 8, 0) % 72);
            byte b4 = (byte)(32 + ReadByteOrDefault(array2, 5, 0) % 80);
            bool flag = enabled && HasTrustedWatermarkExemption(value);
            bool flag2 = enabled && HasTrustedVisualNoiseExemption(value);
            RenderProtectionDescriptor value9 = new RenderProtectionDescriptor();
            value9.Version = 1;
            value9.SetStableSeed(ReadInt32OrDefault(array, 0, value5 ^ value6 ^ value4));
            value9.SetSessionSeed(ReadInt32OrDefault(array2, 0, value7 ^ value8 ^ value4));
            value9.SetPrimaryWatermarkStrength((byte)((!flag) ? b : 0));
            value9.SetBrandWatermarkStrength((byte)((!flag) ? b2 : 0));
            value9.SetPrimaryLayoutVariant((byte)(5 + ReadByteOrDefault(array, 6, 0) % 7));
            value9.SetSecondaryLayoutVariant((byte)(5 + ReadByteOrDefault(array, 7, 0) % 7));
            value9.SetAlphaNoiseStrength(0);
            value9.SetContrastStrength((byte)((!flag2) ? b3 : 0));
            value9.SetEdgeStrength((byte)((!flag2) ? b4 : 0));
            value9.SetPositionJitterSeed((byte)(96 + ReadByteOrDefault(array, 9, 0) % 64));
            value9.SetProtectionFlags(GetDefaultProtectionFlags(array, array2));
            value9.SetLayerPathKey((string)value2);
            value9.SetNormalizedBounds((string)value3);
            RenderProtectionDescriptor value10 = value9;
            value10.SetDescriptorMacHex(ComputeProtectionDescriptorMac(value, value10, text, text2, value4));
            value10.SetProtectionFingerprint(ComputeProtectionFingerprint(value, value10, text, text2, value4));
            return value10;
        }

        private static string ComputeProtectionDescriptorMac(object value, object value2, object value3, object value4, int value5)
        {
            string text = string.Format("{0}|RenderProfile|mac|{1}|", "psd2ugui", ((RenderProtectionDescriptor)value2)?.Version ?? 1) + $"{LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value)?.GetLookupId())}|{LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value)?.GetLicenseId())}|{((RenderProtectionProfile)value)?.GetRevision() ?? 0}|" + LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value)?.GetProjectIdentity()) + "|" + LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value)?.GetDeviceIdentity()) + "|" + $"{LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value)?.GetLedgerHash())}|{((RenderProtectionProfile)value)?.GetGeneratedAtUtcTicks() ?? 0L}|" + $"{value3}|{value4}|{value5}|" + $"{((RenderProtectionDescriptor)value2)?.GetStableSeed() ?? 0}|{((RenderProtectionDescriptor)value2)?.GetSessionSeed() ?? 0}|{((RenderProtectionDescriptor)value2)?.GetPrimaryWatermarkStrength() ?? 0}|{((RenderProtectionDescriptor)value2)?.GetBrandWatermarkStrength() ?? 0}|" + $"{((RenderProtectionDescriptor)value2)?.GetPrimaryLayoutVariant() ?? 0}|{((RenderProtectionDescriptor)value2)?.GetSecondaryLayoutVariant() ?? 0}|{((RenderProtectionDescriptor)value2)?.GetAlphaNoiseStrength() ?? 0}|{((RenderProtectionDescriptor)value2)?.GetContrastStrength() ?? 0}|{((RenderProtectionDescriptor)value2)?.GetEdgeStrength() ?? 0}|{((RenderProtectionDescriptor)value2)?.GetPositionJitterSeed() ?? 0}|{(ushort)(((RenderProtectionDescriptor)value2)?.GetProtectionFlags() ?? ((RenderProtectionFlags)0))}";
            return LicenseCryptoUtility.BytesToHex(LicenseCryptoUtility.ComputeStringHmacSha256(DeriveRenderProfileKey(value), text));
        }

        private static string ComputeProtectionFingerprint(object value, object value2, object value3, object value4, int value5)
        {
            return LicenseCryptoUtility.ComputeStringSha256Hex(string.Format("{0}|PreviewProtection|{1}|", "psd2ugui", ((RenderProtectionDescriptor)value2)?.Version ?? 1) + $"{LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value)?.GetProtectionStateHash())}|{value3}|{value4}|{value5}|" + $"{((RenderProtectionDescriptor)value2)?.GetStableSeed() ?? 0}|{((RenderProtectionDescriptor)value2)?.GetPrimaryWatermarkStrength() ?? 0}|{((RenderProtectionDescriptor)value2)?.GetBrandWatermarkStrength() ?? 0}|{((RenderProtectionDescriptor)value2)?.GetPrimaryLayoutVariant() ?? 0}|{((RenderProtectionDescriptor)value2)?.GetSecondaryLayoutVariant() ?? 0}|{((RenderProtectionDescriptor)value2)?.GetContrastStrength() ?? 0}|{((RenderProtectionDescriptor)value2)?.GetPositionJitterSeed() ?? 0}|{(ushort)(((RenderProtectionDescriptor)value2)?.GetProtectionFlags() ?? ((RenderProtectionFlags)0))}");
        }

        private static bool HasTrustedWatermarkExemption(object value)
        {
            if (value != null && ((RenderProtectionProfile)value).GetIsAuthorized() && ((RenderProtectionProfile)value).GetStatusCode() == 0 && ((RenderProtectionProfile)value).GetSchemaVersion() == 1 && !string.IsNullOrWhiteSpace(((RenderProtectionProfile)value).GetLookupId()) && !string.IsNullOrWhiteSpace(((RenderProtectionProfile)value).GetLicenseId()) && !string.IsNullOrWhiteSpace(((RenderProtectionProfile)value).GetValidationTranscriptHead()))
            {
                return HasValidProtectionStateHash(value);
            }
            return false;
        }

        private static bool HasValidAuthorizedProtectionProfile(object value)
        {
            byte[] array = LicenseCryptoUtility.HexToBytes(((RenderProtectionProfile)value)?.GetProtectionKeyHex());
            if (value != null && ((RenderProtectionProfile)value).GetIsAuthorized() && ((RenderProtectionProfile)value).GetStatusCode() == 0 && ((RenderProtectionProfile)value).GetGeneratedAtUtcTicks() > 0L && array != null && array.Length >= 16 && !string.IsNullOrWhiteSpace(((RenderProtectionProfile)value).GetLedgerHash()) && !string.IsNullOrWhiteSpace(((RenderProtectionProfile)value).GetProtectionKeyHex()))
            {
                return HasValidProtectionStateHash(value);
            }
            return false;
        }

        private static bool HasTrustedVisualNoiseExemption(object value)
        {
            if (value != null && ((RenderProtectionProfile)value).GetIsAuthorized() && ((RenderProtectionProfile)value).GetStatusCode() == 0 && ((RenderProtectionProfile)value).GetSchemaVersion() == 1 && string.IsNullOrWhiteSpace(((RenderProtectionProfile)value).GetStatusMessage()) && !string.IsNullOrWhiteSpace(((RenderProtectionProfile)value).GetProjectIdentity()) && !string.IsNullOrWhiteSpace(((RenderProtectionProfile)value).GetDeviceIdentity()))
            {
                return HasValidProtectionStateHash(value);
            }
            return false;
        }

        private static bool HasValidProtectionStateHash(object value)
        {
            if (value == null)
            {
                return false;
            }
            return string.Equals(LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetProtectionStateHash()), LicenseCryptoUtility.NormalizeHex(ComputeProtectionStateHash(value)), StringComparison.Ordinal);
        }

        private static bool AreProtectionDescriptorsEquivalent(object value, object value2)
        {
            if (value != null && value2 != null)
            {
                if (((RenderProtectionDescriptor)value).Version == ((RenderProtectionDescriptor)value2).Version && ((RenderProtectionDescriptor)value).GetStableSeed() == ((RenderProtectionDescriptor)value2).GetStableSeed() && ((RenderProtectionDescriptor)value).GetSessionSeed() == ((RenderProtectionDescriptor)value2).GetSessionSeed() && ((RenderProtectionDescriptor)value).GetPrimaryWatermarkStrength() == ((RenderProtectionDescriptor)value2).GetPrimaryWatermarkStrength() && ((RenderProtectionDescriptor)value).GetBrandWatermarkStrength() == ((RenderProtectionDescriptor)value2).GetBrandWatermarkStrength() && ((RenderProtectionDescriptor)value).GetPrimaryLayoutVariant() == ((RenderProtectionDescriptor)value2).GetPrimaryLayoutVariant() && ((RenderProtectionDescriptor)value).GetSecondaryLayoutVariant() == ((RenderProtectionDescriptor)value2).GetSecondaryLayoutVariant() && ((RenderProtectionDescriptor)value).GetAlphaNoiseStrength() == ((RenderProtectionDescriptor)value2).GetAlphaNoiseStrength() && ((RenderProtectionDescriptor)value).GetContrastStrength() == ((RenderProtectionDescriptor)value2).GetContrastStrength() && ((RenderProtectionDescriptor)value).GetEdgeStrength() == ((RenderProtectionDescriptor)value2).GetEdgeStrength() && ((RenderProtectionDescriptor)value).GetPositionJitterSeed() == ((RenderProtectionDescriptor)value2).GetPositionJitterSeed() && ((RenderProtectionDescriptor)value).GetProtectionFlags() == ((RenderProtectionDescriptor)value2).GetProtectionFlags() && string.Equals(((RenderProtectionDescriptor)value).GetLayerPathKey(), ((RenderProtectionDescriptor)value2).GetLayerPathKey(), StringComparison.Ordinal) && string.Equals(((RenderProtectionDescriptor)value).GetNormalizedBounds(), ((RenderProtectionDescriptor)value2).GetNormalizedBounds(), StringComparison.Ordinal) && string.Equals(LicenseCryptoUtility.NormalizeHex(((RenderProtectionDescriptor)value).GetDescriptorMacHex()), LicenseCryptoUtility.NormalizeHex(((RenderProtectionDescriptor)value2).GetDescriptorMacHex()), StringComparison.Ordinal))
                {
                    return string.Equals(LicenseCryptoUtility.NormalizeHex(((RenderProtectionDescriptor)value).GetProtectionFingerprint()), LicenseCryptoUtility.NormalizeHex(((RenderProtectionDescriptor)value2).GetProtectionFingerprint()), StringComparison.Ordinal);
                }
                return false;
            }
            return false;
        }

        private static RenderProtectionProfile CreateAuthorizedProtectionProfile(object value)
        {
            DateTime dateTime = LicenseCryptoUtility.ParseUtcOrDefault(((VerifiedLicenseContext)value)?.GetCacheDocument()?.ClockHighWaterUtc, LicenseCryptoUtility.ParseUtcOrDefault(((VerifiedLicenseContext)value)?.GetCacheDocument()?.LastVerifiedUtc, DateTime.UtcNow));
            string text = LicenseCryptoUtility.ComputeProjectScopeHash(LicensePathProvider.GetProjectRoot());
            string text2 = LicenseCryptoUtility.ComputeAssemblyFingerprint();
            RenderProtectionProfile value2 = new RenderProtectionProfile();
            value2.SetProfileName("Main");
            value2.SetIsAuthorized(true);
            object obj;
            if (value != null)
            {
                obj = ((VerifiedLicenseContext)value).GetProtectionKeyHex();
                if (obj != null)
                {
                    goto IL_0081;
                }
            }
            else
            {
                obj = null;
            }
            obj = string.Empty;
            goto IL_0081;
            IL_0081:
            value2.SetProtectionKeyHex((string)obj);
            object obj2;
            if (value == null)
            {
                obj2 = null;
            }
            else
            {
                PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument = ((VerifiedLicenseContext)value).GetCacheDocument();
                if (psdReaderLicenseCacheDocument == null)
                {
                    obj2 = null;
                }
                else
                {
                    obj2 = psdReaderLicenseCacheDocument.LookupId;
                    if (obj2 != null)
                    {
                        goto IL_00a8;
                    }
                }
            }
            obj2 = string.Empty;
            goto IL_00a8;
            IL_0134:
            object obj3;
            value2.SetValidationTranscriptHead((string)obj3);
            value2.SetProjectIdentity(text);
            value2.SetDeviceIdentity(text2);
            value2.SetClockHighWaterUtc(dateTime);
            value2.SetGeneratedAtUtcTicks(DateTime.UtcNow.Ticks);
            value2.SetStatusCode(0);
            value2.SetSchemaVersion(1);
            value2.SetStatusMessage(string.Empty);
            value2.SetLedgerHash(ComputeProtectionLedgerHash(value2));
            value2.SetProtectionStateHash(ComputeProtectionStateHash(value2));
            return value2;
            IL_00cf:
            object obj4;
            value2.SetLicenseId((string)obj4);
            value2.SetRevision((((VerifiedLicenseContext)value)?.GetCacheDocument()?.Revision).GetValueOrDefault());
            if (value == null)
            {
                obj3 = null;
            }
            else
            {
                PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument2 = ((VerifiedLicenseContext)value).GetCacheDocument();
                if (psdReaderLicenseCacheDocument2 == null)
                {
                    obj3 = null;
                }
                else
                {
                    obj3 = psdReaderLicenseCacheDocument2.ValidationTranscriptHead;
                    if (obj3 != null)
                    {
                        goto IL_0134;
                    }
                }
            }
            obj3 = string.Empty;
            goto IL_0134;
            IL_00a8:
            value2.SetLookupId((string)obj2);
            if (value == null)
            {
                obj4 = null;
            }
            else
            {
                PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument3 = ((VerifiedLicenseContext)value).GetCacheDocument();
                if (psdReaderLicenseCacheDocument3 == null)
                {
                    obj4 = null;
                }
                else
                {
                    obj4 = psdReaderLicenseCacheDocument3.LicenseId;
                    if (obj4 != null)
                    {
                        goto IL_00cf;
                    }
                }
            }
            obj4 = string.Empty;
            goto IL_00cf;
        }

        private static RenderProtectionProfile CreateUnauthorizedProtectionProfile()
        {
            EnsureCacheLoaded();
            PsdReaderLicenseStatus licenseStatus = _currentStatus ?? CreateLicenseStatus((LicenseResultCode)4, "尚未激活 Psd2UGUI 授权。");
            string projectIdentity = LicenseCryptoUtility.ComputeProjectScopeHash(LicensePathProvider.GetProjectRoot());
            string deviceIdentity = LicenseCryptoUtility.ComputeAssemblyFingerprint();
            long generatedAtUtcTicks = DateTime.UtcNow.Ticks;
            PsdReaderLicenseCacheDocument cachedLicenseForLookupId = _cachedLicense;
            object lookupIdCandidate;
            if (cachedLicenseForLookupId != null)
            {
                lookupIdCandidate = cachedLicenseForLookupId.LookupId;
                if (lookupIdCandidate != null)
                {
                    goto ResolveLicenseId;
                }
            }
            else
            {
                lookupIdCandidate = null;
            }
            lookupIdCandidate = licenseStatus.GetLookupId() ?? string.Empty;
            goto ResolveLicenseId;
            BuildProtectionProfile:
            object licenseIdCandidate;
            string licenseId = (string)licenseIdCandidate;
            int revision = _cachedLicense?.Revision ?? licenseStatus.GetRevision();
            string lookupId;
            string protectionKeyHex = LicenseCryptoUtility.ComputeStringSha256Hex(string.Format("{0}|DeadLease|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}", "psd2ugui", LicenseCryptoUtility.GetCryptoVersion(), licenseStatus.GetResultCode(), lookupId, licenseId, generatedAtUtcTicks, revision, LicenseCryptoUtility.NormalizeHex(projectIdentity), LicenseCryptoUtility.NormalizeHex(deviceIdentity)));
            string validationTranscriptHead = (string.IsNullOrWhiteSpace(_cachedLicense?.ValidationTranscriptHead) ? LicenseCryptoUtility.ComputeStringSha256Hex($"{protectionKeyHex}|dead|transcript|{licenseStatus.GetResultCode()}|{generatedAtUtcTicks}") : _cachedLicense.ValidationTranscriptHead);
            DateTime clockHighWaterUtc = LicenseCryptoUtility.ParseUtcOrDefault(_cachedLicense?.ClockHighWaterUtc, LicenseCryptoUtility.ParseUtcOrDefault(_cachedLicense?.LastVerifiedUtc, DateTime.UtcNow));
            byte[] protectionKeyBytes = LicenseCryptoUtility.ComputeStringSha256(protectionKeyHex);
            int unauthorizedStatusCode = (int)((protectionKeyBytes != null && protectionKeyBytes.Length >= 4) ? (32 + BitConverter.ToUInt32(protectionKeyBytes, 0) % 96) : 64);
            RenderProtectionProfile protectionProfile = new RenderProtectionProfile();
            protectionProfile.SetProfileName("Main");
            protectionProfile.SetIsAuthorized(false);
            protectionProfile.SetProtectionKeyHex(protectionKeyHex);
            protectionProfile.SetLookupId(lookupId);
            protectionProfile.SetLicenseId(licenseId);
            protectionProfile.SetRevision(revision);
            protectionProfile.SetValidationTranscriptHead(validationTranscriptHead);
            protectionProfile.SetProjectIdentity(projectIdentity);
            protectionProfile.SetDeviceIdentity(deviceIdentity);
            protectionProfile.SetClockHighWaterUtc(clockHighWaterUtc);
            protectionProfile.SetGeneratedAtUtcTicks(generatedAtUtcTicks);
            protectionProfile.SetStatusCode(unauthorizedStatusCode);
            protectionProfile.SetSchemaVersion(1);
            protectionProfile.SetStatusMessage(LicenseStatusPresenter.GetStatusMessage(licenseStatus));
            protectionProfile.SetLedgerHash(ComputeProtectionLedgerHash(protectionProfile));
            protectionProfile.SetProtectionStateHash(ComputeProtectionStateHash(protectionProfile));
            return protectionProfile;
            ResolveLicenseId:
            lookupId = (string)lookupIdCandidate;
            PsdReaderLicenseCacheDocument cachedLicenseForLicenseId = _cachedLicense;
            if (cachedLicenseForLicenseId == null)
            {
                licenseIdCandidate = null;
            }
            else
            {
                licenseIdCandidate = cachedLicenseForLicenseId.LicenseId;
                if (licenseIdCandidate != null)
                {
                    goto BuildProtectionProfile;
                }
            }
            licenseIdCandidate = licenseStatus.GetLicenseId() ?? string.Empty;
            goto BuildProtectionProfile;
        }

        private static string ComputeProtectionLedgerHash(object value)
        {
            if (value != null)
            {
                if (_usageScopeDepth > 0)
                {
                    if (string.IsNullOrWhiteSpace(_cachedBuildLedgerHash))
                    {
                        _cachedBuildLedgerHash = LicenseCryptoUtility.ComputeStringSha256Hex(string.Format("{0}|BuildLedger|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}", "psd2ugui", LicenseCryptoUtility.GetCryptoVersion(), LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetValidationTranscriptHead()), LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetLookupId()), LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetLicenseId()), LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetProjectIdentity()), LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetDeviceIdentity()), ((RenderProtectionProfile)value).GetGeneratedAtUtcTicks(), ((RenderProtectionProfile)value).GetRevision(), _trackedUsageEvents.Count, _trackedValidationEvents.Count, _generationUseCount, ((RenderProtectionProfile)value).GetIsAuthorized() ? 1 : 0));
                    }
                    return _cachedBuildLedgerHash ?? string.Empty;
                }
                return LicenseCryptoUtility.ComputeStringSha256Hex(string.Format("{0}|LeaseLedger|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}", "psd2ugui", LicenseCryptoUtility.GetCryptoVersion(), LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetValidationTranscriptHead()), LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetLookupId()), LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetLicenseId()), LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetProjectIdentity()), LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetDeviceIdentity()), ((RenderProtectionProfile)value).GetGeneratedAtUtcTicks(), ((RenderProtectionProfile)value).GetRevision(), ((RenderProtectionProfile)value).GetIsAuthorized() ? 1 : 0));
            }
            return string.Empty;
        }

        private static string ComputeProtectionStateHash(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            return LicenseCryptoUtility.ComputeStringSha256Hex(string.Format("{0}|ProtectionState|{1}|{2}|", "psd2ugui", 1, ((RenderProtectionProfile)value).GetIsAuthorized() ? 1 : 0) + $"{LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetLookupId())}|{LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetLicenseId())}|{((RenderProtectionProfile)value).GetRevision()}|" + LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetValidationTranscriptHead()) + "|" + LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetProjectIdentity()) + "|" + LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value).GetDeviceIdentity()));
        }

        private static string ComputeCurrentProtectionStateHash()
        {
            PsdReaderLicenseStatus licenseStatus = _currentStatus ?? CreateLicenseStatus((LicenseResultCode)4, "尚未激活 Psd2UGUI 授权。");
            bool isAuthorized = LicenseStatusPresenter.GetAvailabilityState(licenseStatus) == (LicenseAvailabilityState)0;
            PsdReaderLicenseCacheDocument cachedLicenseForLookupId = _cachedLicense;
            object lookupIdCandidate;
            if (cachedLicenseForLookupId == null)
            {
                lookupIdCandidate = null;
            }
            else
            {
                lookupIdCandidate = cachedLicenseForLookupId.LookupId;
                if (lookupIdCandidate != null)
                {
                    goto ResolveLicenseId;
                }
            }
            lookupIdCandidate = licenseStatus.GetLookupId() ?? string.Empty;
            goto ResolveLicenseId;
            ReturnStateHash:
            object validationTranscriptCandidate;
            string validationTranscriptHead = (string)validationTranscriptCandidate;
            string projectIdentity = LicenseCryptoUtility.ComputeProjectScopeHash(LicensePathProvider.GetProjectRoot());
            string deviceIdentity = LicenseCryptoUtility.ComputeAssemblyFingerprint();
            string lookupId;
            string licenseId;
            int revision;
            return LicenseCryptoUtility.ComputeStringSha256Hex(string.Format("{0}|ProtectionState|{1}|{2}|", "psd2ugui", 1, isAuthorized ? 1 : 0) + $"{LicenseCryptoUtility.NormalizeHex(lookupId)}|{LicenseCryptoUtility.NormalizeHex(licenseId)}|{revision}|" + LicenseCryptoUtility.NormalizeHex(validationTranscriptHead) + "|" + LicenseCryptoUtility.NormalizeHex(projectIdentity) + "|" + LicenseCryptoUtility.NormalizeHex(deviceIdentity));
            ResolveLicenseId:
            lookupId = (string)lookupIdCandidate;
            PsdReaderLicenseCacheDocument cachedLicenseForLicenseId = _cachedLicense;
            object licenseIdCandidate;
            if (cachedLicenseForLicenseId == null)
            {
                licenseIdCandidate = null;
            }
            else
            {
                licenseIdCandidate = cachedLicenseForLicenseId.LicenseId;
                if (licenseIdCandidate != null)
                {
                    goto ResolveValidationTranscript;
                }
            }
            licenseIdCandidate = licenseStatus.GetLicenseId() ?? string.Empty;
            goto ResolveValidationTranscript;
            ResolveValidationTranscript:
            licenseId = (string)licenseIdCandidate;
            revision = _cachedLicense?.Revision ?? licenseStatus.GetRevision();
            PsdReaderLicenseCacheDocument cachedLicenseForTranscript = _cachedLicense;
            if (cachedLicenseForTranscript == null)
            {
                validationTranscriptCandidate = null;
            }
            else
            {
                validationTranscriptCandidate = cachedLicenseForTranscript.ValidationTranscriptHead;
                if (validationTranscriptCandidate != null)
                {
                    goto ReturnStateHash;
                }
            }
            validationTranscriptCandidate = string.Empty;
            goto ReturnStateHash;
        }

        private static void NotifyProtectionStateChanged()
        {
            string text;
            lock (_syncRoot)
            {
                text = ComputeCurrentProtectionStateHash();
            }
            if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, _lastNotifiedProtectionStateHash, StringComparison.Ordinal))
            {
                _lastNotifiedProtectionStateHash = text;
                EditorLicenseUsageScopeManager.ResetUsageScope();
                ClearLayerPreviewCaches();
                ClearSharedSpriteAssetCache();
            }
        }

        private static void ClearLayerPreviewCaches()
        {
            try
            {
                Type type = ResolveType("UGF.EditorTools.Psd2UGUI.PsdLayerPreviewCache");
                object obj;
                if ((object)type != null)
                {
                    obj = type.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    ((MethodBase)obj)?.Invoke((object)null, (object[])null);
                }
                else
                {
                    obj = null;
                }
                Type type2 = ResolveType("UGF.EditorTools.Psd2UGUI.PsdLayerNode");
                if (type2 == null)
                {
                    return;
                }
                MethodInfo methodInfo = type2.GetProperty("PreviewTexture", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetSetMethod(nonPublic: true);
                FieldInfo field = type2.GetField("<PreviewTexture>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                Object[] array = Resources.FindObjectsOfTypeAll(type2);
                if (array == null || array.Length == 0)
                {
                    return;
                }
                foreach (Object val in array)
                {
                    if (val == (Object)null)
                    {
                        continue;
                    }
                    try
                    {
                        if (!(methodInfo != null))
                        {
                            field?.SetValue(val, null);
                        }
                        else
                        {
                            methodInfo.Invoke(val, new object[1]);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
                InternalEditorUtility.RepaintAllViews();
            }
            catch (Exception)
            {
            }
        }

        private static void ClearSharedSpriteAssetCache()
        {
            try
            {
                Type type = ResolveType("UGF.EditorTools.Psd2UGUI.Psd2UIFormConverter");
                if (!(type == null))
                {
                    object obj = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null, null);
                    if (obj != null && type.GetField("sharedSpriteAssets", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(obj) is IDictionary dictionary)
                    {
                        dictionary.Clear();
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static Type ResolveType(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return null;
            }
            Type type = Type.GetType((string)value, throwOnError: false);
            if (type != null)
            {
                return type;
            }
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType((string)value, throwOnError: false);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }

        private static byte[] DeriveRenderProfileKey(object value)
        {
            byte[] array = LicenseCryptoUtility.HexToBytes(((RenderProtectionProfile)value)?.GetProtectionKeyHex());
            if (array == null || array.Length == 0)
            {
                array = LicenseCryptoUtility.ComputeStringSha256("psd2ugui|RenderProfileKey|fallback|" + $"{LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value)?.GetLookupId())}|{LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value)?.GetLicenseId())}|{((RenderProtectionProfile)value)?.GetRevision() ?? 0}|" + LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value)?.GetProjectIdentity()) + "|" + LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value)?.GetDeviceIdentity()));
            }
            return LicenseCryptoUtility.ComputeStringHmacSha256(array, "psd2ugui|RenderProfileKey|" + LicenseCryptoUtility.GetCryptoVersion() + "|" + $"{LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value)?.GetLookupId())}|{LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value)?.GetLicenseId())}|{((RenderProtectionProfile)value)?.GetRevision() ?? 0}|" + LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value)?.GetProjectIdentity()) + "|" + LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value)?.GetDeviceIdentity()) + "|" + LicenseCryptoUtility.NormalizeHex(((RenderProtectionProfile)value)?.GetLedgerHash()));
        }

        private static RenderProtectionFlags GetDefaultProtectionFlags(object value, object value2)
        {
            return (RenderProtectionFlags)131;
        }

        private static int ReadInt32OrDefault(object value, int value2, int value3)
        {
            if (value != null && ((Array)value).Length >= value2 + 4)
            {
                return BitConverter.ToInt32((byte[])value, value2);
            }
            return value3;
        }

        private static byte ReadByteOrDefault(object value, int value2, byte value3)
        {
            if (value == null || value2 < 0 || value2 >= ((Array)value).Length)
            {
                return value3;
            }
            return ((byte[])value)[value2];
        }

        private static string NormalizeProtectionPath(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return string.Empty;
            }
            return ((string)value).Replace("\\", "/").Trim();
        }

        private static bool TryRefreshLicenseContext(
            object lookupId,
            object licenseAccessKey,
            object orderId,
            bool forceRemoteRefresh,
            ActivationSource activationSource,
            object activationSourceName,
            out string message,
            out VerifiedLicenseContext verifiedContext)
        {
            verifiedContext = null;
            message = string.Empty;
            PsdReaderLicenseClientConfigDocument defaultClientConfig = CreateDefaultClientConfig();
            if (defaultClientConfig == null)
            {
                message = "当前授权组件初始化失败。";
                lock (_syncRoot)
                {
                    _currentStatus = CreateLicenseStatus((LicenseResultCode)2, message);
                    _cachedFeatureMask = 0;
                }
                TrackLicenseValidation(activationSource, null, orderId, (LicenseResultCode)2, null);
                return false;
            }

            string rootPublicKeyPem = GetRootPublicKeyPem();
            if (!string.IsNullOrWhiteSpace(rootPublicKeyPem))
            {
                lock (_syncRoot)
                {
                    _clientConfig = defaultClientConfig;
                }

                string normalizedOrderId = LicenseCryptoUtility.ExtractDigits(orderId);
                if (normalizedOrderId.Length != 19)
                {
                    normalizedOrderId = GetCachedOrderId();
                }

                byte[] effectiveLicenseAccessKey = (byte[])((licenseAccessKey == null || ((Array)licenseAccessKey).Length == 0)
                    ? GetCachedLicenseAccessKey()
                    : licenseAccessKey);
                if (effectiveLicenseAccessKey != null && effectiveLicenseAccessKey.Length != 0)
                {
                    string resolvedActivationSource = ResolveActivationSource(activationSourceName);
                    if (forceRemoteRefresh || !TryUseCachedLicenseWithoutRefresh(defaultClientConfig, out verifiedContext, out message))
                    {
                        string repositoryRequestTimestamp = CreateRepositoryRequestTimestamp(defaultClientConfig);
                        SaveRepositoryRequestTimestamp(repositoryRequestTimestamp);
                        if (!TryFetchAndValidateRemoteLicense(
                            defaultClientConfig,
                            rootPublicKeyPem,
                            lookupId,
                            effectiveLicenseAccessKey,
                            resolvedActivationSource,
                            repositoryRequestTimestamp,
                            out verifiedContext,
                            out message,
                            out var fetchResultCode))
                        {
                            if (TryFallbackToCachedLicense(fetchResultCode, message, out verifiedContext, out var cacheFallbackMessage))
                            {
                                message = cacheFallbackMessage;
                                lock (_syncRoot)
                                {
                                    _clientConfig = BuildClientConfig(verifiedContext?.GetProductMetadata(), verifiedContext?.GetCacheDocument());
                                    _currentStatus = CreateLicenseStatus((LicenseResultCode)1, message, _cachedLicense);
                                    _cachedFeatureMask = int.MinValue;
                                }
                                NotifyProtectionStateChanged();
                                TrackLicenseValidation(activationSource, _clientConfig, normalizedOrderId, (LicenseResultCode)1, verifiedContext);
                                return true;
                            }

                            PsdReaderLicenseClientConfigDocument failedClientConfig;
                            lock (_syncRoot)
                            {
                                _currentStatus = CreateLicenseStatus(fetchResultCode, message, _cachedLicense);
                                _cachedFeatureMask = 0;
                                failedClientConfig = BuildClientConfig();
                            }
                            TrackLicenseValidation(activationSource, failedClientConfig, normalizedOrderId, fetchResultCode, null);
                            return false;
                        }

                        if (verifiedContext.GetCacheDocument() != null)
                        {
                            verifiedContext.GetCacheDocument().Schema = 1;
                            verifiedContext.GetCacheDocument().ActivationSource = resolvedActivationSource;
                            verifiedContext.GetCacheDocument().LastRepositoryRequestUtc = ResolveRepositoryRequestTimestamp(repositoryRequestTimestamp);
                            verifiedContext.GetCacheDocument().OrderIdCipher = normalizedOrderId.Length == 19
                                ? LicenseCryptoUtility.EncryptDeviceBoundSecret(
                                    Encoding.UTF8.GetBytes(normalizedOrderId),
                                    verifiedContext.GetCacheDocument().DeviceFingerprint)
                                : string.Empty;
                        }

                        LicenseResultCode featureResultCode = EvaluateLicenseForFeature(verifiedContext, "Main");
                        if (featureResultCode != (LicenseResultCode)1)
                        {
                            message = BuildLicenseFailureMessage(verifiedContext, "Main");
                            if (verifiedContext.GetCacheDocument() == null)
                            {
                                lock (_syncRoot)
                                {
                                    _currentStatus = CreateLicenseStatus(featureResultCode, message, _cachedLicense);
                                    _cachedFeatureMask = 0;
                                }
                            }
                            else
                            {
                                verifiedContext.GetCacheDocument().LastResultCode = featureResultCode;
                                verifiedContext.GetCacheDocument().Message = message;
                                lock (_syncRoot)
                                {
                                    _cachedLicense = verifiedContext.GetCacheDocument();
                                    _licenseCacheStore.Save(_cachedLicense);
                                    _currentStatus = CreateLicenseStatus(featureResultCode, message, verifiedContext.GetCacheDocument());
                                    _cachedFeatureMask = 0;
                                }
                                NotifyProtectionStateChanged();
                            }
                            TrackLicenseValidation(
                                activationSource,
                                BuildClientConfig(verifiedContext.GetProductMetadata(), verifiedContext.GetCacheDocument()),
                                normalizedOrderId,
                                featureResultCode,
                                verifiedContext);
                            return false;
                        }

                        lock (_syncRoot)
                        {
                            _cachedLicense = verifiedContext.GetCacheDocument();
                            _clientConfig = BuildClientConfig(verifiedContext.GetProductMetadata(), verifiedContext.GetCacheDocument());
                            _licenseCacheStore.Save(_cachedLicense);
                            _currentStatus = CreateLicenseStatus((LicenseResultCode)1, "授权已更新。", _cachedLicense);
                            _cachedFeatureMask = int.MinValue;
                        }
                        NotifyProtectionStateChanged();
                        TrackLicenseValidation(activationSource, _clientConfig, normalizedOrderId, (LicenseResultCode)1, verifiedContext);
                        message = "授权已更新。";
                        return true;
                    }

                    lock (_syncRoot)
                    {
                        _clientConfig = BuildClientConfig(
                            verifiedContext?.GetProductMetadata(),
                            verifiedContext?.GetCacheDocument(),
                            defaultClientConfig);
                        _currentStatus = CreateLicenseStatus((LicenseResultCode)1, message, _cachedLicense);
                        _cachedFeatureMask = int.MinValue;
                    }
                    TrackLicenseValidation(activationSource, _clientConfig, normalizedOrderId, (LicenseResultCode)1, verifiedContext);
                    return true;
                }

                message = "当前授权需要重新输入订单号完成验证。";
                PsdReaderLicenseClientConfigDocument missingAccessKeyClientConfig;
                lock (_syncRoot)
                {
                    _currentStatus = CreateLicenseStatus((LicenseResultCode)4, message, _cachedLicense);
                    _cachedFeatureMask = 0;
                    missingAccessKeyClientConfig = BuildClientConfig();
                }
                TrackLicenseValidation(
                    activationSource,
                    missingAccessKeyClientConfig,
                    normalizedOrderId,
                    (LicenseResultCode)4,
                    null);
                return false;
            }

            message = "当前授权组件初始化失败。";
            lock (_syncRoot)
            {
                _currentStatus = CreateLicenseStatus((LicenseResultCode)2, message);
                _cachedFeatureMask = 0;
            }
            TrackLicenseValidation(activationSource, defaultClientConfig, orderId, (LicenseResultCode)2, null);
            return false;
        }

        private static void TrackLicenseValidation(ActivationSource value, object value2, object value3, LicenseResultCode value4, object value5)
        {
            string text = LicenseCryptoUtility.ExtractDigits(value3);
            string text2 = AppendTelemetryVersionSuffix(BuildValidationTelemetryEventName(value, value4));
            string text3 = ResolveOrderTelemetryUserId(text);
            if (value2 == null || string.IsNullOrWhiteSpace(text2) || string.IsNullOrWhiteSpace(text3))
            {
                return;
            }
            if (value == (ActivationSource)0 && value4 == (LicenseResultCode)1 && ((VerifiedLicenseContext)value5)?.GetCacheDocument() != null)
            {
                string text4 = LicenseCryptoUtility.NormalizeHex(((VerifiedLicenseContext)value5).GetCacheDocument().LookupId);
                string item = text3 + "|" + text4 + "|" + ((VerifiedLicenseContext)value5).GetCacheDocument().Revision + "|" + (((VerifiedLicenseContext)value5).GetCacheDocument().StatusText ?? string.Empty);
                lock (_syncRoot)
                {
                    if (_usageScopeDepth > 0 && !_trackedValidationEvents.Add(item))
                    {
                        return;
                    }
                }
            }
            _telemetryClient.TrackEvent((PsdReaderLicenseClientConfigDocument)value2, text3, text2);
        }

        private static string ResolveOrderTelemetryUserId(object value)
        {
            string text = LicenseCryptoUtility.ExtractDigits(value);
            if (text.Length == 19)
            {
                return text;
            }
            return CreateErrorTelemetryId(text);
        }

        private static string ResolveValidationTelemetryUserId(object value, ActivationSource value2, LicenseResultCode value3)
        {
            if (((string)value).Length == 19)
            {
                return (string)value;
            }
            string text = GetActivationSourceName(value2);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return CreateValidationAnomalyTelemetryId(text, value, value3);
            }
            return string.Empty;
        }

        private static string GetActivationSourceName(ActivationSource value)
        {
            return value switch
            {
                (ActivationSource)0 => ResolveActivationSourceName(null), 
                (ActivationSource)1 => "order", 
                (ActivationSource)2 => "project_file", 
                _ => string.Empty, 
            };
        }

        private static string CreateValidationAnomalyTelemetryId(object value, object value2, LicenseResultCode value3)
        {
            string text = LicenseCryptoUtility.ComputeDeviceIdHash(LicenseCryptoUtility.TrimOrEmpty(SystemInfo.deviceUniqueIdentifier));
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }
            string text2 = (string.Equals((string)value, "order", StringComparison.Ordinal) ? "ord" : (string.Equals((string)value, "project_file", StringComparison.Ordinal) ? "bin" : "unk"));
            string text3 = LicenseCryptoUtility.NormalizeHex(LicenseCryptoUtility.ComputeStringSha256Hex(string.Format("PsdReader|ValidationTelemetry|v2|{0}|{1}|{2}|{3}|{4}", "psd2ugui", text2, value3, value2, text)));
            if (text3.Length > 16)
            {
                text3 = text3.Substring(0, 16);
            }
            return "anom_" + text2 + "_" + text3;
        }

        private static string BuildValidationTelemetryEventName(ActivationSource value, LicenseResultCode value2)
        {
            return value switch
            {
                (ActivationSource)0 => BuildLicenseEventName(ResolveActivationSourceName(null), value2), 
                (ActivationSource)1 => BuildLicenseEventName("order", value2), 
                (ActivationSource)2 => BuildLicenseEventName("project_file", value2), 
                _ => string.Empty, 
            };
        }

        private static string AppendTelemetryVersionSuffix(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                if (!string.IsNullOrEmpty(_telemetryVersionSuffix))
                {
                    return (string)value + _telemetryVersionSuffix;
                }
                return (string)value;
            }
            return string.Empty;
        }

        private static string BuildTelemetryVersionSuffix()
        {
            string text = "3.0.0";
            if (!string.IsNullOrWhiteSpace(text))
            {
                StringBuilder stringBuilder = new StringBuilder(text.Length + 2);
                stringBuilder.Append("_v");
                foreach (char c in text)
                {
                    stringBuilder.Append((!char.IsLetterOrDigit(c)) ? '_' : c);
                }
                return stringBuilder.ToString();
            }
            return string.Empty;
        }

        private static string BuildLicenseEventName(object value, LicenseResultCode value2)
        {
            string text = GetLicenseEventPrefix(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }
            return text + "_" + GetLicenseResultTelemetrySuffix(value2);
        }

        private static string GetLicenseEventPrefix(object value)
        {
            string a = ResolveActivationSourceName(value);
            if (!string.Equals(a, "order", StringComparison.Ordinal))
            {
                if (string.Equals(a, "project_file", StringComparison.Ordinal))
                {
                    return "lic";
                }
                return string.Empty;
            }
            return "lic_adim";
        }

        private static string ResolveActivationSourceName(object value)
        {
            string a = ((string)(value ?? string.Empty)).Trim();
            if (string.Equals(a, "order", StringComparison.Ordinal))
            {
                return "order";
            }
            object obj;
            if (!string.Equals(a, "project_file", StringComparison.Ordinal))
            {
                PsdReaderLicenseCacheDocument value2 = _cachedLicense;
                if (value2 == null)
                {
                    obj = null;
                }
                else
                {
                    obj = value2.ActivationSource;
                    if (obj != null)
                    {
                        goto IL_004f;
                    }
                }
                obj = string.Empty;
                goto IL_004f;
            }
            return "project_file";
            IL_004f:
            a = ((string)obj).Trim();
            if (string.Equals(a, "order", StringComparison.Ordinal))
            {
                return "order";
            }
            if (!string.Equals(a, "project_file", StringComparison.Ordinal))
            {
                return string.Empty;
            }
            return "project_file";
        }

        private static string GetLicenseResultTelemetrySuffix(LicenseResultCode value)
        {
            return value switch
            {
                (LicenseResultCode)1 => "ok", 
                (LicenseResultCode)2 => "config", 
                (LicenseResultCode)3 => "invalid", 
                (LicenseResultCode)4 => "cache", 
                (LicenseResultCode)5 => "network", 
                (LicenseResultCode)6 => "metadata", 
                (LicenseResultCode)7 => "missing", 
                (LicenseResultCode)8 => "signature", 
                (LicenseResultCode)9 => "product", 
                (LicenseResultCode)10 => "suspended", 
                (LicenseResultCode)11 => "revoked", 
                (LicenseResultCode)12 => "expired", 
                (LicenseResultCode)13 => "feature", 
                (LicenseResultCode)14 => "device", 
                (LicenseResultCode)15 => "offline", 
                _ => "unknown", 
            };
        }

        private static bool TryFetchAndValidateRemoteLicense(object value, object value2, object value3, object value4, object value5, object value6, out VerifiedLicenseContext result, out string result2, out LicenseResultCode result3)
        {
            result = null;
            result2 = string.Empty;
            result3 = (LicenseResultCode)5;
            string text = GetProductMetadataPath();
            string text2 = GetProductMetadataScope();
            if (!FetchAndDecodeRootEnvelope<PsdReaderProductMetaPayloadDocument>(value, value2, text, text2, LicenseCryptoUtility.DeriveRepositoryDocumentKey(text2), out var psdReaderProductMetaPayloadDocument, out var array, out var flag, out var value7))
            {
                result2 = ((!flag) ? "读取产品元数据失败。" : "产品元数据不存在。");
                result3 = ((!flag) ? MapEnvelopeFetchFailureToResultCode(value7) : ((LicenseResultCode)6));
                return false;
            }
            if (!ValidateProductMetadata(psdReaderProductMetaPayloadDocument, out result2, out result3))
            {
                return false;
            }
            PsdReaderLicenseClientConfigDocument psdReaderLicenseClientConfigDocument = BuildClientConfig(psdReaderProductMetaPayloadDocument, _cachedLicense, (PsdReaderLicenseClientConfigDocument)value);
            PsdReaderLicensePayloadDocument psdReaderLicensePayloadDocument;
            byte[] array2;
            byte[] array3;
            string text7;
            string text8;
            string text10;
            PsdReaderLicensePayloadDocument psdReaderLicensePayloadDocument2;
            PsdReaderProductMetaPayloadDocument psdReaderProductMetaPayloadDocument2;
            PsdReaderOfflineLeasePayloadDocument psdReaderOfflineLeasePayloadDocument2;
            string text11;
            string text12;
            object obj;
            if (psdReaderLicenseClientConfigDocument != null && psdReaderLicenseClientConfigDocument.RepositoryBaseUrls != null && psdReaderLicenseClientConfigDocument.RepositoryBaseUrls.Length != 0)
            {
                string text3 = GetLicensePayloadPath(value3);
                string text4 = GetLicensePayloadScope(value3);
                if (FetchAndDecodeKeyedEnvelope<PsdReaderLicensePayloadDocument>(psdReaderLicenseClientConfigDocument, psdReaderProductMetaPayloadDocument.ActiveLeafPublicKeyPem, psdReaderProductMetaPayloadDocument.ActiveKid, text3, text4, LicenseCryptoUtility.DeriveLicensePayloadKey(value3, value4), out psdReaderLicensePayloadDocument, out array2, out var flag2, out var value8))
                {
                    if (ValidateLicensePayload(psdReaderLicensePayloadDocument, psdReaderProductMetaPayloadDocument, value3, out result2, out result3))
                    {
                        string text5 = GetOfflineLeasePath(value3);
                        string text6 = GetOfflineLeaseScope(value3);
                        if (!FetchAndDecodeKeyedEnvelope<PsdReaderOfflineLeasePayloadDocument>(psdReaderLicenseClientConfigDocument, psdReaderProductMetaPayloadDocument.ActiveLeafPublicKeyPem, psdReaderProductMetaPayloadDocument.ActiveKid, text5, text6, LicenseCryptoUtility.DeriveOfflineLeaseKey(value3, value4), out var psdReaderOfflineLeasePayloadDocument, out array3, out var flag3, out var value9))
                        {
                            result2 = (flag3 ? "离线授权租约不存在。" : "读取离线授权租约失败。");
                            result3 = ((!flag3) ? MapEnvelopeFetchFailureToResultCode(value9) : ((LicenseResultCode)8));
                            return false;
                        }
                        if (ValidateOfflineLease(psdReaderOfflineLeasePayloadDocument, psdReaderLicensePayloadDocument, psdReaderProductMetaPayloadDocument, value3, out result2, out result3))
                        {
                            _deviceFingerprintProvider.GetDeviceFingerprintHash(out text7);
                            if (string.IsNullOrWhiteSpace(text7))
                            {
                                result2 = "当前设备标识不可用。";
                                result3 = (LicenseResultCode)2;
                                return false;
                            }
                            text8 = string.Empty;
                            if (TryFetchDeviceShard(psdReaderLicenseClientConfigDocument, psdReaderProductMetaPayloadDocument, text7, out var psdReaderDeviceShardPayloadDocument, out var array4, out var flag4, out var value10))
                            {
                                string text9 = GetDeviceShardPrefix(text7);
                                if (!ValidateDeviceShard(psdReaderDeviceShardPayloadDocument, text9, out result2, out result3))
                                {
                                    return false;
                                }
                                if (IsDeviceBlocked(text7, psdReaderDeviceShardPayloadDocument))
                                {
                                    result2 = "当前设备已被限制使用此授权。";
                                    result3 = (LicenseResultCode)14;
                                    return false;
                                }
                                text8 = LicenseCryptoUtility.Base64UrlEncode(array4);
                            }
                            else if (!flag4)
                            {
                                result2 = "读取设备分片失败。";
                                result3 = MapEnvelopeFetchFailureToResultCode(value10);
                                return false;
                            }
                            text10 = DateTime.UtcNow.ToString("O");
                            psdReaderLicensePayloadDocument2 = psdReaderLicensePayloadDocument;
                            psdReaderProductMetaPayloadDocument2 = psdReaderProductMetaPayloadDocument;
                            psdReaderOfflineLeasePayloadDocument2 = psdReaderOfflineLeasePayloadDocument;
                            text11 = text7;
                            text12 = LicenseCryptoUtility.EncryptDeviceBoundSecret(value4, text7);
                            PsdReaderLicenseCacheDocument value11 = _cachedLicense;
                            if (value11 == null)
                            {
                                obj = null;
                            }
                            else
                            {
                                obj = value11.OrderIdCipher;
                                if (obj != null)
                                {
                                    goto IL_023a;
                                }
                            }
                            obj = string.Empty;
                            goto IL_023a;
                        }
                        return false;
                    }
                    return false;
                }
                result2 = ((!flag2) ? "读取授权文件失败。" : "当前订单对应的授权文件不存在。");
                result3 = ((!flag2) ? MapEnvelopeFetchFailureToResultCode(value8) : ((LicenseResultCode)7));
                return false;
            }
            result2 = "当前授权仓库配置不可用。";
            result3 = (LicenseResultCode)2;
            return false;
            IL_023a:
            PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument = BuildLicenseCacheDocument(psdReaderLicensePayloadDocument2, psdReaderProductMetaPayloadDocument2, psdReaderOfflineLeasePayloadDocument2, text11, text12, obj, value5, ResolveRepositoryRequestTimestamp(value6), text10, LicenseCryptoUtility.Base64UrlEncode(array), LicenseCryptoUtility.Base64UrlEncode(array2), LicenseCryptoUtility.Base64UrlEncode(array3), text8);
            VerifiedLicenseContext value12 = new VerifiedLicenseContext();
            value12.SetLicensePayload(psdReaderLicensePayloadDocument);
            value12.SetCacheDocument(psdReaderLicenseCacheDocument);
            value12.SetProductMetadata(psdReaderProductMetaPayloadDocument);
            value12.SetProtectionKeyHex(LicenseCryptoUtility.ComputeLicenseEntitlementFingerprint(psdReaderLicensePayloadDocument, psdReaderLicenseCacheDocument.CurrentMajorVersion, psdReaderLicenseCacheDocument.LocalCacheTimeoutDays, psdReaderLicenseCacheDocument.MetaRevision, psdReaderLicenseCacheDocument.ActiveKid, text7, psdReaderProductMetaPayloadDocument.StateSalt));
            value12.SetIntegrityToken(ComputeContextIntegrityToken(psdReaderLicensePayloadDocument, psdReaderLicenseCacheDocument, "Main"));
            result = value12;
            result2 = "授权远端验证成功。";
            result3 = (LicenseResultCode)1;
            return true;
        }

        private static bool IsDeviceBlocked(object value, object value2)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && ((PsdReaderDeviceShardPayloadDocument)value2)?.BlockedTargets != null && ((PsdReaderDeviceShardPayloadDocument)value2).BlockedTargets.Length != 0)
            {
                string text = LicenseCryptoUtility.NormalizeHex(value);
                return ((PsdReaderDeviceShardPayloadDocument)value2).BlockedTargets.Any((string candidate) => string.Equals(LicenseCryptoUtility.NormalizeHex(candidate), text, StringComparison.Ordinal));
            }
            return false;
        }

        private static bool TryFetchDeviceShard(object value, object value2, object value3, out PsdReaderDeviceShardPayloadDocument result, out byte[] result2, out bool result3, out EnvelopeFetchFailure result4)
        {
            string text = GetDeviceShardPrefix(value3);
            string text2 = GetDeviceShardPath(text);
            string text3 = GetDeviceShardScope(text);
            return FetchAndDecodeKeyedEnvelope<PsdReaderDeviceShardPayloadDocument>(value, ((PsdReaderProductMetaPayloadDocument)value2).ActiveLeafPublicKeyPem, ((PsdReaderProductMetaPayloadDocument)value2).ActiveKid, text2, text3, LicenseCryptoUtility.DeriveRepositoryDocumentKey(text3), out result, out result2, out result3, out result4, true);
        }

        private static int ComputeFeatureIntegrityToken(object value, object value2, int value3)
        {
            PsdReaderLicensePayloadDocument psdReaderLicensePayloadDocument = ((VerifiedLicenseContext)value).GetLicensePayload();
            PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument = ((VerifiedLicenseContext)value).GetCacheDocument();
            uint num = psdReaderLicensePayloadDocument?.FeatureMask ?? 0;
            bool flag = LicenseFeatureCatalog.ContainsFeature(num, value2);
            bool flag2 = (psdReaderLicensePayloadDocument?.StatusCode ?? byte.MaxValue) == 0;
            bool flag3 = GetLicenseSupportUntilUtc(psdReaderLicensePayloadDocument) >= DateTime.UtcNow;
            bool flag4 = MatchesCurrentDeviceFingerprint((psdReaderLicenseCacheDocument == null) ? string.Empty : psdReaderLicenseCacheDocument.DeviceFingerprint);
            int num2 = psdReaderLicenseCacheDocument?.MetaRevision ?? 1;
            string text = ((psdReaderLicenseCacheDocument == null) ? string.Empty : psdReaderLicenseCacheDocument.LookupId);
            uint num3 = psdReaderLicenseCacheDocument?.CatalogMask ?? 0;
            int num4 = Mathf.Max(1, num2) ^ HashToInt32(text) ^ (int)num3;
            int num5 = HashToInt32((psdReaderLicenseCacheDocument != null) ? psdReaderLicenseCacheDocument.ActiveKid : string.Empty);
            int[] array = new int[5]
            {
                Mathf.Max(1, psdReaderLicenseCacheDocument?.LocalCacheTimeoutDays ?? 1),
                Mathf.Max(1, psdReaderLicenseCacheDocument?.CurrentMajorVersion ?? 1),
                Mathf.Max(1, psdReaderLicensePayloadDocument?.MaxMajorVersion ?? 1),
                num4,
                (int)num
            };
            int num6 = LicenseCryptoUtility.MixIntegrityWords(array);
            return LicenseCryptoUtility.MixIntegrityWords(new int[8]
            {
                (psdReaderLicensePayloadDocument?.Revision ?? 0) ^ value3,
                (!flag) ? (~value3) : value3,
                (!flag2) ? 610839776 : 324508639,
                826366246,
                (!flag3) ? (-869029291) : 1437217740,
                flag4 ? 2135587861 : 365382271,
                num4 ^ HashToInt32((psdReaderLicenseCacheDocument == null) ? string.Empty : psdReaderLicenseCacheDocument.LicenseId),
                num5 ^ num6
            });
        }

        private static int ComputeContextIntegrityToken(object value, object value2, object value3)
        {
            int num = HashToInt32(value3);
            int num2 = ((value2 != null) ? (Mathf.Max(1, ((PsdReaderLicenseCacheDocument)value2).MetaRevision) ^ HashToInt32(((PsdReaderLicenseCacheDocument)value2).LookupId) ^ (int)((PsdReaderLicenseCacheDocument)value2).CatalogMask) : 0);
            int num3 = HashToInt32((value2 == null) ? string.Empty : ((PsdReaderLicenseCacheDocument)value2).ActiveKid);
            int[] array = new int[5]
            {
                Mathf.Max(1, ((PsdReaderLicenseCacheDocument)value2)?.LocalCacheTimeoutDays ?? 1),
                Mathf.Max(1, ((PsdReaderLicenseCacheDocument)value2)?.CurrentMajorVersion ?? 1),
                Mathf.Max(1, ((PsdReaderLicensePayloadDocument)value)?.MaxMajorVersion ?? 1),
                num2,
                (int)(((PsdReaderLicensePayloadDocument)value)?.FeatureMask ?? 0)
            };
            int num4 = LicenseCryptoUtility.MixIntegrityWords(array);
            int[] obj2 = new int[8] { 0, 0, 324508639, 826366246, 1437217740, 2135587861, 0, 0 };
            obj2[0] = (((PsdReaderLicensePayloadDocument)value)?.Revision ?? 0) ^ num;
            obj2[1] = num;
            obj2[6] = num2 ^ HashToInt32((value2 == null) ? string.Empty : ((PsdReaderLicenseCacheDocument)value2).LicenseId);
            obj2[7] = num3 ^ num4;
            return LicenseCryptoUtility.MixIntegrityWords(obj2);
        }

        private static int HashToInt32(object value)
        {
            byte[] array = LicenseCryptoUtility.ComputeStringSha256(value ?? string.Empty);
            if (array.Length >= 4)
            {
                return BitConverter.ToInt32(array, 0);
            }
            return 0;
        }

        private static VerifiedLicenseContext TryRestoreVerifiedContextFromCache(object value)
        {
            if (value == null)
            {
                return null;
            }
            if (!MatchesCurrentDeviceFingerprint(((PsdReaderLicenseCacheDocument)value).DeviceFingerprint))
            {
                return null;
            }
            string text = GetRootPublicKeyPem();
            if (!string.IsNullOrWhiteSpace(text))
            {
                byte[] array = DecryptLicenseAccessKey(value);
                if (array == null || array.Length == 0)
                {
                    return null;
                }
                string text2 = GetProductMetadataScope();
                if (TryDecodeCachedEnvelope<PsdReaderProductMetaPayloadDocument>(((PsdReaderLicenseCacheDocument)value).MetaEnvelopeBase64, text, null, text2, LicenseCryptoUtility.DeriveRepositoryDocumentKey(text2), out var psdReaderProductMetaPayloadDocument))
                {
                    if (ValidateProductMetadata(psdReaderProductMetaPayloadDocument, out var text3, out var value2))
                    {
                        string text4 = (string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)value).LookupId) ? ((PsdReaderLicenseCacheDocument)value).LicenseId : ((PsdReaderLicenseCacheDocument)value).LookupId);
                        string text5 = GetLicensePayloadScope(text4);
                        if (!TryDecodeCachedEnvelope<PsdReaderLicensePayloadDocument>(((PsdReaderLicenseCacheDocument)value).LicenseEnvelopeBase64, psdReaderProductMetaPayloadDocument.ActiveLeafPublicKeyPem, psdReaderProductMetaPayloadDocument.ActiveKid, text5, LicenseCryptoUtility.DeriveLicensePayloadKey(text4, array), out var psdReaderLicensePayloadDocument))
                        {
                            return null;
                        }
                        if (!ValidateLicensePayload(psdReaderLicensePayloadDocument, psdReaderProductMetaPayloadDocument, text4, out text3, out value2))
                        {
                            return null;
                        }
                        string text6 = GetOfflineLeaseScope(text4);
                        if (TryDecodeCachedEnvelope<PsdReaderOfflineLeasePayloadDocument>(((PsdReaderLicenseCacheDocument)value).OfflineLeaseEnvelopeBase64, psdReaderProductMetaPayloadDocument.ActiveLeafPublicKeyPem, psdReaderProductMetaPayloadDocument.ActiveKid, text6, LicenseCryptoUtility.DeriveOfflineLeaseKey(text4, array), out var psdReaderOfflineLeasePayloadDocument))
                        {
                            if (ValidateOfflineLease(psdReaderOfflineLeasePayloadDocument, psdReaderLicensePayloadDocument, psdReaderProductMetaPayloadDocument, text4, out text3, out value2))
                            {
                                _deviceFingerprintProvider.GetDeviceFingerprintHash(out var text7);
                                if (!string.IsNullOrWhiteSpace(text7))
                                {
                                    if (TryValidateClockWatermark(value, text7, out var dateTime, out var dateTime2, out text3, out var text8))
                                    {
                                        DateTime utcNow = DateTime.UtcNow;
                                        if (dateTime > utcNow || dateTime2 < utcNow)
                                        {
                                            return null;
                                        }
                                        if (!string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)value).DeviceShardEnvelopeBase64))
                                        {
                                            string text9 = GetDeviceShardScope(GetDeviceShardPrefix(text7));
                                            if (!TryDecodeCachedEnvelope<PsdReaderDeviceShardPayloadDocument>(((PsdReaderLicenseCacheDocument)value).DeviceShardEnvelopeBase64, psdReaderProductMetaPayloadDocument.ActiveLeafPublicKeyPem, psdReaderProductMetaPayloadDocument.ActiveKid, text9, LicenseCryptoUtility.DeriveRepositoryDocumentKey(text9), out var psdReaderDeviceShardPayloadDocument))
                                            {
                                                return null;
                                            }
                                            if (!ValidateDeviceShard(psdReaderDeviceShardPayloadDocument, GetDeviceShardPrefix(text7), out text8, out value2))
                                            {
                                                return null;
                                            }
                                            if (IsDeviceBlocked(text7, psdReaderDeviceShardPayloadDocument))
                                            {
                                                return null;
                                            }
                                        }
                                        PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument = BuildLicenseCacheDocument(psdReaderLicensePayloadDocument, psdReaderProductMetaPayloadDocument, psdReaderOfflineLeasePayloadDocument, text7, ((PsdReaderLicenseCacheDocument)value).LicenseAccessKeyCipher, ((PsdReaderLicenseCacheDocument)value).OrderIdCipher, ((PsdReaderLicenseCacheDocument)value).ActivationSource, ((PsdReaderLicenseCacheDocument)value).LastRepositoryRequestUtc, ((PsdReaderLicenseCacheDocument)value).LastVerifiedUtc, ((PsdReaderLicenseCacheDocument)value).MetaEnvelopeBase64, ((PsdReaderLicenseCacheDocument)value).LicenseEnvelopeBase64, ((PsdReaderLicenseCacheDocument)value).OfflineLeaseEnvelopeBase64, ((PsdReaderLicenseCacheDocument)value).DeviceShardEnvelopeBase64);
                                        VerifiedLicenseContext value3 = new VerifiedLicenseContext();
                                        value3.SetLicensePayload(psdReaderLicensePayloadDocument);
                                        value3.SetCacheDocument(psdReaderLicenseCacheDocument);
                                        value3.SetProductMetadata(psdReaderProductMetaPayloadDocument);
                                        value3.SetProtectionKeyHex(LicenseCryptoUtility.ComputeLicenseEntitlementFingerprint(psdReaderLicensePayloadDocument, Mathf.Max(1, psdReaderProductMetaPayloadDocument.CurrentMajorVersion), Mathf.Max(1, psdReaderLicenseCacheDocument.LocalCacheTimeoutDays), Mathf.Max(1, psdReaderProductMetaPayloadDocument.Revision), psdReaderLicenseCacheDocument.ActiveKid, text7, psdReaderProductMetaPayloadDocument.StateSalt));
                                        value3.SetIntegrityToken(ComputeContextIntegrityToken(psdReaderLicensePayloadDocument, psdReaderLicenseCacheDocument, "Main"));
                                        return value3;
                                    }
                                    return null;
                                }
                                return null;
                            }
                            return null;
                        }
                        return null;
                    }
                    return null;
                }
                return null;
            }
            return null;
        }

        private static PsdReaderLicenseCacheDocument BuildLicenseCacheDocument(object value, object value2, object value3, object value4, object value5, object value6, object value7, object value8, object value9, object value10, object value11, object value12, object value13)
        {
            ((PsdReaderLicensePayloadDocument)value).Features = LicenseFeatureCatalog.DecodeFeatureMask(((PsdReaderLicensePayloadDocument)value).FeatureMask);
            ((PsdReaderLicensePayloadDocument)value).Status = LicensePayloadBinaryReader.GetStatusText(((PsdReaderLicensePayloadDocument)value).StatusCode);
            DateTime dateTime = LicenseCryptoUtility.ParseUtcOrDefault((!string.IsNullOrWhiteSpace((string)value9)) ? value9 : DateTime.UtcNow.ToString("O"), DateTime.UtcNow);
            DateTime dateTime2 = GetLicenseSupportUntilUtc(value);
            string supportUntilUtc = ((!(dateTime2 > DateTime.MinValue)) ? string.Empty : dateTime2.ToString("O"));
            string text = dateTime.ToString("O");
            PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument = new PsdReaderLicenseCacheDocument
            {
                Schema = 1,
                LookupId = ((PsdReaderLicensePayloadDocument)value).LookupId,
                LicenseId = ((PsdReaderLicensePayloadDocument)value).LicenseId,
                StatusCode = ((PsdReaderLicensePayloadDocument)value).StatusCode,
                StatusText = (((PsdReaderLicensePayloadDocument)value).Status ?? string.Empty),
                Message = "授权有效。",
                Features = (((PsdReaderLicensePayloadDocument)value).Features ?? Array.Empty<string>()),
                FeatureMask = ((PsdReaderLicensePayloadDocument)value).FeatureMask,
                CatalogMask = ((PsdReaderProductMetaPayloadDocument)value2).FeatureCatalogMask,
                LastVerifiedUtc = text,
                SupportUntilUtc = supportUntilUtc,
                ClockHighWaterUtc = text,
                Revision = ((PsdReaderLicensePayloadDocument)value).Revision,
                LocalCacheTimeoutDays = Math.Max(1, ((PsdReaderOfflineLeasePayloadDocument)value3)?.OfflineCacheTimeoutDays ?? ((PsdReaderProductMetaPayloadDocument)value2).LocalCacheTimeoutDays),
                CurrentMajorVersion = Math.Max(1, ((PsdReaderProductMetaPayloadDocument)value2).CurrentMajorVersion),
                MaxMajorVersion = Math.Max(1, ((PsdReaderLicensePayloadDocument)value).MaxMajorVersion),
                DeviceFingerprint = (string)(value4 ?? string.Empty),
                MetaRevision = Math.Max(1, ((PsdReaderProductMetaPayloadDocument)value2).Revision),
                ActiveKid = (((PsdReaderProductMetaPayloadDocument)value2).ActiveKid ?? string.Empty),
                LastResultCode = (LicenseResultCode)1,
                LicenseAccessKeyCipher = (string)(value5 ?? string.Empty),
                OrderIdCipher = (string)(value6 ?? string.Empty),
                GrantSeedCipher = string.Empty,
                StateSaltHex = ((((PsdReaderProductMetaPayloadDocument)value2)?.StateSalt == null || ((PsdReaderProductMetaPayloadDocument)value2).StateSalt.Length == 0) ? string.Empty : LicenseCryptoUtility.BytesToHex(((PsdReaderProductMetaPayloadDocument)value2).StateSalt)),
                TelemetryUrl = (((PsdReaderProductMetaPayloadDocument)value2).TelemetryUrl ?? string.Empty),
                TelemetrySiteId = (((PsdReaderProductMetaPayloadDocument)value2).TelemetrySiteId ?? string.Empty),
                ActivationSource = NormalizeActivationSourceForCache(value7),
                LastRepositoryRequestUtc = (string)(value8 ?? string.Empty),
                MetaEnvelopeBase64 = (string)(value10 ?? string.Empty),
                LicenseEnvelopeBase64 = (string)(value11 ?? string.Empty),
                OfflineLeaseEnvelopeBase64 = (string)(value12 ?? string.Empty),
                DeviceShardEnvelopeBase64 = (string)(value13 ?? string.Empty)
            };
            string text2 = LicenseCryptoUtility.ComputeLicenseCacheProof(psdReaderLicenseCacheDocument, value4, dateTime, dateTime2);
            psdReaderLicenseCacheDocument.ValidationTranscriptHead = LicenseCryptoUtility.ComputeValidationTranscriptHead(psdReaderLicenseCacheDocument, value4, dateTime, dateTime2, text2);
            psdReaderLicenseCacheDocument.ClockWatermarkCipher = LicenseCryptoUtility.EncryptClockWatermark(dateTime, dateTime2, text2, psdReaderLicenseCacheDocument.ValidationTranscriptHead, value4);
            return psdReaderLicenseCacheDocument;
        }

        private static bool TryDecodeCachedEnvelope<TValue>(object value, object value2, object value3, object value4, object value5, out TValue result) where TValue : class
        {
            result = null;
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return false;
            }
            try
            {
                byte[] array = LicenseCryptoUtility.Base64UrlDecode(value);
                if (array != null && array.Length != 0)
                {
                    if (LicenseCryptoUtility.TryReadEncryptedSignedEnvelope(array, value5, value4, out var psdReaderSignedEnvelopeDocument) && psdReaderSignedEnvelopeDocument != null)
                    {
                        if (!string.IsNullOrWhiteSpace((string)value3) && !string.Equals((string)value3, psdReaderSignedEnvelopeDocument.Kid, StringComparison.Ordinal))
                        {
                            return false;
                        }
                        if (!LicenseCryptoUtility.TryVerifyAndDecryptEnvelope(value2, psdReaderSignedEnvelopeDocument, value4, value5, out var array2))
                        {
                            return false;
                        }
                        return DeserializePayload<TValue>(array2, out result);
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

        private static bool TryGetValidCachedFeatureContext(
            object featureName,
            out VerifiedLicenseContext verifiedContext,
            out string message)
        {
            verifiedContext = null;
            message = string.Empty;
            if (IsLicenseCacheValid(_cachedLicense))
            {
                verifiedContext = TryRestoreVerifiedContextFromCache(_cachedLicense);
                if (verifiedContext == null)
                {
                    return false;
                }
                return TryValidateContextForFeature(
                    featureName,
                    verifiedContext,
                    out verifiedContext,
                    out message);
            }
            return false;
        }

        private static bool TryValidateContextForFeature(
            object featureName,
            object sourceContext,
            out VerifiedLicenseContext verifiedContext,
            out string message)
        {
            verifiedContext = (VerifiedLicenseContext)sourceContext;
            message = string.Empty;
            if (verifiedContext == null)
            {
                message = "当前没有可用授权。";
                _currentStatus = CreateLicenseStatus((LicenseResultCode)4, message);
                return false;
            }

            VerifiedLicenseContext integrityCheckedContext = new VerifiedLicenseContext();
            integrityCheckedContext.SetLicensePayload(verifiedContext.GetLicensePayload());
            integrityCheckedContext.SetCacheDocument(verifiedContext.GetCacheDocument());
            integrityCheckedContext.SetProductMetadata(verifiedContext.GetProductMetadata());
            integrityCheckedContext.SetProtectionKeyHex(verifiedContext.GetProtectionKeyHex());
            integrityCheckedContext.SetIntegrityToken(ComputeContextIntegrityToken(
                verifiedContext.GetLicensePayload(),
                verifiedContext.GetCacheDocument(),
                featureName));
            verifiedContext = integrityCheckedContext;

            LicenseResultCode featureResultCode = EvaluateLicenseForFeature(verifiedContext, featureName);
            int featureHash = HashToInt32(featureName);
            int computedIntegrityToken = ComputeFeatureIntegrityToken(verifiedContext, featureName, featureHash);
            if (featureResultCode == (LicenseResultCode)1 &&
                (computedIntegrityToken ^ verifiedContext.GetIntegrityToken()) == 0)
            {
                message = "授权有效。";
                _currentStatus = CreateLicenseStatus((LicenseResultCode)1, message, verifiedContext.GetCacheDocument());
                return true;
            }

            message = BuildLicenseFailureMessage(verifiedContext, featureName);
            _currentStatus = CreateLicenseStatus(
                featureResultCode != (LicenseResultCode)1
                    ? featureResultCode
                    : GetValidationFailureCode(verifiedContext, featureName),
                message,
                verifiedContext.GetCacheDocument());
            return false;
        }

        private static bool TryValidateLicenseServiceInitialization(out string message)
        {
            if (_clientConfig == null)
            {
                _clientConfig = CreateDefaultClientConfig();
            }
            if (_clientConfig != null)
            {
                if (string.IsNullOrWhiteSpace(GetRootPublicKeyPem()))
                {
                    message = "当前授权组件初始化失败。";
                    _currentStatus = CreateLicenseStatus((LicenseResultCode)2, message);
                    return false;
                }
                if (_currentStatus != null && _currentStatus.GetResultCode() != 0 && _currentStatus.GetResultCode() != (LicenseResultCode)1 && _currentStatus.GetResultCode() != (LicenseResultCode)4)
                {
                    message = _currentStatus.Message ?? string.Empty;
                    return false;
                }
                message = "尚未激活 Psd2UGUI 授权。";
                _currentStatus = CreateLicenseStatus((LicenseResultCode)4, message);
                return false;
            }
            message = "当前授权组件初始化失败。";
            _currentStatus = CreateLicenseStatus((LicenseResultCode)2, message);
            return false;
        }

        private static bool HasCachedLicenseLookup()
        {
            if (_cachedLicense != null)
            {
                return !string.IsNullOrWhiteSpace(_cachedLicense.LookupId);
            }
            return false;
        }

        private static byte[] GetCachedLicenseAccessKey()
        {
            return DecryptLicenseAccessKey(_cachedLicense);
        }

        private static byte[] DecryptLicenseAccessKey(object value)
        {
            if (value == null || string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)value).LicenseAccessKeyCipher))
            {
                return Array.Empty<byte>();
            }
            _deviceFingerprintProvider.GetDeviceFingerprintHash(out var text);
            if (!LicenseCryptoUtility.TryDecryptDeviceBoundSecret(((PsdReaderLicenseCacheDocument)value).LicenseAccessKeyCipher, text, out var result))
            {
                return Array.Empty<byte>();
            }
            return result;
        }

        private static string GetCachedOrderId()
        {
            if (_cachedLicense != null && !string.IsNullOrWhiteSpace(_cachedLicense.OrderIdCipher))
            {
                _deviceFingerprintProvider.GetDeviceFingerprintHash(out var text);
                if (!LicenseCryptoUtility.TryDecryptDeviceBoundSecret(_cachedLicense.OrderIdCipher, text, out var array))
                {
                    return string.Empty;
                }
                return LicenseCryptoUtility.ExtractDigits(LicenseCryptoUtility.DecodeUtf8(array));
            }
            return string.Empty;
        }

        private static bool IsOrderActivatedCache(object value)
        {
            if (value != null)
            {
                return string.Equals(ResolveActivationSource(((PsdReaderLicenseCacheDocument)value).ActivationSource), "order", StringComparison.Ordinal);
            }
            return false;
        }

        private static string NormalizeActivationSourceForCache(object value)
        {
            if (string.Equals(((string)(value ?? string.Empty)).Trim(), "order", StringComparison.Ordinal))
            {
                return "order";
            }
            return "project_file";
        }

        private static string ResolveActivationSource(object value)
        {
            string a = ((string)(value ?? string.Empty)).Trim();
            if (string.Equals(a, "project_file", StringComparison.Ordinal))
            {
                return "project_file";
            }
            if (string.Equals(a, "order", StringComparison.Ordinal))
            {
                return "order";
            }
            PsdReaderLicenseCacheDocument value2 = _cachedLicense;
            object obj;
            if (value2 != null)
            {
                obj = value2.ActivationSource;
                if (obj != null)
                {
                    goto IL_0055;
                }
            }
            else
            {
                obj = null;
            }
            obj = string.Empty;
            goto IL_0055;
            IL_0055:
            string a2 = ((string)obj).Trim();
            if (string.Equals(a2, "project_file", StringComparison.Ordinal))
            {
                return "project_file";
            }
            if (!string.Equals(a2, "order", StringComparison.Ordinal))
            {
                if (GetCachedOrderId().Length != 19)
                {
                    return "project_file";
                }
                return "order";
            }
            return "order";
        }

        private static bool ShouldUseProjectLicenseFile()
        {
            if (HasProjectLicenseFile())
            {
                if (HasCachedLicenseLookup())
                {
                    return GetCachedLicenseAccessKey().Length == 0;
                }
                return true;
            }
            return false;
        }

        private static void AutoActivateProjectLicenseIfChanged()
        {
            if (!TryGetProjectLicenseFileStamp(out var b))
            {
                return;
            }
            lock (_syncRoot)
            {
                EnsureCacheLoaded();
                if (string.Equals(_lastProjectLicenseFileStamp, b, StringComparison.Ordinal) || !ShouldAutoActivateProjectLicense())
                {
                    return;
                }
                _lastProjectLicenseFileStamp = b;
            }
            ActivateFromProjectLicenseFile(out var _);
        }

        private static bool ShouldAutoActivateProjectLicense()
        {
            if (_currentStatus != null && _currentStatus.GetResultCode() == (LicenseResultCode)1)
            {
                return false;
            }
            if (HasCachedLicenseLookup())
            {
                return GetCachedLicenseAccessKey().Length == 0;
            }
            return true;
        }

        private static bool TryGetProjectLicenseFileStamp(out string result)
        {
            result = string.Empty;
            string text = LicensePathProvider.GetProjectLicenseAbsolutePath();
            if (File.Exists(text) && TryGetProjectLicenseFile(out var _, out var dateTime, out var _))
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(text);
                    result = fileInfo.Length + ":" + fileInfo.LastWriteTimeUtc.Ticks + ":" + dateTime.Ticks;
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        private static bool TryLoadProjectLicenseContext(out VerifiedLicenseContext result, out string result2)
        {
            LicenseResultCode value;
            return TryActivateProjectLicenseFileCore(out result, out result2, out value);
        }

        private static bool TryActivateProjectLicenseFileCore(out VerifiedLicenseContext result, out string result2, out LicenseResultCode result3)
        {
            result = null;
            result2 = string.Empty;
            result3 = (LicenseResultCode)4;
            if (TryReadProjectLicenseFile(out var psdReaderProjectLicenseBundleDocument, out var dateTime, out result2, out result3))
            {
                if (psdReaderProjectLicenseBundleDocument != null && !(dateTime <= DateTime.UtcNow))
                {
                    if (!LicenseCryptoUtility.TryUnwrapProjectLicenseSecret(psdReaderProjectLicenseBundleDocument.WrappedOrderIdCipher, psdReaderProjectLicenseBundleDocument.LookupId, dateTime, out var array))
                    {
                        result2 = "项目授权文件中的订单号信息无效。";
                        result3 = (LicenseResultCode)8;
                        return false;
                    }
                    string text = LicenseCryptoUtility.ExtractDigits(LicenseCryptoUtility.DecodeUtf8(array));
                    if (text.Length != 19)
                    {
                        result2 = "项目授权文件中的订单号信息无效。";
                        result3 = (LicenseResultCode)8;
                        return false;
                    }
                    if (LicenseCryptoUtility.TryUnwrapProjectLicenseSecret(psdReaderProjectLicenseBundleDocument.WrappedLicenseAccessKey, psdReaderProjectLicenseBundleDocument.LookupId, dateTime, out var array2) && array2 != null && array2.Length != 0)
                    {
                        return TryRefreshLicenseContext(psdReaderProjectLicenseBundleDocument.LookupId, array2, text, true, (ActivationSource)2, "project_file", out result2, out result);
                    }
                    result2 = "项目授权文件中的访问密钥无效。";
                    result3 = (LicenseResultCode)8;
                    return false;
                }
                result2 = "项目授权文件已过期或不可用。";
                result3 = (LicenseResultCode)12;
                return false;
            }
            return false;
        }

        private static bool TryValidateProjectLicenseBundle(object value, out VerifiedLicenseContext result, out string result2, out LicenseResultCode result3)
        {
            result = null;
            result2 = string.Empty;
            result3 = (LicenseResultCode)8;
            if (value == null)
            {
                result2 = "项目授权文件不可用。";
                result3 = (LicenseResultCode)4;
                return false;
            }
            DateTime dateTime = LicenseCryptoUtility.ParseUtcOrDefault(((PsdReaderProjectLicenseBundleDocument)value).ExportExpiresUtc, DateTime.MinValue);
            if (dateTime <= DateTime.UtcNow)
            {
                result2 = "项目授权文件已过期。";
                result3 = (LicenseResultCode)12;
                return false;
            }
            string text = GetRootPublicKeyPem();
            if (string.IsNullOrWhiteSpace(text))
            {
                result2 = "当前授权组件初始化失败。";
                result3 = (LicenseResultCode)2;
                return false;
            }
            if (LicenseCryptoUtility.TryUnwrapProjectLicenseSecret(((PsdReaderProjectLicenseBundleDocument)value).WrappedLicenseAccessKey, ((PsdReaderProjectLicenseBundleDocument)value).LookupId, dateTime, out var array) && array != null && array.Length != 0)
            {
                string text2 = GetProductMetadataScope();
                if (!TryDecodeCachedEnvelope<PsdReaderProductMetaPayloadDocument>(((PsdReaderProjectLicenseBundleDocument)value).MetaEnvelopeBase64, text, null, text2, LicenseCryptoUtility.DeriveRepositoryDocumentKey(text2), out var psdReaderProductMetaPayloadDocument))
                {
                    result2 = "项目授权文件中的产品元数据无效。";
                    result3 = (LicenseResultCode)8;
                    return false;
                }
                if (!ValidateProductMetadata(psdReaderProductMetaPayloadDocument, out result2, out result3))
                {
                    return false;
                }
                string text3 = GetLicensePayloadScope(((PsdReaderProjectLicenseBundleDocument)value).LookupId);
                if (TryDecodeCachedEnvelope<PsdReaderLicensePayloadDocument>(((PsdReaderProjectLicenseBundleDocument)value).LicenseEnvelopeBase64, psdReaderProductMetaPayloadDocument.ActiveLeafPublicKeyPem, psdReaderProductMetaPayloadDocument.ActiveKid, text3, LicenseCryptoUtility.DeriveLicensePayloadKey(((PsdReaderProjectLicenseBundleDocument)value).LookupId, array), out var psdReaderLicensePayloadDocument))
                {
                    if (ValidateLicensePayload(psdReaderLicensePayloadDocument, psdReaderProductMetaPayloadDocument, ((PsdReaderProjectLicenseBundleDocument)value).LookupId, out result2, out result3))
                    {
                        if (string.Equals(psdReaderLicensePayloadDocument.LicenseId, ((PsdReaderProjectLicenseBundleDocument)value).LicenseId, StringComparison.Ordinal))
                        {
                            string text4 = GetOfflineLeaseScope(((PsdReaderProjectLicenseBundleDocument)value).LookupId);
                            if (TryDecodeCachedEnvelope<PsdReaderOfflineLeasePayloadDocument>(((PsdReaderProjectLicenseBundleDocument)value).OfflineLeaseEnvelopeBase64, psdReaderProductMetaPayloadDocument.ActiveLeafPublicKeyPem, psdReaderProductMetaPayloadDocument.ActiveKid, text4, LicenseCryptoUtility.DeriveOfflineLeaseKey(((PsdReaderProjectLicenseBundleDocument)value).LookupId, array), out var psdReaderOfflineLeasePayloadDocument))
                            {
                                if (ValidateOfflineLease(psdReaderOfflineLeasePayloadDocument, psdReaderLicensePayloadDocument, psdReaderProductMetaPayloadDocument, ((PsdReaderProjectLicenseBundleDocument)value).LookupId, out result2, out result3))
                                {
                                    _deviceFingerprintProvider.GetDeviceFingerprintHash(out var text5);
                                    if (string.IsNullOrWhiteSpace(text5))
                                    {
                                        result2 = "当前设备标识不可用。";
                                        result3 = (LicenseResultCode)2;
                                        return false;
                                    }
                                    DateTime dateTime2 = GetLicenseSupportUntilUtc(psdReaderLicensePayloadDocument);
                                    DateTime dateTime3 = ((dateTime2 <= DateTime.MinValue || dateTime < dateTime2) ? dateTime : dateTime2);
                                    if (dateTime3 <= DateTime.UtcNow)
                                    {
                                        result2 = "项目授权文件已过期。";
                                        result3 = (LicenseResultCode)12;
                                        return false;
                                    }
                                    PsdReaderLicensePayloadDocument psdReaderLicensePayloadDocument2 = new PsdReaderLicensePayloadDocument
                                    {
                                        Schema = psdReaderLicensePayloadDocument.Schema,
                                        Revision = psdReaderLicensePayloadDocument.Revision,
                                        LookupId = psdReaderLicensePayloadDocument.LookupId,
                                        LicenseId = psdReaderLicensePayloadDocument.LicenseId,
                                        VendorCode = psdReaderLicensePayloadDocument.VendorCode,
                                        ProductCode = psdReaderLicensePayloadDocument.ProductCode,
                                        StatusCode = psdReaderLicensePayloadDocument.StatusCode,
                                        Status = psdReaderLicensePayloadDocument.Status,
                                        IssuedUtcTicks = psdReaderLicensePayloadDocument.IssuedUtcTicks,
                                        IssuedAtUtc = psdReaderLicensePayloadDocument.IssuedAtUtc,
                                        SupportUntilUtcTicks = dateTime3.Ticks,
                                        SupportUntilUtc = dateTime3.ToString("O"),
                                        MaxMajorVersion = psdReaderLicensePayloadDocument.MaxMajorVersion,
                                        FeatureMask = psdReaderLicensePayloadDocument.FeatureMask,
                                        Features = (psdReaderLicensePayloadDocument.Features ?? Array.Empty<string>()),
                                        GrantSeed = (psdReaderLicensePayloadDocument.GrantSeed ?? Array.Empty<byte>()),
                                        Policy = (psdReaderLicensePayloadDocument.Policy ?? new PsdReaderLicensePolicyDocument())
                                    };
                                    PsdReaderOfflineLeasePayloadDocument psdReaderOfflineLeasePayloadDocument2 = new PsdReaderOfflineLeasePayloadDocument
                                    {
                                        Schema = psdReaderOfflineLeasePayloadDocument.Schema,
                                        Kind = psdReaderOfflineLeasePayloadDocument.Kind,
                                        Revision = psdReaderOfflineLeasePayloadDocument.Revision,
                                        LookupId = psdReaderOfflineLeasePayloadDocument.LookupId,
                                        LicenseId = psdReaderOfflineLeasePayloadDocument.LicenseId,
                                        VendorCode = psdReaderOfflineLeasePayloadDocument.VendorCode,
                                        ProductCode = psdReaderOfflineLeasePayloadDocument.ProductCode,
                                        StatusCode = psdReaderOfflineLeasePayloadDocument.StatusCode,
                                        Status = psdReaderOfflineLeasePayloadDocument.Status,
                                        FeatureMask = psdReaderOfflineLeasePayloadDocument.FeatureMask,
                                        Features = (psdReaderOfflineLeasePayloadDocument.Features ?? Array.Empty<string>()),
                                        SupportUntilUtcTicks = dateTime3.Ticks,
                                        SupportUntilUtc = dateTime3.ToString("O"),
                                        OfflineCacheTimeoutDays = psdReaderOfflineLeasePayloadDocument.OfflineCacheTimeoutDays,
                                        IssuedUtcTicks = psdReaderOfflineLeasePayloadDocument.IssuedUtcTicks,
                                        IssuedAtUtc = psdReaderOfflineLeasePayloadDocument.IssuedAtUtc
                                    };
                                    string text6 = DateTime.UtcNow.ToString("O");
                                    PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument = BuildLicenseCacheDocument(psdReaderLicensePayloadDocument2, psdReaderProductMetaPayloadDocument, psdReaderOfflineLeasePayloadDocument2, text5, LicenseCryptoUtility.EncryptDeviceBoundSecret(array, text5), string.Empty, "project_file", string.Empty, text6, ((PsdReaderProjectLicenseBundleDocument)value).MetaEnvelopeBase64, ((PsdReaderProjectLicenseBundleDocument)value).LicenseEnvelopeBase64, ((PsdReaderProjectLicenseBundleDocument)value).OfflineLeaseEnvelopeBase64, string.Empty);
                                    VerifiedLicenseContext value2 = new VerifiedLicenseContext();
                                    value2.SetLicensePayload(psdReaderLicensePayloadDocument2);
                                    value2.SetCacheDocument(psdReaderLicenseCacheDocument);
                                    value2.SetProductMetadata(psdReaderProductMetaPayloadDocument);
                                    value2.SetProtectionKeyHex(LicenseCryptoUtility.ComputeLicenseEntitlementFingerprint(psdReaderLicensePayloadDocument2, psdReaderLicenseCacheDocument.CurrentMajorVersion, psdReaderLicenseCacheDocument.LocalCacheTimeoutDays, psdReaderLicenseCacheDocument.MetaRevision, psdReaderLicenseCacheDocument.ActiveKid, text5, psdReaderProductMetaPayloadDocument.StateSalt));
                                    value2.SetIntegrityToken(ComputeContextIntegrityToken(psdReaderLicensePayloadDocument2, psdReaderLicenseCacheDocument, "Main"));
                                    result = value2;
                                    result2 = "项目授权文件验证成功。";
                                    result3 = (LicenseResultCode)1;
                                    return true;
                                }
                                return false;
                            }
                            result2 = "项目授权文件中的离线租约无效。";
                            result3 = (LicenseResultCode)8;
                            return false;
                        }
                        result2 = "项目授权文件中的授权标识不匹配。";
                        result3 = (LicenseResultCode)8;
                        return false;
                    }
                    return false;
                }
                result2 = "项目授权文件中的授权数据无效。";
                result3 = (LicenseResultCode)8;
                return false;
            }
            result2 = "项目授权文件中的访问密钥无效。";
            result3 = (LicenseResultCode)8;
            return false;
        }

        private static bool TryBuildProjectLicenseBundle(int value, out PsdReaderProjectLicenseBundleDocument result, out string result2)
        {
            result = null;
            result2 = string.Empty;
            int num = ClampProjectLicenseDays(value);
            if (!TryGetExportableLicenseCache(out var psdReaderLicenseCacheDocument, out var num2, out result2))
            {
                return false;
            }
            if (!TryCalculateProjectLicenseExpiry(num, num2, out var dateTime))
            {
                result2 = "当前授权不允许使用所选时长导出授权文件。";
                return false;
            }
            byte[] array = DecryptLicenseAccessKey(psdReaderLicenseCacheDocument);
            if (array != null && array.Length != 0)
            {
                if (!string.IsNullOrWhiteSpace(psdReaderLicenseCacheDocument.MetaEnvelopeBase64) && !string.IsNullOrWhiteSpace(psdReaderLicenseCacheDocument.LicenseEnvelopeBase64) && !string.IsNullOrWhiteSpace(psdReaderLicenseCacheDocument.OfflineLeaseEnvelopeBase64))
                {
                    DateTime utcNow = DateTime.UtcNow;
                    string text = LicenseCryptoUtility.WrapProjectLicenseSecret(array, psdReaderLicenseCacheDocument.LookupId, dateTime);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        result2 = "封装授权访问密钥失败。";
                        return false;
                    }
                    string text2 = GetCachedOrderId();
                    if (text2.Length != 19)
                    {
                        result2 = "当前授权缓存中的订单号不可用，请先使用订单号重新验证授权。";
                        return false;
                    }
                    string text3 = LicenseCryptoUtility.WrapProjectLicenseSecret(Encoding.UTF8.GetBytes(text2), psdReaderLicenseCacheDocument.LookupId, dateTime);
                    if (string.IsNullOrWhiteSpace(text3))
                    {
                        result2 = "封装订单号失败。";
                        return false;
                    }
                    result = new PsdReaderProjectLicenseBundleDocument
                    {
                        Schema = 2,
                        VendorCode = "efunstudio",
                        ProductCode = "psd2ugui",
                        LookupId = (psdReaderLicenseCacheDocument.LookupId ?? string.Empty),
                        LicenseId = (psdReaderLicenseCacheDocument.LicenseId ?? string.Empty),
                        MetaEnvelopeBase64 = (psdReaderLicenseCacheDocument.MetaEnvelopeBase64 ?? string.Empty),
                        LicenseEnvelopeBase64 = (psdReaderLicenseCacheDocument.LicenseEnvelopeBase64 ?? string.Empty),
                        OfflineLeaseEnvelopeBase64 = (psdReaderLicenseCacheDocument.OfflineLeaseEnvelopeBase64 ?? string.Empty),
                        WrappedLicenseAccessKey = text,
                        WrappedOrderIdCipher = text3,
                        ExportIssuedUtcTicks = utcNow.Ticks,
                        ExportIssuedUtc = utcNow.ToString("O"),
                        ExportExpiresUtcTicks = dateTime.Ticks,
                        ExportExpiresUtc = dateTime.ToString("O")
                    };
                    result.BundleSeal = LicenseCryptoUtility.ComputeProjectLicenseBundleSeal(result);
                    if (string.IsNullOrWhiteSpace(result.BundleSeal))
                    {
                        result2 = "生成授权文件校验信息失败。";
                        result = null;
                        return false;
                    }
                    return true;
                }
                result2 = "当前授权缓存不完整，无法导出授权文件。";
                return false;
            }
            result2 = "当前设备上的授权访问密钥不可用，请先重新验证授权。";
            return false;
        }

        private static bool TryReadProjectLicenseFile(out PsdReaderProjectLicenseBundleDocument result, out DateTime result2, out string result3, out LicenseResultCode result4)
        {
            result = null;
            result2 = DateTime.MinValue;
            result3 = string.Empty;
            result4 = (LicenseResultCode)4;
            string path = LicensePathProvider.GetProjectLicenseAbsolutePath();
            if (File.Exists(path))
            {
                byte[] array;
                try
                {
                    array = File.ReadAllBytes(path);
                }
                catch (Exception)
                {
                    result3 = "读取项目授权文件失败。";
                    result4 = (LicenseResultCode)4;
                    return false;
                }
                if (!LicenseCryptoUtility.TryDeserializeProjectLicenseBundle(array, out result) || result == null)
                {
                    result3 = "项目授权文件内容无效或已损坏。";
                    result = null;
                    result4 = (LicenseResultCode)8;
                    return false;
                }
                result2 = LicenseCryptoUtility.ParseUtcOrDefault(result.ExportExpiresUtc, DateTime.MinValue);
                if (result2 <= DateTime.UtcNow)
                {
                    bool flag = DeleteProjectLicenseFile();
                    result3 = (flag ? "项目授权文件已过期，已自动删除。" : "项目授权文件已过期，请删除后重新导出。");
                    result = null;
                    result2 = DateTime.MinValue;
                    result4 = (LicenseResultCode)12;
                    return false;
                }
                result4 = (LicenseResultCode)1;
                return true;
            }
            result3 = "插件目录里未找到项目授权文件：" + LicensePathProvider.GetProjectLicenseAssetPath();
            return false;
        }

        private static bool TryGetProjectLicenseFile(out PsdReaderProjectLicenseBundleDocument result, out DateTime result2, out string result3)
        {
            LicenseResultCode value;
            return TryReadProjectLicenseFile(out result, out result2, out result3, out value);
        }

        private static bool DeleteProjectLicenseFile()
        {
            try
            {
                if (AssetDatabase.DeleteAsset(LicensePathProvider.GetProjectLicenseAssetPath()))
                {
                    AssetDatabase.Refresh((ImportAssetOptions)8);
                    return true;
                }
            }
            catch (Exception)
            {
            }
            try
            {
                if (File.Exists(LicensePathProvider.GetProjectLicenseAbsolutePath()))
                {
                    File.Delete(LicensePathProvider.GetProjectLicenseAbsolutePath());
                }
                string path = LicensePathProvider.GetProjectLicenseAbsolutePath() + ".meta";
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                AssetDatabase.Refresh((ImportAssetOptions)8);
                return !File.Exists(LicensePathProvider.GetProjectLicenseAbsolutePath());
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryGetExportableLicenseCache(out PsdReaderLicenseCacheDocument result, out long result2, out string result3)
        {
            result = null;
            result2 = 0L;
            result3 = string.Empty;
            lock (_syncRoot)
            {
                EnsureCacheLoaded();
                result = _cachedLicense;
                if (result == null || string.IsNullOrWhiteSpace(result.LookupId))
                {
                    result3 = "当前还没有可导出的授权缓存，请先使用订单号完成激活。";
                    return false;
                }
                if (!IsLicenseCacheValidForExport(result))
                {
                    result3 = "当前授权尚未处于有效状态，无法导出授权文件。";
                    return false;
                }
                if (!IsOrderActivatedCache(result))
                {
                    result3 = "当前设备通过授权文件激活，不具备导出授权文件权限。";
                    return false;
                }
                result2 = ParseUtcTicks(result.SupportUntilUtc);
                if (IsValidFutureTicks(result2))
                {
                    return true;
                }
                result2 = 0L;
                result3 = "当前授权暂不允许导出授权文件。";
                return false;
            }
        }

        private static bool TryCalculateProjectLicenseExpiry(int value, long value2, out DateTime result)
        {
            result = DateTime.MinValue;
            int num = ClampProjectLicenseDays(value);
            DateTime utcNow = DateTime.UtcNow;
            long ticks;
            try
            {
                ticks = utcNow.AddDays(num).Ticks;
            }
            catch
            {
                return false;
            }
            long ticks2 = utcNow.Ticks;
            long num2 = ticks - ticks2;
            long num3 = value2 - ticks2;
            if (num2 > 0L && num3 > 0L)
            {
                long num4 = (((ulong)num2 <= (ulong)num3) ? ticks : value2);
                if (num4 <= ticks2)
                {
                    return false;
                }
                result = new DateTime(num4, DateTimeKind.Utc);
                return true;
            }
            return false;
        }

        private static int ClampProjectLicenseDays(int value)
        {
            return Mathf.Clamp(Mathf.Max(1, value), 1, 36500);
        }

        private static bool IsValidFutureTicks(long value)
        {
            if (value > DateTime.UtcNow.Ticks)
            {
                return value <= DateTime.MaxValue.Ticks;
            }
            return false;
        }

        private static bool TryDecryptGrantSeed(object value, out byte[] result)
        {
            result = Array.Empty<byte>();
            if (value != null && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)value).GrantSeedCipher))
            {
                _deviceFingerprintProvider.GetDeviceFingerprintHash(out var text);
                if (!DeviceFingerprintsMatch(((PsdReaderLicenseCacheDocument)value).DeviceFingerprint, text))
                {
                    return false;
                }
                if (!LicenseCryptoUtility.TryDecryptDeviceBoundSecret(((PsdReaderLicenseCacheDocument)value).GrantSeedCipher, text, out result))
                {
                    return false;
                }
                return result.Length != 0;
            }
            return false;
        }

        private static bool MatchesCurrentDeviceFingerprint(object value)
        {
            _deviceFingerprintProvider.GetDeviceFingerprintHash(out var text);
            return DeviceFingerprintsMatch(value, text);
        }

        private static bool DeviceFingerprintsMatch(object value, object value2)
        {
            if (string.IsNullOrWhiteSpace((string)value) || string.IsNullOrWhiteSpace((string)value2))
            {
                return false;
            }
            return string.Equals(LicenseCryptoUtility.NormalizeHex(value), LicenseCryptoUtility.NormalizeHex(value2), StringComparison.Ordinal);
        }

        private static long ParseUtcTicks(object value)
        {
            DateTime dateTime = LicenseCryptoUtility.ParseUtcOrDefault(value, DateTime.MinValue);
            if (!(dateTime <= DateTime.MinValue))
            {
                return dateTime.Ticks;
            }
            return 0L;
        }

        private static PsdReaderLicenseClientConfigDocument BuildClientConfig(PsdReaderProductMetaPayloadDocument value = null, PsdReaderLicenseCacheDocument value2 = null, PsdReaderLicenseClientConfigDocument value3 = null)
        {
            PsdReaderLicenseClientConfigDocument psdReaderLicenseClientConfigDocument = value3 ?? _clientConfig ?? CreateDefaultClientConfig();
            if (psdReaderLicenseClientConfigDocument == null)
            {
                return null;
            }
            string text = ((!string.IsNullOrWhiteSpace(value?.TelemetryUrl)) ? value.TelemetryUrl : ((!string.IsNullOrWhiteSpace(value2?.TelemetryUrl)) ? value2.TelemetryUrl : (string.IsNullOrWhiteSpace(_cachedLicense?.TelemetryUrl) ? psdReaderLicenseClientConfigDocument.MatomoUrl : _cachedLicense.TelemetryUrl)));
            string text2 = ((!string.IsNullOrWhiteSpace(value?.TelemetrySiteId)) ? value.TelemetrySiteId : ((!string.IsNullOrWhiteSpace(value2?.TelemetrySiteId)) ? value2.TelemetrySiteId : (string.IsNullOrWhiteSpace(_cachedLicense?.TelemetrySiteId) ? psdReaderLicenseClientConfigDocument.MatomoSiteId : _cachedLicense.TelemetrySiteId)));
            PsdReaderLicenseClientConfigDocument psdReaderLicenseClientConfigDocument2 = new PsdReaderLicenseClientConfigDocument();
            psdReaderLicenseClientConfigDocument2.VendorCode = psdReaderLicenseClientConfigDocument.VendorCode;
            psdReaderLicenseClientConfigDocument2.ProductCode = psdReaderLicenseClientConfigDocument.ProductCode;
            psdReaderLicenseClientConfigDocument2.RepositoryBaseUrls = MergeRepositoryBaseUrls(value?.RepositoryBaseUrls, psdReaderLicenseClientConfigDocument.RepositoryBaseUrls);
            psdReaderLicenseClientConfigDocument2.MatomoUrl = text ?? string.Empty;
            psdReaderLicenseClientConfigDocument2.MatomoSiteId = text2 ?? string.Empty;
            psdReaderLicenseClientConfigDocument2.RequestTimeoutSeconds = psdReaderLicenseClientConfigDocument.RequestTimeoutSeconds;
            psdReaderLicenseClientConfigDocument2.CurrentMajorVersion = psdReaderLicenseClientConfigDocument.CurrentMajorVersion;
            return psdReaderLicenseClientConfigDocument2;
        }

        private static string[] MergeRepositoryBaseUrls(params string[][] repositoryBaseUrlSets)
        {
            List<string> list = new List<string>();
            if (repositoryBaseUrlSets != null)
            {
                foreach (string[] array in repositoryBaseUrlSets)
                {
                    if (array != null && array.Length != 0)
                    {
                        list.AddRange(array);
                    }
                }
            }
            return LicenseRepositoryClient.NormalizeRepositoryBaseUrls(list);
        }

        private static PsdReaderLicenseClientConfigDocument CreateDefaultClientConfig()
        {
            return LicenseClientDefaults.CreateDefaultConfig();
        }

        private static string GetRootPublicKeyPem()
        {
            return LicenseClientDefaults.GetEmbeddedPublicKeyPem();
        }

        private static void EnsureCacheLoaded()
        {
            if (_isCacheLoaded)
            {
                return;
            }
            _isCacheLoaded = true;
            _cachedLicense = _licenseCacheStore.Load();
            if (_cachedLicense == null)
            {
                if (_currentStatus == null)
                {
                    _currentStatus = CreateLicenseStatus((LicenseResultCode)4, (!HasProjectLicenseFile()) ? "尚未激活 Psd2UGUI 授权。" : "已检测到插件目录授权文件，可直接使用授权文件激活。");
                }
                return;
            }
            _cachedLicense.ActivationSource = ResolveActivationSource(_cachedLicense.ActivationSource);
            if (TryRestoreVerifiedContextFromCache(_cachedLicense) == null || GetCachedLicenseAccessKey().Length == 0)
            {
                _clientConfig = CreateDefaultClientConfig();
                _currentStatus = CreateLicenseStatus((LicenseResultCode)4, HasProjectLicenseFile() ? "已检测到插件目录授权文件，可直接使用授权文件激活。" : "当前授权需要重新输入订单号完成验证。");
            }
            else
            {
                _clientConfig = BuildClientConfig(null, _cachedLicense);
                _currentStatus = CreateLicenseStatus(_cachedLicense.LastResultCode, _cachedLicense.Message, _cachedLicense);
            }
        }

        private static bool TryFallbackToCachedLicense(LicenseResultCode value, object value2, out VerifiedLicenseContext result, out string result2)
        {
            result = null;
            result2 = string.Empty;
            if (!IsNetworkFailure(value, value2))
            {
                return false;
            }
            if (!IsLicenseCacheValid(_cachedLicense))
            {
                return false;
            }
            result = TryRestoreVerifiedContextFromCache(_cachedLicense);
            if (result == null)
            {
                return false;
            }
            result2 = "当前网络不可用，已继续使用最近一次本地授权缓存。";
            return true;
        }

        private static bool TryUseCachedLicenseWithoutRefresh(object value, out VerifiedLicenseContext result, out string result2)
        {
            result = null;
            result2 = string.Empty;
            if (_cachedLicense != null && IsLicenseCacheValid(_cachedLicense))
            {
                result = TryRestoreVerifiedContextFromCache(_cachedLicense);
                if (result == null)
                {
                    return false;
                }
                if (!HasRepositoryBaseUrls(value))
                {
                    result2 = "授权有效。";
                    return true;
                }
                if (!WasRepositoryRequestedRecently(_cachedLicense))
                {
                    result = null;
                    result2 = string.Empty;
                    return false;
                }
                result2 = "授权有效。";
                return true;
            }
            return false;
        }

        private static bool IsLicenseCacheValid(object value)
        {
            if (value == null)
            {
                return false;
            }
            if (((PsdReaderLicenseCacheDocument)value).LastResultCode != (LicenseResultCode)1)
            {
                return false;
            }
            if (!string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)value).LookupId) && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)value).LicenseId) && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)value).MetaEnvelopeBase64) && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)value).LicenseEnvelopeBase64) && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)value).OfflineLeaseEnvelopeBase64) && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)value).LicenseAccessKeyCipher) && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)value).ClockHighWaterUtc) && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)value).ClockWatermarkCipher) && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)value).ValidationTranscriptHead) && ((PsdReaderLicenseCacheDocument)value).StatusCode == 0 && ((PsdReaderLicenseCacheDocument)value).FeatureMask != 0 && MatchesCurrentDeviceFingerprint(((PsdReaderLicenseCacheDocument)value).DeviceFingerprint))
            {
                _deviceFingerprintProvider.GetDeviceFingerprintHash(out var text);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return false;
                }
                if (TryValidateClockWatermark(value, text, out var dateTime, out var dateTime2, out var _, out var _))
                {
                    DateTime utcNow = DateTime.UtcNow;
                    DateTime dateTime3 = LicenseCryptoUtility.ParseUtcOrDefault(((PsdReaderLicenseCacheDocument)value).LastVerifiedUtc, DateTime.MinValue);
                    if (!(dateTime3 <= DateTime.MinValue) && !(dateTime != dateTime3) && !(dateTime > utcNow) && !(dateTime2 < utcNow))
                    {
                        return utcNow <= dateTime.AddDays(Mathf.Max(1, ((PsdReaderLicenseCacheDocument)value).LocalCacheTimeoutDays));
                    }
                    return false;
                }
                return false;
            }
            return false;
        }

        private static bool HasRepositoryBaseUrls(object value)
        {
            if (((PsdReaderLicenseClientConfigDocument)value)?.RepositoryBaseUrls != null)
            {
                return ((PsdReaderLicenseClientConfigDocument)value).RepositoryBaseUrls.Length != 0;
            }
            return false;
        }

        private static bool WasRepositoryRequestedRecently(object value)
        {
            if (value != null && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)value).LastRepositoryRequestUtc))
            {
                DateTime dateTime = LicenseCryptoUtility.ParseUtcOrDefault(((PsdReaderLicenseCacheDocument)value).LastRepositoryRequestUtc, DateTime.MinValue);
                if (dateTime <= DateTime.MinValue)
                {
                    return false;
                }
                return DateTime.UtcNow <= dateTime.AddHours(1.0);
            }
            return false;
        }

        private static string CreateRepositoryRequestTimestamp(object value)
        {
            if (HasRepositoryBaseUrls(value))
            {
                return DateTime.UtcNow.ToString("O");
            }
            return string.Empty;
        }

        private static void SaveRepositoryRequestTimestamp(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && _cachedLicense != null)
            {
                _cachedLicense.LastRepositoryRequestUtc = (string)value;
                _licenseCacheStore.Save(_cachedLicense);
            }
        }

        private static string ResolveRepositoryRequestTimestamp(object value)
        {
            object obj;
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                obj = value;
                goto IL_001b;
            }
            PsdReaderLicenseCacheDocument value2 = _cachedLicense;
            if (value2 != null)
            {
                obj = value2.LastRepositoryRequestUtc;
                if (obj != null)
                {
                    goto IL_001b;
                }
            }
            else
            {
                obj = null;
            }
            return string.Empty;
            IL_001b:
            return (string)obj;
        }

        private static bool IsLicenseCacheValidForExport(object value)
        {
            return IsLicenseCacheValid(value);
        }

        private static bool TryValidateClockWatermark(object value, object value2, out DateTime result, out DateTime result2, out string result3, out string result4)
        {
            result = DateTime.MinValue;
            result2 = DateTime.MinValue;
            result3 = string.Empty;
            result4 = string.Empty;
            if (value == null || string.IsNullOrWhiteSpace((string)value2) || string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)value).ClockHighWaterUtc) || string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)value).ClockWatermarkCipher) || string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)value).ValidationTranscriptHead))
            {
                return false;
            }
            if (LicenseCryptoUtility.TryDecryptClockWatermark(((PsdReaderLicenseCacheDocument)value).ClockWatermarkCipher, value2, out var dateTime, out var dateTime2, out result3, out result4))
            {
                result = LicenseCryptoUtility.ParseUtcOrDefault(((PsdReaderLicenseCacheDocument)value).ClockHighWaterUtc, DateTime.MinValue);
                result2 = dateTime2;
                DateTime dateTime3 = LicenseCryptoUtility.ParseUtcOrDefault(((PsdReaderLicenseCacheDocument)value).SupportUntilUtc, DateTime.MinValue);
                if (!(result <= DateTime.MinValue) && !(result != dateTime) && !(dateTime3 <= DateTime.MinValue) && !(dateTime3 != result2) && !(result2 < result) && string.Equals(((PsdReaderLicenseCacheDocument)value).ValidationTranscriptHead, result4, StringComparison.Ordinal) && !(result != LicenseCryptoUtility.ParseUtcOrDefault(((PsdReaderLicenseCacheDocument)value).LastVerifiedUtc, DateTime.MinValue)))
                {
                    if (!string.Equals(LicenseCryptoUtility.ComputeLicenseCacheProof(value, value2, result, result2), result3, StringComparison.Ordinal))
                    {
                        return false;
                    }
                    return string.Equals(LicenseCryptoUtility.ComputeValidationTranscriptHead(value, value2, result, result2, result3), result4, StringComparison.Ordinal);
                }
                return false;
            }
            return false;
        }

        private static bool IsNetworkFailure(LicenseResultCode value, object value2)
        {
            if (value != (LicenseResultCode)5)
            {
                return false;
            }
            string text = ((string)(value2 ?? string.Empty)).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }
            string[] array = new string[13]
            {
                "timeout", "timed out", "request timeout", "cannot resolve", "cannot connect", "connection", "network", "unreachable", "refused", "proxy",
                "ssl", "tls", "transport"
            };
            int num = 0;
            while (true)
            {
                if (num < array.Length)
                {
                    if (text.IndexOf(array[num], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        break;
                    }
                    num++;
                    continue;
                }
                return true;
            }
            return true;
        }

        private static LicenseResultCode GetValidationFailureCode(object verifiedContext, object featureName)
        {
            if (verifiedContext == null || ((VerifiedLicenseContext)verifiedContext).GetLicensePayload() == null)
            {
                return (LicenseResultCode)4;
            }
            if (((VerifiedLicenseContext)verifiedContext).GetLicensePayload().StatusCode == 1)
            {
                return (LicenseResultCode)10;
            }
            if (((VerifiedLicenseContext)verifiedContext).GetLicensePayload().StatusCode == 2)
            {
                return (LicenseResultCode)11;
            }
            if (!LicenseFeatureCatalog.ContainsFeature(
                ((VerifiedLicenseContext)verifiedContext).GetLicensePayload().FeatureMask,
                featureName))
            {
                return (LicenseResultCode)13;
            }
            if (GetLicenseSupportUntilUtc(((VerifiedLicenseContext)verifiedContext).GetLicensePayload()) < DateTime.UtcNow)
            {
                return (LicenseResultCode)12;
            }
            return (LicenseResultCode)8;
        }

        private static LicenseResultCode EvaluateLicenseForFeature(object verifiedContext, object featureName)
        {
            if (verifiedContext != null && ((VerifiedLicenseContext)verifiedContext).GetLicensePayload() != null)
            {
                if (((VerifiedLicenseContext)verifiedContext).GetLicensePayload().StatusCode == 1)
                {
                    return (LicenseResultCode)10;
                }
                if (((VerifiedLicenseContext)verifiedContext).GetLicensePayload().StatusCode == 2)
                {
                    return (LicenseResultCode)11;
                }
                if (LicenseFeatureCatalog.ContainsFeature(
                    ((VerifiedLicenseContext)verifiedContext).GetLicensePayload().FeatureMask,
                    featureName))
                {
                    if (!(GetLicenseSupportUntilUtc(((VerifiedLicenseContext)verifiedContext).GetLicensePayload()) < DateTime.UtcNow))
                    {
                        return (LicenseResultCode)1;
                    }
                    return (LicenseResultCode)12;
                }
                return (LicenseResultCode)13;
            }
            return (LicenseResultCode)4;
        }

        private static string BuildLicenseFailureMessage(object verifiedContext, object featureName)
        {
            if (verifiedContext == null || ((VerifiedLicenseContext)verifiedContext).GetLicensePayload() == null)
            {
                return "授权缓存不可用。";
            }
            if (((VerifiedLicenseContext)verifiedContext).GetLicensePayload().StatusCode == 1)
            {
                return "当前订单授权已被暂停。";
            }
            if (((VerifiedLicenseContext)verifiedContext).GetLicensePayload().StatusCode == 2)
            {
                return "当前订单授权已被封禁。";
            }
            if (!LicenseFeatureCatalog.ContainsFeature(
                ((VerifiedLicenseContext)verifiedContext).GetLicensePayload().FeatureMask,
                featureName))
            {
                return "当前授权不包含功能: " + (string)featureName;
            }
            if (GetLicenseSupportUntilUtc(((VerifiedLicenseContext)verifiedContext).GetLicensePayload()) < DateTime.UtcNow)
            {
                return "当前授权已过支持期。";
            }
            return "Psd2UGUI 授权状态折叠校验失败。";
        }

        private static DateTime GetLicenseSupportUntilUtc(object licensePayload)
        {
            if (licensePayload == null)
            {
                return DateTime.MinValue;
            }

            if (((PsdReaderLicensePayloadDocument)licensePayload).SupportUntilUtcTicks > 0L)
            {
                return new DateTime(
                    ((PsdReaderLicensePayloadDocument)licensePayload).SupportUntilUtcTicks,
                    DateTimeKind.Utc);
            }

            return LicenseCryptoUtility.ParseUtcOrDefault(
                ((PsdReaderLicensePayloadDocument)licensePayload).SupportUntilUtc,
                DateTime.MinValue);
        }

        /// <summary>
        /// 将本次授权操作结果与可选的缓存文档组装为授权状态对象。
        /// </summary>
        private static PsdReaderLicenseStatus CreateLicenseStatus(
            LicenseResultCode resultCode,
            object message,
            PsdReaderLicenseCacheDocument cacheDocument = null)
        {
            string statusText = string.Empty;
            string[] features = Array.Empty<string>();
            if (cacheDocument != null)
            {
                statusText = !string.IsNullOrWhiteSpace(cacheDocument.StatusText)
                    ? cacheDocument.StatusText
                    : LicensePayloadBinaryReader.GetStatusText(cacheDocument.StatusCode);
                features = cacheDocument.Features == null || cacheDocument.Features.Length == 0
                    ? LicenseFeatureCatalog.DecodeFeatureMask(cacheDocument.FeatureMask)
                    : cacheDocument.Features;
            }

            PsdReaderLicenseStatus licenseStatus = new PsdReaderLicenseStatus();
            licenseStatus.SetResultCode(resultCode);
            licenseStatus.Message = (string)(message ?? string.Empty);
            licenseStatus.SetLicenseId(cacheDocument?.LicenseId ?? string.Empty);
            licenseStatus.SetLookupId(cacheDocument?.LookupId ?? string.Empty);
            licenseStatus.SetStatusText(statusText);
            licenseStatus.SetFeatures(features);
            licenseStatus.SetLastVerifiedUtc(LicenseCryptoUtility.ParseUtcOrDefault(cacheDocument?.LastVerifiedUtc, DateTime.MinValue));
            licenseStatus.SetSupportUntilUtc(LicenseCryptoUtility.ParseUtcOrDefault(cacheDocument?.SupportUntilUtc, DateTime.MinValue));
            licenseStatus.SetRevision(cacheDocument?.Revision ?? 0);
            return licenseStatus;
        }

        /// <summary>
        /// 复制授权状态，并单独复制功能列表，避免调用方修改内部缓存状态。
        /// </summary>
        private static PsdReaderLicenseStatus CloneLicenseStatus(object sourceStatus)
        {
            if (sourceStatus == null)
            {
                return new PsdReaderLicenseStatus();
            }

            PsdReaderLicenseStatus clonedStatus = new PsdReaderLicenseStatus();
            PsdReaderLicenseStatus typedSourceStatus = (PsdReaderLicenseStatus)sourceStatus;
            clonedStatus.SetResultCode(typedSourceStatus.GetResultCode());
            clonedStatus.Message = typedSourceStatus.Message;
            clonedStatus.SetLicenseId(typedSourceStatus.GetLicenseId());
            clonedStatus.SetLookupId(typedSourceStatus.GetLookupId());
            clonedStatus.SetStatusText(typedSourceStatus.GetStatusText());
            string[] sourceFeatures = typedSourceStatus.GetFeatures();
            clonedStatus.SetFeatures(sourceFeatures?.ToArray() ?? Array.Empty<string>());
            clonedStatus.SetLastVerifiedUtc(typedSourceStatus.GetLastVerifiedUtc());
            clonedStatus.SetSupportUntilUtc(typedSourceStatus.GetSupportUntilUtc());
            clonedStatus.SetRevision(typedSourceStatus.GetRevision());
            return clonedStatus;
        }

        private static string GetProductMetadataPath()
        {
            return "products/psd2ugui/state.data";
        }

        private static string GetProductMetadataScope()
        {
            return "state:psd2ugui";
        }

        private static string GetLicensePayloadPath(object value)
        {
            string text = LicenseCryptoUtility.NormalizeHex(value);
            string text2 = ((text.Length < 2) ? "00" : text.Substring(0, 2));
            string text3 = ((text.Length >= 4) ? text.Substring(2, 2) : "00");
            return "products/psd2ugui/licenses/" + text2 + "/" + text3 + "/" + text + ".data";
        }

        private static string GetLicensePayloadScope(object value)
        {
            return "license:psd2ugui:" + LicenseCryptoUtility.NormalizeHex(value);
        }

        private static string GetOfflineLeasePath(object value)
        {
            string text = LicenseCryptoUtility.NormalizeHex(value);
            string text2 = ((text.Length < 2) ? "00" : text.Substring(0, 2));
            string text3 = ((text.Length >= 4) ? text.Substring(2, 2) : "00");
            return "products/psd2ugui/offline-leases/" + text2 + "/" + text3 + "/" + text + ".data";
        }

        private static string GetOfflineLeaseScope(object value)
        {
            return "offline-lease:psd2ugui:" + LicenseCryptoUtility.NormalizeHex(value);
        }

        private static string GetDeviceShardPath(object value)
        {
            return "products/psd2ugui/devices/" + GetDeviceShardPrefix(value) + ".data";
        }

        private static string GetDeviceShardScope(object value)
        {
            return "device-shard:psd2ugui:" + GetDeviceShardPrefix(value);
        }

        private static string GetDeviceShardPrefix(object value)
        {
            string text = LicenseCryptoUtility.NormalizeHex(value);
            if (text.Length < 2)
            {
                return "00";
            }
            return text.Substring(0, 2);
        }

        private static bool ValidateProductMetadata(object value, out string result, out LicenseResultCode result2)
        {
            result = string.Empty;
            result2 = (LicenseResultCode)1;
            if (value == null)
            {
                result = "产品元数据为空。";
                result2 = (LicenseResultCode)6;
                return false;
            }
            if (((PsdReaderProductMetaPayloadDocument)value).Schema < 1 || ((PsdReaderProductMetaPayloadDocument)value).Revision <= 0)
            {
                result = "产品元数据版本无效。";
                result2 = (LicenseResultCode)6;
                return false;
            }
            if (string.Equals(((PsdReaderProductMetaPayloadDocument)value).ProductCode, "psd2ugui", StringComparison.Ordinal) && string.Equals(((PsdReaderProductMetaPayloadDocument)value).VendorCode, "efunstudio", StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(((PsdReaderProductMetaPayloadDocument)value).ActiveKid) && !string.IsNullOrWhiteSpace(((PsdReaderProductMetaPayloadDocument)value).ActiveLeafPublicKeyPem))
                {
                    if (((PsdReaderProductMetaPayloadDocument)value).FeatureCatalogMask != 0 && ((PsdReaderProductMetaPayloadDocument)value).LocalCacheTimeoutDays > 0 && ((PsdReaderProductMetaPayloadDocument)value).CurrentMajorVersion > 0 && ((PsdReaderProductMetaPayloadDocument)value).RepositoryBaseUrls != null && ((PsdReaderProductMetaPayloadDocument)value).RepositoryBaseUrls.Length != 0 && ((PsdReaderProductMetaPayloadDocument)value).StateSalt != null && ((PsdReaderProductMetaPayloadDocument)value).StateSalt.Length != 0)
                    {
                        return true;
                    }
                    result = "产品元数据内容无效。";
                    result2 = (LicenseResultCode)6;
                    return false;
                }
                result = "产品元数据签名信息无效。";
                result2 = (LicenseResultCode)6;
                return false;
            }
            result = "产品元数据与当前插件不匹配。";
            result2 = (LicenseResultCode)9;
            return false;
        }

        private static bool ValidateLicensePayload(object value, object value2, object value3, out string result, out LicenseResultCode result2)
        {
            result = string.Empty;
            result2 = (LicenseResultCode)1;
            if (value != null)
            {
                if (((PsdReaderLicensePayloadDocument)value).Schema >= 1 && ((PsdReaderLicensePayloadDocument)value).Revision > 0 && !string.IsNullOrWhiteSpace(((PsdReaderLicensePayloadDocument)value).LicenseId) && ((PsdReaderLicensePayloadDocument)value).MaxMajorVersion > 0 && ((PsdReaderLicensePayloadDocument)value).GrantSeed != null && ((PsdReaderLicensePayloadDocument)value).GrantSeed.Length != 0)
                {
                    if (!string.Equals(LicenseCryptoUtility.NormalizeHex(((PsdReaderLicensePayloadDocument)value).LookupId), LicenseCryptoUtility.NormalizeHex(value3), StringComparison.Ordinal))
                    {
                        result = "授权文件索引不匹配。";
                        result2 = (LicenseResultCode)8;
                        return false;
                    }
                    if (string.Equals(((PsdReaderLicensePayloadDocument)value).ProductCode, "psd2ugui", StringComparison.Ordinal) && string.Equals(((PsdReaderLicensePayloadDocument)value).VendorCode, "efunstudio", StringComparison.Ordinal))
                    {
                        if (value2 == null)
                        {
                            result = "产品元数据为空。";
                            result2 = (LicenseResultCode)6;
                            return false;
                        }
                        if ((((PsdReaderLicensePayloadDocument)value).FeatureMask & ~((PsdReaderProductMetaPayloadDocument)value2).FeatureCatalogMask) != 0)
                        {
                            result = "授权文件包含未声明功能。";
                            result2 = (LicenseResultCode)9;
                            return false;
                        }
                        ((PsdReaderLicensePayloadDocument)value).Features = LicenseFeatureCatalog.DecodeFeatureMask(((PsdReaderLicensePayloadDocument)value).FeatureMask);
                        ((PsdReaderLicensePayloadDocument)value).Status = LicensePayloadBinaryReader.GetStatusText(((PsdReaderLicensePayloadDocument)value).StatusCode);
                        if (((PsdReaderLicensePayloadDocument)value).StatusCode > 2)
                        {
                            result = "授权状态码无效。";
                            result2 = (LicenseResultCode)8;
                            return false;
                        }
                        return true;
                    }
                    result = "授权文件产品信息不匹配。";
                    result2 = (LicenseResultCode)9;
                    return false;
                }
                result = "授权文件内容无效。";
                result2 = (LicenseResultCode)8;
                return false;
            }
            result = "授权文件为空。";
            result2 = (LicenseResultCode)8;
            return false;
        }

        private static bool ValidateDeviceShard(object value, object value2, out string result, out LicenseResultCode result2)
        {
            result = string.Empty;
            result2 = (LicenseResultCode)1;
            if (value != null)
            {
                if (((PsdReaderDeviceShardPayloadDocument)value).Schema >= 1 && string.Equals(((PsdReaderDeviceShardPayloadDocument)value).Kind, "DeviceShard", StringComparison.Ordinal) && string.Equals(((PsdReaderDeviceShardPayloadDocument)value).ProductCode, "psd2ugui", StringComparison.Ordinal) && string.Equals(GetDeviceShardPrefix(((PsdReaderDeviceShardPayloadDocument)value).Prefix), GetDeviceShardPrefix(value2), StringComparison.Ordinal))
                {
                    return true;
                }
                result = "设备分片内容无效。";
                result2 = (LicenseResultCode)8;
                return false;
            }
            result = "设备分片为空。";
            result2 = (LicenseResultCode)8;
            return false;
        }

        private static bool ValidateOfflineLease(object value, object value2, object value3, object value4, out string result, out LicenseResultCode result2)
        {
            result = string.Empty;
            result2 = (LicenseResultCode)1;
            if (value != null)
            {
                if (((PsdReaderOfflineLeasePayloadDocument)value).Schema == 1 && string.Equals(((PsdReaderOfflineLeasePayloadDocument)value).Kind, "OfflineLease", StringComparison.Ordinal) && ((PsdReaderOfflineLeasePayloadDocument)value).Revision > 0 && ((PsdReaderOfflineLeasePayloadDocument)value).OfflineCacheTimeoutDays > 0)
                {
                    if (value2 != null && value3 != null)
                    {
                        if (!string.Equals(LicenseCryptoUtility.NormalizeHex(((PsdReaderOfflineLeasePayloadDocument)value).LookupId), LicenseCryptoUtility.NormalizeHex(value4), StringComparison.Ordinal))
                        {
                            result = "离线授权租约索引不匹配。";
                            result2 = (LicenseResultCode)8;
                            return false;
                        }
                        if (string.Equals(((PsdReaderOfflineLeasePayloadDocument)value).ProductCode, "psd2ugui", StringComparison.Ordinal) && string.Equals(((PsdReaderOfflineLeasePayloadDocument)value).VendorCode, "efunstudio", StringComparison.Ordinal))
                        {
                            if ((((PsdReaderOfflineLeasePayloadDocument)value).FeatureMask & ~((PsdReaderProductMetaPayloadDocument)value3).FeatureCatalogMask) != 0)
                            {
                                result = "离线授权租约包含未声明功能。";
                                result2 = (LicenseResultCode)9;
                                return false;
                            }
                            ((PsdReaderOfflineLeasePayloadDocument)value).Features = LicenseFeatureCatalog.DecodeFeatureMask(((PsdReaderOfflineLeasePayloadDocument)value).FeatureMask);
                            ((PsdReaderOfflineLeasePayloadDocument)value).Status = LicensePayloadBinaryReader.GetStatusText(((PsdReaderOfflineLeasePayloadDocument)value).StatusCode);
                            ((PsdReaderOfflineLeasePayloadDocument)value).SupportUntilUtc = ((((PsdReaderOfflineLeasePayloadDocument)value).SupportUntilUtcTicks > 0L) ? new DateTime(((PsdReaderOfflineLeasePayloadDocument)value).SupportUntilUtcTicks, DateTimeKind.Utc).ToString("O") : string.Empty);
                            ((PsdReaderOfflineLeasePayloadDocument)value).IssuedAtUtc = ((((PsdReaderOfflineLeasePayloadDocument)value).IssuedUtcTicks <= 0L) ? string.Empty : new DateTime(((PsdReaderOfflineLeasePayloadDocument)value).IssuedUtcTicks, DateTimeKind.Utc).ToString("O"));
                            if (((PsdReaderOfflineLeasePayloadDocument)value).StatusCode <= 2)
                            {
                                if (string.Equals(LicenseCryptoUtility.NormalizeHex(((PsdReaderLicensePayloadDocument)value2).LookupId), LicenseCryptoUtility.NormalizeHex(((PsdReaderOfflineLeasePayloadDocument)value).LookupId), StringComparison.Ordinal) && string.Equals(((PsdReaderLicensePayloadDocument)value2).LicenseId, ((PsdReaderOfflineLeasePayloadDocument)value).LicenseId, StringComparison.Ordinal) && string.Equals(((PsdReaderLicensePayloadDocument)value2).VendorCode, ((PsdReaderOfflineLeasePayloadDocument)value).VendorCode, StringComparison.Ordinal) && string.Equals(((PsdReaderLicensePayloadDocument)value2).ProductCode, ((PsdReaderOfflineLeasePayloadDocument)value).ProductCode, StringComparison.Ordinal) && ((PsdReaderLicensePayloadDocument)value2).Revision == ((PsdReaderOfflineLeasePayloadDocument)value).Revision && ((PsdReaderLicensePayloadDocument)value2).StatusCode == ((PsdReaderOfflineLeasePayloadDocument)value).StatusCode && ((PsdReaderLicensePayloadDocument)value2).FeatureMask == ((PsdReaderOfflineLeasePayloadDocument)value).FeatureMask && ((PsdReaderLicensePayloadDocument)value2).SupportUntilUtcTicks == ((PsdReaderOfflineLeasePayloadDocument)value).SupportUntilUtcTicks)
                                {
                                    return true;
                                }
                                result = "离线授权租约与授权文件不一致。";
                                result2 = (LicenseResultCode)8;
                                return false;
                            }
                            result = "离线授权租约状态码无效。";
                            result2 = (LicenseResultCode)8;
                            return false;
                        }
                        result = "离线授权租约产品信息不匹配。";
                        result2 = (LicenseResultCode)9;
                        return false;
                    }
                    result = "离线授权租约依赖数据无效。";
                    result2 = (LicenseResultCode)6;
                    return false;
                }
                result = "离线授权租约内容无效。";
                result2 = (LicenseResultCode)8;
                return false;
            }
            result = "离线授权租约为空。";
            result2 = (LicenseResultCode)8;
            return false;
        }

        private static bool FetchAndDecodeRootEnvelope<TValue>(object value, object value2, object value3, object value4, object value5, out TValue result, out byte[] result2, out bool result3, out EnvelopeFetchFailure result4) where TValue : class
        {
            result = null;
            result2 = Array.Empty<byte>();
            result3 = false;
            result4 = (EnvelopeFetchFailure)0;
            if (!DownloadSignedEnvelope(value, value3, value4, value5, out var psdReaderSignedEnvelopeDocument, out result2, out result3, out result4))
            {
                return false;
            }
            if (!LicenseCryptoUtility.TryVerifyAndDecryptEnvelope(value2, psdReaderSignedEnvelopeDocument, value4, value5, out var array))
            {
                result4 = (EnvelopeFetchFailure)3;
                return false;
            }
            if (!DeserializePayload<TValue>(array, out result))
            {
                result4 = (EnvelopeFetchFailure)4;
                return false;
            }
            return true;
        }

        private static bool FetchAndDecodeKeyedEnvelope<TValue>(object value, object value2, object value3, object value4, object value5, object value6, out TValue result, out byte[] result2, out bool result3, out EnvelopeFetchFailure result4, bool enabled = false) where TValue : class
        {
            result = null;
            result2 = Array.Empty<byte>();
            result3 = false;
            result4 = (EnvelopeFetchFailure)0;
            if (!DownloadSignedEnvelope(value, value4, value5, value6, out var psdReaderSignedEnvelopeDocument, out result2, out result3, out result4, enabled))
            {
                return false;
            }
            if (!string.IsNullOrWhiteSpace((string)value3) && !string.Equals((string)value3, psdReaderSignedEnvelopeDocument?.Kid, StringComparison.Ordinal))
            {
                result4 = (EnvelopeFetchFailure)2;
                return false;
            }
            if (!LicenseCryptoUtility.TryVerifyAndDecryptEnvelope(value2, psdReaderSignedEnvelopeDocument, value5, value6, out var array))
            {
                result4 = (EnvelopeFetchFailure)3;
                return false;
            }
            if (!DeserializePayload<TValue>(array, out result))
            {
                result4 = (EnvelopeFetchFailure)4;
                return false;
            }
            return true;
        }

        private static bool DownloadSignedEnvelope(object value, object value2, object value3, object value4, out PsdReaderSignedEnvelopeDocument result, out byte[] result2, out bool result3, out EnvelopeFetchFailure result4, bool enabled = false)
        {
            result = null;
            result2 = Array.Empty<byte>();
            result3 = false;
            result4 = (EnvelopeFetchFailure)0;
            if (_repositoryClient.TryDownloadWithFailover(((PsdReaderLicenseClientConfigDocument)value).RepositoryBaseUrls, (string)value2, ((PsdReaderLicenseClientConfigDocument)value).RequestTimeoutSeconds, out var array, out result3, out var _, enabled))
            {
                if (LicenseCryptoUtility.TryReadEncryptedSignedEnvelope(array, value4, value3, out result) && result != null)
                {
                    result2 = array;
                    return true;
                }
                result4 = (EnvelopeFetchFailure)5;
                return false;
            }
            result4 = (EnvelopeFetchFailure)1;
            return false;
        }

        private static bool DeserializePayload<TValue>(object value, out TValue result) where TValue : class
        {
            result = null;
            if (value != null && ((Array)value).Length != 0)
            {
                result = (LicensePayloadBinaryReader.TryDeserializePayload<TValue>(value, out TValue val) ? val : null);
                return result != null;
            }
            return false;
        }

        private static LicenseResultCode MapEnvelopeFetchFailureToResultCode(EnvelopeFetchFailure value)
        {
            return value switch
            {
                (EnvelopeFetchFailure)1 => (LicenseResultCode)5, 
                (EnvelopeFetchFailure)2 => (LicenseResultCode)8, 
                (EnvelopeFetchFailure)3 => (LicenseResultCode)8, 
                (EnvelopeFetchFailure)4 => (LicenseResultCode)8, 
                (EnvelopeFetchFailure)5 => (LicenseResultCode)8, 
                _ => (LicenseResultCode)8, 
            };
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderLicenseService GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
