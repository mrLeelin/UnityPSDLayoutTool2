using System;
using System.Reflection;
using PsdProtectionGuards;
using UnityEngine;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer
{

public class fsConfig
{
	public Type[] SerializeAttributes;

	public Type[] IgnoreSerializeAttributes;

	public Type[] IgnoreSerializeTypeAttributes;

	public fsMemberSerialization DefaultMemberSerialization;

	public Func<string, MemberInfo, string> GetJsonNameFromMemberName;

	public bool EnablePropertySerialization;

	public bool SerializeNonAutoProperties;

	public bool SerializeNonPublicSetProperties;

	public string CustomDateTimeFormatString;

	public bool Serialize64BitIntegerAsString;

	public bool SerializeEnumsAsInteger;

	public fsConfig()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		SerializeAttributes = new Type[2]
		{
			typeof(SerializeField),
			typeof(fsPropertyAttribute)
		};
		IgnoreSerializeAttributes = new Type[2]
		{
			typeof(NonSerializedAttribute),
			typeof(fsIgnoreAttribute)
		};
		IgnoreSerializeTypeAttributes = new Type[1] { typeof(fsIgnoreAttribute) };
		DefaultMemberSerialization = fsMemberSerialization.Default;
		GetJsonNameFromMemberName = (string name, MemberInfo info) => name;
		EnablePropertySerialization = true;
		SerializeNonPublicSetProperties = true;
	}
}
}
