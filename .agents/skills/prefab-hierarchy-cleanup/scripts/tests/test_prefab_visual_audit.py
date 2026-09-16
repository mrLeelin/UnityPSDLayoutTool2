from __future__ import annotations

import importlib.util
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "prefab_visual_audit.py"
SPEC = importlib.util.spec_from_file_location("prefab_visual_audit", SCRIPT_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class CapturePayloadTests(unittest.TestCase):
    def test_capture_payload_is_parameterized_and_restores_reviewed_states(self) -> None:
        payload = MODULE.render_capture_payload(
            prefab_asset_path="Assets/UI/Reward Panel.prefab",
            image_output_path="Library/Audit/reward.png",
            width=640,
            height=1136,
            camera_position=(4.5, -2.0, -900.0),
            orthographic_size=568.0,
            near_clip=0.25,
            far_clip=2000.0,
            background_rgba=(0.1, 0.2, 0.3, 1.0),
            state_overrides=[MODULE.StateOverride("[States]/Claimable", True)],
        )

        self.assertIn('var prefabAssetPath = "Assets/UI/Reward Panel.prefab";', payload)
        self.assertIn("var captureWidth = 640;", payload)
        self.assertIn("camera.orthographicSize = 568.0f;", payload)
        self.assertIn('new System.Collections.Generic.KeyValuePair<string, bool>("[States]/Claimable", true)', payload)
        self.assertIn("saved.Key.SetActive(saved.Value);", payload)
        self.assertIn("PrefabUtility.UnloadPrefabContents(root)", payload)
        self.assertIn('";stateCoverage=" + stateCoverage', payload)
        self.assertNotIn("SaveAsPrefabAsset", payload)
        self.assertNotIn("SetDirty", payload)

    def test_capture_rejects_duplicate_or_unsafe_state_paths(self) -> None:
        kwargs = dict(
            prefab_asset_path="Assets/UI/View.prefab",
            image_output_path="Library/view.png",
            width=10,
            height=10,
            camera_position=(0.0, 0.0, -10.0),
            orthographic_size=5.0,
            near_clip=0.1,
            far_clip=20.0,
            background_rgba=(0.0, 0.0, 0.0, 0.0),
        )
        with self.assertRaisesRegex(ValueError, "unique"):
            MODULE.render_capture_payload(
                **kwargs,
                state_overrides=[MODULE.StateOverride("States/A", True), MODULE.StateOverride("States/A", False)],
            )
        with self.assertRaisesRegex(ValueError, "descendant"):
            MODULE.render_capture_payload(**kwargs, state_overrides=[MODULE.StateOverride("../A", True)])

    def test_capture_escapes_csharp_strings(self) -> None:
        payload = MODULE.render_capture_payload(
            prefab_asset_path='Assets/UI/A"B.prefab',
            image_output_path=r"C:\Audit\image.png",
            width=1,
            height=1,
            camera_position=(0.0, 0.0, -1.0),
            orthographic_size=1.0,
            near_clip=0.1,
            far_clip=2.0,
            background_rgba=(0.0, 0.0, 0.0, 0.0),
        )
        self.assertIn('Assets/UI/A\\"B.prefab', payload)
        self.assertIn('C:\\\\Audit\\\\image.png', payload)


class OwnershipPayloadTests(unittest.TestCase):
    def test_ownership_payload_is_generic_recursive_and_sorted(self) -> None:
        payload = MODULE.render_texture_ownership_payload(
            texture_directories=["Assets/UI/Textures", "Assets/Shared/Icons"],
            owner_roots=["Assets/UI", "Assets/Config"],
            owner_extensions=[".prefab", ".asset"],
        )
        self.assertIn('new [] { "Assets/UI/Textures", "Assets/Shared/Icons" }', payload)
        self.assertIn("AssetDatabase.GetDependencies(ownerPath, true)", payload)
        self.assertIn("OrderBy(path => path", payload)
        self.assertIn("TEXTURE_OWNERSHIP_V1", payload)
        self.assertNotIn("RenameAsset", payload)

    def test_ownership_payload_rejects_non_asset_paths(self) -> None:
        with self.assertRaisesRegex(ValueError, "below Assets"):
            MODULE.render_texture_ownership_payload(
                texture_directories=["Library/Textures"],
                owner_roots=["Assets"],
                owner_extensions=[".prefab"],
            )


class CompareTests(unittest.TestCase):
    def setUp(self) -> None:
        self.root = Path.cwd() / "Library"
        self.root.mkdir(parents=True, exist_ok=True)
        self.paths: list[Path] = []

    def tearDown(self) -> None:
        for path in self.paths:
            path.unlink(missing_ok=True)

    def save(self, name: str, size: tuple[int, int], pixels: list[tuple[int, int, int, int]]) -> Path:
        descriptor, raw_path = tempfile.mkstemp(prefix="visual-audit-", suffix="-" + name, dir=self.root)
        os.close(descriptor)
        path = Path(raw_path)
        self.paths.append(path)
        image = Image.new("RGBA", size)
        image.putdata(pixels)
        image.save(path)
        return path

    def test_compare_identical_rgba_images(self) -> None:
        before = self.save("before.png", (2, 1), [(1, 2, 3, 4), (5, 6, 7, 8)])
        after = self.save("after.png", (2, 1), [(1, 2, 3, 4), (5, 6, 7, 8)])
        report = MODULE.compare_images(before, after, None)
        self.assertTrue(report["match"])
        self.assertEqual("identical", report["reason"])

    def test_compare_detects_alpha_only_difference(self) -> None:
        before = self.save("before.png", (1, 1), [(10, 20, 30, 40)])
        after = self.save("after.png", (1, 1), [(10, 20, 30, 41)])
        descriptor, raw_diff = tempfile.mkstemp(prefix="visual-audit-", suffix="-diff.png", dir=self.root)
        os.close(descriptor)
        diff = Path(raw_diff)
        self.paths.append(diff)
        report = MODULE.compare_images(before, after, diff)
        self.assertFalse(report["match"])
        self.assertEqual(1, report["changedPixels"])
        self.assertEqual(1, report["changedChannels"])
        self.assertEqual(1, report["maxChannelDelta"])
        self.assertTrue(diff.exists())

    def test_compare_rejects_dimension_mismatch(self) -> None:
        before = self.save("before.png", (1, 1), [(0, 0, 0, 0)])
        after = self.save("after.png", (2, 1), [(0, 0, 0, 0), (0, 0, 0, 0)])
        report = MODULE.compare_images(before, after, None)
        self.assertFalse(report["match"])
        self.assertEqual("dimension_mismatch", report["reason"])

    def test_cli_returns_failure_and_writes_machine_readable_report(self) -> None:
        before = self.save("before.png", (1, 1), [(0, 0, 0, 0)])
        after = self.save("after.png", (1, 1), [(1, 0, 0, 0)])
        descriptor, raw_report = tempfile.mkstemp(prefix="visual-audit-", suffix="-report.json", dir=self.root)
        os.close(descriptor)
        report_path = Path(raw_report)
        self.paths.append(report_path)
        completed = subprocess.run(
            [
                sys.executable,
                str(SCRIPT_PATH),
                "compare",
                "--before",
                str(before),
                "--after",
                str(after),
                "--report-output",
                str(report_path),
            ],
            text=True,
            capture_output=True,
            check=False,
        )
        self.assertEqual(1, completed.returncode)
        self.assertEqual("pixel_mismatch", json.loads(report_path.read_text(encoding="utf-8"))["reason"])


if __name__ == "__main__":
    unittest.main()
