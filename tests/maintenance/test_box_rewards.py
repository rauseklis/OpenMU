"""Validate the complete update only on the task-owned disposable cluster."""

import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import unittest


ROOT = Path(__file__).resolve().parents[2]
DATABASE = os.environ.get("BOX_REWARD_TEST_DATABASE", "mu_box_rewards_20261024_final")
PORT = os.environ.get("BOX_REWARD_TEST_PORT", "55443")
USER = os.environ.get("BOX_REWARD_TEST_USER", "boxvalidation")
if (DATABASE, PORT, USER) not in {
    ("mu_box_rewards_20261024_final", "55443", "boxvalidation"),
    ("mu_bonus_progression_20261026", "55503", "progressionvalidation"),
}:
    raise RuntimeError("Refusing any database other than the task-owned disposable fixtures")
ENV = {**os.environ, "PGHOST": "127.0.0.1", "PGPORT": PORT,
       "PGUSER": USER, "PGDATABASE": DATABASE, "PGPASSWORD": ""}
PSQL = os.environ.get("BOX_REWARD_PSQL", "psql")


def query(sql):
    result = subprocess.run([PSQL, "-X", "-A", "-t", "-v", "ON_ERROR_STOP=1"],
                            input=sql, env=ENV, text=True, capture_output=True, check=True)
    return result.stdout.strip()


def snapshot(schemas):
    tables = json.loads(query("SELECT coalesce(json_agg(t),'[]') FROM (SELECT schemaname,tablename "
                              "FROM pg_tables WHERE schemaname IN (" +
                              ",".join("'" + name + "'" for name in schemas) +
                              ") ORDER BY 1,2) t"))
    hashes = {}
    for table in tables:
        schema, name = table["schemaname"], table["tablename"]
        contents = query(f'SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text),\'[]\') FROM "{schema}"."{name}" t')
        hashes[f"{schema}.{name}"] = hashlib.sha256(contents.encode()).hexdigest()
    return hashes


def apply(script, commit=False, expected=DATABASE):
    return subprocess.run([PSQL, "-X", "-v", f"expected_database={expected}",
                           "-v", f"apply={'true' if commit else 'false'}"],
                          input=script, env=ENV, text=True, capture_output=True)


class BoxRewardMaintenanceTests(unittest.TestCase):
    def test_preview_application_and_repeat(self):
        self.assertEqual(query("SELECT inet_server_port()"), PORT)
        self.assertEqual(query("SELECT current_database()"), DATABASE)
        generated = subprocess.run([sys.executable, str(ROOT / "deploy/live/prepare-box-rewards.py")],
                                   text=True, capture_output=True, check=True).stdout
        before = snapshot(("config", "data", "guild", "friend"))
        event_items_sql = '''SELECT coalesce(jsonb_agg(to_jsonb(j) ORDER BY to_jsonb(j)::text),'[]')
            FROM config."DropItemGroupItemDefinition" j WHERE j."DropItemGroupId" IN
            (SELECT "ItemRewardId" FROM config."MiniGameReward" WHERE "ItemRewardId" IS NOT NULL)'''
        event_items = query(event_items_sql)
        unrelated_items = query('SELECT coalesce(jsonb_agg(to_jsonb(i) ORDER BY "Id"),\'[]\') FROM data."Item" i WHERE "Id"::text NOT LIKE \'24b00002-%\'')
        wrong = apply(generated, commit=True, expected="wrong_database")
        self.assertNotEqual(wrong.returncode, 0)
        self.assertIn("Database does not match", wrong.stderr)
        self.assertEqual(snapshot(("config", "data", "guild", "friend")), before)
        preview = apply(generated)
        self.assertEqual(preview.returncode, 0, preview.stderr)
        self.assertEqual(snapshot(("config", "data", "guild", "friend")), before)
        committed = apply(generated, commit=True)
        self.assertEqual(committed.returncode, 0, committed.stderr)
        after = snapshot(("config", "data", "guild", "friend"))
        self.assertEqual(query(event_items_sql), event_items, "Event payout contents must survive monster-source cleanup")
        changed = {name for name in before if before[name] != after[name]}
        allowed = {"config.ItemDropItemGroup", "config.ItemDropItemGroupItemDefinition",
                   "config.DropItemGroup", "config.DropItemGroupItemDefinition",
                   "config.GameMapDefinitionDropItemGroup", "config.MonsterDefinitionDropItemGroup",
                   "config.GameServerDefinition", "config.ItemDefinition", "data.Item"}
        self.assertLessEqual(changed, allowed)
        self.assertEqual(query('SELECT coalesce(jsonb_agg(to_jsonb(i) ORDER BY "Id"),\'[]\') FROM data."Item" i WHERE "Id"::text NOT LIKE \'24b00002-%\''), unrelated_items)
        self.assertEqual(query('SELECT count(*) FROM data."Item" WHERE "Id"::text LIKE \'24b00002-%\' AND "StorePrice"=1000'), "10")
        actual_chances = json.loads(query('SELECT json_object_agg("SourceItemLevel",chance) FROM (SELECT DISTINCT "SourceItemLevel","ExcellentSecondOptionChance" AS chance FROM config."ItemDropItemGroup" WHERE "Id"::text LIKE \'24b00000-%\' AND "ItemType"=2) t'))
        self.assertEqual(actual_chances, {"8": .30, "9": .25, "10": .20, "11": .15, "12": .10})
        self.assertEqual(query('SELECT count(*) FROM config."ItemDropItemGroup" WHERE "Id"::text LIKE \'24b00000-%\''), "73")
        self.assertEqual(query('SELECT count(*) FROM config."DropItemGroup" WHERE "ItemType"=7 AND "GameConfigurationId" IS NOT NULL'), "69")
        self.assertEqual(query('SELECT "SmallBonusDropChance" FROM config."GameServerDefinition" WHERE "ServerID"=0'), "0.05")
        actual = json.loads(query('''SELECT json_agg(json_build_array(m."Number",n."Number",g."ItemLevel"))
            FROM config."DropItemGroup" g JOIN config."GameMapDefinitionDropItemGroup" j ON j."DropItemGroupId"=g."Id"
            JOIN config."GameMapDefinition" m ON m."Id"=j."GameMapDefinitionId"
            JOIN config."MonsterDefinition" n ON n."Id"=g."MonsterId" WHERE g."ItemType"=7'''))
        rules = json.loads((ROOT / "deploy/live/box-reward-rules.json").read_text())
        levels = {"SILVER MEDAL": 5, "GOLD MEDAL": 6, "HEART OF LOVE": 3, "FIRECRACKER": 2, "BOX OF HEAVEN": 7, "STAR": 1}
        expected = [[entry["map"], entry["monster"], levels[entry["bonus"]]] for entry in rules["eligibleMonsters"]]
        self.assertEqual(sorted(actual), sorted(expected))
        repeated = apply(generated, commit=True)
        self.assertEqual(repeated.returncode, 0, repeated.stderr)
        self.assertEqual(snapshot(("config", "data", "guild", "friend")), after)


if __name__ == "__main__":
    unittest.main()
