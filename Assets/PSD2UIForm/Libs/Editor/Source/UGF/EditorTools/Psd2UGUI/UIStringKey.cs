using PsdProtectionGuards;
using UnityEngine;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

public sealed class UIStringKey : MonoBehaviour
{
	[SerializeField]
	private string m_Key;

	internal string Key
	{
		get
		{
			return m_Key ?? string.Empty;
		}
		set
		{
			m_Key = value;
		}
	}

	public UIStringKey()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private UIStringKey(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
