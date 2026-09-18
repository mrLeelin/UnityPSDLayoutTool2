namespace PsdLayoutTool2.Tests
{
    using System.IO;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// 05：私有资源改名（textureRenames / spriteAtlasRenames）。
    /// 预检只校验身份、所有权与目标冲突；Apply 改名并保持 GUID，Prefab 引用继续有效。
    /// </summary>
    public sealed class PsdHierarchyV2AssetRenameTests
    {
        private const string Folder = "Assets/__PsdV2AssetRenameTests";
        private const string Target = Folder + "/ExampleView.prefab";
        private const string TextureFolder = Folder + "/Texture";
        private const string AtlasFolder = Folder + "/Atlas";
        private const string TexturePath = TextureFolder + "/old_background.png";
        private const string AtlasPath = AtlasFolder + "/old_atlas.spriteatlas";
        private const string RenamedTexturePath = TextureFolder + "/ExampleView_Background.png";
        private const string RenamedAtlasPath = AtlasFolder + "/ExampleView.spriteatlas";
        private const string SharedTextureFolder = "Assets/__PsdV2AssetRenameShared";
        private const string SharedTexturePath = SharedTextureFolder + "/shared_icon.png";

        private PsdHierarchyChatContext context;
        private bool ownsFolder;
        private bool ownsSharedFolder;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False);
            AssetDatabase.CreateFolder("Assets", "__PsdV2AssetRenameTests");
            AssetDatabase.CreateFolder(Folder, "Texture");
            AssetDatabase.CreateFolder(Folder, "Atlas");
            ownsFolder = true;
            BuildFixture();
        }

        [TearDown]
        public void TearDown()
        {
            if (ownsFolder)
            {
                AssetDatabase.DeleteAsset(Folder);
            }

            if (ownsSharedFolder)
            {
                AssetDatabase.DeleteAsset(SharedTextureFolder);
            }
        }

        private static void CreateTexture(string assetPath)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels(new[]
                {
                    Color.green, Color.green, Color.green, Color.green,
                    Color.green, Color.green, Color.green, Color.green,
                    Color.green, Color.green, Color.green, Color.green,
                    Color.green, Color.green, Color.green, Color.green,
                });
                texture.Apply();
                File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            Assert.That(importer, Is.Not.Null, "纹理导入器缺失：" + assetPath);
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        private void BuildFixture()
        {
            CreateTexture(TexturePath);
            var loadedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            Assert.That(loadedTexture, Is.Not.Null, "纹理必须能加载：" + TexturePath);

            var atlas = new UnityEngine.U2D.SpriteAtlas();
            AssetDatabase.CreateAsset(atlas, AtlasPath);
            AssetDatabase.SaveAssets();

            var root = new GameObject("ExampleView", typeof(RectTransform));
            try
            {
                var background = new GameObject("Background", typeof(RectTransform), typeof(RawImage));
                background.transform.SetParent(root.transform, false);
                background.GetComponent<RawImage>().texture = loadedTexture;
                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, Target), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            RefreshContext();
        }

        private void RefreshContext()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            context = new PsdHierarchyChatContext(project, "", Target, "", "", "",
                hierarchySnapshotJson: new JObject
                {
                    ["nodes"] = new JArray(
                        new JObject { ["id"] = "root", ["path"] = "ExampleView" },
                        new JObject { ["id"] = "background", ["path"] = "ExampleView/Background", ["parentId"] = "root" }),
                }.ToString(),
                hierarchySnapshotFingerprint: PsdHierarchyChatContextBuilder.ComputeFileFingerprint(
                    Path.Combine(project, Target)));
        }

        private JObject Plan(
            string textureToName = "ExampleView_Background",
            string expectedGuid = "",
            string textureFrom = TexturePath,
            string atlasFrom = AtlasPath)
        {
            var plan = new JObject
            {
                ["version"] = 2,
                ["snapshotFingerprint"] = context.hierarchySnapshotFingerprint,
                ["prefabName"] = "ExampleView",
                ["prefabAssetPath"] = Target,
                ["output"] = new JObject { ["mode"] = "in_place", ["assetPath"] = Target },
                ["verify"] = new JObject(),
                ["textureRenames"] = new JArray(new JObject
                {
                    ["from"] = textureFrom,
                    ["toName"] = textureToName,
                    ["expectedGuid"] = expectedGuid,
                }),
                ["spriteAtlasRenames"] = atlasFrom == null
                    ? new JArray()
                    : new JArray(new JObject
                    {
                        ["from"] = atlasFrom,
                        ["toName"] = "ExampleView",
                        ["expectedGuid"] = string.Empty,
                    }),
            };
            foreach (string key in new[]
                     {
                         "wrappers", "moves", "renames", "tightBounds", "emptyContainerRemovals",
                         "componentFamilyDecisions", "containmentResolutions", "flatSiblingResolutions",
                         "componentExtractions", "stateComponentExtractions", "variantComponentExtractions",
                         "statefulComponentExtractions", "selectedPrefabExtractions",
                         "crossParentPrefabExtractions", "postGroupingExtractionIntents",
                     })
            {
                plan[key] = new JArray();
            }

            return plan;
        }

        [Test]
        public async Task PrivateTextureAndAtlasAreRenamedWithIdentityAndReferencesPreserved()
        {
            JObject plan = Plan();
            string textureGuid = AssetDatabase.AssetPathToGUID(TexturePath);
            string atlasGuid = AssetDatabase.AssetPathToGUID(AtlasPath);
            byte[] prefabBefore = File.ReadAllBytes(Target);

            var preflight = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());
            Assert.That(preflight.success, Is.True, preflight.message);
            Assert.That(File.Exists(RenamedTexturePath), Is.False, "预检不得改名。");
            Assert.That(File.Exists(RenamedAtlasPath), Is.False, "预检不得改名。");
            Assert.That(File.ReadAllBytes(Target), Is.EqualTo(prefabBefore), "预检不得保存 Prefab。");

            var result = await PsdHierarchyNativeCleanupExecutor.ApplyAsync(context, plan.ToString());
            Assert.That(result.success, Is.True, result.message);

            // 纹理：新路径存在、旧路径消失、GUID 与导入设置保持不变。
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(RenamedTexturePath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath), Is.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(RenamedTexturePath), Is.EqualTo(textureGuid));
            Assert.That(((TextureImporter)AssetImporter.GetAtPath(RenamedTexturePath)).isReadable, Is.True);

            // 图集：同样保持身份。
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(RenamedAtlasPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(AtlasPath), Is.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(RenamedAtlasPath), Is.EqualTo(atlasGuid));

            // Prefab 的图片引用必须仍然有效，并指向改名后的资源。
            GameObject root = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                RawImage image = root.transform.Find("Background").GetComponent<RawImage>();
                Assert.That(image.texture, Is.Not.Null, "改名后 Prefab 的纹理引用不能失效。");
                Assert.That(AssetDatabase.GetAssetPath(image.texture), Is.EqualTo(RenamedTexturePath));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public async Task MissingSourceAssetIsRejectedBeforeAnyWrite()
        {
            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(
                context, Plan(textureFrom: TextureFolder + "/missing.png").ToString());

            Assert.That(result.success, Is.False);
            Assert.That(File.Exists(RenamedTexturePath), Is.False);
        }

        [Test]
        public async Task GuidMismatchIsRejectedBeforeAnyWrite()
        {
            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(
                context, Plan(expectedGuid: "00000000000000000000000000000000").ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("identity changed"));
            Assert.That(File.Exists(RenamedTexturePath), Is.False);
        }

        [Test]
        public async Task TargetConflictIsRejectedBeforeAnyWrite()
        {
            CreateTexture(RenamedTexturePath);

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, Plan().ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("already exists"));
        }

        [Test]
        public async Task TextureNameWithoutPrefabPrefixIsRejectedBeforeAnyWrite()
        {
            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(
                context, Plan(textureToName: "Background").ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("must start with"));
            Assert.That(File.Exists(RenamedTexturePath), Is.False);
        }

        [Test]
        public async Task TextureOutsideThePrefabDependenciesIsRejectedBeforeAnyWrite()
        {
            Assert.That(AssetDatabase.IsValidFolder(SharedTextureFolder), Is.False);
            AssetDatabase.CreateFolder("Assets", "__PsdV2AssetRenameShared");
            ownsSharedFolder = true;
            CreateTexture(SharedTexturePath);

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(
                context,
                Plan(textureFrom: SharedTexturePath, atlasFrom: null).ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("not referenced by the current target Prefab"));
            Assert.That(File.Exists(SharedTextureFolder + "/ExampleView_Shared_icon.png"), Is.False);
        }
    }
}
