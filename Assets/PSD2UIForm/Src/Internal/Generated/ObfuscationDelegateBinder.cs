using System;
using System.Reflection;

namespace ObfuscationDelegateBinderNamespace
{
    internal class ObfuscationDelegateBinder
    {
        internal delegate void DelegateBindingAction(object target);

        internal static Module s_TargetModule = typeof(ObfuscationDelegateBinder).Assembly.ManifestModule;

        private static ObfuscationDelegateBinder s_ObfuscationSentinel;

        internal static void BindDelegatesForTypeToken(int typeDefinitionTokenOffset)
        {
            int num2 = default(int);
            MethodInfo method = default(MethodInfo);
            FieldInfo fieldInfo = default(FieldInfo);
            while (true)
            {
                Type type = s_TargetModule.ResolveType(33554432 + typeDefinitionTokenOffset);
                while (true)
                {
                    FieldInfo[] fields = type.GetFields();
                    int num = 3;
                    if (GetObfuscationSentinel() == null)
                    {
                        goto IL_006f;
                    }
                    goto IL_0081;
                    IL_0081:
                    switch (num)
                    {
                    case 9:
                        break;
                    case 7:
                        goto IL_0031;
                    case 1:
                        goto IL_0054;
                    case 6:
                        goto IL_005c;
                    default:
                        goto IL_0065;
                    case 3:
                        goto IL_006f;
                    case 4:
                        continue;
                    case 5:
                        goto end_IL_00b0;
                    case 8:
                        return;
                    }
                    goto IL_0006;
                    IL_006f:
                    num2 = 0;
                    num = 0;
                    if (!IsObfuscationSentinelNull())
                    {
                        goto IL_0065;
                    }
                    goto IL_0081;
                    IL_0006:
                    method = (MethodInfo)s_TargetModule.ResolveMethod(fieldInfo.MetadataToken + 100663296);
                    num = 5;
                    if (GetObfuscationSentinel() == null)
                    {
                        goto IL_0031;
                    }
                    goto IL_0081;
                    IL_0031:
                    fieldInfo.SetValue(null, (MulticastDelegate)Delegate.CreateDelegate(type, method));
                    num = 0;
                    if (GetObfuscationSentinel() == null)
                    {
                        goto IL_0054;
                    }
                    goto IL_0081;
                    IL_0054:
                    num2++;
                    goto IL_0065;
                    IL_0065:
                    if (num2 >= fields.Length)
                    {
                        return;
                    }
                    goto IL_005c;
                    IL_005c:
                    fieldInfo = fields[num2];
                    goto IL_0006;
                    continue;
                    end_IL_00b0:
                    break;
                }
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static ObfuscationDelegateBinder GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
