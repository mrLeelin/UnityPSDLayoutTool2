using System;
using System.Runtime.CompilerServices;
using cn.efunstudio.psdreader;

namespace PsdReaderCurrentVersionInfoNamespace
{
    internal static class PsdReaderCurrentVersionInfo
    {
        [SpecialName]
        internal static Version GetCurrentVersion()
        {
            return PsdReaderVersion.CurrentValue;
        }

        [SpecialName]
        internal static int GetCurrentBuildNumber()
        {
            return 0;
        }
    }
}
