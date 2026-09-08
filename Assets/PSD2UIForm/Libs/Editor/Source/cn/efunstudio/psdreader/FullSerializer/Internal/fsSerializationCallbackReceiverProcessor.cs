using System;
using PsdProtectionGuards;
using UnityEngine;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer.Internal
{

public class fsSerializationCallbackReceiverProcessor : fsObjectProcessor
{
	public override bool CanProcess(Type type)
	{
		return typeof(ISerializationCallbackReceiver).IsAssignableFrom(type);
	}

	public override void OnBeforeSerialize(Type storageType, object instance)
	{
		if (instance != null)
		{
			((ISerializationCallbackReceiver)instance).OnBeforeSerialize();
		}
	}

	public override void OnAfterDeserialize(Type storageType, object instance)
	{
		if (instance != null)
		{
			((ISerializationCallbackReceiver)instance).OnAfterDeserialize();
		}
	}

	public fsSerializationCallbackReceiverProcessor()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private fsSerializationCallbackReceiverProcessor(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
