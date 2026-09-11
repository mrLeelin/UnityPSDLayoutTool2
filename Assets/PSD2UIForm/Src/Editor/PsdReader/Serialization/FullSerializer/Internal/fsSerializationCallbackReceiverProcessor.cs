using System;
using UnityEngine;

using Object = UnityEngine.Object;
namespace cn.efunstudio.psdreader.FullSerializer.Internal
{
    public class fsSerializationCallbackReceiverProcessor : fsObjectProcessor
    {
        private static fsSerializationCallbackReceiverProcessor s_ObfuscationSentinel;

        public override bool CanProcess(Type type)
        {
            return typeof(ISerializationCallbackReceiver).IsAssignableFrom(type);
        }

        public override void OnBeforeSerialize(Type storageType, object instance)
        {
            if (instance != null)
            {
                ((ISerializationCallbackReceiver)instance).OnBeforeSerialize();
            }
        }

        public override void OnAfterDeserialize(Type storageType, object instance)
        {
            if (instance != null)
            {
                ((ISerializationCallbackReceiver)instance).OnAfterDeserialize();
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static fsSerializationCallbackReceiverProcessor GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
