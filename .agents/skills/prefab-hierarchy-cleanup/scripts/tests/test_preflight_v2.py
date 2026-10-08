"""Negative tests for preflight_v2.py.

A preflight that never fails is worthless, so every rule gets a mutant that must be caught, plus
the predicted-second-stage coverage rule, which is the whole reason the tool exists.

Run:  python tests/test_preflight_v2.py
"""
from __future__ import annotations

import copy
import importlib.util
import json
import sys
import unittest
from pathlib import Path

SCRIPT_ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("preflight_v2", SCRIPT_ROOT / "preflight_v2.py")
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)

ROOT = "Root"
FINGERPRINT = "a" * 64


def leaf(node_id, name, parent, sibling, world_rect, components=None):
    return {
        "id": node_id, "path": "", "name": name, "parentId": parent, "siblingIndex": sibling,
        "childCount": 0, "active": True,
        "components": components or ["UnityEngine.RectTransform", "UnityEngine.CanvasRenderer",
                                     "UnityEngine.UI.Image"],
        "rect": {"anchorMin": [0.5, 0.5], "anchorMax": [0.5, 0.5], "pivot": [0.5, 0.5],
                 "anchoredPosition": [0.0, 0.0], "sizeDelta": [1.0, 1.0]},
        "worldRect": list(world_rect),
    }


def container(node_id, name, parent, sibling, world_rect):
    node = leaf(node_id, name, parent, sibling, world_rect, components=["UnityEngine.RectTransform"])
    return node


def build_snapshot(board_name="[Board]", candidates=None, findings=None, extra=None):
    nodes = [
        container("n000000", ROOT, "", 0, [-200.0, -200.0, 200.0, 200.0]),
        container("n000001", board_name, "n000000", 0, [-100.0, -100.0, 100.0, 100.0]),
        leaf("n000002", "Bg", "n000001", 0, [-100.0, -100.0, 100.0, 100.0]),
        leaf("n000003", "IconA", "n000001", 1, [-90.0, -90.0, -50.0, -50.0]),
        leaf("n000004", "IconB", "n000001", 2, [-40.0, -40.0, 0.0, 0.0]),
        leaf("n000005", "IconC", "n000001", 3, [10.0, 10.0, 50.0, 50.0]),
    ] + list(extra or [])
    for node in nodes:
        node["path"] = ROOT if node["parentId"] == "" else \
            next(other["path"] for other in nodes if other["id"] == node["parentId"]) + "/" + node["name"]
    counts = {node["id"]: 0 for node in nodes}
    for node in nodes:
        if node["parentId"]:
            counts[node["parentId"]] += 1
    for node in nodes:
        node["childCount"] = counts[node["id"]]
    return {
        "schemaVersion": 1,
        "prefabAssetPath": "Assets/UI/Root.prefab",
        "fingerprint": FINGERPRINT,
        "nodeReferenceSyntax": "node:<id>",
        "nodes": nodes,
        "componentFamilyCandidates": candidates or [],
        "containmentFindings": [],
        "flatSiblingFindings": findings or [],
    }


def base_plan():
    return {
        "version": 2,
        "snapshotFingerprint": FINGERPRINT,
        "reviewVersion": "rv-test-01",
        "prefabAssetPath": "Assets/UI/Root.prefab",
        "targetPrefabAssetPath": "Assets/UI/Root.prefab",
        "output": {"mode": "in_place", "assetPath": "Assets/UI/Root.prefab"},
        "prefabName": "DemoView",
        "selectionNodeIds": ["node:n000000", "node:n000001", "node:n000002",
                             "node:n000003", "node:n000004", "node:n000005"],
        "operationScope": {"kind": "full_cleanup", "scope": "whole_prefab"},
        "expectedNodeCount": 7,
        "expectedHierarchy": [{"path": "Root", "childCount": 1}],
        "directChildren": [{"path": "Root", "children": ["[Board]"]}],
        "absentPaths": [],
        "preserveRequirements": {"visualResult": "unchanged"},
        "wrappers": [{"id": "row", "parent": "node:n000001", "name": "[Row]", "siblingIndex": 0}],
        "moves": [
            {"source": "node:n000003", "destination": "@row", "siblingIndex": 0},
            {"source": "node:n000004", "destination": "@row", "siblingIndex": 1},
            {"source": "node:n000005", "destination": "@row", "siblingIndex": 2},
        ],
        "renames": [{"target": "node:n000001", "name": "[Board]"}],
        "emptyContainerRemovals": [],
        "tightBounds": [{"target": "@row"}],
        "textureRenames": [],
        "spriteAtlasRenames": [],
        "componentFamilyDecisions": [],
        "containmentResolutions": [],
        "flatSiblingResolutions": [],
        "componentExtractions": [],
        "stateComponentExtractions": [],
        "variantComponentExtractions": [],
        "statefulComponentExtractions": [],
        "postGroupingExtractionIntents": [],
        "selectedPrefabExtractions": [],
        "crossParentPrefabExtractions": [],
        "verify": {"nodes": 7},
    }


class PreflightHarness(unittest.TestCase):
    def write_fixtures(self, plan, snapshot):
        self.tmp = Path(self._testMethodName).with_suffix("")
        self.tmp = Path(__import__("tempfile").mkdtemp())
        plan_path = self.tmp / "plan.json"
        snapshot_path = self.tmp / "snapshot.json"
        plan_path.write_text(json.dumps(plan, ensure_ascii=False), encoding="utf-8")
        snapshot_path.write_text(json.dumps(snapshot, ensure_ascii=False), encoding="utf-8")
        return plan_path, snapshot_path

    def run_preflight(self, plan, snapshot=None):
        plan_path, snapshot_path = self.write_fixtures(plan, snapshot or build_snapshot())
        return MODULE.run(str(plan_path), str(snapshot_path))

    def assert_error_contains(self, report, fragment):
        joined = "\n".join(report.errors)
        self.assertIn(fragment, joined, msg="expected an error containing %r, got:\n%s" % (fragment, joined))


class Baseline(PreflightHarness):
    def test_valid_plan_has_no_errors(self):
        report = self.run_preflight(base_plan())
        self.assertEqual([], report.errors, msg="\n".join(report.errors))

    def test_operation_scope_as_array_is_rejected(self):
        plan = base_plan()
        plan["operationScope"] = [{"kind": "full_cleanup"}]
        report = self.run_preflight(plan)
        self.assert_error_contains(report, "operationScope 必须写成对象")

    def test_preserve_requirements_as_array_is_rejected(self):
        plan = base_plan()
        plan["preserveRequirements"] = ["keep everything"]
        report = self.run_preflight(plan)
        self.assert_error_contains(report, "preserveRequirements 必须写成对象")

    def test_unknown_root_field_is_rejected(self):
        plan = base_plan()
        plan["extraNotes"] = {"why": "should be rejected"}
        report = self.run_preflight(plan)
        self.assert_error_contains(report, "不支持的根字段")

    def test_containment_resolutions_must_stay_empty(self):
        plan = base_plan()
        plan["containmentResolutions"] = [{"source": "node:n000003"}]
        report = self.run_preflight(plan)
        self.assert_error_contains(report, "containmentResolutions 必须为空数组")

    def test_unsupported_verify_field_is_rejected(self):
        plan = base_plan()
        plan["verify"] = {"nodes": 7, "components": []}
        report = self.run_preflight(plan)
        self.assert_error_contains(report, "verify.components 不被执行器支持")

    def test_verify_path_must_start_with_root(self):
        plan = base_plan()
        plan["verify"] = {"nodes": 7, "hierarchy": [{"path": "Wrong/[Board]", "childCount": 1}]}
        report = self.run_preflight(plan)
        self.assert_error_contains(report, "必须以未改名的主根")

    def test_root_rename_is_rejected(self):
        plan = base_plan()
        plan["renames"].append({"target": "node:n000000", "name": "DemoView"})
        report = self.run_preflight(plan)
        self.assert_error_contains(report, "试图把主根改名")

    def test_leftover_non_semantic_name_is_rejected(self):
        plan = base_plan()
        plan["renames"] = [{"target": "node:n000001", "name": "[Board]"},
                           {"target": "node:n000002", "name": "35_50"}]
        report = self.run_preflight(plan)
        self.assert_error_contains(report, "命名闸门")

    def test_texture_prefix_mismatch_is_rejected(self):
        plan = base_plan()
        plan["textureRenames"] = [{"from": "Assets/UI/Old.png", "toName": "Wrong_Background"}]
        report = self.run_preflight(plan)
        self.assert_error_contains(report, "必须")

    def test_move_destination_must_be_wrapper(self):
        plan = base_plan()
        plan["moves"] = [{"source": "node:n000003", "destination": "node:n000001", "siblingIndex": 0},
                         {"source": "node:n000004", "destination": "@row", "siblingIndex": 1},
                         {"source": "node:n000005", "destination": "@row", "siblingIndex": 2}]
        report = self.run_preflight(plan)
        self.assert_error_contains(report, "moves.destination 必须是已声明的 @wrapperId")

    def test_wrapper_missing_from_tight_bounds_is_a_quality_issue(self):
        # Unity accepts this silently (the wrapper just saves as 0x0), so it must NOT be reported
        # as a rejection -- mislabelling the bucket hides which failures are expensive.
        plan = base_plan()
        plan["tightBounds"] = []
        report = self.run_preflight(plan)
        self.assertEqual([], report.reject)
        self.assertEqual([], report.partial_risk)
        self.assertTrue(any("R1" in message for message in report.quality), msg=report.quality)

    def test_wrong_expected_node_count_is_a_quality_issue(self):
        # expectedNodeCount is never compared by Unity; it is presence-checked only.
        plan = base_plan()
        plan["expectedNodeCount"] = 99
        report = self.run_preflight(plan)
        self.assertEqual([], report.reject)
        self.assertTrue(any("expectedNodeCount=99" in message for message in report.quality),
                        msg=report.quality)

    def test_wrong_verify_nodes_is_partial_risk(self):
        # verify.nodes IS compared after the save: Unity would write, then report partial.
        plan = base_plan()
        plan["verify"] = {"nodes": 99}
        report = self.run_preflight(plan)
        self.assertEqual([], report.reject)
        self.assertTrue(any("verify.nodes=99" in message for message in report.partial_risk),
                        msg=report.partial_risk)

    def test_wrong_direct_children_is_partial_risk(self):
        plan = base_plan()
        plan["verify"] = {"nodes": 7, "directChildren": [{"path": "Root", "children": ["[Board]"]},
                                                         {"path": "Root/[Board]/[Row]",
                                                          "children": ["IconC", "IconA", "IconB"]}]}
        report = self.run_preflight(plan)
        self.assertEqual([], report.reject)
        self.assertTrue(report.partial_risk, msg="expected a post-save verification risk")

    def test_structural_problem_is_a_rejection(self):
        plan = base_plan()
        plan["extraNotes"] = {"why": "should be rejected"}
        report = self.run_preflight(plan)
        self.assertTrue(report.reject, msg="structural problems must be rejection-class")

    def test_snapshot_fingerprint_mismatch_is_rejected(self):
        plan = base_plan()
        plan["snapshotFingerprint"] = "b" * 64
        report = self.run_preflight(plan)
        self.assert_error_contains(report, "snapshotFingerprint 与当前快照不一致")

    def test_duplicate_move_source_is_rejected(self):
        plan = base_plan()
        plan["moves"].append({"source": "node:n000003", "destination": "@row", "siblingIndex": 3})
        report = self.run_preflight(plan)
        self.assert_error_contains(report, "move.source 被使用 2 次")


class Removals(PreflightHarness):
    """Rules about emptyContainerRemovals, including the two the older gates miss."""

    def empty_container_plan(self):
        snapshot = build_snapshot(extra=[
            container("n000006", "Empty", "n000001", 4, [0.0, 0.0, 10.0, 10.0])])
        plan = base_plan()
        plan["emptyContainerRemovals"] = [{"source": "node:n000006"}]
        plan["absentPaths"] = ["Root/[Board]/Empty"]
        plan["expectedNodeCount"] = 7          # 8 nodes + wrapper - 1 removed
        plan["verify"] = {"nodes": 7}
        return plan, snapshot

    def test_removal_with_exact_absent_path_passes(self):
        plan, snapshot = self.empty_container_plan()
        report = self.run_preflight(plan, snapshot)
        self.assertEqual([], report.errors, msg="\n".join(report.errors))

    def test_removal_without_exact_absent_path_is_a_quality_issue(self):
        plan, snapshot = self.empty_container_plan()
        plan["absentPaths"] = []
        report = self.run_preflight(plan, snapshot)
        self.assertEqual([], report.reject)
        self.assertTrue(any("精确配对" in message for message in report.quality), msg=report.quality)

    def test_rename_on_a_removed_node_is_rejected(self):
        plan, snapshot = self.empty_container_plan()
        plan["renames"].append({"target": "node:n000006", "name": "Gone"})
        report = self.run_preflight(plan, snapshot)
        self.assertTrue(any("将被删除的节点" in message for message in report.reject), msg=report.reject)

    def test_removal_with_a_non_transform_component_is_rejected(self):
        snapshot = build_snapshot(extra=[
            leaf("n000006", "Empty", "n000001", 4, [0.0, 0.0, 10.0, 10.0])])
        plan = base_plan()
        plan["emptyContainerRemovals"] = [{"source": "node:n000006"}]
        plan["absentPaths"] = ["Root/[Board]/Empty"]
        plan["expectedNodeCount"] = 7
        plan["verify"] = {"nodes": 7}
        report = self.run_preflight(plan, snapshot)
        self.assertTrue(any("Transform 以外的组件" in message for message in report.reject), msg=report.reject)


class SnapshotCandidates(PreflightHarness):
    def test_mandatory_candidate_without_decision_is_rejected(self):
        snapshot = build_snapshot(candidates=[{
            "id": "family_001", "kind": "numbered_repeated", "parent": "node:n000001",
            "sources": ["node:n000003", "node:n000004", "node:n000005"],
            "suggestedAssetName": "Row", "instanceCount": 3,
            "recommendedMode": "component", "requiresExtraction": True,
        }])
        report = self.run_preflight(base_plan(), snapshot)
        self.assert_error_contains(report, "需要恰好 1 条 componentFamilyDecisions")

    def test_decision_with_wrong_parent_is_rejected(self):
        snapshot = build_snapshot(candidates=[{
            "id": "family_001", "kind": "numbered_repeated", "parent": "node:n000001",
            "sources": ["node:n000003", "node:n000004", "node:n000005"],
            "suggestedAssetName": "Row", "instanceCount": 3,
            "recommendedMode": "component", "requiresExtraction": True,
        }])
        plan = base_plan()
        plan["componentFamilyDecisions"] = [{
            "candidateId": "family_001", "parent": "node:n000000",
            "sources": ["node:n000003", "node:n000004", "node:n000005"],
            "mode": "component", "extractionId": "row", "reason": "test",
        }]
        plan["componentExtractions"] = [{
            "id": "row", "assetPath": "Assets/UI/Common/Row.prefab",
            "template": "node:n000003",
            "instances": ["node:n000003", "node:n000004", "node:n000005"],
        }]
        report = self.run_preflight(plan, snapshot)
        self.assert_error_contains(report, "decision.parent")


class PredictedSecondStage(PreflightHarness):
    """The rule that decides whether Unity will answer `partial`."""

    def plan_with_numbered_wrappers(self):
        plan = base_plan()
        plan["wrappers"] = [
            {"id": "row_1", "parent": "node:n000001", "name": "[Row_1]", "siblingIndex": 0},
            {"id": "row_2", "parent": "node:n000001", "name": "[Row_2]", "siblingIndex": 1},
            {"id": "row_3", "parent": "node:n000001", "name": "[Row_3]", "siblingIndex": 2},
        ]
        plan["moves"] = [
            {"source": "node:n000003", "destination": "@row_1", "siblingIndex": 0},
            {"source": "node:n000004", "destination": "@row_2", "siblingIndex": 0},
            {"source": "node:n000005", "destination": "@row_3", "siblingIndex": 0},
        ]
        plan["tightBounds"] = [{"target": "@row_1"}, {"target": "@row_2"}, {"target": "@row_3"}]
        plan["expectedNodeCount"] = 9
        plan["verify"] = {"nodes": 9}
        return plan

    def test_predicted_family_without_intent_is_rejected(self):
        report = self.run_preflight(self.plan_with_numbered_wrappers())
        self.assert_error_contains(report, "需要恰好 1 条覆盖它的 postGroupingExtractionIntents")
        self.assert_error_contains(report, "partial")

    def test_predicted_family_with_covering_intent_passes(self):
        plan = self.plan_with_numbered_wrappers()
        plan["postGroupingExtractionIntents"] = [{
            "id": "row", "mode": "component",
            "assetPath": "Assets/UI/Common/Row.prefab",
            "templatePath": "Root/[Board]/[Row_1]",
            "commonMembers": [],
            "instances": [
                {"path": "Root/[Board]/[Row_1]", "state": "", "commonSourceNames": [], "stateSourceNames": []},
                {"path": "Root/[Board]/[Row_2]", "state": "", "commonSourceNames": [], "stateSourceNames": []},
                {"path": "Root/[Board]/[Row_3]", "state": "", "commonSourceNames": [], "stateSourceNames": []},
            ],
            "states": [],
            "defaultState": "",
        }]
        report = self.run_preflight(plan)
        self.assertEqual([], report.errors, msg="\n".join(report.errors))
        self.assertTrue(any("由 intent" in note for note in report.notes), msg=report.notes)

    def test_bracketed_container_suppresses_flat_sibling_finding(self):
        report = self.run_preflight(base_plan())
        self.assertFalse(any("flat-sibling" in warning for warning in report.warnings), msg=report.warnings)

    def test_unbracketed_container_raises_flat_sibling_finding(self):
        snapshot = build_snapshot(board_name="Board")
        plan = base_plan()
        plan["renames"] = [{"target": "node:n000001", "name": "Board"}]
        plan["wrappers"] = []
        plan["moves"] = []
        plan["tightBounds"] = []
        plan["expectedNodeCount"] = 6
        plan["verify"] = {"nodes": 6}
        report = self.run_preflight(plan, snapshot)
        self.assertTrue(any("flat-sibling" in warning for warning in report.warnings), msg=report.warnings)


class RealPlan(unittest.TestCase):
    """Integration check against the plan produced for 主界面.prefab, when it is present."""

    PROJECT = Path("E:/Project/Demo/monsterhunter")
    PLAN = Path("E:/Project/Demo/monsterhunter/Library/PsdHierarchyTerminal/"
                "external-20261008-113033-29db9f.plan.json")
    SNAPSHOT = Path("E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/"
                    "f2568a614f8c048c748da7ba96d04e140e8ae10a018694b962a1bd45f975abea.json")

    def test_real_plan_passes(self):
        if not self.PLAN.exists() or not self.SNAPSHOT.exists():
            self.skipTest("real plan/snapshot not present")
        plan = json.loads(self.PLAN.read_text(encoding="utf-8"))
        report = MODULE.run(str(self.PLAN), str(self.SNAPSHOT), str(self.PROJECT))
        # This fixture is a plan this project really applied. A rename consumes its source path and
        # occupies its target path, so on the machine that ran that apply the same plan must now be
        # refused for exactly those renames and for nothing else; a machine that has not applied it
        # yet must still see a completely clean report. Counting the already-consumed renames lets
        # both states assert the same invariant, instead of asserting "no errors" and then breaking
        # the moment the reviewed plan is actually executed.
        renames = (plan.get("textureRenames") or []) + (plan.get("spriteAtlasRenames") or [])
        consumed = [rename for rename in renames
                    if not (self.PROJECT / rename["from"]).exists()]
        collisions = [error for error in report.errors if "改名目标已存在" in error]
        missing = [warning for warning in report.warnings if "改名来源在磁盘上不存在" in warning]
        self.assertEqual(len(consumed), len(collisions), msg="\n".join(report.errors))
        self.assertEqual(len(collisions), len(report.errors), msg="\n".join(report.errors))
        self.assertEqual(len(consumed), len(missing), msg="\n".join(report.warnings))
        self.assertEqual(len(missing), len(report.warnings), msg="\n".join(report.warnings))


if __name__ == "__main__":
    unittest.main(verbosity=2)
