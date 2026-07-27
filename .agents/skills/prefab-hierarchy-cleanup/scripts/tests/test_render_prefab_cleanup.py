import json
import sys
import unittest
from pathlib import Path


SCRIPT_DIRECTORY = Path(__file__).resolve().parents[1]
SKILL_DIRECTORY = SCRIPT_DIRECTORY.parent
sys.path.insert(0, str(SCRIPT_DIRECTORY))

from render_prefab_cleanup import normalize_plan, render


class RenderPrefabCleanupTests(unittest.TestCase):
    def test_legacy_plan_without_component_family_decisions_still_loads(self):
        raw_plan = json.loads(
            (SKILL_DIRECTORY / "examples" / "sample-plan.json").read_text(encoding="utf-8")
        )
        raw_plan.pop("componentFamilyDecisions", None)

        normalize_plan(raw_plan, "verify")

    def test_component_family_decision_requires_a_matching_extraction(self):
        raw_plan = json.loads(
            (SKILL_DIRECTORY / "examples" / "sample-plan.json").read_text(encoding="utf-8")
        )
        raw_plan["componentFamilyDecisions"] = [
            {
                "parent": "RewardPanel/Root",
                "sources": ["RewardPanel/Root/Title", "RewardPanel/Root/Subtitle"],
                "mode": "stateful",
                "extractionId": "reward_marker",
                "reason": "The items require an explicit stateful component.",
            }
        ]

        with self.assertRaisesRegex(ValueError, "matching stateful extraction"):
            normalize_plan(raw_plan, "verify")

    def test_wrapper_reference_cannot_address_a_child_path(self):
        raw_plan = json.loads(
            (SKILL_DIRECTORY / "examples" / "sample-plan.json").read_text(encoding="utf-8")
        )
        raw_plan["wrappers"] = [
            {
                "id": "content",
                "parent": "RewardPanel/Root",
                "name": "[Content]",
                "siblingIndex": 0,
            }
        ]
        raw_plan["renames"] = [
            {"target": "@content/Title", "name": "TitleLabel"}
        ]

        with self.assertRaisesRegex(ValueError, "must be exactly @wrapperId"):
            normalize_plan(raw_plan, "verify")

    def test_preflight_checks_source_paths_without_rendering_an_apply_operation(self):
        raw_plan = json.loads(
            (SKILL_DIRECTORY / "examples" / "sample-plan.json").read_text(encoding="utf-8")
        )
        raw_plan["renames"] = [
            {"target": "RewardPanel/Root/MissingLabel", "name": "RewardLabel"}
        ]

        generated = render(normalize_plan(raw_plan, "preflight"), "preflight")

        self.assertIn(
            'AssertPlanPath(root, "renames[0].target", "RewardPanel/Root/MissingLabel");',
            generated,
        )
        self.assertIn('return "PREFLIGHT_OK";', generated)
        self.assertNotIn("PrefabUtility.SaveAsPrefabAsset(root, outputPath)", generated)
        self.assertNotIn("AssetDatabase.RenameAsset", generated)

    def test_preflight_returns_exact_candidate_source_paths_for_a_missing_node(self):
        raw_plan = json.loads(
            (SKILL_DIRECTORY / "examples" / "sample-plan.json").read_text(encoding="utf-8")
        )
        raw_plan["moves"] = [
            {
                "source": "RewardPanel/Root/MissingLabel",
                "destination": "RewardPanel/Root",
                "siblingIndex": 0,
            }
        ]

        generated = render(normalize_plan(raw_plan, "preflight"), "preflight")

        self.assertIn("string PlanPath(Transform node)", generated)
        self.assertIn("string FindCandidateSourcePaths(GameObject root, string path)", generated)
        self.assertIn("Candidate source paths: ", generated)
        self.assertIn("if (node.name.Length < 3) continue;", generated)

    def test_preflight_simulates_moves_before_empty_container_removal(self):
        raw_plan = json.loads(
            (SKILL_DIRECTORY / "examples" / "sample-plan.json").read_text(encoding="utf-8")
        )
        raw_plan["moves"] = [
            {
                "source": "RewardPanel/Root/LegacyGroup/Title",
                "destination": "@content",
                "siblingIndex": 0,
            }
        ]
        raw_plan["emptyContainerRemovals"] = [
            {"source": "RewardPanel/Root/LegacyGroup"}
        ]

        generated = render(normalize_plan(raw_plan, "preflight"), "preflight")

        capture = (
            'var preflightMoveSource0 = FindByPath(root, '
            '"RewardPanel/Root/LegacyGroup/Title").transform;'
        )
        move = "preflightMoveSource0.SetParent(preflightWrapper0.transform, true);"
        removal = "RemoveEmptyContainer(root.transform, preflightRemoval0);"
        self.assertIn(capture, generated)
        self.assertIn(move, generated)
        self.assertIn(removal, generated)
        self.assertIn("var preflightRemovalErrors = new List<string>();", generated)
        self.assertIn(
            'preflightRemovalErrors.Add("emptyContainerRemovals[0]: " + '
            "preflightRemovalError0.Message);",
            generated,
        )
        self.assertLess(generated.index(move), generated.index(removal))
        self.assertNotIn("PrefabUtility.SaveAsPrefabAsset(root, outputPath)", generated)

    def test_verify_ignores_preexisting_missing_sprites_inside_nested_prefabs(self):
        raw_plan = json.loads(
            (SKILL_DIRECTORY / "examples" / "sample-plan.json").read_text(encoding="utf-8")
        )

        generated = render(normalize_plan(raw_plan, "verify"), "verify")

        self.assertIn("IsNestedPrefabContent", generated)
        self.assertIn("ignoredNestedMissingSprites", generated)
        self.assertIn("Image has a missing Sprite", generated)
        self.assertIn("IsNestedPrefabContent(image.transform, reopened.transform)", generated)

    def test_verify_reports_non_blocking_issues_without_throwing_by_default(self):
        raw_plan = json.loads(
            (SKILL_DIRECTORY / "examples" / "sample-plan.json").read_text(encoding="utf-8")
        )

        generated = render(normalize_plan(raw_plan, "verify"), "verify")

        self.assertIn("if (reopened == null) throw new InvalidOperationException", generated)
        self.assertIn("catch (Exception verificationError)", generated)
        self.assertIn(
            'return "VERIFY_WARN issue=" + verificationError.Message.Replace("\\r", " ").Replace("\\n", " ");',
            generated,
        )


if __name__ == "__main__":
    unittest.main()
