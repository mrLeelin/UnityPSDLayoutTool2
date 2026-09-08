using System;
using System.Collections.Generic;
using System.Reflection;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer.Internal
{

public static class fsVersionManager
{
	private static readonly Dictionary<Type, fsOption<fsVersionedType>> _cache;

	public static fsResult GetVersionImportPath(string currentVersion, fsVersionedType targetVersion, out List<fsVersionedType> path)
	{
		path = new List<fsVersionedType>();
		if (!GetVersionImportPathRecursive(path, currentVersion, targetVersion))
		{
			return fsResult.Fail("There is no migration path from \"" + currentVersion + "\" to \"" + targetVersion.VersionString + "\"");
		}
		path.Add(targetVersion);
		return fsResult.Success;
	}

	private static bool GetVersionImportPathRecursive(List<fsVersionedType> path, string currentVersion, fsVersionedType current)
	{
		int num = 0;
		fsVersionedType fsVersionedType2;
		while (true)
		{
			if (num < current.Ancestors.Length)
			{
				fsVersionedType2 = current.Ancestors[num];
				if (fsVersionedType2.VersionString == currentVersion || GetVersionImportPathRecursive(path, currentVersion, fsVersionedType2))
				{
					break;
				}
				num++;
				continue;
			}
			return false;
		}
		path.Add(fsVersionedType2);
		return true;
	}

	public static fsOption<fsVersionedType> GetVersionedType(Type type)
	{
		if (!_cache.TryGetValue(type, out var value))
		{
			fsObjectAttribute attribute = fsPortableReflection.GetAttribute<fsObjectAttribute>(type);
			if (attribute != null && (!string.IsNullOrEmpty(attribute.VersionString) || attribute.PreviousModels != null))
			{
				if (attribute.PreviousModels != null && string.IsNullOrEmpty(attribute.VersionString))
				{
					throw new Exception("fsObject attribute on " + type?.ToString() + " contains a PreviousModels specifier - it must also include a VersionString modifier");
				}
				fsVersionedType[] array = new fsVersionedType[(attribute.PreviousModels != null) ? attribute.PreviousModels.Length : 0];
				for (int i = 0; i < array.Length; i++)
				{
					fsOption<fsVersionedType> versionedType = GetVersionedType(attribute.PreviousModels[i]);
					if (!versionedType.IsEmpty)
					{
						array[i] = versionedType.Value;
						continue;
					}
					throw new Exception("Unable to create versioned type for ancestor " + versionedType.ToString() + "; please add an [fsObject(VersionString=\"...\")] attribute");
				}
				fsVersionedType obj = new fsVersionedType
				{
					Ancestors = array,
					VersionString = attribute.VersionString,
					ModelType = type
				};
				VerifyUniqueVersionStrings(obj);
				VerifyConstructors(obj);
				value = fsOption.Just(obj);
			}
			_cache[type] = value;
		}
		return value;
	}

	private static void VerifyConstructors(fsVersionedType type)
	{
		ConstructorInfo[] declaredConstructors = type.ModelType.GetDeclaredConstructors();
		int num = 0;
		Type modelType;
		while (true)
		{
			if (num >= type.Ancestors.Length)
			{
				return;
			}
			modelType = type.Ancestors[num].ModelType;
			bool flag = false;
			for (int i = 0; i < declaredConstructors.Length; i++)
			{
				ParameterInfo[] parameters = declaredConstructors[i].GetParameters();
				if (parameters.Length == 1 && parameters[0].ParameterType == modelType)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				break;
			}
			num++;
		}
		throw new fsMissingVersionConstructorException(type.ModelType, modelType);
	}

	private static void VerifyUniqueVersionStrings(fsVersionedType type)
	{
		Dictionary<string, Type> dictionary = new Dictionary<string, Type>();
		Queue<fsVersionedType> queue = new Queue<fsVersionedType>();
		queue.Enqueue(type);
		while (queue.Count > 0)
		{
			fsVersionedType fsVersionedType2 = queue.Dequeue();
			if (!dictionary.ContainsKey(fsVersionedType2.VersionString) || !(dictionary[fsVersionedType2.VersionString] != fsVersionedType2.ModelType))
			{
				dictionary[fsVersionedType2.VersionString] = fsVersionedType2.ModelType;
				fsVersionedType[] ancestors = fsVersionedType2.Ancestors;
				foreach (fsVersionedType item in ancestors)
				{
					queue.Enqueue(item);
				}
				continue;
			}
			throw new fsDuplicateVersionNameException(dictionary[fsVersionedType2.VersionString], fsVersionedType2.ModelType, fsVersionedType2.VersionString);
		}
	}

	static fsVersionManager()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_cache = new Dictionary<Type, fsOption<fsVersionedType>>();
	}
}
}
