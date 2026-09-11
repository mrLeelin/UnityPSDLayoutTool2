using System.IO;
using AiPathUtilityNamespace;
using NUnit.Framework;
using Psd2UIFormPluginPathResolverNamespace;
using UnityEditor;
using UnityEngine;

namespace Psd2UIForm.Tests
{
    /// <summary>
    /// 锁住"插件根解析"。它曾因拆分而静默失效：
    /// Psd2UIFormPluginPathResolver.GetPluginAssetRoot() 里有一道按旧程序集名
    /// "cn.efunstudio.psd2ugui" 做的等值判断，而拆分后 UGUIParser 搬到了
    /// "cn.efunstudio.psd2ugui.Editor"，于是 TryResolveFromScript 被短路跳过，
    /// 插件根返回空串 -> 找不到 AIPrompts/TaskPrompt.md -> AI 提示词被写成 0 字节 ->
    /// CLI 报 "AI prompt template is missing or empty"。
    /// </summary>
    public class PluginPathResolutionTests
    {
        [Test]
        public void PluginRoot_ResolvesToFolderContainingAIPrompts()
        {
            string root = Psd2UIFormPluginPathResolver.GetPluginAssetRoot();

            Assert.That(string.IsNullOrWhiteSpace(root), Is.False,
                "插件根不能为空：为空会导致 AI 提示词模板解析失败并写出 0 字节 prompt");
            Assert.That(AssetDatabase.IsValidFolder(root + "/AIPrompts"), Is.True,
                "插件根下应当有 AIPrompts 目录，实际解析为：" + root);
        }

        [Test]
        public void AiPromptTemplate_IsResolvableAndNonEmpty()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string promptPath = AiPathUtility.ResolvePluginRelativePath(projectRoot, "AIPrompts/TaskPrompt.md");

            Assert.That(string.IsNullOrWhiteSpace(promptPath), Is.False, "AI 提示词模板路径解析为空");
            Assert.That(File.Exists(promptPath), Is.True, "AI 提示词模板文件不存在：" + promptPath);
            Assert.That(new FileInfo(promptPath).Length, Is.GreaterThan(0), "AI 提示词模板为空文件：" + promptPath);
        }
    }
}
