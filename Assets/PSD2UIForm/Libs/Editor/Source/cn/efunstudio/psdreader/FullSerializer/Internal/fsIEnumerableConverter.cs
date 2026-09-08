using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer.Internal
{

public class fsIEnumerableConverter : fsConverter
{
	public override bool CanProcess(Type type)
	{
		if (!typeof(IEnumerable).IsAssignableFrom(type))
		{
			return false;
		}
		return GetAddMethod(type) != null;
	}

	public override object CreateInstance(fsData data, Type storageType)
	{
		return fsMetaType.Get(Serializer.Config, storageType).CreateInstance();
	}

	public override fsResult TrySerialize(object instance_, out fsData serialized, Type storageType)
	{
		IEnumerable enumerable = (IEnumerable)instance_;
		fsResult success = global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
		Type elementType = GetElementType(storageType);
		serialized = fsData.CreateList(HintSize(enumerable));
		List<fsData> asList = serialized.AsList;
		foreach (object item in enumerable)
		{
			fsData data;
			fsResult result = Serializer.TrySerialize(elementType, item, out data);
			success.AddMessages(result);
			if (!result.Failed)
			{
				asList.Add(data);
			}
		}
		if (IsStack(enumerable.GetType()))
		{
			asList.Reverse();
		}
		return success;
	}

	private bool IsStack(Type type)
	{
		if (!type.Resolve().IsGenericType)
		{
			return false;
		}
		return type.Resolve().GetGenericTypeDefinition() == typeof(Stack<>);
	}

	public override fsResult TryDeserialize(fsData data, ref object instance_, Type storageType)
	{
		IEnumerable enumerable = (IEnumerable)instance_;
		fsResult success = global::cn.efunstudio.psdreader.FullSerializer.fsResult.Success;
		fsResult fsResult = (success += CheckType(data, fsDataType.Array));
		if (!fsResult.Failed)
		{
			Type elementType = GetElementType(storageType);
			MethodInfo addMethod = GetAddMethod(storageType);
			MethodInfo flattenedMethod = storageType.GetFlattenedMethod("get_Item");
			MethodInfo flattenedMethod2 = storageType.GetFlattenedMethod("set_Item");
			if (flattenedMethod2 == null)
			{
				TryClear(storageType, enumerable);
			}
			int num = TryGetExistingSize(storageType, enumerable);
			List<fsData> asList = data.AsList;
			for (int i = 0; i < asList.Count; i++)
			{
				fsData data2 = asList[i];
				object result = null;
				if (flattenedMethod != null && i < num)
				{
					result = flattenedMethod.Invoke(enumerable, new object[1] { i });
				}
				fsResult result2 = Serializer.TryDeserialize(data2, elementType, ref result);
				success.AddMessages(result2);
				if (!result2.Failed)
				{
					if (flattenedMethod2 != null && i < num)
					{
						flattenedMethod2.Invoke(enumerable, new object[2] { i, result });
					}
					else
					{
						addMethod.Invoke(enumerable, new object[1] { result });
					}
				}
			}
			return success;
		}
		return success;
	}

	private static int HintSize(IEnumerable collection)
	{
		if (collection is ICollection)
		{
			return ((ICollection)collection).Count;
		}
		return 0;
	}

	private static Type GetElementType(Type objectType)
	{
		if (!objectType.HasElementType)
		{
			Type type = fsReflectionUtility.GetInterface(objectType, typeof(IEnumerable<>));
			if (type != null)
			{
				return type.GetGenericArguments()[0];
			}
			return typeof(object);
		}
		return objectType.GetElementType();
	}

	private static void TryClear(Type type, object instance)
	{
		MethodInfo flattenedMethod = type.GetFlattenedMethod("Clear");
		if (flattenedMethod != null)
		{
			flattenedMethod.Invoke(instance, null);
		}
	}

	private static int TryGetExistingSize(Type type, object instance)
	{
		PropertyInfo flattenedProperty = type.GetFlattenedProperty("Count");
		if (flattenedProperty != null)
		{
			return (int)flattenedProperty.GetGetMethod().Invoke(instance, null);
		}
		return 0;
	}

	private static MethodInfo GetAddMethod(Type type)
	{
		Type type2 = fsReflectionUtility.GetInterface(type, typeof(ICollection<>));
		if (type2 != null)
		{
			MethodInfo declaredMethod = type2.GetDeclaredMethod("Add");
			if (declaredMethod != null)
			{
				return declaredMethod;
			}
		}
		return type.GetFlattenedMethod("Add") ?? type.GetFlattenedMethod("Push") ?? type.GetFlattenedMethod("Enqueue");
	}

	public fsIEnumerableConverter()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private fsIEnumerableConverter(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
