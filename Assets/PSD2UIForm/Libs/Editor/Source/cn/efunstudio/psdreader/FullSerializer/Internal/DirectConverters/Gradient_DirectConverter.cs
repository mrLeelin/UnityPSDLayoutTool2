using System;
using System.Collections.Generic;
using PsdProtectionGuards;
using UnityEngine;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer.Internal.DirectConverters
{

public class Gradient_DirectConverter : fsDirectConverter<Gradient>
{
	protected override fsResult DoSerialize(Gradient model, Dictionary<string, fsData> serialized)
	{
		return global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success + SerializeMember(serialized, null, "alphaKeys", model.alphaKeys) + SerializeMember(serialized, null, "colorKeys", model.colorKeys);
	}

	protected override fsResult DoDeserialize(Dictionary<string, fsData> data, ref Gradient model)
	{
		fsResult success = global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
		GradientAlphaKey[] value = model.alphaKeys;
		fsResult fsResult = success + DeserializeMember<GradientAlphaKey[]>(data, null, "alphaKeys", out value);
		model.alphaKeys = value;
		GradientColorKey[] value2 = model.colorKeys;
		fsResult result = fsResult + DeserializeMember<GradientColorKey[]>(data, null, "colorKeys", out value2);
		model.colorKeys = value2;
		return result;
	}

	public override object CreateInstance(fsData data, Type storageType)
	{
		return new Gradient();
	}

	public Gradient_DirectConverter()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private Gradient_DirectConverter(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
