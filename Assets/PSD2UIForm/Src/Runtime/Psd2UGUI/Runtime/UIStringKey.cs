using UnityEngine;

using Object = UnityEngine.Object;
namespace UGF.EditorTools.Psd2UGUI
{
    public sealed class UIStringKey : MonoBehaviour
    {
        [SerializeField]
        private string m_Key;

        internal static UIStringKey s_UIStringKeyObfuscationSentinel;

        internal string Key
        {
            get
            {
                return m_Key ?? string.Empty;
            }
            set
            {
                m_Key = value;
            }
        }

        internal static bool IsUIStringKeyObfuscationSentinelNull()
        {
            return (object)s_UIStringKeyObfuscationSentinel == null;
        }

        internal static UIStringKey GetUIStringKeyObfuscationSentinel()
        {
            return s_UIStringKeyObfuscationSentinel;
        }
    }
}
