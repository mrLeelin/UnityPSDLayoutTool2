using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer
{

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class fsObjectAttribute : Attribute
{
	public Type[] PreviousModels;

	public string VersionString;

	public fsMemberSerialization MemberSerialization = fsMemberSerialization.Default;

	public Type Converter;

	public Type Processor;

	public fsObjectAttribute()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private fsObjectAttribute(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}

	public fsObjectAttribute(string versionString, params Type[] previousModels)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), versionString, previousModels)
	{
	}

	private fsObjectAttribute(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, string versionString, Type[] previousModels)
		: base()
	{
		VersionString = versionString;
		PreviousModels = previousModels;
	}
}
}
