using System;
using NUnit.Framework;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Psd2UIForm.Tests
{
    /// <summary>
    /// 锁住"面板能显示"的前提：Inspector 的显示条件是
    /// targetEditor.IsDocumentLoaded()，而它只有在 Attach() 被触发过、并且把
    /// 绑定的 PSD 加载进来之后才为 true。
    ///
    /// 这次的事故就是 Attach() 没人触发（Inspector 只 GetOrCreate 没 Attach，
    /// 且域重载后 PrefabStage 已开着时 prefabStageOpened 不再触发），
    /// 结果 IsDocumentLoaded() 恒为 false，面板一直显示"请打开Prefab…"。
    /// </summary>
    public class ConverterAttachTests
    {
        private const string SamplePsd = "Assets/UnityPSDLayoutTool2/Assets/PSD2UIForm/Examples/Psd2UguiForm_UGUI.psd";

        [Test]
        public void Attach_LoadsBoundPsd_SoInspectorPanelCanShow()
        {
            var go = new GameObject("AttachTestRoot");
            try
            {
                var shell = go.AddComponent<Psd2UIFormConverter>();
                Psd2UIFormConverterEditor editor = Psd2UIFormConverterEditor.GetOrCreate(shell);

                // 挂接前：文档还没加载（正是用户看到"请打开Prefab…"的状态）
                Assert.That(editor.IsDocumentLoaded(), Is.False);

                shell.psdAssetPath = SamplePsd;

                // Inspector 的 OnEnable / PrefabStage overlay 走的就是这一句
                editor.Attach();

                Assert.That(editor.IsDocumentLoaded(), Is.True,
                    "Attach() 之后必须把绑定的 PSD 加载进来，否则 Inspector 面板不会显示");
                Assert.That(editor.GetSourcePsdAssetPath(), Is.EqualTo(SamplePsd));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Attach_IsIdempotent()
        {
            var go = new GameObject("AttachIdempotentTest");
            try
            {
                var shell = go.AddComponent<Psd2UIFormConverter>();
                shell.psdAssetPath = SamplePsd;
                Psd2UIFormConverterEditor editor = Psd2UIFormConverterEditor.GetOrCreate(shell);

                editor.Attach();
                Assert.That(editor.IsDocumentLoaded(), Is.True);

                // 再挂一次不应重复订阅回调，也不应把已加载的文档丢掉
                editor.Attach();
                Assert.That(editor.IsDocumentLoaded(), Is.True);
                Assert.That(Psd2UIFormConverterEditor.GetAttached(shell), Is.SameAs(editor));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Detach_IsReachableDuringDestroy_AndReleasesCacheEntry()
        {
            var go = new GameObject("DetachReachTest");
            var shell = go.AddComponent<Psd2UIFormConverter>();
            shell.psdAssetPath = SamplePsd;
            Psd2UIFormConverterEditor editor = Psd2UIFormConverterEditor.GetOrCreate(shell);
            editor.Attach();

            Object.DestroyImmediate(go);

            Assert.That(Psd2UIFormConverterEditor.GetAttached(shell), Is.Null,
                "壳销毁时必须能退订（OnDestroy 阶段 Unity 的 == 已把壳判为 null，Detach 不能依赖存在性判断）");
        }

        [Test]
        public void Detach_KeepsDocumentAlive_SoNodePreviewsStillRender()
        {
            var root = new GameObject("DetachDocLifetimeTest");
            try
            {
                var shell = root.AddComponent<Psd2UIFormConverter>();
                shell.psdAssetPath = SamplePsd;
                var childGo = new GameObject("BoundLayerNode");
                childGo.transform.SetParent(root.transform, false);
                var node = childGo.AddComponent<PsdLayerNode>();
                node.BindPsdLayerIndex = 0;

                Psd2UIFormConverterEditor editor = Psd2UIFormConverterEditor.GetOrCreate(shell);
                editor.Attach();
                Assert.That(editor.IsDocumentLoaded(), Is.True);
                Assert.That(node.GetBoundPsdLayer(), Is.Not.Null, "Attach 应把子节点重新绑定到文档图层");

                // 模拟"从 converter 根切到某个图层节点"：converter 的 Inspector 失去选中 -> Detach。
                // 此时若释放文档，新选中的 PsdLayerNode 在首次渲染时会 NullReferenceException
                // （PsdBinaryReader.get_Position），这就是用户报的那个 NRE。
                editor.Detach();
                Assert.That(editor.IsDocumentLoaded(), Is.True,
                    "Detach 不得释放 PSD 文档：节点仍持有该文档的 PsdLayer");
                Assert.That(node.GetBoundPsdLayer(), Is.Not.Null);

                // 壳真的被销毁时才彻底释放
                editor.Dispose();
                Assert.That(editor.IsDocumentLoaded(), Is.False);
                Assert.That(Psd2UIFormConverterEditor.GetAttached(shell), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AttachBootstrap_TypeIsPresentInEditorAssembly()
        {
            // PrefabStage overlay：域重载后 Stage 已开着时靠它补挂
            Type bootstrap = typeof(Psd2UIFormConverterEditor).Assembly
                .GetType("UGF.EditorTools.Psd2UGUI.Psd2UIFormAttachBootstrap");
            Assert.That(bootstrap, Is.Not.Null, "缺少 Prefab Stage 补挂入口（[InitializeOnLoad]）");
            Assert.That(bootstrap.IsAbstract && bootstrap.IsSealed, Is.True, "应当是 static class");
        }
    }
}
