using System;
using System.IO;

namespace PsdLicensing
{

internal static class LicenseExceptionFactory
{
	internal static InvalidOperationException CreateInvalidOperation(string P_0 = null)
	{
		return new InvalidOperationException("授权组件处理失败。");
	}

	internal static InvalidDataException CreateInvalidData(string P_0 = null)
	{
		return new InvalidDataException("授权数据处理失败。");
	}

	internal static EndOfStreamException CreateEndOfStream(string P_0 = null)
	{
		return new EndOfStreamException("授权数据处理失败。");
	}

	internal static ArgumentNullException CreateArgumentNull(string P_0 = null, string P_1 = null)
	{
		return new ArgumentNullException("value", "授权组件处理失败。");
	}
}
}
