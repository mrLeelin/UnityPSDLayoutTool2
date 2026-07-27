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
