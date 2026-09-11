using System.Runtime.CompilerServices;

// 编辑器程序集把内部类型开放给测试程序集：拆分边界（编辑器侧不得有 MonoBehaviour、
// 运行期侧不得引用编辑器侧）需要在 Test Runner 里直接被断言。
[assembly: InternalsVisibleTo("Psd2UIForm.Editor.Tests")]
