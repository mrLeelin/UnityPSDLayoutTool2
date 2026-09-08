using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PsdProtectionGuards;
using UnityEngine;
using UnityEngine.Rendering;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.Reconstructor
{

internal class SpriteReconstructor : IReconstructor
{
	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass5_0
	{
		public Stack<Transform> hierarchy;

		public ReconstructData data;

		public int sortIdx;

		public SpriteReconstructor _003C_003E4__this;

		public Vector2 docRoot;

		public _003C_003Ec__DisplayClass5_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal void _003CReconstruct_003Eb__0(ImportLayerData layer)
		{
			if (layer.Childs.Count <= 0 && layer.import)
			{
				GameObject gameObject = new GameObject(layer.name);
				Transform transform = gameObject.transform;
				transform.SetParent(hierarchy.Peek());
				transform.SetAsLastSibling();
				if (data.spriteIndex.TryGetValue(layer.indexId, out var value))
				{
					SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
					spriteRenderer.sprite = value;
					spriteRenderer.sortingOrder = sortIdx;
					sortIdx--;
				}
				Vector2 vector = _003C_003E4__this.GetLayerPosition(data, layer.indexId) - docRoot;
				vector /= data.documentPPU;
				transform.position = vector;
			}
		}

		internal void _003CReconstruct_003Eb__2(ImportLayerData layer)
		{
			Transform transform = new GameObject(layer.name).transform;
			transform.SetParent(hierarchy.Peek());
			hierarchy.Push(transform);
		}

		internal void _003CReconstruct_003Eb__3(ImportLayerData layer)
		{
			hierarchy.Pop();
		}
	}

	private const string DISPLAY_NAME = "Unity Sprites";

	public string DisplayName => "Unity Sprites";

	public string HelpMessage => string.Empty;

	public bool CanReconstruct(GameObject selection)
	{
		return true;
	}

	private Vector2 GetLayerPosition(ReconstructData data, int[] layerIdx)
	{
		if (!data.layerBoundsIndex.TryGetValue(layerIdx, out var value))
		{
			return Vector2.zero;
		}
		if (data.spriteAnchors.TryGetValue(layerIdx, out var value2))
		{
			return new Vector2(Mathf.Lerp(value.xMin, value.xMax, value2.x), Mathf.Lerp(value.yMin, value.yMax, value2.y));
		}
		return Vector2.zero;
	}

	public GameObject Reconstruct(ImportLayerData root, ReconstructData data, GameObject selection)
	{
		_003C_003Ec__DisplayClass5_0 CS_0024_003C_003E8__locals22 = new _003C_003Ec__DisplayClass5_0();
		CS_0024_003C_003E8__locals22.data = data;
		CS_0024_003C_003E8__locals22._003C_003E4__this = this;
		GameObject gameObject = new GameObject(root.name);
		if (selection != null)
		{
			gameObject.transform.SetParent(selection.transform);
		}
		CS_0024_003C_003E8__locals22.hierarchy = new Stack<Transform>();
		CS_0024_003C_003E8__locals22.hierarchy.Push(gameObject.transform);
		CS_0024_003C_003E8__locals22.docRoot = CS_0024_003C_003E8__locals22.data.documentSize;
		CS_0024_003C_003E8__locals22.docRoot.x *= CS_0024_003C_003E8__locals22.data.documentPivot.x;
		CS_0024_003C_003E8__locals22.docRoot.y *= CS_0024_003C_003E8__locals22.data.documentPivot.y;
		CS_0024_003C_003E8__locals22.sortIdx = 0;
		root.Iterate(delegate(ImportLayerData layer)
		{
			if (layer.Childs.Count <= 0 && layer.import)
			{
				GameObject gameObject2 = new GameObject(layer.name);
				Transform transform = gameObject2.transform;
				transform.SetParent(CS_0024_003C_003E8__locals22.hierarchy.Peek());
				transform.SetAsLastSibling();
				if (CS_0024_003C_003E8__locals22.data.spriteIndex.TryGetValue(layer.indexId, out var value))
				{
					SpriteRenderer spriteRenderer = gameObject2.AddComponent<SpriteRenderer>();
					spriteRenderer.sprite = value;
					spriteRenderer.sortingOrder = CS_0024_003C_003E8__locals22.sortIdx;
					CS_0024_003C_003E8__locals22.sortIdx--;
				}
				Vector2 vector = CS_0024_003C_003E8__locals22._003C_003E4__this.GetLayerPosition(CS_0024_003C_003E8__locals22.data, layer.indexId) - CS_0024_003C_003E8__locals22.docRoot;
				vector /= CS_0024_003C_003E8__locals22.data.documentPPU;
				transform.position = vector;
			}
		}, (ImportLayerData checkGroup) => checkGroup.import, delegate(ImportLayerData layer)
		{
			Transform transform = new GameObject(layer.name).transform;
			transform.SetParent(CS_0024_003C_003E8__locals22.hierarchy.Peek());
			CS_0024_003C_003E8__locals22.hierarchy.Push(transform);
		}, delegate
		{
			CS_0024_003C_003E8__locals22.hierarchy.Pop();
		});
		gameObject.AddComponent<SortingGroup>();
		return gameObject;
	}

	public SpriteReconstructor()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}
