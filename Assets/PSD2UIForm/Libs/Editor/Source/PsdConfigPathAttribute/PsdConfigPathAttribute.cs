using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace PsdEditorAttributes
{

[AttributeUsage(AttributeTargets.Class)]
internal sealed class PsdConfigPathAttribute : Attribute
{
	internal string RelativePath;

	internal PsdConfigPathAttribute(string P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private PsdConfigPathAttribute(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, string P_0)
		: base()
	{
		if (string.IsNullOrEmpty(P_0))
		{
			throw new ArgumentException("Invalid relative path (it is empty)");
		}
		if (P_0[0] == '/')
		{
			P_0 = P_0.Substring(1);
		}
		RelativePath = P_0;
	}
}
}
