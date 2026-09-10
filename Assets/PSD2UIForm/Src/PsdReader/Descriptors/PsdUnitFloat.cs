using PsdBinaryReaderNamespace;
using PsdBinaryUtilityNamespace;
using PropertyCollectionNamespace;

namespace PsdUnitFloatNamespace
{
    internal class PsdUnitFloat : PropertyCollection
    {
        private static PsdUnitFloat s_ObfuscationSentinel;

        public PsdUnitFloat()
            : base(2)
        {
        }

        public PsdUnitFloat(PsdBinaryReader psdBinaryReader)
        {
            string text = psdBinaryReader.ReadSignature();
            Add("Type", PsdBinaryUtility.ParseUnitType(text));
            Add("Value", psdBinaryReader.ReadDouble());
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdUnitFloat GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
