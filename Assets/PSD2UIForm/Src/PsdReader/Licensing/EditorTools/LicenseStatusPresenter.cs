using System;
using LicenseResultCodeNamespace;
using LicenseAvailabilityStateNamespace;
using PsdReaderLicenseStatusNamespace;

namespace LicenseStatusPresenterNamespace
{
    internal sealed class LicenseStatusPresenter
    {
        internal static LicenseStatusPresenter s_ObfuscationSentinel;

        internal static LicenseAvailabilityState GetAvailabilityState(object licenseStatusObject)
        {
            if (licenseStatusObject != null && string.Equals(((PsdReaderLicenseStatus)licenseStatusObject).GetStatusText(), "Private", StringComparison.Ordinal))
            {
                return (LicenseAvailabilityState)0;
            }
            if (licenseStatusObject != null)
            {
                return ((PsdReaderLicenseStatus)licenseStatusObject).GetResultCode() switch
                {
                    (LicenseResultCode)1 => (LicenseAvailabilityState)0, // 验证成功：可用
                    (LicenseResultCode)3 => (LicenseAvailabilityState)1, // 订单号无效：需要重新激活
                    (LicenseResultCode)4 => (LicenseAvailabilityState)1, // 本地缓存不可用：需要激活
                    (LicenseResultCode)7 => (LicenseAvailabilityState)1, // 远程授权不存在：需要激活
                    _ => (LicenseAvailabilityState)2,                    // 其他校验错误：暂不可用
                };
            }
            return (LicenseAvailabilityState)2;
        }

        internal static string GetActivationHelpText(object licenseStatusObject)
        {
            if (licenseStatusObject == null || !string.Equals(((PsdReaderLicenseStatus)licenseStatusObject).GetStatusText(), "Private", StringComparison.Ordinal))
            {
                return GetAvailabilityState(licenseStatusObject) switch
                {
                    (LicenseAvailabilityState)0 => "当前授权已生效，可以正常使用 Psd2UGUI。", 
                    (LicenseAvailabilityState)1 => "可输入购买订单号激活；如果插件目录里已有授权文件，也可以直接用授权文件激活。", 
                    _ => "当前授权暂不可用。你仍可尝试重新验证，若问题持续请联系支持。", 
                };
            }
            return "当前为 EFUN_PRIVATE 源码自用模式。不需要授权，也不会发送统计事件。";
        }

        internal static string GetStatusLabel(object licenseStatusObject)
        {
            if (licenseStatusObject != null && string.Equals(((PsdReaderLicenseStatus)licenseStatusObject).GetStatusText(), "Private", StringComparison.Ordinal))
            {
                return "源码自用版";
            }
            return GetAvailabilityState(licenseStatusObject) switch
            {
                (LicenseAvailabilityState)1 => "需要授权", 
                (LicenseAvailabilityState)0 => "可用", 
                _ => "暂不可用", 
            };
        }

        internal static string GetStatusMessage(object licenseStatusObject)
        {
            if (licenseStatusObject != null)
            {
                if (!string.Equals(((PsdReaderLicenseStatus)licenseStatusObject).GetStatusText(), "Private", StringComparison.Ordinal))
                {
                    return GetAvailabilityState(licenseStatusObject) switch
                    {
                        (LicenseAvailabilityState)0 => "授权已生效，可以开始使用 Psd2UGUI。", 
                        (LicenseAvailabilityState)1 => "当前尚未完成插件授权。请在授权窗口输入购买订单号，或直接使用插件目录中的授权文件激活。", 
                        _ => "当前授权暂不可用，请稍后重试或联系支持。", 
                    };
                }
                return "当前为 EFUN_PRIVATE 源码自用模式。不需要授权，也不会发送统计事件。";
            }
            return "当前无法完成授权验证，请稍后重试。";
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LicenseStatusPresenter GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
