using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using PsdProtectionGuards;
using UnityEngine;
using cn.efunstudio.psdreader.FullSerializer.Internal;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer
{

public class fsMetaType
{
	public class AotFailureException : Exception
	{
		public AotFailureException(string reason)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), reason)
		{
		}

		private AotFailureException(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, string reason)
			: base(reason)
		{
		}
	}

	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass5_0
	{
		public MemberInfo member;

		public _003C_003Ec__DisplayClass5_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal bool _003CCollectProperties_003Eb__0(Type t)
		{
			return fsPortableReflection.HasAttribute(member, t);
		}

		internal bool _003CCollectProperties_003Eb__1(Type t)
		{
			return fsPortableReflection.HasAttribute(member, t);
		}

		internal bool _003CCollectProperties_003Eb__2(Type t)
		{
			return fsPortableReflection.HasAttribute(member, t);
		}
	}

	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass7_0
	{
		public PropertyInfo property;

		public _003C_003Ec__DisplayClass7_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal bool _003CCanSerializeProperty_003Eb__0(Type t)
		{
			return fsPortableReflection.HasAttribute(property.PropertyType, t);
		}

		internal bool _003CCanSerializeProperty_003Eb__1(Type t)
		{
			return fsPortableReflection.HasAttribute(property, t);
		}
	}

	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass8_0
	{
		public FieldInfo field;

		public _003C_003Ec__DisplayClass8_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal bool _003CCanSerializeField_003Eb__0(Type t)
		{
			return fsPortableReflection.HasAttribute(field.FieldType, t);
		}

		internal bool _003CCanSerializeField_003Eb__1(Type t)
		{
			return fsPortableReflection.HasAttribute(field, t);
		}
	}

	private static Dictionary<fsConfig, Dictionary<Type, fsMetaType>> _configMetaTypes;

	public Type ReflectedType;

	private bool? _hasDefaultConstructorCache;

	private bool? _isDefaultConstructorPublicCache;

	public fsMetaProperty[] Properties { get; private set; }

	public bool HasDefaultConstructor
	{
		get
		{
			if (!_hasDefaultConstructorCache.HasValue)
			{
				if (ReflectedType.Resolve().IsArray)
				{
					_hasDefaultConstructorCache = true;
					_isDefaultConstructorPublicCache = true;
				}
				else if (ReflectedType.Resolve().IsValueType)
				{
					_hasDefaultConstructorCache = true;
					_isDefaultConstructorPublicCache = true;
				}
				else
				{
					ConstructorInfo declaredConstructor = ReflectedType.GetDeclaredConstructor(fsPortableReflection.EmptyTypes);
					_hasDefaultConstructorCache = declaredConstructor != null;
					if (declaredConstructor != null)
					{
						_isDefaultConstructorPublicCache = declaredConstructor.IsPublic;
					}
				}
			}
			return _hasDefaultConstructorCache.Value;
		}
	}

	public bool IsDefaultConstructorPublic
	{
		get
		{
			if (!_isDefaultConstructorPublicCache.HasValue)
			{
				_ = HasDefaultConstructor;
			}
			return _isDefaultConstructorPublicCache.Value;
		}
	}

	public static fsMetaType Get(fsConfig config, Type type)
	{
		Dictionary<Type, fsMetaType> value;
		lock (typeof(fsMetaType))
		{
			if (!_configMetaTypes.TryGetValue(config, out value))
			{
				Dictionary<Type, fsMetaType> dictionary = (_configMetaTypes[config] = new Dictionary<Type, fsMetaType>());
				value = dictionary;
			}
		}
		if (!value.TryGetValue(type, out var value2))
		{
			value2 = (value[type] = new fsMetaType(config, type));
		}
		return value2;
	}

	public static void ClearCache()
	{
		lock (typeof(fsMetaType))
		{
			_configMetaTypes = new Dictionary<fsConfig, Dictionary<Type, fsMetaType>>();
		}
	}

	private fsMetaType(fsConfig config, Type reflectedType)
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		ReflectedType = reflectedType;
		List<fsMetaProperty> list = new List<fsMetaProperty>();
		CollectProperties(config, list, reflectedType);
		Properties = list.ToArray();
	}

	private static void CollectProperties(fsConfig config, List<fsMetaProperty> properties, Type reflectedType)
	{
		bool flag = config.DefaultMemberSerialization == fsMemberSerialization.OptIn;
		bool flag2 = config.DefaultMemberSerialization == fsMemberSerialization.OptOut;
		fsObjectAttribute attribute = fsPortableReflection.GetAttribute<fsObjectAttribute>(reflectedType);
		if (attribute != null)
		{
			flag = attribute.MemberSerialization == fsMemberSerialization.OptIn;
			flag2 = attribute.MemberSerialization == fsMemberSerialization.OptOut;
		}
		MemberInfo[] declaredMembers = reflectedType.GetDeclaredMembers();
		MemberInfo[] array = declaredMembers;
		for (int i = 0; i < array.Length; i++)
		{
			_003C_003Ec__DisplayClass5_0 CS_0024_003C_003E8__locals6 = new _003C_003Ec__DisplayClass5_0();
			CS_0024_003C_003E8__locals6.member = array[i];
			if (config.IgnoreSerializeAttributes.Any((Type t) => fsPortableReflection.HasAttribute(CS_0024_003C_003E8__locals6.member, t)))
			{
				continue;
			}
			PropertyInfo propertyInfo = CS_0024_003C_003E8__locals6.member as PropertyInfo;
			FieldInfo fieldInfo = CS_0024_003C_003E8__locals6.member as FieldInfo;
			if ((propertyInfo == null && fieldInfo == null) || (propertyInfo != null && !config.EnablePropertySerialization) || (flag && !config.SerializeAttributes.Any((Type t) => fsPortableReflection.HasAttribute(CS_0024_003C_003E8__locals6.member, t))) || (flag2 && config.IgnoreSerializeAttributes.Any((Type t) => fsPortableReflection.HasAttribute(CS_0024_003C_003E8__locals6.member, t))))
			{
				continue;
			}
			if (propertyInfo != null)
			{
				if (CanSerializeProperty(config, propertyInfo, declaredMembers, flag2))
				{
					properties.Add(new fsMetaProperty(config, propertyInfo));
				}
			}
			else if (fieldInfo != null && CanSerializeField(config, fieldInfo, flag2))
			{
				properties.Add(new fsMetaProperty(config, fieldInfo));
			}
		}
		if (reflectedType.Resolve().BaseType != null)
		{
			CollectProperties(config, properties, reflectedType.Resolve().BaseType);
		}
	}

	private static bool IsAutoProperty(PropertyInfo property, MemberInfo[] members)
	{
		if (property.CanWrite && property.CanRead)
		{
			return fsPortableReflection.HasAttribute(property.GetGetMethod(), typeof(CompilerGeneratedAttribute), shouldCache: false);
		}
		return false;
	}

	private static bool CanSerializeProperty(fsConfig config, PropertyInfo property, MemberInfo[] members, bool annotationFreeValue)
	{
		_003C_003Ec__DisplayClass7_0 CS_0024_003C_003E8__locals10 = new _003C_003Ec__DisplayClass7_0();
		CS_0024_003C_003E8__locals10.property = property;
		if (!typeof(Delegate).IsAssignableFrom(CS_0024_003C_003E8__locals10.property.PropertyType))
		{
			MethodInfo getMethod = CS_0024_003C_003E8__locals10.property.GetGetMethod(nonPublic: false);
			MethodInfo setMethod = CS_0024_003C_003E8__locals10.property.GetSetMethod(nonPublic: false);
			if ((getMethod != null && getMethod.IsStatic) || (setMethod != null && setMethod.IsStatic))
			{
				return false;
			}
			if (CS_0024_003C_003E8__locals10.property.GetIndexParameters().Length != 0)
			{
				return false;
			}
			if (config.IgnoreSerializeTypeAttributes.Any((Type t) => fsPortableReflection.HasAttribute(CS_0024_003C_003E8__locals10.property.PropertyType, t)))
			{
				return false;
			}
			if (!config.SerializeAttributes.Any((Type t) => fsPortableReflection.HasAttribute(CS_0024_003C_003E8__locals10.property, t)))
			{
				if (CS_0024_003C_003E8__locals10.property.CanRead && CS_0024_003C_003E8__locals10.property.CanWrite)
				{
					if (getMethod != null && (config.SerializeNonPublicSetProperties || setMethod != null) && (config.SerializeNonAutoProperties || IsAutoProperty(CS_0024_003C_003E8__locals10.property, members)))
					{
						return true;
					}
					return annotationFreeValue;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	private static bool CanSerializeField(fsConfig config, FieldInfo field, bool annotationFreeValue)
	{
		_003C_003Ec__DisplayClass8_0 CS_0024_003C_003E8__locals7 = new _003C_003Ec__DisplayClass8_0();
		CS_0024_003C_003E8__locals7.field = field;
		if (!typeof(Delegate).IsAssignableFrom(CS_0024_003C_003E8__locals7.field.FieldType))
		{
			if (!CS_0024_003C_003E8__locals7.field.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
			{
				if (CS_0024_003C_003E8__locals7.field.IsStatic)
				{
					return false;
				}
				if (!config.IgnoreSerializeTypeAttributes.Any((Type t) => fsPortableReflection.HasAttribute(CS_0024_003C_003E8__locals7.field.FieldType, t)))
				{
					if (config.SerializeAttributes.Any((Type t) => fsPortableReflection.HasAttribute(CS_0024_003C_003E8__locals7.field, t)))
					{
						return true;
					}
					if (!annotationFreeValue && !CS_0024_003C_003E8__locals7.field.IsPublic)
					{
						return false;
					}
					return true;
				}
				return false;
			}
			return false;
		}
		return false;
	}

	public void EmitAotData(bool throwException)
	{
		fsAotCompilationManager.AotCandidateTypes.Add(ReflectedType);
		if (!throwException)
		{
			return;
		}
		for (int i = 0; i < Properties.Length; i++)
		{
			if (Properties[i].IsPublic)
			{
				if (Properties[i].IsReadOnly)
				{
					throw new AotFailureException(ReflectedType.CSharpName(includeNamespace: true) + "::" + Properties[i].MemberName + " is readonly");
				}
				continue;
			}
			throw new AotFailureException(ReflectedType.CSharpName(includeNamespace: true) + "::" + Properties[i].MemberName + " is not public");
		}
		if (!HasDefaultConstructor)
		{
			throw new AotFailureException(ReflectedType.CSharpName(includeNamespace: true) + " does not have a default constructor");
		}
	}

	public object CreateInstance()
	{
		if (!ReflectedType.Resolve().IsInterface && !ReflectedType.Resolve().IsAbstract)
		{
			if (!typeof(ScriptableObject).IsAssignableFrom(ReflectedType))
			{
				if (!(typeof(string) == ReflectedType))
				{
					if (HasDefaultConstructor)
					{
						if (!ReflectedType.Resolve().IsArray)
						{
							try
							{
								return Activator.CreateInstance(ReflectedType, nonPublic: true);
							}
							catch (MissingMethodException innerException)
							{
								throw new InvalidOperationException("Unable to create instance of " + ReflectedType?.ToString() + "; there is no default constructor", innerException);
							}
							catch (TargetInvocationException innerException2)
							{
								throw new InvalidOperationException("Constructor of " + ReflectedType?.ToString() + " threw an exception when creating an instance", innerException2);
							}
							catch (MemberAccessException innerException3)
							{
								throw new InvalidOperationException("Unable to access constructor of " + ReflectedType, innerException3);
							}
						}
						return Array.CreateInstance(ReflectedType.GetElementType(), 0);
					}
					return FormatterServices.GetSafeUninitializedObject(ReflectedType);
				}
				return string.Empty;
			}
			return ScriptableObject.CreateInstance(ReflectedType);
		}
		throw new Exception("Cannot create an instance of an interface or abstract type for " + ReflectedType);
	}

	static fsMetaType()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_configMetaTypes = new Dictionary<fsConfig, Dictionary<Type, fsMetaType>>();
	}
}
}
