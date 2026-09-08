using System;
using System.Collections;
using System.Collections.Generic;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer.Internal
{

public class fsDictionaryConverter : fsConverter
{
	public override bool CanProcess(Type type)
	{
		return typeof(IDictionary).IsAssignableFrom(type);
	}

	public override object CreateInstance(fsData data, Type storageType)
	{
		return fsMetaType.Get(Serializer.Config, storageType).CreateInstance();
	}

	public override fsResult TryDeserialize(fsData data, ref object instance_, Type storageType)
	{
		IDictionary dictionary = (IDictionary)instance_;
		fsResult success = global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
		GetKeyValueTypes(dictionary.GetType(), out var keyStorageType, out var valueStorageType);
		fsResult result3;
		if (!data.IsList)
		{
			if (!data.IsDictionary)
			{
				return FailExpectedType(data, fsDataType.Array, fsDataType.Object);
			}
			foreach (KeyValuePair<string, fsData> item in data.AsDictionary)
			{
				if (fsSerializer.IsReservedKeyword(item.Key))
				{
					continue;
				}
				fsData data2 = new fsData(item.Key);
				fsData value = item.Value;
				object result = null;
				object result2 = null;
				result3 = (success += Serializer.TryDeserialize(data2, keyStorageType, ref result));
				if (!result3.Failed)
				{
					fsResult fsResult = (success += Serializer.TryDeserialize(value, valueStorageType, ref result2));
					if (!fsResult.Failed)
					{
						AddItemToDictionary(dictionary, result, result2);
						continue;
					}
					result3 = success;
				}
				else
				{
					result3 = success;
				}
				goto IL_0101;
			}
		}
		else
		{
			List<fsData> asList = data.AsList;
			for (int i = 0; i < asList.Count; i++)
			{
				fsData data3 = asList[i];
				if (!(success += CheckType(data3, fsDataType.Object)).Failed)
				{
					if (!(success += CheckKey(data3, "Key", out var subitem)).Failed)
					{
						if (!(success += CheckKey(data3, "Value", out var subitem2)).Failed)
						{
							object result4 = null;
							object result5 = null;
							if (!(success += Serializer.TryDeserialize(subitem, keyStorageType, ref result4)).Failed)
							{
								if (!(success += Serializer.TryDeserialize(subitem2, valueStorageType, ref result5)).Failed)
								{
									AddItemToDictionary(dictionary, result4, result5);
									continue;
								}
								return success;
							}
							return success;
						}
						return success;
					}
					return success;
				}
				return success;
			}
		}
		return success;
		IL_0101:
		return result3;
	}

	public override fsResult TrySerialize(object instance_, out fsData serialized, Type storageType)
	{
		serialized = fsData.Null;
		fsResult success = global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
		IDictionary obj = (IDictionary)instance_;
		GetKeyValueTypes(obj.GetType(), out var keyStorageType, out var valueStorageType);
		IDictionaryEnumerator enumerator = obj.GetEnumerator();
		bool flag = true;
		List<fsData> list = new List<fsData>(obj.Count);
		List<fsData> list2 = new List<fsData>(obj.Count);
		while (enumerator.MoveNext())
		{
			if (!(success += Serializer.TrySerialize(keyStorageType, enumerator.Key, out var data)).Failed)
			{
				if (!(success += Serializer.TrySerialize(valueStorageType, enumerator.Value, out var data2)).Failed)
				{
					list.Add(data);
					list2.Add(data2);
					flag &= data.IsString;
					continue;
				}
				return success;
			}
			return success;
		}
		if (flag)
		{
			serialized = fsData.CreateDictionary();
			Dictionary<string, fsData> asDictionary = serialized.AsDictionary;
			for (int i = 0; i < list.Count; i++)
			{
				fsData fsData = list[i];
				fsData value = list2[i];
				asDictionary[fsData.AsString] = value;
			}
		}
		else
		{
			serialized = fsData.CreateList(list.Count);
			List<fsData> asList = serialized.AsList;
			for (int j = 0; j < list.Count; j++)
			{
				fsData value2 = list[j];
				fsData value3 = list2[j];
				Dictionary<string, fsData> dictionary = new Dictionary<string, fsData>();
				dictionary["Key"] = value2;
				dictionary["Value"] = value3;
				asList.Add(new fsData(dictionary));
			}
		}
		return success;
	}

	private fsResult AddItemToDictionary(IDictionary dictionary, object key, object value)
	{
		if (key != null && value != null)
		{
			dictionary[key] = value;
			return global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
		}
		Type type = fsReflectionUtility.GetInterface(dictionary.GetType(), typeof(ICollection<>));
		if (type == null)
		{
			return fsResult.Warn(dictionary.GetType()?.ToString() + " does not extend ICollection");
		}
		object obj = Activator.CreateInstance(type.GetGenericArguments()[0], key, value);
		type.GetFlattenedMethod("Add").Invoke(dictionary, new object[1] { obj });
		return global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
	}

	private static void GetKeyValueTypes(Type dictionaryType, out Type keyStorageType, out Type valueStorageType)
	{
		Type type = fsReflectionUtility.GetInterface(dictionaryType, typeof(IDictionary<, >));
		if (!(type != null))
		{
			keyStorageType = typeof(object);
			valueStorageType = typeof(object);
		}
		else
		{
			Type[] genericArguments = type.GetGenericArguments();
			keyStorageType = genericArguments[0];
			valueStorageType = genericArguments[1];
		}
	}

	public fsDictionaryConverter()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private fsDictionaryConverter(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
