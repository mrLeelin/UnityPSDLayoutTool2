#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Parse a hierarchy snapshot (Unity chat JSON or the NativeUnity text snapshot).

Why this exists
---------------
`snapshot_prefab_hierarchy.ps1` (and the NativeUnity runner's Snapshot phase) emit a *text*
snapshot: `SUMMARY\tnodes=..` plus one `NODE\t<full path>\t...` line per node. That form has
**no `node:<id>`**, therefore it can never be used to author a version 2 (node-id) plan.
This tool turns either form into a normalised tree and can emit a version 1 path-based plan
skeleton, which is the only plan shape a pure-CLI session can author on its own.

Handles the two traps found in practice:
  * Unity serialises names that start with `[` as `'[Name]'` and non-ASCII as `"\\u65e5..."`;
  * files written by PowerShell `Out-File` may carry a BOM / mojibake if the caller forgot
    `[Console]::OutputEncoding = UTF8`.

Usage
-----
  python parse_hierarchy_snapshot.py --snapshot <snapshot.json|snapshot.txt> [--json-out tree.json]
                                     [--v1-skeleton-out skeleton.json] [--max-depth 3]
"""
from __future__ import annotations

import argparse
import codecs
import collections
import json
import os
import re
import sys

ESC = chr(92) + "u"


def unescape_name(name: str) -> str:
    name = name.strip()
    if len(name) >= 2 and name[0] == name[-1] and name[0] in "'\"":
        name = name[1:-1]
    if ESC in name:
        try:
            name = codecs.decode(name, "unicode_escape")
        except Exception:
            pass
    return name


def load_snapshot(path: str):
    """Return (summary: dict, nodes: list[dict]) from either snapshot form."""
    raw = open(path, encoding="utf-8-sig", errors="replace").read()
    stripped = raw.lstrip()
    if stripped.startswith("{"):
        data = json.loads(raw)
        payload = data.get("data", {}).get("result", {}) if isinstance(data, dict) else {}
        text = payload.get("result") if isinstance(payload, dict) else None
        if not isinstance(text, str):
            # already the chat-style JSON snapshot
            nodes = data.get("nodes") if isinstance(data, dict) else None
            if nodes is None:
                raise SystemExit("unrecognised snapshot json: " + path)
            summary = {"nodes": str(len(nodes)), "fingerprint": data.get("fingerprint", "")}
            return summary, [
                {"id": n.get("id"), "path": n.get("path"), "name": n.get("name"),
                 "kind": "json", "node": n} for n in nodes]
        raw = text
    summary = {}
    nodes = []
    raw = raw.replace("\r\n", "\n").replace("\r", "\n")
    for line in raw.split("\n"):
        if line.startswith("SUMMARY\t"):
            for kv in line.split("\t")[1:]:
                if "=" in kv:
                    k, v = kv.split("=", 1)
                    summary[k.strip()] = v.strip()
            continue
        if not line.startswith("NODE\t"):
            continue
        parts = line.split("\t")
        rec = {"path": parts[1], "kind": "text"}
        for p in parts[2:]:
            if "=" in p:
                k, v = p.split("=", 1)
                rec[k.strip()] = v.strip()
        rec["name"] = rec["path"].rsplit("/", 1)[-1]
        rec["name"] = unescape_name(rec["name"])
        nodes.append(rec)
    if not nodes:
        raise SystemExit("no NODE lines found in " + path)
    return summary, nodes


def size_delta(rec):
    m = re.search(r"sizeDelta=([-\d.,]+)", rec.get("rect", ""))
    if not m:
        return (0.0, 0.0)
    vals = [float(x) for x in m.group(1).split(",") if x.strip()]
    return (vals[0], vals[1]) if len(vals) >= 2 else (0.0, 0.0)


def build(summary, nodes):
    paths = [n["path"] for n in nodes]
    children = collections.defaultdict(list)
    for n in nodes:
        p = n["path"]
        parent = p.rsplit("/", 1)[0] if "/" in p else ""
        children[parent].append(p)

    def sibling_key(p):
        rec = next(n for n in nodes if n["path"] == p)
        try:
            return int(rec.get("sibling", 0))
        except Exception:
            return 0

    for k in children:
        children[k].sort(key=sibling_key)
    return paths, children


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--snapshot", required=True)
    ap.add_argument("--json-out")
    ap.add_argument("--v1-skeleton-out")
    ap.add_argument("--max-depth", type=int, default=2)
    args = ap.parse_args()

    summary, nodes = load_snapshot(args.snapshot)
    paths, children = build(summary, nodes)
    root = paths[0]
    print("SUMMARY:", " ".join("%s=%s" % kv for kv in summary.items()))
    print("nodes=%d root=%s" % (len(nodes), root))
    if "fingerprint" in summary and summary["fingerprint"]:
        print("fingerprint:", summary["fingerprint"])
    print("note: the text snapshot carries NO node:<id>; author a version 1 (path) plan with it,")
    print("      or take the Unity chat snapshot when a version 2 (node-id) plan is required.")

    def depth(p):
        return p.count("/")

    for p in paths:
        if depth(p) <= args.max_depth:
            rec = next(n for n in nodes if n["path"] == p)
            print("  " * depth(p) + rec["name"] + "  [children=%d sizeDelta=%s]" %
                  (len(children[p]), size_delta(rec)))

    if args.json_out:
        out = {"summary": summary, "root": root,
               "nodes": [{"path": n["path"], "name": n["name"], "children": children[n["path"]],
                          "sizeDelta": size_delta(n), "active": n.get("active"),
                          "components": n.get("components"), "nestedPrefab": n.get("nestedPrefab"),
                          "id": n.get("id")} for n in nodes]}
        with open(args.json_out, "w", encoding="utf-8") as fh:
            json.dump(out, fh, ensure_ascii=False, indent=1)
        print("wrote", args.json_out)

    if args.v1_skeleton_out:
        skeleton = {
            "version": 1,
            "prefabAssetPath": "<Assets/.../Target.prefab>",
            "output": {"mode": "in_place", "assetPath": "<Assets/.../Target.prefab>"},
            "prefabName": "<TargetView>",
            "wrappers": [], "moves": [], "renames": [], "emptyContainerRemovals": [],
            "tightBounds": [],
            "textureRenames": [], "spriteAtlasRenames": [],
            "componentFamilyDecisions": [], "containmentResolutions": [], "flatSiblingResolutions": [],
            "componentExtractions": [], "stateComponentExtractions": [],
            "variantComponentExtractions": [], "statefulComponentExtractions": [],
            "verify": {
                "nodes": int(summary.get("nodes", len(nodes))),
                "components": int(summary.get("components", 0) or 0),
                "images": int(summary.get("images", 0) or 0),
                "missingComponents": int(summary.get("missingComponents", 0) or 0),
                "requireEnglishNames": True,
                "forbiddenObjectNamePatterns": [],
                "hierarchy": [{"path": p, "childCount": len(children[p])}
                              for p in paths if children[p]],
                "directChildren": [{"path": p, "children": [c.rsplit("/", 1)[-1] for c in children[p]]}
                                   for p in paths if children[p]],
                "absentPaths": [],
            },
        }
        with open(args.v1_skeleton_out, "w", encoding="utf-8") as fh:
            json.dump(skeleton, fh, ensure_ascii=False, indent=2)
        print("wrote", args.v1_skeleton_out, "(verify.hierarchy/directChildren pre-filled)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
