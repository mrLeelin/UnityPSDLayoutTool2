using System;
using System.Collections.Generic;
using UnityEngine;

using Object = UnityEngine.Object;
namespace cn.efunstudio.psdreader.FullSerializer.Internal.DirectConverters
{
    public class Rect_DirectConverter : fsDirectConverter<Rect>
    {
        private static Rect_DirectConverter s_ObfuscationSentinel;

        protected override fsResult DoSerialize(Rect model, Dictionary<string, fsData> serialized)
        {
            return fsResult.Success + SerializeMember(serialized, null, "xMin", model.xMin) + SerializeMember(serialized, null, "yMin", model.yMin) + SerializeMember(serialized, null, "xMax", model.xMax) + SerializeMember(serialized, null, "yMax", model.yMax);
        }

        protected override fsResult DoDeserialize(Dictionary<string, fsData> data, ref Rect model)
        {
            fsResult success = fsResult.Success;
            float value = model.xMin;
            fsResult fsResultA = success + DeserializeMember<float>(data, null, "xMin", out value);
            model.xMin = value;
            float value2 = model.yMin;
            fsResult fsResult2 = fsResultA + DeserializeMember<float>(data, null, "yMin", out value2);
            model.yMin = value2;
            float value3 = model.xMax;
            fsResult fsResult3 = fsResult2 + DeserializeMember<float>(data, null, "xMax", out value3);
            model.xMax = value3;
            float value4 = model.yMax;
            fsResult result = fsResult3 + DeserializeMember<float>(data, null, "yMax", out value4);
            model.yMax = value4;
            return result;
        }

        public override object CreateInstance(fsData data, Type storageType)
        {
            return (object)default(Rect);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static Rect_DirectConverter GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
