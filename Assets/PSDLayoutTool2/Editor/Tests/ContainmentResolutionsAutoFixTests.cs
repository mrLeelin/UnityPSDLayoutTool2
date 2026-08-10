namespace PsdLayoutTool2.Tests
{
    using System;
    using System.IO;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    /// <summary>
    /// 验证 containmentResolutions 自动修复逻辑
    /// </summary>
    public sealed class ContainmentResolutionsAutoFixTests
    {
        private const string TestSnapshotPath =
            "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/57e5a61f71300531b0f0a2821593c239f1528ba6c53e8c6a7b22a8ac34eb08fb.json";

        [Test]
        public void SnapshotContainsContainmentFindings()
        {
            if (!File.Exists(TestSnapshotPath))
            {
                Assert.Ignore("测试快照不存在");
                return;
            }

            string json = File.ReadAllText(TestSnapshotPath);
            JObject snapshot = JObject.Parse(json);
            JArray findings = snapshot["containmentFindings"] as JArray;

            Assert.That(findings, Is.Not.Null, "快照应该包含 containmentFindings");
            Assert.That(findings.Count, Is.GreaterThan(0), "应该至少有一个 containment finding");

            // 验证 finding 结构
            JObject firstFinding = findings[0] as JObject;
            Assert.That(firstFinding, Is.Not.Null);
            Assert.That(firstFinding["mapping"], Is.Not.Null);

            JArray mapping = firstFinding["mapping"] as JArray;
            Assert.That(mapping.Count, Is.EqualTo(3), "应该有 3 个需要解决的 containment sources");

            // 记录需要解决的 sources
            UnityEngine.Debug.Log("=== 需要解决的 Containment Sources ===");
            foreach (JObject pair in mapping)
            {
                string source = pair.Value<string>("source");
                string containedBy = pair.Value<string>("containedBy");
                UnityEngine.Debug.Log($"  - {source} (位于 {containedBy} 内)");
            }
        }

        [Test]
        public void NormalizeGeometricContainmentResolutionsAddsRequiredResolutions()
        {
            // 创建一个模拟的 plan（没有 containmentResolutions）
            var plan = new JObject
            {
                ["version"] = 2,
                ["snapshotFingerprint"] = "test",
                ["prefabAssetPath"] = "Assets/Test.prefab"
            };

            // 创建模拟的 context
            var containmentFindings = new JArray
            {
                new JObject
                {
                    ["innerParent"] = "node:n000001",
                    ["innerCandidateId"] = "test_001",
                    ["maxAreaRatio"] = 0.046,
                    ["mapping"] = new JArray
                    {
                        new JObject
                        {
                            ["source"] = "node:n000003",
                            ["containedBy"] = "node:n000017"
                        },
                        new JObject
                        {
                            ["source"] = "node:n000006",
                            ["containedBy"] = "node:n000023"
                        },
                        new JObject
                        {
                            ["source"] = "node:n000009",
                            ["containedBy"] = "node:n000020"
                        }
                    }
                }
            };

            // 模拟 context
            var context = CreateMockContext(containmentFindings);

            // 调用我们的修复方法（使用反射）
            var method = typeof(PsdHierarchyChatCleanupExecution).GetMethod(
                "NormalizeGeometricContainmentResolutions",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            Assert.That(method, Is.Not.Null, "应该能找到 NormalizeGeometricContainmentResolutions 方法");

            method.Invoke(null, new object[] { plan, context });

            // 验证结果
            JArray resolutions = plan["containmentResolutions"] as JArray;
            Assert.That(resolutions, Is.Not.Null, "应该生成 containmentResolutions");
            Assert.That(resolutions.Count, Is.EqualTo(3), "应该为 3 个 sources 生成 resolutions");

            // 验证每个 resolution 的结构
            foreach (JObject resolution in resolutions)
            {
                Assert.That(resolution["source"], Is.Not.Null, "resolution 应该有 source");
                Assert.That(resolution["mode"], Is.Not.Null, "resolution 应该有 mode");
                Assert.That(resolution.Value<string>("mode"), Is.EqualTo("keep"), "mode 应该是 keep");
                Assert.That(resolution["evidence"], Is.Not.Null, "resolution 应该有 evidence");

                string evidence = resolution.Value<string>("evidence");
                Assert.That(evidence.Length, Is.GreaterThanOrEqualTo(20), "evidence 应该至少 20 个字符");

                UnityEngine.Debug.Log($"生成的 resolution: source={resolution.Value<string>("source")}, mode={resolution.Value<string>("mode")}");
            }
        }

        private PsdHierarchyChatContext CreateMockContext(JArray containmentFindings)
        {
            // 使用反射创建 context
            var contextType = typeof(PsdHierarchyChatContext);
            var constructor = contextType.GetConstructor(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                new Type[]
                {
                    typeof(string), typeof(string), typeof(string), typeof(string),
                    typeof(JArray), typeof(JArray), typeof(JArray), typeof(PsdHierarchyLocalRepairScope)
                },
                null);

            if (constructor == null)
            {
                Assert.Fail("无法找到 PsdHierarchyChatContext 构造函数");
                return null;
            }

            return (PsdHierarchyChatContext)constructor.Invoke(new object[]
            {
                "E:/Project/Demo/monsterhunter/Assets/UnityPSDLayoutTool2",
                "Assets/Test.prefab",
                "test_fingerprint",
                "{}",
                new JArray(), // requiredComponentFamilies
                containmentFindings,
                new JArray(), // flatSiblingFindings
                null // localRepairScope
            });
        }
    }
}
