using System;
using System.Reflection;
using UnityEngine;

namespace cn.efunstudio.psdreader.FullSerializer
{
    public class fsConfig
    {
        public Type[] SerializeAttributes = new Type[2]
        {
            typeof(SerializeField),
            typeof(fsPropertyAttribute)
        };

        public Type[] IgnoreSerializeAttributes = new Type[2]
        {
            typeof(NonSerializedAttribute),
            typeof(fsIgnoreAttribute)
        };

        public Type[] IgnoreSerializeTypeAttributes = new Type[1] { typeof(fsIgnoreAttribute) };

        public fsMemberSerialization DefaultMemberSerialization = fsMemberSerialization.Default;

        public Func<string, MemberInfo, string> GetJsonNameFromMemberName = (string name, MemberInfo info) => name;

        public bool EnablePropertySerialization = true;

        public bool SerializeNonAutoProperties;

        public bool SerializeNonPublicSetProperties = true;

        public string CustomDateTimeFormatString;

        public bool Serialize64BitIntegerAsString;

        public bool SerializeEnumsAsInteger;

        private static fsConfig s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static fsConfig GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
