#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Draw-order preservation check - cheap topology, no rendering.

Unity draws a Canvas in depth-first pre-order of sibling order (a parent before its
children, siblings in order). A grouping plan that changes parents can silently flip two
*overlapping* elements, which changes the rendered picture even though every
RectTransform keeps its world position.

This tool enumerates every pair of overlapping drawable nodes and proves their relative
order is unchanged. It needs only the hierarchy snapshot (worldRect + tree order); no
atlas, no font, no renderer, no Pillow. Reach for it BEFORE building any offline renderer:
if it reports 0 inversions for a grouping-only plan, the picture cannot have changed at
those overlaps, and a render diff becomes optional evidence rather than the primary gate.

Usage
-----
  # overlap report only (diagnostic)
  python check_draw_order.py --snapshot snap.json

  # prove a plan keeps relative order: simulate it, compare before/after
  python check_draw_order.py --snapshot snap.json --plan plan.json
  python check_draw_order.py --snapshot snap.json --snapshot-after after.json

Exit code 0 = no inverted overlapping pair.
"""
from __future__ import annotations

import argparse
import collections
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

from parse_hierarchy_snapshot import load_snapshot  # noqa: E402

DRAWABLE_COMPONENT_HINTS = ('Image', 'RawImage', 'TextMeshProUGUI', 'TMP_Text', 'Text',
                            'SpriteRenderer')


# --------------------------------------------------------------------------------------
# snapshot model
# --------------------------------------------------------------------------------------


class Overlay:
    """Ordered drawable overlay extracted from a hierarchy snapshot."""

    def __init__(self, name):
        self.name = name
        self.order = []          # drawable identity (snapshot node id) in draw order
        self.rect = {}           # identity -> (x0, y0, x1, y1)
        self.label = {}          # identity -> path
        self.skipped_inactive = 0
        self.total_nodes = 0

    def overlaps(self, tol=0.0):
        """All pairs of drawables whose world rects intersect with positive area."""
        items = [(k, self.rect[k]) for k in self.order if k in self.rect]
        out = []
        for i in range(len(items)):
            ka, ra = items[i]
            for j in range(i + 1, len(items)):
                kb, rb = items[j]
                ox = min(ra[2], rb[2]) - max(ra[0], rb[0])
                oy = min(ra[3], rb[3]) - max(ra[1], rb[1])
                if ox > tol and oy > tol:
                    area = ox * oy
                    smaller = min((ra[2] - ra[0]) * (ra[3] - ra[1]),
                                  (rb[2] - rb[0]) * (rb[3] - rb[1]))
                    out.append((ka, kb, ox, oy, area / smaller if smaller > 0 else 0.0))
        return out


def _is_drawable(node, components):
    comps = components or node.get('components') or []
    for c in comps:
        text = c if isinstance(c, str) else json.dumps(c, ensure_ascii=False)
        if any(h in text for h in DRAWABLE_COMPONENT_HINTS):
            return True
    return bool(node.get('sprite') or node.get('spriteAssetPath') or node.get('fontAssetPath'))


def overlay_from_snapshot(path, name=None):
    _, records = load_snapshot(path)
    by_id = {r['id']: r for r in records}
    children = collections.defaultdict(list)
    roots = []
    for r in records:
        pid = (r['node'] or {}).get('parentId')
        if pid and pid in by_id:
            children[pid].append(r['id'])
        else:
            roots.append(r['id'])

    def sibling_key(nid):
        try:
            return int((by_id[nid]['node'] or {}).get('siblingIndex', 0))
        except (TypeError, ValueError):
            return 0

    for pid in children:
        children[pid].sort(key=sibling_key)
    if not roots:
        raise SystemExit('snapshot has no root node: ' + path)

    ov = Overlay(name or os.path.basename(path))
    ov.total_nodes = len(records)

    stack = [(roots[0], True)]
    order = []
    while stack:
        nid, parent_visible = stack.pop()
        rec = by_id[nid]
        node = rec['node'] or {}
        visible = parent_visible and bool(node.get('active', True))
        if visible and _is_drawable(node, node.get('components')):
            wr = node.get('worldRect')
            if isinstance(wr, (list, tuple)) and len(wr) == 4:
                order.append(nid)
                ov.rect[nid] = (float(wr[0]), float(wr[1]), float(wr[2]), float(wr[3]))
                ov.label[nid] = rec['path']
        elif not visible and _is_drawable(node, node.get('components')):
            ov.skipped_inactive += 1
        for c in reversed(children.get(nid, [])):
            stack.append((c, visible))

    ov.order = order
    return ov


def overlay_from_simulation(snapshot_path, plan_path, name='after'):
    """Apply the plan in memory and return the draw order it would produce.

    Grouping cannot move geometry, so every original node keeps the worldRect it had in
    the snapshot; only the traversal order (and therefore the draw order) can change.
    """
    from simulate_and_verify_plan import simulate

    plan = json.load(open(plan_path, encoding='utf-8-sig'))
    _, records = load_snapshot(snapshot_path)
    by_id = {r['id']: r for r in records}
    sim = simulate(plan, records)
    if sim.errors:
        raise SystemExit('plan simulation failed:\n  ' + '\n  '.join(sim.errors))

    ov = Overlay(name)
    ov.total_nodes = len(sim.kids)
    roots = [k for k in sim.kids if sim.par.get(k) is None]
    if len(roots) != 1:
        raise SystemExit('simulation produced %d roots' % len(roots))

    id_by_key = {}
    for nid, key in sim.key_by_id.items():
        id_by_key[key] = nid

    stack = [(roots[0], True)]
    while stack:
        key, _ = stack.pop()
        nid = id_by_key.get(key)
        if nid and nid in by_id:
            node = by_id[nid]['node'] or {}
            if _is_drawable(node, node.get('components')):
                wr = node.get('worldRect')
                if isinstance(wr, (list, tuple)) and len(wr) == 4:
                    ov.order.append(nid)
                    ov.rect[nid] = (float(wr[0]), float(wr[1]), float(wr[2]), float(wr[3]))
                    ov.label[nid] = sim.path_of(key)
        for c in reversed(sim.kids.get(key, [])):
            stack.append((c, True))
    return ov


# --------------------------------------------------------------------------------------
# comparison
# --------------------------------------------------------------------------------------


IDENTITY_KEYS = {
    # identity used to correlate a node across two overlays
    'id': lambda ov, nid, label: nid,
    'path': lambda ov, nid, label: ov.label.get(nid),
    'name': lambda ov, nid, label: (label or '').rsplit('/', 1)[-1],
}


def correlate(before: Overlay, after: Overlay, how: str):
    """Build a before->after key map. Two independent snapshots do NOT share node ids
    (each capture assigns its own), so comparing them by id silently matches nothing.
    Refuse loudly instead of reporting a bogus result."""
    keyf = IDENTITY_KEYS[how]
    a_index = {}
    for i, nid in enumerate(after.order):
        k = keyf(after, nid, after.label.get(nid))
        a_index.setdefault(k, i)
    mapping = {}
    for nid in before.order:
        k = keyf(before, nid, before.label.get(nid))
        if k in a_index:
            mapping[nid] = (k, a_index[k])
    return mapping


def compare(before: Overlay, after: Overlay, tol, min_ratio, identity='id'):
    pairs = before.overlaps(tol)
    before_index = {k: i for i, k in enumerate(before.order)}
    mapping = correlate(before, after, identity)
    inverted, unpaired = [], []
    for ka, kb, ox, oy, ratio in pairs:
        if ratio < min_ratio:
            continue
        if ka not in mapping or kb not in mapping:
            unpaired.append((ka, kb))
            continue
        was = before_index[ka] < before_index[kb]
        now = mapping[ka][1] < mapping[kb][1]
        if was != now:
            inverted.append({
                'a': before.label.get(ka, ka), 'b': before.label.get(kb, kb),
                'beforeOrder': 'a-before-b' if was else 'b-before-a',
                'afterOrder': 'a-before-b' if now else 'b-before-a',
                'overlapW': round(ox, 2), 'overlapH': round(oy, 2),
                'overlapRatio': round(ratio, 4),
            })
    return pairs, inverted, unpaired, mapping


def main():
    ap = argparse.ArgumentParser(description='Prove a plan does not invert overlapping draws.')
    ap.add_argument('--snapshot', required=True)
    ap.add_argument('--plan', help='simulate this v2 plan to get the post-grouping order')
    ap.add_argument('--snapshot-after',
                    help='use an already-captured post-plan snapshot. NOTE: two independent '
                         'captures do not share node ids; use --identity to correlate.')
    ap.add_argument('--identity', choices=sorted(IDENTITY_KEYS), default='id',
                    help='how to correlate a node between the two overlays (default id)')
    ap.add_argument('--tolerance', type=float, default=0.0,
                    help='worldRect overlap tolerance (default 0 = any positive overlap)')
    ap.add_argument('--min-overlap-ratio', type=float, default=0.0,
                    help='ignore pairs whose overlap covers less than this fraction of the '
                         'smaller rect (e.g. 0.001 to skip 1px touches)')
    ap.add_argument('--json-out')
    ap.add_argument('--top', type=int, default=10)
    args = ap.parse_args()

    before = overlay_from_snapshot(args.snapshot, 'before')
    print('before : %d drawable nodes, %d skipped (inactive), %d snapshot nodes'
          % (len(before.order), before.skipped_inactive, before.total_nodes))

    if args.plan and args.snapshot_after:
        raise SystemExit('use --plan or --snapshot-after, not both')

    if args.plan:
        after = overlay_from_simulation(args.snapshot, args.plan)
        # the simulation carries snapshot node ids through, so identity stays 'id'
        args.identity = 'id'
    elif args.snapshot_after:
        after = overlay_from_snapshot(args.snapshot_after, 'after')
    else:
        after = None

    if after is None:
        pairs = before.overlaps(args.tolerance)
        ranked = sorted(pairs, key=lambda p: -p[4])
        print('overlapping drawable pairs: %d' % len(pairs))
        for ka, kb, ox, oy, ratio in ranked[:args.top]:
            print('  %5.1fx%-5.1f ratio=%-6.3f  %s  <  %s'
                  % (ox, oy, ratio, before.label.get(ka, ka), before.label.get(kb, kb)))
        if args.json_out:
            json.dump({'pairs': len(pairs), 'beforeNodes': len(before.order)},
                      open(args.json_out, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
        return 0

    print('after  : %d drawable nodes, %d snapshot nodes'
          % (len(after.order), after.total_nodes))
    pairs, inverted, unpaired, mapping = compare(before, after, args.tolerance,
                                                 args.min_overlap_ratio, args.identity)
    matched = len(mapping)
    print('identity used                         : %s (%d of %d before-nodes correlated)'
          % (args.identity, matched, len(before.order)))
    print()
    print('overlapping drawable pairs considered : %d' % len(pairs))
    print('pairs that could not be correlated    : %d' % len(unpaired))
    print('INVERTED ORDER PAIRS                  : %d' % len(inverted))
    for item in inverted[:args.top]:
        print('  ! %s\n      before: %s\n      after : %s\n      overlap %sx%s (%.4f of smaller)'
              % (item['a'] + '  vs  ' + item['b'], item['beforeOrder'], item['afterOrder'],
                 item['overlapW'], item['overlapH'], item['overlapRatio']))

    if args.json_out:
        json.dump({'pairs': len(pairs), 'inverted': inverted, 'unpaired': len(unpaired),
                   'identity': args.identity, 'correlated': matched,
                   'beforeDrawables': len(before.order), 'afterDrawables': len(after.order)},
                  open(args.json_out, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
        print('wrote', args.json_out)

    print()
    if inverted:
        print('RESULT: FAIL - relative draw order of overlapping elements changed.')
        return 1
    if not args.plan and matched < 0.9 * len(before.order):
        print('RESULT: INCONCLUSIVE - only %d/%d before-nodes could be correlated with the '
              'after-overlay.' % (matched, len(before.order)))
        print('        Two independent snapshots assign their own node ids; correlate with '
              '--identity name|path, or use --plan so the plan itself says where each node went.')
        return 2
    print('RESULT: OK - every overlapping pair keeps its relative draw order.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
