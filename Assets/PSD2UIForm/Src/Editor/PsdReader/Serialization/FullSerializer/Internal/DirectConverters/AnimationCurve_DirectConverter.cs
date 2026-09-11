using System;
using System.Collections.Generic;
using UnityEngine;

using Object = UnityEngine.Object;
namespace cn.efunstudio.psdreader.FullSerializer.Internal.DirectConverters
{
    public class AnimationCurve_DirectConverter : fsDirectConverter<AnimationCurve>
    {
        private static AnimationCurve_DirectConverter s_ObfuscationSentinel;

        protected override fsResult DoSerialize(AnimationCurve model, Dictionary<string, fsData> serialized)
        {
            return fsResult.Success + SerializeMember(serialized, null, "keys", model.keys) + SerializeMember<WrapMode>(serialized, null, "preWrapMode", model.preWrapMode) + SerializeMember<WrapMode>(serialized, null, "postWrapMode", model.postWrapMode);
        }

        protected override fsResult DoDeserialize(Dictionary<string, fsData> data, ref AnimationCurve model)
        {
            fsResult success = fsResult.Success;
            Keyframe[] value = model.keys;
            fsResult fsResultA = success + DeserializeMember<Keyframe[]>(data, null, "keys", out value);
            model.keys = value;
            WrapMode value2 = model.preWrapMode;
            fsResult fsResult2 = fsResultA + DeserializeMember<WrapMode>(data, null, "preWrapMode", out value2);
            model.preWrapMode = value2;
            WrapMode value3 = model.postWrapMode;
            fsResult result = fsResult2 + DeserializeMember<WrapMode>(data, null, "postWrapMode", out value3);
            model.postWrapMode = value3;
            return result;
        }

        public override object CreateInstance(fsData data, Type storageType)
        {
            return (object)new AnimationCurve();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AnimationCurve_DirectConverter GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
