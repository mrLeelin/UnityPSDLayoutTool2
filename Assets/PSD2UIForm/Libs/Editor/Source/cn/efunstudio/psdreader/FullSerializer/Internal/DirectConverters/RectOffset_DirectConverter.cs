using System;
using System.Collections.Generic;
using PsdProtectionGuards;
using UnityEngine;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer.Internal.DirectConverters
{

public class RectOffset_DirectConverter : fsDirectConverter<RectOffset>
{
	protected override fsResult DoSerialize(RectOffset model, Dictionary<string, fsData> serialized)
	{
		return global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success + SerializeMember(serialized, null, "bottom", model.bottom) + SerializeMember(serialized, null, "left", model.left) + SerializeMember(serialized, null, "right", model.right) + SerializeMember(serialized, null, "top", model.top);
	}

	protected override fsResult DoDeserialize(Dictionary<string, fsData> data, ref RectOffset model)
	{
		fsResult success = global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
		int value = model.bottom;
		fsResult fsResult = success + DeserializeMember<int>(data, null, "bottom", out value);
		model.bottom = value;
		int value2 = model.left;
		fsResult fsResult2 = fsResult + DeserializeMember<int>(data, null, "left", out value2);
		model.left = value2;
		int value3 = model.right;
		fsResult fsResult3 = fsResult2 + DeserializeMember<int>(data, null, "right", out value3);
		model.right = value3;
		int value4 = model.top;
		fsResult result = fsResult3 + DeserializeMember<int>(data, null, "top", out value4);
		model.top = value4;
		return result;
	}

	public override object CreateInstance(fsData data, Type storageType)
	{
		return new RectOffset();
	}

	public RectOffset_DirectConverter()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private RectOffset_DirectConverter(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
