using System;
using PsdProtectionGuards;
using UnityEngine;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[Serializable]
public class UGUIParseRule
{
	public GUIType UIType;

	public string UITypeDesc;

	public string[] TypeMatches;

	public GameObject UIPrefab;

	public string UIHelper;

	public string Comment;

	public UGUIParseRule()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}
