using System;
using System.Collections;
using System.Collections.Generic;
using PsdProtectionGuards;
using UnityEditor;
using UnityEngine;
using PsdProtectionRuntime;
using PsdEditorCoroutines;

namespace cn.efunstudio.psdreader
{

internal class EditorCoroutineRunner
{
	private static List<EditorCoroutineExecution> coroutineStates;

	private static List<EditorCoroutineExecution> finishedThisUpdate;

	private static EditorCoroutineExecution uiCoroutineState;

	public static void KillAllCoroutines()
	{
		EditorUtility.ClearProgressBar();
		uiCoroutineState = null;
		coroutineStates.Clear();
		finishedThisUpdate.Clear();
	}

	public static EditorCoroutine StartCoroutine(IEnumerator coroutine)
	{
		return StoreCoroutine(new EditorCoroutineExecution(coroutine));
	}

	public static EditorCoroutine StartCoroutineWithUI(IEnumerator coroutine, string title, bool isCancelable = false)
	{
		if (uiCoroutineState != null)
		{
			Debug.LogError("EditorCoroutineRunner only supports running one coroutine that draws a GUI! [" + title + "]");
			return null;
		}
		uiCoroutineState = new EditorCoroutineExecution(coroutine, title, isCancelable);
		return StoreCoroutine(uiCoroutineState);
	}

	private static EditorCoroutine StoreCoroutine(EditorCoroutineExecution state)
	{
		if (coroutineStates == null)
		{
			coroutineStates = new List<EditorCoroutineExecution>();
			finishedThisUpdate = new List<EditorCoroutineExecution>();
		}
		if (coroutineStates.Count == 0)
		{
			EditorApplication.update = (EditorApplication.CallbackFunction)Delegate.Combine(EditorApplication.update, new EditorApplication.CallbackFunction(Runner));
		}
		coroutineStates.Add(state);
		return state.Coroutine;
	}

	public static void UpdateUILabel(string label)
	{
		if (uiCoroutineState != null && uiCoroutineState.ShowProgress)
		{
			uiCoroutineState.ProgressLabel = label;
		}
	}

	public static void UpdateUIProgressBar(float percent)
	{
		if (uiCoroutineState != null && uiCoroutineState.ShowProgress)
		{
			uiCoroutineState.ProgressPercent = percent;
		}
	}

	public static void UpdateUI(string label, float percent)
	{
		if (uiCoroutineState != null && uiCoroutineState.ShowProgress)
		{
			uiCoroutineState.ProgressLabel = label;
			uiCoroutineState.ProgressPercent = percent;
		}
	}

	private static void Runner()
	{
		for (int i = 0; i < coroutineStates.Count; i++)
		{
			TickState(coroutineStates[i]);
		}
		for (int j = 0; j < finishedThisUpdate.Count; j++)
		{
			coroutineStates.Remove(finishedThisUpdate[j]);
			if (uiCoroutineState == finishedThisUpdate[j])
			{
				uiCoroutineState = null;
				EditorUtility.ClearProgressBar();
			}
		}
		finishedThisUpdate.Clear();
		if (coroutineStates.Count == 0)
		{
			EditorApplication.update = (EditorApplication.CallbackFunction)Delegate.Remove(EditorApplication.update, new EditorApplication.CallbackFunction(Runner));
		}
	}

	private static void TickState(EditorCoroutineExecution state)
	{
		if (state.IsRunning())
		{
			state.Update();
			if (state.ShowProgress && uiCoroutineState == state)
			{
				uiCoroutineState.DisplayProgress();
			}
		}
		else
		{
			finishedThisUpdate.Add(state);
		}
	}

	public EditorCoroutineRunner()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}
