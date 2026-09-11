using System.Runtime.CompilerServices;

// 运行期程序集把内部类型开放给编辑器程序集：
// Psd2UIFormEditorHostAdapter 需要实现 internal 的 IPsd2UIFormEditorHost（其成员签名含 internal 类型，
// 例如 PsdTextStyleInfo），并需要访问 PsdLayerNode 的 internal 成员来完成生成期/导出期操作。
[assembly: InternalsVisibleTo("cn.efunstudio.psd2ugui.Editor")]
