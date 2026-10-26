"""Check the shipped progression plan without connecting to any database."""

import json
from pathlib import Path
import runpy
import sys
import unittest


ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'deploy/live'))
GENERATOR = runpy.run_path(str(ROOT / 'deploy/live/prepare-box-rewards.py'))


class BonusProgressionPlanTests(unittest.TestCase):
    def test_shipped_resource_matches_reviewed_manifests(self):
        shipped = json.loads((ROOT / 'src/Persistence/Initialization/Resources/BonusProgression.json').read_text())
        self.assertEqual(shipped, GENERATOR['render_plan']())
        self.assertEqual(len(shipped['bonuses']), 69)
        self.assertEqual(len(shipped['rewards']), 73)

    def test_star_is_balanced_without_firecracker_duplicates(self):
        plan = GENERATOR['render_plan']()
        star = [group for group in plan['rewards'] if group['level'] == 1]
        for name in ('DW', 'DK', 'Elf', 'MG', 'DL', 'Summoner', 'RF'):
            self.assertAlmostEqual(sum(group['chance'] for group in star
                                       if group['description'].startswith(f'Approved Star: {name} /')), 1 / 7)
        totals = {'armor': 0, 'weapons': 0, 'shields': 0}
        for group in star:
            category = 'armor' if group['items'][0][0] >= 7 else ('shields' if group['items'][0][0] == 6 else 'weapons')
            totals[category] += group['chance']
        for category, expected in [('armor', .60), ('weapons', .35), ('shields', .05)]:
            self.assertAlmostEqual(totals[category], expected)
        firecracker = {tuple(item) for group in plan['rewards'] if group['level'] == 2 for item in group['items']}
        self.assertFalse(firecracker & {tuple(item) for group in star for item in group['items']})
        self.assertIn((3, 10), {tuple(item) for group in star for item in group['items']})
        self.assertNotIn((0, 17), {tuple(item) for group in star for item in group['items']})

    def test_non_equipment_probabilities_and_upgrade_levels_are_preserved(self):
        plan = GENERATOR['render_plan']()
        for level, zen in [(5, .20), (6, .10), (3, .10), (2, .25), (7, .20)]:
            groups = [group for group in plan['rewards'] if group['level'] == level]
            self.assertAlmostEqual(sum(group['chance'] for group in groups), 1)
            self.assertAlmostEqual(sum(group['chance'] for group in groups if group['type'] == 5), zen)
        for group in plan['rewards']:
            if group['level'] in (2, 3, 5, 6) and group['items'] and group['items'][0][0] <= 6:
                self.assertEqual((group['minimum'], group['maximum']), (8, 10) if group['level'] == 3 else (7, 9))
            if any(item[0] == 5 and item[1] in (21, 22, 23) for item in group['items']) and group['type'] != 2:
                self.assertEqual(group['skill'], 1)


if __name__ == '__main__':
    unittest.main()
