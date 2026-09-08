using System;
using System.Collections.Generic;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer
{

public abstract class fsDirectConverter : fsBaseConverter
{
	public abstract Type ModelType { get; }

	protected fsDirectConverter()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private fsDirectConverter(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
public abstract class fsDirectConverter<TModel> : fsDirectConverter
{
	internal static object M8DKERZkxqS0pBU3Fa8m;

	public override Type ModelType => typeof(TModel);

	public sealed override fsResult TrySerialize(object instance, out fsData serialized, Type storageType)
	{
		Dictionary<string, fsData> dictionary = new Dictionary<string, fsData>();
		fsResult result = DoSerialize((TModel)instance, dictionary);
		serialized = new fsData(dictionary);
		return result;
	}

	public sealed override fsResult TryDeserialize(fsData data, ref object instance, Type storageType)
	{
		fsResult success = global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
		fsResult fsResult2 = (success += CheckType(data, fsDataType.Object));
		if (fsResult2.Failed)
		{
			return success;
		}
		TModel model = (TModel)instance;
		success += DoDeserialize(data.AsDictionary, ref model);
		instance = model;
		return success;
	}

	protected abstract fsResult DoSerialize(TModel model, Dictionary<string, fsData> serialized);

	protected abstract fsResult DoDeserialize(Dictionary<string, fsData> data, ref TModel model);

	protected fsDirectConverter()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private fsDirectConverter(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}

	internal static bool IljrX5ZkIUFrUPFTJUro()
	{
		return M8DKERZkxqS0pBU3Fa8m == null;
	}

	internal static object ApS2LiZkGCUklCql03Ek()
	{
		return M8DKERZkxqS0pBU3Fa8m;
	}
}
}
