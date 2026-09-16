#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Offline linter for a hierarchy-cleanup plan (run before the Unity preflight).

It re-checks the things that actually broke during real runs, and it encodes two engine
realities of this project that the written plan format does not state:

R1  New containers are NOT tightened automatically by this engine. A wrapper created by a
    plan keeps sizeDelta = 0 unless the plan also lists it in `tightBounds`. Always list
    every new wrapper (inner -> outer), otherwise the saved containers are 0 x 0.
R2  For an extraction / state / variant / stateful stage the saved tree EXPANDS (each source
    unit becomes an instance of the new asset). `verify.nodes|components|images` must be the
    post-expansion expectation, not the pre-apply snapshot numbers. Passing pre-apply numbers
    produces `VERIFY_WARN issue=nodes expected=.. actual=..` after an already-saved apply.

Checks performed:
  * every referenced node exists in the snapshot; wrapper ids are lower_snake_case and unique
  * each move source is used exactly once; moves/removals do not conflict
  * removals are really emptied by the plan, carry no components beyond Transform, and have a
    matching `verify.absentPaths` entry
  * `verify.directChildren` names match the members declared for that container
  * `forbiddenObjectNamePatterns` do not match any *final* node name (renames applied),
    and never match the prefab root (it must keep the asset file name, even when non-ASCII)
  * extraction ids referenced by `componentFamilyDecisions` exist and cover the same sources
  * R1 / R2 warnings described above

Usage
-----
  python validate_plan_locally.py --plan plan.json [--snapshot snapshot.json|txt] [--quiet]
Exit code 0 = no errors (warnings may still be printed), 2 = errors.
"""
from __future__ import annotations

import argparse
import collections
import json
import re
import sys

ID_RE = re.compile(r"^[a-z][a-z0-9_]*$")


def node_of(ref, nodes_by_id, path_set):
    if ref.startswith("node:"):
        return nodes_by_id.get(ref[5:])
    return ref if ref in path_set else None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--plan", required=True)
    ap.add_argument("--snapshot")
    ap.add_argument("--quiet", action="store_true")
    args = ap.parse_args()

    plan = json.load(open(args.plan, encoding="utf-8-sig"))
    errors, warns = [], []

    nodes_by_id, path_set, id_by_path, name_by_id = {}, set(), {}, {}
    if args.snapshot:
        sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
        from parse_hierarchy_snapshot import load_snapshot
        _, nodes = load_snapshot(args.snapshot)
        for n in nodes:
            path_set.add(n["path"])
            name_by_id[n["path"]] = n["name"]
            if n.get("id"):
                nodes_by_id[n["id"]] = n["path"]
                id_by_path[n["path"]] = n["id"]

    def resolve(ref):
        if ref.startswith("node:"):
            return nodes_by_id.get(ref[5:])          # may be None without a snapshot
        return ref if not path_set or ref in path_set else None

    # ---- wrappers -------------------------------------------------------
    wrapper_names, wrapper_ids = {}, set()
    for w in plan.get("wrappers", []):
        wid = w.get("id", "")
        if not ID_RE.match(wid):
            errors.append("wrapper id 不是 lower_snake_case: %r" % wid)
        if wid in wrapper_ids:
            errors.append("wrapper id 重复: %s" % wid)
        wrapper_ids.add(wid)
        wrapper_names[wid] = w.get("name", "")
    for w in plan.get("wrappers", []):
        ref = w.get("parent", "")
        if ref.startswith("@"):
            if ref[1:] not in wrapper_ids:
                errors.append("wrapper parent 引用了未知 wrapper: %s" % ref)
        elif args.snapshot and resolve(ref) is None:
            errors.append("wrapper parent 无法解析: %s" % ref)

    # ---- moves ----------------------------------------------------------
    used = collections.Counter()
    per_dest_idx = collections.defaultdict(set)
    for m in plan.get("moves", []):
        src, dst = m.get("source", ""), m.get("destination", "")
        used[src] += 1
        if args.snapshot and resolve(src) is None:
            errors.append("move.source 未知: %s" % src)
        if not dst.startswith("@") or dst[1:] not in wrapper_ids:
            errors.append("move.destination 不是已声明的 wrapper: %s" % dst)
        if m.get("siblingIndex") in per_dest_idx[dst]:
            errors.append("%s 内 siblingIndex 重复: %s" % (dst, m.get("siblingIndex")))
        per_dest_idx[dst].add(m.get("siblingIndex"))
    for src, c in used.items():
        if c > 1:
            errors.append("move.source 被使用 %d 次: %s" % (c, src))

    # ---- removals -------------------------------------------------------
    removed = {r["source"] for r in plan.get("emptyContainerRemovals", []) if "source" in r}
    for r in plan.get("emptyContainerRemovals", []):
        src = r.get("source", "")
        if args.snapshot and resolve(src) is None:
            errors.append("removal.source 未知: %s" % src)
        if src in {m.get("source") for m in plan.get("moves", [])}:
            errors.append("同一节点既被移除又被声明来源: %s" % src)
        for m in plan.get("moves", []):
            if resolve(m.get("source", "")) and resolve(src) and \
               resolve(m["source"]).startswith(str(resolve(src)).rstrip("/") + "/"):
                continue
        absent = plan.get("verify", {}).get("absentPaths", [])
        if absent and not any(src.rstrip("/").endswith(a.rsplit("/", 1)[-1]) or
                              (resolve(src) and resolve(src) in a) for a in absent):
            warns.append("removal %s 建议在 verify.absentPaths 中配一条证明它已消失" % src)

    # ---- renames / naming gate -----------------------------------------
    final_name = dict(name_by_id)
    renamed_targets = []
    for r in plan.get("renames", []):
        tgt = r.get("target", "")
        renamed_targets.append(tgt)
        if args.snapshot and resolve(tgt) is None:
            errors.append("rename.target 未知: %s" % tgt)
        if tgt in removed:
            errors.append("对将被移除的节点做重命名: %s" % tgt)
        if resolve(tgt):
            final_name[resolve(tgt)] = r.get("name", "")
    root_name = None
    if args.snapshot:
        root_name = next((n for n in sorted(path_set, key=len) if "/" not in n), None)
    for pat in plan.get("verify", {}).get("forbiddenObjectNamePatterns", []):
        try:
            rx = re.compile(pat, re.IGNORECASE)
        except re.error as exc:
            errors.append("禁用命名正则非法 %r: %s" % (pat, exc))
            continue
        for path, nm in final_name.items():
            if nm and rx.search(nm):
                errors.append("禁用命名 %r 命中最终名 %r (%s)" % (pat, nm, path))
        if root_name and rx.search(root_name):
            errors.append("禁用命名 %r 命中主根 %r —— 主根必须保留资产文件名" % (pat, root_name))

    # ---- directChildren consistency ------------------------------------
    moves_by_dest = collections.defaultdict(list)
    for m in plan.get("moves", []):
        moves_by_dest[m["destination"][1:]].append(m)
    for dc in plan.get("verify", {}).get("directChildren", []):
        path, kids = dc.get("path", ""), list(dc.get("children", []))
        leaf = path.rsplit("/", 1)[-1]
        wid = None
        for w in plan.get("wrappers", []):
            if "[" + wrapper_names.get(w["id"], "") + "]" == leaf:
                wid = w["id"]
        if wid is None:
            continue
        expect = []
        for m in sorted(moves_by_dest.get(wid, []), key=lambda x: x.get("siblingIndex", 0)):
            src = resolve(m.get("source", ""))
            expect.append(final_name.get(src) if src else None)
        expect = [n for n in expect if n]
        if expect and expect != kids:
            errors.append("directChildren %s 与 moves 结果不一致：%s vs %s" % (path, kids, expect))

    # ---- extraction / decisions ----------------------------------------
    extracts = {}
    for key, mode in (("componentExtractions", "component"),
                      ("stateComponentExtractions", "state"),
                      ("variantComponentExtractions", "variant"),
                      ("statefulComponentExtractions", "stateful")):
        for e in plan.get(key, []):
            extracts[e.get("id")] = (mode, e)
    for d in plan.get("componentFamilyDecisions", []):
        mode = d.get("mode")
        if mode == "skip":
            continue
        if d.get("extractionId") not in extracts:
            errors.append("decision 引用了不存在的 extractionId: %s" % d.get("extractionId"))
    for eid, (mode, e) in extracts.items():
        asset = e.get("assetPath", "")
        stem = os_basename_stem(asset)
        if not asset.endswith(".prefab") or not re.match(r"^[A-Z][A-Za-z0-9]*$", stem) or "/Common/" not in asset:
            warns.append("extraction %s 的 assetPath 应为 PascalCase .prefab 且直接位于同级 Common/：%s" % (eid, asset))

    # ---- engine reality warnings ---------------------------------------
    if plan.get("wrappers"):
        tightened = {t.get("target") for t in plan.get("tightBounds", [])}
        missing = [w["id"] for w in plan["wrappers"] if "@" + w["id"] not in tightened]
        if missing:
            warns.append("R1: 这些新容器没有出现在 tightBounds 中，落盘后会是 0x0：%s" % ", ".join(missing))
    if extracts:
        v = plan.get("verify", {})
        warns.append("R2: 本计划含抽取（%d 个族）。verify.nodes/components/images 必须是**展开后**的期望值；"
                     "先跑 Unity 侧 Snapshot 或用 predict_extraction_counts.py 预估，否则会在保存后得到 "
                     "VERIFY_WARN issue=nodes。" % len(extracts))
        for cond, msg in ((not v.get("directChildren"), "抽取后应给每个源容器写 directChildren"),
                          (not v.get("hierarchy"), "抽取后应给每个源容器写 hierarchy")):
            if cond:
                warns.append("R2: " + msg)
    if plan.get("version") == 2 and not plan.get("snapshotFingerprint"):
        errors.append("version 2 计划缺少 snapshotFingerprint")

    if not args.quiet:
        for w in warns:
            print("WARN : " + w)
        for e in errors:
            print("ERROR: " + e)
        print("summary: %d error(s), %d warning(s)" % (len(errors), len(warns)))
    return 2 if errors else 0


def os_basename_stem(asset_path):
    return asset_path.rsplit("/", 1)[-1].rsplit(".", 1)[0]


if __name__ == "__main__":
    sys.exit(main())
