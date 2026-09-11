using System;
using System.Collections.Generic;
using UnityEngine;

using Object = UnityEngine.Object;
namespace cn.efunstudio.psdreader.FullSerializer.Internal.DirectConverters
{
    public class Bounds_DirectConverter : fsDirectConverter<Bounds>
    {
        internal static Bounds_DirectConverter s_ObfuscationSentinel;

        protected override fsResult DoSerialize(Bounds model, Dictionary<string, fsData> serialized)
        {
            return fsResult.Success + SerializeMember<Vector3>(serialized, null, "center", model.center) + SerializeMember<Vector3>(serialized, null, "size", model.size);
        }

        protected override fsResult DoDeserialize(Dictionary<string, fsData> data, ref Bounds model)
        {
            fsResult success = fsResult.Success;
            Vector3 value = model.center;
            fsResult fsResultA = success + DeserializeMember<Vector3>(data, null, "center", out value);
            model.center = value;
            Vector3 value2 = model.size;
            fsResult result = fsResultA + DeserializeMember<Vector3>(data, null, "size", out value2);
            model.size = value2;
            return result;
        }

        public override object CreateInstance(fsData data, Type storageType)
        {
            return (object)default(Bounds);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static Bounds_DirectConverter GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
