using System;
using System.Collections.Generic;
using PsdProtectionGuards;
using UnityEngine;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer.Internal.DirectConverters
{

public class Bounds_DirectConverter : fsDirectConverter<Bounds>
{
	protected override fsResult DoSerialize(Bounds model, Dictionary<string, fsData> serialized)
	{
		return global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success + SerializeMember(serialized, null, "center", model.center) + SerializeMember(serialized, null, "size", model.size);
	}

	protected override fsResult DoDeserialize(Dictionary<string, fsData> data, ref Bounds model)
	{
		fsResult success = global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
		Vector3 value = model.center;
		fsResult fsResult = success + DeserializeMember<Vector3>(data, null, "center", out value);
		model.center = value;
		Vector3 value2 = model.size;
		fsResult result = fsResult + DeserializeMember<Vector3>(data, null, "size", out value2);
		model.size = value2;
		return result;
	}

	public override object CreateInstance(fsData data, Type storageType)
	{
		return default(Bounds);
	}

	public Bounds_DirectConverter()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private Bounds_DirectConverter(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
