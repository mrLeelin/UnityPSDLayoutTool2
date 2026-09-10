using System;
using System.Collections.Generic;

namespace LicenseFeatureCatalogNamespace
{
    internal sealed class LicenseFeatureCatalog
    {
        private static readonly string[] _featureNames = new string[1] { "Main" };

        internal static LicenseFeatureCatalog s_ObfuscationSentinel;

        internal static uint EncodeFeatureMask(object value)
        {
            if (value != null && ((Array)value).Length != 0)
            {
                uint num = 0u;
                for (int i = 0; i < ((Array)value).Length; i++)
                {
                    int num2 = Array.IndexOf(_featureNames, (string)((object[])value)[i]);
                    if (num2 >= 0)
                    {
                        num |= (uint)(1 << num2);
                    }
                }
                return num;
            }
            return 0u;
        }

        internal static string[] DecodeFeatureMask(uint featureMask)
        {
            if (featureMask == 0)
            {
                return Array.Empty<string>();
            }
            List<string> enabledFeatures = new List<string>(_featureNames.Length);
            for (int featureIndex = 0; featureIndex < _featureNames.Length; featureIndex++)
            {
                if ((featureMask & (uint)(1 << featureIndex)) != 0)
                {
                    enabledFeatures.Add(_featureNames[featureIndex]);
                }
            }
            return enabledFeatures.ToArray();
        }

        internal static bool ContainsFeature(uint value, object text)
        {
            int num = Array.IndexOf(_featureNames, (string)(text ?? string.Empty));
            if (num < 0)
            {
                return false;
            }
            return (value & (uint)(1 << num)) != 0;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LicenseFeatureCatalog GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
