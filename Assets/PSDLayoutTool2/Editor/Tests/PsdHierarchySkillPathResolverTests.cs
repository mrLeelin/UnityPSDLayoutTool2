namespace PsdLayoutTool2.Tests
{
    using System;
    using System.IO;
    using NUnit.Framework;

    public sealed class PsdHierarchySkillPathResolverTests
    {
        [Test]
        public void FindsSkillBesidePackageWhenPackageIsNestedUnderAssets()
        {
            string projectRoot = Path.Combine(Path.GetTempPath(), "PsdHierarchySkillPathTests", Guid.NewGuid().ToString("N"));
            string packageRoot = Path.Combine(projectRoot, "Assets", "RenamedPsdTool");
            string scriptAssetPath =
                "Assets/RenamedPsdTool/Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyChatClient.cs";
            string expectedSkillPath = Path.Combine(
                packageRoot,
                ".agents",
                "skills",
                "prefab-hierarchy-cleanup",
                "SKILL.md");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(expectedSkillPath));
                File.WriteAllText(expectedSkillPath, "skill");

                bool resolved = PsdHierarchyChatContextBuilder.TryResolvePackageFilePath(
                    projectRoot,
                    scriptAssetPath,
                    ".agents/skills/prefab-hierarchy-cleanup/SKILL.md",
                    out string skillPath);

                Assert.That(resolved, Is.True);
                Assert.That(skillPath, Is.EqualTo(Path.GetFullPath(expectedSkillPath)));
            }
            finally
            {
                if (Directory.Exists(projectRoot))
                {
                    Directory.Delete(projectRoot, true);
                }
            }
        }
    }
}
