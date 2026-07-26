namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using PhotoshopFile;
    using PsdLayoutTool2.Editor;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Immutable ownership information captured when the preview is opened.
    /// The request itself is retained only so Apply can compare the preview
    /// with a freshly rebuilt request before starting an import.
    /// </summary>
    public sealed class PsdHierarchyOrganizerInput
    {
        internal PsdHierarchyOrganizerInput(
            string sourcePsdPath,
            string sourcePsdGuid,
            string targetPrefabPath,
            PsdHierarchyRequest request,
            PsdHierarchyOrganizerPreviewModel previewModel)
        {
            this.sourcePsdPath = sourcePsdPath;
            this.sourcePsdGuid = sourcePsdGuid;
            this.targetPrefabPath = targetPrefabPath;
            this.request = request;
            this.previewModel = previewModel;
        }

        public string sourcePsdPath { get; private set; }
        public string sourcePsdGuid { get; private set; }
        public string targetPrefabPath { get; private set; }
        public PsdHierarchyOrganizerPreviewModel previewModel { get; private set; }
        internal PsdHierarchyRequest request { get; private set; }
    }

    /// <summary>
    /// Builds the organizer preview from read-only snapshots. Opening the
    /// window never saves or edits the target Prefab, Profile, or materials.
    /// </summary>
    public static class PsdHierarchyOrganizerEntry
    {
        public const string PreviewButtonLabel = "AI 层级整理";

        public static bool TryResolveAvailability(
            string psdAssetPath,
            PsdImporter.OutputDirectoryMode outputMode,
            string outputFolderName,
            PsdImporter.PrefabOutputMode prefabMode,
            bool useUnityUI,
            Func<string, bool> prefabExists,
            out string targetPrefabPath,
            out string explanation)
        {
            targetPrefabPath = string.Empty;
            explanation = string.Empty;
            if (!useUnityUI)
            {
                explanation = "AI 层级整理仅支持 Unity UI（Canvas）模式。";
                return false;
            }

            if (!PsdGeneratedPrefabPathResolver.TryResolve(
                    psdAssetPath, outputMode, outputFolderName, prefabMode, out targetPrefabPath))
            {
                explanation = "无法根据当前导出设置计算目标 Prefab 路径。";
                return false;
            }

            if (prefabExists == null) throw new ArgumentNullException("prefabExists");
            if (!prefabExists(targetPrefabPath))
            {
                explanation = "目标 Prefab 不存在：" + targetPrefabPath + "。请先点击生成预制体。";
                return false;
            }

            return true;
        }

        public static PsdHierarchyOrganizerInput BuildReadOnlyInput(
            string sourcePsdPath,
            string sourcePsdGuid,
            string targetPrefabPath,
            PsdPrefabDocumentModel document,
            IEnumerable<PsdHierarchyPrefabNodeMetadata> prefabMetadata,
            PsdHierarchyProfile persistedProfile,
            IPsdHierarchyAiRunner runner)
        {
            if (document == null) throw new ArgumentNullException("document");
            if (runner == null) throw new ArgumentNullException("runner");
            if (string.IsNullOrEmpty(sourcePsdGuid)) throw new ArgumentException("必须提供源 PSD GUID。", "sourcePsdGuid");

            PsdHierarchyProfile working = persistedProfile != null
                ? UnityEngine.Object.Instantiate(persistedProfile)
                : ScriptableObject.CreateInstance<PsdHierarchyProfile>();
            try
            {
                if (persistedProfile == null)
                {
                    working.sourcePsdGuid = sourcePsdGuid;
                }
                else if (!string.Equals(working.sourcePsdGuid, sourcePsdGuid, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("层级 Profile 属于其他 PSD。");
                }

                PsdHierarchyReconciliationResult reconciliation = working.Reconcile(document);
                PsdHierarchyPlan baseline = CreatePlan(working, sourcePsdGuid);
                PsdHierarchyRequest request = PsdHierarchyContextBuilder.Build(
                    document, prefabMetadata, sourcePsdGuid);
                var preview = new PsdHierarchyOrganizerPreviewModel(
                    targetPrefabPath, request, baseline, reconciliation, runner);
                return new PsdHierarchyOrganizerInput(
                    NormalizePath(sourcePsdPath), sourcePsdGuid, NormalizePath(targetPrefabPath), request, preview);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(working);
            }
        }

        public static PsdHierarchyOrganizerInput BuildFromAssets(
            string sourcePsdPath,
            string expectedTargetPrefabPath,
            IPsdHierarchyAiRunner runner)
        {
            PsdImporter.ApplyProjectOutputSettings(PsdLayoutProjectSettings.instance.ResolveOutputSettings());
            string resolvedTarget;
            string explanation;
            if (!TryResolveAvailability(
                    sourcePsdPath,
                    PsdImporter.OutputMode,
                    PsdImporter.OutputFolderName,
                    PsdImporter.PrefabMode,
                    PsdImporter.UseUnityUI,
                    path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null,
                    out resolvedTarget,
                    out explanation))
                throw new InvalidOperationException(explanation);
            if (!string.Equals(NormalizePath(expectedTargetPrefabPath), NormalizePath(resolvedTarget), StringComparison.Ordinal))
                throw new InvalidOperationException("打开层级预览后，配置的目标 Prefab 已发生变化。");

            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePsdPath);
            if (string.IsNullOrEmpty(sourceGuid))
                throw new InvalidOperationException("所选 PSD 没有 AssetDatabase GUID：" + sourcePsdPath);
            string fullSourcePath = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", sourcePsdPath.Replace('/', Path.DirectorySeparatorChar)));
            PsdPrefabDocumentModel document = PsdPrefabModelBuilder.Build(new PsdFile(fullSourcePath));
            string profilePath = PsdPrefabTransactionalSave.GetProfilePath(resolvedTarget, sourceGuid);
            PsdHierarchyProfile profile = PsdPrefabTransactionalSave.ResolveBoundProfileForImport(profilePath, resolvedTarget);
            IEnumerable<PsdHierarchyPrefabNodeMetadata> metadata = profile == null
                ? Enumerable.Empty<PsdHierarchyPrefabNodeMetadata>()
                : PsdPrefabIncrementalMerge.BuildProfilePrefabMetadata(resolvedTarget, profile);
            return BuildReadOnlyInput(
                sourcePsdPath, sourceGuid, resolvedTarget, document, metadata, profile, runner);
        }

        public static PsdHierarchyOrganizerWindow Open(string sourcePsdPath)
        {
            PsdImporter.ApplyProjectOutputSettings(PsdLayoutProjectSettings.instance.ResolveOutputSettings());
            string targetPath;
            string explanation;
            if (!TryResolveAvailability(
                    sourcePsdPath,
                    PsdImporter.OutputMode,
                    PsdImporter.OutputFolderName,
                    PsdImporter.PrefabMode,
                    PsdImporter.UseUnityUI,
                    path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null,
                    out targetPath,
                    out explanation))
                throw new InvalidOperationException(explanation);

            PsdHierarchyOrganizerInput input = BuildFromAssets(
                sourcePsdPath, targetPath, PsdHierarchyAiRunnerFactory.CreateConfigured());
            return PsdHierarchyOrganizerWindow.Open(
                input.previewModel,
                plan => PsdImporter.GeneratePrefabWithHierarchyPlan(
                    input.sourcePsdPath, input.sourcePsdGuid, input.targetPrefabPath, plan),
                input.sourcePsdPath);
        }

        private static PsdHierarchyPlan CreatePlan(PsdHierarchyProfile profile, string sourceGuid)
        {
            var plan = new PsdHierarchyPlan
            {
                schemaVersion = PsdHierarchyPlan.CurrentSchemaVersion,
                sourcePsdGuid = sourceGuid,
                sourceFingerprint = profile.sourceFingerprint ?? string.Empty,
                contentFingerprint = profile.sourceContentFingerprint ?? string.Empty,
                structureFingerprint = profile.sourceStructureFingerprint ?? string.Empty,
                geometryFingerprint = profile.sourceGeometryFingerprint ?? string.Empty
            };
            foreach (PsdHierarchyProfileGroup group in profile.groups ?? new List<PsdHierarchyProfileGroup>())
            {
                if (group == null) continue;
                plan.groups.Add(new PsdHierarchyPlanGroup
                {
                    key = group.key,
                    parentKey = group.parentKey,
                    displayName = group.displayName,
                    memberStableIds = new List<string>(group.stableLayerIds ?? new List<string>()),
                    evidence = "已保存且通过校验的层级 Profile",
                    confidence = 1d
                });
            }
            foreach (PsdHierarchyProfileRename rename in profile.renames ?? new List<PsdHierarchyProfileRename>())
            {
                if (rename == null) continue;
                plan.renames.Add(new PsdHierarchyPlanRename
                {
                    stableId = rename.stableId,
                    name = rename.name,
                    evidence = "已保存且通过校验的层级 Profile",
                    confidence = 1d
                });
            }
            return plan;
        }

        private static string NormalizePath(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/');
        }
    }

    /// <summary>
    /// Process-local handoff from the explicit preview Apply action to exactly
    /// one matching importer save. It is never persisted and mismatched imports
    /// cannot observe or consume it.
    /// </summary>
    public static class PsdHierarchyPendingOperation
    {
        private static string sourceGuid;
        private static string sourcePath;
        private static string targetPath;
        private static PsdHierarchyPlan plan;

        public static void Enqueue(string psdGuid, string psdPath, string prefabPath, PsdHierarchyPlan validatedPlan)
        {
            if (validatedPlan == null) throw new ArgumentNullException("validatedPlan");
            if (plan != null) throw new InvalidOperationException("已有一个层级应用操作正在等待处理。");
            sourceGuid = psdGuid ?? string.Empty;
            sourcePath = Normalize(psdPath);
            targetPath = Normalize(prefabPath);
            plan = ClonePlan(validatedPlan);
        }

        public static bool HasMatch(string psdGuid, string prefabPath)
        {
            return plan != null &&
                   string.Equals(sourceGuid, psdGuid ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(targetPath, Normalize(prefabPath), StringComparison.Ordinal);
        }

        public static bool TryTake(string psdGuid, string prefabPath, out PsdHierarchyPlan validatedPlan)
        {
            validatedPlan = null;
            if (!HasMatch(psdGuid, prefabPath)) return false;
            validatedPlan = plan;
            Clear();
            return true;
        }

        public static void Clear()
        {
            sourceGuid = null;
            sourcePath = null;
            targetPath = null;
            plan = null;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/');
        }

        private static PsdHierarchyPlan ClonePlan(PsdHierarchyPlan source)
        {
            var clone = new PsdHierarchyPlan
            {
                schemaVersion = source.schemaVersion,
                sourcePsdGuid = source.sourcePsdGuid,
                sourceFingerprint = source.sourceFingerprint,
                contentFingerprint = source.contentFingerprint,
                structureFingerprint = source.structureFingerprint,
                geometryFingerprint = source.geometryFingerprint
            };
            foreach (PsdHierarchyPlanGroup group in source.groups ?? new List<PsdHierarchyPlanGroup>())
            {
                if (group == null) continue;
                clone.groups.Add(new PsdHierarchyPlanGroup
                {
                    key = group.key,
                    parentKey = group.parentKey,
                    memberStableIds = new List<string>(group.memberStableIds ?? new List<string>()),
                    displayName = group.displayName,
                    evidence = group.evidence,
                    confidence = group.confidence
                });
            }
            foreach (PsdHierarchyPlanRename rename in source.renames ?? new List<PsdHierarchyPlanRename>())
            {
                if (rename == null) continue;
                clone.renames.Add(new PsdHierarchyPlanRename
                {
                    stableId = rename.stableId,
                    name = rename.name,
                    evidence = rename.evidence,
                    confidence = rename.confidence
                });
            }
            return clone;
        }
    }
}
