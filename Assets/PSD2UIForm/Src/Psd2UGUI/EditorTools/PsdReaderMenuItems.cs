using UnityEditor;

namespace cn.efunstudio.psdreader
{
    internal static class PsdReaderMenuItems
    {
        [MenuItem("Window/PSDReader/Force Reset")]
        private static void ForceReset()
        {
            PsdReaderMenuActions.ForceReset();
        }
    }
}