using System;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Events;

namespace cn.efunstudio.psdreader.FullSerializer.Internal.Converters
{
    public class UnityEvent_Converter : fsConverter
    {
        internal static UnityEvent_Converter s_ObfuscationSentinel;

        public override bool CanProcess(Type type)
        {
            if (typeof(UnityEvent).Resolve().IsAssignableFrom(type.Resolve()))
            {
                return !type.IsGenericType();
            }
            return false;
        }

        public override bool RequestCycleSupport(Type storageType)
        {
            return false;
        }

        public override fsResult TryDeserialize(fsData data, ref object instance, Type storageType)
        {
            Type type = (Type)instance;
            fsResult success = fsResult.Success;
            instance = JsonUtility.FromJson(fsJsonPrinter.CompressedJson(data), type);
            return success;
        }

        public override fsResult TrySerialize(object instance, out fsData serialized, Type storageType)
        {
            fsResult success = fsResult.Success;
            serialized = fsJsonParser.Parse(JsonUtility.ToJson(instance));
            return success;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static UnityEvent_Converter GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
