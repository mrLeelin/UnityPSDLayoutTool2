using System;

namespace cn.efunstudio.psdreader.FullSerializer.Internal
{
    public class fsWeakReferenceConverter : fsConverter
    {
        private static fsWeakReferenceConverter s_ObfuscationSentinel;

        public override bool CanProcess(Type type)
        {
            return type == typeof(WeakReference);
        }

        public override bool RequestCycleSupport(Type storageType)
        {
            return false;
        }

        public override bool RequestInheritanceSupport(Type storageType)
        {
            return false;
        }

        public override fsResult TrySerialize(object instance, out fsData serialized, Type storageType)
        {
            WeakReference weakReference = (WeakReference)instance;
            fsResult success = fsResult.Success;
            serialized = fsData.CreateDictionary();
            if (weakReference.IsAlive)
            {
                fsData data;
                fsResult fsResultA = (success += Serializer.TrySerialize(weakReference.Target, out data));
                if (fsResultA.Failed)
                {
                    return success;
                }
                serialized.AsDictionary["Target"] = data;
                serialized.AsDictionary["TrackResurrection"] = new fsData(weakReference.TrackResurrection);
            }
            return success;
        }

        public override fsResult TryDeserialize(fsData data, ref object instance, Type storageType)
        {
            fsResult success = fsResult.Success;
            fsResult fsResultA = (success += CheckType(data, fsDataType.Object));
            if (fsResultA.Failed)
            {
                return success;
            }
            if (data.AsDictionary.ContainsKey("Target"))
            {
                fsData data2 = data.AsDictionary["Target"];
                object result = null;
                fsResultA = (success += Serializer.TryDeserialize(data2, typeof(object), ref result));
                if (fsResultA.Failed)
                {
                    return success;
                }
                bool trackResurrection = false;
                if (data.AsDictionary.ContainsKey("TrackResurrection") && data.AsDictionary["TrackResurrection"].IsBool)
                {
                    trackResurrection = data.AsDictionary["TrackResurrection"].AsBool;
                }
                instance = new WeakReference(result, trackResurrection);
            }
            return success;
        }

        public override object CreateInstance(fsData data, Type storageType)
        {
            return new WeakReference(null);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static fsWeakReferenceConverter GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
