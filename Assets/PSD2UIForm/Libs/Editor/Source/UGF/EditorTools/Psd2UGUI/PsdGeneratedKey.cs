using System.Runtime.CompilerServices;
using PsdProtectionGuards;
using UnityEngine;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[AddComponentMenu("")]
[DisallowMultipleComponent]
public sealed class PsdGeneratedKey : MonoBehaviour
{
	[HideInInspector]
	[SerializeField]
	private string m_Key;

	[SerializeField]
	[HideInInspector]
	private string m_TypeKey;

	[SerializeField]
	[HideInInspector]
	private bool m_IsContainer;

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

	[SpecialName]
	internal string GetTypeKey()
	{
		return m_TypeKey ?? string.Empty;
	}

	[SpecialName]
	internal void SetTypeKey(string P_0)
	{
		m_TypeKey = P_0;
	}

	[SpecialName]
	internal bool GetIsContainer()
	{
		return m_IsContainer;
	}

	[SpecialName]
	internal void SetIsContainer(bool P_0)
	{
		m_IsContainer = P_0;
	}

	[SpecialName]
	internal string GetCompositeIdentity()
	{
		return (m_IsContainer ? "C" : "N") + ":" + GetTypeKey() + ":" + Key;
	}

	public PsdGeneratedKey()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private PsdGeneratedKey(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
