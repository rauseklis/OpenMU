"""Integration regression for an explicitly selected disposable PostgreSQL fixture.

Run against a freshly seeded Season 6 database named mu_profile_restore_test,
on loopback port 55432. Requires psql on PATH; never connects to production.
"""

import json
import os
from pathlib import Path
import subprocess
import unittest


ROOT = Path(__file__).resolve().parents[2]
RESTORE = ROOT / "deploy/live/restore-gameplay-profile.sql"
DATABASE = "mu_profile_restore_test"
ENV = {**os.environ, "PGHOST": "127.0.0.1", "PGPORT": "55432",
       "PGUSER": "muvalidation", "PGDATABASE": DATABASE, "PGPASSWORD": ""}


def query(sql):
    result = subprocess.run(
        ["psql", "-X", "-A", "-t", "-v", "ON_ERROR_STOP=1", "-c", sql],
        env=ENV, text=True, capture_output=True, check=True)
    return result.stdout.strip()


def restore(*, apply=False, expected=DATABASE):
    return subprocess.run(
        ["psql", "-X", "-v", f"expected_database={expected}",
         "-v", f"apply={'true' if apply else 'false'}", "-f", str(RESTORE)],
        env=ENV, text=True, capture_output=True)


def snapshot():
    """Hash every config/player table, sorting rows so heap order is irrelevant."""
    tables = json.loads(query("""
        SELECT json_agg(t) FROM (
          SELECT schemaname, tablename FROM pg_tables
          WHERE schemaname IN ('config','data') ORDER BY 1,2
        ) t
    """))
    statements = []
    for table in tables:
        schema, name = table["schemaname"], table["tablename"]
        statements.append(
            f"SELECT '{schema}.{name}' AS name, md5(coalesce("
            f"string_agg(to_jsonb(t)::text, '' ORDER BY to_jsonb(t)::text),'')) AS digest "
            f'FROM "{schema}"."{name}" t')
    rows = json.loads(query("SELECT json_agg(s) FROM (" + " UNION ALL ".join(statements) + ") s"))
    return {row["name"]: row["digest"] for row in rows}


def profile_is_restored():
    return query("""
        SELECT count(*) FROM config."PlugInConfiguration"
        WHERE "TypeId"='6a9d585d-79d7-4674-b6ea-7e87392fa501' AND "IsActive"
          AND "CustomConfiguration"::jsonb @> '{"PointsPerReset":500,"ResetLimit":50}'
    """) == "1"


class RestoreGameplayProfileTests(unittest.TestCase):
    def test_missing_profile_restoration_and_safety(self):
        self.assertEqual(query("SELECT inet_server_port()"), "55432")
        self.assertFalse(profile_is_restored(), "Use a fresh disposable fixture")
        before = snapshot()
        original_spawns = query('SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY "Id"),\'[]\') FROM config."MonsterSpawnArea" t')
        old_count = int(query('SELECT count(*) FROM config."MonsterSpawnArea"'))
        old_gates = json.loads(query('SELECT jsonb_agg(to_jsonb(t)) FROM config."ExitGate" t'))
        blocked_gate_ids = set(json.loads(query('''
            SELECT coalesce(json_agg(g."Id"),'[]') FROM config."ExitGate" g
            JOIN config."GameMapDefinition" m ON m."Id"=g."MapId" WHERE m."Number"=6
              AND NOT EXISTS (SELECT FROM generate_series(g."X1"::integer,g."X2"::integer) x,
                generate_series(g."Y1"::integer,g."Y2"::integer) y
                WHERE get_byte(m."TerrainData",3+y*256+x) IN (0,1))
        ''')))
        self.assertTrue(blocked_gate_ids, "Fixture must reproduce blocked Arena arrivals")

        rejected = restore(apply=True, expected="different_database")
        self.assertNotEqual(rejected.returncode, 0)
        self.assertIn("Database does not match", rejected.stderr)
        self.assertEqual(snapshot(), before, "Wrong-target refusal must change nothing")

        dry_run = restore()
        self.assertEqual(dry_run.returncode, 0, dry_run.stderr)
        self.assertEqual(snapshot(), before, "Dry run must roll back all writes")
        self.assertFalse(profile_is_restored())

        applied = restore(apply=True)
        self.assertEqual(applied.returncode, 0, applied.stderr)
        self.assertTrue(profile_is_restored())
        self.assertEqual(query('SELECT count(*) FROM config."ItemDefinition" WHERE "Group"=14 AND "Number" IN (0,1,2,3,4,5,6,8) AND "Durability"=50'), "8")
        self.assertEqual(query('SELECT count(*) FROM config."ItemDefinition" WHERE "Group"=14 AND "Number" BETWEEN 35 AND 40 AND "Durability"=3'), "6")
        self.assertEqual(query('SELECT count(*) FROM config."ItemBasePowerUpDefinition" p JOIN config."ItemDefinition" i ON i."Id"=p."ItemDefinitionId" WHERE i."Group"=4 AND NOT i."IsAmmunition" AND p."TargetAttributeId"=\'4cd0b1ae-3fe0-499b-b421-4e4078182090\' AND p."BaseValue"<>0'), "0")
        self.assertEqual(int(query('SELECT count(*) FROM config."MonsterSpawnArea"')), old_count + 15)
        current_spawns = json.loads(query('SELECT jsonb_agg(to_jsonb(t)) FROM config."MonsterSpawnArea" t'))
        current_by_id = {spawn["Id"]: spawn for spawn in current_spawns}
        for spawn in json.loads(original_spawns):
            self.assertEqual(current_by_id[spawn["Id"]], spawn, "Existing spawns must remain identical")
        new_gates = {gate["Id"]: gate for gate in json.loads(query('SELECT jsonb_agg(to_jsonb(t)) FROM config."ExitGate" t'))}
        for old_gate in old_gates:
            new_gate = new_gates[old_gate["Id"]]
            if old_gate["Id"] not in blocked_gate_ids:
                self.assertEqual(new_gate, old_gate)
            else:
                coordinates = {"X1", "X2", "Y1", "Y2"}
                self.assertEqual({k: v for k, v in old_gate.items() if k not in coordinates},
                                 {k: v for k, v in new_gate.items() if k not in coordinates})
                self.assertEqual(new_gate["X1"], new_gate["X2"])
                self.assertEqual(new_gate["Y1"], new_gate["Y2"])
        self.assertEqual(query('''SELECT count(*) FROM config."ExitGate" g JOIN config."GameMapDefinition" m ON m."Id"=g."MapId"
            WHERE m."Number"=6 AND EXISTS (SELECT FROM generate_series(-1,1) dx,generate_series(-1,1) dy
            WHERE get_byte(m."TerrainData",3+(g."Y1"+dy)*256+g."X1"+dx) NOT IN (0,1))'''), "0")

        after = snapshot()
        changed = {name for name in before if before[name] != after[name]}
        self.assertEqual(changed, {
            "config.PlugInConfiguration", "config.ItemDefinition",
            "config.ItemBasePowerUpDefinition", "config.MonsterSpawnArea", "config.ExitGate"})
        # This includes unchanged EXP, terrain, formulas and every data table.
        repeated = restore(apply=True)
        self.assertEqual(repeated.returncode, 0, repeated.stderr)
        self.assertEqual(snapshot(), after, "Rerun must not duplicate or alter anything")

        spot = "98b255d9-72d5-5438-ac69-ec0e97115142"
        query(f'UPDATE config."MonsterSpawnArea" SET "Quantity"=11 WHERE "Id"=\'{spot}\'')
        conflict_snapshot = snapshot()
        conflict = restore(apply=True)
        self.assertNotEqual(conflict.returncode, 0)
        self.assertIn("existing custom spawn differs", conflict.stderr)
        self.assertEqual(snapshot(), conflict_snapshot, "Collision must roll back the whole operation")
        query(f'UPDATE config."MonsterSpawnArea" SET "Quantity"=10 WHERE "Id"=\'{spot}\'')
        self.assertEqual(snapshot(), after)


if __name__ == "__main__":
    unittest.main()
