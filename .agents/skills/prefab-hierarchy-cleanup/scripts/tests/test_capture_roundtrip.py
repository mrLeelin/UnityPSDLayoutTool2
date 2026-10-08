"""Tests for capture_roundtrip.py.

The contract that matters: both capture payloads must share ONE frozen framing block so the two
rendered PNGs are pixel-comparable, and the compare command must be strict on every RGBA channel.

Run:  python tests/test_capture_roundtrip.py
"""
from __future__ import annotations

import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT_ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("capture_roundtrip", SCRIPT_ROOT / "capture_roundtrip.py")
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)

try:
    from PIL import Image
    HAVE_PIL = True
except ImportError:  # pragma: no cover
    HAVE_PIL = False

FINGERPRINT = "c" * 64


def node(node_id, name, parent, sibling, world_rect, child_count=0):
    return {
        "id": node_id, "path": "Root" if not parent else "Root/" + name, "name": name,
        "parentId": parent, "siblingIndex": sibling, "childCount": child_count, "active": True,
        "components": ["UnityEngine.RectTransform"],
        "rect": {"anchorMin": [0.5, 0.5], "anchorMax": [0.5, 0.5], "pivot": [0.5, 0.5],
                 "anchoredPosition": [0.0, 0.0], "sizeDelta": [1.0, 1.0]},
        "worldRect": list(world_rect),
    }


class Frame(unittest.TestCase):
    def records(self):
        return [{"id": "n0", "path": "Root", "name": "Root", "node": node("n0", "Root", "", 0, [-100.0, -50.0, 100.0, 50.0], 2)},
                {"id": "n1", "path": "Root/A", "name": "A", "node": node("n1", "A", "n0", 0, [-40.0, -20.0, 0.0, 20.0])},
                {"id": "n2", "path": "Root/B", "name": "B", "node": node("n2", "B", "n0", 1, [500.0, 500.0, 600.0, 600.0])}]

    def test_root_frame_covers_root_rect_exactly(self):
        # root rect is 200 x 100 -> aspect 2 -> a 100px-tall image is 200px wide
        frame = MODULE.derive_frame(self.records(), mode="root", height=100, margin=0.0)
        self.assertEqual((200, 100), (frame["width"], frame["height"]))
        self.assertAlmostEqual(50.0, frame["orthographicSize"], places=6)
        self.assertEqual([0.0, 0.0, -100.0], frame["cameraPosition"])
        # visible width = 2 * ortho * aspect must equal the framed rect width
        self.assertAlmostEqual(200.0, 2 * frame["orthographicSize"] * (frame["width"] / frame["height"]), places=6)

    def test_margin_widens_the_visible_area(self):
        frame = MODULE.derive_frame(self.records(), mode="root", height=100, margin=0.1)
        self.assertAlmostEqual(55.0, frame["orthographicSize"], places=6)
        self.assertAlmostEqual(220.0, 2 * frame["orthographicSize"] * (frame["width"] / frame["height"]), places=6)

    def test_union_frame_includes_offscreen_content(self):
        # union rect is 700 x 650
        frame = MODULE.derive_frame(self.records(), mode="union", height=100, margin=0.0)
        self.assertEqual([-100.0, -50.0, 600.0, 600.0], frame["rect"])
        self.assertEqual(250.0, frame["cameraPosition"][0])   # (x0 + x1) / 2
        self.assertEqual(275.0, frame["cameraPosition"][1])   # (y0 + y1) / 2
        self.assertAlmostEqual(325.0, frame["orthographicSize"], places=6)
        # the image keeps the framed rect's aspect ratio up to integer width rounding
        self.assertAlmostEqual(700.0 / 650.0, frame["width"] / frame["height"], places=2)
        self.assertAlmostEqual(700.0, 2 * frame["orthographicSize"] * (frame["width"] / frame["height"]), delta=8.0)

    def test_degenerate_rect_is_rejected(self):
        records = [{"id": "n0", "path": "Root", "name": "Root",
                    "node": node("n0", "Root", "", 0, [0.0, 0.0, 0.0, 0.0])}]
        with self.assertRaises(SystemExit):
            MODULE.derive_frame(records, mode="root")


class Prepare(unittest.TestCase):
    def snapshot_path(self, tmp):
        snapshot = {
            "schemaVersion": 1, "fingerprint": FINGERPRINT,
            "nodes": [node("n0", "Root", "", 0, [-100.0, -50.0, 100.0, 50.0], 1),
                      node("n1", "A", "n0", 0, [-40.0, -20.0, 0.0, 20.0])],
        }
        path = Path(tmp) / "snapshot.json"
        path.write_text(json.dumps(snapshot, ensure_ascii=False), encoding="utf-8")
        return path

    def test_prepare_freezes_framing_and_emits_two_comparable_payloads(self):
        with tempfile.TemporaryDirectory() as tmp:
            snapshot = self.snapshot_path(tmp)
            code = MODULE.main(["prepare", "--snapshot", str(snapshot),
                                "--prefab-asset-path", "Assets/UI/Demo.prefab",
                                "--out-dir", tmp, "--tag", "demo", "--height", "100"])
            self.assertEqual(0, code)
            manifest = json.loads((Path(tmp) / "demo.framing.json").read_text(encoding="utf-8"))
            before = (Path(tmp) / "demo.before.capture.cs").read_text(encoding="utf-8")
            after = (Path(tmp) / "demo.after.capture.cs").read_text(encoding="utf-8")

            self.assertEqual(FINGERPRINT, manifest["snapshotFingerprint"])
            self.assertEqual(100, manifest["frame"]["height"])
            # identical framing, only the output image path differs
            self.assertEqual(before.replace("demo.before.png", "X"), after.replace("demo.after.png", "X"))
            # rect 200 x 100, default margin 0.02 -> 200x100 image, ortho = 50 * 1.02
            self.assertIn("camera.orthographicSize = 51.0f;", before)
            self.assertIn("captureWidth = 200;", before)
            self.assertIn("demo.before.png", before)
            self.assertIn("demo.after.png", after)


@unittest.skipUnless(HAVE_PIL, "Pillow is required for compare")
class Compare(unittest.TestCase):
    def test_identical_images_match(self):
        with tempfile.TemporaryDirectory() as tmp:
            first = Path(tmp) / "a.png"
            second = Path(tmp) / "b.png"
            Image.new("RGBA", (16, 16), (10, 20, 30, 255)).save(first)
            Image.new("RGBA", (16, 16), (10, 20, 30, 255)).save(second)
            self.assertEqual(0, MODULE.main(["compare", "--before", str(first), "--after", str(second)]))

    def test_single_channel_difference_is_detected(self):
        with tempfile.TemporaryDirectory() as tmp:
            first = Path(tmp) / "a.png"
            second = Path(tmp) / "b.png"
            report_path = Path(tmp) / "report.json"
            diff_path = Path(tmp) / "diff.png"
            Image.new("RGBA", (16, 16), (10, 20, 30, 255)).save(first)
            image = Image.new("RGBA", (16, 16), (10, 20, 30, 255))
            image.putpixel((3, 4), (10, 20, 30, 254))
            image.save(second)
            code = MODULE.main(["compare", "--before", str(first), "--after", str(second),
                                "--diff-output", str(diff_path), "--report-output", str(report_path)])
            self.assertEqual(1, code)
            report = json.loads(report_path.read_text(encoding="utf-8"))
            self.assertFalse(report["match"])
            self.assertEqual(1, report["changedPixels"])
            self.assertEqual("pixel_mismatch", report["reason"])
            self.assertTrue(diff_path.exists())

    def test_dimension_mismatch_is_reported(self):
        with tempfile.TemporaryDirectory() as tmp:
            first = Path(tmp) / "a.png"
            second = Path(tmp) / "b.png"
            Image.new("RGBA", (16, 16), (0, 0, 0, 255)).save(first)
            Image.new("RGBA", (16, 17), (0, 0, 0, 255)).save(second)
            report = MODULE.VISUAL_AUDIT.compare_images(first, second, None)
            self.assertFalse(report["match"])
            self.assertEqual("dimension_mismatch", report["reason"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
