#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Parse the authoritative Unity hierarchy snapshot (JSON with node:<id> entries).

Unity writes this snapshot for the chat window and for terminal sessions. Every node carries
`id`, `path`, `name`, `parentId` and `siblingIndex`; a version 2 plan references nodes only by
`node:<id>` taken from it. This tool loads it into a normalised tree and prints an overview.

Usage
-----
  python parse_hierarchy_snapshot.py --snapshot <snapshot.json> [--json-out tree.json] [--max-depth 3]
"""
from __future__ import annotations

import argparse
import collections
import json
import sys


def load_snapshot(path: str):
    """Return (summary: dict, nodes: list[dict]) from the Unity JSON snapshot."""
    with open(path, encoding="utf-8-sig", errors="replace") as fh:
        raw = fh.read()
    try:
        data = json.loads(raw)
    except ValueError as exc:
        raise SystemExit("snapshot is not JSON: %s (%s)" % (path, exc))
    nodes = data.get("nodes") if isinstance(data, dict) else None
    if not isinstance(nodes, list) or not nodes:
        raise SystemExit("snapshot has no nodes array: " + path)
    summary = {"nodes": str(len(nodes)), "fingerprint": data.get("fingerprint", "")}
    records = []
    for n in nodes:
        if not n.get("id") or not n.get("path"):
            raise SystemExit("snapshot node without id/path: %r" % (n,))
        records.append({"id": n["id"], "path": n["path"],
                        "name": n.get("name") or n["path"].rsplit("/", 1)[-1], "node": n})
    return summary, records


def size_delta(rec):
    values = ((rec.get("node") or {}).get("rect") or {}).get("sizeDelta") or []
    return (float(values[0]), float(values[1])) if len(values) >= 2 else (0.0, 0.0)


def build(summary, nodes):
    paths = [n["path"] for n in nodes]
    by_path = {n["path"]: n for n in nodes}
    children = collections.defaultdict(list)
    for n in nodes:
        p = n["path"]
        parent = p.rsplit("/", 1)[0] if "/" in p else ""
        children[parent].append(p)

    def sibling_key(p):
        try:
            return int((by_path[p].get("node") or {}).get("siblingIndex", 0))
        except (TypeError, ValueError):
            return 0

    for k in children:
        children[k].sort(key=sibling_key)
    return paths, children


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--snapshot", required=True)
    ap.add_argument("--json-out")
    ap.add_argument("--max-depth", type=int, default=2)
    args = ap.parse_args()

    summary, nodes = load_snapshot(args.snapshot)
    paths, children = build(summary, nodes)
    by_path = {n["path"]: n for n in nodes}
    root = paths[0]
    print("SUMMARY:", " ".join("%s=%s" % kv for kv in summary.items()))
    print("nodes=%d root=%s" % (len(nodes), root))

    for p in paths:
        if p.count("/") <= args.max_depth:
            rec = by_path[p]
            print("  " * p.count("/") + "%s  [node:%s children=%d sizeDelta=%s]" %
                  (rec["name"], rec["id"], len(children[p]), size_delta(rec)))

    if args.json_out:
        out = {"summary": summary, "root": root,
               "nodes": [{"id": n["id"], "path": n["path"], "name": n["name"],
                          "children": children[n["path"]], "sizeDelta": size_delta(n),
                          "active": n["node"].get("active"), "components": n["node"].get("components"),
                          "nestedPrefab": n["node"].get("nestedPrefabAssetPath")} for n in nodes]}
        with open(args.json_out, "w", encoding="utf-8") as fh:
            json.dump(out, fh, ensure_ascii=False, indent=1)
        print("wrote", args.json_out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
