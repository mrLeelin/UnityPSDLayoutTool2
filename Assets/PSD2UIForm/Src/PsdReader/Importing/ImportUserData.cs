using UnityEngine;

namespace cn.efunstudio.psdreader
{
    internal class ImportUserData
    {
        public NamingConvention fileNaming;

        public GroupMode groupMode;

        public string PackingTag;

        public string TargetDirectory;

        public bool AutoImport;

        public SpriteAlignment DefaultAlignment;

        public Vector2 DefaultPivot = new Vector2(0.5f, 0.5f);

        public ScaleFactor ScaleFactor;

        public SpriteAlignment DocAlignment;

        public Vector2 DocPivot = new Vector2(0.5f, 0.5f);

        public ImportLayerData DocRoot;

        private static ImportUserData s_ObfuscationSentinel;

        public ImportLayerData GetLayerData(int[] layerIdx)
        {
            if (DocRoot != null)
            {
                ImportLayerData importLayerData = DocRoot;
                int num = 0;
                while (true)
                {
                    if (num < layerIdx.Length)
                    {
                        int num2 = layerIdx[num];
                        if (num2 < 0 || num2 >= importLayerData.Childs.Count)
                        {
                            break;
                        }
                        importLayerData = importLayerData.Childs[num2];
                        num++;
                        continue;
                    }
                    return importLayerData;
                }
                return null;
            }
            return null;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static ImportUserData GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
