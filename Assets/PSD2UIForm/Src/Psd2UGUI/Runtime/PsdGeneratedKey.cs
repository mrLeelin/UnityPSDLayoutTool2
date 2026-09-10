using System.Runtime.CompilerServices;
using UnityEngine;

using Object = UnityEngine.Object;
namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class PsdGeneratedKey : MonoBehaviour
    {
        [HideInInspector]
        [SerializeField]
        private string m_Key;

        [HideInInspector]
        [SerializeField]
        private string m_TypeKey;

        [SerializeField]
        [HideInInspector]
        private bool m_IsContainer;

        internal static PsdGeneratedKey s_PsdGeneratedKeyObfuscationSentinel;

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

        [SpecialName]
        internal string GetTypeKey()
        {
            return m_TypeKey ?? string.Empty;
        }

        [SpecialName]
        internal void SetTypeKey(string key)
        {
            m_TypeKey = key;
        }

        [SpecialName]
        internal bool IsContainer()
        {
            return m_IsContainer;
        }

        [SpecialName]
        internal void SetIsContainer(bool enabled)
        {
            m_IsContainer = enabled;
        }

        [SpecialName]
        internal string BuildCompositeKey()
        {
            return (m_IsContainer ? "C" : "N") + ":" + GetTypeKey() + ":" + Key;
        }

        internal static bool IsPsdGeneratedKeyObfuscationSentinelNull()
        {
            return (object)s_PsdGeneratedKeyObfuscationSentinel == null;
        }

        internal static PsdGeneratedKey GetPsdGeneratedKeyObfuscationSentinel()
        {
            return s_PsdGeneratedKeyObfuscationSentinel;
        }
    }
}
