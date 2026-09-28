#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Offline simulator for a version 2 hierarchy-cleanup plan (node:<id> references).

It rebuilds the final tree in memory with the same semantics the Unity executor uses:
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
  python simulate_and_verify_plan.py --plan plan.json --snapshot snapshot.json [--print-tree]
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


class Simulation:
    """Final tree after grouping. Keys are snapshot paths (existing nodes) or @wrapperId."""

    def __init__(self):
        self.label = {}
        self.final = {}
        self.kids = collections.OrderedDict()
        self.par = {}
        self.key_by_id = {}
        self.errors = []

    def path_of(self, key):
        parts, cur = [], key
        while cur:
            parts.append(self.final.get(cur, self.label.get(cur, cur)))
            cur = self.par.get(cur)
        return "/".join(reversed(parts))

    def final_path_of_node(self, ref):
        """Post-grouping path of an existing node referenced as node:<id> (None if removed)."""
        key = self.key_by_id.get(ref[5:]) if ref.startswith("node:") else None
        return self.path_of(key) if key in self.kids else None


def require_v2(plan):
    if plan.get("version") != 2:
        raise SystemExit("plan version must be 2 (node:<id> references), got %r" % plan.get("version"))


def simulate(plan, nodes):
    require_v2(plan)
    sim = Simulation()
    by_path = {n["path"]: n for n in nodes}
    for n in nodes:
        sim.label[n["path"]] = n["name"]
        sim.kids[n["path"]] = []
        sim.par[n["path"]] = n["path"].rsplit("/", 1)[0] if "/" in n["path"] else None
        sim.key_by_id[n["id"]] = n["path"]
    for n in nodes:
        if sim.par[n["path"]]:
            sim.kids[sim.par[n["path"]]].append(n["path"])
    for p in sim.kids:
        sim.kids[p].sort(key=lambda x: int((by_path[x].get("node") or {}).get("siblingIndex", 0) or 0))
    for n in nodes:
        expected = (n.get("node") or {}).get("childCount")
        if expected is not None and int(expected) != len(sim.kids[n["path"]]):
            raise SystemExit("snapshot childCount mismatch at " + n["path"])

    def resolve(ref):
        if ref.startswith("node:"):
            p = sim.key_by_id.get(ref[5:])
            if p is None:
                raise SystemExit("unknown node id in plan: " + ref)
            return p
        if ref.startswith("@"):
            return ref
        raise SystemExit("plan reference must be node:<id> or @wrapperId, not a hierarchy path: " + ref)

    # 1. wrappers
    for w in plan.get("wrappers", []):
        key = "@" + w["id"]
        parent = resolve(w["parent"])
        if key in sim.kids:
            sim.errors.append("duplicate wrapper id: " + w["id"])
            continue
        sim.label[key] = w.get("name", w["id"])
        sim.kids[key] = []
        sim.par[key] = parent
        idx = min(int(w.get("siblingIndex", 0)), len(sim.kids[parent]))
        sim.kids[parent].insert(idx, key)

    # 2. moves
    for m in plan.get("moves", []):
        src = resolve(m["source"])
        dst = resolve(m["destination"])
        if src not in sim.par:
            sim.errors.append("move source missing: " + src)
            continue
        if dst not in sim.kids:
            sim.errors.append("move destination missing: " + dst)
            continue
        sim.kids[sim.par[src]].remove(src)
        sim.kids[dst].insert(min(int(m.get("siblingIndex", 0)), len(sim.kids[dst])), src)
        sim.par[src] = dst

    # 3. removals (child before parent)
    removals = [resolve(r["source"]) for r in plan.get("emptyContainerRemovals", [])]
    for idx, src in enumerate(removals):
        if sim.kids.get(src):
            sim.errors.append("removal target is not empty in simulation (%d children): %s (%s)"
                              % (len(sim.kids[src]), src, [sim.label[c] for c in sim.kids[src]][:6]))
            continue
        remaining = set(removals[idx + 1:])
        if any(r.startswith(src.rstrip("/") + "/") for r in remaining):
            sim.errors.append("removal order must be child before parent: " + src)
        if sim.par.get(src):
            sim.kids[sim.par[src]].remove(src)
        sim.kids.pop(src, None)
        sim.label.pop(src, None)
        sim.par.pop(src, None)

    # 4. renames
    sim.final = dict(sim.label)
    for r in plan.get("renames", []):
        tgt = resolve(r["target"])
        if tgt in sim.final:
            sim.final[tgt] = r.get("name", sim.final[tgt])
    return sim


def verify_contracts(plan, sim):
    errors = list(sim.errors)
    root = [k for k in sim.kids if sim.par.get(k) is None]
    if len(root) != 1:
        errors.append("simulation produced %d roots: %s" % (len(root), root))
    paths = {k: sim.path_of(k) for k in sim.kids}
    for h in plan.get("verify", {}).get("hierarchy", []):
        hit = [k for k, p in paths.items() if p == h["path"]]
        if not hit:
            errors.append("verify.hierarchy path not reachable: " + h["path"])
            continue
        if len(sim.kids[hit[0]]) != h["childCount"]:
            errors.append("verify.hierarchy childCount %s: %d != %d"
                          % (h["path"], len(sim.kids[hit[0]]), h["childCount"]))
    for dc in plan.get("verify", {}).get("directChildren", []):
        hit = [k for k, p in paths.items() if p == dc["path"]]
        if not hit:
            errors.append("verify.directChildren path not reachable: " + dc["path"])
            continue
        got = [sim.final[c] for c in sim.kids[hit[0]]]
        if got != dc["children"]:
            errors.append("verify.directChildren %s\n      got=%s\n      want=%s"
                          % (dc["path"], got, dc["children"]))
    for a in plan.get("verify", {}).get("absentPaths", []):
        if a in paths.values():
            errors.append("verify.absentPaths still present: " + a)
    return errors, root


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--plan", required=True)
    ap.add_argument("--snapshot", required=True)
    ap.add_argument("--print-tree", action="store_true")
    ap.add_argument("--max-depth", type=int, default=4)
    args = ap.parse_args()

    plan = json.load(open(args.plan, encoding="utf-8-sig"))
    _, nodes = load_snapshot(args.snapshot)
    sim = simulate(plan, nodes)
    errors, root = verify_contracts(plan, sim)

    print("simulated nodes: %d (snapshot %d)" % (len(sim.kids), len(nodes)))
    if args.print_tree and root:
        def dump(k, d=0):
            if d > args.max_depth:
                return
            print("  " * d + sim.final[k] + " [%d]" % len(sim.kids[k]))
            for c in sim.kids[k]:
                dump(c, d + 1)
        dump(root[0])
    print("contract errors: %d" % len(errors))
    for e in errors:
        print("  - " + e)
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
