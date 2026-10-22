"""Run on the fresh disposable mu_devias_spots_test fixture at loopback:55432."""

import json
from pathlib import Path
import subprocess
import unittest

from test_restore_gameplay_profile import ENV, query, snapshot


ENV["PGDATABASE"] = "mu_devias_spots_test"
SCRIPT = Path(__file__).resolve().parents[2] / "deploy/live/add-devias-farming-spots.sql"
IDS = ["3be31bf7-2d69-50f4-a4aa-7d79c8a00a5a",
       "11c0cd30-9e1b-5aa6-94bb-a28e3f9bbbf6",
       "40a097b1-69f3-5539-bcc4-39c782efeeaa"]


def apply(*, commit=False, expected="mu_devias_spots_test"):
    return subprocess.run(["psql", "-X", "-v", f"expected_database={expected}",
                           "-v", f"apply={'true' if commit else 'false'}", "-f", str(SCRIPT)],
                          env=ENV, capture_output=True, text=True)


def spawns():
    rows = json.loads(query('SELECT jsonb_agg(to_jsonb(t)) FROM config."MonsterSpawnArea" t'))
    return {row["Id"]: row for row in rows}


class DeviasFarmingSpotsTests(unittest.TestCase):
    def test_additive_spots_rollback_idempotence_and_refusals(self):
        self.assertEqual(query("SELECT current_database()"), "mu_devias_spots_test")
        self.assertEqual(query("SELECT inet_server_port()"), "55432")
        before = snapshot()
        old_spawns = spawns()
        self.assertTrue(set(IDS).isdisjoint(old_spawns), "Use a fresh disposable fixture")
        wrong = apply(commit=True, expected="wrong_database")
        self.assertNotEqual(wrong.returncode, 0)
        self.assertIn("Database does not match", wrong.stderr)
        self.assertEqual(snapshot(), before)
        dry_run = apply()
        self.assertEqual(dry_run.returncode, 0, dry_run.stderr)
        self.assertEqual(snapshot(), before)

        original_tile = int(query('SELECT get_byte("TerrainData",3+84*256+200) FROM config."GameMapDefinition" WHERE "Number"=2'))
        query('UPDATE config."GameMapDefinition" SET "TerrainData"=set_byte("TerrainData",3+84*256+200,1) WHERE "Number"=2')
        blocked_before = snapshot()
        blocked = apply(commit=True)
        self.assertNotEqual(blocked.returncode, 0)
        self.assertIn("clear 9x9 hunting area", blocked.stderr)
        self.assertEqual(snapshot(), blocked_before)
        query(f'UPDATE config."GameMapDefinition" SET "TerrainData"=set_byte("TerrainData",3+84*256+200,{original_tile}) WHERE "Number"=2')
        self.assertEqual(snapshot(), before)

        applied = apply(commit=True)
        self.assertEqual(applied.returncode, 0, applied.stderr)
        after_spawns = spawns()
        self.assertEqual(set(after_spawns)-set(old_spawns), set(IDS))
        for identity, old in old_spawns.items():
            self.assertEqual(after_spawns[identity], old, "Existing spawns, including Arena, must remain identical")
        for identity, x, y in zip(IDS, [200,50,225], [84,42,200]):
            spawn = after_spawns[identity]
            self.assertEqual([spawn["X1"],spawn["Y1"],spawn["X2"],spawn["Y2"],spawn["Quantity"]], [x,y,x,y,10])
            self.assertEqual(spawn["GameMapId"], "00000300-0002-0000-0000-000000000000")
        after = snapshot()
        self.assertEqual({name for name in before if before[name] != after[name]}, {"config.MonsterSpawnArea"})
        repeat = apply(commit=True)
        self.assertEqual(repeat.returncode, 0, repeat.stderr)
        self.assertEqual(snapshot(), after)
        query(f'UPDATE config."MonsterSpawnArea" SET "Quantity"=11 WHERE "Id"=\'{IDS[0]}\'')
        conflict_before = snapshot()
        conflict = apply(commit=True)
        self.assertNotEqual(conflict.returncode, 0)
        self.assertIn("existing Devias spot differs", conflict.stderr)
        self.assertEqual(snapshot(), conflict_before)
        query(f'UPDATE config."MonsterSpawnArea" SET "Quantity"=10 WHERE "Id"=\'{IDS[0]}\'')
        self.assertEqual(snapshot(), after)


if __name__ == "__main__":
    unittest.main()
