using System;

namespace cn.efunstudio.psdreader.FullSerializer.Internal
{
    public class fsPrimitiveConverter : fsConverter
    {
        private static fsPrimitiveConverter s_ObfuscationSentinel;

        public override bool CanProcess(Type type)
        {
            if (!type.Resolve().IsPrimitive && !(type == typeof(string)))
            {
                return type == typeof(decimal);
            }
            return true;
        }

        public override bool RequestCycleSupport(Type storageType)
        {
            return false;
        }

        public override bool RequestInheritanceSupport(Type storageType)
        {
            return false;
        }

        private static bool UseBool(Type type)
        {
            return type == typeof(bool);
        }

        private static bool UseInt64(Type type)
        {
            if (!(type == typeof(sbyte)) && !(type == typeof(byte)) && !(type == typeof(short)) && !(type == typeof(ushort)) && !(type == typeof(int)) && !(type == typeof(uint)) && !(type == typeof(long)))
            {
                return type == typeof(ulong);
            }
            return true;
        }

        private static bool UseDouble(Type type)
        {
            if (!(type == typeof(float)) && !(type == typeof(double)))
            {
                return type == typeof(decimal);
            }
            return true;
        }

        private static bool UseString(Type type)
        {
            if (type == typeof(string))
            {
                return true;
            }
            return type == typeof(char);
        }

        public override fsResult TrySerialize(object instance, out fsData serialized, Type storageType)
        {
            Type type = instance.GetType();
            if (Serializer.Config.Serialize64BitIntegerAsString && (type == typeof(long) || type == typeof(ulong)))
            {
                serialized = new fsData((string)Convert.ChangeType(instance, typeof(string)));
                return fsResult.Success;
            }
            if (!UseBool(type))
            {
                if (!UseInt64(type))
                {
                    if (UseDouble(type))
                    {
                        if (instance.GetType() == typeof(float) && (float)instance != float.MinValue && (float)instance != float.MaxValue && !float.IsInfinity((float)instance) && !float.IsNaN((float)instance))
                        {
                            serialized = new fsData((double)(decimal)(float)instance);
                            return fsResult.Success;
                        }
                        serialized = new fsData((double)Convert.ChangeType(instance, typeof(double)));
                        return fsResult.Success;
                    }
                    if (!UseString(type))
                    {
                        serialized = null;
                        return fsResult.Fail("Unhandled primitive type " + instance.GetType());
                    }
                    serialized = new fsData((string)Convert.ChangeType(instance, typeof(string)));
                    return fsResult.Success;
                }
                serialized = new fsData((long)Convert.ChangeType(instance, typeof(long)));
                return fsResult.Success;
            }
            serialized = new fsData((bool)instance);
            return fsResult.Success;
        }

        public override fsResult TryDeserialize(fsData storage, ref object instance, Type storageType)
        {
            fsResult success = fsResult.Success;
            if (!UseBool(storageType))
            {
                if (!UseDouble(storageType) && !UseInt64(storageType))
                {
                    if (UseString(storageType))
                    {
                        fsResult fsResultA = (success += CheckType(storage, fsDataType.String));
                        if (fsResultA.Succeeded)
                        {
                            instance = storage.AsString;
                        }
                        return success;
                    }
                    return fsResult.Fail(GetType().Name + ": Bad data; expected bool, number, string, but got " + storage);
                }
                if (!storage.IsDouble)
                {
                    if (storage.IsInt64)
                    {
                        instance = Convert.ChangeType(storage.AsInt64, storageType);
                    }
                    else
                    {
                        if (!Serializer.Config.Serialize64BitIntegerAsString || !storage.IsString || (!(storageType == typeof(long)) && !(storageType == typeof(ulong))))
                        {
                            return fsResult.Fail(GetType().Name + " expected number but got " + storage.Type.ToString() + " in " + storage);
                        }
                        instance = Convert.ChangeType(storage.AsString, storageType);
                    }
                }
                else
                {
                    instance = Convert.ChangeType(storage.AsDouble, storageType);
                }
                return fsResult.Success;
            }
            if ((success += CheckType(storage, fsDataType.Boolean)).Succeeded)
            {
                instance = storage.AsBool;
            }
            return success;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static fsPrimitiveConverter GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
