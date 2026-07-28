import json
import sys
import unittest
from pathlib import Path


SCRIPT_DIRECTORY = Path(__file__).resolve().parents[1]
SKILL_DIRECTORY = SCRIPT_DIRECTORY.parent
sys.path.insert(0, str(SCRIPT_DIRECTORY))

from render_prefab_cleanup import normalize_plan, render
from find_prefab_component_candidates import Node, numbered_component_candidates


class RenderPrefabCleanupTests(unittest.TestCase):
    def load_plan(self, filename):
        return json.loads((SKILL_DIRECTORY / "plans" / filename).read_text(encoding="utf-8"))

    def test_numbered_family_candidate_keeps_size_variants_and_recommends_stateful(self):
        root = self.make_candidate_node("Root", 0, 1)
        parent = self.make_candidate_node("Root/[DayCards]", 0, 3)
        root.children.append(parent)
        cards = [
            self.make_candidate_node("Root/[DayCards]/[DayCard_1]", 0, 1, (128.0, 223.0)),
            self.make_candidate_node("Root/[DayCards]/[DayCard_2]", 1, 1, (128.0, 226.0)),
            self.make_candidate_node("Root/[DayCards]/[DayCard_3]", 2, 1, (128.0, 272.0)),
        ]
        parent.children.extend(cards)
        cards[0].children.append(self.make_candidate_node(cards[0].path + "/Background", 0, 0))
        cards[1].children.append(self.make_candidate_node(cards[1].path + "/Background", 0, 0))
        cards[2].children.append(self.make_candidate_node(cards[2].path + "/Background", 0, 0))
        state_member = self.make_candidate_node(cards[2].path + "/Lock", 0, 1)
        state_member.children.append(self.make_candidate_node(state_member.path + "/Overlay", 0, 0))
        cards[2].children.append(state_member)

        candidates = numbered_component_candidates(root)

        self.assertEqual(len(candidates), 1)
        candidate = candidates[0]
        self.assertEqual(candidate["suggestedAssetName"], "DayCard")
        self.assertTrue(candidate["requiresExtraction"])
        self.assertTrue(candidate["sizeDeltaOverridesAllowed"])
        self.assertEqual(candidate["recommendedMode"], "stateful")
        self.assertEqual(candidate["instances"], [card.path for card in cards])

    def test_numbered_family_without_common_direct_members_is_optional_variant(self):
        root = self.make_candidate_node("Root", 0, 1)
        parent = self.make_candidate_node("Root/[TaskItems]", 0, 3)
        root.children.append(parent)
        items = [
            self.make_candidate_node("Root/[TaskItems]/[TaskItem_1]", 0, 1),
            self.make_candidate_node("Root/[TaskItems]/[TaskItem_2]", 1, 2),
            self.make_candidate_node("Root/[TaskItems]/[TaskItem_3]", 2, 1),
        ]
        parent.children.extend(items)
        items[0].children.append(self.make_candidate_node(items[0].path + "/Background", 0, 0))
        items[1].children.extend(
            [
                self.make_candidate_node(items[1].path + "/Content", 0, 0),
                self.make_candidate_node(items[1].path + "/Toggle", 1, 0),
            ]
        )
        items[2].children.append(self.make_candidate_node(items[2].path + "/LockIcon", 0, 0))

        candidates = numbered_component_candidates(root)

        self.assertEqual(len(candidates), 1)
        candidate = candidates[0]
        self.assertFalse(candidate["requiresExtraction"])
        self.assertEqual(candidate["recommendedMode"], "variant")

    def test_numbered_family_includes_matching_bare_index_sibling(self):
        root = self.make_candidate_node("Root", 0, 1)
        parent = self.make_candidate_node("Root/[Tasks]", 0, 5)
        root.children.append(parent)
        items = [
            self.make_candidate_node("Root/[Tasks]/[Task_5]", 0, 1),
            self.make_candidate_node("Root/[Tasks]/[Task_4]", 1, 1),
            self.make_candidate_node("Root/[Tasks]/[Task_3]", 2, 1),
            self.make_candidate_node("Root/[Tasks]/[Task_2]", 3, 1),
            self.make_candidate_node("Root/[Tasks]/1", 4, 1),
        ]
        parent.children.extend(items)
        for index, item in enumerate(items):
            item.children.append(self.make_candidate_node(item.path + f"/State{index}", 0, 0))

        candidates = numbered_component_candidates(root)

        self.assertEqual(len(candidates), 1)
        candidate = candidates[0]
        self.assertEqual(candidate["suggestedAssetName"], "Task")
        self.assertEqual(candidate["instanceCount"], 5)
        self.assertEqual(candidate["instances"], [item.path for item in items])

    @staticmethod
    def make_candidate_node(path, sibling, child_count, size=(128.0, 223.0)):
        return Node(
            path=path,
            depth=path.count("/"),
            sibling=sibling,
            child_count=child_count,
            nested_prefab=False,
            components=("RectTransform",),
            anchor_min=(0.5, 0.5),
            anchor_max=(0.5, 0.5),
            pivot=(0.5, 0.5),
            anchored_position=(0.0, 0.0),
            size_delta=size,
        )

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

    def test_combined_hierarchy_and_non_overlapping_extraction_modes_are_allowed(self):
        plan = self.load_plan("seven-day-task-view-task-item-variants.in-place.plan.json")
        stateful_plan = self.load_plan("seven-day-task-view-day-reward-items.in-place.plan.json")
        plan["statefulComponentExtractions"] = stateful_plan["statefulComponentExtractions"]
        variant = plan["variantComponentExtractions"][0]
        stateful = plan["statefulComponentExtractions"][0]
        plan["componentFamilyDecisions"] = [
            {
                "parent": variant["template"].rsplit("/", 1)[0],
                "sources": [instance["source"] for instance in variant["instances"]],
                "mode": "variant",
                "extractionId": variant["id"],
                "reason": "Visible task rows are explicit visual variants.",
            },
            {
                "parent": stateful["template"].rsplit("/", 1)[0],
                "sources": [instance["source"] for instance in stateful["instances"]],
                "mode": "stateful",
                "extractionId": stateful["id"],
                "reason": "Day rewards contain shared members and visual states.",
            },
        ]
        plan["renames"] = [
            {
                "target": "SevenDayTaskView/AnimatorRoot/[TaskList]",
                "name": "[TaskList]",
            }
        ]

        normalized = normalize_plan(plan, "apply")

        self.assertEqual(len(normalized["variant_component_extractions"]), 1)
        self.assertEqual(len(normalized["stateful_component_extractions"]), 1)
        self.assertEqual(len(normalized["renames"]), 1)

    def test_component_assets_are_limited_to_the_target_prefab_common_directory(self):
        plan = self.load_plan("seven-day-task-view-task-item-variants.in-place.plan.json")
        plan["variantComponentExtractions"][0]["assetPath"] = "Assets/UI/Other/TaskItem.prefab"

        with self.assertRaisesRegex(ValueError, "directly under .*Prefab/Common"):
            normalize_plan(plan, "apply")

    def test_stateful_component_allows_an_explicit_all_common_state(self):
        plan = self.load_plan("seven-day-task-view-day-reward-items.in-place.plan.json")
        extraction = plan["statefulComponentExtractions"][0]
        all_common_state = extraction["states"][1]
        all_common_state["members"] = []
        for instance in extraction["instances"]:
            if instance["state"] == all_common_state["id"]:
                instance["stateSourceNames"] = []
        plan["componentFamilyDecisions"] = [
            {
                "parent": extraction["template"].rsplit("/", 1)[0],
                "sources": [instance["source"] for instance in extraction["instances"]],
                "mode": "stateful",
                "extractionId": extraction["id"],
                "reason": "The explicit all-common state has no distinct visual members.",
            }
        ]

        normalized = normalize_plan(plan, "apply")
        generated = render(normalized, "apply")

        self.assertEqual(
            normalized["stateful_component_extractions"][0]["states"][1]["members"],
            [],
        )
        self.assertIn("new string[0]", generated)
        self.assertNotIn("new[] {  }", generated)

    def test_reapply_mode_reuses_existing_component_assets_without_recreating_them(self):
        plan = self.load_plan("seven-day-task-view-task-item-variants.in-place.plan.json")
        extraction = plan["variantComponentExtractions"][0]
        plan["componentFamilyDecisions"] = [{
            "parent": extraction["template"].rsplit("/", 1)[0],
            "sources": [instance["source"] for instance in extraction["instances"]],
            "mode": "variant",
            "extractionId": extraction["id"],
            "reason": "replay test",
        }]

        normalized = normalize_plan(plan, "reapply")
        generated = render(normalized, "reapply")

        self.assertIn("LoadExistingComponentPrefab", generated)
        self.assertIn("?? CreateVariantComponentPrefab(", generated)
        self.assertEqual(
            generated.count("Variant component Prefab target already exists"),
            1,
        )
        self.assertIn("Candidate source paths", generated)
        self.assertIn(
            'AssertPlanPath(root, "variantComponentExtractions[0].template"',
            generated,
        )
        self.assertIn("ReplaceVariantSourceWithComponent", generated)

    def test_reapply_mode_accepts_existing_stateful_asset(self):
        plan = self.load_plan("seven-day-task-view-day-reward-items.in-place.plan.json")
        extraction = plan["statefulComponentExtractions"][0]
        plan["componentFamilyDecisions"] = [{
            "parent": extraction["template"].rsplit("/", 1)[0],
            "sources": [instance["source"] for instance in extraction["instances"]],
            "mode": "stateful",
            "extractionId": extraction["id"],
            "reason": "replay test",
        }]

        normalized = normalize_plan(plan, "reapply")
        generated = render(normalized, "reapply")

        self.assertIn("LoadExistingComponentPrefab", generated)
        self.assertIn("?? CreateStatefulComponentPrefab(", generated)
        self.assertEqual(
            generated.count("Stateful component Prefab target already exists"),
            1,
        )
        self.assertIn("ReplaceStatefulSourceWithComponent", generated)

    def test_reapply_temp_target_keeps_component_ownership_bound_to_original_prefab(self):
        plan = self.load_plan("seven-day-task-view-task-item-variants.in-place.plan.json")
        extraction = plan["variantComponentExtractions"][0]
        plan["componentFamilyDecisions"] = [{
            "parent": extraction["template"].rsplit("/", 1)[0],
            "sources": [instance["source"] for instance in extraction["instances"]],
            "mode": "variant",
            "extractionId": extraction["id"],
            "reason": "replay ownership test",
        }]
        original_target = plan["prefabAssetPath"]
        temporary_target = "Assets/PSDLayoutTool2Settings/HierarchyReplayTemp/candidate.prefab"
        plan["replaySourcePrefabAssetPath"] = original_target
        plan["prefabAssetPath"] = temporary_target
        plan["output"]["assetPath"] = temporary_target

        normalized = normalize_plan(plan, "reapply")

        self.assertEqual(normalized["prefab_path"], temporary_target)
        self.assertEqual(
            normalized["variant_component_extractions"][0]["assetPath"],
            extraction["assetPath"],
        )

    def test_reapply_asset_rename_preserves_formal_guid_and_remaps_candidate_references(self):
        raw_plan = json.loads(
            (SKILL_DIRECTORY / "examples" / "sample-plan.json").read_text(encoding="utf-8")
        )
        raw_plan["textureRenames"] = [
            {
                "from": "Assets/UI/Prefab/Texture/raw_icon.png",
                "toName": "RewardPanelView_Icon",
                "expectedGuid": "0123456789abcdef0123456789abcdef",
            }
        ]
        raw_plan["spriteAtlasRenames"] = []

        generated = render(normalize_plan(raw_plan, "reapply"), "reapply")

        self.assertIn(
            'AssertGuid("Assets/UI/Prefab/Texture/RewardPanelView_Icon.png", '
            '"0123456789abcdef0123456789abcdef");',
            generated,
        )
        self.assertNotIn("Rename target already exists", generated)
        self.assertIn("RefreshRenamedAsset", generated)
        self.assertIn("RemapAssetReferences(root", generated)
        self.assertIn("EditorUtility.CopySerialized(sourceImporter, targetImporter)", generated)
        self.assertIn("targetImporter.SaveAndReimport()", generated)
        self.assertIn(
            'AssetDatabase.DeleteAsset("Assets/UI/Prefab/Texture/raw_icon.png")',
            generated,
        )
        self.assertIn("if (replayAssetAlreadyRenamed0)", generated)
        self.assertIn("var replayRenameError0 = AssetDatabase.RenameAsset", generated)

    def test_apply_saves_prefab_only_after_transactional_asset_renames(self):
        raw_plan = json.loads(
            (SKILL_DIRECTORY / "examples" / "sample-plan.json").read_text(encoding="utf-8")
        )

        generated = render(normalize_plan(raw_plan, "apply"), "apply")

        first_rename = generated.index("var renameError0 = AssetDatabase.RenameAsset")
        prefab_save = generated.index("PrefabUtility.SaveAsPrefabAsset(root, outputPath)")
        self.assertLess(first_rename, prefab_save)
        self.assertIn("completedAssetRenames.Add", generated)
        self.assertIn("for (var rollbackIndex = completedAssetRenames.Count - 1", generated)
        self.assertIn("Asset rename rollback failed", generated)


if __name__ == "__main__":
    unittest.main()
