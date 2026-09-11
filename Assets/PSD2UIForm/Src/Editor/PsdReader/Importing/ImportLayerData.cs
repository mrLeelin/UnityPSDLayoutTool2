using System;
using System.Collections.Generic;
using UnityEngine;

namespace cn.efunstudio.psdreader
{
    internal class ImportLayerData
    {
        public string name;

        public string path;

        public int[] indexId;

        public bool import;

        public bool useDefaults;

        public SpriteAlignment Alignment;

        public Vector2 Pivot;

        public ScaleFactor ScaleFactor;

        public List<ImportLayerData> Childs;

        internal static ImportLayerData s_ObfuscationSentinel;

        public void Iterate(Action<ImportLayerData> layerCallback, Func<ImportLayerData, bool> canEnterGroup = null, Action<ImportLayerData> enterGroupCallback = null, Action<ImportLayerData> exitGroupCallback = null)
        {
            for (int num = Childs.Count - 1; num >= 0; num--)
            {
                ImportLayerData importLayerData = Childs[num];
                if (importLayerData != null)
                {
                    layerCallback?.Invoke(importLayerData);
                    if (importLayerData.Childs.Count > 0)
                    {
                        bool flag = true;
                        if (canEnterGroup != null)
                        {
                            flag = canEnterGroup(importLayerData);
                        }
                        if (flag)
                        {
                            enterGroupCallback?.Invoke(importLayerData);
                            importLayerData.Iterate(layerCallback, canEnterGroup, enterGroupCallback, exitGroupCallback);
                            exitGroupCallback?.Invoke(importLayerData);
                        }
                    }
                }
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static ImportLayerData GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
