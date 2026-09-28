#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Offline linter for a version 2 hierarchy-cleanup plan (run before the Unity preflight).

It re-checks the things that actually broke during real runs, and it encodes two engine
realities of this project that the written plan format does not state:

R1  New containers are NOT tightened automatically by this engine. A wrapper created by a
    plan keeps sizeDelta = 0 unless the plan also lists it in `tightBounds`. Always list
    every new wrapper (inner -> outer), otherwise the saved containers are 0 x 0.
R2  For an extraction / state / variant / stateful stage the saved tree EXPANDS (each source
    unit becomes an instance of the new asset). `verify.nodes` must be the post-expansion
    expectation, not the pre-apply snapshot number. Passing the pre-apply number produces
    `VERIFY_WARN issue=nodes expected=.. actual=..` after an already-saved apply.

Checks performed:
  * the plan is version 2 and carries a snapshotFingerprint
  * every node reference is `node:<id>` (or `@wrapperId` where a wrapper is allowed) and, with a
    snapshot, exists in it; raw hierarchy paths are rejected
  * wrapper ids are lower_snake_case and unique
  * each move source is used exactly once; moves/removals do not conflict
  * `verify` only uses the keys the Unity executor accepts
  * no rename targets the prefab root with a name other than the asset file name
  * every Texture `toName` starts with `prefabName_` and every SpriteAtlas `toName` equals `prefabName`
  * `verify.directChildren` names match the members declared for that container
  * extraction ids referenced by `componentFamilyDecisions` exist
  * R1 / R2 warnings described above

Usage
-----
  python validate_plan_locally.py --plan plan.json [--snapshot snapshot.json] [--quiet]
Exit code 0 = no errors (warnings may still be printed), 2 = errors.

For a validated draft that must be published to a separate contract path, use
`publish_validated_plan.py`. It runs this linter and the simulator in order, then
copies the unchanged bytes and verifies their SHA-256 hashes.
"""
from __future__ import annotations

import argparse
import collections
import json
import os
import re
import sys

ID_RE = re.compile(r"^[a-z][a-z0-9_]*$")
SUPPORTED_VERIFY_KEYS = ("nodes", "hierarchy", "absentPaths", "directChildren", "tightBounds")


def os_basename_stem(asset_path):
    return asset_path.rsplit("/", 1)[-1].rsplit(".", 1)[0]


def lint(plan, nodes=None):
    """Return (errors, warnings) for a plan; `nodes` is the parsed snapshot (optional)."""
    errors, warns = [], []
    if plan.get("version") != 2:
        errors.append("计划 version 必须为 2（节点引用使用 node:<id>），当前为 %r" % plan.get("version"))
        return errors, warns
    if not plan.get("snapshotFingerprint"):
        errors.append("version 2 计划缺少 snapshotFingerprint")

    has_snapshot = nodes is not None
    path_by_id, name_by_path = {}, {}
    for n in nodes or []:
        path_by_id[n["id"]] = n["path"]
        name_by_path[n["path"]] = n["name"]

    def resolve(ref, label, allow_wrapper=False):
        """Snapshot path for node:<id>, the ref itself for @wrapper, else None (error recorded)."""
        ref = ref or ""
        if ref.startswith("@"):
            if not allow_wrapper:
                errors.append("%s 不能引用 wrapper: %s" % (label, ref))
                return None
            return ref
        if not ref.startswith("node:"):
            errors.append("%s 必须使用 node:<id>，不能写层级路径: %s" % (label, ref))
            return None
        if has_snapshot and ref[5:] not in path_by_id:
            errors.append("%s 引用的节点不在快照中: %s" % (label, ref))
            return None
        return path_by_id.get(ref[5:], ref)

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
        if ref.startswith("@") and ref[1:] not in wrapper_ids:
            errors.append("wrapper parent 引用了未知 wrapper: %s" % ref)
        else:
            resolve(ref, "wrapper.parent", allow_wrapper=True)

    # ---- moves ----------------------------------------------------------
    used = collections.Counter()
    per_dest_idx = collections.defaultdict(set)
    for m in plan.get("moves", []):
        src, dst = m.get("source", ""), m.get("destination", "")
        used[src] += 1
        resolve(src, "move.source")
        if not dst.startswith("@") or dst[1:] not in wrapper_ids:
            errors.append("move.destination 不是已声明的 wrapper: %s" % dst)
        if m.get("siblingIndex") in per_dest_idx[dst]:
            errors.append("%s 内 siblingIndex 重复: %s" % (dst, m.get("siblingIndex")))
        per_dest_idx[dst].add(m.get("siblingIndex"))
    for src, c in used.items():
        if c > 1:
            errors.append("move.source 被使用 %d 次: %s" % (c, src))

    # ---- removals -------------------------------------------------------
    removed = {r.get("source", "") for r in plan.get("emptyContainerRemovals", [])}
    move_sources = {m.get("source") for m in plan.get("moves", [])}
    absent = plan.get("verify", {}).get("absentPaths", [])
    for r in plan.get("emptyContainerRemovals", []):
        src = r.get("source", "")
        path = resolve(src, "removal.source")
        if src in move_sources:
            errors.append("同一节点既被移除又被声明来源: %s" % src)
        if absent and path and not any(a.rsplit("/", 1)[-1] == path.rsplit("/", 1)[-1] for a in absent):
            warns.append("removal %s 建议在 verify.absentPaths 中配一条证明它已消失" % src)

    # ---- verify keys ----------------------------------------------------
    for key in plan.get("verify", {}):
        if key not in SUPPORTED_VERIFY_KEYS:
            errors.append("verify.%s 不被 Unity 执行器支持（只允许 %s）" % (key, ", ".join(SUPPORTED_VERIFY_KEYS)))

    # ---- renames --------------------------------------------------------
    final_name = dict(name_by_path)
    required_root = os_basename_stem(plan.get("prefabAssetPath", ""))
    for index, r in enumerate(plan.get("renames", [])):
        tgt = r.get("target", "")
        path = resolve(tgt, "renames[%d].target" % index, allow_wrapper=True)
        if tgt in removed:
            errors.append("对将被移除的节点做重命名: %s" % tgt)
        if not path or path.startswith("@"):
            continue
        final_name[path] = r.get("name", "")
        if has_snapshot and "/" not in path and r.get("name", "") != required_root:
            errors.append("renames[%d] 试图把主根改名为 %r —— 主根必须保留资产文件名 %r，请删除这条 rename"
                          % (index, r.get("name", ""), required_root))

    # ---- private asset rename prefix -----------------------------------
    prefab_name = (plan.get("prefabName") or "").strip()
    for index, t in enumerate(plan.get("textureRenames", [])):
        to_name = t.get("toName", "")
        if not prefab_name or not to_name.startswith(prefab_name + "_"):
            errors.append("textureRenames[%d].toName %r 必须以 prefabName + \"_\"（%r）开头；"
                          "prefabName 就是纹理前缀，请让两者一致" % (index, to_name, prefab_name + "_"))
    for index, a in enumerate(plan.get("spriteAtlasRenames", [])):
        if a.get("toName", "") != prefab_name:
            errors.append("spriteAtlasRenames[%d].toName %r 必须等于 prefabName %r"
                          % (index, a.get("toName", ""), prefab_name))

    # ---- directChildren consistency ------------------------------------
    moves_by_dest = collections.defaultdict(list)
    for m in plan.get("moves", []):
        moves_by_dest[m.get("destination", "")[1:]].append(m)
    for dc in plan.get("verify", {}).get("directChildren", []):
        path, kids = dc.get("path", ""), list(dc.get("children", []))
        leaf = path.rsplit("/", 1)[-1]
        wid = next((w["id"] for w in plan.get("wrappers", []) if wrapper_names.get(w["id"], "") == leaf), None)
        if wid is None or not has_snapshot:
            continue
        expect = [final_name.get(path_by_id.get(m.get("source", "")[5:], ""))
                  for m in sorted(moves_by_dest.get(wid, []), key=lambda x: x.get("siblingIndex", 0))]
        expect = [n for n in expect if n]
        if expect and expect != kids:
            errors.append("directChildren %s 与 moves 结果不一致：%s vs %s" % (path, kids, expect))

    # ---- extraction / decisions ----------------------------------------
    extracts = {}
    for key in ("componentExtractions", "stateComponentExtractions",
                "variantComponentExtractions", "statefulComponentExtractions"):
        for e in plan.get(key, []):
            extracts[e.get("id")] = e
    intents = {i.get("id") for i in plan.get("postGroupingExtractionIntents", [])}
    for d in plan.get("componentFamilyDecisions", []):
        if d.get("mode") == "skip":
            continue
        if d.get("extractionId") not in extracts and d.get("extractionId") not in intents:
            errors.append("decision 引用了不存在的 extractionId: %s" % d.get("extractionId"))
    for eid, e in extracts.items():
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
        warns.append("R2: 本计划含抽取（%d 个族）。verify.nodes 必须是**展开后**的期望值，"
                     "否则会在保存后得到 VERIFY_WARN issue=nodes。" % len(extracts))
        for cond, msg in ((not v.get("directChildren"), "抽取后应给每个源容器写 directChildren"),
                          (not v.get("hierarchy"), "抽取后应给每个源容器写 hierarchy")):
            if cond:
                warns.append("R2: " + msg)
    return errors, warns


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--plan", required=True)
    ap.add_argument("--snapshot")
    ap.add_argument("--quiet", action="store_true")
    args = ap.parse_args()

    plan = json.load(open(args.plan, encoding="utf-8-sig"))
    nodes = None
    if args.snapshot:
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        from parse_hierarchy_snapshot import load_snapshot
        _, nodes = load_snapshot(args.snapshot)
    errors, warns = lint(plan, nodes)

    if not args.quiet:
        for w in warns:
            print("WARN : " + w)
        for e in errors:
            print("ERROR: " + e)
        print("summary: %d error(s), %d warning(s)" % (len(errors), len(warns)))
    return 2 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
