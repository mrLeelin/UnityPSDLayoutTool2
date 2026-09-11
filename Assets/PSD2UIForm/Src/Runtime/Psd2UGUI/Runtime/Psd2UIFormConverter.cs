using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ReadOnlyFieldAttributeNamespace;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    /// <summary>
    /// 生成元数据的序列化载体（原为 Psd2UIFormConverter 的私有嵌套类型）。
    /// 字段名与类型保持不变，因此既有资产的序列化内容无需迁移。
    /// </summary>
    [Serializable]
    internal sealed class GeneratedMetadataEntry
    {
        public string GlobalObjectId;

        public string Key;

        public string TypeKey;

        public bool IsContainer;

        internal static GeneratedMetadataEntry s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static GeneratedMetadataEntry GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }

    /// <summary>
    /// 生成元数据的序列化载体（原为 Psd2UIFormConverter 的私有嵌套类型）。
    /// </summary>
    [Serializable]
    internal sealed class GeneratedMetadataSerializedEntry
    {
        public string PrefabAssetPath;

        [HideInInspector]
        public string Json;

        public List<GeneratedMetadataEntry> Entries = new List<GeneratedMetadataEntry>();

        internal static GeneratedMetadataSerializedEntry s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static GeneratedMetadataSerializedEntry GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }

    /// <summary>
    /// PSD 生成物的作者锚点（原 UGF.EditorTools.Psd2UGUI.Psd2UIFormConverter 的"状态部分"）。
    ///
    /// 它必须放在**运行期程序集**里：Unity 不允许把 Editor 程序集里的脚本 AddComponent 到对象上
    /// （"Can't add script behaviour '...' because it is an editor script"），而生成期需要
    /// <c>AddComponent&lt;Psd2UIFormConverter&gt;()</c> 把作者锚点挂到生成根上。
    ///
    /// 这里只保留序列化状态与生命周期回调；全部生成期逻辑在 Psd2UIFormConverterEditor
    /// （Editor 程序集）中，通过 Psd2UIFormEditorHost 门面在 OnEnable/OnDestroy 时挂接/摘除，
    /// 以保证触发时机与拆分前完全一致。
    ///
    /// 字段名、类型与属性顺序与拆分前逐字一致，保证既有资产序列化不变。
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Psd2UIFormConverter : MonoBehaviour
    {
        [ReadOnlyFieldAttribute]
        [SerializeField]
        internal string psdAssetChangeTime;

        [Tooltip("UIForm名字")]
        [SerializeField]
        internal string uiFormName;

        [SerializeField]
        [Tooltip("关联的psd文件")]
        internal Sprite psdAsset;

        [HideInInspector]
        [SerializeField]
        internal Sprite previewSprite;

        [SerializeField]
        [HideInInspector]
        internal string psdAssetPath;

        [SerializeField]
        [Header("Debug:")]
        internal bool drawLayerRectGizmos = true;

        [SerializeField]
        internal Color drawLayerRectGizmosColor = Color.gray;

        [SerializeField]
        [Tooltip("Scene点选时，优先选中命中区域内面积最小的图层节点")]
        internal bool preferSmallestLayerOnScenePick = true;

        [HideInInspector]
        [SerializeField]
        internal List<GeneratedMetadataSerializedEntry> generatedMetadataEntries = new List<GeneratedMetadataSerializedEntry>();

        internal static Psd2UIFormConverter s_Psd2UIFormConverterObfuscationSentinel;

        [CompilerGenerated]
        private static Psd2UIFormConverter s_Instance;

        internal static Psd2UIFormConverter Instance
        {
            [CompilerGenerated]
            get
            {
                return s_Instance;
            }
            [CompilerGenerated]
            private set
            {
                s_Instance = value;
            }
        }

        private void OnEnable()
        {
            Instance = this;
            Psd2UIFormEditorHost.Current?.AttachConverter(this);
        }

        private void Start()
        {
            Psd2UIFormEditorHost.Current?.LoadConverterDocument(this);
        }

        private void OnDestroy()
        {
            Psd2UIFormEditorHost.Current?.DetachConverter(this);
        }

        private void OnDrawGizmos()
        {
            Psd2UIFormEditorHost.Current?.DrawConverterGizmos(this);
        }

        internal static bool IsPsd2UIFormConverterObfuscationSentinelNull()
        {
            return (object)s_Psd2UIFormConverterObfuscationSentinel == null;
        }

        internal static Psd2UIFormConverter GetPsd2UIFormConverterObfuscationSentinel()
        {
            return s_Psd2UIFormConverterObfuscationSentinel;
        }
    }
}
