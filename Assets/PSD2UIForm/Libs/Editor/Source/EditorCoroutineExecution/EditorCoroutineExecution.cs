using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using PsdProtectionGuards;
using UnityEditor;
using UnityEngine;
using cn.efunstudio.psdreader;
using PsdProtectionRuntime;

namespace PsdEditorCoroutines
{

internal class EditorCoroutineExecution
{
	private IEnumerator _enumerator;

	public EditorCoroutine Coroutine;

	private object _currentYield;

	private Type _currentYieldType;

	private float _remainingWaitSeconds;

	private EditorCoroutine _nestedCoroutine;

	private DateTime _lastUpdateTime;

	public bool ShowProgress;

	private bool _cancelable;

	private bool _canceled;

	private string _progressTitle;

	public string ProgressLabel;

	public float ProgressPercent;

	[SpecialName]
	public bool IsRunning()
	{
		return _enumerator != null;
	}

	public EditorCoroutineExecution(IEnumerator P_0)
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_enumerator = P_0;
		Coroutine = new EditorCoroutine();
		ShowProgress = false;
		_lastUpdateTime = DateTime.Now;
	}

	public EditorCoroutineExecution(IEnumerator P_0, string P_1, bool P_2)
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_enumerator = P_0;
		Coroutine = new EditorCoroutine();
		ShowProgress = true;
		_cancelable = P_2;
		_progressTitle = P_1;
		ProgressLabel = "initializing....";
		ProgressPercent = 0f;
		_lastUpdateTime = DateTime.Now;
	}

	public void Update()
	{
		if (_enumerator == null)
		{
			return;
		}
		if (_canceled)
		{
			Complete();
			return;
		}
		bool flag = false;
		DateTime now = DateTime.Now;
		if (_currentYield != null)
		{
			if (_currentYieldType == typeof(WaitForSeconds))
			{
				_remainingWaitSeconds -= (float)(now - _lastUpdateTime).TotalSeconds;
				if (_remainingWaitSeconds > 0f)
				{
					flag = true;
				}
			}
			else if (!(_currentYieldType == typeof(WaitForEndOfFrame)) && !(_currentYieldType == typeof(WaitForFixedUpdate)))
			{
				if (!typeof(AsyncOperation).IsAssignableFrom(_currentYieldType))
				{
					if (_currentYieldType.IsSubclassOf(typeof(CustomYieldInstruction)))
					{
						if ((_currentYield as CustomYieldInstruction).keepWaiting)
						{
							flag = true;
						}
					}
					else if (!(_currentYieldType == typeof(EditorCoroutine)))
					{
						if (typeof(IEnumerator).IsAssignableFrom(_currentYieldType))
						{
							if (_nestedCoroutine == null)
							{
								_nestedCoroutine = EditorCoroutineRunner.StartCoroutine(_currentYield as IEnumerator);
								flag = true;
							}
							else
							{
								flag = !_nestedCoroutine.HasFinished;
							}
						}
						else if (_currentYieldType == typeof(Coroutine))
						{
							Debug.LogError("Nested Coroutines started by Unity's defaut StartCoroutine method are not supported in editor! please use EditorCoroutineRunner.Start instead. Canceling.");
							_canceled = true;
						}
						else
						{
							Debug.LogError("Unsupported yield (" + _currentYieldType?.ToString() + ") in editor coroutine!! Canceling.");
							_canceled = true;
						}
					}
					else if (!(_currentYield as EditorCoroutine).HasFinished)
					{
						flag = true;
					}
				}
				else if (_currentYield is AsyncOperation { isDone: false })
				{
					flag = true;
				}
			}
			else
			{
				flag = false;
			}
		}
		_lastUpdateTime = now;
		if (!_canceled)
		{
			if (flag)
			{
				return;
			}
			if (_enumerator.MoveNext())
			{
				_currentYield = _enumerator.Current;
				if (_currentYield == null)
				{
					return;
				}
				_currentYieldType = _currentYield.GetType();
				if (_currentYieldType == typeof(WaitForSeconds))
				{
					WaitForSeconds obj = _currentYield as WaitForSeconds;
					FieldInfo field = typeof(WaitForSeconds).GetField("m_Seconds", BindingFlags.Instance | BindingFlags.NonPublic);
					if (field != null)
					{
						_remainingWaitSeconds = (float)field.GetValue(obj);
					}
				}
				else if (_currentYieldType == typeof(EditorStatusUpdate))
				{
					EditorStatusUpdate editorStatusUpdate = _currentYield as EditorStatusUpdate;
					if (editorStatusUpdate.HasLabelUpdate)
					{
						ProgressLabel = editorStatusUpdate.Label;
					}
					if (editorStatusUpdate.HasPercentUpdate)
					{
						ProgressPercent = editorStatusUpdate.PercentComplete;
					}
				}
			}
			else
			{
				Complete();
			}
		}
		else
		{
			Complete();
		}
	}

	private void Complete()
	{
		_enumerator = null;
		Coroutine.HasFinished = true;
	}

	public void DisplayProgress()
	{
		if (!_cancelable)
		{
			EditorUtility.DisplayProgressBar(_progressTitle, ProgressLabel, ProgressPercent);
			return;
		}
		_canceled = EditorUtility.DisplayCancelableProgressBar(_progressTitle, ProgressLabel, ProgressPercent);
		if (_canceled)
		{
			Debug.Log("CANCELED");
		}
	}
}
}
