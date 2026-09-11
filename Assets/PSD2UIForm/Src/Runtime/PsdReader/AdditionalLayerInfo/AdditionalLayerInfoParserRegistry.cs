using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;
using UnknownAdditionalLayerInfoSectionNamespace;
using AdditionalLayerInfoSectionNamespace;

namespace AdditionalLayerInfoParserRegistryNamespace
{
    internal sealed class AdditionalLayerInfoParserRegistry
    {
        private static readonly Dictionary<string, Type> _parserTypesById;

        internal static AdditionalLayerInfoParserRegistry s_ObfuscationSentinel;

        static AdditionalLayerInfoParserRegistry()
        {
            IEnumerable<Type> enumerable = from item in typeof(AdditionalLayerInfoSection).Assembly.GetTypes()
                where typeof(AdditionalLayerInfoSection).IsAssignableFrom(item) && (item.Attributes & TypeAttributes.Abstract) != TypeAttributes.Abstract
                select item;
            _parserTypesById = new Dictionary<string, Type>(enumerable.Count());
            foreach (Type item in enumerable)
            {
                object[] customAttributes = item.GetCustomAttributes(typeof(AdditionalLayerInfoParserAttribute), inherit: true);
                if (customAttributes.Length != 0)
                {
                    AdditionalLayerInfoParserAttribute value = customAttributes.First() as AdditionalLayerInfoParserAttribute;
                    _parserTypesById.Add(value.ID, item);
                }
            }
        }

        public static AdditionalLayerInfoSection CreateParser(object text, object additionalLayerInfoSection, long value)
        {
            Type objectType = typeof(UnknownAdditionalLayerInfoSection);
            if (_parserTypesById.ContainsKey((string)text))
            {
                objectType = _parserTypesById[(string)text];
            }
            return TypeDescriptor.CreateInstance(null, objectType, new Type[2]
            {
                typeof(PsdBinaryReader),
                typeof(long)
            }, new object[2] { additionalLayerInfoSection, value }) as AdditionalLayerInfoSection;
        }

        public static string GetParserDisplayName(Type type)
        {
            return (type.GetCustomAttributes(typeof(AdditionalLayerInfoParserAttribute), inherit: true).First() as AdditionalLayerInfoParserAttribute).DisplayName;
        }

        public static string GetParserDisplayNameById(object name)
        {
            if (!_parserTypesById.ContainsKey((string)name))
            {
                return (string)name;
            }
            return GetParserDisplayName(_parserTypesById[(string)name]);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AdditionalLayerInfoParserRegistry GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
