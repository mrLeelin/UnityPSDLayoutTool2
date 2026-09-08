using System;
using PsdLicensing;

namespace PsdLicensing
{

internal static class LicenseStatusPresentation
{
	internal static LicenseAvailability GetAvailability(object P_0)
	{
		if (P_0 != null && string.Equals(((LicenseStatusSnapshot)P_0).GetStatusText(), "Private", StringComparison.Ordinal))
		{
			return (LicenseAvailability)0;
		}
		if (P_0 == null)
		{
			return (LicenseAvailability)2;
		}
		return ((LicenseStatusSnapshot)P_0).GetValidationStatus() switch
		{
			(LicenseValidationStatus)1 => (LicenseAvailability)0, 
			(LicenseValidationStatus)3 => (LicenseAvailability)1, 
			(LicenseValidationStatus)4 => (LicenseAvailability)1, 
			(LicenseValidationStatus)7 => (LicenseAvailability)1, 
			_ => (LicenseAvailability)2, 
		};
	}

	internal static string GetActivationHint(object P_0)
	{
		if (P_0 != null && string.Equals(((LicenseStatusSnapshot)P_0).GetStatusText(), "Private", StringComparison.Ordinal))
		{
			return "当前为 EFUN_PRIVATE 源码自用模式。不需要授权，也不会发送统计事件。";
		}
		return GetAvailability(P_0) switch
		{
			(LicenseAvailability)1 => "可输入购买订单号激活；如果插件目录里已有授权文件，也可以直接用授权文件激活。", 
			(LicenseAvailability)0 => "当前授权已生效，可以正常使用 Psd2UGUI。", 
			_ => "当前授权暂不可用。你仍可尝试重新验证，若问题持续请联系支持。", 
		};
	}

	internal static string GetStatusLabel(object P_0)
	{
		if (P_0 != null && string.Equals(((LicenseStatusSnapshot)P_0).GetStatusText(), "Private", StringComparison.Ordinal))
		{
			return "源码自用版";
		}
		return GetAvailability(P_0) switch
		{
			(LicenseAvailability)1 => "需要授权", 
			(LicenseAvailability)0 => "可用", 
			_ => "暂不可用", 
		};
	}

	internal static string GetDetailedStatusMessage(object P_0)
	{
		if (P_0 != null)
		{
			if (!string.Equals(((LicenseStatusSnapshot)P_0).GetStatusText(), "Private", StringComparison.Ordinal))
			{
				return GetAvailability(P_0) switch
				{
					(LicenseAvailability)0 => "授权已生效，可以开始使用 Psd2UGUI。", 
					(LicenseAvailability)1 => "当前尚未完成插件授权。请在授权窗口输入购买订单号，或直接使用插件目录中的授权文件激活。", 
					_ => "当前授权暂不可用，请稍后重试或联系支持。", 
				};
			}
			return "当前为 EFUN_PRIVATE 源码自用模式。不需要授权，也不会发送统计事件。";
		}
		return "当前无法完成授权验证，请稍后重试。";
	}
}
}
