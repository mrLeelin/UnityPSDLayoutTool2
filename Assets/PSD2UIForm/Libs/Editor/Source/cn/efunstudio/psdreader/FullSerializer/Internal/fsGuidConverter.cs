using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer.Internal
{

public class fsGuidConverter : fsConverter
{
	public override bool CanProcess(Type type)
	{
		return type == typeof(Guid);
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
		serialized = new fsData(((Guid)instance/*cast due to .constrained prefix*/).ToString());
		return fsResult.Success;
	}

	public override fsResult TryDeserialize(fsData data, ref object instance, Type storageType)
	{
		if (!data.IsString)
		{
			return fsResult.Fail("fsGuidConverter encountered an unknown JSON data type");
		}
		instance = new Guid(data.AsString);
		return fsResult.Success;
	}

	public override object CreateInstance(fsData data, Type storageType)
	{
		return default(Guid);
	}

	public fsGuidConverter()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private fsGuidConverter(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
