using PsdProtectionGuards;
using UnityEngine;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader
{

internal class EditorStatusUpdate : CustomYieldInstruction
{
	public string Label;

	public float PercentComplete;

	public bool HasLabelUpdate;

	public bool HasPercentUpdate;

	public override bool keepWaiting => false;

	public EditorStatusUpdate(string label)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), label)
	{
	}

	private EditorStatusUpdate(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, string label)
		: base()
	{
		HasPercentUpdate = false;
		HasLabelUpdate = true;
		Label = label;
	}

	public EditorStatusUpdate(float percent)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), percent)
	{
	}

	private EditorStatusUpdate(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, float percent)
		: base()
	{
		HasPercentUpdate = true;
		PercentComplete = percent;
		HasLabelUpdate = false;
	}

	public EditorStatusUpdate(string label, float percent)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), label, percent)
	{
	}

	private EditorStatusUpdate(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, string label, float percent)
		: base()
	{
		HasPercentUpdate = true;
		PercentComplete = percent;
		HasLabelUpdate = true;
		Label = label;
	}
}
}
