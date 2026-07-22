namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The hierarchy plan is an external trust boundary. Parsing is manual and
    /// allowlist-based so newly invented properties cannot silently enter Unity
    /// through Newtonsoft's otherwise permissive object deserialization.
    /// </summary>
    public static class PsdHierarchyPlanJson
    {
        private static readonly HashSet<string> PlanProperties = Allowed(
            "schemaVersion", "sourceFingerprint", "groups", "renames");
        private static readonly HashSet<string> GroupProperties = Allowed(
            "key", "parentKey", "memberStableIds", "displayName", "evidence", "confidence");
        private static readonly HashSet<string> RenameProperties = Allowed(
            "stableId", "name", "evidence", "confidence");

        public static PsdHierarchyPlan Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new PsdHierarchyPlanFormatException("Hierarchy plan JSON is empty.");
            }

            try
            {
                JObject root;
                using (var stringReader = new StringReader(json))
                using (var reader = new JsonTextReader(stringReader))
                {
                    reader.DateParseHandling = DateParseHandling.None;
                    reader.FloatParseHandling = FloatParseHandling.Double;
                    root = JObject.Load(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        LineInfoHandling = LineInfoHandling.Load
                    });
                    if (reader.Read())
                    {
                        throw new PsdHierarchyPlanFormatException("Hierarchy plan contains trailing JSON content.");
                    }
                }

                RequireAllowedProperties(root, PlanProperties, "plan");
                int schemaVersion = RequireInteger(root, "schemaVersion", "plan");
                if (schemaVersion != PsdHierarchyPlan.CurrentSchemaVersion)
                {
                    throw new PsdHierarchyPlanFormatException(
                        "Unsupported hierarchy plan schema version: " + schemaVersion.ToString(CultureInfo.InvariantCulture));
                }

                var plan = new PsdHierarchyPlan
                {
                    schemaVersion = schemaVersion,
                    sourceFingerprint = RequireString(root, "sourceFingerprint", "plan")
                };

                JArray groups = RequireArray(root, "groups", "plan");
                for (int index = 0; index < groups.Count; index++)
                {
                    JObject group = RequireObject(groups[index], "groups[" + index + "]");
                    RequireAllowedProperties(group, GroupProperties, "groups[" + index + "]");
                    plan.groups.Add(new PsdHierarchyPlanGroup
                    {
                        key = RequireString(group, "key", "groups[" + index + "]"),
                        parentKey = RequireString(group, "parentKey", "groups[" + index + "]"),
                        memberStableIds = RequireStringArray(group, "memberStableIds", "groups[" + index + "]"),
                        displayName = RequireString(group, "displayName", "groups[" + index + "]"),
                        evidence = RequireString(group, "evidence", "groups[" + index + "]"),
                        confidence = RequireFiniteNumber(group, "confidence", "groups[" + index + "]")
                    });
                }

                JArray renames = RequireArray(root, "renames", "plan");
                for (int index = 0; index < renames.Count; index++)
                {
                    JObject rename = RequireObject(renames[index], "renames[" + index + "]");
                    RequireAllowedProperties(rename, RenameProperties, "renames[" + index + "]");
                    plan.renames.Add(new PsdHierarchyPlanRename
                    {
                        stableId = RequireString(rename, "stableId", "renames[" + index + "]"),
                        name = RequireString(rename, "name", "renames[" + index + "]"),
                        evidence = RequireString(rename, "evidence", "renames[" + index + "]"),
                        confidence = RequireFiniteNumber(rename, "confidence", "renames[" + index + "]")
                    });
                }

                return plan;
            }
            catch (PsdHierarchyPlanFormatException)
            {
                throw;
            }
            catch (JsonException exception)
            {
                throw new PsdHierarchyPlanFormatException("Invalid hierarchy plan JSON: " + exception.Message, exception);
            }
            catch (InvalidOperationException exception)
            {
                throw new PsdHierarchyPlanFormatException("Invalid hierarchy plan JSON: " + exception.Message, exception);
            }
        }

        public static string SerializeRequest(PsdHierarchyRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            return JsonConvert.SerializeObject(request, Formatting.None, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Culture = CultureInfo.InvariantCulture
            });
        }

        private static HashSet<string> Allowed(params string[] names)
        {
            return new HashSet<string>(names, StringComparer.Ordinal);
        }

        private static void RequireAllowedProperties(JObject value, HashSet<string> allowed, string path)
        {
            string unknown = value.Properties()
                .Select(property => property.Name)
                .FirstOrDefault(name => !allowed.Contains(name));
            if (unknown != null)
            {
                throw new PsdHierarchyPlanFormatException("Unknown property '" + unknown + "' at " + path + ".");
            }
        }

        private static JObject RequireObject(JToken token, string path)
        {
            var value = token as JObject;
            if (value == null)
            {
                throw new PsdHierarchyPlanFormatException(path + " must be an object.");
            }

            return value;
        }

        private static JArray RequireArray(JObject owner, string name, string path)
        {
            JToken token;
            if (!owner.TryGetValue(name, StringComparison.Ordinal, out token) || token.Type != JTokenType.Array)
            {
                throw new PsdHierarchyPlanFormatException(path + "." + name + " must be an array.");
            }

            return (JArray)token;
        }

        private static string RequireString(JObject owner, string name, string path)
        {
            JToken token;
            if (!owner.TryGetValue(name, StringComparison.Ordinal, out token) || token.Type != JTokenType.String)
            {
                throw new PsdHierarchyPlanFormatException(path + "." + name + " must be a string.");
            }

            return token.Value<string>();
        }

        private static int RequireInteger(JObject owner, string name, string path)
        {
            JToken token;
            if (!owner.TryGetValue(name, StringComparison.Ordinal, out token) || token.Type != JTokenType.Integer)
            {
                throw new PsdHierarchyPlanFormatException(path + "." + name + " must be an integer.");
            }

            long value = token.Value<long>();
            if (value < int.MinValue || value > int.MaxValue)
            {
                throw new PsdHierarchyPlanFormatException(path + "." + name + " is outside the supported integer range.");
            }

            return (int)value;
        }

        private static double RequireFiniteNumber(JObject owner, string name, string path)
        {
            JToken token;
            if (!owner.TryGetValue(name, StringComparison.Ordinal, out token) ||
                (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
            {
                throw new PsdHierarchyPlanFormatException(path + "." + name + " must be a number.");
            }

            double value = token.Value<double>();
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new PsdHierarchyPlanFormatException(path + "." + name + " must be finite.");
            }

            if (value < 0d || value > 1d)
            {
                throw new PsdHierarchyPlanFormatException(path + "." + name + " must be between 0 and 1.");
            }

            return value;
        }

        private static List<string> RequireStringArray(JObject owner, string name, string path)
        {
            JArray array = RequireArray(owner, name, path);
            var values = new List<string>(array.Count);
            for (int index = 0; index < array.Count; index++)
            {
                if (array[index].Type != JTokenType.String)
                {
                    throw new PsdHierarchyPlanFormatException(path + "." + name + "[" + index + "] must be a string.");
                }

                values.Add(array[index].Value<string>());
            }

            return values;
        }
    }
}
