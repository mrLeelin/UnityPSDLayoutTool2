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
            string apiKey,
            string reasoningEffort = null)
        {
            this.provider = provider;
            this.connectionMode = connectionMode;
            this.cliExecutablePath = cliExecutablePath ?? string.Empty;
            this.endpoint = endpoint ?? string.Empty;
            this.model = model ?? string.Empty;
            this.apiKey = apiKey ?? string.Empty;
            this.reasoningEffort = reasoningEffort ?? string.Empty;
        }

        internal readonly PsdHierarchyAiProvider provider;
        internal readonly PsdHierarchyAiConnectionMode connectionMode;
        internal readonly string cliExecutablePath;
        internal readonly string endpoint;
        internal readonly string model;
        internal readonly string apiKey;

        /// <summary>思考程度。留空表示不传该参数，交给 CLI 自身配置。</summary>
        internal readonly string reasoningEffort;

        internal bool TryValidate(out string error)
        {
            if (provider == PsdHierarchyAiProvider.None)
            {
                error = "尚未选择 AI 模型。请先打开全局配置，在「AI 层级整理」里选择一个本机已安装的 CLI。";
                return false;
            }

            if (!PsdHierarchyAiCliDiscovery.TryGetSupported(provider, out _))
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
            string hierarchySnapshotFullPath = "",
            IReadOnlyList<string> assetRenameSourcePaths = null,
            string sourcePsdInfo = "")
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
            this.sourcePsdInfo = sourcePsdInfo ?? string.Empty;
            hasAuthoritativeAssetRenameSourcePaths = assetRenameSourcePaths != null;
            this.assetRenameSourcePaths = (assetRenameSourcePaths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Replace('\\', '/').Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            assetRenameSourcePathSet = new HashSet<string>(
                this.assetRenameSourcePaths,
                StringComparer.Ordinal);
            nodePathsById = ParseNodePaths(this.hierarchySnapshotJson);
            nodeIdsByPath = nodePathsById.ToDictionary(
                pair => pair.Value,
                pair => pair.Key,
                StringComparer.Ordinal);
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
        internal readonly string sourcePsdInfo;
        internal readonly bool hasAuthoritativeAssetRenameSourcePaths;
        internal readonly IReadOnlyList<string> assetRenameSourcePaths;
        internal readonly IReadOnlyList<PsdHierarchyComponentFamilyCandidate> componentFamilyCandidates;

        // Kept as raw snapshot JSON: the plan writer copies these entries through to the
        // runner plan unchanged apart from node-reference resolution.
        internal readonly JArray containmentFindings;
        internal readonly JArray flatSiblingFindings;
        private readonly Dictionary<string, string> nodePathsById;
        private readonly Dictionary<string, string> nodeIdsByPath;
        private readonly Dictionary<string, IReadOnlyList<PsdHierarchySnapshotChild>> directChildrenByPath;
        private readonly HashSet<string> assetRenameSourcePathSet;
        internal PsdHierarchyLocalRepairScope localRepairScope;

        internal bool TryGetNodePath(string nodeId, out string path)
        {
            return nodePathsById.TryGetValue(nodeId ?? string.Empty, out path);
        }

        internal bool TryGetNodeId(string path, out string nodeId)
        {
            return nodeIdsByPath.TryGetValue(path ?? string.Empty, out nodeId);
        }

        internal IReadOnlyList<string> GetNodeIdsWithinSubtrees(IEnumerable<string> sourceReferences)
        {
            var rootNodeIds = new HashSet<string>(StringComparer.Ordinal);
            var rootPaths = new List<string>();
            foreach (string sourceReference in sourceReferences ?? Array.Empty<string>())
            {
                string nodeId = sourceReference != null &&
                                sourceReference.StartsWith("node:", StringComparison.Ordinal)
                    ? sourceReference.Substring("node:".Length)
                    : sourceReference;
                if (string.IsNullOrWhiteSpace(nodeId) || !rootNodeIds.Add(nodeId))
                {
                    continue;
                }

                if (nodePathsById.TryGetValue(nodeId, out string path))
                {
                    rootPaths.Add(path);
                }
            }

            foreach (KeyValuePair<string, string> node in nodePathsById)
            {
                if (rootPaths.Any(rootPath =>
                        string.Equals(node.Value, rootPath, StringComparison.Ordinal) ||
                        node.Value.StartsWith(rootPath + "/", StringComparison.Ordinal)))
                {
                    rootNodeIds.Add(node.Key);
                }
            }

            return rootNodeIds.OrderBy(nodeId => nodeId, StringComparer.Ordinal).ToArray();
        }



        internal string BuildInstructions()
        {
            var builder = new StringBuilder();
            builder.AppendLine("You are assisting with a Unity Prefab hierarchy cleanup from inside the Unity Editor.");
            builder.AppendLine("The user supplied the exact cleanup skill and target Prefab below.");
            builder.AppendLine("Inspect first and provide a complete, reviewable plan. Do not claim to have edited a local asset: the Unity chat window performs the approved update.");
            builder.AppendLine("Do not invoke PowerShell, Python, Unity runners, or file-writing tools yourself.");
            builder.AppendLine("Your first reply must contain the human-readable review in Simplified Chinese, followed by exactly one complete UTF-8 JSON plan in a fenced ```json code block that follows the supplied plan format.");
            builder.AppendLine("Keep the five-section review concise (one or two short sentences per section); the complete JSON plan has priority and must not be truncated.");
            builder.AppendLine("The JSON root must contain \"version\": 2, \"snapshotFingerprint\" copied exactly from the supplied snapshot, and every required operation array, including empty arrays for unused operations. The window rejects incomplete JSON before it can be confirmed.");
            builder.AppendLine("If that reply fails plan validation, the Unity chat window automatically sends the validation error back in this same AI session. Treat that message as an internal correction request: return only one complete replacement JSON code block, never a patch, and never ask the user to retry or send another message. The window preserves the initial five-section review for the user.");
            builder.AppendLine("The user will inspect that reply. When the user replies with an explicit confirmation, the Unity chat window validates the JSON and directly runs the approved plan through Unity Editor APIs. Do not ask for an additional confirmation, output-mode choice, or manual script command.");
            builder.AppendLine("The only allowed output mode is in_place: output.assetPath must exactly equal the supplied target Prefab path.");
            builder.AppendLine("Every reference to an existing Prefab node must use node:<id> from the authoritative snapshot. Never write a raw hierarchy path in wrappers, moves, renames, removals, tight bounds, component-family decisions, or extraction contracts.");
            builder.AppendLine("Private-asset renames ARE executable: textureRenames[] and spriteAtlasRenames[] entries are {from, toName, expectedGuid}. toName has no extension; every Texture toName must start with \"<prefabName>_\", every SpriteAtlas toName must equal prefabName, each from must be a private asset of the current target Prefab (a Texture referenced by it, or a SpriteAtlas in the Prefab's own folder), and the rename target must not exist yet. Leave expectedGuid empty so Unity captures the current identity, or paste an exact GUID to pin it.");
            builder.AppendLine(PsdHierarchyChatClient.PlanIdentifierContract);
            builder.AppendLine("The target is already confirmed for in-place cleanup. Do not ask the user to choose an output mode or whether to create a new Prefab.");
            if (localRepairScope != null)
            {
                builder.AppendLine("This is a local repair stage. Only operate inside the following locked scope: " + localRepairScope.Describe() + ".");
                builder.AppendLine("Do not move, rename, remove, or create a wrapper outside the selected repair boundary. Private-asset renames, containment/flat-sibling resolutions and variant/stateful extraction are not executable in this stage; return empty arrays for them.");
                builder.AppendLine("For a local component extraction, emit exactly one selectedPrefabExtractions entry: {id, name, assetPath, parent: <the selected nodes' direct parent as node:<id>>, sources: [node:<id>...]} where sources are exactly the locked selection, name is PascalCase, and assetPath is a NEW Assets/**.prefab whose file name equals name. Do not add any other extraction array.");
                builder.AppendLine("When the locked selection spans several parents, emit exactly one crossParentPrefabExtractions entry instead: {id, name, assetPath, root: <the selection's lowest common ancestor node:<id>>, templateSources: [the locked selection as node:<id>...], instances: [{sequence, sources: [one complete group as node:<id>...]}...], unmatched: [node:<id>...]}. Every group must have the same member count and order as templateSources, the templateSources group must be listed as one of the instances, unmatched nodes must stay outside every group, and assetPath must be a NEW Assets/**.prefab whose file name equals name.");
                builder.AppendLine("Selected hierarchy paths: " + string.Join("; ", localRepairScope.selectedPaths));
            }
            builder.AppendLine("Do not propose, create, copy, or offer a .cleaned.prefab or any other replacement Prefab. Any later approved cleanup must target the supplied Prefab in place while preserving visual layout, generated assets, bindings, and unrelated components.");
            if (localRepairScope == null)
            {
                builder.AppendLine("Component extraction IS executable: emit one componentExtractions entry per approved component family ({id, name, assetPath, template: node:<id>, instances: [node:<id>...]}). template must also appear in instances, every instance must share the template's recursive component structure, and assetPath must be a NEW PascalCase .prefab under Assets/.");
                builder.AppendLine("State extraction IS executable: use stateComponentExtractions ({id, template: node:<id>, assetPath, defaultState, states: [{id, source: node:<id>, name}]}) only when several direct-sibling roots occupy one visual slot as mutually exclusive states. Every states[].source must be a direct sibling of template, template must be one of them, at least two states are required, and those sources must not be referenced from anywhere outside the extracted states.");
                builder.AppendLine("Variant extraction IS executable: use variantComponentExtractions for rows visible at different list positions that select one of several observed states ({id, template: node:<id>, assetPath, commonName, statesName, defaultState, states: [{id, source: node:<id>, name}], instances: [{source: node:<id>, name, state}]}). states[].source must be direct siblings of template, every state representative must also appear once in instances, and every instance source must be a direct sibling of template whose recursive structure matches its selected state source.");
                builder.AppendLine("Stateful extraction IS executable: use statefulComponentExtractions when repeated items contain real shared content plus a few visual states ({id, template: node:<id>, assetPath, common: {source: node:<id>, members: [{sourceName, name}]}, states: [{id, source: node:<id>, name, members: [...]}], defaultState, instances: [{source: node:<id>, name, state, commonSourceNames, stateSourceNames}]}). Every direct child of each instance source must be mapped exactly once by commonSourceNames plus stateSourceNames, every mapped member must be declared by the common or the selected state contract, and [States] is created before [Common].");
                builder.AppendLine("Containment/flat-sibling resolutions and local-selection extraction are still NOT executable in this initial plan: keep containmentResolutions, flatSiblingResolutions, selectedPrefabExtractions and crossParentPrefabExtractions as empty arrays and report that work in the review text.");
                builder.AppendLine("Every requiresExtraction:true snapshot candidate must have exactly one componentFamilyDecisions entry. Its parent and sources must exactly match the candidate; recommendedMode is advisory only; mode must be component|state|variant|stateful and must match the actual extraction list or postGroupingExtractionIntents entry named by extractionId. That extraction's source roots must completely cover the candidate sources.");
                builder.AppendLine("Post-grouping extraction IS executable, but only as an automatic second stage: postGroupingExtractionIntents[] carries the reviewed child-Prefab work that must run after this grouping is saved. Each entry is {id, mode: component|state|variant|stateful, assetPath, templatePath, commonMembers, states, defaultState, instances: [{path, state, commonSourceNames, stateSourceNames}]}. templatePath and every instances[].path are POST-grouping hierarchy paths (the tree after your wrappers, moves and renames), because Unity resolves them against a refreshed authoritative snapshot; never write a node:<id> there. mode=component requires empty states and an empty defaultState; every other mode declares states: [{id, name, sourcePath, members}] plus a defaultState id, and each instance selects one declared state. A mandatory candidate deferred to this stage must reference exactly one same-mode intent, and Unity must prove that the rebuilt extraction sources completely cover the refreshed candidate sources before executing it. assetPath must be a NEW Assets/**.prefab whose file name matches the reviewed component name. Leave the array empty when this grouping needs no child Prefab.");
                builder.AppendLine("The first confirmable response must use Markdown tables for grouping and naming, child Prefab extraction, preserved or ambiguous content, and verification. In the review text you may still point out flat sibling clusters and local-selection work as pending follow-up; containmentResolutions, flatSiblingResolutions, selectedPrefabExtractions and crossParentPrefabExtractions are not executable in this initial plan and must never appear there.");
            }
            builder.AppendLine("Never put a hierarchy path or an invented node id anywhere in the plan: every existing node reference must be an exact node:<id> taken from the authoritative snapshot.");
            builder.AppendLine("Return an auditable review, not private chain-of-thought. In Simplified Chinese, use Markdown tables for target, grouping and naming, child Prefab extraction, preservation, and verification. Ground every claim in observable hierarchy, geometry, component, sibling-order, or repeated-structure evidence. Ask for exactly one confirmation of the complete reviewed workflow.");
            builder.AppendLine("Source PSD: " + sourcePsdAssetPath);
            builder.AppendLine("Target Prefab: " + targetPrefabAssetPath);
            if (!string.IsNullOrWhiteSpace(sourcePsdInfo))
            {
                builder.AppendLine();
                builder.AppendLine("===== BEGIN SOURCE PSD INFO =====");
                builder.AppendLine(sourcePsdInfo);
                builder.AppendLine("===== END SOURCE PSD INFO =====");
            }
            if (hasAuthoritativeAssetRenameSourcePaths)
            {
                builder.AppendLine("For textureRenames[].from and spriteAtlasRenames[].from, use only an exact path from the authoritative list below. If a needed path is absent, omit that rename; never guess a filename or numeric suffix.");
                builder.AppendLine("===== BEGIN ALLOWED ASSET RENAME SOURCES =====");
                foreach (string assetPath in assetRenameSourcePaths)
                    builder.AppendLine(assetPath);
                builder.AppendLine("===== END ALLOWED ASSET RENAME SOURCES =====");
            }
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
        private const string ReusableItemFallbackName = "ReusableItem";

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

            IReadOnlyList<string> assetRenameSourcePaths =
                CollectAssetRenameSourcePaths(prefabAssetPath);

            string sourcePsdInfo = BuildSourcePsdInfo(projectRoot, sourcePsdAssetPath);

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
                hierarchySnapshotFullPath,
                assetRenameSourcePaths,
                sourcePsdInfo);
            error = string.Empty;
            return true;
        }

        private static IReadOnlyList<string> CollectAssetRenameSourcePaths(string prefabAssetPath)
        {
            return AssetDatabase.GetDependencies(prefabAssetPath, true)
                .Select(NormalizeAssetPath)
                .Where(IsAssetRenameSourcePath)
                .Where(path => AssetDatabase.LoadMainAssetAtPath(path) != null)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool IsAssetRenameSourcePath(string assetPath)
        {
            switch (Path.GetExtension(assetPath ?? string.Empty).ToLowerInvariant())
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".tga":
                case ".psd":
                case ".spriteatlas":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 只为某个 Prefab 资源路径生成权威节点快照（重放时用于证明节点对应关系）。
        /// </summary>
        internal static bool TryBuildSnapshotForPrefab(
            string prefabAssetPath,
            out string snapshotJson,
            out string fingerprint,
            out string error)
        {
            snapshotJson = string.Empty;
            fingerprint = string.Empty;
            error = string.Empty;
            DirectoryInfo projectDirectory = Directory.GetParent(Application.dataPath);
            if (projectDirectory == null)
            {
                error = "无法解析 Unity 项目根目录。";
                return false;
            }

            string projectRoot = projectDirectory.FullName;
            string normalized = NormalizeAssetPath(prefabAssetPath);
            return TryBuildHierarchySnapshot(
                normalized,
                ToFullPath(projectRoot, normalized),
                projectRoot,
                out snapshotJson,
                out fingerprint,
                out _,
                out error);
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
            var nodesById = nodes
                .OfType<JObject>()
                .Where(node => !string.IsNullOrWhiteSpace(node.Value<string>("id")))
                .ToDictionary(node => node.Value<string>("id"), StringComparer.Ordinal);
            IEnumerable<IGrouping<string, JObject>> siblingGroups = nodes
                .OfType<JObject>()
                .Where(node =>
                    !string.IsNullOrWhiteSpace(node.Value<string>("id")) &&
                    !string.IsNullOrWhiteSpace(node.Value<string>("parentId")) &&
                    node.Value<int?>("childCount") == 0 && !IsInsideNestedPrefab(node, nodesById))
                .GroupBy(node => node.Value<string>("parentId"), StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal);

            foreach (IGrouping<string, JObject> group in siblingGroups)
            {
                if (nodesById.TryGetValue(group.Key, out JObject parent) &&
                    IsExplicitStructuralGroup(parent))
                {
                    continue;
                }

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

        // A bracketed container is already an explicit semantic boundary.
        // Re-grouping its direct leaves would create an unbounded wrapper chain.
        private static bool IsExplicitStructuralGroup(JObject node)
        {
            string name = node?.Value<string>("name")?.Trim();
            return !string.IsNullOrEmpty(name) && name.Length > 2 &&
                   name[0] == '[' && name[name.Length - 1] == ']';
        }

        private static bool IsInsideNestedPrefab(JObject node, IReadOnlyDictionary<string, JObject> nodesById)
        {
            JObject current = node;
            while (current != null)
            {
                if (!string.IsNullOrEmpty(current.Value<string>("nestedPrefabAssetPath"))) return true;
                string parentId = current.Value<string>("parentId");
                if (string.IsNullOrEmpty(parentId) || !nodesById.TryGetValue(parentId, out current)) break;
            }
            return false;
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
            if (string.IsNullOrWhiteSpace(stem))
            {
                return ReusableItemFallbackName;
            }

            string suggestedAssetName = char.ToUpperInvariant(stem[0]) + stem.Substring(1);
            return IsAsciiIdentifier(suggestedAssetName)
                ? suggestedAssetName
                : ReusableItemFallbackName;
        }

        private static bool IsAsciiIdentifier(string value)
        {
            return !string.IsNullOrEmpty(value) &&
                   ((value[0] >= 'A' && value[0] <= 'Z') ||
                    (value[0] >= 'a' && value[0] <= 'z')) &&
                   value.Skip(1).All(character =>
                       (character >= 'A' && character <= 'Z') ||
                       (character >= 'a' && character <= 'z') ||
                       (character >= '0' && character <= '9'));
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

        /// <summary>
        /// 构建源 PSD 文件的基本信息描述。
        /// </summary>
        private static string BuildSourcePsdInfo(string projectRoot, string sourcePsdAssetPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePsdAssetPath))
            {
                return string.Empty;
            }

            string psdFullPath = ToFullPath(projectRoot, NormalizeAssetPath(sourcePsdAssetPath));
            if (!File.Exists(psdFullPath))
            {
                return string.Empty;
            }

            var info = new FileInfo(psdFullPath);
            // 与便携提示词同一条规则：交给 AI 的路径一律是绝对路径，
            // 否则在 Unity 之外的工具里无法定位这个 PSD。
            return "PSD path: " + PsdHierarchyChatClient.ToPortableFullPath(psdFullPath) + "\n" +
                   "PSD bytes: " + info.Length + "\n" +
                   "PSD layer parsing is deferred; the Unity hierarchy snapshot is authoritative.";
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
            bool writePromptToStandardInput,
            string promptFilePath = null)
        {
            this.executablePath = executablePath ?? string.Empty;
            this.arguments = arguments ?? string.Empty;
            this.workingDirectory = workingDirectory ?? string.Empty;
            this.writePromptToStandardInput = writePromptToStandardInput;
            this.promptFilePath = promptFilePath ?? string.Empty;
        }

        internal readonly string executablePath;
        internal readonly string arguments;
        internal readonly string workingDirectory;
        internal readonly bool writePromptToStandardInput;

        /// <summary>
        /// 非空时改为把提示词写进该文件，再由 CLI 用 --prompt-file 读取。
        /// 给不接受 stdin、也不便用命令行传长文本的 CLI 使用（当前是 Grok）。
        /// </summary>
        internal readonly string promptFilePath;
    }

    internal static class PsdHierarchyChatClient
    {
        private const int AiRequestTimeoutSeconds = 120;
        internal const string OpenAiEndpoint = "https://api.openai.com/v1/responses";
        internal const string AnthropicEndpoint = "https://api.anthropic.com/v1/messages";
        private const int MaxClaudePromptCharacters = 6000;
        private const string RequiredPlanRootFields =
            "\"version\", \"snapshotFingerprint\", \"prefabAssetPath\", \"output\", \"prefabName\", \"wrappers\", \"moves\", \"renames\", " +
            "\"emptyContainerRemovals\", \"tightBounds\", \"textureRenames\", \"spriteAtlasRenames\", \"containmentResolutions\", \"flatSiblingResolutions\", " +
            "\"componentFamilyDecisions\", \"componentExtractions\", \"stateComponentExtractions\", " +
            "\"variantComponentExtractions\", \"statefulComponentExtractions\", \"postGroupingExtractionIntents\", \"verify\"";
        internal const string PlanIdentifierContract =
            "Every wrappers[].id must use lower snake_case matching [a-z][a-z0-9_]*; examples: screen, screen_root, day_markers. Do not use uppercase, hyphens, spaces, brackets, or @ in an id. The @ prefix is only for a later reference such as @screen_root. Apply the same lower snake_case rule to all extraction IDs and state IDs.";
        internal const string DefaultUserPrompt =
            "请按整理技能完整审查当前目标 Prefab，并输出完整、可确认的层级整理方案，而不是只查看顶层或按名称猜测。\n" +
            "1. 结合 PSD 与 Prefab 的完整层级、节点几何、组件、同级顺序和重复结构，说明当前结构的主要问题。\n" +
            "2. 给出完整的原地整理后树形结构：每个新增语义容器、节点重命名、节点归属和保留顺序都要明确。\n" +
            "3. 对重复视觉单元按整体分组，不要把背景、文本、图标、锁等平铺到按类型命名的大容器中。\n" +
            "4. 标出无法安全推断、存在序列化引用风险或嵌套 Prefab 边界的节点，并说明保持不动的原因。\n" +
            "5. 列出应用前必须验证的布局、组件、引用、激活状态和资源命名不变量。\n" +
            "第一次可确认回复必须使用 Markdown 表格，不要只写段落，也不要输出原始内部推理：\n" +
            "1. 目标表：目标 Prefab、原地输出路径、快照 fingerprint。\n" +
            "2. 分组与命名表：Wrapper/名称、父节点 node:<id>、有序成员、Sibling 顺序、观察证据、推断或未知、风险。\n" +
            "3. 子 Prefab 抽取表：ID、输出路径、模式、有序实例、模板节点、证据、风险。\n" +
            "4. 保留项表：保持不动的节点、嵌套 Prefab/绑定风险和原因。\n" +
            "5. 验证表：布局、组件、引用、激活状态、Sibling 顺序、嵌套边界和生成资源。\n" +
            "表格后附上一个完整的 ```json 计划代码块，严格遵循随附计划格式，并询问用户是否满意并执行。用户只确认一次；确认后由 Unity 原地完成已审阅的层级整理并核验，不得再次确认。\n" +
            "CRITICAL: 当前 Unity 执行器执行 wrappers、moves、renames、tightBounds、emptyContainerRemovals、componentExtractions（componentFamilyDecisions 必须用 mode=component）、stateComponentExtractions、variantComponentExtractions、statefulComponentExtractions 以及 textureRenames / spriteAtlasRenames（toName 不带扩展名；每个 Texture 的 toName 必须以 \"<prefabName>_\" 开头，每个 SpriteAtlas 的 toName 必须等于 prefabName；from 必须是当前目标 Prefab 的私有资源；目标不能已存在；expectedGuid 留空由 Unity 捕获当前身份）。containmentResolutions、flatSiblingResolutions、selectedPrefabExtractions、crossParentPrefabExtractions 必须保持空数组；非空会在任何写入前被拒绝，请在评审文字里说明这些待迁移工作。postGroupingExtractionIntents 可以非空：它记录已审阅的分组后子 Prefab 抽取，Unity 在首阶段保存并重新核验后自动刷新权威快照并执行；templatePath 与每个 instances[].path 必须写分组后的层级路径（不是 node:<id>），mode=component 时 states 与 defaultState 必须为空，其他模式必须声明 states（id/name/sourcePath/members）与 defaultState，每个实例必须提供 state、commonSourceNames、stateSourceNames，且刷新后每个 requiresExtraction 候选都必须被恰好一个 mode=component 意图覆盖。\n" +
            "本次主界面只原地更新当前目标 Prefab，不创建、复制或另存新的屏幕 Prefab；子 Prefab 资产只按 postGroupingExtractionIntents 中已审阅的清单在第二阶段创建。\n" +
            "不要声称已经修改本地文件。用户确认完整表格和计划后，Unity 窗口只会执行已审阅的原地整理、公共组件抽取、状态抽取、变体抽取、有状态抽取、私有资源改名以及分组后子 Prefab 抽取并核验。";

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
            builder.AppendLine("CRITICAL: Every emptyContainerRemovals entry must reference a container that will be COMPLETELY EMPTY after all moves execute. Before adding a container to emptyContainerRemovals, verify that EVERY child node under that container has a corresponding move operation that relocates it elsewhere. If any child remains unmoved, the container is not empty and must NOT be in emptyContainerRemovals. When the error says 'Container is not empty after planned moves', it means you listed a container for removal that still has children—either move ALL its children first, or remove that container from emptyContainerRemovals.");
            builder.AppendLine("CRITICAL: Keep containmentResolutions, flatSiblingResolutions, selectedPrefabExtractions and crossParentPrefabExtractions as EMPTY arrays. The current Unity executor runs wrappers, moves, renames, tightBounds, emptyContainerRemovals, componentExtractions, stateComponentExtractions, variantComponentExtractions, statefulComponentExtractions, textureRenames, spriteAtlasRenames and postGroupingExtractionIntents, and it refuses any other non-empty array before a write; repeating an unsupported operation in the replacement plan cannot succeed. Report the blocked work in the review text instead. Keep every reviewed postGroupingExtractionIntents entry byte-identical: its post-grouping paths and states are resolved against a refreshed snapshot after the hierarchy stage is saved, so do not rewrite them into node:<id> references.");
            builder.AppendLine("Every verify.directChildren entry must use a non-empty, unique list of direct-child names in post-apply sibling order. List each child name exactly once; never duplicate a name as a placeholder or count.");
            return builder.ToString();
        }

        internal static string BuildJsonOnlyPlanRecoveryPrompt(
            string failureDetail,
            PsdHierarchyChatContext context)
        {
            string detail = string.IsNullOrWhiteSpace(failureDetail)
                ? "The previously confirmed cleanup plan could not be applied."
                : failureDetail.Trim();
            var builder = new StringBuilder();
            builder.AppendLine("The confirmed cleanup plan failed before the Prefab was saved. Re-analyze the current authoritative snapshot and return one complete replacement JSON plan.");
            builder.AppendLine("Failure detail: " + detail);
            builder.AppendLine("Do not reuse a failed hierarchy assumption. Every existing-node reference and direct-child contract must be rebuilt from the current authoritative snapshot.");
            builder.AppendLine("For every textureRenames[].from or spriteAtlasRenames[].from, use only an exact path from the current allowed asset source list. If the failed path is absent, remove or replace that operation; never guess an incremented filename.");
            if (context?.hasAuthoritativeAssetRenameSourcePaths == true)
            {
                builder.AppendLine("===== BEGIN CURRENT ALLOWED ASSET RENAME SOURCES =====");
                foreach (string assetPath in context.assetRenameSourcePaths)
                    builder.AppendLine(assetPath);
                builder.AppendLine("===== END CURRENT ALLOWED ASSET RENAME SOURCES =====");
            }
            builder.AppendLine("Return exactly one complete UTF-8 JSON plan in one fenced ```json code block. Do not output prose, headings, explanations, diffs, or Markdown outside that code block. This must be a full replacement plan, not a patch. Do not ask the user to retry or confirm.");
            return builder.ToString();
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
            builder.AppendLine("Return a concise, reviewable hierarchy-cleanup plan in Simplified Chinese using Markdown tables for target, grouping and naming, child Prefab extraction, preservation, and verification. Do not return prose-only sections.");
            builder.AppendLine("After those tables, return exactly one complete UTF-8 JSON plan in a fenced ```json code block. The JSON is an executable contract, not illustrative pseudo-JSON.");
            builder.AppendLine("Keep the tables compact but complete; include parent and member node IDs, sibling order, evidence, risks, every child-Prefab path/mode/instance/Common/State mapping, and verification invariants. The complete JSON plan has priority and must not be truncated.");
            builder.AppendLine("Use exactly these required root fields: " + RequiredPlanRootFields + ". Use [] for every unused operation array.");
            builder.AppendLine("Do not use legacy field names such as wrapperCreations, nodeTransfers, nodeRenames, or privateAssetRenames. The main Prefab output must be in_place at the exact target path.");
            builder.AppendLine("Use version 2 and copy snapshotFingerprint exactly from the authoritative snapshot.");
            builder.AppendLine("A reference beginning with @ must be exactly @wrapperId; never write @wrapperId/Child. Every reference to an existing node must use node:<id> from the authoritative snapshot. Never emit a raw hierarchy path or invent a node ID.");
            builder.AppendLine(PlanIdentifierContract);
            builder.AppendLine("EXECUTABLE OPERATIONS: wrappers, moves, renames, tightBounds, emptyContainerRemovals, componentExtractions (with componentFamilyDecisions mode=component), stateComponentExtractions, variantComponentExtractions, statefulComponentExtractions, textureRenames, spriteAtlasRenames and postGroupingExtractionIntents (executed automatically as a second stage after the grouping is saved and the snapshot is refreshed) are executable. Everything else must stay empty: containmentResolutions, flatSiblingResolutions, selectedPrefabExtractions, crossParentPrefabExtractions. Unity refuses a non-empty unsupported array before any write.");
            builder.AppendLine("Keep prefabName present for schema stability; this version never derives it, and it must not be used to hide conflicting toName values. Every postGroupingExtractionIntents entry uses post-grouping hierarchy paths in templatePath and instances[].path, never node:<id>.");
            builder.AppendLine("The executable plan-format file is authoritative for field names and object shapes; where it still describes an operation as unsupported, this instruction wins.");
            builder.AppendLine("Ask for exactly one confirmation of the complete tables and plan. After that confirmation the Unity window applies the reviewed hierarchy cleanup in place, re-verifies it, refreshes the authoritative snapshot, executes the reviewed post-grouping extraction, and verifies again; never ask for a second confirmation.");
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
            switch (provider)
            {
                case PsdHierarchyAiProvider.Claude:
                    return AnthropicEndpoint;
                case PsdHierarchyAiProvider.Codex:
                    return OpenAiEndpoint;
                default:
                    // Grok 与 Pi 没有可以写死的官方直连地址：Pi 由 --provider 决定后端，
                    // Grok 的直连地址随账号与区域不同。留空即要求用户显式填写，避免猜错地址。
                    return string.Empty;
            }
        }

        internal static string DefaultModel(PsdHierarchyAiProvider provider)
        {
            switch (provider)
            {
                case PsdHierarchyAiProvider.Claude:
                    return "claude-sonnet-5";
                case PsdHierarchyAiProvider.Codex:
                    return "gpt-5";
                default:
                    return string.Empty;
            }
        }

        internal static string GetProviderDisplayName(PsdHierarchyAiProvider provider)
        {
            switch (provider)
            {
                case PsdHierarchyAiProvider.Claude:
                    return "Claude";
                case PsdHierarchyAiProvider.Codex:
                    return "Codex";
                case PsdHierarchyAiProvider.Grok:
                    return "Grok";
                case PsdHierarchyAiProvider.Pi:
                    return "Pi";
                default:
                    return "未选择";
            }
        }

        /// <summary>是否是需要模型名称才能正确调用的 provider（用于界面提示，不参与校验）。</summary>
        internal static bool HasBuiltInApiDefaults(PsdHierarchyAiProvider provider)
        {
            return provider == PsdHierarchyAiProvider.Claude || provider == PsdHierarchyAiProvider.Codex;
        }

        internal static string GetModelDisplayName(PsdHierarchyChatConnection connection)
        {
            string model = (connection.model ?? string.Empty).Trim();
            string effort = (connection.reasoningEffort ?? string.Empty).Trim();
            if (connection.connectionMode == PsdHierarchyAiConnectionMode.CustomApi)
            {
                return model;
            }

            // 本地 CLI：模型与思考程度都留空时才是真正的「CLI 默认」。
            if (string.IsNullOrEmpty(model) && string.IsNullOrEmpty(effort))
            {
                return "CLI 默认";
            }

            if (string.IsNullOrEmpty(model))
            {
                return "CLI 默认 · 思考 " + effort;
            }

            return string.IsNullOrEmpty(effort) ? model : model + " · 思考 " + effort;
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

            bool hasPrompt = !string.IsNullOrWhiteSpace(prompt);
            string modelArguments = BuildModelArguments(connection);
            string effortArguments = BuildReasoningEffortArguments(connection);

            if (connection.provider == PsdHierarchyAiProvider.Grok)
            {
                return CreateGrokCliInvocation(
                    connection,
                    workingDirectory,
                    hasPrompt,
                    cliSessionId,
                    resumeCliSession,
                    modelArguments,
                    effortArguments);
            }

            if (connection.provider == PsdHierarchyAiProvider.Pi)
            {
                return CreatePiCliInvocation(
                    connection,
                    workingDirectory,
                    cliSessionId,
                    resumeCliSession,
                    modelArguments,
                    effortArguments);
            }

            if (connection.provider == PsdHierarchyAiProvider.Claude)
            {
                string claudeSessionArguments = hasSessionId
                    ? (resumeCliSession ? " --resume " : " --session-id ") + QuoteProcessArgument(cliSessionId)
                    : string.Empty;
                if (hasPrompt)
                {
                    string directClaudeExecutable = ResolveClaudeDirectExecutable(connection.cliExecutablePath);
                    if (!string.IsNullOrEmpty(directClaudeExecutable))
                    {
                        return new PsdHierarchyCliInvocation(
                            directClaudeExecutable,
                            "--print --output-format json --permission-mode dontAsk --safe-mode " +
                            "--tools Read --add-dir " + QuoteProcessArgument(workingDirectory) +
                            claudeSessionArguments + modelArguments + effortArguments,
                            workingDirectory,
                            true);
                    }
                }

                return WrapForCommandShim(
                    connection.cliExecutablePath,
                    "--print --output-format json --permission-mode plan --safe-mode" +
                    claudeSessionArguments + modelArguments + effortArguments,
                    workingDirectory,
                    true);
            }

            // Codex：会话 ID 由 CLI 自己生成，只能在 resume 时才带上。
            string codexArguments = resumeCliSession
                ? "exec resume --json " + QuoteProcessArgument(cliSessionId) + " -"
                : "exec --json --sandbox read-only -";
            return WrapForCommandShim(
                connection.cliExecutablePath,
                codexArguments + modelArguments + effortArguments,
                workingDirectory,
                true);
        }

        /// <summary>
        /// Grok 不接受 stdin（不给提示词会进入交互 TUI 并挂住），也没有适合传长文本的命令行参数，
        /// 因此把提示词落到文件，再用 --prompt-file 交给它。
        /// 返回的调用只包含路径，真正的写文件由执行方完成。
        /// </summary>
        private static PsdHierarchyCliInvocation CreateGrokCliInvocation(
            PsdHierarchyChatConnection connection,
            string workingDirectory,
            bool hasPrompt,
            string cliSessionId,
            bool resumeCliSession,
            string modelArguments,
            string effortArguments)
        {
            bool hasSessionId = !string.IsNullOrWhiteSpace(cliSessionId);
            string sessionArguments = hasSessionId
                ? (resumeCliSession ? " --resume " : " --session-id ") + QuoteProcessArgument(cliSessionId)
                : string.Empty;
            string promptPath = string.Empty;
            string promptArguments = string.Empty;
            if (hasPrompt)
            {
                promptPath = Path.Combine(
                    workingDirectory ?? string.Empty,
                    "Library",
                    "PsdHierarchyCliPrompts",
                    Guid.NewGuid().ToString("N") + ".md");
                promptArguments = " --prompt-file " + QuoteProcessArgument(promptPath);
            }

            PsdHierarchyCliInvocation invocation = WrapForCommandShim(
                connection.cliExecutablePath,
                "--output-format json --permission-mode plan" +
                sessionArguments + modelArguments + effortArguments + promptArguments,
                workingDirectory,
                false);
            return new PsdHierarchyCliInvocation(
                invocation.executablePath,
                invocation.arguments,
                invocation.workingDirectory,
                false,
                promptPath);
        }

        /// <summary>
        /// Pi 支持从 stdin 读取提示词（实测 --print --mode json 配合管道输入可正常返回），
        /// 所以长提示词不会撞到 Windows 命令行长度上限。
        /// </summary>
        private static PsdHierarchyCliInvocation CreatePiCliInvocation(
            PsdHierarchyChatConnection connection,
            string workingDirectory,
            string cliSessionId,
            bool resumeCliSession,
            string modelArguments,
            string effortArguments)
        {
            bool hasSessionId = !string.IsNullOrWhiteSpace(cliSessionId);
            // pi 的 --resume 是交互式选择器，定位具体会话要用 --session。
            string sessionArguments = hasSessionId
                ? " --session " + QuoteProcessArgument(cliSessionId)
                : string.Empty;
            return WrapForCommandShim(
                connection.cliExecutablePath,
                "--print --mode json" + sessionArguments + modelArguments + effortArguments,
                workingDirectory,
                true);
        }

        private static string BuildModelArguments(PsdHierarchyChatConnection connection)
        {
            return BuildModelArguments(connection, QuoteProcessArgument);
        }

        private static string BuildReasoningEffortArguments(PsdHierarchyChatConnection connection)
        {
            return BuildReasoningEffortArguments(connection, QuoteProcessArgument);
        }

        /// <summary>
        /// 按 provider 拼出「模型 + 思考程度」参数，供 PowerShell 终端入口复用。
        /// 两处的引号规则不同（CreateProcess 参数 vs PowerShell 字面量），所以引号由调用方给。
        /// </summary>
        internal static string BuildModelAndEffortArguments(
            PsdHierarchyChatConnection connection,
            Func<string, string> quote)
        {
            if (quote == null) throw new ArgumentNullException(nameof(quote));
            return BuildModelArguments(connection, quote) + BuildReasoningEffortArguments(connection, quote);
        }

        private static string BuildModelArguments(
            PsdHierarchyChatConnection connection,
            Func<string, string> quote)
        {
            string model = (connection.model ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(model))
            {
                return string.Empty;
            }

            // Codex 用短选项，其余三个都用 --model。
            string option = connection.provider == PsdHierarchyAiProvider.Codex ? "-m " : "--model ";
            return " " + option + quote(model);
        }

        /// <summary>思考程度留空时不生成任何参数，完全使用 CLI 自身的配置。</summary>
        private static string BuildReasoningEffortArguments(
            PsdHierarchyChatConnection connection,
            Func<string, string> quote)
        {
            string effort = (connection.reasoningEffort ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(effort))
            {
                return string.Empty;
            }

            switch (connection.provider)
            {
                case PsdHierarchyAiProvider.Claude:
                    return " --effort " + quote(effort);
                case PsdHierarchyAiProvider.Grok:
                    return " --reasoning-effort " + quote(effort);
                case PsdHierarchyAiProvider.Pi:
                    return " --thinking " + quote(effort);
                case PsdHierarchyAiProvider.Codex:
                    // codex 的 --config 值先按 TOML 解析，解析失败就按字面量使用，
                    // 所以这里给裸值即可，不必在 cmd 包装层里再嵌一层引号。
                    // 设置层已保证思考程度不含空格与引号。
                    return " -c model_reasoning_effort=" + effort;
                default:
                    return string.Empty;
            }
        }

        private static PsdHierarchyCliInvocation WrapForCommandShim(
            string cliPath,
            string arguments,
            string workingDirectory,
            bool writePromptToStandardInput)
        {
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
                    writePromptToStandardInput);
            }

            return new PsdHierarchyCliInvocation(cliPath, arguments, workingDirectory, writePromptToStandardInput);
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

            string arguments;
            switch (connection.provider)
            {
                case PsdHierarchyAiProvider.Claude:
                    arguments = "--resume " + QuoteProcessArgument(cliSessionId) +
                        " --permission-mode plan --safe-mode --add-dir " + QuoteProcessArgument(workingDirectory);
                    break;
                case PsdHierarchyAiProvider.Grok:
                    arguments = "--resume " + QuoteProcessArgument(cliSessionId) +
                        " --permission-mode plan";
                    break;
                case PsdHierarchyAiProvider.Pi:
                    arguments = "--session " + QuoteProcessArgument(cliSessionId);
                    break;
                default:
                    arguments = "-s read-only resume " + QuoteProcessArgument(cliSessionId);
                    break;
            }

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

        internal static string BuildPortablePrompt(PsdHierarchyChatContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            // 这份提示词也会被复制到 Unity 之外的 CLI / 桌面 AI 里执行，那些工具的工作目录
            // 不受控，所以统一输出「绝对路径 + 正斜杠」，不留相对路径或混合分隔符。
            string targetPrefabFullPath = ToPortableFullPath(context.projectRoot.TrimEnd('/', '\\') + "/" +
                                                             context.targetPrefabAssetPath.TrimStart('/', '\\'));
            string normalizedSkillPath = ToPortableFullPath(context.skillFullPath);
            int skillSeparatorIndex = normalizedSkillPath.LastIndexOf('/');
            string planFormatFullPath = skillSeparatorIndex >= 0
                ? normalizedSkillPath.Substring(0, skillSeparatorIndex + 1) + "references/plan-format.md"
                : "references/plan-format.md";
            var builder = new StringBuilder();
            builder.AppendLine("Use skill prefab-hierarchy-cleanup. Read these local files:");
            builder.AppendLine("全部使用中文输出。");
            builder.AppendLine("Skill: " + normalizedSkillPath);
            builder.AppendLine("Plan format: " + planFormatFullPath);
            builder.AppendLine("Prefab: " + targetPrefabFullPath);
            builder.AppendLine("Hierarchy snapshot: " + ToPortableFullPath(context.hierarchySnapshotFullPath));
            builder.AppendLine("Accuracy rules:");
            builder.AppendLine("- Inspect the complete hierarchy, geometry, component types, active states, sibling order, nested Prefab boundaries, and repeated structures; names alone are insufficient.");
            builder.AppendLine("- Treat the hierarchy snapshot as authoritative. Read only targeted Prefab sections when the snapshot lacks evidence or conflicts with serialized data; do not repeatedly read the complete Prefab and snapshot.");
            builder.AppendLine("- Distinguish observed facts, inferences, and unknowns. Cross-check Prefab and snapshot; cite exact node:<id> references for important conclusions.");
            builder.AppendLine("- If evidence conflicts or intent is ambiguous before the review, ask focused questions. Never guess.");
            builder.AppendLine("- Preserve layout, components, bindings, generated assets, and unrelated content. Review all componentFamilyCandidates and flatSiblingFindings.");
            builder.AppendLine("Single-confirmation workflow:");
            builder.AppendLine("- Require exactly one user confirmation for the complete reviewed workflow. Revisions before that confirmation do not count as extra confirmations.");
            builder.AppendLine("- In the first confirmable response, use Markdown tables, not prose-only sections.");
            builder.AppendLine("- Grouping and naming table columns: wrapper name, parent node, ordered members, sibling order, observed evidence, inference or unknown, and risk.");
            builder.AppendLine("- Child Prefab extraction table columns: component ID, output asset path, mode, ordered instances, ordered Common members, every state ID/name/member list, default or per-instance state, evidence, and risk.");
            builder.AppendLine("- Also include preservation and verification tables covering unchanged nodes, nested Prefab or binding risks, layout, components, references, active states, sibling order, and generated assets.");
            builder.AppendLine("- Cite exact node:<id> references in the tables and disclose every child Prefab intended after grouping. Encode post-grouping work in postGroupingExtractionIntents so the later extraction cannot expand beyond the reviewed IDs, paths, modes, instances, or states.");
            builder.AppendLine("- Ask whether the user is satisfied with this complete grouping, naming, and child-Prefab scheme. Do not modify files before the answer.");
            builder.AppendLine("- After confirmation, execute the complete workflow automatically: create and validate the version 2 in_place plan, apply the reviewed hierarchy stage, refresh the authoritative hierarchy snapshot, generate and validate the exact manifest-matching component stage, create the reviewed child Prefabs, save the target Prefab, and run final verification.");
            builder.AppendLine("- Do not ask for another confirmation after execution starts. Automatic JSON repair, resnapshot, component extraction, save, and verification are covered by the first confirmation.");
            builder.AppendLine("- If refreshed evidence conflicts with the reviewed manifest, stop without guessing or broadening scope and report the exact failure and current saved state.");
            builder.AppendLine("- Time budget: at most one JSON repair before confirmation, one Apply per stage, and one read-only verification after an indeterminate Apply. The automatic second stage gets no repair retry. Never reinstall tools, rerun a failed plan, restore, or reimport automatically.");
            builder.AppendLine("- Finish with a final verification report; do not claim that local assets changed unless the apply and verification commands prove it.");
            builder.AppendLine("If these paths are inaccessible, ask the user to upload the Prefab and snapshot; never guess their contents.");
            return builder.ToString();
        }

        /// <summary>
        /// Normalizes every path that leaves Unity into one absolute, forward-slash form.
        /// A tool started outside the Unity project cannot resolve a path against the project
        /// directory, and mixed separators or backslashes survive command-line quoting badly.
        /// </summary>
        internal static string ToPortableFullPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        /// <summary>
        /// Builds the prompt for an external CLI or desktop AI the user starts by hand. It keeps
        /// the portable prompt unchanged, then pins the project root, the source PSD and the two
        /// output files as absolute paths, so the external session can run from any working
        /// directory and still hand Unity a reviewed, applicable plan.
        /// </summary>
        internal static string BuildExternalSessionPrompt(
            PsdHierarchyChatContext context,
            string planFullPath,
            string reviewFullPath,
            string applyFullPath)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (string.IsNullOrWhiteSpace(planFullPath))
            {
                throw new ArgumentException("外部会话的计划输出路径不能为空。", nameof(planFullPath));
            }

            if (string.IsNullOrWhiteSpace(reviewFullPath))
            {
                throw new ArgumentException("外部会话的复核输出路径不能为空。", nameof(reviewFullPath));
            }

            if (string.IsNullOrWhiteSpace(applyFullPath))
            {
                throw new ArgumentException("外部会话的 Apply 哨兵路径不能为空。", nameof(applyFullPath));
            }

            string applyResultFullPath = PsdHierarchyTerminalApplyWatcher.BuildResultPath(applyFullPath);
            string projectRoot = ToPortableFullPath(context.projectRoot.TrimEnd('/', '\\'));
            string sourcePsdFullPath = string.IsNullOrWhiteSpace(context.sourcePsdAssetPath)
                ? string.Empty
                : ToPortableFullPath(projectRoot + "/" + context.sourcePsdAssetPath.TrimStart('/', '\\'));

            var builder = new StringBuilder(BuildPortablePrompt(context));
            builder.AppendLine();
            builder.AppendLine("===== EXTERNAL SESSION CONTRACT =====");
            builder.AppendLine("Unity project root: " + projectRoot);
            if (sourcePsdFullPath.Length > 0)
            {
                builder.AppendLine("Source PSD: " + sourcePsdFullPath);
            }

            builder.AppendLine("Every path above and below is an absolute path on this machine. Use each value verbatim: never resolve it against your working directory, never rewrite, shorten or re-normalize it, and never substitute a guessed path.");
            builder.AppendLine("If your tool has no skill mechanism, read the SKILL.md file listed above and follow it directly.");
            builder.AppendLine("If a listed file cannot be read, stop and report that exact absolute path instead of guessing its contents.");
            builder.AppendLine("This is an analysis and plan session started outside Unity. Do not modify Unity assets and do not claim that any asset was changed.");
            builder.AppendLine("Write the complete executable version 2 JSON plan (and no partial patch) to: " + ToPortableFullPath(planFullPath));
            builder.AppendLine("Do not write a version 1 path plan. Do not call any Python renderer or CLI runner: their write modes are retired and Unity applies the reviewed plan itself after the .apply sentinel.");
            builder.AppendLine("EXECUTABLE OPERATIONS: wrappers, moves, renames, tightBounds, emptyContainerRemovals, componentExtractions, stateComponentExtractions, variantComponentExtractions, statefulComponentExtractions, textureRenames, spriteAtlasRenames and postGroupingExtractionIntents are executable; every other operation array must stay empty because Unity refuses a non-empty unsupported array before any write.");
            builder.AppendLine("Every requiresExtraction:true snapshot candidate must have exactly one componentFamilyDecisions entry. Its parent and sources must exactly match the candidate; recommendedMode is advisory only; mode must be component|state|variant|stateful and must match the actual extraction list or postGroupingExtractionIntents entry named by extractionId. That extraction's source roots must completely cover the candidate sources. A deferred candidate is checked again against the refreshed snapshot before second-stage execution.");
            builder.AppendLine("containmentResolutions, flatSiblingResolutions, selectedPrefabExtractions and crossParentPrefabExtractions MUST stay empty arrays: Unity refuses a non-empty one before any write.");
            builder.AppendLine("postGroupingExtractionIntents records the reviewed child-Prefab work that Unity executes automatically after it saves the grouping and refreshes the authoritative snapshot. Its templatePath and every instances[].path are POST-grouping hierarchy paths (not node:<id>); mode=component uses empty states and an empty defaultState, every other mode declares states (id/name/sourcePath/members) and a defaultState id, and each instance carries state, commonSourceNames and stateSourceNames. Every requiresExtraction:true candidate of the refreshed snapshot must be covered by exactly one same-mode intent whose rebuilt extraction sources completely cover the candidate sources.");
            builder.AppendLine("Report any containment or flat-sibling follow-up in the Chinese review text only; never encode it in the executable JSON.");
            builder.AppendLine("Every wrappers[].parent, moves[].source, moves[].destination, renames[].target, tightBounds[].target and emptyContainerRemovals[].source must copy an exact node:<id> from the snapshot, or reference an earlier wrapper as @wrapperId. Never invent an id and never write a hierarchy path.");
            builder.AppendLine("Write the human-readable Chinese review to: " + ToPortableFullPath(reviewFullPath));
            builder.AppendLine("After every revision, replace both files atomically or rewrite them completely.");
            builder.AppendLine("Only Unity applying an APPROVED plan may modify the target Prefab. Unity renames .apply to .applying while it works; never write .apply twice for one request.");
            builder.AppendLine("After the human reviewer explicitly approves in this conversation, write an empty file at: " + ToPortableFullPath(applyFullPath));
            builder.AppendLine("That .apply file is the only signal Unity needs to validate and apply the plan automatically. Do not write it before human approval.");
            builder.AppendLine("After writing .apply, poll this result file (about every 2s, up to ~3 minutes): " + ToPortableFullPath(applyResultFullPath));
            builder.AppendLine("The result JSON has success, status, stage and message; status is one of applied, rejected, partial, uncertain.");
            builder.AppendLine("- applied: Unity saved the Prefab and verified it. Report that to the human.");
            builder.AppendLine("- rejected: the plan was refused BEFORE any write, so nothing changed. Do NOT rewrite the approved plan or write another .apply for this request. Quote the FULL message and stop. Any corrected plan is a new request that requires a complete new review and explicit human approval before its own .apply is written.");
            builder.AppendLine("- partial or uncertain: Unity may already have written to the Prefab. Do NOT write another .apply and do NOT claim success. Quote the full message, tell the human the on-disk Prefab must be verified, and ask for a new review before any further apply.");
            builder.AppendLine("Do not claim Unity assets changed until the result file has success=true and status applied.");
            return builder.ToString();
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
                    webRequest.timeout = AiRequestTimeoutSeconds;
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
                // Claude 与 Grok 都支持用 --session-id 预先指定新会话 ID。
                // Pi 的会话 ID 由它自己生成（首轮不给 --session，从 session 事件里读回来）。
                bool supportsPresetSessionId = connection.provider == PsdHierarchyAiProvider.Claude ||
                                               connection.provider == PsdHierarchyAiProvider.Grok;
                string requestedSessionId = supportsPresetSessionId && !resumeCliSession
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
                // Grok 只能用 --prompt-file 接收长提示词，这里把提示词落盘。
                if (!string.IsNullOrEmpty(invocation.promptFilePath))
                {
                    string promptDirectory = Path.GetDirectoryName(invocation.promptFilePath);
                    if (!string.IsNullOrEmpty(promptDirectory))
                    {
                        Directory.CreateDirectory(promptDirectory);
                    }

                    File.WriteAllText(invocation.promptFilePath, prompt, new UTF8Encoding(false));
                }

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
                        DeletePromptFile(invocation.promptFilePath);
                        return new PsdHierarchyChatSendResult(false, "无法启动所选 AI CLI。" );
                    }

                    if (invocation.writePromptToStandardInput)
                    {
                        await process.StandardInput.WriteAsync(prompt);
                    }
                    process.StandardInput.Close();
                    Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                    Task<string> errorTask = process.StandardError.ReadToEndAsync();
                    if (!await WaitForExitAsync(process, AiRequestTimeoutSeconds))
                    {
                        DeletePromptFile(invocation.promptFilePath);
                        return new PsdHierarchyChatSendResult(
                            false,
                            "AI CLI 超过 " + AiRequestTimeoutSeconds + " 秒未完成，已停止本次请求。不会自动重试或启动新的会话。");
                    }

                    string output = await outputTask;
                    string error = await errorTask;
                    int exitCode = process.ExitCode;
                    DeletePromptFile(invocation.promptFilePath);
                    if (exitCode != 0)
                    {
                        return new PsdHierarchyChatSendResult(
                            false,
                            "AI CLI 返回错误：" + FirstNonEmptyLine(error, output, "退出码 " + exitCode));
                    }

                    if (string.IsNullOrWhiteSpace(output))
                    {
                        return new PsdHierarchyChatSendResult(false, "AI CLI 未返回可显示的文本。" );
                    }

                    return ParseCliResponse(connection.provider, output, requestedSessionId);
                }
            }

            private static void DeletePromptFile(string promptFilePath)
            {
                if (string.IsNullOrEmpty(promptFilePath))
                {
                    return;
                }

                try
                {
                    if (File.Exists(promptFilePath))
                    {
                        File.Delete(promptFilePath);
                    }
                }
                catch (IOException)
                {
                    // 清理失败不影响本次结果，残留文件在 Library 下且下次同目录复用。
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            private static PsdHierarchyChatSendResult ParseCliResponse(
                PsdHierarchyAiProvider provider,
                string output,
                string fallbackSessionId)
            {
                switch (provider)
                {
                    case PsdHierarchyAiProvider.Claude:
                        return ParseClaudeCliResponse(output, fallbackSessionId);
                    case PsdHierarchyAiProvider.Grok:
                        return ParseGrokCliResponse(output, fallbackSessionId);
                    case PsdHierarchyAiProvider.Pi:
                        return ParsePiCliResponse(output, fallbackSessionId);
                    default:
                        return ParseCodexCliResponse(output, fallbackSessionId);
                }
            }

            /// <summary>
            /// Grok 的 --output-format json 返回单个对象：
            /// {"text": "...", "sessionId": "...", ...}（camelCase，与 Claude 的 result/session_id 不同）。
            /// </summary>
            private static PsdHierarchyChatSendResult ParseGrokCliResponse(
                string output,
                string fallbackSessionId)
            {
                try
                {
                    GrokCliResult response = JsonUtility.FromJson<GrokCliResult>(output);
                    string message = response == null ? string.Empty : response.text;
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        return new PsdHierarchyChatSendResult(false, "Grok CLI 未返回可显示的文本。");
                    }

                    string sessionId = !string.IsNullOrWhiteSpace(response.sessionId)
                        ? response.sessionId
                        : fallbackSessionId;
                    if (string.IsNullOrWhiteSpace(sessionId))
                    {
                        return new PsdHierarchyChatSendResult(false, "Grok CLI 未返回可恢复的会话 ID。");
                    }

                    return new PsdHierarchyChatSendResult(true, message.Trim(), sessionId);
                }
                catch (ArgumentException exception)
                {
                    return new PsdHierarchyChatSendResult(false, "解析 Grok CLI 会话失败：" + exception.Message);
                }
            }

            /// <summary>
            /// Pi 的 --mode json 是 NDJSON 事件流，不是单个 JSON 对象。实测结构：
            ///   {"type":"session","id":"<会话 ID>",...}
            ///   {"type":"turn_end","message":{"role":"assistant","content":[{"type":"text","text":"..."}]}}
            /// 因此会话 ID 取第一条 session 事件，正文取最后一条 assistant 的 turn_end。
            /// </summary>
            private static PsdHierarchyChatSendResult ParsePiCliResponse(
                string output,
                string fallbackSessionId)
            {
                string sessionId = fallbackSessionId;
                string message = string.Empty;
                int parsedEvents = 0;
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
                            PiCliEvent cliEvent = JsonUtility.FromJson<PiCliEvent>(line);
                            if (cliEvent == null)
                            {
                                continue;
                            }

                            parsedEvents++;
                            if (string.Equals(cliEvent.type, "session", StringComparison.Ordinal) &&
                                !string.IsNullOrWhiteSpace(cliEvent.id))
                            {
                                sessionId = cliEvent.id;
                                continue;
                            }

                            if (!string.Equals(cliEvent.type, "turn_end", StringComparison.Ordinal) ||
                                cliEvent.message == null ||
                                !string.Equals(cliEvent.message.role, "assistant", StringComparison.Ordinal) ||
                                cliEvent.message.content == null)
                            {
                                continue;
                            }

                            var builder = new StringBuilder();
                            for (int index = 0; index < cliEvent.message.content.Length; index++)
                            {
                                PiCliContent part = cliEvent.message.content[index];
                                if (part != null &&
                                    string.Equals(part.type, "text", StringComparison.Ordinal) &&
                                    !string.IsNullOrEmpty(part.text))
                                {
                                    builder.Append(part.text);
                                }
                            }

                            if (builder.Length > 0)
                            {
                                message = builder.ToString();
                            }
                        }
                        catch (ArgumentException)
                        {
                            // 事件流里混入非 JSON 行时跳过该行，不影响后续事件。
                        }
                    }
                }

                if (parsedEvents == 0)
                {
                    return new PsdHierarchyChatSendResult(false, "无法解析 Pi 返回的事件流。");
                }

                if (string.IsNullOrWhiteSpace(message))
                {
                    return new PsdHierarchyChatSendResult(false, "Pi CLI 未返回可显示的文本。");
                }

                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    return new PsdHierarchyChatSendResult(false, "Pi CLI 未返回可恢复的会话 ID。");
                }

                return new PsdHierarchyChatSendResult(true, message.Trim(), sessionId);
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

            private static async Task<bool> WaitForExitAsync(Process process, int timeoutSeconds)
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
                while (!process.HasExited)
                {
                    if (DateTime.UtcNow >= deadline)
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (InvalidOperationException)
                        {
                            // The process exited between the timeout check and Kill.
                        }
                        catch (System.ComponentModel.Win32Exception)
                        {
                            // Report the timeout even when the OS refuses termination.
                        }

                        return false;
                    }

                    await Task.Delay(50);
                }

                return true;
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

            /// <summary>Grok --output-format json 的返回对象。字段名是 camelCase。</summary>
            [Serializable]
            private sealed class GrokCliResult
            {
                public string text;
                public string sessionId;
                public string stopReason;
            }

            /// <summary>
            /// Pi --mode json 的一行 NDJSON 事件。<c>type</c> 为 session / turn_end 时会带出
            /// <c>id</c> 或 <c>message</c>，其余事件类型这两个字段为空，直接跳过。
            /// </summary>
            [Serializable]
            private sealed class PiCliEvent
            {
                public string type;
                public string id;
                public PiCliMessage message;
            }

            [Serializable]
            private sealed class PiCliMessage
            {
                public string role;
                public PiCliContent[] content;
            }

            [Serializable]
            private sealed class PiCliContent
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
