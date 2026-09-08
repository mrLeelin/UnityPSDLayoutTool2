using System;
using PsdLicensing;
using PsdProtectionGuards;
using UeFsms9fnKVPqDEHqUr;
using UnityEditor;
using UnityEngine;
using cn.efunstudio.psdreader;
using PsdProtectionRuntime;

namespace PsdLicensing
{

internal static class PsdProductUpdateService
{
	private static readonly LicenseRepositoryClient _repositoryClient;

	private static bool _startupCheckPerformed;

	internal static PsdReaderProductUpdateInfo CheckForUpdates()
	{
		PsdReaderProductUpdateInfo psdReaderProductUpdateInfo = CreateDefaultUpdateInfo();
		try
		{
			PsdReaderLicenseClientConfigDocument psdReaderLicenseClientConfigDocument = LicenseClientDefaults.CreateClientConfig();
			if (psdReaderLicenseClientConfigDocument != null && psdReaderLicenseClientConfigDocument.RepositoryBaseUrls != null && psdReaderLicenseClientConfigDocument.RepositoryBaseUrls.Length != 0)
			{
				string text = GetUpdateEnvelopeScope();
				if (!TryDownloadSignedUpdateDocument<PsdReaderProductUpdatePayloadDocument>(psdReaderLicenseClientConfigDocument, LicenseClientDefaults.GetPublicKeyPem(), GetUpdateRepositoryPath(), text, LicenseCryptography.DeriveRepositoryDocumentKey(text), out var psdReaderProductUpdatePayloadDocument, out var flag, out var text2))
				{
					psdReaderProductUpdateInfo.Status = ((!flag) ? PsdReaderProductUpdateStatus.NetworkError : PsdReaderProductUpdateStatus.UpdateInfoMissing);
					psdReaderProductUpdateInfo.Message = ((!flag) ? ("检查更新失败：" + ((!string.IsNullOrWhiteSpace(text2)) ? text2 : "网络请求失败。")) : "当前还没有发布更新元数据。");
					return psdReaderProductUpdateInfo;
				}
				if (ValidateUpdateMetadata(psdReaderProductUpdatePayloadDocument, out var status, out var message))
				{
					Version version = ParseVersionOrDefault(psdReaderProductUpdatePayloadDocument.Version, PsdProductVersionAccessor.GetCurrentVersion());
					psdReaderProductUpdateInfo.LatestVersion = psdReaderProductUpdatePayloadDocument.Version;
					psdReaderProductUpdateInfo.DownloadUrl = NormalizeDownloadUrl(psdReaderProductUpdatePayloadDocument.DownloadUrl);
					psdReaderProductUpdateInfo.ReleaseNotes = NormalizeReleaseNotes(psdReaderProductUpdatePayloadDocument.ReleaseNotes);
					psdReaderProductUpdateInfo.PublishedAtUtc = psdReaderProductUpdatePayloadDocument.PublishedAtUtc ?? string.Empty;
					psdReaderProductUpdateInfo.HasUpdate = version.CompareTo(PsdProductVersionAccessor.GetCurrentVersion()) > 0;
					psdReaderProductUpdateInfo.Status = ((!psdReaderProductUpdateInfo.HasUpdate) ? PsdReaderProductUpdateStatus.UpToDate : PsdReaderProductUpdateStatus.UpdateAvailable);
					psdReaderProductUpdateInfo.Message = ((!psdReaderProductUpdateInfo.HasUpdate) ? "当前无可用更新。" : "检测到新版本。");
					return psdReaderProductUpdateInfo;
				}
				psdReaderProductUpdateInfo.Status = status;
				psdReaderProductUpdateInfo.Message = message;
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
		CacheUpdateResult(psdReaderProductUpdateInfo, utcNow);
		if (psdReaderProductUpdateInfo != null)
		{
			if (psdReaderProductUpdateInfo.Status == PsdReaderProductUpdateStatus.UpdateAvailable)
			{
				string message = "发现新版本。" + Environment.NewLine + "当前版本：" + psdReaderProductUpdateInfo.CurrentVersion + Environment.NewLine + "最新版本：" + psdReaderProductUpdateInfo.LatestVersion + Environment.NewLine + Environment.NewLine + "更新说明：" + Environment.NewLine + psdReaderProductUpdateInfo.ReleaseNotes;
				if (EditorUtility.DisplayDialogComplex("Psd2UGUI 更新检查", message, "前往下载", "取消", string.Empty) == 0)
				{
					Application.OpenURL(psdReaderProductUpdateInfo.DownloadUrl);
				}
			}
			else
			{
				EditorUtility.DisplayDialog("Psd2UGUI 更新检查", "当前无可用更新。", "确定");
			}
		}
		else
		{
			EditorUtility.DisplayDialog("Psd2UGUI 更新检查", "当前无可用更新。", "确定");
		}
	}

	internal static void CheckForUpdatesAtStartup()
	{
		if (Application.isBatchMode || _startupCheckPerformed)
		{
			return;
		}
		DateTime utcNow = DateTime.UtcNow;
		FkvU8t94QmHfPS500MK fkvU8t94QmHfPS500MK = LoadUpdateCache();
		if (!ShouldCheckToday(fkvU8t94QmHfPS500MK, utcNow))
		{
			return;
		}
		fkvU8t94QmHfPS500MK.Dax9g04m0k = utcNow.Ticks;
		SaveUpdateCache(fkvU8t94QmHfPS500MK);
		_startupCheckPerformed = true;
		try
		{
			CacheUpdateResult(CheckForUpdates(), utcNow);
		}
		catch (Exception)
		{
		}
		finally
		{
			_startupCheckPerformed = false;
		}
	}

	internal static bool HasCachedUpdate()
	{
		PsdReaderProductUpdateInfo psdReaderProductUpdateInfo;
		return TryGetCachedUpdate(out psdReaderProductUpdateInfo);
	}

	internal static string GetCachedUpdateLabel()
	{
		if (TryGetCachedUpdate(out var psdReaderProductUpdateInfo))
		{
			return "检测到新版本 v" + psdReaderProductUpdateInfo.LatestVersion;
		}
		return string.Empty;
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
			CurrentVersion = "2.0.0",
			LatestVersion = "2.0.0",
			DownloadUrl = "https://assets.efunstudio.cn",
			ReleaseNotes = string.Empty,
			PublishedAtUtc = string.Empty,
			Message = string.Empty
		};
	}

	private static bool ValidateUpdateMetadata(object P_0, out PsdReaderProductUpdateStatus P_1, out string P_2)
	{
		P_1 = PsdReaderProductUpdateStatus.InvalidData;
		P_2 = "更新元数据无效。";
		if (P_0 != null && ((PsdReaderProductUpdatePayloadDocument)P_0).Schema >= 1 && ((PsdReaderProductUpdatePayloadDocument)P_0).Revision > 0)
		{
			if (string.Equals(((PsdReaderProductUpdatePayloadDocument)P_0).VendorCode, "efunstudio", StringComparison.Ordinal) && string.Equals(((PsdReaderProductUpdatePayloadDocument)P_0).ProductCode, "psd2ugui", StringComparison.Ordinal))
			{
				if (!TryParseVersion(((PsdReaderProductUpdatePayloadDocument)P_0).Version, out var version, out var _))
				{
					P_2 = "更新元数据缺少合法版本号。";
					return false;
				}
				((PsdReaderProductUpdatePayloadDocument)P_0).Version = version;
				return true;
			}
			P_1 = PsdReaderProductUpdateStatus.ProductMismatch;
			P_2 = "更新元数据与当前插件不匹配。";
			return false;
		}
		return false;
	}

	private static bool TryDownloadSignedUpdateDocument<h6o2mDmnCYiPlCZqWBV>(object P_0, object P_1, object P_2, object P_3, object P_4, out h6o2mDmnCYiPlCZqWBV P_5, out bool P_6, out string P_7) where h6o2mDmnCYiPlCZqWBV : class
	{
		P_5 = null;
		P_6 = false;
		P_7 = string.Empty;
		if (!TryDownloadUpdateEnvelope(P_0, P_2, P_3, P_4, out var psdReaderSignedEnvelopeDocument, out P_6, out P_7))
		{
			return false;
		}
		if (!LicenseCryptography.TryVerifyAndDecryptSignedEnvelope(P_1, psdReaderSignedEnvelopeDocument, P_3, P_4, out var array))
		{
			P_7 = "更新元数据签名校验失败。";
			return false;
		}
		if (!LicensePayloadBinaryReader.TryReadPayloadDocument<h6o2mDmnCYiPlCZqWBV>(array, out P_5))
		{
			P_7 = "更新元数据反序列化失败。";
			return false;
		}
		return true;
	}

	private static bool TryDownloadUpdateEnvelope(object P_0, object P_1, object P_2, object P_3, out PsdReaderSignedEnvelopeDocument P_4, out bool P_5, out string P_6)
	{
		P_4 = null;
		P_5 = false;
		P_6 = string.Empty;
		if (!_repositoryClient.TryDownloadPayload(((PsdReaderLicenseClientConfigDocument)P_0).RepositoryBaseUrls, (string)P_1, ((PsdReaderLicenseClientConfigDocument)P_0).RequestTimeoutSeconds, out var array, out P_5, out P_6, true))
		{
			return false;
		}
		if (LicenseCryptography.TryDecodeBinaryEnvelope(array, P_3, P_2, out P_4) && P_4 != null)
		{
			return true;
		}
		P_6 = "更新元数据信封解析失败。";
		return false;
	}

	private static string GetUpdateRepositoryPath()
	{
		return "products/psd2ugui/update.data";
	}

	private static string GetUpdateEnvelopeScope()
	{
		return "update:psd2ugui";
	}

	private static bool TryParseVersion(object P_0, out string P_1, out Version P_2)
	{
		P_1 = string.Empty;
		P_2 = null;
		object obj;
		if (P_0 != null)
		{
			obj = ((string)P_0).Trim();
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
				if (Version.TryParse(text, out P_2) && !(P_2 == null))
				{
					P_1 = P_2.ToString(array.Length);
					return true;
				}
				return false;
			}
			return false;
		}
		return false;
	}

	private static Version ParseVersionOrDefault(object P_0, object P_1)
	{
		if (TryParseVersion(P_0, out var _, out var result))
		{
			return result;
		}
		return (Version)P_1;
	}

	private static string NormalizeDownloadUrl(object P_0)
	{
		object obj;
		if (P_0 != null)
		{
			obj = ((string)P_0).Trim();
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
		if (!Uri.TryCreate(text, UriKind.Absolute, out var _))
		{
			return "https://assets.efunstudio.cn";
		}
		return text;
	}

	private static string NormalizeReleaseNotes(object P_0)
	{
		object obj;
		if (P_0 == null)
		{
			obj = null;
		}
		else
		{
			obj = ((string)P_0).Trim();
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

	private static void CacheUpdateResult(object P_0, DateTime P_1)
	{
		FkvU8t94QmHfPS500MK fkvU8t94QmHfPS500MK = LoadUpdateCache();
		fkvU8t94QmHfPS500MK.Dax9g04m0k = P_1.Ticks;
		if (P_0 != null && (((PsdReaderProductUpdateInfo)P_0).Status == PsdReaderProductUpdateStatus.UpdateAvailable || ((PsdReaderProductUpdateInfo)P_0).Status == PsdReaderProductUpdateStatus.UpToDate))
		{
			if (TryParseVersion(((PsdReaderProductUpdateInfo)P_0).LatestVersion, out var rAS9lYC0OH, out var _))
			{
				fkvU8t94QmHfPS500MK.RAS9lYC0OH = rAS9lYC0OH;
			}
			if (((PsdReaderProductUpdateInfo)P_0).Status == PsdReaderProductUpdateStatus.UpdateAvailable)
			{
				fkvU8t94QmHfPS500MK.Q0E9NiTYQb = NormalizeDownloadUrl(((PsdReaderProductUpdateInfo)P_0).DownloadUrl);
				fkvU8t94QmHfPS500MK.HFn9kVLqvA = NormalizeReleaseNotes(((PsdReaderProductUpdateInfo)P_0).ReleaseNotes);
			}
			else
			{
				ResetCachedUpdateDetails(fkvU8t94QmHfPS500MK);
			}
		}
		SaveUpdateCache(fkvU8t94QmHfPS500MK);
	}

	private static bool TryGetCachedUpdate(out PsdReaderProductUpdateInfo P_0)
	{
		P_0 = null;
		FkvU8t94QmHfPS500MK fkvU8t94QmHfPS500MK = LoadUpdateCache();
		if (TryGetCachedVersion(fkvU8t94QmHfPS500MK, out var latestVersion, out var version))
		{
			if (version.CompareTo(PsdProductVersionAccessor.GetCurrentVersion()) <= 0)
			{
				ResetCachedUpdateDetails(fkvU8t94QmHfPS500MK);
				SaveUpdateCache(fkvU8t94QmHfPS500MK);
				return false;
			}
			P_0 = CreateDefaultUpdateInfo();
			P_0.Status = PsdReaderProductUpdateStatus.UpdateAvailable;
			P_0.HasUpdate = true;
			P_0.LatestVersion = latestVersion;
			P_0.DownloadUrl = NormalizeDownloadUrl(fkvU8t94QmHfPS500MK.Q0E9NiTYQb);
			P_0.ReleaseNotes = NormalizeReleaseNotes(fkvU8t94QmHfPS500MK.HFn9kVLqvA);
			P_0.Message = "检测到新版本。";
			return true;
		}
		return false;
	}

	private static bool ShouldCheckToday(object P_0, DateTime P_1)
	{
		if (P_0 != null && ((FkvU8t94QmHfPS500MK)P_0).Dax9g04m0k > 0L)
		{
			try
			{
				return new DateTime(((FkvU8t94QmHfPS500MK)P_0).Dax9g04m0k, DateTimeKind.Utc).Date < P_1.Date;
			}
			catch
			{
				return true;
			}
		}
		return true;
	}

	private static bool TryGetCachedVersion(object P_0, out string P_1, out Version P_2)
	{
		P_1 = string.Empty;
		P_2 = null;
		if (P_0 == null)
		{
			return false;
		}
		return TryParseVersion(((FkvU8t94QmHfPS500MK)P_0).RAS9lYC0OH, out P_1, out P_2);
	}

	private static FkvU8t94QmHfPS500MK LoadUpdateCache()
	{
		string text = EditorPrefs.GetString("cn.efunstudio.psdreader.update.cache", string.Empty);
		if (!string.IsNullOrWhiteSpace(text))
		{
			try
			{
				FkvU8t94QmHfPS500MK fkvU8t94QmHfPS500MK = JsonUtility.FromJson<FkvU8t94QmHfPS500MK>(text);
				if (fkvU8t94QmHfPS500MK == null || fkvU8t94QmHfPS500MK.F359crkKjL < 1)
				{
					return CreateDefaultUpdateCache();
				}
				if (!TryParseVersion(fkvU8t94QmHfPS500MK.RAS9lYC0OH, out var rAS9lYC0OH, out var _))
				{
					fkvU8t94QmHfPS500MK.RAS9lYC0OH = string.Empty;
				}
				else
				{
					fkvU8t94QmHfPS500MK.RAS9lYC0OH = rAS9lYC0OH;
				}
				fkvU8t94QmHfPS500MK.Q0E9NiTYQb = NormalizeDownloadUrl(fkvU8t94QmHfPS500MK.Q0E9NiTYQb);
				string hFn9kVLqvA = fkvU8t94QmHfPS500MK.HFn9kVLqvA;
				object obj;
				if (hFn9kVLqvA != null)
				{
					obj = hFn9kVLqvA.Trim();
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
				fkvU8t94QmHfPS500MK.HFn9kVLqvA = (string)obj;
				if (string.IsNullOrEmpty(fkvU8t94QmHfPS500MK.RAS9lYC0OH))
				{
					ResetCachedUpdateDetails(fkvU8t94QmHfPS500MK);
				}
				return fkvU8t94QmHfPS500MK;
			}
			catch (Exception)
			{
				return CreateDefaultUpdateCache();
			}
		}
		return CreateDefaultUpdateCache();
	}

	private static void SaveUpdateCache(object P_0)
	{
		if (P_0 == null)
		{
			P_0 = CreateDefaultUpdateCache();
		}
		try
		{
			EditorPrefs.SetString("cn.efunstudio.psdreader.update.cache", JsonUtility.ToJson(P_0));
		}
		catch (Exception)
		{
		}
	}

	private static FkvU8t94QmHfPS500MK CreateDefaultUpdateCache()
	{
		return new FkvU8t94QmHfPS500MK
		{
			F359crkKjL = 1,
			Dax9g04m0k = 0L,
			RAS9lYC0OH = string.Empty,
			Q0E9NiTYQb = "https://assets.efunstudio.cn",
			HFn9kVLqvA = string.Empty
		};
	}

	private static void ResetCachedUpdateDetails(object P_0)
	{
		if (P_0 != null)
		{
			((FkvU8t94QmHfPS500MK)P_0).Q0E9NiTYQb = "https://assets.efunstudio.cn";
			((FkvU8t94QmHfPS500MK)P_0).HFn9kVLqvA = string.Empty;
		}
	}

	static PsdProductUpdateService()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_repositoryClient = new LicenseRepositoryClient();
	}
}
}
