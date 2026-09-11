using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    /// <summary>
    /// Prefab Stage 的挂接兜底（与参考分支 TryUpdatePlugins 的 Psd2UIFormHierarchyOverlay 同职责）。
    ///
    /// 为什么不能只依赖壳的 OnEnable：
    /// 1) 域重载后 Stage 已经开着时不会再触发 prefabStageOpened，壳的 OnEnable 也不保证补跑，
    ///    结果是 _attached=false、_psdDocument=null，Inspector 一直显示"请打开Prefab…"；
    /// 2) 打开 Stage 后未选中根节点时，Hierarchy 上的导出勾选框也应立即可用。
    /// </summary>
    [InitializeOnLoad]
    internal static class Psd2UIFormAttachBootstrap
    {
        static Psd2UIFormAttachBootstrap()
        {
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;

            // 域重载后如果已经在 Stage 里，补挂一次
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                OnPrefabStageOpened(stage);
            }
        }

        private static void OnPrefabStageOpened(PrefabStage stage)
        {
            Psd2UIFormConverter converter = FindConverter(stage);
            if (converter == null)
            {
                return;
            }
            Psd2UIFormConverterEditor editor = Psd2UIFormConverterEditor.GetOrCreate(converter);
            if (editor == null)
            {
                return;
            }
            editor.Attach();
            if (!editor.IsDocumentLoaded())
            {
                editor.LoadDocument();
            }
        }

        private static void OnPrefabStageClosing(PrefabStage stage)
        {
            Psd2UIFormConverter converter = FindConverter(stage);
            if (converter == null)
            {
                return;
            }
            Psd2UIFormConverterEditor.GetAttached(converter)?.Detach();
        }

        private static Psd2UIFormConverter FindConverter(PrefabStage stage)
        {
            if (stage == null || stage.prefabContentsRoot == null)
            {
                return null;
            }
            Psd2UIFormConverter converter = stage.prefabContentsRoot.GetComponent<Psd2UIFormConverter>();
            if (converter == null)
            {
                converter = stage.prefabContentsRoot.GetComponentInChildren<Psd2UIFormConverter>(true);
            }
            return converter;
        }
    }
}
