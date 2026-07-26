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


def csharp(value: str) -> str:
    return json.dumps(value, ensure_ascii=True)


def final_asset_path(source: str, new_name: str) -> str:
    source_path = PurePosixPath(source)
    return str(source_path.with_name(new_name + source_path.suffix))


def state_component_target_path(extraction: dict[str, Any]) -> str:
    parent_path = extraction["template"].rsplit("/", 1)[0]
    return parent_path + "/" + PurePosixPath(extraction["assetPath"]).stem


def normalize_plan(raw: dict[str, Any], mode: str) -> dict[str, Any]:
    if raw.get("version") != 1:
        fail("version must be 1")

    prefab_path = asset_path(raw.get("prefabAssetPath"), "prefabAssetPath")
    output = raw.get("output")
    if not isinstance(output, dict):
        fail("output must be an object")
    output_mode = require_string(output.get("mode"), "output.mode")
    output_path = asset_path(output.get("assetPath"), "output.assetPath")
    if output_mode not in {"copy", "in_place"}:
        fail("output.mode must be copy or in_place")
    if output_mode == "copy" and output_path == prefab_path:
        fail("copy output.assetPath must differ from prefabAssetPath")
    if output_mode == "in_place" and output_path != prefab_path:
        fail("in_place output.assetPath must equal prefabAssetPath")

    prefab_name = require_string(raw.get("prefabName"), "prefabName")
    wrappers = require_list(raw.get("wrappers", []), "wrappers")
    moves = require_list(raw.get("moves", []), "moves")
    renames = require_list(raw.get("renames", []), "renames")
    tight_bounds = require_list(raw.get("tightBounds", []), "tightBounds")
    texture_renames = require_list(raw.get("textureRenames", []), "textureRenames")
    atlas_renames = require_list(raw.get("spriteAtlasRenames", []), "spriteAtlasRenames")
    component_extractions = require_list(raw.get("componentExtractions", []), "componentExtractions")
    state_component_extractions = require_list(raw.get("stateComponentExtractions", []), "stateComponentExtractions")
    verify = raw.get("verify", {})
    if not isinstance(verify, dict):
        fail("verify must be an object")

    if (texture_renames or atlas_renames) and not VIEW_RE.match(prefab_name):
        fail("prefabName must be PascalCase and end with View when renaming private assets")
    if (component_extractions or state_component_extractions) and (wrappers or moves or renames or tight_bounds):
        fail("component extraction must run as a standalone plan after hierarchy cleanup")
    if component_extractions and state_component_extractions:
        fail("componentExtractions and stateComponentExtractions must run in separate plans")

    wrapper_ids: set[str] = set()
    for index, wrapper in enumerate(wrappers):
        wrapper_id = require_string(wrapper.get("id"), f"wrappers[{index}].id")
        if not re.match(r"^[a-z][a-z0-9_]*$", wrapper_id):
            fail(f"wrappers[{index}].id must be lower snake_case")
        if wrapper_id in wrapper_ids:
            fail(f"duplicate wrapper id: {wrapper_id}")
        wrapper_ids.add(wrapper_id)
        parent = require_string(wrapper.get("parent"), f"wrappers[{index}].parent")
        if parent.startswith("@") and parent[1:] not in wrapper_ids:
            fail(f"wrappers[{index}].parent references an unknown or later wrapper")
        if not parent.startswith("@"):
            require_string(parent, f"wrappers[{index}].parent")
        require_string(wrapper.get("name"), f"wrappers[{index}].name")
        if not isinstance(wrapper.get("siblingIndex"), int) or wrapper["siblingIndex"] < 0:
            fail(f"wrappers[{index}].siblingIndex must be a non-negative integer")

    move_sources: set[str] = set()
    for index, move in enumerate(moves):
        source = require_string(move.get("source"), f"moves[{index}].source")
        destination = require_string(move.get("destination"), f"moves[{index}].destination")
        if source in move_sources:
            fail(f"each move source must be unique: {source}")
        move_sources.add(source)
        if destination.startswith("@") and destination[1:] not in wrapper_ids:
            fail(f"moves[{index}].destination references an unknown wrapper")
        if not isinstance(move.get("siblingIndex"), int) or move["siblingIndex"] < 0:
            fail(f"moves[{index}].siblingIndex must be a non-negative integer")

    for index, rename in enumerate(renames):
        target = require_string(rename.get("target"), f"renames[{index}].target")
        if target.startswith("@") and target[1:] not in wrapper_ids:
            fail(f"renames[{index}].target references an unknown wrapper")
        require_string(rename.get("name"), f"renames[{index}].name")

    if not tight_bounds:
        tight_bounds = [{"target": "@" + wrapper["id"]} for wrapper in wrappers]

    tight_targets: set[str] = set()
    for index, tight_bound in enumerate(tight_bounds):
        target = require_string(tight_bound.get("target"), f"tightBounds[{index}].target")
        if target.startswith("@") and target[1:] not in wrapper_ids:
            fail(f"tightBounds[{index}].target references an unknown wrapper")
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
        component_asset_path = asset_path(extraction.get("assetPath"), f"componentExtractions[{index}].assetPath")
        if not component_asset_path.endswith(".prefab"):
            fail(f"componentExtractions[{index}].assetPath must end with .prefab")
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
        component_asset_path = asset_path(extraction.get("assetPath"), f"stateComponentExtractions[{index}].assetPath")
        if not component_asset_path.endswith(".prefab"):
            fail(f"stateComponentExtractions[{index}].assetPath must end with .prefab")
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
    if "requireEnglishNames" in verify and not isinstance(verify["requireEnglishNames"], bool):
        fail("verify.requireEnglishNames must be a boolean")

    for key in ("nodes", "components", "objectReferences", "missingComponents", "images", "prefixedTextures"):
        if key in verify and (not isinstance(verify[key], int) or verify[key] < 0):
            fail(f"verify.{key} must be a non-negative integer")
    if verify.get("requireAllImageTexturesPrefixed") and not isinstance(verify.get("texturePathPrefix"), str):
        fail("verify.texturePathPrefix is required when all image textures must be prefixed")

    return {
        "prefab_path": prefab_path,
        "output_mode": output_mode,
        "output_path": output_path,
        "prefab_name": prefab_name,
        "wrappers": wrappers,
        "moves": moves,
        "renames": renames,
        "tight_bounds": tight_bounds,
        "texture_renames": texture_renames,
        "atlas_renames": atlas_renames,
        "component_extractions": component_extractions,
        "state_component_extractions": state_component_extractions,
        "verify": verify,
    }


def value_or_default(values: dict[str, Any], key: str, default: int = -1) -> int:
    value = values.get(key, default)
    return value if isinstance(value, int) else default


def emit_verification(plan: dict[str, Any], mode: str) -> list[str]:
    verify = plan["verify"]
    require_english_names = bool(verify.get("requireEnglishNames", False))
    lines = [
        "var reopened = PrefabUtility.LoadPrefabContents(outputPath);",
        "try",
        "{",
        "    var missingComponents = 0;",
        "    var invalidNames = new List<string>();",
        "    var nodes = CountNodes(reopened.transform);",
        "    var components = CountComponents(reopened.transform, ref missingComponents);",
        "    var objectReferences = CountObjectReferences(reopened.transform);",
        "    var images = reopened.GetComponentsInChildren<Image>(true);",
        "    var prefixedTexturePaths = new HashSet<string>(StringComparer.Ordinal);",
        "    foreach (var image in images)",
        "    {",
        "        if (image.sprite == null || image.sprite.texture == null)",
        "        {",
        "            throw new InvalidOperationException(\"Image has a missing Sprite: \" + image.transform.name);",
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
    for index, item in enumerate(verify.get("hierarchy", [])):
        lines.extend(
            [
                f"    var hierarchyNode{index} = FindByPath(reopened, {csharp(item['path'])});",
                f"    AssertExpected({csharp(item['path'] + '.childCount')}, hierarchyNode{index}.transform.childCount, {item['childCount']});",
            ]
        )
    for index, item in enumerate(verify.get("directChildren", [])):
        expected_children = ", ".join(csharp(child) for child in item["children"])
        lines.extend(
            [
                f"    var directChildrenNode{index} = FindByPath(reopened, {csharp(item['path'])}).transform;",
                f"    AssertDirectChildren(directChildrenNode{index}, new[] {{ {expected_children} }}, {csharp(item['path'])});",
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
        state_names = ", ".join(csharp(state["name"]) for state in extraction["states"])
        default_state = next(state for state in extraction["states"] if state["id"] == extraction["defaultState"])
        lines.extend(
            [
                f"    var stateComponentAsset_{extraction['id']} = AssetDatabase.LoadAssetAtPath<GameObject>({csharp(extraction['assetPath'])});",
                f"    if (stateComponentAsset_{extraction['id']} == null) throw new InvalidOperationException(\"Extracted state component Prefab did not load: \" + {csharp(extraction['assetPath'])});",
                f"    var stateComponentInstance_{extraction['id']} = FindByPath(reopened, {csharp(target_path)});",
                f"    AssertNestedPrefabInstance(stateComponentInstance_{extraction['id']}, {csharp(extraction['assetPath'])});",
                f"    var statesContainer_{extraction['id']} = stateComponentInstance_{extraction['id']}.transform.Find(\"[States]\");",
                f"    if (statesContainer_{extraction['id']} == null) throw new InvalidOperationException(\"State component has no [States] container: \" + stateComponentInstance_{extraction['id']}.name);",
                f"    AssertDirectChildren(statesContainer_{extraction['id']}, new[] {{ {state_names} }}, {csharp(target_path + '/[States]')});",
                f"    AssertExclusiveActiveState(statesContainer_{extraction['id']}, {csharp(default_state['name'])}, {csharp(target_path)});",
            ]
        )

    for rename in plan["texture_renames"] + plan["atlas_renames"]:
        final_path = final_asset_path(rename["from"], rename["toName"])
        lines.append(f"    AssertGuid({csharp(final_path)}, {csharp(rename['expectedGuid'])});")
        lines.append(f"    if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>({csharp(final_path)}) == null) throw new InvalidOperationException(\"Renamed asset did not load: \" + {csharp(final_path)});")

    lines.extend(
        [
            "    return \"VERIFY_OK nodes=\" + nodes + \";components=\" + components + \";objectReferences=\" + objectReferences + \";missingComponents=\" + missingComponents + \";images=\" + images.Length + \";prefixedTextures=\" + prefixedTexturePaths.Count;",
            "}",
            "finally",
            "{",
            "    PrefabUtility.UnloadPrefabContents(reopened);",
            "}",
        ]
    )
    return lines


def render(plan: dict[str, Any], mode: str) -> str:
    verify = plan["verify"]
    prefix = verify.get("texturePathPrefix", "")
    require_prefix = bool(verify.get("requireAllImageTexturesPrefixed", False))
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
        "        Transform next = null;",
        "        for (var childIndex = 0; childIndex < current.childCount; childIndex++)",
        "        {",
        "            var child = current.GetChild(childIndex);",
        "            if (child.name == parts[index]) { next = child; break; }",
        "        }",
        "        if (next == null) throw new InvalidOperationException(\"Path was not found: \" + path);",
        "        current = next;",
        "    }",
        "    return current.gameObject;",
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
        "void CopyHierarchyOverrides(Transform source, Transform destination, Dictionary<Object, Object> objectMap)",
        "{",
        "    destination.name = source.name; destination.gameObject.layer = source.gameObject.layer; destination.gameObject.tag = source.gameObject.tag; CopyTransformData(source, destination);",
        "    foreach (var sourceComponent in source.GetComponents<Component>())",
        "    {",
        "        if (sourceComponent == null || sourceComponent is Transform) continue;",
        "        var destinationComponent = (Component)objectMap[sourceComponent]; EditorUtility.CopySerialized(sourceComponent, destinationComponent); RemapObjectReferences(destinationComponent, objectMap);",
        "        PrefabUtility.RecordPrefabInstancePropertyModifications(destinationComponent);",
        "    }",
        "    PrefabUtility.RecordPrefabInstancePropertyModifications(destination); PrefabUtility.RecordPrefabInstancePropertyModifications(destination.gameObject);",
        "    for (var childIndex = 0; childIndex < source.childCount; childIndex++) CopyHierarchyOverrides(source.GetChild(childIndex), destination.GetChild(childIndex), objectMap);",
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
        "void ReplaceWithComponentInstance(Transform source, GameObject componentAsset)",
        "{",
        "    var parent = source.parent; if (parent == null) throw new InvalidOperationException(\"Cannot replace the Prefab root with a nested component instance\");",
        "    var siblingIndex = source.GetSiblingIndex(); var beforeCorners = new List<Vector3[]>(); CaptureHierarchyCorners(source, beforeCorners);",
        "    var instance = PrefabUtility.InstantiatePrefab(componentAsset) as GameObject; if (instance == null) throw new InvalidOperationException(\"Failed to instantiate component Prefab: \" + componentAsset.name);",
        "    var destination = instance.transform; destination.SetParent(parent, false); destination.SetSiblingIndex(siblingIndex);",
        "    var objectMap = new Dictionary<Object, Object>(); BuildObjectMap(source, destination, objectMap); CopyHierarchyOverrides(source, destination, objectMap);",
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
        "void CollectInvalidNames(Transform node, List<string> invalidNames)",
        "{",
        "    if (!System.Text.RegularExpressions.Regex.IsMatch(node.name, @\"^[A-Za-z0-9_\\[\\]]+$\")) invalidNames.Add(node.name);",
        "    for (var index = 0; index < node.childCount; index++) CollectInvalidNames(node.GetChild(index), invalidNames);",
        "}",
        "",
    ]

    if mode == "verify":
        lines.extend(emit_verification(plan, mode))
        return "\n".join(lines) + "\n"

    lines.extend(
        [
            "if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) throw new InvalidOperationException(\"Prefab did not load: \" + prefabPath);",
            "if (outputPath != prefabPath && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(outputPath) != null) throw new InvalidOperationException(\"Refusing to overwrite copy output: \" + outputPath);",
        ]
    )

    all_assets = plan["texture_renames"] + plan["atlas_renames"]
    for index, rename in enumerate(all_assets):
        source = rename["from"]
        target = final_asset_path(source, rename["toName"])
        lines.extend(
            [
                f"if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>({csharp(source)}) == null) throw new InvalidOperationException(\"Source asset did not load: \" + {csharp(source)});",
                f"if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>({csharp(target)}) != null) throw new InvalidOperationException(\"Rename target already exists: \" + {csharp(target)});",
                f"AssertGuid({csharp(source)}, {csharp(rename['expectedGuid'])});",
            ]
        )
    for extraction in plan["component_extractions"]:
        lines.append(f"if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>({csharp(extraction['assetPath'])}) != null) throw new InvalidOperationException(\"Component Prefab target already exists: \" + {csharp(extraction['assetPath'])});")
    for extraction in plan["state_component_extractions"]:
        lines.append(f"if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>({csharp(extraction['assetPath'])}) != null) throw new InvalidOperationException(\"State component Prefab target already exists: \" + {csharp(extraction['assetPath'])});")

    lines.extend(["var root = PrefabUtility.LoadPrefabContents(prefabPath);", "try", "{"])
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
    for _, instance_vars in extraction_vars:
        for instance_var in instance_vars:
            lines.append(f"    ExcludeHierarchy({instance_var}, excludedCornerNodes);")
    for _, source_vars in state_extraction_vars:
        for source_var in source_vars:
            lines.append(f"    ExcludeHierarchy({source_var}, excludedCornerNodes);")
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
        lines.append(f"    var {component_asset_var} = CreateStateComponentPrefab({template_var}, new[] {{ {source_array} }}, new[] {{ {state_names} }}, {default_state_index}, {csharp(extraction['assetPath'])});")
        lines.append(f"    ReplaceStateSourcesWithComponent({template_var}, new[] {{ {source_array} }}, new[] {{ {state_names} }}, {default_state_index}, {component_asset_var});")

    lines.extend(
        [
            "    var maxWorldCornerDelta = 0f;",
            "    foreach (var pair in beforeCorners)",
            "    {",
            "        var afterCorners = new Vector3[4]; ((RectTransform)pair.Key).GetWorldCorners(afterCorners);",
            "        for (var index = 0; index < 4; index++) maxWorldCornerDelta = Mathf.Max(maxWorldCornerDelta, Vector3.Distance(pair.Value[index], afterCorners[index]));",
            "    }",
            "    if (maxWorldCornerDelta > 0.01f) throw new InvalidOperationException(\"World-corner invariant failed: \" + maxWorldCornerDelta);",
            "    if (PrefabUtility.SaveAsPrefabAsset(root, outputPath) == null) throw new InvalidOperationException(\"Prefab save failed: \" + outputPath);",
            "}",
            "finally",
            "{",
            "    PrefabUtility.UnloadPrefabContents(root);",
            "}",
        ]
    )

    for index, rename in enumerate(all_assets):
        lines.extend(
            [
                f"var renameError{index} = AssetDatabase.RenameAsset({csharp(rename['from'])}, {csharp(rename['toName'])});",
                f"if (!string.IsNullOrEmpty(renameError{index})) throw new InvalidOperationException(\"Asset rename failed: \" + renameError{index});",
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
    parser.add_argument("--mode", required=True, choices=("apply", "verify", "snapshot"))
    parser.add_argument("--prefab-path")
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    try:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        if args.mode == "snapshot":
            args.output.write_text(render_snapshot(asset_path(args.prefab_path, "prefabPath")), encoding="utf-8")
        else:
            if args.plan is None:
                fail("--plan is required for apply and verify modes")
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
