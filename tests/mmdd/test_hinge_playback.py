"""Run generated crouches through official MMDD indexing, IK and D-bone grants."""
import contextlib
import io
import math
from pathlib import Path
import random
import sys
import types
import unittest

from test_pmx_runtime import Vector, Quaternion
from test_position_grants import LIB

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/mmdd'))
from check_pmx_motion import controller


@unittest.skipUnless((LIB / 'vmdlib.py').is_file(), 'Official MMDD Lib required for playback checks')
class HingePlaybackChecks(unittest.TestCase):
    def test_crouch_seek_reset_and_deform_followers(self):
        previous = {name: sys.modules.get(name) for name in ('UnityEngine','vmdlib','mmddmorphfuncext')}
        try:
            for seed in range(8):
                for scale in (.0832,1,3):
                    with self.subTest(seed=seed,scale=scale):
                        order = list(range(9))
                        random.Random(seed).shuffle(order)
                        index = {old:new for new,old in enumerate(order)}
                        names = ['generated_%d_%d' % (seed,i) for i in range(9)]
                        parents = [-1,0,1,2,3,0,1,6,7]
                        heights = [0,1,1,.5,0,0,1,.5,0]
                        bones = [dict(index=index[i],name=names[i],parent=index[parents[i]] if parents[i]>=0 else -1,
                                      position=[0,heights[i],0],flags=6,layer=0 if i<5 else 1 if i==5 else 2)
                                 for i in range(9)]
                        bones[5]['ik'] = dict(target=index[4],iterations=80,angle=2,
                                              links=[dict(bone=index[3],min=[-math.pi,0,0],max=[math.radians(.5),0,0]),dict(bone=index[2])])
                        for i in range(6,9):
                            bones[i]['grant'] = dict(parent=index[i-4],weight=1,rotation=True,position=False,local=False)
                        model = dict(bones=[bones[i] for i in order],morphs=[],rigid_bodies=[])
                        def key(frame,position=None,rotation=None):
                            return types.SimpleNamespace(frameNo=frame,position=position or Vector(),rotation=rotation or Quaternion(),
                                                         xCurve=[0,0,1,1],yCurve=[0,0,1,1],zCurve=[0,0,1,1],rCurve=[0,0,1,1])
                        motion = {name:[key(0),key(10)] for name in names[:6]}
                        motion[names[0]] = [key(0,rotation=Quaternion.Euler(0,25,0))]
                        motion[names[1]] = [key(0,Vector(0,-.3,0)),key(10,Vector(0,-.7,0))]
                        motion[names[2]] = [key(0,rotation=Quaternion.Euler(50,0,0))]
                        motion[names[3]] = [key(0,rotation=Quaternion.Euler(-100,0,0))]
                        c,mapping = controller(model,motion,LIB,ROOT/'scripts/mmdd/item_morph_functions.py',scale)
                        mapped = {d['vmdName']:d['name'] for d in mapping['bones']}
                        def transform(i):
                            return c.getDummyBones(mapped[names[i]]).transform
                        first = {}
                        for frame in (0,10,5,10,0,5):
                            c.setFrame(frame)
                            self.assertLess((transform(4).position-transform(5).position).sqrMagnitude,1.1e-6)
                            self.assertLess((transform(8).position-transform(4).position).sqrMagnitude,1e-12)
                            self.assertGreater(transform(4).position.y,-.0011)
                            if frame in first:
                                self.assertLess((transform(8).position-first[frame]).sqrMagnitude,1e-12)
                            first[frame] = transform(8).position
                        with contextlib.redirect_stdout(io.StringIO()):
                            c.buildWorkBoneIndex()
                        c.setFrame(10)
                        self.assertLess((transform(8).position-first[10]).sqrMagnitude,1e-12)
                        c.resetPose()
                        c.setFrame(0)
                        self.assertLess((transform(8).position-first[0]).sqrMagnitude,1e-12)
        finally:
            for name,module in previous.items():
                if module is None:
                    sys.modules.pop(name,None)
                else:
                    sys.modules[name] = module


if __name__ == '__main__':
    unittest.main()
