namespace cn.efunstudio.psdreader
{
    public static class PsdReaderMenuActions
    {
        public const string ForceResetMenuPath = "Window/PSDReader/Force Reset";

        public static void ForceReset()
        {
            EditorCoroutineRunner.KillAllCoroutines();
        }
    }
}