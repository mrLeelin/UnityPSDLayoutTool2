using System;

namespace ScriptableSingletonPathAttributeNamespace
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class ScriptableSingletonPathAttribute : Attribute
    {
        internal string RelativePath;

        internal static ScriptableSingletonPathAttribute s_ObfuscationSentinel;

        internal ScriptableSingletonPathAttribute(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("Invalid relative path (it is empty)");
            }
            if (text[0] == '/')
            {
                text = text.Substring(1);
            }
            RelativePath = text;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static ScriptableSingletonPathAttribute GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
