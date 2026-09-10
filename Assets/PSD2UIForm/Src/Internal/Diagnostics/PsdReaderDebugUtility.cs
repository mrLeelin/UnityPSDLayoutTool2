using System.Diagnostics;

namespace PsdReaderDebugUtilityNamespace
{
    internal static class PsdReaderDebugUtility
    {
        [Conditional("EFUN_DEBUG")]
        internal static void WriteDebugMessage(object message, object context)
        {
        }

        [Conditional("EFUN_DEBUG")]
        internal static void WriteDebugWarning(object message, object context)
        {
        }

        [Conditional("EFUN_DEBUG")]
        internal static void WriteDebugError(object message, object context)
        {
        }

        [Conditional("EFUN_DEBUG")]
        internal static void WriteDebugContext(object context, object message, object details)
        {
        }

        internal static string FormatDebugValue(object value)
        {
            return string.Empty;
        }

        internal static string FormatDebugType(object value)
        {
            return string.Empty;
        }

        internal static string FormatDebugMember(object value)
        {
            return string.Empty;
        }

        internal static string TruncateDebugText(object value, int value2 = 160)
        {
            return string.Empty;
        }
    }
}
