using System;

namespace cn.efunstudio.psdreader.FullSerializer.Internal
{
    public class fsForwardConverter : fsConverter
    {
        private string _memberName;

        private static fsForwardConverter s_ObfuscationSentinel;

        public fsForwardConverter(fsForwardAttribute attribute)
        {
            _memberName = attribute.MemberName;
        }

        public override bool CanProcess(Type type)
        {
            throw new NotSupportedException("Please use the [fsForward(...)] attribute.");
        }

        private fsResult GetProperty(object instance, out fsMetaProperty property)
        {
            fsMetaProperty[] properties = fsMetaType.Get(Serializer.Config, instance.GetType()).Properties;
            int num = 0;
            while (true)
            {
                if (num < properties.Length)
                {
                    if (properties[num].MemberName == _memberName)
                    {
                        break;
                    }
                    num++;
                    continue;
                }
                property = null;
                return fsResult.Fail("No property named \"" + _memberName + "\" on " + instance.GetType().CSharpName());
            }
            property = properties[num];
            return fsResult.Success;
        }

        public override fsResult TrySerialize(object instance, out fsData serialized, Type storageType)
        {
            serialized = fsData.Null;
            fsResult success = fsResult.Success;
            fsMetaProperty property;
            fsResult fsResultA = (success += GetProperty(instance, out property));
            if (fsResultA.Failed)
            {
                return success;
            }
            object instance2 = property.Read(instance);
            return Serializer.TrySerialize(property.StorageType, instance2, out serialized);
        }

        public override fsResult TryDeserialize(fsData data, ref object instance, Type storageType)
        {
            fsResult success = fsResult.Success;
            fsResult fsResultA = (success += GetProperty(instance, out var property));
            if (!fsResultA.Failed)
            {
                object result = null;
                fsResultA = (success += Serializer.TryDeserialize(data, property.StorageType, ref result));
                if (fsResultA.Failed)
                {
                    return success;
                }
                property.Write(instance, result);
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

        internal static fsForwardConverter GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
