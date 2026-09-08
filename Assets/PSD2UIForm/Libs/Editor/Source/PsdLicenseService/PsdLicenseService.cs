using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using PsdLicensing;
using PsdProtectionGuards;
using PsdPreviewProtection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using cn.efunstudio.psdreader;
using PsdProtectionRuntime;
using PsdLicenseEditor;

namespace PsdLicensing
{

internal static class PsdLicenseService
{
	private enum OK9cinEpCPEorEb3ebh : byte
	{

	}

	private enum EPcPUUEPrgAWTvPplYA : byte
	{

	}

	private sealed class u0JyV2E2DSyWPya5E1p : IDisposable
	{
		private bool dVgE5pIgia;

		public void Dispose()
		{
			if (dVgE5pIgia)
			{
				return;
			}
			dVgE5pIgia = true;
			lock (HxaHki07OS)
			{
				if (wsF3ZHYCUB > 0)
				{
					wsF3ZHYCUB--;
				}
				if (wsF3ZHYCUB == 0)
				{
					G4HMgaLocY();
					OH8MlLIfl3();
				}
			}
		}

		public u0JyV2E2DSyWPya5E1p()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}
	}

	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass96_0
	{
		public string goHEU2bmrC;

		public _003C_003Ec__DisplayClass96_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal bool yR3EB3iJOU(string candidate)
		{
			return string.Equals(LicenseCryptography.NormalizeHexString(candidate), goHEU2bmrC, StringComparison.Ordinal);
		}
	}

	private static readonly object HxaHki07OS;

	private static readonly LicenseRepositoryClient owHHXedVws;

	private static readonly LicenseCacheStore XiAH7XroGM;

	private static readonly LicenseDeviceIdentifierProvider TSHHv3vRBj;

	private static readonly LicenseTelemetryClient v3EHdhhO7g;

	private static readonly HashSet<string> uxhHRepIhf;

	private static readonly HashSet<string> PiLH8JL5Zc;

	private static LicenseStatusSnapshot flxHD5TW8h;

	private static PsdReaderLicenseCacheDocument rdfHVoGDWI;

	private static PsdReaderLicenseClientConfigDocument ub1HyAPIjL;

	private static bool NWPHzw3nnc;

	private static int BCG316CFVC;

	private static int wsF3ZHYCUB;

	private static int RPf3Oclfhp;

	private static string dW03hWKoau;

	private static ProtectedPreviewLicenseState pDk3n5wh0p;

	private static string ULU3pmnc8Q;

	private static string Jby3PH7IrF;

	private static DateTime hid32FLsuw;

	private static string GOt35kDaQo;

	internal static LicenseStatusSnapshot GetStatusSnapshot()
	{
		CDhQXZ78Tm();
		lock (HxaHki07OS)
		{
			NuMHmOksdm();
			return CloneStatusSnapshot(flxHD5TW8h ?? CreateStatusSnapshot((LicenseValidationStatus)4, "尚未激活 Psd2UGUI 授权。"));
		}
	}

	internal static bool ActivateOrder(object P_0, out string P_1)
	{
		return dKDMEHmthv(P_0, (EPcPUUEPrgAWTvPplYA)1, "order", out P_1);
	}

	private static bool dKDMEHmthv(object P_0, EPcPUUEPrgAWTvPplYA P_1, object P_2, out string P_3)
	{
		string text = LicenseCryptography.NormalizeOrderNumber(P_0);
		if (text.Length != 19)
		{
			P_3 = "订单号必须是 19 位数字。";
			PsdReaderLicenseClientConfigDocument psdReaderLicenseClientConfigDocument;
			lock (HxaHki07OS)
			{
				flxHD5TW8h = CreateStatusSnapshot((LicenseValidationStatus)3, P_3);
				BCG316CFVC = 0;
				psdReaderLicenseClientConfigDocument = eRbH58XT8E();
			}
			dKVQ3T6I8j(P_1, psdReaderLicenseClientConfigDocument, text, (LicenseValidationStatus)3, null);
			return false;
		}
		string text2 = LicenseCryptography.HashLicenseLookupId(text);
		byte[] array = LicenseCryptography.DeriveLicenseAccessKey(text, text2);
		LicenseEvaluationContext lxqZcsud7O9XCugf7PT;
		return rj4QHbVwQF(text2, array, text, true, P_1, P_2, out P_3, out lxqZcsud7O9XCugf7PT);
	}

	internal static bool RefreshLicense(out string P_0)
	{
		lock (HxaHki07OS)
		{
			NuMHmOksdm();
			if (rdfHVoGDWI == null || string.IsNullOrWhiteSpace(rdfHVoGDWI.LookupId))
			{
				P_0 = "当前没有可刷新的授权缓存。";
				flxHD5TW8h = CreateStatusSnapshot((LicenseValidationStatus)4, P_0);
				BCG316CFVC = 0;
				return false;
			}
		}
		LicenseEvaluationContext lxqZcsud7O9XCugf7PT;
		return rj4QHbVwQF(rdfHVoGDWI.LookupId, HADQ4X3eaX(), null, true, (EPcPUUEPrgAWTvPplYA)0, null, out P_0, out lxqZcsud7O9XCugf7PT);
	}

	internal static bool HasProjectLicense()
	{
		PsdReaderProjectLicenseBundleDocument psdReaderProjectLicenseBundleDocument;
		DateTime dateTime;
		string text;
		return icqQyaBchS(out psdReaderProjectLicenseBundleDocument, out dateTime, out text);
	}

	internal static string GetProjectLicenseHint()
	{
		if (icqQyaBchS(out var _, out var _, out var _))
		{
			return "已检测到插件目录授权文件：" + LicenseStoragePaths.GetProjectLicenseAssetPath() + "\n可直接使用授权文件激活";
		}
		return "可将项目授权文件保存到插件目录：" + LicenseStoragePaths.GetProjectLicenseAssetPath();
	}

	internal static bool CanSaveProjectLicense()
	{
		string text;
		return CheckProjectLicenseExportEligibility(out text);
	}

	internal static int GetDefaultExportValidDays()
	{
		return 90;
	}

	internal static bool CheckProjectLicenseExportEligibility(out string P_0)
	{
		PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument;
		long num;
		return b9mH1dTdrN(out psdReaderLicenseCacheDocument, out num, out P_0);
	}

	internal static bool ActivateProjectLicense(out string P_0)
	{
		if (!K1EQRjHsYs(out var lxqZcsud7O9XCugf7PT, out P_0, out var LicenseValidationStatus))
		{
			PsdReaderLicenseClientConfigDocument psdReaderLicenseClientConfigDocument;
			lock (HxaHki07OS)
			{
				flxHD5TW8h = CreateStatusSnapshot((LicenseValidationStatus)4, P_0, rdfHVoGDWI);
				BCG316CFVC = 0;
				psdReaderLicenseClientConfigDocument = eRbH58XT8E();
			}
			dKVQ3T6I8j((EPcPUUEPrgAWTvPplYA)2, psdReaderLicenseClientConfigDocument, null, LicenseValidationStatus, null);
			return false;
		}
		lock (HxaHki07OS)
		{
			rdfHVoGDWI = lxqZcsud7O9XCugf7PT.GetLicenseCache();
			ub1HyAPIjL = eRbH58XT8E(lxqZcsud7O9XCugf7PT.GetProductMetadata(), lxqZcsud7O9XCugf7PT.GetLicenseCache());
			XiAH7XroGM.Save(rdfHVoGDWI);
			flxHD5TW8h = CreateStatusSnapshot((LicenseValidationStatus)1, "授权已更新。", rdfHVoGDWI);
			BCG316CFVC = int.MinValue;
		}
		hMHQ93OBLr();
		dKVQ3T6I8j((EPcPUUEPrgAWTvPplYA)2, ub1HyAPIjL, null, (LicenseValidationStatus)1, lxqZcsud7O9XCugf7PT);
		P_0 = "授权已更新。";
		return true;
	}

	internal static bool SaveProjectLicense(int P_0, out string P_1)
	{
		if (xeGQDjG25B(P_0, out var psdReaderProjectLicenseBundleDocument, out P_1))
		{
			try
			{
				byte[] array = LicenseCryptography.EncodeProjectLicenseBundle(psdReaderProjectLicenseBundleDocument);
				if (array != null && array.Length != 0)
				{
					Directory.CreateDirectory(LicenseStoragePaths.GetPluginPhysicalDirectory());
					File.WriteAllBytes(LicenseStoragePaths.GetProjectLicensePhysicalPath(), array);
					AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
					P_1 = "项目授权文件已保存到：" + LicenseStoragePaths.GetProjectLicenseAssetPath();
					return true;
				}
				P_1 = "生成项目授权文件失败。";
				return false;
			}
			catch (Exception)
			{
				P_1 = "写入项目授权文件失败。";
				return false;
			}
		}
		return false;
	}

	internal static IDisposable BeginLicenseOperation()
	{
		lock (HxaHki07OS)
		{
			if (wsF3ZHYCUB == 0)
			{
				OH8MlLIfl3();
			}
			wsF3ZHYCUB++;
			return new u0JyV2E2DSyWPya5E1p();
		}
	}

	internal static bool HasMainFeature()
	{
		return (GetAccessFlags() & 1) != 0;
	}

	internal static string ClearLocalLicense()
	{
		string text = ((File.Exists(LicenseStoragePaths.GetProjectLicensePhysicalPath()) && !XspQzytJSY()) ? "本地授权缓存已清除，但插件目录授权文件删除失败，请手动删除。" : "本地授权缓存和插件目录授权文件已清除。");
		lock (HxaHki07OS)
		{
			XiAH7XroGM.Clear();
			rdfHVoGDWI = null;
			ub1HyAPIjL = null;
			NWPHzw3nnc = true;
			ULU3pmnc8Q = string.Empty;
			flxHD5TW8h = CreateStatusSnapshot((LicenseValidationStatus)4, text);
			BCG316CFVC = 0;
		}
		hMHQ93OBLr();
		return text;
	}

	internal static void pXCMjCaVRM(object P_0)
	{
		if (string.IsNullOrWhiteSpace((string)P_0))
		{
			return;
		}
		lock (HxaHki07OS)
		{
			if (!sOEMN997bV(P_0))
			{
				return;
			}
			NuMHmOksdm();
			PsdReaderLicenseClientConfigDocument psdReaderLicenseClientConfigDocument = eRbH58XT8E();
			if (psdReaderLicenseClientConfigDocument != null)
			{
				string text = rmGMtVVRi4(BTsQcwXXjP(), P_0);
				if (!string.IsNullOrWhiteSpace(text))
				{
					v3EHdhhO7g.QueueTrackingEvent(psdReaderLicenseClientConfigDocument, text, (string)P_0);
				}
			}
		}
	}

	internal static void WlEMY3BKXU()
	{
		lock (HxaHki07OS)
		{
			if (wsF3ZHYCUB > 0)
			{
				RPf3Oclfhp++;
			}
		}
	}

	private static string rmGMtVVRi4(object P_0, object P_1)
	{
		string text = LicenseCryptography.NormalizeOrderNumber(P_0);
		if (text.Length == 19)
		{
			return text;
		}
		if (XAoM4YcVId(P_1))
		{
			return WUuMfFOvLI();
		}
		return wJaMcVsuYn(text);
	}

	private static bool XAoM4YcVId(object P_0)
	{
		if (string.IsNullOrWhiteSpace((string)P_0))
		{
			return false;
		}
		return ((string)P_0).StartsWith("trial_", StringComparison.Ordinal);
	}

	private static string WUuMfFOvLI()
	{
		TSHHv3vRBj.GetDeviceIdHash(out var text);
		text = LicenseCryptography.NormalizeHexString(text);
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}
		string text2 = LicenseCryptography.NormalizeHexString(LicenseCryptography.ComputeStringSha256Hex("PsdReader|TrialTelemetry|v1|psd2ugui|" + text));
		if (text2.Length > 16)
		{
			text2 = text2.Substring(0, 16);
		}
		return "trial_" + text2;
	}

	private static string wJaMcVsuYn(object P_0)
	{
		string text = LicenseCryptography.NormalizeOrderNumber(P_0);
		if (!string.IsNullOrWhiteSpace(text))
		{
			return "error_" + text;
		}
		return "error_order";
	}

	private static void G4HMgaLocY()
	{
		if (RPf3Oclfhp > 0)
		{
			NuMHmOksdm();
			pXCMjCaVRM((LicenseStatusPresentation.GetAvailability(flxHD5TW8h ?? CreateStatusSnapshot((LicenseValidationStatus)4, "尚未激活 Psd2UGUI 授权。")) == (LicenseAvailability)0) ? "generate_uiform" : "trial_generate_uiform");
		}
	}

	private static void OH8MlLIfl3()
	{
		uxhHRepIhf.Clear();
		PiLH8JL5Zc.Clear();
		RPf3Oclfhp = 0;
		dW03hWKoau = string.Empty;
		pDk3n5wh0p = null;
	}

	private static bool sOEMN997bV(object P_0)
	{
		if (wsF3ZHYCUB > 0)
		{
			return uxhHRepIhf.Add((string)(P_0 ?? string.Empty));
		}
		return true;
	}

	internal static ProtectedPreviewLicenseState GetPreviewLicenseState()
	{
		lock (HxaHki07OS)
		{
			if (wsF3ZHYCUB > 0 && pDk3n5wh0p != null)
			{
				return pDk3n5wh0p;
			}
			NuMHmOksdm();
			LicenseEvaluationContext lxqZcsud7O9XCugf7PT = null;
			string text;
			if (JQnQkn0I1L())
			{
				GtKQdcUkVp(out lxqZcsud7O9XCugf7PT, out text);
			}
			if (lxqZcsud7O9XCugf7PT == null && yRNQtWcVCU())
			{
				rj4QHbVwQF(rdfHVoGDWI.LookupId, HADQ4X3eaX(), null, false, (EPcPUUEPrgAWTvPplYA)0, null, out text, out lxqZcsud7O9XCugf7PT);
				if (lxqZcsud7O9XCugf7PT == null)
				{
					lxqZcsud7O9XCugf7PT = XWgQw1Pyh3(rdfHVoGDWI);
				}
			}
			ProtectedPreviewLicenseState result = ((lxqZcsud7O9XCugf7PT == null || !ztVQj7HcWe("Main", lxqZcsud7O9XCugf7PT, out lxqZcsud7O9XCugf7PT, out text)) ? CreateInvalidPreviewLicenseState() : CreateValidPreviewLicenseState(lxqZcsud7O9XCugf7PT));
			if (wsF3ZHYCUB > 0)
			{
				pDk3n5wh0p = result;
			}
			return result;
		}
	}

	internal static ProtectedPreviewLicenseState GetPreviewLicenseStateAfterRefreshAttempt()
	{
		if (!wSyM7kb5Pu(out var result))
		{
			CRRMvYw4wp();
			if (wSyM7kb5Pu(out result))
			{
				return result;
			}
			lock (HxaHki07OS)
			{
				NuMHmOksdm();
				return CreateInvalidPreviewLicenseState();
			}
		}
		return result;
	}

	private static bool wSyM7kb5Pu(out ProtectedPreviewLicenseState P_0)
	{
		P_0 = null;
		lock (HxaHki07OS)
		{
			NuMHmOksdm();
			if (!pnsHufAds0(rdfHVoGDWI))
			{
				return false;
			}
			LicenseEvaluationContext lxqZcsud7O9XCugf7PT = XWgQw1Pyh3(rdfHVoGDWI);
			if (lxqZcsud7O9XCugf7PT != null)
			{
				P_0 = CreateValidPreviewLicenseState(lxqZcsud7O9XCugf7PT);
				return true;
			}
			return false;
		}
	}

	private static void CRRMvYw4wp()
	{
		string text = string.Empty;
		byte[] array = null;
		bool flag = false;
		lock (HxaHki07OS)
		{
			NuMHmOksdm();
			if (pnsHufAds0(rdfHVoGDWI))
			{
				return;
			}
			if (yRNQtWcVCU())
			{
				text = rdfHVoGDWI.LookupId;
				array = HADQ4X3eaX();
				if (array == null || array.Length == 0)
				{
					text = string.Empty;
					array = null;
				}
			}
			if (string.IsNullOrWhiteSpace(text))
			{
				flag = HasProjectLicense();
			}
			string text2 = ((!flag) ? LicenseCryptography.NormalizeHexString(text) : "project_file");
			if (string.IsNullOrWhiteSpace(text2) || !qoeMdjBfhN(text2))
			{
				return;
			}
			Jby3PH7IrF = text2;
			hid32FLsuw = DateTime.UtcNow;
		}
		LicenseEvaluationContext lxqZcsud7O9XCugf7PT;
		string text3;
		if (flag)
		{
			GtKQdcUkVp(out lxqZcsud7O9XCugf7PT, out text3);
		}
		else
		{
			rj4QHbVwQF(text, array, null, false, (EPcPUUEPrgAWTvPplYA)0, null, out text3, out lxqZcsud7O9XCugf7PT);
		}
	}

	private static bool qoeMdjBfhN(object P_0)
	{
		DateTime utcNow = DateTime.UtcNow;
		if (string.Equals(Jby3PH7IrF, (string)P_0, StringComparison.Ordinal) && !(hid32FLsuw <= DateTime.MinValue))
		{
			return utcNow >= hid32FLsuw.AddMinutes(10.0);
		}
		return true;
	}

	internal static int GetAccessFlags()
	{
		if (BCG316CFVC != int.MinValue)
		{
			return BCG316CFVC;
		}
		lock (HxaHki07OS)
		{
			if (BCG316CFVC != int.MinValue)
			{
				return BCG316CFVC;
			}
			try
			{
				BCG316CFVC = (GetPreviewLicenseState().GetLicenseValid() ? 1 : 0);
			}
			catch
			{
				BCG316CFVC = 0;
			}
			return BCG316CFVC;
		}
	}

	internal static PreviewProtectionProfile CreateLicensedPreviewProfile(object P_0, object P_1, object P_2, int P_3, int P_4, int P_5, int P_6, int P_7)
	{
		return CreatePreviewProfile(P_0, P_1, P_2, P_3, P_4, P_5, P_6, P_7, true);
	}

	internal static PreviewProtectionProfile CreateTrialPreviewProfile(object P_0, object P_1, object P_2, int P_3, int P_4, int P_5, int P_6, int P_7)
	{
		return CreatePreviewProfile(P_0, P_1, P_2, P_3, P_4, P_5, P_6, P_7, false);
	}

	internal static bool MatchesPreviewProfile(object P_0, object P_1, int P_2, int P_3, int P_4, int P_5, int P_6)
	{
		if (P_1 != null)
		{
			return sc7QpJ5FUr(CreatePreviewProfile(P_0, ((PreviewProtectionProfile)P_1).OFlTvUd2HH(), ((PreviewProtectionProfile)P_1).aurT8rbs9i(), P_2, P_3, P_4, P_5, P_6, true), P_1);
		}
		return false;
	}

	private static PreviewProtectionProfile CreatePreviewProfile(object P_0, object P_1, object P_2, int P_3, int P_4, int P_5, int P_6, int P_7, bool P_8)
	{
		if (P_0 == null)
		{
			P_0 = new ProtectedPreviewLicenseState();
		}
		P_1 = EdSQQtms6L(P_1);
		P_2 = EdSQQtms6L(P_2);
		string text = LicenseCryptography.ComputeStringSha256Hex(P_1);
		string text2 = LicenseCryptography.ComputeStringSha256Hex(P_2);
		string text3 = string.Format("{0}|RenderProfile|stable|{1}|", "psd2ugui", 1) + $"{LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetLookupId())}|{LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetLicenseId())}|{((ProtectedPreviewLicenseState)P_0).GetRevision()}|" + LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetProjectScopeFingerprint()) + "|" + LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetAssemblyFingerprint()) + "|" + $"{text}|{text2}|{P_4}|{P_5}|{P_6}|{P_7}|{P_3}";
		string text4 = string.Format("{0}|RenderProfile|session|{1}|", "psd2ugui", 1) + $"{LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GbnA4l2khJ())}|{((ProtectedPreviewLicenseState)P_0).GetSessionUtcTicks()}|" + $"{LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetStateFingerprint())}|{text}|{text2}|{P_3}";
		byte[] array = LicenseCryptography.ComputeStringSha256(text3);
		byte[] array2 = LicenseCryptography.ComputeStringHmacSha256(gUBQuTuO5q(P_0), text4);
		byte b = (byte)(104 + lbGQM6r6lK(array, 4, 0) % 72);
		byte b2 = (byte)(56 + lbGQM6r6lK(array, 5, 0) % 64);
		byte b3 = (byte)(24 + lbGQM6r6lK(array, 8, 0) % 72);
		byte b4 = (byte)(32 + lbGQM6r6lK(array2, 5, 0) % 80);
		bool flag = P_8 && lkKQZDEmno(P_0);
		bool flag2 = P_8 && zipQh6jGRo(P_0);
		PreviewProtectionProfile PreviewProtectionProfile = new PreviewProtectionProfile();
		PreviewProtectionProfile.Version = 1;
		PreviewProtectionProfile.SetStableSeed(ninQA8RLvj(array, 0, P_4 ^ P_5 ^ P_3));
		PreviewProtectionProfile.SetSessionSeed(ninQA8RLvj(array2, 0, P_6 ^ P_7 ^ P_3));
		PreviewProtectionProfile.VuYTLVgV7C((byte)((!flag) ? b : 0));
		PreviewProtectionProfile.iboTC3evT4((byte)((!flag) ? b2 : 0));
		PreviewProtectionProfile.OpETIfGOmw((byte)(5 + lbGQM6r6lK(array, 6, 0) % 7));
		PreviewProtectionProfile.TWHTwZyvi0((byte)(5 + lbGQM6r6lK(array, 7, 0) % 7));
		PreviewProtectionProfile.O6hTiEJGVm(0);
		PreviewProtectionProfile.GD7TYSTCK6((byte)((!flag2) ? b3 : 0));
		PreviewProtectionProfile.mm6TfcO4JQ((byte)((!flag2) ? b4 : 0));
		PreviewProtectionProfile.ok1TlGf7Tr((byte)(96 + lbGQM6r6lK(array, 9, 0) % 64));
		PreviewProtectionProfile.SetProtectionFlags(mGuQTav7YK(array, array2));
		PreviewProtectionProfile.d4ATdXT53l((string)P_1);
		PreviewProtectionProfile.q7ZTDNr52h((string)P_2);
		PreviewProtectionProfile vAe2XZTA0x9Y67an8oC2 = PreviewProtectionProfile;
		vAe2XZTA0x9Y67an8oC2.ykaTzR5REh(Qs5MzbYuhe(P_0, vAe2XZTA0x9Y67an8oC2, text, text2, P_3));
		vAe2XZTA0x9Y67an8oC2.uGJAOSdA4J(OjKQ1qReYs(P_0, vAe2XZTA0x9Y67an8oC2, text, text2, P_3));
		return vAe2XZTA0x9Y67an8oC2;
	}

	private static string Qs5MzbYuhe(object P_0, object P_1, object P_2, object P_3, int P_4)
	{
		string text = string.Format("{0}|RenderProfile|mac|{1}|", "psd2ugui", ((PreviewProtectionProfile)P_1)?.Version ?? 1) + $"{LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0)?.GetLookupId())}|{LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0)?.GetLicenseId())}|{((ProtectedPreviewLicenseState)P_0)?.GetRevision() ?? 0}|" + LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0)?.GetProjectScopeFingerprint()) + "|" + LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0)?.GetAssemblyFingerprint()) + "|" + $"{LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0)?.GbnA4l2khJ())}|{((ProtectedPreviewLicenseState)P_0)?.GetSessionUtcTicks() ?? 0L}|" + $"{P_2}|{P_3}|{P_4}|" + $"{((PreviewProtectionProfile)P_1)?.GetStableSeed() ?? 0}|{((PreviewProtectionProfile)P_1)?.GetSessionSeed() ?? 0}|{((PreviewProtectionProfile)P_1)?.GvsTSpIpvK() ?? 0}|{((PreviewProtectionProfile)P_1)?.auwTE03s0j() ?? 0}|" + $"{((PreviewProtectionProfile)P_1)?.xpdTxouQpO() ?? 0}|{((PreviewProtectionProfile)P_1)?.w3RTbRHx9B() ?? 0}|{((PreviewProtectionProfile)P_1)?.gjjTWI68cj() ?? 0}|{((PreviewProtectionProfile)P_1)?.WFaTjh9q7a() ?? 0}|{((PreviewProtectionProfile)P_1)?.vL5T4Sw8Hv() ?? 0}|{((PreviewProtectionProfile)P_1)?.naLTgkaVgS() ?? 0}|{(ushort)(((PreviewProtectionProfile)P_1)?.GetProtectionFlags() ?? ((PreviewProtectionFlags)0))}";
		return LicenseCryptography.EncodeHex(LicenseCryptography.ComputeStringHmacSha256(gUBQuTuO5q(P_0), text));
	}

	private static string OjKQ1qReYs(object P_0, object P_1, object P_2, object P_3, int P_4)
	{
		return LicenseCryptography.ComputeStringSha256Hex(string.Format("{0}|PreviewProtection|{1}|", "psd2ugui", ((PreviewProtectionProfile)P_1)?.Version ?? 1) + $"{LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0)?.Wo3Mnj7DeV())}|{P_2}|{P_3}|{P_4}|" + $"{((PreviewProtectionProfile)P_1)?.GetStableSeed() ?? 0}|{((PreviewProtectionProfile)P_1)?.GvsTSpIpvK() ?? 0}|{((PreviewProtectionProfile)P_1)?.auwTE03s0j() ?? 0}|{((PreviewProtectionProfile)P_1)?.xpdTxouQpO() ?? 0}|{((PreviewProtectionProfile)P_1)?.w3RTbRHx9B() ?? 0}|{((PreviewProtectionProfile)P_1)?.WFaTjh9q7a() ?? 0}|{((PreviewProtectionProfile)P_1)?.naLTgkaVgS() ?? 0}|{(ushort)(((PreviewProtectionProfile)P_1)?.GetProtectionFlags() ?? ((PreviewProtectionFlags)0))}");
	}

	private static bool lkKQZDEmno(object P_0)
	{
		if (P_0 != null && ((ProtectedPreviewLicenseState)P_0).GetLicenseValid() && ((ProtectedPreviewLicenseState)P_0).teRAy9Ly3C() == 0 && ((ProtectedPreviewLicenseState)P_0).zJtMZ3Tf0W() == 1 && !string.IsNullOrWhiteSpace(((ProtectedPreviewLicenseState)P_0).GetLookupId()) && !string.IsNullOrWhiteSpace(((ProtectedPreviewLicenseState)P_0).GetLicenseId()) && !string.IsNullOrWhiteSpace(((ProtectedPreviewLicenseState)P_0).GetValidationTranscriptHead()))
		{
			return lk2QnnOemt(P_0);
		}
		return false;
	}

	private static bool DW2QOahGnG(object P_0)
	{
		byte[] array = LicenseCryptography.DecodeHex(((ProtectedPreviewLicenseState)P_0)?.GetStateFingerprint());
		if (P_0 != null && ((ProtectedPreviewLicenseState)P_0).GetLicenseValid() && ((ProtectedPreviewLicenseState)P_0).teRAy9Ly3C() == 0 && ((ProtectedPreviewLicenseState)P_0).GetSessionUtcTicks() > 0L && array != null && array.Length >= 16 && !string.IsNullOrWhiteSpace(((ProtectedPreviewLicenseState)P_0).GbnA4l2khJ()) && !string.IsNullOrWhiteSpace(((ProtectedPreviewLicenseState)P_0).GetStateFingerprint()))
		{
			return lk2QnnOemt(P_0);
		}
		return false;
	}

	private static bool zipQh6jGRo(object P_0)
	{
		if (P_0 != null && ((ProtectedPreviewLicenseState)P_0).GetLicenseValid() && ((ProtectedPreviewLicenseState)P_0).teRAy9Ly3C() == 0 && ((ProtectedPreviewLicenseState)P_0).zJtMZ3Tf0W() == 1 && string.IsNullOrWhiteSpace(((ProtectedPreviewLicenseState)P_0).GetStatusMessage()) && !string.IsNullOrWhiteSpace(((ProtectedPreviewLicenseState)P_0).GetProjectScopeFingerprint()) && !string.IsNullOrWhiteSpace(((ProtectedPreviewLicenseState)P_0).GetAssemblyFingerprint()))
		{
			return lk2QnnOemt(P_0);
		}
		return false;
	}

	private static bool lk2QnnOemt(object P_0)
	{
		if (P_0 == null)
		{
			return false;
		}
		return string.Equals(LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).Wo3Mnj7DeV()), LicenseCryptography.NormalizeHexString(svRQB45klR(P_0)), StringComparison.Ordinal);
	}

	private static bool sc7QpJ5FUr(object P_0, object P_1)
	{
		if (P_0 != null && P_1 != null)
		{
			if (((PreviewProtectionProfile)P_0).Version == ((PreviewProtectionProfile)P_1).Version && ((PreviewProtectionProfile)P_0).GetStableSeed() == ((PreviewProtectionProfile)P_1).GetStableSeed() && ((PreviewProtectionProfile)P_0).GetSessionSeed() == ((PreviewProtectionProfile)P_1).GetSessionSeed() && ((PreviewProtectionProfile)P_0).GvsTSpIpvK() == ((PreviewProtectionProfile)P_1).GvsTSpIpvK() && ((PreviewProtectionProfile)P_0).auwTE03s0j() == ((PreviewProtectionProfile)P_1).auwTE03s0j() && ((PreviewProtectionProfile)P_0).xpdTxouQpO() == ((PreviewProtectionProfile)P_1).xpdTxouQpO() && ((PreviewProtectionProfile)P_0).w3RTbRHx9B() == ((PreviewProtectionProfile)P_1).w3RTbRHx9B() && ((PreviewProtectionProfile)P_0).gjjTWI68cj() == ((PreviewProtectionProfile)P_1).gjjTWI68cj() && ((PreviewProtectionProfile)P_0).WFaTjh9q7a() == ((PreviewProtectionProfile)P_1).WFaTjh9q7a() && ((PreviewProtectionProfile)P_0).vL5T4Sw8Hv() == ((PreviewProtectionProfile)P_1).vL5T4Sw8Hv() && ((PreviewProtectionProfile)P_0).naLTgkaVgS() == ((PreviewProtectionProfile)P_1).naLTgkaVgS() && ((PreviewProtectionProfile)P_0).GetProtectionFlags() == ((PreviewProtectionProfile)P_1).GetProtectionFlags() && string.Equals(((PreviewProtectionProfile)P_0).OFlTvUd2HH(), ((PreviewProtectionProfile)P_1).OFlTvUd2HH(), StringComparison.Ordinal) && string.Equals(((PreviewProtectionProfile)P_0).aurT8rbs9i(), ((PreviewProtectionProfile)P_1).aurT8rbs9i(), StringComparison.Ordinal) && string.Equals(LicenseCryptography.NormalizeHexString(((PreviewProtectionProfile)P_0).z9eTyqhVJb()), LicenseCryptography.NormalizeHexString(((PreviewProtectionProfile)P_1).z9eTyqhVJb()), StringComparison.Ordinal))
			{
				return string.Equals(LicenseCryptography.NormalizeHexString(((PreviewProtectionProfile)P_0).TnOAZhDjDu()), LicenseCryptography.NormalizeHexString(((PreviewProtectionProfile)P_1).TnOAZhDjDu()), StringComparison.Ordinal);
			}
			return false;
		}
		return false;
	}

	private static ProtectedPreviewLicenseState CreateValidPreviewLicenseState(object P_0)
	{
		DateTime dateTime = LicenseCryptography.ParseUtcDateTime(((LicenseEvaluationContext)P_0)?.GetLicenseCache()?.ClockHighWaterUtc, LicenseCryptography.ParseUtcDateTime(((LicenseEvaluationContext)P_0)?.GetLicenseCache()?.LastVerifiedUtc, DateTime.UtcNow));
		string text = LicenseCryptography.HashProjectScope(LicenseStoragePaths.GetProjectRootDirectory());
		string text2 = LicenseCryptography.GetAssemblyFingerprint();
		ProtectedPreviewLicenseState vDmn1VAsK5dMbjWdF = new ProtectedPreviewLicenseState();
		vDmn1VAsK5dMbjWdF.SetFeatureName("Main");
		vDmn1VAsK5dMbjWdF.SetLicenseValid(true);
		object obj;
		if (P_0 == null)
		{
			obj = null;
		}
		else
		{
			obj = ((LicenseEvaluationContext)P_0).NVQTnvlJrJ();
			if (obj != null)
			{
				goto IL_0081;
			}
		}
		obj = string.Empty;
		goto IL_0081;
		IL_0081:
		vDmn1VAsK5dMbjWdF.SetStateFingerprint((string)obj);
		object obj2;
		if (P_0 == null)
		{
			obj2 = null;
		}
		else
		{
			PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument = ((LicenseEvaluationContext)P_0).GetLicenseCache();
			if (psdReaderLicenseCacheDocument != null)
			{
				obj2 = psdReaderLicenseCacheDocument.LookupId;
				if (obj2 != null)
				{
					goto IL_00a8;
				}
			}
			else
			{
				obj2 = null;
			}
		}
		obj2 = string.Empty;
		goto IL_00a8;
		IL_00cf:
		object obj3;
		vDmn1VAsK5dMbjWdF.SetLicenseId((string)obj3);
		vDmn1VAsK5dMbjWdF.SetRevision((((LicenseEvaluationContext)P_0)?.GetLicenseCache()?.Revision).GetValueOrDefault());
		object obj4;
		if (P_0 == null)
		{
			obj4 = null;
		}
		else
		{
			PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument2 = ((LicenseEvaluationContext)P_0).GetLicenseCache();
			if (psdReaderLicenseCacheDocument2 == null)
			{
				obj4 = null;
			}
			else
			{
				obj4 = psdReaderLicenseCacheDocument2.ValidationTranscriptHead;
				if (obj4 != null)
				{
					goto IL_0134;
				}
			}
		}
		obj4 = string.Empty;
		goto IL_0134;
		IL_00a8:
		vDmn1VAsK5dMbjWdF.SetLookupId((string)obj2);
		if (P_0 == null)
		{
			obj3 = null;
		}
		else
		{
			PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument3 = ((LicenseEvaluationContext)P_0).GetLicenseCache();
			if (psdReaderLicenseCacheDocument3 == null)
			{
				obj3 = null;
			}
			else
			{
				obj3 = psdReaderLicenseCacheDocument3.LicenseId;
				if (obj3 != null)
				{
					goto IL_00cf;
				}
			}
		}
		obj3 = string.Empty;
		goto IL_00cf;
		IL_0134:
		vDmn1VAsK5dMbjWdF.SetValidationTranscriptHead((string)obj4);
		vDmn1VAsK5dMbjWdF.SetProjectScopeFingerprint(text);
		vDmn1VAsK5dMbjWdF.SetAssemblyFingerprint(text2);
		vDmn1VAsK5dMbjWdF.SetClockHighWaterUtc(dateTime);
		vDmn1VAsK5dMbjWdF.SetSessionUtcTicks(DateTime.UtcNow.Ticks);
		vDmn1VAsK5dMbjWdF.sygAzjaVdv(0);
		vDmn1VAsK5dMbjWdF.xk6MODxciN(1);
		vDmn1VAsK5dMbjWdF.SetStatusMessage(string.Empty);
		vDmn1VAsK5dMbjWdF.kp9AfJH9vi(P5lQ5WMN3G(vDmn1VAsK5dMbjWdF));
		vDmn1VAsK5dMbjWdF.AWnMp3vs8l(svRQB45klR(vDmn1VAsK5dMbjWdF));
		return vDmn1VAsK5dMbjWdF;
	}

	private static ProtectedPreviewLicenseState CreateInvalidPreviewLicenseState()
	{
		NuMHmOksdm();
		LicenseStatusSnapshot mFR4ptunmu6ucGmoRvG = flxHD5TW8h ?? CreateStatusSnapshot((LicenseValidationStatus)4, "尚未激活 Psd2UGUI 授权。");
		string text = LicenseCryptography.HashProjectScope(LicenseStoragePaths.GetProjectRootDirectory());
		string text2 = LicenseCryptography.GetAssemblyFingerprint();
		long ticks = DateTime.UtcNow.Ticks;
		PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument = rdfHVoGDWI;
		object obj;
		if (psdReaderLicenseCacheDocument == null)
		{
			obj = null;
		}
		else
		{
			obj = psdReaderLicenseCacheDocument.LookupId;
			if (obj != null)
			{
				goto IL_0064;
			}
		}
		obj = mFR4ptunmu6ucGmoRvG.GetLookupId() ?? string.Empty;
		goto IL_0064;
		IL_008b:
		object obj2;
		string text3 = (string)obj2;
		int num = rdfHVoGDWI?.Revision ?? mFR4ptunmu6ucGmoRvG.GetRevision();
		string text5;
		string text4 = LicenseCryptography.ComputeStringSha256Hex(string.Format("{0}|DeadLease|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}", "psd2ugui", LicenseCryptography.GetProtocolVersion(), mFR4ptunmu6ucGmoRvG.GetValidationStatus(), text5, text3, ticks, num, LicenseCryptography.NormalizeHexString(text), LicenseCryptography.NormalizeHexString(text2)));
		string text6 = (string.IsNullOrWhiteSpace(rdfHVoGDWI?.ValidationTranscriptHead) ? LicenseCryptography.ComputeStringSha256Hex($"{text4}|dead|transcript|{mFR4ptunmu6ucGmoRvG.GetValidationStatus()}|{ticks}") : rdfHVoGDWI.ValidationTranscriptHead);
		DateTime dateTime = LicenseCryptography.ParseUtcDateTime(rdfHVoGDWI?.ClockHighWaterUtc, LicenseCryptography.ParseUtcDateTime(rdfHVoGDWI?.LastVerifiedUtc, DateTime.UtcNow));
		byte[] array = LicenseCryptography.ComputeStringSha256(text4);
		int num2 = (int)((array == null || array.Length < 4) ? 64 : (32 + BitConverter.ToUInt32(array, 0) % 96));
		ProtectedPreviewLicenseState vDmn1VAsK5dMbjWdF = new ProtectedPreviewLicenseState();
		vDmn1VAsK5dMbjWdF.SetFeatureName("Main");
		vDmn1VAsK5dMbjWdF.SetLicenseValid(false);
		vDmn1VAsK5dMbjWdF.SetStateFingerprint(text4);
		vDmn1VAsK5dMbjWdF.SetLookupId(text5);
		vDmn1VAsK5dMbjWdF.SetLicenseId(text3);
		vDmn1VAsK5dMbjWdF.SetRevision(num);
		vDmn1VAsK5dMbjWdF.SetValidationTranscriptHead(text6);
		vDmn1VAsK5dMbjWdF.SetProjectScopeFingerprint(text);
		vDmn1VAsK5dMbjWdF.SetAssemblyFingerprint(text2);
		vDmn1VAsK5dMbjWdF.SetClockHighWaterUtc(dateTime);
		vDmn1VAsK5dMbjWdF.SetSessionUtcTicks(ticks);
		vDmn1VAsK5dMbjWdF.sygAzjaVdv(num2);
		vDmn1VAsK5dMbjWdF.xk6MODxciN(1);
		vDmn1VAsK5dMbjWdF.SetStatusMessage(LicenseStatusPresentation.GetDetailedStatusMessage(mFR4ptunmu6ucGmoRvG));
		vDmn1VAsK5dMbjWdF.kp9AfJH9vi(P5lQ5WMN3G(vDmn1VAsK5dMbjWdF));
		vDmn1VAsK5dMbjWdF.AWnMp3vs8l(svRQB45klR(vDmn1VAsK5dMbjWdF));
		return vDmn1VAsK5dMbjWdF;
		IL_0064:
		text5 = (string)obj;
		PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument2 = rdfHVoGDWI;
		if (psdReaderLicenseCacheDocument2 != null)
		{
			obj2 = psdReaderLicenseCacheDocument2.LicenseId;
			if (obj2 != null)
			{
				goto IL_008b;
			}
		}
		else
		{
			obj2 = null;
		}
		obj2 = mFR4ptunmu6ucGmoRvG.GetLicenseId() ?? string.Empty;
		goto IL_008b;
	}

	private static string P5lQ5WMN3G(object P_0)
	{
		if (P_0 != null)
		{
			if (wsF3ZHYCUB > 0)
			{
				if (string.IsNullOrWhiteSpace(dW03hWKoau))
				{
					dW03hWKoau = LicenseCryptography.ComputeStringSha256Hex(string.Format("{0}|BuildLedger|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}", "psd2ugui", LicenseCryptography.GetProtocolVersion(), LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetValidationTranscriptHead()), LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetLookupId()), LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetLicenseId()), LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetProjectScopeFingerprint()), LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetAssemblyFingerprint()), ((ProtectedPreviewLicenseState)P_0).GetSessionUtcTicks(), ((ProtectedPreviewLicenseState)P_0).GetRevision(), uxhHRepIhf.Count, PiLH8JL5Zc.Count, RPf3Oclfhp, ((ProtectedPreviewLicenseState)P_0).GetLicenseValid() ? 1 : 0));
				}
				return dW03hWKoau ?? string.Empty;
			}
			return LicenseCryptography.ComputeStringSha256Hex(string.Format("{0}|LeaseLedger|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}", "psd2ugui", LicenseCryptography.GetProtocolVersion(), LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetValidationTranscriptHead()), LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetLookupId()), LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetLicenseId()), LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetProjectScopeFingerprint()), LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetAssemblyFingerprint()), ((ProtectedPreviewLicenseState)P_0).GetSessionUtcTicks(), ((ProtectedPreviewLicenseState)P_0).GetRevision(), ((ProtectedPreviewLicenseState)P_0).GetLicenseValid() ? 1 : 0));
		}
		return string.Empty;
	}

	private static string svRQB45klR(object P_0)
	{
		if (P_0 == null)
		{
			return string.Empty;
		}
		return LicenseCryptography.ComputeStringSha256Hex(string.Format("{0}|ProtectionState|{1}|{2}|", "psd2ugui", 1, ((ProtectedPreviewLicenseState)P_0).GetLicenseValid() ? 1 : 0) + $"{LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetLookupId())}|{LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetLicenseId())}|{((ProtectedPreviewLicenseState)P_0).GetRevision()}|" + LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetValidationTranscriptHead()) + "|" + LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetProjectScopeFingerprint()) + "|" + LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0).GetAssemblyFingerprint()));
	}

	private static string TroQUxVkgG()
	{
		LicenseStatusSnapshot mFR4ptunmu6ucGmoRvG = flxHD5TW8h ?? CreateStatusSnapshot((LicenseValidationStatus)4, "尚未激活 Psd2UGUI 授权。");
		bool flag = LicenseStatusPresentation.GetAvailability(mFR4ptunmu6ucGmoRvG) == (LicenseAvailability)0;
		PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument = rdfHVoGDWI;
		object obj;
		if (psdReaderLicenseCacheDocument == null)
		{
			obj = null;
		}
		else
		{
			obj = psdReaderLicenseCacheDocument.LookupId;
			if (obj != null)
			{
				goto IL_0048;
			}
		}
		obj = mFR4ptunmu6ucGmoRvG.GetLookupId() ?? string.Empty;
		goto IL_0048;
		IL_00a4:
		object obj2;
		string text = (string)obj2;
		string text2 = LicenseCryptography.HashProjectScope(LicenseStoragePaths.GetProjectRootDirectory());
		string text3 = LicenseCryptography.GetAssemblyFingerprint();
		string text4;
		string text5;
		int num;
		return LicenseCryptography.ComputeStringSha256Hex(string.Format("{0}|ProtectionState|{1}|{2}|", "psd2ugui", 1, flag ? 1 : 0) + $"{LicenseCryptography.NormalizeHexString(text4)}|{LicenseCryptography.NormalizeHexString(text5)}|{num}|" + LicenseCryptography.NormalizeHexString(text) + "|" + LicenseCryptography.NormalizeHexString(text2) + "|" + LicenseCryptography.NormalizeHexString(text3));
		IL_0048:
		text4 = (string)obj;
		PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument2 = rdfHVoGDWI;
		object obj3;
		if (psdReaderLicenseCacheDocument2 == null)
		{
			obj3 = null;
		}
		else
		{
			obj3 = psdReaderLicenseCacheDocument2.LicenseId;
			if (obj3 != null)
			{
				goto IL_006f;
			}
		}
		obj3 = mFR4ptunmu6ucGmoRvG.GetLicenseId() ?? string.Empty;
		goto IL_006f;
		IL_006f:
		text5 = (string)obj3;
		num = rdfHVoGDWI?.Revision ?? mFR4ptunmu6ucGmoRvG.GetRevision();
		PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument3 = rdfHVoGDWI;
		if (psdReaderLicenseCacheDocument3 != null)
		{
			obj2 = psdReaderLicenseCacheDocument3.ValidationTranscriptHead;
			if (obj2 != null)
			{
				goto IL_00a4;
			}
		}
		else
		{
			obj2 = null;
		}
		obj2 = string.Empty;
		goto IL_00a4;
	}

	private static void hMHQ93OBLr()
	{
		string text;
		lock (HxaHki07OS)
		{
			text = TroQUxVkgG();
		}
		if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, GOt35kDaQo, StringComparison.Ordinal))
		{
			GOt35kDaQo = text;
			EditorLicenseOperationScope.ForceRelease();
			zWRQmkhdDS();
			myTQoENhDC();
		}
	}

	private static void zWRQmkhdDS()
	{
		try
		{
			Type type = wPcQJGeROO("UGF.EditorTools.Psd2UGUI.PsdLayerPreviewCache");
			object obj;
			if ((object)type == null)
			{
				obj = null;
			}
			else
			{
				obj = type.GetMethod("Clear", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
				((MethodBase)obj)?.Invoke((object)null, (object[])null);
			}
			Type type2 = wPcQJGeROO("UGF.EditorTools.Psd2UGUI.PsdLayerNode");
			if (type2 == null)
			{
				return;
			}
			MethodInfo methodInfo = type2.GetProperty("PreviewTexture", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetSetMethod(nonPublic: true);
			FieldInfo field = type2.GetField("<PreviewTexture>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
			UnityEngine.Object[] array = Resources.FindObjectsOfTypeAll(type2);
			if (array == null || array.Length == 0)
			{
				return;
			}
			foreach (UnityEngine.Object obj2 in array)
			{
				if (obj2 == null)
				{
					continue;
				}
				try
				{
					if (methodInfo != null)
					{
						methodInfo.Invoke(obj2, new object[1]);
					}
					else
					{
						field?.SetValue(obj2, null);
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

	private static void myTQoENhDC()
	{
		try
		{
			Type type = wPcQJGeROO("UGF.EditorTools.Psd2UGUI.Psd2UIFormConverter");
			if (!(type == null))
			{
				object obj = type.GetProperty("Instance", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)?.GetValue(null, null);
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

	private static Type wPcQJGeROO(object P_0)
	{
		if (string.IsNullOrWhiteSpace((string)P_0))
		{
			return null;
		}
		Type type = Type.GetType((string)P_0, throwOnError: false);
		if (type != null)
		{
			return type;
		}
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		int num = 0;
		while (true)
		{
			if (num < assemblies.Length)
			{
				type = assemblies[num].GetType((string)P_0, throwOnError: false);
				if (type != null)
				{
					break;
				}
				num++;
				continue;
			}
			return null;
		}
		return type;
	}

	private static byte[] gUBQuTuO5q(object P_0)
	{
		byte[] array = LicenseCryptography.DecodeHex(((ProtectedPreviewLicenseState)P_0)?.GetStateFingerprint());
		if (array == null || array.Length == 0)
		{
			array = LicenseCryptography.ComputeStringSha256("psd2ugui|RenderProfileKey|fallback|" + $"{LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0)?.GetLookupId())}|{LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0)?.GetLicenseId())}|{((ProtectedPreviewLicenseState)P_0)?.GetRevision() ?? 0}|" + LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0)?.GetProjectScopeFingerprint()) + "|" + LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0)?.GetAssemblyFingerprint()));
		}
		return LicenseCryptography.ComputeStringHmacSha256(array, "psd2ugui|RenderProfileKey|" + LicenseCryptography.GetProtocolVersion() + "|" + $"{LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0)?.GetLookupId())}|{LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0)?.GetLicenseId())}|{((ProtectedPreviewLicenseState)P_0)?.GetRevision() ?? 0}|" + LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0)?.GetProjectScopeFingerprint()) + "|" + LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0)?.GetAssemblyFingerprint()) + "|" + LicenseCryptography.NormalizeHexString(((ProtectedPreviewLicenseState)P_0)?.GbnA4l2khJ()));
	}

	private static PreviewProtectionFlags mGuQTav7YK(object P_0, object P_1)
	{
		return (PreviewProtectionFlags)131;
	}

	private static int ninQA8RLvj(object P_0, int P_1, int P_2)
	{
		if (P_0 != null && ((Array)P_0).Length >= P_1 + 4)
		{
			return BitConverter.ToInt32((byte[])P_0, P_1);
		}
		return P_2;
	}

	private static byte lbGQM6r6lK(object P_0, int P_1, byte P_2)
	{
		if (P_0 != null && P_1 >= 0 && P_1 < ((Array)P_0).Length)
		{
			return ((byte[])P_0)[P_1];
		}
		return P_2;
	}

	private static string EdSQQtms6L(object P_0)
	{
		if (!string.IsNullOrWhiteSpace((string)P_0))
		{
			return ((string)P_0).Replace("\\", "/").Trim();
		}
		return string.Empty;
	}

	private static bool rj4QHbVwQF(object P_0, object P_1, object P_2, bool P_3, EPcPUUEPrgAWTvPplYA P_4, object P_5, out string P_6, out LicenseEvaluationContext P_7)
	{
		P_7 = null;
		P_6 = string.Empty;
		PsdReaderLicenseClientConfigDocument psdReaderLicenseClientConfigDocument = St3HUbyQev();
		if (psdReaderLicenseClientConfigDocument == null)
		{
			P_6 = "当前授权组件初始化失败。";
			lock (HxaHki07OS)
			{
				flxHD5TW8h = CreateStatusSnapshot((LicenseValidationStatus)2, P_6);
				BCG316CFVC = 0;
			}
			dKVQ3T6I8j(P_4, null, P_2, (LicenseValidationStatus)2, null);
			return false;
		}
		string text = lyVH9yeWXW();
		if (!string.IsNullOrWhiteSpace(text))
		{
			lock (HxaHki07OS)
			{
				ub1HyAPIjL = psdReaderLicenseClientConfigDocument;
			}
			string text2 = LicenseCryptography.NormalizeOrderNumber(P_2);
			if (text2.Length != 19)
			{
				text2 = BTsQcwXXjP();
			}
			byte[] array = (byte[])((P_1 == null || ((Array)P_1).Length == 0) ? HADQ4X3eaX() : P_1);
			if (array != null && array.Length != 0)
			{
				string text3 = BacQNHOGDQ(P_5);
				if (!P_3 && AXJHJooQ5h(psdReaderLicenseClientConfigDocument, out P_7, out P_6))
				{
					lock (HxaHki07OS)
					{
						ub1HyAPIjL = eRbH58XT8E(P_7?.GetProductMetadata(), P_7?.GetLicenseCache(), psdReaderLicenseClientConfigDocument);
						flxHD5TW8h = CreateStatusSnapshot((LicenseValidationStatus)1, P_6, rdfHVoGDWI);
						BCG316CFVC = int.MinValue;
					}
					dKVQ3T6I8j(P_4, ub1HyAPIjL, text2, (LicenseValidationStatus)1, P_7);
					return true;
				}
				string text4 = fOxHMD36Od(psdReaderLicenseClientConfigDocument);
				zN9HQmWfvf(text4);
				if (!fxaQC0W875(psdReaderLicenseClientConfigDocument, text, P_0, array, text3, text4, out P_7, out P_6, out var LicenseValidationStatus))
				{
					if (e3lHo8bKG3(LicenseValidationStatus, P_6, out P_7, out var text5))
					{
						P_6 = text5;
						lock (HxaHki07OS)
						{
							ub1HyAPIjL = eRbH58XT8E(P_7?.GetProductMetadata(), P_7?.GetLicenseCache());
							flxHD5TW8h = CreateStatusSnapshot((LicenseValidationStatus)1, P_6, rdfHVoGDWI);
							BCG316CFVC = int.MinValue;
						}
						hMHQ93OBLr();
						dKVQ3T6I8j(P_4, ub1HyAPIjL, text2, (LicenseValidationStatus)1, P_7);
						return true;
					}
					PsdReaderLicenseClientConfigDocument psdReaderLicenseClientConfigDocument2;
					lock (HxaHki07OS)
					{
						flxHD5TW8h = CreateStatusSnapshot(LicenseValidationStatus, P_6, rdfHVoGDWI);
						BCG316CFVC = 0;
						psdReaderLicenseClientConfigDocument2 = eRbH58XT8E();
					}
					dKVQ3T6I8j(P_4, psdReaderLicenseClientConfigDocument2, text2, LicenseValidationStatus, null);
					return false;
				}
				if (P_7.GetLicenseCache() != null)
				{
					P_7.GetLicenseCache().Schema = 1;
					P_7.GetLicenseCache().ActivationSource = text3;
					P_7.GetLicenseCache().LastRepositoryRequestUtc = LnjHHLgS5c(text4);
					P_7.GetLicenseCache().OrderIdCipher = ((text2.Length == 19) ? LicenseCryptography.EncryptLocalCacheText(Encoding.UTF8.GetBytes(text2), P_7.GetLicenseCache().DeviceFingerprint) : string.Empty);
				}
				LicenseValidationStatus qjCAnAuOeKKje6af5WM2 = QEMHa6xMmE(P_7, "Main");
				if (qjCAnAuOeKKje6af5WM2 != (LicenseValidationStatus)1)
				{
					P_6 = rYtH6T5nWL(P_7, "Main");
					if (P_7.GetLicenseCache() == null)
					{
						lock (HxaHki07OS)
						{
							flxHD5TW8h = CreateStatusSnapshot(qjCAnAuOeKKje6af5WM2, P_6, rdfHVoGDWI);
							BCG316CFVC = 0;
						}
					}
					else
					{
						P_7.GetLicenseCache().LastResultCode = qjCAnAuOeKKje6af5WM2;
						P_7.GetLicenseCache().Message = P_6;
						lock (HxaHki07OS)
						{
							rdfHVoGDWI = P_7.GetLicenseCache();
							XiAH7XroGM.Save(rdfHVoGDWI);
							flxHD5TW8h = CreateStatusSnapshot(qjCAnAuOeKKje6af5WM2, P_6, P_7.GetLicenseCache());
							BCG316CFVC = 0;
						}
						hMHQ93OBLr();
					}
					dKVQ3T6I8j(P_4, eRbH58XT8E(P_7.GetProductMetadata(), P_7.GetLicenseCache()), text2, qjCAnAuOeKKje6af5WM2, P_7);
					return false;
				}
				lock (HxaHki07OS)
				{
					rdfHVoGDWI = P_7.GetLicenseCache();
					ub1HyAPIjL = eRbH58XT8E(P_7.GetProductMetadata(), P_7.GetLicenseCache());
					XiAH7XroGM.Save(rdfHVoGDWI);
					flxHD5TW8h = CreateStatusSnapshot((LicenseValidationStatus)1, "授权已更新。", rdfHVoGDWI);
					BCG316CFVC = int.MinValue;
				}
				hMHQ93OBLr();
				dKVQ3T6I8j(P_4, ub1HyAPIjL, text2, (LicenseValidationStatus)1, P_7);
				P_6 = "授权已更新。";
				return true;
			}
			P_6 = "当前授权需要重新输入订单号完成验证。";
			PsdReaderLicenseClientConfigDocument psdReaderLicenseClientConfigDocument3;
			lock (HxaHki07OS)
			{
				flxHD5TW8h = CreateStatusSnapshot((LicenseValidationStatus)4, P_6, rdfHVoGDWI);
				BCG316CFVC = 0;
				psdReaderLicenseClientConfigDocument3 = eRbH58XT8E();
			}
			dKVQ3T6I8j(P_4, psdReaderLicenseClientConfigDocument3, text2, (LicenseValidationStatus)4, null);
			return false;
		}
		P_6 = "当前授权组件初始化失败。";
		lock (HxaHki07OS)
		{
			flxHD5TW8h = CreateStatusSnapshot((LicenseValidationStatus)2, P_6);
			BCG316CFVC = 0;
		}
		dKVQ3T6I8j(P_4, psdReaderLicenseClientConfigDocument, P_2, (LicenseValidationStatus)2, null);
		return false;
	}

	private static void dKVQ3T6I8j(EPcPUUEPrgAWTvPplYA P_0, object P_1, object P_2, LicenseValidationStatus P_3, object P_4)
	{
		string text = LicenseCryptography.NormalizeOrderNumber(P_2);
		string text2 = YknQ6BEh87(P_0, P_3);
		string text3 = EugQs6FUBu(text);
		if (P_1 == null || string.IsNullOrWhiteSpace(text2) || string.IsNullOrWhiteSpace(text3))
		{
			return;
		}
		if (P_0 == (EPcPUUEPrgAWTvPplYA)0 && P_3 == (LicenseValidationStatus)1 && ((LicenseEvaluationContext)P_4)?.GetLicenseCache() != null)
		{
			string text4 = LicenseCryptography.NormalizeHexString(((LicenseEvaluationContext)P_4).GetLicenseCache().LookupId);
			string item = text3 + "|" + text4 + "|" + ((LicenseEvaluationContext)P_4).GetLicenseCache().Revision + "|" + (((LicenseEvaluationContext)P_4).GetLicenseCache().StatusText ?? string.Empty);
			lock (HxaHki07OS)
			{
				if (wsF3ZHYCUB > 0 && !PiLH8JL5Zc.Add(item))
				{
					return;
				}
			}
		}
		v3EHdhhO7g.QueueTrackingEvent((PsdReaderLicenseClientConfigDocument)P_1, text3, text2);
	}

	private static string EugQs6FUBu(object P_0)
	{
		string text = LicenseCryptography.NormalizeOrderNumber(P_0);
		if (text.Length == 19)
		{
			return text;
		}
		return wJaMcVsuYn(text);
	}

	private static string f5wQrmv1WO(object P_0, EPcPUUEPrgAWTvPplYA P_1, LicenseValidationStatus P_2)
	{
		if (((string)P_0).Length == 19)
		{
			return (string)P_0;
		}
		string text = OUWQF3xlKW(P_1);
		if (!string.IsNullOrWhiteSpace(text))
		{
			return Yt6QaI9KhG(text, P_0, P_2);
		}
		return string.Empty;
	}

	private static string OUWQF3xlKW(EPcPUUEPrgAWTvPplYA P_0)
	{
		return P_0 switch
		{
			(EPcPUUEPrgAWTvPplYA)0 => hKgQ0Z59eD(null), 
			(EPcPUUEPrgAWTvPplYA)1 => "order", 
			(EPcPUUEPrgAWTvPplYA)2 => "project_file", 
			_ => string.Empty, 
		};
	}

	private static string Yt6QaI9KhG(object P_0, object P_1, LicenseValidationStatus P_2)
	{
		string text = LicenseCryptography.HashDeviceIdentifier(LicenseCryptography.NormalizeDeviceIdentifier(SystemInfo.deviceUniqueIdentifier));
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}
		string text2 = (string.Equals((string)P_0, "order", StringComparison.Ordinal) ? "ord" : ((!string.Equals((string)P_0, "project_file", StringComparison.Ordinal)) ? "unk" : "bin"));
		string text3 = LicenseCryptography.NormalizeHexString(LicenseCryptography.ComputeStringSha256Hex(string.Format("PsdReader|ValidationTelemetry|v2|{0}|{1}|{2}|{3}|{4}", "psd2ugui", text2, P_2, P_1, text)));
		if (text3.Length > 16)
		{
			text3 = text3.Substring(0, 16);
		}
		return "anom_" + text2 + "_" + text3;
	}

	private static string YknQ6BEh87(EPcPUUEPrgAWTvPplYA P_0, LicenseValidationStatus P_1)
	{
		return P_0 switch
		{
			(EPcPUUEPrgAWTvPplYA)0 => fmxQSRkW2c(hKgQ0Z59eD(null), P_1), 
			(EPcPUUEPrgAWTvPplYA)1 => fmxQSRkW2c("order", P_1), 
			(EPcPUUEPrgAWTvPplYA)2 => fmxQSRkW2c("project_file", P_1), 
			_ => string.Empty, 
		};
	}

	private static string fmxQSRkW2c(object P_0, LicenseValidationStatus P_1)
	{
		string text = CCrQLiOqC4(P_0);
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}
		return text + "_" + RCuQEwtTXo(P_1);
	}

	private static string CCrQLiOqC4(object P_0)
	{
		string a = hKgQ0Z59eD(P_0);
		if (string.Equals(a, "order", StringComparison.Ordinal))
		{
			return "lic_adim";
		}
		if (!string.Equals(a, "project_file", StringComparison.Ordinal))
		{
			return string.Empty;
		}
		return "lic";
	}

	private static string hKgQ0Z59eD(object P_0)
	{
		string a = ((string)(P_0 ?? string.Empty)).Trim();
		object obj;
		if (!string.Equals(a, "order", StringComparison.Ordinal))
		{
			if (string.Equals(a, "project_file", StringComparison.Ordinal))
			{
				return "project_file";
			}
			PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument = rdfHVoGDWI;
			if (psdReaderLicenseCacheDocument != null)
			{
				obj = psdReaderLicenseCacheDocument.ActivationSource;
				if (obj != null)
				{
					goto IL_004f;
				}
			}
			else
			{
				obj = null;
			}
			obj = string.Empty;
			goto IL_004f;
		}
		return "order";
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

	private static string RCuQEwtTXo(LicenseValidationStatus P_0)
	{
		return P_0 switch
		{
			(LicenseValidationStatus)1 => "ok", 
			(LicenseValidationStatus)2 => "config", 
			(LicenseValidationStatus)3 => "invalid", 
			(LicenseValidationStatus)4 => "cache", 
			(LicenseValidationStatus)5 => "network", 
			(LicenseValidationStatus)6 => "metadata", 
			(LicenseValidationStatus)7 => "missing", 
			(LicenseValidationStatus)8 => "signature", 
			(LicenseValidationStatus)9 => "product", 
			(LicenseValidationStatus)10 => "suspended", 
			(LicenseValidationStatus)11 => "revoked", 
			(LicenseValidationStatus)12 => "expired", 
			(LicenseValidationStatus)13 => "feature", 
			(LicenseValidationStatus)14 => "device", 
			(LicenseValidationStatus)15 => "offline", 
			_ => "unknown", 
		};
	}

	private static bool fxaQC0W875(object P_0, object P_1, object P_2, object P_3, object P_4, object P_5, out LicenseEvaluationContext P_6, out string P_7, out LicenseValidationStatus P_8)
	{
		P_6 = null;
		P_7 = string.Empty;
		P_8 = (LicenseValidationStatus)5;
		string text = WxPHEnoGHd();
		string text2 = w6KHCaO90d();
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
		if (rPHHYjODPh<PsdReaderProductMetaPayloadDocument>(P_0, P_1, text, text2, LicenseCryptography.DeriveRepositoryDocumentKey(text2), out var psdReaderProductMetaPayloadDocument, out var array, out var flag, out var oK9cinEpCPEorEb3ebh))
		{
			if (EX9HWL1pkA(psdReaderProductMetaPayloadDocument, out P_7, out P_8))
			{
				PsdReaderLicenseClientConfigDocument psdReaderLicenseClientConfigDocument = eRbH58XT8E(psdReaderProductMetaPayloadDocument, rdfHVoGDWI, (PsdReaderLicenseClientConfigDocument)P_0);
				if (psdReaderLicenseClientConfigDocument != null && psdReaderLicenseClientConfigDocument.RepositoryBaseUrls != null && psdReaderLicenseClientConfigDocument.RepositoryBaseUrls.Length != 0)
				{
					string text3 = knWHqkp6aB(P_2);
					string text4 = tfdHx2SF3F(P_2);
					if (!CX2H4D8HhS<PsdReaderLicensePayloadDocument>(psdReaderLicenseClientConfigDocument, psdReaderProductMetaPayloadDocument.ActiveLeafPublicKeyPem, psdReaderProductMetaPayloadDocument.ActiveKid, text3, text4, LicenseCryptography.DeriveLicensePayloadKey(P_2, P_3), out psdReaderLicensePayloadDocument, out array2, out var flag2, out var oK9cinEpCPEorEb3ebh2))
					{
						P_7 = (flag2 ? "当前订单对应的授权文件不存在。" : "读取授权文件失败。");
						P_8 = (flag2 ? ((LicenseValidationStatus)7) : A2LHNMYwoZ(oK9cinEpCPEorEb3ebh2));
						return false;
					}
					if (!tKPHi4cvag(psdReaderLicensePayloadDocument, psdReaderProductMetaPayloadDocument, P_2, out P_7, out P_8))
					{
						return false;
					}
					string text5 = FT6HIqixFs(P_2);
					string text6 = sDpHG7EPhx(P_2);
					if (CX2H4D8HhS<PsdReaderOfflineLeasePayloadDocument>(psdReaderLicenseClientConfigDocument, psdReaderProductMetaPayloadDocument.ActiveLeafPublicKeyPem, psdReaderProductMetaPayloadDocument.ActiveKid, text5, text6, LicenseCryptography.DeriveOfflineLeaseKey(P_2, P_3), out var psdReaderOfflineLeasePayloadDocument, out array3, out var flag3, out var oK9cinEpCPEorEb3ebh3))
					{
						if (!X5jHjtF74D(psdReaderOfflineLeasePayloadDocument, psdReaderLicensePayloadDocument, psdReaderProductMetaPayloadDocument, P_2, out P_7, out P_8))
						{
							return false;
						}
						TSHHv3vRBj.GetDeviceIdHash(out text7);
						if (string.IsNullOrWhiteSpace(text7))
						{
							P_7 = "当前设备标识不可用。";
							P_8 = (LicenseValidationStatus)2;
							return false;
						}
						text8 = string.Empty;
						if (t8xQxeQAqS(psdReaderLicenseClientConfigDocument, psdReaderProductMetaPayloadDocument, text7, out var psdReaderDeviceShardPayloadDocument, out var array4, out var flag4, out var oK9cinEpCPEorEb3ebh4))
						{
							string text9 = Y6cHefyx0Y(text7);
							if (!FYpHK838GR(psdReaderDeviceShardPayloadDocument, text9, out P_7, out P_8))
							{
								return false;
							}
							if (IdOQqXe2xu(text7, psdReaderDeviceShardPayloadDocument))
							{
								P_7 = "当前设备已被限制使用此授权。";
								P_8 = (LicenseValidationStatus)14;
								return false;
							}
							text8 = LicenseCryptography.EncodeBase64Url(array4);
						}
						else if (!flag4)
						{
							P_7 = "读取设备分片失败。";
							P_8 = A2LHNMYwoZ(oK9cinEpCPEorEb3ebh4);
							return false;
						}
						text10 = DateTime.UtcNow.ToString("O");
						psdReaderLicensePayloadDocument2 = psdReaderLicensePayloadDocument;
						psdReaderProductMetaPayloadDocument2 = psdReaderProductMetaPayloadDocument;
						psdReaderOfflineLeasePayloadDocument2 = psdReaderOfflineLeasePayloadDocument;
						text11 = text7;
						text12 = LicenseCryptography.EncryptLocalCacheText(P_3, text7);
						PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument = rdfHVoGDWI;
						if (psdReaderLicenseCacheDocument == null)
						{
							obj = null;
						}
						else
						{
							obj = psdReaderLicenseCacheDocument.OrderIdCipher;
							if (obj != null)
							{
								goto IL_0216;
							}
						}
						obj = string.Empty;
						goto IL_0216;
					}
					P_7 = ((!flag3) ? "读取离线授权租约失败。" : "离线授权租约不存在。");
					P_8 = ((!flag3) ? A2LHNMYwoZ(oK9cinEpCPEorEb3ebh3) : ((LicenseValidationStatus)8));
					return false;
				}
				P_7 = "当前授权仓库配置不可用。";
				P_8 = (LicenseValidationStatus)2;
				return false;
			}
			return false;
		}
		P_7 = (flag ? "产品元数据不存在。" : "读取产品元数据失败。");
		P_8 = ((!flag) ? A2LHNMYwoZ(oK9cinEpCPEorEb3ebh) : ((LicenseValidationStatus)6));
		return false;
		IL_0216:
		PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument2 = hyHQeOE8Al(psdReaderLicensePayloadDocument2, psdReaderProductMetaPayloadDocument2, psdReaderOfflineLeasePayloadDocument2, text11, text12, obj, P_4, LnjHHLgS5c(P_5), text10, LicenseCryptography.EncodeBase64Url(array), LicenseCryptography.EncodeBase64Url(array2), LicenseCryptography.EncodeBase64Url(array3), text8);
		LicenseEvaluationContext lxqZcsud7O9XCugf7PT = new LicenseEvaluationContext();
		lxqZcsud7O9XCugf7PT.SetLicensePayload(psdReaderLicensePayloadDocument);
		lxqZcsud7O9XCugf7PT.SetLicenseCache(psdReaderLicenseCacheDocument2);
		lxqZcsud7O9XCugf7PT.SetProductMetadata(psdReaderProductMetaPayloadDocument);
		lxqZcsud7O9XCugf7PT.y9dTpPFQwG(LicenseCryptography.ComputeLicenseStateFingerprint(psdReaderLicensePayloadDocument, psdReaderLicenseCacheDocument2.CurrentMajorVersion, psdReaderLicenseCacheDocument2.LocalCacheTimeoutDays, psdReaderLicenseCacheDocument2.MetaRevision, psdReaderLicenseCacheDocument2.ActiveKid, text7, psdReaderProductMetaPayloadDocument.StateSalt));
		lxqZcsud7O9XCugf7PT.dmnT5ljly4(LroQGWXQiS(psdReaderLicensePayloadDocument, psdReaderLicenseCacheDocument2, "Main"));
		P_6 = lxqZcsud7O9XCugf7PT;
		P_7 = "授权远端验证成功。";
		P_8 = (LicenseValidationStatus)1;
		return true;
	}

	private static bool IdOQqXe2xu(object P_0, object P_1)
	{
		_003C_003Ec__DisplayClass96_0 CS_0024_003C_003E8__locals2 = new _003C_003Ec__DisplayClass96_0();
		if (!string.IsNullOrWhiteSpace((string)P_0) && ((PsdReaderDeviceShardPayloadDocument)P_1)?.BlockedTargets != null && ((PsdReaderDeviceShardPayloadDocument)P_1).BlockedTargets.Length != 0)
		{
			CS_0024_003C_003E8__locals2.goHEU2bmrC = LicenseCryptography.NormalizeHexString(P_0);
			return ((PsdReaderDeviceShardPayloadDocument)P_1).BlockedTargets.Any((string candidate) => string.Equals(LicenseCryptography.NormalizeHexString(candidate), CS_0024_003C_003E8__locals2.goHEU2bmrC, StringComparison.Ordinal));
		}
		return false;
	}

	private static bool t8xQxeQAqS(object P_0, object P_1, object P_2, out PsdReaderDeviceShardPayloadDocument P_3, out byte[] P_4, out bool P_5, out OK9cinEpCPEorEb3ebh P_6)
	{
		string text = Y6cHefyx0Y(P_2);
		string text2 = sfTHbVB21Z(text);
		string text3 = bf2Hw5rvjk(text);
		return CX2H4D8HhS<PsdReaderDeviceShardPayloadDocument>(P_0, ((PsdReaderProductMetaPayloadDocument)P_1).ActiveLeafPublicKeyPem, ((PsdReaderProductMetaPayloadDocument)P_1).ActiveKid, text2, text3, LicenseCryptography.DeriveRepositoryDocumentKey(text3), out P_3, out P_4, out P_5, out P_6, true);
	}

	private static int yaAQILWlqD(object P_0, object P_1, int P_2)
	{
		PsdReaderLicensePayloadDocument psdReaderLicensePayloadDocument = ((LicenseEvaluationContext)P_0).GetLicensePayload();
		PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument = ((LicenseEvaluationContext)P_0).GetLicenseCache();
		uint num = psdReaderLicensePayloadDocument?.FeatureMask ?? 0;
		bool flag = LicenseFeatureMask.ContainsFeature(num, P_1);
		bool flag2 = (psdReaderLicensePayloadDocument?.StatusCode ?? byte.MaxValue) == 0;
		bool flag3 = ADlHSx91cI(psdReaderLicensePayloadDocument) >= DateTime.UtcNow;
		bool flag4 = Rh2HpaJJ5Z((psdReaderLicenseCacheDocument != null) ? psdReaderLicenseCacheDocument.DeviceFingerprint : string.Empty);
		int b = psdReaderLicenseCacheDocument?.MetaRevision ?? 1;
		string text = ((psdReaderLicenseCacheDocument == null) ? string.Empty : psdReaderLicenseCacheDocument.LookupId);
		uint num2 = psdReaderLicenseCacheDocument?.CatalogMask ?? 0;
		int num3 = Mathf.Max(1, b) ^ gVmQbiKW4V(text) ^ (int)num2;
		int num4 = gVmQbiKW4V((psdReaderLicenseCacheDocument == null) ? string.Empty : psdReaderLicenseCacheDocument.ActiveKid);
		int num5 = LicenseCryptography.CombineStateIntegers(new int[5]
		{
			Mathf.Max(1, psdReaderLicenseCacheDocument?.LocalCacheTimeoutDays ?? 1),
			Mathf.Max(1, psdReaderLicenseCacheDocument?.CurrentMajorVersion ?? PsdProductVersionAccessor.GetCurrentMajorVersion()),
			Mathf.Max(1, psdReaderLicensePayloadDocument?.MaxMajorVersion ?? PsdProductVersionAccessor.GetCurrentMajorVersion()),
			num3,
			(int)num
		});
		return LicenseCryptography.CombineStateIntegers(new int[8]
		{
			(psdReaderLicensePayloadDocument?.Revision ?? 0) ^ P_2,
			(!flag) ? (~P_2) : P_2,
			(!flag2) ? 610839776 : 324508639,
			826366246,
			(!flag3) ? (-869029291) : 1437217740,
			(!flag4) ? 365382271 : 2135587861,
			num3 ^ gVmQbiKW4V((psdReaderLicenseCacheDocument == null) ? string.Empty : psdReaderLicenseCacheDocument.LicenseId),
			num4 ^ num5
		});
	}

	private static int LroQGWXQiS(object P_0, object P_1, object P_2)
	{
		int num = gVmQbiKW4V(P_2);
		int num2 = ((P_1 != null) ? (Mathf.Max(1, ((PsdReaderLicenseCacheDocument)P_1).MetaRevision) ^ gVmQbiKW4V(((PsdReaderLicenseCacheDocument)P_1).LookupId) ^ (int)((PsdReaderLicenseCacheDocument)P_1).CatalogMask) : 0);
		int num3 = gVmQbiKW4V((P_1 != null) ? ((PsdReaderLicenseCacheDocument)P_1).ActiveKid : string.Empty);
		int num4 = LicenseCryptography.CombineStateIntegers(new int[5]
		{
			Mathf.Max(1, ((PsdReaderLicenseCacheDocument)P_1)?.LocalCacheTimeoutDays ?? 1),
			Mathf.Max(1, ((PsdReaderLicenseCacheDocument)P_1)?.CurrentMajorVersion ?? PsdProductVersionAccessor.GetCurrentMajorVersion()),
			Mathf.Max(1, ((PsdReaderLicensePayloadDocument)P_0)?.MaxMajorVersion ?? PsdProductVersionAccessor.GetCurrentMajorVersion()),
			num2,
			(int)(((PsdReaderLicensePayloadDocument)P_0)?.FeatureMask ?? 0)
		});
		int[] obj = new int[8] { 0, 0, 324508639, 826366246, 1437217740, 2135587861, 0, 0 };
		obj[0] = (((PsdReaderLicensePayloadDocument)P_0)?.Revision ?? 0) ^ num;
		obj[1] = num;
		obj[6] = num2 ^ gVmQbiKW4V((P_1 == null) ? string.Empty : ((PsdReaderLicenseCacheDocument)P_1).LicenseId);
		obj[7] = num3 ^ num4;
		return LicenseCryptography.CombineStateIntegers(obj);
	}

	private static int gVmQbiKW4V(object P_0)
	{
		byte[] array = LicenseCryptography.ComputeStringSha256(P_0 ?? string.Empty);
		if (array.Length < 4)
		{
			return 0;
		}
		return BitConverter.ToInt32(array, 0);
	}

	private static LicenseEvaluationContext XWgQw1Pyh3(object P_0)
	{
		if (P_0 == null)
		{
			return null;
		}
		if (!Rh2HpaJJ5Z(((PsdReaderLicenseCacheDocument)P_0).DeviceFingerprint))
		{
			return null;
		}
		string text = lyVH9yeWXW();
		if (!string.IsNullOrWhiteSpace(text))
		{
			byte[] array = RaJQfUTF5K(P_0);
			if (array != null && array.Length != 0)
			{
				string text2 = w6KHCaO90d();
				if (!WlHQWA2Onh<PsdReaderProductMetaPayloadDocument>(((PsdReaderLicenseCacheDocument)P_0).MetaEnvelopeBase64, text, null, text2, LicenseCryptography.DeriveRepositoryDocumentKey(text2), out var psdReaderProductMetaPayloadDocument))
				{
					return null;
				}
				if (EX9HWL1pkA(psdReaderProductMetaPayloadDocument, out var text3, out var LicenseValidationStatus))
				{
					string text4 = ((!string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)P_0).LookupId)) ? ((PsdReaderLicenseCacheDocument)P_0).LookupId : ((PsdReaderLicenseCacheDocument)P_0).LicenseId);
					string text5 = tfdHx2SF3F(text4);
					if (!WlHQWA2Onh<PsdReaderLicensePayloadDocument>(((PsdReaderLicenseCacheDocument)P_0).LicenseEnvelopeBase64, psdReaderProductMetaPayloadDocument.ActiveLeafPublicKeyPem, psdReaderProductMetaPayloadDocument.ActiveKid, text5, LicenseCryptography.DeriveLicensePayloadKey(text4, array), out var psdReaderLicensePayloadDocument))
					{
						return null;
					}
					if (!tKPHi4cvag(psdReaderLicensePayloadDocument, psdReaderProductMetaPayloadDocument, text4, out text3, out LicenseValidationStatus))
					{
						return null;
					}
					string text6 = sDpHG7EPhx(text4);
					if (!WlHQWA2Onh<PsdReaderOfflineLeasePayloadDocument>(((PsdReaderLicenseCacheDocument)P_0).OfflineLeaseEnvelopeBase64, psdReaderProductMetaPayloadDocument.ActiveLeafPublicKeyPem, psdReaderProductMetaPayloadDocument.ActiveKid, text6, LicenseCryptography.DeriveOfflineLeaseKey(text4, array), out var psdReaderOfflineLeasePayloadDocument))
					{
						return null;
					}
					if (!X5jHjtF74D(psdReaderOfflineLeasePayloadDocument, psdReaderLicensePayloadDocument, psdReaderProductMetaPayloadDocument, text4, out text3, out LicenseValidationStatus))
					{
						return null;
					}
					TSHHv3vRBj.GetDeviceIdHash(out var text7);
					if (!string.IsNullOrWhiteSpace(text7))
					{
						if (NdoHsLKOZQ(P_0, text7, out var dateTime, out var dateTime2, out text3, out var text8))
						{
							DateTime utcNow = DateTime.UtcNow;
							if (!(dateTime > utcNow) && !(dateTime2 < utcNow))
							{
								if (!string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)P_0).DeviceShardEnvelopeBase64))
								{
									string text9 = bf2Hw5rvjk(Y6cHefyx0Y(text7));
									if (!WlHQWA2Onh<PsdReaderDeviceShardPayloadDocument>(((PsdReaderLicenseCacheDocument)P_0).DeviceShardEnvelopeBase64, psdReaderProductMetaPayloadDocument.ActiveLeafPublicKeyPem, psdReaderProductMetaPayloadDocument.ActiveKid, text9, LicenseCryptography.DeriveRepositoryDocumentKey(text9), out var psdReaderDeviceShardPayloadDocument))
									{
										return null;
									}
									if (!FYpHK838GR(psdReaderDeviceShardPayloadDocument, Y6cHefyx0Y(text7), out text8, out LicenseValidationStatus))
									{
										return null;
									}
									if (IdOQqXe2xu(text7, psdReaderDeviceShardPayloadDocument))
									{
										return null;
									}
								}
								PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument = hyHQeOE8Al(psdReaderLicensePayloadDocument, psdReaderProductMetaPayloadDocument, psdReaderOfflineLeasePayloadDocument, text7, ((PsdReaderLicenseCacheDocument)P_0).LicenseAccessKeyCipher, ((PsdReaderLicenseCacheDocument)P_0).OrderIdCipher, ((PsdReaderLicenseCacheDocument)P_0).ActivationSource, ((PsdReaderLicenseCacheDocument)P_0).LastRepositoryRequestUtc, ((PsdReaderLicenseCacheDocument)P_0).LastVerifiedUtc, ((PsdReaderLicenseCacheDocument)P_0).MetaEnvelopeBase64, ((PsdReaderLicenseCacheDocument)P_0).LicenseEnvelopeBase64, ((PsdReaderLicenseCacheDocument)P_0).OfflineLeaseEnvelopeBase64, ((PsdReaderLicenseCacheDocument)P_0).DeviceShardEnvelopeBase64);
								LicenseEvaluationContext lxqZcsud7O9XCugf7PT = new LicenseEvaluationContext();
								lxqZcsud7O9XCugf7PT.SetLicensePayload(psdReaderLicensePayloadDocument);
								lxqZcsud7O9XCugf7PT.SetLicenseCache(psdReaderLicenseCacheDocument);
								lxqZcsud7O9XCugf7PT.SetProductMetadata(psdReaderProductMetaPayloadDocument);
								lxqZcsud7O9XCugf7PT.y9dTpPFQwG(LicenseCryptography.ComputeLicenseStateFingerprint(psdReaderLicensePayloadDocument, Mathf.Max(1, psdReaderProductMetaPayloadDocument.CurrentMajorVersion), Mathf.Max(1, psdReaderLicenseCacheDocument.LocalCacheTimeoutDays), Mathf.Max(1, psdReaderProductMetaPayloadDocument.Revision), psdReaderLicenseCacheDocument.ActiveKid, text7, psdReaderProductMetaPayloadDocument.StateSalt));
								lxqZcsud7O9XCugf7PT.dmnT5ljly4(LroQGWXQiS(psdReaderLicensePayloadDocument, psdReaderLicenseCacheDocument, "Main"));
								return lxqZcsud7O9XCugf7PT;
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

	private static PsdReaderLicenseCacheDocument hyHQeOE8Al(object P_0, object P_1, object P_2, object P_3, object P_4, object P_5, object P_6, object P_7, object P_8, object P_9, object P_10, object P_11, object P_12)
	{
		((PsdReaderLicensePayloadDocument)P_0).Features = LicenseFeatureMask.DecodeFeatures(((PsdReaderLicensePayloadDocument)P_0).FeatureMask);
		((PsdReaderLicensePayloadDocument)P_0).Status = LicensePayloadBinaryReader.GetLicenseStatusText(((PsdReaderLicensePayloadDocument)P_0).StatusCode);
		DateTime dateTime = LicenseCryptography.ParseUtcDateTime(string.IsNullOrWhiteSpace((string)P_8) ? DateTime.UtcNow.ToString("O") : P_8, DateTime.UtcNow);
		DateTime dateTime2 = ADlHSx91cI(P_0);
		string supportUntilUtc = ((dateTime2 > DateTime.MinValue) ? dateTime2.ToString("O") : string.Empty);
		string text = dateTime.ToString("O");
		PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument = new PsdReaderLicenseCacheDocument
		{
			Schema = 1,
			LookupId = ((PsdReaderLicensePayloadDocument)P_0).LookupId,
			LicenseId = ((PsdReaderLicensePayloadDocument)P_0).LicenseId,
			StatusCode = ((PsdReaderLicensePayloadDocument)P_0).StatusCode,
			StatusText = (((PsdReaderLicensePayloadDocument)P_0).Status ?? string.Empty),
			Message = "授权有效。",
			Features = (((PsdReaderLicensePayloadDocument)P_0).Features ?? Array.Empty<string>()),
			FeatureMask = ((PsdReaderLicensePayloadDocument)P_0).FeatureMask,
			CatalogMask = ((PsdReaderProductMetaPayloadDocument)P_1).FeatureCatalogMask,
			LastVerifiedUtc = text,
			SupportUntilUtc = supportUntilUtc,
			ClockHighWaterUtc = text,
			Revision = ((PsdReaderLicensePayloadDocument)P_0).Revision,
			LocalCacheTimeoutDays = Math.Max(1, ((PsdReaderOfflineLeasePayloadDocument)P_2)?.OfflineCacheTimeoutDays ?? ((PsdReaderProductMetaPayloadDocument)P_1).LocalCacheTimeoutDays),
			CurrentMajorVersion = Math.Max(1, ((PsdReaderProductMetaPayloadDocument)P_1).CurrentMajorVersion),
			MaxMajorVersion = Math.Max(1, ((PsdReaderLicensePayloadDocument)P_0).MaxMajorVersion),
			DeviceFingerprint = (string)(P_3 ?? string.Empty),
			MetaRevision = Math.Max(1, ((PsdReaderProductMetaPayloadDocument)P_1).Revision),
			ActiveKid = (((PsdReaderProductMetaPayloadDocument)P_1).ActiveKid ?? string.Empty),
			LastResultCode = (LicenseValidationStatus)1,
			LicenseAccessKeyCipher = (string)(P_4 ?? string.Empty),
			OrderIdCipher = (string)(P_5 ?? string.Empty),
			GrantSeedCipher = string.Empty,
			StateSaltHex = ((((PsdReaderProductMetaPayloadDocument)P_1)?.StateSalt == null || ((PsdReaderProductMetaPayloadDocument)P_1).StateSalt.Length == 0) ? string.Empty : LicenseCryptography.EncodeHex(((PsdReaderProductMetaPayloadDocument)P_1).StateSalt)),
			TelemetryUrl = (((PsdReaderProductMetaPayloadDocument)P_1).TelemetryUrl ?? string.Empty),
			TelemetrySiteId = (((PsdReaderProductMetaPayloadDocument)P_1).TelemetrySiteId ?? string.Empty),
			ActivationSource = zM8QlK1koT(P_6),
			LastRepositoryRequestUtc = (string)(P_7 ?? string.Empty),
			MetaEnvelopeBase64 = (string)(P_9 ?? string.Empty),
			LicenseEnvelopeBase64 = (string)(P_10 ?? string.Empty),
			OfflineLeaseEnvelopeBase64 = (string)(P_11 ?? string.Empty),
			DeviceShardEnvelopeBase64 = (string)(P_12 ?? string.Empty)
		};
		string text2 = LicenseCryptography.ComputeCacheProof(psdReaderLicenseCacheDocument, P_3, dateTime, dateTime2);
		psdReaderLicenseCacheDocument.ValidationTranscriptHead = LicenseCryptography.ComputeTranscriptHead(psdReaderLicenseCacheDocument, P_3, dateTime, dateTime2, text2);
		psdReaderLicenseCacheDocument.ClockWatermarkCipher = LicenseCryptography.EncodeClockWatermark(dateTime, dateTime2, text2, psdReaderLicenseCacheDocument.ValidationTranscriptHead, P_3);
		return psdReaderLicenseCacheDocument;
	}

	private static bool WlHQWA2Onh<pP4l45QidSKK8M4ohD0>(object P_0, object P_1, object P_2, object P_3, object P_4, out pP4l45QidSKK8M4ohD0 P_5) where pP4l45QidSKK8M4ohD0 : class
	{
		P_5 = null;
		if (string.IsNullOrWhiteSpace((string)P_0))
		{
			return false;
		}
		try
		{
			byte[] array = LicenseCryptography.DecodeBase64Url(P_0);
			if (array != null && array.Length != 0)
			{
				if (LicenseCryptography.TryDecodeBinaryEnvelope(array, P_4, P_3, out var psdReaderSignedEnvelopeDocument) && psdReaderSignedEnvelopeDocument != null)
				{
					if (!string.IsNullOrWhiteSpace((string)P_2) && !string.Equals((string)P_2, psdReaderSignedEnvelopeDocument.Kid, StringComparison.Ordinal))
					{
						return false;
					}
					if (!LicenseCryptography.TryVerifyAndDecryptSignedEnvelope(P_1, psdReaderSignedEnvelopeDocument, P_3, P_4, out var array2))
					{
						return false;
					}
					return QY1HgeZETr<pP4l45QidSKK8M4ohD0>(array2, out P_5);
				}
				return false;
			}
			return false;
		}
		catch
		{
			P_5 = null;
			return false;
		}
	}

	private static bool UtrQKGS2AH(object P_0, out LicenseEvaluationContext P_1, out string P_2)
	{
		P_1 = null;
		P_2 = string.Empty;
		if (pnsHufAds0(rdfHVoGDWI))
		{
			P_1 = XWgQw1Pyh3(rdfHVoGDWI);
			if (P_1 != null)
			{
				return ztVQj7HcWe(P_0, P_1, out P_1, out P_2);
			}
			return false;
		}
		return false;
	}

	private static bool ztVQj7HcWe(object P_0, object P_1, out LicenseEvaluationContext P_2, out string P_3)
	{
		P_2 = (LicenseEvaluationContext)P_1;
		P_3 = string.Empty;
		if (P_2 != null)
		{
			LicenseEvaluationContext lxqZcsud7O9XCugf7PT = new LicenseEvaluationContext();
			lxqZcsud7O9XCugf7PT.SetLicensePayload(P_2.GetLicensePayload());
			lxqZcsud7O9XCugf7PT.SetLicenseCache(P_2.GetLicenseCache());
			lxqZcsud7O9XCugf7PT.SetProductMetadata(P_2.GetProductMetadata());
			lxqZcsud7O9XCugf7PT.y9dTpPFQwG(P_2.NVQTnvlJrJ());
			lxqZcsud7O9XCugf7PT.dmnT5ljly4(LroQGWXQiS(P_2.GetLicensePayload(), P_2.GetLicenseCache(), P_0));
			P_2 = lxqZcsud7O9XCugf7PT;
			LicenseValidationStatus LicenseValidationStatus = QEMHa6xMmE(P_2, P_0);
			int num = gVmQbiKW4V(P_0);
			int num2 = yaAQILWlqD(P_2, P_0, num);
			if (LicenseValidationStatus == (LicenseValidationStatus)1 && (num2 ^ P_2.RQlT2PAwIt()) == 0)
			{
				P_3 = "授权有效。";
				flxHD5TW8h = CreateStatusSnapshot((LicenseValidationStatus)1, P_3, P_2.GetLicenseCache());
				return true;
			}
			P_3 = rYtH6T5nWL(P_2, P_0);
			flxHD5TW8h = CreateStatusSnapshot((LicenseValidationStatus != (LicenseValidationStatus)1) ? LicenseValidationStatus : ynXHFOJKI8(P_2, P_0), P_3, P_2.GetLicenseCache());
			return false;
		}
		P_3 = "当前没有可用授权。";
		flxHD5TW8h = CreateStatusSnapshot((LicenseValidationStatus)4, P_3);
		return false;
	}

	private static bool bnbQYh426l(out string P_0)
	{
		if (ub1HyAPIjL == null)
		{
			ub1HyAPIjL = St3HUbyQev();
		}
		if (ub1HyAPIjL == null)
		{
			P_0 = "当前授权组件初始化失败。";
			flxHD5TW8h = CreateStatusSnapshot((LicenseValidationStatus)2, P_0);
			return false;
		}
		if (string.IsNullOrWhiteSpace(lyVH9yeWXW()))
		{
			P_0 = "当前授权组件初始化失败。";
			flxHD5TW8h = CreateStatusSnapshot((LicenseValidationStatus)2, P_0);
			return false;
		}
		if (flxHD5TW8h != null && flxHD5TW8h.GetValidationStatus() != 0 && flxHD5TW8h.GetValidationStatus() != (LicenseValidationStatus)1 && flxHD5TW8h.GetValidationStatus() != (LicenseValidationStatus)4)
		{
			P_0 = flxHD5TW8h.Message ?? string.Empty;
			return false;
		}
		P_0 = "尚未激活 Psd2UGUI 授权。";
		flxHD5TW8h = CreateStatusSnapshot((LicenseValidationStatus)4, P_0);
		return false;
	}

	private static bool yRNQtWcVCU()
	{
		if (rdfHVoGDWI != null)
		{
			return !string.IsNullOrWhiteSpace(rdfHVoGDWI.LookupId);
		}
		return false;
	}

	private static byte[] HADQ4X3eaX()
	{
		return RaJQfUTF5K(rdfHVoGDWI);
	}

	private static byte[] RaJQfUTF5K(object P_0)
	{
		if (P_0 != null && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)P_0).LicenseAccessKeyCipher))
		{
			TSHHv3vRBj.GetDeviceIdHash(out var text);
			if (LicenseCryptography.TryDecryptLocalCacheText(((PsdReaderLicenseCacheDocument)P_0).LicenseAccessKeyCipher, text, out var result))
			{
				return result;
			}
			return Array.Empty<byte>();
		}
		return Array.Empty<byte>();
	}

	private static string BTsQcwXXjP()
	{
		if (rdfHVoGDWI != null && !string.IsNullOrWhiteSpace(rdfHVoGDWI.OrderIdCipher))
		{
			TSHHv3vRBj.GetDeviceIdHash(out var text);
			if (LicenseCryptography.TryDecryptLocalCacheText(rdfHVoGDWI.OrderIdCipher, text, out var array))
			{
				return LicenseCryptography.NormalizeOrderNumber(LicenseCryptography.DecodeUtf8(array));
			}
			return string.Empty;
		}
		return string.Empty;
	}

	private static bool dtXQgd6KyT(object P_0)
	{
		if (P_0 == null)
		{
			return false;
		}
		return string.Equals(BacQNHOGDQ(((PsdReaderLicenseCacheDocument)P_0).ActivationSource), "order", StringComparison.Ordinal);
	}

	private static string zM8QlK1koT(object P_0)
	{
		if (!string.Equals(((string)(P_0 ?? string.Empty)).Trim(), "order", StringComparison.Ordinal))
		{
			return "project_file";
		}
		return "order";
	}

	private static string BacQNHOGDQ(object P_0)
	{
		string a = ((string)(P_0 ?? string.Empty)).Trim();
		if (string.Equals(a, "project_file", StringComparison.Ordinal))
		{
			return "project_file";
		}
		object obj;
		if (!string.Equals(a, "order", StringComparison.Ordinal))
		{
			PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument = rdfHVoGDWI;
			if (psdReaderLicenseCacheDocument == null)
			{
				obj = null;
			}
			else
			{
				obj = psdReaderLicenseCacheDocument.ActivationSource;
				if (obj != null)
				{
					goto IL_004f;
				}
			}
			obj = string.Empty;
			goto IL_004f;
		}
		return "order";
		IL_004f:
		string a2 = ((string)obj).Trim();
		if (!string.Equals(a2, "project_file", StringComparison.Ordinal))
		{
			if (!string.Equals(a2, "order", StringComparison.Ordinal))
			{
				if (BTsQcwXXjP().Length != 19)
				{
					return "project_file";
				}
				return "order";
			}
			return "order";
		}
		return "project_file";
	}

	private static bool JQnQkn0I1L()
	{
		if (HasProjectLicense())
		{
			if (!yRNQtWcVCU())
			{
				return true;
			}
			return HADQ4X3eaX().Length == 0;
		}
		return false;
	}

	private static void CDhQXZ78Tm()
	{
		if (!C08QvHmacJ(out var text))
		{
			return;
		}
		lock (HxaHki07OS)
		{
			NuMHmOksdm();
			if (string.Equals(ULU3pmnc8Q, text, StringComparison.Ordinal) || !lGuQ7PsVEy())
			{
				return;
			}
			ULU3pmnc8Q = text;
		}
		ActivateProjectLicense(out var _);
	}

	private static bool lGuQ7PsVEy()
	{
		if (flxHD5TW8h != null && flxHD5TW8h.GetValidationStatus() == (LicenseValidationStatus)1)
		{
			return false;
		}
		if (yRNQtWcVCU())
		{
			return HADQ4X3eaX().Length == 0;
		}
		return true;
	}

	private static bool C08QvHmacJ(out string P_0)
	{
		P_0 = string.Empty;
		string text = LicenseStoragePaths.GetProjectLicensePhysicalPath();
		if (File.Exists(text) && icqQyaBchS(out var _, out var dateTime, out var _))
		{
			try
			{
				FileInfo fileInfo = new FileInfo(text);
				P_0 = fileInfo.Length + ":" + fileInfo.LastWriteTimeUtc.Ticks + ":" + dateTime.Ticks;
				return true;
			}
			catch
			{
				return false;
			}
		}
		return false;
	}

	private static bool GtKQdcUkVp(out LicenseEvaluationContext P_0, out string P_1)
	{
		LicenseValidationStatus LicenseValidationStatus;
		return K1EQRjHsYs(out P_0, out P_1, out LicenseValidationStatus);
	}

	private static bool K1EQRjHsYs(out LicenseEvaluationContext P_0, out string P_1, out LicenseValidationStatus P_2)
	{
		P_0 = null;
		P_1 = string.Empty;
		P_2 = (LicenseValidationStatus)4;
		if (!LY6QVQwM5O(out var psdReaderProjectLicenseBundleDocument, out var dateTime, out P_1, out P_2))
		{
			return false;
		}
		if (psdReaderProjectLicenseBundleDocument != null && !(dateTime <= DateTime.UtcNow))
		{
			if (!LicenseCryptography.TryUnwrapProjectLicenseBytes(psdReaderProjectLicenseBundleDocument.WrappedOrderIdCipher, psdReaderProjectLicenseBundleDocument.LookupId, dateTime, out var array))
			{
				P_1 = "项目授权文件中的订单号信息无效。";
				P_2 = (LicenseValidationStatus)8;
				return false;
			}
			string text = LicenseCryptography.NormalizeOrderNumber(LicenseCryptography.DecodeUtf8(array));
			if (text.Length != 19)
			{
				P_1 = "项目授权文件中的订单号信息无效。";
				P_2 = (LicenseValidationStatus)8;
				return false;
			}
			if (LicenseCryptography.TryUnwrapProjectLicenseBytes(psdReaderProjectLicenseBundleDocument.WrappedLicenseAccessKey, psdReaderProjectLicenseBundleDocument.LookupId, dateTime, out var array2) && array2 != null && array2.Length != 0)
			{
				return rj4QHbVwQF(psdReaderProjectLicenseBundleDocument.LookupId, array2, text, true, (EPcPUUEPrgAWTvPplYA)2, "project_file", out P_1, out P_0);
			}
			P_1 = "项目授权文件中的访问密钥无效。";
			P_2 = (LicenseValidationStatus)8;
			return false;
		}
		P_1 = "项目授权文件已过期或不可用。";
		P_2 = (LicenseValidationStatus)12;
		return false;
	}

	private static bool sYPQ8V2lMK(object P_0, out LicenseEvaluationContext P_1, out string P_2, out LicenseValidationStatus P_3)
	{
		P_1 = null;
		P_2 = string.Empty;
		P_3 = (LicenseValidationStatus)8;
		if (P_0 == null)
		{
			P_2 = "项目授权文件不可用。";
			P_3 = (LicenseValidationStatus)4;
			return false;
		}
		DateTime dateTime = LicenseCryptography.ParseUtcDateTime(((PsdReaderProjectLicenseBundleDocument)P_0).ExportExpiresUtc, DateTime.MinValue);
		if (dateTime <= DateTime.UtcNow)
		{
			P_2 = "项目授权文件已过期。";
			P_3 = (LicenseValidationStatus)12;
			return false;
		}
		string text = lyVH9yeWXW();
		if (string.IsNullOrWhiteSpace(text))
		{
			P_2 = "当前授权组件初始化失败。";
			P_3 = (LicenseValidationStatus)2;
			return false;
		}
		if (LicenseCryptography.TryUnwrapProjectLicenseBytes(((PsdReaderProjectLicenseBundleDocument)P_0).WrappedLicenseAccessKey, ((PsdReaderProjectLicenseBundleDocument)P_0).LookupId, dateTime, out var array) && array != null && array.Length != 0)
		{
			string text2 = w6KHCaO90d();
			if (!WlHQWA2Onh<PsdReaderProductMetaPayloadDocument>(((PsdReaderProjectLicenseBundleDocument)P_0).MetaEnvelopeBase64, text, null, text2, LicenseCryptography.DeriveRepositoryDocumentKey(text2), out var psdReaderProductMetaPayloadDocument))
			{
				P_2 = "项目授权文件中的产品元数据无效。";
				P_3 = (LicenseValidationStatus)8;
				return false;
			}
			if (EX9HWL1pkA(psdReaderProductMetaPayloadDocument, out P_2, out P_3))
			{
				string text3 = tfdHx2SF3F(((PsdReaderProjectLicenseBundleDocument)P_0).LookupId);
				if (!WlHQWA2Onh<PsdReaderLicensePayloadDocument>(((PsdReaderProjectLicenseBundleDocument)P_0).LicenseEnvelopeBase64, psdReaderProductMetaPayloadDocument.ActiveLeafPublicKeyPem, psdReaderProductMetaPayloadDocument.ActiveKid, text3, LicenseCryptography.DeriveLicensePayloadKey(((PsdReaderProjectLicenseBundleDocument)P_0).LookupId, array), out var psdReaderLicensePayloadDocument))
				{
					P_2 = "项目授权文件中的授权数据无效。";
					P_3 = (LicenseValidationStatus)8;
					return false;
				}
				if (tKPHi4cvag(psdReaderLicensePayloadDocument, psdReaderProductMetaPayloadDocument, ((PsdReaderProjectLicenseBundleDocument)P_0).LookupId, out P_2, out P_3))
				{
					if (string.Equals(psdReaderLicensePayloadDocument.LicenseId, ((PsdReaderProjectLicenseBundleDocument)P_0).LicenseId, StringComparison.Ordinal))
					{
						string text4 = sDpHG7EPhx(((PsdReaderProjectLicenseBundleDocument)P_0).LookupId);
						if (!WlHQWA2Onh<PsdReaderOfflineLeasePayloadDocument>(((PsdReaderProjectLicenseBundleDocument)P_0).OfflineLeaseEnvelopeBase64, psdReaderProductMetaPayloadDocument.ActiveLeafPublicKeyPem, psdReaderProductMetaPayloadDocument.ActiveKid, text4, LicenseCryptography.DeriveOfflineLeaseKey(((PsdReaderProjectLicenseBundleDocument)P_0).LookupId, array), out var psdReaderOfflineLeasePayloadDocument))
						{
							P_2 = "项目授权文件中的离线租约无效。";
							P_3 = (LicenseValidationStatus)8;
							return false;
						}
						if (!X5jHjtF74D(psdReaderOfflineLeasePayloadDocument, psdReaderLicensePayloadDocument, psdReaderProductMetaPayloadDocument, ((PsdReaderProjectLicenseBundleDocument)P_0).LookupId, out P_2, out P_3))
						{
							return false;
						}
						TSHHv3vRBj.GetDeviceIdHash(out var text5);
						if (string.IsNullOrWhiteSpace(text5))
						{
							P_2 = "当前设备标识不可用。";
							P_3 = (LicenseValidationStatus)2;
							return false;
						}
						DateTime dateTime2 = ADlHSx91cI(psdReaderLicensePayloadDocument);
						DateTime dateTime3 = ((dateTime2 <= DateTime.MinValue || dateTime < dateTime2) ? dateTime : dateTime2);
						if (dateTime3 <= DateTime.UtcNow)
						{
							P_2 = "项目授权文件已过期。";
							P_3 = (LicenseValidationStatus)12;
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
						PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument = hyHQeOE8Al(psdReaderLicensePayloadDocument2, psdReaderProductMetaPayloadDocument, psdReaderOfflineLeasePayloadDocument2, text5, LicenseCryptography.EncryptLocalCacheText(array, text5), string.Empty, "project_file", string.Empty, text6, ((PsdReaderProjectLicenseBundleDocument)P_0).MetaEnvelopeBase64, ((PsdReaderProjectLicenseBundleDocument)P_0).LicenseEnvelopeBase64, ((PsdReaderProjectLicenseBundleDocument)P_0).OfflineLeaseEnvelopeBase64, string.Empty);
						LicenseEvaluationContext lxqZcsud7O9XCugf7PT = new LicenseEvaluationContext();
						lxqZcsud7O9XCugf7PT.SetLicensePayload(psdReaderLicensePayloadDocument2);
						lxqZcsud7O9XCugf7PT.SetLicenseCache(psdReaderLicenseCacheDocument);
						lxqZcsud7O9XCugf7PT.SetProductMetadata(psdReaderProductMetaPayloadDocument);
						lxqZcsud7O9XCugf7PT.y9dTpPFQwG(LicenseCryptography.ComputeLicenseStateFingerprint(psdReaderLicensePayloadDocument2, psdReaderLicenseCacheDocument.CurrentMajorVersion, psdReaderLicenseCacheDocument.LocalCacheTimeoutDays, psdReaderLicenseCacheDocument.MetaRevision, psdReaderLicenseCacheDocument.ActiveKid, text5, psdReaderProductMetaPayloadDocument.StateSalt));
						lxqZcsud7O9XCugf7PT.dmnT5ljly4(LroQGWXQiS(psdReaderLicensePayloadDocument2, psdReaderLicenseCacheDocument, "Main"));
						P_1 = lxqZcsud7O9XCugf7PT;
						P_2 = "项目授权文件验证成功。";
						P_3 = (LicenseValidationStatus)1;
						return true;
					}
					P_2 = "项目授权文件中的授权标识不匹配。";
					P_3 = (LicenseValidationStatus)8;
					return false;
				}
				return false;
			}
			return false;
		}
		P_2 = "项目授权文件中的访问密钥无效。";
		P_3 = (LicenseValidationStatus)8;
		return false;
	}

	private static bool xeGQDjG25B(int P_0, out PsdReaderProjectLicenseBundleDocument P_1, out string P_2)
	{
		P_1 = null;
		P_2 = string.Empty;
		int num = GorHOV3F8m(P_0);
		if (b9mH1dTdrN(out var psdReaderLicenseCacheDocument, out var num2, out P_2))
		{
			if (U1tHZ8118a(num, num2, out var dateTime))
			{
				byte[] array = RaJQfUTF5K(psdReaderLicenseCacheDocument);
				if (array != null && array.Length != 0)
				{
					if (!string.IsNullOrWhiteSpace(psdReaderLicenseCacheDocument.MetaEnvelopeBase64) && !string.IsNullOrWhiteSpace(psdReaderLicenseCacheDocument.LicenseEnvelopeBase64) && !string.IsNullOrWhiteSpace(psdReaderLicenseCacheDocument.OfflineLeaseEnvelopeBase64))
					{
						DateTime utcNow = DateTime.UtcNow;
						string text = LicenseCryptography.WrapProjectLicenseBytes(array, psdReaderLicenseCacheDocument.LookupId, dateTime);
						if (string.IsNullOrWhiteSpace(text))
						{
							P_2 = "封装授权访问密钥失败。";
							return false;
						}
						string text2 = BTsQcwXXjP();
						if (text2.Length != 19)
						{
							P_2 = "当前授权缓存中的订单号不可用，请先使用订单号重新验证授权。";
							return false;
						}
						string text3 = LicenseCryptography.WrapProjectLicenseBytes(Encoding.UTF8.GetBytes(text2), psdReaderLicenseCacheDocument.LookupId, dateTime);
						if (!string.IsNullOrWhiteSpace(text3))
						{
							P_1 = new PsdReaderProjectLicenseBundleDocument
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
							P_1.BundleSeal = LicenseCryptography.ComputeProjectLicenseSeal(P_1);
							if (string.IsNullOrWhiteSpace(P_1.BundleSeal))
							{
								P_2 = "生成授权文件校验信息失败。";
								P_1 = null;
								return false;
							}
							return true;
						}
						P_2 = "封装订单号失败。";
						return false;
					}
					P_2 = "当前授权缓存不完整，无法导出授权文件。";
					return false;
				}
				P_2 = "当前设备上的授权访问密钥不可用，请先重新验证授权。";
				return false;
			}
			P_2 = "当前授权不允许使用所选时长导出授权文件。";
			return false;
		}
		return false;
	}

	private static bool LY6QVQwM5O(out PsdReaderProjectLicenseBundleDocument P_0, out DateTime P_1, out string P_2, out LicenseValidationStatus P_3)
	{
		P_0 = null;
		P_1 = DateTime.MinValue;
		P_2 = string.Empty;
		P_3 = (LicenseValidationStatus)4;
		string path = LicenseStoragePaths.GetProjectLicensePhysicalPath();
		if (!File.Exists(path))
		{
			P_2 = "插件目录里未找到项目授权文件：" + LicenseStoragePaths.GetProjectLicenseAssetPath();
			return false;
		}
		byte[] array;
		try
		{
			array = File.ReadAllBytes(path);
		}
		catch (Exception)
		{
			P_2 = "读取项目授权文件失败。";
			P_3 = (LicenseValidationStatus)4;
			return false;
		}
		if (LicenseCryptography.TryDecodeProjectLicenseBundle(array, out P_0) && P_0 != null)
		{
			P_1 = LicenseCryptography.ParseUtcDateTime(P_0.ExportExpiresUtc, DateTime.MinValue);
			if (!(P_1 <= DateTime.UtcNow))
			{
				P_3 = (LicenseValidationStatus)1;
				return true;
			}
			bool flag = XspQzytJSY();
			P_2 = ((!flag) ? "项目授权文件已过期，请删除后重新导出。" : "项目授权文件已过期，已自动删除。");
			P_0 = null;
			P_1 = DateTime.MinValue;
			P_3 = (LicenseValidationStatus)12;
			return false;
		}
		P_2 = "项目授权文件内容无效或已损坏。";
		P_0 = null;
		P_3 = (LicenseValidationStatus)8;
		return false;
	}

	private static bool icqQyaBchS(out PsdReaderProjectLicenseBundleDocument P_0, out DateTime P_1, out string P_2)
	{
		LicenseValidationStatus LicenseValidationStatus;
		return LY6QVQwM5O(out P_0, out P_1, out P_2, out LicenseValidationStatus);
	}

	private static bool XspQzytJSY()
	{
		try
		{
			if (AssetDatabase.DeleteAsset(LicenseStoragePaths.GetProjectLicenseAssetPath()))
			{
				AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
				return true;
			}
		}
		catch (Exception)
		{
		}
		try
		{
			if (File.Exists(LicenseStoragePaths.GetProjectLicensePhysicalPath()))
			{
				File.Delete(LicenseStoragePaths.GetProjectLicensePhysicalPath());
			}
			string path = LicenseStoragePaths.GetProjectLicensePhysicalPath() + ".meta";
			if (File.Exists(path))
			{
				File.Delete(path);
			}
			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
			return !File.Exists(LicenseStoragePaths.GetProjectLicensePhysicalPath());
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static bool b9mH1dTdrN(out PsdReaderLicenseCacheDocument P_0, out long P_1, out string P_2)
	{
		P_0 = null;
		P_1 = 0L;
		P_2 = string.Empty;
		lock (HxaHki07OS)
		{
			NuMHmOksdm();
			P_0 = rdfHVoGDWI;
			if (P_0 != null && !string.IsNullOrWhiteSpace(P_0.LookupId))
			{
				if (!QgjH3kThr8(P_0))
				{
					P_2 = "当前授权尚未处于有效状态，无法导出授权文件。";
					return false;
				}
				if (dtXQgd6KyT(P_0))
				{
					P_1 = fIAH2D8V6J(P_0.SupportUntilUtc);
					if (WmxHhRxmX5(P_1))
					{
						return true;
					}
					P_1 = 0L;
					P_2 = "当前授权暂不允许导出授权文件。";
					return false;
				}
				P_2 = "当前设备通过授权文件激活，不具备导出授权文件权限。";
				return false;
			}
			P_2 = "当前还没有可导出的授权缓存，请先使用订单号完成激活。";
			return false;
		}
	}

	private static bool U1tHZ8118a(int P_0, long P_1, out DateTime P_2)
	{
		P_2 = DateTime.MinValue;
		int num = GorHOV3F8m(P_0);
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
		long num3 = P_1 - ticks2;
		if (num2 > 0L && num3 > 0L)
		{
			long num4 = (((ulong)num2 <= (ulong)num3) ? ticks : P_1);
			if (num4 <= ticks2)
			{
				return false;
			}
			P_2 = new DateTime(num4, DateTimeKind.Utc);
			return true;
		}
		return false;
	}

	private static int GorHOV3F8m(int P_0)
	{
		return Mathf.Clamp(Mathf.Max(1, P_0), 1, 36500);
	}

	private static bool WmxHhRxmX5(long P_0)
	{
		if (P_0 > DateTime.UtcNow.Ticks)
		{
			return P_0 <= DateTime.MaxValue.Ticks;
		}
		return false;
	}

	private static bool jnuHn26KR8(object P_0, out byte[] P_1)
	{
		P_1 = Array.Empty<byte>();
		if (P_0 != null && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)P_0).GrantSeedCipher))
		{
			TSHHv3vRBj.GetDeviceIdHash(out var text);
			if (!LMIHPJIv2V(((PsdReaderLicenseCacheDocument)P_0).DeviceFingerprint, text))
			{
				return false;
			}
			if (LicenseCryptography.TryDecryptLocalCacheText(((PsdReaderLicenseCacheDocument)P_0).GrantSeedCipher, text, out P_1))
			{
				return P_1.Length != 0;
			}
			return false;
		}
		return false;
	}

	private static bool Rh2HpaJJ5Z(object P_0)
	{
		TSHHv3vRBj.GetDeviceIdHash(out var text);
		return LMIHPJIv2V(P_0, text);
	}

	private static bool LMIHPJIv2V(object P_0, object P_1)
	{
		if (!string.IsNullOrWhiteSpace((string)P_0) && !string.IsNullOrWhiteSpace((string)P_1))
		{
			return string.Equals(LicenseCryptography.NormalizeHexString(P_0), LicenseCryptography.NormalizeHexString(P_1), StringComparison.Ordinal);
		}
		return false;
	}

	private static long fIAH2D8V6J(object P_0)
	{
		DateTime dateTime = LicenseCryptography.ParseUtcDateTime(P_0, DateTime.MinValue);
		if (!(dateTime <= DateTime.MinValue))
		{
			return dateTime.Ticks;
		}
		return 0L;
	}

	private static PsdReaderLicenseClientConfigDocument eRbH58XT8E(PsdReaderProductMetaPayloadDocument P_0 = null, PsdReaderLicenseCacheDocument P_1 = null, PsdReaderLicenseClientConfigDocument P_2 = null)
	{
		PsdReaderLicenseClientConfigDocument psdReaderLicenseClientConfigDocument = P_2 ?? ub1HyAPIjL ?? St3HUbyQev();
		if (psdReaderLicenseClientConfigDocument != null)
		{
			string text = ((!string.IsNullOrWhiteSpace(P_0?.TelemetryUrl)) ? P_0.TelemetryUrl : ((!string.IsNullOrWhiteSpace(P_1?.TelemetryUrl)) ? P_1.TelemetryUrl : (string.IsNullOrWhiteSpace(rdfHVoGDWI?.TelemetryUrl) ? psdReaderLicenseClientConfigDocument.MatomoUrl : rdfHVoGDWI.TelemetryUrl)));
			string text2 = ((!string.IsNullOrWhiteSpace(P_0?.TelemetrySiteId)) ? P_0.TelemetrySiteId : ((!string.IsNullOrWhiteSpace(P_1?.TelemetrySiteId)) ? P_1.TelemetrySiteId : ((!string.IsNullOrWhiteSpace(rdfHVoGDWI?.TelemetrySiteId)) ? rdfHVoGDWI.TelemetrySiteId : psdReaderLicenseClientConfigDocument.MatomoSiteId)));
			PsdReaderLicenseClientConfigDocument psdReaderLicenseClientConfigDocument2 = new PsdReaderLicenseClientConfigDocument();
			psdReaderLicenseClientConfigDocument2.VendorCode = psdReaderLicenseClientConfigDocument.VendorCode;
			psdReaderLicenseClientConfigDocument2.ProductCode = psdReaderLicenseClientConfigDocument.ProductCode;
			psdReaderLicenseClientConfigDocument2.RepositoryBaseUrls = zE7HBfV5R4(P_0?.RepositoryBaseUrls, psdReaderLicenseClientConfigDocument.RepositoryBaseUrls);
			psdReaderLicenseClientConfigDocument2.MatomoUrl = text ?? string.Empty;
			psdReaderLicenseClientConfigDocument2.MatomoSiteId = text2 ?? string.Empty;
			psdReaderLicenseClientConfigDocument2.RequestTimeoutSeconds = psdReaderLicenseClientConfigDocument.RequestTimeoutSeconds;
			psdReaderLicenseClientConfigDocument2.CurrentMajorVersion = psdReaderLicenseClientConfigDocument.CurrentMajorVersion;
			return psdReaderLicenseClientConfigDocument2;
		}
		return null;
	}

	private static string[] zE7HBfV5R4(params string[][] repositoryBaseUrlSets)
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

	private static PsdReaderLicenseClientConfigDocument St3HUbyQev()
	{
		return LicenseClientDefaults.CreateClientConfig();
	}

	private static string lyVH9yeWXW()
	{
		return LicenseClientDefaults.GetPublicKeyPem();
	}

	private static void NuMHmOksdm()
	{
		if (NWPHzw3nnc)
		{
			return;
		}
		NWPHzw3nnc = true;
		rdfHVoGDWI = XiAH7XroGM.Load();
		if (rdfHVoGDWI == null)
		{
			if (flxHD5TW8h == null)
			{
				flxHD5TW8h = CreateStatusSnapshot((LicenseValidationStatus)4, (!HasProjectLicense()) ? "尚未激活 Psd2UGUI 授权。" : "已检测到插件目录授权文件，可直接使用授权文件激活。");
			}
			return;
		}
		rdfHVoGDWI.ActivationSource = BacQNHOGDQ(rdfHVoGDWI.ActivationSource);
		if (XWgQw1Pyh3(rdfHVoGDWI) != null && HADQ4X3eaX().Length != 0)
		{
			ub1HyAPIjL = eRbH58XT8E(null, rdfHVoGDWI);
			flxHD5TW8h = CreateStatusSnapshot(rdfHVoGDWI.LastResultCode, rdfHVoGDWI.Message, rdfHVoGDWI);
		}
		else
		{
			ub1HyAPIjL = St3HUbyQev();
			flxHD5TW8h = CreateStatusSnapshot((LicenseValidationStatus)4, HasProjectLicense() ? "已检测到插件目录授权文件，可直接使用授权文件激活。" : "当前授权需要重新输入订单号完成验证。");
		}
	}

	private static bool e3lHo8bKG3(LicenseValidationStatus P_0, object P_1, out LicenseEvaluationContext P_2, out string P_3)
	{
		P_2 = null;
		P_3 = string.Empty;
		if (!dIrHrwQiNB(P_0, P_1))
		{
			return false;
		}
		if (pnsHufAds0(rdfHVoGDWI))
		{
			P_2 = XWgQw1Pyh3(rdfHVoGDWI);
			if (P_2 != null)
			{
				P_3 = "当前网络不可用，已继续使用最近一次本地授权缓存。";
				return true;
			}
			return false;
		}
		return false;
	}

	private static bool AXJHJooQ5h(object P_0, out LicenseEvaluationContext P_1, out string P_2)
	{
		P_1 = null;
		P_2 = string.Empty;
		if (rdfHVoGDWI != null && pnsHufAds0(rdfHVoGDWI))
		{
			P_1 = XWgQw1Pyh3(rdfHVoGDWI);
			if (P_1 == null)
			{
				return false;
			}
			if (!E19HTUq1m8(P_0))
			{
				P_2 = "授权有效。";
				return true;
			}
			if (reOHAbdkm8(rdfHVoGDWI))
			{
				P_2 = "授权有效。";
				return true;
			}
			P_1 = null;
			P_2 = string.Empty;
			return false;
		}
		return false;
	}

	private static bool pnsHufAds0(object P_0)
	{
		if (P_0 != null)
		{
			if (((PsdReaderLicenseCacheDocument)P_0).LastResultCode != (LicenseValidationStatus)1)
			{
				return false;
			}
			if (!string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)P_0).LookupId) && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)P_0).LicenseId) && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)P_0).MetaEnvelopeBase64) && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)P_0).LicenseEnvelopeBase64) && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)P_0).OfflineLeaseEnvelopeBase64) && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)P_0).LicenseAccessKeyCipher) && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)P_0).ClockHighWaterUtc) && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)P_0).ClockWatermarkCipher) && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)P_0).ValidationTranscriptHead) && ((PsdReaderLicenseCacheDocument)P_0).StatusCode == 0 && ((PsdReaderLicenseCacheDocument)P_0).FeatureMask != 0 && Rh2HpaJJ5Z(((PsdReaderLicenseCacheDocument)P_0).DeviceFingerprint))
			{
				TSHHv3vRBj.GetDeviceIdHash(out var text);
				if (string.IsNullOrWhiteSpace(text))
				{
					return false;
				}
				if (!NdoHsLKOZQ(P_0, text, out var dateTime, out var dateTime2, out var _, out var _))
				{
					return false;
				}
				DateTime utcNow = DateTime.UtcNow;
				DateTime dateTime3 = LicenseCryptography.ParseUtcDateTime(((PsdReaderLicenseCacheDocument)P_0).LastVerifiedUtc, DateTime.MinValue);
				if (!(dateTime3 <= DateTime.MinValue) && !(dateTime != dateTime3) && !(dateTime > utcNow) && !(dateTime2 < utcNow))
				{
					return utcNow <= dateTime.AddDays(Mathf.Max(1, ((PsdReaderLicenseCacheDocument)P_0).LocalCacheTimeoutDays));
				}
				return false;
			}
			return false;
		}
		return false;
	}

	private static bool E19HTUq1m8(object P_0)
	{
		if (((PsdReaderLicenseClientConfigDocument)P_0)?.RepositoryBaseUrls == null)
		{
			return false;
		}
		return ((PsdReaderLicenseClientConfigDocument)P_0).RepositoryBaseUrls.Length != 0;
	}

	private static bool reOHAbdkm8(object P_0)
	{
		if (P_0 != null && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)P_0).LastRepositoryRequestUtc))
		{
			DateTime dateTime = LicenseCryptography.ParseUtcDateTime(((PsdReaderLicenseCacheDocument)P_0).LastRepositoryRequestUtc, DateTime.MinValue);
			if (dateTime <= DateTime.MinValue)
			{
				return false;
			}
			return DateTime.UtcNow <= dateTime.AddHours(1.0);
		}
		return false;
	}

	private static string fOxHMD36Od(object P_0)
	{
		if (!E19HTUq1m8(P_0))
		{
			return string.Empty;
		}
		return DateTime.UtcNow.ToString("O");
	}

	private static void zN9HQmWfvf(object P_0)
	{
		if (!string.IsNullOrWhiteSpace((string)P_0) && rdfHVoGDWI != null)
		{
			rdfHVoGDWI.LastRepositoryRequestUtc = (string)P_0;
			XiAH7XroGM.Save(rdfHVoGDWI);
		}
	}

	private static string LnjHHLgS5c(object P_0)
	{
		object obj;
		if (!string.IsNullOrWhiteSpace((string)P_0))
		{
			obj = P_0;
			goto IL_001b;
		}
		PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument = rdfHVoGDWI;
		if (psdReaderLicenseCacheDocument != null)
		{
			obj = psdReaderLicenseCacheDocument.LastRepositoryRequestUtc;
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

	private static bool QgjH3kThr8(object P_0)
	{
		return pnsHufAds0(P_0);
	}

	private static bool NdoHsLKOZQ(object P_0, object P_1, out DateTime P_2, out DateTime P_3, out string P_4, out string P_5)
	{
		P_2 = DateTime.MinValue;
		P_3 = DateTime.MinValue;
		P_4 = string.Empty;
		P_5 = string.Empty;
		if (P_0 != null && !string.IsNullOrWhiteSpace((string)P_1) && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)P_0).ClockHighWaterUtc) && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)P_0).ClockWatermarkCipher) && !string.IsNullOrWhiteSpace(((PsdReaderLicenseCacheDocument)P_0).ValidationTranscriptHead))
		{
			if (!LicenseCryptography.TryDecodeClockWatermark(((PsdReaderLicenseCacheDocument)P_0).ClockWatermarkCipher, P_1, out var dateTime, out var dateTime2, out P_4, out P_5))
			{
				return false;
			}
			P_2 = LicenseCryptography.ParseUtcDateTime(((PsdReaderLicenseCacheDocument)P_0).ClockHighWaterUtc, DateTime.MinValue);
			P_3 = dateTime2;
			DateTime dateTime3 = LicenseCryptography.ParseUtcDateTime(((PsdReaderLicenseCacheDocument)P_0).SupportUntilUtc, DateTime.MinValue);
			if (!(P_2 <= DateTime.MinValue) && !(P_2 != dateTime) && !(dateTime3 <= DateTime.MinValue) && !(dateTime3 != P_3) && !(P_3 < P_2) && string.Equals(((PsdReaderLicenseCacheDocument)P_0).ValidationTranscriptHead, P_5, StringComparison.Ordinal) && !(P_2 != LicenseCryptography.ParseUtcDateTime(((PsdReaderLicenseCacheDocument)P_0).LastVerifiedUtc, DateTime.MinValue)))
			{
				if (!string.Equals(LicenseCryptography.ComputeCacheProof(P_0, P_1, P_2, P_3), P_4, StringComparison.Ordinal))
				{
					return false;
				}
				return string.Equals(LicenseCryptography.ComputeTranscriptHead(P_0, P_1, P_2, P_3, P_4), P_5, StringComparison.Ordinal);
			}
			return false;
		}
		return false;
	}

	private static bool dIrHrwQiNB(LicenseValidationStatus P_0, object P_1)
	{
		if (P_0 != (LicenseValidationStatus)5)
		{
			return false;
		}
		string text = ((string)(P_1 ?? string.Empty)).Trim();
		if (!string.IsNullOrWhiteSpace(text))
		{
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
		return true;
	}

	private static LicenseValidationStatus ynXHFOJKI8(object P_0, object P_1)
	{
		if (P_0 != null && ((LicenseEvaluationContext)P_0).GetLicensePayload() != null)
		{
			if (((LicenseEvaluationContext)P_0).GetLicensePayload().StatusCode == 1)
			{
				return (LicenseValidationStatus)10;
			}
			if (((LicenseEvaluationContext)P_0).GetLicensePayload().StatusCode == 2)
			{
				return (LicenseValidationStatus)11;
			}
			if (!LicenseFeatureMask.ContainsFeature(((LicenseEvaluationContext)P_0).GetLicensePayload().FeatureMask, P_1))
			{
				return (LicenseValidationStatus)13;
			}
			if (!(ADlHSx91cI(((LicenseEvaluationContext)P_0).GetLicensePayload()) < DateTime.UtcNow))
			{
				return (LicenseValidationStatus)8;
			}
			return (LicenseValidationStatus)12;
		}
		return (LicenseValidationStatus)4;
	}

	private static LicenseValidationStatus QEMHa6xMmE(object P_0, object P_1)
	{
		if (P_0 != null && ((LicenseEvaluationContext)P_0).GetLicensePayload() != null)
		{
			if (((LicenseEvaluationContext)P_0).GetLicensePayload().StatusCode == 1)
			{
				return (LicenseValidationStatus)10;
			}
			if (((LicenseEvaluationContext)P_0).GetLicensePayload().StatusCode == 2)
			{
				return (LicenseValidationStatus)11;
			}
			if (LicenseFeatureMask.ContainsFeature(((LicenseEvaluationContext)P_0).GetLicensePayload().FeatureMask, P_1))
			{
				if (!(ADlHSx91cI(((LicenseEvaluationContext)P_0).GetLicensePayload()) < DateTime.UtcNow))
				{
					return (LicenseValidationStatus)1;
				}
				return (LicenseValidationStatus)12;
			}
			return (LicenseValidationStatus)13;
		}
		return (LicenseValidationStatus)4;
	}

	private static string rYtH6T5nWL(object P_0, object P_1)
	{
		if (P_0 != null && ((LicenseEvaluationContext)P_0).GetLicensePayload() != null)
		{
			if (((LicenseEvaluationContext)P_0).GetLicensePayload().StatusCode == 1)
			{
				return "当前订单授权已被暂停。";
			}
			if (((LicenseEvaluationContext)P_0).GetLicensePayload().StatusCode == 2)
			{
				return "当前订单授权已被封禁。";
			}
			if (LicenseFeatureMask.ContainsFeature(((LicenseEvaluationContext)P_0).GetLicensePayload().FeatureMask, P_1))
			{
				if (!(ADlHSx91cI(((LicenseEvaluationContext)P_0).GetLicensePayload()) < DateTime.UtcNow))
				{
					return "Psd2UGUI 授权状态折叠校验失败。";
				}
				return "当前授权已过支持期。";
			}
			return "当前授权不包含功能: " + (string)P_1;
		}
		return "授权缓存不可用。";
	}

	private static DateTime ADlHSx91cI(object P_0)
	{
		if (P_0 == null)
		{
			return DateTime.MinValue;
		}
		if (((PsdReaderLicensePayloadDocument)P_0).SupportUntilUtcTicks > 0L)
		{
			return new DateTime(((PsdReaderLicensePayloadDocument)P_0).SupportUntilUtcTicks, DateTimeKind.Utc);
		}
		return LicenseCryptography.ParseUtcDateTime(((PsdReaderLicensePayloadDocument)P_0).SupportUntilUtc, DateTime.MinValue);
	}

	private static LicenseStatusSnapshot CreateStatusSnapshot(LicenseValidationStatus P_0, object P_1, PsdReaderLicenseCacheDocument P_2 = null)
	{
		string text = string.Empty;
		string[] array = Array.Empty<string>();
		if (P_2 != null)
		{
			text = (string.IsNullOrWhiteSpace(P_2.StatusText) ? LicensePayloadBinaryReader.GetLicenseStatusText(P_2.StatusCode) : P_2.StatusText);
			array = ((P_2.Features == null || P_2.Features.Length == 0) ? LicenseFeatureMask.DecodeFeatures(P_2.FeatureMask) : P_2.Features);
		}
		LicenseStatusSnapshot mFR4ptunmu6ucGmoRvG = new LicenseStatusSnapshot();
		mFR4ptunmu6ucGmoRvG.SetValidationStatus(P_0);
		mFR4ptunmu6ucGmoRvG.Message = (string)(P_1 ?? string.Empty);
		object obj;
		if (P_2 != null)
		{
			obj = P_2.LicenseId;
			if (obj != null)
			{
				goto IL_008b;
			}
		}
		else
		{
			obj = null;
		}
		obj = string.Empty;
		goto IL_008b;
		IL_008b:
		mFR4ptunmu6ucGmoRvG.SetLicenseId((string)obj);
		object obj2;
		if (P_2 == null)
		{
			obj2 = null;
		}
		else
		{
			obj2 = P_2.LookupId;
			if (obj2 != null)
			{
				goto IL_00a6;
			}
		}
		obj2 = string.Empty;
		goto IL_00a6;
		IL_00a6:
		mFR4ptunmu6ucGmoRvG.SetLookupId((string)obj2);
		mFR4ptunmu6ucGmoRvG.SetStatusText(text);
		mFR4ptunmu6ucGmoRvG.SetFeatures(array);
		mFR4ptunmu6ucGmoRvG.SetLastVerifiedUtc(LicenseCryptography.ParseUtcDateTime(P_2?.LastVerifiedUtc, DateTime.MinValue));
		mFR4ptunmu6ucGmoRvG.SetSupportUntilUtc(LicenseCryptography.ParseUtcDateTime(P_2?.SupportUntilUtc, DateTime.MinValue));
		mFR4ptunmu6ucGmoRvG.SetRevision(P_2?.Revision ?? 0);
		return mFR4ptunmu6ucGmoRvG;
	}

	private static LicenseStatusSnapshot CloneStatusSnapshot(object P_0)
	{
		if (P_0 == null)
		{
			return new LicenseStatusSnapshot();
		}
		LicenseStatusSnapshot mFR4ptunmu6ucGmoRvG = new LicenseStatusSnapshot();
		mFR4ptunmu6ucGmoRvG.SetValidationStatus(((LicenseStatusSnapshot)P_0).GetValidationStatus());
		mFR4ptunmu6ucGmoRvG.Message = ((LicenseStatusSnapshot)P_0).Message;
		mFR4ptunmu6ucGmoRvG.SetLicenseId(((LicenseStatusSnapshot)P_0).GetLicenseId());
		mFR4ptunmu6ucGmoRvG.SetLookupId(((LicenseStatusSnapshot)P_0).GetLookupId());
		mFR4ptunmu6ucGmoRvG.SetStatusText(((LicenseStatusSnapshot)P_0).GetStatusText());
		string[] array = ((LicenseStatusSnapshot)P_0).GetFeatures();
		object obj;
		if (array != null)
		{
			obj = array.ToArray();
			if (obj != null)
			{
				goto IL_0066;
			}
		}
		else
		{
			obj = null;
		}
		obj = Array.Empty<string>();
		goto IL_0066;
		IL_0066:
		mFR4ptunmu6ucGmoRvG.SetFeatures((string[])obj);
		mFR4ptunmu6ucGmoRvG.SetLastVerifiedUtc(((LicenseStatusSnapshot)P_0).GetLastVerifiedUtc());
		mFR4ptunmu6ucGmoRvG.SetSupportUntilUtc(((LicenseStatusSnapshot)P_0).GetSupportUntilUtc());
		mFR4ptunmu6ucGmoRvG.SetRevision(((LicenseStatusSnapshot)P_0).GetRevision());
		return mFR4ptunmu6ucGmoRvG;
	}

	private static string WxPHEnoGHd()
	{
		return "products/psd2ugui/state.data";
	}

	private static string w6KHCaO90d()
	{
		return "state:psd2ugui";
	}

	private static string knWHqkp6aB(object P_0)
	{
		string text = LicenseCryptography.NormalizeHexString(P_0);
		string text2 = ((text.Length >= 2) ? text.Substring(0, 2) : "00");
		string text3 = ((text.Length < 4) ? "00" : text.Substring(2, 2));
		return "products/psd2ugui/licenses/" + text2 + "/" + text3 + "/" + text + ".data";
	}

	private static string tfdHx2SF3F(object P_0)
	{
		return "license:psd2ugui:" + LicenseCryptography.NormalizeHexString(P_0);
	}

	private static string FT6HIqixFs(object P_0)
	{
		string text = LicenseCryptography.NormalizeHexString(P_0);
		string text2 = ((text.Length < 2) ? "00" : text.Substring(0, 2));
		string text3 = ((text.Length < 4) ? "00" : text.Substring(2, 2));
		return "products/psd2ugui/offline-leases/" + text2 + "/" + text3 + "/" + text + ".data";
	}

	private static string sDpHG7EPhx(object P_0)
	{
		return "offline-lease:psd2ugui:" + LicenseCryptography.NormalizeHexString(P_0);
	}

	private static string sfTHbVB21Z(object P_0)
	{
		return "products/psd2ugui/devices/" + Y6cHefyx0Y(P_0) + ".data";
	}

	private static string bf2Hw5rvjk(object P_0)
	{
		return "device-shard:psd2ugui:" + Y6cHefyx0Y(P_0);
	}

	private static string Y6cHefyx0Y(object P_0)
	{
		string text = LicenseCryptography.NormalizeHexString(P_0);
		if (text.Length < 2)
		{
			return "00";
		}
		return text.Substring(0, 2);
	}

	private static bool EX9HWL1pkA(object P_0, out string P_1, out LicenseValidationStatus P_2)
	{
		P_1 = string.Empty;
		P_2 = (LicenseValidationStatus)1;
		if (P_0 != null)
		{
			if (((PsdReaderProductMetaPayloadDocument)P_0).Schema >= 1 && ((PsdReaderProductMetaPayloadDocument)P_0).Revision > 0)
			{
				if (string.Equals(((PsdReaderProductMetaPayloadDocument)P_0).ProductCode, "psd2ugui", StringComparison.Ordinal) && string.Equals(((PsdReaderProductMetaPayloadDocument)P_0).VendorCode, "efunstudio", StringComparison.Ordinal))
				{
					if (!string.IsNullOrWhiteSpace(((PsdReaderProductMetaPayloadDocument)P_0).ActiveKid) && !string.IsNullOrWhiteSpace(((PsdReaderProductMetaPayloadDocument)P_0).ActiveLeafPublicKeyPem))
					{
						if (((PsdReaderProductMetaPayloadDocument)P_0).FeatureCatalogMask != 0 && ((PsdReaderProductMetaPayloadDocument)P_0).LocalCacheTimeoutDays > 0 && ((PsdReaderProductMetaPayloadDocument)P_0).CurrentMajorVersion > 0 && ((PsdReaderProductMetaPayloadDocument)P_0).RepositoryBaseUrls != null && ((PsdReaderProductMetaPayloadDocument)P_0).RepositoryBaseUrls.Length != 0 && ((PsdReaderProductMetaPayloadDocument)P_0).StateSalt != null && ((PsdReaderProductMetaPayloadDocument)P_0).StateSalt.Length != 0)
						{
							return true;
						}
						P_1 = "产品元数据内容无效。";
						P_2 = (LicenseValidationStatus)6;
						return false;
					}
					P_1 = "产品元数据签名信息无效。";
					P_2 = (LicenseValidationStatus)6;
					return false;
				}
				P_1 = "产品元数据与当前插件不匹配。";
				P_2 = (LicenseValidationStatus)9;
				return false;
			}
			P_1 = "产品元数据版本无效。";
			P_2 = (LicenseValidationStatus)6;
			return false;
		}
		P_1 = "产品元数据为空。";
		P_2 = (LicenseValidationStatus)6;
		return false;
	}

	private static bool tKPHi4cvag(object P_0, object P_1, object P_2, out string P_3, out LicenseValidationStatus P_4)
	{
		P_3 = string.Empty;
		P_4 = (LicenseValidationStatus)1;
		if (P_0 == null)
		{
			P_3 = "授权文件为空。";
			P_4 = (LicenseValidationStatus)8;
			return false;
		}
		if (((PsdReaderLicensePayloadDocument)P_0).Schema >= 1 && ((PsdReaderLicensePayloadDocument)P_0).Revision > 0 && !string.IsNullOrWhiteSpace(((PsdReaderLicensePayloadDocument)P_0).LicenseId) && ((PsdReaderLicensePayloadDocument)P_0).MaxMajorVersion > 0 && ((PsdReaderLicensePayloadDocument)P_0).GrantSeed != null && ((PsdReaderLicensePayloadDocument)P_0).GrantSeed.Length != 0)
		{
			if (!string.Equals(LicenseCryptography.NormalizeHexString(((PsdReaderLicensePayloadDocument)P_0).LookupId), LicenseCryptography.NormalizeHexString(P_2), StringComparison.Ordinal))
			{
				P_3 = "授权文件索引不匹配。";
				P_4 = (LicenseValidationStatus)8;
				return false;
			}
			if (string.Equals(((PsdReaderLicensePayloadDocument)P_0).ProductCode, "psd2ugui", StringComparison.Ordinal) && string.Equals(((PsdReaderLicensePayloadDocument)P_0).VendorCode, "efunstudio", StringComparison.Ordinal))
			{
				if (P_1 != null)
				{
					if ((((PsdReaderLicensePayloadDocument)P_0).FeatureMask & ~((PsdReaderProductMetaPayloadDocument)P_1).FeatureCatalogMask) == 0)
					{
						((PsdReaderLicensePayloadDocument)P_0).Features = LicenseFeatureMask.DecodeFeatures(((PsdReaderLicensePayloadDocument)P_0).FeatureMask);
						((PsdReaderLicensePayloadDocument)P_0).Status = LicensePayloadBinaryReader.GetLicenseStatusText(((PsdReaderLicensePayloadDocument)P_0).StatusCode);
						if (((PsdReaderLicensePayloadDocument)P_0).StatusCode <= 2)
						{
							return true;
						}
						P_3 = "授权状态码无效。";
						P_4 = (LicenseValidationStatus)8;
						return false;
					}
					P_3 = "授权文件包含未声明功能。";
					P_4 = (LicenseValidationStatus)9;
					return false;
				}
				P_3 = "产品元数据为空。";
				P_4 = (LicenseValidationStatus)6;
				return false;
			}
			P_3 = "授权文件产品信息不匹配。";
			P_4 = (LicenseValidationStatus)9;
			return false;
		}
		P_3 = "授权文件内容无效。";
		P_4 = (LicenseValidationStatus)8;
		return false;
	}

	private static bool FYpHK838GR(object P_0, object P_1, out string P_2, out LicenseValidationStatus P_3)
	{
		P_2 = string.Empty;
		P_3 = (LicenseValidationStatus)1;
		if (P_0 != null)
		{
			if (((PsdReaderDeviceShardPayloadDocument)P_0).Schema >= 1 && string.Equals(((PsdReaderDeviceShardPayloadDocument)P_0).Kind, "DeviceShard", StringComparison.Ordinal) && string.Equals(((PsdReaderDeviceShardPayloadDocument)P_0).ProductCode, "psd2ugui", StringComparison.Ordinal) && string.Equals(Y6cHefyx0Y(((PsdReaderDeviceShardPayloadDocument)P_0).Prefix), Y6cHefyx0Y(P_1), StringComparison.Ordinal))
			{
				return true;
			}
			P_2 = "设备分片内容无效。";
			P_3 = (LicenseValidationStatus)8;
			return false;
		}
		P_2 = "设备分片为空。";
		P_3 = (LicenseValidationStatus)8;
		return false;
	}

	private static bool X5jHjtF74D(object P_0, object P_1, object P_2, object P_3, out string P_4, out LicenseValidationStatus P_5)
	{
		P_4 = string.Empty;
		P_5 = (LicenseValidationStatus)1;
		if (P_0 == null)
		{
			P_4 = "离线授权租约为空。";
			P_5 = (LicenseValidationStatus)8;
			return false;
		}
		if (((PsdReaderOfflineLeasePayloadDocument)P_0).Schema == 1 && string.Equals(((PsdReaderOfflineLeasePayloadDocument)P_0).Kind, "OfflineLease", StringComparison.Ordinal) && ((PsdReaderOfflineLeasePayloadDocument)P_0).Revision > 0 && ((PsdReaderOfflineLeasePayloadDocument)P_0).OfflineCacheTimeoutDays > 0)
		{
			if (P_1 != null && P_2 != null)
			{
				if (string.Equals(LicenseCryptography.NormalizeHexString(((PsdReaderOfflineLeasePayloadDocument)P_0).LookupId), LicenseCryptography.NormalizeHexString(P_3), StringComparison.Ordinal))
				{
					if (string.Equals(((PsdReaderOfflineLeasePayloadDocument)P_0).ProductCode, "psd2ugui", StringComparison.Ordinal) && string.Equals(((PsdReaderOfflineLeasePayloadDocument)P_0).VendorCode, "efunstudio", StringComparison.Ordinal))
					{
						if ((((PsdReaderOfflineLeasePayloadDocument)P_0).FeatureMask & ~((PsdReaderProductMetaPayloadDocument)P_2).FeatureCatalogMask) == 0)
						{
							((PsdReaderOfflineLeasePayloadDocument)P_0).Features = LicenseFeatureMask.DecodeFeatures(((PsdReaderOfflineLeasePayloadDocument)P_0).FeatureMask);
							((PsdReaderOfflineLeasePayloadDocument)P_0).Status = LicensePayloadBinaryReader.GetLicenseStatusText(((PsdReaderOfflineLeasePayloadDocument)P_0).StatusCode);
							((PsdReaderOfflineLeasePayloadDocument)P_0).SupportUntilUtc = ((((PsdReaderOfflineLeasePayloadDocument)P_0).SupportUntilUtcTicks <= 0L) ? string.Empty : new DateTime(((PsdReaderOfflineLeasePayloadDocument)P_0).SupportUntilUtcTicks, DateTimeKind.Utc).ToString("O"));
							((PsdReaderOfflineLeasePayloadDocument)P_0).IssuedAtUtc = ((((PsdReaderOfflineLeasePayloadDocument)P_0).IssuedUtcTicks <= 0L) ? string.Empty : new DateTime(((PsdReaderOfflineLeasePayloadDocument)P_0).IssuedUtcTicks, DateTimeKind.Utc).ToString("O"));
							if (((PsdReaderOfflineLeasePayloadDocument)P_0).StatusCode <= 2)
							{
								if (string.Equals(LicenseCryptography.NormalizeHexString(((PsdReaderLicensePayloadDocument)P_1).LookupId), LicenseCryptography.NormalizeHexString(((PsdReaderOfflineLeasePayloadDocument)P_0).LookupId), StringComparison.Ordinal) && string.Equals(((PsdReaderLicensePayloadDocument)P_1).LicenseId, ((PsdReaderOfflineLeasePayloadDocument)P_0).LicenseId, StringComparison.Ordinal) && string.Equals(((PsdReaderLicensePayloadDocument)P_1).VendorCode, ((PsdReaderOfflineLeasePayloadDocument)P_0).VendorCode, StringComparison.Ordinal) && string.Equals(((PsdReaderLicensePayloadDocument)P_1).ProductCode, ((PsdReaderOfflineLeasePayloadDocument)P_0).ProductCode, StringComparison.Ordinal) && ((PsdReaderLicensePayloadDocument)P_1).Revision == ((PsdReaderOfflineLeasePayloadDocument)P_0).Revision && ((PsdReaderLicensePayloadDocument)P_1).StatusCode == ((PsdReaderOfflineLeasePayloadDocument)P_0).StatusCode && ((PsdReaderLicensePayloadDocument)P_1).FeatureMask == ((PsdReaderOfflineLeasePayloadDocument)P_0).FeatureMask && ((PsdReaderLicensePayloadDocument)P_1).SupportUntilUtcTicks == ((PsdReaderOfflineLeasePayloadDocument)P_0).SupportUntilUtcTicks)
								{
									return true;
								}
								P_4 = "离线授权租约与授权文件不一致。";
								P_5 = (LicenseValidationStatus)8;
								return false;
							}
							P_4 = "离线授权租约状态码无效。";
							P_5 = (LicenseValidationStatus)8;
							return false;
						}
						P_4 = "离线授权租约包含未声明功能。";
						P_5 = (LicenseValidationStatus)9;
						return false;
					}
					P_4 = "离线授权租约产品信息不匹配。";
					P_5 = (LicenseValidationStatus)9;
					return false;
				}
				P_4 = "离线授权租约索引不匹配。";
				P_5 = (LicenseValidationStatus)8;
				return false;
			}
			P_4 = "离线授权租约依赖数据无效。";
			P_5 = (LicenseValidationStatus)6;
			return false;
		}
		P_4 = "离线授权租约内容无效。";
		P_5 = (LicenseValidationStatus)8;
		return false;
	}

	private static bool rPHHYjODPh<rk4pxxHtoBjZvax9i2R>(object P_0, object P_1, object P_2, object P_3, object P_4, out rk4pxxHtoBjZvax9i2R P_5, out byte[] P_6, out bool P_7, out OK9cinEpCPEorEb3ebh P_8) where rk4pxxHtoBjZvax9i2R : class
	{
		P_5 = null;
		P_6 = Array.Empty<byte>();
		P_7 = false;
		P_8 = (OK9cinEpCPEorEb3ebh)0;
		if (!jaQHcZMoKR(P_0, P_2, P_3, P_4, out var psdReaderSignedEnvelopeDocument, out P_6, out P_7, out P_8))
		{
			return false;
		}
		if (!LicenseCryptography.TryVerifyAndDecryptSignedEnvelope(P_1, psdReaderSignedEnvelopeDocument, P_3, P_4, out var array))
		{
			P_8 = (OK9cinEpCPEorEb3ebh)3;
			return false;
		}
		if (!QY1HgeZETr<rk4pxxHtoBjZvax9i2R>(array, out P_5))
		{
			P_8 = (OK9cinEpCPEorEb3ebh)4;
			return false;
		}
		return true;
	}

	private static bool CX2H4D8HhS<hY5AidHfmeen52XxobQ>(object P_0, object P_1, object P_2, object P_3, object P_4, object P_5, out hY5AidHfmeen52XxobQ P_6, out byte[] P_7, out bool P_8, out OK9cinEpCPEorEb3ebh P_9, bool P_10 = false) where hY5AidHfmeen52XxobQ : class
	{
		P_6 = null;
		P_7 = Array.Empty<byte>();
		P_8 = false;
		P_9 = (OK9cinEpCPEorEb3ebh)0;
		if (!jaQHcZMoKR(P_0, P_3, P_4, P_5, out var psdReaderSignedEnvelopeDocument, out P_7, out P_8, out P_9, P_10))
		{
			return false;
		}
		if (!string.IsNullOrWhiteSpace((string)P_2) && !string.Equals((string)P_2, psdReaderSignedEnvelopeDocument?.Kid, StringComparison.Ordinal))
		{
			P_9 = (OK9cinEpCPEorEb3ebh)2;
			return false;
		}
		if (!LicenseCryptography.TryVerifyAndDecryptSignedEnvelope(P_1, psdReaderSignedEnvelopeDocument, P_4, P_5, out var array))
		{
			P_9 = (OK9cinEpCPEorEb3ebh)3;
			return false;
		}
		if (!QY1HgeZETr<hY5AidHfmeen52XxobQ>(array, out P_6))
		{
			P_9 = (OK9cinEpCPEorEb3ebh)4;
			return false;
		}
		return true;
	}

	private static bool jaQHcZMoKR(object P_0, object P_1, object P_2, object P_3, out PsdReaderSignedEnvelopeDocument P_4, out byte[] P_5, out bool P_6, out OK9cinEpCPEorEb3ebh P_7, bool P_8 = false)
	{
		P_4 = null;
		P_5 = Array.Empty<byte>();
		P_6 = false;
		P_7 = (OK9cinEpCPEorEb3ebh)0;
		if (owHHXedVws.TryDownloadPayload(((PsdReaderLicenseClientConfigDocument)P_0).RepositoryBaseUrls, (string)P_1, ((PsdReaderLicenseClientConfigDocument)P_0).RequestTimeoutSeconds, out var array, out P_6, out var _, P_8))
		{
			if (LicenseCryptography.TryDecodeBinaryEnvelope(array, P_3, P_2, out P_4) && P_4 != null)
			{
				P_5 = array;
				return true;
			}
			P_7 = (OK9cinEpCPEorEb3ebh)5;
			return false;
		}
		P_7 = (OK9cinEpCPEorEb3ebh)1;
		return false;
	}

	private static bool QY1HgeZETr<yqsdlDHlJEM1wMfohBF>(object P_0, out yqsdlDHlJEM1wMfohBF P_1) where yqsdlDHlJEM1wMfohBF : class
	{
		P_1 = null;
		if (P_0 != null && ((Array)P_0).Length != 0)
		{
			P_1 = (LicensePayloadBinaryReader.TryReadPayloadDocument<yqsdlDHlJEM1wMfohBF>(P_0, out var val) ? val : null);
			return P_1 != null;
		}
		return false;
	}

	private static LicenseValidationStatus A2LHNMYwoZ(OK9cinEpCPEorEb3ebh P_0)
	{
		return P_0 switch
		{
			(OK9cinEpCPEorEb3ebh)1 => (LicenseValidationStatus)5, 
			(OK9cinEpCPEorEb3ebh)2 => (LicenseValidationStatus)8, 
			(OK9cinEpCPEorEb3ebh)3 => (LicenseValidationStatus)8, 
			(OK9cinEpCPEorEb3ebh)4 => (LicenseValidationStatus)8, 
			(OK9cinEpCPEorEb3ebh)5 => (LicenseValidationStatus)8, 
			_ => (LicenseValidationStatus)8, 
		};
	}

	static PsdLicenseService()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		HxaHki07OS = new object();
		owHHXedVws = new LicenseRepositoryClient();
		XiAH7XroGM = new LicenseCacheStore();
		TSHHv3vRBj = new LicenseDeviceIdentifierProvider();
		v3EHdhhO7g = new LicenseTelemetryClient();
		uxhHRepIhf = new HashSet<string>(StringComparer.Ordinal);
		PiLH8JL5Zc = new HashSet<string>(StringComparer.Ordinal);
		BCG316CFVC = int.MinValue;
		dW03hWKoau = string.Empty;
		ULU3pmnc8Q = string.Empty;
		Jby3PH7IrF = string.Empty;
		hid32FLsuw = DateTime.MinValue;
		GOt35kDaQo = string.Empty;
	}
}
}
