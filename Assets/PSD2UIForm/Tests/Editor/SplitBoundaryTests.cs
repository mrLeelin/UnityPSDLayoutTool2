using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Psd2UIForm.Tests
{
    /// <summary>
    /// 程序集拆分边界的回归闸门。
    ///
    /// 背景（本测试就是为这次事故立的）：
    /// 生成流程会 AddComponent 那些"挂在生成物上"的组件，而 Unity 不允许
    /// AddComponent 任何"仅编辑器编译"程序集里的脚本，报错形如
    /// "Can't add script behaviour 'X' because it is an editor script."
    /// 一旦某个 MonoBehaviour 被分到 Editor 程序集，生成到一半就会崩。
    /// </summary>
    public class SplitBoundaryTests
    {
        private const string RuntimeAssemblyName = "cn.efunstudio.psd2ugui";
        private const string EditorAssemblyName = "cn.efunstudio.psd2ugui.Editor";

        private static Assembly RuntimeAssembly
        {
            get
            {
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == RuntimeAssemblyName);
                Assert.That(assembly, Is.Not.Null, "找不到运行期程序集 " + RuntimeAssemblyName);
                return assembly;
            }
        }

        private static Assembly EditorAssembly
        {
            get
            {
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == EditorAssemblyName);
                Assert.That(assembly, Is.Not.Null, "找不到编辑器程序集 " + EditorAssemblyName);
                return assembly;
            }
        }

        [Test]
        public void EditorAssembly_DeclaresNoMonoBehaviour()
        {
            string[] offenders = EditorAssembly.GetTypes()
                .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t))
                .Select(t => t.FullName)
                .ToArray();

            Assert.That(offenders, Is.Empty,
                "编辑器程序集里的 MonoBehaviour 无法被 AddComponent（Unity 会当成 editor script 拒绝）。" +
                "需要它出现在生成物上，就必须放到运行期程序集里。违规类型：\n" +
                string.Join("\n", offenders));
        }

        [Test]
        public void RuntimeAssembly_DoesNotReferenceEditorAssembly()
        {
            foreach (AssemblyName reference in RuntimeAssembly.GetReferencedAssemblies())
            {
                Assert.That(reference.Name, Is.Not.EqualTo(EditorAssemblyName),
                    "运行期程序集不能引用编辑器程序集（会有循环/平台冲突）");
            }
        }

        private static readonly Regex EditorApiUsage = new Regex(
            @"(AssetDatabase\.|EditorUtility\.|EditorGUI\.|EditorGUILayout\.|EditorGUIUtility\.|EditorApplication\.|EditorSceneManager\.|PrefabUtility\.|Selection\.|Undo\.|CompilationPipeline\.|using UnityEditor|\[MenuItem|\[CustomEditor|\[CanEditMultipleObjects)",
            RegexOptions.Compiled);

        /// <summary>
        /// 运行期程序集是平台无限制的，所以任何 UnityEditor API 都必须包在 #if UNITY_EDITOR 里；
        /// 扫描的是 Player 程序集自己声明的源文件列表，因此不会漏掉文件。
        /// 注意：编辑器里编译时包住的调用仍会让程序集引用 UnityEditor.CoreModule —— 那是允许的，
        /// 真正要防的是"未包住"导致的播放器构建失败。
        /// </summary>
        [Test]
        public void RuntimeSources_UseUnityEditorOnlyInsideEditorGuards()
        {
            var playerSet = UnityEditor.Compilation.CompilationPipeline
                .GetAssemblies(UnityEditor.Compilation.AssembliesType.Player);
            var runtime = playerSet.First(a => a.name == RuntimeAssemblyName);

            var offenders = new List<string>();
            foreach (string file in runtime.sourceFiles)
            {
                int guardDepth = 0;
                int lineNumber = 0;
                foreach (string rawLine in File.ReadLines(file))
                {
                    lineNumber++;
                    string line = StripLineComment(rawLine);
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("#if"))
                    {
                        guardDepth++;
                        continue;
                    }
                    if (trimmed.StartsWith("#endif"))
                    {
                        guardDepth = Math.Max(0, guardDepth - 1);
                        continue;
                    }
                    if (guardDepth == 0 && EditorApiUsage.IsMatch(line))
                    {
                        offenders.Add(Path.GetFileName(file) + ":" + lineNumber + ": " + line.Trim());
                    }
                }
            }

            Assert.That(offenders, Is.Empty,
                "运行期程序集里的 UnityEditor 调用必须包在 #if UNITY_EDITOR 里，否则播放器构建会失败：\n" +
                string.Join("\n", offenders));
        }

        private static string StripLineComment(string line)
        {
            int index = line.IndexOf("//", StringComparison.Ordinal);
            return (index < 0) ? line : line.Substring(0, index);
        }

        [Test]
        public void PlayerAssemblySet_ContainsRuntimeAndExcludesEditor()
        {
            var playerSet = UnityEditor.Compilation.CompilationPipeline
                .GetAssemblies(UnityEditor.Compilation.AssembliesType.Player);

            Assert.That(playerSet.Any(a => a.name == RuntimeAssemblyName), Is.True,
                "运行期程序集必须参与播放器构建（includePlatforms 不能限制为 Editor）");
            Assert.That(playerSet.Any(a => a.name == EditorAssemblyName), Is.False,
                "编辑器程序集不能进入播放器构建");

            var runtime = playerSet.First(a => a.name == RuntimeAssemblyName);
            Assert.That(runtime.assemblyReferences.Select(r => r.name), Has.No.Member(EditorAssemblyName));
        }

        [Test]
        public void EveryRuntimeMonoBehaviour_CanBeAddedToGameObject()
        {
            Type[] types = RuntimeAssembly.GetTypes()
                .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t) && !t.IsAbstract)
                .ToArray();

            Assert.That(types, Is.Not.Empty, "运行期程序集里应当存在挂在生成物上的组件");

            var failures = new List<string>();
            foreach (Type type in types)
            {
                var go = new GameObject("SplitBoundaryTest_" + type.Name);
                try
                {
                    Component component = go.AddComponent(type);
                    if (component == null)
                    {
                        failures.Add(type.FullName + " -> AddComponent 返回 null");
                    }
                }
                catch (Exception e)
                {
                    failures.Add(type.FullName + " -> " + e.GetBaseException().Message);
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }

            Assert.That(failures, Is.Empty, "运行期组件必须都能被 AddComponent：\n" + string.Join("\n", failures));
        }

        [Test]
        public void ConverterShell_IsRuntimeMonoBehaviour_AndAttachesEditorLogicOnEnable()
        {
            Assert.That(typeof(Psd2UIFormConverter).Assembly.GetName().Name, Is.EqualTo(RuntimeAssemblyName),
                "Psd2UIFormConverter 是生成期 AddComponent 的作者锚点，必须位于运行期程序集");

            var go = new GameObject("ConverterShellTest");
            try
            {
                var shell = go.AddComponent<Psd2UIFormConverter>();
                Assert.That(shell, Is.Not.Null);

                // OnEnable 已经经门面挂上编辑器侧逻辑，说明挂接链完整（而不是只挂了个空壳）
                Psd2UIFormConverterEditor logic = Psd2UIFormConverterEditor.GetAttached(shell);
                Assert.That(logic, Is.Not.Null, "壳的 OnEnable 应当经门面挂接编辑器侧逻辑");
                Assert.That(logic.Owner, Is.SameAs(shell));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ConverterShell_DetachesEditorLogicOnDestroy()
        {
            var go = new GameObject("ConverterDetachTest");
            var shell = go.AddComponent<Psd2UIFormConverter>();
            Psd2UIFormConverterEditor logic = Psd2UIFormConverterEditor.GetAttached(shell);
            Assert.That(logic, Is.Not.Null);

            // OnDestroy 期间 Unity 的 == 已把壳判为 null，Detach 必须仍能找到逻辑对象并退订回调
            Object.DestroyImmediate(go);

            Assert.That(Psd2UIFormConverterEditor.GetAttached(shell), Is.Null,
                "壳销毁后不应再持有已挂接的逻辑对象（否则 SceneView/Hierarchy 回调会泄漏）");
        }
    }
}
