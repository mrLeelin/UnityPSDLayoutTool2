using System;
using System.Collections.Generic;
using System.Reflection;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer.Internal
{

public class fsKeyValuePairConverter : fsConverter
{
	public override bool CanProcess(Type type)
	{
		if (type.Resolve().IsGenericType)
		{
			return type.GetGenericTypeDefinition() == typeof(KeyValuePair<, >);
		}
		return false;
	}

	public override bool RequestCycleSupport(Type storageType)
	{
		return false;
	}

	public override bool RequestInheritanceSupport(Type storageType)
	{
		return false;
	}

	public override fsResult TryDeserialize(fsData data, ref object instance, Type storageType)
	{
		fsResult success = global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
		fsData subitem;
		fsResult fsResult = (success += CheckKey(data, "Key", out subitem));
		if (fsResult.Failed)
		{
			return success;
		}
		if ((success += CheckKey(data, "Value", out var subitem2)).Failed)
		{
			return success;
		}
		Type[] genericArguments = storageType.GetGenericArguments();
		Type storageType2 = genericArguments[0];
		Type storageType3 = genericArguments[1];
		object result = null;
		object result2 = null;
		success.AddMessages(Serializer.TryDeserialize(subitem, storageType2, ref result));
		success.AddMessages(Serializer.TryDeserialize(subitem2, storageType3, ref result2));
		instance = Activator.CreateInstance(storageType, result, result2);
		return success;
	}

	public override fsResult TrySerialize(object instance, out fsData serialized, Type storageType)
	{
		PropertyInfo declaredProperty = storageType.GetDeclaredProperty("Key");
		PropertyInfo declaredProperty2 = storageType.GetDeclaredProperty("Value");
		object value = declaredProperty.GetValue(instance, null);
		object value2 = declaredProperty2.GetValue(instance, null);
		Type[] genericArguments = storageType.GetGenericArguments();
		Type storageType2 = genericArguments[0];
		Type storageType3 = genericArguments[1];
		fsResult success = global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
		success.AddMessages(Serializer.TrySerialize(storageType2, value, out var data));
		success.AddMessages(Serializer.TrySerialize(storageType3, value2, out var data2));
		serialized = fsData.CreateDictionary();
		if (data != null)
		{
			serialized.AsDictionary["Key"] = data;
		}
		if (data2 != null)
		{
			serialized.AsDictionary["Value"] = data2;
		}
		return success;
	}

	public fsKeyValuePairConverter()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private fsKeyValuePairConverter(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
