#!/usr/bin/env python3
"""Validate a Prefab hierarchy cleanup plan and render a Unity Editor C# payload."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path, PurePosixPath
from typing import Any


GUID_RE = re.compile(r"^[0-9a-f]{32}$")
VIEW_RE = re.compile(r"^[A-Z][A-Za-z0-9]*View$")
COMPONENT_PREFAB_NAME_RE = re.compile(r"^[A-Z][A-Za-z0-9]*$")


def fail(message: str) -> None:
    raise ValueError(message)


def require_string(value: Any, label: str) -> str:
    if not isinstance(value, str) or not value:
        fail(f"{label} must be a non-empty string")
    return value


def require_list(value: Any, label: str) -> list[dict[str, Any]]:
    if not isinstance(value, list):
        fail(f"{label} must be a list")
    if not all(isinstance(item, dict) for item in value):
        fail(f"{label} entries must be objects")
    return value


def asset_path(value: Any, label: str) -> str:
    path = require_string(value, label)
    if not path.startswith("Assets/"):
        fail(f"{label} must be project-relative and start with Assets/")
    return path


def component_prefab_asset_path(value: Any, label: str, prefab_path: str) -> str:
    path = asset_path(value, label)
    expected_directory = PurePosixPath(prefab_path).parent / "Common"
    path_object = PurePosixPath(path)
    if path_object.parent != expected_directory:
        fail(f"{label} must be directly under {expected_directory.as_posix()}")
    if path_object.suffix != ".prefab":
        fail(f"{label} must end with .prefab")
    if not COMPONENT_PREFAB_NAME_RE.match(path_object.stem):
        fail(f"{label} filename must be PascalCase")
    return path


def csharp(value: str) -> str:
    return json.dumps(value, ensure_ascii=True)


def csharp_string_array(values: list[str]) -> str:
    if not values:
        return "new string[0]"
    return "new string[] { " + ", ".join(csharp(value) for value in values) + " }"


def final_asset_path(source: str, new_name: str) -> str:
    source_path = PurePosixPath(source)
    return str(source_path.with_name(new_name + source_path.suffix))


def state_component_target_path(extraction: dict[str, Any]) -> str:
    parent_path = extraction["template"].rsplit("/", 1)[0]
    return parent_path + "/" + PurePosixPath(extraction["assetPath"]).stem


def validate_wrapper_reference(
    value: str,
    label: str,
    wrapper_ids: set[str],
    unknown_wrapper_message: str,
) -> None:
    if not value.startswith("@"):
        return

    wrapper_id = value[1:]
    if "/" in wrapper_id or "\\" in wrapper_id:
        fail(
            f"{label} wrapper reference must be exactly @wrapperId; "
            "use the original pre-apply full path to address an existing child"
        )
    if wrapper_id not in wrapper_ids:
        fail(unknown_wrapper_message)


def normalize_plan(raw: dict[str, Any], mode: str) -> dict[str, Any]:
    if mode not in {"apply", "preflight", "verify", "reapply"}:
        fail(f"unsupported render mode: {mode}")
    if raw.get("version") != 1:
        fail("version must be 1")

    prefab_path = asset_path(raw.get("prefabAssetPath"), "prefabAssetPath")
    output = raw.get("output")
    if not isinstance(output, dict):
        fail("output must be an object")
    output_mode = require_string(output.get("mode"), "output.mode")
    output_path = asset_path(output.get("assetPath"), "output.assetPath")
    if output_mode != "in_place":
        fail("output.mode must be in_place; this cleanup never creates a copy or .cleaned.prefab")
    if output_path != prefab_path:
        fail("in_place output.assetPath must exactly equal prefabAssetPath")
    component_owner_prefab_path = prefab_path
    if mode == "reapply" and raw.get("replaySourcePrefabAssetPath") is not None:
        component_owner_prefab_path = asset_path(
            raw.get("replaySourcePrefabAssetPath"),
            "replaySourcePrefabAssetPath",
        )
        if not component_owner_prefab_path.endswith(".prefab"):
            fail("replaySourcePrefabAssetPath must end with .prefab")

    prefab_name = require_string(raw.get("prefabName"), "prefabName")
    wrappers = require_list(raw.get("wrappers", []), "wrappers")
    moves = require_list(raw.get("moves", []), "moves")
    renames = require_list(raw.get("renames", []), "renames")
    empty_container_removals = require_list(raw.get("emptyContainerRemovals", []), "emptyContainerRemovals")
    tight_bounds = require_list(raw.get("tightBounds", []), "tightBounds")
    texture_renames = require_list(raw.get("textureRenames", []), "textureRenames")
    atlas_renames = require_list(raw.get("spriteAtlasRenames", []), "spriteAtlasRenames")
    component_extractions = require_list(raw.get("componentExtractions", []), "componentExtractions")
    state_component_extractions = require_list(raw.get("stateComponentExtractions", []), "stateComponentExtractions")
    variant_component_extractions = require_list(raw.get("variantComponentExtractions", []), "variantComponentExtractions")
    stateful_component_extractions = require_list(raw.get("statefulComponentExtractions", []), "statefulComponentExtractions")
    verify = raw.get("verify", {})
    if not isinstance(verify, dict):
        fail("verify must be an object")
    component_family_decisions = require_list(
        raw.get("componentFamilyDecisions", []),
        "componentFamilyDecisions",
    )

    if (texture_renames or atlas_renames) and not VIEW_RE.match(prefab_name):
        fail("prefabName must be PascalCase and end with View when renaming private assets")
    wrapper_ids: set[str] = set()
    for index, wrapper in enumerate(wrappers):
        wrapper_id = require_string(wrapper.get("id"), f"wrappers[{index}].id")
        if not re.match(r"^[a-z][a-z0-9_]*$", wrapper_id):
            fail(f"wrappers[{index}].id must be lower snake_case")
        if wrapper_id in wrapper_ids:
            fail(f"duplicate wrapper id: {wrapper_id}")
        wrapper_ids.add(wrapper_id)
        parent = require_string(wrapper.get("parent"), f"wrappers[{index}].parent")
        validate_wrapper_reference(
            parent,
            f"wrappers[{index}].parent",
            wrapper_ids,
            f"wrappers[{index}].parent references an unknown or later wrapper",
        )
        if not parent.startswith("@"):
            require_string(parent, f"wrappers[{index}].parent")
        require_string(wrapper.get("name"), f"wrappers[{index}].name")
        if not isinstance(wrapper.get("siblingIndex"), int) or wrapper["siblingIndex"] < 0:
            fail(f"wrappers[{index}].siblingIndex must be a non-negative integer")

    move_sources: set[str] = set()
    for index, move in enumerate(moves):
        source = require_string(move.get("source"), f"moves[{index}].source")
        destination = require_string(move.get("destination"), f"moves[{index}].destination")
        if source.startswith("@"):
            fail(f"moves[{index}].source must use the original pre-apply full path")
        if source in move_sources:
            fail(f"each move source must be unique: {source}")
        move_sources.add(source)
        validate_wrapper_reference(
            destination,
            f"moves[{index}].destination",
            wrapper_ids,
            f"moves[{index}].destination references an unknown wrapper",
        )
        if not isinstance(move.get("siblingIndex"), int) or move["siblingIndex"] < 0:
            fail(f"moves[{index}].siblingIndex must be a non-negative integer")

    for index, rename in enumerate(renames):
        target = require_string(rename.get("target"), f"renames[{index}].target")
        validate_wrapper_reference(
            target,
            f"renames[{index}].target",
            wrapper_ids,
            f"renames[{index}].target references an unknown wrapper",
        )
        require_string(rename.get("name"), f"renames[{index}].name")

    removal_sources: set[str] = set()
    for index, removal in enumerate(empty_container_removals):
        source = require_string(removal.get("source"), f"emptyContainerRemovals[{index}].source")
        if source.startswith("@"):
            fail(f"emptyContainerRemovals[{index}].source must use the original pre-apply full path")
        if "/" not in source:
            fail(f"emptyContainerRemovals[{index}].source must not be the Prefab root")
        if source in removal_sources:
            fail(f"duplicate empty container removal source: {source}")
        removal_sources.add(source)

    if not tight_bounds:
        tight_bounds = [{"target": "@" + wrapper["id"]} for wrapper in wrappers]

    tight_targets: set[str] = set()
    for index, tight_bound in enumerate(tight_bounds):
        target = require_string(tight_bound.get("target"), f"tightBounds[{index}].target")
        validate_wrapper_reference(
            target,
            f"tightBounds[{index}].target",
            wrapper_ids,
            f"tightBounds[{index}].target references an unknown wrapper",
        )
        if target in tight_targets:
            fail(f"duplicate tightBounds target: {target}")
        tight_targets.add(target)
    for wrapper_id in wrapper_ids:
        if "@" + wrapper_id not in tight_targets:
            fail(f"wrappers must have a tightBounds entry: @{wrapper_id}")

    extraction_ids: set[str] = set()
    extraction_asset_paths: set[str] = set()
    extraction_instance_paths: set[str] = set()
    for index, extraction in enumerate(component_extractions):
        extraction_id = require_string(extraction.get("id"), f"componentExtractions[{index}].id")
        if not re.match(r"^[a-z][a-z0-9_]*$", extraction_id):
            fail(f"componentExtractions[{index}].id must be lower snake_case")
        if extraction_id in extraction_ids:
            fail(f"duplicate component extraction id: {extraction_id}")
        extraction_ids.add(extraction_id)
        template = require_string(extraction.get("template"), f"componentExtractions[{index}].template")
        component_asset_path = component_prefab_asset_path(
            extraction.get("assetPath"), f"componentExtractions[{index}].assetPath", component_owner_prefab_path
        )
        if component_asset_path in extraction_asset_paths:
            fail(f"duplicate component extraction assetPath: {component_asset_path}")
        if component_asset_path in {prefab_path, output_path}:
            fail(f"componentExtractions[{index}].assetPath must not replace the target Prefab")
        extraction_asset_paths.add(component_asset_path)
        instances = extraction.get("instances")
        if not isinstance(instances, list) or not all(isinstance(instance, str) and instance for instance in instances):
            fail(f"componentExtractions[{index}].instances must be a non-empty string list")
        if len(instances) < 2:
            fail(f"componentExtractions[{index}].instances must contain at least two repeated units")
        if template not in instances:
            fail(f"componentExtractions[{index}].template must also appear in instances")
        if len(instances) != len(set(instances)):
            fail(f"componentExtractions[{index}].instances must not contain duplicates")
        for instance_path in instances:
            for existing_path in extraction_instance_paths:
                if instance_path == existing_path:
                    fail(f"component extraction instance appears more than once: {instance_path}")
                if instance_path.startswith(existing_path + "/") or existing_path.startswith(instance_path + "/"):
                    fail(
                        "component extraction instances must not overlap or nest: "
                        f"{instance_path} and {existing_path}"
                    )
            extraction_instance_paths.add(instance_path)

    for index, extraction in enumerate(state_component_extractions):
        extraction_id = require_string(extraction.get("id"), f"stateComponentExtractions[{index}].id")
        if not re.match(r"^[a-z][a-z0-9_]*$", extraction_id):
            fail(f"stateComponentExtractions[{index}].id must be lower snake_case")
        if extraction_id in extraction_ids:
            fail(f"duplicate component extraction id: {extraction_id}")
        extraction_ids.add(extraction_id)
        template = require_string(extraction.get("template"), f"stateComponentExtractions[{index}].template")
        template_parent = template.rsplit("/", 1)[0] if "/" in template else ""
        if not template_parent:
            fail(f"stateComponentExtractions[{index}].template must not be the Prefab root")
        component_asset_path = component_prefab_asset_path(
            extraction.get("assetPath"), f"stateComponentExtractions[{index}].assetPath", component_owner_prefab_path
        )
        if component_asset_path in extraction_asset_paths:
            fail(f"duplicate component extraction assetPath: {component_asset_path}")
        if component_asset_path in {prefab_path, output_path}:
            fail(f"stateComponentExtractions[{index}].assetPath must not replace the target Prefab")
        extraction_asset_paths.add(component_asset_path)
        states = require_list(extraction.get("states"), f"stateComponentExtractions[{index}].states")
        if len(states) < 2:
            fail(f"stateComponentExtractions[{index}].states must contain at least two mutually exclusive states")
        state_ids: set[str] = set()
        state_sources: set[str] = set()
        for state_index, state in enumerate(states):
            state_id = require_string(state.get("id"), f"stateComponentExtractions[{index}].states[{state_index}].id")
            if not re.match(r"^[a-z][a-z0-9_]*$", state_id):
                fail(f"stateComponentExtractions[{index}].states[{state_index}].id must be lower snake_case")
            if state_id in state_ids:
                fail(f"duplicate state id: {state_id}")
            state_ids.add(state_id)
            source = require_string(state.get("source"), f"stateComponentExtractions[{index}].states[{state_index}].source")
            if source.rsplit("/", 1)[0] != template_parent:
                fail(f"stateComponentExtractions[{index}].states[{state_index}].source must be a sibling of template")
            if source in state_sources:
                fail(f"duplicate state source: {source}")
            for existing_path in extraction_instance_paths:
                if source == existing_path or source.startswith(existing_path + "/") or existing_path.startswith(source + "/"):
                    fail(f"state source overlaps another component extraction: {source} and {existing_path}")
            state_sources.add(source)
            extraction_instance_paths.add(source)
            name = require_string(state.get("name"), f"stateComponentExtractions[{index}].states[{state_index}].name")
            if not (name.startswith("[") and name.endswith("]")):
                fail(f"stateComponentExtractions[{index}].states[{state_index}].name must be a bracketed structural state name")
        if template not in state_sources:
            fail(f"stateComponentExtractions[{index}].template must also appear in states[].source")
        default_state = require_string(extraction.get("defaultState"), f"stateComponentExtractions[{index}].defaultState")
        if default_state not in state_ids:
            fail(f"stateComponentExtractions[{index}].defaultState must match a states[].id")

    for index, extraction in enumerate(variant_component_extractions):
        label = f"variantComponentExtractions[{index}]"
        extraction_id = require_string(extraction.get("id"), f"{label}.id")
        if not re.match(r"^[a-z][a-z0-9_]*$", extraction_id):
            fail(f"{label}.id must be lower snake_case")
        if extraction_id in extraction_ids:
            fail(f"duplicate component extraction id: {extraction_id}")
        extraction_ids.add(extraction_id)
        template = require_string(extraction.get("template"), f"{label}.template")
        template_parent = template.rsplit("/", 1)[0] if "/" in template else ""
        if not template_parent:
            fail(f"{label}.template must not be the Prefab root")
        component_asset_path = component_prefab_asset_path(
            extraction.get("assetPath"), f"{label}.assetPath", component_owner_prefab_path
        )
        if component_asset_path in extraction_asset_paths:
            fail(f"duplicate component extraction assetPath: {component_asset_path}")
        if component_asset_path in {prefab_path, output_path}:
            fail(f"{label}.assetPath must not replace the target Prefab")
        extraction_asset_paths.add(component_asset_path)
        common_name = require_string(extraction.get("commonName", "[Common]"), f"{label}.commonName")
        states_name = require_string(extraction.get("statesName", "[States]"), f"{label}.statesName")
        if common_name != "[Common]" or states_name != "[States]":
            fail(f"{label} must use [Common] and [States] containers")
        states = require_list(extraction.get("states"), f"{label}.states")
        if len(states) < 2:
            fail(f"{label}.states must contain at least two visual states")
        state_ids: set[str] = set()
        state_sources: set[str] = set()
        for state_index, state in enumerate(states):
            state_label = f"{label}.states[{state_index}]"
            state_id = require_string(state.get("id"), f"{state_label}.id")
            if not re.match(r"^[a-z][a-z0-9_]*$", state_id):
                fail(f"{state_label}.id must be lower snake_case")
            if state_id in state_ids:
                fail(f"duplicate state id: {state_id}")
            state_ids.add(state_id)
            source = require_string(state.get("source"), f"{state_label}.source")
            if source.rsplit("/", 1)[0] != template_parent:
                fail(f"{state_label}.source must be a sibling of template")
            if source in state_sources:
                fail(f"duplicate state source: {source}")
            for existing_path in extraction_instance_paths:
                if source == existing_path or source.startswith(existing_path + "/") or existing_path.startswith(source + "/"):
                    fail(f"variant state source overlaps another component extraction: {source} and {existing_path}")
            state_sources.add(source)
            extraction_instance_paths.add(source)
            name = require_string(state.get("name"), f"{state_label}.name")
            if not (name.startswith("[") and name.endswith("]")):
                fail(f"{state_label}.name must be a bracketed structural state name")
        if template not in state_sources:
            fail(f"{label}.template must also appear in states[].source")
        default_state = require_string(extraction.get("defaultState"), f"{label}.defaultState")
        if default_state not in state_ids:
            fail(f"{label}.defaultState must match a states[].id")
        instances = require_list(extraction.get("instances"), f"{label}.instances")
        if len(instances) < 2:
            fail(f"{label}.instances must contain at least two list instances")
        instance_sources: set[str] = set()
        instance_names: set[str] = set()
        for instance_index, instance in enumerate(instances):
            instance_label = f"{label}.instances[{instance_index}]"
            source = require_string(instance.get("source"), f"{instance_label}.source")
            if source not in state_sources:
                fail(f"{instance_label}.source must match one of the state sources")
            if source in instance_sources:
                fail(f"duplicate variant component instance source: {source}")
            instance_sources.add(source)
            name = require_string(instance.get("name"), f"{instance_label}.name")
            if not (name.startswith("[") and name.endswith("]")):
                fail(f"{instance_label}.name must be a bracketed semantic item name")
            if name in instance_names:
                fail(f"duplicate variant component instance name: {name}")
            instance_names.add(name)
            state_id = require_string(instance.get("state"), f"{instance_label}.state")
            if state_id not in state_ids:
                fail(f"{instance_label}.state must match a states[].id")
        if instance_sources != state_sources:
            fail(f"{label}.instances must replace every approved state source exactly once")

    for index, extraction in enumerate(stateful_component_extractions):
        label = f"statefulComponentExtractions[{index}]"
        extraction_id = require_string(extraction.get("id"), f"{label}.id")
        if not re.match(r"^[a-z][a-z0-9_]*$", extraction_id):
            fail(f"{label}.id must be lower snake_case")
        if extraction_id in extraction_ids:
            fail(f"duplicate component extraction id: {extraction_id}")
        extraction_ids.add(extraction_id)
        template = require_string(extraction.get("template"), f"{label}.template")
        template_parent = template.rsplit("/", 1)[0] if "/" in template else ""
        if not template_parent:
            fail(f"{label}.template must not be the Prefab root")
        component_asset_path = component_prefab_asset_path(
            extraction.get("assetPath"), f"{label}.assetPath", component_owner_prefab_path
        )
        if component_asset_path in extraction_asset_paths:
            fail(f"duplicate component extraction assetPath: {component_asset_path}")
        if component_asset_path in {prefab_path, output_path}:
            fail(f"{label}.assetPath must not replace the target Prefab")
        extraction_asset_paths.add(component_asset_path)

        common = extraction.get("common")
        if not isinstance(common, dict):
            fail(f"{label}.common must be an object")
        common_source = require_string(common.get("source"), f"{label}.common.source")
        if common_source.rsplit("/", 1)[0] != template_parent:
            fail(f"{label}.common.source must be a sibling of template")
        common_members = require_list(common.get("members"), f"{label}.common.members")
        if not common_members:
            fail(f"{label}.common.members must not be empty")

        def normalize_members(members: list[dict[str, Any]], member_label: str) -> tuple[list[str], list[str]]:
            source_names: list[str] = []
            target_names: list[str] = []
            for member_index, member in enumerate(members):
                source_name = require_string(member.get("sourceName"), f"{member_label}[{member_index}].sourceName")
                target_name = require_string(member.get("name"), f"{member_label}[{member_index}].name")
                source_names.append(source_name)
                target_names.append(target_name)
            if len(source_names) != len(set(source_names)):
                fail(f"{member_label} sourceName entries must not contain duplicates")
            if len(target_names) != len(set(target_names)):
                fail(f"{member_label} name entries must not contain duplicates")
            return source_names, target_names

        common_source_names, common_target_names = normalize_members(common_members, f"{label}.common.members")
        states = require_list(extraction.get("states"), f"{label}.states")
        if len(states) < 2:
            fail(f"{label}.states must contain at least two visual states")
        state_ids: set[str] = set()
        state_sources: set[str] = set()
        state_member_counts: dict[str, int] = {}
        for state_index, state in enumerate(states):
            state_label = f"{label}.states[{state_index}]"
            state_id = require_string(state.get("id"), f"{state_label}.id")
            if not re.match(r"^[a-z][a-z0-9_]*$", state_id):
                fail(f"{state_label}.id must be lower snake_case")
            if state_id in state_ids:
                fail(f"duplicate state id: {state_id}")
            state_ids.add(state_id)
            source = require_string(state.get("source"), f"{state_label}.source")
            if source.rsplit("/", 1)[0] != template_parent:
                fail(f"{state_label}.source must be a sibling of template")
            if source in state_sources:
                fail(f"duplicate state source: {source}")
            state_sources.add(source)
            name = require_string(state.get("name"), f"{state_label}.name")
            if not (name.startswith("[") and name.endswith("]")):
                fail(f"{state_label}.name must be a bracketed structural state name")
            state_members = require_list(state.get("members"), f"{state_label}.members")
            source_names, _ = normalize_members(state_members, f"{state_label}.members")
            if set(source_names).intersection(common_source_names):
                fail(f"{state_label}.members overlap common members")
            state_member_counts[state_id] = len(state_members)

        default_state = require_string(extraction.get("defaultState"), f"{label}.defaultState")
        if default_state not in state_ids:
            fail(f"{label}.defaultState must match a states[].id")
        instances = require_list(extraction.get("instances"), f"{label}.instances")
        if len(instances) < 2:
            fail(f"{label}.instances must contain at least two visible instances")
        instance_sources: set[str] = set()
        instance_names: set[str] = set()
        for instance_index, instance in enumerate(instances):
            instance_label = f"{label}.instances[{instance_index}]"
            source = require_string(instance.get("source"), f"{instance_label}.source")
            if source.rsplit("/", 1)[0] != template_parent:
                fail(f"{instance_label}.source must be a sibling of template")
            if source in instance_sources:
                fail(f"duplicate stateful component instance source: {source}")
            instance_sources.add(source)
            for existing_path in extraction_instance_paths:
                if source == existing_path or source.startswith(existing_path + "/") or existing_path.startswith(source + "/"):
                    fail(f"stateful component source overlaps another component extraction: {source} and {existing_path}")
            extraction_instance_paths.add(source)
            name = require_string(instance.get("name"), f"{instance_label}.name")
            if not (name.startswith("[") and name.endswith("]")):
                fail(f"{instance_label}.name must be a bracketed semantic item name")
            if name in instance_names:
                fail(f"duplicate stateful component instance name: {name}")
            instance_names.add(name)
            state_id = require_string(instance.get("state"), f"{instance_label}.state")
            if state_id not in state_ids:
                fail(f"{instance_label}.state must match a states[].id")
            common_sources = instance.get("commonSourceNames")
            if not isinstance(common_sources, list) or not all(isinstance(item, str) and item for item in common_sources):
                fail(f"{instance_label}.commonSourceNames must be a string list")
            if len(common_sources) != len(common_source_names) or len(common_sources) != len(set(common_sources)):
                fail(f"{instance_label}.commonSourceNames must map every common member once")
            state_sources_for_instance = instance.get("stateSourceNames")
            if not isinstance(state_sources_for_instance, list) or not all(isinstance(item, str) and item for item in state_sources_for_instance):
                fail(f"{instance_label}.stateSourceNames must be a string list")
            if len(state_sources_for_instance) != state_member_counts[state_id] or len(state_sources_for_instance) != len(set(state_sources_for_instance)):
                fail(f"{instance_label}.stateSourceNames must map every selected-state member once")
            if set(common_sources).intersection(state_sources_for_instance):
                fail(f"{instance_label}.commonSourceNames and stateSourceNames must not overlap")
        if template not in instance_sources or common_source not in instance_sources:
            fail(f"{label}.template and common.source must appear in instances[].source")
        if not state_sources.issubset(instance_sources):
            fail(f"{label}.states[].source must appear in instances[].source")

    extraction_decision_modes: dict[str, str] = {}
    extraction_sources_by_id: dict[str, set[str]] = {}
    for decision_mode, entries in (
        ("component", component_extractions),
        ("state", state_component_extractions),
        ("variant", variant_component_extractions),
        ("stateful", stateful_component_extractions),
    ):
        for extraction in entries:
            extraction_id = extraction["id"]
            extraction_decision_modes[extraction_id] = decision_mode
            if decision_mode == "state":
                extraction_sources_by_id[extraction_id] = {
                    state["source"] for state in extraction["states"]
                }
            else:
                extraction_sources_by_id[extraction_id] = {
                    instance["source"] for instance in extraction["instances"]
                }

    declared_extraction_ids: set[str] = set()
    declared_decision_sources: set[str] = set()
    for index, decision in enumerate(component_family_decisions):
        label = f"componentFamilyDecisions[{index}]"
        require_string(decision.get("parent"), f"{label}.parent")
        sources = decision.get("sources")
        if not isinstance(sources, list) or not all(isinstance(source, str) and source for source in sources):
            fail(f"{label}.sources must be a non-empty string list")
        if len(sources) < 2 or len(sources) != len(set(sources)):
            fail(f"{label}.sources must contain at least two unique paths")
        if declared_decision_sources.intersection(sources):
            fail(f"{label}.sources overlap another component family decision")
        declared_decision_sources.update(sources)
        decision_mode = require_string(decision.get("mode"), f"{label}.mode")
        if decision_mode not in {"skip", "component", "state", "variant", "stateful"}:
            fail(f"{label}.mode must be skip, component, state, variant, or stateful")
        require_string(decision.get("reason"), f"{label}.reason")
        if decision_mode == "skip":
            if "extractionId" in decision:
                fail(f"{label}.skip must not define extractionId")
            continue
        extraction_id = require_string(decision.get("extractionId"), f"{label}.extractionId")
        if extraction_id in declared_extraction_ids:
            fail(f"duplicate component family decision extractionId: {extraction_id}")
        declared_extraction_ids.add(extraction_id)
        if extraction_decision_modes.get(extraction_id) != decision_mode:
            fail(f"{label}.extractionId must reference a matching {decision_mode} extraction")
        if set(sources) != extraction_sources_by_id[extraction_id]:
            fail(f"{label}.sources must exactly cover its extraction instances")

    if declared_extraction_ids != set(extraction_decision_modes):
        fail("componentFamilyDecisions must declare every component extraction exactly once")

    asset_targets: set[str] = set()
    for label, entries in (("textureRenames", texture_renames), ("spriteAtlasRenames", atlas_renames)):
        for index, rename in enumerate(entries):
            source = asset_path(rename.get("from"), f"{label}[{index}].from")
            to_name = require_string(rename.get("toName"), f"{label}[{index}].toName")
            if not to_name.startswith(prefab_name + "_") and label == "textureRenames":
                fail(f"{label}[{index}].toName must start with {prefab_name}_")
            if label == "spriteAtlasRenames" and to_name != prefab_name:
                fail(f"{label}[{index}].toName must equal prefabName")
            target = final_asset_path(source, to_name)
            if target in asset_targets:
                fail(f"duplicate rename target: {target}")
            asset_targets.add(target)
            expected_guid = rename.get("expectedGuid")
            if not isinstance(expected_guid, str) or not GUID_RE.match(expected_guid):
                fail(f"{label}[{index}].expectedGuid must be a lowercase Unity GUID")

    hierarchy = require_list(verify.get("hierarchy", []), "verify.hierarchy")
    for index, item in enumerate(hierarchy):
        require_string(item.get("path"), f"verify.hierarchy[{index}].path")
        if not isinstance(item.get("childCount"), int) or item["childCount"] < 0:
            fail(f"verify.hierarchy[{index}].childCount must be a non-negative integer")
    direct_children = require_list(verify.get("directChildren", []), "verify.directChildren")
    for index, item in enumerate(direct_children):
        require_string(item.get("path"), f"verify.directChildren[{index}].path")
        children = item.get("children")
        if not isinstance(children, list) or not all(isinstance(child, str) and child for child in children):
            fail(f"verify.directChildren[{index}].children must be a non-empty string list")
        if len(children) != len(set(children)):
            fail(f"verify.directChildren[{index}].children must not contain duplicates")
    verify_tight_bounds = require_list(verify.get("tightBounds", []), "verify.tightBounds")
    for index, target in enumerate(verify_tight_bounds):
        require_string(target.get("path"), f"verify.tightBounds[{index}].path")
    absent_paths = verify.get("absentPaths", [])
    if not isinstance(absent_paths, list) or not all(isinstance(path, str) and path for path in absent_paths):
        fail("verify.absentPaths must be a string list")
    if "requireEnglishNames" in verify and not isinstance(verify["requireEnglishNames"], bool):
        fail("verify.requireEnglishNames must be a boolean")
    forbidden_name_patterns = verify.get("forbiddenObjectNamePatterns", [])
    if not isinstance(forbidden_name_patterns, list) or not all(isinstance(pattern, str) and pattern for pattern in forbidden_name_patterns):
        fail("verify.forbiddenObjectNamePatterns must be a string list")
    allowed_missing_image_prefixes = verify.get("allowedMissingImagePathPrefixes", [])
    if not isinstance(allowed_missing_image_prefixes, list) or not all(isinstance(prefix, str) and prefix for prefix in allowed_missing_image_prefixes):
        fail("verify.allowedMissingImagePathPrefixes must be a string list")
    if "requireAllPrivateTextureAssetsPrefixed" in verify and not isinstance(verify["requireAllPrivateTextureAssetsPrefixed"], bool):
        fail("verify.requireAllPrivateTextureAssetsPrefixed must be a boolean")

    for key in ("nodes", "components", "objectReferences", "missingComponents", "images", "prefixedTextures"):
        if key in verify and (not isinstance(verify[key], int) or verify[key] < 0):
            fail(f"verify.{key} must be a non-negative integer")
    if verify.get("requireAllImageTexturesPrefixed") and not isinstance(verify.get("texturePathPrefix"), str):
        fail("verify.texturePathPrefix is required when all image textures must be prefixed")
    if verify.get("requireAllPrivateTextureAssetsPrefixed"):
        if not isinstance(verify.get("privateTextureDirectory"), str) or not verify["privateTextureDirectory"].startswith("Assets/"):
            fail("verify.privateTextureDirectory is required when all private Texture assets must be prefixed")
        if not isinstance(verify.get("texturePathPrefix"), str) or not verify["texturePathPrefix"]:
            fail("verify.texturePathPrefix is required when all private Texture assets must be prefixed")

    return {
        "prefab_path": prefab_path,
        "output_mode": output_mode,
        "output_path": output_path,
        "prefab_name": prefab_name,
        "wrappers": wrappers,
        "moves": moves,
        "renames": renames,
        "empty_container_removals": empty_container_removals,
        "tight_bounds": tight_bounds,
        "texture_renames": texture_renames,
        "atlas_renames": atlas_renames,
        "component_extractions": component_extractions,
        "state_component_extractions": state_component_extractions,
        "variant_component_extractions": variant_component_extractions,
        "stateful_component_extractions": stateful_component_extractions,
        "component_family_decisions": component_family_decisions,
        "verify": verify,
    }


def value_or_default(values: dict[str, Any], key: str, default: int = -1) -> int:
    value = values.get(key, default)
    return value if isinstance(value, int) else default


def emit_verification(plan: dict[str, Any], mode: str) -> list[str]:
    verify = plan["verify"]
    require_english_names = bool(verify.get("requireEnglishNames", False))
    forbidden_name_patterns = ", ".join(csharp(pattern) for pattern in verify.get("forbiddenObjectNamePatterns", []))
    allowed_missing_image_prefixes = ", ".join(csharp(prefix) for prefix in verify.get("allowedMissingImagePathPrefixes", []))
    lines = [
        "var reopened = PrefabUtility.LoadPrefabContents(outputPath);",
        "if (reopened == null) throw new InvalidOperationException(\"Prefab did not load for verification: \" + outputPath);",
        "try",
        "{",
        "    try",
        "    {",
        "    var missingComponents = 0;",
        "    var invalidNames = new List<string>();",
        "    var nodes = CountNodes(reopened.transform);",
        "    var components = CountComponents(reopened.transform, ref missingComponents);",
        "    var objectReferences = CountObjectReferences(reopened.transform);",
        "    var images = reopened.GetComponentsInChildren<Image>(true);",
        "    var prefixedTexturePaths = new HashSet<string>(StringComparer.Ordinal);",
        "    var ignoredNestedMissingSprites = new List<string>();",
        "    foreach (var image in images)",
        "    {",
        "        if (image.sprite == null || image.sprite.texture == null)",
        "        {",
        "            var imagePath = TransformPath(image.transform);",
        "            if (IsNestedPrefabContent(image.transform, reopened.transform))",
        "            {",
        "                ignoredNestedMissingSprites.Add(imagePath);",
        "                continue;",
        "            }",
        "            if (!IsAllowedMissingImage(imagePath, allowedMissingImagePathPrefixes)) throw new InvalidOperationException(\"Image has a missing Sprite: \" + imagePath);",
        "            continue;",
        "        }",
        "        var texturePath = AssetDatabase.GetAssetPath(image.sprite.texture);",
        "        if (requireAllImageTexturesPrefixed && !texturePath.StartsWith(texturePathPrefix, StringComparison.Ordinal))",
        "        {",
        "            throw new InvalidOperationException(\"Image references an unexpected Texture: \" + image.transform.name + \" => \" + texturePath);",
        "        }",
        "        if (texturePath.StartsWith(texturePathPrefix, StringComparison.Ordinal))",
        "        {",
        "            prefixedTexturePaths.Add(texturePath);",
        "        }",
        "    }",
        "    AssertExpected(\"nodes\", nodes, expectedNodes);",
        "    AssertExpected(\"components\", components, expectedComponents);",
        "    AssertExpected(\"objectReferences\", objectReferences, expectedObjectReferences);",
        "    AssertExpected(\"missingComponents\", missingComponents, expectedMissingComponents);",
        "    AssertExpected(\"images\", images.Length, expectedImages);",
        "    AssertExpected(\"prefixedTextures\", prefixedTexturePaths.Count, expectedPrefixedTextures);",
    ]
    if require_english_names:
        lines.extend(
            [
                "    CollectInvalidNames(reopened.transform, invalidNames);",
                "    if (invalidNames.Count > 0) throw new InvalidOperationException(\"Non-English semantic object names: \" + string.Join(\", \", invalidNames.ToArray()));",
            ]
        )
    if forbidden_name_patterns:
        lines.extend(
            [
                "    CollectForbiddenNames(reopened.transform, forbiddenObjectNamePatterns, invalidNames);",
                "    if (invalidNames.Count > 0) throw new InvalidOperationException(\"Forbidden object names: \" + string.Join(\", \", invalidNames.ToArray()));",
            ]
        )
    for index, item in enumerate(verify.get("hierarchy", [])):
        lines.extend(
            [
                f"    var hierarchyNode{index} = FindByPath(reopened, {csharp(item['path'])});",
                f"    AssertExpected({csharp(item['path'] + '.childCount')}, hierarchyNode{index}.transform.childCount, {item['childCount']});",
            ]
        )
    for path in verify.get("absentPaths", []):
        lines.append(f"    AssertPathAbsent(reopened, {csharp(path)});")
    for index, item in enumerate(verify.get("directChildren", [])):
        expected_children = csharp_string_array(item["children"])
        lines.extend(
            [
                f"    var directChildrenNode{index} = FindByPath(reopened, {csharp(item['path'])}).transform;",
                f"    AssertDirectChildren(directChildrenNode{index}, {expected_children}, {csharp(item['path'])});",
            ]
        )
    for index, item in enumerate(verify.get("tightBounds", [])):
        lines.extend(
            [
                f"    AssertTightBounds(FindByPath(reopened, {csharp(item['path'])}).GetComponent<RectTransform>(), {csharp(item['path'])});",
            ]
        )
    for extraction in plan["component_extractions"]:
        lines.extend(
            [
                f"    var componentAsset_{extraction['id']} = AssetDatabase.LoadAssetAtPath<GameObject>({csharp(extraction['assetPath'])});",
                f"    if (componentAsset_{extraction['id']} == null) throw new InvalidOperationException(\"Extracted component Prefab did not load: \" + {csharp(extraction['assetPath'])});",
            ]
        )
        for instance_path in extraction["instances"]:
            lines.extend(
                [
                    f"    AssertNestedPrefabInstance(FindByPath(reopened, {csharp(instance_path)}), {csharp(extraction['assetPath'])});",
                ]
            )

    for extraction in plan["state_component_extractions"]:
        target_path = state_component_target_path(extraction)
        state_names = csharp_string_array([state["name"] for state in extraction["states"]])
        default_state = next(state for state in extraction["states"] if state["id"] == extraction["defaultState"])
        lines.extend(
            [
                f"    var stateComponentAsset_{extraction['id']} = AssetDatabase.LoadAssetAtPath<GameObject>({csharp(extraction['assetPath'])});",
                f"    if (stateComponentAsset_{extraction['id']} == null) throw new InvalidOperationException(\"Extracted state component Prefab did not load: \" + {csharp(extraction['assetPath'])});",
                f"    var stateComponentInstance_{extraction['id']} = FindByPath(reopened, {csharp(target_path)});",
                f"    AssertNestedPrefabInstance(stateComponentInstance_{extraction['id']}, {csharp(extraction['assetPath'])});",
                f"    var statesContainer_{extraction['id']} = stateComponentInstance_{extraction['id']}.transform.Find(\"[States]\");",
                f"    if (statesContainer_{extraction['id']} == null) throw new InvalidOperationException(\"State component has no [States] container: \" + stateComponentInstance_{extraction['id']}.name);",
                f"    AssertDirectChildren(statesContainer_{extraction['id']}, {state_names}, {csharp(target_path + '/[States]')});",
                f"    AssertExclusiveActiveState(statesContainer_{extraction['id']}, {csharp(default_state['name'])}, {csharp(target_path)});",
            ]
        )

    for extraction in plan["variant_component_extractions"]:
        state_names = csharp_string_array([state["name"] for state in extraction["states"]])
        lines.extend(
            [
                f"    var variantComponentAsset_{extraction['id']} = AssetDatabase.LoadAssetAtPath<GameObject>({csharp(extraction['assetPath'])});",
                f"    if (variantComponentAsset_{extraction['id']} == null) throw new InvalidOperationException(\"Extracted variant component Prefab did not load: \" + {csharp(extraction['assetPath'])});",
                f"    var variantCommon_{extraction['id']} = variantComponentAsset_{extraction['id']}.transform.Find(\"[Common]\");",
                f"    var variantStates_{extraction['id']} = variantComponentAsset_{extraction['id']}.transform.Find(\"[States]\");",
                f"    if (variantCommon_{extraction['id']} == null || variantStates_{extraction['id']} == null) throw new InvalidOperationException(\"Variant component must contain [Common] and [States]: \" + variantComponentAsset_{extraction['id']}.name);",
                f"    AssertDirectChildren(variantStates_{extraction['id']}, {state_names}, {csharp(extraction['assetPath'] + '/[States]')});",
            ]
        )
        for instance_index, instance in enumerate(extraction["instances"]):
            instance_path = instance["source"].rsplit("/", 1)[0] + "/" + instance["name"]
            expected_state = next(state for state in extraction["states"] if state["id"] == instance["state"])
            lines.extend(
                [
                    f"    var variantInstance_{extraction['id']}_{instance_index} = FindByPath(reopened, {csharp(instance_path)});",
                    f"    AssertNestedPrefabInstance(variantInstance_{extraction['id']}_{instance_index}, {csharp(extraction['assetPath'])});",
                    f"    AssertVariantState(variantInstance_{extraction['id']}_{instance_index}.transform, {csharp(expected_state['name'])}, {csharp(instance_path)});",
                ]
            )

    for extraction in plan["stateful_component_extractions"]:
        common_target_names = csharp_string_array([member["name"] for member in extraction["common"]["members"]])
        state_names = csharp_string_array([state["name"] for state in extraction["states"]])
        lines.extend(
            [
                f"    var statefulComponentAsset_{extraction['id']} = AssetDatabase.LoadAssetAtPath<GameObject>({csharp(extraction['assetPath'])});",
                f"    if (statefulComponentAsset_{extraction['id']} == null) throw new InvalidOperationException(\"Extracted stateful component Prefab did not load: \" + {csharp(extraction['assetPath'])});",
                f"    var statefulCommon_{extraction['id']} = statefulComponentAsset_{extraction['id']}.transform.Find(\"[Common]\");",
                f"    var statefulStates_{extraction['id']} = statefulComponentAsset_{extraction['id']}.transform.Find(\"[States]\");",
                f"    if (statefulCommon_{extraction['id']} == null || statefulStates_{extraction['id']} == null) throw new InvalidOperationException(\"Stateful component must contain [Common] and [States]: \" + statefulComponentAsset_{extraction['id']}.name);",
                f"    AssertDirectChildren(statefulCommon_{extraction['id']}, {common_target_names}, {csharp(extraction['assetPath'] + '/[Common]')});",
                f"    AssertDirectChildren(statefulStates_{extraction['id']}, {state_names}, {csharp(extraction['assetPath'] + '/[States]')});",
            ]
        )
        for state_index, state in enumerate(extraction["states"]):
            member_names = csharp_string_array([member["name"] for member in state["members"]])
            lines.append(f"    AssertDirectChildren(statefulStates_{extraction['id']}.GetChild({state_index}), {member_names}, {csharp(extraction['assetPath'] + '/[States]/' + state['name'])});")
        state_names_by_id = {state["id"]: state["name"] for state in extraction["states"]}
        for instance_index, instance in enumerate(extraction["instances"]):
            instance_path = instance["source"].rsplit("/", 1)[0] + "/" + instance["name"]
            expected_state = state_names_by_id[instance["state"]]
            lines.extend(
                [
                    f"    var statefulInstance_{extraction['id']}_{instance_index} = FindByPath(reopened, {csharp(instance_path)});",
                    f"    AssertNestedPrefabInstance(statefulInstance_{extraction['id']}_{instance_index}, {csharp(extraction['assetPath'])});",
                    f"    AssertVariantState(statefulInstance_{extraction['id']}_{instance_index}.transform, {csharp(expected_state)}, {csharp(instance_path)});",
                ]
            )

    for rename in plan["texture_renames"] + plan["atlas_renames"]:
        final_path = final_asset_path(rename["from"], rename["toName"])
        lines.append(f"    AssertGuid({csharp(final_path)}, {csharp(rename['expectedGuid'])});")
        lines.append(f"    if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>({csharp(final_path)}) == null) throw new InvalidOperationException(\"Renamed asset did not load: \" + {csharp(final_path)});")

    if verify.get("requireAllPrivateTextureAssetsPrefixed", False):
        lines.append("    AssertPrivateTextureAssetNames(privateTextureDirectory, texturePathPrefix);")

    lines.extend(
        [
            "    return \"VERIFY_OK nodes=\" + nodes + \";components=\" + components + \";objectReferences=\" + objectReferences + \";missingComponents=\" + missingComponents + \";images=\" + images.Length + \";prefixedTextures=\" + prefixedTexturePaths.Count + \";ignoredNestedMissingSprites=\" + ignoredNestedMissingSprites.Count + \";ignoredNestedMissingSpritePaths=\" + string.Join(\"|\", ignoredNestedMissingSprites.ToArray());",
            "    }",
            "    catch (Exception verificationError)",
            "    {",
            "        return \"VERIFY_WARN issue=\" + verificationError.Message.Replace(\"\\r\", \" \").Replace(\"\\n\", \" \");",
            "    }",
            "}",
            "finally",
            "{",
            "    PrefabUtility.UnloadPrefabContents(reopened);",
            "}",
        ]
    )
    return lines


def emit_preflight(plan: dict[str, Any]) -> list[str]:
    lines = [
        "if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) throw new InvalidOperationException(\"Prefab did not load: \" + prefabPath);",
        "if (!string.Equals(outputPath, prefabPath, StringComparison.Ordinal)) throw new InvalidOperationException(\"Cleanup must save only the exact target Prefab in place.\");",
    ]

    for index, rename in enumerate(plan["texture_renames"] + plan["atlas_renames"]):
        source = rename["from"]
        target = final_asset_path(source, rename["toName"])
        lines.extend(
            [
                f"if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>({csharp(source)}) == null) throw new InvalidOperationException(\"Source asset did not load: \" + {csharp(source)});",
                f"if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>({csharp(target)}) != null) throw new InvalidOperationException(\"Rename target already exists: \" + {csharp(target)});",
                f"AssertGuid({csharp(source)}, {csharp(rename['expectedGuid'])});",
            ]
        )

    for label, extractions in (
        ("Component", plan["component_extractions"]),
        ("State component", plan["state_component_extractions"]),
        ("Variant component", plan["variant_component_extractions"]),
        ("Stateful component", plan["stateful_component_extractions"]),
    ):
        for extraction in extractions:
            asset = csharp(extraction["assetPath"])
            lines.append(f"if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>({asset}) != null) throw new InvalidOperationException(\"{label} Prefab target already exists: \" + {asset});")

    lines.extend(["var root = PrefabUtility.LoadPrefabContents(prefabPath);", "try", "{"])

    def assert_path(operation: str, path: str) -> None:
        lines.append(f"    AssertPlanPath(root, {csharp(operation)}, {csharp(path)});")

    for index, wrapper in enumerate(plan["wrappers"]):
        if not wrapper["parent"].startswith("@"):
            assert_path(f"wrappers[{index}].parent", wrapper["parent"])
    for index, move in enumerate(plan["moves"]):
        assert_path(f"moves[{index}].source", move["source"])
        if not move["destination"].startswith("@"):
            assert_path(f"moves[{index}].destination", move["destination"])
    for index, rename in enumerate(plan["renames"]):
        if not rename["target"].startswith("@"):
            assert_path(f"renames[{index}].target", rename["target"])
    for index, removal in enumerate(plan["empty_container_removals"]):
        assert_path(f"emptyContainerRemovals[{index}].source", removal["source"])
    for index, tight_bound in enumerate(plan["tight_bounds"]):
        if not tight_bound["target"].startswith("@"):
            assert_path(f"tightBounds[{index}].target", tight_bound["target"])
    for index, decision in enumerate(plan["component_family_decisions"]):
        assert_path(f"componentFamilyDecisions[{index}].parent", decision["parent"])
        for source_index, source in enumerate(decision["sources"]):
            assert_path(f"componentFamilyDecisions[{index}].sources[{source_index}]", source)
    for index, extraction in enumerate(plan["component_extractions"]):
        assert_path(f"componentExtractions[{index}].template", extraction["template"])
        for instance_index, instance in enumerate(extraction["instances"]):
            assert_path(f"componentExtractions[{index}].instances[{instance_index}]", instance)
    for index, extraction in enumerate(plan["state_component_extractions"]):
        assert_path(f"stateComponentExtractions[{index}].template", extraction["template"])
        for state_index, state in enumerate(extraction["states"]):
            assert_path(f"stateComponentExtractions[{index}].states[{state_index}].source", state["source"])
    for index, extraction in enumerate(plan["variant_component_extractions"]):
        assert_path(f"variantComponentExtractions[{index}].template", extraction["template"])
        for state_index, state in enumerate(extraction["states"]):
            assert_path(f"variantComponentExtractions[{index}].states[{state_index}].source", state["source"])
        for instance_index, instance in enumerate(extraction["instances"]):
            assert_path(f"variantComponentExtractions[{index}].instances[{instance_index}].source", instance["source"])
    for index, extraction in enumerate(plan["stateful_component_extractions"]):
        assert_path(f"statefulComponentExtractions[{index}].template", extraction["template"])
        assert_path(f"statefulComponentExtractions[{index}].common.source", extraction["common"]["source"])
        for state_index, state in enumerate(extraction["states"]):
            assert_path(f"statefulComponentExtractions[{index}].states[{state_index}].source", state["source"])
        for instance_index, instance in enumerate(extraction["instances"]):
            assert_path(f"statefulComponentExtractions[{index}].instances[{instance_index}].source", instance["source"])

    for index, wrapper in enumerate(plan["wrappers"]):
        if not wrapper["parent"].startswith("@"):
            lines.append(
                f"    var preflightWrapperParent{index} = FindByPath(root, {csharp(wrapper['parent'])}).transform;"
            )
    for index, move in enumerate(plan["moves"]):
        lines.append(
            f"    var preflightMoveSource{index} = FindByPath(root, {csharp(move['source'])}).transform;"
        )
        if not move["destination"].startswith("@"):
            lines.append(
                f"    var preflightMoveDestination{index} = FindByPath(root, {csharp(move['destination'])}).transform;"
            )
    for index, removal in enumerate(plan["empty_container_removals"]):
        lines.append(
            f"    var preflightRemoval{index} = FindByPath(root, {csharp(removal['source'])}).transform;"
        )

    preflight_wrapper_vars: dict[str, str] = {}
    for index, wrapper in enumerate(plan["wrappers"]):
        parent = wrapper["parent"]
        parent_expr = (
            f"{preflight_wrapper_vars[parent[1:]]}.transform"
            if parent.startswith("@")
            else f"preflightWrapperParent{index}"
        )
        variable = f"preflightWrapper{index}"
        preflight_wrapper_vars[wrapper["id"]] = variable
        lines.append(
            f"    var {variable} = CreateWrapper({parent_expr}, {csharp(wrapper['name'])}, {wrapper['siblingIndex']});"
        )

    for index, move in enumerate(plan["moves"]):
        destination = move["destination"]
        destination_expr = (
            f"{preflight_wrapper_vars[destination[1:]]}.transform"
            if destination.startswith("@")
            else f"preflightMoveDestination{index}"
        )
        lines.append(
            f"    preflightMoveSource{index}.SetParent({destination_expr}, true);"
        )
        lines.append(
            f"    preflightMoveSource{index}.SetSiblingIndex({move['siblingIndex']});"
        )

    if plan["empty_container_removals"]:
        lines.append("    var preflightRemovalErrors = new List<string>();")
    for index, _ in enumerate(plan["empty_container_removals"]):
        lines.extend(
            [
                f"    try {{ RemoveEmptyContainer(root.transform, preflightRemoval{index}); }}",
                f"    catch (Exception preflightRemovalError{index})",
                "    {",
                f"        preflightRemovalErrors.Add(\"emptyContainerRemovals[{index}]: \" + preflightRemovalError{index}.Message);",
                "    }",
            ]
        )
    if plan["empty_container_removals"]:
        lines.append(
            '    if (preflightRemovalErrors.Count > 0) throw new InvalidOperationException("Planned empty container removals are invalid: " + string.Join(" | ", preflightRemovalErrors.ToArray()));'
        )

    lines.extend(
        [
            "    return \"PREFLIGHT_OK\";",
            "}",
            "finally",
            "{",
            "    PrefabUtility.UnloadPrefabContents(root);",
            "}",
        ]
    )
    return lines


def render(plan: dict[str, Any], mode: str) -> str:
    verify = plan["verify"]
    prefix = verify.get("texturePathPrefix", "")
    require_prefix = bool(verify.get("requireAllImageTexturesPrefixed", False))
    private_texture_directory = verify.get("privateTextureDirectory", "")
    forbidden_name_patterns = csharp_string_array(verify.get("forbiddenObjectNamePatterns", []))
    allowed_missing_image_prefixes = csharp_string_array(verify.get("allowedMissingImagePathPrefixes", []))
    lines = [
        "using System;",
        "using System.Collections.Generic;",
        "using System.IO;",
        "using System.Linq;",
        "using UnityEditor;",
        "using UnityEngine;",
        "using UnityEngine.UI;",
        "",
        f"var prefabPath = {csharp(plan['prefab_path'])};",
        f"var outputPath = {csharp(plan['output_path'])};",
        f"var texturePathPrefix = {csharp(prefix)};",
        f"var requireAllImageTexturesPrefixed = {'true' if require_prefix else 'false'};",
        f"var privateTextureDirectory = {csharp(private_texture_directory)};",
        f"var forbiddenObjectNamePatterns = {forbidden_name_patterns};",
        f"var allowedMissingImagePathPrefixes = {allowed_missing_image_prefixes};",
        f"var expectedNodes = {value_or_default(verify, 'nodes')};",
        f"var expectedComponents = {value_or_default(verify, 'components')};",
        f"var expectedObjectReferences = {value_or_default(verify, 'objectReferences')};",
        f"var expectedMissingComponents = {value_or_default(verify, 'missingComponents')};",
        f"var expectedImages = {value_or_default(verify, 'images')};",
        f"var expectedPrefixedTextures = {value_or_default(verify, 'prefixedTextures')};",
        "",
        "GameObject FindByPath(GameObject root, string path)",
        "{",
        "    var parts = path.Split('/');",
        "    var current = root.transform;",
        "    var index = parts.Length > 0 && parts[0] == current.name ? 1 : 0;",
        "    for (; index < parts.Length; index++)",
        "    {",
        "        var segment = parts[index]; var occurrence = 0; var marker = segment.LastIndexOf('#');",
        "        if (marker > 0 && marker < segment.Length - 1 && int.TryParse(segment.Substring(marker + 1), out occurrence) && occurrence >= 0) segment = segment.Substring(0, marker); else occurrence = 0;",
        "        Transform next = null;",
        "        var matched = 0;",
        "        for (var childIndex = 0; childIndex < current.childCount; childIndex++)",
        "        {",
        "            var child = current.GetChild(childIndex);",
        "            if (child.name == segment && matched++ == occurrence) { next = child; break; }",
        "        }",
        "        if (next == null) throw new InvalidOperationException(\"Path was not found: \" + path);",
        "        current = next;",
        "    }",
        "    return current.gameObject;",
        "}",
        "",
        "string PlanPath(Transform node)",
        "{",
        "    var segments = new List<string>();",
        "    for (var current = node; current != null; current = current.parent)",
        "    {",
        "        var segment = current.name;",
        "        if (current.parent != null)",
        "        {",
        "            var occurrence = 0;",
        "            for (var siblingIndex = 0; siblingIndex < current.parent.childCount; siblingIndex++)",
        "            {",
        "                var sibling = current.parent.GetChild(siblingIndex);",
        "                if (sibling.name != current.name) continue;",
        "                if (sibling == current) break;",
        "                occurrence++;",
        "            }",
        "            if (occurrence > 0) segment += \"#\" + occurrence;",
        "        }",
        "        segments.Add(segment);",
        "    }",
        "    segments.Reverse();",
        "    return string.Join(\"/\", segments.ToArray());",
        "}",
        "",
        "string FindCandidateSourcePaths(GameObject root, string path)",
        "{",
        "    var requestedName = path.Substring(path.LastIndexOf('/') + 1);",
        "    var duplicateMarker = requestedName.LastIndexOf('#');",
        "    if (duplicateMarker > 0) requestedName = requestedName.Substring(0, duplicateMarker);",
        "    var candidates = new List<string>();",
        "    foreach (var node in root.GetComponentsInChildren<Transform>(true))",
        "    {",
        "        if (node.name.Length < 3) continue;",
        "        if (node.name.IndexOf(requestedName, StringComparison.OrdinalIgnoreCase) < 0 && requestedName.IndexOf(node.name, StringComparison.OrdinalIgnoreCase) < 0) continue;",
        "        candidates.Add(PlanPath(node));",
        "        if (candidates.Count >= 8) break;",
        "    }",
        "    return string.Join(\", \", candidates.ToArray());",
        "}",
        "",
        "void AssertPlanPath(GameObject root, string operation, string path)",
        "{",
        "    try { FindByPath(root, path); }",
        "    catch (InvalidOperationException)",
        "    {",
        "        var candidates = FindCandidateSourcePaths(root, path);",
        "        var hint = string.IsNullOrEmpty(candidates) ? string.Empty : \" Candidate source paths: \" + candidates;",
        "        throw new InvalidOperationException(\"Plan source path was not found for \" + operation + \": \" + path + hint);",
        "    }",
        "}",
        "",
        "bool PathExists(GameObject root, string path)",
        "{",
        "    var segments = path.Split('/'); if (segments.Length == 0 || segments[0] != root.name) return false;",
        "    var current = root.transform;",
        "    for (var segmentIndex = 1; segmentIndex < segments.Length; segmentIndex++)",
        "    {",
        "        Transform next = null; for (var childIndex = 0; childIndex < current.childCount; childIndex++) { var child = current.GetChild(childIndex); if (child.name == segments[segmentIndex]) { next = child; break; } }",
        "        if (next == null) return false; current = next;",
        "    }",
        "    return true;",
        "}",
        "",
        "void AssertPathAbsent(GameObject root, string path)",
        "{",
        "    if (PathExists(root, path)) throw new InvalidOperationException(\"Path must be absent: \" + path);",
        "}",
        "",
        "string TransformPath(Transform node)",
        "{",
        "    var names = new List<string>(); for (var current = node; current != null; current = current.parent) names.Add(current.name); names.Reverse(); return string.Join(\"/\", names.ToArray());",
        "}",
        "",
        "bool IsAllowedMissingImage(string path, string[] allowedPrefixes)",
        "{",
        "    foreach (var prefix in allowedPrefixes) if (path.StartsWith(prefix, StringComparison.Ordinal)) return true; return false;",
        "}",
        "",
        "bool IsNestedPrefabContent(Transform node, Transform prefabRoot)",
        "{",
        "    for (var current = node; current != null && current != prefabRoot; current = current.parent)",
        "    {",
        "        if (PrefabUtility.IsAnyPrefabInstanceRoot(current.gameObject)) return true;",
        "    }",
        "    return false;",
        "}",
        "",
        "void CollectForbiddenNames(Transform node, string[] patterns, List<string> invalidNames)",
        "{",
        "    foreach (var pattern in patterns) if (System.Text.RegularExpressions.Regex.IsMatch(node.name, pattern)) { invalidNames.Add(TransformPath(node)); break; }",
        "    for (var index = 0; index < node.childCount; index++) CollectForbiddenNames(node.GetChild(index), patterns, invalidNames);",
        "}",
        "",
        "void AssertPrivateTextureAssetNames(string directory, string requiredPrefix)",
        "{",
        "    var guids = AssetDatabase.FindAssets(\"t:Texture2D\", new[] { directory });",
        "    var requiredFileNamePrefix = Path.GetFileName(requiredPrefix);",
        "    foreach (var guid in guids) { var path = AssetDatabase.GUIDToAssetPath(guid); var fileName = Path.GetFileNameWithoutExtension(path); if (!fileName.StartsWith(requiredFileNamePrefix, StringComparison.Ordinal)) throw new InvalidOperationException(\"Private Texture name must start with \" + requiredFileNamePrefix + \": \" + path); }",
        "}",
        "",
        "GameObject CreateWrapper(Transform parent, string name, int siblingIndex)",
        "{",
        "    var wrapper = new GameObject(name, typeof(RectTransform));",
        "    var rect = wrapper.GetComponent<RectTransform>();",
        "    rect.SetParent(parent, false);",
        "    rect.anchorMin = new Vector2(0.5f, 0.5f); rect.anchorMax = new Vector2(0.5f, 0.5f);",
        "    rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = Vector2.zero;",
        "    rect.sizeDelta = Vector2.zero; rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;",
        "    rect.SetSiblingIndex(siblingIndex);",
        "    return wrapper;",
        "}",
        "",
        "void RemoveEmptyContainer(Transform prefabRoot, Transform container)",
        "{",
        "    if (container.parent == null) throw new InvalidOperationException(\"Cannot remove the Prefab root\");",
        "    if (container.childCount != 0)",
        "    {",
        "        var remainingChildren = new List<string>();",
        "        for (var childIndex = 0; childIndex < container.childCount; childIndex++) remainingChildren.Add(TransformPath(container.GetChild(childIndex)));",
        "        throw new InvalidOperationException(\"Container is not empty after planned moves: \" + TransformPath(container) + \". Remaining direct children: \" + string.Join(\", \", remainingChildren.ToArray()));",
        "    }",
        "    foreach (var component in container.GetComponents<Component>()) if (component != null && !(component is Transform)) throw new InvalidOperationException(\"Container has non-Transform components: \" + TransformPath(container));",
        "    AssertNoExternalReferences(prefabRoot, container);",
        "    Object.DestroyImmediate(container.gameObject);",
        "}",
        "",
        "void TightenToChildren(RectTransform rect)",
        "{",
        "    if (rect.childCount == 0) throw new InvalidOperationException(\"Cannot tighten an empty wrapper: \" + rect.name);",
        "    var parent = rect.parent as RectTransform;",
        "    if (parent == null) throw new InvalidOperationException(\"Wrapper has no RectTransform parent: \" + rect.name);",
        "    var bounds = new Bounds(); var initialized = false;",
        "    for (var childIndex = 0; childIndex < rect.childCount; childIndex++)",
        "    {",
        "        var child = rect.GetChild(childIndex) as RectTransform;",
        "        if (child == null) continue;",
        "        var corners = new Vector3[4]; child.GetWorldCorners(corners);",
        "        for (var cornerIndex = 0; cornerIndex < 4; cornerIndex++)",
        "        {",
        "            var point = parent.InverseTransformPoint(corners[cornerIndex]);",
        "            if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; } else { bounds.Encapsulate(point); }",
        "        }",
        "    }",
        "    if (!initialized) throw new InvalidOperationException(\"Wrapper has no RectTransform children: \" + rect.name);",
        "    var children = new List<Transform>(); var siblingIndices = new List<int>();",
        "    for (var childIndex = 0; childIndex < rect.childCount; childIndex++) { children.Add(rect.GetChild(childIndex)); siblingIndices.Add(rect.GetChild(childIndex).GetSiblingIndex()); }",
        "    for (var childIndex = 0; childIndex < children.Count; childIndex++) children[childIndex].SetParent(parent, true);",
        "    rect.anchorMin = new Vector2(0.5f, 0.5f); rect.anchorMax = new Vector2(0.5f, 0.5f);",
        "    rect.pivot = new Vector2(0.5f, 0.5f); rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;",
        "    rect.anchoredPosition = new Vector2(bounds.center.x, bounds.center.y);",
        "    rect.sizeDelta = new Vector2(bounds.size.x, bounds.size.y);",
        "    for (var childIndex = 0; childIndex < children.Count; childIndex++) { children[childIndex].SetParent(rect, true); children[childIndex].SetSiblingIndex(siblingIndices[childIndex]); }",
        "}",
        "",
        "void AssertTightBounds(RectTransform rect, string path)",
        "{",
        "    if (rect == null) throw new InvalidOperationException(\"Tight-bounds target is not a RectTransform: \" + path);",
        "    if (rect.childCount == 0) throw new InvalidOperationException(\"Tight-bounds target has no children: \" + path);",
        "    var parent = rect.parent as RectTransform; var rectCorners = new Vector3[4]; rect.GetWorldCorners(rectCorners);",
        "    var min = Vector3.zero; var max = Vector3.zero; var initialized = false;",
        "    for (var childIndex = 0; childIndex < rect.childCount; childIndex++)",
        "    {",
        "        var child = rect.GetChild(childIndex) as RectTransform; if (child == null) continue;",
        "        var corners = new Vector3[4]; child.GetWorldCorners(corners);",
        "        for (var cornerIndex = 0; cornerIndex < 4; cornerIndex++) { var point = parent.InverseTransformPoint(corners[cornerIndex]); if (!initialized) { min = point; max = point; initialized = true; } else { min = Vector3.Min(min, point); max = Vector3.Max(max, point); } }",
        "    }",
        "    if (!initialized) throw new InvalidOperationException(\"Tight-bounds target has no RectTransform children: \" + path);",
        "    var wrapperMin = parent.InverseTransformPoint(rectCorners[0]); var wrapperMax = parent.InverseTransformPoint(rectCorners[2]);",
        "    if (Vector2.Distance(new Vector2(min.x, min.y), new Vector2(wrapperMin.x, wrapperMin.y)) > 0.01f || Vector2.Distance(new Vector2(max.x, max.y), new Vector2(wrapperMax.x, wrapperMax.y)) > 0.01f) throw new InvalidOperationException(\"Tight-bounds invariant failed: \" + path);",
        "}",
        "",
        "void CaptureWorldCorners(Transform node, Dictionary<Transform, Vector3[]> results, HashSet<Transform> excluded)",
        "{",
        "    var rect = node as RectTransform;",
        "    if (rect != null && !excluded.Contains(node)) { var corners = new Vector3[4]; rect.GetWorldCorners(corners); results[node] = corners; }",
        "    for (var index = 0; index < node.childCount; index++) CaptureWorldCorners(node.GetChild(index), results, excluded);",
        "}",
        "",
        "int CountNodes(Transform node)",
        "{",
        "    var count = 1; for (var index = 0; index < node.childCount; index++) count += CountNodes(node.GetChild(index)); return count;",
        "}",
        "",
        "int CountComponents(Transform node, ref int missing)",
        "{",
        "    var count = 0; foreach (var component in node.GetComponents<Component>()) { if (component == null) missing++; else count++; }",
        "    for (var index = 0; index < node.childCount; index++) count += CountComponents(node.GetChild(index), ref missing); return count;",
        "}",
        "",
        "int CountObjectReferences(Transform node)",
        "{",
        "    var count = 0;",
        "    foreach (var component in node.GetComponents<Component>())",
        "    {",
        "        if (component == null) continue;",
        "        var serialized = new SerializedObject(component); var property = serialized.GetIterator(); var enterChildren = true;",
        "        while (property.NextVisible(enterChildren)) { enterChildren = false; if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null) count++; }",
        "    }",
        "    for (var index = 0; index < node.childCount; index++) count += CountObjectReferences(node.GetChild(index)); return count;",
        "}",
        "",
        "void AssertExpected(string label, int actual, int expected)",
        "{",
        "    if (expected >= 0 && actual != expected) throw new InvalidOperationException(label + \" expected=\" + expected + \" actual=\" + actual);",
        "}",
        "",
        "void AssertDirectChildren(Transform node, string[] expectedChildren, string path)",
        "{",
        "    if (node.childCount != expectedChildren.Length) throw new InvalidOperationException(path + \" direct child count expected=\" + expectedChildren.Length + \" actual=\" + node.childCount);",
        "    for (var index = 0; index < expectedChildren.Length; index++)",
        "    {",
        "        var actual = node.GetChild(index).name;",
        "        if (!string.Equals(actual, expectedChildren[index], StringComparison.Ordinal)) throw new InvalidOperationException(path + \" direct child[\" + index + \"] expected=\" + expectedChildren[index] + \" actual=\" + actual);",
        "    }",
        "}",
        "",
        "void ExcludeHierarchy(Transform node, HashSet<Transform> excluded)",
        "{",
        "    excluded.Add(node);",
        "    for (var index = 0; index < node.childCount; index++) ExcludeHierarchy(node.GetChild(index), excluded);",
        "}",
        "",
        "bool IsInHierarchy(Transform candidate, Transform ancestor)",
        "{",
        "    for (var current = candidate; current != null; current = current.parent) if (current == ancestor) return true;",
        "    return false;",
        "}",
        "",
        "void AppendStructureSignature(Transform node, List<string> parts)",
        "{",
        "    var componentTypes = new List<string>();",
        "    foreach (var component in node.GetComponents<Component>()) if (component != null && !(component is Transform)) componentTypes.Add(component.GetType().FullName);",
        "    componentTypes.Sort(); parts.Add(node.childCount + \":\" + string.Join(\",\", componentTypes.ToArray()));",
        "    for (var index = 0; index < node.childCount; index++) AppendStructureSignature(node.GetChild(index), parts);",
        "}",
        "",
        "string StructureSignature(Transform node)",
        "{",
        "    var parts = new List<string>(); AppendStructureSignature(node, parts); return string.Join(\"|\", parts.ToArray());",
        "}",
        "",
        "void AssertNoNestedPrefabRoots(Transform source)",
        "{",
        "    foreach (var node in source.GetComponentsInChildren<Transform>(true))",
        "    {",
        "        if (node != source && PrefabUtility.IsAnyPrefabInstanceRoot(node.gameObject)) throw new InvalidOperationException(\"Cannot extract a repeated unit containing a nested Prefab: \" + source.name);",
        "    }",
        "}",
        "",
        "void AssertNoExternalReferences(Transform prefabRoot, Transform source)",
        "{",
        "    var internalObjects = new HashSet<Object>();",
        "    foreach (var node in source.GetComponentsInChildren<Transform>(true))",
        "    {",
        "        internalObjects.Add(node.gameObject); foreach (var component in node.GetComponents<Component>()) if (component != null) internalObjects.Add(component);",
        "    }",
        "    foreach (var owner in prefabRoot.GetComponentsInChildren<Transform>(true))",
        "    {",
        "        if (IsInHierarchy(owner, source)) continue;",
        "        foreach (var component in owner.GetComponents<Component>())",
        "        {",
        "            if (component == null) continue;",
        "            var serialized = new SerializedObject(component); var property = serialized.GetIterator(); var enterChildren = true;",
        "            while (property.NextVisible(enterChildren))",
        "            {",
        "                enterChildren = false;",
        "                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null && internalObjects.Contains(property.objectReferenceValue)) throw new InvalidOperationException(\"Cannot extract a unit referenced outside its hierarchy: \" + source.name + \" by \" + owner.name);",
        "            }",
        "        }",
        "    }",
        "}",
        "",
        "void BuildObjectMap(Transform source, Transform destination, Dictionary<Object, Object> objectMap)",
        "{",
        "    if (source.childCount != destination.childCount) throw new InvalidOperationException(\"Repeated unit structure differs while extracting \" + source.name);",
        "    objectMap[source.gameObject] = destination.gameObject; objectMap[source] = destination;",
        "    var sourceComponents = source.GetComponents<Component>(); var destinationComponents = destination.GetComponents<Component>();",
        "    for (var sourceIndex = 0; sourceIndex < sourceComponents.Length; sourceIndex++)",
        "    {",
        "        var sourceComponent = sourceComponents[sourceIndex]; if (sourceComponent == null || sourceComponent is Transform) continue;",
        "        var ordinal = 0; for (var priorIndex = 0; priorIndex < sourceIndex; priorIndex++) if (sourceComponents[priorIndex] != null && sourceComponents[priorIndex].GetType() == sourceComponent.GetType()) ordinal++;",
        "        var destinationComponent = destinationComponents.Where(component => component != null && component.GetType() == sourceComponent.GetType()).Skip(ordinal).FirstOrDefault();",
        "        if (destinationComponent == null) throw new InvalidOperationException(\"Repeated unit component signature differs while extracting \" + source.name);",
        "        objectMap[sourceComponent] = destinationComponent;",
        "    }",
        "    for (var childIndex = 0; childIndex < source.childCount; childIndex++) BuildObjectMap(source.GetChild(childIndex), destination.GetChild(childIndex), objectMap);",
        "}",
        "",
        "void CopyTransformData(Transform source, Transform destination)",
        "{",
        "    destination.localPosition = source.localPosition; destination.localRotation = source.localRotation; destination.localScale = source.localScale;",
        "    var sourceRect = source as RectTransform; var destinationRect = destination as RectTransform;",
        "    if (sourceRect != null && destinationRect != null)",
        "    {",
        "        destinationRect.anchorMin = sourceRect.anchorMin; destinationRect.anchorMax = sourceRect.anchorMax; destinationRect.pivot = sourceRect.pivot;",
        "        destinationRect.anchoredPosition3D = sourceRect.anchoredPosition3D; destinationRect.sizeDelta = sourceRect.sizeDelta;",
        "    }",
        "}",
        "",
        "void CopyStateRootData(Transform source, Transform destination)",
        "{",
        "    var sourceRect = source as RectTransform; var destinationRect = destination as RectTransform;",
        "    if (sourceRect == null || destinationRect == null) throw new InvalidOperationException(\"State roots must use RectTransform\");",
        "    destinationRect.anchorMin = sourceRect.anchorMin; destinationRect.anchorMax = sourceRect.anchorMax; destinationRect.pivot = sourceRect.pivot; destinationRect.anchoredPosition3D = Vector3.zero; destinationRect.sizeDelta = sourceRect.sizeDelta; destinationRect.localScale = sourceRect.localScale; destinationRect.localRotation = sourceRect.localRotation;",
        "}",
        "",
        "void RemapObjectReferences(Component component, Dictionary<Object, Object> objectMap)",
        "{",
        "    var serialized = new SerializedObject(component); var property = serialized.GetIterator(); var enterChildren = true; var changed = false;",
        "    while (property.NextVisible(enterChildren))",
        "    {",
        "        enterChildren = false;",
        "        if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null) continue;",
        "        Object mapped; if (!objectMap.TryGetValue(property.objectReferenceValue, out mapped)) continue;",
        "        property.objectReferenceValue = mapped; changed = true;",
        "    }",
        "    if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();",
        "}",
        "",
        "void CopyHierarchyOverrides(Transform source, Transform destination, Dictionary<Object, Object> objectMap, bool copyNames)",
        "{",
        "    if (copyNames) destination.name = source.name; destination.gameObject.layer = source.gameObject.layer; destination.gameObject.tag = source.gameObject.tag; CopyTransformData(source, destination);",
        "    foreach (var sourceComponent in source.GetComponents<Component>())",
        "    {",
        "        if (sourceComponent == null || sourceComponent is Transform) continue;",
        "        var destinationComponent = (Component)objectMap[sourceComponent]; EditorUtility.CopySerialized(sourceComponent, destinationComponent); RemapObjectReferences(destinationComponent, objectMap);",
        "        PrefabUtility.RecordPrefabInstancePropertyModifications(destinationComponent);",
        "    }",
        "    PrefabUtility.RecordPrefabInstancePropertyModifications(destination); PrefabUtility.RecordPrefabInstancePropertyModifications(destination.gameObject);",
        "    for (var childIndex = 0; childIndex < source.childCount; childIndex++) CopyHierarchyOverrides(source.GetChild(childIndex), destination.GetChild(childIndex), objectMap, copyNames);",
        "    destination.gameObject.SetActive(source.gameObject.activeSelf);",
        "}",
        "",
        "void CaptureHierarchyCorners(Transform node, List<Vector3[]> corners)",
        "{",
        "    var rect = node as RectTransform; if (rect != null) { var values = new Vector3[4]; rect.GetWorldCorners(values); corners.Add(values); }",
        "    for (var index = 0; index < node.childCount; index++) CaptureHierarchyCorners(node.GetChild(index), corners);",
        "}",
        "",
        "void AssertHierarchyCorners(Transform node, List<Vector3[]> beforeCorners, string label)",
        "{",
        "    var afterCorners = new List<Vector3[]>(); CaptureHierarchyCorners(node, afterCorners);",
        "    if (afterCorners.Count != beforeCorners.Count) throw new InvalidOperationException(\"Component extraction changed RectTransform count: \" + label);",
        "    var maxDelta = 0f; for (var nodeIndex = 0; nodeIndex < beforeCorners.Count; nodeIndex++) for (var cornerIndex = 0; cornerIndex < 4; cornerIndex++) maxDelta = Mathf.Max(maxDelta, Vector3.Distance(beforeCorners[nodeIndex][cornerIndex], afterCorners[nodeIndex][cornerIndex]));",
        "    if (maxDelta > 0.01f) throw new InvalidOperationException(\"Component extraction world-corner invariant failed for \" + label + \": \" + maxDelta);",
        "}",
        "",
        "void EnsureAssetFolder(string assetPath)",
        "{",
        "    var directory = assetPath.Substring(0, assetPath.LastIndexOf('/')); var parts = directory.Split('/'); var current = parts[0];",
        "    for (var index = 1; index < parts.Length; index++) { var next = current + \"/\" + parts[index]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]); current = next; }",
        "}",
        "",
        "GameObject CreateComponentPrefab(Transform template, string assetPath)",
        "{",
        "    if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null) throw new InvalidOperationException(\"Component Prefab target already exists: \" + assetPath);",
        "    EnsureAssetFolder(assetPath); var clone = Object.Instantiate(template.gameObject);",
        "    try { clone.name = Path.GetFileNameWithoutExtension(assetPath); clone.transform.SetParent(null, false); var componentAsset = PrefabUtility.SaveAsPrefabAsset(clone, assetPath); if (componentAsset == null) throw new InvalidOperationException(\"Failed to save component Prefab: \" + assetPath); return componentAsset; }",
        "    finally { Object.DestroyImmediate(clone); }",
        "}",
        "",
        "GameObject LoadExistingComponentPrefab(string assetPath)",
        "{",
        "    var componentAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);",
        "    if (componentAsset == null) throw new InvalidOperationException(\"Reusable component Prefab did not load: \" + assetPath);",
        "    return componentAsset;",
        "}",
        "",
        "void ReplaceWithComponentInstance(Transform source, GameObject componentAsset)",
        "{",
        "    var parent = source.parent; if (parent == null) throw new InvalidOperationException(\"Cannot replace the Prefab root with a nested component instance\");",
        "    var siblingIndex = source.GetSiblingIndex(); var beforeCorners = new List<Vector3[]>(); CaptureHierarchyCorners(source, beforeCorners);",
        "    var instance = PrefabUtility.InstantiatePrefab(componentAsset) as GameObject; if (instance == null) throw new InvalidOperationException(\"Failed to instantiate component Prefab: \" + componentAsset.name);",
        "    var destination = instance.transform; destination.SetParent(parent, false); destination.SetSiblingIndex(siblingIndex);",
        "    var objectMap = new Dictionary<Object, Object>(); BuildObjectMap(source, destination, objectMap); CopyHierarchyOverrides(source, destination, objectMap, true);",
        "    Object.DestroyImmediate(source.gameObject); AssertHierarchyCorners(destination, beforeCorners, destination.name);",
        "}",
        "",
        "void AssertExclusiveActiveState(Transform stateContainer, string defaultStateName, string label)",
        "{",
        "    Transform defaultState = null; var activeCount = 0;",
        "    for (var index = 0; index < stateContainer.childCount; index++)",
        "    {",
        "        var state = stateContainer.GetChild(index); if (state.name == defaultStateName) defaultState = state; if (state.gameObject.activeSelf) activeCount++;",
        "    }",
        "    if (defaultState == null) throw new InvalidOperationException(\"State component default state is missing: \" + defaultStateName + \" in \" + label);",
        "    if (activeCount != 1 || !defaultState.gameObject.activeSelf) throw new InvalidOperationException(\"State component must have exactly one active default state: \" + label);",
        "}",
        "",
        "void AssertVariantState(Transform instance, string expectedStateName, string label)",
        "{",
        "    var common = instance.Find(\"[Common]\"); var states = instance.Find(\"[States]\");",
        "    if (common == null || states == null) throw new InvalidOperationException(\"Variant component must contain [Common] and [States]: \" + label);",
        "    AssertExclusiveActiveState(states, expectedStateName, label);",
        "}",
        "",
        "GameObject CreateStateComponentPrefab(Transform template, Transform[] sources, string[] stateNames, int defaultStateIndex, string assetPath)",
        "{",
        "    if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null) throw new InvalidOperationException(\"State component Prefab target already exists: \" + assetPath);",
        "    if (sources == null || stateNames == null || sources.Length != stateNames.Length || defaultStateIndex < 0 || defaultStateIndex >= sources.Length) throw new InvalidOperationException(\"Invalid state component extraction contract\");",
        "    var parent = template.parent; if (parent == null) throw new InvalidOperationException(\"Cannot extract states from the Prefab root\");",
        "    for (var index = 0; index < sources.Length; index++) if (sources[index].parent != parent) throw new InvalidOperationException(\"State sources must be direct siblings: \" + sources[index].name);",
        "    EnsureAssetFolder(assetPath);",
        "    var builder = new GameObject(Path.GetFileNameWithoutExtension(assetPath), typeof(RectTransform));",
        "    builder.transform.SetParent(parent, false); builder.transform.SetSiblingIndex(template.GetSiblingIndex()); builder.layer = template.gameObject.layer; builder.tag = template.gameObject.tag; CopyTransformData(template, builder.transform);",
        "    var statesContainer = new GameObject(\"[States]\", typeof(RectTransform)); var statesRect = statesContainer.GetComponent<RectTransform>();",
        "    statesContainer.transform.SetParent(builder.transform, false); statesRect.anchorMin = new Vector2(0.5f, 0.5f); statesRect.anchorMax = new Vector2(0.5f, 0.5f); statesRect.pivot = new Vector2(0.5f, 0.5f); statesRect.anchoredPosition3D = Vector3.zero; statesRect.sizeDelta = Vector2.zero; statesRect.localScale = Vector3.one; statesRect.localRotation = Quaternion.identity;",
        "    try",
        "    {",
        "        for (var index = 0; index < sources.Length; index++)",
        "        {",
        "            var clone = Object.Instantiate(sources[index].gameObject, parent); clone.name = stateNames[index]; clone.transform.SetParent(statesContainer.transform, true); clone.SetActive(index == defaultStateIndex);",
        "        }",
        "        AssertDirectChildren(statesContainer.transform, stateNames, builder.name + \"/[States]\"); AssertExclusiveActiveState(statesContainer.transform, stateNames[defaultStateIndex], builder.name);",
        "        var componentAsset = PrefabUtility.SaveAsPrefabAsset(builder, assetPath); if (componentAsset == null) throw new InvalidOperationException(\"Failed to save state component Prefab: \" + assetPath); return componentAsset;",
        "    }",
        "    finally { Object.DestroyImmediate(builder); }",
        "}",
        "",
        "GameObject ReplaceStateSourcesWithComponent(Transform template, Transform[] sources, string[] stateNames, int defaultStateIndex, GameObject componentAsset)",
        "{",
        "    var parent = template.parent; if (parent == null) throw new InvalidOperationException(\"Cannot replace state sources at the Prefab root\"); var siblingIndex = template.GetSiblingIndex();",
        "    var sourceCorners = new List<List<Vector3[]>>(); for (var index = 0; index < sources.Length; index++) { var corners = new List<Vector3[]>(); CaptureHierarchyCorners(sources[index], corners); sourceCorners.Add(corners); }",
        "    var instance = PrefabUtility.InstantiatePrefab(componentAsset) as GameObject; if (instance == null) throw new InvalidOperationException(\"Failed to instantiate state component Prefab: \" + componentAsset.name);",
        "    var destination = instance.transform; destination.SetParent(parent, false); destination.SetSiblingIndex(siblingIndex); destination.gameObject.layer = template.gameObject.layer; destination.gameObject.tag = template.gameObject.tag; CopyTransformData(template, destination); destination.gameObject.SetActive(true);",
        "    var statesContainer = destination.Find(\"[States]\"); if (statesContainer == null) throw new InvalidOperationException(\"State component has no [States] container: \" + destination.name); AssertDirectChildren(statesContainer, stateNames, destination.name + \"/[States]\"); AssertExclusiveActiveState(statesContainer, stateNames[defaultStateIndex], destination.name);",
        "    for (var index = 0; index < sources.Length; index++) Object.DestroyImmediate(sources[index].gameObject);",
        "    for (var index = 0; index < sourceCorners.Count; index++) AssertHierarchyCorners(statesContainer.GetChild(index), sourceCorners[index], stateNames[index]);",
        "    return instance;",
        "}",
        "",
        "GameObject CreateVariantComponentPrefab(Transform template, Transform[] sources, string[] stateNames, int defaultStateIndex, string assetPath)",
        "{",
        "    if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null) throw new InvalidOperationException(\"Variant component Prefab target already exists: \" + assetPath);",
        "    if (sources == null || stateNames == null || sources.Length != stateNames.Length || defaultStateIndex < 0 || defaultStateIndex >= sources.Length) throw new InvalidOperationException(\"Invalid variant component extraction contract\");",
        "    var parent = template.parent; if (parent == null) throw new InvalidOperationException(\"Cannot extract variants from the Prefab root\");",
        "    for (var index = 0; index < sources.Length; index++) if (sources[index].parent != parent) throw new InvalidOperationException(\"Variant sources must be direct siblings: \" + sources[index].name);",
        "    EnsureAssetFolder(assetPath);",
        "    var builder = new GameObject(Path.GetFileNameWithoutExtension(assetPath), typeof(RectTransform));",
        "    builder.transform.SetParent(parent, false); builder.transform.SetSiblingIndex(template.GetSiblingIndex()); builder.layer = template.gameObject.layer; builder.tag = template.gameObject.tag; CopyTransformData(template, builder.transform);",
        "    var common = new GameObject(\"[Common]\", typeof(RectTransform)); common.transform.SetParent(builder.transform, false); var commonRect = common.GetComponent<RectTransform>(); commonRect.anchorMin = new Vector2(0.5f, 0.5f); commonRect.anchorMax = new Vector2(0.5f, 0.5f); commonRect.pivot = new Vector2(0.5f, 0.5f); commonRect.anchoredPosition3D = Vector3.zero; commonRect.sizeDelta = Vector2.zero; commonRect.localScale = Vector3.one; commonRect.localRotation = Quaternion.identity;",
        "    var statesContainer = new GameObject(\"[States]\", typeof(RectTransform)); statesContainer.transform.SetParent(builder.transform, false); var statesRect = statesContainer.GetComponent<RectTransform>(); statesRect.anchorMin = new Vector2(0.5f, 0.5f); statesRect.anchorMax = new Vector2(0.5f, 0.5f); statesRect.pivot = new Vector2(0.5f, 0.5f); statesRect.anchoredPosition3D = Vector3.zero; statesRect.sizeDelta = Vector2.zero; statesRect.localScale = Vector3.one; statesRect.localRotation = Quaternion.identity;",
        "    try",
        "    {",
        "        for (var index = 0; index < sources.Length; index++) { var clone = Object.Instantiate(sources[index].gameObject, parent); clone.name = stateNames[index]; clone.transform.SetParent(statesContainer.transform, false); CopyStateRootData(template, clone.transform); clone.SetActive(index == defaultStateIndex); }",
        "        AssertDirectChildren(builder.transform, new[] { \"[Common]\", \"[States]\" }, builder.name); AssertDirectChildren(statesContainer.transform, stateNames, builder.name + \"/[States]\"); AssertExclusiveActiveState(statesContainer.transform, stateNames[defaultStateIndex], builder.name);",
        "        var componentAsset = PrefabUtility.SaveAsPrefabAsset(builder, assetPath); if (componentAsset == null) throw new InvalidOperationException(\"Failed to save variant component Prefab: \" + assetPath); return componentAsset;",
        "    }",
        "    finally { Object.DestroyImmediate(builder); }",
        "}",
        "",
        "GameObject ReplaceVariantSourceWithComponent(Transform source, string instanceName, string activeStateName, GameObject componentAsset)",
        "{",
        "    var parent = source.parent; if (parent == null) throw new InvalidOperationException(\"Cannot replace the Prefab root with a variant component instance\"); var siblingIndex = source.GetSiblingIndex();",
        "    var beforeCorners = new List<Vector3[]>(); CaptureHierarchyCorners(source, beforeCorners);",
        "    var instance = PrefabUtility.InstantiatePrefab(componentAsset) as GameObject; if (instance == null) throw new InvalidOperationException(\"Failed to instantiate variant component Prefab: \" + componentAsset.name);",
        "    var destination = instance.transform; destination.SetParent(parent, false); destination.SetSiblingIndex(siblingIndex); destination.name = instanceName; destination.gameObject.layer = source.gameObject.layer; destination.gameObject.tag = source.gameObject.tag; CopyTransformData(source, destination); destination.gameObject.SetActive(source.gameObject.activeSelf);",
        "    var states = destination.Find(\"[States]\"); if (states == null) throw new InvalidOperationException(\"Variant component has no [States] container: \" + destination.name); for (var index = 0; index < states.childCount; index++) states.GetChild(index).gameObject.SetActive(states.GetChild(index).name == activeStateName); AssertVariantState(destination, activeStateName, destination.name);",
        "    Object.DestroyImmediate(source.gameObject); AssertHierarchyCorners(states.GetChild(FindStateIndex(states, activeStateName)), beforeCorners, destination.name); return instance;",
        "}",
        "",
        "int FindStateIndex(Transform states, string name)",
        "{",
        "    for (var index = 0; index < states.childCount; index++) if (states.GetChild(index).name == name) return index; throw new InvalidOperationException(\"Variant state not found: \" + name);",
        "}",
        "",
        "Transform FindDirectChild(Transform parent, string name)",
        "{",
        "    for (var index = 0; index < parent.childCount; index++) if (parent.GetChild(index).name == name) return parent.GetChild(index);",
        "    throw new InvalidOperationException(\"Direct child was not found: \" + TransformPath(parent) + \"/\" + name);",
        "}",
        "",
        "void AssertDirectSourceMembers(Transform source, string[] commonSourceNames, string[] stateSourceNames)",
        "{",
        "    var expected = new HashSet<string>(commonSourceNames); foreach (var name in stateSourceNames) if (!expected.Add(name)) throw new InvalidOperationException(\"Common and state members overlap: \" + name);",
        "    if (source.childCount != expected.Count) throw new InvalidOperationException(\"Stateful source has an unmapped member: \" + TransformPath(source));",
        "    for (var index = 0; index < source.childCount; index++) if (!expected.Contains(source.GetChild(index).name)) throw new InvalidOperationException(\"Stateful source has an unmapped member: \" + TransformPath(source.GetChild(index)));",
        "}",
        "",
        "RectTransform CreateStructuralContainer(string name, Transform parent, RectTransform template)",
        "{",
        "    var container = new GameObject(name, typeof(RectTransform)); var rect = container.GetComponent<RectTransform>(); rect.SetParent(parent, false);",
        "    rect.anchorMin = template.anchorMin; rect.anchorMax = template.anchorMax; rect.pivot = template.pivot; rect.anchoredPosition3D = Vector3.zero; rect.sizeDelta = template.sizeDelta; rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity; return rect;",
        "}",
        "",
        "void CloneMappedMember(Transform sourceParent, string sourceName, string targetName, Transform destinationParent)",
        "{",
        "    var source = FindDirectChild(sourceParent, sourceName); var clone = Object.Instantiate(source.gameObject); clone.name = targetName; clone.transform.SetParent(destinationParent, false); CopyTransformData(source, clone.transform); clone.SetActive(source.gameObject.activeSelf);",
        "}",
        "",
        "void CopyMappedMemberOverride(Transform sourceParent, string sourceName, Transform destinationParent, string targetName, List<List<Vector3[]>> beforeCorners, List<Transform> destinations)",
        "{",
        "    var source = FindDirectChild(sourceParent, sourceName); var destination = FindDirectChild(destinationParent, targetName); var corners = new List<Vector3[]>(); CaptureHierarchyCorners(source, corners); beforeCorners.Add(corners); destinations.Add(destination);",
        "    var objectMap = new Dictionary<Object, Object>(); BuildObjectMap(source, destination, objectMap); CopyHierarchyOverrides(source, destination, objectMap, false);",
        "}",
        "",
        "GameObject CreateStatefulComponentPrefab(Transform template, Transform commonSource, string[] commonSourceNames, string[] commonTargetNames, Transform[] stateSources, string[] stateNames, string[][] stateSourceNames, string[][] stateTargetNames, int defaultStateIndex, string assetPath)",
        "{",
        "    if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null) throw new InvalidOperationException(\"Stateful component Prefab target already exists: \" + assetPath);",
        "    if (commonSourceNames.Length != commonTargetNames.Length || stateSources.Length != stateNames.Length || stateSources.Length != stateSourceNames.Length || stateSources.Length != stateTargetNames.Length || defaultStateIndex < 0 || defaultStateIndex >= stateSources.Length) throw new InvalidOperationException(\"Invalid stateful component extraction contract\");",
        "    var parent = template.parent; if (parent == null) throw new InvalidOperationException(\"Cannot extract a stateful component from the Prefab root\"); EnsureAssetFolder(assetPath);",
        "    var builder = new GameObject(Path.GetFileNameWithoutExtension(assetPath), typeof(RectTransform)); builder.transform.SetParent(parent, false); builder.transform.SetSiblingIndex(template.GetSiblingIndex()); builder.layer = template.gameObject.layer; builder.tag = template.gameObject.tag; CopyTransformData(template, builder.transform);",
        "    var templateRect = template as RectTransform; if (templateRect == null) throw new InvalidOperationException(\"Stateful component sources must use RectTransform\");",
        "    var states = CreateStructuralContainer(\"[States]\", builder.transform, templateRect);",
        "    var common = CreateStructuralContainer(\"[Common]\", builder.transform, templateRect);",
        "    try",
        "    {",
        "        for (var index = 0; index < commonSourceNames.Length; index++) CloneMappedMember(commonSource, commonSourceNames[index], commonTargetNames[index], common.transform);",
        "        for (var stateIndex = 0; stateIndex < stateSources.Length; stateIndex++)",
        "        {",
        "            if (stateSourceNames[stateIndex].Length != stateTargetNames[stateIndex].Length) throw new InvalidOperationException(\"State member mapping length differs\");",
        "            var state = CreateStructuralContainer(stateNames[stateIndex], states.transform, templateRect); state.gameObject.SetActive(stateIndex == defaultStateIndex);",
        "            for (var memberIndex = 0; memberIndex < stateSourceNames[stateIndex].Length; memberIndex++) CloneMappedMember(stateSources[stateIndex], stateSourceNames[stateIndex][memberIndex], stateTargetNames[stateIndex][memberIndex], state.transform);",
        "        }",
        "        AssertDirectChildren(states.transform, stateNames, builder.name + \"/[States]\"); AssertDirectChildren(common.transform, commonTargetNames, builder.name + \"/[Common]\"); AssertExclusiveActiveState(states.transform, stateNames[defaultStateIndex], builder.name);",
        "        var componentAsset = PrefabUtility.SaveAsPrefabAsset(builder, assetPath); if (componentAsset == null) throw new InvalidOperationException(\"Failed to save stateful component Prefab: \" + assetPath); return componentAsset;",
        "    }",
        "    finally { Object.DestroyImmediate(builder); }",
        "}",
        "",
        "GameObject ReplaceStatefulSourceWithComponent(Transform source, string instanceName, string activeStateName, string[] commonSourceNames, string[] commonTargetNames, string[] stateSourceNames, string[] stateTargetNames, GameObject componentAsset)",
        "{",
        "    AssertDirectSourceMembers(source, commonSourceNames, stateSourceNames); var parent = source.parent; if (parent == null) throw new InvalidOperationException(\"Cannot replace the Prefab root with a stateful component instance\"); var siblingIndex = source.GetSiblingIndex();",
        "    var instance = PrefabUtility.InstantiatePrefab(componentAsset) as GameObject; if (instance == null) throw new InvalidOperationException(\"Failed to instantiate stateful component Prefab: \" + componentAsset.name);",
        "    var destination = instance.transform; destination.SetParent(parent, false); destination.SetSiblingIndex(siblingIndex); destination.name = instanceName; destination.gameObject.layer = source.gameObject.layer; destination.gameObject.tag = source.gameObject.tag; CopyTransformData(source, destination); destination.gameObject.SetActive(source.gameObject.activeSelf);",
        "    var states = destination.Find(\"[States]\"); var common = destination.Find(\"[Common]\"); if (states == null || common == null) throw new InvalidOperationException(\"Stateful component has no [States] or [Common] container: \" + destination.name);",
        "    for (var index = 0; index < states.childCount; index++) states.GetChild(index).gameObject.SetActive(states.GetChild(index).name == activeStateName); var activeState = states.Find(activeStateName); if (activeState == null) throw new InvalidOperationException(\"Stateful component state was not found: \" + activeStateName);",
        "    if (commonSourceNames.Length != commonTargetNames.Length || stateSourceNames.Length != stateTargetNames.Length) throw new InvalidOperationException(\"Stateful instance member mapping length differs\");",
        "    var beforeCorners = new List<List<Vector3[]>>(); var destinations = new List<Transform>();",
        "    for (var index = 0; index < commonSourceNames.Length; index++) CopyMappedMemberOverride(source, commonSourceNames[index], common, commonTargetNames[index], beforeCorners, destinations);",
        "    for (var index = 0; index < stateSourceNames.Length; index++) CopyMappedMemberOverride(source, stateSourceNames[index], activeState, stateTargetNames[index], beforeCorners, destinations);",
        "    Object.DestroyImmediate(source.gameObject); for (var index = 0; index < destinations.Count; index++) AssertHierarchyCorners(destinations[index], beforeCorners[index], destination.name); AssertVariantState(destination, activeStateName, destination.name); return instance;",
        "}",
        "",
        "void AssertNestedPrefabInstance(GameObject instance, string assetPath)",
        "{",
        "    if (!PrefabUtility.IsAnyPrefabInstanceRoot(instance)) throw new InvalidOperationException(\"Expected nested Prefab instance: \" + instance.name);",
        "    var actualAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);",
        "    if (!string.Equals(actualAssetPath, assetPath, StringComparison.Ordinal)) throw new InvalidOperationException(\"Nested Prefab source mismatch for \" + instance.name + \": \" + actualAssetPath);",
        "}",
        "",
        "void AssertGuid(string assetPath, string expectedGuid)",
        "{",
        "    var actualGuid = AssetDatabase.AssetPathToGUID(assetPath);",
        "    if (!string.Equals(actualGuid, expectedGuid, StringComparison.Ordinal)) throw new InvalidOperationException(\"GUID invariant failed for \" + assetPath);",
        "}",
        "",
        "Dictionary<Object, Object> RefreshRenamedAsset(string sourcePath, string targetPath, string expectedTargetGuid)",
        "{",
        "    AssertGuid(targetPath, expectedTargetGuid);",
        "    var sourceObjects = AssetDatabase.LoadAllAssetsAtPath(sourcePath).Where(value => value != null).ToArray();",
        "    if (sourceObjects.Length == 0) throw new InvalidOperationException(\"Replay source asset did not load: \" + sourcePath);",
        "    var sourceImporter = AssetImporter.GetAtPath(sourcePath); var targetImporter = AssetImporter.GetAtPath(targetPath);",
        "    if (sourceImporter == null || targetImporter == null || sourceImporter.GetType() != targetImporter.GetType()) throw new InvalidOperationException(\"Replay asset importer types do not match: \" + sourcePath + \" => \" + targetPath);",
        "    var projectRoot = Directory.GetParent(Application.dataPath);",
        "    if (projectRoot == null) throw new InvalidOperationException(\"Unity project root could not be resolved\");",
        "    var sourceFullPath = Path.GetFullPath(Path.Combine(projectRoot.FullName, sourcePath.Replace('/', Path.DirectorySeparatorChar)));",
        "    var targetFullPath = Path.GetFullPath(Path.Combine(projectRoot.FullName, targetPath.Replace('/', Path.DirectorySeparatorChar)));",
        "    var projectPrefix = projectRoot.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;",
        "    if (!sourceFullPath.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase) || !targetFullPath.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException(\"Replay asset path escaped the Unity project\");",
        "    File.Copy(sourceFullPath, targetFullPath, true);",
        "    EditorUtility.CopySerialized(sourceImporter, targetImporter);",
        "    targetImporter.SaveAndReimport();",
        "    AssertGuid(targetPath, expectedTargetGuid);",
        "    var targetObjects = AssetDatabase.LoadAllAssetsAtPath(targetPath).Where(value => value != null).ToArray();",
        "    var mapping = new Dictionary<Object, Object>();",
        "    foreach (var sourceObject in sourceObjects)",
        "    {",
        "        var exactMatches = targetObjects.Where(value => value.GetType() == sourceObject.GetType() && string.Equals(value.name, sourceObject.name, StringComparison.Ordinal)).ToArray();",
        "        var typeMatches = targetObjects.Where(value => value.GetType() == sourceObject.GetType()).ToArray();",
        "        var targetObject = exactMatches.Length == 1 ? exactMatches[0] : typeMatches.Length == 1 ? typeMatches[0] : null;",
        "        if (targetObject == null) throw new InvalidOperationException(\"Could not map replay asset object while preserving GUID: \" + sourcePath + \" :: \" + sourceObject.GetType().FullName + \"/\" + sourceObject.name);",
        "        mapping[sourceObject] = targetObject;",
        "    }",
        "    return mapping;",
        "}",
        "",
        "void RemapAssetReferences(GameObject root, Dictionary<Object, Object> mapping)",
        "{",
        "    foreach (var component in root.GetComponentsInChildren<Component>(true))",
        "    {",
        "        if (component == null) continue;",
        "        var serialized = new SerializedObject(component); var property = serialized.GetIterator(); var enterChildren = true; var changed = false;",
        "        while (property.Next(enterChildren))",
        "        {",
        "            enterChildren = false;",
        "            if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null) continue;",
        "            if (!mapping.TryGetValue(property.objectReferenceValue, out var replacement)) continue;",
        "            property.objectReferenceValue = replacement; changed = true;",
        "        }",
        "        if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();",
        "    }",
        "}",
        "",
        "void CollectInvalidNames(Transform node, List<string> invalidNames)",
        "{",
        "    if (IsNonSemanticObjectName(node.name)) invalidNames.Add(TransformPath(node));",
        "    for (var index = 0; index < node.childCount; index++) CollectInvalidNames(node.GetChild(index), invalidNames);",
        "}",
        "",
        "bool IsNonSemanticObjectName(string name)",
        "{",
        "    if (!System.Text.RegularExpressions.Regex.IsMatch(name, @\"^[A-Za-z0-9_\\[\\]]+$\")) return true;",
        "    return System.Text.RegularExpressions.Regex.IsMatch(name, @\"^(?:\\d+(?:_\\d+)?|\\d+(?:\\.\\d+)?[kKmM]|\\d+[A-Za-z]\\d+[A-Za-z]|[+_-]+|img_v\\d.*|(?:ui|daily)_[A-Za-z0-9_]+)$\");",
        "}",
        "",
    ]

    if mode == "verify":
        lines.extend(emit_verification(plan, mode))
        return "\n".join(lines) + "\n"

    if mode == "preflight":
        lines.extend(emit_preflight(plan))
        return "\n".join(lines) + "\n"

    lines.extend(
        [
            "if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) throw new InvalidOperationException(\"Prefab did not load: \" + prefabPath);",
            "if (!string.Equals(outputPath, prefabPath, StringComparison.Ordinal)) throw new InvalidOperationException(\"Cleanup must save only the exact target Prefab in place.\");",
        ]
    )

    all_assets = plan["texture_renames"] + plan["atlas_renames"]
    for index, rename in enumerate(all_assets):
        source = rename["from"]
        target = final_asset_path(source, rename["toName"])
        lines.append(
            f"if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>({csharp(source)}) == null) throw new InvalidOperationException(\"Source asset did not load: \" + {csharp(source)});"
        )
        if mode == "reapply":
            lines.extend(
                [
                    f"var replayAssetAlreadyRenamed{index} = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>({csharp(target)}) != null;",
                    f"if (replayAssetAlreadyRenamed{index}) AssertGuid({csharp(target)}, {csharp(rename['expectedGuid'])}); else AssertGuid({csharp(source)}, {csharp(rename['expectedGuid'])});",
                ]
            )
        else:
            lines.extend(
                [
                    f"if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>({csharp(target)}) != null) throw new InvalidOperationException(\"Rename target already exists: \" + {csharp(target)});",
                    f"AssertGuid({csharp(source)}, {csharp(rename['expectedGuid'])});",
                ]
            )
    for label, extractions in (
        ("Component", plan["component_extractions"]),
        ("State component", plan["state_component_extractions"]),
        ("Variant component", plan["variant_component_extractions"]),
        ("Stateful component", plan["stateful_component_extractions"]),
    ):
        for extraction in extractions:
            asset = csharp(extraction["assetPath"])
            if mode != "reapply":
                lines.append(f"if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>({asset}) != null) throw new InvalidOperationException(\"{label} Prefab target already exists: \" + {asset});")

    lines.extend(["var root = PrefabUtility.LoadPrefabContents(prefabPath);", "try", "{"])

    if mode == "reapply":
        def assert_replay_path(operation: str, path: str) -> None:
            lines.append(
                f"    AssertPlanPath(root, {csharp(operation)}, {csharp(path)});"
            )

        for index, wrapper in enumerate(plan["wrappers"]):
            if not wrapper["parent"].startswith("@"):
                assert_replay_path(f"wrappers[{index}].parent", wrapper["parent"])
        for index, move in enumerate(plan["moves"]):
            assert_replay_path(f"moves[{index}].source", move["source"])
            if not move["destination"].startswith("@"):
                assert_replay_path(f"moves[{index}].destination", move["destination"])
        for index, rename in enumerate(plan["renames"]):
            if not rename["target"].startswith("@"):
                assert_replay_path(f"renames[{index}].target", rename["target"])
        for index, removal in enumerate(plan["empty_container_removals"]):
            assert_replay_path(
                f"emptyContainerRemovals[{index}].source", removal["source"]
            )
        for index, tight_bound in enumerate(plan["tight_bounds"]):
            if not tight_bound["target"].startswith("@"):
                assert_replay_path(f"tightBounds[{index}].target", tight_bound["target"])
        for index, decision in enumerate(plan["component_family_decisions"]):
            assert_replay_path(
                f"componentFamilyDecisions[{index}].parent", decision["parent"]
            )
            for source_index, source in enumerate(decision["sources"]):
                assert_replay_path(
                    f"componentFamilyDecisions[{index}].sources[{source_index}]",
                    source,
                )
        for index, extraction in enumerate(plan["component_extractions"]):
            assert_replay_path(
                f"componentExtractions[{index}].template", extraction["template"]
            )
            for instance_index, instance in enumerate(extraction["instances"]):
                assert_replay_path(
                    f"componentExtractions[{index}].instances[{instance_index}]",
                    instance,
                )
        for index, extraction in enumerate(plan["state_component_extractions"]):
            assert_replay_path(
                f"stateComponentExtractions[{index}].template", extraction["template"]
            )
            for state_index, state in enumerate(extraction["states"]):
                assert_replay_path(
                    f"stateComponentExtractions[{index}].states[{state_index}].source",
                    state["source"],
                )
        for index, extraction in enumerate(plan["variant_component_extractions"]):
            assert_replay_path(
                f"variantComponentExtractions[{index}].template", extraction["template"]
            )
            for state_index, state in enumerate(extraction["states"]):
                assert_replay_path(
                    f"variantComponentExtractions[{index}].states[{state_index}].source",
                    state["source"],
                )
            for instance_index, instance in enumerate(extraction["instances"]):
                assert_replay_path(
                    f"variantComponentExtractions[{index}].instances[{instance_index}].source",
                    instance["source"],
                )
        for index, extraction in enumerate(plan["stateful_component_extractions"]):
            assert_replay_path(
                f"statefulComponentExtractions[{index}].template", extraction["template"]
            )
            assert_replay_path(
                f"statefulComponentExtractions[{index}].common.source",
                extraction["common"]["source"],
            )
            for state_index, state in enumerate(extraction["states"]):
                assert_replay_path(
                    f"statefulComponentExtractions[{index}].states[{state_index}].source",
                    state["source"],
                )
            for instance_index, instance in enumerate(extraction["instances"]):
                assert_replay_path(
                    f"statefulComponentExtractions[{index}].instances[{instance_index}].source",
                    instance["source"],
                )

    lines.extend(
        [
            "    var excludedCornerNodes = new HashSet<Transform>();",
            "    var beforeCorners = new Dictionary<Transform, Vector3[]>();",
        ]
    )
    for index, move in enumerate(plan["moves"]):
        lines.append(f"    var moveSource{index} = FindByPath(root, {csharp(move['source'])}).transform;")
        if not move["destination"].startswith("@"):
            lines.append(f"    var moveDestination{index} = FindByPath(root, {csharp(move['destination'])}).transform;")
    for index, rename in enumerate(plan["renames"]):
        if not rename["target"].startswith("@"):
            lines.append(f"    var renameTarget{index} = FindByPath(root, {csharp(rename['target'])});")
    removal_vars: list[str] = []
    for index, removal in enumerate(plan["empty_container_removals"]):
        variable = f"emptyContainerRemoval{index}"
        removal_vars.append(variable)
        lines.append(f"    var {variable} = FindByPath(root, {csharp(removal['source'])}).transform;")
    extraction_vars: list[tuple[str, list[str]]] = []
    for extraction_index, extraction in enumerate(plan["component_extractions"]):
        template_var = f"componentTemplate{extraction_index}"
        lines.append(f"    var {template_var} = FindByPath(root, {csharp(extraction['template'])}).transform;")
        instance_vars: list[str] = []
        for instance_index, instance_path in enumerate(extraction["instances"]):
            instance_var = f"componentInstance{extraction_index}_{instance_index}"
            lines.append(f"    var {instance_var} = FindByPath(root, {csharp(instance_path)}).transform;")
            instance_vars.append(instance_var)
        extraction_vars.append((template_var, instance_vars))
    state_extraction_vars: list[tuple[str, list[str]]] = []
    for extraction_index, extraction in enumerate(plan["state_component_extractions"]):
        template_var = f"stateTemplate{extraction_index}"
        lines.append(f"    var {template_var} = FindByPath(root, {csharp(extraction['template'])}).transform;")
        source_vars: list[str] = []
        for state_index, state in enumerate(extraction["states"]):
            source_var = f"stateSource{extraction_index}_{state_index}"
            lines.append(f"    var {source_var} = FindByPath(root, {csharp(state['source'])}).transform;")
            source_vars.append(source_var)
        state_extraction_vars.append((template_var, source_vars))
    variant_extraction_vars: list[tuple[str, list[str], list[str]]] = []
    for extraction_index, extraction in enumerate(plan["variant_component_extractions"]):
        template_var = f"variantTemplate{extraction_index}"
        lines.append(f"    var {template_var} = FindByPath(root, {csharp(extraction['template'])}).transform;")
        source_vars: list[str] = []
        for state_index, state in enumerate(extraction["states"]):
            source_var = f"variantSource{extraction_index}_{state_index}"
            lines.append(f"    var {source_var} = FindByPath(root, {csharp(state['source'])}).transform;")
            source_vars.append(source_var)
        instance_vars: list[str] = []
        for instance_index, instance in enumerate(extraction["instances"]):
            instance_var = f"variantInstance{extraction_index}_{instance_index}"
            lines.append(f"    var {instance_var} = FindByPath(root, {csharp(instance['source'])}).transform;")
            instance_vars.append(instance_var)
        variant_extraction_vars.append((template_var, source_vars, instance_vars))
    stateful_extraction_vars: list[tuple[str, str, list[str], list[str]]] = []
    for extraction_index, extraction in enumerate(plan["stateful_component_extractions"]):
        template_var = f"statefulTemplate{extraction_index}"
        common_source_var = f"statefulCommonSource{extraction_index}"
        lines.append(f"    var {template_var} = FindByPath(root, {csharp(extraction['template'])}).transform;")
        lines.append(f"    var {common_source_var} = FindByPath(root, {csharp(extraction['common']['source'])}).transform;")
        state_source_vars: list[str] = []
        for state_index, state in enumerate(extraction["states"]):
            state_source_var = f"statefulStateSource{extraction_index}_{state_index}"
            lines.append(f"    var {state_source_var} = FindByPath(root, {csharp(state['source'])}).transform;")
            state_source_vars.append(state_source_var)
        instance_vars: list[str] = []
        for instance_index, instance in enumerate(extraction["instances"]):
            instance_var = f"statefulInstance{extraction_index}_{instance_index}"
            lines.append(f"    var {instance_var} = FindByPath(root, {csharp(instance['source'])}).transform;")
            instance_vars.append(instance_var)
        stateful_extraction_vars.append((template_var, common_source_var, state_source_vars, instance_vars))
    wrapper_vars: dict[str, str] = {}
    for index, wrapper in enumerate(plan["wrappers"]):
        variable = f"wrapper{index}"
        wrapper_vars[wrapper["id"]] = variable
        parent = wrapper["parent"]
        parent_expr = f"{wrapper_vars[parent[1:]]}.transform" if parent.startswith("@") else f"FindByPath(root, {csharp(parent)}).transform"
        lines.append(f"    var {variable} = CreateWrapper({parent_expr}, {csharp(wrapper['name'])}, {wrapper['siblingIndex']});")
        lines.append(f"    excludedCornerNodes.Add({variable}.transform);")

    tight_vars: list[str] = []
    for index, tight_bound in enumerate(plan["tight_bounds"]):
        target = tight_bound["target"]
        variable = f"tightTarget{index}"
        tight_vars.append(variable)
        target_expr = f"{wrapper_vars[target[1:]]}.GetComponent<RectTransform>()" if target.startswith("@") else f"FindByPath(root, {csharp(target)}).GetComponent<RectTransform>()"
        lines.append(f"    var {variable} = {target_expr};")
    for variable in tight_vars:
        lines.append(f"    excludedCornerNodes.Add({variable}.transform);")
    for variable in removal_vars:
        lines.append(f"    excludedCornerNodes.Add({variable});")
    for _, instance_vars in extraction_vars:
        for instance_var in instance_vars:
            lines.append(f"    ExcludeHierarchy({instance_var}, excludedCornerNodes);")
    for _, source_vars in state_extraction_vars:
        for source_var in source_vars:
            lines.append(f"    ExcludeHierarchy({source_var}, excludedCornerNodes);")
    for _, _, instance_vars in variant_extraction_vars:
        for instance_var in instance_vars:
            lines.append(f"    ExcludeHierarchy({instance_var}, excludedCornerNodes);")
    for _, _, _, instance_vars in stateful_extraction_vars:
        for instance_var in instance_vars:
            lines.append(f"    ExcludeHierarchy({instance_var}, excludedCornerNodes);")
    lines.append("    CaptureWorldCorners(root.transform, beforeCorners, excludedCornerNodes);")

    for index, move in enumerate(plan["moves"]):
        destination = move["destination"]
        destination_expr = f"{wrapper_vars[destination[1:]]}.transform" if destination.startswith("@") else f"moveDestination{index}"
        lines.extend(
            [
                f"    moveSource{index}.SetParent({destination_expr}, true);",
                f"    moveSource{index}.SetSiblingIndex({move['siblingIndex']});",
            ]
        )
    for index, rename in enumerate(plan["renames"]):
        target = rename["target"]
        target_expr = wrapper_vars[target[1:]] if target.startswith("@") else f"renameTarget{index}"
        lines.append(f"    {target_expr}.name = {csharp(rename['name'])};")
    for variable in tight_vars:
        lines.append(f"    TightenToChildren({variable});")
    for variable in removal_vars:
        lines.append(f"    RemoveEmptyContainer(root.transform, {variable});")
    for extraction_index, extraction in enumerate(plan["component_extractions"]):
        template_var, instance_vars = extraction_vars[extraction_index]
        signature_var = f"componentSignature{extraction_index}"
        lines.append(f"    var {signature_var} = StructureSignature({template_var});")
        for instance_var in instance_vars:
            lines.extend(
                [
                    f"    if (!string.Equals({signature_var}, StructureSignature({instance_var}), StringComparison.Ordinal)) throw new InvalidOperationException(\"Repeated unit structure differs for component extraction: \" + {instance_var}.name);",
                    f"    AssertNoNestedPrefabRoots({instance_var});",
                    f"    AssertNoExternalReferences(root.transform, {instance_var});",
                ]
            )
        component_asset_var = f"componentAsset{extraction_index}"
        if mode == "reapply":
            lines.append(f"    var {component_asset_var} = AssetDatabase.LoadAssetAtPath<GameObject>({csharp(extraction['assetPath'])}) ?? CreateComponentPrefab({template_var}, {csharp(extraction['assetPath'])});")
        else:
            lines.append(f"    var {component_asset_var} = CreateComponentPrefab({template_var}, {csharp(extraction['assetPath'])});")
        for instance_var in instance_vars:
            lines.append(f"    ReplaceWithComponentInstance({instance_var}, {component_asset_var});")
    for extraction_index, extraction in enumerate(plan["state_component_extractions"]):
        template_var, source_vars = state_extraction_vars[extraction_index]
        default_state_index = next(index for index, state in enumerate(extraction["states"]) if state["id"] == extraction["defaultState"])
        state_names = ", ".join(csharp(state["name"]) for state in extraction["states"])
        source_array = ", ".join(source_vars)
        for source_var in source_vars:
            lines.extend(
                [
                    f"    AssertNoNestedPrefabRoots({source_var});",
                    f"    AssertNoExternalReferences(root.transform, {source_var});",
                ]
            )
        component_asset_var = f"stateComponentAsset{extraction_index}"
        if mode == "reapply":
            lines.append(f"    var {component_asset_var} = AssetDatabase.LoadAssetAtPath<GameObject>({csharp(extraction['assetPath'])}) ?? CreateStateComponentPrefab({template_var}, new[] {{ {source_array} }}, new[] {{ {state_names} }}, {default_state_index}, {csharp(extraction['assetPath'])});")
        else:
            lines.append(f"    var {component_asset_var} = CreateStateComponentPrefab({template_var}, new[] {{ {source_array} }}, new[] {{ {state_names} }}, {default_state_index}, {csharp(extraction['assetPath'])});")
        lines.append(f"    ReplaceStateSourcesWithComponent({template_var}, new[] {{ {source_array} }}, new[] {{ {state_names} }}, {default_state_index}, {component_asset_var});")
    for extraction_index, extraction in enumerate(plan["variant_component_extractions"]):
        template_var, source_vars, instance_vars = variant_extraction_vars[extraction_index]
        default_state_index = next(index for index, state in enumerate(extraction["states"]) if state["id"] == extraction["defaultState"])
        state_names = ", ".join(csharp(state["name"]) for state in extraction["states"])
        source_array = ", ".join(source_vars)
        for source_var in source_vars:
            lines.extend(
                [
                    f"    AssertNoNestedPrefabRoots({source_var});",
                    f"    AssertNoExternalReferences(root.transform, {source_var});",
                ]
            )
        component_asset_var = f"variantComponentAsset{extraction_index}"
        if mode == "reapply":
            lines.append(f"    var {component_asset_var} = AssetDatabase.LoadAssetAtPath<GameObject>({csharp(extraction['assetPath'])}) ?? CreateVariantComponentPrefab({template_var}, new[] {{ {source_array} }}, new[] {{ {state_names} }}, {default_state_index}, {csharp(extraction['assetPath'])});")
        else:
            lines.append(f"    var {component_asset_var} = CreateVariantComponentPrefab({template_var}, new[] {{ {source_array} }}, new[] {{ {state_names} }}, {default_state_index}, {csharp(extraction['assetPath'])});")
        state_names_by_id = {state["id"]: state["name"] for state in extraction["states"]}
        for instance, instance_var in zip(extraction["instances"], instance_vars):
            lines.append(f"    ReplaceVariantSourceWithComponent({instance_var}, {csharp(instance['name'])}, {csharp(state_names_by_id[instance['state']])}, {component_asset_var});")
    for extraction_index, extraction in enumerate(plan["stateful_component_extractions"]):
        template_var, common_source_var, state_source_vars, instance_vars = stateful_extraction_vars[extraction_index]
        default_state_index = next(index for index, state in enumerate(extraction["states"]) if state["id"] == extraction["defaultState"])
        common_source_names = [member["sourceName"] for member in extraction["common"]["members"]]
        common_target_names = [member["name"] for member in extraction["common"]["members"]]
        state_names = ", ".join(csharp(state["name"]) for state in extraction["states"])
        state_source_array = ", ".join(state_source_vars)
        state_member_source_arrays = ", ".join(
            csharp_string_array([member["sourceName"] for member in state["members"]])
            for state in extraction["states"]
        )
        state_member_target_arrays = ", ".join(
            csharp_string_array([member["name"] for member in state["members"]])
            for state in extraction["states"]
        )
        for instance_var in instance_vars:
            lines.extend(
                [
                    f"    AssertNoNestedPrefabRoots({instance_var});",
                    f"    AssertNoExternalReferences(root.transform, {instance_var});",
                ]
            )
        component_asset_var = f"statefulComponentAsset{extraction_index}"
        if mode == "reapply":
            lines.append(
                f"    var {component_asset_var} = AssetDatabase.LoadAssetAtPath<GameObject>({csharp(extraction['assetPath'])}) ?? CreateStatefulComponentPrefab({template_var}, {common_source_var}, {csharp_string_array(common_source_names)}, {csharp_string_array(common_target_names)}, new[] {{ {state_source_array} }}, new[] {{ {state_names} }}, new[] {{ {state_member_source_arrays} }}, new[] {{ {state_member_target_arrays} }}, {default_state_index}, {csharp(extraction['assetPath'])});"
            )
        else:
            lines.append(
                f"    var {component_asset_var} = CreateStatefulComponentPrefab({template_var}, {common_source_var}, {csharp_string_array(common_source_names)}, {csharp_string_array(common_target_names)}, new[] {{ {state_source_array} }}, new[] {{ {state_names} }}, new[] {{ {state_member_source_arrays} }}, new[] {{ {state_member_target_arrays} }}, {default_state_index}, {csharp(extraction['assetPath'])});"
            )
        state_by_id = {state["id"]: state for state in extraction["states"]}
        for instance, instance_var in zip(extraction["instances"], instance_vars):
            state = state_by_id[instance["state"]]
            instance_common_names = csharp_string_array(instance["commonSourceNames"])
            instance_state_names = csharp_string_array(instance["stateSourceNames"])
            state_target_names = csharp_string_array([member["name"] for member in state["members"]])
            lines.append(
                f"    ReplaceStatefulSourceWithComponent({instance_var}, {csharp(instance['name'])}, {csharp(state['name'])}, {instance_common_names}, {csharp_string_array(common_target_names)}, {instance_state_names}, {state_target_names}, {component_asset_var});"
            )

    if mode == "reapply" and all_assets:
        lines.append("    var replayAssetReferenceMap = new Dictionary<Object, Object>();")
        for index, rename in enumerate(all_assets):
            source = rename["from"]
            target = final_asset_path(source, rename["toName"])
            lines.append(f"    if (replayAssetAlreadyRenamed{index})")
            lines.append("    {")
            lines.append(f"        foreach (var pair in RefreshRenamedAsset({csharp(source)}, {csharp(target)}, {csharp(rename['expectedGuid'])})) replayAssetReferenceMap[pair.Key] = pair.Value;")
            lines.append("    }")
        lines.append("    RemapAssetReferences(root, replayAssetReferenceMap);")

    lines.extend(
        [
            "    var maxWorldCornerDelta = 0f;",
            "    foreach (var pair in beforeCorners)",
            "    {",
            "        var afterCorners = new Vector3[4]; ((RectTransform)pair.Key).GetWorldCorners(afterCorners);",
            "        for (var index = 0; index < 4; index++) maxWorldCornerDelta = Mathf.Max(maxWorldCornerDelta, Vector3.Distance(pair.Value[index], afterCorners[index]));",
            "    }",
            "    if (maxWorldCornerDelta > 0.01f) throw new InvalidOperationException(\"World-corner invariant failed: \" + maxWorldCornerDelta);",
        ]
    )

    if mode != "reapply" and all_assets:
        lines.extend(
            [
                "    var completedAssetRenames = new List<KeyValuePair<string, string>>();",
                "    try",
                "    {",
            ]
        )
        for index, rename in enumerate(all_assets):
            source = rename["from"]
            target = final_asset_path(source, rename["toName"])
            lines.extend(
                [
                    f"        var renameError{index} = AssetDatabase.RenameAsset({csharp(source)}, {csharp(rename['toName'])});",
                    f"        if (!string.IsNullOrEmpty(renameError{index})) throw new InvalidOperationException(\"Asset rename failed: \" + renameError{index});",
                    f"        completedAssetRenames.Add(new KeyValuePair<string, string>({csharp(source)}, {csharp(target)}));",
                ]
            )
        lines.extend(
            [
                "        if (PrefabUtility.SaveAsPrefabAsset(root, outputPath) == null) throw new InvalidOperationException(\"Prefab save failed: \" + outputPath);",
                "    }",
                "    catch",
                "    {",
                "        for (var rollbackIndex = completedAssetRenames.Count - 1; rollbackIndex >= 0; rollbackIndex--)",
                "        {",
                "            var pair = completedAssetRenames[rollbackIndex];",
                "            var rollbackError = AssetDatabase.RenameAsset(pair.Value, Path.GetFileNameWithoutExtension(pair.Key));",
                "            if (!string.IsNullOrEmpty(rollbackError)) Debug.LogError(\"Asset rename rollback failed: \" + rollbackError);",
                "        }",
                "        throw;",
                "    }",
            ]
        )
    else:
        lines.append("    if (PrefabUtility.SaveAsPrefabAsset(root, outputPath) == null) throw new InvalidOperationException(\"Prefab save failed: \" + outputPath);")

    lines.extend(
        [
            "}",
            "finally",
            "{",
            "    PrefabUtility.UnloadPrefabContents(root);",
            "}",
        ]
    )

    for index, rename in enumerate(all_assets):
        if mode == "reapply":
            lines.extend(
                [
                    f"if (replayAssetAlreadyRenamed{index})",
                    "{",
                    f"    if (!AssetDatabase.DeleteAsset({csharp(rename['from'])})) throw new InvalidOperationException(\"Could not delete replay source asset after remapping: \" + {csharp(rename['from'])});",
                    "}",
                    "else",
                    "{",
                    f"    var replayRenameError{index} = AssetDatabase.RenameAsset({csharp(rename['from'])}, {csharp(rename['toName'])});",
                    f"    if (!string.IsNullOrEmpty(replayRenameError{index})) throw new InvalidOperationException(\"Replay asset rename failed: \" + replayRenameError{index});",
                    "}",
                ]
            )
    lines.extend(["AssetDatabase.SaveAssets();", "AssetDatabase.Refresh();", ""])
    lines.extend(emit_verification(plan, mode))
    return "\n".join(lines) + "\n"


def render_snapshot(prefab_path: str) -> str:
    """Render a read-only Unity payload that reports the entire Prefab tree."""
    return "\n".join(
        [
            "using System;",
            "using System.Collections.Generic;",
            "using System.Text;",
            "using TMPro;",
            "using UnityEditor;",
            "using UnityEngine;",
            "using UnityEngine.UI;",
            "",
            f"var prefabPath = {csharp(prefab_path)};",
            "",
            "string Escape(string value)",
            "{",
            "    return (value ?? string.Empty).Replace(\"\\\\\", \"\\\\\\\\\").Replace(\"\\t\", \"\\\\t\").Replace(\"\\r\", \"\\\\r\").Replace(\"\\n\", \"\\\\n\");",
            "}",
            "",
            "string TransformPath(Transform transform)",
            "{",
            "    var parts = new Stack<string>();",
            "    for (var current = transform; current != null; current = current.parent) parts.Push(current.name);",
            "    return string.Join(\"/\", parts.ToArray());",
            "}",
            "",
            "string Vector2Text(Vector2 value)",
            "{",
            "    return value.x.ToString(\"0.###\", System.Globalization.CultureInfo.InvariantCulture) + \",\" + value.y.ToString(\"0.###\", System.Globalization.CultureInfo.InvariantCulture);",
            "}",
            "",
            "string Vector3Text(Vector3 value)",
            "{",
            "    return value.x.ToString(\"0.###\", System.Globalization.CultureInfo.InvariantCulture) + \",\" + value.y.ToString(\"0.###\", System.Globalization.CultureInfo.InvariantCulture) + \",\" + value.z.ToString(\"0.###\", System.Globalization.CultureInfo.InvariantCulture);",
            "}",
            "",
            "var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);",
            "if (prefab == null) throw new InvalidOperationException(\"Prefab did not load: \" + prefabPath);",
            "var root = PrefabUtility.LoadPrefabContents(prefabPath);",
            "try",
            "{",
            "    var nodes = new List<Transform>();",
            "    var pending = new Stack<Transform>();",
            "    pending.Push(root.transform);",
            "    while (pending.Count > 0)",
            "    {",
            "        var current = pending.Pop();",
            "        nodes.Add(current);",
            "        for (var index = current.childCount - 1; index >= 0; index--) pending.Push(current.GetChild(index));",
            "    }",
            "",
            "    var componentCount = 0;",
            "    var missingComponentCount = 0;",
            "    var objectReferenceCount = 0;",
            "    var imageCount = 0;",
            "    var textCount = 0;",
            "    var nestedPrefabCount = 0;",
            "    var output = new StringBuilder();",
            "    output.AppendLine(\"SNAPSHOT_BEGIN\");",
            "    foreach (var node in nodes)",
            "    {",
            "        var components = new List<string>();",
            "        var details = new List<string>();",
            "        var nodeComponents = node.GetComponents<Component>();",
            "        foreach (var component in nodeComponents)",
            "        {",
            "            if (component == null)",
            "            {",
            "                missingComponentCount++;",
            "                components.Add(\"MissingComponent\");",
            "                continue;",
            "            }",
            "",
            "            componentCount++;",
            "            components.Add(component.GetType().Name);",
            "            var serialized = new SerializedObject(component);",
            "            var property = serialized.GetIterator();",
            "            var enterChildren = true;",
            "            while (property.NextVisible(enterChildren))",
            "            {",
            "                enterChildren = false;",
            "                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null) objectReferenceCount++;",
            "            }",
            "        }",
            "",
            "        var image = node.GetComponent<Image>();",
            "        if (image != null)",
            "        {",
            "            imageCount++;",
            "            var spritePath = image.sprite == null ? string.Empty : AssetDatabase.GetAssetPath(image.sprite);",
            "            var texturePath = image.sprite == null || image.sprite.texture == null ? string.Empty : AssetDatabase.GetAssetPath(image.sprite.texture);",
            "            details.Add(\"Image(sprite=\" + Escape(image.sprite == null ? string.Empty : image.sprite.name) + \",texture=\" + Escape(texturePath) + \",type=\" + image.type + \")\");",
            "        }",
            "",
            "        var text = node.GetComponent<TMP_Text>();",
            "        if (text != null)",
            "        {",
            "            textCount++;",
            "            details.Add(\"TMP(text=\" + Escape(text.text) + \",font=\" + Escape(text.font == null ? string.Empty : AssetDatabase.GetAssetPath(text.font)) + \",material=\" + Escape(text.fontSharedMaterial == null ? string.Empty : AssetDatabase.GetAssetPath(text.fontSharedMaterial)) + \")\");",
            "        }",
            "",
            "        var isNestedPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(node.gameObject);",
            "        if (isNestedPrefab) nestedPrefabCount++;",
            "        var rect = node as RectTransform;",
            "        var rectInfo = rect == null ? string.Empty : \"anchorMin=\" + Vector2Text(rect.anchorMin) + \",anchorMax=\" + Vector2Text(rect.anchorMax) + \",pivot=\" + Vector2Text(rect.pivot) + \",anchoredPosition=\" + Vector2Text(rect.anchoredPosition) + \",sizeDelta=\" + Vector2Text(rect.sizeDelta) + \",scale=\" + Vector3Text(rect.localScale) + \",rotation=\" + Vector3Text(rect.localEulerAngles);",
            "        output.Append(\"NODE\\t\").Append(Escape(TransformPath(node))).Append(\"\\tdepth=\").Append(node.GetComponentsInParent<Transform>(true).Length - 1).Append(\"\\tsibling=\").Append(node.GetSiblingIndex()).Append(\"\\tactive=\").Append(node.gameObject.activeSelf).Append(\"\\tchildren=\").Append(node.childCount).Append(\"\\tnestedPrefab=\").Append(isNestedPrefab).Append(\"\\tcomponents=\").Append(Escape(string.Join(\",\", components.ToArray()))).Append(\"\\trect=\").Append(Escape(rectInfo)).Append(\"\\tdetails=\").Append(Escape(string.Join(\";\", details.ToArray()))).AppendLine();",
            "    }",
            "    output.Insert(\"SNAPSHOT_BEGIN\\n\".Length, \"SUMMARY\\tnodes=\" + nodes.Count + \"\\tcomponents=\" + componentCount + \"\\tmissingComponents=\" + missingComponentCount + \"\\tobjectReferences=\" + objectReferenceCount + \"\\timages=\" + imageCount + \"\\ttmpText=\" + textCount + \"\\tnestedPrefabRoots=\" + nestedPrefabCount + \"\\n\");",
            "    output.AppendLine(\"SNAPSHOT_END\");",
            "    return output.ToString();",
            "}",
            "finally",
            "{",
            "    PrefabUtility.UnloadPrefabContents(root);",
            "}",
            "",
        ]
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--plan", type=Path)
    parser.add_argument("--mode", required=True, choices=("apply", "preflight", "verify", "reapply", "snapshot"))
    parser.add_argument("--prefab-path")
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    try:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        if args.mode == "snapshot":
            args.output.write_text(render_snapshot(asset_path(args.prefab_path, "prefabPath")), encoding="utf-8")
        else:
            if args.plan is None:
                fail("--plan is required for apply, preflight, verify, and reapply modes")
            raw = json.loads(args.plan.read_text(encoding="utf-8"))
            if not isinstance(raw, dict):
                fail("plan root must be an object")
            plan = normalize_plan(raw, args.mode)
            args.output.write_text(render(plan, args.mode), encoding="utf-8")
    except (OSError, json.JSONDecodeError, ValueError) as error:
        parser.error(str(error))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
