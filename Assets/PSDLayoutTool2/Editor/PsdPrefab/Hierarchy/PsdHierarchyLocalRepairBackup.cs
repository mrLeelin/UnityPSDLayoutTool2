namespace PsdLayoutTool2
{
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// 局部整理备份管理系统
    /// </summary>
    internal static class PsdHierarchyLocalRepairBackup
    {
        private const string BackupRootDirectory = "Library/PSDLayoutTool2/LocalRepairBackups";
        private const string BackupManifestFileName = "backup_manifest.json";

        /// <summary>
        /// 备份清单信息
        /// </summary>
        [Serializable]
        private class BackupManifest
        {
            public string originalPrefabPath;
            public string backupTimestamp;
            public string backupDescription;
            public string backupFilePath;
            public long fileSizeBytes;
        }

        /// <summary>
        /// 创建 Prefab 备份
        /// </summary>
        /// <param name="prefabAssetPath">要备份的 Prefab 资源路径</param>
        /// <param name="description">备份描述</param>
        /// <returns>备份文件路径；失败返回 null</returns>
        public static string CreateBackup(string prefabAssetPath, string description)
        {
            if (string.IsNullOrEmpty(prefabAssetPath))
            {
                Debug.LogError("[LocalRepairBackup] Prefab 路径为空");
                return null;
            }

            if (!File.Exists(prefabAssetPath))
            {
                Debug.LogError($"[LocalRepairBackup] Prefab 文件不存在: {prefabAssetPath}");
                return null;
            }

            try
            {
                // 确保备份根目录存在
                if (!Directory.Exists(BackupRootDirectory))
                {
                    Directory.CreateDirectory(BackupRootDirectory);
                }

                // 生成备份文件名（使用时间戳）
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string prefabFileName = Path.GetFileNameWithoutExtension(prefabAssetPath);
                string backupFileName = $"{prefabFileName}_{timestamp}.prefab";
                string backupFilePath = Path.Combine(BackupRootDirectory, backupFileName);

                // 复制 Prefab 文件
                File.Copy(prefabAssetPath, backupFilePath, overwrite: true);

                // 同时备份 .meta 文件（如果存在）
                string metaSourcePath = prefabAssetPath + ".meta";
                if (File.Exists(metaSourcePath))
                {
                    string metaBackupPath = backupFilePath + ".meta";
                    File.Copy(metaSourcePath, metaBackupPath, overwrite: true);
                }

                // 创建备份清单
                var manifest = new BackupManifest
                {
                    originalPrefabPath = prefabAssetPath,
                    backupTimestamp = timestamp,
                    backupDescription = description ?? "无描述",
                    backupFilePath = backupFilePath,
                    fileSizeBytes = new FileInfo(backupFilePath).Length
                };

                string manifestPath = Path.Combine(BackupRootDirectory, $"{prefabFileName}_{timestamp}_manifest.json");
                string manifestJson = JsonUtility.ToJson(manifest, prettyPrint: true);
                File.WriteAllText(manifestPath, manifestJson);

                Debug.Log($"[LocalRepairBackup] 备份已创建: {backupFilePath} ({FormatFileSize(manifest.fileSizeBytes)})");
                return backupFilePath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalRepairBackup] 创建备份失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从备份恢复 Prefab
        /// </summary>
        /// <param name="backupFilePath">备份文件路径</param>
        /// <returns>是否恢复成功</returns>
        public static bool RestoreBackup(string backupFilePath)
        {
            if (string.IsNullOrEmpty(backupFilePath))
            {
                Debug.LogError("[LocalRepairBackup] 备份文件路径为空");
                return false;
            }

            if (!File.Exists(backupFilePath))
            {
                Debug.LogError($"[LocalRepairBackup] 备份文件不存在: {backupFilePath}");
                return false;
            }

            try
            {
                // 读取备份清单
                string manifestPath = backupFilePath.Replace(".prefab", "_manifest.json");
                if (!File.Exists(manifestPath))
                {
                    Debug.LogWarning($"[LocalRepairBackup] 备份清单不存在，尝试从文件名推断原始路径: {manifestPath}");

                    // 尝试从备份文件名推断原始路径（格式：PrefabName_timestamp.prefab）
                    string backupFileName = Path.GetFileNameWithoutExtension(backupFilePath);
                    int lastUnderscoreIndex = backupFileName.LastIndexOf('_');
                    if (lastUnderscoreIndex <= 0)
                    {
                        Debug.LogError("[LocalRepairBackup] 无法推断原始 Prefab 路径");
                        return false;
                    }

                    string originalPrefabName = backupFileName.Substring(0, lastUnderscoreIndex);
                    Debug.LogWarning($"[LocalRepairBackup] 推断的原始 Prefab 名称: {originalPrefabName}（需要手动指定完整路径）");
                    return false;
                }

                string manifestJson = File.ReadAllText(manifestPath);
                BackupManifest manifest = JsonUtility.FromJson<BackupManifest>(manifestJson);

                if (manifest == null || string.IsNullOrEmpty(manifest.originalPrefabPath))
                {
                    Debug.LogError("[LocalRepairBackup] 备份清单损坏或缺少原始路径信息");
                    return false;
                }

                // 确认恢复操作
                bool confirmed = EditorUtility.DisplayDialog(
                    "确认恢复备份",
                    $"即将从备份恢复 Prefab:\n\n" +
                    $"原始路径: {manifest.originalPrefabPath}\n" +
                    $"备份时间: {manifest.backupTimestamp}\n" +
                    $"备份描述: {manifest.backupDescription}\n\n" +
                    $"此操作将覆盖当前的 Prefab 文件。是否继续？",
                    "恢复",
                    "取消");

                if (!confirmed)
                {
                    Debug.Log("[LocalRepairBackup] 用户取消了恢复操作");
                    return false;
                }

                // 执行恢复
                File.Copy(backupFilePath, manifest.originalPrefabPath, overwrite: true);

                // 恢复 .meta 文件（如果存在）
                string metaBackupPath = backupFilePath + ".meta";
                if (File.Exists(metaBackupPath))
                {
                    string metaTargetPath = manifest.originalPrefabPath + ".meta";
                    File.Copy(metaBackupPath, metaTargetPath, overwrite: true);
                }

                // 刷新 Unity AssetDatabase
                AssetDatabase.ImportAsset(manifest.originalPrefabPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();

                Debug.Log($"[LocalRepairBackup] 已从备份恢复: {manifest.originalPrefabPath}");
                EditorUtility.DisplayDialog("恢复成功", $"Prefab 已从备份恢复:\n{manifest.originalPrefabPath}", "确定");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalRepairBackup] 恢复备份失败: {ex.Message}");
                EditorUtility.DisplayDialog("恢复失败", $"恢复备份时发生错误:\n{ex.Message}", "确定");
                return false;
            }
        }

        /// <summary>
        /// 清理旧备份（保留指定天数内的备份）
        /// </summary>
        /// <param name="keepDays">保留天数</param>
        public static void CleanOldBackups(int keepDays = 7)
        {
            if (!Directory.Exists(BackupRootDirectory))
            {
                return;
            }

            try
            {
                DateTime cutoffDate = DateTime.Now.AddDays(-keepDays);
                string[] backupFiles = Directory.GetFiles(BackupRootDirectory, "*.prefab", SearchOption.TopDirectoryOnly);

                int deletedCount = 0;
                long freedSpace = 0;

                foreach (string backupFile in backupFiles)
                {
                    FileInfo fileInfo = new FileInfo(backupFile);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        long fileSize = fileInfo.Length;

                        // 删除备份文件
                        File.Delete(backupFile);

                        // 删除关联的 .meta 文件
                        string metaFile = backupFile + ".meta";
                        if (File.Exists(metaFile))
                        {
                            File.Delete(metaFile);
                        }

                        // 删除关联的清单文件
                        string manifestFile = backupFile.Replace(".prefab", "_manifest.json");
                        if (File.Exists(manifestFile))
                        {
                            File.Delete(manifestFile);
                        }

                        deletedCount++;
                        freedSpace += fileSize;
                    }
                }

                if (deletedCount > 0)
                {
                    Debug.Log($"[LocalRepairBackup] 已清理 {deletedCount} 个超过 {keepDays} 天的旧备份，释放空间: {FormatFileSize(freedSpace)}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalRepairBackup] 清理旧备份失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 列出所有可用备份
        /// </summary>
        /// <returns>备份信息数组</returns>
        public static BackupInfo[] ListBackups()
        {
            if (!Directory.Exists(BackupRootDirectory))
            {
                return Array.Empty<BackupInfo>();
            }

            try
            {
                string[] manifestFiles = Directory.GetFiles(BackupRootDirectory, "*_manifest.json", SearchOption.TopDirectoryOnly);
                var backupInfos = new System.Collections.Generic.List<BackupInfo>();

                foreach (string manifestFile in manifestFiles)
                {
                    try
                    {
                        string manifestJson = File.ReadAllText(manifestFile);
                        BackupManifest manifest = JsonUtility.FromJson<BackupManifest>(manifestJson);

                        if (manifest != null && File.Exists(manifest.backupFilePath))
                        {
                            backupInfos.Add(new BackupInfo
                            {
                                BackupFilePath = manifest.backupFilePath,
                                OriginalPrefabPath = manifest.originalPrefabPath,
                                Timestamp = manifest.backupTimestamp,
                                Description = manifest.backupDescription,
                                FileSizeBytes = manifest.fileSizeBytes
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[LocalRepairBackup] 读取备份清单失败: {manifestFile}, 错误: {ex.Message}");
                    }
                }

                return backupInfos.ToArray();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalRepairBackup] 列出备份失败: {ex.Message}");
                return Array.Empty<BackupInfo>();
            }
        }

        /// <summary>
        /// 获取备份目录路径
        /// </summary>
        public static string GetBackupDirectory()
        {
            return BackupRootDirectory;
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// 备份信息（供外部查询使用）
        /// </summary>
        public struct BackupInfo
        {
            public string BackupFilePath;
            public string OriginalPrefabPath;
            public string Timestamp;
            public string Description;
            public long FileSizeBytes;
        }
    }
}
