using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer.Internal
{

public class fsNullableConverter : fsConverter
{
	public override bool CanProcess(Type type)
	{
		if (type.Resolve().IsGenericType)
		{
			return type.GetGenericTypeDefinition() == typeof(Nullable<>);
		}
		return false;
	}

	public override fsResult TrySerialize(object instance, out fsData serialized, Type storageType)
	{
		return Serializer.TrySerialize(Nullable.GetUnderlyingType(storageType), instance, out serialized);
	}

	public override fsResult TryDeserialize(fsData data, ref object instance, Type storageType)
	{
		return Serializer.TryDeserialize(data, Nullable.GetUnderlyingType(storageType), ref instance);
	}

	public override object CreateInstance(fsData data, Type storageType)
	{
		return storageType;
	}

	public fsNullableConverter()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private fsNullableConverter(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
