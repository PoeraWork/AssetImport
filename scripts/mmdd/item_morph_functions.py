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
    _install_runtime()


def _install_runtime():
    # MMDD normally scans extensions only at startup. Refresh on a missing
    # AssetImport method, once per changed extension inventory, so importing a
    # second model during playback does not leave its FUNC morphs unbound.
    import mmddmorphfuncext as library
    if not getattr(library.getMethod, '_assetimport_lazy', False):
        import os
        original = library.getMethod
        seen = [None]

        def get_method(actor, name, install=True):
            ours = name and name.startswith(('get_assetimport_', 'set_assetimport_'))
            if ours and not hasattr(actor, name) and name not in library._userFuncs:
                folder = os.path.dirname(os.path.abspath(library.__file__))
                stamp = tuple(sorted((f, os.path.getmtime(os.path.join(folder, f)),
                                      os.path.getsize(os.path.join(folder, f)))
                                     for f in os.listdir(folder)
                                     if f.startswith('MmddExtMorphFunc_AssetImport_Pmx_') and f.endswith('.py')))
                if stamp != seen[0]:
                    seen[0] = stamp
                    library.loadUserFuncFiles()
            return original(actor, name, install)

        get_method._assetimport_lazy = True
        library.getMethod = get_method

    import vmdlib
    original_solver = vmdlib.VmdLib.mmdIKSolver
    if not getattr(original_solver, '_assetimport_limits', False):
        def solve(bone, target, chain, iterations, step, limits, debug='ik_foot_r'):
            if bone.name.startswith('assetimport_pmx_') and any(limit is not None for limit in limits) and not _ordinary_knee(limits):
                return _solve_pmx_ik(bone, target, chain, iterations, step, limits)
            return original_solver(bone, target, chain, iterations, step, limits, debug)
        solve._assetimport_limits = True
        vmdlib.VmdLib.mmdIKSolver = staticmethod(solve)


def _ordinary_knee(limits):
    if len(limits) != 2 or limits[0] is None or limits[1] is not None:
        return False
    expected = ((0.5, 180.0), (0.0, 0.0), (0.0, 0.0))
    return all(abs(limits[0][axis][side] - expected[axis][side]) < 0.001
               for axis in range(3) for side in range(2))


def _clamp_angle(angle, bounds):
    # Choose the equivalent Euler angle nearest the permitted interval. This
    # avoids a discontinuity at 0/360 and also accepts ranges crossing 180.
    low, high = bounds
    angle = (angle + 180.0) % 360.0 - 180.0
    candidates = (angle - 360.0, angle, angle + 360.0)
    best = min(candidates, key=lambda v: abs(v - min(high, max(low, v))))
    return min(high, max(low, best))


def _solve_pmx_ik(bone, target, chain, iterations, step, limits):
    # Scoped CCD for non-knee PMX constraints. MMDD's stock MMD solver treats
    # every limited link as a positive-X knee, including two-axis toe joints.
    from UnityEngine import Quaternion, Vector3
    from vmdlib import VmdLib
    goal = bone.transform.position
    for _ in range(iterations):
        moved = False
        for link, bounds in zip(chain, limits):
            end = target.transform.position
            if (goal - end).sqrMagnitude < 0.000001:
                return
            transform = link.transform
            source, dest = end - transform.position, goal - transform.position
            if source.sqrMagnitude < 1e-12 or dest.sqrMagnitude < 1e-12:
                continue
            angle, axis = VmdLib.rotateVectorFromTo(source, dest)
            if angle < 1e-6:
                continue
            if axis.sqrMagnitude < 1e-12:
                # Antiparallel directions have no unique cross-product axis.
                axis = Vector3.Cross(source, Vector3.right)
                if axis.sqrMagnitude < 1e-12:
                    axis = Vector3.Cross(source, Vector3.up)
            result = Quaternion.AngleAxis(min(angle, step), axis) * transform.rotation
            if bounds is not None:
                parent = transform.parent.rotation if transform.parent else Quaternion.identity
                local = Quaternion.Inverse(parent) * result
                euler = local.eulerAngles
                result = parent * Quaternion.Euler(_clamp_angle(euler.x, bounds[0]),
                                                   _clamp_angle(euler.y, bounds[1]),
                                                   _clamp_angle(euler.z, bounds[2]))
            if Quaternion.Angle(transform.rotation, result) > 1e-6:
                moved = True
                transform.rotation = result
        if not moved:
            return
