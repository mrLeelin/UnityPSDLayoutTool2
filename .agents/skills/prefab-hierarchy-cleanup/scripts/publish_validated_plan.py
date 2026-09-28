#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Validate a draft plan once, then publish the exact same bytes.

The linter and simulator intentionally run as separate Python modules but in one
process. This preserves their short-circuit order while avoiding two interpreter
startup costs. Publishing is a byte-for-byte copy; the destination is never
regenerated from parsed JSON or Markdown.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from validate_plan_locally import lint  # noqa: E402
from simulate_and_verify_plan import load_snapshot, simulate, verify_contracts  # noqa: E402


def digest(path):
    h = hashlib.sha256()
    with open(path, "rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--plan", required=True)
    parser.add_argument("--snapshot", required=True)
    parser.add_argument("--destination", required=True)
    args = parser.parse_args(argv)

    with open(args.plan, "r", encoding="utf-8") as stream:
        plan = json.load(stream)
    _, nodes = load_snapshot(args.snapshot)

    errors, warnings = lint(plan, nodes)
    if errors:
        for error in errors:
            print("ERROR", error, file=sys.stderr)
        return 2

    simulated = simulate(plan, nodes)
    simulation_errors, _ = verify_contracts(plan, simulated)
    if simulation_errors:
        for error in simulation_errors:
            print("ERROR", error, file=sys.stderr)
        return 2

    destination = os.path.abspath(args.destination)
    os.makedirs(os.path.dirname(destination), exist_ok=True)
    shutil.copyfile(args.plan, destination)
    source_hash = digest(args.plan)
    destination_hash = digest(destination)
    if source_hash != destination_hash:
        print("ERROR published plan hash differs from draft", file=sys.stderr)
        return 3

    print("VALIDATED_AND_PUBLISHED", destination)
    print("SHA256", destination_hash)
    if warnings:
        print("WARNINGS", len(warnings))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
