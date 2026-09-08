using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer.Internal
{

public class fsWeakReferenceConverter : fsConverter
{
	public override bool CanProcess(Type type)
	{
		return type == typeof(WeakReference);
	}

	public override bool RequestCycleSupport(Type storageType)
	{
		return false;
	}

	public override bool RequestInheritanceSupport(Type storageType)
	{
		return false;
	}

	public override fsResult TrySerialize(object instance, out fsData serialized, Type storageType)
	{
		WeakReference weakReference = (WeakReference)instance;
		fsResult success = global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
		serialized = fsData.CreateDictionary();
		if (weakReference.IsAlive)
		{
			fsData data;
			fsResult fsResult = (success += Serializer.TrySerialize(weakReference.Target, out data));
			if (fsResult.Failed)
			{
				return success;
			}
			serialized.AsDictionary["Target"] = data;
			serialized.AsDictionary["TrackResurrection"] = new fsData(weakReference.TrackResurrection);
		}
		return success;
	}

	public override fsResult TryDeserialize(fsData data, ref object instance, Type storageType)
	{
		fsResult success = global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
		fsResult fsResult = (success += CheckType(data, fsDataType.Object));
		if (fsResult.Failed)
		{
			return success;
		}
		if (data.AsDictionary.ContainsKey("Target"))
		{
			fsData data2 = data.AsDictionary["Target"];
			object result = null;
			fsResult = (success += Serializer.TryDeserialize(data2, typeof(object), ref result));
			if (fsResult.Failed)
			{
				return success;
			}
			bool trackResurrection = false;
			if (data.AsDictionary.ContainsKey("TrackResurrection") && data.AsDictionary["TrackResurrection"].IsBool)
			{
				trackResurrection = data.AsDictionary["TrackResurrection"].AsBool;
			}
			instance = new WeakReference(result, trackResurrection);
		}
		return success;
	}

	public override object CreateInstance(fsData data, Type storageType)
	{
		return new WeakReference(null);
	}

	public fsWeakReferenceConverter()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private fsWeakReferenceConverter(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
