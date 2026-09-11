using System;
using System.Collections.Generic;
using UnityEngine;

using Object = UnityEngine.Object;
namespace cn.efunstudio.psdreader.FullSerializer.Internal.DirectConverters
{
    public class Keyframe_DirectConverter : fsDirectConverter<Keyframe>
    {
        private static Keyframe_DirectConverter s_ObfuscationSentinel;

        protected override fsResult DoSerialize(Keyframe model, Dictionary<string, fsData> serialized)
        {
            return fsResult.Success + SerializeMember(serialized, null, "time", model.time) + SerializeMember(serialized, null, "value", model.value) + SerializeMember(serialized, null, "inTangent", model.inTangent) + SerializeMember(serialized, null, "outTangent", model.outTangent) + SerializeMember(serialized, null, "inWeight", model.inWeight) + SerializeMember(serialized, null, "outWeight", model.outWeight) + SerializeMember<WeightedMode>(serialized, null, "weightedMode", model.weightedMode);
        }

        protected override fsResult DoDeserialize(Dictionary<string, fsData> data, ref Keyframe model)
        {
            fsResult success = fsResult.Success;
            float value = model.time;
            success += DeserializeMember<float>(data, null, "time", out value);
            model.time = value;
            float value2 = model.value;
            success += DeserializeMember<float>(data, null, "value", out value2);
            model.value = value2;
            if (data.ContainsKey("tangentMode"))
            {
                int value3 = 0;
                success += DeserializeMember<int>(data, null, "tangentMode", out value3);
            }
            float value4 = model.inTangent;
            success += DeserializeMember<float>(data, null, "inTangent", out value4);
            model.inTangent = value4;
            float value5 = model.outTangent;
            success += DeserializeMember<float>(data, null, "outTangent", out value5);
            model.outTangent = value5;
            float value6 = model.inWeight;
            success += DeserializeMember<float>(data, null, "inWeight", out value6);
            model.inWeight = value6;
            float value7 = model.outWeight;
            success += DeserializeMember<float>(data, null, "outWeight", out value7);
            model.outWeight = value7;
            WeightedMode value8 = model.weightedMode;
            success += DeserializeMember<WeightedMode>(data, null, "weightedMode", out value8);
            model.weightedMode = value8;
            return success;
        }

        public override object CreateInstance(fsData data, Type storageType)
        {
            return (object)default(Keyframe);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static Keyframe_DirectConverter GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
