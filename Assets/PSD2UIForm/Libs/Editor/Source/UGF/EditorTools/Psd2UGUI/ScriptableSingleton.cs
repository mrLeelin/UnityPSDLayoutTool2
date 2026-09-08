using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using PsdProtectionGuards;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using PsdProtectionRuntime;
using PsdEditorAttributes;

namespace UGF.EditorTools.Psd2UGUI
{

public abstract class ScriptableSingleton<T> : ScriptableObject where T : ScriptableObject
{
	private static readonly Regex ImUnICohY1;

	private static readonly Regex qkOnGah4Hq;

	private static T uIMnb51h1M;

	internal static object YtmvkJZeNG936AG9d8b4;

	internal static T Instance
	{
		get
		{
			if (!uIMnb51h1M)
			{
				QXVnsKgb1Q();
			}
			return uIMnb51h1M;
		}
	}

	internal static T QXVnsKgb1Q()
	{
		string filePath = GetFilePath();
		if (!string.IsNullOrEmpty(filePath))
		{
			bool flag = File.Exists(iO6nqwXcEC(filePath));
			uIMnb51h1M = InternalEditorUtility.LoadSerializedFileAndForget(filePath).OfType<T>().FirstOrDefault();
			if (!uIMnb51h1M)
			{
				if (flag && oy0nFXfUdC(filePath, out var val))
				{
					uIMnb51h1M = val;
					SFSnrIUk3L();
					Debug.LogWarning(typeof(T).Name + ": 检测到旧版配置反序列化失败，已自动迁移配置文件: " + filePath);
				}
				else
				{
					uIMnb51h1M = ScriptableObject.CreateInstance<T>();
					if (flag)
					{
						SFSnrIUk3L();
						Debug.LogWarning(typeof(T).Name + ": 配置文件反序列化失败，已重建默认配置: " + filePath);
					}
				}
			}
		}
		else
		{
			Debug.LogError("ScriptableSingleton: 请设置持久化存档路径！ ");
		}
		return uIMnb51h1M;
	}

	internal static void SFSnrIUk3L(bool P_0 = true)
	{
		if (!uIMnb51h1M)
		{
			return;
		}
		string filePath = GetFilePath();
		if (!string.IsNullOrEmpty(filePath))
		{
			string directoryName = Path.GetDirectoryName(filePath);
			if (!Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			UnityEngine.Object[] obj = new T[1] { uIMnb51h1M };
			InternalEditorUtility.SaveToSerializedFileAndForget(obj, filePath, P_0);
		}
	}

	protected static string GetFilePath()
	{
		return typeof(T).GetCustomAttributes(inherit: true).Cast<PsdConfigPathAttribute>().FirstOrDefault((PsdConfigPathAttribute v) => v != null)?.RelativePath;
	}

	private static bool oy0nFXfUdC(string P_0, out T P_1)
	{
		P_1 = null;
		string text = iO6nqwXcEC(P_0);
		if (File.Exists(text))
		{
			if (Ticna1pfN9(text, out P_1))
			{
				return true;
			}
			return ovxn6SWNUC(text, out P_1);
		}
		return false;
	}

	private static bool Ticna1pfN9(string P_0, out T P_1)
	{
		P_1 = null;
		if (b0vnS6pjfG(out var arg, out var num))
		{
			string text = File.ReadAllText(P_0);
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}
			Match match = ImUnICohY1.Match(text);
			if (match.Success)
			{
				string text2 = $"{match.Groups[1].Value}{{fileID: {num}, guid: {arg}, type: 3}}";
				string contents = text.Substring(0, match.Index) + text2 + text.Substring(match.Index + match.Length);
				string path = Path.Combine(Path.GetTempPath(), $"{typeof(T).Name}_{Guid.NewGuid():N}.asset");
				try
				{
					File.WriteAllText(path, contents);
					UnityEngine.Object[] source = InternalEditorUtility.LoadSerializedFileAndForget(path);
					P_1 = source.OfType<T>().FirstOrDefault();
					return P_1 != null;
				}
				catch
				{
					P_1 = null;
					return false;
				}
				finally
				{
					if (File.Exists(path))
					{
						File.Delete(path);
					}
				}
			}
			return false;
		}
		return false;
	}

	private static bool ovxn6SWNUC(string P_0, out T P_1)
	{
		P_1 = ScriptableObject.CreateInstance<T>();
		try
		{
			Dictionary<string, string> dictionary = kvjnLxY5e3(File.ReadAllText(P_0));
			bool flag = false;
			foreach (FieldInfo item in qTen0MNByV())
			{
				if (dictionary.TryGetValue(item.Name, out var value) && pYgnEFfxHN(value, item.FieldType, out var value2))
				{
					item.SetValue(P_1, value2);
					flag = true;
				}
			}
			if (flag)
			{
				return true;
			}
			UnityEngine.Object.DestroyImmediate(P_1);
			P_1 = null;
			return false;
		}
		catch
		{
			if (P_1 != null)
			{
				UnityEngine.Object.DestroyImmediate(P_1);
			}
			P_1 = null;
			return false;
		}
	}

	private static bool b0vnS6pjfG(out string P_0, out long P_1)
	{
		P_0 = null;
		P_1 = 0L;
		T val = ScriptableObject.CreateInstance<T>();
		try
		{
			MonoScript monoScript = MonoScript.FromScriptableObject(val);
			return (bool)monoScript && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(monoScript, out P_0, out P_1);
		}
		finally
		{
			UnityEngine.Object.DestroyImmediate(val);
		}
	}

	private static Dictionary<string, string> kvjnLxY5e3(string P_0)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (Match item in qkOnGah4Hq.Matches(P_0))
		{
			dictionary[item.Groups[1].Value] = item.Groups[2].Value;
		}
		return dictionary;
	}

	private static IEnumerable<FieldInfo> qTen0MNByV()
	{
		return from field in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			where !field.IsStatic && !field.IsInitOnly && !field.IsLiteral && !field.IsNotSerialized && (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null)
			select field;
	}

	private static bool pYgnEFfxHN(string P_0, Type P_1, out object P_2)
	{
		string text = ((P_0 != null) ? P_0.Trim() : string.Empty);
		if (!(P_1 == typeof(string)))
		{
			float result6;
			if (P_1 == typeof(bool))
			{
				if (text == "1")
				{
					P_2 = true;
					return true;
				}
				if (text == "0")
				{
					P_2 = false;
					return true;
				}
				if (bool.TryParse(text, out var result))
				{
					P_2 = result;
					return true;
				}
			}
			else if (P_1 == typeof(int))
			{
				if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2))
				{
					P_2 = result2;
					return true;
				}
			}
			else if (P_1 == typeof(long))
			{
				if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result3))
				{
					P_2 = result3;
					return true;
				}
			}
			else if (!(P_1 == typeof(float)))
			{
				double result5;
				if (!(P_1 == typeof(double)))
				{
					if (P_1.IsEnum)
					{
						if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result4))
						{
							P_2 = Enum.ToObject(P_1, result4);
							return true;
						}
						string value = J93nCgUfag(text);
						if (!string.IsNullOrEmpty(value))
						{
							try
							{
								P_2 = Enum.Parse(P_1, value, ignoreCase: true);
								return true;
							}
							catch
							{
							}
						}
					}
				}
				else if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result5))
				{
					P_2 = result5;
					return true;
				}
			}
			else if (float.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result6))
			{
				P_2 = result6;
				return true;
			}
			P_2 = null;
			return false;
		}
		P_2 = J93nCgUfag(text);
		return true;
	}

	private static string J93nCgUfag(string P_0)
	{
		if (!string.IsNullOrEmpty(P_0) && !(P_0 == "null") && !(P_0 == "~"))
		{
			if (P_0.Length >= 2)
			{
				if (P_0[0] == '"' && P_0[P_0.Length - 1] == '"')
				{
					return P_0.Substring(1, P_0.Length - 2).Replace("\\\\", "\\").Replace("\\\"", "\"")
						.Replace("\\n", "\n")
						.Replace("\\r", "\r")
						.Replace("\\t", "\t");
				}
				if (P_0[0] == '\'' && P_0[P_0.Length - 1] == '\'')
				{
					return P_0.Substring(1, P_0.Length - 2).Replace("''", "'");
				}
			}
			return P_0;
		}
		if (!(P_0 == string.Empty))
		{
			return null;
		}
		return string.Empty;
	}

	private static string iO6nqwXcEC(string P_0)
	{
		if (!Path.IsPathRooted(P_0))
		{
			return Path.GetFullPath(P_0);
		}
		return P_0;
	}

	protected ScriptableSingleton()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private ScriptableSingleton(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}

	static ScriptableSingleton()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		ImUnICohY1 = new Regex("^(\\s*m_Script:\\s*)\\{fileID:\\s*-?\\d+,\\s*guid:\\s*[0-9a-fA-F]+,\\s*type:\\s*\\d+\\}\\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
		qkOnGah4Hq = new Regex("^\\s{2}([A-Za-z_][A-Za-z0-9_]*):(?:\\s*(.*))?$", RegexOptions.Compiled | RegexOptions.Multiline);
	}

	internal static bool diAguiZekP5ZpvyPHpjO()
	{
		return YtmvkJZeNG936AG9d8b4 == null;
	}

	internal static object c7PcXUZeXO1APhhJ78q6()
	{
		return YtmvkJZeNG936AG9d8b4;
	}
}
}
