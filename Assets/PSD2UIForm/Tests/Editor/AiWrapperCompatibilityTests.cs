using System.IO;
using AiAnalysisPackageBuilderNamespace;
using AiJobManagerNamespace;
using NUnit.Framework;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Psd2UIForm.Tests
{
    /// <summary>
    /// 锁住这类历史 bug：AI 链路的方法形参多是 <c>object</c>，实际装的是编辑器侧
    /// Psd2UIFormConverterEditor（拆分前转换器本身就是 MonoBehaviour，所以旧写法
    /// <c>(Object)value</c> / <c>((Component)value)</c> 一直是对的）。
    /// 形参是 object 时编译器查不出来（<c>(Object)(object)value</c> 更是被洗白），
    /// 运行时装的是逻辑对象就会抛 InvalidCastException。
    /// </summary>
    public class AiWrapperCompatibilityTests
    {
        private const string SamplePsd = "Assets/UnityPSDLayoutTool2/Assets/PSD2UIForm/Examples/Psd2UguiForm_UGUI.psd";

        [Test]
        public void AnalysisPackageBuild_AcceptsEditorWrapper()
        {
            var root = new GameObject("AiWrapperCompatTest");
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

                var manager = new AiJobManager();
                AiJobContext context = manager.CreateJobContext(
                    "unit-test-provider",
                    Directory.GetParent(Application.dataPath).FullName,
                    SamplePsd);
                Assert.That(context, Is.Not.Null);

                bool built = new AiAnalysisPackageBuilder().TryBuildAnalysisPackage(
                    editor, context, out string _, out string message);

                Assert.That(built, Is.True,
                    "编辑器 wrapper 必须能被 AI 分析包构建接受（历史 bug：把 wrapper 当 UnityEngine.Object 转型，运行时 InvalidCastException）。message=" + message);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
