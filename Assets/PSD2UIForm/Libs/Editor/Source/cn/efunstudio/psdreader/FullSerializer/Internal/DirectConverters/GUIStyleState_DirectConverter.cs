using System;
using System.Collections.Generic;
using PsdProtectionGuards;
using UnityEngine;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer.Internal.DirectConverters
{

public class GUIStyleState_DirectConverter : fsDirectConverter<GUIStyleState>
{
	protected override fsResult DoSerialize(GUIStyleState model, Dictionary<string, fsData> serialized)
	{
		return global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success + SerializeMember(serialized, null, "background", model.background) + SerializeMember(serialized, null, "textColor", model.textColor);
	}

	protected override fsResult DoDeserialize(Dictionary<string, fsData> data, ref GUIStyleState model)
	{
		fsResult success = global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
		Texture2D value = model.background;
		fsResult fsResult = success + DeserializeMember<Texture2D>(data, null, "background", out value);
		model.background = value;
		Color value2 = model.textColor;
		fsResult result = fsResult + DeserializeMember<Color>(data, null, "textColor", out value2);
		model.textColor = value2;
		return result;
	}

	public override object CreateInstance(fsData data, Type storageType)
	{
		return new GUIStyleState();
	}

	public GUIStyleState_DirectConverter()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private GUIStyleState_DirectConverter(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
