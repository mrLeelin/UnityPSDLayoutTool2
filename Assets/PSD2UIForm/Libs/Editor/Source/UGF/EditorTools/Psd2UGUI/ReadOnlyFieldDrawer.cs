using PsdProtectionGuards;
using PsdEditorAttributes;
using UnityEditor;
using UnityEngine;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[CustomPropertyDrawer(typeof(PsdReadOnlyAttribute))]
internal sealed class ReadOnlyFieldDrawer : PropertyDrawer
{
	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		return EditorGUI.GetPropertyHeight(property, label, includeChildren: true);
	}

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		GUI.enabled = false;
		EditorGUI.PropertyField(position, property, label, includeChildren: true);
		GUI.enabled = true;
	}

	public ReadOnlyFieldDrawer()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private ReadOnlyFieldDrawer(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
