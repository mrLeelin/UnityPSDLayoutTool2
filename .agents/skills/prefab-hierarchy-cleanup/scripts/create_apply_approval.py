#!/usr/bin/env python3
"""Create a platform-canonical PSD hierarchy approval sentinel.

The Unity watcher compares planPath as a string. Always derive it from the
actual plan file so Windows uses ``\\`` and macOS/Linux use ``/``.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from datetime import datetime, timezone
from pathlib import Path
import tempfile

APPROVAL_PHRASE = "批准当前 JSON 计划并执行 apply"


def canonical_path(path: str) -> str:
    """Return an existing absolute path in the host platform's native form."""
    resolved = Path(path).expanduser().resolve(strict=True)
    return str(resolved)


def sha256_bytes(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def create_approval(plan_path: str, apply_path: str, approval_text: str = APPROVAL_PHRASE) -> dict:
    if approval_text != APPROVAL_PHRASE:
        raise ValueError("approvalText 必须精确等于：" + APPROVAL_PHRASE)

    plan_file = Path(plan_path).expanduser().resolve(strict=True)
    apply_file = Path(apply_path).expanduser()
    result_file = Path(str(apply_file) + "-result.json")
    if apply_file.exists() or result_file.exists():
        raise FileExistsError("当前审批会话已有 .apply 或 .apply-result.json，必须使用新的会话文件。")

    with plan_file.open("r", encoding="utf-8-sig") as stream:
        plan = json.load(stream)
    if plan.get("version") != 2:
        raise ValueError("计划 version 必须为 2。")
    target = plan.get("targetPrefabAssetPath") or plan.get("prefabAssetPath")
    for field in ("snapshotFingerprint", "reviewVersion", "targetPrefabAssetPath"):
        if not plan.get(field):
            raise ValueError("计划缺少 " + field + "。")
    if target != plan["targetPrefabAssetPath"]:
        raise ValueError("targetPrefabAssetPath 与 prefabAssetPath 不一致。")

    approval = {
        "version": 2,
        "approvalText": approval_text,
        "planPath": canonical_path(str(plan_file)),
        "planSha256": sha256_bytes(plan_file),
        "snapshotFingerprint": plan["snapshotFingerprint"],
        "reviewVersion": plan["reviewVersion"],
        "targetPrefabPath": target,
        "approvedAtUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
    }

    apply_file.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary_name = tempfile.mkstemp(
        prefix=apply_file.name + ".tmp-", dir=str(apply_file.parent)
    )
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="") as stream:
            json.dump(approval, stream, ensure_ascii=False, indent=2)
            stream.write("\n")
        os.replace(temporary_name, apply_file)
    finally:
        if os.path.exists(temporary_name):
            os.unlink(temporary_name)
    return approval


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--plan", required=True)
    parser.add_argument("--apply", required=True)
    args = parser.parse_args()
    approval = create_approval(args.plan, args.apply)
    print(json.dumps(approval, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
