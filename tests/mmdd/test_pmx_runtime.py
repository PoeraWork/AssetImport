"""Numerical CCD checks with transform doubles, not Unity/IronPython playback."""
import importlib.util
import math
from pathlib import Path
import sys
import types
import unittest


class Vector:
    def __init__(self, x=0, y=0, z=0):
        self.x, self.y, self.z = x, y, z

    def __add__(self, other):
        return Vector(self.x + other.x, self.y + other.y, self.z + other.z)

    def __sub__(self, other):
        return Vector(self.x - other.x, self.y - other.y, self.z - other.z)

    @property
    def sqrMagnitude(self):
        return self.x**2 + self.y**2 + self.z**2

    @staticmethod
    def Cross(a, b):
        return Vector(a.y*b.z-a.z*b.y, a.z*b.x-a.x*b.z, a.x*b.y-a.y*b.x)


class Quaternion:
    def __init__(self, x=0, y=0, z=0, w=1):
        self.x, self.y, self.z, self.w = x, y, z, w

    def __mul__(self, b):
        if isinstance(b, Vector):
            q = self * Quaternion(b.x, b.y, b.z, 0) * self.Inverse(self)
            return Vector(q.x, q.y, q.z)
        a = self
        return Quaternion(a.w*b.x+a.x*b.w+a.y*b.z-a.z*b.y,
                          a.w*b.y-a.x*b.z+a.y*b.w+a.z*b.x,
                          a.w*b.z+a.x*b.y-a.y*b.x+a.z*b.w,
                          a.w*b.w-a.x*b.x-a.y*b.y-a.z*b.z)

    @staticmethod
    def Inverse(q):
        return Quaternion(-q.x, -q.y, -q.z, q.w)

    @staticmethod
    def AngleAxis(degrees, axis):
        half = math.radians(degrees) / 2
        scale = math.sin(half) / math.sqrt(axis.sqrMagnitude)
        return Quaternion(axis.x*scale, axis.y*scale, axis.z*scale, math.cos(half))

    @staticmethod
    def Euler(x, y, z):
        # Unity's documented Z, X, Y order.
        return (Quaternion.AngleAxis(y, Vector(0, 1, 0)) *
                Quaternion.AngleAxis(x, Vector(1, 0, 0)) *
                Quaternion.AngleAxis(z, Vector(0, 0, 1)))

    @property
    def eulerAngles(self):
        x, y, z, w = self.x, self.y, self.z, self.w
        return Vector(math.degrees(math.asin(max(-1, min(1, 2*(w*x-y*z))))) % 360,
                      math.degrees(math.atan2(2*(x*z+w*y), 1-2*(x*x+y*y))) % 360,
                      math.degrees(math.atan2(2*(x*y+w*z), 1-2*(x*x+z*z))) % 360)

    @staticmethod
    def Angle(a, b):
        dot = abs(a.x*b.x+a.y*b.y+a.z*b.z+a.w*b.w)
        return math.degrees(2*math.acos(min(1, dot)))


class Transform:
    def __init__(self, position=None, parent=None, rotation=None):
        self.offset = position or Vector()
        self.parent = parent
        self.local = rotation or Quaternion()

    @property
    def position(self):
        if self.parent:
            return self.parent.position + self.parent.rotation * self.offset
        return self.offset

    @property
    def rotation(self):
        return self.parent.rotation * self.local if self.parent else self.local

    @rotation.setter
    def rotation(self, value):
        self.local = Quaternion.Inverse(self.parent.rotation) * value if self.parent else value


def from_to(a, b):
    dot = (a.x*b.x+a.y*b.y+a.z*b.z) / math.sqrt(a.sqrMagnitude*b.sqrMagnitude)
    return math.degrees(math.acos(max(-1, min(1, dot)))), Vector.Cross(a, b)


class RuntimeChecks(unittest.TestCase):
    def test_inactive_solver_never_reads_or_changes_transforms(self):
        # Missing transform attributes make unintended access fail immediately.
        for iterations, step, chain in ((0, 20, [object()]), (5, 0, [object()]), (5, 20, [])):
            self.runtime._solve_pmx_ik(object(), object(), chain, iterations, step, [None])

    def setUp(self):
        self.modules = dict(sys.modules)
        Vector.right, Vector.up = Vector(1, 0, 0), Vector(0, 1, 0)
        Quaternion.identity = Quaternion()
        self.stock_calls = []
        lib = type('VmdLib', (), {'rotateVectorFromTo': staticmethod(from_to),
                                 'mmdIKSolver': staticmethod(lambda *args: self.stock_calls.append(args))})
        sys.modules['UnityEngine'] = types.SimpleNamespace(Vector3=Vector, Quaternion=Quaternion)
        controller = type('MotionController', (), {'processTrack': lambda *a: None, 'buildWorkBoneIndex': lambda *a: None})
        sys.modules['vmdlib'] = types.SimpleNamespace(VmdLib=lib, MotionController=controller)
        self.library = types.SimpleNamespace(getMethod=lambda *args: None, _userFuncs={})
        sys.modules['mmddmorphfuncext'] = self.library
        path = Path(__file__).resolve().parents[2] / 'scripts/mmdd/item_morph_functions.py'
        spec = importlib.util.spec_from_file_location('pmx_runtime_test', path)
        self.runtime = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.runtime)
        self.runtime._register({})
        self.solve = lib.mmdIKSolver

    def tearDown(self):
        for name in ('UnityEngine', 'vmdlib', 'mmddmorphfuncext'):
            if name in self.modules:
                sys.modules[name] = self.modules[name]
            else:
                sys.modules.pop(name, None)

    def chain(self, goal, parent_rotation=None):
        parent = Transform(rotation=parent_rotation)
        pivot = Transform(parent=parent)
        tip = types.SimpleNamespace(transform=Transform(Vector(0, 0, 1), pivot))
        bone = types.SimpleNamespace(name='assetimport_pmx_0015', transform=Transform(parent.rotation * goal))
        return bone, tip, [types.SimpleNamespace(transform=pivot)]

    def test_toe_rotates_on_two_axes_under_rotated_parent(self):
        goal = Quaternion.Euler(-12, 35, 0) * Vector(0, 0, 1)
        bone, tip, chain = self.chain(goal, Quaternion.Euler(15, -25, 10))
        self.solve(bone, tip, chain, 40, 20, [[[-35, 20], [-60, 60], [0, 0]]])
        self.assertLess((bone.transform.position-tip.transform.position).sqrMagnitude, .00001)
        self.assertEqual(self.stock_calls, [])
        euler = chain[0].transform.local.eulerAngles
        self.assertAlmostEqual(self.runtime._clamp_angle(euler.z, [-180, 180]), 0, places=4)
        self.assertAlmostEqual(euler.y, 35, delta=.2)

    def test_limits_and_step_budget(self):
        goal = Quaternion.Euler(0, 80, 0) * Vector(0, 0, 1)
        bone, tip, chain = self.chain(goal)
        limits = [[[0, 0], [-20, 20], [0, 0]]]
        self.solve(bone, tip, chain, 1, 5, limits)
        self.assertAlmostEqual(chain[0].transform.local.eulerAngles.y, 5)
        self.solve(bone, tip, chain, 100, 5, limits)
        self.assertAlmostEqual(chain[0].transform.local.eulerAngles.y, 20)

    def test_antiparallel_and_degenerate(self):
        bone, tip, chain = self.chain(Vector(0, 0, -1))
        self.solve(bone, tip, chain, 4, 10, [[[-180, 180], [-180, 180], [-180, 180]]])
        self.assertGreater(Quaternion.Angle(chain[0].transform.rotation, Quaternion()), 0)
        tip.transform.offset = Vector()
        self.solve(bone, tip, chain, 40, 20, [[[0, 0], [-60, 60], [0, 0]]])

    def test_stock_models_and_knees_are_untouched(self):
        bone, tip, chain = self.chain(Vector(0, 0, 1))
        bone.name = 'other_plugin_ik'
        self.solve(bone, tip, chain, 3, 10, [[[0, 0], [-60, 60], [0, 0]]])
        bone.name = 'assetimport_pmx_0014'
        self.solve(bone, tip, chain, 40, 57, [[[.5, 180], [0, 0], [0, 0]], None])
        self.assertEqual(len(self.stock_calls), 2)
        method = self.library.getMethod
        self.runtime._register({})
        self.assertIs(method, self.library.getMethod, 'Reload must not stack discovery hooks')

    def test_angle_wrap(self):
        self.assertEqual(self.runtime._clamp_angle(350, [-20, 20]), -10)
        self.assertEqual(self.runtime._clamp_angle(190, [170, 200]), 190)
        self.assertEqual(self.runtime._clamp_angle(50, [0, 0]), 0)


if __name__ == '__main__':
    unittest.main()
