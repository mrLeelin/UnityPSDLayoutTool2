using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using PsdProtectionGuards;
using PsdResources;
using PsdReaderMetadata;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdReaderMetadata
{

internal static class PsdTaggedBlockFactory
{
	private static readonly Dictionary<string, Type> _readerTypesBySignature;

	static PsdTaggedBlockFactory()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		IEnumerable<Type> enumerable = from item in typeof(PsdResourceReader).Assembly.GetTypes()
			where typeof(PsdResourceReader).IsAssignableFrom(item) && (item.Attributes & TypeAttributes.Abstract) != TypeAttributes.Abstract
			select item;
		_readerTypesBySignature = new Dictionary<string, Type>(enumerable.Count());
		foreach (Type item in enumerable)
		{
			object[] customAttributes = item.GetCustomAttributes(typeof(PsdBlockSignatureAttribute), inherit: true);
			if (customAttributes.Length != 0)
			{
				PsdBlockSignatureAttribute PsdBlockSignatureAttribute = customAttributes.First() as PsdBlockSignatureAttribute;
				_readerTypesBySignature.Add(PsdBlockSignatureAttribute.ID, item);
			}
		}
	}

	public static PsdResourceReader CreateTaggedBlockReader(object P_0, object P_1, long P_2)
	{
		Type objectType = typeof(UnknownPsdResourceReader);
		if (_readerTypesBySignature.ContainsKey((string)P_0))
		{
			objectType = _readerTypesBySignature[(string)P_0];
		}
		return TypeDescriptor.CreateInstance(null, objectType, new Type[2]
		{
			typeof(PsdBigEndianReader),
			typeof(long)
		}, new object[2] { P_1, P_2 }) as PsdResourceReader;
	}

	public static string GetReaderDisplayName(Type P_0)
	{
		return (P_0.GetCustomAttributes(typeof(PsdBlockSignatureAttribute), inherit: true).First() as PsdBlockSignatureAttribute).DisplayName;
	}

	public static string GetSignatureDisplayName(object P_0)
	{
		if (_readerTypesBySignature.ContainsKey((string)P_0))
		{
			return GetReaderDisplayName(_readerTypesBySignature[(string)P_0]);
		}
		return (string)P_0;
	}
}
}
