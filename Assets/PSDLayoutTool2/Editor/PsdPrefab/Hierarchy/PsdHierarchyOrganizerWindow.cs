namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Pure preview/orchestration state. It clones all plans and pending lists,
    /// so opening, retrying, confirming cleanup, cancelling, or closing a window
    /// cannot mutate the Prefab or persisted Profile.
    /// </summary>
    public sealed class PsdHierarchyOrganizerPreviewModel
    {
        private readonly PsdHierarchyRequest fullRequest;
        private readonly PsdHierarchyPlan baselinePlan;
        private readonly PsdHierarchyReconciliationResult reconciliation;
        private readonly IPsdHierarchyAiRunner runner;

        public PsdHierarchyOrganizerPreviewModel(
            string targetPrefabPath,
            PsdHierarchyRequest fullRequest,
            PsdHierarchyPlan baselinePlan,
            PsdHierarchyReconciliationResult reconciliation,
            IPsdHierarchyAiRunner runner)
        {
            this.targetPrefabPath = targetPrefabPath ?? string.Empty;
            this.fullRequest = CloneRequest(fullRequest ?? throw new ArgumentNullException("fullRequest"));
            this.baselinePlan = ClonePlan(baselinePlan ?? throw new ArgumentNullException("baselinePlan"));
            this.reconciliation = reconciliation ?? throw new ArgumentNullException("reconciliation");
            this.runner = runner ?? throw new ArgumentNullException("runner");
            proposedPlan = ClonePlan(this.baselinePlan);
            pendingMissingStableIds = new List<string>(reconciliation.pendingMissingStableIds);
        }

        public string targetPrefabPath { get; private set; }
        public IList<PsdHierarchyRequestNode> currentTreeNodes
        {
            get { return fullRequest.nodes.Select(CloneNode).ToList(); }
        }
        public PsdHierarchyPlan proposedPlan { get; private set; }
        public List<string> validationErrors { get; } = new List<string>();
        public List<string> pendingMissingStableIds { get; private set; }
        public bool canApply { get; private set; }
        public bool isRunning { get; private set; }

        public async Task RefreshAsync(bool confirmMissingCleanup, CancellationToken cancellationToken)
        {
            isRunning = true;
            canApply = false;
            validationErrors.Clear();
            pendingMissingStableIds = confirmMissingCleanup
                ? new List<string>()
                : new List<string>(reconciliation.pendingMissingStableIds);
            PsdHierarchyPlan working = ClonePlan(baselinePlan);

            try
            {
                List<HashSet<string>> scopes = BuildFocusedScopes(working);
                foreach (HashSet<string> scope in scopes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    PsdHierarchyRequest scopedRequest = CloneScopedRequest(fullRequest, scope);
                    var runRequest = new PsdHierarchyAiRunRequest
                    {
                        operationId = Guid.NewGuid().ToString("N"),
                        request = scopedRequest,
                        targetPrefabPath = targetPrefabPath,
                        timeout = TimeSpan.FromMinutes(2)
                    };
                    PsdHierarchyAiRunResult result = await runner.RunAsync(runRequest, cancellationToken);
                    if (result == null || !result.succeeded || result.plan == null)
                    {
                        validationErrors.Add(result != null && !string.IsNullOrWhiteSpace(result.error)
                            ? result.error
                            : "Hierarchy planner returned no validated plan.");
                        proposedPlan = working;
                        return;
                    }

                    EnsurePartialPlanStaysInScope(result.plan, scope);
                    MergeScope(working, result.plan, scope);
                }

                if (confirmMissingCleanup)
                {
                    RemoveConfirmedMissing(working, reconciliation.pendingMissingStableIds);
                }
                AdoptCurrentIdentity(working, fullRequest);
                PsdHierarchyPlanValidator.Validate(working, fullRequest);
                proposedPlan = working;
                if (pendingMissingStableIds.Count > 0)
                {
                    validationErrors.Add("Missing PSD IDs are pending explicit cleanup confirmation.");
                    return;
                }
                canApply = true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is PsdHierarchyPlanValidationException ||
                exception is InvalidOperationException ||
                exception is ArgumentException)
            {
                validationErrors.Add(exception.Message);
                proposedPlan = working;
            }
            finally
            {
                isRunning = false;
            }
        }

        public void ImportManualPlan(string json)
        {
            canApply = false;
            validationErrors.Clear();
            try
            {
                PsdHierarchyPlan candidate = PsdHierarchyPlanJson.Parse(json);
                PsdHierarchyPlanValidator.Validate(candidate, fullRequest);
                proposedPlan = ClonePlan(candidate);
                if (pendingMissingStableIds.Count > 0)
                {
                    validationErrors.Add("Missing PSD IDs are pending explicit cleanup confirmation.");
                    return;
                }
                canApply = true;
            }
            catch (Exception exception) when (
                exception is PsdHierarchyPlanFormatException ||
                exception is PsdHierarchyPlanValidationException)
            {
                validationErrors.Add(exception.Message);
            }
        }

        private List<HashSet<string>> BuildFocusedScopes(PsdHierarchyPlan plan)
        {
            var seeds = reconciliation.focusedInvalidatedScopeStableIds
                .Concat(reconciliation.unsortedNewStableIds)
                .Concat(reconciliation.geometryValidationStableIds)
                .Where(PsdStableLayerIdUtility.IsPersistable)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var unassigned = new HashSet<string>(seeds, StringComparer.Ordinal);
            var scopes = new List<HashSet<string>>();
            while (unassigned.Count > 0)
            {
                string seed = unassigned.OrderBy(value => value, StringComparer.Ordinal).First();
                var scope = new HashSet<string>(StringComparer.Ordinal) { seed };

                // Existing groups are atomic decisions. If one member changes,
                // include that whole old group so unaffected groups remain exact.
                bool expanded;
                do
                {
                    expanded = false;
                    foreach (PsdHierarchyPlanGroup group in plan.groups ?? new List<PsdHierarchyPlanGroup>())
                    {
                        if (group != null && group.memberStableIds.Any(scope.Contains))
                        {
                            int before = scope.Count;
                            scope.UnionWith(group.memberStableIds.Where(PsdStableLayerIdUtility.IsPersistable));
                            expanded |= before != scope.Count;
                        }
                    }
                }
                while (expanded);

                unassigned.ExceptWith(scope);
                scopes.Add(scope);
            }
            return scopes;
        }

        private static void EnsurePartialPlanStaysInScope(PsdHierarchyPlan partial, HashSet<string> scope)
        {
            IEnumerable<string> touched = (partial.groups ?? new List<PsdHierarchyPlanGroup>())
                .Where(group => group != null)
                .SelectMany(group => group.memberStableIds ?? new List<string>())
                .Concat((partial.renames ?? new List<PsdHierarchyPlanRename>())
                    .Where(rename => rename != null)
                    .Select(rename => rename.stableId));
            string outside = touched.FirstOrDefault(id => !scope.Contains(id));
            if (outside != null)
            {
                throw new InvalidOperationException(
                    "Focused hierarchy plan touched ID '" + outside + "' outside its allowed scope.");
            }
        }

        private static void MergeScope(PsdHierarchyPlan target, PsdHierarchyPlan partial, HashSet<string> scope)
        {
            target.groups.RemoveAll(group => group != null && group.memberStableIds.Any(scope.Contains));
            target.renames.RemoveAll(rename => rename != null && scope.Contains(rename.stableId));
            target.groups.AddRange((partial.groups ?? new List<PsdHierarchyPlanGroup>()).Select(CloneGroup));
            target.renames.AddRange((partial.renames ?? new List<PsdHierarchyPlanRename>()).Select(CloneRename));
        }

        private static void RemoveConfirmedMissing(PsdHierarchyPlan plan, IEnumerable<string> missingStableIds)
        {
            var missing = new HashSet<string>(missingStableIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            plan.renames.RemoveAll(rename => rename != null && missing.Contains(rename.stableId));
            foreach (PsdHierarchyPlanGroup group in plan.groups)
            {
                group.memberStableIds.RemoveAll(missing.Contains);
            }
            plan.groups.RemoveAll(group => group.memberStableIds.Count == 0);
        }

        private static PsdHierarchyRequest CloneScopedRequest(PsdHierarchyRequest source, HashSet<string> scope)
        {
            return new PsdHierarchyRequest
            {
                schemaVersion = source.schemaVersion,
                sourcePsdGuid = source.sourcePsdGuid,
                sourceFingerprint = source.sourceFingerprint,
                contentFingerprint = source.contentFingerprint,
                structureFingerprint = source.structureFingerprint,
                geometryFingerprint = source.geometryFingerprint,
                documentWidth = source.documentWidth,
                documentHeight = source.documentHeight,
                nodes = source.nodes.Where(node => node != null && scope.Contains(node.stableId)).Select(CloneNode).ToList(),
                currentPrefabHierarchy = source.currentPrefabHierarchy
                    .Where(node => node != null && scope.Contains(node.stableId))
                    .Select(CloneMetadata).ToList(),
                previews = source.previews.Select(ClonePreview).ToList()
            };
        }

        private static PsdHierarchyRequest CloneRequest(PsdHierarchyRequest source)
        {
            return new PsdHierarchyRequest
            {
                schemaVersion = source.schemaVersion,
                sourcePsdGuid = source.sourcePsdGuid,
                sourceFingerprint = source.sourceFingerprint,
                contentFingerprint = source.contentFingerprint,
                structureFingerprint = source.structureFingerprint,
                geometryFingerprint = source.geometryFingerprint,
                documentWidth = source.documentWidth,
                documentHeight = source.documentHeight,
                nodes = (source.nodes ?? new List<PsdHierarchyRequestNode>()).Where(node => node != null).Select(CloneNode).ToList(),
                currentPrefabHierarchy = (source.currentPrefabHierarchy ?? new List<PsdHierarchyPrefabNodeMetadata>())
                    .Where(node => node != null).Select(CloneMetadata).ToList(),
                previews = (source.previews ?? new List<PsdHierarchyPreviewReference>())
                    .Where(preview => preview != null).Select(ClonePreview).ToList()
            };
        }

        private static void AdoptCurrentIdentity(PsdHierarchyPlan plan, PsdHierarchyRequest request)
        {
            plan.schemaVersion = PsdHierarchyPlan.CurrentSchemaVersion;
            plan.sourcePsdGuid = request.sourcePsdGuid;
            plan.sourceFingerprint = request.sourceFingerprint;
            plan.contentFingerprint = request.contentFingerprint;
            plan.structureFingerprint = request.structureFingerprint;
            plan.geometryFingerprint = request.geometryFingerprint;
        }

        private static PsdHierarchyPlan ClonePlan(PsdHierarchyPlan source)
        {
            return new PsdHierarchyPlan
            {
                schemaVersion = source.schemaVersion,
                sourcePsdGuid = source.sourcePsdGuid,
                sourceFingerprint = source.sourceFingerprint,
                contentFingerprint = source.contentFingerprint,
                structureFingerprint = source.structureFingerprint,
                geometryFingerprint = source.geometryFingerprint,
                groups = (source.groups ?? new List<PsdHierarchyPlanGroup>()).Select(CloneGroup).ToList(),
                renames = (source.renames ?? new List<PsdHierarchyPlanRename>()).Select(CloneRename).ToList()
            };
        }

        private static PsdHierarchyPlanGroup CloneGroup(PsdHierarchyPlanGroup source)
        {
            return new PsdHierarchyPlanGroup
            {
                key = source.key,
                parentKey = source.parentKey,
                memberStableIds = new List<string>(source.memberStableIds ?? new List<string>()),
                displayName = source.displayName,
                evidence = source.evidence,
                confidence = source.confidence
            };
        }

        private static PsdHierarchyPlanRename CloneRename(PsdHierarchyPlanRename source)
        {
            return new PsdHierarchyPlanRename
            {
                stableId = source.stableId,
                name = source.name,
                evidence = source.evidence,
                confidence = source.confidence
            };
        }

        private static PsdHierarchyRequestNode CloneNode(PsdHierarchyRequestNode source)
        {
            return new PsdHierarchyRequestNode
            {
                stableId = source.stableId,
                originalName = source.originalName,
                kind = source.kind,
                parentStableId = source.parentStableId,
                siblingIndex = source.siblingIndex,
                rectangle = source.rectangle,
                hasProjectComponents = source.hasProjectComponents,
                isProtectedBoundary = source.isProtectedBoundary,
                protectedBoundaryStableId = source.protectedBoundaryStableId
            };
        }

        private static PsdHierarchyPrefabNodeMetadata CloneMetadata(PsdHierarchyPrefabNodeMetadata source)
        {
            return new PsdHierarchyPrefabNodeMetadata
            {
                stableId = source.stableId,
                parentStableId = source.parentStableId,
                siblingIndex = source.siblingIndex,
                hierarchyPath = source.hierarchyPath,
                componentTypes = new List<string>(source.componentTypes ?? new List<string>()),
                hasProjectComponents = source.hasProjectComponents,
                isProtectedBoundary = source.isProtectedBoundary,
                protectedBoundaryStableId = source.protectedBoundaryStableId
            };
        }

        private static PsdHierarchyPreviewReference ClonePreview(PsdHierarchyPreviewReference source)
        {
            return new PsdHierarchyPreviewReference { key = source.key, kind = source.kind, crop = source.crop };
        }
    }

    /// <summary>
    /// Non-mutating Editor preview. Apply is exposed as an event only after full
    /// validation; the window itself never saves an Asset, Prefab or Profile.
    /// </summary>
    public sealed class PsdHierarchyOrganizerWindow : EditorWindow
    {
        private PsdHierarchyOrganizerPreviewModel model;
        private CancellationTokenSource cancellation;
        private Vector2 scroll;
        private bool confirmMissingCleanup;

        public event Action<PsdHierarchyPlan> applyRequested;

        public static PsdHierarchyOrganizerWindow Open(PsdHierarchyOrganizerPreviewModel previewModel)
        {
            var window = GetWindow<PsdHierarchyOrganizerWindow>(true, "PSD Hierarchy Preview", true);
            window.model = previewModel ?? throw new ArgumentNullException("previewModel");
            window.minSize = new Vector2(720f, 480f);
            window.Show();
            return window;
        }

        private void OnDisable()
        {
            CancelRunningRequest();
        }

        private void OnGUI()
        {
            if (model == null)
            {
                EditorGUILayout.HelpBox("No PSD hierarchy preview context is loaded.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Target Prefab (exact configured path)", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(model.targetPrefabPath, EditorStyles.textField, GUILayout.Height(20f));

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !model.isRunning;
                if (GUILayout.Button("Generate / Retry Preview"))
                {
                    StartRefresh();
                }
                if (GUILayout.Button("Import Manual Plan"))
                {
                    ImportManualPlan();
                }
                GUI.enabled = model.isRunning;
                if (GUILayout.Button("Cancel"))
                {
                    CancelRunningRequest();
                }
                GUI.enabled = true;
            }

            if (model.pendingMissingStableIds.Count > 0)
            {
                confirmMissingCleanup = EditorGUILayout.ToggleLeft(
                    "Confirm cleanup of missing PSD IDs in the proposed plan only",
                    confirmMissingCleanup);
            }

            foreach (string error in model.validationErrors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawCurrentTree();
            GUILayout.Space(8f);
            DrawProposedTree();
            EditorGUILayout.EndScrollView();

            GUI.enabled = model.canApply && !model.isRunning;
            if (GUILayout.Button("Apply Validated Plan"))
            {
                applyRequested?.Invoke(model.proposedPlan);
            }
            GUI.enabled = true;
        }

        private async void StartRefresh()
        {
            CancelRunningRequest();
            cancellation = new CancellationTokenSource();
            try
            {
                await model.RefreshAsync(confirmMissingCleanup, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // User cancellation/closing is an expected non-mutating outcome.
            }
            finally
            {
                cancellation?.Dispose();
                cancellation = null;
                Repaint();
            }
        }

        private void ImportManualPlan()
        {
            string path = EditorUtility.OpenFilePanel("Import PSD hierarchy plan", string.Empty, "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            try
            {
                model.ImportManualPlan(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void CancelRunningRequest()
        {
            if (cancellation != null && !cancellation.IsCancellationRequested)
            {
                cancellation.Cancel();
            }
        }

        private void DrawCurrentTree()
        {
            EditorGUILayout.LabelField("Current tree", EditorStyles.boldLabel);
            foreach (PsdHierarchyRequestNode node in model.currentTreeNodes
                         .OrderBy(node => node.parentStableId, StringComparer.Ordinal)
                         .ThenBy(node => node.siblingIndex))
            {
                string parent = string.IsNullOrEmpty(node.parentStableId) ? "root" : node.parentStableId;
                EditorGUILayout.LabelField(
                    node.originalName + "  [id=" + node.stableId + ", parent=" + parent + ", index=" + node.siblingIndex + "]");
            }
        }

        private void DrawProposedTree()
        {
            EditorGUILayout.LabelField("Proposed tree / evidence / confidence", EditorStyles.boldLabel);
            foreach (PsdHierarchyPlanGroup group in model.proposedPlan.groups ?? new List<PsdHierarchyPlanGroup>())
            {
                EditorGUILayout.LabelField(group.displayName + "  [" + group.key + "]");
                EditorGUILayout.LabelField("  members: " + string.Join(", ", group.memberStableIds.ToArray()));
                EditorGUILayout.LabelField("  confidence: " + group.confidence.ToString("0.00") + "  evidence: " + group.evidence,
                    EditorStyles.wordWrappedMiniLabel);
            }
            foreach (PsdHierarchyPlanRename rename in model.proposedPlan.renames ?? new List<PsdHierarchyPlanRename>())
            {
                EditorGUILayout.LabelField(rename.stableId + " -> " + rename.name);
                EditorGUILayout.LabelField("  confidence: " + rename.confidence.ToString("0.00") + "  evidence: " + rename.evidence,
                    EditorStyles.wordWrappedMiniLabel);
            }
        }
    }
}
