using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer
{

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Struct)]
public sealed class fsIgnoreAttribute : Attribute
{
	public fsIgnoreAttribute()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private fsIgnoreAttribute(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
