using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;
using cn.efunstudio.psdreader;
using LicenseCryptoUtilityNamespace;

namespace LicenseTelemetryClientNamespace
{
    internal sealed class LicenseTelemetryClient
    {
        private static readonly object _dispatchLock = new object();

        private static Task _pendingDispatch = Task.CompletedTask;

        internal static LicenseTelemetryClient s_ObfuscationSentinel;

        internal void TrackEvent(PsdReaderLicenseClientConfigDocument psdReaderLicenseClientConfigDocument, string text5, string text6)
        {
            string text = NormalizeTelemetryUserId(text5);
            string text2 = LicenseCryptoUtility.TrimOrEmpty(SystemInfo.deviceUniqueIdentifier);
            string text3 = LicenseCryptoUtility.ComputeDeviceIdHash(text2);
            if (text3.Length > 16)
            {
                text3 = text3.Substring(0, 16);
            }
            if (psdReaderLicenseClientConfigDocument == null || string.IsNullOrWhiteSpace(psdReaderLicenseClientConfigDocument.MatomoUrl) || string.IsNullOrWhiteSpace(psdReaderLicenseClientConfigDocument.MatomoSiteId) || string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(text2) || string.IsNullOrWhiteSpace(text6))
            {
                return;
            }
            try
            {
                if (!TryBuildMatomoTrackingUri(psdReaderLicenseClientConfigDocument, text, text2, text6, text3, out var uri))
                {
                    return;
                }
                string text4 = uri.AbsoluteUri;
                int value = Mathf.Clamp(psdReaderLicenseClientConfigDocument.RequestTimeoutSeconds, 3, 60);
                lock (_dispatchLock)
                {
                    _pendingDispatch = _pendingDispatch.ContinueWith(delegate
                    {
                        SendTrackingRequest(uri, text4, value, text6);
                    }, TaskScheduler.Default);
                }
            }
            catch (Exception)
            {
            }
        }

        private static string NormalizeTelemetryUserId(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                StringBuilder stringBuilder = new StringBuilder(((string)value).Length);
                for (int i = 0; i < ((string)value).Length; i++)
                {
                    char c = char.ToLowerInvariant(((string)value)[i]);
                    if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.')
                    {
                        stringBuilder.Append(c);
                    }
                }
                return stringBuilder.ToString().Trim('.', '_', '-');
            }
            return string.Empty;
        }

        private static bool TryBuildMatomoTrackingUri(object value, object value2, object value3, object value4, object value5, out Uri result2)
        {
            result2 = null;
            object obj;
            if (value != null)
            {
                obj = ((PsdReaderLicenseClientConfigDocument)value).MatomoUrl;
                if (obj != null)
                {
                    goto IL_0019;
                }
            }
            else
            {
                obj = null;
            }
            obj = string.Empty;
            goto IL_0019;
            IL_0019:
            string text = ((string)obj).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
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
            stringBuilder.Append("idsite=").Append(Uri.EscapeDataString(((PsdReaderLicenseClientConfigDocument)value).MatomoSiteId));
            stringBuilder.Append("&rec=1");
            stringBuilder.Append("&apiv=1");
            stringBuilder.Append("&rand=").Append(DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            stringBuilder.Append("&uid=").Append(Uri.EscapeDataString((string)value2));
            if (!string.IsNullOrWhiteSpace((string)value5))
            {
                stringBuilder.Append("&_id=").Append(Uri.EscapeDataString((string)value5));
            }
            stringBuilder.Append("&e_c=").Append(Uri.EscapeDataString((string)value2));
            stringBuilder.Append("&e_a=").Append(Uri.EscapeDataString((string)value3));
            stringBuilder.Append("&e_n=").Append(Uri.EscapeDataString((string)value4));
            UriBuilder uriBuilder = new UriBuilder(result)
            {
                Path = text2,
                Query = stringBuilder.ToString()
            };
            result2 = uriBuilder.Uri;
            return result2 != null;
        }

        private static void SendTrackingRequest(object value, object value2, int value3, object value4)
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
                HttpClient val = new HttpClient((HttpMessageHandler)new HttpClientHandler
                {
                    AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate)
                })
                {
                    Timeout = TimeSpan.FromSeconds(value3)
                };
                try
                {
                    HttpRequestMessage val2 = new HttpRequestMessage(HttpMethod.Get, (Uri)value);
                    try
                    {
                        ((HttpHeaders)val2.Headers).TryAddWithoutValidation("User-Agent", "eFunStudio.Psd2UGUI.Telemetry/1.0");
                        HttpResponseMessage result = val.SendAsync(val2).GetAwaiter().GetResult();
                        try
                        {
                            result.EnsureSuccessStatusCode();
                        }
                        finally
                        {
                            ((IDisposable)result)?.Dispose();
                        }
                    }
                    finally
                    {
                        ((IDisposable)val2)?.Dispose();
                    }
                }
                finally
                {
                    ((IDisposable)val)?.Dispose();
                }
            }
            catch (Exception)
            {
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LicenseTelemetryClient GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
