using System;
using System.Runtime.CompilerServices;
using PsdProtectionGuards;
using UnityEditor;
using UnityEngine;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

public abstract class UIHelperBase : MonoBehaviour
{
	[SpecialName]
	internal PsdLayerNode GetLayerNode()
	{
		return GetComponent<PsdLayerNode>();
	}

	private void OnEnable()
	{
		ParseAndAttachUIElements();
	}

	internal abstract void ParseAndAttachUIElements();

	internal abstract PsdLayerNode[] GetDependencies();

	protected abstract void InitUIElements(GameObject uiRoot);

	protected PsdLayerNode[] CalculateDependencies(params PsdLayerNode[] nodes)
	{
		if (nodes != null && nodes.Length != 0)
		{
			for (int num = nodes.Length - 1; num >= 0; num--)
			{
				PsdLayerNode psdLayerNode = nodes[num];
				if (psdLayerNode == null || psdLayerNode == GetLayerNode())
				{
					ArrayUtility.RemoveAt(ref nodes, num);
				}
			}
			return nodes;
		}
		return null;
	}

	internal GameObject CreateOrUpdateUiObject(GameObject P_0 = null)
	{
		if (GetLayerNode().IsPrimaryUiElement() && GetLayerNode().UIType != GUIType.Null)
		{
			UGUIParseRule uGUIParseRule = UGUIParser.Instance.GetRuleForUiType(GetLayerNode().UIType);
			if (uGUIParseRule != null && !(uGUIParseRule.UIPrefab == null))
			{
				if (P_0 != null && !HaveMatchingPrimaryUiComponents(P_0, uGUIParseRule.UIPrefab))
				{
					UnityEngine.Object.DestroyImmediate(P_0);
					P_0 = null;
				}
				if (P_0 == null)
				{
					P_0 = UnityEngine.Object.Instantiate(uGUIParseRule.UIPrefab, Vector3.zero, Quaternion.identity);
				}
				if (GetLayerNode().IsPrimaryUiElement())
				{
					P_0.name = GetLayerNode().GetGeneratedObjectName();
				}
				InitUIElements(P_0);
				return P_0;
			}
			Debug.LogWarning($"创建UI类型{GetLayerNode().UIType}失败:Rule配置项不存在或UIPrefab为空");
			return null;
		}
		return null;
	}

	private static bool HaveMatchingPrimaryUiComponents(object P_0, object P_1)
	{
		Type type = GetPrimaryUiComponentType(P_1);
		Type type2 = GetPrimaryUiComponentType(P_0);
		if (!(type == null) && !(type2 == null))
		{
			return type == type2;
		}
		return false;
	}

	private static Type GetPrimaryUiComponentType(object P_0)
	{
		if ((UnityEngine.Object)P_0 == null)
		{
			return null;
		}
		Component[] components = ((GameObject)P_0).GetComponents<Component>();
		foreach (Component component in components)
		{
			if (!(component == null))
			{
				Type type = component.GetType();
				if (!(type == typeof(Transform)) && !(type == typeof(RectTransform)) && !(type == typeof(CanvasRenderer)) && !(type == typeof(UIStringKey)) && !(type == typeof(PsdGeneratedKey)))
				{
					return type;
				}
			}
		}
		return null;
	}

	protected UIHelperBase()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private UIHelperBase(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
