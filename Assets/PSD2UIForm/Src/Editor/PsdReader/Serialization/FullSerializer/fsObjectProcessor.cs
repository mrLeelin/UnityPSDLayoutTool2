using System;

namespace cn.efunstudio.psdreader.FullSerializer
{
    public abstract class fsObjectProcessor
    {
        private static fsObjectProcessor s_ObfuscationSentinel;

        public virtual bool CanProcess(Type type)
        {
            throw new NotImplementedException();
        }

        public virtual void OnBeforeSerialize(Type storageType, object instance)
        {
        }

        public virtual void OnAfterSerialize(Type storageType, object instance, ref fsData data)
        {
        }

        public virtual void OnBeforeDeserialize(Type storageType, ref fsData data)
        {
        }

        public virtual void OnBeforeDeserializeAfterInstanceCreation(Type storageType, object instance, ref fsData data)
        {
        }

        public virtual void OnAfterDeserialize(Type storageType, object instance)
        {
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static fsObjectProcessor GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
