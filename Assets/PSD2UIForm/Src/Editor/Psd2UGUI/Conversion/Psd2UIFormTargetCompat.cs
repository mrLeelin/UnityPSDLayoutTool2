using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    /// <summary>
    /// 兼容层：AI/生成期代码里大量方法的形参是 <c>object</c>，实际装的可能是
    /// <list type="bullet">
    /// <item>运行期壳 Psd2UIFormConverter（拆分前转换器本身就是 MonoBehaviour）；</item>
    /// <item>编辑器侧逻辑对象 Psd2UIFormConverterEditor。</item>
    /// </list>
    /// 旧代码直接写 <c>(Object)value</c> / <c>((Component)value)</c>：
    /// 形参是 object 时编译器查不出来（引用转型合法，<c>(Object)(object)value</c> 更是被洗白），
    /// 只有在运行时装的是逻辑对象时才会抛 InvalidCastException。
    ///
    /// 这里提供与"装的是 Unity 对象"时语义完全等价的替代实现：
    /// 装逻辑对象时按它承载的壳处理，装 Unity 对象时行为与原来一致。
    /// </summary>
    internal static class Psd2UIFormTargetCompat
    {
        /// <summary>等价于 <c>(Object)target == (Object)null</c>，且对逻辑对象安全（壳被销毁也算 null）。</summary>
        internal static bool IsNull(object target)
        {
            if (target == null)
            {
                return true;
            }
            Psd2UIFormConverterEditor converterEditor = target as Psd2UIFormConverterEditor;
            if (converterEditor != null)
            {
                return (Object)converterEditor.Owner == (Object)null;
            }
            return (Object)(target as Object) == (Object)null;
        }

        /// <summary>等价于 <c>(Object)(object)((Component)target).gameObject</c>。</summary>
        internal static GameObject GameObjectOf(object target)
        {
            Psd2UIFormConverterEditor converterEditor = target as Psd2UIFormConverterEditor;
            if (converterEditor != null)
            {
                return (converterEditor.Owner == null) ? null : converterEditor.gameObject;
            }
            Component component = target as Component;
            if ((Object)component != (Object)null)
            {
                return component.gameObject;
            }
            return target as GameObject;
        }

        /// <summary>等价于 <c>((Component)target).transform</c>。</summary>
        internal static Transform TransformOf(object target)
        {
            Psd2UIFormConverterEditor converterEditor = target as Psd2UIFormConverterEditor;
            if (converterEditor != null)
            {
                return (converterEditor.Owner == null) ? null : converterEditor.transform;
            }
            Transform transform = target as Transform;
            if ((Object)transform != (Object)null)
            {
                return transform;
            }
            Component component = target as Component;
            return ((Object)component != (Object)null) ? component.transform : null;
        }

        /// <summary>等价于 <c>(Object)target</c>（给 Undo/SetDirty 这类需要 Unity 对象的 API 用）。</summary>
        internal static Object AsObject(object target)
        {
            Psd2UIFormConverterEditor converterEditor = target as Psd2UIFormConverterEditor;
            if (converterEditor != null)
            {
                return converterEditor.Owner;
            }
            return target as Object;
        }
    }
}
