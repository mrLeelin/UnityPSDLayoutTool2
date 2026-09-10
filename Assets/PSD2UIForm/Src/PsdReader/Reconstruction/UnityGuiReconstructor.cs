using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace cn.efunstudio.psdreader.Reconstructor
{
    internal class UnityGuiReconstructor : IReconstructor
    {
        private const string DISPLAY_NAME = "Unity UI";

        internal static UnityGuiReconstructor s_ObfuscationSentinel;

        public string DisplayName => "Unity UI";

        public string HelpMessage => "Select an object in a Unity UI hierarchy";

        public bool CanReconstruct(GameObject selection)
        {
            if ((Object)(object)selection == (Object)null)
            {
                return false;
            }
            return (Object)(object)selection.GetComponentInParent<Canvas>() != (Object)null;
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
            return GetLayerPosition(value, value2);
        }

        private Vector2 GetLayerPosition(Rect layerRect, Vector2 layerAnchor)
        {
            return new Vector2(Mathf.Lerp(layerRect.xMin, layerRect.xMax, layerAnchor.x), Mathf.Lerp(layerRect.yMin, layerRect.yMax, layerAnchor.y));
        }

        public GameObject Reconstruct(ImportLayerData root, ReconstructData data, GameObject selection)
        {
            if ((Object)(object)selection == (Object)null)
            {
                return null;
            }
            if (!CanReconstruct(selection))
            {
                return null;
            }
            RectTransform rootT = CreateObject(root.name);
            ((Transform)rootT).SetParent(selection.transform);
            rootT.sizeDelta = data.documentSize;
            rootT.pivot = data.documentPivot;
            ((Transform)rootT).localPosition = Vector3.zero;
            Stack<RectTransform> hierarchy = new Stack<RectTransform>();
            hierarchy.Push(rootT);
            Vector2 docRoot = data.documentSize;
            docRoot.x *= data.documentPivot.x;
            docRoot.y *= data.documentPivot.y;
            root.Iterate(delegate(ImportLayerData layer)
            {
                if (layer.Childs.Count <= 0 && layer.import)
                {
                    RectTransform val = CreateObject(layer.name);
                    ((Transform)val).SetParent((Transform)(object)hierarchy.Peek());
                    ((Transform)val).SetAsFirstSibling();
                    if (data.spriteIndex.TryGetValue(layer.indexId, out var value))
                    {
                        ((Component)val).gameObject.AddComponent<Image>().sprite = value;
                    }
                    if (!data.layerBoundsIndex.TryGetValue(layer.indexId, out var value2))
                    {
                        value2 = Rect.zero;
                    }
                    if (!data.spriteAnchors.TryGetValue(layer.indexId, out var value3))
                    {
                        value3 = Vector2.zero;
                    }
                    Vector2 val2 = GetLayerPosition(value2, value3) - docRoot;
                    ((Transform)val).position = ((Transform)rootT).TransformPoint(val2.x, val2.y, 0f);
                    val.pivot = value3;
                    val.sizeDelta = new Vector2(value2.width, value2.height);
                }
            }, (ImportLayerData checkGroup) => checkGroup.import, delegate(ImportLayerData layer)
            {
                RectTransform val = CreateObject(layer.name);
                ((Transform)val).SetParent((Transform)(object)hierarchy.Peek());
                ((Transform)val).SetAsFirstSibling();
                val.anchorMin = Vector2.zero;
                val.anchorMax = Vector2.one;
                val.offsetMin = Vector2.zero;
                val.offsetMax = Vector2.zero;
                hierarchy.Push(val);
            }, delegate
            {
                hierarchy.Pop();
            });
            return ((Component)rootT).gameObject;
        }

        private RectTransform CreateObject(string name)
        {
            GameObject val = new GameObject(name);
            RectTransform val2 = val.GetComponent<RectTransform>();
            if ((Object)(object)val2 == (Object)null)
            {
                val2 = val.AddComponent<RectTransform>();
            }
            return val2;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static UnityGuiReconstructor GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
