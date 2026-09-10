using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using UnityEngine;
using Object = UnityEngine.Object;
using cn.efunstudio.psdreader.FullSerializer.Internal;

namespace cn.efunstudio.psdreader.FullSerializer
{
    public class fsMetaType
    {
        public class AotFailureException : Exception
        {
            internal static AotFailureException s_ObfuscationSentinel;

            public AotFailureException(string reason)
                : base(reason)
            {
            }

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static AotFailureException GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private static Dictionary<fsConfig, Dictionary<Type, fsMetaType>> _configMetaTypes = new Dictionary<fsConfig, Dictionary<Type, fsMetaType>>();

        public Type ReflectedType;

        private bool? _hasDefaultConstructorCache;

        private bool? _isDefaultConstructorPublicCache;

        internal static fsMetaType s_ObfuscationSentinel;

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
                    else if (!ReflectedType.Resolve().IsValueType)
                    {
                        ConstructorInfo declaredConstructor = ReflectedType.GetDeclaredConstructor(fsPortableReflection.EmptyTypes);
                        _hasDefaultConstructorCache = declaredConstructor != null;
                        if (declaredConstructor != null)
                        {
                            _isDefaultConstructorPublicCache = declaredConstructor.IsPublic;
                        }
                    }
                    else
                    {
                        _hasDefaultConstructorCache = true;
                        _isDefaultConstructorPublicCache = true;
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
            foreach (MemberInfo member in array)
            {
                if (config.IgnoreSerializeAttributes.Any((Type t) => fsPortableReflection.HasAttribute(member, t)))
                {
                    continue;
                }
                PropertyInfo propertyInfo = member as PropertyInfo;
                FieldInfo fieldInfo = member as FieldInfo;
                if ((propertyInfo == null && fieldInfo == null) || (propertyInfo != null && !config.EnablePropertySerialization) || (flag && !config.SerializeAttributes.Any((Type t) => fsPortableReflection.HasAttribute(member, t))) || (flag2 && config.IgnoreSerializeAttributes.Any((Type t) => fsPortableReflection.HasAttribute(member, t))))
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
            if (typeof(Delegate).IsAssignableFrom(property.PropertyType))
            {
                return false;
            }
            MethodInfo getMethod = property.GetGetMethod(nonPublic: false);
            MethodInfo setMethod = property.GetSetMethod(nonPublic: false);
            if ((!(getMethod != null) || !getMethod.IsStatic) && (!(setMethod != null) || !setMethod.IsStatic))
            {
                if (property.GetIndexParameters().Length != 0)
                {
                    return false;
                }
                if (config.IgnoreSerializeTypeAttributes.Any((Type t) => fsPortableReflection.HasAttribute(property.PropertyType, t)))
                {
                    return false;
                }
                if (config.SerializeAttributes.Any((Type t) => fsPortableReflection.HasAttribute(property, t)))
                {
                    return true;
                }
                if (property.CanRead && property.CanWrite)
                {
                    if (getMethod != null && (config.SerializeNonPublicSetProperties || setMethod != null) && (config.SerializeNonAutoProperties || IsAutoProperty(property, members)))
                    {
                        return true;
                    }
                    return annotationFreeValue;
                }
                return false;
            }
            return false;
        }

        private static bool CanSerializeField(fsConfig config, FieldInfo field, bool annotationFreeValue)
        {
            if (typeof(Delegate).IsAssignableFrom(field.FieldType))
            {
                return false;
            }
            if (!field.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            {
                if (!field.IsStatic)
                {
                    if (config.IgnoreSerializeTypeAttributes.Any((Type t) => fsPortableReflection.HasAttribute(field.FieldType, t)))
                    {
                        return false;
                    }
                    if (!config.SerializeAttributes.Any((Type t) => fsPortableReflection.HasAttribute(field, t)))
                    {
                        if (!annotationFreeValue && !field.IsPublic)
                        {
                            return false;
                        }
                        return true;
                    }
                    return true;
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
            int num = 0;
            while (true)
            {
                if (num < Properties.Length)
                {
                    if (Properties[num].IsPublic)
                    {
                        if (Properties[num].IsReadOnly)
                        {
                            break;
                        }
                        num++;
                        continue;
                    }
                    throw new AotFailureException(ReflectedType.CSharpName(includeNamespace: true) + "::" + Properties[num].MemberName + " is not public");
                }
                if (HasDefaultConstructor)
                {
                    return;
                }
                throw new AotFailureException(ReflectedType.CSharpName(includeNamespace: true) + " does not have a default constructor");
            }
            throw new AotFailureException(ReflectedType.CSharpName(includeNamespace: true) + "::" + Properties[num].MemberName + " is readonly");
        }

        public object CreateInstance()
        {
            if (!ReflectedType.Resolve().IsInterface && !ReflectedType.Resolve().IsAbstract)
            {
                if (typeof(ScriptableObject).IsAssignableFrom(ReflectedType))
                {
                    return ScriptableObject.CreateInstance(ReflectedType);
                }
                if (!(typeof(string) == ReflectedType))
                {
                    if (HasDefaultConstructor)
                    {
                        if (ReflectedType.Resolve().IsArray)
                        {
                            return Array.CreateInstance(ReflectedType.GetElementType(), 0);
                        }
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
                    return FormatterServices.GetSafeUninitializedObject(ReflectedType);
                }
                return string.Empty;
            }
            throw new Exception("Cannot create an instance of an interface or abstract type for " + ReflectedType);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static fsMetaType GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
