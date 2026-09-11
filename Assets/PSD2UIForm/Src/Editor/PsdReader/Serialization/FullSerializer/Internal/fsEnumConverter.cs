using System;
using System.Collections.Generic;
using System.Text;

namespace cn.efunstudio.psdreader.FullSerializer.Internal
{
    public class fsEnumConverter : fsConverter
    {
        internal static fsEnumConverter s_ObfuscationSentinel;

        public override bool CanProcess(Type type)
        {
            return type.Resolve().IsEnum;
        }

        public override bool RequestCycleSupport(Type storageType)
        {
            return false;
        }

        public override bool RequestInheritanceSupport(Type storageType)
        {
            return false;
        }

        public override object CreateInstance(fsData data, Type storageType)
        {
            return Enum.ToObject(storageType, (object)0);
        }

        public override fsResult TrySerialize(object instance, out fsData serialized, Type storageType)
        {
            if (!Serializer.Config.SerializeEnumsAsInteger)
            {
                if (fsPortableReflection.GetAttribute<FlagsAttribute>(storageType) != null)
                {
                    long num = Convert.ToInt64(instance);
                    StringBuilder stringBuilder = new StringBuilder();
                    bool flag = true;
                    foreach (object value in Enum.GetValues(storageType))
                    {
                        long num2 = Convert.ToInt64(value);
                        if ((ulong)(num & num2) > 0uL)
                        {
                            if (!flag)
                            {
                                stringBuilder.Append(",");
                            }
                            flag = false;
                            stringBuilder.Append(value.ToString());
                        }
                    }
                    serialized = new fsData(stringBuilder.ToString());
                }
                else
                {
                    serialized = new fsData(Enum.GetName(storageType, instance));
                }
            }
            else
            {
                serialized = new fsData(Convert.ToInt64(instance));
            }
            return fsResult.Success;
        }

        public override fsResult TryDeserialize(fsData data, ref object instance, Type storageType)
        {
            if (!data.IsString)
            {
                if (!data.IsInt64)
                {
                    return fsResult.Fail("EnumConverter encountered an unknown JSON data type");
                }
                int num = (int)data.AsInt64;
                instance = Enum.ToObject(storageType, (object)num);
                return fsResult.Success;
            }
            string[] array = data.AsString.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            long num2 = 0L;
            foreach (string text in array)
            {
                if (ArrayContains(Enum.GetNames(storageType), text))
                {
                    long num3 = (long)Convert.ChangeType(Enum.Parse(storageType, text), typeof(long));
                    num2 |= num3;
                    continue;
                }
                return fsResult.Fail("Cannot find enum name " + text + " on type " + storageType);
            }
            instance = Enum.ToObject(storageType, (object)num2);
            return fsResult.Success;
        }

        private static bool ArrayContains<T>(T[] values, T value)
        {
            int num = 0;
            while (true)
            {
                if (num < values.Length)
                {
                    if (EqualityComparer<T>.Default.Equals(values[num], value))
                    {
                        break;
                    }
                    num++;
                    continue;
                }
                return false;
            }
            return true;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static fsEnumConverter GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
