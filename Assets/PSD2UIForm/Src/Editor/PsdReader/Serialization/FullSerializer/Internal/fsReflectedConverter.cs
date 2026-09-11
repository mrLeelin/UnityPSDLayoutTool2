using System;
using System.Collections;

namespace cn.efunstudio.psdreader.FullSerializer.Internal
{
    public class fsReflectedConverter : fsConverter
    {
        internal static fsReflectedConverter s_ObfuscationSentinel;

        public override bool CanProcess(Type type)
        {
            if (!type.Resolve().IsArray && !typeof(ICollection).IsAssignableFrom(type))
            {
                return true;
            }
            return false;
        }

        public override fsResult TrySerialize(object instance, out fsData serialized, Type storageType)
        {
            serialized = fsData.CreateDictionary();
            fsResult success = fsResult.Success;
            fsMetaType fsMetaType = fsMetaType.Get(Serializer.Config, instance.GetType());
            fsMetaType.EmitAotData(throwException: false);
            for (int i = 0; i < fsMetaType.Properties.Length; i++)
            {
                fsMetaProperty fsMetaProperty2 = fsMetaType.Properties[i];
                if (fsMetaProperty2.CanRead)
                {
                    fsData data;
                    fsResult result = Serializer.TrySerialize(fsMetaProperty2.StorageType, fsMetaProperty2.OverrideConverterType, fsMetaProperty2.Read(instance), out data);
                    success.AddMessages(result);
                    if (!result.Failed)
                    {
                        serialized.AsDictionary[fsMetaProperty2.JsonName] = data;
                    }
                }
            }
            return success;
        }

        public override fsResult TryDeserialize(fsData data, ref object instance, Type storageType)
        {
            fsResult success = fsResult.Success;
            fsResult fsResultA = (success += CheckType(data, fsDataType.Object));
            if (!fsResultA.Failed)
            {
                fsMetaType fsMetaType = fsMetaType.Get(Serializer.Config, storageType);
                fsMetaType.EmitAotData(throwException: false);
                for (int i = 0; i < fsMetaType.Properties.Length; i++)
                {
                    fsMetaProperty fsMetaProperty2 = fsMetaType.Properties[i];
                    if (fsMetaProperty2.CanWrite && data.AsDictionary.TryGetValue(fsMetaProperty2.JsonName, out var value))
                    {
                        object result = null;
                        if (fsMetaProperty2.CanRead)
                        {
                            result = fsMetaProperty2.Read(instance);
                        }
                        fsResult result2 = Serializer.TryDeserialize(value, fsMetaProperty2.StorageType, fsMetaProperty2.OverrideConverterType, ref result);
                        success.AddMessages(result2);
                        if (!result2.Failed)
                        {
                            fsMetaProperty2.Write(instance, result);
                        }
                    }
                }
                return success;
            }
            return success;
        }

        public override object CreateInstance(fsData data, Type storageType)
        {
            return fsMetaType.Get(Serializer.Config, storageType).CreateInstance();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static fsReflectedConverter GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
