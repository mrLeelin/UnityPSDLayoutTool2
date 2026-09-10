using System.Collections.Generic;
using UnityEngine;

using Object = UnityEngine.Object;
namespace cn.efunstudio.psdreader.Reconstructor
{
    internal struct ReconstructData
    {
        public Dictionary<int[], Sprite> spriteIndex;

        public Dictionary<int[], Vector2> spriteAnchors;

        public Dictionary<int[], Rect> layerBoundsIndex;

        public Vector2 documentSize;

        public Vector2 documentPivot;

        public float documentPPU;

        internal static object s_ObfuscationSentinel;

        public ReconstructData(Vector2 docSize, Vector2 docPivot, float PPU)
        {
            spriteIndex = new Dictionary<int[], Sprite>();
            spriteAnchors = new Dictionary<int[], Vector2>();
            layerBoundsIndex = new Dictionary<int[], Rect>();
            documentPivot = docPivot;
            documentSize = docSize;
            documentPPU = PPU;
        }

        public void AddSprite(int[] layerIdx, Sprite sprite, Vector2 anchor)
        {
            spriteIndex.Add(layerIdx, sprite);
            spriteAnchors.Add(layerIdx, anchor);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static object GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
