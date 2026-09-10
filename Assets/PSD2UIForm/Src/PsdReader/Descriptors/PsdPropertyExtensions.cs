using System;
using cn.efunstudio.psdreader.PsdParser;

namespace PsdPropertyExtensionsNamespace
{
    internal static class PsdPropertyExtensions
    {
internal static bool ContainsProperty(this object properties2, object value, params string[] properties)
        {
            return ((IProperties)properties2).Contains(BuildPropertyPath(value, properties));
        }

        internal static TValue GetProperty<TValue>(this object value, object tValue, params string[] properties)
        {
            return (TValue)((IProperties)value)[BuildPropertyPath(tValue, properties)];
        }

        internal static Guid GetGuid(this object value, object value2, params string[] properties)
        {
            return new Guid(value.GetString(value2, properties));
        }

        internal static string GetString(this object value, object value2, params string[] properties)
        {
            return value.GetProperty<string>(value2, properties);
        }

        internal static byte GetByte(this object value, object value2, params string[] properties)
        {
            return value.GetProperty<byte>(value2, properties);
        }

        internal static int GetInt32(this object value, object value2, params string[] properties)
        {
            return value.GetProperty<int>(value2, properties);
        }

        internal static float GetSingle(this object value, object value2, params string[] properties)
        {
            return value.GetProperty<float>(value2, properties);
        }

        internal static double GetDouble(this object value, object value2, params string[] properties)
        {
            return value.GetProperty<double>(value2, properties);
        }

        internal static bool GetBoolean(this object value, object value2, params string[] properties)
        {
            return value.GetProperty<bool>(value2, properties);
        }

        internal static bool TryGetProperty<TValue>(this object properties2, ref TValue tValue, object value, params string[] properties)
        {
            string text = BuildPropertyPath(value, properties);
            if (!((IProperties)properties2).Contains(text))
            {
                return false;
            }
            tValue = properties2.GetProperty<TValue>(text, Array.Empty<string>());
            return true;
        }

        private static string BuildPropertyPath(object value, params string[] properties)
        {
            if (properties.Length != 0)
            {
                return (string)value + "." + string.Join(".", properties);
            }
            return (string)value;
        }

        internal static bool IsObfuscationSentinelValid()

        {

            return true;

        }
}
}
