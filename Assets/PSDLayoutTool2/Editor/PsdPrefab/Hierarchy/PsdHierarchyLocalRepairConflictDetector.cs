namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// 冲突严重级别
    /// </summary>
    internal enum ConflictSeverity
    {
        /// <summary>信息提示</summary>
        Info = 0,

        /// <summary>警告</summary>
        Warning = 1,

        /// <summary>严重冲突</summary>
        Critical = 2
    }

    /// <summary>
    /// 冲突解决策略
    /// </summary>
    internal enum ConflictResolutionStrategy
    {
        /// <summary>跳过此节点</summary>
        Skip,

        /// <summary>强制执行</summary>
        ForceApply,

        /// <summary>手动审查</summary>
        ManualReview,

        /// <summary>自动合并</summary>
        AutoMerge,

        /// <summary>保留现有结构</summary>
        KeepExisting,

        /// <summary>替换为新结构</summary>
        ReplaceWithNew
    }

    /// <summary>
    /// 局部整理冲突信息
    /// </summary>
    internal sealed class LocalRepairConflict
    {
        /// <summary>冲突类型</summary>
        public string ConflictType { get; set; }

        /// <summary>冲突描述</summary>
        public string Description { get; set; }

        /// <summary>详细信息</summary>
        public string DetailedInfo { get; set; }

        /// <summary>节点路径</summary>
        public string NodePath { get; set; }

        /// <summary>该冲突关联的节点路径列表（用于按路径精确剔除被跳过的节点）</summary>
        public List<string> NodePaths { get; set; }

        /// <summary>严重级别</summary>
        public ConflictSeverity Severity { get; set; }

        /// <summary>可用的解决策略</summary>
        public List<ConflictResolutionStrategy> AvailableStrategies { get; set; }

        /// <summary>推荐策略</summary>
        public ConflictResolutionStrategy RecommendedStrategy { get; set; }

        /// <summary>用户选择的策略</summary>
        public ConflictResolutionStrategy SelectedStrategy { get; set; }

        /// <summary>相关的 GameObject（如果有）</summary>
        public GameObject RelatedObject { get; set; }

        public LocalRepairConflict()
        {
            AvailableStrategies = new List<ConflictResolutionStrategy>();
            NodePaths = new List<string>();
            RecommendedStrategy = ConflictResolutionStrategy.ManualReview;
            SelectedStrategy = ConflictResolutionStrategy.ManualReview;
        }
    }

    /// <summary>
    /// 局部整理冲突检测器
    /// </summary>
    internal static class PsdHierarchyLocalRepairConflictDetector
    {
        /// <summary>
        /// 检测局部整理的潜在冲突
        /// </summary>
        public static List<LocalRepairConflict> DetectConflicts(
            PsdHierarchyLocalRepairScope scope,
            GameObject prefabRoot)
        {
            var conflicts = new List<LocalRepairConflict>();

            if (scope == null || prefabRoot == null)
            {
                return conflicts;
            }

            // 1. 检测嵌套 Prefab 冲突
            DetectNestedPrefabConflicts(scope, prefabRoot, conflicts);

            // 2. 检测跨父节点选择冲突
            DetectCrossParentConflicts(scope, prefabRoot, conflicts);

            // 3. 检测组件依赖冲突
            DetectComponentDependencyConflicts(scope, prefabRoot, conflicts);

            // 4. 检测命名冲突
            DetectNamingConflicts(scope, prefabRoot, conflicts);

            // 5. 检测层级深度冲突
            DetectHierarchyDepthConflicts(scope, prefabRoot, conflicts);

            // 6. 检测 Identity 缺失冲突
            DetectMissingIdentityConflicts(scope, prefabRoot, conflicts);

            return conflicts;
        }

        private static void DetectNestedPrefabConflicts(
            PsdHierarchyLocalRepairScope scope,
            GameObject prefabRoot,
            List<LocalRepairConflict> conflicts)
        {
            foreach (string path in scope.selectedPaths)
            {
                GameObject node = FindNodeByPath(prefabRoot, path);
                if (node == null) continue;

                if (PrefabUtility.IsPartOfPrefabInstance(node))
                {
                    GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(node);
                    if (instanceRoot == node)
                    {
                        conflicts.Add(new LocalRepairConflict
                        {
                            ConflictType = "NestedPrefab",
                            Description = "选区包含嵌套 Prefab 实例",
                            DetailedInfo = $"节点 '{node.name}' 是嵌套 Prefab 实例的根节点。局部整理不支持嵌套 Prefab。",
                            NodePath = path,
                            NodePaths = new List<string> { path },
                            Severity = ConflictSeverity.Critical,
                            RelatedObject = node,
                            AvailableStrategies = new List<ConflictResolutionStrategy>
                            {
                                ConflictResolutionStrategy.Skip,
                                ConflictResolutionStrategy.ManualReview
                            },
                            RecommendedStrategy = ConflictResolutionStrategy.Skip
                        });
                    }
                }
            }
        }

        private static void DetectCrossParentConflicts(
            PsdHierarchyLocalRepairScope scope,
            GameObject prefabRoot,
            List<LocalRepairConflict> conflicts)
        {
            var parentGroups = new Dictionary<Transform, List<string>>();

            foreach (string path in scope.selectedPaths)
            {
                GameObject node = FindNodeByPath(prefabRoot, path);
                if (node == null) continue;

                Transform parent = node.transform.parent;
                if (parent != null)
                {
                    if (!parentGroups.ContainsKey(parent))
                    {
                        parentGroups[parent] = new List<string>();
                    }
                    parentGroups[parent].Add(path);
                }
            }

            // 检查是否有多个父节点（跨父节点选择）
            if (parentGroups.Count > 1)
            {
                conflicts.Add(new LocalRepairConflict
                {
                    ConflictType = "CrossParent",
                    Description = "选区跨越多个父节点",
                    DetailedInfo = $"选中的 {scope.selectedPaths.Length} 个节点分布在 {parentGroups.Count} 个不同的父节点下。" +
                                   "这可能导致组件化方案复杂度增加。",
                    NodePath = string.Join(", ", parentGroups.Keys.Select(p => BuildNodePath(p))),
                    NodePaths = scope.selectedPaths.ToList(),
                    Severity = ConflictSeverity.Warning,
                    AvailableStrategies = new List<ConflictResolutionStrategy>
                    {
                        ConflictResolutionStrategy.ForceApply,
                        ConflictResolutionStrategy.ManualReview,
                        ConflictResolutionStrategy.Skip
                    },
                    RecommendedStrategy = ConflictResolutionStrategy.ForceApply
                });
            }
        }

        private static void DetectComponentDependencyConflicts(
            PsdHierarchyLocalRepairScope scope,
            GameObject prefabRoot,
            List<LocalRepairConflict> conflicts)
        {
            foreach (string path in scope.selectedPaths)
            {
                GameObject node = FindNodeByPath(prefabRoot, path);
                if (node == null) continue;

                // 检查是否有特殊组件（Layout、Canvas、等）
                var components = node.GetComponents<Component>();
                var specialComponents = new List<string>();

                foreach (var component in components)
                {
                    if (component == null) continue;

                    string typeName = component.GetType().Name;
                    if (typeName.Contains("Layout") ||
                        typeName.Contains("Canvas") ||
                        typeName.Contains("Group") ||
                        typeName.Contains("Mask"))
                    {
                        specialComponents.Add(typeName);
                    }
                }

                if (specialComponents.Count > 0)
                {
                    conflicts.Add(new LocalRepairConflict
                    {
                        ConflictType = "ComponentDependency",
                        Description = "节点包含特殊组件",
                        DetailedInfo = $"节点 '{node.name}' 包含以下特殊组件：{string.Join(", ", specialComponents)}。" +
                                       "这些组件可能影响布局行为。",
                        NodePath = path,
                        NodePaths = new List<string> { path },
                        Severity = ConflictSeverity.Info,
                        RelatedObject = node,
                        AvailableStrategies = new List<ConflictResolutionStrategy>
                        {
                            ConflictResolutionStrategy.ForceApply,
                            ConflictResolutionStrategy.ManualReview
                        },
                        RecommendedStrategy = ConflictResolutionStrategy.ForceApply
                    });
                }
            }
        }

        private static void DetectNamingConflicts(
            PsdHierarchyLocalRepairScope scope,
            GameObject prefabRoot,
            List<LocalRepairConflict> conflicts)
        {
            var nameGroups = new Dictionary<string, List<string>>();

            foreach (string path in scope.selectedPaths)
            {
                GameObject node = FindNodeByPath(prefabRoot, path);
                if (node == null) continue;

                string nodeName = node.name;
                if (!nameGroups.ContainsKey(nodeName))
                {
                    nameGroups[nodeName] = new List<string>();
                }
                nameGroups[nodeName].Add(path);
            }

            // 检查重名节点
            foreach (var kvp in nameGroups)
            {
                if (kvp.Value.Count > 1)
                {
                    conflicts.Add(new LocalRepairConflict
                    {
                        ConflictType = "NamingConflict",
                        Description = $"存在 {kvp.Value.Count} 个同名节点 '{kvp.Key}'",
                        DetailedInfo = "重名节点可能导致组件化时的引用混淆。建议确认这些节点是否应该共享同一个组件模板。",
                        NodePath = string.Join("; ", kvp.Value),
                        NodePaths = kvp.Value.ToList(),
                        Severity = ConflictSeverity.Warning,
                        AvailableStrategies = new List<ConflictResolutionStrategy>
                        {
                            ConflictResolutionStrategy.AutoMerge,
                            ConflictResolutionStrategy.ManualReview
                        },
                        RecommendedStrategy = ConflictResolutionStrategy.AutoMerge
                    });
                }
            }
        }

        private static void DetectHierarchyDepthConflicts(
            PsdHierarchyLocalRepairScope scope,
            GameObject prefabRoot,
            List<LocalRepairConflict> conflicts)
        {
            int maxDepth = 0;
            string deepestPath = string.Empty;

            foreach (string path in scope.selectedPaths)
            {
                GameObject node = FindNodeByPath(prefabRoot, path);
                if (node == null) continue;

                int depth = GetNodeDepth(node.transform);
                if (depth > maxDepth)
                {
                    maxDepth = depth;
                    deepestPath = path;
                }
            }

            // 如果层级深度超过 10 层，发出警告
            if (maxDepth > 10)
            {
                conflicts.Add(new LocalRepairConflict
                {
                    ConflictType = "HierarchyDepth",
                    Description = $"选区包含深层嵌套节点（深度: {maxDepth}）",
                    DetailedInfo = "深层嵌套可能导致性能问题和维护困难。建议简化层级结构。",
                    NodePath = deepestPath,
                    NodePaths = new List<string> { deepestPath },
                    Severity = ConflictSeverity.Info,
                    AvailableStrategies = new List<ConflictResolutionStrategy>
                    {
                        ConflictResolutionStrategy.ForceApply,
                        ConflictResolutionStrategy.ManualReview
                    },
                    RecommendedStrategy = ConflictResolutionStrategy.ForceApply
                });
            }
        }

        private static void DetectMissingIdentityConflicts(
            PsdHierarchyLocalRepairScope scope,
            GameObject prefabRoot,
            List<LocalRepairConflict> conflicts)
        {
            var missingIdentityNodes = new List<string>();

            foreach (string path in scope.selectedPaths)
            {
                GameObject node = FindNodeByPath(prefabRoot, path);
                if (node == null) continue;

                if (node.GetComponent<PsdPrefabNodeIdentity>() == null)
                {
                    missingIdentityNodes.Add(path);
                }
            }

            if (missingIdentityNodes.Count > 0)
            {
                conflicts.Add(new LocalRepairConflict
                {
                    ConflictType = "MissingIdentity",
                    Description = $"{missingIdentityNodes.Count} 个节点缺少 PsdPrefabNodeIdentity",
                    DetailedInfo = "这些节点可能不是通过 PSD Layout Tool 生成的，或者 Identity 组件已被移除。" +
                                   "局部整理需要 Identity 来追踪节点变更。",
                    NodePath = string.Join("; ", missingIdentityNodes.Take(3)) +
                               (missingIdentityNodes.Count > 3 ? "..." : ""),
                    NodePaths = missingIdentityNodes.ToList(),
                    Severity = ConflictSeverity.Critical,
                    AvailableStrategies = new List<ConflictResolutionStrategy>
                    {
                        ConflictResolutionStrategy.Skip,
                        ConflictResolutionStrategy.ManualReview
                    },
                    RecommendedStrategy = ConflictResolutionStrategy.Skip
                });
            }
        }

        private static GameObject FindNodeByPath(GameObject root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path))
                return null;

            string[] parts = path.Split('/');
            Transform current = root.transform;

            foreach (string part in parts)
            {
                if (current.name == part)
                    continue;

                Transform found = null;
                foreach (Transform child in current)
                {
                    if (child.name == part)
                    {
                        found = child;
                        break;
                    }
                }

                if (found == null)
                    return null;

                current = found;
            }

            return current.gameObject;
        }

        private static int GetNodeDepth(Transform transform)
        {
            int depth = 0;
            Transform current = transform;
            while (current.parent != null)
            {
                depth++;
                current = current.parent;
            }
            return depth;
        }

        private static string BuildNodePath(Transform node)
        {
            if (node == null) return string.Empty;

            var segments = new List<string>();
            for (Transform current = node; current != null; current = current.parent)
            {
                segments.Add(current.name);
            }

            segments.Reverse();
            return string.Join("/", segments.ToArray());
        }
    }

    /// <summary>
    /// 冲突解决策略辅助类
    /// </summary>
    internal static class PsdHierarchyLocalRepairStrategyHelper
    {
        /// <summary>
        /// 获取策略的显示名称
        /// </summary>
        public static string GetStrategyDisplayName(ConflictResolutionStrategy strategy)
        {
            switch (strategy)
            {
                case ConflictResolutionStrategy.Skip:
                    return "跳过";
                case ConflictResolutionStrategy.ForceApply:
                    return "强制执行";
                case ConflictResolutionStrategy.ManualReview:
                    return "手动审查";
                case ConflictResolutionStrategy.AutoMerge:
                    return "自动合并";
                case ConflictResolutionStrategy.KeepExisting:
                    return "保留现有";
                case ConflictResolutionStrategy.ReplaceWithNew:
                    return "替换为新";
                default:
                    return strategy.ToString();
            }
        }

        /// <summary>
        /// 获取策略的详细描述
        /// </summary>
        public static string GetStrategyDescription(ConflictResolutionStrategy strategy)
        {
            switch (strategy)
            {
                case ConflictResolutionStrategy.Skip:
                    return "跳过此节点，不进行局部整理";
                case ConflictResolutionStrategy.ForceApply:
                    return "忽略冲突，强制执行局部整理";
                case ConflictResolutionStrategy.ManualReview:
                    return "需要手动检查并决定如何处理";
                case ConflictResolutionStrategy.AutoMerge:
                    return "自动合并重复节点到同一组件模板";
                case ConflictResolutionStrategy.KeepExisting:
                    return "保留现有的结构和组件";
                case ConflictResolutionStrategy.ReplaceWithNew:
                    return "用新生成的结构替换现有内容";
                default:
                    return "未知策略";
            }
        }
    }
}
