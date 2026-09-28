# -*- coding: utf-8 -*-
import json
import os
import sys
import tempfile
import unittest

SCRIPTS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, SCRIPTS)

from parse_hierarchy_snapshot import load_snapshot  # noqa: E402
from simulate_and_verify_plan import simulate, verify_contracts  # noqa: E402
from validate_plan_locally import lint  # noqa: E402

SNAPSHOT = {
    "fingerprint": "fp-1",
    "nodes": [
        {"id": "n000000", "path": "RewardView", "name": "RewardView", "parentId": "", "siblingIndex": 0, "childCount": 2},
        {"id": "n000001", "path": "RewardView/Title", "name": "Title", "parentId": "n000000", "siblingIndex": 0, "childCount": 0},
        {"id": "n000002", "path": "RewardView/Icon", "name": "Icon", "parentId": "n000000", "siblingIndex": 1, "childCount": 0},
    ],
}


def load_nodes(snapshot):
    with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False, encoding="utf-8") as fh:
        json.dump(snapshot, fh)
        path = fh.name
    try:
        return load_snapshot(path)[1]
    finally:
        os.remove(path)


def plan(**overrides):
    value = {
        "version": 2, "snapshotFingerprint": "fp-1",
        "prefabAssetPath": "Assets/UI/RewardView.prefab", "prefabName": "RewardView",
        "wrappers": [{"id": "content", "name": "[Content]", "parent": "node:n000000", "siblingIndex": 0}],
        "moves": [{"source": "node:n000001", "destination": "@content", "siblingIndex": 0},
                  {"source": "node:n000002", "destination": "@content", "siblingIndex": 1}],
        "renames": [{"target": "node:n000001", "name": "TitleLabel"}],
        "emptyContainerRemovals": [], "tightBounds": [{"target": "@content"}],
        "textureRenames": [], "spriteAtlasRenames": [],
        "verify": {"directChildren": [{"path": "RewardView/[Content]", "children": ["TitleLabel", "Icon"]}]},
    }
    value.update(overrides)
    return value


class SnapshotTests(unittest.TestCase):
    def test_json_snapshot_loads_with_ids(self):
        nodes = load_nodes(SNAPSHOT)
        self.assertEqual([n["id"] for n in nodes], ["n000000", "n000001", "n000002"])

    def test_non_json_snapshot_is_rejected(self):
        with tempfile.NamedTemporaryFile("w", suffix=".txt", delete=False, encoding="utf-8") as fh:
            fh.write("SUMMARY\tnodes=1\nNODE\tRoot\tsibling=0\n")
            path = fh.name
        try:
            with self.assertRaises(SystemExit):
                load_snapshot(path)
        finally:
            os.remove(path)


class LintTests(unittest.TestCase):
    def setUp(self):
        self.nodes = load_nodes(SNAPSHOT)

    def test_valid_v2_plan_has_no_errors(self):
        errors, _ = lint(plan(), self.nodes)
        self.assertEqual(errors, [])

    def test_non_v2_plan_is_rejected(self):
        errors, _ = lint(plan(version=3), self.nodes)
        self.assertTrue(any("version" in e for e in errors), errors)

    def test_raw_path_reference_is_rejected(self):
        p = plan(moves=[{"source": "RewardView/Title", "destination": "@content", "siblingIndex": 0}])
        errors, _ = lint(p, self.nodes)
        self.assertTrue(any("node:<id>" in e and "RewardView/Title" in e for e in errors), errors)

    def test_unsupported_verify_key_is_rejected(self):
        p = plan(verify={"nodes": 4, "forbiddenObjectNamePatterns": ["^\\d+$"], "components": 3})
        errors, _ = lint(p, self.nodes)
        self.assertTrue(any("verify.forbiddenObjectNamePatterns" in e for e in errors), errors)
        self.assertTrue(any("verify.components" in e for e in errors), errors)

    def test_texture_prefix_must_match_prefab_name(self):
        bad = plan(prefabName="主界面View", textureRenames=[{"from": "Assets/a.png", "toName": "MainScreenView_Icon"}])
        errors, _ = lint(bad, self.nodes)
        self.assertTrue(any("textureRenames[0]" in e for e in errors), errors)
        good = plan(prefabName="MainScreenView",
                    textureRenames=[{"from": "Assets/a.png", "toName": "MainScreenView_Icon"}],
                    spriteAtlasRenames=[{"from": "Assets/a.spriteatlasv2", "toName": "MainScreenView"}])
        self.assertEqual(lint(good, self.nodes)[0], [])

    def test_sprite_atlas_must_equal_prefab_name(self):
        p = plan(spriteAtlasRenames=[{"from": "Assets/a.spriteatlasv2", "toName": "OtherView"}])
        errors, _ = lint(p, self.nodes)
        self.assertTrue(any("spriteAtlasRenames[0]" in e for e in errors), errors)

    def test_root_rename_is_rejected(self):
        p = plan(renames=[{"target": "node:n000000", "name": "MainView"}])
        errors, _ = lint(p, self.nodes)
        self.assertTrue(any("主根" in e for e in errors), errors)


class SimulationTests(unittest.TestCase):
    def setUp(self):
        self.nodes = load_nodes(SNAPSHOT)

    def test_simulation_proves_direct_children(self):
        sim = simulate(plan(), self.nodes)
        errors, root = verify_contracts(plan(), sim)
        self.assertEqual(errors, [])
        self.assertEqual(sim.final_path_of_node("node:n000001"), "RewardView/[Content]/TitleLabel")

    def test_simulation_rejects_raw_paths_and_non_v2(self):
        with self.assertRaises(SystemExit):
            simulate(plan(moves=[{"source": "RewardView/Title", "destination": "@content", "siblingIndex": 0}]),
                     self.nodes)
        with self.assertRaises(SystemExit):
            simulate(plan(version=3), self.nodes)


if __name__ == "__main__":
    unittest.main()
