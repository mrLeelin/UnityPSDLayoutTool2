#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Snapshot freshness gate.

`snapshotFingerprint` is the Prefab file SHA-256. A snapshot is only usable while that
hash still matches, because every successful apply rewrites the Prefab (and every Unity
re-save changes the bytes).

Two modes:

  check   (default) assert one snapshot is fresh for one prefab  -> exit 0 / 1
  survey  list every snapshot for a prefab, newest first, and mark which (if any) is fresh

`survey` exists because of a real trap: after a successful two-stage apply the newest
snapshot on disk can be the *intermediate* stage (post-grouping, pre-extraction) and its
fingerprint will not match the final Prefab. Never plan against that file.

Usage
-----
  python check_snapshot_freshness.py --prefab <p.prefab> --snapshot <s.json>
  python check_snapshot_freshness.py --prefab <p.prefab> --survey [--snapshot-dir Library/PSDLayoutTool2/HierarchySnapshots]
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
from datetime import datetime

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

DEFAULT_SNAPSHOT_DIR = os.path.join('Library', 'PSDLayoutTool2', 'HierarchySnapshots')


def file_sha256(path: str) -> str:
    digest = hashlib.sha256()
    with open(path, 'rb') as fh:
        for chunk in iter(lambda: fh.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def read_snapshot_meta(path: str):
    """-> {fingerprint, prefabAssetPath, nodeCount, schemaVersion} (nodeCount may be None)."""
    with open(path, 'r', encoding='utf-8-sig', errors='replace') as fh:
        data = json.load(fh)
    nodes = data.get('nodes')
    return {
        'fingerprint': (data.get('fingerprint') or '').lower(),
        'prefabAssetPath': data.get('prefabAssetPath'),
        'nodeCount': len(nodes) if isinstance(nodes, list) else None,
        'schemaVersion': data.get('schemaVersion'),
    }


def normalise_path(value: str) -> str:
    """Compare `Assets/.../x.prefab` and `E:/proj/Assets/.../x.prefab` as the same asset."""
    if not value:
        return ''
    return value.replace('\\', '/').lower().lstrip('./')


def same_asset(snapshot_asset_path: str, prefab_path: str) -> bool:
    a = normalise_path(snapshot_asset_path)
    b = normalise_path(prefab_path)
    if not a or not b:
        return False
    return a == b or a.endswith('/' + b) or b.endswith('/' + a)


def snapshot_dir_near_prefab(prefab_path: str, snapshot_dir=None) -> str:
    if snapshot_dir:
        return snapshot_dir
    # walk up from the prefab until a Library/PSDLayoutTool2/HierarchySnapshots exists
    cur = os.path.abspath(os.path.dirname(prefab_path))
    for _ in range(8):
        cand = os.path.join(cur, DEFAULT_SNAPSHOT_DIR)
        if os.path.isdir(cand):
            return cand
        parent = os.path.dirname(cur)
        if parent == cur:
            break
        cur = parent
    return DEFAULT_SNAPSHOT_DIR


def survey(prefab_path: str, snapshot_dir: str, limit: int = 8):
    sha = file_sha256(prefab_path)
    if not os.path.isdir(snapshot_dir):
        print('snapshot directory not found: %s' % snapshot_dir)
        print('prefab sha256: %s' % sha)
        return 1
    entries = []
    for fn in os.listdir(snapshot_dir):
        if not fn.endswith('.json'):
            continue
        full = os.path.join(snapshot_dir, fn)
        try:
            meta = read_snapshot_meta(full)
        except Exception as exc:
            entries.append((os.path.getmtime(full), fn, None, str(exc)))
            continue
        entries.append((os.path.getmtime(full), fn, meta, None))
    entries.sort(reverse=True)

    print('prefab            : %s' % prefab_path)
    print('prefab sha256     : %s' % sha)
    print('snapshot dir      : %s' % snapshot_dir)
    print('snapshots on disk : %d' % len(entries))
    print()
    print('%-5s %-19s %-7s %-6s %s' % ('fresh', 'mtime', 'nodes', 'match', 'file'))
    fresh = []
    for mtime, fn, meta, err in entries[:limit]:
        stamp = datetime.fromtimestamp(mtime).strftime('%Y-%m-%d %H:%M:%S')
        if err or meta is None:
            print('%-5s %-19s %-7s %-6s %s  (unreadable: %s)' % ('?', stamp, '?', '?', fn, err))
            continue
        match = (meta['fingerprint'] == sha)
        if match:
            fresh.append(fn)
        print('%-5s %-19s %-7s %-6s %s' % ('YES' if match else 'no', stamp,
                                          meta['nodeCount'], 'yes' if match else 'no', fn))
    print()
    if fresh:
        print('FRESH snapshot(s) for the current Prefab: %s' % ', '.join(fresh))
        return 0
    print('NO fresh snapshot: the newest snapshot is stale for this Prefab.')
    newest = entries[0][2] if entries and entries[0][2] else {}
    print('Latest snapshot fingerprint : %s' % newest.get('fingerprint', '?'))
    print('Current Prefab sha256       : %s' % sha)
    print('-> Re-capture the hierarchy snapshot inside Unity before planning anything.')
    return 1


def check(prefab_path: str, snapshot_path: str):
    sha = file_sha256(prefab_path)
    meta = read_snapshot_meta(snapshot_path)
    match = (meta['fingerprint'] == sha)
    asset_ok = same_asset(meta['prefabAssetPath'] or '', prefab_path)
    print('prefab        : %s' % prefab_path)
    print('prefab sha256 : %s' % sha)
    print('snapshot      : %s' % snapshot_path)
    print('fingerprint   : %s' % meta['fingerprint'])
    print('nodes         : %s' % meta['nodeCount'])
    print('asset match   : %s' % ('yes' if asset_ok else
                                  'CHECK: snapshot describes %r' % meta['prefabAssetPath']))
    if not asset_ok:
        print('RESULT: STALE - snapshot belongs to a different asset.')
        return 1
    if match:
        print('RESULT: FRESH - safe to plan against this snapshot.')
        return 0
    print('RESULT: STALE - fingerprint does not match the Prefab bytes.')
    print('        Planning against it would bind node ids to a hierarchy that no longer exists.')
    return 1


def main():
    ap = argparse.ArgumentParser(description='Verify a hierarchy snapshot is fresh for a Prefab.')
    ap.add_argument('--prefab', required=True)
    ap.add_argument('--snapshot')
    ap.add_argument('--survey', action='store_true',
                    help='list all snapshots for this prefab and mark the fresh one(s)')
    ap.add_argument('--snapshot-dir')
    ap.add_argument('--limit', type=int, default=8)
    args = ap.parse_args()

    if args.survey or not args.snapshot:
        return survey(args.prefab, snapshot_dir_near_prefab(args.prefab, args.snapshot_dir),
                      args.limit)
    return check(args.prefab, args.snapshot)


if __name__ == '__main__':
    sys.exit(main())
