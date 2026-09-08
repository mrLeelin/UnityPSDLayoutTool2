using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace PsdReaderMetadata
{

[AttributeUsage(AttributeTargets.Class)]
internal class PsdBlockSignatureAttribute : Attribute
{
	private readonly string _id;

	private string _displayName;

	public string ID => _id;

	public string DisplayName
	{
		get
		{
			if (!string.IsNullOrEmpty(_displayName))
			{
				return _displayName;
			}
			return _id;
		}
		set
		{
			_displayName = value;
		}
	}

	public PsdBlockSignatureAttribute(string P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private PsdBlockSignatureAttribute(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, string P_0)
		: base()
	{
		_id = P_0;
	}
}
}
