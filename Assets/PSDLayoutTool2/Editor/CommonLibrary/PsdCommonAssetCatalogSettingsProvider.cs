namespace PsdLayoutTool2
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// 用于生成和检查公共资源映射表的项目设置界面。
    /// </summary>
    internal static class PsdCommonAssetCatalogSettingsProvider
    {
        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/PSD Layout Tool/Common Asset Catalog", SettingsScope.Project)
            {
                label = "PSD Layout Tool - 公共资源映射表",
                guiHandler = DrawGui,
                keywords = new HashSet<string>(new[] { "PSD", "Common", "Prefab", "Texture", "Catalog" })
            };
        }

        private static void DrawGui(string searchContext)
        {
            PsdCommonAssetCatalog catalog = PsdCommonAssetCatalog.Load();
            PsdCommonAssetNamingSnapshot naming = PsdLayoutProjectSettings.instance.ResolveCommonAssetNaming();
            EditorGUILayout.HelpBox(
                "刷新时会扫描整个项目，查找名称以 " + naming.prefabPrefix +
                " 或 " + naming.texturePrefix +
                " 开头的资源，并在映射表中保存直接的 GUID 引用。PSD 导入只读取该映射表。" +
                "命名前缀请在项目的 PsdLayoutProjectSettings 配置资产中修改。",
                MessageType.Info);
            if (GUILayout.Button(catalog == null ? "生成公共资源映射表" : "刷新公共资源映射表"))
            {
                catalog = PsdCommonAssetCatalog.CreateOrRefresh();
                Selection.activeObject = catalog;
            }

            if (catalog == null)
            {
                return;
            }

            EditorGUILayout.LabelField("映射表状态", catalog.needsRefresh ? "已过期，需要生成或刷新" : "已就绪，可自动更新通用资源变更");
            EditorGUILayout.LabelField("Prefab 数量", catalog.prefabs.Count.ToString());
            EditorGUILayout.LabelField("纹理 / Sprite 数量", catalog.textures.Count.ToString());
            if (GUILayout.Button("选中映射表资产"))
            {
                Selection.activeObject = catalog;
            }
        }
    }
}
