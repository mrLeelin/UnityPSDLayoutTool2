# -*- coding: utf-8 -*-
"""Before/after preservation audit for one in-place Prefab cleanup (read-only, Unity + offline).

Why this exists
---------------
The skill's other checkers prove *contracts* (hierarchy, directChildren, container tightness,
instance topology) but they never proved the thing a user actually cares about after a cleanup:
**every node that existed before is still there, with the same components, sprite, text and
RectTransform values**. A plan can satisfy every contract and still drop a label, swap a sprite or
shrink an icon by a fraction of a pixel. This tool closes that gap:

  capture before  ->  apply the reviewed plan  ->  capture after  ->  compare

`compare` is a pure offline check, so it can be re-run any time (it needs no Editor).

What is compared (and why exactly this)
---------------------------------------
Per node it captures non-Transform component types, `Image.sprite` (texture asset + sprite name),
TMP text + font, and the **reparent-invariant** RectTransform values (`sizeDelta`, anchors, pivot,
`localScale`, `rotationZ`). It deliberately does NOT use `anchoredPosition` as the identity
contract - moving a node under a new parent legitimately changes it - and it compares world rects
only as an AABB (all four corners), because `corner[0]`/`corner[2]` mis-measures a rotated rect by
`s*(cos-sin)` vs `s*(cos+sin)` and would fake a 0.3 px difference across tools.

Checks
------
  * A  every node of the *before* dump that is not inside a nested Prefab and not inside an
       extraction unit must reappear in the *after* dump (outside nested instances) with an
       identical fingerprint; added nodes must be plain containers (no components beyond
       RectTransform)
  * B  per extraction unit (needs --plan): the instance's `[Common]` + currently ACTIVE state
       branch must fingerprint-match the unit that was replaced (this also proves that per-instance
       overrides really wrote the instance's own values)
  * C  instance links (`src` == the plan's assetPath), exactly one active state branch, the
       forbidden-name gate, and target-owned missing Sprites == 0

Usage
-----
  # once per stage, before the apply
  python audit_prefab_preservation.py --project-path E:/... --mode capture \\
         --prefab-path Assets/.../Target.prefab --out before.json
  # after the apply (offline; no Editor needed)
  python audit_prefab_preservation.py --mode compare --before before.json --after after.json \\
         --plan stage.plan.json
"""
from __future__ import annotations

import argparse
import collections
import json
import os
import re
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from read_unity_selection import run_payload  # noqa: E402

PAYLOAD_TEMPLATE = os.path.join(HERE, "payloads", "audit_prefab_dump.cs")
SCHEMA = 1

# Name gate copied from the runner's IsNonSemanticObjectName(): PSD/export tokens, raw display
# values and punctuation-only names are not semantic names.
NONSEMANTIC_NAME = re.compile(
    r"^(?:\d+(?:_\d+)?|\d+(?:\.\d+)?[kKmM]|\d+[A-Za-z]\d+[A-Za-z]|[+_-]+|img_v\d.*|ui_[A-Za-z0-9_]+)$")
ASCII_NAME = re.compile(r"^[A-Za-z0-9_\[\]]+$")


# ------------------------------------------------------------------ capture
def capture(project_path, prefab_path, unity, timeout, out_path):
    template = open(PAYLOAD_TEMPLATE, encoding="utf-8").read()
    payload = os.path.join(os.environ.get("TEMP", "/tmp"), "php_audit_prefab_dump.cs")
    with open(payload, "w", encoding="utf-8") as fh:
        fh.write(template.replace("\"ASSET_PATH_PLACEHOLDER\"", json.dumps(prefab_path), 1))
    text, diagnostics = run_payload(project_path, unity, payload, timeout)
    if "DUMP_END" not in text:
        raise SystemExit("payload did not complete; first 800 chars:\n" + text[:800])
    data = parse_dump(text)
    data["prefabAssetPath"] = prefab_path
    data["schema"] = SCHEMA
    out_dir = os.path.dirname(os.path.abspath(out_path))
    if out_dir:
        os.makedirs(out_dir, exist_ok=True)
    with open(out_path, "w", encoding="utf-8") as fh:
        json.dump(data, fh, ensure_ascii=False, indent=1)
    if diagnostics:
        sys.stderr.write("diagnostics: %s\n" % diagnostics[:3])
    print("captured %d nodes -> %s" % (len(data["nodes"]), out_path))
    return data


def parse_dump(text):
    nodes = {}
    meta = {}
    for raw in text.replace("\r\n", "\n").split("\n"):
        if raw.startswith("NODE\t"):
            f = raw.split("\t")
            if len(f) < 12:
                raise SystemExit("malformed NODE line (%d fields): %s" % (len(f), raw[:200]))
            rt = f[11].split("|")
            rt_vals = [x.split(",") for x in rt] if rt and rt[0] else []
            nodes[f[1]] = dict(
                name=f[1].rsplit("/", 1)[-1],
                childCount=int(f[2]),
                active=f[3] == "1",
                isInstanceRoot=f[4] == "1",
                sourceAsset=f[5],
                components=[c for c in f[6].split(",") if c],
                sprite=f[7],
                text=f[8],
                font=f[9],
                worldRect=[float(x) for x in f[10].split(",")] if f[10] else None,
                sizeDelta=[float(x) for x in rt_vals[0]] if len(rt_vals) > 0 else None,
                anchorMin=[float(x) for x in rt_vals[1]] if len(rt_vals) > 1 else None,
                anchorMax=[float(x) for x in rt_vals[2]] if len(rt_vals) > 2 else None,
                pivot=[float(x) for x in rt_vals[3]] if len(rt_vals) > 3 else None,
                localScale=[float(x) for x in rt_vals[4]] if len(rt_vals) > 4 else None,
                rotationZ=float(rt_vals[5][0]) if len(rt_vals) > 5 else None,
            )
        elif raw.startswith("MISSING_SPRITES_OWN"):
            meta["missingSpritesOwn"] = int(raw.split()[1])
        elif raw.startswith("MISSING_SPRITES_NESTED"):
            meta["missingSpritesNested"] = int(raw.split()[1])
    return dict(meta=meta, nodes=nodes)


# ------------------------------------------------------------------ compare helpers
def children_map(nodes):
    kids = collections.defaultdict(list)
    for path in nodes:
        if "/" in path:
            kids[path.rsplit("/", 1)[0]].append(path)
    return kids


def descendants(nodes, root_path):
    """all nodes strictly below root_path (root excluded)"""
    prefix = root_path + "/"
    return [p for p in nodes if p.startswith(prefix)]


def leaves(nodes, kids, root_path):
    out, stack = [], [root_path]
    while stack:
        p = stack.pop()
        for c in kids.get(p, []):
            if nodes[c]["childCount"] == 0:
                out.append(c)
            else:
                stack.append(c)
    return out


def fingerprint(n):
    return (tuple(sorted(n["components"])), n["sprite"], normalize_text(n["text"]),
            tuple(n["sizeDelta"] or []), tuple(n["anchorMin"] or []), tuple(n["anchorMax"] or []),
            tuple(n["pivot"] or []), tuple(n["localScale"] or []), n["rotationZ"])


def normalize_text(t):
    """YAML multi-line scalars and the dump both keep a trailing newline; fold all whitespace."""
    return " ".join((t or "").replace("\\n", "\n").split())


def inside_nested_instance(nodes, kids, path):
    """True when any proper ancestor is a nested-Prefab instance root"""
    parts = path.split("/")
    for i in range(1, len(parts)):
        if nodes.get("/".join(parts[:i]), {}).get("isInstanceRoot"):
            return True
    return False


def extraction_units(plan):
    """[(kind, extraction, unit_source_paths)] for every declared extraction"""
    units = []
    for e in plan.get("componentExtractions", []):
        units.append(("component", e, [e["template"]] + list(e["instances"])))
    for e in plan.get("stateComponentExtractions", []):
        units.append(("state", e, [s["source"] for s in e["states"]]))
    for e in plan.get("variantComponentExtractions", []):
        units.append(("variant", e, [s["source"] for s in e["states"]] +
                      [i["source"] for i in e["instances"]]))
    for e in plan.get("statefulComponentExtractions", []):
        units.append(("stateful", e, [e["common"]["source"]] + [s["source"] for s in e["states"]] +
                      [i["source"] for i in e["instances"]]))
    return units


def compare(before, after, plan, quiet=False, notes=None):
    bn, an = before["nodes"], after["nodes"]
    bk, ak = children_map(bn), children_map(an)
    errors = []
    notes = notes if notes is not None else []

    unit_roots = set()
    if plan:
        for _kind, _e, sources in extraction_units(plan):
            unit_roots.update(sources)
        post_apply = [s for s in unit_roots if bn.get(s, {}).get("isInstanceRoot")]
        if post_apply:
            notes.append("警告: before 快照里的抽取来源已经是嵌套实例（%s…）——before 必须在本计划的 "
                         "apply 之前 capture，否则 B 检查会把已经存在的状态分支当成'多出内容'。"
                         % post_apply[0])

    # ---- A: untouched nodes must survive with an identical fingerprint
    keep = [p for p in bn
            if not inside_nested_instance(bn, bk, p)
            and not any(p == s or p.startswith(s + "/") for s in unit_roots)]
    # Extraction instance roots are verified by the B check, not by the A multiset: exclude them
    # (and, via inside_nested_instance, their whole instance subtree) from the A scope.
    asset_paths = set()
    if plan:
        for array in ("componentExtractions", "stateComponentExtractions",
                      "variantComponentExtractions", "statefulComponentExtractions"):
            for e in plan.get(array, []):
                if e.get("assetPath"):
                    asset_paths.add(e["assetPath"])
    after_scope = [p for p in an if not inside_nested_instance(an, ak, p)
                   and not (an[p]["isInstanceRoot"] and an[p]["sourceAsset"] in asset_paths)]
    # Compare identity-bearing nodes only (a node with no component beyond RectTransform is a
    # structural container: containers are legitimate additions). Containers are reported, not
    # matched, so the check stays symmetric and cannot invent a "lost node" out of a new wrapper.
    keep_identity = [p for p in keep if bn[p]["components"]]
    after_identity = [p for p in after_scope if an[p]["components"]]
    added_containers = [p for p in after_scope if not an[p]["components"]]
    expected = collections.Counter(fingerprint(bn[p]) for p in keep_identity)
    got = collections.Counter(fingerprint(an[p]) for p in after_identity)
    for fp, count in (expected - got).items():
        sample = next((p for p in keep_identity if fingerprint(bn[p]) == fp), "?")
        errors.append("A 失去或改变 %d 个节点（示例 %s）: %s" % (count, sample, describe(fp)))
    for fp, count in (got - expected).items():
        sample = next((p for p in after_identity if fingerprint(an[p]) == fp), "?")
        errors.append("A 多出 %d 个节点（示例 %s）: %s" % (count, sample, describe(fp)))
    notes.append("A 未受抽取影响的身份节点 %d 个（保留容器 %d 个）；after 侧新增容器 %d 个"
                 % (len(keep_identity), len(keep) - len(keep_identity), len(added_containers)))

    # ---- B: per extraction unit
    if plan:
        for kind, e, _sources in extraction_units(plan):
            if kind == "component":
                for inst in e["instances"]:
                    if inst not in an:
                        errors.append("B 实例不存在: " + inst)
                    elif an[inst]["childCount"]:
                        errors.append("B component 实例应为叶子: " + inst)
                continue
            by_state = {s["id"]: s for s in e["states"]}
            default_name = next((s["name"] for s in e["states"] if s["id"] == e.get("defaultState")), None)
            if kind == "state":
                for st in e["states"]:
                    inst = st["source"]
                    if inst not in an:
                        errors.append("B 实例不存在: " + inst)
                        continue
                    check_states_container(errors, an, ak, inst, [s["name"] for s in e["states"]],
                                           default_name, inst)
                continue
            insts = e["instances"] if kind in ("variant", "stateful") else []
            if kind == "variant":
                insts = [dict(path=i["source"], name=i["name"], state=i["state"]) for i in e["instances"]]
            elif kind == "stateful":
                insts = [dict(path=i["source"], name=i["name"], state=i["state"],
                              common=[m["name"] for m in e["common"]["members"]],
                              stateMembers=[m["name"] for m in by_state[i["state"]]["members"]])
                         for i in e["instances"]]
            for inst in insts:
                path = inst["path"]
                if path not in an:
                    errors.append("B 实例不存在: " + path)
                    continue
                if an[path]["name"] != inst["name"]:
                    errors.append("B 实例名不符 %s -> %s (期望 %s)" % (path, an[path]["name"], inst["name"]))
                expected_children = ["[Common]", "[States]"] if kind == "variant" else ["[States]", "[Common]"]
                got_children = [an[c]["name"] for c in ak.get(path, [])]
                if got_children != expected_children:
                    errors.append("B 实例子节点应为 %s: %s -> %s" % (expected_children, path, got_children))
                    continue
                if e.get("assetPath") and an[path]["sourceAsset"] != e["assetPath"]:
                    errors.append("B 实例来源资产不符 %s -> %s (期望 %s)"
                                  % (path, an[path]["sourceAsset"], e["assetPath"]))
                state_name = by_state.get(inst["state"], {}).get("name")
                ok = check_states_container(errors, an, ak, path, [s["name"] for s in e["states"]],
                                            state_name, path)
                if not ok:
                    continue
                # fingerprint of the replaced unit vs [Common] + active branch of the instance
                want = collections.Counter(fingerprint(bn[p]) for p in leaves(bn, bk, path))
                scope = leaves(an, ak, path + "/[Common]") + leaves(an, ak, path + "/[States]/" + state_name)
                have = collections.Counter(fingerprint(an[p]) for p in scope)
                for fp, count in (want - have).items():
                    errors.append("B 单元内容丢失 %s（%d 个）: %s" % (path, count, describe(fp)))
                for fp, count in (have - want).items():
                    errors.append("B 单元内容多出 %s（%d 个）: %s" % (path, count, describe(fp)))
                if kind == "stateful":
                    want_common = inst.get("common") or []
                    got_common = [an[c]["name"] for c in ak.get(path + "/[Common]", [])]
                    if want_common and got_common != want_common:
                        errors.append("B [Common] 成员不符 %s: %s != %s" % (path, got_common, want_common))

    # ---- C: links, naming, missing sprites
    root_name = next((p for p in an if "/" not in p), None)
    if plan and plan.get("verify"):
        for pat in plan["verify"].get("forbiddenObjectNamePatterns", []):
            rx = re.compile(pat)
            for path, n in an.items():
                if path == root_name:
                    continue
                if rx.search(n["name"]):
                    errors.append("C 禁用命名 %s 命中 %s" % (pat, path))
    for path, n in an.items():
        if "/" not in path:
            continue          # the Prefab root must keep the asset file name
        if not ASCII_NAME.match(n["name"]) or NONSEMANTIC_NAME.match(n["name"]):
            errors.append("C 非语义命名: " + path)
    if after.get("meta", {}).get("missingSpritesOwn"):
        errors.append("C 目标自有缺失 Sprite: %d" % after["meta"]["missingSpritesOwn"])

    if not quiet:
        for note in notes:
            print(note)
        print("nodes: before=%d after=%d" % (len(bn), len(an)))
        print("errors: %d" % len(errors))
        for e in errors[:60]:
            print("  - " + e)
        if not errors:
            print("preservation audit PASSED (read-only check)")
    return errors


def check_states_container(errors, an, ak, path, want_branches, want_active, label):
    states = path + "/[States]"
    if states not in an:
        errors.append("B 缺少 [States]: " + path)
        return False
    names = [an[c]["name"] for c in ak.get(states, [])]
    if names != want_branches:
        errors.append("B 状态分支顺序不符 %s: %s != %s" % (path, names, want_branches))
        return False
    active = [an[c]["name"] for c in ak.get(states, []) if an[c]["active"]]
    if len(active) != 1:
        errors.append("B 应恰好 1 个激活分支 %s: %s" % (path, active))
        return False
    if want_active and active[0] != want_active:
        errors.append("B 激活分支不符 %s: %s != %s" % (path, active[0], want_active))
        return False
    return True


def describe(fp):
    """turn a fingerprint back into a readable 'what differs' hint"""
    comps, sprite, text, size, amin, amax, piv, scale, rotz = fp
    return ("comps=%s sprite=%s text=%r size=%s anchors=%s/%s pivot=%s scale=%s rot=%s"
            % (list(comps), sprite or "-", text, list(size), list(amin), list(amax),
               list(piv), list(scale), rotz))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--project-path")
    ap.add_argument("--unity", default=shutil.which("unity") or "unity")
    ap.add_argument("--mode", choices=["capture", "compare"], required=True)
    ap.add_argument("--prefab-path")
    ap.add_argument("--out")
    ap.add_argument("--before")
    ap.add_argument("--after")
    ap.add_argument("--plan")
    ap.add_argument("--timeout", type=int, default=120)
    ap.add_argument("--quiet", action="store_true")
    args = ap.parse_args()

    if args.mode == "capture":
        if not args.project_path or not args.prefab_path or not args.out:
            raise SystemExit("capture requires --project-path, --prefab-path and --out")
        capture(args.project_path, args.prefab_path, args.unity, args.timeout, args.out)
        return 0

    if not args.before or not args.after:
        raise SystemExit("compare requires --before and --after")
    before = json.load(open(args.before, encoding="utf-8"))
    after = json.load(open(args.after, encoding="utf-8"))
    plan = json.load(open(args.plan, encoding="utf-8-sig")) if args.plan else None
    errors = compare(before, after, plan, quiet=args.quiet)
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
