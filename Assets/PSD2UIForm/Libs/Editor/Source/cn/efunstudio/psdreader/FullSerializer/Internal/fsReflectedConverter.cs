using System;
using System.Collections;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer.Internal
{

public class fsReflectedConverter : fsConverter
{
	public override bool CanProcess(Type type)
	{
		if (!type.Resolve().IsArray && !typeof(ICollection).IsAssignableFrom(type))
		{
			return true;
		}
		return false;
	}

	public override fsResult TrySerialize(object instance, out fsData serialized, Type storageType)
	{
		serialized = fsData.CreateDictionary();
		fsResult success = global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
		fsMetaType fsMetaType = fsMetaType.Get(Serializer.Config, instance.GetType());
		fsMetaType.EmitAotData(throwException: false);
		for (int i = 0; i < fsMetaType.Properties.Length; i++)
		{
			fsMetaProperty fsMetaProperty2 = fsMetaType.Properties[i];
			if (fsMetaProperty2.CanRead)
			{
				fsData data;
				fsResult result = Serializer.TrySerialize(fsMetaProperty2.StorageType, fsMetaProperty2.OverrideConverterType, fsMetaProperty2.Read(instance), out data);
				success.AddMessages(result);
				if (!result.Failed)
				{
					serialized.AsDictionary[fsMetaProperty2.JsonName] = data;
				}
			}
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
		fsMetaType fsMetaType = fsMetaType.Get(Serializer.Config, storageType);
		fsMetaType.EmitAotData(throwException: false);
		for (int i = 0; i < fsMetaType.Properties.Length; i++)
		{
			fsMetaProperty fsMetaProperty2 = fsMetaType.Properties[i];
			if (fsMetaProperty2.CanWrite && data.AsDictionary.TryGetValue(fsMetaProperty2.JsonName, out var value))
			{
				object result = null;
				if (fsMetaProperty2.CanRead)
				{
					result = fsMetaProperty2.Read(instance);
				}
				fsResult result2 = Serializer.TryDeserialize(value, fsMetaProperty2.StorageType, fsMetaProperty2.OverrideConverterType, ref result);
				success.AddMessages(result2);
				if (!result2.Failed)
				{
					fsMetaProperty2.Write(instance, result);
				}
			}
		}
		return success;
	}

	public override object CreateInstance(fsData data, Type storageType)
	{
		return fsMetaType.Get(Serializer.Config, storageType).CreateInstance();
	}

	public fsReflectedConverter()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private fsReflectedConverter(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
