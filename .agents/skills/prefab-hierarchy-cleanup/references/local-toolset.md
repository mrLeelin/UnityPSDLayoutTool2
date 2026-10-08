# Local (offline) toolset — how to get the same answers in one pass

Everything here runs without Unity, against the JSON hierarchy snapshot and version 2
`node:<id>` plans. The scripts are fixture-agnostic: no asset path, object name, canvas
size or expected count is baked in. All angles, sizes and counts are caller inputs or are
derived from the snapshot/plan.

## Why this page exists

A real single-prefab cleanup session spent roughly 16 minutes on evidence + plan + review
and 2 seconds on the actual Unity apply. Most of the 16 minutes went into throw-away
scripts that each had to be debugged from scratch — a hand-rolled compositor, a tree
rebuilder, an order differ, a pixel differ. Those five debugging rounds are the reason
this page exists. Use the scripts below instead of writing a new one.

Rule of thumb: **cheapest check first.** A topological check that answers the question is
worth more than a rendered image that needs five rounds of debugging, and the renderer
also cannot tell you *why* something moved.

## the four tools

### 1. `unity_prefab_yaml.py` — disk ground truth

Reads a text-serialized `.prefab` directly, independent of Unity's snapshot, so the two can
be cross-checked. Use it whenever you need to know what is actually saved.

```bash
python scripts/unity_prefab_yaml.py --prefab <p.prefab> --search-root <project>/Assets \
    --print-tree --max-depth 4 --json-out <dump.json>
```

Produces: `rootName`, `hierarchyNodeCount`, `gameObjectCount`, `prefabInstanceCount`,
`drawableObjectCount`, `scriptObjectCount`, `componentKinds`, `blockCounts`, the ordered
tree, and every PrefabInstance with its resolved source asset path.

Things it handles that hand-rolled parsers get wrong:

- **`stripped` blocks.** `--- !u!224 &<id> stripped` is a PrefabInstance's root transform: no
  `m_GameObject`, just an `m_PrefabInstance` back-reference and an `m_CorrespondingSourceObject`
  pointing into the child prefab. Miss the ` stripped` marker and the instance vanishes from
  the tree (or the node count silently loses four).
- **Escape decoding, done strictly.** `\uXXXX` / `\UXXXXXXXX` / `\xXX` are decoded by hand and
  a malformed escape raises. The tempting `unicode_escape` + latin-1 round-trip silently
  returns the escaped text for CJK, which makes every node look like a different name.
- **Multi-line scalars.** `m_text` can contain real newlines inside a single-quoted scalar, so
  scalars are read with a scanner, not a per-line regex.
- **Component classification.** `UnityEngine.UI.Image` and TMP are MonoBehaviours too, so
  `m_Script` cannot separate them from a gameplay script. `m_EditorClassIdentifier` can.
- **Sibling order.** `m_Children` order *is* the draw order; it is preserved, not re-sorted.

### 2. `check_snapshot_freshness.py` — the gate

`snapshotFingerprint` is the Prefab file SHA-256. Every successful apply rewrites the Prefab,
so every earlier snapshot is dead the moment an apply lands.

```bash
python scripts/check_snapshot_freshness.py --prefab <p.prefab> --snapshot <s.json>   # gate
python scripts/check_snapshot_freshness.py --prefab <p.prefab> --survey              # inventory
```

`--survey` exists because of a real trap: after a two-stage apply the newest snapshot on
disk can be the **intermediate** stage (post-grouping, pre-extraction). Its fingerprint
will not match the final Prefab, and there may be no fresh snapshot at all. Planning against
that file binds `node:<id>` references to a hierarchy that no longer exists. Run `--survey`
after every apply and re-capture inside Unity before authoring the next plan.

### 3. `check_draw_order.py` — cheap, and usually enough

Unity draws a Canvas in **depth-first pre-order of sibling order**. Grouping changes parents,
and that can silently flip two *overlapping* elements even though every RectTransform keeps
its world position. This tool enumerates every overlapping drawable pair and proves no pair
flips.

```bash
# diagnostic: what overlaps at all
python scripts/check_draw_order.py --snapshot <s.json>

# proof: does this plan keep the relative order of everything that overlaps?
python scripts/check_draw_order.py --snapshot <s.json> --plan <plan.json> --json-out <r.json>
```

Exit `0` clean, `1` inverted pair found, `2` inconclusive (see below).

Only the snapshot is needed — no atlas, no font, no Pillow. Run it **before** building any
compositor. If it is clean for a grouping-only plan, the picture cannot have changed at the
overlaps, and a render diff becomes optional supporting evidence instead of the primary gate.

Two details that matter:

- **Inactive subtrees are skipped** (Unity does not draw them), so a hidden branch cannot
  produce phantom overlaps.
- **`--snapshot-after` needs an identity.** Two independent captures assign their own
  `n000…` ids, so comparing them by id correlates nothing and would report a nonsense
  result. Pass `--identity name|path`, or prefer `--plan`, which states where each node went.
  When correlation covers less than 90% of the before-nodes the tool returns `2`
  (inconclusive) rather than pretending to pass.

### 4. `verify_applied_prefab.py` — do not trust the sentinel

`apply-result.json` is the executor's self-report. Read the Prefab that is on disk.

```bash
python scripts/verify_applied_prefab.py \
    --prefab <applied.prefab> --plan <plan.json> --before-snapshot <snap the plan used> \
    --search-root <project>/Assets --json-out <report.json>
```

Checks, all derived from the plan plus the pre-apply snapshot:

1. the Prefab bytes changed (an apply that wrote nothing is not a success)
2. root name is unchanged and is not one of the plan's rename targets
3. no gameplay script components were introduced
4. hierarchy node count equals the simulated post-plan count, **after** subtracting what the
   extraction intents absorb
5. drawable component count equals before-minus-absorbed
6. every wrapper name from the plan is present in the final tree
7. each `postGroupingExtractionIntents[].assetPath` exists on disk, and the PrefabInstances
   referencing it match the intent's instance list, root names and parents

Two gotchas it was built around:

- An extraction intent's `templatePath` is only the **structure donor**. Each entry in
  `instances` absorbs its own subtree, and they can differ (per-instance states). Counting
  only the template under-counts the reduction — this tool failed its own first live run
  because of exactly that.
- The executor's ledger may report `newAssetPaths: []` even when it did create a child
  prefab. Check the filesystem, not the ledger.

## Suggested order in a session

```bash
# before authoring
python scripts/check_snapshot_freshness.py --prefab <p.prefab> --snapshot <snap>
python scripts/parse_hierarchy_snapshot.py --snapshot <snap> --json-out <tree.json>

# author the plan, then
python scripts/validate_plan_locally.py --plan <plan> --snapshot <snap>
python scripts/simulate_and_verify_plan.py --plan <plan> --snapshot <snap>
python scripts/check_draw_order.py --snapshot <snap> --plan <plan>

# approve, apply (Unity), then
python scripts/verify_applied_prefab.py --prefab <p.prefab> --plan <plan> \
    --before-snapshot <snap> --search-root <project>/Assets
python scripts/check_snapshot_freshness.py --prefab <p.prefab> --survey
```

## Regression suite

`Library/PsdHierarchyTerminal/_work/test_tools.py` (written during the 2026-10-08 session)
exercises every tool in both directions — the negative cases matter more than the positive
ones, because a checker that only ever passes is worse than no checker:

| case | expected |
|---|---|
| stale snapshot | exit 1 |
| fresh snapshot | exit 0 |
| `--survey` with nothing fresh | exit 1 |
| executed plan keeps draw order (plan mode) | exit 0 |
| two independent snapshots | exit 2 (inconclusive), never a silent pass |
| synthetically inverted overlapping siblings | exit 1 |
| identical overlay | exit 0 |
| applied Prefab matches the approved plan | exit 0 |
| wrong prefab / stale before-snapshot | exit 1 |

## What is deliberately *not* faked offline

Do not build a home-made rasteriser to satisfy a visual-audit requirement. The sanctioned
pixel comparison is `prefab_visual_audit.py` (`capture-payload` in Unity, then strict RGBA
`compare`), and it is the only thing that can speak about anti-aliasing, font rasterisation,
nine-slice stretching and blend order. `check_draw_order.py` covers the part of that risk
which is provable from geometry alone, cheaply; use it first and escalate deliberately.

## Serialization truths worth re-reading

- Prefab root is rename-locked; a root inside `renames[]` is rejected.
- `verify` accepts only `nodes`, `hierarchy`, `absentPaths`, `directChildren`, `tightBounds`.
- New wrappers are **not** tightened automatically: `sizeDelta = 0` unless the plan says
  otherwise, so every new wrapper needs a `tightBounds` entry.
- Version 2 has no anchor operation array. Anchor conclusions are review + verification
  requirements only; never invent an anchor mutation.
- `containmentResolutions`, `flatSiblingResolutions`, `selectedPrefabExtractions` and
  `crossParentPrefabExtractions` must stay empty arrays.
- Unity writes an ASCII-only name unquoted, a name needing quoting in single quotes, and a
  non-ASCII name in double quotes (`m_Name: "中文"`). Escaped `\uXXXX` forms also occur.
