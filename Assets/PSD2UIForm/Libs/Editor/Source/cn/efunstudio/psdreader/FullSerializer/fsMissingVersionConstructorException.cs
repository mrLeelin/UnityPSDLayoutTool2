using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer
{

public sealed class fsMissingVersionConstructorException : Exception
{
	public fsMissingVersionConstructorException(Type versionedType, Type constructorType)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), versionedType, constructorType)
	{
	}

	private fsMissingVersionConstructorException(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, Type versionedType, Type constructorType)
		: base(versionedType?.ToString() + " is missing a constructor for previous model type " + constructorType)
	{
	}
}
}
