using System;
using System.IO;

namespace LicenseExceptionFactoryNamespace
{
    internal sealed class LicenseExceptionFactory
    {
        private static LicenseExceptionFactory s_ObfuscationSentinel;

        internal static InvalidOperationException CreateInvalidOperationException(string id = null)
        {
            return new InvalidOperationException("授权组件处理失败。");
        }

        internal static InvalidDataException CreateInvalidDataException(string id = null)
        {
            return new InvalidDataException("授权数据处理失败。");
        }

        internal static EndOfStreamException CreateEndOfStreamException(string text = null)
        {
            return new EndOfStreamException("授权数据处理失败。");
        }

        internal static ArgumentNullException CreateArgumentNullException(string text = null, string text2 = null)
        {
            return new ArgumentNullException("value", "授权组件处理失败。");
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LicenseExceptionFactory GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
