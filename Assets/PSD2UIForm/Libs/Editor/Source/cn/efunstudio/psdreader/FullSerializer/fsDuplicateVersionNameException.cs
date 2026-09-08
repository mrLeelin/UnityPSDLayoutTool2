using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer
{

public sealed class fsDuplicateVersionNameException : Exception
{
	public fsDuplicateVersionNameException(Type typeA, Type typeB, string version)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), typeA, typeB, version)
	{
	}

	private fsDuplicateVersionNameException(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, Type typeA, Type typeB, string version)
		: base(typeA?.ToString() + " and " + typeB?.ToString() + " have the same version string (" + version + "); please change one of them.")
	{
	}
}
}
