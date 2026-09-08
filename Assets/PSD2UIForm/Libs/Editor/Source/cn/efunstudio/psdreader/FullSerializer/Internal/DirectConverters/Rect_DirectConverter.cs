using System;
using System.Collections.Generic;
using PsdProtectionGuards;
using UnityEngine;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer.Internal.DirectConverters
{

public class Rect_DirectConverter : fsDirectConverter<Rect>
{
	protected override fsResult DoSerialize(Rect model, Dictionary<string, fsData> serialized)
	{
		return global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success + SerializeMember(serialized, null, "xMin", model.xMin) + SerializeMember(serialized, null, "yMin", model.yMin) + SerializeMember(serialized, null, "xMax", model.xMax) + SerializeMember(serialized, null, "yMax", model.yMax);
	}

	protected override fsResult DoDeserialize(Dictionary<string, fsData> data, ref Rect model)
	{
		fsResult success = global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
		float value = model.xMin;
		fsResult fsResult = success + DeserializeMember<float>(data, null, "xMin", out value);
		model.xMin = value;
		float value2 = model.yMin;
		fsResult fsResult2 = fsResult + DeserializeMember<float>(data, null, "yMin", out value2);
		model.yMin = value2;
		float value3 = model.xMax;
		fsResult fsResult3 = fsResult2 + DeserializeMember<float>(data, null, "xMax", out value3);
		model.xMax = value3;
		float value4 = model.yMax;
		fsResult result = fsResult3 + DeserializeMember<float>(data, null, "yMax", out value4);
		model.yMax = value4;
		return result;
	}

	public override object CreateInstance(fsData data, Type storageType)
	{
		return default(Rect);
	}

	public Rect_DirectConverter()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private Rect_DirectConverter(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
