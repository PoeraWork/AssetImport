# -*- coding: utf-8 -*-
"""Template for MMDD 3's MmddExtMorphFunc_*.py user extension API.

The generator appends _register(targets). No MMDD files are patched. Only the
selected item's renderers are touched; actor-owned caches avoid global scene
references and preserve independent instances of the same model.
"""
from __future__ import unicode_literals

_TARGETS = {}


def _resolve(actor, key):
    from UnityEngine import SkinnedMeshRenderer
    renderer_name, shape_name = _TARGETS[key]
    item = actor.objctrl.objectItem
    if item is None:
        raise ValueError('AssetImport morph: the selected item no longer exists')
    cache = getattr(actor, '_assetimport_morph_cache', None)
    if cache is None:
        cache = {}
        actor._assetimport_morph_cache = cache
    cached = cache.get(key)
    if cached is not None:
        old_item, matches = cached
        if old_item == item and all(renderer and renderer.sharedMesh == mesh for renderer, mesh, index in matches):
            return matches
    matches = []
    for renderer in item.GetComponentsInChildren[SkinnedMeshRenderer](True):
        mesh = renderer.sharedMesh
        if renderer.name != renderer_name or mesh is None:
            continue
        for index in range(mesh.blendShapeCount):
            if mesh.GetBlendShapeName(index) == shape_name:
                matches.append((renderer, mesh, index))
    if not matches:
        raise ValueError('AssetImport morph: expected at least one %s / %s, found %d; '
                         'enable Import Blendshapes and disable Material per Renderer'
                         % (renderer_name, shape_name, len(matches)))
    # KK may split a large material mesh into several identically named chunks.
    cache[key] = (item, matches)
    return matches


def _make_functions(key):
    def read(actor):
        return [float(renderer.GetBlendShapeWeight(index)) for renderer, mesh, index in _resolve(actor, key)]

    def write(actor, value):
        from System import Single
        matches = _resolve(actor, key)
        # Playback supplies a scalar; MMDD reset supplies the original per-chunk
        # list returned by read(), preserving distinct initial values.
        values = value if isinstance(value, (tuple, list)) else [value] * len(matches)
        if len(values) != len(matches):
            raise ValueError('AssetImport morph: renderer chunk count changed; rebind the item')
        for (renderer, mesh, index), weight in zip(matches, values):
            renderer.SetBlendShapeWeight(index, Single(weight))

    return read, write


def _register(targets):
    _TARGETS.update(targets)
    for key in targets:
        read, write = _make_functions(key)
        globals()['get_assetimport_' + key] = read
        globals()['set_assetimport_' + key] = write
