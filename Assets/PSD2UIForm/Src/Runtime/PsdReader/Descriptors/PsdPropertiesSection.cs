using System;
using System.Collections;
using System.Collections.Generic;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using PropertyCollectionNamespace;
using LengthPrefixedPsdSectionNamespace;

namespace PsdPropertiesSectionNamespace
{
    internal abstract class PsdPropertiesSection : LengthPrefixedPsdSection<IProperties>, IEnumerable<KeyValuePair<string, object>>, IEnumerable, IProperties
    {
        private static PsdPropertiesSection s_ObfuscationSentinel;

        public object this[string property] => GetPropertiesOrNull()?[property];

        public int Count => GetPropertiesOrNull()?.Count ?? 0;

        protected PsdPropertiesSection(PsdBinaryReader psdBinaryReader, object value)
            : base(psdBinaryReader, value)
        {
        }

        protected PsdPropertiesSection(PsdBinaryReader psdBinaryReader, long value, object value2)
            : base(psdBinaryReader, value, value2)
        {
        }

        public bool Contains(string text)
        {
            return GetPropertiesOrNull()?.Contains(text) ?? false;
        }

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
        {
            IProperties properties = GetPropertiesOrNull();
            return (properties ?? new PropertyCollection()).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            IProperties properties = GetPropertiesOrNull();
            return (properties ?? new PropertyCollection()).GetEnumerator();
        }

        private IProperties GetPropertiesOrNull()
        {
            try
            {
                return base.Value;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdPropertiesSection GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
