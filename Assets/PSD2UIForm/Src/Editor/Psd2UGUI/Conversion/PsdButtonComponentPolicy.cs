using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UGF.EditorTools.Psd2UGUI
{
    internal static class PsdButtonComponentPolicy
    {
        internal static Type Resolve(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == typeof(Button).FullName) return typeof(Button);
            var matches = TypeCache.GetTypesDerivedFrom<MonoBehaviour>().Where(type =>
                type.FullName == name.Trim() || type.AssemblyQualifiedName == name.Trim() || type.Name == name.Trim()).ToArray();
            if (matches.Length != 1 || matches[0].IsAbstract || matches[0].ContainsGenericParameters)
                throw new InvalidOperationException("默认 Button 组件类型无效或不唯一，请填写可挂载的 MonoBehaviour 完整类名。");
            var type = matches[0];
            var runtimeAssemblies = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Player);
            if (!type.Assembly.GetName().Name.StartsWith("UnityEngine", StringComparison.Ordinal) &&
                !runtimeAssemblies.Any(assembly => assembly.name == type.Assembly.GetName().Name))
                throw new InvalidOperationException("默认 Button 组件必须位于运行时程序集，不能选择 Editor 脚本。");
            return type;
        }

        internal static void Apply(GameObject root, IList<UIHelperBase> helpers, IList<GameObject> nodes, Type type)
        {
            if (type == typeof(Button)) return;
            for (int i = 0; i < helpers.Count; i++)
            {
                if (!(helpers[i] is ButtonHelper) && !(helpers[i] is TMPButtonHelper)) continue;
                var old = nodes[i].GetComponent<Button>();
                if (old == null || old.GetType() == type) continue;
                Replace(root, old, type);
            }
        }

        internal static void Replace(GameObject root, Button old, Type type)
        {
            // Capture references before destruction, including explicit navigation links.
            var references = new List<(Component owner, string path)>();
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component == old) continue;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == old)
                        references.Add((component, property.propertyPath));
            }
            if (!typeof(Button).IsAssignableFrom(type) && references.Count > 0)
                throw new InvalidOperationException("按钮 " + old.name + " 被其他组件引用，请使用 Button 子类以保留绑定。");

            var host = old.gameObject;
            var navigation = old.navigation;
            bool selfUp = navigation.selectOnUp == old, selfDown = navigation.selectOnDown == old;
            bool selfLeft = navigation.selectOnLeft == old, selfRight = navigation.selectOnRight == old;
            var backup = new GameObject("ButtonSettings", typeof(RectTransform), typeof(Button)) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var saved = backup.GetComponent<Button>();
                EditorUtility.CopySerialized(old, saved);
                Object.DestroyImmediate(old);
                var replacement = host.AddComponent(type) as MonoBehaviour;
                if (replacement == null) throw new InvalidOperationException("无法添加 Button 组件：" + type.FullName);
                replacement.enabled = saved.enabled;
                if (replacement is Button button)
                {
                    button.interactable = saved.interactable;
                    button.transition = saved.transition;
                    button.colors = saved.colors;
                    button.spriteState = saved.spriteState;
                    button.animationTriggers = saved.animationTriggers;
                    button.targetGraphic = saved.targetGraphic;
                    if (selfUp) navigation.selectOnUp = button;
                    if (selfDown) navigation.selectOnDown = button;
                    if (selfLeft) navigation.selectOnLeft = button;
                    if (selfRight) navigation.selectOnRight = button;
                    button.navigation = navigation;
                    button.onClick = saved.onClick;
                }
                else if (host.GetComponent<Button>() != null)
                    throw new InvalidOperationException("自定义按钮脚本依赖标准 Button，不能替换为独立按钮组件。");
                foreach (var reference in references)
                {
                    var serialized = new SerializedObject(reference.owner);
                    serialized.FindProperty(reference.path).objectReferenceValue = replacement;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            finally { Object.DestroyImmediate(backup); }
        }
    }
}
