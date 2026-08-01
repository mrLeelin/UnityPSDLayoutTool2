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
    def test_runner_falls_back_to_npx_when_global_uloop_is_unavailable(self):
        runner = (SCRIPT_DIRECTORY / "run_prefab_hierarchy_cleanup.ps1").read_text(
            encoding="utf-8"
        )

        self.assertIn("function Invoke-UloopCli", runner)
        self.assertIn('Get-Command -Name "uloop.cmd", "uloop"', runner)
        self.assertIn('Get-Command -Name "npx.cmd", "npx"', runner)
        self.assertIn('"uloop-cli@2.2.0"', runner)
        self.assertIn("Invoke-UloopCli -Arguments $cliArguments", runner)
        self.assertIn("$script:UloopExitCode = $LASTEXITCODE", runner)
        self.assertIn("$unityExitCode = $script:UloopExitCode", runner)
        self.assertNotIn("& uloop execute-dynamic-code", runner)

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

        candidates = self.numbered_families(numbered_component_candidates(root))

        self.assertEqual(len(candidates), 1)
        candidate = candidates[0]
        self.assertEqual(candidate["suggestedAssetName"], "DayCard")
        self.assertTrue(candidate["requiresExtraction"])
        self.assertTrue(candidate["sizeDeltaOverridesAllowed"])
        self.assertEqual(candidate["recommendedMode"], "stateful")
        self.assertEqual(candidate["instances"], [card.path for card in cards])

    def test_numbered_family_without_common_direct_members_is_mandatory_variant(self):
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

        candidates = self.numbered_families(numbered_component_candidates(root))

        self.assertEqual(len(candidates), 1)
        candidate = candidates[0]
        self.assertTrue(candidate["requiresExtraction"])
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

        candidates = self.numbered_families(numbered_component_candidates(root))

        self.assertEqual(len(candidates), 1)
        candidate = candidates[0]
        self.assertEqual(candidate["suggestedAssetName"], "Task")
        self.assertEqual(candidate["instanceCount"], 5)
        self.assertEqual(candidate["instances"], [item.path for item in items])

    def test_pure_bare_numbered_siblings_form_a_component_family(self):
        root = self.make_candidate_node("Root", 0, 1)
        parent = self.make_candidate_node("Root/[Tasks]", 0, 5)
        root.children.append(parent)
        items = [
            self.make_candidate_node(f"Root/[Tasks]/{index}", sibling, 1)
            for sibling, index in enumerate((5, 4, 3, 2, 1))
        ]
        parent.children.extend(items)
        for index, item in enumerate(items):
            item.children.append(self.make_candidate_node(item.path + f"/State{index}", 0, 0))

        candidates = self.numbered_families(numbered_component_candidates(root))

        self.assertEqual(len(candidates), 1)
        candidate = candidates[0]
        self.assertEqual(candidate["suggestedAssetName"], "Task")
        self.assertTrue(candidate["requiresExtraction"])
        self.assertEqual(candidate["instanceCount"], 5)
        self.assertEqual(candidate["instances"], [item.path for item in items])

    def test_numbered_family_includes_bare_sibling_when_its_index_is_already_named(self):
        root = self.make_candidate_node("Root", 0, 1)
        parent = self.make_candidate_node("Root/[TaskItems]", 0, 4)
        root.children.append(parent)
        items = [
            self.make_candidate_node("Root/[TaskItems]/[TaskItem_1]", 0, 1),
            self.make_candidate_node("Root/[TaskItems]/[TaskItem_2]", 1, 1),
            self.make_candidate_node("Root/[TaskItems]/[TaskItem_3]", 2, 1),
            self.make_candidate_node("Root/[TaskItems]/1", 3, 2),
        ]
        parent.children.extend(items)
        for index, item in enumerate(items):
            item.children.append(self.make_candidate_node(item.path + f"/State{index}", 0, 0))
        items[3].children.append(self.make_candidate_node(items[3].path + "/Lock", 1, 0))

        candidates = self.numbered_families(numbered_component_candidates(root))

        self.assertEqual(len(candidates), 1)
        candidate = candidates[0]
        self.assertEqual(candidate["suggestedAssetName"], "TaskItem")
        self.assertEqual(candidate["instanceCount"], 4)
        self.assertEqual(candidate["recommendedMode"], "variant")
        self.assertEqual(candidate["instances"], [item.path for item in items])

    def test_partially_identical_family_also_reports_structure_subsets(self):
        root = self.make_candidate_node("Root", 0, 1)
        parent = self.make_candidate_node("Root/[StorySection]", 0, 3)
        root.children.append(parent)
        cards = [
            self.make_candidate_node("Root/[StorySection]/[StoryCard_1]", 0, 1),
            self.make_candidate_node("Root/[StorySection]/[StoryCard_2]", 1, 1),
            self.make_candidate_node("Root/[StorySection]/[StoryCard_3]", 2, 2),
        ]
        parent.children.extend(cards)
        for card in cards:
            card.children.append(self.make_candidate_node(card.path + "/Background", 0, 0))
        cards[2].children.append(self.make_candidate_node(cards[2].path + "/Badge", 1, 0))

        candidates = numbered_component_candidates(root)
        subsets = [
            candidate
            for candidate in candidates
            if candidate["kind"] == "numbered_structure_subset"
        ]

        self.assertEqual(len(self.numbered_families(candidates)), 1)
        self.assertEqual(len(subsets), 2)
        self.assertEqual(subsets[0]["instances"], [cards[0].path, cards[1].path])
        self.assertEqual(subsets[0]["recommendedMode"], "component")
        self.assertEqual(subsets[0]["familyCandidateId"], "numbered_001")
        self.assertEqual(subsets[1]["instances"], [cards[2].path])
        self.assertEqual(subsets[1]["recommendedMode"], "skip")
        self.assertFalse(subsets[1]["requiresExtraction"])

    def test_structure_subset_is_not_forced_when_family_itself_is_extractable(self):
        root = self.make_candidate_node("Root", 0, 1)
        parent = self.make_candidate_node("Root/[StorySection]", 0, 3)
        root.children.append(parent)
        cards = [
            self.make_candidate_node("Root/[StorySection]/[StoryCard_1]", 0, 1),
            self.make_candidate_node("Root/[StorySection]/[StoryCard_2]", 1, 1),
            self.make_candidate_node("Root/[StorySection]/[StoryCard_3]", 2, 2),
        ]
        parent.children.extend(cards)
        for card in cards:
            card.children.append(self.make_candidate_node(card.path + "/Background", 0, 0))
        cards[2].children.append(self.make_candidate_node(cards[2].path + "/Badge", 1, 0))

        candidates = numbered_component_candidates(root)
        family = self.numbered_families(candidates)[0]
        subsets = [
            candidate
            for candidate in candidates
            if candidate["kind"] == "numbered_structure_subset"
        ]

        # The family and its subsets claim the same sources, so a forced subset would
        # make the two obligations unsatisfiable at the same time.
        self.assertTrue(family["requiresExtraction"])
        self.assertTrue(all(not subset["requiresExtraction"] for subset in subsets))

    def test_identical_family_reports_no_structure_subsets(self):
        root = self.make_candidate_node("Root", 0, 1)
        parent = self.make_candidate_node("Root/[Coins]", 0, 3)
        root.children.append(parent)
        coins = [
            self.make_candidate_node("Root/[Coins]/[Coin_1]", 0, 1),
            self.make_candidate_node("Root/[Coins]/[Coin_2]", 1, 1),
            self.make_candidate_node("Root/[Coins]/[Coin_3]", 2, 1),
        ]
        parent.children.extend(coins)
        for coin in coins:
            coin.children.append(self.make_candidate_node(coin.path + "/Icon", 0, 0))

        candidates = numbered_component_candidates(root)

        self.assertEqual(len(candidates), 1)
        self.assertEqual(candidates[0]["recommendedMode"], "component")

    @staticmethod
    def numbered_families(candidates):
        return [
            candidate
            for candidate in candidates
            if candidate["kind"] == "numbered_repeated"
        ]

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

    def test_family_and_subset_boundaries_cannot_both_claim_one_source(self):
        raw_plan = json.loads(
            (SKILL_DIRECTORY / "examples" / "sample-plan.json").read_text(encoding="utf-8")
        )
        family_sources = [
            "RewardPanel/Root/[Card_1]",
            "RewardPanel/Root/[Card_2]",
            "RewardPanel/Root/[Card_3]",
        ]
        raw_plan["componentExtractions"] = [
            {
                "id": "ex_family",
                "template": family_sources[0],
                "assetPath": "Assets/UI/Common/Card.prefab",
                "instances": list(family_sources),
            },
            {
                "id": "ex_subset",
                "template": family_sources[0],
                "assetPath": "Assets/UI/Common/CardSmall.prefab",
                "instances": family_sources[:2],
            },
        ]
        raw_plan["componentFamilyDecisions"] = [
            {
                "candidateId": "family_001",
                "parent": "RewardPanel/Root",
                "sources": list(family_sources),
                "mode": "component",
                "reason": "family-level boundary",
                "extractionId": "ex_family",
            },
            {
                "candidateId": "family_001_s01",
                "parent": "RewardPanel/Root",
                "sources": family_sources[:2],
                "mode": "component",
                "reason": "subset boundary",
                "extractionId": "ex_subset",
            },
        ]

        with self.assertRaisesRegex(ValueError, "appears more than once"):
            normalize_plan(raw_plan, "verify")

    def make_required_family_plan(self, sources=None):
        raw_plan = json.loads(
            (SKILL_DIRECTORY / "examples" / "sample-plan.json").read_text(encoding="utf-8")
        )
        raw_plan["requiredComponentFamilies"] = [
            {
                "candidateId": "family_001",
                "parent": "RewardPanel/Root",
                "sources": sources
                or ["RewardPanel/Root/Title", "RewardPanel/Root/Subtitle"],
            }
        ]
        return raw_plan

    def test_required_component_family_must_be_covered_by_a_decision(self):
        raw_plan = self.make_required_family_plan()
        raw_plan["componentFamilyDecisions"] = []

        with self.assertRaisesRegex(
            ValueError, "must cover required candidate family_001"
        ):
            normalize_plan(raw_plan, "verify")

    def test_required_component_family_cannot_be_skipped(self):
        raw_plan = self.make_required_family_plan()
        raw_plan["componentFamilyDecisions"] = [
            {
                "parent": "RewardPanel/Root",
                "sources": ["RewardPanel/Root/Title", "RewardPanel/Root/Subtitle"],
                "mode": "skip",
                "reason": "They only share a numbered name prefix.",
            }
        ]

        with self.assertRaisesRegex(
            ValueError, "family_001 must not use skip"
        ):
            normalize_plan(raw_plan, "verify")

    def test_required_component_family_matches_decision_sources_in_any_order(self):
        raw_plan = self.make_required_family_plan(
            sources=["RewardPanel/Root/Subtitle", "RewardPanel/Root/Title"]
        )
        raw_plan["componentFamilyDecisions"] = [
            {
                "parent": "RewardPanel/Root",
                "sources": ["RewardPanel/Root/Title", "RewardPanel/Root/Subtitle"],
                "mode": "skip",
                "reason": "They only share a numbered name prefix.",
            }
        ]

        with self.assertRaisesRegex(
            ValueError, "family_001 must not use skip"
        ):
            normalize_plan(raw_plan, "verify")

    def test_required_component_family_parent_must_match_its_decision(self):
        raw_plan = self.make_required_family_plan()
        raw_plan["requiredComponentFamilies"][0]["parent"] = "RewardPanel"
        raw_plan["componentFamilyDecisions"] = [
            {
                "parent": "RewardPanel/Root",
                "sources": ["RewardPanel/Root/Title", "RewardPanel/Root/Subtitle"],
                "mode": "skip",
                "reason": "They only share a numbered name prefix.",
            }
        ]

        with self.assertRaisesRegex(
            ValueError, r"requiredComponentFamilies\[0\].parent must match"
        ):
            normalize_plan(raw_plan, "verify")

    def test_required_component_family_passes_when_it_is_extracted(self):
        raw_plan = self.make_required_family_plan()
        raw_plan["componentExtractions"] = [
            {
                "id": "reward_row",
                "template": "RewardPanel/Root/Title",
                "assetPath": "Assets/UI/Common/RewardRow.prefab",
                "instances": [
                    "RewardPanel/Root/Title",
                    "RewardPanel/Root/Subtitle",
                ],
            }
        ]
        raw_plan["componentFamilyDecisions"] = [
            {
                "parent": "RewardPanel/Root",
                "sources": ["RewardPanel/Root/Title", "RewardPanel/Root/Subtitle"],
                "mode": "component",
                "extractionId": "reward_row",
                "reason": "Both rows share an identical child structure.",
            }
        ]

        plan = normalize_plan(raw_plan, "verify")

        self.assertEqual(len(plan["required_component_families"]), 1)
        self.assertEqual(
            plan["required_component_families"][0]["candidateId"], "family_001"
        )

    def test_component_decision_sources_must_cover_the_extraction_instances(self):
        raw_plan = json.loads(
            (SKILL_DIRECTORY / "examples" / "sample-plan.json").read_text(encoding="utf-8")
        )
        raw_plan["componentExtractions"] = [
            {
                "id": "reward_row",
                "template": "RewardPanel/Root/Title",
                "assetPath": "Assets/UI/Common/RewardRow.prefab",
                "instances": [
                    "RewardPanel/Root/Title",
                    "RewardPanel/Root/Subtitle",
                ],
            }
        ]
        raw_plan["componentFamilyDecisions"] = [
            {
                "parent": "RewardPanel/Root",
                "sources": ["RewardPanel/Root/Title", "RewardPanel/Root/Footer"],
                "mode": "component",
                "extractionId": "reward_row",
                "reason": "Both rows share an identical child structure.",
            }
        ]

        with self.assertRaisesRegex(
            ValueError, "must exactly cover its extraction instances"
        ):
            normalize_plan(raw_plan, "verify")

    def test_required_component_family_needs_at_least_two_unique_sources(self):
        raw_plan = self.make_required_family_plan(
            sources=["RewardPanel/Root/Title", "RewardPanel/Root/Title"]
        )

        with self.assertRaisesRegex(ValueError, "at least two unique paths"):
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

    def test_preflight_rejects_component_extractions_with_different_structures(self):
        raw_plan = json.loads(
            (SKILL_DIRECTORY / "examples" / "sample-plan.json").read_text(encoding="utf-8")
        )
        sources = [
            "RewardPanel/Root/Title",
            "RewardPanel/Root/LegacyGroup",
        ]
        raw_plan["componentExtractions"] = [
            {
                "id": "title_or_legacy",
                "template": sources[0],
                "assetPath": "Assets/UI/Common/TitleOrLegacy.prefab",
                "instances": sources,
            }
        ]
        raw_plan["componentFamilyDecisions"] = [
            {
                "parent": "RewardPanel/Root",
                "sources": sources,
                "mode": "component",
                "extractionId": "title_or_legacy",
                "reason": "Preflight must validate structural compatibility.",
            }
        ]

        generated = render(normalize_plan(raw_plan, "preflight"), "preflight")

        self.assertIn(
            'var preflightComponentTemplate0 = FindByPath(root, "RewardPanel/Root/Title").transform;',
            generated,
        )
        self.assertIn(
            "var preflightComponentSignature0 = StructureSignature(preflightComponentTemplate0);",
            generated,
        )
        self.assertIn("Repeated unit structure differs for component extraction", generated)

    def test_preflight_runs_runtime_safety_checks_for_every_extraction_mode(self):
        component_plan = json.loads(
            (SKILL_DIRECTORY / "examples" / "sample-plan.json").read_text(encoding="utf-8")
        )
        component_sources = [
            "RewardPanel/Root/Title",
            "RewardPanel/Root/Subtitle",
        ]
        component_plan["componentExtractions"] = [
            {
                "id": "reward_label",
                "template": component_sources[0],
                "assetPath": "Assets/UI/Common/RewardLabel.prefab",
                "instances": component_sources,
            }
        ]
        component_plan["componentFamilyDecisions"] = [
            {
                "parent": "RewardPanel/Root",
                "sources": component_sources,
                "mode": "component",
                "extractionId": "reward_label",
                "reason": "Both labels share one reusable component contract.",
            }
        ]

        state_plan = json.loads(
            (SKILL_DIRECTORY / "examples" / "sample-plan.json").read_text(encoding="utf-8")
        )
        state_sources = [
            "RewardPanel/Root/Title",
            "RewardPanel/Root/Subtitle",
        ]
        state_extraction = {
            "id": "reward_state",
            "template": state_sources[0],
            "assetPath": "Assets/UI/Common/RewardState.prefab",
            "defaultState": "primary",
            "states": [
                {
                    "id": "primary",
                    "source": state_sources[0],
                    "name": "[State_Primary]",
                },
                {
                    "id": "secondary",
                    "source": state_sources[1],
                    "name": "[State_Secondary]",
                },
            ],
        }
        state_plan["stateComponentExtractions"] = [state_extraction]
        state_plan["componentFamilyDecisions"] = [
            {
                "parent": state_extraction["template"].rsplit("/", 1)[0],
                "sources": [state["source"] for state in state_extraction["states"]],
                "mode": "state",
                "extractionId": state_extraction["id"],
                "reason": "The sibling roots are mutually exclusive visual states.",
            }
        ]

        variant_plan = self.load_plan("seven-day-task-view-task-item-variants.in-place.plan.json")
        variant_extraction = variant_plan["variantComponentExtractions"][0]
        variant_plan["componentFamilyDecisions"] = [
            {
                "parent": variant_extraction["template"].rsplit("/", 1)[0],
                "sources": [instance["source"] for instance in variant_extraction["instances"]],
                "mode": "variant",
                "extractionId": variant_extraction["id"],
                "reason": "The visible rows share one component with observed variants.",
            }
        ]

        stateful_plan = self.load_plan("seven-day-task-view-day-reward-items.in-place.plan.json")
        stateful_extraction = stateful_plan["statefulComponentExtractions"][0]
        stateful_plan["componentFamilyDecisions"] = [
            {
                "parent": stateful_extraction["template"].rsplit("/", 1)[0],
                "sources": [instance["source"] for instance in stateful_extraction["instances"]],
                "mode": "stateful",
                "extractionId": stateful_extraction["id"],
                "reason": "The repeated units have common members and explicit states.",
            }
        ]

        cases = (
            (component_plan, "preflightComponentInstance0_0", False),
            (state_plan, "preflightStateSource0_0", False),
            (variant_plan, "preflightVariantInstance0_0", False),
            (stateful_plan, "preflightStatefulInstance0_0", True),
        )
        for raw_plan, variable, requires_member_check in cases:
            with self.subTest(variable=variable):
                generated = render(normalize_plan(raw_plan, "preflight"), "preflight")
                self.assertIn(f"AssertNoNestedPrefabRoots({variable});", generated)
                self.assertIn(
                    f"AssertNoExternalReferences(root.transform, {variable});",
                    generated,
                )
                if variable == "preflightVariantInstance0_0":
                    self.assertIn(
                        'AssertVariantSourceCompatible("task_item", "in_progress",',
                        generated,
                    )
                if requires_member_check:
                    self.assertIn(f"AssertDirectSourceMembers({variable},", generated)

    def test_variant_failures_include_actionable_structure_details(self):
        plan = self.load_plan("seven-day-task-view-task-item-variants.in-place.plan.json")
        extraction = plan["variantComponentExtractions"][0]
        plan["componentFamilyDecisions"] = [
            {
                "parent": extraction["template"].rsplit("/", 1)[0],
                "sources": [instance["source"] for instance in extraction["instances"]],
                "mode": "variant",
                "extractionId": extraction["id"],
                "reason": "The visible rows share one component with observed variants.",
            }
        ]

        generated = render(normalize_plan(plan, "apply"), "apply")

        self.assertIn("Variant extraction structure mismatch", generated)
        self.assertIn("extractionId=", generated)
        self.assertIn("instance=", generated)
        self.assertIn("state=", generated)
        self.assertIn("source=", generated)
        self.assertIn("expectedRectTransforms=", generated)
        self.assertIn("actualRectTransforms=", generated)

    def test_apply_wraps_all_component_prefab_writes_in_one_transaction(self):
        plan = self.load_plan("seven-day-task-view-task-item-variants.in-place.plan.json")
        extraction = plan["variantComponentExtractions"][0]
        plan["componentFamilyDecisions"] = [
            {
                "parent": extraction["template"].rsplit("/", 1)[0],
                "sources": [instance["source"] for instance in extraction["instances"]],
                "mode": "variant",
                "extractionId": extraction["id"],
                "reason": "The visible rows share one component with observed variants.",
            }
        ]

        generated = render(normalize_plan(plan, "apply"), "apply")

        begin_call = "var componentAssetTransaction = BeginComponentAssetTransaction("
        commit_call = "CommitComponentAssetTransaction(componentAssetTransaction);"
        rollback_call = "RollbackComponentAssetTransaction(componentAssetTransaction);"
        self.assertIn(begin_call, generated)
        self.assertIn(commit_call, generated)
        self.assertIn(rollback_call, generated)

        begin = generated.index(begin_call)
        create = generated.index("CreateVariantComponentPrefab(", begin)
        prefab_save = generated.index("PrefabUtility.SaveAsPrefabAsset(root, outputPath)")
        commit = generated.index(commit_call, prefab_save)
        self.assertLess(begin, create)
        self.assertLess(create, prefab_save)
        self.assertLess(prefab_save, commit)

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

    def test_variant_component_allows_multiple_instances_of_one_state(self):
        plan = self.load_plan("seven-day-task-view-task-item-variants.in-place.plan.json")
        extraction = plan["variantComponentExtractions"][0]
        extraction["states"] = [
            extraction["states"][0],
            extraction["states"][3],
        ]
        extraction["instances"] = [
            extraction["instances"][0],
            extraction["instances"][3],
            extraction["instances"][4],
        ]
        extraction["instances"][2]["state"] = "locked"
        plan["componentFamilyDecisions"] = [
            {
                "parent": extraction["template"].rsplit("/", 1)[0],
                "sources": [instance["source"] for instance in extraction["instances"]],
                "mode": "variant",
                "extractionId": extraction["id"],
                "reason": "Multiple visible rows reuse the locked visual state.",
            }
        ]

        normalized = normalize_plan(plan, "verify")

        self.assertEqual(
            [instance["state"] for instance in normalized["variant_component_extractions"][0]["instances"]],
            ["in_progress", "locked", "locked"],
        )
        self.assertIn("CopyStateRootData(source, activeState);", render(normalized, "apply"))
        preflight = render(normalize_plan(plan, "preflight"), "preflight")
        self.assertNotIn("Variant component Prefab target already exists", preflight)

    def test_single_state_variant_directs_plan_repair_to_component_extraction(self):
        plan = self.load_plan("seven-day-task-view-task-item-variants.in-place.plan.json")
        extraction = plan["variantComponentExtractions"][0]
        extraction["states"] = [extraction["states"][0]]
        extraction["instances"] = [
            extraction["instances"][0],
            extraction["instances"][4],
        ]
        extraction["instances"][1]["state"] = "in_progress"

        with self.assertRaisesRegex(ValueError, "use componentExtractions"):
            normalize_plan(plan, "verify")

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
        self.assertNotIn("Variant component Prefab target already exists", generated)
        self.assertIn("Candidate source paths", generated)
        self.assertIn(
            'AssertPlanPath(root, "variantComponentExtractions[0].template"',
            generated,
        )
        self.assertIn("ReplaceVariantSourceWithComponent", generated)

    def test_reapply_rolls_back_component_assets_created_during_failed_replay(self):
        plan = self.load_plan("seven-day-task-view-task-item-variants.in-place.plan.json")
        extraction = plan["variantComponentExtractions"][0]
        plan["componentFamilyDecisions"] = [{
            "parent": extraction["template"].rsplit("/", 1)[0],
            "sources": [instance["source"] for instance in extraction["instances"]],
            "mode": "variant",
            "extractionId": extraction["id"],
            "reason": "replay transaction test",
        }]

        generated = render(normalize_plan(plan, "reapply"), "reapply")

        filter_missing = ".Where(assetPath => AssetDatabase.LoadMainAssetAtPath(assetPath) == null).ToArray()"
        begin_call = "BeginComponentAssetTransaction(componentAssetTransactionPaths)"
        rollback_call = "RollbackComponentAssetTransaction(componentAssetTransaction);"
        self.assertIn(filter_missing, generated)
        self.assertIn(begin_call, generated)
        self.assertIn(rollback_call, generated)
        begin = generated.index(begin_call)
        self.assertLess(begin, generated.index("?? CreateVariantComponentPrefab(", begin))
        self.assertLess(generated.index("AssetDatabase.SaveAssets();", begin), generated.rindex(rollback_call))

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
        self.assertNotIn("Stateful component Prefab target already exists", generated)
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

    def test_guid_invariant_failure_reports_expected_and_actual_identity(self):
        raw_plan = json.loads(
            (SKILL_DIRECTORY / "examples" / "sample-plan.json").read_text(encoding="utf-8")
        )

        generated = render(normalize_plan(raw_plan, "apply"), "apply")

        self.assertIn("GUID invariant failed: path=", generated)
        self.assertIn(";expectedGuid=", generated)
        self.assertIn(";actualGuid=", generated)

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

    def make_containment_plan(self, resolutions=None):
        raw_plan = json.loads(
            (SKILL_DIRECTORY / "examples" / "sample-plan.json").read_text(encoding="utf-8")
        )
        raw_plan["containmentFindings"] = [
            {
                "innerCandidateId": "family_002",
                "innerParent": "RewardPanel/Root/[Coins]",
                "mapping": [
                    {
                        "source": "RewardPanel/Root/[Coins]/[Coin_1]",
                        "containedBy": "RewardPanel/Root/[Cards]/[Card_1]",
                    },
                    {
                        "source": "RewardPanel/Root/[Coins]/[Coin_2]",
                        "containedBy": "RewardPanel/Root/[Cards]/[Card_2]",
                    },
                ],
            }
        ]
        if resolutions is not None:
            raw_plan["containmentResolutions"] = resolutions
        return raw_plan

    def test_containment_finding_without_a_resolution_is_rejected(self):
        raw_plan = self.make_containment_plan(resolutions=[])

        with self.assertRaisesRegex(
            ValueError,
            r"containmentResolutions must resolve RewardPanel/Root/\[Coins\]/\[Coin_1\]",
        ):
            normalize_plan(raw_plan, "verify")

    def test_containment_finding_needs_a_resolution_for_every_member(self):
        raw_plan = self.make_containment_plan(
            resolutions=[
                {
                    "source": "RewardPanel/Root/[Coins]/[Coin_1]",
                    "mode": "reparent",
                    "newParent": "RewardPanel/Root/[Cards]/[Card_1]",
                }
            ]
        )

        with self.assertRaisesRegex(
            ValueError,
            r"containmentResolutions must resolve RewardPanel/Root/\[Coins\]/\[Coin_2\]",
        ):
            normalize_plan(raw_plan, "verify")

    def test_containment_reparent_outside_the_container_is_rejected(self):
        raw_plan = self.make_containment_plan(
            resolutions=[
                {
                    "source": "RewardPanel/Root/[Coins]/[Coin_1]",
                    "mode": "reparent",
                    "newParent": "RewardPanel/Root/[Cards]/[Card_2]",
                },
                {
                    "source": "RewardPanel/Root/[Coins]/[Coin_2]",
                    "mode": "reparent",
                    "newParent": "RewardPanel/Root/[Cards]/[Card_2]",
                },
            ]
        )

        with self.assertRaisesRegex(ValueError, "which is not inside"):
            normalize_plan(raw_plan, "verify")

    def test_containment_keep_requires_substantive_evidence(self):
        raw_plan = self.make_containment_plan(
            resolutions=[
                {
                    "source": "RewardPanel/Root/[Coins]/[Coin_1]",
                    "mode": "keep",
                    "evidence": "overlaps",
                },
                {
                    "source": "RewardPanel/Root/[Coins]/[Coin_2]",
                    "mode": "keep",
                    "evidence": "overlaps",
                },
            ]
        )

        with self.assertRaisesRegex(ValueError, "at least 20 characters"):
            normalize_plan(raw_plan, "verify")

    def test_containment_resolution_mode_must_be_reparent_or_keep(self):
        raw_plan = self.make_containment_plan(
            resolutions=[
                {
                    "source": "RewardPanel/Root/[Coins]/[Coin_1]",
                    "mode": "ignore",
                }
            ]
        )

        with self.assertRaisesRegex(ValueError, "must be reparent or keep"):
            normalize_plan(raw_plan, "verify")

    def test_duplicate_containment_resolution_sources_are_rejected(self):
        resolution = {
            "source": "RewardPanel/Root/[Coins]/[Coin_1]",
            "mode": "reparent",
            "newParent": "RewardPanel/Root/[Cards]/[Card_1]",
        }
        raw_plan = self.make_containment_plan(resolutions=[resolution, dict(resolution)])

        with self.assertRaisesRegex(ValueError, "duplicate containmentResolutions source"):
            normalize_plan(raw_plan, "verify")

    def test_containment_finding_mapping_must_not_be_empty(self):
        raw_plan = self.make_containment_plan(resolutions=[])
        raw_plan["containmentFindings"][0]["mapping"] = []

        with self.assertRaisesRegex(ValueError, r"mapping must not be empty"):
            normalize_plan(raw_plan, "verify")

    def test_containment_findings_pass_when_each_member_is_reparented(self):
        raw_plan = self.make_containment_plan(
            resolutions=[
                {
                    "source": "RewardPanel/Root/[Coins]/[Coin_1]",
                    "mode": "reparent",
                    "newParent": "RewardPanel/Root/[Cards]/[Card_1]",
                },
                {
                    "source": "RewardPanel/Root/[Coins]/[Coin_2]",
                    "mode": "reparent",
                    "newParent": "RewardPanel/Root/[Cards]/[Card_2]/[Body]",
                },
            ]
        )

        plan = normalize_plan(raw_plan, "verify")

        self.assertEqual(len(plan["containment_findings"]), 1)
        self.assertEqual(len(plan["containment_resolutions"]), 2)

    def test_containment_findings_pass_with_documented_keep_evidence(self):
        raw_plan = self.make_containment_plan(
            resolutions=[
                {
                    "source": "RewardPanel/Root/[Coins]/[Coin_1]",
                    "mode": "keep",
                    "evidence": "The coin row is driven by a shared layout group above the cards.",
                },
                {
                    "source": "RewardPanel/Root/[Coins]/[Coin_2]",
                    "mode": "keep",
                    "evidence": "The coin row is driven by a shared layout group above the cards.",
                },
            ]
        )

        plan = normalize_plan(raw_plan, "verify")

        self.assertEqual(len(plan["containment_resolutions"]), 2)

    def test_containment_finding_paths_are_asserted_in_generated_code(self):
        raw_plan = self.make_containment_plan(
            resolutions=[
                {
                    "source": "RewardPanel/Root/[Coins]/[Coin_1]",
                    "mode": "reparent",
                    "newParent": "RewardPanel/Root/[Cards]/[Card_1]",
                },
                {
                    "source": "RewardPanel/Root/[Coins]/[Coin_2]",
                    "mode": "reparent",
                    "newParent": "RewardPanel/Root/[Cards]/[Card_2]",
                },
            ]
        )

        generated = render(normalize_plan(raw_plan, "preflight"), "preflight")

        self.assertIn(
            'AssertPlanPath(root, "containmentFindings[0].innerParent", '
            '"RewardPanel/Root/[Coins]");',
            generated,
        )
        self.assertIn(
            'AssertPlanPath(root, "containmentFindings[0].mapping[0].source", '
            '"RewardPanel/Root/[Coins]/[Coin_1]");',
            generated,
        )
        self.assertIn(
            'AssertPlanPath(root, "containmentFindings[0].mapping[0].containedBy", '
            '"RewardPanel/Root/[Cards]/[Card_1]");',
            generated,
        )


if __name__ == "__main__":
    unittest.main()
