using System;
using System.Collections.Generic;
using System.Reflection;
using PsdProtectionGuards;
using cn.efunstudio.psdreader.FullSerializer.Internal;
using cn.efunstudio.psdreader.FullSerializer.Internal.DirectConverters;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer
{

public class fsConverterRegistrar
{
	public static AnimationCurve_DirectConverter Register_AnimationCurve_DirectConverter;

	public static Bounds_DirectConverter Register_Bounds_DirectConverter;

	public static GUIStyleState_DirectConverter Register_GUIStyleState_DirectConverter;

	public static GUIStyle_DirectConverter Register_GUIStyle_DirectConverter;

	public static Gradient_DirectConverter Register_Gradient_DirectConverter;

	public static Keyframe_DirectConverter Register_Keyframe_DirectConverter;

	public static LayerMask_DirectConverter Register_LayerMask_DirectConverter;

	public static RectOffset_DirectConverter Register_RectOffset_DirectConverter;

	public static Rect_DirectConverter Register_Rect_DirectConverter;

	public static List<Type> Converters;

	static fsConverterRegistrar()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		Converters = new List<Type>();
		FieldInfo[] declaredFields = typeof(fsConverterRegistrar).GetDeclaredFields();
		foreach (FieldInfo fieldInfo in declaredFields)
		{
			if (fieldInfo.Name.StartsWith("Register_"))
			{
				Converters.Add(fieldInfo.FieldType);
			}
		}
		MethodInfo[] declaredMethods = typeof(fsConverterRegistrar).GetDeclaredMethods();
		foreach (MethodInfo methodInfo in declaredMethods)
		{
			if (methodInfo.Name.StartsWith("Register_"))
			{
				methodInfo.Invoke(null, null);
			}
		}
		List<Type> list = new List<Type>(Converters);
		foreach (Type converter in Converters)
		{
			object obj = null;
			try
			{
				obj = Activator.CreateInstance(converter);
			}
			catch (Exception)
			{
			}
			if (obj is fsIAotConverter fsIAotConverter2 && !fsAotCompilationManager.IsAotModelUpToDate(fsMetaType.Get(new fsConfig(), fsIAotConverter2.ModelType), fsIAotConverter2))
			{
				list.Remove(converter);
			}
		}
		Converters = list;
	}

	public fsConverterRegistrar()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}
