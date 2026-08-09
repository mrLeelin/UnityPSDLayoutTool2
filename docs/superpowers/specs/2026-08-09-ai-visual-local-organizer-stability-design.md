# AI Visual Local Organizer Stability Design

## Status

Approved in conversation on 2026-08-09.

This design supersedes the visual and extraction limitations in
`2026-08-03-ai-hierarchy-local-repair-design.md` and the statement in
`2026-08-04-cross-parent-local-prefab-organization-design.md` that local
organization must remain inside the main chat window. The existing dedicated
local-repair window is retained and evolved.

## Goal

Build a stable local Prefab organization workflow in which AI is mandatory and
real visual analysis is a required planning stage. The AI must compare both the
original PSD appearance and the actual Unity rendering, infer component
boundaries, repeated instances, visual states, and semantic names, and then
produce a reviewable organization decision.

Unity remains authoritative for scope, identity, concrete node references,
asset paths, operation ordering, preflight validation, transactional writes,
rollback, and replay persistence. AI failure must never silently fall back to a
mock score or a non-AI organization path.

The first release supports Codex CLI visual input only.

## Success Criteria

- Every local organization plan is preceded by a successful real AI visual
  analysis.
- PSD and Unity images are both present for every candidate presented to AI.
- A plan cannot reference a node outside the locked local scope.
- The same snapshot and selection produce the same candidate set and concrete
  structural operations.
- One plan can create multiple non-overlapping Common Prefabs.
- No failed analysis or preflight writes a Prefab, Profile, or Replay asset.
- Any failed confirmed apply restores every touched asset and removes new empty
  or partial assets.
- The final plan remains behind explicit human confirmation.

Target reliability for the approved fixture is:

- zero out-of-scope writes;
- zero partial asset writes;
- 100 percent candidate-set consistency across 20 repeated runs;
- 100 percent child-Prefab count consistency across 20 repeated runs;
- at least 95 percent executable-plan success across 20 repeated runs.

## Non-Goals

- Claude CLI, OpenAI Custom API, Anthropic Custom API, or other visual providers
  in the first release.
- Automatic apply based only on AI confidence.
- Silent numeric suffixes for conflicting Prefab asset names.
- AI-invented node IDs, hierarchy paths, asset paths, or operation targets.
- Full-Prefab similarity scanning by default.
- Overlapping or recursively nested extraction sources in the first release.
- A mock visual-analysis fallback.
- Modifying the PSD source file.

## Core Ownership Boundary

AI owns semantic interpretation:

- which candidate is a reusable component;
- which candidates are instances of the same component;
- whether the component is plain, variant, or stateful;
- which visible differences represent states rather than different components;
- suggested English PascalCase names;
- visual evidence, confidence, and explanation.

Unity owns executable structure:

- candidate generation and stable IDs;
- locked selection and editable node registry;
- exact source nodes and instance mappings;
- wrapper, move, delete, and tight-bounds operations;
- Common Prefab asset paths;
- conflict and overlap resolution;
- preflight simulation;
- transactional apply and rollback;
- Profile and Replay updates.

AI is therefore required, but it operates inside a Unity-defined candidate and
scope contract rather than writing arbitrary executable operations.

## User Workflow

1. The user opens the target Prefab in Prefab Stage.
2. The user opens the existing dedicated `Local Organize` window from the main
   AI organizer.
3. The user selects one or more nodes and presses `Lock Selection`.
4. Unity refreshes the authoritative snapshot and creates a scope fingerprint.
5. Unity generates deterministic, non-overlapping local candidates.
6. Unity creates paired PSD and Unity images for every candidate.
7. Codex CLI performs visual analysis and returns schema-constrained evidence.
8. Unity validates the complete visual-evidence response.
9. The same primary CLI session generates semantic organization decisions from
   the validated evidence.
10. Unity compiles those decisions into a concrete execution plan and performs
    a no-write preflight.
11. The window presents paired images, AI reasoning, confidence, candidate
    exclusions, output assets, and Unity normalizations.
12. The user may deselect a complete extraction item or regenerate from a new
    locked selection. The user cannot hand-edit source membership.
13. `Confirm and Update` revalidates the snapshot and performs one
    transactional write.

## Local Scope

The first release defaults to strict local organization.

The scope contains:

- the locked selected node IDs;
- descendants allowed by the selected scope mode;
- direct parent IDs allowed as wrapper destinations;
- a scope fingerprint derived from the target Prefab, hierarchy snapshot,
  selected IDs, editable IDs, geometry, and referenced visual assets.

AI may combine or classify candidates inside the scope. It cannot enlarge or
shrink a concrete candidate source set, reference a node outside the scope, or
move a candidate to an unapproved parent.

Any change to selection, hierarchy, RectTransform geometry, Sprite, material,
font, active state, or source PSD identity invalidates visual evidence and the
plan. The user must capture a new scope and rerun AI.

`Find Similar Instances` remains an explicit advanced action. It is not enabled
by default. When enabled in a later stage, the UI must show the common ancestor
and candidate scan boundary before AI runs.

## Deterministic Candidate Generation

Unity generates a finite candidate graph before any AI call. Candidate records
contain only observed data:

- `candidateId`;
- ordered `nodeIds`;
- ordered PSD stable layer IDs;
- direct parent and lowest common ancestor IDs;
- RectTransform geometry and sibling order;
- Image, TMP, Mask, material, and component signatures;
- structural fingerprint;
- source bounds and visual asset fingerprints.

Candidates are sorted by parent ID, depth, first sibling index, and candidate ID.
Candidates with invalid identities, external serialized references, unsupported
render ordering, or incomplete PSD mappings are excluded before image capture
and reported to the user.

When candidate sources overlap, Unity uses this deterministic priority:

1. mandatory component-family candidates;
2. higher structural match score;
3. deeper hierarchy candidate;
4. ordinal candidate ID.

The first release sends only non-overlapping candidates to AI. AI cannot restore
an excluded overlap.

## Visual Evidence Package

Each run writes temporary evidence under:

```text
Library/PSDLayoutTool2/VisualAnalysis/<runId>/
```

Each candidate has:

```text
candidate_001/
  psd.png
  unity.png
  manifest.json
```

The manifest contains:

```json
{
  "candidateId": "candidate_001",
  "nodeIds": ["n000031", "n000032"],
  "stableLayerIds": ["lyid:101", "lyid:102"],
  "snapshotFingerprint": "...",
  "scopeFingerprint": "...",
  "psdImageHash": "...",
  "unityImageHash": "...",
  "sourceBounds": {
    "width": 420,
    "height": 180
  }
}
```

Files remain under `Library`, do not enter `Assets`, do not trigger asset
imports, and are not tracked by Git.

### PSD Image

Unity loads the source PSD through the existing PhotoshopFile integration and
maps candidate nodes through persisted stable layer IDs. It composites only the
candidate layers in original order, using the union of their resolved layout
bounds plus fixed proportional transparent padding.

The crop preserves aspect ratio, alpha, visibility, opacity, masks, and PSD
ordering as far as the existing parser exposes them. It is never stretched to a
square. A candidate with missing or ambiguous stable-layer mapping is rejected;
the implementation must not guess by display name.

### Unity Image

The current temporary-camera implementation is replaced. It must not toggle
objects in the user's Prefab Stage.

Unity creates an isolated in-memory Preview Scene, clones the candidate under a
dedicated Screen Space Camera Canvas, records its observed active state, and
temporarily activates only the isolated clone for capture. It renders through a
fixed orthographic camera to a transparent RenderTexture. The capture preserves
Sprite, material, TMP, Mask, color, alpha, the recorded active-state metadata,
and relative RectTransform geometry. The user's Prefab Stage object is never
toggled. The Preview Scene and all temporary objects are destroyed after
capture.

PSD and Unity images use the same content aspect ratio and equivalent source
bounds so the model can compare design intent and imported result directly.
When their evidence conflicts, Unity rendering is authoritative for executable
structure, while the conflict remains visible and requires user review.

### Capture Validation

Before a CLI call, each pair must pass:

- positive width and height;
- PNG encode and decode round trip;
- minimum non-transparent pixel ratio;
- rejection of fully transparent, all-black, or single-color empty captures;
- manifest hash equality;
- exact candidate ID and source membership;
- presence of both PSD and Unity images.

Failure of either image stops that candidate batch. There is no mock or
structure-only fallback.

## Batch Limit

A visual batch contains at most eight candidates, or sixteen images. Candidates
are batched by common parent and visual region in deterministic order.

Each batch uses a visual CLI session. For one batch, that same session becomes
the primary planning session. For multiple batches, Unity validates every batch
and merges the sealed evidence by candidate ID. The first batch session becomes
the primary planning session. Unity then resumes that primary session once per
additional batch, attaching at most that batch's sixteen images plus its
validated manifest and evidence. After the primary session has seen every batch,
Unity sends the final planning prompt with the complete merged evidence. No
single request exceeds the image cap, and the planning AI has directly received
all images.

Cross-batch duplicate candidate IDs, overlapping sources, or inconsistent
template assignments reject the merged evidence.

## Codex CLI Transport

The first visual request uses `codex exec` with:

- one `--image` entry per PSD and Unity image;
- `--output-schema <visual-evidence-schema>`;
- `--json` for event parsing;
- the existing read-only sandbox and project working directory.

The prompt lists attachments in exact argument order and maps each pair to one
candidate ID. Image paths are absolute, quoted, and support spaces and Unicode.

`codex exec resume` also receives `--image`, `--output-schema`, and `--json` for
same-session corrections and additional-batch ingestion. The response parser
reads the structured final response. Markdown fence extraction is not the
primary visual-response contract.

## Visual Evidence Contract

The visual response contains the two fingerprints and exactly one result for
every candidate in the batch.

Each result contains:

- `candidateId`;
- `decision`: `extract`, `group_only`, `leave_unchanged`, or `needs_review`;
- `componentKind`: `component`, `variant`, `stateful`, `structural_group`, or
  `none`;
- `suggestedName`;
- `similarityScore` from 0 through 100;
- `confidence` from 0 through 100;
- `psdUnityConsistency`;
- template and instance candidate IDs;
- observed visual-state assignments;
- concise evidence grounded in the attached images.

AI must not return hierarchy paths, asset paths, concrete moves, deletes,
siblings, wrapper IDs, or unobserved candidate IDs.

Unity rejects evidence when:

- either fingerprint differs;
- a candidate is missing or duplicated;
- an ID is absent from the manifest;
- instances overlap across extraction decisions;
- a template is absent from its instance set;
- a variant has fewer than two observed visual states;
- a stateful result lacks both common and state-specific evidence;
- PSD and Unity disagreement is hidden behind a high-confidence result.

Confidence behavior is:

- 80 through 100: eligible for the confirmation plan;
- 60 through 79: shown as `Needs Review` and disabled by default;
- 0 through 59: ineligible for automatic extraction;
- any PSD/Unity conflict: `Needs Review` regardless of numeric confidence.

## Planning Stage

After visual evidence passes validation, the primary CLI session receives the
complete validated evidence and the current hierarchy contract. AI remains
responsible for semantic organization, component kind, state interpretation,
names, and reasons. It returns decisions keyed only by candidate ID.

Unity compiles those decisions into:

- exact `node:<id>` sources;
- template and instance mappings;
- unique reviewed `Common/*.prefab` paths;
- wrappers, moves, sibling indices, and tight bounds;
- valid empty-container removals;
- component, variant, and stateful extraction records;
- verification invariants.

An existing target asset path blocks confirmation. Unity does not silently add a
numeric suffix. The user must accept a regenerated name or change the selection.

Unity records every normalization, including dropped conflicting empty-container
removals, and shows it in the review.

## Retry And Failure Recovery

Retries are finite and preserve full context.

### Visual Stage

1. An invalid first response receives one correction request in the same CLI
   session with the batch images, schema, manifest, and validation error
   attached again.
2. If correction fails, Unity clears the session ID and starts one fresh session
   with all images, manifests, fingerprints, and the concrete failure reason.
3. If the fresh response fails, the batch stops. Unity preserves both responses
   and the exact parse or validation error.

### Planning Stage

1. An invalid plan receives one correction request in the primary session.
2. If correction fails, Unity clears the session ID and creates a fresh primary
   session. It replays every visual batch sequentially, attaching no more than
   sixteen images per request, then sends the complete merged evidence, current
   snapshot, and concrete failure reason for a new plan.
3. If the fresh plan fails, the run stops without modifying assets.

A fresh session never receives only the last error message. It receives the
complete current context and reattaches every required image in deterministic
bounded batches.

## State Machine

The dedicated local organizer follows:

```text
Idle
  -> SelectionLocked
  -> CapturingImages
  -> VisualAnalyzing
  -> VisualValidated
  -> Planning
  -> PlanValidated
  -> AwaitingConfirmation
  -> Applying
  -> Succeeded
```

Any stage may enter `Failed`. A failed run retains its evidence package and
diagnostics. One window cannot run concurrent analysis. Selection changes reset
the run to `SelectionLocked`. Confirmation is enabled only in
`AwaitingConfirmation`.

Cancellation is allowed before `Applying`. Once transactional apply starts, the
system completes either verification or rollback before accepting cancellation.

## Review UI

The existing dedicated local-repair window displays:

- locked target and scope summary;
- paired PSD and Unity thumbnails;
- AI decision, component kind, similarity, confidence, and reason;
- PSD/Unity disagreement;
- template, instance, and state counts;
- output Common Prefab name and path;
- excluded overlaps and unsupported candidates;
- Unity normalizations;
- preflight node, asset, and instance counts;
- first-response, correction, and parse diagnostics on failure.

The user may disable a whole extraction decision. Source membership cannot be
edited in place; changing it requires a new locked selection and a new AI run.

## No-Write Preflight

Before confirmation, Unity loads an isolated in-memory copy of the main Prefab
and simulates the compiled plan without saving assets. Preflight verifies:

- wrappers, moves, renames, removals, and tight bounds;
- final emptiness of removal targets;
- non-overlapping extraction sources;
- direct-sibling requirements;
- variant and stateful mappings;
- complete instance replacement;
- unique asset paths;
- expected main-Prefab node count;
- expected child-Prefab and instance-reference counts.

No Prefab, Profile, Replay, or `.meta` file is created during analysis or
preflight.

## Transactional Apply

On confirmation, Unity:

1. refreshes the authoritative snapshot and compares both fingerprints;
2. acquires a target-Prefab write lock;
3. backs up the main Prefab, Profile, Replay, and any existing touched asset and
   `.meta` file into a transaction directory under `Library`;
4. records non-existent output paths so new files can be removed on rollback;
5. creates child Prefabs in ordinal candidate-ID order;
6. replaces source groups with instances and preserves reviewed overrides;
7. saves the main Prefab;
8. reloads all assets and verifies asset links, source removal, geometry, child
   order, and expected counts;
9. updates Profile and Replay only after asset verification succeeds;
10. commits and releases the write lock.

On failure, Unity restores backed-up bytes and `.meta` files, removes all new
assets, refreshes AssetDatabase, and verifies restored hashes. A rollback
verification failure is reported as a high-priority recovery error with the
backup path.

## Run Records

Every run stores a read-only diagnostic package under:

```text
Library/PSDLayoutTool2/VisualRuns/<runId>/
```

The package includes manifests, paired images, raw and validated visual
responses, raw and compiled plans, preflight report, apply report, retry history,
and normalization records.

The tool keeps the latest five successful runs and latest ten failed runs. These
records do not update Replay and do not enter Git.

## Testing

### Unit Tests

- visual schema parsing and exact candidate coverage;
- fingerprint and scope invalidation;
- overlap exclusion and deterministic ordering;
- confidence gates and PSD/Unity conflicts;
- component, variant, and stateful evidence validation;
- multi-extraction path uniqueness;
- retry state transitions and preserved diagnostics.

### Capture Tests

- Sprite, TMP, Mask, material, alpha, inactive nodes, and duplicate names;
- PSD stable-layer mapping and group-bound union;
- nonblank image checks and PNG round trip;
- repeated capture pixel-hash consistency;
- Preview Scene cleanup and unchanged Prefab Stage state;
- Unicode and space-containing file paths.

### CLI Transport Tests

- initial request contains every expected `--image` argument;
- attachment order matches candidate manifests;
- `--output-schema` and `--json` are present;
- same-session correction resumes the session and reattaches the batch images;
- fresh retry clears the session ID and reattaches complete images and context;
- timeout, missing JSON, missing candidate, duplicate candidate, and malformed
  score diagnostics.

### Executor Tests

- one plan creates three non-overlapping child Prefabs;
- the main Prefab references all expected assets;
- no duplicate or empty child Prefab remains;
- direct-sibling and state mappings are enforced;
- every write phase has a fault-injection rollback test;
- Replay changes only after complete asset verification.

### Real AI Smoke And Stability

Use the approved PSD fixture and a locked local selection that has at least three
valid child-Prefab candidates. Run the full visual and planning workflow 20 times
without applying, recording candidate and plan hashes. Apply one reviewed run and
verify the main Prefab, Common directory, instance references, Profile, and Replay.

## Rollout Sequence

1. Replace mock analysis with deterministic paired capture and Codex CLI image
   transport.
2. Add visual JSON Schema, parser, validator, run records, and finite retry state.
3. Compile AI candidate decisions into existing v2 plan operations.
4. Enforce strict local scope and multiple non-overlapping extractions.
5. Add no-write preflight and transactional multi-asset apply verification.
6. Complete unit, capture, transport, rollback, and real-AI stability tests.
