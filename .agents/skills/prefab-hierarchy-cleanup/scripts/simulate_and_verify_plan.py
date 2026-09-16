#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Offline simulator for a hierarchy-cleanup plan (v1 path plan or v2 node-id plan).

It rebuilds the final tree in memory with the same semantics the renderer uses:
  * wrappers are created in array order, position clamped by SetSiblingIndex semantics
  * moves are applied in array order into the destination wrapper
  * removals are applied last and must be emptied (child-before-parent order is enforced)
  * renames are applied to the final tree
then it proves every `verify.hierarchy`, `verify.directChildren` and `verify.absentPaths`
entry against the simulated tree.

This is the check that caught a real defect in this project: a plan that created wrappers for
items that were *already* containers (task rows) doubled the rows. Run it before preflight.

Usage
-----
  python simulate_and_verify_plan.py --plan plan.json --snapshot snapshot.json|txt [--print-tree]
Exit code 0 = every simulated contract holds.
"""
from __future__ import annotations

import argparse
import collections
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from parse_hierarchy_snapshot import load_snapshot  # noqa: E402


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--plan", required=True)
    ap.add_argument("--snapshot", required=True)
    ap.add_argument("--print-tree", action="store_true")
    ap.add_argument("--max-depth", type=int, default=4)
    args = ap.parse_args()

    plan = json.load(open(args.plan, encoding="utf-8-sig"))
    _, nodes = load_snapshot(args.snapshot)
    by_path = {n["path"]: n for n in nodes}
    by_id = {n["id"]: n["path"] for n in nodes if n.get("id")}

    label, kids, par = {}, collections.OrderedDict(), {}
    for n in nodes:
        label[n["path"]] = n["name"]
        kids[n["path"]] = []
        par[n["path"]] = n["path"].rsplit("/", 1)[0] if "/" in n["path"] else None
    for n in nodes:
        if par[n["path"]]:
            kids[par[n["path"]]].append(n["path"])
    for p in kids:
        kids[p].sort(key=lambda x: int(by_path[x].get("sibling", 0) or 0))
    for n in nodes:
        assert int(by_path[n["path"]].get("children", len(kids[n["path"]])) or 0) == len(kids[n["path"]]), \
            "snapshot childCount mismatch at " + n["path"]

    def resolve(ref):
        if ref.startswith("node:"):
            p = by_id.get(ref[5:])
            if p is None:
                raise SystemExit("unknown node id in plan: " + ref)
            return p
        if ref.startswith("@"):
            return ref
        if ref not in by_path:
            raise SystemExit("unknown path in plan: " + ref)
        return ref

    errors = []
    # 1. wrappers
    for w in plan.get("wrappers", []):
        key = "@" + w["id"]
        parent = resolve(w["parent"])
        if key in kids:
            errors.append("duplicate wrapper id: " + w["id"])
            continue
        label[key] = w.get("name", w["id"])
        kids[key] = []
        par[key] = parent
        idx = min(int(w.get("siblingIndex", 0)), len(kids[parent]))
        kids[parent].insert(idx, key)

    # 2. moves
    for m in plan.get("moves", []):
        src = resolve(m["source"])
        dst = resolve(m["destination"])
        if src not in par:
            errors.append("move source missing: " + src)
            continue
        if dst not in kids:
            errors.append("move destination missing: " + dst)
            continue
        kids[par[src]].remove(src)
        kids[dst].insert(min(int(m.get("siblingIndex", 0)), len(kids[dst])), src)
        par[src] = dst

    # 3. removals (child before parent)
    removals = [resolve(r["source"]) for r in plan.get("emptyContainerRemovals", [])]
    for idx, src in enumerate(removals):
        if kids.get(src):
            errors.append("removal target is not empty in simulation (%d children): %s (%s)"
                          % (len(kids[src]), src, [label[c] for c in kids[src]][:6]))
            continue
        remaining = set(removals[idx + 1:])
        if any(r.startswith(src.rstrip("/") + "/") for r in remaining):
            errors.append("removal order must be child before parent: " + src)
        if par.get(src):
            kids[par[src]].remove(src)
        kids.pop(src, None)
        label.pop(src, None)
        par.pop(src, None)

    # 4. renames
    final = dict(label)
    for r in plan.get("renames", []):
        tgt = resolve(r["target"])
        if tgt in final:
            final[tgt] = r.get("name", final[tgt])

    # 5. contracts
    def path_of(key):
        parts, cur = [], key
        while cur:
            parts.append(final.get(cur, label.get(cur, cur)))
            cur = par.get(cur)
        return "/".join(reversed(parts))

    root = [k for k in kids if par.get(k) is None]
    if len(root) != 1:
        errors.append("simulation produced %d roots: %s" % (len(root), root))
    for h in plan.get("verify", {}).get("hierarchy", []):
        hit = [k for k in kids if path_of(k) == h["path"]]
        if not hit:
            if h["path"] not in {path_of(k) for k in kids}:
                errors.append("verify.hierarchy path not reachable: " + h["path"])
            continue
        if len(kids[hit[0]]) != h["childCount"]:
            errors.append("verify.hierarchy childCount %s: %d != %d"
                          % (h["path"], len(kids[hit[0]]), h["childCount"]))
    for dc in plan.get("verify", {}).get("directChildren", []):
        hit = [k for k in kids if path_of(k) == dc["path"]]
        if not hit:
            errors.append("verify.directChildren path not reachable: " + dc["path"])
            continue
        got = [final[c] for c in kids[hit[0]]]
        if got != dc["children"]:
            errors.append("verify.directChildren %s\n      got=%s\n      want=%s"
                          % (dc["path"], got, dc["children"]))
    for a in plan.get("verify", {}).get("absentPaths", []):
        if any(path_of(k) == a for k in kids):
            errors.append("verify.absentPaths still present: " + a)

    print("simulated nodes: %d (snapshot %d)" % (len(kids), len(nodes)))
    if args.print_tree:
        def dump(k, d=0):
            if d > args.max_depth:
                return
            print("  " * d + final[k] + " [%d]" % len(kids[k]))
            for c in kids[k]:
                dump(c, d + 1)
        dump(root[0])
    print("contract errors: %d" % len(errors))
    for e in errors:
        print("  - " + e)
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
