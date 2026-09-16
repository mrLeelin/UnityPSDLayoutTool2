from __future__ import annotations

import importlib.util
import sys
import unittest
from pathlib import Path

SCRIPT_PATH = Path(__file__).resolve().parents[1] / "audit_prefab_preservation.py"
SPEC = importlib.util.spec_from_file_location("audit_prefab_preservation", SCRIPT_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)

ASSET = "Assets/Common/SevenDayRewardEntry.prefab"


def node(name, comps=("CanvasRenderer", "Image"), active=True, root=False, src="",
         size=(10.0, 10.0), text="", sprite=""):
    return dict(name=name, childCount=0, active=active, isInstanceRoot=root, sourceAsset=src,
                components=list(comps), sprite=sprite, text=text, font="",
                worldRect=[0.0, 0.0, 10.0, 10.0],
                sizeDelta=list(size), anchorMin=[0.5, 0.5], anchorMax=[0.5, 0.5],
                pivot=[0.5, 0.5], localScale=[1.0, 1.0, 1.0], rotationZ=0.0)


def mk(paths):
    """fill childCount from the path set so fixtures stay consistent"""
    nodes = dict(paths)
    for path in list(nodes):
        nodes[path]["childCount"] = sum(
            1 for other in nodes if other.rsplit("/", 1)[0] == path and "/" in other)
    return dict(meta=dict(missingSpritesOwn=0, missingSpritesNested=0), nodes=nodes)


def stateful_plan():
    return {"statefulComponentExtractions": [{
        "id": "day_entry", "template": "Root/[DayEntry_1]", "assetPath": ASSET,
        "common": {"source": "Root/[DayEntry_1]",
                   "members": [{"sourceName": "DayLabel", "name": "DayLabel"}]},
        "states": [
            {"id": "default", "source": "Root/[DayEntry_1]", "name": "[State_Default]",
             "members": [{"sourceName": "DayBg", "name": "DayBg"}]},
            {"id": "locked", "source": "Root/[DayEntry_2]", "name": "[State_Locked]",
             "members": [{"sourceName": "DayBg", "name": "DayBg"},
                         {"sourceName": "Lock", "name": "Lock"}]}],
        "defaultState": "default",
        "instances": [
            {"source": "Root/[DayEntry_1]", "name": "[DayEntry_1]", "state": "default",
             "commonSourceNames": ["DayLabel"], "stateSourceNames": ["DayBg"]},
            {"source": "Root/[DayEntry_2]", "name": "[DayEntry_2]", "state": "locked",
             "commonSourceNames": ["DayLabel"], "stateSourceNames": ["DayBg", "Lock"]}],
    }]}


def before_dump():
    return mk({
        "Root": node("Root", comps=()),
        "Root/[DayEntry_1]": node("[DayEntry_1]", comps=(), size=(128.0, 223.0)),
        "Root/[DayEntry_1]/DayBg": node("DayBg", size=(128.0, 216.0)),
        "Root/[DayEntry_1]/DayLabel": node("DayLabel", comps=("CanvasRenderer", "TMP"), text="day"),
        "Root/[DayEntry_2]": node("[DayEntry_2]", comps=(), size=(128.0, 272.0)),
        "Root/[DayEntry_2]/DayBg": node("DayBg", size=(128.0, 216.0)),
        "Root/[DayEntry_2]/DayLabel": node("DayLabel", comps=("CanvasRenderer", "TMP"), text="day"),
        "Root/[DayEntry_2]/Lock": node("Lock", size=(83.2, 83.2)),
    })


def after_dump(active_default=True, drop_lock=False, extra_active_branch=False, rename=None):
    nodes = {
        "Root": node("Root", comps=()),
        "Root/[DayEntry_1]": node("[DayEntry_1]", comps=(), root=True, src=ASSET, size=(128.0, 223.0)),
        "Root/[DayEntry_1]/[States]": node("[States]", comps=()),
        "Root/[DayEntry_1]/[States]/[State_Default]": node("[State_Default]", comps=(), active=active_default),
        "Root/[DayEntry_1]/[States]/[State_Locked]": node("[State_Locked]", comps=(), active=not active_default),
        "Root/[DayEntry_1]/[States]/[State_Default]/DayBg": node("DayBg", size=(128.0, 216.0)),
        "Root/[DayEntry_1]/[States]/[State_Locked]/DayBg": node("DayBg", size=(128.0, 216.0)),
        "Root/[DayEntry_1]/[States]/[State_Locked]/Lock": node("Lock", size=(83.2, 83.2)),
        "Root/[DayEntry_1]/[Common]": node("[Common]", comps=()),
        "Root/[DayEntry_1]/[Common]/DayLabel": node("DayLabel", comps=("CanvasRenderer", "TMP"), text="day"),
        "Root/[DayEntry_2]": node("[DayEntry_2]", comps=(), root=True, src=ASSET, size=(128.0, 272.0)),
        "Root/[DayEntry_2]/[States]": node("[States]", comps=()),
        "Root/[DayEntry_2]/[States]/[State_Default]": node("[State_Default]", comps=(), active=False),
        "Root/[DayEntry_2]/[States]/[State_Locked]": node("[State_Locked]", comps=(), active=True),
        "Root/[DayEntry_2]/[States]/[State_Default]/DayBg": node("DayBg", size=(128.0, 216.0)),
        "Root/[DayEntry_2]/[States]/[State_Locked]/DayBg": node("DayBg", size=(128.0, 216.0)),
        "Root/[DayEntry_2]/[States]/[State_Locked]/Lock": node("Lock", size=(83.2, 83.2)),
        "Root/[DayEntry_2]/[Common]": node("[Common]", comps=()),
        "Root/[DayEntry_2]/[Common]/DayLabel": node("DayLabel", comps=("CanvasRenderer", "TMP"), text="day"),
    }
    if drop_lock:
        del nodes["Root/[DayEntry_2]/[States]/[State_Locked]/Lock"]
    if extra_active_branch:
        # a second active branch inside the same instance must be reported
        nodes["Root/[DayEntry_2]/[States]/[State_Default]/DayBg"]["active"] = True
        nodes["Root/[DayEntry_2]/[States]/[State_Default]"] = node(
            "[State_Default]", comps=(), active=True)
    if rename:
        nodes["Root/[DayEntry_1]/[Common]/DayLabel"]["name"] = rename
    return mk(nodes)


class CompareTests(unittest.TestCase):
    def test_stateful_extraction_reproduces_every_unit_leaf(self) -> None:
        errors = MODULE.compare(before_dump(), after_dump(), stateful_plan(), quiet=True)
        self.assertEqual([], errors)

    def test_missing_unit_leaf_is_reported(self) -> None:
        errors = MODULE.compare(before_dump(), after_dump(drop_lock=True), stateful_plan(), quiet=True)
        self.assertTrue(any("单元内容丢失" in e for e in errors), errors)
        self.assertTrue(any("[DayEntry_2]" in e for e in errors), errors)

    def test_two_active_state_branches_are_reported(self) -> None:
        errors = MODULE.compare(before_dump(), after_dump(extra_active_branch=True),
                                stateful_plan(), quiet=True)
        self.assertTrue(any("激活分支" in e for e in errors), errors)

    def test_nonsemantic_name_is_reported(self) -> None:
        errors = MODULE.compare(before_dump(), after_dump(rename="ui_daily_yq2"), stateful_plan(), quiet=True)
        self.assertTrue(any("非语义命名" in e for e in errors), errors)

    def test_changed_text_of_an_untouched_node_is_reported(self) -> None:
        before = mk({
            "Root": node("Root", comps=()),
            "Root/TaskList": node("TaskList", comps=()),
            "Root/TaskList/ClaimLabel": node("ClaimLabel", comps=("CanvasRenderer", "TMP"), text="Claim"),
        })
        after = mk({
            "Root": node("Root", comps=()),
            "Root/TaskList": node("TaskList", comps=()),
            "Root/TaskList/ClaimLabel": node("ClaimLabel", comps=("CanvasRenderer", "TMP"), text="WRONG"),
        })
        errors = MODULE.compare(before, after, None, quiet=True)
        self.assertTrue(any("失去或改变" in e for e in errors), errors)

    def test_missing_own_sprite_is_reported(self) -> None:
        before = mk({"Root": node("Root", comps=()),
                     "Root/Icon": node("Icon", sprite="Assets/a.png#a")})
        after = mk({"Root": node("Root", comps=()),
                    "Root/Icon": node("Icon", sprite="Assets/a.png#a")})
        after["meta"]["missingSpritesOwn"] = 1
        errors = MODULE.compare(before, after, None, quiet=True)
        self.assertTrue(any("缺失 Sprite" in e for e in errors), errors)

    def test_variant_and_component_shapes(self) -> None:
        plan = {
            "variantComponentExtractions": [{
                "id": "task_item", "template": "Root/T/[TaskItem_1]", "assetPath": ASSET,
                "states": [{"id": "a", "source": "Root/T/[TaskItem_1]", "name": "[State_A]"},
                           {"id": "b", "source": "Root/T/[TaskItem_2]", "name": "[State_B]"}],
                "defaultState": "a",
                "instances": [{"source": "Root/T/[TaskItem_1]", "name": "[TaskItem_1]", "state": "a"},
                              {"source": "Root/T/[TaskItem_2]", "name": "[TaskItem_2]", "state": "b"}]}],
            "componentExtractions": [{
                "id": "tick", "template": "Root/Tick_1", "assetPath": ASSET,
                "instances": ["Root/Tick_1"]}],
        }
        before = mk({
            "Root": node("Root", comps=()), "Root/T": node("T", comps=()),
            "Root/T/[TaskItem_1]": node("[TaskItem_1]", comps=(), size=(830.0, 173.0)),
            "Root/T/[TaskItem_1]/Bg": node("Bg", size=(830.0, 173.0)),
            "Root/T/[TaskItem_2]": node("[TaskItem_2]", comps=(), size=(830.0, 173.0)),
            "Root/T/[TaskItem_2]/Bg": node("Bg", size=(830.0, 173.0)),
            "Root/Tick_1": node("Tick_1", size=(12.0, 54.0)),
        })
        after = mk({
            "Root": node("Root", comps=()), "Root/T": node("T", comps=()),
            "Root/T/[TaskItem_1]": node("[TaskItem_1]", comps=(), root=True, src=ASSET, size=(830.0, 173.0)),
            "Root/T/[TaskItem_1]/[Common]": node("[Common]", comps=()),
            "Root/T/[TaskItem_1]/[States]": node("[States]", comps=()),
            "Root/T/[TaskItem_1]/[States]/[State_A]": node("[State_A]", comps=(), active=True),
            "Root/T/[TaskItem_1]/[States]/[State_B]": node("[State_B]", comps=(), active=False),
            "Root/T/[TaskItem_1]/[States]/[State_A]/Bg": node("Bg", size=(830.0, 173.0)),
            "Root/T/[TaskItem_1]/[States]/[State_B]/Bg": node("Bg", size=(830.0, 173.0)),
            "Root/T/[TaskItem_2]": node("[TaskItem_2]", comps=(), root=True, src=ASSET, size=(830.0, 173.0)),
            "Root/T/[TaskItem_2]/[Common]": node("[Common]", comps=()),
            "Root/T/[TaskItem_2]/[States]": node("[States]", comps=()),
            "Root/T/[TaskItem_2]/[States]/[State_A]": node("[State_A]", comps=(), active=False),
            "Root/T/[TaskItem_2]/[States]/[State_B]": node("[State_B]", comps=(), active=True),
            "Root/T/[TaskItem_2]/[States]/[State_A]/Bg": node("Bg", size=(830.0, 173.0)),
            "Root/T/[TaskItem_2]/[States]/[State_B]/Bg": node("Bg", size=(830.0, 173.0)),
            "Root/Tick_1": node("Tick_1", root=True, src=ASSET),
        })
        errors = MODULE.compare(before, after, plan, quiet=True)
        self.assertEqual([], errors)

    def test_post_apply_before_snapshot_is_warned(self) -> None:
        # a 'before' capture taken after the apply must be flagged (B would otherwise compare an
        # instance's own inactive branches against itself)
        notes: list[str] = []
        errors = MODULE.compare(after_dump(), after_dump(), stateful_plan(), quiet=True, notes=notes)
        # it must both warn about the misuse AND fail the unit check (the before unit still holds
        # its inactive branches, which the active branch + [Common] of the after side cannot match)
        self.assertTrue(any("警告" in n and "apply 之前" in n for n in notes), notes)
        self.assertTrue(any("单元内容丢失" in e for e in errors), errors)


if __name__ == "__main__":
    unittest.main()
