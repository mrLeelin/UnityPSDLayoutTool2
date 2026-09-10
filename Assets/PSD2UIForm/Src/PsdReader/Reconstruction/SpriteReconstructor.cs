using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Rendering;

namespace cn.efunstudio.psdreader.Reconstructor
{
    internal class SpriteReconstructor : IReconstructor
    {
        private const string DISPLAY_NAME = "Unity Sprites";

        private static SpriteReconstructor s_ObfuscationSentinel;

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
            if (!data.spriteAnchors.TryGetValue(layerIdx, out var value2))
            {
                return Vector2.zero;
            }
            return new Vector2(Mathf.Lerp(value.xMin, value.xMax, value2.x), Mathf.Lerp(value.yMin, value.yMax, value2.y));
        }

        public GameObject Reconstruct(ImportLayerData root, ReconstructData data, GameObject selection)
        {
            GameObject val = new GameObject(root.name);
            if ((Object)(object)selection != (Object)null)
            {
                val.transform.SetParent(selection.transform);
            }
            Stack<Transform> hierarchy = new Stack<Transform>();
            hierarchy.Push(val.transform);
            Vector2 docRoot = data.documentSize;
            docRoot.x *= data.documentPivot.x;
            docRoot.y *= data.documentPivot.y;
            int sortIdx = 0;
            root.Iterate(delegate(ImportLayerData layer)
            {
                if (layer.Childs.Count <= 0 && layer.import)
                {
                    GameObject val2 = new GameObject(layer.name);
                    Transform transform = val2.transform;
                    transform.SetParent(hierarchy.Peek());
                    transform.SetAsLastSibling();
                    if (data.spriteIndex.TryGetValue(layer.indexId, out var value))
                    {
                        SpriteRenderer obj = val2.AddComponent<SpriteRenderer>();
                        obj.sprite = value;
                        ((Renderer)obj).sortingOrder = sortIdx;
                        sortIdx--;
                    }
                    Vector2 val3 = GetLayerPosition(data, layer.indexId) - docRoot;
                    val3 /= data.documentPPU;
                    transform.position = (Vector2)(val3);
                }
            }, (ImportLayerData checkGroup) => checkGroup.import, delegate(ImportLayerData layer)
            {
                Transform transform = new GameObject(layer.name).transform;
                transform.SetParent(hierarchy.Peek());
                hierarchy.Push(transform);
            }, delegate
            {
                hierarchy.Pop();
            });
            val.AddComponent<SortingGroup>();
            return val;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static SpriteReconstructor GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
