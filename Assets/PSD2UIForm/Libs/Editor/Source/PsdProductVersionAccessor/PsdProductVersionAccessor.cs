using System;
using System.Runtime.CompilerServices;
using cn.efunstudio.psdreader;

namespace PsdLicensing
{

internal static class PsdProductVersionAccessor
{
	[SpecialName]
	internal static Version GetCurrentVersion()
	{
		return PsdReaderVersion.CurrentValue;
	}

	[SpecialName]
	internal static int GetCurrentMajorVersion()
	{
		return PsdReaderVersion.CurrentMajor;
	}
}
}
