"""Run only on a fresh disposable mu_small_bonus_test at loopback:55432."""

from pathlib import Path
import subprocess
import unittest

from test_restore_gameplay_profile import ENV, query, snapshot


ENV["PGDATABASE"] = "mu_small_bonus_test"
SCRIPT = Path(__file__).resolve().parents[2] / "deploy/live/small-bonus-drops.sql"
FIRST_ID = "19b00500-0000-4000-8000-000000000000"


def apply(*, commit=False, expected="mu_small_bonus_test"):
    return subprocess.run(["psql", "-X", "-v", f"expected_database={expected}",
                           "-v", f"apply={'true' if commit else 'false'}", "-f", str(SCRIPT)],
                          env=ENV, capture_output=True, text=True)


class SmallBonusDropsTests(unittest.TestCase):
    def test_configuration_and_safety(self):
        self.assertEqual(query("SELECT inet_server_port()"), "55432")
        self.assertEqual(query('SELECT count(*) FROM config."DropItemGroup" WHERE "ItemType"=7'), "0",
                         "Use a fresh disposable fixture")
        servers_before = query('SELECT jsonb_agg(to_jsonb(t)-\'SmallBonusDropsEnabled\' ORDER BY "Id") FROM config."GameServerDefinition" t')
        before = snapshot()
        wrong = apply(commit=True, expected="wrong_database")
        self.assertNotEqual(wrong.returncode, 0)
        self.assertIn("Database does not match", wrong.stderr)
        self.assertEqual(snapshot(), before)
        preview = apply()
        self.assertEqual(preview.returncode, 0, preview.stderr)
        self.assertEqual(snapshot(), before)
        applied = apply(commit=True)
        self.assertEqual(applied.returncode, 0, applied.stderr)
        after = snapshot()
        self.assertEqual({name for name in before if before[name] != after[name]}, {
            "config.DropItemGroup", "config.DropItemGroupItemDefinition", "config.GameMapDefinitionDropItemGroup", "config.GameServerDefinition"})
        self.assertEqual(query('SELECT count(*) FROM config."DropItemGroup" WHERE "ItemType"=7'), "9")
        self.assertAlmostEqual(float(query('SELECT sum("Chance") FROM config."DropItemGroup" WHERE "ItemType"=7')), .05)
        self.assertEqual(query('''SELECT count(*) FROM config."DropItemGroupItemDefinition" j
            JOIN config."DropItemGroup" g ON g."Id"=j."DropItemGroupId" WHERE g."ItemType"=7'''), "19")
        self.assertEqual(query('''SELECT count(*) FROM config."GameMapDefinition" m
            WHERE m."GameConfigurationId"='00000001-0001-0000-0000-000000000000'
            AND (SELECT count(*) FROM config."GameMapDefinitionDropItemGroup" j
            JOIN config."DropItemGroup" g ON g."Id"=j."DropItemGroupId"
            WHERE j."GameMapDefinitionId"=m."Id" AND g."ItemType"=7) <> 9'''), "0")
        self.assertEqual(query('SELECT count(*) FROM config."GameServerDefinition" WHERE "ServerID"=0 AND "SmallBonusDropsEnabled"'), "1")
        self.assertEqual(query('SELECT count(*) FROM config."GameServerDefinition" WHERE "ServerID"<>0 AND "SmallBonusDropsEnabled"'), "0")
        self.assertEqual(query('SELECT jsonb_agg(to_jsonb(t)-\'SmallBonusDropsEnabled\' ORDER BY "Id") FROM config."GameServerDefinition" t'), servers_before)
        repeated = apply(commit=True)
        self.assertEqual(repeated.returncode, 0, repeated.stderr)
        self.assertEqual(snapshot(), after)
        query(f'''UPDATE config."DropItemGroup" SET "Chance"=.1 WHERE "Id"='{FIRST_ID}' ''')
        conflict_before = snapshot()
        conflict = apply(commit=True)
        self.assertNotEqual(conflict.returncode, 0)
        self.assertIn("Existing bonus group differs", conflict.stderr)
        self.assertEqual(snapshot(), conflict_before)
        query(f'''UPDATE config."DropItemGroup" SET "Chance"=.05::double precision/19 WHERE "Id"='{FIRST_ID}' ''')
        self.assertEqual(snapshot(), after)


if __name__ == "__main__":
    unittest.main()
