using System;
using System.Collections.Generic;
using UnityEngine;

using Object = UnityEngine.Object;
namespace cn.efunstudio.psdreader.FullSerializer.Internal.DirectConverters
{
    public class GUIStyleState_DirectConverter : fsDirectConverter<GUIStyleState>
    {
        private static GUIStyleState_DirectConverter s_ObfuscationSentinel;

        protected override fsResult DoSerialize(GUIStyleState model, Dictionary<string, fsData> serialized)
        {
            return fsResult.Success + SerializeMember<Texture2D>(serialized, null, "background", model.background) + SerializeMember<Color>(serialized, null, "textColor", model.textColor);
        }

        protected override fsResult DoDeserialize(Dictionary<string, fsData> data, ref GUIStyleState model)
        {
            fsResult success = fsResult.Success;
            Texture2D value = model.background;
            fsResult fsResultA = success + DeserializeMember<Texture2D>(data, null, "background", out value);
            model.background = value;
            Color value2 = model.textColor;
            fsResult result = fsResultA + DeserializeMember<Color>(data, null, "textColor", out value2);
            model.textColor = value2;
            return result;
        }

        public override object CreateInstance(fsData data, Type storageType)
        {
            return (object)new GUIStyleState();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static GUIStyleState_DirectConverter GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
