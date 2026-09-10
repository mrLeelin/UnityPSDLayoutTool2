using System;
using System.Collections.Generic;
using UnityEngine;

using Object = UnityEngine.Object;
namespace cn.efunstudio.psdreader.FullSerializer.Internal.DirectConverters
{
    public class LayerMask_DirectConverter : fsDirectConverter<LayerMask>
    {
        internal static LayerMask_DirectConverter s_ObfuscationSentinel;

        protected override fsResult DoSerialize(LayerMask model, Dictionary<string, fsData> serialized)
        {
            return fsResult.Success + SerializeMember(serialized, null, "value", model.value);
        }

        protected override fsResult DoDeserialize(Dictionary<string, fsData> data, ref LayerMask model)
        {
            fsResult success = fsResult.Success;
            int value = model.value;
            fsResult result = success + DeserializeMember<int>(data, null, "value", out value);
            model.value = value;
            return result;
        }

        public override object CreateInstance(fsData data, Type storageType)
        {
            return (object)default(LayerMask);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LayerMask_DirectConverter GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
