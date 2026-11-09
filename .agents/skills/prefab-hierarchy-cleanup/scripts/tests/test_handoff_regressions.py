"""Regressions from the 2026-11-09 external-session handoff."""
import json
import sys
import tempfile
import unittest
from unittest.mock import patch, Mock
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from validate_plan_locally import lint
from unity_prefab_yaml import PrefabDoc, read_overrides
from preflight_v2 import PredictedNode, build_predicted_candidates, Preflight, check_private_assets
from read_unity_selection import run_payload
from test_v2_plan_tools import SNAPSHOT, load_nodes, plan
from test_audit_prefab_preservation import MODULE as audit, before_dump, after_dump, stateful_plan


class MoveRegressionTests(unittest.TestCase):
    def test_existing_parent_accepts_same_parent_reorder(self):
        p = plan(wrappers=[], moves=[{
            "source": "node:n000002", "destination": "node:n000000", "siblingIndex": 0}],
            verify={})
        self.assertEqual([], lint(p, load_nodes(SNAPSHOT))[0])

    def test_unknown_destination_and_wrapper_still_rejected(self):
        for dest in ("node:missing", "@missing"):
            p = plan(moves=[{"source": "node:n000001", "destination": dest, "siblingIndex": 0}])
            self.assertTrue(lint(p, load_nodes(SNAPSHOT))[0])

    def test_move_cycle_is_rejected(self):
        p = plan(moves=[{"source": "node:n000000", "destination": "node:n000001", "siblingIndex": 0}])
        self.assertTrue(lint(p, load_nodes(SNAPSHOT))[0])


class YamlRegressionTests(unittest.TestCase):
    def test_multiline_target_override_is_parsed(self):
        body = """    - target: {fileID: 10, guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
        type: 3}
      propertyPath: m_Name
      value: '[UpgradeCard_4]'
      objectReference: {fileID: 0}
"""
        self.assertEqual("[UpgradeCard_4]", read_overrides(body)["m_Name"]["value"])

    def test_root_name_uses_source_root_object_not_first_or_last_override(self):
        source = """%YAML 1.1
--- !u!1 &10
GameObject:
  m_Name: OriginalRoot
--- !u!224 &20
RectTransform:
  m_GameObject: {fileID: 10}
  m_Father: {fileID: 0}
"""
        for wrapped in (False, True):
            with self.subTest(wrapped=wrapped), tempfile.TemporaryDirectory() as folder:
                path = Path(folder) / "Source.prefab"
                path.write_text(source, encoding="utf-8")
                modifications = ""
                for target, value in ((11, "ChildBefore"), (10, r'"[\u5347\u7ea7_4]"'), (12, "ChildAfter")):
                    mapping = "{fileID: %s, guid: %s,%stype: 3}" % (
                        target, "a" * 32, "\n        " if wrapped else " ")
                    modifications += "    - target: %s\n      propertyPath: m_Name\n      value: %s\n      objectReference: {fileID: 0}\n" % (mapping, value)
                blocks = [
                    ('1', '1', 'GameObject:\n  m_Name: Host\n', False),
                    ('224', '2', 'RectTransform:\n  m_GameObject: {fileID: 1}\n  m_Father: {fileID: 0}\n  m_Children:\n  - {fileID: 3}\n', False),
                    ('224', '3', 'RectTransform:\n  m_PrefabInstance: {fileID: 4}\n  m_CorrespondingSourceObject: {fileID: 20, guid: %s, type: 3}\n' % ('a' * 32), True),
                    ('1001', '4', 'PrefabInstance:\n  m_SourcePrefab: {fileID: 100100000, guid: %s, type: 3}\n  m_TransformParent: {fileID: 2}\n%s' % ('a' * 32, modifications), False),
                ]
                doc = PrefabDoc("host", "", "", blocks, {'a' * 32: str(path)})
                self.assertEqual("[升级_4]", doc.prefab_instances[0]['root_name'])


class CandidateRegressionTests(unittest.TestCase):
    def test_six_rows_three_structures_include_complete_advisory_subsets(self):
        frame = ([0.5, 0.5], [0.5, 0.5], [0.5, 0.5])
        nodes = {'root': PredictedNode('root', '[Cards]', ['Rect'], frame, None, 'root', 0)}
        for i in range(6):
            key = 'card%s' % i
            nodes[key] = PredictedNode(key, '[Card_%s]' % i, ['Rect'], frame, None, key, i)
            nodes['root'].children.append(key)
            for j in range(i // 2 + 1):
                child = key + '/child%s' % j
                nodes[child] = PredictedNode(child, 'Common' if j == 0 else 'Part%s' % j,
                                             ['Image'], frame, None, child, j)
                nodes[key].children.append(child)
        candidates = build_predicted_candidates(nodes)
        self.assertEqual(['numbered_repeated'] + ['numbered_structure_subset'] * 3,
                         [c['kind'] for c in candidates])
        self.assertEqual(['stateful', 'component', 'component', 'component'], [c['recommendedMode'] for c in candidates])
        self.assertEqual([True, False, False, False], [c['requiresExtraction'] for c in candidates])
        self.assertEqual([['card0', 'card1'], ['card2', 'card3'], ['card4', 'card5']], [c['sources'] for c in candidates[1:]])
        nodes['card0'].nested = True
        self.assertEqual([], build_predicted_candidates(nodes))


class AtlasDirectoryRegressionTests(unittest.TestCase):
    def test_path_segment_boundaries_match_native_rule(self):
        for directory, accepted in (("Prefab/Private", True), ("Atlas", True), ("Atlas/Sub", True),
                                    ("PrefabOther", False), ("AtlasOther", False), ("Elsewhere", False)):
            with self.subTest(directory=directory):
                report = Preflight()
                check_private_assets(report, {'prefabName': 'DemoView',
                    'prefabAssetPath': 'Assets/Demo/Prefab/DemoView.prefab',
                    'spriteAtlasRenames': [{'from': 'Assets/Demo/' + directory + '/old.spriteatlas', 'toName': 'DemoView'}]}, None)
                self.assertEqual(accepted, not report.errors, report.errors)


class DetachedQueryRegressionTests(unittest.TestCase):
    def cli(self, data):
        return Mock(returncode=0, stdout=json.dumps({'success': True, 'data': data}), stderr='')

    def test_detached_job_submits_once_and_collects_existing_job(self):
        responses = [self.cli({'jobId': 'job1'}), self.cli({'state': 'running'}),
                     self.cli({'state': 'completed', 'result': {'success': True, 'result': 'owners=1', 'diagnostics': []}})]
        with patch('read_unity_selection.subprocess.run', side_effect=responses) as run, patch('read_unity_selection.time.sleep'):
            self.assertEqual(('owners=1', []), run_payload('project', 'unity', 'read.cs', 60, detached=True))
            self.assertIn('--detach', run.call_args_list[0].args[0])
            self.assertEqual(1, sum('eval_file' in call.args[0] for call in run.call_args_list))

    def test_failed_job_does_not_resubmit(self):
        with patch('read_unity_selection.subprocess.run', side_effect=[self.cli({'jobId': 'job1'}), self.cli({'state': 'failed', 'error': 'timeout'})]) as run:
            with self.assertRaisesRegex(SystemExit, 'jobId=job1'):
                run_payload('project', 'unity', 'read.cs', 60, detached=True)
            self.assertEqual(2, run.call_count)


class PreservationRegressionTests(unittest.TestCase):
    def test_active_state_descendant_displacement_fails_audit(self):
        after = after_dump()
        after["nodes"]["Root/[DayEntry_1]/[States]/[State_Default]/DayBg"]["worldRect"] = [-344, 236, -334, 246]
        self.assertTrue(audit.compare(before_dump(), after, stateful_plan(), quiet=True))

    def test_small_float_noise_is_tolerated(self):
        after = after_dump()
        after["nodes"]["Root/[DayEntry_1]/[States]/[State_Default]/DayBg"]["worldRect"][0] += 0.00001
        self.assertEqual([], audit.compare(before_dump(), after, stateful_plan(), quiet=True))


if __name__ == "__main__":
    unittest.main()
