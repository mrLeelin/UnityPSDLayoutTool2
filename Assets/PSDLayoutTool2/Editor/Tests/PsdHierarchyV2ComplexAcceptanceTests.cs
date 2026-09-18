namespace PsdLayoutTool2.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// 10：跨入口组合验收。终端会话完成「层级整理 + 私有资源改名 + 分组后组件抽取」的完整流程，
    /// 再对保存下来的 v2 Profile 做 PSD 更新后的重放，两个阶段都做真实资产核验。
    /// </summary>
    public sealed class PsdHierarchyV2ComplexAcceptanceTests
    {
        private const string Folder = "Assets/__PsdV2AcceptanceTests";
        private const string SourceAssetPath = Folder + "/Source.txt";
        private const string OriginalTexturePath = Folder + "/Texture/RowBackground.png";
        private const string RenamedTexturePath = Folder + "/Texture/Source_RowBackground.png";
        private const string ComponentPath = Folder + "/Common/TaskItem.prefab";
        private const string CandidatePath = Folder + "/Regenerated/Source.prefab";
        private string terminalDirectory;
        private string sessionId;
        private string targetPrefabPath;
        private string snapshotJson;
        private PsdImporter.OutputDirectoryMode originalOutputMode;
        private PsdImporter.PrefabOutputMode originalPrefabMode;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False);
            AssetDatabase.CreateFolder("Assets", "__PsdV2AcceptanceTests");
            File.WriteAllText(ToFullPath(SourceAssetPath), "psd placeholder", new UTF8Encoding(false));
            AssetDatabase.ImportAsset(SourceAssetPath, ImportAssetOptions.ForceSynchronousImport);
            CreateTexture(OriginalTexturePath);

            originalOutputMode = PsdImporter.OutputMode;
            originalPrefabMode = PsdImporter.PrefabMode;
            PsdImporter.OutputMode = PsdImporter.OutputDirectoryMode.PsdDirectory;
            PsdImporter.PrefabMode = PsdImporter.PrefabOutputMode.SiblingToOutputFolder;
            Assert.That(
                PsdGeneratedPrefabPathResolver.TryResolve(
                    SourceAssetPath,
                    PsdImporter.OutputMode,
                    PsdImporter.OutputFolderName,
                    PsdImporter.FixedOutputPath,
                    PsdImporter.PrefabOutputPath,
                    PsdImporter.PrefabMode,
                    out targetPrefabPath),
                Is.True);
            EnsureAssetFolder(Path.GetDirectoryName(targetPrefabPath).Replace('\\', '/'));
            BuildPrefab(targetPrefabPath, includeExtraHeaderChild: false);
            Assert.That(
                PsdHierarchyChatContextBuilder.TryBuildSnapshotForPrefab(
                    targetPrefabPath,
                    out snapshotJson,
                    out _,
                    out string snapshotError),
                Is.True,
                snapshotError);

            terminalDirectory = PsdHierarchyTerminalApplyWatcher.TerminalDirectoryPath;
            Assert.That(terminalDirectory, Is.Not.Null.And.Not.Empty);
            Directory.CreateDirectory(terminalDirectory);
            sessionId = "acceptance-" + Guid.NewGuid().ToString("N");
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(sessionId) && Directory.Exists(terminalDirectory))
            {
                foreach (string file in Directory.GetFiles(terminalDirectory, sessionId + "*"))
                {
                    File.Delete(file);
                }
            }

            sessionId = null;
            PsdImporter.OutputMode = originalOutputMode;
            PsdImporter.PrefabMode = originalPrefabMode;
            PsdHierarchyCleanupReplayProfile.Remove(SourceAssetPath, targetPrefabPath);
            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        [Test]
        public async System.Threading.Tasks.Task TerminalStageThenUpdatedPsdReplayKeepsExtractionRenamesAndNewContent()
        {
            // ---- 阶段一：终端哨兵执行「分组 + 改名 + 分组后抽取」 ----
            JObject plan = BuildInitialPlan();
            WriteTerminalSession();
            File.WriteAllText(
                PlanPath(),
                plan.ToString(Newtonsoft.Json.Formatting.None),
                new UTF8Encoding(false));
            File.WriteAllText(ApplyPath(), string.Empty, new UTF8Encoding(false));

            Assert.That(
                await PsdHierarchyTerminalApplyWatcher.RunPendingApplyOnceForTestsAsync(),
                Is.True);
            JObject receipt = JObject.Parse(File.ReadAllText(ResultPath(), Encoding.UTF8));
            Assert.That(receipt.Value<bool>("success"), Is.True, receipt.ToString());
            Assert.That(
                receipt.Value<string>("status"),
                Is.EqualTo(PsdHierarchyTerminalApplyWatcher.StatusApplied));

            AssertOrganizedTarget(targetPrefabPath);

            // ---- 模拟 PSD 更新后重新生成：纹理回到原始名字、候选多出 Header 子节点 ----
            Assert.That(
                string.IsNullOrEmpty(AssetDatabase.MoveAsset(RenamedTexturePath, OriginalTexturePath)),
                Is.True,
                "模拟重新生成时，导出纹理应回到 PSD 的原始名字。");
            EnsureAssetFolder(Path.GetDirectoryName(CandidatePath).Replace('\\', '/'));
            BuildPrefab(CandidatePath, includeExtraHeaderChild: true);

            // ---- 阶段二：v2 Profile 重放（含抽取与改名），业务 Prefab 与本阶段均为真实写入 ----
            string sourceGuid = AssetDatabase.AssetPathToGUID(SourceAssetPath);
            PsdHierarchyCleanupReplayProfile profile =
                PsdHierarchyCleanupReplayProfile.Load(targetPrefabPath, sourceGuid);
            Assert.That(profile, Is.Not.Null, "终端应用必须保存可重放的 v2 Profile。");
            Assert.That(
                profile.TryBuildReplayPlans(
                    sourceGuid,
                    targetPrefabPath,
                    CandidatePath,
                    out System.Collections.Generic.IReadOnlyList<string> stages,
                    out string replayError),
                Is.True,
                replayError);
            Assert.That(stages, Has.Count.EqualTo(1));

            var replayed = await PsdHierarchyChatCleanupExecution.ReapplyPersistedPlanAsync(
                Directory.GetParent(Application.dataPath).FullName,
                stages[0]);
            Assert.That(replayed.success, Is.True, replayed.message);

            AssertOrganizedTarget(CandidatePath, expectExtraHeaderChild: true);
        }

        private void AssertOrganizedTarget(string prefabPath, bool expectExtraHeaderChild = false)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform header = root.transform.Find("Header");
                Assert.That(header, Is.Not.Null, "原有内容必须保留：" + prefabPath);
                Assert.That(
                    header.childCount,
                    Is.EqualTo(expectExtraHeaderChild ? 1 : 0),
                    "PSD 新增内容必须保留，原有内容不得被丢弃。");
                Transform rows = root.transform.Find("[Rows]");
                Assert.That(rows, Is.Not.Null, "分组容器必须存在：" + prefabPath);
                Assert.That(rows.childCount, Is.EqualTo(3));
                for (int index = 0; index < 3; index++)
                {
                    Transform row = rows.GetChild(index);
                    Assert.That(row.name, Is.EqualTo("TaskItem_" + (index + 1)));
                    Assert.That(
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(row.gameObject),
                        Is.EqualTo(ComponentPath),
                        "共享组件抽取必须完成：" + prefabPath);
                    Image image = row.Find("RowBackground")?.GetComponent<Image>();
                    Assert.That(image, Is.Not.Null, "每行必须有背景图：" + prefabPath);
                    Assert.That(image.sprite, Is.Not.Null, "改名后引用必须仍然有效：" + prefabPath);
                    Assert.That(
                        AssetDatabase.GetAssetPath(image.sprite),
                        Is.EqualTo(RenamedTexturePath),
                        "私有资源改名必须生效且引用保持：" + prefabPath);
                }

                Assert.That(
                    AssetDatabase.LoadAssetAtPath<GameObject>(ComponentPath),
                    Is.Not.Null,
                    "抽取出的共享组件资产必须存在。");
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<Texture2D>(OriginalTexturePath),
                    Is.Null,
                    "重放后原始纹理名不应再存在：" + prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private JObject BuildInitialPlan()
        {
            var snapshot = JObject.Parse(snapshotJson);
            string rootId = ReadNodeId(snapshot, "Source");
            string[] rowIds =
            {
                ReadNodeId(snapshot, "Source/TaskItem_1"),
                ReadNodeId(snapshot, "Source/TaskItem_2"),
                ReadNodeId(snapshot, "Source/TaskItem_3"),
            };

            // 三个完全相同的行是强制抽取候选：本轮无法抽取的源必须延期到已审阅的分组后阶段。
            JObject candidate = ReadMandatoryCandidate(snapshot);
            Assert.That(candidate, Is.Not.Null, "该夹具必须产生 requiresExtraction=true 的候选。");

            var instances = new JArray();
            for (int index = 1; index <= 3; index++)
            {
                instances.Add(new JObject
                {
                    ["path"] = "Source/[Rows]/TaskItem_" + index,
                    ["state"] = string.Empty,
                    ["commonSourceNames"] = new JArray(),
                    ["stateSourceNames"] = new JArray(),
                });
            }

            return new JObject
            {
                ["version"] = 2,
                ["snapshotFingerprint"] = snapshot.Value<string>("fingerprint"),
                ["prefabName"] = "Source",
                ["prefabAssetPath"] = targetPrefabPath,
                ["output"] = new JObject { ["mode"] = "in_place", ["assetPath"] = targetPrefabPath },
                ["verify"] = new JObject(),
                ["wrappers"] = new JArray(new JObject
                {
                    ["id"] = "rows",
                    ["parent"] = "node:" + rootId,
                    ["name"] = "[Rows]",
                    ["siblingIndex"] = 0,
                }),
                ["moves"] = new JArray(
                    new JObject { ["source"] = "node:" + rowIds[0], ["destination"] = "@rows", ["siblingIndex"] = 0 },
                    new JObject { ["source"] = "node:" + rowIds[1], ["destination"] = "@rows", ["siblingIndex"] = 1 },
                    new JObject { ["source"] = "node:" + rowIds[2], ["destination"] = "@rows", ["siblingIndex"] = 2 }),
                ["renames"] = new JArray(),
                ["tightBounds"] = new JArray(),
                ["emptyContainerRemovals"] = new JArray(),
                ["textureRenames"] = new JArray(new JObject
                {
                    ["from"] = OriginalTexturePath,
                    ["toName"] = "Source_RowBackground",
                    ["expectedGuid"] = string.Empty,
                }),
                ["spriteAtlasRenames"] = new JArray(),
                ["componentFamilyDecisions"] = new JArray(new JObject
                {
                    ["candidateId"] = candidate.Value<string>("id"),
                    ["parent"] = candidate.Value<string>("parent"),
                    ["sources"] = candidate["sources"].DeepClone(),
                    ["mode"] = "component",
                    ["extractionId"] = "task_item",
                }),
                ["containmentResolutions"] = new JArray(),
                ["flatSiblingResolutions"] = new JArray(),
                ["componentExtractions"] = new JArray(),
                ["stateComponentExtractions"] = new JArray(),
                ["variantComponentExtractions"] = new JArray(),
                ["statefulComponentExtractions"] = new JArray(),
                ["postGroupingExtractionIntents"] = new JArray(new JObject
                {
                    ["id"] = "task_item",
                    ["mode"] = "component",
                    ["assetPath"] = ComponentPath,
                    ["templatePath"] = "Source/[Rows]/TaskItem_1",
                    ["commonMembers"] = new JArray(),
                    ["instances"] = instances,
                    ["states"] = new JArray(),
                    ["defaultState"] = string.Empty,
                }),
            };
        }

        private void BuildPrefab(string assetPath, bool includeExtraHeaderChild)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(OriginalTexturePath);
            Assert.That(sprite, Is.Not.Null, "测试纹理必须作为 Sprite 导入：" + OriginalTexturePath);

            var root = new GameObject("Source", typeof(RectTransform));
            try
            {
                var header = new GameObject("Header", typeof(RectTransform), typeof(Text));
                header.transform.SetParent(root.transform, false);
                header.GetComponent<Text>().text = "Title";
                header.GetComponent<Text>().font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (includeExtraHeaderChild)
                {
                    var subtitle = new GameObject("Subtitle", typeof(RectTransform), typeof(Text));
                    subtitle.transform.SetParent(header.transform, false);
                    subtitle.GetComponent<Text>().text = "New from PSD";
                    subtitle.GetComponent<Text>().font =
                        Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                for (int index = 1; index <= 3; index++)
                {
                    // 三个结构完全相同的行：必须被判定为强制抽取候选。
                    var row = new GameObject("TaskItem_" + index, typeof(RectTransform));
                    row.transform.SetParent(root.transform, false);
                    var background = new GameObject(
                        "RowBackground", typeof(RectTransform), typeof(Image));
                    background.transform.SetParent(row.transform, false);
                    background.GetComponent<Image>().sprite = sprite;
                    var label = new GameObject("TaskLabel", typeof(RectTransform), typeof(Text));
                    label.transform.SetParent(row.transform, false);
                    Text text = label.GetComponent<Text>();
                    text.text = "Row " + index;
                    text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, assetPath), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateTexture(string assetPath)
        {
            EnsureAssetFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            try
            {
                for (int y = 0; y < texture.height; y++)
                {
                    for (int x = 0; x < texture.width; x++)
                    {
                        texture.SetPixel(x, y, new Color(0.2f, 0.4f, 0.8f, 1f));
                    }
                }

                texture.Apply();
                File.WriteAllBytes(ToFullPath(assetPath), texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            Assert.That(importer, Is.Not.Null, "纹理必须导入成功：" + assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        private static JObject ReadMandatoryCandidate(JObject snapshot)
        {
            foreach (JToken token in snapshot["componentFamilyCandidates"] as JArray ?? new JArray())
            {
                if (token is JObject candidate && (candidate.Value<bool?>("requiresExtraction") ?? false))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string ReadNodeId(JObject snapshot, string path)
        {
            foreach (JToken token in (JArray)snapshot["nodes"])
            {
                if (token.Value<string>("path") == path)
                {
                    return token.Value<string>("id");
                }
            }

            Assert.Fail("快照中找不到节点路径：" + path);
            return string.Empty;
        }

        private void WriteTerminalSession()
        {
            PsdHierarchyOrganizerEntry.WriteTerminalSession(
                sessionId,
                SourceAssetPath,
                targetPrefabPath,
                PlanPath(),
                Path.Combine(terminalDirectory, sessionId + ".review.md"));
        }

        private string ApplyPath()
        {
            return Path.Combine(terminalDirectory, sessionId + ".apply");
        }

        private string PlanPath()
        {
            return Path.Combine(terminalDirectory, sessionId + ".plan.json");
        }

        private string ResultPath()
        {
            return Path.Combine(terminalDirectory, sessionId + ".apply-result.json");
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder))
            {
                return;
            }

            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static string ToFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
