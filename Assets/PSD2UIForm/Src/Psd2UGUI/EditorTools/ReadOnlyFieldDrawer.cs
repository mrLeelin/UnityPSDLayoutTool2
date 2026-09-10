using ReadOnlyFieldAttributeNamespace;
using UnityEditor;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    [CustomPropertyDrawer(typeof(ReadOnlyFieldAttribute))]
    internal sealed class ReadOnlyFieldDrawer : PropertyDrawer
    {
        private static ReadOnlyFieldDrawer s_ObfuscationSentinel;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static ReadOnlyFieldDrawer GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
