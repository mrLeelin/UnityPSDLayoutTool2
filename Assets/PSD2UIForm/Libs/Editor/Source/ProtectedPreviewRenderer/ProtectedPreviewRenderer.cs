using System;
using System.Collections.Generic;
using PsdProtectionGuards;
using PsdPreviewProtection;
using UnityEngine;
using PsdProtectionRuntime;

namespace PsdPreviewProtection
{

internal sealed class ProtectedPreviewRenderer
{
	private readonly struct lLm2PAE9HSj3vreS8BF
	{
		internal readonly float uMnEmQL4Cn;

		internal readonly float iwvEoMEmDp;

		internal readonly int MBfEJkD065;

		internal readonly int XH3EuDyxFa;

		internal readonly int wBaETPNMxV;

		internal readonly int whAEAlO2YZ;

		private static object fvZcNOZvM3JjNTAfkVpm;

		internal lLm2PAE9HSj3vreS8BF(float P_0, float P_1, int P_2, int P_3, int P_4, int P_5)
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			uMnEmQL4Cn = P_0;
			iwvEoMEmDp = P_1;
			MBfEJkD065 = P_2;
			XH3EuDyxFa = P_3;
			wBaETPNMxV = P_4;
			whAEAlO2YZ = P_5;
		}

		internal static bool BMdivkZvQblMkv8e3kgM()
		{
			return fvZcNOZvM3JjNTAfkVpm == null;
		}

		internal static object HmUwNYZvHSZKZoAkCwdD()
		{
			return fvZcNOZvM3JjNTAfkVpm;
		}
	}

	private readonly PreviewProtectionProfile kf1sgUMLY3;

	private readonly int C2eslGAMCw;

	private readonly int JCnsNlM0pA;

	private readonly int aHcskeGmm1;

	private readonly int B33sXY2eTr;

	private readonly int ieHs7k5wIn;

	private readonly float lbUsvZVFhx;

	private readonly float HGmsdVrj1N;

	private readonly string Lj7sRuQQbo;

	private readonly string NSbs8fbjZY;

	private readonly float u7GsDl6EZW;

	private readonly float ekasVKyWMu;

	private readonly float fHNsy73kjC;

	private readonly float idKszbvG7Z;

	private readonly float JqCr1UZuW6;

	private readonly float MtZrZAEVHT;

	private readonly float WpjrObusv9;

	private readonly float znErh5F4y4;

	private readonly float vAlrnjBIPh;

	private readonly float PWmrpgq9Z3;

	private readonly float G17rP0keVw;

	private readonly float mSZr28s45X;

	private readonly float KNyr5GsSYZ;

	private readonly float sy1rBRrTBx;

	private readonly float VGUrUpRhk6;

	private readonly float tT6r9vL1ig;

	private readonly float in9rmKqniw;

	private readonly float VAeroqqDWW;

	private readonly Color32 XKRrJiZyIa;

	private readonly Color32 o0nrum8ktt;

	private readonly Color32 yAqrT9Hq2R;

	private readonly Color32 WcsrA7GI62;

	private readonly bool DjurMtvnHF;

	private readonly bool XDwrQX7e9Q;

	private readonly bool wNRrHwyKr0;

	private readonly bool bjOr30UqTK;

	private readonly bool LLGrsnELuk;

	private readonly bool gcirrTftX5;

	private readonly WatermarkCoverageBitmap gtGrFXuj1M;

	private readonly WatermarkCoverageBitmap ulHraZNJM9;

	private readonly lLm2PAE9HSj3vreS8BF[] Vcsr6sHBq4;

	private readonly float YfRrSfn08O;

	private readonly float OjArLE0wKQ;

	private readonly float SB4r0Ww0BY;

	private readonly float fiprEX0NgU;

	private readonly float R2FrCPtkor;

	private readonly float I0grqdnC0W;

	private readonly float qRTrxh7NCD;

	private readonly float MRZrIGxMf5;

	private readonly float wMGrGJHaEY;

	private readonly float b2rrbVxqLd;

	private readonly float hFSrwentvq;

	internal ProtectedPreviewRenderer(PreviewProtectionProfile P_0, int P_1, int P_2, int P_3, int P_4, int P_5)
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		kf1sgUMLY3 = P_0 ?? new PreviewProtectionProfile();
		C2eslGAMCw = P_1;
		JCnsNlM0pA = P_2;
		aHcskeGmm1 = Math.Max(0, P_3);
		B33sXY2eTr = Math.Max(0, P_4);
		ieHs7k5wIn = P_5;
		Lj7sRuQQbo = "试用版";
		NSbs8fbjZY = "efunstudio.cn";
		DjurMtvnHF = (kf1sgUMLY3.GetProtectionFlags() & (PreviewProtectionFlags)1) == (PreviewProtectionFlags)1;
		XDwrQX7e9Q = (kf1sgUMLY3.GetProtectionFlags() & (PreviewProtectionFlags)2) == (PreviewProtectionFlags)2;
		wNRrHwyKr0 = (kf1sgUMLY3.GetProtectionFlags() & (PreviewProtectionFlags)16) == (PreviewProtectionFlags)16;
		bjOr30UqTK = kf1sgUMLY3.gjjTWI68cj() > 0;
		LLGrsnELuk = (kf1sgUMLY3.GetProtectionFlags() & (PreviewProtectionFlags)128) == (PreviewProtectionFlags)128;
		gcirrTftX5 = wNRrHwyKr0;
		YfRrSfn08O = (float)(aHcskeGmm1 - 1) * 0.5f;
		OjArLE0wKQ = (float)(B33sXY2eTr - 1) * 0.5f;
		SB4r0Ww0BY = Mathf.Sqrt(aHcskeGmm1 * aHcskeGmm1 + B33sXY2eTr * B33sXY2eTr);
		fiprEX0NgU = Math.Max(1f, Math.Min(Math.Max(1, aHcskeGmm1), Math.Max(1, B33sXY2eTr)));
		R2FrCPtkor = Math.Max(1f, (float)aHcskeGmm1 - 1f);
		I0grqdnC0W = Math.Max(1f, (float)B33sXY2eTr - 1f);
		float num = Math.Max(24f, fiprEX0NgU);
		float val = PreviewWatermarkBitmapFont.lxL3jFuJQ5(Lj7sRuQQbo, 1f);
		float val2 = PreviewWatermarkBitmapFont.DXm3YbEbAW(Lj7sRuQQbo, 1f);
		float val3 = PreviewWatermarkBitmapFont.lxL3jFuJQ5(NSbs8fbjZY, 1f);
		float val4 = PreviewWatermarkBitmapFont.DXm3YbEbAW(NSbs8fbjZY, 1f);
		float val5 = Mathf.Clamp(num / (23.5f + (float)(int)kf1sgUMLY3.w3RTbRHx9B() * 0.64f), 1.25f, 7.2f);
		float val6 = Math.Max(0.95f, Math.Min(Math.Max(44f, (float)aHcskeGmm1 * 0.72f) / Math.Max(1f, val), Math.Max(18f, (float)B33sXY2eTr * 0.24f) / Math.Max(1f, val2)));
		u7GsDl6EZW = Mathf.Clamp(Math.Min(val5, val6), 0.95f, 7.2f);
		float val7 = Math.Max(1.4f, Math.Min(Math.Max(72f, (float)aHcskeGmm1 * 1.28f) / Math.Max(1f, val3), Math.Max(22f, (float)B33sXY2eTr * 0.3f) / Math.Max(1f, val4)));
		ekasVKyWMu = Mathf.Clamp(Math.Min(u7GsDl6EZW * 1.14f, val7), 1.4f, 10.5f);
		fHNsy73kjC = PreviewWatermarkBitmapFont.lxL3jFuJQ5(Lj7sRuQQbo, u7GsDl6EZW);
		JqCr1UZuW6 = PreviewWatermarkBitmapFont.DXm3YbEbAW(Lj7sRuQQbo, u7GsDl6EZW);
		float jqCr1UZuW = JqCr1UZuW6;
		float num2 = Math.Min(ekasVKyWMu * 2f, (float)aHcskeGmm1 / Math.Max(1f, val3));
		idKszbvG7Z = PreviewWatermarkBitmapFont.lxL3jFuJQ5(NSbs8fbjZY, num2);
		MtZrZAEVHT = PreviewWatermarkBitmapFont.DXm3YbEbAW(NSbs8fbjZY, num2);
		float num3 = Math.Max(aHcskeGmm1, B33sXY2eTr);
		WpjrObusv9 = Mathf.Max(jqCr1UZuW * 1.15f, Math.Max(16f, num3 * 0.38f));
		znErh5F4y4 = Mathf.Max(fHNsy73kjC * 0.08f, Math.Max(6f, (float)aHcskeGmm1 * 0.02f));
		float num4 = (float)aHcskeGmm1 / (float)Math.Max(1, B33sXY2eTr);
		float num5 = (((kf1sgUMLY3.GetStableSeed() & 1) != 0) ? (-1f) : 1f);
		vAlrnjBIPh = num5 * ((num4 >= 1.4f) ? 0.22f : 0.16f) + kBvsieJXNV((byte)((kf1sgUMLY3.GetStableSeed() >> 16) & 0xFF)) * 0.06f;
		PWmrpgq9Z3 = (0f - num5) * 0.12f + kBvsieJXNV((byte)((kf1sgUMLY3.GetSessionSeed() >> 16) & 0xFF)) * 0.05f;
		G17rP0keVw = fHNsy73kjC * (0.18f + (float)(kf1sgUMLY3.xpdTxouQpO() - 5) * 0.025f);
		mSZr28s45X = 0f;
		float num6 = kBvsieJXNV((byte)(kf1sgUMLY3.naLTgkaVgS() ^ (kf1sgUMLY3.GetStableSeed() & 0xFF))) * Math.Max(4f, (float)aHcskeGmm1 * 0.12f);
		float num7 = kBvsieJXNV((byte)(kf1sgUMLY3.naLTgkaVgS() ^ ((kf1sgUMLY3.GetStableSeed() >> 8) & 0xFF))) * Math.Max(4f, (float)B33sXY2eTr * 0.08f);
		float num8 = kBvsieJXNV((byte)(kf1sgUMLY3.GetSessionSeed() & 0xFF)) * Math.Max(2f, (float)aHcskeGmm1 * 0.025f);
		float num9 = kBvsieJXNV((byte)((kf1sgUMLY3.GetSessionSeed() >> 8) & 0xFF)) * Math.Max(2f, (float)B33sXY2eTr * 0.02f);
		lbUsvZVFhx = (float)(aHcskeGmm1 - 1) * 0.5f + num6 + num8;
		HGmsdVrj1N = (float)(B33sXY2eTr - 1) * 0.5f + num7 + num9;
		byte r = (byte)(168 + ((kf1sgUMLY3.GetStableSeed() >> 3) & 0x1F));
		byte g = (byte)(152 + ((kf1sgUMLY3.GetSessionSeed() >> 5) & 0x2F));
		byte b = (byte)(92 + ((kf1sgUMLY3.GetStableSeed() >> 11) & 0x3F));
		XKRrJiZyIa = new Color32(248, 248, 248, byte.MaxValue);
		o0nrum8ktt = new Color32(16, 16, 16, byte.MaxValue);
		yAqrT9Hq2R = new Color32(r, g, b, byte.MaxValue);
		WcsrA7GI62 = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
		qRTrxh7NCD = Math.Max(7f, fiprEX0NgU * (0.06f + (float)(int)kf1sgUMLY3.WFaTjh9q7a() / 255f * 0.03f));
		MRZrIGxMf5 = qRTrxh7NCD * (0.82f + (float)(int)kf1sgUMLY3.vL5T4Sw8Hv() / 255f * 0.16f);
		wMGrGJHaEY = Math.Max(4.5f, fiprEX0NgU * (0.038f + (float)(int)kf1sgUMLY3.auwTE03s0j() / 255f * 0.022f));
		b2rrbVxqLd = Math.Max(14f, fiprEX0NgU * (0.16f + (float)(kf1sgUMLY3.xpdTxouQpO() % 5) * 0.018f));
		hFSrwentvq = Math.Max(14f, fiprEX0NgU * (0.16f + (float)(kf1sgUMLY3.w3RTbRHx9B() % 5) * 0.018f));
		gtGrFXuj1M = ((!DjurMtvnHF) ? WatermarkCoverageBitmap.GetEmpty() : PreviewWatermarkBitmapFont.RasterizeCoverageBitmap(Lj7sRuQQbo, u7GsDl6EZW));
		ulHraZNJM9 = (XDwrQX7e9Q ? PreviewWatermarkBitmapFont.RasterizeCoverageBitmap(NSbs8fbjZY, num2) : WatermarkCoverageBitmap.GetEmpty());
		gmcsu1CunK(aHcskeGmm1, B33sXY2eTr, out KNyr5GsSYZ, out sy1rBRrTBx, out VGUrUpRhk6, out tT6r9vL1ig);
		in9rmKqniw = Math.Max(10f, idKszbvG7Z * 0.18f);
		VAeroqqDWW = Math.Max(8f, MtZrZAEVHT * 0.2f);
		Vcsr6sHBq4 = qCisJnN7bf();
	}

	internal void RVLso9nLBq(byte[] P_0, int P_1, int P_2)
	{
		if (P_0 != null && P_1 > 0 && P_2 > 0 && kf1sgUMLY3.HasVisibleProtection())
		{
			long num = (long)P_1 * (long)P_2 * 4L;
			if (num > 0L && num <= P_0.Length)
			{
				pv2sTlBGa6(P_0, P_1, P_2);
				MIpsAvyUZA(P_0, P_1, P_2);
			}
		}
	}

	private lLm2PAE9HSj3vreS8BF[] qCisJnN7bf()
	{
		if (DjurMtvnHF && gtGrFXuj1M.HasCoverage() && !(fHNsy73kjC <= 0f) && !(JqCr1UZuW6 <= 0f) && aHcskeGmm1 > 0 && B33sXY2eTr > 0)
		{
			float num = 0.70710677f;
			float num2 = 0.70710677f;
			float num3 = -0.70710677f;
			float num4 = num;
			float num5 = Math.Max(JqCr1UZuW6 * 1.15f, WpjrObusv9);
			float num6 = fHNsy73kjC + Math.Max(u7GsDl6EZW * 2f, znErh5F4y4);
			if (!(num5 <= 0.001f) && num6 > 0.001f)
			{
				float num7 = float.MaxValue;
				float num8 = float.MinValue;
				float num9 = float.MaxValue;
				float num10 = float.MinValue;
				float yfRrSfn08O = YfRrSfn08O;
				float ojArLE0wKQ = OjArLE0wKQ;
				float[] array = new float[2]
				{
					0f,
					Math.Max(0f, (float)aHcskeGmm1 - 1f)
				};
				float[] array2 = new float[2]
				{
					0f,
					Math.Max(0f, (float)B33sXY2eTr - 1f)
				};
				for (int i = 0; i < array2.Length; i++)
				{
					for (int j = 0; j < array.Length; j++)
					{
						float num11 = array[j] - yfRrSfn08O;
						float num12 = array2[i] - ojArLE0wKQ;
						float val = num11 * num + num12 * num2;
						float val2 = num11 * num3 + num12 * num4;
						num7 = Math.Min(num7, val);
						num8 = Math.Max(num8, val);
						num9 = Math.Min(num9, val2);
						num10 = Math.Max(num10, val2);
					}
				}
				float num13 = JqCr1UZuW6 + Math.Abs(G17rP0keVw) + Math.Max(4f, u7GsDl6EZW * 1.5f);
				float num14 = fHNsy73kjC + Math.Abs(vAlrnjBIPh) * JqCr1UZuW6 + Math.Max(4f, u7GsDl6EZW * 1.5f);
				bool flag;
				float num15 = ((!(flag = XDwrQX7e9Q && ulHraZNJM9.HasCoverage())) ? 0f : (KNyr5GsSYZ - in9rmKqniw));
				float num16 = ((!flag) ? (-1f) : (VGUrUpRhk6 + in9rmKqniw));
				float num17 = ((!flag) ? 0f : (sy1rBRrTBx - VAeroqqDWW));
				float num18 = (flag ? (tT6r9vL1ig + VAeroqqDWW) : (-1f));
				int num19 = (int)Math.Floor((num9 - num13) / num5) - 2;
				int num20 = (int)Math.Ceiling((num10 + num13) / num5) + 2;
				int num21 = (int)Math.Floor((num7 - num14) / num6) - 2;
				int num22 = (int)Math.Ceiling((num8 + num14) / num6) + 2;
				float num23 = Math.Abs(vAlrnjBIPh) * JqCr1UZuW6 * 0.5f;
				List<lLm2PAE9HSj3vreS8BF> list = new List<lLm2PAE9HSj3vreS8BF>();
				for (int k = num19; k <= num20; k++)
				{
					float num24 = (float)k * num5;
					if (k != 0)
					{
						float num25 = (tOBsjKlLwI(kf1sgUMLY3.GetStableSeed() ^ 0x23B1, k, ieHs7k5wIn, 1) - 0.5f) * Math.Max(1f, u7GsDl6EZW * 0.65f);
						num24 += (float)k * G17rP0keVw * 0.14f + num25;
					}
					float num26 = yfRrSfn08O + num3 * num24;
					float num27 = ojArLE0wKQ + num4 * num24;
					for (int l = num21; l <= num22; l++)
					{
						float num28 = (float)l * num6;
						if (l != 0 || k != 0)
						{
							float num29 = (tOBsjKlLwI(kf1sgUMLY3.GetSessionSeed() ^ 0xBDE3, l, k, 1) - 0.5f) * Math.Max(0.75f, u7GsDl6EZW * (0.32f + Math.Abs(vAlrnjBIPh) * 0.85f));
							num28 += num29 + (float)k * G17rP0keVw * 0.08f;
						}
						float num30 = num26 + num * num28;
						float num31 = num27 + num2 * num28;
						float num32 = num30 - fHNsy73kjC * 0.5f;
						float num33 = num31 - JqCr1UZuW6 * 0.5f;
						int num34 = Math.Max(0, (int)Math.Floor(num32 - num23) - 1);
						int num35 = Math.Min(aHcskeGmm1 - 1, (int)Math.Ceiling(num32 + fHNsy73kjC + num23) + 1);
						int num36 = Math.Max(0, (int)Math.Floor(num33) - 1);
						int num37 = Math.Min(B33sXY2eTr - 1, (int)Math.Ceiling(num33 + JqCr1UZuW6) + 1);
						if (num34 <= num35 && num36 <= num37 && (!flag || (float)num35 < num15 || (float)num34 > num16 || (float)num37 < num17 || (float)num36 > num18))
						{
							list.Add(new lLm2PAE9HSj3vreS8BF(num32, num33, num34, num35, num36, num37));
						}
					}
				}
				if (list.Count <= 0)
				{
					return Array.Empty<lLm2PAE9HSj3vreS8BF>();
				}
				return list.ToArray();
			}
			return Array.Empty<lLm2PAE9HSj3vreS8BF>();
		}
		return Array.Empty<lLm2PAE9HSj3vreS8BF>();
	}

	private void gmcsu1CunK(int P_0, int P_1, out float P_2, out float P_3, out float P_4, out float P_5)
	{
		P_2 = 0f;
		P_3 = 0f;
		P_4 = 0f;
		P_5 = 0f;
		if (P_0 > 0 && P_1 > 0 && XDwrQX7e9Q && ulHraZNJM9.HasCoverage() && !(idKszbvG7Z <= 0f) && MtZrZAEVHT > 0f)
		{
			P_2 = Mathf.Max(0f, ((float)P_0 - idKszbvG7Z) * 0.5f);
			P_3 = Mathf.Max(0f, ((float)P_1 - MtZrZAEVHT) * 0.5f);
			P_4 = Math.Min(P_0, P_2 + idKszbvG7Z);
			P_5 = Math.Min(P_1, P_3 + MtZrZAEVHT);
		}
	}

	private void pv2sTlBGa6(byte[] P_0, int P_1, int P_2)
	{
		if (P_0 == null || P_1 <= 0 || P_2 <= 0 || !DjurMtvnHF || !gtGrFXuj1M.HasCoverage() || Vcsr6sHBq4 == null || Vcsr6sHBq4.Length == 0 || fHNsy73kjC <= 0f || !(JqCr1UZuW6 > 0f))
		{
			return;
		}
		float num = (float)(int)kf1sgUMLY3.GvsTSpIpvK() / 255f * 0.96f;
		if (num <= 0f)
		{
			return;
		}
		for (int i = 0; i < Vcsr6sHBq4.Length; i++)
		{
			lLm2PAE9HSj3vreS8BF lLm2PAE9HSj3vreS8BF = Vcsr6sHBq4[i];
			for (int j = lLm2PAE9HSj3vreS8BF.wBaETPNMxV; j <= lLm2PAE9HSj3vreS8BF.whAEAlO2YZ; j++)
			{
				int num2 = (P_2 - 1 - j) * P_1 * 4;
				float num3 = (float)j - lLm2PAE9HSj3vreS8BF.iwvEoMEmDp;
				float num4 = (num3 - JqCr1UZuW6 * 0.5f) * vAlrnjBIPh;
				for (int k = lLm2PAE9HSj3vreS8BF.MBfEJkD065; k <= lLm2PAE9HSj3vreS8BF.XH3EuDyxFa; k++)
				{
					int num5 = num2 + k * 4;
					byte b = P_0[num5 + 3];
					if (b == 0 || (XDwrQX7e9Q && (float)k >= KNyr5GsSYZ - in9rmKqniw && (float)k <= VGUrUpRhk6 + in9rmKqniw && (float)j >= sy1rBRrTBx - VAeroqqDWW && (float)j <= tT6r9vL1ig + VAeroqqDWW))
					{
						continue;
					}
					float num6 = (float)k - lLm2PAE9HSj3vreS8BF.uMnEmQL4Cn - num4;
					if (num6 < -1f || num6 > fHNsy73kjC + 1f || num3 < -1f || num3 > JqCr1UZuW6 + 1f)
					{
						continue;
					}
					float num7 = gtGrFXuj1M.SampleBilinearCoverage(num6, num3);
					float num8 = LlwsQJHil8(gtGrFXuj1M, num6, num3, num7);
					if (num7 <= 0f && num8 <= 0f)
					{
						continue;
					}
					float num9 = MathF.Sqrt(MathF.Max(0f, (float)(int)b / 255f));
					float num10 = num7 * num * num9;
					float num11 = num8 * R9MsMtMCRF() * num9;
					if (!(num10 <= 0f) || !(num11 <= 0f))
					{
						byte b2 = P_0[num5];
						byte b3 = P_0[num5 + 1];
						byte b4 = P_0[num5 + 2];
						if (num11 > 0f)
						{
							KmbsYFx7KL(ref b2, ref b3, ref b4, Ee4sWYJ6rT(b2, b3, b4), num11);
						}
						if (num10 > 0f)
						{
							KmbsYFx7KL(ref b2, ref b3, ref b4, sDQsejI4HL(b2, b3, b4), num10);
						}
						P_0[num5] = b2;
						P_0[num5 + 1] = b3;
						P_0[num5 + 2] = b4;
					}
				}
			}
		}
	}

	private void MIpsAvyUZA(byte[] P_0, int P_1, int P_2)
	{
		if (P_0 == null || P_1 <= 0 || P_2 <= 0 || !XDwrQX7e9Q || !ulHraZNJM9.HasCoverage() || idKszbvG7Z <= 0f || !(MtZrZAEVHT > 0f))
		{
			return;
		}
		float kNyr5GsSYZ = KNyr5GsSYZ;
		float num = sy1rBRrTBx;
		float num2 = Math.Abs(PWmrpgq9Z3) * MtZrZAEVHT * 0.5f;
		int num3 = Math.Max(0, (int)Math.Floor(kNyr5GsSYZ - num2) - 1);
		int num4 = Math.Min(P_1 - 1, (int)Math.Ceiling(VGUrUpRhk6) + 1);
		int num5 = Math.Max(0, (int)Math.Floor(num) - 1);
		int num6 = Math.Min(P_2 - 1, (int)Math.Ceiling(num + MtZrZAEVHT) + 1);
		if (num3 > num4 || num5 > num6)
		{
			return;
		}
		float num7 = (float)(int)kf1sgUMLY3.auwTE03s0j() / 255f * 0.96f;
		if (num7 <= 0f)
		{
			return;
		}
		for (int i = num5; i <= num6; i++)
		{
			int num8 = (P_2 - 1 - i) * P_1 * 4;
			float num9 = (float)i - num;
			float num10 = (num9 - MtZrZAEVHT * 0.5f) * PWmrpgq9Z3;
			for (int j = num3; j <= num4; j++)
			{
				int num11 = num8 + j * 4;
				byte b = P_0[num11];
				byte b2 = P_0[num11 + 1];
				byte b3 = P_0[num11 + 2];
				byte b4 = P_0[num11 + 3];
				if (b4 == 0)
				{
					continue;
				}
				float num12 = (float)j - kNyr5GsSYZ - num10;
				if (num12 < -1f || num12 > idKszbvG7Z + 1f || num9 < -1f || num9 > MtZrZAEVHT + 1f)
				{
					continue;
				}
				float num13 = ulHraZNJM9.SampleBilinearCoverage(num12, num9);
				float num14 = LlwsQJHil8(ulHraZNJM9, num12, num9, num13);
				if (num13 <= 0f && num14 <= 0f)
				{
					continue;
				}
				float num15 = MathF.Sqrt(MathF.Max(0f, (float)(int)b4 / 255f));
				float num16 = num13 * num7 * num15;
				float num17 = num14 * R9MsMtMCRF() * num15;
				if (!(num16 <= 0f) || !(num17 <= 0f))
				{
					if (num17 > 0f)
					{
						KmbsYFx7KL(ref b, ref b2, ref b3, Ee4sWYJ6rT(b, b2, b3), num17);
					}
					if (num16 > 0f)
					{
						KmbsYFx7KL(ref b, ref b2, ref b3, sDQsejI4HL(b, b2, b3), num16);
					}
					P_0[num11] = b;
					P_0[num11 + 1] = b2;
					P_0[num11 + 2] = b3;
					P_0[num11 + 3] = b4;
				}
			}
		}
	}

	private float R9MsMtMCRF()
	{
		if (LLGrsnELuk)
		{
			return (float)(int)kf1sgUMLY3.vL5T4Sw8Hv() / 255f * 0.82f;
		}
		return 0f;
	}

	private static float LlwsQJHil8(object P_0, float P_1, float P_2, float P_3)
	{
		if (P_0 != null && ((WatermarkCoverageBitmap)P_0).HasCoverage())
		{
			float num = Math.Max(Math.Max(((WatermarkCoverageBitmap)P_0).SampleBilinearCoverage(P_1 - 1f, P_2), ((WatermarkCoverageBitmap)P_0).SampleBilinearCoverage(P_1 + 1f, P_2)), Math.Max(((WatermarkCoverageBitmap)P_0).SampleBilinearCoverage(P_1, P_2 - 1f), ((WatermarkCoverageBitmap)P_0).SampleBilinearCoverage(P_1, P_2 + 1f)));
			return Mathf.Clamp01(num - P_3 + ((!(num > 0f) || P_3 <= 0f) ? 0f : 0.18f));
		}
		return 0f;
	}

	private byte[] O1isHuQKOy(int P_0, int P_1)
	{
		if (DjurMtvnHF && P_0 > 0 && P_1 > 0)
		{
			long num = (long)P_0 * (long)P_1;
			if (num > 0L && num <= 2147483647L)
			{
				byte[] array = new byte[(int)num];
				for (int i = 0; i < P_1; i++)
				{
					for (int j = 0; j < P_0; j++)
					{
						int num2 = i * P_0 + j;
						array[num2] = VUOsLe8gUS(diqsC8O1Bv(j, i));
					}
				}
				return array;
			}
			return null;
		}
		return null;
	}

	internal void fYDs36wh4j(ref byte P_0, ref byte P_1, ref byte P_2, ref byte P_3, int P_4, int P_5)
	{
		MhFsF3IKRS(ref P_0, ref P_1, ref P_2, ref P_3, P_4, P_5, 1f, 0.52f, 0.68f);
	}

	internal void uIJssB9koo(ref byte P_0, ref byte P_1, ref byte P_2, ref byte P_3, int P_4, int P_5)
	{
		MhFsF3IKRS(ref P_0, ref P_1, ref P_2, ref P_3, P_4, P_5, 0.36f, 0.78f, 0.42f);
	}

	internal void TCesrtmv6E(ref byte P_0, int P_1, int P_2)
	{
		if (!bjOr30UqTK)
		{
			return;
		}
		float num = NptsxF0OnG(P_1, P_2);
		if (!(num * 1.08f <= 0f))
		{
			float num2 = num;
			float num3 = pHKsI29WID(P_1, P_2, num * 1.02f);
			float num4 = MathF.Sqrt(MathF.Max(0f, (float)(int)P_0 / 255f)) * MathF.Max(MathF.Max(num2 * 0.52f, num * 0.3f), num3 * 0.28f) * ((float)(int)kf1sgUMLY3.gjjTWI68cj() / 255f) * 0.58f;
			if (!(num4 <= 0f))
			{
				int num5 = (int)Math.Round((tOBsjKlLwI(kf1sgUMLY3.GetSessionSeed() ^ kf1sgUMLY3.GetStableSeed(), C2eslGAMCw + P_1, JCnsNlM0pA + P_2, 23) - 0.5f) * 2f * (float)(int)kf1sgUMLY3.gjjTWI68cj() * num4, MidpointRounding.AwayFromZero);
				P_0 = BR5s4BO3Gp(P_0 + num5);
			}
		}
	}

	private void MhFsF3IKRS(ref byte P_0, ref byte P_1, ref byte P_2, ref byte P_3, int P_4, int P_5, float P_6, float P_7, float P_8)
	{
		float num = (gcirrTftX5 ? NptsxF0OnG(P_4, P_5) : 0f);
		float num2 = num * 1.1f;
		float num3 = ((!LLGrsnELuk) ? 0f : num);
		float num4 = ((!gcirrTftX5) ? 0f : pHKsI29WID(P_4, P_5, num * 1.04f));
		float num5 = MathF.Sqrt(MathF.Max(0f, (float)(int)P_3 / 255f));
		float num6 = num5 * num2 * P_6 * ((float)(int)kf1sgUMLY3.GvsTSpIpvK() / 255f);
		float num7 = num5 * MathF.Max(num3 * 1f, num * 0.34f) * P_7 * ((float)(int)kf1sgUMLY3.vL5T4Sw8Hv() / 255f);
		float num8 = num5 * num4 * P_8 * ((float)(int)kf1sgUMLY3.auwTE03s0j() / 255f) * 0.96f;
		if (num6 > 0f)
		{
			Color32 color = ((!gcirrTftX5) ? sDQsejI4HL(P_0, P_1, P_2) : WcsrA7GI62);
			float num9 = num6 * (0.42f + (float)(int)kf1sgUMLY3.WFaTjh9q7a() / 255f * 0.68f);
			KmbsYFx7KL(ref P_0, ref P_1, ref P_2, color, num9);
		}
		if (num7 > 0f)
		{
			KmbsYFx7KL(ref P_0, ref P_1, ref P_2, gcirrTftX5 ? WcsrA7GI62 : Ee4sWYJ6rT(P_0, P_1, P_2), num7);
		}
		if (num8 > 0f)
		{
			KmbsYFx7KL(ref P_0, ref P_1, ref P_2, (!gcirrTftX5) ? yAqrT9Hq2R : WcsrA7GI62, num8);
		}
		float num10 = num7 * 0.34f + num8 * 0.2f;
		if (num10 > 0f && bjOr30UqTK)
		{
			int num11 = (int)Math.Round((tOBsjKlLwI(kf1sgUMLY3.GetStableSeed(), C2eslGAMCw + P_4, JCnsNlM0pA + P_5, 41) - 0.5f) * 2f * (float)(int)kf1sgUMLY3.gjjTWI68cj() * num10, MidpointRounding.AwayFromZero);
			P_3 = BR5s4BO3Gp(P_3 + num11);
		}
	}

	private void OEEsaNt9Jv(ref byte P_0, ref byte P_1, ref byte P_2, ref byte P_3, int P_4, int P_5, byte[] P_6, int P_7, int P_8)
	{
		float num = (float)(int)P_3 / 255f;
		float num2 = 0.36f + num * 0.64f;
		float num3 = 0.78f - num * 0.26f;
		float num4 = 0.42f + num * 0.26f;
		UgHsSMOyHA(ref P_0, ref P_1, ref P_2, ref P_3, P_4, P_5, num2, num3, num4, P_6, P_7, P_8);
	}

	private void byRs6En19J(ref byte P_0, ref byte P_1, ref byte P_2, ref byte P_3, int P_4, int P_5)
	{
		float num = (float)(int)P_3 / 255f;
		float num2 = 0.36f + num * 0.64f;
		float num3 = 0.78f - num * 0.26f;
		float num4 = 0.42f + num * 0.26f;
		MhFsF3IKRS(ref P_0, ref P_1, ref P_2, ref P_3, P_4, P_5, num2, num3, num4);
	}

	private void UgHsSMOyHA(ref byte P_0, ref byte P_1, ref byte P_2, ref byte P_3, int P_4, int P_5, float P_6, float P_7, float P_8, byte[] P_9, int P_10, int P_11)
	{
		float num = (gcirrTftX5 ? NptsxF0OnG(P_4, P_5) : 0f);
		float num2 = num * 1.1f;
		float num3 = (LLGrsnELuk ? num : 0f);
		float num4 = ((!gcirrTftX5) ? 0f : pHKsI29WID(P_4, P_5, num * 1.04f));
		float num5 = MathF.Sqrt(MathF.Max(0f, (float)(int)P_3 / 255f));
		float num6 = num5 * num2 * P_6 * ((float)(int)kf1sgUMLY3.GvsTSpIpvK() / 255f);
		float num7 = num5 * MathF.Max(num3 * 1f, num * 0.34f) * P_7 * ((float)(int)kf1sgUMLY3.vL5T4Sw8Hv() / 255f);
		float num8 = num5 * num4 * P_8 * ((float)(int)kf1sgUMLY3.auwTE03s0j() / 255f) * 0.96f;
		if (num6 > 0f)
		{
			Color32 color = ((!gcirrTftX5) ? sDQsejI4HL(P_0, P_1, P_2) : WcsrA7GI62);
			float num9 = num6 * (0.42f + (float)(int)kf1sgUMLY3.WFaTjh9q7a() / 255f * 0.68f);
			KmbsYFx7KL(ref P_0, ref P_1, ref P_2, color, num9);
		}
		if (num7 > 0f)
		{
			KmbsYFx7KL(ref P_0, ref P_1, ref P_2, gcirrTftX5 ? WcsrA7GI62 : Ee4sWYJ6rT(P_0, P_1, P_2), num7);
		}
		if (num8 > 0f)
		{
			KmbsYFx7KL(ref P_0, ref P_1, ref P_2, (!gcirrTftX5) ? yAqrT9Hq2R : WcsrA7GI62, num8);
		}
		float num10 = num7 * 0.34f + num8 * 0.2f;
		if (num10 > 0f && bjOr30UqTK)
		{
			int num11 = (int)Math.Round((tOBsjKlLwI(kf1sgUMLY3.GetStableSeed(), C2eslGAMCw + P_4, JCnsNlM0pA + P_5, 41) - 0.5f) * 2f * (float)(int)kf1sgUMLY3.gjjTWI68cj() * num10, MidpointRounding.AwayFromZero);
			P_3 = BR5s4BO3Gp(P_3 + num11);
		}
	}

	private static byte VUOsLe8gUS(float P_0)
	{
		P_0 = Math.Min(1f, Math.Max(0f, P_0));
		return (byte)Math.Round(P_0 * 255f, MidpointRounding.AwayFromZero);
	}

	private float VvRs0rPw3I(byte[] P_0, int P_1, int P_2, int P_3, int P_4)
	{
		if (DjurMtvnHF && P_0 != null && P_1 > 0 && P_2 > 0 && P_3 >= 0 && P_4 >= 0 && P_3 < P_1 && P_4 < P_2)
		{
			int num = P_4 * P_1 + P_3;
			if (num >= 0 && num < P_0.Length)
			{
				return (float)(int)P_0[num] / 255f;
			}
			return 0f;
		}
		return 0f;
	}

	private float DfIsESBT2g(byte[] P_0, int P_1, int P_2, int P_3, int P_4, float P_5)
	{
		if (LLGrsnELuk)
		{
			float num = Math.Max(Math.Max(VvRs0rPw3I(P_0, P_1, P_2, P_3 - 1, P_4), VvRs0rPw3I(P_0, P_1, P_2, P_3 + 1, P_4)), Math.Max(VvRs0rPw3I(P_0, P_1, P_2, P_3, P_4 - 1), VvRs0rPw3I(P_0, P_1, P_2, P_3, P_4 + 1)));
			return Mathf.Clamp01(num - P_5 + ((!(num > 0f) || P_5 <= 0f) ? 0f : 0.18f));
		}
		return 0f;
	}

	private float diqsC8O1Bv(int P_0, int P_1)
	{
		if (!DjurMtvnHF)
		{
			return 0f;
		}
		return nfNswyou22(gtGrFXuj1M, Lj7sRuQQbo, u7GsDl6EZW, fHNsy73kjC, JqCr1UZuW6, WpjrObusv9, znErh5F4y4, vAlrnjBIPh, G17rP0keVw, mSZr28s45X, P_0, P_1, 1, false);
	}

	private float r37sqdj2uS(int P_0, int P_1, float P_2)
	{
		if (LLGrsnELuk)
		{
			float num = Math.Max(Math.Max(diqsC8O1Bv(P_0 - 1, P_1), diqsC8O1Bv(P_0 + 1, P_1)), Math.Max(diqsC8O1Bv(P_0, P_1 - 1), diqsC8O1Bv(P_0, P_1 + 1)));
			return Mathf.Clamp01(num - P_2 + ((!(num > 0f) || P_2 <= 0f) ? 0f : 0.18f));
		}
		return 0f;
	}

	private float NptsxF0OnG(int P_0, int P_1)
	{
		if (!gcirrTftX5)
		{
			return 0f;
		}
		float a = D92sGU6AD1(P_0, P_1, 0f, 0f, R2FrCPtkor, I0grqdnC0W, qRTrxh7NCD);
		float b = D92sGU6AD1(P_0, P_1, R2FrCPtkor, 0f, 0f, I0grqdnC0W, MRZrIGxMf5);
		return Mathf.Clamp01(Mathf.Max(a, b));
	}

	private float pHKsI29WID(int P_0, int P_1, float P_2)
	{
		if (P_2 <= 0f)
		{
			return 0f;
		}
		float num = 0f;
		float num3;
		float num4;
		float num2 = fUwsbhJUAr(P_0, P_1, R2FrCPtkor, 0f, 0f, I0grqdnC0W, wMGrGJHaEY * 0.94f, out num3, out num4);
		if (num2 > 0f && wNRrHwyKr0)
		{
			float num5 = ((APDsfVijbn(num3 + (float)((kf1sgUMLY3.GetSessionSeed() >> 4) & 0x3F), hFSrwentvq) <= hFSrwentvq * 0.56f) ? 1f : 0f);
			num = Math.Max(num, num2 * num5 * 0.84f);
		}
		return num * P_2;
	}

	private static float D92sGU6AD1(float P_0, float P_1, float P_2, float P_3, float P_4, float P_5, float P_6)
	{
		float num;
		float num2;
		return fUwsbhJUAr(P_0, P_1, P_2, P_3, P_4, P_5, P_6, out num, out num2);
	}

	private static float fUwsbhJUAr(float P_0, float P_1, float P_2, float P_3, float P_4, float P_5, float P_6, out float P_7, out float P_8)
	{
		P_7 = 0f;
		P_8 = 0f;
		if (P_6 <= 0f)
		{
			return 0f;
		}
		float num = P_4 - P_2;
		float num2 = P_5 - P_3;
		float num3 = num * num + num2 * num2;
		if (num3 <= 0.0001f)
		{
			return 0f;
		}
		P_8 = Mathf.Sqrt(num3);
		float value = ((P_0 - P_2) * num + (P_1 - P_3) * num2) / num3;
		value = Mathf.Clamp01(value);
		P_7 = P_8 * value;
		float num4 = P_2 + num * value;
		float num5 = P_3 + num2 * value;
		float num6 = Mathf.Sqrt((P_0 - num4) * (P_0 - num4) + (P_1 - num5) * (P_1 - num5));
		return 1f - Mathf.Clamp01(num6 / P_6);
	}

	private float nfNswyou22(WatermarkCoverageBitmap P_0, string P_1, float P_2, float P_3, float P_4, float P_5, float P_6, float P_7, float P_8, float P_9, int P_10, int P_11, int P_12, bool P_13)
	{
		if (!string.IsNullOrEmpty(P_1) && !(P_2 <= 0f) && !(P_3 <= 0f) && !(P_4 <= 0f) && P_0.HasCoverage() && aHcskeGmm1 > 0 && B33sXY2eTr > 0)
		{
			float num = (P_13 ? (-0.70710677f) : 0.70710677f);
			float num2 = 0.70710677f;
			float num3 = -0.70710677f;
			float num4 = num;
			float num5 = YfRrSfn08O + num3 * P_9;
			float num6 = OjArLE0wKQ + num4 * P_9;
			float num7 = (float)P_10 - num5;
			float num8 = (float)P_11 - num6;
			float num9 = num7 * num + num8 * num2;
			float num10 = num7 * num3 + num8 * num4;
			float num11 = P_3 + Math.Max(P_2 * 2f, P_6);
			if (num11 <= 0.001f)
			{
				return 0f;
			}
			float num12 = Math.Max(P_4 * 1.15f, P_5);
			int num13 = ((num12 < SB4r0Ww0BY * 0.72f) ? 1 : 0);
			int num14 = (int)Math.Round(num9 / num11, MidpointRounding.AwayFromZero);
			int num15 = ((num13 > 0) ? ((int)Math.Round(num10 / num12, MidpointRounding.AwayFromZero)) : 0);
			float num16 = 0f;
			for (int i = num15 - num13; i <= num15 + num13; i++)
			{
				float num17 = (float)i * num12;
				if (i != 0)
				{
					float num18 = (tOBsjKlLwI(kf1sgUMLY3.GetStableSeed() ^ (P_12 * 9137), i, ieHs7k5wIn, P_12) - 0.5f) * Math.Max(1f, P_2 * 0.65f);
					num17 += (float)i * P_8 * 0.14f + num18;
				}
				for (int j = num14 - 1; j <= num14 + 1; j++)
				{
					float num19 = (float)j * num11;
					if (j != 0 || i != 0)
					{
						float num20 = (tOBsjKlLwI(kf1sgUMLY3.GetSessionSeed() ^ (P_12 * 48611), j, i, P_12) - 0.5f) * Math.Max(0.75f, P_2 * (0.32f + Math.Abs(P_7) * 0.85f));
						num19 += num20 + (float)i * P_8 * 0.08f;
					}
					float num21 = num5 + num * num19 + num3 * num17;
					float num22 = num6 + num2 * num19 + num4 * num17;
					float num23 = (float)P_10 - (num21 - P_3 * 0.5f);
					float num24 = (float)P_11 - (num22 - P_4 * 0.5f);
					if (!(num23 < -1f) && !(num23 > P_3 + 1f) && !(num24 < -1f) && !(num24 > P_4 + 1f))
					{
						float num25 = P_0.SampleBilinearCoverage(num23, num24);
						if (num25 > num16)
						{
							num16 = num25;
						}
					}
				}
			}
			return num16;
		}
		return 0f;
	}

	private Color32 sDQsejI4HL(byte P_0, byte P_1, byte P_2)
	{
		if (!(CVasKOS7Yb(P_0, P_1, P_2) >= 0.58f))
		{
			return XKRrJiZyIa;
		}
		return o0nrum8ktt;
	}

	private Color32 Ee4sWYJ6rT(byte P_0, byte P_1, byte P_2)
	{
		if (!(CVasKOS7Yb(P_0, P_1, P_2) >= 0.58f))
		{
			return o0nrum8ktt;
		}
		return XKRrJiZyIa;
	}

	private static float kBvsieJXNV(byte P_0)
	{
		return (float)(int)P_0 / 127.5f - 1f;
	}

	private static float CVasKOS7Yb(byte P_0, byte P_1, byte P_2)
	{
		return ((float)(int)P_0 * 0.2126f + (float)(int)P_1 * 0.7152f + (float)(int)P_2 * 0.0722f) / 255f;
	}

	private static float tOBsjKlLwI(int P_0, int P_1, int P_2, int P_3)
	{
		int num = P_0 ^ (P_1 * 73856093) ^ (P_2 * 19349663) ^ (P_3 * 83492791);
		int num2 = (num ^ (int)((uint)num >> 16)) * -2048144789;
		int num3 = (num2 ^ (int)((uint)num2 >> 13)) * -1028477387;
		return (float)(uint)((num3 ^ (int)((uint)num3 >> 16)) & 0xFFFF) / 65535f;
	}

	private static void KmbsYFx7KL(ref byte P_0, ref byte P_1, ref byte P_2, Color32 P_3, float P_4)
	{
		P_0 = M8bstVxhSs(P_0, P_3.r, P_4);
		P_1 = M8bstVxhSs(P_1, P_3.g, P_4);
		P_2 = M8bstVxhSs(P_2, P_3.b, P_4);
	}

	private static byte M8bstVxhSs(byte P_0, byte P_1, float P_2)
	{
		if (P_2 <= 0f)
		{
			return P_0;
		}
		if (P_2 >= 1f)
		{
			return P_1;
		}
		return (byte)Math.Round((float)(int)P_0 + (float)(P_1 - P_0) * P_2, MidpointRounding.AwayFromZero);
	}

	private static byte BR5s4BO3Gp(int P_0)
	{
		return (byte)Math.Max(0, Math.Min(255, P_0));
	}

	private static float APDsfVijbn(float P_0, float P_1)
	{
		if (P_1 <= 0f)
		{
			return 0f;
		}
		float num = P_0 % P_1;
		if (!(num < 0f))
		{
			return num;
		}
		return num + P_1;
	}

	private static int htisclPqn2(int P_0, int P_1)
	{
		if (P_1 > 0)
		{
			int num = P_0 % P_1;
			if (num >= 0)
			{
				return num;
			}
			return num + P_1;
		}
		return 0;
	}
}
}
