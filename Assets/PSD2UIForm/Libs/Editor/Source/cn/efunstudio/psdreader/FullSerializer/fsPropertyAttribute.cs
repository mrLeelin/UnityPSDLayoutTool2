using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer
{

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class fsPropertyAttribute : Attribute
{
	public string Name;

	public Type Converter;

	public fsPropertyAttribute()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private fsPropertyAttribute(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: this(string.Empty)
	{
	}

	public fsPropertyAttribute(string name)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), name)
	{
	}

	private fsPropertyAttribute(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, string name)
		: base()
	{
		Name = name;
	}
}
}
