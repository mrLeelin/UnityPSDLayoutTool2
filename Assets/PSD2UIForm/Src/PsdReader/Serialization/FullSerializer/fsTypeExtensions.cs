using System;
using System.Collections.Generic;
using System.Linq;

namespace cn.efunstudio.psdreader.FullSerializer
{
    public static class fsTypeExtensions
    {
public static string CSharpName(this Type type)
        {
            return type.CSharpName(includeNamespace: false);
        }

        public static string CSharpName(this Type type, bool includeNamespace, bool ensureSafeDeclarationName)
        {
            string text = type.CSharpName(includeNamespace);
            if (ensureSafeDeclarationName)
            {
                text = text.Replace('>', '_').Replace('<', '_').Replace('.', '_')
                    .Replace(',', '_');
            }
            return text;
        }

        public static string CSharpName(this Type type, bool includeNamespace)
        {
            if (!(type == typeof(void)))
            {
                if (type == typeof(int))
                {
                    return "int";
                }
                if (type == typeof(float))
                {
                    return "float";
                }
                if (!(type == typeof(bool)))
                {
                    if (!(type == typeof(double)))
                    {
                        if (!(type == typeof(string)))
                        {
                            if (!type.IsGenericParameter)
                            {
                                string text = "";
                                IEnumerable<Type> source = type.GetGenericArguments();
                                if (type.IsNested)
                                {
                                    text = text + type.DeclaringType.CSharpName() + ".";
                                    if (type.DeclaringType.GetGenericArguments().Length != 0)
                                    {
                                        source = source.Skip(type.DeclaringType.GetGenericArguments().Length);
                                    }
                                }
                                if (!source.Any())
                                {
                                    text += type.Name;
                                }
                                else
                                {
                                    int num = type.Name.IndexOf('`');
                                    if (num > 0)
                                    {
                                        text += type.Name.Substring(0, num);
                                    }
                                    text = text + "<" + string.Join(",", source.Select((Type t) => t.CSharpName(includeNamespace)).ToArray()) + ">";
                                }
                                if (includeNamespace && type.Namespace != null)
                                {
                                    text = type.Namespace + "." + text;
                                }
                                return text;
                            }
                            return type.ToString();
                        }
                        return "string";
                    }
                    return "double";
                }
                return "bool";
            }
            return "void";
        }

        public static bool IsInterface(this Type type)
        {
            return type.IsInterface;
        }

        public static bool IsAbstract(this Type type)
        {
            return type.IsAbstract;
        }

        public static bool IsGenericType(this Type type)
        {
            return type.IsGenericType;
        }

        internal static bool IsObfuscationSentinelValid()

        {

            return true;

        }
}
}
