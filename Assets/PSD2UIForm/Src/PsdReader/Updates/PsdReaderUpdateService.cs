using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using LicenseRepositoryClientNamespace;
using cn.efunstudio.psdreader;
using PsdReaderUpdateCacheNamespace;
using PsdReaderCurrentVersionInfoNamespace;
using LicensePayloadBinaryReaderNamespace;
using LicenseClientDefaultsNamespace;
using LicenseCryptoUtilityNamespace;

namespace PsdReaderUpdateServiceNamespace
{
    internal sealed class PsdReaderUpdateService
    {
        private static readonly LicenseRepositoryClient _repositoryClient = new LicenseRepositoryClient();

        private static bool _isDailyCheckInProgress;

        private static PsdReaderUpdateService s_ObfuscationSentinel;

        internal static PsdReaderProductUpdateInfo CheckForUpdates()
        {
            PsdReaderProductUpdateInfo psdReaderProductUpdateInfo = CreateDefaultUpdateInfo();
            try
            {
                PsdReaderLicenseClientConfigDocument psdReaderLicenseClientConfigDocument = LicenseClientDefaults.CreateDefaultConfig();
                if (psdReaderLicenseClientConfigDocument != null && psdReaderLicenseClientConfigDocument.RepositoryBaseUrls != null && psdReaderLicenseClientConfigDocument.RepositoryBaseUrls.Length != 0)
                {
                    string text = GetUpdateDocumentPurpose();
                    if (TryDownloadVerifiedPayload<PsdReaderProductUpdatePayloadDocument>(psdReaderLicenseClientConfigDocument, LicenseClientDefaults.GetEmbeddedPublicKeyPem(), GetUpdateMetadataPath(), text, LicenseCryptoUtility.DeriveRepositoryDocumentKey(text), out var psdReaderProductUpdatePayloadDocument, out var flag, out var text2))
                    {
                        if (ValidateUpdatePayload(psdReaderProductUpdatePayloadDocument, out var status, out var message))
                        {
                            Version version = ParseVersionOrDefault(psdReaderProductUpdatePayloadDocument.Version, PsdReaderCurrentVersionInfo.GetCurrentVersion());
                            psdReaderProductUpdateInfo.LatestVersion = psdReaderProductUpdatePayloadDocument.Version;
                            psdReaderProductUpdateInfo.DownloadUrl = NormalizeDownloadUrl(psdReaderProductUpdatePayloadDocument.DownloadUrl);
                            psdReaderProductUpdateInfo.ReleaseNotes = NormalizeReleaseNotes(psdReaderProductUpdatePayloadDocument.ReleaseNotes);
                            psdReaderProductUpdateInfo.PublishedAtUtc = psdReaderProductUpdatePayloadDocument.PublishedAtUtc ?? string.Empty;
                            psdReaderProductUpdateInfo.HasUpdate = version.CompareTo(PsdReaderCurrentVersionInfo.GetCurrentVersion()) > 0;
                            psdReaderProductUpdateInfo.Status = ((!psdReaderProductUpdateInfo.HasUpdate) ? PsdReaderProductUpdateStatus.UpToDate : PsdReaderProductUpdateStatus.UpdateAvailable);
                            psdReaderProductUpdateInfo.Message = (psdReaderProductUpdateInfo.HasUpdate ? "检测到新版本。" : "当前无可用更新。");
                            return psdReaderProductUpdateInfo;
                        }
                        psdReaderProductUpdateInfo.Status = status;
                        psdReaderProductUpdateInfo.Message = message;
                        return psdReaderProductUpdateInfo;
                    }
                    psdReaderProductUpdateInfo.Status = ((!flag) ? PsdReaderProductUpdateStatus.NetworkError : PsdReaderProductUpdateStatus.UpdateInfoMissing);
                    psdReaderProductUpdateInfo.Message = ((!flag) ? ("检查更新失败：" + ((!string.IsNullOrWhiteSpace(text2)) ? text2 : "网络请求失败。")) : "当前还没有发布更新元数据。");
                    return psdReaderProductUpdateInfo;
                }
                psdReaderProductUpdateInfo.Status = PsdReaderProductUpdateStatus.NotConfigured;
                psdReaderProductUpdateInfo.Message = "当前未配置可用的更新检查仓库地址。";
                return psdReaderProductUpdateInfo;
            }
            catch (Exception ex)
            {
                psdReaderProductUpdateInfo.Status = PsdReaderProductUpdateStatus.UnknownError;
                psdReaderProductUpdateInfo.Message = "检查更新失败：" + ex.Message;
                return psdReaderProductUpdateInfo;
            }
        }

        internal static void CheckForUpdatesAndPrompt()
        {
            DateTime utcNow = DateTime.UtcNow;
            EditorUtility.DisplayProgressBar("Psd2UGUI 更新检查", "正在联网检查更新...", 0.5f);
            PsdReaderProductUpdateInfo psdReaderProductUpdateInfo;
            try
            {
                psdReaderProductUpdateInfo = CheckForUpdates();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            UpdateCacheFromResult(psdReaderProductUpdateInfo, utcNow);
            if (psdReaderProductUpdateInfo == null)
            {
                EditorUtility.DisplayDialog("Psd2UGUI 更新检查", "当前无可用更新。", "确定");
            }
            else if (psdReaderProductUpdateInfo.Status == PsdReaderProductUpdateStatus.UpdateAvailable)
            {
                string text = "发现新版本。" + Environment.NewLine + "当前版本：" + psdReaderProductUpdateInfo.CurrentVersion + Environment.NewLine + "最新版本：" + psdReaderProductUpdateInfo.LatestVersion + Environment.NewLine + Environment.NewLine + "更新说明：" + Environment.NewLine + psdReaderProductUpdateInfo.ReleaseNotes;
                if (EditorUtility.DisplayDialogComplex("Psd2UGUI 更新检查", text, "前往下载", "取消", string.Empty) == 0)
                {
                    Application.OpenURL(psdReaderProductUpdateInfo.DownloadUrl);
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Psd2UGUI 更新检查", "当前无可用更新。", "确定");
            }
        }

        internal static void CheckForUpdatesDaily()
        {
            if (Application.isBatchMode || _isDailyCheckInProgress)
            {
                return;
            }
            DateTime utcNow = DateTime.UtcNow;
            PsdReaderUpdateCache value = LoadUpdateCache();
            if (!ShouldRunDailyCheck(value, utcNow))
            {
                return;
            }
            value.LastCheckUtcTicks = utcNow.Ticks;
            SaveUpdateCache(value);
            _isDailyCheckInProgress = true;
            try
            {
                UpdateCacheFromResult(CheckForUpdates(), utcNow);
            }
            catch (Exception)
            {
            }
            finally
            {
                _isDailyCheckInProgress = false;
            }
        }

        internal static bool HasCachedUpdate()
        {
            PsdReaderProductUpdateInfo psdReaderProductUpdateInfo;
            return TryGetCachedUpdate(out psdReaderProductUpdateInfo);
        }

        internal static string GetCachedUpdateBadge()
        {
            if (!TryGetCachedUpdate(out var psdReaderProductUpdateInfo))
            {
                return string.Empty;
            }
            return "检测到新版本 v" + psdReaderProductUpdateInfo.LatestVersion;
        }

        internal static bool OpenCachedUpdateDownload()
        {
            if (TryGetCachedUpdate(out var psdReaderProductUpdateInfo))
            {
                Application.OpenURL(NormalizeDownloadUrl(psdReaderProductUpdateInfo.DownloadUrl));
                return true;
            }
            return false;
        }

        private static PsdReaderProductUpdateInfo CreateDefaultUpdateInfo()
        {
            return new PsdReaderProductUpdateInfo
            {
                Status = PsdReaderProductUpdateStatus.UnknownError,
                CurrentVersion = "3.0.0",
                LatestVersion = "3.0.0",
                DownloadUrl = "https://assets.efunstudio.cn",
                ReleaseNotes = string.Empty,
                PublishedAtUtc = string.Empty,
                Message = string.Empty
            };
        }

        private static bool ValidateUpdatePayload(object value, out PsdReaderProductUpdateStatus result, out string result2)
        {
            result = PsdReaderProductUpdateStatus.InvalidData;
            result2 = "更新元数据无效。";
            if (value != null && ((PsdReaderProductUpdatePayloadDocument)value).Schema >= 1 && ((PsdReaderProductUpdatePayloadDocument)value).Revision > 0)
            {
                if (!string.Equals(((PsdReaderProductUpdatePayloadDocument)value).VendorCode, "efunstudio", StringComparison.Ordinal) || !string.Equals(((PsdReaderProductUpdatePayloadDocument)value).ProductCode, "psd2ugui", StringComparison.Ordinal))
                {
                    result = PsdReaderProductUpdateStatus.ProductMismatch;
                    result2 = "更新元数据与当前插件不匹配。";
                    return false;
                }
                if (TryNormalizeVersion(((PsdReaderProductUpdatePayloadDocument)value).Version, out var version, out var _))
                {
                    ((PsdReaderProductUpdatePayloadDocument)value).Version = version;
                    return true;
                }
                result2 = "更新元数据缺少合法版本号。";
                return false;
            }
            return false;
        }

        private static bool TryDownloadVerifiedPayload<TValue>(object value, object value2, object value3, object value4, object value5, out TValue result, out bool result2, out string result3) where TValue : class
        {
            result = null;
            result2 = false;
            result3 = string.Empty;
            if (!TryDownloadSignedEnvelope(value, value3, value4, value5, out var psdReaderSignedEnvelopeDocument, out result2, out result3))
            {
                return false;
            }
            if (!LicenseCryptoUtility.TryVerifyAndDecryptEnvelope(value2, psdReaderSignedEnvelopeDocument, value4, value5, out var array))
            {
                result3 = "更新元数据签名校验失败。";
                return false;
            }
            if (!LicensePayloadBinaryReader.TryDeserializePayload<TValue>(array, out result))
            {
                result3 = "更新元数据反序列化失败。";
                return false;
            }
            return true;
        }

        private static bool TryDownloadSignedEnvelope(object value, object value2, object value3, object value4, out PsdReaderSignedEnvelopeDocument result, out bool result2, out string result3)
        {
            result = null;
            result2 = false;
            result3 = string.Empty;
            if (!_repositoryClient.TryDownloadWithFailover(((PsdReaderLicenseClientConfigDocument)value).RepositoryBaseUrls, (string)value2, ((PsdReaderLicenseClientConfigDocument)value).RequestTimeoutSeconds, out var array, out result2, out result3, true))
            {
                return false;
            }
            if (LicenseCryptoUtility.TryReadEncryptedSignedEnvelope(array, value4, value3, out result) && result != null)
            {
                return true;
            }
            result3 = "更新元数据信封解析失败。";
            return false;
        }

        private static string GetUpdateMetadataPath()
        {
            return "products/psd2ugui/update.data";
        }

        private static string GetUpdateDocumentPurpose()
        {
            return "update:psd2ugui";
        }

        private static bool TryNormalizeVersion(object value, out string result, out Version result2)
        {
            result = string.Empty;
            result2 = null;
            object obj;
            if (value != null)
            {
                obj = ((string)value).Trim();
                if (obj != null)
                {
                    goto IL_001f;
                }
            }
            else
            {
                obj = null;
            }
            obj = string.Empty;
            goto IL_001f;
            IL_001f:
            string text = (string)obj;
            if (!string.IsNullOrWhiteSpace(text))
            {
                string[] array = text.Split('.', StringSplitOptions.None);
                if (array.Length >= 2 && array.Length <= 4)
                {
                    if (Version.TryParse(text, out result2) && !(result2 == null))
                    {
                        result = result2.ToString(array.Length);
                        return true;
                    }
                    return false;
                }
                return false;
            }
            return false;
        }

        private static Version ParseVersionOrDefault(object value, object value2)
        {
            if (!TryNormalizeVersion(value, out var _, out var result))
            {
                return (Version)value2;
            }
            return result;
        }

        private static string NormalizeDownloadUrl(object value)
        {
            object obj;
            if (value != null)
            {
                obj = ((string)value).Trim();
                if (obj != null)
                {
                    goto IL_0015;
                }
            }
            else
            {
                obj = null;
            }
            obj = string.Empty;
            goto IL_0015;
            IL_0015:
            string text = (string)obj;
            if (Uri.TryCreate(text, UriKind.Absolute, out var _))
            {
                return text;
            }
            return "https://assets.efunstudio.cn";
        }

        private static string NormalizeReleaseNotes(object value)
        {
            object obj;
            if (value == null)
            {
                obj = null;
            }
            else
            {
                obj = ((string)value).Trim();
                if (obj != null)
                {
                    goto IL_0015;
                }
            }
            obj = string.Empty;
            goto IL_0015;
            IL_0015:
            string text = (string)obj;
            if (string.IsNullOrWhiteSpace(text))
            {
                return "检测到新版本, 推荐更新";
            }
            return text;
        }

        private static void UpdateCacheFromResult(object value, DateTime value2)
        {
            PsdReaderUpdateCache value3 = LoadUpdateCache();
            value3.LastCheckUtcTicks = value2.Ticks;
            if (value != null && (((PsdReaderProductUpdateInfo)value).Status == PsdReaderProductUpdateStatus.UpdateAvailable || ((PsdReaderProductUpdateInfo)value).Status == PsdReaderProductUpdateStatus.UpToDate))
            {
                if (TryNormalizeVersion(((PsdReaderProductUpdateInfo)value).LatestVersion, out var normalizedLatestVersion, out var _))
                {
                    value3.LatestVersion = normalizedLatestVersion;
                }
                if (((PsdReaderProductUpdateInfo)value).Status == PsdReaderProductUpdateStatus.UpdateAvailable)
                {
                    value3.DownloadUrl = NormalizeDownloadUrl(((PsdReaderProductUpdateInfo)value).DownloadUrl);
                    value3.ReleaseNotes = NormalizeReleaseNotes(((PsdReaderProductUpdateInfo)value).ReleaseNotes);
                }
                else
                {
                    ClearCachedUpdateDetails(value3);
                }
            }
            SaveUpdateCache(value3);
        }

        private static bool TryGetCachedUpdate(out PsdReaderProductUpdateInfo result)
        {
            result = null;
            PsdReaderUpdateCache value = LoadUpdateCache();
            if (!TryGetCachedVersion(value, out var latestVersion, out var version))
            {
                return false;
            }
            if (version.CompareTo(PsdReaderCurrentVersionInfo.GetCurrentVersion()) > 0)
            {
                result = CreateDefaultUpdateInfo();
                result.Status = PsdReaderProductUpdateStatus.UpdateAvailable;
                result.HasUpdate = true;
                result.LatestVersion = latestVersion;
                result.DownloadUrl = NormalizeDownloadUrl(value.DownloadUrl);
                result.ReleaseNotes = NormalizeReleaseNotes(value.ReleaseNotes);
                result.Message = "检测到新版本。";
                return true;
            }
            ClearCachedUpdateDetails(value);
            SaveUpdateCache(value);
            return false;
        }

        private static bool ShouldRunDailyCheck(object value, DateTime value2)
        {
            if (value != null && ((PsdReaderUpdateCache)value).LastCheckUtcTicks > 0L)
            {
                try
                {
                    return new DateTime(((PsdReaderUpdateCache)value).LastCheckUtcTicks, DateTimeKind.Utc).Date < value2.Date;
                }
                catch
                {
                    return true;
                }
            }
            return true;
        }

        private static bool TryGetCachedVersion(object value, out string result, out Version result2)
        {
            result = string.Empty;
            result2 = null;
            if (value != null)
            {
                return TryNormalizeVersion(((PsdReaderUpdateCache)value).LatestVersion, out result, out result2);
            }
            return false;
        }

        private static PsdReaderUpdateCache LoadUpdateCache()
        {
            string text = EditorPrefs.GetString("cn.efunstudio.psdreader.update.cache", string.Empty);
            if (!string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    PsdReaderUpdateCache value = JsonUtility.FromJson<PsdReaderUpdateCache>(text);
                    if (value == null || value.SchemaVersion < 1)
                    {
                        return CreateDefaultUpdateCache();
                    }
                    if (!TryNormalizeVersion(value.LatestVersion, out var normalizedLatestVersion, out var _))
                    {
                        value.LatestVersion = string.Empty;
                    }
                    else
                    {
                        value.LatestVersion = normalizedLatestVersion;
                    }
                    value.DownloadUrl = NormalizeDownloadUrl(value.DownloadUrl);
                    string text2 = value.ReleaseNotes;
                    object obj;
                    if (text2 != null)
                    {
                        obj = text2.Trim();
                        if (obj != null)
                        {
                            goto IL_0092;
                        }
                    }
                    else
                    {
                        obj = null;
                    }
                    obj = string.Empty;
                    goto IL_0092;
                    IL_0092:
                    value.ReleaseNotes = (string)obj;
                    if (string.IsNullOrEmpty(value.LatestVersion))
                    {
                        ClearCachedUpdateDetails(value);
                    }
                    return value;
                }
                catch (Exception)
                {
                    return CreateDefaultUpdateCache();
                }
            }
            return CreateDefaultUpdateCache();
        }

        private static void SaveUpdateCache(object value)
        {
            if (value == null)
            {
                value = CreateDefaultUpdateCache();
            }
            try
            {
                EditorPrefs.SetString("cn.efunstudio.psdreader.update.cache", JsonUtility.ToJson(value));
            }
            catch (Exception)
            {
            }
        }

        private static PsdReaderUpdateCache CreateDefaultUpdateCache()
        {
            return new PsdReaderUpdateCache
            {
                SchemaVersion = 1,
                LastCheckUtcTicks = 0L,
                LatestVersion = string.Empty,
                DownloadUrl = "https://assets.efunstudio.cn",
                ReleaseNotes = string.Empty
            };
        }

        private static void ClearCachedUpdateDetails(object value)
        {
            if (value != null)
            {
                ((PsdReaderUpdateCache)value).DownloadUrl = "https://assets.efunstudio.cn";
                ((PsdReaderUpdateCache)value).ReleaseNotes = string.Empty;
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderUpdateService GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
