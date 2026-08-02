namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Networking;
    using UnityEngine.UI;

    internal readonly struct PsdHierarchyChatConnection
    {
        internal PsdHierarchyChatConnection(
            PsdHierarchyAiProvider provider,
            PsdHierarchyAiConnectionMode connectionMode,
            string cliExecutablePath,
            string endpoint,
            string model,
            string apiKey)
        {
            this.provider = provider;
            this.connectionMode = connectionMode;
            this.cliExecutablePath = cliExecutablePath ?? string.Empty;
            this.endpoint = endpoint ?? string.Empty;
            this.model = model ?? string.Empty;
            this.apiKey = apiKey ?? string.Empty;
        }

        internal readonly PsdHierarchyAiProvider provider;
        internal readonly PsdHierarchyAiConnectionMode connectionMode;
        internal readonly string cliExecutablePath;
        internal readonly string endpoint;
        internal readonly string model;
        internal readonly string apiKey;

        internal bool TryValidate(out string error)
        {
            if (provider != PsdHierarchyAiProvider.Claude && provider != PsdHierarchyAiProvider.Codex)
            {
                error = "选择的 AI 不受支持。";
                return false;
            }

            if (connectionMode == PsdHierarchyAiConnectionMode.LocalCli)
            {
                if (string.IsNullOrWhiteSpace(cliExecutablePath) || !File.Exists(cliExecutablePath))
                {
                    error = "所选 AI CLI 已不可用，请在全局配置中重新选择。";
                    return false;
                }

                error = string.Empty;
                return true;
            }

            if (connectionMode != PsdHierarchyAiConnectionMode.CustomApi)
            {
                error = "选择的 AI 连接方式不受支持。";
                return false;
            }

            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri parsedEndpoint) ||
                (parsedEndpoint.Scheme != Uri.UriSchemeHttp && parsedEndpoint.Scheme != Uri.UriSchemeHttps))
            {
                error = "请填写有效的自定义 API 地址。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(model))
            {
                error = "请填写模型名称。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                error = "请在全局配置中填写 API Key。";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    internal readonly struct PsdHierarchyChatMessage
    {
        internal PsdHierarchyChatMessage(string role, string content)
        {
            this.role = role ?? string.Empty;
            this.content = content ?? string.Empty;
        }

        internal readonly string role;
        internal readonly string content;
    }

    internal sealed class PsdHierarchyComponentFamilyCandidate
    {
        internal PsdHierarchyComponentFamilyCandidate(
            string id,
            string suggestedAssetName,
            string parent,
            IReadOnlyList<string> sources,
            bool requiresExtraction,
            string recommendedMode)
        {
            this.id = id ?? string.Empty;
            this.suggestedAssetName = suggestedAssetName ?? string.Empty;
            this.parent = parent ?? string.Empty;
            this.sources = sources ?? Array.Empty<string>();
            this.requiresExtraction = requiresExtraction;
            this.recommendedMode = recommendedMode ?? string.Empty;
        }

        internal readonly string id;
        internal readonly string suggestedAssetName;
        internal readonly string parent;
        internal readonly IReadOnlyList<string> sources;
        internal readonly bool requiresExtraction;
        internal readonly string recommendedMode;
    }

    internal readonly struct PsdHierarchySnapshotChild
    {
        internal PsdHierarchySnapshotChild(string path, string name, int siblingIndex)
        {
            this.path = path ?? string.Empty;
            this.name = name ?? string.Empty;
            this.siblingIndex = siblingIndex;
        }

        internal readonly string path;
        internal readonly string name;
        internal readonly int siblingIndex;
    }

    internal sealed class PsdHierarchyChatContext
    {
        internal PsdHierarchyChatContext(
            string projectRoot,
            string sourcePsdAssetPath,
            string targetPrefabAssetPath,
            string skillFullPath,
            string skillContent,
            string prefabContent,
            string planFormatContent = "",
            string hierarchySnapshotJson = "",
            string hierarchySnapshotFingerprint = "",
            string hierarchySnapshotFullPath = "")
        {
            this.projectRoot = projectRoot ?? string.Empty;
            this.sourcePsdAssetPath = sourcePsdAssetPath ?? string.Empty;
            this.targetPrefabAssetPath = targetPrefabAssetPath ?? string.Empty;
            this.skillFullPath = skillFullPath ?? string.Empty;
            this.skillContent = skillContent ?? string.Empty;
            this.prefabContent = prefabContent ?? string.Empty;
            this.planFormatContent = planFormatContent ?? string.Empty;
            this.hierarchySnapshotJson = hierarchySnapshotJson ?? string.Empty;
            this.hierarchySnapshotFingerprint = hierarchySnapshotFingerprint ?? string.Empty;
            this.hierarchySnapshotFullPath = hierarchySnapshotFullPath ?? string.Empty;
            nodePathsById = ParseNodePaths(this.hierarchySnapshotJson);
            directChildrenByPath = ParseDirectChildren(this.hierarchySnapshotJson);
            componentFamilyCandidates = ParseComponentFamilyCandidates(this.hierarchySnapshotJson);
            containmentFindings = ParseContainmentFindings(this.hierarchySnapshotJson);
            flatSiblingFindings = ParseFlatSiblingFindings(this.hierarchySnapshotJson);
        }

        internal readonly string projectRoot;
        internal readonly string sourcePsdAssetPath;
        internal readonly string targetPrefabAssetPath;
        internal readonly string skillFullPath;
        internal readonly string skillContent;
        internal readonly string prefabContent;
        internal readonly string planFormatContent;
        internal readonly string hierarchySnapshotJson;
        internal readonly string hierarchySnapshotFingerprint;
        internal readonly string hierarchySnapshotFullPath;
        internal readonly IReadOnlyList<PsdHierarchyComponentFamilyCandidate> componentFamilyCandidates;

        // Kept as raw snapshot JSON: the plan writer copies these entries through to the
        // runner plan unchanged apart from node-reference resolution.
        internal readonly JArray containmentFindings;
        internal readonly JArray flatSiblingFindings;
        private readonly Dictionary<string, string> nodePathsById;
        private readonly Dictionary<string, IReadOnlyList<PsdHierarchySnapshotChild>> directChildrenByPath;

        internal bool TryGetNodePath(string nodeId, out string path)
        {
            return nodePathsById.TryGetValue(nodeId ?? string.Empty, out path);
        }

        internal bool TryGetDirectChildren(
            string parentPath,
            out IReadOnlyList<PsdHierarchySnapshotChild> children)
        {
            return directChildrenByPath.TryGetValue(parentPath ?? string.Empty, out children);
        }

        internal string BuildInstructions()
        {
            var builder = new StringBuilder();
            builder.AppendLine("You are assisting with a Unity Prefab hierarchy cleanup from inside the Unity Editor.");
            builder.AppendLine("The user supplied the exact cleanup skill and target Prefab below.");
            builder.AppendLine("Inspect first and provide a complete, reviewable plan. Do not claim to have edited a local asset: the Unity chat window performs the approved update.");
            builder.AppendLine("Do not invoke PowerShell, Python, Unity runners, or file-writing tools yourself.");
            builder.AppendLine("Your first reply must contain the human-readable review in Simplified Chinese, followed by exactly one complete UTF-8 JSON plan in a fenced ```json code block that follows the supplied plan format.");
            builder.AppendLine("The JSON root must contain \"version\": 2, \"snapshotFingerprint\" copied exactly from the supplied snapshot, and every required operation array, including empty arrays for unused operations. The window rejects incomplete JSON before it can be confirmed.");
            builder.AppendLine("If that reply fails plan validation, the Unity chat window automatically sends the validation error back in this same AI session. Treat that message as an internal correction request: return only one complete replacement JSON code block, never a patch, and never ask the user to retry or send another message. The window preserves the initial five-section review for the user.");
            builder.AppendLine("The user will inspect that reply. When the user replies with an explicit confirmation, the Unity chat window validates the JSON and directly runs the approved plan through Unity Editor APIs. Do not ask for an additional confirmation, output-mode choice, or manual script command.");
            builder.AppendLine("The only allowed output mode is in_place: output.assetPath must exactly equal the supplied target Prefab path.");
            builder.AppendLine("Every reference to an existing Prefab node must use node:<id> from the authoritative snapshot. Never write a raw hierarchy path in wrappers, moves, renames, removals, tight bounds, component-family decisions, or extraction contracts.");
            builder.AppendLine("Asset rename expectedGuid values are owned by Unity. In this version 2 AI plan, use an empty expectedGuid string; the Unity window resolves each existing from path and captures its current AssetDatabase GUID before runner validation.");
            builder.AppendLine("Private-asset naming is reviewed through textureRenames[].toName and spriteAtlasRenames[].toName. When either array is non-empty, Unity derives the internal prefabName from their one common PascalCase name ending with View. Keep the required prefabName field present for schema stability, but do not guess it independently or change it instead of the reviewed rename targets.");
            builder.AppendLine(PsdHierarchyChatClient.PlanIdentifierContract);
            builder.AppendLine("The target is already confirmed for in-place cleanup. Do not ask the user to choose an output mode or whether to create a new Prefab.");
            builder.AppendLine("Do not propose, create, copy, or offer a .cleaned.prefab or any other replacement Prefab. Any later approved cleanup must target the supplied Prefab in place while preserving visual layout, generated assets, bindings, and unrelated components.");
            builder.AppendLine("If evidence supports a reusable component, state, variant, or stateful extraction, include the complete reviewed extraction contract in the one JSON plan. Each componentFamilyDecision for a supplied candidate must copy its candidateId, parent, and complete sources exactly. Candidates marked requiresExtraction:true must use component, state, variant, or stateful mode; skip is forbidden for them. Candidates marked requiresExtraction:false are advisory and may use skip with concrete structural evidence; do not force a variant solely because sibling names repeat. Do not silently omit a repeated component family.");
            builder.AppendLine("When the snapshot includes flatSiblingFindings, set every finding's flatSiblingResolutions mode to group. Unity deterministically derives the wrapper id as <findingId>_group, the exact finding parent, the observed background siblingIndex, every listed member move in listed order, and tightBounds; do not invent alternate wrapper ids or destinations. These fields are normalized from the authoritative snapshot before execution.");
            builder.AppendLine("For a mandatory variant family, create one observed state for every distinct recursive structure and map every source to an exact matching state. Even when every source has a distinct structure, the mandatory family must not be skipped or reduced to hierarchy-only cleanup.");
            builder.AppendLine("Every extracted assetPath must be a new PascalCase .prefab directly under the target Prefab's sibling Common directory. Multiple non-overlapping component families and hierarchy cleanup operations are intentionally supported in one reviewed plan.");
            builder.AppendLine("Return an auditable analysis summary, not private chain-of-thought. In Simplified Chinese, use exactly these sections: 分析摘要, 分组依据, 风险与保留项, 原地整理方案, 验证清单. Ground every claim in observable hierarchy, geometry, component, sibling-order, or repeated-structure evidence.");
            builder.AppendLine("Source PSD: " + sourcePsdAssetPath);
            builder.AppendLine("Target Prefab: " + targetPrefabAssetPath);
            builder.AppendLine();
            builder.AppendLine("===== BEGIN prefab-hierarchy-cleanup/SKILL.md =====");
            builder.AppendLine(skillContent);
            builder.AppendLine("===== END prefab-hierarchy-cleanup/SKILL.md =====");
            if (!string.IsNullOrWhiteSpace(planFormatContent))
            {
                builder.AppendLine();
                builder.AppendLine("===== BEGIN prefab-hierarchy-cleanup/references/plan-format.md =====");
                builder.AppendLine(planFormatContent);
                builder.AppendLine("===== END prefab-hierarchy-cleanup/references/plan-format.md =====");
            }
            builder.AppendLine();
            builder.AppendLine("===== BEGIN TARGET PREFAB NODE SNAPSHOT =====");
            builder.AppendLine(hierarchySnapshotJson);
            builder.AppendLine("===== END TARGET PREFAB NODE SNAPSHOT =====");
            return builder.ToString();
        }

        private static Dictionary<string, string> ParseNodePaths(string snapshotJson)
        {
            var paths = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(snapshotJson))
            {
                return paths;
            }

            try
            {
                JObject snapshot = JObject.Parse(snapshotJson);
                if (!(snapshot["nodes"] is JArray nodes))
                {
                    return paths;
                }

                foreach (JObject node in nodes.OfType<JObject>())
                {
                    string id = node.Value<string>("id");
                    string path = node.Value<string>("path");
                    if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(path))
                    {
                        paths[id] = path;
                    }
                }
            }
            catch (Newtonsoft.Json.JsonException)
            {
                // Invalid snapshots are rejected by the context builder.
            }

            return paths;
        }

        private static Dictionary<string, IReadOnlyList<PsdHierarchySnapshotChild>> ParseDirectChildren(
            string snapshotJson)
        {
            var result = new Dictionary<string, IReadOnlyList<PsdHierarchySnapshotChild>>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(snapshotJson))
            {
                return result;
            }

            try
            {
                JObject snapshot = JObject.Parse(snapshotJson);
                if (!(snapshot["nodes"] is JArray nodes))
                {
                    return result;
                }

                var nodesById = nodes
                    .OfType<JObject>()
                    .Where(node => !string.IsNullOrWhiteSpace(node.Value<string>("id")))
                    .ToDictionary(node => node.Value<string>("id"), StringComparer.Ordinal);
                var childrenByParentPath = new Dictionary<string, List<PsdHierarchySnapshotChild>>(StringComparer.Ordinal);
                foreach (JObject node in nodesById.Values)
                {
                    string parentId = node.Value<string>("parentId");
                    if (string.IsNullOrWhiteSpace(parentId) ||
                        !nodesById.TryGetValue(parentId, out JObject parent))
                    {
                        continue;
                    }

                    string parentPath = parent.Value<string>("path");
                    string path = node.Value<string>("path");
                    string name = node.Value<string>("name");
                    if (string.IsNullOrWhiteSpace(parentPath) || string.IsNullOrWhiteSpace(path) ||
                        string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (!childrenByParentPath.TryGetValue(parentPath, out List<PsdHierarchySnapshotChild> children))
                    {
                        children = new List<PsdHierarchySnapshotChild>();
                        childrenByParentPath.Add(parentPath, children);
                    }

                    children.Add(new PsdHierarchySnapshotChild(
                        path,
                        name,
                        node.Value<int?>("siblingIndex") ?? int.MaxValue));
                }

                foreach (KeyValuePair<string, List<PsdHierarchySnapshotChild>> entry in childrenByParentPath)
                {
                    result.Add(
                        entry.Key,
                        entry.Value
                            .OrderBy(child => child.siblingIndex)
                            .ThenBy(child => child.path, StringComparer.Ordinal)
                            .ToArray());
                }
            }
            catch (Newtonsoft.Json.JsonException)
            {
                // Invalid snapshots are rejected by the context builder.
            }

            return result;
        }

        private static IReadOnlyList<PsdHierarchyComponentFamilyCandidate> ParseComponentFamilyCandidates(
            string snapshotJson)
        {
            var candidates = new List<PsdHierarchyComponentFamilyCandidate>();
            if (string.IsNullOrWhiteSpace(snapshotJson))
            {
                return candidates;
            }

            try
            {
                JObject snapshot = JObject.Parse(snapshotJson);
                if (!(snapshot["componentFamilyCandidates"] is JArray entries))
                {
                    return candidates;
                }

                foreach (JObject entry in entries.OfType<JObject>())
                {
                    string id = entry.Value<string>("id");
                    string parent = entry.Value<string>("parent");
                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(parent) ||
                        !(entry["sources"] is JArray sourceTokens))
                    {
                        continue;
                    }

                    string[] sources = sourceTokens
                        .Values<string>()
                        .Where(source => !string.IsNullOrWhiteSpace(source))
                        .ToArray();
                    if (sources.Length < 2)
                    {
                        continue;
                    }

                    candidates.Add(new PsdHierarchyComponentFamilyCandidate(
                        id,
                        entry.Value<string>("suggestedAssetName"),
                        parent,
                        sources,
                        entry.Value<bool?>("requiresExtraction") ?? false,
                        entry.Value<string>("recommendedMode")));
                }
            }
            catch (Newtonsoft.Json.JsonException)
            {
                // Invalid snapshots are rejected by the context builder.
            }

            return candidates;
        }

        private static JArray ParseContainmentFindings(string snapshotJson)
        {
            if (string.IsNullOrWhiteSpace(snapshotJson))
            {
                return new JArray();
            }

            try
            {
                JObject snapshot = JObject.Parse(snapshotJson);
                return snapshot["containmentFindings"] is JArray entries
                    ? (JArray)entries.DeepClone()
                    : new JArray();
            }
            catch (Newtonsoft.Json.JsonException)
            {
                // Invalid snapshots are rejected by the context builder.
                return new JArray();
            }
        }

        private static JArray ParseFlatSiblingFindings(string snapshotJson)
        {
            if (string.IsNullOrWhiteSpace(snapshotJson))
            {
                return new JArray();
            }

            try
            {
                JObject snapshot = JObject.Parse(snapshotJson);
                return snapshot["flatSiblingFindings"] is JArray entries
                    ? (JArray)entries.DeepClone()
                    : new JArray();
            }
            catch (Newtonsoft.Json.JsonException)
            {
                // Invalid snapshots are rejected by the context builder.
                return new JArray();
            }
        }
    }

    internal static class PsdHierarchyChatContextBuilder
    {
        internal const string DefaultSkillRelativePath =
            ".agents/skills/prefab-hierarchy-cleanup/SKILL.md";

        internal const string DefaultPlanFormatRelativePath =
            ".agents/skills/prefab-hierarchy-cleanup/references/plan-format.md";

        private const string LegacyPackageRootRelativePath = "Assets/UnityPSDLayoutTool2";
        private const string ScriptAssetPathMarker = "/Assets/PSDLayoutTool2/";

        internal const long MaxContextFileBytes = 512 * 1024;

        internal static bool TryCreate(
            string sourcePsdAssetPath,
            string targetPrefabAssetPath,
            out PsdHierarchyChatContext context,
            out string error)
        {
            context = null;
            DirectoryInfo projectDirectory = Directory.GetParent(Application.dataPath);
            if (projectDirectory == null)
            {
                error = "无法解析 Unity 项目根目录。";
                return false;
            }

            string projectRoot = projectDirectory.FullName;
            string prefabAssetPath = NormalizeAssetPath(targetPrefabAssetPath);
            string prefabFullPath = ToFullPath(projectRoot, prefabAssetPath);
            if (!TryReadContextFile(prefabFullPath, "目标 Prefab", out string prefabContent, out error))
            {
                return false;
            }

            if (!TryResolvePackageFilePath(
                    projectRoot,
                    FindSourceScriptAssetPath(),
                    DefaultSkillRelativePath,
                    out string skillFullPath))
            {
                error = "AI 整理技能不存在。请确认 Unity PSD Layout Tool 2 已完整安装：" + skillFullPath;
                return false;
            }

            if (!TryReadContextFile(skillFullPath, "AI 整理技能", out string skillContent, out error))
            {
                return false;
            }

            string planFormatFullPath = Path.Combine(
                Path.GetDirectoryName(skillFullPath),
                "references",
                "plan-format.md");
            if (!TryReadContextFile(planFormatFullPath, "整理计划格式", out string planFormatContent, out error))
            {
                return false;
            }

            string snapshotFingerprint;
            string hierarchySnapshotJson;
            string hierarchySnapshotFullPath;
            if (!TryBuildHierarchySnapshot(
                    prefabAssetPath,
                    prefabFullPath,
                    projectRoot,
                    out hierarchySnapshotJson,
                    out snapshotFingerprint,
                    out hierarchySnapshotFullPath,
                    out error))
            {
                return false;
            }

            PsdHierarchyCleanupExecutionSettingsSnapshot executionSettings =
                PsdLayoutProjectSettings.instance.ResolveHierarchyCleanupExecutionSettings();
            if (!executionSettings.TryValidate(out error))
            {
                return false;
            }

            context = new PsdHierarchyChatContext(
                projectRoot,
                NormalizeAssetPath(sourcePsdAssetPath),
                prefabAssetPath,
                skillFullPath,
                skillContent,
                prefabContent,
                planFormatContent,
                hierarchySnapshotJson,
                snapshotFingerprint,
                hierarchySnapshotFullPath);
            error = string.Empty;
            return true;
        }

        private static bool TryBuildHierarchySnapshot(
            string prefabAssetPath,
            string prefabFullPath,
            string projectRoot,
            out string snapshotJson,
            out string fingerprint,
            out string snapshotFullPath,
            out string error)
        {
            snapshotJson = string.Empty;
            fingerprint = string.Empty;
            snapshotFullPath = string.Empty;
            GameObject root = null;
            try
            {
                fingerprint = ComputeFileFingerprint(prefabFullPath);
                root = PrefabUtility.LoadPrefabContents(prefabAssetPath);
                if (root == null)
                {
                    error = "无法加载目标 Prefab 以生成节点快照：" + prefabAssetPath;
                    return false;
                }

                var nodes = new JArray();
                int nodeIndex = 0;
                AppendSnapshotNode(root.transform, string.Empty, nodes, ref nodeIndex);
                JArray componentFamilyCandidates = BuildComponentFamilyCandidates(nodes);
                var snapshot = new JObject
                {
                    ["schemaVersion"] = 1,
                    ["prefabAssetPath"] = prefabAssetPath,
                    ["fingerprint"] = fingerprint,
                    ["nodeReferenceSyntax"] = "node:<id>",
                    ["nodes"] = nodes,
                    ["componentFamilyCandidates"] = componentFamilyCandidates,
                    ["containmentFindings"] = BuildContainmentFindings(nodes, componentFamilyCandidates),
                    ["flatSiblingFindings"] = BuildFlatSiblingFindings(nodes),
                };
                snapshotJson = snapshot.ToString(Formatting.None);

                snapshotFullPath = Path.Combine(
                    projectRoot,
                    "Library",
                    "PSDLayoutTool2",
                    "HierarchySnapshots",
                    fingerprint + ".json");
                Directory.CreateDirectory(Path.GetDirectoryName(snapshotFullPath));
                File.WriteAllText(snapshotFullPath, snapshotJson, new UTF8Encoding(false));
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "生成目标 Prefab 节点快照失败：" + exception.Message;
                return false;
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void AppendSnapshotNode(
            Transform node,
            string parentId,
            JArray nodes,
            ref int nodeIndex)
        {
            string id = "n" + nodeIndex.ToString("D6");
            nodeIndex++;
            var componentTypes = new JArray();
            foreach (Component component in node.GetComponents<Component>())
            {
                componentTypes.Add(component == null ? "<Missing>" : component.GetType().FullName);
            }

            var entry = new JObject
            {
                ["id"] = id,
                ["path"] = BuildPlanPath(node),
                ["name"] = node.name,
                ["parentId"] = parentId,
                ["siblingIndex"] = node.GetSiblingIndex(),
                ["childCount"] = node.childCount,
                ["active"] = node.gameObject.activeSelf,
                ["components"] = componentTypes,
            };

            if (node is RectTransform rect)
            {
                entry["rect"] = new JObject
                {
                    ["anchoredPosition"] = Vector(rect.anchoredPosition.x, rect.anchoredPosition.y),
                    ["sizeDelta"] = Vector(rect.sizeDelta.x, rect.sizeDelta.y),
                    ["anchorMin"] = Vector(rect.anchorMin.x, rect.anchorMin.y),
                    ["anchorMax"] = Vector(rect.anchorMax.x, rect.anchorMax.y),
                    ["pivot"] = Vector(rect.pivot.x, rect.pivot.y),
                    ["localScale"] = new JArray(rect.localScale.x, rect.localScale.y, rect.localScale.z),
                    ["rotationZ"] = rect.localEulerAngles.z,
                };

                // The axis-aligned world box is what containment questions are asked
                // against; deriving it later from local rects would have to re-walk the
                // parent chain and would break on any rotated or scaled ancestor.
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                float minX = corners[0].x;
                float minY = corners[0].y;
                float maxX = corners[0].x;
                float maxY = corners[0].y;
                for (int cornerIndex = 1; cornerIndex < corners.Length; cornerIndex++)
                {
                    minX = Mathf.Min(minX, corners[cornerIndex].x);
                    minY = Mathf.Min(minY, corners[cornerIndex].y);
                    maxX = Mathf.Max(maxX, corners[cornerIndex].x);
                    maxY = Mathf.Max(maxY, corners[cornerIndex].y);
                }

                entry["worldRect"] = new JArray(minX, minY, maxX, maxY);
            }

            string displayedText = ReadDisplayedText(node);
            if (!string.IsNullOrEmpty(displayedText))
            {
                entry["displayedText"] = displayedText;
            }

            Image image = node.GetComponent<Image>();
            if (image != null && image.sprite != null)
            {
                entry["sprite"] = image.sprite.name;
                entry["spriteAssetPath"] = AssetDatabase.GetAssetPath(image.sprite);
            }

            if (PrefabUtility.IsAnyPrefabInstanceRoot(node.gameObject))
            {
                string nestedPrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(node.gameObject);
                if (!string.IsNullOrEmpty(nestedPrefabPath))
                {
                    entry["nestedPrefabAssetPath"] = nestedPrefabPath;
                }
            }

            nodes.Add(entry);
            for (int childIndex = 0; childIndex < node.childCount; childIndex++)
            {
                AppendSnapshotNode(node.GetChild(childIndex), id, nodes, ref nodeIndex);
            }
        }

        // This report is part of the authoritative snapshot, not an AI guess.
        internal static JArray BuildComponentFamilyCandidates(JArray nodes)
        {
            const string generatedFlatSiblingStem = "__generated_flat_sibling__";
            var nodeById = nodes
                .OfType<JObject>()
                .Where(node => !string.IsNullOrWhiteSpace(node.Value<string>("id")))
                .ToDictionary(node => node.Value<string>("id"), StringComparer.Ordinal);
            var childrenByParentId = new Dictionary<string, List<JObject>>(StringComparer.Ordinal);
            foreach (JObject node in nodeById.Values)
            {
                string parentId = node.Value<string>("parentId");
                if (string.IsNullOrWhiteSpace(parentId))
                {
                    continue;
                }

                if (!childrenByParentId.TryGetValue(parentId, out List<JObject> children))
                {
                    children = new List<JObject>();
                    childrenByParentId.Add(parentId, children);
                }

                children.Add(node);
            }

            var candidates = new JArray();
            var emittedSourceSets = new HashSet<string>(StringComparer.Ordinal);
            int candidateIndex = 1;
            foreach (KeyValuePair<string, List<JObject>> parent in childrenByParentId.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (!nodeById.TryGetValue(parent.Key, out JObject parentNode))
                {
                    continue;
                }

                var groups = new Dictionary<string, List<JObject>>(StringComparer.Ordinal);
                var bareIndexChildren = new List<KeyValuePair<int, JObject>>();
                foreach (JObject child in parent.Value)
                {
                    if (child.Value<int?>("childCount") <= 0)
                    {
                        continue;
                    }

                    if (!TryGetRepeatedFamilyStem(child.Value<string>("name"), out string stem) &&
                        !TryGetGeneratedFlatSiblingFamilyStem(child.Value<string>("name"), out stem))
                    {
                        if (TryGetBareRepeatedIndex(child.Value<string>("name"), out int bareIndex))
                        {
                            bareIndexChildren.Add(new KeyValuePair<int, JObject>(bareIndex, child));
                        }

                        continue;
                    }

                    if (!groups.TryGetValue(stem, out List<JObject> group))
                    {
                        group = new List<JObject>();
                        groups.Add(stem, group);
                    }

                    group.Add(child);
                }

                foreach (KeyValuePair<int, JObject> bareEntry in bareIndexChildren)
                {
                    JObject bareChild = bareEntry.Value;
                    List<KeyValuePair<string, List<JObject>>> eligibleGroups = groups
                        .Where(pair => pair.Value.Count >= 2)
                        .Where(pair => !ContainsNestedPrefab(bareChild.Value<string>("id"), nodeById))
                        .Where(pair => HasConsistentRectTransformFrame(pair.Value.Concat(new[] { bareChild }).ToList()))
                        .ToList();
                    if (eligibleGroups.Count == 1)
                    {
                        eligibleGroups[0].Value.Add(bareChild);
                    }
                }

                if (groups.Count == 0 && bareIndexChildren.Select(entry => entry.Key).Distinct().Count() >= 3)
                {
                    List<JObject> bareChildren = bareIndexChildren.Select(entry => entry.Value).ToList();
                    if (HasConsistentRectTransformFrame(bareChildren) &&
                        TryGetBareNumberedFamilyStem(parentNode.Value<string>("name"), out string bareFamilyStem))
                    {
                        groups.Add(bareFamilyStem, bareChildren);
                    }
                }

                foreach (KeyValuePair<string, List<JObject>> groupEntry in groups.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    List<JObject> group = groupEntry.Value
                        .OrderBy(node => node.Value<int?>("siblingIndex") ?? int.MaxValue)
                        .ToList();
                    if (string.Equals(groupEntry.Key, generatedFlatSiblingStem, StringComparison.Ordinal) &&
                        IsDuplicateRootContainer(parentNode, nodeById))
                    {
                        continue;
                    }

                    if (group.Count < 3 || !HasConsistentRectTransformFrame(group) ||
                        group.Any(node => ContainsNestedPrefab(node.Value<string>("id"), nodeById)))
                    {
                        continue;
                    }

                    string[] sources = group.Select(node => "node:" + node.Value<string>("id")).ToArray();
                    string sourceSetKey = string.Join("|", sources);
                    if (!emittedSourceSets.Add(sourceSetKey))
                    {
                        continue;
                    }

                    bool identicalStructure = group
                        .Select(node => BuildStructureSignature(node.Value<string>("id"), nodeById, childrenByParentId))
                        .Distinct(StringComparer.Ordinal)
                        .Count() == 1;
                    bool hasCommonDirectChild = HasCommonDirectChildName(group, childrenByParentId);
                    // Passing the repeated-family checks establishes the reusable boundary.
                    // Structural differences select the extraction mode; they never make the
                    // complete family optional because a variant can preserve every observed shape.
                    bool requiresExtraction = true;
                    string suggestedAssetName = string.Equals(
                        groupEntry.Key,
                        generatedFlatSiblingStem,
                        StringComparison.Ordinal)
                        ? TryGetBareNumberedFamilyStem(parentNode.Value<string>("name"), out string parentStem)
                            ? ToSuggestedAssetName(parentStem)
                            : ToSuggestedAssetName(parentNode.Value<string>("name"))
                        : ToSuggestedAssetName(groupEntry.Key);
                    string familyCandidateId = "family_" + candidateIndex.ToString("D3");
                    candidates.Add(new JObject
                    {
                        ["id"] = familyCandidateId,
                        ["kind"] = "numbered_repeated",
                        ["parent"] = "node:" + parentNode.Value<string>("id"),
                        ["sources"] = new JArray(sources),
                        ["suggestedAssetName"] = suggestedAssetName,
                        ["instanceCount"] = sources.Length,
                        ["recommendedMode"] = identicalStructure ? "component" : hasCommonDirectChild ? "stateful" : "variant",
                        ["requiresExtraction"] = requiresExtraction,
                        ["evidence"] = new JArray(
                            "same-parent numbered family",
                            "matching RectTransform anchors and pivot; per-instance size is retained as an override",
                            identicalStructure
                                ? "matching recursive structure"
                                : hasCommonDirectChild
                                    ? "different child structures require explicit state mapping"
                                    : "no common direct-child member; every distinct recursive structure requires an exact observed variant state"),
                    });
                    candidateIndex++;
                    if (identicalStructure)
                    {
                        continue;
                    }

                    // A family where only one member differs would otherwise offer no clean
                    // component boundary at all, so the identical members are also published
                    // as their own subset candidate.
                    int subsetIndex = 1;
                    foreach (List<JObject> subset in BuildStructureSubsets(group, nodeById, childrenByParentId))
                    {
                        string[] subsetSources = subset.Select(node => "node:" + node.Value<string>("id")).ToArray();

                        // A subset and its family compete for the same sources, so only one of
                        // them can be an obligation. The family wins when it is already
                        // extractable; the subset is forced only when the family is not.
                        bool subsetExtractable = subsetSources.Length >= 2 && !requiresExtraction;
                        candidates.Add(new JObject
                        {
                            ["id"] = familyCandidateId + "_s" + subsetIndex.ToString("D2"),
                            ["kind"] = "numbered_structure_subset",
                            ["familyCandidateId"] = familyCandidateId,
                            ["parent"] = "node:" + parentNode.Value<string>("id"),
                            ["sources"] = new JArray(subsetSources),
                            ["suggestedAssetName"] = suggestedAssetName,
                            ["instanceCount"] = subsetSources.Length,
                            ["recommendedMode"] = subsetSources.Length >= 2 ? "component" : "skip",
                            ["requiresExtraction"] = subsetExtractable,
                            ["evidence"] = new JArray(
                                "subset of " + familyCandidateId + " sharing one recursive structure",
                                subsetSources.Length < 2
                                    ? "only member with this structure, so it has no peer to share a component Prefab with"
                                    : subsetExtractable
                                        ? "the full family has no clean component boundary, so this subset is the largest one"
                                        : "usable as a narrower component boundary if the family-level extraction is rejected"),
                        });
                        subsetIndex++;
                    }
                }
            }

            return candidates;
        }

        /// <summary>
        /// Groups one numbered family into buckets that share a recursive structure
        /// signature, ordered by first sibling index so output is deterministic.
        /// </summary>
        private static List<List<JObject>> BuildStructureSubsets(
            List<JObject> group,
            Dictionary<string, JObject> nodeById,
            Dictionary<string, List<JObject>> childrenByParentId)
        {
            var buckets = new Dictionary<string, List<JObject>>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (JObject node in group)
            {
                string key = BuildStructureSignature(node.Value<string>("id"), nodeById, childrenByParentId);
                if (!buckets.TryGetValue(key, out List<JObject> bucket))
                {
                    bucket = new List<JObject>();
                    buckets.Add(key, bucket);
                    order.Add(key);
                }

                bucket.Add(node);
            }

            return order.Select(key => buckets[key]).ToList();
        }

        // Geometry says these nodes belong to a repeated unit even though the hierarchy
        // groups them elsewhere. Like the candidate report this is measured, not guessed,
        // so the plan validator can treat it as a hard requirement.
        internal static JArray BuildContainmentFindings(JArray nodes, JArray candidates)
        {
            var findings = new JArray();
            var nodeById = nodes
                .OfType<JObject>()
                .Where(node => !string.IsNullOrWhiteSpace(node.Value<string>("id")))
                .ToDictionary(node => node.Value<string>("id"), StringComparer.Ordinal);
            List<JObject> families = candidates
                .OfType<JObject>()
                .Where(candidate => string.Equals(
                    candidate.Value<string>("kind"), "numbered_repeated", StringComparison.Ordinal))
                .ToList();
            foreach (JObject inner in families)
            {
                List<JObject> innerNodes = ResolveFamilyNodes(inner, nodeById);
                if (innerNodes == null)
                {
                    continue;
                }

                foreach (JObject outer in families)
                {
                    if (ReferenceEquals(inner, outer) ||
                        string.Equals(
                            inner.Value<string>("parent"),
                            outer.Value<string>("parent"),
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    List<JObject> outerNodes = ResolveFamilyNodes(outer, nodeById);
                    if (outerNodes == null || outerNodes.Count != innerNodes.Count)
                    {
                        continue;
                    }

                    var mapping = new JArray();
                    var usedOuterIds = new HashSet<string>(StringComparer.Ordinal);
                    double maxAreaRatio = 0d;
                    foreach (JObject innerNode in innerNodes)
                    {
                        JObject container = null;
                        double bestRatio = 0d;
                        foreach (JObject outerNode in outerNodes)
                        {
                            if (usedOuterIds.Contains(outerNode.Value<string>("id")) ||
                                !TryGetAreaRatioIfContained(innerNode, outerNode, out double ratio))
                            {
                                continue;
                            }

                            if (container == null || ratio < bestRatio)
                            {
                                container = outerNode;
                                bestRatio = ratio;
                            }
                        }

                        if (container == null || bestRatio > ContainmentAreaRatioLimit)
                        {
                            mapping = null;
                            break;
                        }

                        usedOuterIds.Add(container.Value<string>("id"));
                        maxAreaRatio = Math.Max(maxAreaRatio, bestRatio);
                        mapping.Add(new JObject
                        {
                            ["source"] = "node:" + innerNode.Value<string>("id"),
                            ["containedBy"] = "node:" + container.Value<string>("id"),
                        });
                    }

                    if (mapping == null || mapping.Count != innerNodes.Count)
                    {
                        continue;
                    }

                    findings.Add(new JObject
                    {
                        ["innerParent"] = inner.Value<string>("parent"),
                        ["innerCandidateId"] = inner.Value<string>("id"),
                        ["outerCandidateId"] = outer.Value<string>("id"),
                        ["maxAreaRatio"] = Math.Round(maxAreaRatio, 4),
                        ["mapping"] = mapping,
                        ["evidence"] = new JArray(
                            "every member is fully inside a distinct member of the outer family",
                            "equal cardinality with a one-to-one containment mapping",
                            "each member covers at most " +
                                (ContainmentAreaRatioLimit * 100d).ToString("0.#") +
                                "% of its container area"),
                    });
                }
            }

            return findings;
        }

        private const double ContainmentAreaRatioLimit = 0.25d;

        // A flat visual unit is safe to flag only when source order and geometry agree:
        // direct leaf siblings are consecutive and the first fully contains the rest.
        internal static JArray BuildFlatSiblingFindings(JArray nodes)
        {
            var findings = new JArray();
            var claimedNodeIds = new HashSet<string>(StringComparer.Ordinal);
            IEnumerable<IGrouping<string, JObject>> siblingGroups = nodes
                .OfType<JObject>()
                .Where(node =>
                    !string.IsNullOrWhiteSpace(node.Value<string>("id")) &&
                    !string.IsNullOrWhiteSpace(node.Value<string>("parentId")) &&
                    node.Value<int?>("childCount") == 0)
                .GroupBy(node => node.Value<string>("parentId"), StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal);

            foreach (IGrouping<string, JObject> group in siblingGroups)
            {
                List<JObject> siblings = group
                    .OrderBy(node => node.Value<int?>("siblingIndex") ?? int.MaxValue)
                    .ThenBy(node => node.Value<string>("id"), StringComparer.Ordinal)
                    .ToList();
                for (int startIndex = 0; startIndex < siblings.Count; startIndex++)
                {
                    JObject background = siblings[startIndex];
                    string backgroundId = background.Value<string>("id");
                    int? backgroundSiblingIndex = background.Value<int?>("siblingIndex");
                    if (claimedNodeIds.Contains(backgroundId) || !backgroundSiblingIndex.HasValue)
                    {
                        continue;
                    }

                    var members = new List<JObject> { background };
                    int expectedSiblingIndex = backgroundSiblingIndex.Value + 1;
                    for (int memberIndex = startIndex + 1; memberIndex < siblings.Count; memberIndex++)
                    {
                        JObject member = siblings[memberIndex];
                        string memberId = member.Value<string>("id");
                        if (claimedNodeIds.Contains(memberId) ||
                            member.Value<int?>("siblingIndex") != expectedSiblingIndex ||
                            !TryGetAreaRatioIfContained(member, background, out double areaRatio) ||
                            areaRatio > ContainmentAreaRatioLimit)
                        {
                            break;
                        }

                        members.Add(member);
                        expectedSiblingIndex++;
                    }

                    if (members.Count < 3)
                    {
                        continue;
                    }

                    foreach (JObject member in members)
                    {
                        claimedNodeIds.Add(member.Value<string>("id"));
                    }

                    findings.Add(new JObject
                    {
                        ["id"] = "flat_sibling_" + (findings.Count + 1).ToString("000"),
                        ["parent"] = "node:" + group.Key,
                        ["background"] = "node:" + backgroundId,
                        ["members"] = new JArray(members.Select(member =>
                            "node:" + member.Value<string>("id"))),
                        ["evidence"] = new JArray(
                            "all members are direct leaf siblings with consecutive source order",
                            "the first leaf fully contains every other member at a small area ratio"),
                    });
                }
            }

            return findings;
        }

        private static List<JObject> ResolveFamilyNodes(
            JObject candidate,
            IReadOnlyDictionary<string, JObject> nodeById)
        {
            var resolved = new List<JObject>();
            foreach (string source in (candidate.Value<JArray>("sources") ?? new JArray())
                .Select(token => token?.ToString()))
            {
                if (string.IsNullOrEmpty(source) || !source.StartsWith("node:", StringComparison.Ordinal) ||
                    !nodeById.TryGetValue(source.Substring("node:".Length), out JObject node) ||
                    node["worldRect"] == null)
                {
                    return null;
                }

                resolved.Add(node);
            }

            return resolved.Count >= 2 ? resolved : null;
        }

        private static bool TryGetAreaRatioIfContained(JObject inner, JObject outer, out double ratio)
        {
            ratio = 0d;
            if (!TryReadWorldRect(inner, out double[] innerRect) ||
                !TryReadWorldRect(outer, out double[] outerRect))
            {
                return false;
            }

            const double tolerance = 0.01d;
            if (innerRect[0] < outerRect[0] - tolerance || innerRect[1] < outerRect[1] - tolerance ||
                innerRect[2] > outerRect[2] + tolerance || innerRect[3] > outerRect[3] + tolerance)
            {
                return false;
            }

            double outerArea = (outerRect[2] - outerRect[0]) * (outerRect[3] - outerRect[1]);
            if (outerArea <= 0d)
            {
                return false;
            }

            ratio = (innerRect[2] - innerRect[0]) * (innerRect[3] - innerRect[1]) / outerArea;
            return true;
        }

        private static bool TryReadWorldRect(JObject node, out double[] rect)
        {
            rect = null;
            if (!(node?["worldRect"] is JArray values) || values.Count != 4)
            {
                return false;
            }

            rect = values.Select(value => value.Value<double>()).ToArray();
            return true;
        }

        private static bool HasCommonDirectChildName(
            IReadOnlyList<JObject> group,
            IReadOnlyDictionary<string, List<JObject>> childrenByParentId)
        {
            HashSet<string> commonNames = null;
            foreach (JObject node in group ?? Array.Empty<JObject>())
            {
                string nodeId = node.Value<string>("id");
                var names = new HashSet<string>(
                    childrenByParentId.TryGetValue(nodeId, out List<JObject> children)
                        ? children.Select(child => child.Value<string>("name"))
                        : Enumerable.Empty<string>(),
                    StringComparer.Ordinal);
                if (commonNames == null)
                {
                    commonNames = names;
                }
                else
                {
                    commonNames.IntersectWith(names);
                }

                if (commonNames.Count == 0)
                {
                    return false;
                }
            }

            return commonNames != null && commonNames.Count > 0;
        }

        private static bool TryGetRepeatedFamilyStem(string name, out string stem)
        {
            return TryGetRepeatedFamilyParts(name, out stem, out int ignoredIndex);
        }

        private static bool TryGetGeneratedFlatSiblingFamilyStem(string name, out string stem)
        {
            const string prefix = "FlatSibling_flat_sibling_";
            string value = (name ?? string.Empty).Trim().Trim('[', ']');
            stem = "__generated_flat_sibling__";
            if (!value.StartsWith(prefix, StringComparison.Ordinal) ||
                value.Length == prefix.Length ||
                !value.Substring(prefix.Length).All(char.IsDigit))
            {
                stem = string.Empty;
                return false;
            }

            return true;
        }

        private static bool IsDuplicateRootContainer(
            JObject parentNode,
            IReadOnlyDictionary<string, JObject> nodeById)
        {
            string parentId = parentNode?.Value<string>("parentId");
            return !string.IsNullOrWhiteSpace(parentId) &&
                   nodeById.TryGetValue(parentId, out JObject outerParent) &&
                   string.Equals(
                       parentNode.Value<string>("name"),
                       outerParent.Value<string>("name"),
                       StringComparison.Ordinal);
        }

        private static bool TryGetRepeatedFamilyParts(string name, out string stem, out int index)
        {
            stem = string.Empty;
            index = 0;
            string value = (name ?? string.Empty).Trim().Trim('[', ']');
            int digitsStart = value.Length;
            while (digitsStart > 0 && char.IsDigit(value[digitsStart - 1]))
            {
                digitsStart--;
            }

            if (digitsStart == value.Length || digitsStart == 0)
            {
                return false;
            }

            if (!int.TryParse(value.Substring(digitsStart), out index))
            {
                return false;
            }

            int stemEnd = digitsStart;
            while (stemEnd > 0 && (value[stemEnd - 1] == '_' || value[stemEnd - 1] == '-' || value[stemEnd - 1] == ' '))
            {
                stemEnd--;
            }

            string candidate = value.Substring(0, stemEnd);
            if (string.IsNullOrWhiteSpace(candidate) || !char.IsLetter(candidate[0]) ||
                candidate.Any(character => !char.IsLetterOrDigit(character)))
            {
                return false;
            }

            stem = candidate;
            return true;
        }

        private static bool TryGetBareRepeatedIndex(string name, out int index)
        {
            index = 0;
            string value = (name ?? string.Empty).Trim().Trim('[', ']');
            return value.Length > 0 && value.All(char.IsDigit) && int.TryParse(value, out index);
        }

        private static bool TryGetBareNumberedFamilyStem(string parentName, out string stem)
        {
            stem = (parentName ?? string.Empty).Trim().Trim('[', ']');
            if (string.IsNullOrWhiteSpace(stem) || !char.IsLetter(stem[0]) ||
                stem.Any(character => !char.IsLetterOrDigit(character)))
            {
                stem = string.Empty;
                return false;
            }

            if (stem.EndsWith("ies", StringComparison.Ordinal) && stem.Length > 3)
            {
                stem = stem.Substring(0, stem.Length - 3) + "y";
            }
            else if (stem.EndsWith("s", StringComparison.Ordinal) && stem.Length > 1 &&
                     !stem.EndsWith("ss", StringComparison.Ordinal) &&
                     !stem.EndsWith("us", StringComparison.Ordinal) &&
                     !stem.EndsWith("is", StringComparison.Ordinal))
            {
                stem = stem.Substring(0, stem.Length - 1);
            }

            return true;
        }

        private static string ToSuggestedAssetName(string stem)
        {
            if (string.IsNullOrEmpty(stem))
            {
                return "ReusableItem";
            }

            return char.ToUpperInvariant(stem[0]) + stem.Substring(1);
        }

        private static bool HasConsistentRectTransformFrame(IReadOnlyList<JObject> nodes)
        {
            if (nodes == null || nodes.Count < 2)
            {
                return false;
            }

            JObject baseline = nodes[0]["rect"] as JObject;
            if (baseline == null)
            {
                return false;
            }

            foreach (JObject node in nodes.Skip(1))
            {
                JObject rect = node["rect"] as JObject;
                if (rect == null ||
                    !VectorEquals(baseline["anchorMin"] as JArray, rect["anchorMin"] as JArray) ||
                    !VectorEquals(baseline["anchorMax"] as JArray, rect["anchorMax"] as JArray) ||
                    !VectorEquals(baseline["pivot"] as JArray, rect["pivot"] as JArray))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool VectorEquals(JArray left, JArray right)
        {
            if (left == null || right == null || left.Count != right.Count || left.Count == 0)
            {
                return false;
            }

            for (int index = 0; index < left.Count; index++)
            {
                if (left[index].Type != JTokenType.Float && left[index].Type != JTokenType.Integer ||
                    right[index].Type != JTokenType.Float && right[index].Type != JTokenType.Integer ||
                    Math.Abs(left[index].Value<float>() - right[index].Value<float>()) > 0.01f)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsNestedPrefab(string sourceId, IReadOnlyDictionary<string, JObject> nodeById)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return false;
            }

            if (nodeById.TryGetValue(sourceId, out JObject sourceNode))
            {
                for (JObject current = sourceNode; current != null;)
                {
                    if (!string.IsNullOrWhiteSpace(current.Value<string>("nestedPrefabAssetPath")))
                    {
                        return true;
                    }

                    string parentId = current.Value<string>("parentId");
                    current = !string.IsNullOrWhiteSpace(parentId) &&
                              nodeById.TryGetValue(parentId, out JObject parent)
                        ? parent
                        : null;
                }
            }

            foreach (JObject node in nodeById.Values)
            {
                if (string.IsNullOrWhiteSpace(node.Value<string>("nestedPrefabAssetPath")))
                {
                    continue;
                }

                for (JObject current = node; current != null;)
                {
                    string currentId = current.Value<string>("id");
                    if (string.Equals(currentId, sourceId, StringComparison.Ordinal))
                    {
                        return true;
                    }

                    string parentId = current.Value<string>("parentId");
                    current = !string.IsNullOrWhiteSpace(parentId) && nodeById.TryGetValue(parentId, out JObject parent)
                        ? parent
                        : null;
                }
            }

            return false;
        }

        private static string BuildStructureSignature(
            string nodeId,
            IReadOnlyDictionary<string, JObject> nodeById,
            IReadOnlyDictionary<string, List<JObject>> childrenByParentId)
        {
            if (!nodeById.TryGetValue(nodeId, out JObject node))
            {
                return string.Empty;
            }

            string components = string.Join(",", (node["components"] as JArray)?.Values<string>() ?? Enumerable.Empty<string>());
            if (!childrenByParentId.TryGetValue(nodeId, out List<JObject> children) || children.Count == 0)
            {
                return "(" + components + ")";
            }

            string childSignatures = string.Join(",", children
                .OrderBy(child => child.Value<int?>("siblingIndex") ?? int.MaxValue)
                .Select(child => BuildStructureSignature(child.Value<string>("id"), nodeById, childrenByParentId)));
            return "(" + components + "[" + childSignatures + "])";
        }

        private static string ReadDisplayedText(Transform node)
        {
            foreach (Component component in node.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                try
                {
                    var serialized = new SerializedObject(component);
                    SerializedProperty text = serialized.FindProperty("m_Text");
                    if (text != null && text.propertyType == SerializedPropertyType.String &&
                        !string.IsNullOrEmpty(text.stringValue))
                    {
                        return text.stringValue;
                    }
                }
                catch (ArgumentException)
                {
                    // Components without serialized text are expected.
                }
            }

            return string.Empty;
        }

        private static string BuildPlanPath(Transform node)
        {
            var segments = new List<string>();
            for (Transform current = node; current != null; current = current.parent)
            {
                string segment = current.name;
                if (current.parent != null)
                {
                    int occurrence = 0;
                    for (int siblingIndex = 0; siblingIndex < current.parent.childCount; siblingIndex++)
                    {
                        Transform sibling = current.parent.GetChild(siblingIndex);
                        if (!string.Equals(sibling.name, current.name, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (sibling == current)
                        {
                            break;
                        }

                        occurrence++;
                    }

                    if (occurrence > 0)
                    {
                        segment += "#" + occurrence;
                    }
                }

                segments.Add(segment);
            }

            segments.Reverse();
            return string.Join("/", segments.ToArray());
        }

        private static JArray Vector(float x, float y)
        {
            return new JArray(x, y);
        }

        internal static string ComputeFileFingerprint(string fullPath)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(File.ReadAllBytes(fullPath));
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        internal static string PlanFormatFullPath(string projectRoot)
        {
            TryResolvePackageFilePath(
                projectRoot,
                FindSourceScriptAssetPath(),
                DefaultPlanFormatRelativePath,
                out string fullPath);
            return fullPath;
        }

        internal static bool TryResolvePackageFilePath(
            string projectRoot,
            string sourceScriptAssetPath,
            string packageRelativePath,
            out string fullPath)
        {
            fullPath = string.Empty;
            foreach (string packageRoot in GetPackageRootCandidates(projectRoot, sourceScriptAssetPath))
            {
                string candidate = Path.GetFullPath(Path.Combine(
                    packageRoot,
                    (packageRelativePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar)));
                if (string.IsNullOrEmpty(fullPath))
                {
                    fullPath = candidate;
                }

                if (File.Exists(candidate))
                {
                    fullPath = candidate;
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> GetPackageRootCandidates(
            string projectRoot,
            string sourceScriptAssetPath)
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string sourcePackageRoot = GetPackageRootFromScriptAssetPath(projectRoot, sourceScriptAssetPath);
            if (!string.IsNullOrEmpty(sourcePackageRoot) && candidates.Add(sourcePackageRoot))
            {
                yield return sourcePackageRoot;
            }

            string normalizedProjectRoot = Path.GetFullPath(projectRoot);
            if (candidates.Add(normalizedProjectRoot))
            {
                yield return normalizedProjectRoot;
            }

            string legacyPackageRoot = Path.Combine(
                normalizedProjectRoot,
                LegacyPackageRootRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (candidates.Add(legacyPackageRoot))
            {
                yield return legacyPackageRoot;
            }
        }

        private static string GetPackageRootFromScriptAssetPath(string projectRoot, string sourceScriptAssetPath)
        {
            string normalizedAssetPath = (sourceScriptAssetPath ?? string.Empty).Replace('\\', '/');
            int markerIndex = normalizedAssetPath.IndexOf(ScriptAssetPathMarker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return string.Empty;
            }

            string packageRootRelativePath = normalizedAssetPath.Substring(0, markerIndex);
            return ToFullPath(projectRoot, packageRootRelativePath);
        }

        private static string FindSourceScriptAssetPath()
        {
            foreach (string guid in AssetDatabase.FindAssets("PsdHierarchyChatClient t:Script"))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (assetPath.EndsWith("/PsdHierarchyChatClient.cs", StringComparison.OrdinalIgnoreCase))
                {
                    return assetPath;
                }
            }

            return string.Empty;
        }

        private static bool TryReadContextFile(string fullPath, string label, out string content, out string error)
        {
            content = string.Empty;
            if (!File.Exists(fullPath))
            {
                error = label + "不存在：" + fullPath;
                return false;
            }

            var info = new FileInfo(fullPath);
            if (info.Length > MaxContextFileBytes)
            {
                error = label + "过大，不能直接发送给 AI（上限 " + MaxContextFileBytes + " 字节）：" + fullPath;
                return false;
            }

            try
            {
                content = File.ReadAllText(fullPath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(content))
                {
                    error = label + "为空：" + fullPath;
                    return false;
                }
            }
            catch (Exception exception)
            {
                error = "读取" + label + "失败：" + exception.Message;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static string ToFullPath(string projectRoot, string path)
        {
            string candidate = path ?? string.Empty;
            if (!Path.IsPathRooted(candidate))
            {
                candidate = Path.Combine(projectRoot, candidate.Replace('/', Path.DirectorySeparatorChar));
            }

            return Path.GetFullPath(candidate);
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }

    internal sealed class PsdHierarchyChatHttpRequest
    {
        private readonly Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal PsdHierarchyChatHttpRequest(string url, string body)
        {
            this.url = url ?? string.Empty;
            this.body = body ?? string.Empty;
        }

        internal readonly string url;
        internal readonly string body;

        internal void SetHeader(string name, string value)
        {
            headers[name] = value ?? string.Empty;
        }

        internal string GetHeader(string name)
        {
            return headers.TryGetValue(name, out string value) ? value : string.Empty;
        }

        internal IEnumerable<KeyValuePair<string, string>> Headers => headers;
    }

    internal readonly struct PsdHierarchyChatHttpResponse
    {
        internal PsdHierarchyChatHttpResponse(bool success, long statusCode, string body, string error)
        {
            this.success = success;
            this.statusCode = statusCode;
            this.body = body ?? string.Empty;
            this.error = error ?? string.Empty;
        }

        internal readonly bool success;
        internal readonly long statusCode;
        internal readonly string body;
        internal readonly string error;
    }

    internal readonly struct PsdHierarchyChatSendResult
    {
        internal PsdHierarchyChatSendResult(bool success, string message, string cliSessionId = "")
        {
            this.success = success;
            this.message = message ?? string.Empty;
            this.cliSessionId = cliSessionId ?? string.Empty;
        }

        internal readonly bool success;
        internal readonly string message;
        internal readonly string cliSessionId;
    }

    internal interface IPsdHierarchyChatTransport
    {
        Task<PsdHierarchyChatHttpResponse> SendAsync(PsdHierarchyChatHttpRequest request);
    }

    internal interface IPsdHierarchyCliChatTransport
    {
        Task<PsdHierarchyChatSendResult> SendAsync(
            PsdHierarchyChatContext context,
            PsdHierarchyChatConnection connection,
            IReadOnlyList<PsdHierarchyChatMessage> messages,
            string cliSessionId);
    }

    internal readonly struct PsdHierarchyCliInvocation
    {
        internal PsdHierarchyCliInvocation(
            string executablePath,
            string arguments,
            string workingDirectory,
            bool writePromptToStandardInput)
        {
            this.executablePath = executablePath ?? string.Empty;
            this.arguments = arguments ?? string.Empty;
            this.workingDirectory = workingDirectory ?? string.Empty;
            this.writePromptToStandardInput = writePromptToStandardInput;
        }

        internal readonly string executablePath;
        internal readonly string arguments;
        internal readonly string workingDirectory;
        internal readonly bool writePromptToStandardInput;
    }

    internal static class PsdHierarchyChatClient
    {
        internal const string OpenAiEndpoint = "https://api.openai.com/v1/responses";
        internal const string AnthropicEndpoint = "https://api.anthropic.com/v1/messages";
        private const int MaxClaudePromptCharacters = 6000;
        private const string RequiredPlanRootFields =
            "\"version\", \"snapshotFingerprint\", \"prefabAssetPath\", \"output\", \"prefabName\", \"wrappers\", \"moves\", \"renames\", " +
            "\"emptyContainerRemovals\", \"tightBounds\", \"textureRenames\", \"spriteAtlasRenames\", " +
            "\"componentFamilyDecisions\", \"flatSiblingResolutions\", \"componentExtractions\", \"stateComponentExtractions\", " +
            "\"variantComponentExtractions\", \"statefulComponentExtractions\", \"verify\"";
        internal const string PlanIdentifierContract =
            "Every wrappers[].id must use lower snake_case matching [a-z][a-z0-9_]*; examples: screen, screen_root, day_markers. Do not use uppercase, hyphens, spaces, brackets, or @ in an id. The @ prefix is only for a later reference such as @screen_root. Apply the same lower snake_case rule to all extraction IDs and state IDs.";
        internal const string DefaultUserPrompt =
            "请按整理技能完整审查当前目标 Prefab，并输出完整、可确认的层级整理方案，而不是只查看顶层或按名称猜测。\n" +
            "1. 结合 PSD 与 Prefab 的完整层级、节点几何、组件、同级顺序和重复结构，说明当前结构的主要问题。\n" +
            "2. 给出完整的原地整理后树形结构：每个新增语义容器、节点重命名、节点归属和保留顺序都要明确。\n" +
            "3. 对重复视觉单元按整体分组，不要把背景、文本、图标、锁等平铺到按类型命名的大容器中。\n" +
            "4. 标出无法安全推断、存在序列化引用风险或嵌套 Prefab 边界的节点，并说明保持不动的原因。\n" +
            "5. 列出应用前必须验证的布局、组件、引用、激活状态和资源命名不变量。\n" +
            "请严格按以下 5 个章节输出可审查的分析摘要，不要输出原始内部推理：\n" +
            "一、分析摘要：说明观察到的结构与主要问题。\n" +
            "二、分组依据：逐项说明几何、组件、同级顺序或重复结构证据。\n" +
            "三、风险与保留项：说明不移动或不改名节点的具体原因。\n" +
            "四、原地整理方案：给出完整目标树与每项调整。\n" +
            "五、验证清单：列出应用前后必须检查的不变量。\n" +
            "在上述五个章节后，必须额外附上一个完整的 ```json 计划代码块，严格遵循随附计划格式。\n" +
            "本次主界面只原地更新当前目标 Prefab，不创建、复制或另存新的屏幕 Prefab；仅当证据充分时，才可在计划中声明经确认的 Prefab/Common 复用组件。\n" +
            "不要声称已经修改本地文件。用户确认该计划后，Unity 窗口会直接更新 Prefab。";

        internal static string BuildJsonOnlyPlanRepairPrompt(string validationError)
        {
            return BuildJsonOnlyPlanRepairPrompt(validationError, null);
        }

        internal static string BuildJsonOnlyPlanRepairPrompt(
            string validationError,
            PsdHierarchyChatContext context)
        {
            string error = string.IsNullOrWhiteSpace(validationError)
                ? "The plan was incomplete or failed execution-plan validation."
                : validationError.Trim();
            var builder = new StringBuilder();
            builder.AppendLine("The previously returned plan failed Unity execution-plan validation:");
            builder.AppendLine(error);
            builder.AppendLine("Return exactly one complete UTF-8 JSON plan in one fenced ```json code block. Do not output prose, headings, explanations, diffs, or Markdown outside that code block. This must be a full replacement plan, not a patch.");
            builder.AppendLine("Use \"version\": 2 and exactly these required root fields: " + RequiredPlanRootFields + ". Copy snapshotFingerprint exactly from the authoritative snapshot. Use [] for unused operation arrays. Do not use legacy fields wrapperCreations, nodeTransfers, nodeRenames, or privateAssetRenames. prefabAssetPath and output.assetPath must exactly equal the current target Prefab, and output.mode must be in_place.");
            builder.AppendLine(PlanIdentifierContract);
            builder.AppendLine("A reference beginning with @ must be exactly @wrapperId; never write @wrapperId/Child. Every existing-node reference must be node:<id> and must use only node IDs listed in the authoritative snapshot already present in this session. Re-audit every existing-node reference across all operations before returning. A missing ID proves the old operation is invalid: Remove an operation when it cannot be replaced with an exact observed node ID; never invent a node ID, reconstruct one from a name, or emit a raw hierarchy path. Do not ask the user to resend, retry, or confirm.");
            builder.AppendLine("Do not guess or preserve an asset rename expectedGuid. Use an empty expectedGuid string in the version 2 replacement plan; Unity resolves the current GUID from each existing from path.");
            builder.AppendLine("Do not repair prefabName by guessing. For private asset renames, Unity derives the internal prefabName from the reviewed toName values: every texture name must share one '<PrefabName>_' prefix and every SpriteAtlas toName must equal that same PascalCase name ending with View. Repair conflicting toName values themselves; prefabName alone cannot repair the plan.");
            builder.AppendLine("Every verify.directChildren entry must use a non-empty, unique list of direct-child names in post-apply sibling order. List each child name exactly once; never duplicate a name as a placeholder or count.");
            builder.AppendLine("For every variantComponentExtractions entry, states[].source contains exactly one observed representative row per unique visual state, while instances[].source contains every visible list row. A row may reuse a state through instances[].state, so do not require every instance source to appear in states[].source. Every state representative source must appear exactly once in instances, and every instance source must be a distinct direct sibling of template. A variant requires at least two distinct observed visual states. If every visible instance has one observed state, replace the variant extraction and its matching componentFamilyDecision with componentExtractions; never invent a second state.");
            builder.AppendLine("A componentExtraction is valid only when its template and every instance have the same recursive structure. If the failure says 'Repeated unit structure differs for component extraction', do not return that component extraction again. Preserve every mandatory candidate source and use the candidate's observed variant or stateful mode with a complete mapping; never solve a structural mismatch by dropping, shrinking, or reordering sources.");
            builder.AppendLine("For every statefulComponentExtractions instance, commonSourceNames and stateSourceNames together must cover all direct children exactly once. commonSourceNames must contain one observed direct-child name for every common.members entry; stateSourceNames must do the same for the selected states[].members entry. When one side is complete, derive the other as the ordered direct-child complement. Re-read the authoritative snapshot instead of guessing or dropping a member.");
            builder.AppendLine("Enforce this exact equation for every stateful instance: directChildCount == common.members.Count + selectedState.members.Count. If it fails, rebuild common.members, the affected states[].members, and every corresponding instance mapping; changing only commonSourceNames or stateSourceNames cannot repair a contract-count mismatch. Never place the same observed source child in both Common and the selected state.");
            builder.AppendLine("For every flatSiblingFindings entry in the authoritative snapshot, include exactly one flatSiblingResolutions entry with mode=group and wrapperId=<findingId>_group. Unity replaces any AI wrapper, move, or tightBounds details with the deterministic values derived from the finding; do not use keep or an unrelated existing container.");
            AppendRequiredComponentFamilyRepairContract(builder, context);
            AppendFlatSiblingRepairContract(builder, context);
            return builder.ToString();
        }

        private static void AppendRequiredComponentFamilyRepairContract(
            StringBuilder builder,
            PsdHierarchyChatContext context)
        {
            PsdHierarchyComponentFamilyCandidate[] requiredCandidates = context?.componentFamilyCandidates?
                .Where(candidate => candidate.requiresExtraction)
                .ToArray() ?? Array.Empty<PsdHierarchyComponentFamilyCandidate>();
            if (requiredCandidates.Length == 0)
            {
                return;
            }

            builder.AppendLine("The following are authoritative mandatory component-family records. For EVERY record, include exactly one componentFamilyDecisions entry that copies candidateId, parent, and sources exactly and in the listed order. mode must not be skip. Use the recommendedMode unless the supplied snapshot proves another executable extraction mode. Each decision must name a lower_snake_case extractionId, and exactly one matching entry with that id must appear in componentExtractions, stateComponentExtractions, variantComponentExtractions, or statefulComponentExtractions. Do not omit, merge, shrink, reorder, or replace any source list. Do not echo this list outside your replacement JSON plan.");
            var records = new JArray(requiredCandidates.Select(candidate => new JObject
            {
                ["candidateId"] = candidate.id,
                ["suggestedAssetName"] = candidate.suggestedAssetName,
                ["recommendedMode"] = candidate.recommendedMode,
                ["parent"] = candidate.parent,
                ["sources"] = new JArray(candidate.sources),
                ["sourceStructures"] = BuildRequiredCandidateSourceStructures(context, candidate),
            }));
            builder.AppendLine("===== BEGIN REQUIRED COMPONENT FAMILIES =====");
            builder.AppendLine(records.ToString(Formatting.None));
            builder.AppendLine("===== END REQUIRED COMPONENT FAMILIES =====");
        }

        private static void AppendFlatSiblingRepairContract(
            StringBuilder builder,
            PsdHierarchyChatContext context)
        {
            JArray findings = context?.flatSiblingFindings;
            if (findings == null || findings.Count == 0)
            {
                return;
            }

            var records = new JArray();
            foreach (JObject finding in findings.OfType<JObject>())
            {
                string id = finding.Value<string>("id");
                string parent = finding.Value<string>("parent");
                string background = finding.Value<string>("background");
                JArray members = finding["members"] as JArray;
                if (string.IsNullOrWhiteSpace(id) ||
                    string.IsNullOrWhiteSpace(parent) ||
                    string.IsNullOrWhiteSpace(background) ||
                    members == null ||
                    members.Count < 3)
                {
                    throw new InvalidDataException(
                        "Cannot build authoritative repair context for flat sibling finding " +
                        (id ?? "<missing>") + ".");
                }

                records.Add(new JObject
                {
                    ["id"] = id,
                    ["parent"] = parent,
                    ["background"] = background,
                    ["members"] = members.DeepClone(),
                });
            }

            if (records.Count == 0)
            {
                throw new InvalidDataException(
                    "Cannot build authoritative repair context because flatSiblingFindings contains no records.");
            }

            builder.AppendLine("The following are authoritative flat sibling finding records. For EVERY record, include exactly one flatSiblingResolutions entry with findingId copied exactly, mode=group, and wrapperId=<findingId>_group. Unity deterministically derives parent, siblingIndex, member moves, and tightBounds from this list; do not echo this list outside your replacement JSON plan.");
            builder.AppendLine("===== BEGIN FLAT SIBLING FINDINGS =====");
            builder.AppendLine(records.ToString(Formatting.None));
            builder.AppendLine("===== END FLAT SIBLING FINDINGS =====");
        }

        private static JArray BuildRequiredCandidateSourceStructures(
            PsdHierarchyChatContext context,
            PsdHierarchyComponentFamilyCandidate candidate)
        {
            var structures = new JArray();
            foreach (string source in candidate.sources)
            {
                string nodeId = source != null && source.StartsWith("node:", StringComparison.Ordinal)
                    ? source.Substring("node:".Length)
                    : string.Empty;
                if (string.IsNullOrEmpty(nodeId) ||
                    !context.TryGetNodePath(nodeId, out string sourcePath) ||
                    !context.TryGetDirectChildren(
                        sourcePath,
                        out IReadOnlyList<PsdHierarchySnapshotChild> directChildren))
                {
                    throw new InvalidDataException(
                        "Cannot build authoritative repair context for mandatory component family " +
                        candidate.id + ": source " + source + " has no direct-child evidence in the snapshot.");
                }

                structures.Add(new JObject
                {
                    ["source"] = source,
                    ["directChildren"] = new JArray(directChildren.Select(child => child.name)),
                });
            }

            return structures;
        }

        internal static string BuildClaudeDirectPrompt(
            PsdHierarchyChatContext context,
            IReadOnlyList<PsdHierarchyChatMessage> messages)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            PsdHierarchyChatMessage[] normalized = NormalizeMessages(messages);
            string userPrompt = DefaultUserPrompt;
            for (int index = normalized.Length - 1; index >= 0; index--)
            {
                if (string.Equals(normalized[index].role, "user", StringComparison.Ordinal))
                {
                    userPrompt = normalized[index].content;
                    break;
                }
            }

            if (userPrompt.Length > MaxClaudePromptCharacters)
            {
                userPrompt = userPrompt.Substring(0, MaxClaudePromptCharacters) + "\n[后续追问已截断]";
            }

            var builder = new StringBuilder();
            builder.AppendLine("You are reviewing one existing Unity Prefab hierarchy from inside a Unity Editor tool.");
            builder.AppendLine("Use the Read tool to inspect exactly these three files before answering:");
            builder.AppendLine("1. Cleanup skill: " + context.skillFullPath);
            builder.AppendLine("2. Executable plan format: " + PsdHierarchyChatContextBuilder.PlanFormatFullPath(context.projectRoot));
            builder.AppendLine("3. Authoritative Prefab node snapshot: " + context.hierarchySnapshotFullPath);
            builder.AppendLine("Do not use any other tool. Do not edit, create, rename, or delete any file.");
            builder.AppendLine("Return a concise, reviewable hierarchy-cleanup plan in Simplified Chinese with exactly these five sections: 分析摘要, 分组依据, 风险与保留项, 原地整理方案, 验证清单.");
            builder.AppendLine("After those five sections, return exactly one complete UTF-8 JSON plan in a fenced ```json code block. The JSON is an executable contract, not illustrative pseudo-JSON.");
            builder.AppendLine("Use exactly these required root fields: " + RequiredPlanRootFields + ". Use [] for every unused operation array.");
            builder.AppendLine("Do not use legacy field names such as wrapperCreations, nodeTransfers, nodeRenames, or privateAssetRenames. The main Prefab output must be in_place at the exact target path.");
            builder.AppendLine("Use version 2 and copy snapshotFingerprint exactly from the authoritative snapshot.");
            builder.AppendLine("A reference beginning with @ must be exactly @wrapperId; never write @wrapperId/Child. Every reference to an existing node must use node:<id> from the authoritative snapshot. Never emit a raw hierarchy path or invent a node ID.");
            builder.AppendLine(PlanIdentifierContract);
            builder.AppendLine("For textureRenames and spriteAtlasRenames in this version 2 AI plan, set expectedGuid to an empty string. Unity validates the from asset and injects its current AssetDatabase GUID before execution.");
            builder.AppendLine("Private-asset naming is reviewed through textureRenames[].toName and spriteAtlasRenames[].toName. When either array is non-empty, Unity derives the internal prefabName from their one common PascalCase name ending with View. Keep prefabName present, but do not guess it independently or use it to hide conflicting toName values.");
            builder.AppendLine("If evidence supports a reusable component, state, variant, or stateful extraction, include the complete reviewed extraction contract. Use componentFamilyDecisions for every candidate. A candidate marked requiresExtraction:false is advisory and may be skipped with concrete recursive-structure evidence; repeated names alone do not justify a variant extraction.");
            builder.AppendLine("When the snapshot includes flatSiblingFindings, set every finding's flatSiblingResolutions mode to group and wrapperId to <findingId>_group. Unity deterministically derives parent, siblingIndex, member moves, and tightBounds from the authoritative finding; never use keep or an unrelated existing container.");
            builder.AppendLine("For a mandatory variant family, create one observed state for every distinct recursive structure and map every source to an exact matching state. Even when every source has a distinct structure, the mandatory family must not be skipped or reduced to hierarchy-only cleanup.");
            builder.AppendLine("For variantComponentExtractions, choose one observed representative row in states[].source for each unique visual state. Put every visible repeated row in instances[] exactly once, and set its state to the selected representative state ID; multiple instance rows may use the same state ID. Every states[].source must also appear in instances[].source, but extra instances must not be added as duplicate states. Use a variant only when at least two distinct observed visual states exist. When all visible rows have one state, use componentExtractions and a matching componentFamilyDecisions mode instead; never invent a second state.");
            builder.AppendLine("Every variant instance must have the same recursive component/child signature and RectTransform count as its selected states[].source. If no observed state representative matches exactly, skip the advisory family instead of generating an unsafe extraction.");
            builder.AppendLine("For every stateful instance, commonSourceNames plus stateSourceNames must cover every direct child exactly once. Map commonSourceNames in common.members order and stateSourceNames in the selected states[].members order. If one side is complete, derive the other from the authoritative ordered direct-child complement; do not omit repeated members.");
            builder.AppendLine("The executable plan-format file is authoritative. Follow its field names and object shapes exactly.");
            builder.AppendLine("Do not claim that a local asset was changed.");
            builder.AppendLine("User request:");
            builder.Append(userPrompt);
            return builder.ToString();
        }

        internal static PsdHierarchyChatHttpRequest BuildRequest(
            PsdHierarchyChatContext context,
            PsdHierarchyChatConnection connection,
            IReadOnlyList<PsdHierarchyChatMessage> messages)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!connection.TryValidate(out string validationError))
            {
                throw new ArgumentException(validationError, nameof(connection));
            }

            if (connection.connectionMode != PsdHierarchyAiConnectionMode.CustomApi)
            {
                throw new ArgumentException("仅自定义 API 连接可以构建 HTTP 请求。", nameof(connection));
            }

            return connection.provider == PsdHierarchyAiProvider.Codex
                ? BuildOpenAiRequest(context, connection, messages)
                : BuildAnthropicRequest(context, connection, messages);
        }

        internal static async Task<PsdHierarchyChatSendResult> SendAsync(
            PsdHierarchyChatContext context,
            PsdHierarchyChatConnection connection,
            IReadOnlyList<PsdHierarchyChatMessage> messages,
            IPsdHierarchyChatTransport transport = null,
            IPsdHierarchyCliChatTransport cliTransport = null)
        {
            return await SendWithCliSessionAsync(
                context,
                connection,
                messages,
                string.Empty,
                transport,
                cliTransport);
        }

        internal static async Task<PsdHierarchyChatSendResult> SendWithCliSessionAsync(
            PsdHierarchyChatContext context,
            PsdHierarchyChatConnection connection,
            IReadOnlyList<PsdHierarchyChatMessage> messages,
            string cliSessionId,
            IPsdHierarchyChatTransport transport = null,
            IPsdHierarchyCliChatTransport cliTransport = null)
        {
            if (!connection.TryValidate(out string validationError))
            {
                return new PsdHierarchyChatSendResult(false, validationError);
            }

            if (connection.connectionMode == PsdHierarchyAiConnectionMode.LocalCli)
            {
                try
                {
                    return await (cliTransport ?? new ProcessCliChatTransport()).SendAsync(
                        context,
                        connection,
                        messages,
                        cliSessionId);
                }
                catch (Exception exception)
                {
                    return new PsdHierarchyChatSendResult(false, "AI CLI 调用失败：" + exception.Message);
                }
            }

            PsdHierarchyChatHttpRequest request;
            try
            {
                request = BuildRequest(context, connection, messages);
            }
            catch (Exception exception)
            {
                return new PsdHierarchyChatSendResult(false, "无法构建 AI 请求：" + exception.Message);
            }

            PsdHierarchyChatHttpResponse response;
            try
            {
                response = await (transport ?? new UnityWebRequestChatTransport()).SendAsync(request);
            }
            catch (Exception exception)
            {
                return new PsdHierarchyChatSendResult(false, "AI 请求失败：" + exception.Message);
            }

            return ParseResponse(connection.provider, response);
        }

        internal static string DefaultEndpoint(PsdHierarchyAiProvider provider)
        {
            return provider == PsdHierarchyAiProvider.Codex ? OpenAiEndpoint : AnthropicEndpoint;
        }

        internal static string DefaultModel(PsdHierarchyAiProvider provider)
        {
            return provider == PsdHierarchyAiProvider.Codex ? "gpt-5" : "claude-sonnet-5";
        }

        internal static string GetProviderDisplayName(PsdHierarchyAiProvider provider)
        {
            return provider == PsdHierarchyAiProvider.Codex ? "Codex" : "Claude";
        }

        internal static string GetModelDisplayName(PsdHierarchyChatConnection connection)
        {
            return connection.connectionMode == PsdHierarchyAiConnectionMode.LocalCli
                ? "本地 CLI 默认"
                : connection.model.Trim();
        }

        internal static bool TryOpenInteractiveCli(
            PsdHierarchyChatConnection connection,
            string projectRoot,
            string cliSessionId,
            out string error)
        {
            if (connection.connectionMode != PsdHierarchyAiConnectionMode.LocalCli)
            {
                error = "当前会话使用自定义 API，不能打开本地 CLI。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(cliSessionId))
            {
                error = "当前对话尚未收到可恢复的 CLI 会话 ID。";
                return false;
            }

            if (!connection.TryValidate(out error))
            {
                return false;
            }

            try
            {
                PsdHierarchyCliInvocation invocation = CreateInteractiveCliInvocation(
                    connection,
                    projectRoot,
                    cliSessionId);
                Process.Start(new ProcessStartInfo
                {
                    FileName = invocation.executablePath,
                    Arguments = invocation.arguments,
                    WorkingDirectory = invocation.workingDirectory,
                    UseShellExecute = true,
                });
            }
            catch (Exception exception)
            {
                error = "打开本次对话的 CLI 失败：" + exception.Message;
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal static PsdHierarchyCliInvocation CreateCliInvocation(
            PsdHierarchyChatConnection connection,
            string workingDirectory)
        {
            return CreateCliInvocation(connection, workingDirectory, string.Empty, string.Empty, false);
        }

        internal static PsdHierarchyCliInvocation CreateCliInvocation(
            PsdHierarchyChatConnection connection,
            string workingDirectory,
            string prompt)
        {
            return CreateCliInvocation(connection, workingDirectory, prompt, string.Empty, false);
        }

        internal static PsdHierarchyCliInvocation CreateCliInvocation(
            PsdHierarchyChatConnection connection,
            string workingDirectory,
            string prompt,
            string cliSessionId,
            bool resumeCliSession)
        {
            if (connection.connectionMode != PsdHierarchyAiConnectionMode.LocalCli)
            {
                throw new ArgumentException("仅默认 CLI 连接可以构建 CLI 调用。", nameof(connection));
            }

            bool hasSessionId = !string.IsNullOrWhiteSpace(cliSessionId);
            if (resumeCliSession && !hasSessionId)
            {
                throw new ArgumentException("恢复 CLI 会话时必须提供会话 ID。", nameof(cliSessionId));
            }

            string sessionArguments = connection.provider == PsdHierarchyAiProvider.Claude && hasSessionId
                ? (resumeCliSession ? " --resume " : " --session-id ") + QuoteProcessArgument(cliSessionId)
                : string.Empty;
            if (connection.provider == PsdHierarchyAiProvider.Claude && !string.IsNullOrWhiteSpace(prompt))
            {
                string directClaudeExecutable = ResolveClaudeDirectExecutable(connection.cliExecutablePath);
                if (!string.IsNullOrEmpty(directClaudeExecutable))
                {
                    return new PsdHierarchyCliInvocation(
                        directClaudeExecutable,
                        "--print --output-format json --permission-mode dontAsk --safe-mode " +
                        "--tools Read --add-dir " + QuoteProcessArgument(workingDirectory) +
                        sessionArguments,
                        workingDirectory,
                        true);
                }
            }

            string arguments = connection.provider == PsdHierarchyAiProvider.Claude
                ? "--print --output-format json --permission-mode plan --safe-mode" + sessionArguments
                : resumeCliSession
                    ? "exec resume --json " + QuoteProcessArgument(cliSessionId) + " -"
                    : "exec --json --sandbox read-only -";
            string cliPath = connection.cliExecutablePath;
            if (cliPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                cliPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                string commandProcessor = Environment.GetEnvironmentVariable("ComSpec");
                if (string.IsNullOrWhiteSpace(commandProcessor))
                {
                    commandProcessor = "cmd.exe";
                }

                return new PsdHierarchyCliInvocation(
                    commandProcessor,
                    "/d /s /c \"\"" + cliPath.Replace("\"", "\"\"") + "\" " + arguments + "\"",
                    workingDirectory,
                    true);
            }

            return new PsdHierarchyCliInvocation(cliPath, arguments, workingDirectory, true);
        }

        internal static PsdHierarchyCliInvocation CreateInteractiveCliInvocation(
            PsdHierarchyChatConnection connection,
            string workingDirectory,
            string cliSessionId)
        {
            if (connection.connectionMode != PsdHierarchyAiConnectionMode.LocalCli)
            {
                throw new ArgumentException("仅本地 CLI 连接可以打开交互式会话。", nameof(connection));
            }

            if (string.IsNullOrWhiteSpace(cliSessionId))
            {
                throw new ArgumentException("恢复 CLI 会话时必须提供会话 ID。", nameof(cliSessionId));
            }

            string arguments = connection.provider == PsdHierarchyAiProvider.Claude
                ? "--resume " + QuoteProcessArgument(cliSessionId) +
                  " --permission-mode plan --safe-mode --add-dir " + QuoteProcessArgument(workingDirectory)
                : "-s read-only resume " + QuoteProcessArgument(cliSessionId);
            string commandProcessor = Environment.GetEnvironmentVariable("ComSpec");
            if (string.IsNullOrWhiteSpace(commandProcessor))
            {
                commandProcessor = "cmd.exe";
            }

            return new PsdHierarchyCliInvocation(
                commandProcessor,
                "/d /s /k \"\"" + connection.cliExecutablePath.Replace("\"", "\"\"") +
                "\" " + arguments + "\"",
                workingDirectory,
                false);
        }

        private static string ResolveClaudeDirectExecutable(string cliExecutablePath)
        {
            if (string.IsNullOrWhiteSpace(cliExecutablePath))
            {
                return string.Empty;
            }

            if (cliExecutablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return cliExecutablePath;
            }

            if (!cliExecutablePath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) &&
                !cliExecutablePath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            string npmDirectory = Path.GetDirectoryName(cliExecutablePath);
            return string.IsNullOrEmpty(npmDirectory)
                ? string.Empty
                : Path.Combine(
                    npmDirectory,
                    "node_modules",
                    "@anthropic-ai",
                    "claude-code",
                    "bin",
                    "claude.exe");
        }

        private static string QuoteProcessArgument(string value)
        {
            string input = value ?? string.Empty;
            var builder = new StringBuilder(input.Length + 2);
            builder.Append('"');
            int slashCount = 0;
            for (int index = 0; index < input.Length; index++)
            {
                char character = input[index];
                if (character == '\\')
                {
                    slashCount++;
                    continue;
                }

                if (character == '"')
                {
                    builder.Append('\\', slashCount * 2 + 1);
                    builder.Append(character);
                    slashCount = 0;
                    continue;
                }

                builder.Append('\\', slashCount);
                slashCount = 0;
                builder.Append(character);
            }

            builder.Append('\\', slashCount * 2);
            builder.Append('"');
            return builder.ToString();
        }

        internal static string ResolveUserPrompt(string prompt)
        {
            return string.IsNullOrWhiteSpace(prompt) ? DefaultUserPrompt : prompt.Trim();
        }

        internal static PsdHierarchyChatSendResult ParseResponse(
            PsdHierarchyAiProvider provider,
            PsdHierarchyChatHttpResponse response)
        {
            if (!response.success)
            {
                string apiError = TryExtractErrorMessage(response.body);
                string detail = string.IsNullOrEmpty(apiError) ? response.error : apiError;
                if (string.IsNullOrEmpty(detail))
                {
                    detail = "HTTP " + response.statusCode;
                }

                return new PsdHierarchyChatSendResult(false, "AI 请求失败：" + detail);
            }

            try
            {
                string text = provider == PsdHierarchyAiProvider.Codex
                    ? ExtractOpenAiText(response.body)
                    : ExtractAnthropicText(response.body);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return new PsdHierarchyChatSendResult(false, "AI 未返回可显示的文本。");
                }

                return new PsdHierarchyChatSendResult(true, text.Trim());
            }
            catch (Exception exception)
            {
                return new PsdHierarchyChatSendResult(false, "解析 AI 返回内容失败：" + exception.Message);
            }
        }

        private static PsdHierarchyChatHttpRequest BuildOpenAiRequest(
            PsdHierarchyChatContext context,
            PsdHierarchyChatConnection connection,
            IReadOnlyList<PsdHierarchyChatMessage> messages)
        {
            var request = new OpenAiRequest
            {
                model = connection.model.Trim(),
                instructions = context.BuildInstructions(),
                input = BuildOpenAiMessages(messages),
                store = false,
            };
            var httpRequest = new PsdHierarchyChatHttpRequest(connection.endpoint, JsonUtility.ToJson(request));
            httpRequest.SetHeader("Content-Type", "application/json");
            httpRequest.SetHeader("Authorization", "Bearer " + connection.apiKey.Trim());
            return httpRequest;
        }

        private static PsdHierarchyChatHttpRequest BuildAnthropicRequest(
            PsdHierarchyChatContext context,
            PsdHierarchyChatConnection connection,
            IReadOnlyList<PsdHierarchyChatMessage> messages)
        {
            var request = new AnthropicRequest
            {
                model = connection.model.Trim(),
                max_tokens = 4096,
                system = context.BuildInstructions(),
                messages = BuildAnthropicMessages(messages),
            };
            var httpRequest = new PsdHierarchyChatHttpRequest(connection.endpoint, JsonUtility.ToJson(request));
            httpRequest.SetHeader("Content-Type", "application/json");
            httpRequest.SetHeader("x-api-key", connection.apiKey.Trim());
            httpRequest.SetHeader("anthropic-version", "2023-06-01");
            return httpRequest;
        }

        private static OpenAiMessage[] BuildOpenAiMessages(IReadOnlyList<PsdHierarchyChatMessage> messages)
        {
            PsdHierarchyChatMessage[] normalized = NormalizeMessages(messages);
            var result = new OpenAiMessage[normalized.Length];
            for (int index = 0; index < normalized.Length; index++)
            {
                result[index] = new OpenAiMessage
                {
                    role = normalized[index].role,
                    content = normalized[index].content,
                };
            }

            return result;
        }

        private static AnthropicMessage[] BuildAnthropicMessages(IReadOnlyList<PsdHierarchyChatMessage> messages)
        {
            PsdHierarchyChatMessage[] normalized = NormalizeMessages(messages);
            var result = new AnthropicMessage[normalized.Length];
            for (int index = 0; index < normalized.Length; index++)
            {
                result[index] = new AnthropicMessage
                {
                    role = normalized[index].role,
                    content = normalized[index].content,
                };
            }

            return result;
        }

        private static PsdHierarchyChatMessage[] NormalizeMessages(IReadOnlyList<PsdHierarchyChatMessage> messages)
        {
            if (messages == null || messages.Count == 0)
            {
                return new[] { new PsdHierarchyChatMessage("user", DefaultUserPrompt) };
            }

            var result = new List<PsdHierarchyChatMessage>(messages.Count);
            for (int index = 0; index < messages.Count; index++)
            {
                PsdHierarchyChatMessage message = messages[index];
                if (string.IsNullOrWhiteSpace(message.content))
                {
                    continue;
                }

                string role = string.Equals(message.role, "assistant", StringComparison.OrdinalIgnoreCase)
                    ? "assistant"
                    : "user";
                string content = message.content.Trim();
                int previousIndex = result.Count - 1;
                if (previousIndex >= 0 && string.Equals(result[previousIndex].role, role, StringComparison.Ordinal))
                {
                    PsdHierarchyChatMessage previous = result[previousIndex];
                    result[previousIndex] = new PsdHierarchyChatMessage(
                        role,
                        previous.content + "\n\n" + content);
                    continue;
                }

                result.Add(new PsdHierarchyChatMessage(role, content));
            }

            return result.Count == 0
                ? new[] { new PsdHierarchyChatMessage("user", DefaultUserPrompt) }
                : result.ToArray();
        }

        private static string ExtractOpenAiText(string json)
        {
            OpenAiResponse response = JsonUtility.FromJson<OpenAiResponse>(json);
            if (response == null || response.output == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (OpenAiOutput output in response.output)
            {
                if (output == null || output.content == null)
                {
                    continue;
                }

                foreach (OpenAiContent content in output.content)
                {
                    if (content != null && !string.IsNullOrWhiteSpace(content.text))
                    {
                        if (builder.Length > 0)
                        {
                            builder.AppendLine();
                        }

                        builder.Append(content.text);
                    }
                }
            }

            return builder.ToString();
        }

        private static string ExtractAnthropicText(string json)
        {
            AnthropicResponse response = JsonUtility.FromJson<AnthropicResponse>(json);
            if (response == null || response.content == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (AnthropicContent content in response.content)
            {
                if (content != null && !string.IsNullOrWhiteSpace(content.text))
                {
                    if (builder.Length > 0)
                    {
                        builder.AppendLine();
                    }

                    builder.Append(content.text);
                }
            }

            return builder.ToString();
        }

        private static string TryExtractErrorMessage(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            try
            {
                ApiErrorEnvelope envelope = JsonUtility.FromJson<ApiErrorEnvelope>(json);
                return envelope != null && envelope.error != null ? envelope.error.message ?? string.Empty : string.Empty;
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
        }

        [Serializable]
        private sealed class OpenAiRequest
        {
            public string model;
            public string instructions;
            public OpenAiMessage[] input;
            public bool store;
        }

        [Serializable]
        private sealed class OpenAiMessage
        {
            public string role;
            public string content;
        }

        [Serializable]
        private sealed class AnthropicRequest
        {
            public string model;
            public int max_tokens;
            public string system;
            public AnthropicMessage[] messages;
        }

        [Serializable]
        private sealed class AnthropicMessage
        {
            public string role;
            public string content;
        }

        [Serializable]
        private sealed class OpenAiResponse
        {
            public OpenAiOutput[] output;
        }

        [Serializable]
        private sealed class OpenAiOutput
        {
            public OpenAiContent[] content;
        }

        [Serializable]
        private sealed class OpenAiContent
        {
            public string text;
        }

        [Serializable]
        private sealed class AnthropicResponse
        {
            public AnthropicContent[] content;
        }

        [Serializable]
        private sealed class AnthropicContent
        {
            public string text;
        }

        [Serializable]
        private sealed class ApiErrorEnvelope
        {
            public ApiError error;
        }

        [Serializable]
        private sealed class ApiError
        {
            public string message;
        }

        private sealed class UnityWebRequestChatTransport : IPsdHierarchyChatTransport
        {
            public async Task<PsdHierarchyChatHttpResponse> SendAsync(PsdHierarchyChatHttpRequest request)
            {
                using (var webRequest = new UnityWebRequest(request.url, UnityWebRequest.kHttpVerbPOST))
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.body));
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    foreach (KeyValuePair<string, string> header in request.Headers)
                    {
                        webRequest.SetRequestHeader(header.Key, header.Value);
                    }

                    UnityWebRequestAsyncOperation operation = webRequest.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    bool success = webRequest.result == UnityWebRequest.Result.Success;
                    return new PsdHierarchyChatHttpResponse(
                        success,
                        webRequest.responseCode,
                        webRequest.downloadHandler.text,
                        webRequest.error);
                }
            }
        }

        private sealed class ProcessCliChatTransport : IPsdHierarchyCliChatTransport
        {
            public async Task<PsdHierarchyChatSendResult> SendAsync(
                PsdHierarchyChatContext context,
                PsdHierarchyChatConnection connection,
                IReadOnlyList<PsdHierarchyChatMessage> messages,
                string cliSessionId)
            {
                bool resumeCliSession = !string.IsNullOrWhiteSpace(cliSessionId);
                string requestedSessionId = connection.provider == PsdHierarchyAiProvider.Claude && !resumeCliSession
                    ? Guid.NewGuid().ToString()
                    : cliSessionId;
                string prompt = resumeCliSession
                    ? LastUserMessage(messages)
                    : connection.provider == PsdHierarchyAiProvider.Claude
                        ? BuildClaudeDirectPrompt(context, messages)
                        : BuildCliPrompt(context, messages);
                PsdHierarchyCliInvocation invocation = CreateCliInvocation(
                    connection,
                    context.projectRoot,
                    prompt,
                    requestedSessionId,
                    resumeCliSession);
                var startInfo = new ProcessStartInfo
                {
                    FileName = invocation.executablePath,
                    Arguments = invocation.arguments,
                    WorkingDirectory = invocation.workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardInputEncoding = new UTF8Encoding(false),
                    StandardOutputEncoding = new UTF8Encoding(false),
                    StandardErrorEncoding = new UTF8Encoding(false),
                    CreateNoWindow = true,
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return new PsdHierarchyChatSendResult(false, "无法启动所选 AI CLI。" );
                    }

                    if (invocation.writePromptToStandardInput)
                    {
                        await process.StandardInput.WriteAsync(prompt);
                    }
                    process.StandardInput.Close();
                    Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                    Task<string> errorTask = process.StandardError.ReadToEndAsync();
                    await WaitForExitAsync(process);

                    string output = await outputTask;
                    string error = await errorTask;
                    if (process.ExitCode != 0)
                    {
                        return new PsdHierarchyChatSendResult(
                            false,
                            "AI CLI 返回错误：" + FirstNonEmptyLine(error, output, "退出码 " + process.ExitCode));
                    }

                    if (string.IsNullOrWhiteSpace(output))
                    {
                        return new PsdHierarchyChatSendResult(false, "AI CLI 未返回可显示的文本。" );
                    }

                    return ParseCliResponse(connection.provider, output, requestedSessionId);
                }
            }

            private static PsdHierarchyChatSendResult ParseCliResponse(
                PsdHierarchyAiProvider provider,
                string output,
                string fallbackSessionId)
            {
                return provider == PsdHierarchyAiProvider.Claude
                    ? ParseClaudeCliResponse(output, fallbackSessionId)
                    : ParseCodexCliResponse(output, fallbackSessionId);
            }

            private static PsdHierarchyChatSendResult ParseClaudeCliResponse(
                string output,
                string fallbackSessionId)
            {
                try
                {
                    ClaudeCliResult response = JsonUtility.FromJson<ClaudeCliResult>(output);
                    string message = response == null ? string.Empty : response.result;
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        return new PsdHierarchyChatSendResult(false, "Claude CLI 未返回可显示的文本。");
                    }

                    string sessionId = !string.IsNullOrWhiteSpace(response.session_id)
                        ? response.session_id
                        : fallbackSessionId;
                    if (string.IsNullOrWhiteSpace(sessionId))
                    {
                        return new PsdHierarchyChatSendResult(false, "Claude CLI 未返回可恢复的会话 ID。");
                    }

                    return new PsdHierarchyChatSendResult(true, message.Trim(), sessionId);
                }
                catch (ArgumentException exception)
                {
                    return new PsdHierarchyChatSendResult(false, "解析 Claude CLI 会话失败：" + exception.Message);
                }
            }

            private static PsdHierarchyChatSendResult ParseCodexCliResponse(
                string output,
                string fallbackSessionId)
            {
                string sessionId = fallbackSessionId;
                var message = new StringBuilder();
                using (var reader = new StringReader(output))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        try
                        {
                            CodexCliEvent cliEvent = JsonUtility.FromJson<CodexCliEvent>(line);
                            if (cliEvent == null)
                            {
                                continue;
                            }

                            if (string.Equals(cliEvent.type, "thread.started", StringComparison.Ordinal) &&
                                !string.IsNullOrWhiteSpace(cliEvent.thread_id))
                            {
                                sessionId = cliEvent.thread_id;
                            }

                            if (string.Equals(cliEvent.type, "item.completed", StringComparison.Ordinal) &&
                                cliEvent.item != null &&
                                string.Equals(cliEvent.item.type, "agent_message", StringComparison.Ordinal) &&
                                !string.IsNullOrWhiteSpace(cliEvent.item.text))
                            {
                                if (message.Length > 0)
                                {
                                    message.AppendLine();
                                }

                                message.Append(cliEvent.item.text);
                            }
                        }
                        catch (ArgumentException)
                        {
                            // Ignore non-event lines because Codex may write transport diagnostics to stdout.
                        }
                    }
                }

                if (message.Length == 0)
                {
                    return new PsdHierarchyChatSendResult(false, "Codex CLI 未返回可显示的文本。");
                }

                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    return new PsdHierarchyChatSendResult(false, "Codex CLI 未返回可恢复的会话 ID。");
                }

                return new PsdHierarchyChatSendResult(true, message.ToString().Trim(), sessionId);
            }

            private static async Task WaitForExitAsync(Process process)
            {
                while (!process.HasExited)
                {
                    await Task.Delay(50);
                }
            }

            private static string BuildCliPrompt(
                PsdHierarchyChatContext context,
                IReadOnlyList<PsdHierarchyChatMessage> messages)
            {
                var builder = new StringBuilder(context.BuildInstructions());
                builder.AppendLine();
                builder.AppendLine("===== BEGIN CHAT HISTORY =====");
                PsdHierarchyChatMessage[] normalized = NormalizeMessages(messages);
                foreach (PsdHierarchyChatMessage message in normalized)
                {
                    builder.AppendLine("[" + message.role + "]");
                    builder.AppendLine(message.content);
                }

                builder.AppendLine("===== END CHAT HISTORY =====");
                return builder.ToString();
            }

            [Serializable]
            private sealed class ClaudeCliResult
            {
                public string result;
                public string session_id;
            }

            [Serializable]
            private sealed class CodexCliEvent
            {
                public string type;
                public string thread_id;
                public CodexCliItem item;
            }

            [Serializable]
            private sealed class CodexCliItem
            {
                public string type;
                public string text;
            }

            private static string BuildClaudeDirectPrompt(
                PsdHierarchyChatContext context,
                IReadOnlyList<PsdHierarchyChatMessage> messages)
            {
                return PsdHierarchyChatClient.BuildClaudeDirectPrompt(context, messages);
            }

            private static string LastUserMessage(IReadOnlyList<PsdHierarchyChatMessage> messages)
            {
                PsdHierarchyChatMessage[] normalized = NormalizeMessages(messages);
                for (int index = normalized.Length - 1; index >= 0; index--)
                {
                    if (string.Equals(normalized[index].role, "user", StringComparison.Ordinal))
                    {
                        return normalized[index].content;
                    }
                }

                return DefaultUserPrompt;
            }

            private static string TrimPrompt(string prompt)
            {
                string value = prompt ?? string.Empty;
                if (value.Length <= MaxClaudePromptCharacters)
                {
                    return value;
                }

                return value.Substring(0, MaxClaudePromptCharacters) + "\n[后续追问已截断]";
            }

            private static string FirstNonEmptyLine(string first, string second, string fallback)
            {
                string[] candidates = { first, second };
                foreach (string candidate in candidates)
                {
                    if (string.IsNullOrWhiteSpace(candidate))
                    {
                        continue;
                    }

                    using (var reader = new StringReader(candidate))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                return line.Trim();
                            }
                        }
                    }
                }

                return fallback;
            }
        }
    }
}
