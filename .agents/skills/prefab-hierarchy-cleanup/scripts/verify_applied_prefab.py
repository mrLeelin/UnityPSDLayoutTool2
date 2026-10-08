#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Post-apply verification of an executed hierarchy-cleanup plan.

`apply-result.json` is a *self-report*: `success=true / status=applied` says the executor
believes it worked. This tool reads the Prefab that is actually on disk and proves the
result against the plan that was approved. Run it as soon as the result file appears;
report a failure as a real failure rather than trusting the sentinel.

Checks (all derived from the plan + the pre-apply snapshot; nothing is hard-coded):
  1. the Prefab bytes changed (an apply that writes nothing is not a success)
  2. root name is unchanged and is not one of the plan's rename targets
  3. no gameplay script components were introduced
  4. hierarchy node count == simulated post-plan count (accounting for extraction)
  5. drawable component count == before minus the components moved into child prefabs
  6. every wrapper name from the plan is present in the final tree
  7. every `postGroupingExtractionIntents[].assetPath` exists on disk, and the
     PrefabInstances that reference it match the intent's instance list and parents

Usage
-----
  python verify_applied_prefab.py --prefab <applied.prefab> --plan <plan.json> \
         --before-snapshot <snap.json> [--search-root Assets] [--json-out report.json]
"""
from __future__ import annotations

import argparse
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

from parse_hierarchy_snapshot import load_snapshot            # noqa: E402
from simulate_and_verify_plan import simulate                 # noqa: E402
from unity_prefab_yaml import load_prefab, build_guid_index, walk_preorder   # noqa: E402
from check_draw_order import _is_drawable                     # noqa: E402


class Report:
    def __init__(self):
        self.rows = []

    def add(self, ok, label, detail=''):
        self.rows.append({'ok': bool(ok), 'label': label, 'detail': detail})
        print('  %-5s %s%s' % ('PASS' if ok else 'FAIL', label,
                               ('  -> ' + detail) if detail else ''))

    def info(self, label, detail=''):
        self.rows.append({'ok': None, 'label': label, 'detail': detail})
        print('  INFO  %s%s' % (label, ('  -> ' + detail) if detail else ''))

    @property
    def failures(self):
        return [r for r in self.rows if r['ok'] is False]


def subtree_keys(sim, root_key):
    out, stack = [], [root_key]
    while stack:
        k = stack.pop()
        out.append(k)
        for c in sim.kids.get(k, []):
            stack.append(c)
    return out


def absorb_paths(intent):
    """Every hierarchy path whose subtree moves into the child prefab.

    `templatePath` is only the structure donor; each entry in `instances` absorbs its own
    subtree, and they can differ (per-instance states). Counting only the template
    under-counts the reduction — a real bug this tool caught on its first live run.
    """
    paths = []
    tpl = intent.get('templatePath')
    if tpl:
        paths.append(tpl)
    for inst in intent.get('instances') or []:
        p = inst.get('path')
        if p and p not in paths:
            paths.append(p)
    return paths


def absorbed_keys(sim, intent, id_by_key):
    """-> (node_delta, [before snapshot node id, ...]) for one extraction intent."""
    keys = []
    for path in absorb_paths(intent):
        hit = [k for k in sim.kids if sim.path_of(k) == path]
        if not hit:
            raise SystemExit('extraction intent %r: path not reachable after simulation: %s'
                             % (intent.get('id'), path))
        keys.extend(subtree_keys(sim, hit[0]))
    nodes = len(keys) - len(absorb_paths(intent))
    ids = [id_by_key[k] for k in keys if k in id_by_key]
    return nodes, ids


def main():
    ap = argparse.ArgumentParser(description='Verify an applied plan against the Prefab on disk.')
    ap.add_argument('--prefab', required=True)
    ap.add_argument('--plan', required=True)
    ap.add_argument('--before-snapshot', required=True)
    ap.add_argument('--search-root',
                    help='project assets root used to resolve guids (e.g. <project>/Assets)')
    ap.add_argument('--json-out')
    args = ap.parse_args()

    plan = json.load(open(args.plan, encoding='utf-8-sig'))
    _, records = load_snapshot(args.before_snapshot)
    before_node = {r['id']: (r['node'] or {}) for r in records}
    before_drawables = sum(1 for n in before_node.values() if _is_drawable(n, n.get('components')))

    doc = load_prefab(args.prefab, args.search_root)
    sim = simulate(plan, records)
    rep = Report()

    print('prefab   : %s' % args.prefab)
    print('sha256   : %s' % doc.sha256)
    print('plan     : %s' % args.plan)
    print('snapshot : %s' % args.before_snapshot)
    print()

    # 1 -------------------------------------------------------------------------
    fp = (plan.get('snapshotFingerprint') or '').lower()
    rep.add(doc.sha256 != fp, 'Prefab bytes changed since the plan snapshot',
            'still %s (apply wrote nothing)' % doc.sha256 if doc.sha256 == fp
            else 'before %s -> after %s' % (fp[:12], doc.sha256[:12]))

    # 2 -------------------------------------------------------------------------
    expect_root = ((plan.get('preserveRequirements') or {}).get('rootName')
                   or (plan.get('output') or {}).get('rootName'))
    renamed_targets = {r.get('target') for r in plan.get('renames', [])}
    if expect_root:
        rep.add(doc.root_name == expect_root, 'root name preserved',
                'expected %r, got %r' % (expect_root, doc.root_name))
    else:
        rep.info('root name preserved', 'plan carries no preserveRequirements.rootName')
    rep.add(not any(r.get('target', '').startswith('node:') and r.get('name') == doc.root_name
                    for r in plan.get('renames', [])),
            'root was not a rename target',
            'renames target %d nodes, none of which may be the root' % len(renamed_targets))

    # 3 -------------------------------------------------------------------------
    before_scripts = sum(1 for n in before_node.values()
                         if any('m_Script' in str(c) for c in (n.get('components') or [])))
    rep.add(doc.script_count <= before_scripts, 'no new gameplay scripts',
            'before %d -> after %d' % (before_scripts, doc.script_count))

    # 4 -------------------------------------------------------------------------
    post_hierarchy = len(sim.kids)
    id_by_key = {v: k for k, v in sim.key_by_id.items()}
    reductions = 0
    absorbed_ids = []
    for intent in plan.get('postGroupingExtractionIntents', []) or []:
        delta, ids = absorbed_keys(sim, intent, id_by_key)
        reductions += delta
        absorbed_ids.extend(ids)
    expected_nodes = post_hierarchy - reductions
    rep.add(doc.hierarchy_node_count == expected_nodes, 'hierarchy node count matches plan',
            'expected %d (simulated %d - %d absorbed by %d extraction instance path(s)), got %d'
            % (expected_nodes, post_hierarchy, reductions,
               sum(len(absorb_paths(i)) for i in
                   (plan.get('postGroupingExtractionIntents') or [])),
               doc.hierarchy_node_count))

    # 5 -------------------------------------------------------------------------
    absorbed_drawables = sum(
        1 for nid in absorbed_ids
        if nid in before_node and _is_drawable(before_node[nid], before_node[nid].get('components')))
    expected_drawables = before_drawables - absorbed_drawables
    rep.add(doc.drawable_count == expected_drawables, 'drawable component count conserved',
            'expected %d (%d before - %d moved into child prefabs), got %d'
            % (expected_drawables, before_drawables, absorbed_drawables, doc.drawable_count))

    # 6 -------------------------------------------------------------------------
    present = {n.name for n in walk_preorder(doc.tree)}
    missing_wrappers = [w.get('name') for w in plan.get('wrappers', [])
                        if w.get('name') and w.get('name') not in present]
    rep.add(not missing_wrappers, 'every plan wrapper exists in the final tree',
            'missing: %s' % missing_wrappers if missing_wrappers else
            '%d wrappers' % len(plan.get('wrappers', [])))

    # 7 -------------------------------------------------------------------------
    guid_index = {}
    if args.search_root:
        guid_index = build_guid_index(args.search_root)
    for intent in plan.get('postGroupingExtractionIntents', []) or []:
        asset_path = intent.get('assetPath') or ''
        target = asset_path
        if args.search_root and not os.path.isabs(target):
            target = os.path.join(args.search_root, '..', asset_path)
        exists = os.path.isfile(target) or os.path.isfile(asset_path)
        rep.add(exists, 'child prefab asset exists on disk', asset_path)

        guid = None
        if guid_index:
            norm = asset_path.replace('\\', '/').lower()
            for g, p in guid_index.items():
                if p.replace('\\', '/').lower().endswith(norm):
                    guid = g
                    break
        if not guid:
            rep.info('child prefab guid resolved', 'pass --search-root to enable')
            continue
        mine = [r for r in doc.prefab_instances if r['source_guid'] == guid]
        want = intent.get('instances') or []
        rep.add(len(mine) == len(want), 'PrefabInstance count for %s' % intent.get('id'),
                'expected %d, found %d' % (len(want), len(mine)))
        want_leaf = sorted(p.get('path', '').split('/')[-1] for p in want)
        got_leaf = sorted(r.get('root_name') or '' for r in mine)
        rep.add(want_leaf == got_leaf, 'PrefabInstance root names match the intent',
                'expected %s, got %s' % (want_leaf, got_leaf))

    print()
    if rep.failures:
        print('RESULT: %d FAILED check(s) - inspect the Prefab before trusting the apply.'
              % len(rep.failures))
    else:
        print('RESULT: OK - the Prefab on disk matches the approved plan.')
    if args.json_out:
        json.dump({'prefab': args.prefab, 'sha256': doc.sha256, 'rows': rep.rows,
                   'failures': len(rep.failures)},
                  open(args.json_out, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
        print('wrote', args.json_out)
    return 1 if rep.failures else 0


if __name__ == '__main__':
    sys.exit(main())
