using ScriptableSingletonPathAttributeNamespace;

namespace UGF.EditorTools.Psd2UGUI
{
    [ScriptableSingletonPathAttribute("ProjectSettings/Psd2UIFormSettings.asset")]
    public sealed class Psd2UIFormSettings : ScriptableSingleton<Psd2UIFormSettings>
    {
        public string UIImagesOutputDir;

        public string UIFormOutputDir = "Assets";

        public bool UseUIFormOutputDir = true;

        public bool CompressImage;

        public bool AutoCropMinimalNineSlice;

        public string LastUIFormOutputDir = "Assets";

        private static Psd2UIFormSettings s_Psd2UIFormSettingsObfuscationSentinel;

        internal static bool IsPsd2UIFormSettingsObfuscationSentinelNull()
        {
            return (object)s_Psd2UIFormSettingsObfuscationSentinel == null;
        }

        internal static Psd2UIFormSettings GetPsd2UIFormSettingsObfuscationSentinel()
        {
            return s_Psd2UIFormSettingsObfuscationSentinel;
        }
    }
}
