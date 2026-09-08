using PsdProtectionGuards;
using UnityEngine;
using PsdProtectionRuntime;

namespace PsdEditorAttributes
{

internal sealed class PsdReadOnlyAttribute : PropertyAttribute
{
	public PsdReadOnlyAttribute()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private PsdReadOnlyAttribute(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
