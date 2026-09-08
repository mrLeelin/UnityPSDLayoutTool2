using PsdProtectionGuards;
using PsdProtectionRuntime;
using PsdEditorAttributes;

namespace UGF.EditorTools.Psd2UGUI
{

[PsdConfigPathAttribute("ProjectSettings/Psd2UIFormSettings.asset")]
public sealed class Psd2UIFormSettings : ScriptableSingleton<Psd2UIFormSettings>
{
	public string UIImagesOutputDir;

	public string UIFormOutputDir = "Assets";

	public bool UseUIFormOutputDir = true;

	public bool CompressImage;

	public bool AutoCropMinimalNineSlice;

	public string LastUIFormOutputDir = "Assets";

	public Psd2UIFormSettings()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private Psd2UIFormSettings(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
