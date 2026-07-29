namespace PsdLayoutTool2
{
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEditorInternal;
    using UnityEngine;

    /// <summary>
    /// 管理适用于 UPM 的配置文件生命周期。
    /// 包内容视为只读，首次使用时将内置模板复制到使用方项目的 Assets 目录，后续包更新不得覆盖项目副本。
    /// </summary>
    internal static class PsdLayoutProjectSettingsAsset
    {
        internal const string ProjectAssetPath =
            "Assets/PSDLayoutTool2Settings/PsdLayoutProjectSettings.asset";

        // 包内默认模板的固定 GUID。
        // 通过 GUID 查找可兼容 Registry、Git、嵌入式以及直接放入 Assets 的安装方式。
        internal const string TemplateAssetGuid = "d4e1f950cc604fac96ef82b30dfa04b6";

        internal static PsdLayoutProjectSettings GetOrCreate()
        {
            PsdLayoutProjectSettings existing =
                AssetDatabase.LoadAssetAtPath<PsdLayoutProjectSettings>(ProjectAssetPath);
            if (existing != null)
            {
                return existing;
            }

            string templatePath = AssetDatabase.GUIDToAssetPath(TemplateAssetGuid);
            if (string.IsNullOrEmpty(templatePath))
            {
                throw new InvalidOperationException(
                    "已安装的 UPM 包中缺少 PSD Layout Tool 默认配置模板。");
            }

            return EnsureAtPath(ProjectAssetPath, templatePath, ReadLegacyProjectSettings());
        }

        internal static PsdLayoutProjectSettings EnsureAtPath(
            string projectAssetPath,
            string templateAssetPath,
            PsdLayoutProjectSettingsMigrationSnapshot migration)
        {
            PsdLayoutProjectSettings existing =
                AssetDatabase.LoadAssetAtPath<PsdLayoutProjectSettings>(projectAssetPath);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(Path.GetDirectoryName(projectAssetPath).Replace('\\', '/'));
            if (!AssetDatabase.CopyAsset(templateAssetPath, projectAssetPath))
            {
                throw new InvalidOperationException(
                    "无法将 PSD Layout Tool 默认配置模板复制到：" + projectAssetPath);
            }

            AssetDatabase.ImportAsset(projectAssetPath, ImportAssetOptions.ForceSynchronousImport);
            PsdLayoutProjectSettings created =
                AssetDatabase.LoadAssetAtPath<PsdLayoutProjectSettings>(projectAssetPath);
            if (created == null)
            {
                throw new InvalidOperationException(
                    "无法加载复制后的 PSD Layout Tool 项目配置：" + projectAssetPath);
            }

            created.ApplyMigration(migration);
            return created;
        }

        internal static PsdLayoutProjectSettings OpenInInspector()
        {
            PsdLayoutProjectSettings settings = GetOrCreate();
            //点击按钮的时候不需要Ping
            //Selection.activeObject = settings;
            PsdLayoutProjectSettingsWindow.Open(settings);
            return settings;
        }

        private static PsdLayoutProjectSettingsMigrationSnapshot ReadLegacyProjectSettings()
        {
            string legacyPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "ProjectSettings",
                "PSDLayoutTool2Settings.asset");
            if (!File.Exists(legacyPath))
            {
                return default(PsdLayoutProjectSettingsMigrationSnapshot);
            }

            try
            {
                UnityEngine.Object[] loaded = InternalEditorUtility.LoadSerializedFileAndForget(legacyPath);
                foreach (UnityEngine.Object candidate in loaded)
                {
                    PsdLayoutProjectSettings settings = candidate as PsdLayoutProjectSettings;
                    if (settings != null)
                    {
                        return settings.CreateMigrationSnapshot();
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("PSD Layout Tool 无法迁移旧版项目配置：" + exception.Message);
            }

            return default(PsdLayoutProjectSettingsMigrationSnapshot);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
