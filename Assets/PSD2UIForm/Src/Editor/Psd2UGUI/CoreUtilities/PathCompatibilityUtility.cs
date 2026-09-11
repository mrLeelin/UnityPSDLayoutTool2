using System.IO;

namespace PathCompatibilityUtilityNamespace
{
    internal sealed class PathCompatibilityUtility
    {
        internal static PathCompatibilityUtility s_ObfuscationSentinel;

        internal static string GetRelativePath(object path, object path2)
        {
            return Path.GetRelativePath((string)path, (string)path2);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PathCompatibilityUtility GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
