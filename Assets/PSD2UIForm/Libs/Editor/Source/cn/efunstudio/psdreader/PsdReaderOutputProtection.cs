using System;
using System.Text;
using PsdLicensing;
using PsdProtectionGuards;
using PsdPreviewProtection;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader
{

public static class PsdReaderOutputProtection
{
	[Serializable]
	private sealed class KPLB49EMQhPRyl6g1xS
	{
		public int YHDEQLLFQP;

		public string OjhEHO3noa;

		public string HmQE31mFu7;

		public string Bs8Esdva7S;

		public string OjjErOa4DU;

		public string RhjEFQMSMg;

		public string HRKEaOOOFW;

		public string eVFE6qgewh;

		public string UxpESJcSUS;

		public int hrJELPUGMD;

		public string rKTE0vRYxp;

		public string t14EEucOxV;

		public string gxvECuJ9lB;

		public string JDIEqUH0Vx;

		public string WwSExiMrOL;

		public long qDAEITD517;

		public string HHUEGXkkAr;

		public string hZeEbM3TIH;

		public string RDOEwr9sdR;

		public string xYaEejmS10;

		public string GFuEW7wppA;

		public string kZlEis1tPS;

		public string FNDEKo1YVT;

		public string YNqEjVd2pb;

		public string u1BEYSRjEv;

		public bool G3uEttmj1C;

		public string H1ME43GqdQ;

		public KPLB49EMQhPRyl6g1xS()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			YHDEQLLFQP = 1;
			OjhEHO3noa = "ProtectedAssetSeal";
			HmQE31mFu7 = "efunstudio";
			Bs8Esdva7S = "psd2ugui";
			OjjErOa4DU = string.Empty;
			RhjEFQMSMg = string.Empty;
			HRKEaOOOFW = string.Empty;
			eVFE6qgewh = string.Empty;
			UxpESJcSUS = string.Empty;
			rKTE0vRYxp = string.Empty;
			t14EEucOxV = string.Empty;
			gxvECuJ9lB = string.Empty;
			JDIEqUH0Vx = string.Empty;
			WwSExiMrOL = string.Empty;
			HHUEGXkkAr = string.Empty;
			hZeEbM3TIH = string.Empty;
			RDOEwr9sdR = string.Empty;
			xYaEejmS10 = string.Empty;
			GFuEW7wppA = string.Empty;
			kZlEis1tPS = string.Empty;
			FNDEKo1YVT = string.Empty;
			YNqEjVd2pb = string.Empty;
			u1BEYSRjEv = string.Empty;
			H1ME43GqdQ = string.Empty;
		}
	}

	internal static ProtectedPreviewRenderer EX0re7VEWt(int P_0, int P_1, int P_2, int P_3, int P_4, object P_5, object P_6, bool P_7 = false)
	{
		PreviewProtectionProfile PreviewProtectionProfile = q7OrKXyH15(P_0, P_1, P_2, P_3, P_4, P_5, P_6, P_7);
		if (PreviewProtectionProfile != null && PreviewProtectionProfile.HasVisibleProtection())
		{
			return new ProtectedPreviewRenderer(PreviewProtectionProfile, P_0, P_1, P_2, P_3, P_4);
		}
		return null;
	}

	internal static string pgArWc2aJD(int P_0, int P_1, int P_2, int P_3, int P_4, object P_5, object P_6, bool P_7 = true)
	{
		PreviewProtectionProfile PreviewProtectionProfile = q7OrKXyH15(P_0, P_1, P_2, P_3, P_4, P_5, P_6, P_7);
		object obj;
		if (PreviewProtectionProfile == null)
		{
			obj = null;
		}
		else
		{
			obj = PreviewProtectionProfile.TnOAZhDjDu();
			if (obj != null)
			{
				goto IL_0026;
			}
		}
		obj = string.Empty;
		goto IL_0026;
		IL_0026:
		return (string)obj;
	}

	internal static void MNLriQIjAl(object P_0, int P_1 = 0)
	{
		if (P_0 != null && !((PsdRenderedImage)P_0).IsEmpty)
		{
			PsdLicenseService.WlEMY3BKXU();
		}
	}

	private static PreviewProtectionProfile q7OrKXyH15(int P_0, int P_1, int P_2, int P_3, int P_4, object P_5, object P_6, bool P_7)
	{
		ProtectedPreviewLicenseState vDmn1VAsK5dMbjWdF = ((!P_7) ? PsdLicenseService.GetPreviewLicenseState() : PsdLicenseService.GetPreviewLicenseStateAfterRefreshAttempt());
		PreviewProtectionProfile PreviewProtectionProfile = PsdLicenseService.CreateLicensedPreviewProfile(vDmn1VAsK5dMbjWdF, P_5, P_6, P_4, P_0, P_1, P_2, P_3);
		if (!PsdLicenseService.MatchesPreviewProfile(vDmn1VAsK5dMbjWdF, PreviewProtectionProfile, P_4, P_0, P_1, P_2, P_3))
		{
			return PsdLicenseService.CreateTrialPreviewProfile(vDmn1VAsK5dMbjWdF, P_5, P_6, P_4, P_0, P_1, P_2, P_3);
		}
		return PreviewProtectionProfile;
	}

	internal static string i8brjPIYqg(object P_0, object P_1, object P_2, object P_3, object P_4, object P_5, object P_6)
	{
		if (P_0 == null)
		{
			return string.Empty;
		}
		return iXjrtAjDhA(XH9rYCmLHV(P_0, string.Empty, P_4, P_5), ((ProtectedPreviewLicenseState)P_0).GetStateFingerprint(), P_1, P_2, P_3, P_6);
	}

	private static KPLB49EMQhPRyl6g1xS XH9rYCmLHV(object P_0, object P_1, object P_2, object P_3)
	{
		DateTime dateTime = ((P_0 == null || ((ProtectedPreviewLicenseState)P_0).GetSessionUtcTicks() <= 0L) ? DateTime.UtcNow : new DateTime(((ProtectedPreviewLicenseState)P_0).GetSessionUtcTicks(), DateTimeKind.Utc));
		DateTime dateTime2 = ((P_0 == null || !(((ProtectedPreviewLicenseState)P_0).GetClockHighWaterUtc() > DateTime.MinValue)) ? dateTime : ((ProtectedPreviewLicenseState)P_0).GetClockHighWaterUtc());
		KPLB49EMQhPRyl6g1xS obj = new KPLB49EMQhPRyl6g1xS
		{
			OjjErOa4DU = WD4rgFA7yQ(P_2),
			RhjEFQMSMg = hsQrc6cGGh(P_3),
			HRKEaOOOFW = Cu1rfTjxfv(P_1)
		};
		object obj2;
		if (P_0 != null)
		{
			obj2 = ((ProtectedPreviewLicenseState)P_0).GetLookupId();
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
		obj.t14EEucOxV = (string)obj3;
		object obj4;
		if (P_0 == null)
		{
			obj4 = null;
		}
		else
		{
			obj4 = ((ProtectedPreviewLicenseState)P_0).GbnA4l2khJ();
			if (obj4 != null)
			{
				goto IL_00fe;
			}
		}
		obj4 = string.Empty;
		goto IL_00fe;
		IL_0119:
		object obj5;
		obj.JDIEqUH0Vx = (string)obj5;
		object obj6;
		if (P_0 == null)
		{
			obj6 = null;
		}
		else
		{
			obj6 = ((ProtectedPreviewLicenseState)P_0).GetAssemblyFingerprint();
			if (obj6 != null)
			{
				goto IL_0134;
			}
		}
		obj6 = string.Empty;
		goto IL_0134;
		IL_00a4:
		object obj7;
		obj.UxpESJcSUS = (string)obj7;
		obj.hrJELPUGMD = ((ProtectedPreviewLicenseState)P_0)?.GetRevision() ?? 0;
		obj.rKTE0vRYxp = dateTime2.ToString("O");
		if (P_0 != null)
		{
			obj3 = ((ProtectedPreviewLicenseState)P_0).GetValidationTranscriptHead();
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
		obj.WwSExiMrOL = (string)obj6;
		obj.qDAEITD517 = dateTime.Ticks;
		obj.HHUEGXkkAr = dateTime.ToString("O");
		obj.G3uEttmj1C = ((ProtectedPreviewLicenseState)P_0)?.GetLicenseValid() ?? false;
		return obj;
		IL_00fe:
		obj.gxvECuJ9lB = (string)obj4;
		if (P_0 != null)
		{
			obj5 = ((ProtectedPreviewLicenseState)P_0).GetProjectScopeFingerprint();
			if (obj5 != null)
			{
				goto IL_0119;
			}
		}
		else
		{
			obj5 = null;
		}
		obj5 = string.Empty;
		goto IL_0119;
		IL_0089:
		obj.eVFE6qgewh = (string)obj2;
		if (P_0 != null)
		{
			obj7 = ((ProtectedPreviewLicenseState)P_0).GetLicenseId();
			if (obj7 != null)
			{
				goto IL_00a4;
			}
		}
		else
		{
			obj7 = null;
		}
		obj7 = string.Empty;
		goto IL_00a4;
	}

	private static string iXjrtAjDhA(object P_0, object P_1, object P_2, object P_3, object P_4, object P_5)
	{
		if (P_0 != null)
		{
			string text = string.Join("|", "psd2ugui", "OutputFamily", LicenseCryptography.GetProtocolVersion(), LicenseCryptography.NormalizeHexString(((KPLB49EMQhPRyl6g1xS)P_0).eVFE6qgewh), LicenseCryptography.NormalizeHexString(((KPLB49EMQhPRyl6g1xS)P_0).UxpESJcSUS), ((KPLB49EMQhPRyl6g1xS)P_0).hrJELPUGMD.ToString(), LicenseCryptography.NormalizeHexString(((KPLB49EMQhPRyl6g1xS)P_0).JDIEqUH0Vx), LicenseCryptography.NormalizeHexString(((KPLB49EMQhPRyl6g1xS)P_0).rKTE0vRYxp), WD4rgFA7yQ(P_2), WD4rgFA7yQ(P_3), WD4rgFA7yQ(P_4), LicenseCryptography.NormalizeHexString(((KPLB49EMQhPRyl6g1xS)P_0).gxvECuJ9lB), WD4rgFA7yQ(((KPLB49EMQhPRyl6g1xS)P_0).OjjErOa4DU), hsQrc6cGGh(((KPLB49EMQhPRyl6g1xS)P_0).RhjEFQMSMg), LicenseCryptography.NormalizeHexString(P_5), LicenseCryptography.NormalizeHexString(((KPLB49EMQhPRyl6g1xS)P_0).t14EEucOxV), LicenseCryptography.NormalizeHexString(((KPLB49EMQhPRyl6g1xS)P_0).WwSExiMrOL), (!((KPLB49EMQhPRyl6g1xS)P_0).G3uEttmj1C) ? "0" : "1");
			return LicenseCryptography.EncodeHex(LicenseCryptography.ComputeStringHmacSha256(AOgr4HGWaq(P_1, P_0), text));
		}
		return string.Empty;
	}

	private static byte[] AOgr4HGWaq(object P_0, object P_1)
	{
		byte[] array = LicenseCryptography.DecodeHex(P_0);
		if (array == null || array.Length == 0)
		{
			array = LicenseCryptography.ComputeStringSha256(P_0 ?? string.Empty);
		}
		return LicenseCryptography.ComputeStringHmacSha256(array, string.Format("{0}|OutputKey|{1}|{2}|{3}|{4}|{5}|{6}", "psd2ugui", LicenseCryptography.GetProtocolVersion(), LicenseCryptography.NormalizeHexString(((KPLB49EMQhPRyl6g1xS)P_1)?.eVFE6qgewh), LicenseCryptography.NormalizeHexString(((KPLB49EMQhPRyl6g1xS)P_1)?.UxpESJcSUS), ((KPLB49EMQhPRyl6g1xS)P_1)?.hrJELPUGMD ?? 0, LicenseCryptography.NormalizeHexString(((KPLB49EMQhPRyl6g1xS)P_1)?.JDIEqUH0Vx), LicenseCryptography.NormalizeHexString(((KPLB49EMQhPRyl6g1xS)P_1)?.WwSExiMrOL)));
	}

	private static string Cu1rfTjxfv(object P_0)
	{
		if (!string.IsNullOrWhiteSpace((string)P_0))
		{
			return ((string)P_0).Replace("\\", "/").Trim();
		}
		return string.Empty;
	}

	private static string hsQrc6cGGh(object P_0)
	{
		if (!string.IsNullOrWhiteSpace((string)P_0))
		{
			return ((string)P_0).Replace("\\", "/").Trim();
		}
		return string.Empty;
	}

	private static string WD4rgFA7yQ(object P_0)
	{
		if (!string.IsNullOrWhiteSpace((string)P_0))
		{
			StringBuilder stringBuilder = new StringBuilder(((string)P_0).Length);
			for (int i = 0; i < ((string)P_0).Length; i++)
			{
				char c = char.ToLowerInvariant(((string)P_0)[i]);
				if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-')
				{
					stringBuilder.Append(c);
				}
			}
			return stringBuilder.ToString();
		}
		return string.Empty;
	}
}
}
