#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Shared library: read a Unity `.prefab` (text serialization) into a normalised model.

This is the *disk ground truth* reader. It is deliberately independent of Unity's own
hierarchy snapshot so the two can be cross-checked, and it is deliberately generic: no
project path, node id, asset name or canvas size is hard-coded anywhere.

Design rules learned the hard way (do not relax them):
  * Escapes are decoded by hand. The old `unicode_escape` + latin-1 round-trip silently
    corrupted CJK and returned the escaped text, which made every node look like a
    different name. Here any malformed escape raises `UnityYamlError` instead.
  * `m_text` may contain real newlines inside a single-quoted YAML scalar, so scalars are
    read with a proper scanner instead of a line regex.
  * PrefabInstance roots have no `!u!224` block in the owning file: their RectTransform is
    a stripped object from the nested prefab. Those child fileIDs are reattached to the
    instance by matching `m_TransformParent`, so the tree stays complete.

Public API
----------
    doc = load_prefab(path)                 # -> PrefabDoc
    doc.root_name, doc.hierarchy_node_count
    doc.tree                                # ordered nested dict {name, kind, children}
    doc.drawable_count, doc.script_count
    doc.prefab_instances                    # [{...source_guid, source_asset_path...}]
    walk_preorder(doc.tree)                 # -> [node, ...] depth-first pre-order
    resolve_guid(r'Assets/.../x.prefab', guid)   # -> asset path or None

CLI: `python unity_prefab_yaml.py --prefab x.prefab [--search-root Assets] [--print-tree]`
"""
from __future__ import annotations

import argparse
import collections
import hashlib
import json
import os
import re
import sys

# --------------------------------------------------------------------------------------
# errors
# --------------------------------------------------------------------------------------


class UnityYamlError(RuntimeError):
    """Raised for anything we refuse to guess about. Never silently degraded."""


# managed types that put pixels on screen (used for draw-order analysis)
DRAWABLE_TYPES = ('Image', 'RawImage', 'TextMeshProUGUI', 'Text', 'SpriteRenderer')

_CLASS_ID_HINT = re.compile(r'm_EditorClassIdentifier:[ \t]*(.*)')


def classify_monobehaviour(body: str) -> str:
    """Name a `!u!114` block. UI components are MonoBehaviours too, so the script guid
    alone cannot tell an Image from a gameplay script — the managed class name can."""
    m = _CLASS_ID_HINT.search(body)
    ident = (m.group(1).strip() if m else '')
    if 'Image' in ident and 'Raw' not in ident:
        return 'Image'
    if 'RawImage' in ident:
        return 'RawImage'
    if 'TextMeshPro' in ident or 'TMP_Text' in ident:
        return 'TextMeshProUGUI'
    if '::Text' in ident:
        return 'Text'
    if 'SpriteRenderer' in ident:
        return 'SpriteRenderer'
    # fall back to field presence, text first: TMP also carries no m_Sprite
    if 'm_text:' in body or 'm_fontAsset:' in body:
        return 'TextMeshProUGUI'
    if 'm_Sprite:' in body:
        return 'Image'
    if 'm_Script:' in body:
        return 'MonoBehaviour(script)'
    return 'MonoBehaviour'


def read_overrides(body: str):
    """Read a PrefabInstance's `m_Modifications` into {propertyPath: {...}}.

    Entry shape (indentation varies between Unity versions, so it is not matched literally):
        - target: {fileID: ..., guid: ..., type: 3}
          propertyPath: m_Name
          value: '[NavItem_Album]'
          objectReference: {fileID: 0}
    """
    out = {}
    pattern = re.compile(r'(?ms)^[ \t]*-[ \t]*target:[^\n]*\n[ \t]*propertyPath:[ \t]*')
    for m in pattern.finditer(body):
        prop, after = read_scalar(body, m.end())
        vm = re.search(r'(?m)^[ \t]*value:[ \t]*', body[after:after + 400])
        if not vm:
            continue
        value, after_val = read_scalar(body, after + vm.end())
        rm = re.search(r'objectReference: \{fileID: (-?\d+)(?:, guid: ([0-9a-fA-F]+))?',
                       body[after_val:after_val + 240])
        out[prop] = {
            'value': value,
            'objectFileId': rm.group(1) if rm else None,
            'objectGuid': rm.group(2).lower() if rm and rm.group(2) else None,
        }
    return out


# --------------------------------------------------------------------------------------
# scalar / escape decoding
# --------------------------------------------------------------------------------------

_SIMPLE_ESCAPES = {'n': '\n', 'r': '\r', 't': '\t', '\\': '\\', '"': '"', "'": "'", '0': '\0'}


def _decode_backslash(body: str, strict: bool = True) -> str:
    """Decode a Unity/YAML escape body.

    `strict=True` (double-quoted scalars, where YAML *does* process escapes): any unknown
    escape raises. `strict=False` (single-quoted scalars, where YAML treats backslashes
    literally): only the unambiguous `\\uXXXX` / `\\UXXXXXXXX` / `\\xXX` forms are decoded —
    which is what Unity emits for non-ASCII names — anything else is left untouched.
    """
    out = []
    i = 0
    n = len(body)
    while i < n:
        ch = body[i]
        if ch != '\\':
            out.append(ch)
            i += 1
            continue
        if i + 1 >= n:
            if strict:
                raise UnityYamlError('trailing backslash in escaped scalar: %r' % body)
            out.append(ch)
            break
        nxt = body[i + 1]
        if nxt in ('u', 'U'):
            width = 4 if nxt == 'u' else 8
            hexs = body[i + 2:i + 2 + width]
            if len(hexs) != width or not re.fullmatch(r'[0-9a-fA-F]{%d}' % width, hexs):
                raise UnityYamlError('bad \\%s escape in %r' % (nxt, body))
            code = int(hexs, 16)
            if code > 0x10FFFF or 0xD800 <= code <= 0xDFFF:
                raise UnityYamlError('out-of-range \\%s escape %r in %r' % (nxt, hexs, body))
            out.append(chr(code))
            i += 2 + width
            continue
        if nxt == 'x':
            hexs = body[i + 2:i + 4]
            if len(hexs) != 2 or not re.fullmatch(r'[0-9a-fA-F]{2}', hexs):
                raise UnityYamlError('bad \\x escape in %r' % body)
            out.append(chr(int(hexs, 16)))
            i += 4
            continue
        if nxt in _SIMPLE_ESCAPES:
            out.append(_SIMPLE_ESCAPES[nxt])
            i += 2
            continue
        if strict:
            raise UnityYamlError('unknown escape "\\%s" in %r' % (nxt, body))
        out.append(ch)
        i += 1
    return ''.join(out)


def _finalize_single_quoted(body: str) -> str:
    """Body of a single-quoted scalar (already de-doubled). Unity writes non-ASCII names as
    \\uXXXX even inside single quotes, so decode only those unambiguous forms."""
    if re.search(r'\\u|\\U|\\x', body):
        return _decode_backslash(body, strict=False)
    return body


def unquote_scalar(token: str) -> str:
    """Turn one raw YAML scalar *token* (already cut out, quotes included) into text."""
    if len(token) >= 2 and token[0] == "'" and token[-1] == "'":
        return _finalize_single_quoted(token[1:-1].replace("''", "'"))
    if len(token) >= 2 and token[0] == '"' and token[-1] == '"':
        return _decode_backslash(token[1:-1])
    if '\\' in token:
        return _decode_backslash(token)
    return token


def read_scalar(text: str, i: int):
    """Read a YAML scalar starting at `i`; returns (value, index_after_scalar).

    Handles bare, single-quoted (inline or multi-line) and double-quoted scalars, which is
    what `m_Name`, `m_text` and `propertyPath/value` need.
    """
    while i < len(text) and text[i] in ' \t':
        i += 1
    if i >= len(text):
        raise UnityYamlError('scalar expected at end of document')
    quote = text[i]
    if quote == "'":
        i += 1
        buf = []
        while i < len(text):
            if text[i] == "'":
                if i + 1 < len(text) and text[i + 1] == "'":
                    buf.append("'")
                    i += 2
                    continue
                return _finalize_single_quoted(''.join(buf)), i + 1
            buf.append(text[i])
            i += 1
        raise UnityYamlError("unterminated single-quoted scalar: %r" % text[max(0, i - 60):i + 1])
    if quote == '"':
        i += 1
        buf = []
        while i < len(text):
            if text[i] == '\\':
                buf.append(text[i:i + 2])
                i += 2
                continue
            if text[i] == '"':
                return _decode_backslash(''.join(buf)), i + 1
            buf.append(text[i])
            i += 1
        raise UnityYamlError("unterminated double-quoted scalar: %r" % text[max(0, i - 60):i + 1])
    end = text.find('\n', i)
    end = len(text) if end < 0 else end
    return text[i:end].rstrip('\r'), end


def scalar_after(text: str, key: str, start: int = 0, end: int | None = None):
    """Value of `key:` inside text[start:end]; None when absent. Raises when malformed."""
    end = len(text) if end is None else end
    m = re.search(r'(?m)^[ \t-]*%s:[ \t]*' % re.escape(key), text[start:end])
    if not m:
        return None
    at = start + m.end()
    val, _ = read_scalar(text, at)
    return val


def float_after(text: str, key: str, start: int = 0, end: int | None = None):
    v = scalar_after(text, key, start, end)
    if v is None or v == '':
        return None
    try:
        return float(v)
    except ValueError:
        return None


def vec2_after(text: str, key: str, start: int = 0, end: int | None = None):
    """Read `key: {x: a, y: b}` -> (a, b) or None."""
    end = len(text) if end is None else end
    m = re.search(r'(?m)^[ \t-]*%s:[ \t]*\{([^}]*)\}' % re.escape(key), text[start:end])
    if not m:
        return None
    body = m.group(1)
    x = re.search(r'x:[ \t]*([-0-9.eE+]+)', body)
    y = re.search(r'y:[ \t]*([-0-9.eE+]+)', body)
    if not x or not y:
        return None
    return (float(x.group(1)), float(y.group(1)))


def fileid_after(text: str, key: str, start: int = 0, end: int | None = None):
    """Read `key: {fileID: 123}` -> '123' (string) or None."""
    end = len(text) if end is None else end
    m = re.search(r'(?m)^[ \t-]*%s:[ \t]*\{fileID:[ \t]*(-?\d+)' % re.escape(key), text[start:end])
    return m.group(1) if m else None


def guid_after(text: str, key: str, start: int = 0, end: int | None = None):
    """Read the guid inside `key: {fileID: 1, guid: abc...}` -> lower-case guid or None."""
    end = len(text) if end is None else end
    m = re.search(r'(?m)^[ \t-]*%s:[ \t]*\{fileID: -?\d+, guid: ([0-9a-fA-F]{32})'
                  % re.escape(key), text[start:end])
    return m.group(1).lower() if m else None


# --------------------------------------------------------------------------------------
# document model
# --------------------------------------------------------------------------------------

BLOCK_RE = re.compile(r'^--- !u!(\d+) &(-?\d+)(?P<stripped>[ \t]+stripped)?[ \t]*$', re.M)

# component class -> friendly name (only what this toolchain actually emits)
CLASS_NAMES = {
    '1': 'GameObject',
    '4': 'Transform',
    '20': 'Camera',
    '23': 'MeshRenderer',
    '33': 'MeshFilter',
    '95': 'Animator',
    '108': 'Light',
    '114': 'MonoBehaviour',
    '115': 'MonoScript',
    '120': 'LineRenderer',
    '128': 'Font',
    '199': 'ParticleSystemRenderer',
    '212': 'SpriteRenderer',
    '222': 'CanvasRenderer',
    '223': 'Canvas',
    '224': 'RectTransform',
    '225': 'CanvasGroup',
    '1001': 'PrefabInstance',
    '1660057539': 'SceneRoots',
}


class PrefabNode:
    __slots__ = ('name', 'kind', 'file_id', 'rect_file_id', 'parent', 'children', 'depth', 'extra')

    def __init__(self, name, kind, file_id=None):
        self.name = name
        self.kind = kind          # 'gameObject' | 'prefabInstance'
        self.file_id = file_id
        self.rect_file_id = None
        self.parent = None
        self.children = []
        self.depth = 0
        self.extra = {}

    def path(self):
        parts = []
        cur = self
        while cur is not None:
            parts.append(cur.name)
            cur = cur.parent
        return '/'.join(reversed(parts))

    def as_dict(self):
        d = {'name': self.name, 'kind': self.kind, 'path': self.path()}
        if self.extra:
            d.update(self.extra)
        d['children'] = [c.as_dict() for c in self.children]
        return d


def walk_preorder(node):
    """Depth-first pre-order; in Unity that is exactly the draw order."""
    yield node
    for c in node.children:
        yield from walk_preorder(c)


class PrefabDoc:
    def __init__(self, path, text, sha256, blocks):
        self.path = path
        self.text = text
        self.sha256 = sha256
        self.blocks = blocks                 # [(class_id, file_id, body, stripped)]
        self.block_counts = collections.Counter(c for c, *_ in blocks)

        # ---- GameObject names -------------------------------------------------------
        self.game_objects = {}
        for cls, fid, body, stripped in blocks:
            if cls == '1' and not stripped:
                name = scalar_after(body, 'm_Name')
                if name is None:
                    raise UnityYamlError('GameObject &%s has no m_Name' % fid)
                self.game_objects[fid] = name

        # ---- RectTransforms ---------------------------------------------------------
        # `stripped` transforms belong to nested prefab instances: they carry no
        # m_GameObject but an m_PrefabInstance back-reference to their owner.
        self.rects = {}          # transform file_id -> dict
        self.stripped_rects = {}  # transform file_id -> {prefab_instance, source_file_id}
        for cls, fid, body, stripped in blocks:
            if cls not in ('224', '4'):
                continue
            if stripped:
                self.stripped_rects[fid] = {
                    'prefab_instance': fileid_after(body, 'm_PrefabInstance'),
                    'source_file_id': fileid_after(body, 'm_CorrespondingSourceObject'),
                    'source_guid': guid_after(body, 'm_CorrespondingSourceObject'),
                }
                continue
            seg = re.search(r'(?m)^  m_Children:[ \t]*\n((?:  - \{fileID: -?\d+\}\n?)*)', body)
            kids = re.findall(r'fileID: (-?\d+)', seg.group(1)) if seg else []
            self.rects[fid] = {
                'class': CLASS_NAMES.get(cls, cls),
                'stripped': False,
                'game_object': fileid_after(body, 'm_GameObject'),
                'father': fileid_after(body, 'm_Father'),
                'children': kids,
                'anchoredPosition': vec2_after(body, 'm_AnchoredPosition'),
                'sizeDelta': vec2_after(body, 'm_SizeDelta'),
                'anchorMin': vec2_after(body, 'm_AnchorMin'),
                'anchorMax': vec2_after(body, 'm_AnchorMax'),
                'pivot': vec2_after(body, 'm_Pivot'),
                'localScale': vec2_after(body, 'm_LocalScale'),
            }

        # ---- components per GameObject ---------------------------------------------
        self.components = collections.defaultdict(list)
        self.drawable = set()
        self.scripted = set()
        self.component_kinds = collections.Counter()
        for cls, fid, body, stripped in blocks:
            if cls in ('1', '224', '4', '1001') or stripped:
                continue
            owner = fileid_after(body, 'm_GameObject')
            if owner is None or owner == '0':
                continue
            name = CLASS_NAMES.get(cls, cls)
            if cls == '114':
                name = classify_monobehaviour(body)
                if name == 'MonoBehaviour(script)':
                    self.scripted.add(owner)
                elif name in DRAWABLE_TYPES:
                    self.drawable.add(owner)
            self.components[owner].append(name)
            self.component_kinds[name] += 1

        # ---- PrefabInstances --------------------------------------------------------
        self.prefab_instances = []
        inst_by_parent = collections.defaultdict(list)
        for cls, fid, body, stripped in blocks:
            if cls != '1001' or stripped:
                continue
            src = guid_after(body, 'm_SourcePrefab')
            if not src:
                # older serialization keeps the guid on the target lines only
                m = re.search(r'guid: ([0-9a-fA-F]{32})', body)
                src = m.group(1).lower() if m else None
            parent = fileid_after(body, 'm_TransformParent')
            root_name = None
            name_m = re.search(r'(?ms)^[ \t]*-[ \t]*target:[^\n]*\n'
                               r'[ \t]*propertyPath:[ \t]*m_Name[ \t]*\n'
                               r'[ \t]*value:[ \t]*', body)
            if name_m:
                root_name, _ = read_scalar(body, name_m.end())
            rec = {
                'file_id': fid,
                'source_guid': src,
                'transform_parent': parent,
                'root_name': root_name,
                'root_transform': None,
                'override_count': len(re.findall(r'(?m)^[ \t]*propertyPath:', body)),
                'overrides': read_overrides(body),
            }
            self.prefab_instances.append(rec)
            if parent:
                inst_by_parent[parent].append(rec)

        # ---- build the tree --------------------------------------------------------
        node_of_rect = {}
        self.node_by_rect = node_of_rect
        for tfile, rect in self.rects.items():
            go = rect['game_object']
            if go is None or go not in self.game_objects:
                continue
            node = PrefabNode(self.game_objects[go], 'gameObject', go)
            node.rect_file_id = tfile
            node_of_rect[tfile] = node

        # Each instance root is a `stripped` RectTransform pointing back at its owner via
        # m_PrefabInstance. If a Unity version omits it, fall back to pairing the parent's
        # unresolved child references with `m_TransformParent`.
        stripped_by_instance = {}
        for tfile, srec in self.stripped_rects.items():
            if srec['prefab_instance']:
                stripped_by_instance.setdefault(srec['prefab_instance'], []).append(tfile)
        unresolved = collections.defaultdict(list)
        for tfile, rect in self.rects.items():
            for c in rect['children']:
                if c not in self.rects and c not in self.stripped_rects:
                    unresolved[tfile].append(c)

        for rec in self.prefab_instances:
            parent_t = rec['transform_parent']
            tfile = None
            cand = stripped_by_instance.get(rec['file_id'])
            if cand:
                tfile = cand[0]
            elif parent_t and unresolved.get(parent_t):
                tfile = unresolved[parent_t].pop(0)
            if tfile is None:
                # keep the node in the model rather than silently dropping it
                node = PrefabNode(rec['root_name'] or '(PrefabInstance)',
                                  'prefabInstance', rec['file_id'])
                node.extra['sourceGuid'] = rec['source_guid']
                node.extra['overrideCount'] = rec['override_count']
                node.extra['missingRootTransform'] = True
                rec['root_transform'] = None
                self._orphan_instances = getattr(self, '_orphan_instances', [])
                self._orphan_instances.append(node)
                continue
            rec['root_transform'] = tfile
            node = PrefabNode(rec['root_name'] or '(PrefabInstance)',
                              'prefabInstance', rec['file_id'])
            node.rect_file_id = tfile
            node.extra['sourceGuid'] = rec['source_guid']
            node.extra['overrideCount'] = rec['override_count']
            node_of_rect[tfile] = node
            self.rects[tfile] = {'class': 'RectTransform(stripped)', 'stripped': True,
                                 'game_object': None, 'father': parent_t, 'children': [],
                                 'anchoredPosition': None, 'sizeDelta': None,
                                 'anchorMin': None, 'anchorMax': None, 'pivot': None,
                                 'localScale': None}

        roots = []
        for node in node_of_rect.values():
            rect = self.rects.get(node.rect_file_id) or {}
            father = rect.get('father')
            if father and father != '0' and father in node_of_rect:
                parent_node = node_of_rect[father]
                seen = set()
                probe = parent_node
                while probe is not None and id(probe) not in seen:
                    seen.add(id(probe))
                    if probe is node:
                        raise UnityYamlError('cycle detected at %s' % node.name)
                    probe = probe.parent
                node.parent = parent_node
                parent_node.children.append(node)
            else:
                roots.append(node)

        # reorder children by m_Children order, then set depth
        for tfile, rect in self.rects.items():
            parent_node = node_of_rect.get(tfile)
            if parent_node is None:
                continue
            order = {c: i for i, c in enumerate(rect['children'])}
            parent_node.children.sort(key=lambda c: order.get(c.rect_file_id, 1 << 30))
        self.roots = roots
        if len(roots) != 1:
            raise UnityYamlError('expected exactly 1 root, found %d: %s'
                                 % (len(roots), [r.name for r in roots]))
        self.tree = roots[0]

        stack = [(self.tree, 0)]
        while stack:
            node, d = stack.pop()
            node.depth = d
            for c in node.children:
                stack.append((c, d + 1))

        self._resolve_instance_assets(None)

    # -- helpers -------------------------------------------------------------------
    def _resolve_instance_assets(self, search_root):
        self.instance_asset_paths = {}
        if search_root and os.path.isdir(search_root):
            self.instance_asset_paths = build_guid_index(search_root)
        for rec in self.prefab_instances:
            rec['source_asset_path'] = self.instance_asset_paths.get(rec['source_guid'] or '')

    def resolve_instance_assets(self, search_root):
        self._resolve_instance_assets(search_root)
        return self.instance_asset_paths

    @property
    def root_name(self):
        return self.tree.name

    @property
    def hierarchy_node_count(self):
        return sum(1 for _ in walk_preorder(self.tree))

    @property
    def drawable_count(self):
        return len(self.drawable)

    @property
    def script_count(self):
        return len(self.scripted)

    def rect_of(self, node):
        """RectTransform record of a node (dict; geometry keys may be None)."""
        return self.rects.get(node.rect_file_id) or {}

    def components_of(self, node):
        return list(self.components.get(node.file_id, [])) if node.file_id else []

    def summary(self):
        return {
            'prefabPath': self.path,
            'sha256': self.sha256,
            'rootName': self.root_name,
            'hierarchyNodeCount': self.hierarchy_node_count,
            'gameObjectCount': len(self.game_objects),
            'prefabInstanceCount': len(self.prefab_instances),
            'drawableObjectCount': self.drawable_count,
            'scriptObjectCount': self.script_count,
            'componentKinds': dict(sorted(self.component_kinds.items())),
            'blockCounts': dict(sorted(self.block_counts.items())),
        }


# --------------------------------------------------------------------------------------
# guid index (for resolving prefab / texture references)
# --------------------------------------------------------------------------------------

_GUID_RE = re.compile(r'(?m)^guid: ([0-9a-fA-F]{32})\s*$')


def build_guid_index(root, extensions=('.prefab', '.png', '.jpg', '.jpeg', '.tga', '.psd',
                                       '.asset', '.spriteatlas', '.mat', '.controller')):
    """guid -> asset path (project-relative with forward slashes), scanning `*.meta`."""
    index = {}
    root = os.path.abspath(root)
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in ('Library', 'Temp', 'obj', '.git')]
        for fn in filenames:
            if not fn.endswith('.meta'):
                continue
            target = fn[:-5]
            if extensions and not target.lower().endswith(extensions):
                continue
            meta = os.path.join(dirpath, fn)
            try:
                with open(meta, 'r', encoding='utf-8', errors='replace') as fh:
                    head = fh.read(4096)
            except OSError:
                continue
            m = _GUID_RE.search(head)
            if not m:
                continue
            asset = os.path.join(dirpath, target)
            index[m.group(1).lower()] = asset.replace('\\', '/')
    return index


def resolve_guid(asset_root, guid, extensions=None):
    """Convenience one-shot lookup (prefer building an index once and reusing it)."""
    if not guid:
        return None
    return build_guid_index(asset_root, extensions or ('.prefab',)).get(guid.lower())


# --------------------------------------------------------------------------------------
# loader
# --------------------------------------------------------------------------------------


def load_prefab(path, search_root=None):
    with open(path, 'rb') as fh:
        raw = fh.read()
    sha = hashlib.sha256(raw).hexdigest()
    try:
        text = raw.decode('utf-8-sig')
    except UnicodeDecodeError as exc:
        raise UnityYamlError('%s is not UTF-8 text serialization (%s). '
                             'Enable Force Text serialization in Unity.' % (path, exc))
    if '%YAML' not in text[:64]:
        raise UnityYamlError('%s is not a YAML Unity asset (binary serialization?)' % path)
    blocks = []
    marks = list(BLOCK_RE.finditer(text))
    if not marks:
        raise UnityYamlError('no `--- !u!` blocks found in ' + path)
    for idx, m in enumerate(marks):
        stop = marks[idx + 1].start() if idx + 1 < len(marks) else len(text)
        body = text[m.end():stop]
        blocks.append((m.group(1), m.group(2), body, bool(m.group('stripped'))))
    doc = PrefabDoc(path, text, sha, blocks)
    if search_root:
        doc.resolve_instance_assets(search_root)
    return doc


# --------------------------------------------------------------------------------------
# CLI
# --------------------------------------------------------------------------------------


def main():
    ap = argparse.ArgumentParser(description='Read a Unity prefab into a normalised model.')
    ap.add_argument('--prefab', required=True)
    ap.add_argument('--search-root', default=None,
                    help='project root used to resolve PrefabInstance source guids (e.g. Assets)')
    ap.add_argument('--print-tree', action='store_true')
    ap.add_argument('--max-depth', type=int, default=6)
    ap.add_argument('--json-out')
    args = ap.parse_args()

    doc = load_prefab(args.prefab, args.search_root)
    print(json.dumps(doc.summary(), ensure_ascii=False, indent=1))
    if doc.prefab_instances:
        print('prefabInstances:')
        for rec in doc.prefab_instances:
            print('  &%s  name=%s  guid=%s  parent=%s  asset=%s'
                  % (rec['file_id'], rec['root_name'], rec['source_guid'],
                     rec['transform_parent'], rec['source_asset_path']))
    if args.print_tree:
        def dump(node, d):
            if d > args.max_depth:
                return
            tag = '[PI] ' if node.kind == 'prefabInstance' else ''
            print('  ' * d + tag + node.name)
            for c in node.children:
                dump(c, d + 1)
        dump(doc.tree, 0)
    if args.json_out:
        payload = dict(doc.summary())
        payload['tree'] = doc.tree.as_dict()
        payload['prefabInstances'] = doc.prefab_instances
        with open(args.json_out, 'w', encoding='utf-8') as fh:
            json.dump(payload, fh, ensure_ascii=False, indent=1)
        print('wrote', args.json_out)
    return 0


if __name__ == '__main__':
    sys.exit(main())
