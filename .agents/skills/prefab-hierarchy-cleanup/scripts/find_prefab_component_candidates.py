"""Report structurally compatible repeated UI units from a Prefab snapshot.

The report is advisory. Unity's apply pass remains responsible for checking
serialized external references and preserving instance overrides.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path


NODE_RE = re.compile(
    r"^NODE\t(?P<path>.+?)\tdepth=(?P<depth>\d+)\tsibling=(?P<sibling>\d+)"
    r"\tactive=(?P<active>True|False)\tchildren=(?P<children>\d+)"
    r"\tnestedPrefab=(?P<nested>True|False)\tcomponents=(?P<components>[^\t]*)"
)
RECT_RE = re.compile(
    r"anchorMin=(?P<anchor_min_x>-?[\d.]+),(?P<anchor_min_y>-?[\d.]+),"
    r"anchorMax=(?P<anchor_max_x>-?[\d.]+),(?P<anchor_max_y>-?[\d.]+),"
    r"pivot=(?P<pivot_x>-?[\d.]+),(?P<pivot_y>-?[\d.]+),"
    r"anchoredPosition=(?P<position_x>-?[\d.]+),(?P<position_y>-?[\d.]+),"
    r"sizeDelta=(?P<size_x>-?[\d.]+),(?P<size_y>-?[\d.]+)"
)
TRAILING_STATE_INDEX_RE = re.compile(r"(?:[_\s-]*(?:\d+|[一二三四五六七八九十]+))$")


NUMBERED_FAMILY_RE = re.compile(r"^(?P<stem>[A-Za-z][A-Za-z0-9]*)(?:[_\s-]?)(?P<index>\d+)$")
BARE_INDEX_RE = re.compile(r"^\d+$")


@dataclass
class Node:
    path: str
    depth: int
    sibling: int
    child_count: int
    nested_prefab: bool
    components: tuple[str, ...]
    anchor_min: tuple[float, float] | None
    anchor_max: tuple[float, float] | None
    pivot: tuple[float, float] | None
    anchored_position: tuple[float, float] | None
    size_delta: tuple[float, float] | None
    children: list["Node"] = field(default_factory=list)

    @property
    def name(self) -> str:
        return self.path.rsplit("/", 1)[-1]


def parse_snapshot(text: str) -> Node:
    nodes: dict[str, Node] = {}
    root: Node | None = None
    for raw_line in text.splitlines():
        match = NODE_RE.match(raw_line)
        if match is None:
            continue
        path = match.group("path")
        rect_match = RECT_RE.search(raw_line)
        rect_values = rect_match.groupdict() if rect_match is not None else {}

        def vector(prefix: str) -> tuple[float, float] | None:
            x = rect_values.get(prefix + "_x")
            y = rect_values.get(prefix + "_y")
            return (float(x), float(y)) if x is not None and y is not None else None

        node = Node(
            path=path,
            depth=int(match.group("depth")),
            sibling=int(match.group("sibling")),
            child_count=int(match.group("children")),
            nested_prefab=match.group("nested") == "True",
            components=tuple(filter(None, match.group("components").split(","))),
            anchor_min=vector("anchor_min"),
            anchor_max=vector("anchor_max"),
            pivot=vector("pivot"),
            anchored_position=vector("position"),
            size_delta=vector("size"),
        )
        nodes[path] = node
        if "/" not in path:
            root = node
            continue
        parent_path = path.rsplit("/", 1)[0]
        parent = nodes.get(parent_path)
        if parent is None:
            raise ValueError(f"snapshot parent is missing for {path}")
        parent.children.append(node)

    if root is None:
        raise ValueError("snapshot contains no NODE entries")
    return root


def signature(node: Node) -> tuple[tuple[str, ...], tuple[object, ...]]:
    return (
        node.components,
        tuple(signature(child) for child in sorted(node.children, key=lambda item: item.sibling)),
    )


def has_common_direct_child_name(nodes: list[Node]) -> bool:
    common_names: set[str] | None = None
    for node in nodes:
        names = {child.name for child in node.children}
        common_names = names if common_names is None else common_names & names
        if not common_names:
            return False
    return bool(common_names)


def has_nested_prefab(node: Node) -> bool:
    return node.nested_prefab or any(has_nested_prefab(child) for child in node.children)


def visit(node: Node) -> list[Node]:
    result = [node]
    for child in node.children:
        result.extend(visit(child))
    return result


def common_instance_name(nodes: list[Node]) -> str:
    normalized = [base_name(node) for node in nodes]
    return normalized[0] if len(set(normalized)) == 1 else "RepeatedUnit"


def base_name(node: Node) -> str:
    return TRAILING_STATE_INDEX_RE.sub("", node.name.strip("[]").strip()).strip()


def approximately_equal(left: tuple[float, float], right: tuple[float, float], tolerance: float = 0.001) -> bool:
    return abs(left[0] - right[0]) <= tolerance and abs(left[1] - right[1]) <= tolerance


def state_overlap(left: Node, right: Node) -> float | None:
    """Return overlap relative to the smaller area when two roots occupy one slot."""
    required = (
        left.anchor_min,
        left.anchor_max,
        left.pivot,
        left.anchored_position,
        left.size_delta,
        right.anchor_min,
        right.anchor_max,
        right.pivot,
        right.anchored_position,
        right.size_delta,
    )
    if any(value is None for value in required):
        return None
    if not (
        approximately_equal(left.anchor_min, right.anchor_min)
        and approximately_equal(left.anchor_max, right.anchor_max)
        and approximately_equal(left.pivot, right.pivot)
    ):
        return None

    def bounds(node: Node) -> tuple[float, float, float, float]:
        assert node.anchored_position is not None and node.size_delta is not None and node.pivot is not None
        x, y = node.anchored_position
        width, height = node.size_delta
        pivot_x, pivot_y = node.pivot
        return (x - width * pivot_x, y - height * pivot_y, x + width * (1 - pivot_x), y + height * (1 - pivot_y))

    left_min_x, left_min_y, left_max_x, left_max_y = bounds(left)
    right_min_x, right_min_y, right_max_x, right_max_y = bounds(right)
    overlap_width = max(0.0, min(left_max_x, right_max_x) - max(left_min_x, right_min_x))
    overlap_height = max(0.0, min(left_max_y, right_max_y) - max(left_min_y, right_min_y))
    smaller_area = min((left_max_x - left_min_x) * (left_max_y - left_min_y), (right_max_x - right_min_x) * (right_max_y - right_min_y))
    if smaller_area <= 0.0:
        return None
    return overlap_width * overlap_height / smaller_area


def state_groups(parent: Node) -> list[list[Node]]:
    sources = [child for child in parent.children if child.child_count > 0 and not has_nested_prefab(child)]
    adjacent: dict[str, set[str]] = {child.path: set() for child in sources}
    by_path = {child.path: child for child in sources}
    for left_index, left in enumerate(sources):
        for right in sources[left_index + 1 :]:
            overlap = state_overlap(left, right)
            if overlap is None or overlap < 0.9:
                continue
            adjacent[left.path].add(right.path)
            adjacent[right.path].add(left.path)

    result: list[list[Node]] = []
    visited: set[str] = set()
    for source in sources:
        if source.path in visited or not adjacent[source.path]:
            continue
        stack = [source.path]
        component: list[Node] = []
        visited.add(source.path)
        while stack:
            path = stack.pop()
            component.append(by_path[path])
            for connected in adjacent[path]:
                if connected not in visited:
                    visited.add(connected)
                    stack.append(connected)
        if len(component) >= 2:
            result.append(sorted(component, key=lambda item: item.sibling))
    return result


def numbered_family_name(node: Node) -> str | None:
    match = NUMBERED_FAMILY_RE.match(node.name.strip("[]").strip())
    return match.group("stem") if match is not None else None


def numbered_family_index(node: Node) -> int | None:
    match = NUMBERED_FAMILY_RE.match(node.name.strip("[]").strip())
    return int(match.group("index")) if match is not None else None


def bare_numbered_index(node: Node) -> int | None:
    value = node.name.strip("[]").strip()
    return int(value) if BARE_INDEX_RE.match(value) is not None else None


def matching_rect_transform_frame(nodes: list[Node]) -> bool:
    if len(nodes) < 2:
        return False
    first = nodes[0]
    if first.anchor_min is None or first.anchor_max is None or first.pivot is None:
        return False
    return all(
        node.anchor_min is not None
        and node.anchor_max is not None
        and node.pivot is not None
        and approximately_equal(first.anchor_min, node.anchor_min)
        and approximately_equal(first.anchor_max, node.anchor_max)
        and approximately_equal(first.pivot, node.pivot)
        for node in nodes[1:]
    )


def numbered_component_candidates(root: Node) -> list[dict[str, object]]:
    """Find high-confidence numbered families, including stateful size variants."""
    result: list[dict[str, object]] = []
    candidate_index = 1
    for parent in visit(root):
        groups: dict[str, list[Node]] = {}
        bare_index_nodes: list[tuple[int, Node]] = []
        for child in parent.children:
            if child.child_count == 0 or has_nested_prefab(child):
                continue
            family_name = numbered_family_name(child)
            if family_name is None:
                bare_index = bare_numbered_index(child)
                if bare_index is not None:
                    bare_index_nodes.append((bare_index, child))
                continue
            groups.setdefault(family_name, []).append(child)

        for bare_index, bare_node in bare_index_nodes:
            eligible_families = []
            for family_name, group in groups.items():
                represented_indices = {numbered_family_index(node) for node in group}
                if (
                    len(group) >= 2
                    and bare_index not in represented_indices
                    and matching_rect_transform_frame(group + [bare_node])
                ):
                    eligible_families.append(family_name)
            if len(eligible_families) == 1:
                groups[eligible_families[0]].append(bare_node)

        for family_name, group in sorted(groups.items()):
            ordered = sorted(group, key=lambda item: item.sibling)
            if len(ordered) < 3 or not matching_rect_transform_frame(ordered):
                continue
            identical_structure = len({signature(node) for node in ordered}) == 1
            has_common_direct_child = has_common_direct_child_name(ordered)
            result.append(
                {
                    "id": f"numbered_{candidate_index:03d}",
                    "kind": "numbered_repeated",
                    "parent": parent.path,
                    "suggestedAssetName": family_name,
                    "template": ordered[0].path,
                    "instances": [node.path for node in ordered],
                    "instanceCount": len(ordered),
                    "recommendedMode": (
                        "component"
                        if identical_structure
                        else "stateful"
                        if has_common_direct_child
                        else "variant"
                    ),
                    "requiresExtraction": identical_structure or has_common_direct_child,
                    "sizeDeltaOverridesAllowed": True,
                    "nestedPrefabInsideAnySource": False,
                    "requiresUnityExternalReferenceCheck": True,
                }
            )
            candidate_index += 1
    return result


def component_candidates(
    root: Node,
    state_paths: set[str],
    excluded_instance_paths: set[str] | None = None,
) -> list[dict[str, object]]:
    result: list[dict[str, object]] = []
    excluded_instance_paths = excluded_instance_paths or set()
    for parent in visit(root):
        groups: dict[tuple[tuple[str, ...], tuple[object, ...]], list[Node]] = {}
        for child in parent.children:
            if child.child_count == 0 or has_nested_prefab(child):
                continue
            groups.setdefault(signature(child), []).append(child)
        for group in groups.values():
            if len(group) < 2:
                continue
            ordered = sorted(group, key=lambda item: item.sibling)
            if all(node.path in state_paths for node in ordered):
                continue
            if any(node.path in excluded_instance_paths for node in ordered):
                continue
            result.append(
                {
                    "parent": parent.path,
                    "suggestedAssetName": common_instance_name(ordered),
                    "template": ordered[0].path,
                    "instances": [node.path for node in ordered],
                    "instanceCount": len(ordered),
                    "directChildren": [child.name for child in sorted(ordered[0].children, key=lambda item: item.sibling)],
                    "componentSignature": list(ordered[0].components),
                    "nestedPrefabInsideAnySource": False,
                    "requiresUnityExternalReferenceCheck": True,
                }
            )
    return result


def state_candidates(root: Node) -> list[dict[str, object]]:
    result: list[dict[str, object]] = []
    for parent in visit(root):
        for group in state_groups(parent):
            overlaps = [
                state_overlap(left, right)
                for left_index, left in enumerate(group)
                for right in group[left_index + 1 :]
            ]
            result.append(
                {
                    "parent": parent.path,
                    "suggestedAssetName": common_instance_name(group),
                    "template": group[0].path,
                    "sources": [node.path for node in group],
                    "stateIds": [f"state_{index:02d}" for index in range(1, len(group) + 1)],
                    "stateNames": [f"[State_{index:02d}]" for index in range(1, len(group) + 1)],
                    "stateCount": len(group),
                    "minimumPairwiseOverlap": round(min(value for value in overlaps if value is not None), 4),
                    "nestedPrefabInsideAnySource": False,
                    "requiresSemanticStateMapping": True,
                    "requiresSingleActiveDefault": True,
                    "requiresUnityExternalReferenceCheck": True,
                }
            )
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description="Find repeated component-Prefab candidates from a snapshot")
    parser.add_argument("snapshot", help="UTF-8 snapshot file, or - to read stdin")
    args = parser.parse_args()
    text = sys.stdin.read() if args.snapshot == "-" else Path(args.snapshot).read_text(encoding="utf-8-sig")
    root = parse_snapshot(text)
    states = state_candidates(root)
    state_paths = {path for state in states for path in state["sources"]}
    numbered = numbered_component_candidates(root)
    components = numbered + component_candidates(
        root,
        state_paths,
        {path for candidate in numbered for path in candidate["instances"]},
    )
    report = {
        "candidates": components,
        "componentCandidates": components,
        "stateCandidates": states,
    }
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
