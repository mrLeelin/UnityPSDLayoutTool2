# PSD workflow safety boundary

The selection-driven path is now treated as a review transaction:

`SelectionRead -> SelectionNormalized -> SnapshotCaptured -> ReviewDrafted -> PlanValidated -> AwaitingApproval -> Approved -> Applying -> Applied|Rejected|Partial|Uncertain`.

Only `Approved` can create an `.apply` sentinel. The sentinel contains a JSON approval record with the exact approval phrase, plan path, SHA-256, snapshot fingerprint, review version, and target Prefab path. Empty sentinels and legacy sessions are rejected before a Prefab is loaded for writing.

`TMP_SubMeshUI` selections normalize to their owning `TextMeshProUGUI`. The normalizer retains original and normalized paths, instance IDs, and component names so a review can explain the change. A plan must carry `selectionNodeIds`, `operationScope`, `expectedNodeCount`, `expectedHierarchy`, `directChildren`, `absentPaths`, and `preserveRequirements`; the executor re-reads the authoritative snapshot and rejects drift or unknown node IDs.

The terminal watcher remains the single apply entry point for this protocol. It claims a sentinel once, records a workflow ledger, and never retries `partial` or `uncertain` requests. Existing protocol-2 sessions are intentionally rejected when they do not contain the authoritative snapshot binding; launching a new AI review creates a current session record with the fingerprint.

`NavItem_Diary.prefab` is treated as an incident artifact. It must be inspected and compared against the ledger and the intended review before any cleanup or deletion is planned. This change does not modify or delete it.

The Unity CLI/editor process remains a privileged boundary: arbitrary external `eval` or editor scripts must be disabled or separately sandboxed by the host. The C# gate cannot prove that an out-of-process actor did not bypass Unity's apply watcher.
