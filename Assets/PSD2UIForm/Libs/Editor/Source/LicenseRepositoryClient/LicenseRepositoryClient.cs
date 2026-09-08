using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using PsdProtectionGuards;
using PsdLicensing;
using UnityEngine;
using UnityEngine.Networking;
using PsdProtectionRuntime;

namespace PsdLicensing
{

internal sealed class LicenseRepositoryClient
{
	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass1_0
	{
		public string DefaultBranch;

		public _003C_003Ec__DisplayClass1_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal string NormalizeUrlWithBranch(string url)
		{
			return NormalizeRepositoryBaseUrl(url, DefaultBranch);
		}
	}

	internal bool TryDownloadPayload(string[] P_0, string P_1, int P_2, out byte[] P_3, out bool P_4, out string P_5, bool P_6 = false)
	{
		P_3 = Array.Empty<byte>();
		P_4 = false;
		P_5 = string.Empty;
		string[] array = NormalizeRepositoryBaseUrls(P_0);
		if (array.Length == 0)
		{
			P_5 = "License repository base url is empty.";
			return false;
		}
		object obj;
		if (P_1 == null)
		{
			obj = null;
		}
		else
		{
			obj = P_1.TrimStart('/', '\\');
			if (obj != null)
			{
				goto IL_0055;
			}
		}
		obj = string.Empty;
		goto IL_0055;
		IL_0055:
		string text = (string)obj;
		bool flag = false;
		bool flag2 = false;
		List<string> list = new List<string>(array.Length);
		int num = 0;
		while (true)
		{
			if (num < array.Length)
			{
				if (TryDownloadBytes(CombineRepositoryPath(array[num], text), P_2, out P_3, out var flag3, out var text2, P_6))
				{
					break;
				}
				flag = flag || flag3;
				flag2 = flag2 || !flag3;
				if (!string.IsNullOrWhiteSpace(text2))
				{
					list.Add(text2);
				}
				num++;
				continue;
			}
			if (P_6 && flag)
			{
				P_4 = true;
				P_5 = string.Empty;
				return false;
			}
			P_4 = flag && !flag2;
			P_5 = ((list.Count != 0) ? LicenseDebugDiagnostics.wZ7mfWsT1S(string.Join(" | ", list.Distinct(StringComparer.Ordinal)), 512) : (P_4 ? "Not found." : string.Empty));
			return false;
		}
		P_4 = false;
		P_5 = string.Empty;
		return true;
	}

	internal static string[] NormalizeRepositoryBaseUrls(IEnumerable<string> P_0, string P_1 = "master")
	{
		_003C_003Ec__DisplayClass1_0 CS_0024_003C_003E8__locals2 = new _003C_003Ec__DisplayClass1_0();
		CS_0024_003C_003E8__locals2.DefaultBranch = P_1;
		if (P_0 == null)
		{
			return Array.Empty<string>();
		}
		return (from url in P_0
			select NormalizeRepositoryBaseUrl(url, CS_0024_003C_003E8__locals2.DefaultBranch) into url
			where !string.IsNullOrWhiteSpace(url)
			select url).Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
	}

	private static string NormalizeRepositoryBaseUrl(object P_0, object P_1)
	{
		if (TryParseRepositoryUrl(P_0, P_1, out var uri, out var text, out var text2, out var text3) && IsSupportedRepositoryHost(uri))
		{
			return uri.Scheme + "://" + uri.Authority + "/" + text + "/" + text2 + "/raw/" + text3;
		}
		return string.Empty;
	}

	private static bool TryParseRepositoryUrl(object P_0, object P_1, out Uri P_2, out string P_3, out string P_4, out string P_5)
	{
		P_2 = null;
		P_3 = string.Empty;
		P_4 = string.Empty;
		P_5 = ((!string.IsNullOrWhiteSpace((string)P_1)) ? ((string)P_1).Trim() : "master");
		if (Uri.TryCreate(((string)(P_0 ?? string.Empty)).Trim(), UriKind.Absolute, out P_2))
		{
			string[] array = (P_2.AbsolutePath ?? string.Empty).Split(new char[1] { '/' }, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length < 2)
			{
				return false;
			}
			P_3 = array[0];
			P_4 = array[1];
			if (P_4.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
			{
				P_4 = P_4.Substring(0, P_4.Length - 4);
			}
			if (array.Length >= 4 && string.Equals(array[2], "raw", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(array[3]))
			{
				P_5 = array[3].Trim();
			}
			if (!string.IsNullOrWhiteSpace(P_3) && !string.IsNullOrWhiteSpace(P_4))
			{
				return !string.IsNullOrWhiteSpace(P_5);
			}
			return false;
		}
		return false;
	}

	private static string CombineRepositoryPath(object P_0, object P_1)
	{
		string text = ((string)(P_0 ?? string.Empty)).TrimEnd(new char[2] { '/', '\\' });
		string text2 = ((string)(P_1 ?? string.Empty)).TrimStart(new char[2] { '/', '\\' }).Replace('\\', '/');
		if (!string.IsNullOrWhiteSpace(text2))
		{
			return text + "/" + text2;
		}
		return text;
	}

	private static bool IsSupportedRepositoryHost(object P_0)
	{
		object obj;
		if (P_0 == null)
		{
			obj = null;
		}
		else
		{
			obj = ((Uri)P_0).Host;
			if (obj != null)
			{
				goto IL_0015;
			}
		}
		obj = string.Empty;
		goto IL_0015;
		IL_0015:
		return !string.Equals(((string)obj).Trim().ToLowerInvariant(), "gitcode.com", StringComparison.OrdinalIgnoreCase);
	}

	private static bool TryDownloadBytes(object P_0, int P_1, out byte[] P_2, out bool P_3, out string P_4, bool P_5)
	{
		P_2 = Array.Empty<byte>();
		P_3 = false;
		P_4 = string.Empty;
		try
		{
			using UnityWebRequest unityWebRequest = UnityWebRequest.Get((string)P_0);
			unityWebRequest.timeout = Mathf.Clamp(P_1, 3, 60);
			unityWebRequest.SetRequestHeader("Accept", "application/octet-stream");
			unityWebRequest.SetRequestHeader("User-Agent", "eFunStudio.Psd2UGUI.License/1.0");
			UnityWebRequestAsyncOperation unityWebRequestAsyncOperation = unityWebRequest.SendWebRequest();
			while (!unityWebRequestAsyncOperation.isDone)
			{
				Thread.Sleep(10);
			}
			long responseCode = unityWebRequest.responseCode;
			if (responseCode == 404L)
			{
				P_3 = true;
				return false;
			}
			if (unityWebRequest.result != UnityWebRequest.Result.Success)
			{
				P_4 = (string.IsNullOrWhiteSpace(unityWebRequest.error) ? $"HTTP {((responseCode <= 0L) ? (-1L) : responseCode)}" : unityWebRequest.error);
				return false;
			}
			P_2 = ((unityWebRequest.downloadHandler == null) ? Array.Empty<byte>() : (unityWebRequest.downloadHandler.data ?? Array.Empty<byte>()));
			return P_2.Length != 0;
		}
		catch (Exception ex)
		{
			P_4 = ex.Message;
			return false;
		}
	}

	public LicenseRepositoryClient()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}
