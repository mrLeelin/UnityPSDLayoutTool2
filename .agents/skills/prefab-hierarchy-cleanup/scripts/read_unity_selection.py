#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Read-only Unity Editor queries through the project's Unity Pipeline CLI (`eval_file`).

Why: an external/terminal AI session has no Editor API of its own. The running Editor exposes a
local pipeline server, and `unity command eval_file` executes a small C# payload *inside that
same Editor process*, so it can answer questions about the user's live session:

  --mode selection       what the user currently has selected (path, components, rect, nested
                         prefab source, TMP text, sprite, open Prefab Stage, active scene)
  --mode instance-links  every nested-prefab instance root in a prefab and the asset it points
                         at (proves that extracted child Prefabs are really linked)
  --mode prefab-stage    which Prefab Stage is currently open

Everything here is READ-ONLY: no payload saves or mutates an asset. Latency is ~2-5 s per call.
Payload contract (learned the hard way): top-level statements only, no `using` directives,
UnityEngine.UI.* fully qualified.

Usage
-----
  python read_unity_selection.py --project-path E:/.../monsterhunter --mode selection
  python read_unity_selection.py --project-path ... --mode instance-links \
         --prefab-path Assets/.../Target.prefab
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PAYLOADS = os.path.join(HERE, "payloads")

LINKS_PAYLOAD = """var prefabPath = {prefab};
var root = PrefabUtility.LoadPrefabContents(prefabPath);
if (root == null) throw new System.InvalidOperationException("prefab did not load: " + prefabPath);
try
{{
    var counts = new System.Collections.Generic.Dictionary<string, int>();
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("LINKS_BEGIN");
    var stack = new System.Collections.Generic.Stack<Transform>();
    stack.Push(root.transform);
    while (stack.Count > 0)
    {{
        var t = stack.Pop();
        if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
        {{
            var src = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
            if (!counts.ContainsKey(src)) counts[src] = 0;
            counts[src]++;
            sb.AppendLine("INSTANCE\\t" + t.name + "\\tsource=" + src + "\\tactive=" + t.gameObject.activeSelf);
            continue;
        }}
        for (var i = 0; i < t.childCount; i++) stack.Push(t.GetChild(i));
    }}
    foreach (var kv in counts) sb.AppendLine("ASSET\\t" + kv.Key + "\\tinstances=" + kv.Value);
    sb.AppendLine("LINKS_END");
    return sb.ToString();
}}
finally {{ PrefabUtility.UnloadPrefabContents(root); }}
"""


def run_payload(project_path, unity, payload_path, timeout):
    cmd = [unity, "--json", "--non-interactive", "command",
           "--project-path", project_path, "--timeout", str(timeout + 30),
           "eval_file", "--", "--file", payload_path, "--timeout", str(timeout * 1000)]
    proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    raw = proc.stdout or ""
    try:
        data = json.loads(raw)
    except Exception:
        sys.stderr.write(raw[:800] + "\n" + (proc.stderr or "")[:400] + "\n")
        raise SystemExit("could not parse Unity CLI output")
    if not data.get("success"):
        raise SystemExit("Unity CLI error: " + json.dumps(data.get("errors"), ensure_ascii=False)[:600])
    result = (data.get("data") or {}).get("result") or {}
    return result.get("result") or "", result.get("diagnostics") or []


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--project-path", required=True)
    ap.add_argument("--unity", default=shutil.which("unity") or "unity")
    ap.add_argument("--mode", choices=["selection", "instance-links", "prefab-stage"], default="selection")
    ap.add_argument("--prefab-path")
    ap.add_argument("--timeout", type=int, default=60)
    args = ap.parse_args()

    if args.mode == "selection":
        payload = os.path.join(PAYLOADS, "read_selection.cs")
    elif args.mode == "prefab-stage":
        payload = os.path.join(PAYLOADS, "read_selection.cs")
    else:
        if not args.prefab_path:
            raise SystemExit("--prefab-path is required for --mode instance-links")
        payload = os.path.join(os.environ.get("TEMP", "/tmp"), "php_instance_links.cs")
        with open(payload, "w", encoding="utf-8") as fh:
            fh.write(LINKS_PAYLOAD.format(prefab=json.dumps(args.prefab_path)))

    text, diagnostics = run_payload(args.project_path, args.unity, payload, args.timeout)
    print(text)
    if args.mode == "selection":
        for line in text.splitlines():
            if line.startswith("prefabStage="):
                print("(read-only query; nothing was modified)")
    if diagnostics:
        print("diagnostics:", diagnostics[:3])
    return 0


if __name__ == "__main__":
    sys.exit(main())
