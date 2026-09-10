using System;

namespace AdditionalLayerInfoParserAttributeNamespace
{
    [AttributeUsage(AttributeTargets.Class)]
    internal class AdditionalLayerInfoParserAttribute : Attribute
    {
        private readonly string _id;

        private string _displayName;

        internal static AdditionalLayerInfoParserAttribute s_ObfuscationSentinel;

        public string ID => _id;

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(_displayName))
                {
                    return _displayName;
                }
                return _id;
            }
            set
            {
                _displayName = value;
            }
        }

        public AdditionalLayerInfoParserAttribute(string text)
        {
            _id = text;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AdditionalLayerInfoParserAttribute GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
