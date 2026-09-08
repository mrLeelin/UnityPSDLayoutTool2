using System;
using System.Globalization;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer.Internal
{

public class fsDateConverter : fsConverter
{
	private string DateTimeFormatString => Serializer.Config.CustomDateTimeFormatString ?? "o";

	public override bool CanProcess(Type type)
	{
		if (!(type == typeof(DateTime)) && !(type == typeof(DateTimeOffset)))
		{
			return type == typeof(TimeSpan);
		}
		return true;
	}

	public override fsResult TrySerialize(object instance, out fsData serialized, Type storageType)
	{
		if (!(instance is DateTime dateTime))
		{
			if (!(instance is DateTimeOffset dateTimeOffset))
			{
				if (!(instance is TimeSpan timeSpan))
				{
					throw new InvalidOperationException("FullSerializer Internal Error -- Unexpected serialization type");
				}
				serialized = new fsData(timeSpan.ToString());
				return fsResult.Success;
			}
			serialized = new fsData(dateTimeOffset.ToString("o"));
			return fsResult.Success;
		}
		serialized = new fsData(dateTime.ToString(DateTimeFormatString));
		return fsResult.Success;
	}

	public override fsResult TryDeserialize(fsData data, ref object instance, Type storageType)
	{
		if (!data.IsString)
		{
			return fsResult.Fail("Date deserialization requires a string, not " + data.Type);
		}
		if (storageType == typeof(DateTime))
		{
			if (DateTime.TryParse(data.AsString, null, DateTimeStyles.RoundtripKind, out var result))
			{
				instance = result;
				return fsResult.Success;
			}
			if (fsGlobalConfig.AllowInternalExceptions)
			{
				try
				{
					instance = Convert.ToDateTime(data.AsString);
					return fsResult.Success;
				}
				catch (Exception ex)
				{
					return fsResult.Fail("Unable to parse " + data.AsString + " into a DateTime; got exception " + ex);
				}
			}
			return fsResult.Fail("Unable to parse " + data.AsString + " into a DateTime");
		}
		if (storageType == typeof(DateTimeOffset))
		{
			if (!DateTimeOffset.TryParse(data.AsString, null, DateTimeStyles.RoundtripKind, out var result2))
			{
				return fsResult.Fail("Unable to parse " + data.AsString + " into a DateTimeOffset");
			}
			instance = result2;
			return fsResult.Success;
		}
		if (!(storageType == typeof(TimeSpan)))
		{
			throw new InvalidOperationException("FullSerializer Internal Error -- Unexpected deserialization type");
		}
		if (!TimeSpan.TryParse(data.AsString, out var result3))
		{
			return fsResult.Fail("Unable to parse " + data.AsString + " into a TimeSpan");
		}
		instance = result3;
		return fsResult.Success;
	}

	public fsDateConverter()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private fsDateConverter(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
