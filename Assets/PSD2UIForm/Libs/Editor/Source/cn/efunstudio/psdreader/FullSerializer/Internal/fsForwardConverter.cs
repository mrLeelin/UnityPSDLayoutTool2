using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer.Internal
{

public class fsForwardConverter : fsConverter
{
	private string _memberName;

	public fsForwardConverter(fsForwardAttribute attribute)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), attribute)
	{
	}

	private fsForwardConverter(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, fsForwardAttribute attribute)
		: base()
	{
		_memberName = attribute.MemberName;
	}

	public override bool CanProcess(Type type)
	{
		throw new NotSupportedException("Please use the [fsForward(...)] attribute.");
	}

	private fsResult GetProperty(object instance, out fsMetaProperty property)
	{
		fsMetaProperty[] properties = fsMetaType.Get(Serializer.Config, instance.GetType()).Properties;
		int num = 0;
		while (true)
		{
			if (num < properties.Length)
			{
				if (properties[num].MemberName == _memberName)
				{
					break;
				}
				num++;
				continue;
			}
			property = null;
			return fsResult.Fail("No property named \"" + _memberName + "\" on " + instance.GetType().CSharpName());
		}
		property = properties[num];
		return global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
	}

	public override fsResult TrySerialize(object instance, out fsData serialized, Type storageType)
	{
		serialized = fsData.Null;
		fsResult success = global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
		fsMetaProperty property;
		fsResult fsResult = (success += GetProperty(instance, out property));
		if (!fsResult.Failed)
		{
			object instance2 = property.Read(instance);
			return Serializer.TrySerialize(property.StorageType, instance2, out serialized);
		}
		return success;
	}

	public override fsResult TryDeserialize(fsData data, ref object instance, Type storageType)
	{
		fsResult success = global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
		fsResult fsResult = (success += GetProperty(instance, out var property));
		if (!fsResult.Failed)
		{
			object result = null;
			fsResult = (success += Serializer.TryDeserialize(data, property.StorageType, ref result));
			if (!fsResult.Failed)
			{
				property.Write(instance, result);
				return success;
			}
			return success;
		}
		return success;
	}

	public override object CreateInstance(fsData data, Type storageType)
	{
		return fsMetaType.Get(Serializer.Config, storageType).CreateInstance();
	}
}
}
