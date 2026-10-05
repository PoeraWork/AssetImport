"""Offline exporter dependency regressions, independent of private PMX files."""
import random
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'scripts/mmdd'))
from generate_pmx_mapping import create


class DependencyTests(unittest.TestCase):
    def test_nonlocal_position_and_combined_grants(self):
        for rotation in (False, True):
            for weight in (-.25, .2, 1.5):
                model = {'bones': [
                    {'index': 0, 'name': 'source', 'parent': -1, 'flags': 6, 'layer': 0},
                    {'index': 1, 'name': 'follower', 'parent': -1, 'flags': 0, 'layer': 0,
                     'grant': {'parent': 0, 'weight': weight, 'position': True, 'rotation': rotation, 'local': False}}
                ], 'morphs': [], 'rigid_bodies': []}
                mapping, report = create(model, 'generated position grant')
                follower = mapping['bones'][1]
                self.assertEqual(follower['trackData'], ['rp' if rotation else 'p', weight, 'assetimport_pmx_0000'])
                self.assertTrue(follower['movable'])
                self.assertEqual(follower['rotatable'], rotation)
                self.assertEqual(report['position_grants'], 1)
                self.assertEqual(report['rotation_grants'], int(rotation))
                model['bones'][1]['grant']['local'] = True
                with self.assertRaisesRegex(ValueError, 'Unsupported local grant'):
                    create(model, 'local semantics cannot use position-only fix')

    def test_transitive_physics_ik_append_closure(self):
        for seed in range(24):
            for selected in (False, True):
                for target_dependency in (False, True):
                    with self.subTest(seed=seed, selected=selected, target=target_dependency):
                        order = list(range(10))
                        rng = random.Random(seed)
                        rng.shuffle(order)
                        index = {old: new for new, old in enumerate(order)}
                        bones = [{'index': index[i], 'name': 'generated_%d_%d' % (seed, i),
                                  'parent': -1 if i == 0 else index[0], 'flags': 6,
                                  'layer': rng.randrange(4)} for i in range(10)]
                        bones[2]['parent'] = index[1]
                        for i, dependency in ((3, 2), (6, 5)):
                            bones[i]['ik'] = {'target': index[dependency if target_dependency else 8],
                                             'links': [{'bone': index[8 if target_dependency else dependency]}],
                                             'iterations': 4, 'angle': 1}
                        bones[4]['parent'] = index[3]
                        for i, source in ((5, 4), (7, 6)):
                            bones[i]['grant'] = {'parent': index[source], 'weight': .5,
                                                 'position': True, 'local': True}
                        bones[9]['grant'] = {'parent': index[7], 'weight': 0, 'position': False, 'local': False}
                        model = {'bones': [bones[i] for i in order], 'morphs': [],
                                 'rigid_bodies': [] if selected else [{'mode': 1, 'bone': index[1]}]}
                        kwargs = {'dynamic_roots': [index[1]]} if selected else {}
                        mapping, report = create(model, 'generated graph', **kwargs)
                        self.assertEqual({b['vmdName'] for b in mapping['bones']},
                                         {bones[i]['name'] for i in (0, 8, 9)})
                        self.assertEqual(report['ik_solvers'], 0)
                        self.assertEqual(sum('Physics append follower' in w for w in report['warnings']), 2)


if __name__ == '__main__':
    unittest.main()
