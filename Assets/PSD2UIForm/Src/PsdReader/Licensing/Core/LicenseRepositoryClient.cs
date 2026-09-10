using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Networking;
using PsdReaderDebugUtilityNamespace;

namespace LicenseRepositoryClientNamespace
{
    internal sealed class LicenseRepositoryClient
    {
        internal static LicenseRepositoryClient s_ObfuscationSentinel;

        internal bool TryDownloadWithFailover(string[] texts, string text3, int value, out byte[] result, out bool result2, out string result3, bool enabled = false)
        {
            result = Array.Empty<byte>();
            result2 = false;
            result3 = string.Empty;
            string[] array = NormalizeRepositoryBaseUrls(texts);
            object obj;
            if (array.Length != 0)
            {
                if (text3 == null)
                {
                    obj = null;
                }
                else
                {
                    obj = text3.TrimStart('/', '\\');
                    if (obj != null)
                    {
                        goto IL_004e;
                    }
                }
                obj = string.Empty;
                goto IL_004e;
            }
            result3 = "License repository base url is empty.";
            return false;
            IL_004e:
            string text = (string)obj;
            bool flag = false;
            bool flag2 = false;
            List<string> list = new List<string>(array.Length);
            int num = 0;
            while (true)
            {
                if (num < array.Length)
                {
                    if (TryDownloadUrl(CombineUrl(array[num], text), value, out result, out var flag3, out var text2, enabled))
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
                if (enabled && flag)
                {
                    result2 = true;
                    result3 = string.Empty;
                    return false;
                }
                result2 = flag && !flag2;
                result3 = ((list.Count != 0) ? PsdReaderDebugUtility.TruncateDebugText(string.Join(" | ", list.Distinct(StringComparer.Ordinal)), 512) : (result2 ? "Not found." : string.Empty));
                return false;
            }
            result2 = false;
            result3 = string.Empty;
            return true;
        }

        internal static string[] NormalizeRepositoryBaseUrls(IEnumerable<string> urls, string url2 = "master")
        {
            if (urls == null)
            {
                return Array.Empty<string>();
            }
            return (from url in urls
                select NormalizeRawRepositoryUrl(url, url2) into url
                where !string.IsNullOrWhiteSpace(url)
                select url).Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
        }

        private static string NormalizeRawRepositoryUrl(object value, object value2)
        {
            if (TryParseRepositoryUrl(value, value2, out var uri, out var text, out var text2, out var text3) && IsSupportedHost(uri))
            {
                return uri.Scheme + "://" + uri.Authority + "/" + text + "/" + text2 + "/raw/" + text3;
            }
            return string.Empty;
        }

        private static bool TryParseRepositoryUrl(object value, object value2, out Uri result, out string result2, out string result3, out string result4)
        {
            result = null;
            result2 = string.Empty;
            result3 = string.Empty;
            result4 = ((!string.IsNullOrWhiteSpace((string)value2)) ? ((string)value2).Trim() : "master");
            if (Uri.TryCreate(((string)(value ?? string.Empty)).Trim(), UriKind.Absolute, out result))
            {
                string[] array = (result.AbsolutePath ?? string.Empty).Split(new char[1] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (array.Length < 2)
                {
                    return false;
                }
                result2 = array[0];
                result3 = array[1];
                if (result3.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                {
                    result3 = result3.Substring(0, result3.Length - 4);
                }
                if (array.Length >= 4 && string.Equals(array[2], "raw", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(array[3]))
                {
                    result4 = array[3].Trim();
                }
                if (string.IsNullOrWhiteSpace(result2) || string.IsNullOrWhiteSpace(result3))
                {
                    return false;
                }
                return !string.IsNullOrWhiteSpace(result4);
            }
            return false;
        }

        private static string CombineUrl(object value, object value2)
        {
            string text = ((string)(value ?? string.Empty)).TrimEnd(new char[2] { '/', '\\' });
            string text2 = ((string)(value2 ?? string.Empty)).TrimStart(new char[2] { '/', '\\' }).Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(text2))
            {
                return text;
            }
            return text + "/" + text2;
        }

        private static bool IsSupportedHost(object value)
        {
            object obj;
            if (value != null)
            {
                obj = ((Uri)value).Host;
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
            return !string.Equals(((string)obj).Trim().ToLowerInvariant(), "gitcode.com", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryDownloadUrl(object value, int value2, out byte[] result, out bool result2, out string result3, bool enabled)
        {
            result = Array.Empty<byte>();
            result2 = false;
            result3 = string.Empty;
            try
            {
                UnityWebRequest val = UnityWebRequest.Get((string)value);
                try
                {
                    val.timeout = Mathf.Clamp(value2, 3, 60);
                    val.SetRequestHeader("Accept", "application/octet-stream");
                    val.SetRequestHeader("User-Agent", "eFunStudio.Psd2UGUI.License/1.0");
                    UnityWebRequestAsyncOperation val2 = val.SendWebRequest();
                    while (!((AsyncOperation)val2).isDone)
                    {
                        Thread.Sleep(10);
                    }
                    long responseCode = val.responseCode;
                    if (responseCode == 404L)
                    {
                        result2 = true;
                        return false;
                    }
                    if ((int)val.result != 1)
                    {
                        result3 = ((!string.IsNullOrWhiteSpace(val.error)) ? val.error : $"HTTP {((responseCode <= 0L) ? (-1L) : responseCode)}");
                        return false;
                    }
                    result = ((val.downloadHandler == null) ? Array.Empty<byte>() : (val.downloadHandler.data ?? Array.Empty<byte>()));
                    return result.Length != 0;
                }
                finally
                {
                    ((IDisposable)val)?.Dispose();
                }
            }
            catch (Exception ex)
            {
                result3 = ex.Message;
                return false;
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LicenseRepositoryClient GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
