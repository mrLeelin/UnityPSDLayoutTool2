#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Verify a finished child-Prefab extraction against the plan that produced it (read-only).

Checks, per declared extraction:
  * every declared instance exists, keeps its name and its list position
  * variant: the instance has direct `[Common]` then `[States]`, the state branches are present in
    the declared order, and exactly ONE branch is active - the one the plan maps
  * stateful: the instance has direct `[States]` then `[Common]`, the state branches are present in
    the declared order, exactly one is active, and `[Common]` / the active branch carry exactly the
    declared Common and state member names in order
  * state: the instance has a direct `[States]` container with the declared branches and exactly
    one active branch (the plan's `defaultState`)
  * `[Common]` is empty when the plan declares no common members (variant/component shape)
  * component extraction: the instance is a leaf (no children)
  * separately: `verify.hierarchy` / `verify.directChildren` of the plan still hold, and the
    forbidden-name gate still passes

Usage
-----
  python check_extraction_result.py --plan extract.plan.json --snapshot after-apply.(json|txt)
"""
from __future__ import annotations

import argparse
import collections
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from parse_hierarchy_snapshot import load_snapshot  # noqa: E402


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--plan", required=True)
    ap.add_argument("--snapshot", required=True)
    ap.add_argument("--quiet", action="store_true")
    args = ap.parse_args()

    plan = json.load(open(args.plan, encoding="utf-8-sig"))
    _, nodes = load_snapshot(args.snapshot)
    rec = {n["path"]: n for n in nodes}
    kids = collections.defaultdict(list)
    for n in nodes:
        kids[n["path"].rsplit("/", 1)[0] if "/" in n["path"] else ""].append(n["path"])
    for parent in kids:
        kids[parent].sort(key=lambda p: int(rec[p].get("sibling", 0) or 0))
    errors = []

    def children(path):
        return kids.get(path, [])

    def leaf_name(path):
        return rec[path]["name"]

    # ---- extraction contracts -------------------------------------------
    for extract in plan.get("variantComponentExtractions", []):
        want_branches = [s["name"] for s in extract["states"]]
        by_id = {s["id"]: s["name"] for s in extract["states"]}
        for inst in extract["instances"]:
            src = inst["source"]
            if src not in rec:
                errors.append("实例不存在: " + src)
                continue
            names = [leaf_name(c) for c in children(src)]
            if names != ["[Common]", "[States]"]:
                errors.append("变体实例子节点应为 [Common],[States]: %s -> %s" % (src, names))
                continue
            bnames = [leaf_name(c) for c in children(src + "/[States]")]
            if bnames != want_branches:
                errors.append("状态分支顺序不符 %s -> %s" % (src, bnames))
                continue
            active = [leaf_name(c) for c in children(src + "/[States]")
                      if rec[c].get("active") == "True"]
            if len(active) != 1:
                errors.append("应恰好 1 个激活分支 %s -> %s" % (src, active))
            elif active[0] != by_id.get(inst["state"]):
                errors.append("激活分支不符 %s: %s != %s" % (src, active[0], by_id.get(inst["state"])))
            if children(src + "/[Common]"):
                errors.append("[Common] 非空（计划声明无共同成员）: " + src)
    for extract in plan.get("stateComponentExtractions", []):
        want_branches = [s["name"] for s in extract["states"]]
        default_name = next((s["name"] for s in extract["states"]
                             if s["id"] == extract.get("defaultState")), None)
        for state in extract["states"]:
            inst = state["source"]
            if inst not in rec:
                errors.append("实例不存在: " + inst)
                continue
            names = [leaf_name(c) for c in children(inst)]
            if names != ["[States]"]:
                errors.append("state 实例子节点应仅为 [States]: %s -> %s" % (inst, names))
                continue
            bnames = [leaf_name(c) for c in children(inst + "/[States]")]
            if bnames != want_branches:
                errors.append("状态分支顺序不符 %s -> %s" % (inst, bnames))
                continue
            active = [leaf_name(c) for c in children(inst + "/[States]")
                      if rec[c].get("active") == "True"]
            if len(active) != 1:
                errors.append("应恰好 1 个激活分支 %s -> %s" % (inst, active))
            elif default_name and active[0] != default_name:
                errors.append("激活分支不符 %s: %s != %s" % (inst, active[0], default_name))
    for extract in plan.get("statefulComponentExtractions", []):
        want_branches = [s["name"] for s in extract["states"]]
        by_state = {s["id"]: s for s in extract["states"]}
        want_common = [m["name"] for m in extract["common"]["members"]]
        for inst in extract["instances"]:
            src = inst["source"]
            if src not in rec:
                errors.append("实例不存在: " + src)
                continue
            names = [leaf_name(c) for c in children(src)]
            if names != ["[States]", "[Common]"]:
                errors.append("stateful 实例子节点应为 [States],[Common]: %s -> %s" % (src, names))
                continue
            bnames = [leaf_name(c) for c in children(src + "/[States]")]
            if bnames != want_branches:
                errors.append("状态分支顺序不符 %s -> %s" % (src, bnames))
                continue
            active = [leaf_name(c) for c in children(src + "/[States]")
                      if rec[c].get("active") == "True"]
            if len(active) != 1:
                errors.append("应恰好 1 个激活分支 %s -> %s" % (src, active))
                continue
            state = by_state.get(inst.get("state"))
            if state is None:
                errors.append("实例状态未在 states 中声明 %s -> %s" % (src, inst.get("state")))
                continue
            if active[0] != state["name"]:
                errors.append("激活分支不符 %s: %s != %s" % (src, active[0], state["name"]))
            got_common = [leaf_name(c) for c in children(src + "/[Common]")]
            if got_common != want_common:
                errors.append("[Common] 成员不符 %s: %s != %s" % (src, got_common, want_common))
            got_state = [leaf_name(c) for c in children(src + "/[States]/" + state["name"])]
            want_state = [m["name"] for m in state["members"]]
            if got_state != want_state:
                errors.append("状态分支成员不符 %s: %s != %s" % (src, got_state, want_state))
    for extract in plan.get("componentExtractions", []):
        for inst in extract["instances"]:
            if inst not in rec:
                errors.append("实例不存在: " + inst)
            elif children(inst):
                errors.append("component 实例应为叶子: %s -> %s" % (inst, [leaf_name(c) for c in children(inst)]))

    # ---- plan verify contracts still hold --------------------------------
    for h in plan.get("verify", {}).get("hierarchy", []):
        if h["path"] in rec or any(True for _ in [0]):
            if h["path"] not in rec:
                errors.append("verify.hierarchy 路径缺失: " + h["path"])
            elif len(children(h["path"])) != h["childCount"]:
                errors.append("verify.hierarchy %s: %d != %d" % (h["path"], len(children(h["path"])), h["childCount"]))
    for dc in plan.get("verify", {}).get("directChildren", []):
        if dc["path"] not in rec:
            errors.append("verify.directChildren 路径缺失: " + dc["path"])
        else:
            got = [leaf_name(c) for c in children(dc["path"])]
            if got != dc["children"]:
                errors.append("verify.directChildren %s: %s != %s" % (dc["path"], got, dc["children"]))
    root_name = next((n["name"] for n in nodes if "/" not in n["path"]), None)
    for pat in plan.get("verify", {}).get("forbiddenObjectNamePatterns", []):
        rx = re.compile(pat, re.IGNORECASE)
        for path in rec:
            nm = rec[path]["name"]
            if nm == root_name:
                continue
            if rx.search(nm):
                errors.append("禁用命名 %s 命中 %s" % (pat, path))

    if not args.quiet:
        print("checked extractions: %d component / %d state / %d variant / %d stateful" %
              (len(plan.get("componentExtractions", [])), len(plan.get("stateComponentExtractions", [])),
               len(plan.get("variantComponentExtractions", [])),
               len(plan.get("statefulComponentExtractions", []))))
        print("errors: %d" % len(errors))
        for e in errors:
            print("  - " + e)
        if not errors:
            print("extraction result matches the plan (read-only check)")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
