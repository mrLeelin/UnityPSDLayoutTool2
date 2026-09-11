using System;
using System.Collections.Generic;
using System.Text;
using cn.efunstudio.psdreader.FullSerializer.Internal;

namespace cn.efunstudio.psdreader.FullSerializer
{
    public class fsAotCompilationManager
    {
        public static HashSet<Type> AotCandidateTypes = new HashSet<Type>();

        private static fsAotCompilationManager s_ObfuscationSentinel;

        private static bool HasMember(fsAotVersionInfo versionInfo, fsAotVersionInfo.Member member)
        {
            fsAotVersionInfo.Member[] members = versionInfo.Members;
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i] == member)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsAotModelUpToDate(fsMetaType currentModel, fsIAotConverter aotModel)
        {
            if (currentModel.IsDefaultConstructorPublic != aotModel.VersionInfo.IsConstructorPublic)
            {
                return false;
            }
            if (currentModel.Properties.Length != aotModel.VersionInfo.Members.Length)
            {
                return false;
            }
            fsMetaProperty[] properties = currentModel.Properties;
            foreach (fsMetaProperty property in properties)
            {
                if (!HasMember(aotModel.VersionInfo, new fsAotVersionInfo.Member(property)))
                {
                    return false;
                }
            }
            return true;
        }

        public static string RunAotCompilationForType(fsConfig config, Type type)
        {
            fsMetaType fsMetaType2 = fsMetaType.Get(config, type);
            fsMetaType2.EmitAotData(throwException: true);
            return GenerateDirectConverterForTypeInCSharp(type, fsMetaType2.Properties, fsMetaType2.IsDefaultConstructorPublic);
        }

        private static string EmitVersionInfo(string prefix, Type type, fsMetaProperty[] members, bool isConstructorPublic)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("new fsAotVersionInfo {");
            stringBuilder.AppendLine(prefix + "    IsConstructorPublic = " + ((!isConstructorPublic) ? "false" : "true") + ",");
            stringBuilder.AppendLine(prefix + "    Members = new fsAotVersionInfo.Member[] {");
            foreach (fsMetaProperty fsMetaProperty in members)
            {
                stringBuilder.AppendLine(prefix + "        new fsAotVersionInfo.Member {");
                stringBuilder.AppendLine(prefix + "            MemberName = \"" + fsMetaProperty.MemberName + "\",");
                stringBuilder.AppendLine(prefix + "            JsonName = \"" + fsMetaProperty.JsonName + "\",");
                stringBuilder.AppendLine(prefix + "            StorageType = \"" + fsMetaProperty.StorageType.CSharpName(includeNamespace: true) + "\",");
                if (fsMetaProperty.OverrideConverterType != null)
                {
                    stringBuilder.AppendLine(prefix + "            OverrideConverterType = \"" + fsMetaProperty.OverrideConverterType.CSharpName(includeNamespace: true) + "\",");
                }
                stringBuilder.AppendLine(prefix + "        },");
            }
            stringBuilder.AppendLine(prefix + "    }");
            stringBuilder.Append(prefix + "}");
            return stringBuilder.ToString();
        }

        private static string GetConverterString(fsMetaProperty member)
        {
            if (!(member.OverrideConverterType == null))
            {
                return $"typeof({member.OverrideConverterType.CSharpName(includeNamespace: true)})";
            }
            return "null";
        }

        public static string GetQualifiedConverterNameForType(Type type)
        {
            return "FullSerializer.Speedup." + type.CSharpName(includeNamespace: true, ensureSafeDeclarationName: true) + "_DirectConverter";
        }

        private static string GenerateDirectConverterForTypeInCSharp(Type type, fsMetaProperty[] members, bool isConstructorPublic)
        {
            StringBuilder stringBuilder = new StringBuilder();
            string text = type.CSharpName(includeNamespace: true);
            string text2 = type.CSharpName(includeNamespace: true, ensureSafeDeclarationName: true);
            stringBuilder.AppendLine("using System;");
            stringBuilder.AppendLine("using System.Collections.Generic;");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("namespace FullSerializer {");
            stringBuilder.AppendLine("    partial class fsConverterRegistrar {");
            stringBuilder.AppendLine("        public static Speedup." + text2 + "_DirectConverter Register_" + text2 + ";");
            stringBuilder.AppendLine("    }");
            stringBuilder.AppendLine("}");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("namespace FullSerializer.Speedup {");
            stringBuilder.AppendLine("    public class " + text2 + "_DirectConverter : fsDirectConverter<" + text + ">, fsIAotConverter {");
            stringBuilder.AppendLine("        private fsAotVersionInfo _versionInfo = " + EmitVersionInfo("        ", type, members, isConstructorPublic) + ";");
            stringBuilder.AppendLine("        fsAotVersionInfo fsIAotConverter.VersionInfo { get { return _versionInfo; } }");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("        protected override fsResult DoSerialize(" + text + " model, Dictionary<string, fsData> serialized) {");
            stringBuilder.AppendLine("            var result = fsResult.Success;");
            stringBuilder.AppendLine();
            foreach (fsMetaProperty fsMetaProperty in members)
            {
                stringBuilder.AppendLine("            result += SerializeMember(serialized, " + GetConverterString(fsMetaProperty) + ", \"" + fsMetaProperty.JsonName + "\", model." + fsMetaProperty.MemberName + ");");
            }
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("            return result;");
            stringBuilder.AppendLine("        }");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("        protected override fsResult DoDeserialize(Dictionary<string, fsData> data, ref " + text + " model) {");
            stringBuilder.AppendLine("            var result = fsResult.Success;");
            stringBuilder.AppendLine();
            for (int j = 0; j < members.Length; j++)
            {
                fsMetaProperty fsMetaProperty2 = members[j];
                stringBuilder.AppendLine("            var t" + j + " = model." + fsMetaProperty2.MemberName + ";");
                stringBuilder.AppendLine("            result += DeserializeMember(data, " + GetConverterString(fsMetaProperty2) + ", \"" + fsMetaProperty2.JsonName + "\", out t" + j + ");");
                stringBuilder.AppendLine("            model." + fsMetaProperty2.MemberName + " = t" + j + ";");
                stringBuilder.AppendLine();
            }
            stringBuilder.AppendLine("            return result;");
            stringBuilder.AppendLine("        }");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("        public override object CreateInstance(fsData data, Type storageType) {");
            if (!isConstructorPublic)
            {
                stringBuilder.AppendLine("            return Activator.CreateInstance(typeof(" + text + "), /*nonPublic:*/true);");
            }
            else
            {
                stringBuilder.AppendLine("            return new " + text + "();");
            }
            stringBuilder.AppendLine("        }");
            stringBuilder.AppendLine("    }");
            stringBuilder.AppendLine("}");
            return stringBuilder.ToString();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static fsAotCompilationManager GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
