using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PsdProtectionGuards;
using UnityEngine;
using UnityEngine.UI;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.Reconstructor
{

internal class UnityGuiReconstructor : IReconstructor
{
	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass6_0
	{
		public UnityGuiReconstructor _003C_003E4__this;

		public Stack<RectTransform> hierarchy;

		public ReconstructData data;

		public Vector2 docRoot;

		public RectTransform rootT;

		public _003C_003Ec__DisplayClass6_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal void _003CReconstruct_003Eb__0(ImportLayerData layer)
		{
			if (layer.Childs.Count <= 0 && layer.import)
			{
				RectTransform rectTransform = _003C_003E4__this.CreateObject(layer.name);
				rectTransform.SetParent(hierarchy.Peek());
				rectTransform.SetAsFirstSibling();
				if (data.spriteIndex.TryGetValue(layer.indexId, out var value))
				{
					rectTransform.gameObject.AddComponent<Image>().sprite = value;
				}
				if (!data.layerBoundsIndex.TryGetValue(layer.indexId, out var value2))
				{
					value2 = Rect.zero;
				}
				if (!data.spriteAnchors.TryGetValue(layer.indexId, out var value3))
				{
					value3 = Vector2.zero;
				}
				Vector2 vector = _003C_003E4__this.GetLayerPosition(value2, value3) - docRoot;
				rectTransform.position = rootT.TransformPoint(vector.x, vector.y, 0f);
				rectTransform.pivot = value3;
				rectTransform.sizeDelta = new Vector2(value2.width, value2.height);
			}
		}

		internal void _003CReconstruct_003Eb__2(ImportLayerData layer)
		{
			RectTransform rectTransform = _003C_003E4__this.CreateObject(layer.name);
			rectTransform.SetParent(hierarchy.Peek());
			rectTransform.SetAsFirstSibling();
			rectTransform.anchorMin = Vector2.zero;
			rectTransform.anchorMax = Vector2.one;
			rectTransform.offsetMin = Vector2.zero;
			rectTransform.offsetMax = Vector2.zero;
			hierarchy.Push(rectTransform);
		}

		internal void _003CReconstruct_003Eb__3(ImportLayerData layer)
		{
			hierarchy.Pop();
		}
	}

	private const string DISPLAY_NAME = "Unity UI";

	public string DisplayName => "Unity UI";

	public string HelpMessage => "Select an object in a Unity UI hierarchy";

	public bool CanReconstruct(GameObject selection)
	{
		if (!(selection == null))
		{
			return selection.GetComponentInParent<Canvas>() != null;
		}
		return false;
	}

	private Vector2 GetLayerPosition(ReconstructData data, int[] layerIdx)
	{
		if (data.layerBoundsIndex.TryGetValue(layerIdx, out var value))
		{
			if (data.spriteAnchors.TryGetValue(layerIdx, out var value2))
			{
				return GetLayerPosition(value, value2);
			}
			return Vector2.zero;
		}
		return Vector2.zero;
	}

	private Vector2 GetLayerPosition(Rect layerRect, Vector2 layerAnchor)
	{
		return new Vector2(Mathf.Lerp(layerRect.xMin, layerRect.xMax, layerAnchor.x), Mathf.Lerp(layerRect.yMin, layerRect.yMax, layerAnchor.y));
	}

	public GameObject Reconstruct(ImportLayerData root, ReconstructData data, GameObject selection)
	{
		_003C_003Ec__DisplayClass6_0 CS_0024_003C_003E8__locals31 = new _003C_003Ec__DisplayClass6_0();
		CS_0024_003C_003E8__locals31._003C_003E4__this = this;
		CS_0024_003C_003E8__locals31.data = data;
		if (!(selection == null))
		{
			if (!CanReconstruct(selection))
			{
				return null;
			}
			CS_0024_003C_003E8__locals31.rootT = CreateObject(root.name);
			CS_0024_003C_003E8__locals31.rootT.SetParent(selection.transform);
			CS_0024_003C_003E8__locals31.rootT.sizeDelta = CS_0024_003C_003E8__locals31.data.documentSize;
			CS_0024_003C_003E8__locals31.rootT.pivot = CS_0024_003C_003E8__locals31.data.documentPivot;
			CS_0024_003C_003E8__locals31.rootT.localPosition = Vector3.zero;
			CS_0024_003C_003E8__locals31.hierarchy = new Stack<RectTransform>();
			CS_0024_003C_003E8__locals31.hierarchy.Push(CS_0024_003C_003E8__locals31.rootT);
			CS_0024_003C_003E8__locals31.docRoot = CS_0024_003C_003E8__locals31.data.documentSize;
			CS_0024_003C_003E8__locals31.docRoot.x *= CS_0024_003C_003E8__locals31.data.documentPivot.x;
			CS_0024_003C_003E8__locals31.docRoot.y *= CS_0024_003C_003E8__locals31.data.documentPivot.y;
			root.Iterate(delegate(ImportLayerData layer)
			{
				if (layer.Childs.Count <= 0 && layer.import)
				{
					RectTransform rectTransform = CS_0024_003C_003E8__locals31._003C_003E4__this.CreateObject(layer.name);
					rectTransform.SetParent(CS_0024_003C_003E8__locals31.hierarchy.Peek());
					rectTransform.SetAsFirstSibling();
					if (CS_0024_003C_003E8__locals31.data.spriteIndex.TryGetValue(layer.indexId, out var value))
					{
						rectTransform.gameObject.AddComponent<Image>().sprite = value;
					}
					if (!CS_0024_003C_003E8__locals31.data.layerBoundsIndex.TryGetValue(layer.indexId, out var value2))
					{
						value2 = Rect.zero;
					}
					if (!CS_0024_003C_003E8__locals31.data.spriteAnchors.TryGetValue(layer.indexId, out var value3))
					{
						value3 = Vector2.zero;
					}
					Vector2 vector = CS_0024_003C_003E8__locals31._003C_003E4__this.GetLayerPosition(value2, value3) - CS_0024_003C_003E8__locals31.docRoot;
					rectTransform.position = CS_0024_003C_003E8__locals31.rootT.TransformPoint(vector.x, vector.y, 0f);
					rectTransform.pivot = value3;
					rectTransform.sizeDelta = new Vector2(value2.width, value2.height);
				}
			}, (ImportLayerData checkGroup) => checkGroup.import, delegate(ImportLayerData layer)
			{
				RectTransform rectTransform = CS_0024_003C_003E8__locals31._003C_003E4__this.CreateObject(layer.name);
				rectTransform.SetParent(CS_0024_003C_003E8__locals31.hierarchy.Peek());
				rectTransform.SetAsFirstSibling();
				rectTransform.anchorMin = Vector2.zero;
				rectTransform.anchorMax = Vector2.one;
				rectTransform.offsetMin = Vector2.zero;
				rectTransform.offsetMax = Vector2.zero;
				CS_0024_003C_003E8__locals31.hierarchy.Push(rectTransform);
			}, delegate
			{
				CS_0024_003C_003E8__locals31.hierarchy.Pop();
			});
			return CS_0024_003C_003E8__locals31.rootT.gameObject;
		}
		return null;
	}

	private RectTransform CreateObject(string name)
	{
		GameObject gameObject = new GameObject(name);
		RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
		if (rectTransform == null)
		{
			rectTransform = gameObject.AddComponent<RectTransform>();
		}
		return rectTransform;
	}

	public UnityGuiReconstructor()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}
