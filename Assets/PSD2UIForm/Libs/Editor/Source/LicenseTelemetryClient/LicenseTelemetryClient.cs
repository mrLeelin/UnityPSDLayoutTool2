using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using PsdLicensing;
using PsdProtectionGuards;
using UnityEngine;
using cn.efunstudio.psdreader;
using PsdProtectionRuntime;

namespace PsdLicensing
{

internal sealed class LicenseTelemetryClient
{
	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass2_0
	{
		public string EventName;

		public Uri RequestUri;

		public string RequestUrl;

		public int TimeoutSeconds;

		public _003C_003Ec__DisplayClass2_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal void SendQueuedTrackingRequest(Task _)
		{
			SendTrackingRequest(RequestUri, RequestUrl, TimeoutSeconds, EventName);
		}
	}

	private static readonly object _queueLock;

	private static Task _queuedTask;

	internal void QueueTrackingEvent(PsdReaderLicenseClientConfigDocument P_0, string P_1, string P_2)
	{
		_003C_003Ec__DisplayClass2_0 CS_0024_003C_003E8__locals11 = new _003C_003Ec__DisplayClass2_0();
		CS_0024_003C_003E8__locals11.EventName = P_2;
		string text = NormalizeEventCategory(P_1);
		string text2 = LicenseCryptography.NormalizeDeviceIdentifier(SystemInfo.deviceUniqueIdentifier);
		string text3 = LicenseCryptography.HashDeviceIdentifier(text2);
		if (text3.Length > 16)
		{
			text3 = text3.Substring(0, 16);
		}
		if (P_0 == null || string.IsNullOrWhiteSpace(P_0.MatomoUrl) || string.IsNullOrWhiteSpace(P_0.MatomoSiteId) || string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(text2) || string.IsNullOrWhiteSpace(CS_0024_003C_003E8__locals11.EventName))
		{
			return;
		}
		try
		{
			if (!TryBuildMatomoRequestUri(P_0, text, text2, CS_0024_003C_003E8__locals11.EventName, text3, out CS_0024_003C_003E8__locals11.RequestUri))
			{
				return;
			}
			CS_0024_003C_003E8__locals11.RequestUrl = CS_0024_003C_003E8__locals11.RequestUri.AbsoluteUri;
			CS_0024_003C_003E8__locals11.TimeoutSeconds = Mathf.Clamp(P_0.RequestTimeoutSeconds, 3, 60);
			lock (_queueLock)
			{
				_queuedTask = _queuedTask.ContinueWith(delegate
				{
					SendTrackingRequest(CS_0024_003C_003E8__locals11.RequestUri, CS_0024_003C_003E8__locals11.RequestUrl, CS_0024_003C_003E8__locals11.TimeoutSeconds, CS_0024_003C_003E8__locals11.EventName);
				}, TaskScheduler.Default);
			}
		}
		catch (Exception)
		{
		}
	}

	private static string NormalizeEventCategory(object P_0)
	{
		if (string.IsNullOrWhiteSpace((string)P_0))
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder(((string)P_0).Length);
		for (int i = 0; i < ((string)P_0).Length; i++)
		{
			char c = char.ToLowerInvariant(((string)P_0)[i]);
			if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.')
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString().Trim('.', '_', '-');
	}

	private static bool TryBuildMatomoRequestUri(object P_0, object P_1, object P_2, object P_3, object P_4, out Uri P_5)
	{
		P_5 = null;
		object obj;
		if (P_0 == null)
		{
			obj = null;
		}
		else
		{
			obj = ((PsdReaderLicenseClientConfigDocument)P_0).MatomoUrl;
			if (obj != null)
			{
				goto IL_0019;
			}
		}
		obj = string.Empty;
		goto IL_0019;
		IL_0019:
		string text = ((string)obj).Trim();
		if (!string.IsNullOrWhiteSpace(text))
		{
			if (!Uri.TryCreate(text, UriKind.Absolute, out var result))
			{
				if (text.IndexOf("://", StringComparison.Ordinal) < 0)
				{
					Uri.TryCreate("https://" + text, UriKind.Absolute, out result);
				}
				if (result == null)
				{
					return false;
				}
			}
			string text2 = (result.AbsolutePath ?? string.Empty).Trim();
			if (!string.IsNullOrWhiteSpace(text2) && !(text2 == "/"))
			{
				text2 = text2.TrimEnd('/');
				if (!text2.EndsWith(".php", StringComparison.OrdinalIgnoreCase))
				{
					text2 += "/matomo.php";
				}
			}
			else
			{
				text2 = "/matomo.php";
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("idsite=").Append(Uri.EscapeDataString(((PsdReaderLicenseClientConfigDocument)P_0).MatomoSiteId));
			stringBuilder.Append("&rec=1");
			stringBuilder.Append("&apiv=1");
			stringBuilder.Append("&rand=").Append(DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
			stringBuilder.Append("&uid=").Append(Uri.EscapeDataString((string)P_1));
			if (!string.IsNullOrWhiteSpace((string)P_4))
			{
				stringBuilder.Append("&_id=").Append(Uri.EscapeDataString((string)P_4));
			}
			stringBuilder.Append("&e_c=").Append(Uri.EscapeDataString((string)P_1));
			stringBuilder.Append("&e_a=").Append(Uri.EscapeDataString((string)P_2));
			stringBuilder.Append("&e_n=").Append(Uri.EscapeDataString((string)P_3));
			UriBuilder uriBuilder = new UriBuilder(result)
			{
				Path = text2,
				Query = stringBuilder.ToString()
			};
			P_5 = uriBuilder.Uri;
			return P_5 != null;
		}
		return false;
	}

	private static void SendTrackingRequest(object P_0, object P_1, int P_2, object P_3)
	{
		try
		{
			try
			{
				ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
			}
			catch
			{
			}
			using HttpClient httpClient = new HttpClient(new HttpClientHandler
			{
				AutomaticDecompression = (DecompressionMethods.Deflate | DecompressionMethods.GZip)
			})
			{
				Timeout = TimeSpan.FromSeconds(P_2)
			};
			using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, (Uri)P_0);
			httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", "eFunStudio.Psd2UGUI.Telemetry/1.0");
			using HttpResponseMessage httpResponseMessage = httpClient.SendAsync(httpRequestMessage).GetAwaiter().GetResult();
			httpResponseMessage.EnsureSuccessStatusCode();
		}
		catch (Exception)
		{
		}
	}

	public LicenseTelemetryClient()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}

	static LicenseTelemetryClient()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_queueLock = new object();
		_queuedTask = Task.CompletedTask;
	}
}
}
