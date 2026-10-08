#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""One-command Unity render round-trip for a Prefab review (before / after / compare).

WHY THIS EXISTS
---------------
`prefab_visual_audit.py` already generates parameterized NativeUnity capture payloads and compares
RGBA images, but every session still has to re-derive the camera framing by hand and keep the
before/after parameters identical. A Python sprite composite (what a previous session fell back
to) is NOT equivalent: it draws no TextMeshPro text and no materials.

This module freezes the framing once into a manifest, emits both capture payloads from it, and
then compares the two rendered PNGs. It deliberately does not invoke Unity: it prints the exact
NativeUnity command for the caller to run, matching prefab_visual_audit.py's contract.

Workflow
--------
  1. prepare   (before the .apply)            -> framing.json + before/after .capture.cs payloads
  2. run the before payload in Unity          -> <tag>.before.png
  3. let Unity apply the approved plan
  4. run the after payload in Unity           -> <tag>.after.png
  5. compare                                  -> diff PNG + strict RGBA report

Usage
-----
  python capture_roundtrip.py prepare --snapshot S --prefab-asset-path P --out-dir D --tag T
                                      [--frame root|union] [--height 1024] [--margin 0.02]
  python capture_roundtrip.py compare --before B.png --after A.png [--diff-output D.png]
                                      [--report-output R.json]
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import os
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from parse_hierarchy_snapshot import load_snapshot  # noqa: E402


def _load_visual_audit():
    path = SCRIPT_DIR / "prefab_visual_audit.py"
    spec = importlib.util.spec_from_file_location("prefab_visual_audit", path)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


VISUAL_AUDIT = _load_visual_audit()


def derive_frame(records, mode="root", height=1024, margin=0.02, near_clip=1.0, far_clip=1000.0,
                 distance=100.0):
    """Camera parameters that put the chosen world rect exactly inside the captured image.

    The capture payload parents the Prefab root to a WorldSpace canvas at the origin without
    scaling, so a snapshot `worldRect` is already camera-space XY. All UI sits at z = 0, so the
    camera is placed at negative z with clipping that contains the z = 0 plane.
    """
    if mode == "union":
        rects = [record["node"].get("worldRect") for record in records]
        rects = [r for r in rects if r and len(r) == 4]
        if not rects:
            raise SystemExit("snapshot has no worldRect to frame")
        x0 = min(r[0] for r in rects)
        y0 = min(r[1] for r in rects)
        x1 = max(r[2] for r in rects)
        y1 = max(r[3] for r in rects)
    else:
        root = next((record for record in records if "/" not in record["path"]), records[0])
        rect = (root["node"] or {}).get("worldRect")
        if not rect or len(rect) != 4:
            raise SystemExit("root node has no worldRect; use --frame union")
        x0, y0, x1, y1 = rect

    rect_width, rect_height = x1 - x0, y1 - y0
    if rect_width <= 0 or rect_height <= 0:
        raise SystemExit("framed rect is degenerate: %r" % ((x0, y0, x1, y1),))
    if height <= 0:
        raise SystemExit("--height must be positive")

    aspect = rect_width / rect_height
    width = max(1, int(round(height * aspect)))
    orthographic_size = (rect_height / 2.0) * (1.0 + margin)
    return {
        "frameMode": mode,
        "rect": [x0, y0, x1, y1],
        "width": width,
        "height": height,
        "orthographicSize": orthographic_size,
        "cameraPosition": [round((x0 + x1) / 2.0, 6), round((y0 + y1) / 2.0, 6), -abs(distance)],
        "nearClip": near_clip,
        "farClip": far_clip,
        "margin": margin,
        "backgroundRgba": [0.0, 0.0, 0.0, 0.0],
    }


def emit_payload(frame, prefab_asset_path, image_output_path, state_overrides=()):
    from pathlib import PurePosixPath
    image_posix = str(PurePosixPath(str(image_output_path).replace("\\", "/")))
    return VISUAL_AUDIT.render_capture_payload(
        prefab_asset_path=prefab_asset_path,
        image_output_path=image_posix,
        width=frame["width"],
        height=frame["height"],
        camera_position=tuple(frame["cameraPosition"]),
        orthographic_size=frame["orthographicSize"],
        near_clip=frame["nearClip"],
        far_clip=frame["farClip"],
        background_rgba=tuple(frame["backgroundRgba"]),
        state_overrides=list(state_overrides),
    )


def write_text(path: Path, text: str):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def command_prepare(args):
    summary, records = load_snapshot(args.snapshot)
    frame = derive_frame(records, args.frame, args.height, args.margin)
    out_dir = Path(args.out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    before_png = (out_dir / (args.tag + ".before.png")).as_posix()
    after_png = (out_dir / (args.tag + ".after.png")).as_posix()
    manifest = {
        "tag": args.tag,
        "prefabAssetPath": args.prefab_asset_path,
        "snapshot": str(Path(args.snapshot).resolve()),
        "snapshotFingerprint": summary.get("fingerprint", ""),
        "beforeImage": before_png,
        "afterImage": after_png,
        "frame": frame,
    }
    manifest_path = out_dir / (args.tag + ".framing.json")
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    before_payload = out_dir / (args.tag + ".before.capture.cs")
    after_payload = out_dir / (args.tag + ".after.capture.cs")
    write_text(before_payload, emit_payload(frame, args.prefab_asset_path, before_png) + "\n")
    write_text(after_payload, emit_payload(frame, args.prefab_asset_path, after_png) + "\n")

    diff_path = out_dir / (args.tag + ".diff.png")
    report_path = out_dir / (args.tag + ".compare.json")
    print("PREPARED tag=%s" % args.tag)
    print("  framing      : %s" % manifest_path)
    print("  frame mode   : %s  rect=%s" % (frame["frameMode"], frame["rect"]))
    print("  capture      : %dx%d  orthographicSize=%.4f  camera=%s"
          % (frame["width"], frame["height"], frame["orthographicSize"], frame["cameraPosition"]))
    print("  before png   : %s" % before_png)
    print("  after  png   : %s" % after_png)
    print("")
    print("Next: run each payload through your NativeUnity backend while the Prefab is in the")
    print("matching state, then compare. Example command sequence (adjust to your runner):")
    print("  1) run  %s   -> %s" % (before_payload, before_png))
    print("  2) apply the approved plan (write the .apply sentinel)")
    print("  3) run  %s   -> %s" % (after_payload, after_png))
    print("  4) python capture_roundtrip.py compare --before %s --after %s --diff-output %s "
          "--report-output %s" % (before_png, after_png, diff_path, report_path))
    print("")
    print("Both payloads share one frozen framing block, so the two PNGs are pixel-comparable.")
    return 0


def command_compare(args):
    report = VISUAL_AUDIT.compare_images(Path(args.before), Path(args.after),
                                        Path(args.diff_output) if args.diff_output else None)
    if args.report_output:
        write_text(Path(args.report_output), json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps(report, ensure_ascii=False, indent=2))
    if report.get("match"):
        print("RESULT: identical - the render did not change.")
    else:
        print("RESULT: pixel changes detected (%s). Inspect the diff image and report."
              % report.get("reason"))
    return 0 if report.get("match") else 1


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)

    prepare = sub.add_parser("prepare", help="freeze framing and emit both capture payloads")
    prepare.add_argument("--snapshot", required=True)
    prepare.add_argument("--prefab-asset-path", required=True)
    prepare.add_argument("--out-dir", required=True)
    prepare.add_argument("--tag", required=True)
    prepare.add_argument("--frame", choices=("root", "union"), default="root")
    prepare.add_argument("--height", type=int, default=1024)
    prepare.add_argument("--margin", type=float, default=0.02)

    compare = sub.add_parser("compare", help="strictly compare two rendered PNGs")
    compare.add_argument("--before", required=True)
    compare.add_argument("--after", required=True)
    compare.add_argument("--diff-output")
    compare.add_argument("--report-output")
    return parser


def main(argv=None):
    args = build_parser().parse_args(argv)
    try:
        if args.command == "prepare":
            return command_prepare(args)
        return command_compare(args)
    except (OSError, ValueError) as exc:
        print("ERROR: %s" % exc, file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
