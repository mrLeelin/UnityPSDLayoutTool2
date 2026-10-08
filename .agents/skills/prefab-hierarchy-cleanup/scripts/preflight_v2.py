#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Offline mirror of the Unity-side version 2 plan gate (external-session preflight).

WHY THIS EXISTS
---------------
`validate_plan_locally.py` only covers a subset of what Unity actually rejects. The remaining
rules live inside ~3.4k lines of C# spread over four files, so an external session otherwise has
to read them all before it can trust a plan. This module mirrors them in one offline command and
additionally PREDICTS the post-grouping candidate report -- the measurement that decides whether
the automatic second stage will come back `partial`.

Mirrored sources (keep in sync when the executor changes):
  * PsdWorkflowSafety.TryValidate
      binding fields, selectionNodeIds, fingerprint / target path / reviewVersion contracts
  * PsdHierarchyChatCleanupExecution
      SupportedRootProperties / SupportedRootArrayProperties / RequiredArrayProperties,
      SupportedVerifyFields, IsNonSemanticObjectName, TryValidateSemanticNames,
      ValidatePrivateAssetNamePrefix, ValidatePrefabRootNotRenamed, ValidatePlanTarget
  * PsdHierarchyNativeCleanupExecutor
      unsupported non-empty arrays, verify schema, containment/flatSibling must be empty
  * PsdHierarchyPrefabExtraction (ValidateComponentFamilyDecisions)
      one decision per mandatory candidate, exact parent, set-equal sources, same-mode extraction
  * PsdHierarchyChatClient
      BuildComponentFamilyCandidates, BuildFlatSiblingFindings, BuildStructureSignature,
      HasCommonDirectChildName, HasConsistentRectTransformFrame, TryGetRepeatedFamilyParts,
      TryGetGeneratedFlatSiblingFamilyStem, TryGetAreaRatioIfContained, ContainmentAreaRatioLimit

Usage
-----
  python preflight_v2.py --plan plan.json --snapshot snapshot.json [--project-root <dir>]
                         [--json-out report.json]

Exit 0 = Unity should accept the plan before any write. Exit 2 = at least one would-be
rejection. Warnings and notes never fail the run.
"""
from __future__ import annotations

import argparse
import collections
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

from parse_hierarchy_snapshot import load_snapshot  # noqa: E402
from simulate_and_verify_plan import simulate, verify_contracts  # noqa: E402

# ---------------------------------------------------------------- Unity constants

REQUIRED_ARRAY_PROPERTIES = (
    "wrappers", "moves", "renames", "emptyContainerRemovals", "tightBounds",
    "textureRenames", "spriteAtlasRenames", "componentFamilyDecisions",
    "containmentResolutions", "flatSiblingResolutions",
    "componentExtractions", "stateComponentExtractions",
    "variantComponentExtractions", "statefulComponentExtractions",
)

SUPPORTED_ROOT_ARRAY_PROPERTIES = set(REQUIRED_ARRAY_PROPERTIES) | {
    "requiredComponentFamilies", "containmentFindings", "flatSiblingFindings",
    "selectedPrefabExtractions", "crossParentPrefabExtractions", "postGroupingExtractionIntents",
}

REVIEW_METADATA_ARRAY_PROPERTIES = {"selectionNodeIds", "expectedHierarchy", "directChildren", "absentPaths"}

SUPPORTED_ROOT_PROPERTIES = SUPPORTED_ROOT_ARRAY_PROPERTIES | {
    "version", "snapshotFingerprint", "prefabAssetPath", "targetPrefabAssetPath",
    "operationScope", "expectedNodeCount", "preserveRequirements", "reviewVersion",
    "output", "prefabName", "verify",
} | REVIEW_METADATA_ARRAY_PROPERTIES

SUPPORTED_VERIFY_FIELDS = ("nodes", "hierarchy", "absentPaths", "directChildren", "tightBounds")

# Unity refuses a non-empty array here before any write.
MUST_BE_EMPTY_ARRAYS = (
    "containmentResolutions", "flatSiblingResolutions",
    "selectedPrefabExtractions", "crossParentPrefabExtractions",
)

EXTRACTION_ARRAYS = (
    "componentExtractions", "stateComponentExtractions",
    "variantComponentExtractions", "statefulComponentExtractions",
)

VALID_MODES = ("component", "state", "variant", "stateful")

SEMANTIC_ASCII_RE = re.compile(r"^[A-Za-z0-9_\[\]]+$")
NON_SEMANTIC_TOKEN_RE = re.compile(
    r"^(?:\d+(?:_\d+)?|\d+(?:\.\d+)?[kKmM]|\d+[A-Za-z]\d+[A-Za-z]|[+_-]+|img_v\d.*|ui_[A-Za-z0-9_]+)$"
)
CONTAINMENT_AREA_RATIO_LIMIT = 0.25
GENERATED_FLAT_SIBLING_STEM = "__generated_flat_sibling__"
GENERATED_FLAT_SIBLING_PREFIX = "FlatSibling_flat_sibling_"


class Preflight:
    """Findings are bucketed by what Unity ACTUALLY does, not by how bad they feel.

    reject       Unity refuses before any write (`rejected`). Cheapest place to fail.
    partial_risk Unity applies the plan, then its own post-save verification fails ->
                 `partial`: the Prefab is ALREADY WRITTEN and needs a fresh review.
    quality      Unity accepts the plan and saves a Prefab that is wrong (0x0 container, a stale
                 count field nobody compares). No gate upstream of the save will stop it.
    """

    def __init__(self):
        self.reject, self.partial_risk, self.quality = [], [], []
        self.warnings, self.notes = [], []

    @property
    def errors(self):
        """Every finding that should stop a publication."""
        return self.reject + self.partial_risk + self.quality

    def error(self, message):
        """Schema/structure/naming/decision problems: Unity refuses before writing."""
        self.reject.append(message)

    def partial(self, message):
        """Would be written, then fail Unity's own post-save verification."""
        self.partial_risk.append(message)

    def quality_issue(self, message):
        """Unity accepts it silently; the saved Prefab is still wrong."""
        self.quality.append(message)

    def warn(self, message):
        self.warnings.append(message)

    def note(self, message):
        self.notes.append(message)


# ---------------------------------------------------------------- C# name helpers


def _trim_brackets(value):
    return (value or "").strip().strip("[]").strip()


def try_get_repeated_family_parts(name):
    """Port of PsdHierarchyChatClient.TryGetRepeatedFamilyParts -> stem or None."""
    value = _trim_brackets(name)
    digits_start = len(value)
    while digits_start > 0 and value[digits_start - 1].isdigit():
        digits_start -= 1
    if digits_start == len(value) or digits_start == 0:
        return None
    try:
        int(value[digits_start:])
    except ValueError:
        return None
    stem_end = digits_start
    while stem_end > 0 and value[stem_end - 1] in "_- ":
        stem_end -= 1
    candidate = value[:stem_end]
    if not candidate or not candidate[0].isalpha() or any(not c.isalnum() for c in candidate):
        return None
    return candidate


def is_generated_flat_sibling_name(name):
    value = _trim_brackets(name)
    return (value.startswith(GENERATED_FLAT_SIBLING_PREFIX)
            and len(value) > len(GENERATED_FLAT_SIBLING_PREFIX)
            and value[len(GENERATED_FLAT_SIBLING_PREFIX):].isdigit())


def try_get_bare_repeated_index(name):
    value = _trim_brackets(name)
    return int(value) if value.isdigit() else None


def try_get_bare_numbered_family_stem(parent_name):
    stem = _trim_brackets(parent_name)
    if not stem or not stem[0].isalpha() or any(not c.isalnum() for c in stem):
        return None
    if stem.endswith("ies") and len(stem) > 3:
        return stem[:-3] + "y"
    if stem.endswith("s") and len(stem) > 1 and not stem.endswith(("ss", "us", "is")):
        return stem[:-1]
    return stem


def is_explicit_structural_group(name):
    value = (name or "").strip()
    return len(value) > 2 and value[0] == "[" and value[-1] == "]"


def is_non_semantic_object_name(name):
    value = name or ""
    return not SEMANTIC_ASCII_RE.match(value) or bool(NON_SEMANTIC_TOKEN_RE.match(value))


# ---------------------------------------------------------------- predicted snapshot


class PredictedNode:
    """One node of the Prefab as it will look AFTER the plan is applied."""

    __slots__ = ("key", "name", "components", "frame", "world_rect", "existing_id", "sibling", "children")

    def __init__(self, key, name, components, frame, world_rect, existing_id, sibling):
        self.key = key
        self.name = name
        self.components = list(components or [])
        self.frame = frame                      # (anchorMin, anchorMax, pivot) or (None, None, None)
        self.world_rect = tuple(world_rect) if world_rect else None
        self.existing_id = existing_id          # snapshot id, or None for a plan-created wrapper
        self.sibling = int(sibling) if sibling is not None else None
        self.children: list[str] = []

    @property
    def child_count(self):
        return len(self.children)

    @property
    def sort_key(self):
        """C# orders by `siblingIndex ?? int.MaxValue`."""
        return self.sibling if self.sibling is not None else 2 ** 31 - 1


def _rect_frame(raw_rect):
    if not isinstance(raw_rect, dict):
        return (None, None, None)
    return (raw_rect.get("anchorMin"), raw_rect.get("anchorMax"), raw_rect.get("pivot"))


def _approx_equal(left, right, tolerance=0.001):
    return abs(left[0] - right[0]) <= tolerance and abs(left[1] - right[1]) <= tolerance


def consistent_frame(nodes):
    """Port of HasConsistentRectTransformFrame."""
    if len(nodes) < 2:
        return False
    first = nodes[0].frame
    if any(part is None for part in first):
        return False
    for node in nodes[1:]:
        other = node.frame
        if any(part is None for part in other):
            return False
        if not (_approx_equal(first[0], other[0]) and _approx_equal(first[1], other[1])
                and _approx_equal(first[2], other[2])):
            return False
    return True


def build_predicted_snapshot(plan, records, sim):
    """Rebuild the saved Prefab: final names, wrapper rects, child counts.

    Unity re-reads the snapshot after apply, so prediction must use FINAL names (renames are
    applied before the save). Wrapper rects are the union of their members, which is exactly what
    `tightBounds` produces; wrappers themselves get a (0.5,0.5) point frame from CreateWrapper.
    """
    by_key = {}
    for record in records:
        raw = record["node"]
        by_key[record["path"]] = PredictedNode(
            key=record["path"], name=record["name"], components=raw.get("components"),
            frame=_rect_frame(raw.get("rect")), world_rect=raw.get("worldRect"),
            existing_id=record["id"], sibling=raw.get("siblingIndex"),
        )

    wrapper_names, wrapper_siblings = {}, {}
    for wrapper in _read_objects(plan, "wrappers"):
        wrapper_names[wrapper.get("id")] = wrapper.get("name") or wrapper.get("id")
        wrapper_siblings[wrapper.get("id")] = wrapper.get("siblingIndex")
    for key in sim.kids:
        if key.startswith("@") and key not in by_key:
            by_key[key] = PredictedNode(
                key=key, name=wrapper_names.get(key[1:], key[1:]),
                components=["UnityEngine.RectTransform"],
                frame=([0.5, 0.5], [0.5, 0.5], [0.5, 0.5]),
                world_rect=None, existing_id=None, sibling=wrapper_siblings.get(key[1:]),
            )

    for key, node in by_key.items():
        node.children = [child for child in sim.kids.get(key, []) if child in by_key]

    key_by_id = {node.existing_id: node.key for node in by_key.values() if node.existing_id}
    for rename in _read_objects(plan, "renames"):
        target = rename.get("target") or ""
        key = key_by_id.get(target[5:]) if target.startswith("node:") else (target if target.startswith("@") else None)
        if key and key in by_key and rename.get("name"):
            by_key[key].name = rename["name"]

    def union_rect(key, guard):
        node = by_key.get(key)
        if node is None or key in guard:
            return None
        if node.world_rect:
            return node.world_rect
        rects = [union_rect(child, guard | {key}) for child in node.children]
        rects = [rect for rect in rects if rect]
        if not rects:
            return None
        return (min(r[0] for r in rects), min(r[1] for r in rects),
                max(r[2] for r in rects), max(r[3] for r in rects))

    for node in by_key.values():
        if node.world_rect is None:
            node.world_rect = union_rect(node.key, frozenset())
    return by_key


def predicted_paths(by_key, sim):
    """Final post-apply path of every surviving node key."""
    out = {}
    for key in by_key:
        if key not in sim.kids:
            continue
        parts, cur, guard = [], key, set()
        while cur and cur not in guard:
            guard.add(cur)
            parts.append(by_key[cur].name if cur in by_key else str(cur))
            cur = sim.par.get(cur)
        out[key] = "/".join(reversed(parts))
    return out


# ---------------------------------------------------------------- predicted candidates


def _structure_signature(key, by_key):
    node = by_key[key]
    components = ",".join(node.components)
    if not node.children:
        return "(" + components + ")"
    ordered = sorted(node.children, key=lambda child: by_key[child].sort_key)
    return "(" + components + "[" + ",".join(_structure_signature(c, by_key) for c in ordered) + "])"


def _has_common_direct_child_name(group, by_key):
    common = None
    for key in group:
        names = {by_key[child].name for child in by_key[key].children}
        common = names if common is None else common & names
        if not common:
            return False
    return bool(common)


def _parent_key_of(key, by_key):
    for node in by_key.values():
        if key in node.children:
            return node.key
    return None


def build_predicted_candidates(by_key):
    """Port of PsdHierarchyChatClient.BuildComponentFamilyCandidates."""
    candidates, emitted, index = [], set(), 1
    for parent_key in sorted(by_key):
        parent = by_key[parent_key]
        groups, bare = {}, []
        for child_key in parent.children:
            child = by_key[child_key]
            if child.child_count <= 0:
                continue
            stem = try_get_repeated_family_parts(child.name)
            if stem is None and is_generated_flat_sibling_name(child.name):
                stem = GENERATED_FLAT_SIBLING_STEM
            if stem is None:
                bare_index = try_get_bare_repeated_index(child.name)
                if bare_index is not None:
                    bare.append((bare_index, child_key))
                continue
            groups.setdefault(stem, []).append(child_key)

        for _, bare_child in bare:
            eligible = [stem for stem, members in groups.items()
                        if len(members) >= 2
                        and consistent_frame([by_key[k] for k in members + [bare_child]])]
            if len(eligible) == 1:
                groups[eligible[0]].append(bare_child)

        if not groups and len({index for index, _ in bare}) >= 3:
            bare_children = [k for _, k in bare]
            stem = try_get_bare_numbered_family_stem(parent.name)
            if stem and consistent_frame([by_key[k] for k in bare_children]):
                groups[stem] = bare_children

        for stem in sorted(groups):
            group = sorted(groups[stem], key=lambda k: by_key[k].sort_key)
            if len(group) < 3 or not consistent_frame([by_key[k] for k in group]):
                continue
            if stem == GENERATED_FLAT_SIBLING_STEM:
                outer = _parent_key_of(parent_key, by_key)
                if outer is not None and by_key[outer].name == parent.name:
                    continue
            signature_key = "|".join(group)
            if signature_key in emitted:
                continue
            emitted.add(signature_key)
            identical = len({_structure_signature(k, by_key) for k in group}) == 1
            has_common = _has_common_direct_child_name(group, by_key)
            suggested = stem if stem != GENERATED_FLAT_SIBLING_STEM else parent.name
            candidates.append({
                "id": "family_%03d" % index,
                "kind": "numbered_repeated",
                "parent": parent_key,
                "sources": list(group),
                "suggestedAssetName": suggested,
                "instanceCount": len(group),
                "recommendedMode": "component" if identical else ("stateful" if has_common else "variant"),
                "requiresExtraction": True,
            })
            index += 1
    return candidates


def build_predicted_flat_siblings(by_key):
    """Port of PsdHierarchyChatClient.BuildFlatSiblingFindings."""
    findings, claimed = [], set()
    by_parent = collections.defaultdict(list)
    for node in by_key.values():
        if node.child_count == 0:
            by_parent[_parent_key_of(node.key, by_key)].append(node.key)

    for parent_key in sorted(by_parent, key=lambda value: (value is None, value or "")):
        parent = by_key.get(parent_key)
        # A bracketed container is already an explicit semantic boundary.
        if parent is not None and is_explicit_structural_group(parent.name):
            continue
        siblings = sorted(by_parent[parent_key], key=lambda k: (by_key[k].sort_key, k))
        for start in range(len(siblings)):
            background = siblings[start]
            background_index = by_key[background].sibling
            if background in claimed or background_index is None:
                continue
            members = [background]
            expected = background_index + 1
            for member in siblings[start + 1:]:
                ratio = _area_ratio_if_contained(by_key[member].world_rect, by_key[background].world_rect)
                if member in claimed or by_key[member].sibling != expected or ratio is None \
                        or ratio > CONTAINMENT_AREA_RATIO_LIMIT:
                    break
                members.append(member)
                expected += 1
            if len(members) < 3:
                continue
            claimed.update(members)
            findings.append({
                "id": "flat_sibling_%03d" % (len(findings) + 1),
                "parent": parent_key,
                "background": background,
                "members": list(members),
            })
    return findings


def _area_ratio_if_contained(inner, outer):
    if not inner or not outer or len(inner) != 4 or len(outer) != 4:
        return None
    tolerance = 0.01
    if inner[0] < outer[0] - tolerance or inner[1] < outer[1] - tolerance \
            or inner[2] > outer[2] + tolerance or inner[3] > outer[3] + tolerance:
        return None
    outer_area = (outer[2] - outer[0]) * (outer[3] - outer[1])
    if outer_area <= 0:
        return None
    return (inner[2] - inner[0]) * (inner[3] - inner[1]) / outer_area


# ---------------------------------------------------------------- helpers


def _read_objects(plan, key):
    return [item for item in (plan.get(key) or []) if isinstance(item, dict)]


def _node_refs(tokens):
    return [token for token in (tokens or []) if isinstance(token, str)]


def _normalize_asset_path(value):
    return (value or "").replace("\\", "/").strip()


def _subtree_existing_ids(key, by_key, guard=None):
    guard = guard or set()
    if key in guard:
        return set()
    node = by_key.get(key)
    if node is None:
        return set()
    out = {node.existing_id} if node.existing_id else set()
    for child in node.children:
        out |= _subtree_existing_ids(child, by_key, guard | {key})
    return out


# ---------------------------------------------------------------- checks


def check_root_contract(report, plan, snapshot_fingerprint, target_prefab_asset_path):
    required_root_name = os.path.basename(_normalize_asset_path(target_prefab_asset_path)).rsplit(".", 1)[0]

    if plan.get("version") != 2:
        report.error("version 必须为 2，当前为 %r" % plan.get("version"))
    fingerprint = plan.get("snapshotFingerprint")
    if not fingerprint:
        report.error("缺少 snapshotFingerprint")
    elif snapshot_fingerprint and fingerprint != snapshot_fingerprint:
        report.error("snapshotFingerprint 与当前快照不一致（计划已过期，必须重新生成 review/plan）")
    if not (plan.get("reviewVersion") or "").strip():
        report.error("缺少 reviewVersion")

    output = plan.get("output")
    if not isinstance(output, dict):
        report.error("缺少 output 对象")
    else:
        if output.get("mode") != "in_place":
            report.error("output.mode 必须为 in_place，当前为 %r" % output.get("mode"))
        if _normalize_asset_path(output.get("assetPath")) != _normalize_asset_path(target_prefab_asset_path):
            report.error("output.assetPath 必须等于当前目标 Prefab 路径")

    if _normalize_asset_path(plan.get("prefabAssetPath")) != _normalize_asset_path(target_prefab_asset_path):
        report.error("prefabAssetPath 必须等于当前目标 Prefab 路径")
    declared_target = plan.get("targetPrefabAssetPath")
    if declared_target is not None and _normalize_asset_path(declared_target) != _normalize_asset_path(target_prefab_asset_path):
        report.error("targetPrefabAssetPath 必须等于当前目标 Prefab 路径")

    for key, value in plan.items():
        if key in SUPPORTED_ROOT_PROPERTIES or value is None:
            continue
        if isinstance(value, list) and not value:
            continue
        report.error("不支持的根字段（必须为空数组或 null）：%s" % key)

    for key in REQUIRED_ARRAY_PROPERTIES:
        if not isinstance(plan.get(key), list):
            report.error("缺少数组字段 %s" % key)
    for key in MUST_BE_EMPTY_ARRAYS:
        if isinstance(plan.get(key), list) and plan[key]:
            report.error("%s 必须为空数组（执行器在任何写入前拒绝非空值）" % key)

    if not isinstance(plan.get("verify"), dict):
        report.error("缺少 verify 对象")

    scope = plan.get("operationScope")
    if scope is None or scope == "" or scope == [] or scope == {}:
        report.error("缺少 operationScope（必须是有内容的对象；空数组会被判 Unknown operation array）")
    elif isinstance(scope, list):
        report.error("operationScope 必须写成对象，不能是数组（否则会被判 Unknown operation array）")

    if not isinstance(plan.get("expectedNodeCount"), int):
        report.error("expectedNodeCount 必须为整数")
    for key in ("preserveRequirements", "expectedHierarchy", "directChildren", "absentPaths"):
        if plan.get(key) is None:
            report.error("缺少 %s" % key)
    if isinstance(plan.get("preserveRequirements"), list):
        report.error("preserveRequirements 必须写成对象，不能是数组（否则会被判 Unknown operation array）")

    selection = plan.get("selectionNodeIds")
    if not isinstance(selection, list) or not selection:
        report.error("selectionNodeIds 必须是非空数组")
    return required_root_name


def check_verify(report, plan, required_root_name):
    verify = plan.get("verify") or {}
    for key in verify:
        if key not in SUPPORTED_VERIFY_FIELDS:
            report.error("verify.%s 不被执行器支持（只允许 %s）" % (key, ", ".join(SUPPORTED_VERIFY_FIELDS)))

    paths = []
    for key in ("hierarchy", "directChildren", "tightBounds"):
        for entry in verify.get(key) or []:
            if isinstance(entry, dict) and entry.get("path"):
                paths.append(entry["path"])
    paths.extend(path for path in (verify.get("absentPaths") or []) if isinstance(path, str))
    for path in paths:
        if path.split("/", 1)[0] != required_root_name:
            report.error("verify 路径必须以未改名的主根 %r 开头，实际为 %r" % (required_root_name, path))
    if not verify.get("absentPaths"):
        report.warn("verify.absentPaths 为空：删除容器时无法证明旧路径已消失")


def check_references(report, plan, snapshot_ids, path_by_id):
    wrapper_ids = {w.get("id") for w in _read_objects(plan, "wrappers")}
    errors_before = len(report.errors)

    def resolve(label, ref, allow_wrapper):
        ref = ref or ""
        if ref.startswith("@"):
            if not allow_wrapper:
                report.error("%s 不允许引用 wrapper：%s" % (label, ref))
            elif ref[1:] not in wrapper_ids:
                report.error("%s 引用了未声明的 wrapper：%s" % (label, ref))
            return
        if not ref.startswith("node:"):
            report.error("%s 必须使用 node:<id>，不能写层级路径：%r" % (label, ref))
            return
        if ref[5:] not in snapshot_ids:
            report.error("%s 引用的节点不在快照中：%s" % (label, ref))

    for wrapper in _read_objects(plan, "wrappers"):
        if not re.match(r"^[a-z][a-z0-9_]*$", wrapper.get("id") or ""):
            report.error("wrapper id 不是 lower_snake_case：%r" % wrapper.get("id"))
        resolve("wrappers[%s].parent" % wrapper.get("id"), wrapper.get("parent"), True)
    for move in _read_objects(plan, "moves"):
        resolve("moves.source", move.get("source"), False)
        if not (move.get("destination") or "").startswith("@"):
            report.error("moves.destination 必须是已声明的 @wrapperId，当前为 %r（不能指向既有节点）"
                         % move.get("destination"))
        else:
            resolve("moves.destination", move.get("destination"), True)
    for rename in _read_objects(plan, "renames"):
        resolve("renames.target", rename.get("target"), True)
    for bound in _read_objects(plan, "tightBounds"):
        resolve("tightBounds.target", bound.get("target"), True)
    for removal in _read_objects(plan, "emptyContainerRemovals"):
        resolve("emptyContainerRemovals.source", removal.get("source"), False)

    required_root_name = os.path.basename(_normalize_asset_path(plan.get("prefabAssetPath"))).rsplit(".", 1)[0]
    for index, rename in enumerate(_read_objects(plan, "renames")):
        target = rename.get("target") or ""
        if target in {entry.get("source") for entry in _read_objects(plan, "emptyContainerRemovals")}:
            # Same verdict as validate_plan_locally: renaming a node you delete is meaningless and
            # makes every final-name expectation for it vacuous.
            report.error("renames[%d] 指向将被删除的节点 %s（与 validate_plan_locally 同一判定）"
                         % (index, target))
        if target.startswith("node:") and target[5:] in path_by_id and "/" not in path_by_id[target[5:]]:
            if rename.get("name") != required_root_name:
                report.error("renames[%d] 试图把主根改名为 %r；主根必须保持资产文件名 %r"
                             % (index, rename.get("name"), required_root_name))
    return errors_before


def check_removal_coverage(report, plan, records):
    """Every removal needs an absentPaths entry that names its EXACT post-apply path.

    Unity does not require this and the local linter only warns. Pairing by leaf name is not
    enough: two different nodes can share a name (this Prefab has two `读条`), so a dropped
    absence proof would otherwise be invisible to every gate.
    """
    by_id = {record["id"]: record for record in records}
    final_name = {record["id"]: record["name"] for record in records}
    for rename in _read_objects(plan, "renames"):
        target = rename.get("target") or ""
        if target.startswith("node:") and target[5:] in final_name:
            final_name[target[5:]] = rename.get("name") or final_name[target[5:]]

    def final_path(node_id):
        parts, cur, guard = [], node_id, set()
        while cur and cur in by_id and cur not in guard:
            guard.add(cur)
            parts.append(final_name.get(cur, by_id[cur]["name"]))
            cur = (by_id[cur]["node"] or {}).get("parentId") or None
        return "/".join(reversed(parts))

    absent = {path for path in (plan.get("absentPaths") or []) if isinstance(path, str)}
    for entry in _read_objects(plan, "emptyContainerRemovals"):
        source = entry.get("source") or ""
        node_id = source[5:] if source.startswith("node:") else None
        if not node_id or node_id not in by_id:
            continue
        expected = final_path(node_id)
        if expected not in absent:
            report.quality_issue("emptyContainerRemovals %s 缺少精确配对的 verify.absentPaths 条目 "
                                 "%r（按叶子名配对会在重名时产生歧义）；删除后没有任何校验证明它消失"
                                 % (source, expected))


def check_moves_and_removals(report, plan, sim, records):
    used = collections.Counter(move.get("source") for move in _read_objects(plan, "moves"))
    for source, count in used.items():
        if count > 1:
            report.error("move.source 被使用 %d 次：%s" % (count, source))
    remove_sources = {entry.get("source") for entry in _read_objects(plan, "emptyContainerRemovals")}
    for source in sorted(remove_sources & set(used), key=str):
        report.error("同一节点既被移动又被删除：%s" % source)
    components_by_id = {record["id"]: (record["node"] or {}).get("components") or [] for record in records}
    for entry in _read_objects(plan, "emptyContainerRemovals"):
        source = entry.get("source") or ""
        node_id = source[5:] if source.startswith("node:") else None
        children = sim.kids.get(sim.key_by_id.get(node_id), []) if node_id else []
        if children:
            report.error("待删除容器在模拟后仍非空（%d 个子节点）：%s" % (len(children), source))
        extra = [component for component in components_by_id.get(node_id, [])
                 if component not in ("UnityEngine.RectTransform", "UnityEngine.Transform")]
        if extra:
            report.error("待删除容器 %s 带有 Transform 以外的组件（%s）；only Transform may be removed"
                         % (source, ", ".join(extra)))


def check_naming(report, plan, records, _unused=None):
    excluded, path_by_id, name_by_id = set(), {}, {}
    nested_instance_roots = []
    for record in records:
        path_by_id[record["id"]] = record["path"]
        name_by_id[record["id"]] = record["name"]
        raw = record["node"] or {}
        if "/" not in record["path"]:
            excluded.add(record["id"])                      # the Prefab root keeps its asset name
        if raw.get("nestedPrefabAssetPath"):
            nested_instance_roots.append(record["path"])     # instance roots are still checked
    for record in records:
        if any(record["path"].startswith(root + "/") for root in nested_instance_roots):
            excluded.add(record["id"])                      # nodes inside a nested instance are not ours
    for removal in _read_objects(plan, "emptyContainerRemovals"):
        ref = removal.get("source") or ""
        if ref.startswith("node:"):
            excluded.add(ref[5:])

    deferred = set()

    def walk(token):
        if isinstance(token, dict):
            for key, value in token.items():
                if key == "sourceName" and isinstance(value, str):
                    deferred.add(value)
                elif key in ("commonSourceNames", "stateSourceNames") and isinstance(value, list):
                    deferred.update(item for item in value if isinstance(item, str))
                else:
                    walk(value)
        elif isinstance(token, list):
            for item in token:
                walk(item)

    walk(plan)

    final = dict(name_by_id)
    for rename in _read_objects(plan, "renames"):
        target = rename.get("target") or ""
        if target.startswith("node:"):
            final[target[5:]] = rename.get("name") or final.get(target[5:], "")

    issues = []
    for node_id in sorted(final):
        if node_id in excluded:
            continue
        new_name = final[node_id]
        if new_name == name_by_id.get(node_id) and new_name in deferred:
            continue
        if is_non_semantic_object_name(new_name):
            issues.append("node:%s (%s) 最终名 %r%s" % (
                node_id, path_by_id.get(node_id, "?"), new_name,
                "" if new_name != name_by_id.get(node_id) else "（计划没给它改名）"))
    if issues:
        report.error("命名闸门：共 %d 个节点在应用后仍不是英文语义名：\n    - %s"
                     % (len(issues), "\n    - ".join(issues[:150])))
        if len(issues) > 150:
            report.error("命名闸门：另有 %d 条同类问题未列出" % (len(issues) - 150))

    wrapper_names = {w.get("id"): w.get("name") or "" for w in _read_objects(plan, "wrappers")}
    for rename in _read_objects(plan, "renames"):
        target = rename.get("target") or ""
        if target.startswith("@"):
            wrapper_names[target[1:]] = rename.get("name") or ""
    for wrapper_id in sorted(wrapper_names):
        if is_non_semantic_object_name(wrapper_names[wrapper_id]):
            report.error("@%s 新容器名 %r 不合规" % (wrapper_id, wrapper_names[wrapper_id]))


def check_private_assets(report, plan, project_root):
    prefab_name = (plan.get("prefabName") or "").strip()
    textures = _read_objects(plan, "textureRenames")
    atlases = _read_objects(plan, "spriteAtlasRenames")
    if not textures and not atlases:
        return
    if not prefab_name:
        report.error("存在私有资源改名时必须给出 prefabName（它就是前缀）")
        return
    for index, texture in enumerate(textures):
        to_name = texture.get("toName") or ""
        if not to_name.startswith(prefab_name + "_"):
            report.error("textureRenames[%d].toName %r 必须以 %r 开头" % (index, to_name, prefab_name + "_"))
        if not texture.get("from"):
            report.error("textureRenames[%d] 缺少 from" % index)
    for index, atlas in enumerate(atlases):
        if (atlas.get("toName") or "") != prefab_name:
            report.error("spriteAtlasRenames[%d].toName %r 必须等于 prefabName %r"
                         % (index, atlas.get("toName"), prefab_name))
    seen = set()
    for entry in textures:
        source = _normalize_asset_path(entry.get("from"))
        if source in seen:
            report.warn("textureRenames 重复列出同一资产：%s" % source)
        seen.add(source)
    if not project_root:
        return
    for entry in list(textures) + list(atlases):
        source = _normalize_asset_path(entry.get("from"))
        if not source:
            continue
        if not os.path.exists(os.path.join(project_root, source)):
            report.warn("改名来源在磁盘上不存在（apply 会被拒绝）：%s" % source)
        extension = source[source.rfind("."):] if "." in source.rsplit("/", 1)[-1] else ""
        target = source.rsplit("/", 1)[0] + "/" + (entry.get("toName") or "") + extension
        if os.path.exists(os.path.join(project_root, target)):
            report.error("改名目标已存在，RenameAsset 会失败：%s" % target)


def check_snapshot_candidates(report, plan, raw_snapshot):
    """Port of PsdHierarchyPrefabExtraction.ValidateComponentFamilyDecisions."""
    candidates = [c for c in (raw_snapshot.get("componentFamilyCandidates") or []) if isinstance(c, dict)]
    decisions = _read_objects(plan, "componentFamilyDecisions")
    by_candidate = collections.defaultdict(list)
    for decision in decisions:
        by_candidate[decision.get("candidateId")].append(decision)

    if not candidates:
        report.note("当前快照没有 componentFamilyCandidates -> componentFamilyDecisions 可以为空数组")
    known_ids = {c.get("id") for c in candidates}
    for decision in decisions:
        candidate_id = decision.get("candidateId")
        if candidate_id and candidate_id not in known_ids:
            report.warn("componentFamilyDecisions 引用了快照中不存在的 candidateId：%r" % candidate_id)
        if decision.get("mode") == "skip" and not (decision.get("reason") or "").strip():
            report.error("componentFamilyDecisions %s 使用 skip 但缺少 reason" % candidate_id)

    for candidate in candidates:
        if not candidate.get("requiresExtraction"):
            continue
        matching = by_candidate.get(candidate.get("id"), [])
        if len(matching) != 1:
            report.error("必抽候选 %s 需要恰好 1 条 componentFamilyDecisions，实际 %d 条"
                         % (candidate.get("id"), len(matching)))
            continue
        decision = matching[0]
        if decision.get("parent") != candidate.get("parent"):
            report.error("候选 %s 的 decision.parent=%r 与快照候选 %r 不一致"
                         % (candidate.get("id"), decision.get("parent"), candidate.get("parent")))
        if set(_node_refs(decision.get("sources"))) != set(_node_refs(candidate.get("sources"))):
            report.error("候选 %s 的 decision.sources 与快照候选不一致：%s vs %s"
                         % (candidate.get("id"), _node_refs(decision.get("sources")),
                            _node_refs(candidate.get("sources"))))
        mode = decision.get("mode")
        if mode not in VALID_MODES:
            report.error("候选 %s 的 mode=%r 不可执行；只能是 %s" % (candidate.get("id"), mode, "/".join(VALID_MODES)))
            continue
        extraction_id = decision.get("extractionId")
        extraction = next((e for e in _read_objects(plan, mode + "Extractions") if e.get("id") == extraction_id), None)
        intent = next((i for i in _read_objects(plan, "postGroupingExtractionIntents")
                       if i.get("id") == extraction_id and i.get("mode") == mode), None)
        if extraction is None and intent is None:
            report.error("候选 %s 的 extractionId=%r 既不是同 mode 的抽取，也不是同 mode 的 postGrouping 意图"
                         % (candidate.get("id"), extraction_id))
            continue
        if extraction is not None:
            sources = set()
            for token in extraction.get("instances") or []:
                sources.add(token if isinstance(token, str) else None)
            if extraction.get("template"):
                sources.add(extraction["template"])
            missing = [s for s in _node_refs(candidate.get("sources")) if s not in sources]
            if missing:
                report.error("候选 %s 的抽取 %s 未覆盖这些来源：%s" % (candidate.get("id"), extraction_id, missing))


def check_predicted_stage(report, plan, by_key, path_map):
    predicted = build_predicted_candidates(by_key)
    findings = build_predicted_flat_siblings(by_key)
    intents = _read_objects(plan, "postGroupingExtractionIntents")

    report.note("预测（分组后）componentFamilyCandidates=%d，flatSiblingFindings=%d"
                % (len(predicted), len(findings)))
    for finding in findings:
        report.warn("预测（分组后）flat-sibling 发现 %s：父 %r，成员 %s。请用普通 wrappers/moves 分组并在中文审查中说明；"
                    "flatSiblingResolutions 必须保持空数组。"
                    % (finding["id"], path_map.get(finding["parent"], finding["parent"]),
                       [path_map.get(m, m) for m in finding["members"]]))
    if not predicted:
        report.note("预测（分组后）没有 requiresExtraction 候选 -> postGroupingExtractionIntents 可以为空")
        return

    reverse = {value: key for key, value in path_map.items()}
    for candidate in predicted:
        if not candidate.get("requiresExtraction"):
            continue
        # A candidate source may be a plan-created wrapper: after apply it has a real node id, so
        # coverage is proven through the existing snapshot nodes inside its subtree.
        source_sets = [_subtree_existing_ids(key, by_key) for key in candidate["sources"]]
        if not any(source_sets):
            report.warn("预测必抽候选 %s 的来源全部是计划新建容器且其子树不含既有节点，无法离线证明覆盖；"
                        "apply 后必须由 Unity 复核。" % candidate["id"])
            continue
        covering = []
        for intent in intents:
            rebuilt = set()
            paths = [intent.get("templatePath")]
            paths += [i.get("path") for i in (intent.get("instances") or []) if isinstance(i, dict)]
            for path in paths:
                key = reverse.get(path) if path else None
                if key:
                    rebuilt |= _subtree_existing_ids(key, by_key)
            if all(not ids or ids <= rebuilt for ids in source_sets):
                covering.append(intent)
        label = "预测必抽候选 %s（父 %r，成员 %s）" % (
            candidate["id"], path_map.get(candidate["parent"], candidate["parent"]),
            [path_map.get(s, s) for s in candidate["sources"]])
        if len(covering) != 1:
            report.error("%s 需要恰好 1 条覆盖它的 postGroupingExtractionIntents，实际 %d 条。"
                         "Unity 会在第二阶段返回 partial。" % (label, len(covering)))
            continue
        report.note("%s 由 intent %r（mode=%s）覆盖" % (label, covering[0].get("id"), covering[0].get("mode")))
        decision = next((d for d in _read_objects(plan, "componentFamilyDecisions")
                         if d.get("extractionId") == covering[0].get("id")), None)
        if decision is not None and decision.get("mode") != covering[0].get("mode"):
            report.error("%s 的 intent mode=%r 与 decision mode=%r 不一致"
                         % (label, covering[0].get("mode"), decision.get("mode")))


def check_engine_reality(report, plan, sim):
    wrappers = _read_objects(plan, "wrappers")
    if wrappers:
        tightened = {entry.get("target") for entry in _read_objects(plan, "tightBounds")}
        missing = [w.get("id") for w in wrappers if "@" + (w.get("id") or "") not in tightened]
        if missing:
            # Unity creates wrappers at sizeDelta 0 and does NOT tighten them itself; a wrapper
            # missing from tightBounds saves as 0x0 and nothing rejects it.
            report.quality_issue("R1：新容器没有出现在 tightBounds 中，落盘后会是 0x0：%s" % ", ".join(missing))
    for key in EXTRACTION_ARRAYS:
        for extraction in _read_objects(plan, key):
            asset_path = extraction.get("assetPath") or ""
            stem = asset_path.rsplit("/", 1)[-1].rsplit(".", 1)[0]
            if not asset_path.endswith(".prefab") or not re.match(r"^[A-Z][A-Za-z0-9]*$", stem) \
                    or "/Common/" not in asset_path:
                report.warn("抽取 %s 的 assetPath 应为 PascalCase .prefab 且位于同级 Common/：%s"
                            % (extraction.get("id"), asset_path))
    for intent in _read_objects(plan, "postGroupingExtractionIntents"):
        if intent.get("mode") not in VALID_MODES:
            report.error("postGroupingExtractionIntents %s 的 mode=%r 非法" % (intent.get("id"), intent.get("mode")))
            continue
        if intent.get("mode") == "component":
            if intent.get("states") or intent.get("defaultState"):
                report.error("postGroupingExtractionIntents %s 为 component 模式时 states 与 defaultState 必须为空"
                             % intent.get("id"))
        else:
            if not intent.get("states"):
                report.error("postGroupingExtractionIntents %s 为非 component 模式但未声明 states" % intent.get("id"))
            declared = {s.get("id") for s in (intent.get("states") or []) if isinstance(s, dict)}
            for instance in (intent.get("instances") or []):
                if isinstance(instance, dict) and instance.get("state") not in declared:
                    report.error("postGroupingExtractionIntents %s 的实例 %r 引用了未声明的 state"
                                 % (intent.get("id"), instance.get("path")))
            if intent.get("defaultState") not in declared:
                report.error("postGroupingExtractionIntents %s 的 defaultState 未在 states 中声明" % intent.get("id"))


def check_expected_contract(report, plan, sim):
    # `expectedNodeCount` is only presence-checked by Unity (PsdWorkflowSafety.TryValidate) and is
    # never compared with the result -- a wrong value is silently accepted, so it is a quality
    # issue, not a rejection.
    if isinstance(plan.get("expectedNodeCount"), int) and plan["expectedNodeCount"] != len(sim.kids):
        report.quality_issue("expectedNodeCount=%d 与模拟节点数 %d 不一致（Unity 不比对，会静默保存；此处按质量错误处理）"
                             % (plan["expectedNodeCount"], len(sim.kids)))
    # `verify.nodes` IS compared after the save: a mismatch is reported as VERIFY_WARN after an
    # already-saved apply, i.e. `partial`.
    verify = plan.get("verify") or {}
    if isinstance(verify.get("nodes"), int) and verify["nodes"] != len(sim.kids):
        report.partial("verify.nodes=%d 与模拟节点数 %d 不一致 -> 保存后校验会失败（partial）"
                       % (verify["nodes"], len(sim.kids)))
    errors, _ = verify_contracts(plan, sim)
    for message in errors:
        report.partial("模拟契约（保存后校验）：%s" % message)


# ---------------------------------------------------------------- main


def run(plan_path, snapshot_path, project_root=None):
    report = Preflight()
    with open(plan_path, encoding="utf-8-sig") as handle:
        plan = json.load(handle)
    with open(snapshot_path, encoding="utf-8-sig") as handle:
        raw_snapshot = json.load(handle)
    summary, records = load_snapshot(snapshot_path)
    snapshot_ids = {record["id"] for record in records}
    path_by_id = {record["id"]: record["path"] for record in records}

    target = plan.get("targetPrefabAssetPath") or plan.get("prefabAssetPath") or ""
    required_root_name = check_root_contract(report, plan, summary.get("fingerprint", ""), target)

    check_verify(report, plan, required_root_name)
    check_references(report, plan, snapshot_ids, path_by_id)

    for entry in plan.get("selectionNodeIds") or []:
        if not isinstance(entry, str) or not entry.startswith("node:") or entry[5:] not in snapshot_ids:
            report.error("selectionNodeIds 含无效引用：%r" % entry)

    check_private_assets(report, plan, project_root)
    check_removal_coverage(report, plan, records)
    check_snapshot_candidates(report, plan, raw_snapshot)

    try:
        sim = simulate(plan, records)
    except SystemExit as exc:
        report.error("模拟失败：%s" % exc)
        return report

    check_moves_and_removals(report, plan, sim, records)
    check_expected_contract(report, plan, sim)
    check_engine_reality(report, plan, sim)
    check_naming(report, plan, records, None)

    by_key = build_predicted_snapshot(plan, records, sim)
    path_map = predicted_paths(by_key, sim)
    check_predicted_stage(report, plan, by_key, path_map)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--plan", required=True)
    parser.add_argument("--snapshot", required=True)
    parser.add_argument("--project-root", help="用于检查改名来源存在性/目标冲突（可选）")
    parser.add_argument("--json-out")
    parser.add_argument("--quiet", action="store_true")
    args = parser.parse_args()

    report = run(args.plan, args.snapshot, args.project_root)

    if not args.quiet:
        for message in report.notes:
            print("NOTE  : " + message)
        for message in report.warnings:
            print("WARN  : " + message)
        for message in report.reject:
            print("REJECT: " + message)
        for message in report.partial_risk:
            print("PARTIAL: " + message)
        for message in report.quality:
            print("QUALITY: " + message)
        print("summary: reject=%d  would-be-partial=%d  quality=%d  warning=%d  note=%d"
              % (len(report.reject), len(report.partial_risk), len(report.quality),
                 len(report.warnings), len(report.notes)))
        if report.reject:
            print("  -> Unity would refuse BEFORE any write; nothing has changed on disk.")
        elif report.partial_risk:
            print("  -> Unity would WRITE the Prefab and then fail its own post-save verification.")
        elif report.quality:
            print("  -> Unity would accept this and save a wrong result; no gate would stop it.")
        else:
            print("  -> no rejection, no post-save verification risk, no quality gap detected.")

    if args.json_out:
        with open(args.json_out, "w", encoding="utf-8") as handle:
            json.dump({"reject": report.reject, "partialRisk": report.partial_risk,
                       "quality": report.quality, "warnings": report.warnings,
                       "notes": report.notes}, handle, ensure_ascii=False, indent=2)
    return 2 if report.reject else (1 if (report.partial_risk or report.quality) else 0)


if __name__ == "__main__":
    sys.exit(main())
