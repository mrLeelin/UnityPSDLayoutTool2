# AI Hierarchy Intent Compiler And Issue Workspace Design

## Status

Approved in conversation on 2026-08-10, pending written-spec review.

This design supplements `2026-08-09-ai-visual-local-organizer-stability-design.md`.
It replaces the current assumption that a semantic or topology conflict must
prevent the organizer from producing any useful result.

## Problem

The current hierarchy chat asks AI to return a complete executable JSON plan.
Unity then rewrites parts of that plan through many deterministic normalizers,
resolves node references, validates the rewritten result, and sends failures
back to AI for one correction attempt. The Python runner repeats several of the
same semantic validations.

This creates a non-convergent repair loop. A local repair can satisfy one
invariant while exposing another interaction among component extraction,
wrappers, moves, sibling order, removal, and asset naming. The failure is then
presented as "no executable plan", even when most proposed work is independent
and safe.

## Goal

Keep the one-click automatic organizer, but make Unity the only author of the
executable plan. AI returns semantic intent. Unity compiles that intent into
deterministic operations, simulates the final hierarchy, and always returns a
reviewable workspace.

Recoverable conflicts appear in a visible `待处理问题` area. They do not erase
the plan, discard successful analysis, or prevent independent safe work from
being previewed and applied.

## Non-Goals

- Automatically applying an unresolved operation.
- Allowing AI to invent node references, paths, wrapper IDs, asset paths, move
  order, or sibling indices.
- Applying a partial plan when its supposedly safe batches depend on an
  unresolved batch.
- Hiding infrastructure failures such as an unreadable snapshot or an
  unavailable target Prefab.
- Rewriting all existing execution code in one release.

## Ownership

AI owns semantic intent:

- candidate classification;
- component, variant, stateful, group-only, or unchanged intent;
- semantic English name suggestions;
- observed state meaning;
- concise evidence and confidence.

Unity owns executable structure:

- exact candidate membership and node identity;
- operation ownership and conflict priority;
- wrappers, moves, sibling indices, and removals;
- extraction contracts and Common asset paths;
- virtual hierarchy simulation;
- preflight, transaction, rollback, and replay.

The fixed operation-ownership priority is:

1. component-family extraction;
2. authoritative flat-sibling grouping;
3. ordinary hierarchy organization;
4. empty-container removal.

Once a higher-priority operation claims a node, a lower-priority operation
cannot independently move, group, or remove that node.

## Interfaces

The main seam is a pure compiler module:

```csharp
PlanCompileResult Compile(HierarchySnapshot snapshot, HierarchyIntent intent);
```

The compiler never returns a nullable workspace and does not mutate Unity
assets.

```text
PlanCompileResult
  Workspace
    SnapshotFingerprint
    Intent
    Batches[]
    Issues[]
    PreviewSummary
    State
  Diagnostics
```

`Workspace.State` is one of:

- `Ready`: every enabled batch compiled and passed virtual preflight;
- `NeedsAttention`: at least one recoverable issue is unresolved, while one or
  more independent batches may still be safe;
- `NoSafeChanges`: analysis completed but every proposed batch is quarantined;
- `InfrastructureBlocked`: the snapshot, target, compiler, or preflight could
  not run. Diagnostics and retry actions remain available.

The UI therefore always has a result to display. It no longer treats a semantic
compile issue as an absent plan.

## Intent Contract

AI returns only candidate-keyed intent:

```json
{
  "snapshotFingerprint": "...",
  "decisions": [
    {
      "candidateId": "family_002",
      "intent": "variant",
      "suggestedName": "DailyRewardItem",
      "stateAssignments": [
        { "sourceId": "n000059", "stateKey": "standard" },
        { "sourceId": "n000089", "stateKey": "final" }
      ],
      "confidence": 91,
      "reason": "Seven observed reward cards with a distinct final state."
    }
  ]
}
```

It contains no executable paths or operations. Candidate membership comes from
the authoritative snapshot and cannot be reduced, enlarged, or reordered by AI.

## Deterministic Compilation

Compilation is one ordered pass over a virtual hierarchy:

1. validate the fingerprint and intent schema;
2. create node claims for every approved semantic decision;
3. resolve claim conflicts through the fixed ownership priority;
4. compile extraction batches first;
5. compile grouping and ordinary organization only from unclaimed nodes;
6. derive removals from the simulated final child sets;
7. derive stable IDs, paths, asset names, moves, and sibling indices;
8. execute every batch against the virtual hierarchy;
9. validate global final-state invariants;
10. emit executable batches and issues together.

The compiler must make invalid combinations unrepresentable. For example, once
all seven `组 16` sources are claimed by one variant extraction, a separate
ordinary move cannot claim only six of them.

## Batches And Dependencies

An executable plan is split into atomic batches. A batch records:

- stable batch ID;
- owned node IDs;
- generated operations;
- dependencies on other batches;
- virtual-preflight result;
- output asset paths;
- issue IDs that disable it.

Two batches are independent only when their owned nodes, ancestor mutations,
wrapper destinations, removal targets, and output assets do not overlap.

When a batch has an unresolved issue, that batch and every dependent batch are
quarantined. Independent batches remain previewable. `应用安全项` is enabled
only after the exact enabled subset passes no-write preflight.

## Issue Model

Recoverable failures are data, not exceptions escaping to the window.

```text
PlanIssue
  Id
  Severity
  Category
  CandidateId
  AffectedNodeIds[]
  AffectedBatchIds[]
  Summary
  TechnicalDetails
  RecommendedResolution
  AvailableResolutions[]
  State
```

Severity is:

- `Info`: deterministic normalization was applied;
- `Warning`: the compiler used a safe fallback;
- `NeedsDecision`: user input is required for the affected batch;
- `Blocked`: the affected batch cannot be compiled or preflighted.

Every candidate-level issue offers these actions when applicable:

- `使用推荐方案`: apply the compiler's deterministic resolution;
- `保持原结构`: quarantine this candidate and leave its nodes untouched;
- `定位节点`: ping or select the affected nodes in Prefab Stage;
- `重新分析此项`: invoke bounded local AI semantic analysis for this candidate;
- `查看详情`: show invariant, ownership, and source evidence without requiring
  the user to edit JSON.

`保持原结构` is always available for semantic and topology issues. A formerly
mandatory `requiresExtraction` candidate may remain unchanged, but it cannot be
silently omitted: the workspace records the explicit unresolved or unchanged
decision.

User resolutions are stored only for the current snapshot fingerprint. A new
snapshot invalidates them.

## Review UI

The organizer keeps the simple one-click primary workflow and adds a persistent
`待处理问题` section below the plan summary.

The top summary shows:

- safe batches;
- quarantined batches;
- unresolved issue count;
- affected node count;
- planned output assets;
- preflight state.

Each issue row shows the semantic item, concise reason, affected nodes, and the
recommended action. Technical details are collapsed by default. Resolving an
item recompiles the workspace locally and updates the preview without rerunning
the full AI request.

Primary commands are:

- `应用全部`: available only in `Ready`;
- `应用安全项`: available in `NeedsAttention` when the enabled independent
  subset passes preflight;
- `处理问题`: focuses the first unresolved issue;
- `重新分析`: starts a new full semantic analysis;
- `导出诊断`: writes the workspace, issues, and compiler diagnostics under
  `Library/PSDLayoutTool2`.

An infrastructure failure uses the same panel. It shows retry, locate target,
refresh snapshot, and export-diagnostic actions instead of replacing the whole
window with a terminal error string.

## Apply And Rollback

Analysis, compilation, issue resolution, and virtual preflight never write the
Prefab.

Before either apply action, Unity refreshes the snapshot fingerprint and runs
preflight against exactly the enabled batch subset. Apply remains one
transaction. Any failure rolls back every file touched by that apply.

Quarantined nodes must be byte-for-byte and hierarchy-position unchanged by the
enabled subset. If that cannot be proven, the dependent batch is also
quarantined.

## Failure Policy

AI retry is limited to semantic-contract failures. Mechanical conflicts are
never returned to AI for editing executable JSON.

The system distinguishes:

- semantic uncertainty: create `NeedsDecision`;
- deterministic conflict: use a defined fallback or create `NeedsDecision`;
- unsupported structure: quarantine the candidate with `Blocked`;
- stale fingerprint: invalidate the workspace and offer refresh;
- compiler defect: create `InfrastructureBlocked`, preserve diagnostics, and
  produce a regression fixture;
- apply failure: rollback and keep the workspace plus failure report.

No recoverable candidate issue becomes "AI failed to generate a plan".

## Migration

### Stage 1: Workspace Compatibility Layer

Wrap the existing preparation pipeline in `PlanWorkspace`. Convert current
validation exceptions into structured issues. Preserve the existing validated
plan as one batch. This delivers the issue panel and diagnostic persistence
without changing execution semantics.

### Stage 2: Ownership Graph And Virtual Hierarchy

Introduce the pure compiler module, node claims, batch dependencies, virtual
operations, and final-state invariants. Move component-family, variant, and
flat-sibling interaction rules into this module first.

### Stage 3: Intent-Only AI Contract

Replace the executable JSON prompt with the candidate-keyed intent schema. Add
a temporary adapter that can read old reviewed plans during migration, but all
new AI runs use intent.

### Stage 4: Safe Partial Apply

Enable `应用安全项` after dependency isolation, subset preflight, quarantine
verification, and transactional tests are complete.

### Stage 5: Remove Duplicate Repair Logic

Delete superseded local normalizers and Python semantic validation. The runner
retains only schema, asset existence, transaction, and execution-time safety
checks.

## Testing

### Compiler Fixtures

- every stored hierarchy snapshot compiles deterministically;
- `组 16` variant extraction cannot coexist with a six-of-seven ordinary move;
- component, flat-sibling, move, removal, and asset claims follow fixed priority;
- the same snapshot and intent produce byte-identical workspace output;
- unsupported candidates produce issues rather than a null plan.

### Property Tests

- every node has at most one structural owner;
- enabled batches have no overlapping claims or output paths;
- extraction sources share the required final parent;
- removal targets are empty in the virtual final hierarchy;
- quarantined nodes remain unchanged;
- every emitted executable plan passes the same global invariants.

### UI Tests

- an issue appears without hiding the safe plan;
- every issue action recompiles and refreshes the workspace;
- `应用全部` and `应用安全项` obey workspace state;
- node location uses authoritative IDs;
- technical diagnostics remain available after failed analysis or preflight.

### Transaction Tests

- safe-subset apply touches only enabled batch assets;
- fault injection at every write stage restores all touched files;
- unresolved and quarantined nodes remain unchanged after successful apply;
- stale fingerprints block apply but retain the issue workspace.

## Acceptance Criteria

- AI never returns executable hierarchy operations for new runs.
- The organizer always displays a workspace after analysis, even when no batch
  is currently executable.
- Candidate-level conflicts appear in `待处理问题` with at least `保持原结构`
  and `查看详情` actions.
- Independent safe batches remain previewable and can be applied only after
  subset preflight.
- No unresolved batch or dependent batch writes assets.
- All historical organizer failure snapshots are permanent compiler fixtures.
- Successful compiler output cannot fail later semantic topology validation;
  any such failure is classified as a compiler defect, retained as diagnostics,
  and added as a regression fixture.
