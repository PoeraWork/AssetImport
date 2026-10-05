"""Execute official MMDD 3 indexing/playback/pose methods with engine doubles.

Set ASSETIMPORT_MMDD_LIB to an existing MMDD Lib directory. The official source
is read locally, never copied into this repository or an installation package.
"""
import ast
import contextlib
import importlib.util
import io
import math
import os
from pathlib import Path
import sys
import types
import unittest

from test_pmx_runtime import Vector, Quaternion

ROOT = Path(__file__).resolve().parents[2]
LIB = Path(os.environ.get('ASSETIMPORT_MMDD_LIB', str(ROOT / '.build-downloads/mmdd3/MMDD v3.0 KK/BepInEx/plugins/Console/Lib')))


class Py2Dict(dict):
    def has_key(self, key):
        return key in self


class Adjustment:
    def __init__(self, base):
        self.basePosition = base

    def GetAdjustedPosition(self, position):
        return self.basePosition + position

    def GetAdjustedRotation(self, rotation):
        return rotation


def slerp(a, b, rate):
    dot = a.x*b.x + a.y*b.y + a.z*b.z + a.w*b.w
    if dot < 0:
        b, dot = Quaternion(-b.x, -b.y, -b.z, -b.w), -dot
    if dot > .999999:
        return Quaternion(a.x, a.y, a.z, a.w)
    angle = math.acos(min(1, dot))
    left, right = math.sin((1-rate)*angle)/math.sin(angle), math.sin(rate*angle)/math.sin(angle)
    return Quaternion(*(getattr(a, axis)*left + getattr(b, axis)*right for axis in ('x','y','z','w')))


class Utility:
    def __init__(self):
        self.position_tracks = 0

    def SetTransformLocalPosition(self, transform, value):
        transform.localPosition = value

    def SetTransformLocalRotation(self, transform, value):
        transform.localRotation = value

    def SetWorkBoneIndexTrackLocalPosition(self, target, source, rate):
        self.position_tracks += 1
        a, b = target.localPosition, source.localPosition
        target.localPosition = Vector(a.x+b.x*rate, a.y+b.y*rate, a.z+b.z*rate)

    def SetWorkBoneIndexTrackLocalRotation(self, target, source, rate):
        target.localRotation = slerp(Quaternion.identity, source.localRotation, rate) * target.localRotation


@unittest.skipUnless((LIB / 'vmdlib.py').is_file(), 'Official MMDD Lib required for playback checks')
class PositionGrantChecks(unittest.TestCase):
    def setUp(self):
        self.modules = {name: sys.modules.get(name) for name in ('UnityEngine','vmdlib','mmddmorphfuncext')}
        Quaternion.identity, Vector.zero = Quaternion(), Vector()
        Quaternion.SlerpUnclamped = staticmethod(slerp)
        tree = ast.parse((LIB / 'vmdlib.py').read_text(encoding='utf-8-sig'))
        cls = next(n for n in tree.body if isinstance(n, ast.ClassDef) and n.name == 'MotionController')
        methods = {'processTrack','buildWorkBoneIndex','initPose','resetPose','updatePose',
                   'setFrame','getRangeIndexValue','getWorkBoneIndex'}
        cls.body = [n for n in cls.body if isinstance(n, ast.FunctionDef) and n.name in methods]
        self.utility, self.optimized = Utility(), False
        env = {'Quaternion': Quaternion, 'Vector3': Vector, 'GData': types.SimpleNamespace,
               'UseUtility': lambda: self.optimized, 'GetUtility': lambda: self.utility}
        exec(compile(ast.Module(body=[cls], type_ignores=[]), str(LIB / 'vmdlib.py'), 'exec'), env)
        self.controller_type = env['MotionController']
        lib = type('VmdLib', (), {'mmdIKSolver': staticmethod(lambda *a: None)})
        sys.modules['vmdlib'] = types.SimpleNamespace(VmdLib=lib, MotionController=self.controller_type)
        sys.modules['UnityEngine'] = types.SimpleNamespace(Quaternion=Quaternion, Vector3=Vector)
        sys.modules['mmddmorphfuncext'] = types.SimpleNamespace(getMethod=lambda *a: None, _userFuncs={})
        spec = importlib.util.spec_from_file_location('position_runtime', ROOT / 'scripts/mmdd/item_morph_functions.py')
        self.runtime = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.runtime)
        self.runtime._register({})

    def tearDown(self):
        for name, module in self.modules.items():
            if module is None: sys.modules.pop(name, None)
            else: sys.modules[name] = module

    def controller(self, rate=.2, scale=1, keyed=True, ik=False):
        c = self.controller_type()
        names = ['assetimport_pmx_%04d' % i for i in range(3)] + ['foreign_track']
        c.boneNameList, c.boneDefineMap, c.dummyBones = names, {}, {}
        bases = [Vector(10*scale,-2*scale,8*scale), Vector(-3*scale,5*scale,9*scale), Vector(2*scale,7*scale,-4*scale), Vector(1,2,3)]
        for i, (name, base) in enumerate(zip(names, bases)):
            c.boneDefineMap[name] = types.SimpleNamespace(enable=True, vmdName=name, movable=True,
                rotatable=True, isTrack=i>0, trackData=('rp' if i==1 else 'p', rate if i==1 else -.5, names[max(0,i-1)]),
                isLimitAxis=False, isIK=ik and i==1, adjustment=Adjustment(base))
            c.dummyBones[name] = types.SimpleNamespace(transform=types.SimpleNamespace(localPosition=base, localRotation=Quaternion()))
        c.getDummyBones = lambda name: c.dummyBones[name]
        c.boneKeyFrames = Py2Dict({name:[types.SimpleNamespace(frameNo=0),types.SimpleNamespace(frameNo=10)] for name in names[:2]} if keyed else {})
        c.ikvKeyframes, c.ikBoneList, c.ikSolverIndex = [], [], {}
        c.ik_calls = 0
        if ik:
            def solve():
                c.ik_calls += 1
            c.ikSolverIndex[names[1]] = [types.SimpleNamespace(enable=True, defaultEnable=True), None, None, solve]
            c.ikBoneList = [names[1]]
        c.CharName = 'generated'
        c.processTwistDisperse = lambda: None
        c.initPose()
        c.playbackSpeed, c.delay, c.differentiation, c.useVisibleFrame = 1, 0, False, False
        c.ociChar = types.SimpleNamespace(treeNodeObject=types.SimpleNamespace(visible=True))
        c.updateOnlyWhenVisible, c.Baked, c.indexMode, c.HighHeels = False, False, True, None
        c.twistDisperse = []
        def frame(frame_no, entry):
            own = .1 if entry.bonename == names[1] else 1
            entry.wPosition = Vector(frame_no*own*scale, -frame_no*own*scale, .5*frame_no*own*scale)
            entry.wRotation = Quaternion.AngleAxis(frame_no*own, Vector(0,0,1))
        c.getVmdBoneFrameIndexed = frame
        with contextlib.redirect_stdout(io.StringIO()): c.buildWorkBoneIndex()
        return c, names, bases

    def vector_equal(self, a, b):
        self.assertLess((a-b).sqrMagnitude, 1e-10)

    def test_indexed_offsets_chains_seek_and_native_utility(self):
        for optimized in (False, True):
            self.optimized = optimized
            for rate in (.2, -1, 1.5):
                for scale in (.0832, 3):
                    c, names, bases = self.controller(rate, scale, ik=True)
                    for frame in (0,10,10,2,0):
                        c.setFrame(frame)
                        delta = Vector(frame*scale*(.1+rate), -frame*scale*(.1+rate), .5*frame*scale*(.1+rate))
                        first = bases[1] + delta
                        second = bases[2] + Vector(-.5*delta.x,-.5*delta.y,-.5*delta.z)
                        self.vector_equal(c.getDummyBones(names[1]).transform.localPosition, first)
                        self.vector_equal(c.getDummyBones(names[2]).transform.localPosition, second)
                        # Unrelated MMDD mappings retain stock absolute-position tracking.
                        self.vector_equal(c.getDummyBones(names[3]).transform.localPosition,
                                          bases[3] + Vector(-.5*second.x,-.5*second.y,-.5*second.z))
                        self.assertAlmostEqual(Quaternion.Angle(c.getDummyBones(names[1]).transform.localRotation,
                                               Quaternion.AngleAxis(frame*(.1+rate), Vector(0,0,1))), 0, places=4)
                    self.assertEqual(c.ik_calls, 5, 'grant correction must not replace or duplicate real IK')
                    self.assertEqual(len(c.workBoneIndexBuffer), 6)
                    self.assertEqual(c.getWorkBoneIndex(names[1]).bonename, names[1])
        self.assertGreater(self.utility.position_tracks, 0, 'native utility path remains enabled')

    def test_pose_reset_and_unkeyed_rest_pose(self):
        c, names, bases = self.controller(keyed=False)
        for _ in range(3):
            c.setFrame(10)
            for i in (0,1,2): self.vector_equal(c.getDummyBones(names[i]).transform.localPosition, bases[i])
        c.poseData[names[0]][1] = bases[0] + Vector(2,3,4)
        c.poseData[names[1]][1] = bases[1] + Vector(1,2,3)
        for _ in range(3):
            c.updatePose()
            self.vector_equal(c.getDummyBones(names[1]).transform.localPosition, bases[1]+Vector(1.4,2.6,3.8))
        c.resetPose()
        c.updatePose()
        for i in (0,1,2): self.vector_equal(c.getDummyBones(names[i]).transform.localPosition, bases[i])

    def test_ik_visibility_does_not_disable_position_grant(self):
        c, names, bases = self.controller(ik=True)
        c.ikSolverIndex[names[1]][2] = types.SimpleNamespace(enable=True, tempRequest=None, length=1, valueList=[False])
        with contextlib.redirect_stdout(io.StringIO()): c.buildWorkBoneIndex()
        c.setFrame(10)
        self.assertEqual(c.ik_calls, 0)
        self.vector_equal(c.getDummyBones(names[1]).transform.localPosition, bases[1]+Vector(3,-3,1.5))

    def test_controller_isolation_reload_and_index_rebuild(self):
        a, names, bases = self.controller()
        b, _, other_bases = self.controller(scale=5)
        method = self.controller_type.buildWorkBoneIndex
        self.runtime._register({})
        self.assertIs(method, self.controller_type.buildWorkBoneIndex)
        a.setFrame(10)
        b.setFrame(2)
        self.vector_equal(a.getDummyBones(names[1]).transform.localPosition, bases[1]+Vector(3,-3,1.5))
        self.vector_equal(b.getDummyBones(names[1]).transform.localPosition, other_bases[1]+Vector(3,-3,1.5))
        with contextlib.redirect_stdout(io.StringIO()): a.buildWorkBoneIndex()
        self.assertEqual(len(a.workBoneIndexBuffer), 6, 'rebuild must not accumulate correction entries')
        a.setFrame(0)
        self.vector_equal(a.getDummyBones(names[1]).transform.localPosition, bases[1])
        a.boneDefineMap[names[1]].enable = False
        with contextlib.redirect_stdout(io.StringIO()): a.buildWorkBoneIndex()
        self.assertNotIn(names[1]+'__position_rest', [e.bonename for e in a.workBoneIndexBuffer])

    def test_python2_unbound_method_reload_marker(self):
        class LegacyMethod:
            def __init__(self, function): self.function = function

            def __get__(self, instance, owner):
                if instance is None: return types.SimpleNamespace(im_func=self.function)
                return self.function.__get__(instance, owner)
        c, names, bases = self.controller()
        track = self.controller_type.processTrack
        build = self.controller_type.buildWorkBoneIndex
        self.controller_type.processTrack = LegacyMethod(track)
        self.controller_type.buildWorkBoneIndex = LegacyMethod(build)
        self.runtime._register({})
        self.assertIs(self.controller_type.processTrack.im_func, track)
        self.assertIs(self.controller_type.buildWorkBoneIndex.im_func, build)
        c.setFrame(10)
        self.vector_equal(c.getDummyBones(names[1]).transform.localPosition, bases[1]+Vector(3,-3,1.5))


if __name__ == '__main__':
    unittest.main()
