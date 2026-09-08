using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace PsdBinaryUtilities
{

internal class PsdInvalidDataException : Exception
{
	public PsdInvalidDataException()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private PsdInvalidDataException(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base("Invalid PSD file")
	{
	}

	public PsdInvalidDataException(string P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private PsdInvalidDataException(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, string P_0)
		: base(P_0)
	{
	}

	public PsdInvalidDataException(string P_0, params object[] args)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, args)
	{
	}

	private PsdInvalidDataException(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, string P_0, object[] args)
		: base(string.Format(P_0, args))
	{
	}
}
}
