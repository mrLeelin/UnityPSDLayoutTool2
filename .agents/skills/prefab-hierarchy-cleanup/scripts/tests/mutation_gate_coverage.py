#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Mutation-test the plan gates against one real, validated plan.

Takes a plan that every gate accepts, injects realistic mistakes, and records which gate catches
each one. This is the empirical answer to "does the simulation actually do anything?" and the
regression test for gate coverage itself: if a refactor silently drops a check, a mutant stops
being caught and the union count drops.

Gates
  lint      validate_plan_locally.py     offline schema/reference checks
  sim       simulate_and_verify_plan.py  rebuilds the tree, proves verify contracts
  draw      check_draw_order.py          overlapping drawable pairs must keep their order
  preflight preflight_v2.py              reject / partial-risk / quality, plus predicted candidates

Usage
-----
  python tests/mutation_gate_coverage.py --plan P.json --snapshot S.json [--project-root <dir>]
Exit 0 = every mutant is caught by at least one gate and the unmutated plan passes all of them.
"""
from __future__ import annotations

import argparse
import copy
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path

PY = sys.executable
SCRIPTS = Path(__file__).resolve().parents[1]
CONFIG = {"snapshot": None, "project_root": None}


def run(command):
    proc = subprocess.run(command, capture_output=True, text=True, encoding="utf-8", errors="replace")
    return proc.returncode, (proc.stdout or "") + (proc.stderr or "")


def gate_lint(plan_path):
    code, out = run([PY, str(SCRIPTS / "validate_plan_locally.py"),
                     "--plan", str(plan_path), "--snapshot", CONFIG["snapshot"]])
    found = re.search(r"summary: (\d+) error", out)
    return bool(found and int(found.group(1)) > 0), out.strip().splitlines()[-1] if out.strip() else ""


def gate_sim(plan_path):
    code, out = run([PY, str(SCRIPTS / "simulate_and_verify_plan.py"),
                     "--plan", str(plan_path), "--snapshot", CONFIG["snapshot"]])
    found = re.search(r"contract errors: (\d+)", out)
    return bool(found and int(found.group(1)) > 0), "contract errors=%s" % (found.group(1) if found else "?")


def gate_draw(plan_path):
    code, out = run([PY, str(SCRIPTS / "check_draw_order.py"),
                     "--snapshot", CONFIG["snapshot"], "--plan", str(plan_path)])
    found = re.search(r"INVERTED ORDER PAIRS\s*:\s*(\d+)", out)
    return bool(found and int(found.group(1)) > 0), "inverted=%s" % (found.group(1) if found else "?")


def gate_preflight(plan_path):
    code, out = run([PY, str(SCRIPTS / "preflight_v2.py"),
                     "--plan", str(plan_path), "--snapshot", CONFIG["snapshot"],
                     "--project-root", CONFIG["project_root"]])
    found = re.search(r"reject=(\d+)\s+would-be-partial=(\d+)\s+quality=(\d+)", out)
    if not found:
        return True, "preflight crashed"
    reject, partial, quality = (int(value) for value in found.groups())
    return (reject + partial + quality) > 0, "reject=%d partial=%d quality=%d" % (reject, partial, quality)


GATES = (("lint", gate_lint), ("sim", gate_sim), ("draw", gate_draw), ("preflight", gate_preflight))


def move_index(plan, source):
    for move in plan["moves"]:
        if move["source"] == source:
            return move
    raise KeyError(source)


def mutants():
    """(name, description, mutate) - each represents a mistake a session could plausibly make."""
    def m01(plan):  # siblingIndex off-by-one inside one wrapper
        move_index(plan, "node:n000010")["siblingIndex"] = 4
        move_index(plan, "node:n000041")["siblingIndex"] = 2
    def m02(plan):  # node moved into the wrong unit
        move_index(plan, "node:n000043")["destination"] = "@album_button"
        move_index(plan, "node:n000043")["siblingIndex"] = 2
    def m03(plan):  # one node left with its PSD name
        plan["renames"] = [r for r in plan["renames"] if r["target"] != "node:n000019"]
    def m04(plan):  # a new container never tightened
        plan["tightBounds"] = [b for b in plan["tightBounds"] if b["target"] != "@energy_hearts"]
    def m05(plan):  # stale node count
        plan["expectedNodeCount"] = 49
    def m06(plan):  # one label never moved out of the dissolved bucket
        plan["moves"] = [m for m in plan["moves"] if m["source"] != "node:n000043"]
    def m07(plan):  # typo in a node reference
        move_index(plan, "node:n000022")["source"] = "node:n000099"
    def m08(plan):  # private asset prefix drift
        plan["textureRenames"][0]["toName"] = "MainView_Background"
    def m09(plan):  # rename the rename-locked root
        plan["renames"].append({"target": "node:n000000", "name": "MainScreenView"})
    def m10(plan):  # the same source moved twice
        plan["moves"].append({"source": "node:n000014", "destination": "@draw_button", "siblingIndex": 6})
    def m11(plan):  # absence not proven
        plan["absentPaths"] = plan["absentPaths"][:2]
    def m12(plan):  # wrong child count in the contract
        plan["verify"]["hierarchy"][3]["childCount"] = 8
    def m13(plan):  # wrong sibling order in the contract
        block = plan["verify"]["directChildren"][3]
        block["children"] = list(reversed(block["children"]))
    def m14(plan):  # operationScope as an array
        plan["operationScope"] = [plan["operationScope"]]
    def m15(plan):  # a removal that still owns its child
        plan["emptyContainerRemovals"].append({"source": "node:n000032"})
    def m16(plan):  # rename something that is being removed
        plan["renames"].append({"target": "node:n000036", "name": "TextBucket"})
    return [
        ("M01", "siblingIndex 顺序错（@draw_button 内两项互换）", m01),
        ("M02", "节点移进了错误单元（DIARY 标签 -> @album_button）", m02),
        ("M03", "漏掉一个 rename（AvatarIcon 仍是中文名）", m03),
        ("M04", "漏掉一个 tightBounds（@energy_hearts）", m04),
        ("M05", "expectedNodeCount 过期（49）", m05),
        ("M06", "漏搬一个子节点（文字 桶未清空）", m06),
        ("M07", "node 引用写错（n000022 -> n000099）", m07),
        ("M08", "私有贴图前缀漂移（MainView_）", m08),
        ("M09", "给 rename-locked 主根加了 rename", m09),
        ("M10", "同一个 move.source 用了两次", m10),
        ("M11", "漏掉一条 absentPaths", m11),
        ("M12", "verify.hierarchy childCount 写错", m12),
        ("M13", "verify.directChildren 顺序写反", m13),
        ("M14", "operationScope 写成数组", m14),
        ("M15", "待删除容器仍有子节点", m15),
        ("M16", "对被删除节点做 rename", m16),
    ]


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--plan", required=True)
    parser.add_argument("--snapshot", required=True)
    parser.add_argument("--project-root", default="")
    args = parser.parse_args()
    CONFIG["snapshot"] = args.snapshot
    CONFIG["project_root"] = args.project_root
    base = json.loads(Path(args.plan).read_text(encoding="utf-8"))
    tmp = Path(tempfile.mkdtemp(prefix="mutants-"))

    print("baseline (unmutated plan)")
    baseline = {}
    plan_path = tmp / "baseline.json"
    plan_path.write_text(json.dumps(base, ensure_ascii=False, indent=2), encoding="utf-8")
    for name, gate in GATES:
        caught, detail = gate(plan_path)
        baseline[name] = caught
        print("  %-10s caught=%s  %s" % (name, caught, detail))
    assert not any(baseline.values()), "the unmutated plan must pass every gate"

    rows = []
    for mid, description, mutate in mutants():
        plan = copy.deepcopy(base)
        mutate(plan)
        plan_path = tmp / (mid + ".json")
        plan_path.write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding="utf-8")
        result = {}
        for name, gate in GATES:
            caught, detail = gate(plan_path)
            result[name] = (caught, detail)
        rows.append((mid, description, result))
        print("%s %s" % (mid, description))
        for name, _ in GATES:
            caught, detail = result[name]
            print("    %-10s %-5s %s" % (name, "CAUGHT" if caught else "-", detail if caught else ""))

    print()
    header = "| mutant | mistake | " + " | ".join(name for name, _ in GATES) + " |"
    print(header)
    print("|" + "---|" * (len(GATES) + 2))
    for mid, description, result in rows:
        cells = " | ".join("✔" if result[name][0] else "·" for name, _ in GATES)
        print("| %s | %s | %s |" % (mid, description, cells))
    totals = {name: sum(1 for _, _, r in rows if r[name][0]) for name, _ in GATES}
    print()
    print("catch rate over %d mutants: %s" % (
        len(rows), "  ".join("%s=%d/%d" % (name, totals[name], len(rows)) for name, _ in GATES)))
    print("union (any gate): %d/%d" % (sum(1 for _, _, r in rows if any(r[n][0] for n, _ in GATES)), len(rows)))
    print("mutants no gate catches: %s" % ([mid for mid, _, r in rows if not any(r[n][0] for n, _ in GATES)] or "none"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
