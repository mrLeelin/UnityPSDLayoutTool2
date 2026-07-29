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
TRANSFORM_RE = re.compile(
    r"scale=(?P<scale_x>-?[\d.]+),(?P<scale_y>-?[\d.]+),(?P<scale_z>-?[\d.]+),"
    r"rotation=(?P<rotation_x>-?[\d.]+),(?P<rotation_y>-?[\d.]+),(?P<rotation_z>-?[\d.]+)"
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
    axis_aligned: bool = True
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
        transform_match = TRANSFORM_RE.search(raw_line)
        axis_aligned = True
        if transform_match is not None:
            transform_values = transform_match.groupdict()
            axis_aligned = all(
                abs(float(transform_values[f"scale_{axis}"]) - 1.0) <= 0.001
                for axis in ("x", "y", "z")
            ) and all(
                min(
                    abs(float(transform_values[f"rotation_{axis}"])),
                    abs(360.0 - float(transform_values[f"rotation_{axis}"])),
                )
                <= 0.001
                for axis in ("x", "y", "z")
            )

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
            axis_aligned=axis_aligned,
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


def world_rects(root: Node) -> dict[str, tuple[float, float, float, float]]:
    """Derive root-space rects for every node that carries complete rect data.

    Snapshots only report local anchor data, so containment across different
    containers has to be reconstructed here. Nodes with a non-identity scale or
    any rotation are skipped rather than approximated: an axis-aligned rect
    would be wrong for them, and a wrong rect is worse than a missing one.
    """
    result: dict[str, tuple[float, float, float, float]] = {}

    def walk(node: Node, parent_rect: tuple[float, float, float, float] | None) -> None:
        if node.size_delta is None or node.pivot is None or not node.axis_aligned:
            return
        if parent_rect is None:
            width, height = node.size_delta
            pivot_x, pivot_y = node.pivot
            rect = (
                -width * pivot_x,
                -height * pivot_y,
                width * (1 - pivot_x),
                height * (1 - pivot_y),
            )
        else:
            if node.anchor_min is None or node.anchor_max is None or node.anchored_position is None:
                return
            parent_min_x, parent_min_y, parent_max_x, parent_max_y = parent_rect
            parent_width = parent_max_x - parent_min_x
            parent_height = parent_max_y - parent_min_y
            anchor_min_x = parent_min_x + parent_width * node.anchor_min[0]
            anchor_min_y = parent_min_y + parent_height * node.anchor_min[1]
            anchor_max_x = parent_min_x + parent_width * node.anchor_max[0]
            anchor_max_y = parent_min_y + parent_height * node.anchor_max[1]
            width = (anchor_max_x - anchor_min_x) + node.size_delta[0]
            height = (anchor_max_y - anchor_min_y) + node.size_delta[1]
            pivot_x, pivot_y = node.pivot
            center_x = anchor_min_x + (anchor_max_x - anchor_min_x) * pivot_x + node.anchored_position[0]
            center_y = anchor_min_y + (anchor_max_y - anchor_min_y) * pivot_y + node.anchored_position[1]
            rect = (
                center_x - width * pivot_x,
                center_y - height * pivot_y,
                center_x + width * (1 - pivot_x),
                center_y + height * (1 - pivot_y),
            )
        result[node.path] = rect
        for child in node.children:
            walk(child, rect)

    walk(root, None)
    return result


def rect_area(rect: tuple[float, float, float, float]) -> float:
    return max(0.0, rect[2] - rect[0]) * max(0.0, rect[3] - rect[1])


def contains_rect(
    outer: tuple[float, float, float, float],
    inner: tuple[float, float, float, float],
    tolerance: float = 1.0,
) -> bool:
    return (
        inner[0] >= outer[0] - tolerance
        and inner[1] >= outer[1] - tolerance
        and inner[2] <= outer[2] + tolerance
        and inner[3] <= outer[3] + tolerance
    )


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


def structure_subsets(ordered: list[Node]) -> list[list[Node]]:
    """Split one numbered family into buckets that share a recursive signature.

    A family such as [StoryCard_1..3] where only _3 carries an extra child is
    otherwise reported as a single non-identical family, so the two members that
    ARE identical can never be proposed as a plain component extraction. Bucketing
    by signature keeps the whole family visible while making each internally
    consistent subset reachable on its own.
    """
    buckets: dict[tuple, list[Node]] = {}
    for node in ordered:
        buckets.setdefault(signature(node), []).append(node)
    return sorted(
        (sorted(bucket, key=lambda item: item.sibling) for bucket in buckets.values()),
        key=lambda bucket: bucket[0].sibling,
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
                if (
                    len(group) >= 2
                    and matching_rect_transform_frame(group + [bare_node])
                ):
                    eligible_families.append(family_name)
            if len(eligible_families) == 1:
                groups[eligible_families[0]].append(bare_node)

        if not groups and len({index for index, _ in bare_index_nodes}) >= 3:
            bare_nodes = [node for _, node in bare_index_nodes]
            family_name = singular_component_name(parent.name)
            if family_name is not None and matching_rect_transform_frame(bare_nodes):
                groups[family_name] = bare_nodes

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
                    # A repeated family is reusable even when its instances have no
                    # direct child in common: that case is represented as a variant.
                    "requiresExtraction": True,
                    "sizeDeltaOverridesAllowed": True,
                    "nestedPrefabInsideAnySource": False,
                    "requiresUnityExternalReferenceCheck": True,
                }
            )
            family_candidate_id = f"numbered_{candidate_index:03d}"
            candidate_index += 1
            if identical_structure:
                continue
            for subset_index, subset in enumerate(structure_subsets(ordered), start=1):
                result.append(
                    {
                        "id": f"{family_candidate_id}_s{subset_index:02d}",
                        "kind": "numbered_structure_subset",
                        "parent": parent.path,
                        "familyCandidateId": family_candidate_id,
                        "suggestedAssetName": family_name,
                        "template": subset[0].path,
                        "instances": [node.path for node in subset],
                        "instanceCount": len(subset),
                        "recommendedMode": "component" if len(subset) >= 2 else "skip",
                        # A subset and its family compete for the same sources, so the
                        # subset is an obligation only when the family itself is not.
                        "requiresExtraction": len(subset) >= 2
                        and not (identical_structure or has_common_direct_child),
                        "evidence": (
                            "only member of "
                            + family_name
                            + " with this recursive signature, so it has no peer to "
                            "share a component Prefab with"
                            if len(subset) < 2
                            else "members of "
                            + family_name
                            + " that share one recursive signature; use this narrower "
                            "boundary when the family-level extraction does not apply"
                        ),
                        "sizeDeltaOverridesAllowed": True,
                        "nestedPrefabInsideAnySource": False,
                        "requiresUnityExternalReferenceCheck": True,
                    }
                )
    return result


def singular_component_name(container_name: str) -> str | None:
    """Derive a component name from an English plural container such as [Tasks]."""
    name = container_name.strip().strip("[]")
    if not re.fullmatch(r"[A-Za-z][A-Za-z0-9]*", name):
        return None
    if name.endswith("ies") and len(name) > 3:
        return name[:-3] + "y"
    if (
        name.endswith("s")
        and len(name) > 1
        and not name.endswith(("ss", "us", "is"))
    ):
        return name[:-1]
    return name


def containment_misgroupings(
    root: Node,
    candidates: list[dict[str, object]],
    area_ratio_threshold: float = 0.25,
) -> list[dict[str, object]]:
    """Report numbered families that geometrically live inside another family.

    SKILL.md already says repeated labels and counters belong to the nearest
    repeated visual unit when geometry and cardinality line up, but nothing
    enforced it, so a family grouped by name prefix could sit in a sibling
    container that does not visually own it. This reports the case that is
    unambiguous: equal member counts, every member fully inside a distinct
    member of the other family, and a small area ratio.
    """
    rects = world_rects(root)
    families = [
        candidate
        for candidate in candidates
        if candidate.get("kind") == "numbered_repeated"
    ]
    result: list[dict[str, object]] = []
    for inner in families:
        inner_paths = list(inner["instances"])
        inner_rects = [rects.get(path) for path in inner_paths]
        if any(rect is None for rect in inner_rects):
            continue
        for outer in families:
            if outer is inner or outer["parent"] == inner["parent"]:
                continue
            outer_paths = list(outer["instances"])
            if len(outer_paths) != len(inner_paths):
                continue
            outer_rects = [rects.get(path) for path in outer_paths]
            if any(rect is None for rect in outer_rects):
                continue
            if any(
                inner_path == outer_path or inner_path.startswith(outer_path + "/")
                for inner_path in inner_paths
                for outer_path in outer_paths
            ):
                continue

            mapping: dict[str, str] = {}
            used_outer: set[str] = set()
            ratios: list[float] = []
            for inner_path, inner_rect in zip(inner_paths, inner_rects):
                matches = [
                    outer_path
                    for outer_path, outer_rect in zip(outer_paths, outer_rects)
                    if outer_path not in used_outer and contains_rect(outer_rect, inner_rect)
                ]
                if len(matches) != 1:
                    mapping = {}
                    break
                outer_path = matches[0]
                outer_area = rect_area(outer_rects[outer_paths.index(outer_path)])
                if outer_area <= 0.0:
                    mapping = {}
                    break
                ratio = rect_area(inner_rect) / outer_area
                if ratio > area_ratio_threshold:
                    mapping = {}
                    break
                used_outer.add(outer_path)
                mapping[inner_path] = outer_path
                ratios.append(ratio)

            if len(mapping) != len(inner_paths):
                continue
            result.append(
                {
                    "innerCandidateId": inner["id"],
                    "outerCandidateId": outer["id"],
                    "innerParent": inner["parent"],
                    "outerParent": outer["parent"],
                    "memberCount": len(inner_paths),
                    "maxAreaRatio": round(max(ratios), 4),
                    "mapping": [
                        {"source": inner_path, "containedBy": mapping[inner_path]}
                        for inner_path in inner_paths
                    ],
                    "severity": "blocking",
                    "reason": (
                        "every member is fully inside a distinct member of "
                        f"{outer['suggestedAssetName']} with equal cardinality and a small "
                        "area ratio, so it belongs to that repeated unit rather than to "
                        f"{inner['parent']}"
                    ),
                }
            )
    return result


def sparse_containers(
    root: Node,
    fill_ratio_threshold: float = 0.2,
    minimum_children: int = 2,
) -> list[dict[str, object]]:
    """Report containers whose children fill very little of their own rect.

    Advisory only. A container grouped by name prefix rather than by layout
    tends to be far larger than the union of what it holds, which is a hint
    that its members were placed by naming instead of by geometry.
    """
    rects = world_rects(root)
    result: list[dict[str, object]] = []
    for node in visit(root):
        if node.depth == 0 or len(node.children) < minimum_children:
            continue
        own = rects.get(node.path)
        if own is None:
            continue
        own_area = rect_area(own)
        if own_area <= 0.0:
            continue
        child_rects = [rects.get(child.path) for child in node.children]
        if any(rect is None for rect in child_rects):
            continue
        union = (
            min(rect[0] for rect in child_rects),
            min(rect[1] for rect in child_rects),
            max(rect[2] for rect in child_rects),
            max(rect[3] for rect in child_rects),
        )
        covered = sum(rect_area(rect) for rect in child_rects)
        fill_ratio = covered / own_area
        if fill_ratio >= fill_ratio_threshold:
            continue
        result.append(
            {
                "parent": node.path,
                "childCount": len(node.children),
                "fillRatio": round(fill_ratio, 4),
                "unionCoversOwnRect": contains_rect(union, own),
                "severity": "warning",
                "reason": (
                    "direct children cover a small fraction of this container's rect, "
                    "which suggests the container was formed by name prefix rather than "
                    "by layout; confirm each child belongs here"
                ),
            }
        )
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
        "containmentMisgroupings": containment_misgroupings(root, components),
        "sparseContainers": sparse_containers(root),
    }
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
