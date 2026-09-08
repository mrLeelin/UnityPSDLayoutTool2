using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer
{

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct)]
public sealed class fsForwardAttribute : Attribute
{
	public string MemberName;

	public fsForwardAttribute(string memberName)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), memberName)
	{
	}

	private fsForwardAttribute(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, string memberName)
		: base()
	{
		MemberName = memberName;
	}
}
}
