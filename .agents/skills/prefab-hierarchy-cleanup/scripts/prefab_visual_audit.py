"""Generate read-only NativeUnity audit payloads and compare Prefab renders.

This module deliberately does not invoke Unity.  It produces parameterized C#
payloads for the caller's selected NativeUnity backend and provides a strict
RGBA image comparison command.
"""

from __future__ import annotations

import argparse
import json
import math
import sys
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Iterable

from PIL import Image, ImageChops


DEFAULT_OWNER_EXTENSIONS = (".prefab", ".unity", ".asset", ".mat", ".spriteatlas")


def csharp_string(value: str) -> str:
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"').replace("\r", "\\r").replace("\n", "\\n") + '"'


def csharp_float(value: float) -> str:
    if not math.isfinite(value):
        raise ValueError("floating-point arguments must be finite")
    text = format(value, ".9g")
    if "." not in text and "e" not in text.lower():
        text += ".0"
    return text + "f"


def require_asset_path(value: str, *, suffix: str | None = None) -> str:
    normalized = value.replace("\\", "/").strip()
    parts = PurePosixPath(normalized).parts
    if (normalized != "Assets" and not normalized.startswith("Assets/")) or ".." in parts:
        raise ValueError(f"Unity asset path must be below Assets/: {value}")
    if suffix is not None and not normalized.lower().endswith(suffix.lower()):
        raise ValueError(f"Unity asset path must end with {suffix}: {value}")
    return normalized


def require_relative_node_path(value: str) -> str:
    normalized = value.replace("\\", "/").strip("/")
    parts = PurePosixPath(normalized).parts
    if not normalized or normalized == "." or ".." in parts:
        raise ValueError(f"state override must name a descendant path: {value}")
    return normalized


@dataclass(frozen=True)
class StateOverride:
    relative_path: str
    active: bool


def parse_state_override(value: str) -> StateOverride:
    try:
        raw_path, raw_active = value.rsplit("=", 1)
    except ValueError as exc:
        raise ValueError("state override must use RELATIVE/PATH=true|false") from exc
    lowered = raw_active.strip().lower()
    if lowered not in {"true", "false"}:
        raise ValueError("state override value must be true or false")
    return StateOverride(require_relative_node_path(raw_path), lowered == "true")


def render_capture_payload(
    *,
    prefab_asset_path: str,
    image_output_path: str,
    width: int,
    height: int,
    camera_position: tuple[float, float, float],
    orthographic_size: float,
    near_clip: float,
    far_clip: float,
    background_rgba: tuple[float, float, float, float],
    state_overrides: Iterable[StateOverride] = (),
) -> str:
    prefab_asset_path = require_asset_path(prefab_asset_path, suffix=".prefab")
    if width <= 0 or height <= 0:
        raise ValueError("capture dimensions must be positive")
    if orthographic_size <= 0 or near_clip <= 0 or far_clip <= near_clip:
        raise ValueError("camera clipping and orthographic size are invalid")
    if any(channel < 0 or channel > 1 for channel in background_rgba):
        raise ValueError("background RGBA channels must be between 0 and 1")

    overrides = list(state_overrides)
    paths = [require_relative_node_path(item.relative_path) for item in overrides]
    if len(paths) != len(set(paths)):
        raise ValueError("state override paths must be unique")

    override_rows = ",\n    ".join(
        "new System.Collections.Generic.KeyValuePair<string, bool>(%s, %s)"
        % (csharp_string(path), "true" if item.active else "false")
        for path, item in zip(paths, overrides)
    )
    x, y, z = camera_position
    r, g, b, a = background_rgba
    return f'''var prefabAssetPath = {csharp_string(prefab_asset_path)};
var imageOutputPath = {csharp_string(image_output_path)};
var captureWidth = {width};
var captureHeight = {height};
var reviewedStateOverrides = new System.Collections.Generic.KeyValuePair<string, bool>[] {{
    {override_rows}
}};
var root = UnityEditor.PrefabUtility.LoadPrefabContents(prefabAssetPath);
UnityEngine.GameObject canvasObject = null;
UnityEngine.GameObject cameraObject = null;
UnityEngine.RenderTexture renderTexture = null;
UnityEngine.Texture2D image = null;
var restoredStates = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<UnityEngine.GameObject, bool>>();
try {{
    System.Func<UnityEngine.Transform, string, UnityEngine.Transform> findExactDescendant = (start, relativePath) => {{
        var current = start;
        foreach (var segment in relativePath.Split('/')) {{
            UnityEngine.Transform match = null;
            var matches = 0;
            for (var index = 0; index < current.childCount; index++) {{
                var child = current.GetChild(index);
                if (child.name != segment) continue;
                match = child;
                matches++;
            }}
            if (matches != 1) throw new System.InvalidOperationException("STATE_OVERRIDE_PATH_NOT_UNIQUE path=" + relativePath + " segment=" + segment + " matches=" + matches);
            current = match;
        }}
        return current;
    }};
    foreach (var stateOverride in reviewedStateOverrides) {{
        var target = findExactDescendant(root.transform, stateOverride.Key).gameObject;
        restoredStates.Add(new System.Collections.Generic.KeyValuePair<UnityEngine.GameObject, bool>(target, target.activeSelf));
        target.SetActive(stateOverride.Value);
    }}

    canvasObject = new UnityEngine.GameObject("PrefabVisualAuditCanvas", typeof(UnityEngine.Canvas));
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasObject, root.scene);
    var canvas = canvasObject.GetComponent<UnityEngine.Canvas>();
    canvas.renderMode = UnityEngine.RenderMode.WorldSpace;
    root.transform.SetParent(canvasObject.transform, false);

    cameraObject = new UnityEngine.GameObject("PrefabVisualAuditCamera", typeof(UnityEngine.Camera));
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, root.scene);
    var camera = cameraObject.GetComponent<UnityEngine.Camera>();
    camera.scene = root.scene;
    camera.transform.position = new UnityEngine.Vector3({csharp_float(x)}, {csharp_float(y)}, {csharp_float(z)});
    camera.orthographic = true;
    camera.orthographicSize = {csharp_float(orthographic_size)};
    camera.aspect = (float)captureWidth / captureHeight;
    camera.nearClipPlane = {csharp_float(near_clip)};
    camera.farClipPlane = {csharp_float(far_clip)};
    camera.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
    camera.backgroundColor = new UnityEngine.Color({csharp_float(r)}, {csharp_float(g)}, {csharp_float(b)}, {csharp_float(a)});
    canvas.worldCamera = camera;
    foreach (var textComponent in root.GetComponentsInChildren<TMPro.TMP_Text>(true)) textComponent.ForceMeshUpdate(true, true);
    UnityEngine.Canvas.ForceUpdateCanvases();

    renderTexture = new UnityEngine.RenderTexture(captureWidth, captureHeight, 24, UnityEngine.RenderTextureFormat.ARGB32);
    camera.targetTexture = renderTexture;
    camera.Render();
    var previousRenderTexture = UnityEngine.RenderTexture.active;
    try {{
        UnityEngine.RenderTexture.active = renderTexture;
        image = new UnityEngine.Texture2D(captureWidth, captureHeight, UnityEngine.TextureFormat.RGBA32, false);
        image.ReadPixels(new UnityEngine.Rect(0, 0, captureWidth, captureHeight), 0, 0);
        image.Apply();
    }} finally {{
        UnityEngine.RenderTexture.active = previousRenderTexture;
    }}
    var outputDirectory = System.IO.Path.GetDirectoryName(imageOutputPath);
    if (!string.IsNullOrEmpty(outputDirectory)) System.IO.Directory.CreateDirectory(outputDirectory);
    System.IO.File.WriteAllBytes(imageOutputPath, image.EncodeToPNG());
    var stateCoverage = string.Join("|", reviewedStateOverrides.Select(stateOverride => stateOverride.Key + "=" + stateOverride.Value.ToString().ToLowerInvariant()));
    return "CAPTURE_OK path=" + imageOutputPath + ";width=" + captureWidth + ";height=" + captureHeight + ";reviewedStateOverrides=" + reviewedStateOverrides.Length + ";stateCoverage=" + stateCoverage;
}} finally {{
    for (var index = restoredStates.Count - 1; index >= 0; index--) {{
        var saved = restoredStates[index];
        if (saved.Key != null) saved.Key.SetActive(saved.Value);
    }}
    if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
    if (image != null) UnityEngine.Object.DestroyImmediate(image);
    if (renderTexture != null) UnityEngine.Object.DestroyImmediate(renderTexture);
    if (root != null) root.transform.SetParent(null, false);
    if (canvasObject != null) UnityEngine.Object.DestroyImmediate(canvasObject);
    if (root != null) UnityEditor.PrefabUtility.UnloadPrefabContents(root);
}}'''


def render_texture_ownership_payload(
    *, texture_directories: Iterable[str], owner_roots: Iterable[str], owner_extensions: Iterable[str]
) -> str:
    directories = [require_asset_path(value) for value in texture_directories]
    roots = [require_asset_path(value) for value in owner_roots]
    if not directories or not roots:
        raise ValueError("at least one texture directory and owner root are required")
    extensions = []
    for value in owner_extensions:
        extension = value.strip().lower()
        if not extension.startswith(".") or "/" in extension or "\\" in extension:
            raise ValueError(f"invalid owner extension: {value}")
        extensions.append(extension)
    if not extensions:
        raise ValueError("at least one owner extension is required")

    cs_directories = ", ".join(csharp_string(value) for value in directories)
    cs_roots = ", ".join(csharp_string(value) for value in roots)
    cs_extensions = ", ".join(csharp_string(value) for value in extensions)
    return f'''var textureDirectories = new [] {{ {cs_directories} }};
var ownerRoots = new [] {{ {cs_roots} }};
var ownerExtensions = new System.Collections.Generic.HashSet<string>(new [] {{ {cs_extensions} }}, System.StringComparer.OrdinalIgnoreCase);
var texturePaths = UnityEditor.AssetDatabase.FindAssets("t:Texture2D", textureDirectories)
    .Select(guid => UnityEditor.AssetDatabase.GUIDToAssetPath(guid))
    .Where(path => !string.IsNullOrEmpty(path))
    .Distinct(System.StringComparer.Ordinal)
    .OrderBy(path => path, System.StringComparer.Ordinal)
    .ToArray();
var wantedTextures = new System.Collections.Generic.HashSet<string>(texturePaths, System.StringComparer.Ordinal);
var ownership = texturePaths.ToDictionary(path => path, path => new System.Collections.Generic.List<string>(), System.StringComparer.Ordinal);
foreach (var ownerPath in UnityEditor.AssetDatabase.GetAllAssetPaths().OrderBy(path => path, System.StringComparer.Ordinal)) {{
    if (!ownerRoots.Any(rootPath => ownerPath == rootPath || ownerPath.StartsWith(rootPath.TrimEnd('/') + "/", System.StringComparison.Ordinal))) continue;
    if (!ownerExtensions.Contains(System.IO.Path.GetExtension(ownerPath))) continue;
    foreach (var dependency in UnityEditor.AssetDatabase.GetDependencies(ownerPath, true).Distinct(System.StringComparer.Ordinal)) {{
        if (dependency == ownerPath || !wantedTextures.Contains(dependency)) continue;
        ownership[dependency].Add(ownerPath);
    }}
}}
var report = new System.Text.StringBuilder();
report.AppendLine("TEXTURE_OWNERSHIP_V1");
foreach (var texturePath in texturePaths) {{
    var guid = UnityEditor.AssetDatabase.AssetPathToGUID(texturePath);
    var owners = ownership[texturePath].Distinct(System.StringComparer.Ordinal).OrderBy(path => path, System.StringComparer.Ordinal).ToArray();
    report.Append("TEXTURE\\t").Append(guid).Append('\\t').Append(texturePath).Append('\\t').Append(owners.Length).Append('\\t').AppendLine(string.Join("|", owners));
}}
report.Append("SUMMARY\\ttextures=").Append(texturePaths.Length).Append("\\towners=").Append(ownership.Values.SelectMany(value => value).Distinct(System.StringComparer.Ordinal).Count()).Append("\\tdependencyMode=recursive");
return report.ToString();'''


def write_payload(payload: str, output: Path | None) -> None:
    if output is None:
        sys.stdout.write(payload)
        if not payload.endswith("\n"):
            sys.stdout.write("\n")
        return
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(payload, encoding="utf-8", newline="\n")


def compare_images(before_path: Path, after_path: Path, diff_output: Path | None) -> dict[str, object]:
    with Image.open(before_path) as before_source, Image.open(after_path) as after_source:
        before = before_source.convert("RGBA")
        after = after_source.convert("RGBA")
        if before.size != after.size:
            return {
                "match": False,
                "reason": "dimension_mismatch",
                "beforeSize": list(before.size),
                "afterSize": list(after.size),
            }
        difference = ImageChops.difference(before, after)
        # Pillow may otherwise use only the alpha band for RGBA bounding boxes.
        bbox = difference.getbbox(alpha_only=False)
        changed_pixels = 0
        changed_channels = 0
        max_channel_delta = 0
        if bbox is not None:
            for pixel in difference.getdata():
                nonzero = sum(channel != 0 for channel in pixel)
                if nonzero:
                    changed_pixels += 1
                    changed_channels += nonzero
                    max_channel_delta = max(max_channel_delta, *pixel)
            if diff_output is not None:
                diff_output.parent.mkdir(parents=True, exist_ok=True)
                difference.save(diff_output)
        return {
            "match": bbox is None,
            "reason": "identical" if bbox is None else "pixel_mismatch",
            "size": list(before.size),
            "differenceBoundingBox": list(bbox) if bbox is not None else None,
            "changedPixels": changed_pixels,
            "changedChannels": changed_channels,
            "maxChannelDelta": max_channel_delta,
        }


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    capture = subparsers.add_parser("capture-payload", help="generate a read-only NativeUnity capture payload")
    capture.add_argument("--prefab-asset-path", required=True)
    capture.add_argument("--image-output-path", required=True)
    capture.add_argument("--payload-output", type=Path)
    capture.add_argument("--width", type=int, required=True)
    capture.add_argument("--height", type=int, required=True)
    capture.add_argument("--camera-position", type=float, nargs=3, metavar=("X", "Y", "Z"), required=True)
    capture.add_argument("--orthographic-size", type=float, required=True)
    capture.add_argument("--near-clip", type=float, default=0.1)
    capture.add_argument("--far-clip", type=float, required=True)
    capture.add_argument("--background-rgba", type=float, nargs=4, metavar=("R", "G", "B", "A"), default=(0.0, 0.0, 0.0, 0.0))
    capture.add_argument("--state-override", action="append", default=[], metavar="RELATIVE/PATH=true|false")

    ownership = subparsers.add_parser("ownership-payload", help="generate a read-only NativeUnity texture ownership payload")
    ownership.add_argument("--texture-directory", action="append", required=True)
    ownership.add_argument("--owner-root", action="append", required=True)
    ownership.add_argument("--owner-extension", action="append", default=[])
    ownership.add_argument("--payload-output", type=Path)

    compare = subparsers.add_parser("compare", help="strictly compare all RGBA channels of two images")
    compare.add_argument("--before", type=Path, required=True)
    compare.add_argument("--after", type=Path, required=True)
    compare.add_argument("--diff-output", type=Path)
    compare.add_argument("--report-output", type=Path)
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        if args.command == "capture-payload":
            payload = render_capture_payload(
                prefab_asset_path=args.prefab_asset_path,
                image_output_path=args.image_output_path,
                width=args.width,
                height=args.height,
                camera_position=tuple(args.camera_position),
                orthographic_size=args.orthographic_size,
                near_clip=args.near_clip,
                far_clip=args.far_clip,
                background_rgba=tuple(args.background_rgba),
                state_overrides=[parse_state_override(value) for value in args.state_override],
            )
            write_payload(payload, args.payload_output)
            return 0
        if args.command == "ownership-payload":
            payload = render_texture_ownership_payload(
                texture_directories=args.texture_directory,
                owner_roots=args.owner_root,
                owner_extensions=args.owner_extension or DEFAULT_OWNER_EXTENSIONS,
            )
            write_payload(payload, args.payload_output)
            return 0

        report = compare_images(args.before, args.after, args.diff_output)
        serialized = json.dumps(report, ensure_ascii=False, indent=2) + "\n"
        if args.report_output is None:
            sys.stdout.write(serialized)
        else:
            args.report_output.parent.mkdir(parents=True, exist_ok=True)
            args.report_output.write_text(serialized, encoding="utf-8", newline="\n")
        return 0 if report["match"] else 1
    except (OSError, ValueError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
