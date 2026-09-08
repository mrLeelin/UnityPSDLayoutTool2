using System;
using System.Collections.Generic;
using PsdProtectionGuards;
using UnityEngine;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer.Internal.DirectConverters
{

public class AnimationCurve_DirectConverter : fsDirectConverter<AnimationCurve>
{
	protected override fsResult DoSerialize(AnimationCurve model, Dictionary<string, fsData> serialized)
	{
		return global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success + SerializeMember(serialized, null, "keys", model.keys) + SerializeMember(serialized, null, "preWrapMode", model.preWrapMode) + SerializeMember(serialized, null, "postWrapMode", model.postWrapMode);
	}

	protected override fsResult DoDeserialize(Dictionary<string, fsData> data, ref AnimationCurve model)
	{
		fsResult success = global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
		Keyframe[] value = model.keys;
		fsResult fsResult = success + DeserializeMember<Keyframe[]>(data, null, "keys", out value);
		model.keys = value;
		WrapMode value2 = model.preWrapMode;
		fsResult fsResult2 = fsResult + DeserializeMember<WrapMode>(data, null, "preWrapMode", out value2);
		model.preWrapMode = value2;
		WrapMode value3 = model.postWrapMode;
		fsResult result = fsResult2 + DeserializeMember<WrapMode>(data, null, "postWrapMode", out value3);
		model.postWrapMode = value3;
		return result;
	}

	public override object CreateInstance(fsData data, Type storageType)
	{
		return new AnimationCurve();
	}

	public AnimationCurve_DirectConverter()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private AnimationCurve_DirectConverter(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
