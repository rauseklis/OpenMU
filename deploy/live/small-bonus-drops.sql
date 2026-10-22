-- Manual opt-in configuration. Defaults to rollback; deploy SmallBonus-aware code first.
-- psql -X -v expected_database=<database> [-v apply=true] -f small-bonus-drops.sql
\set ON_ERROR_STOP on
\if :{?apply}
\else
\set apply false
\endif
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
SELECT set_config('mu.expected_database', :'expected_database', true);
DO $$ BEGIN
 IF current_database() <> current_setting('mu.expected_database') THEN
   RAISE EXCEPTION 'Database does not match explicitly selected maintenance target'; END IF;
END $$;

CREATE TEMP TABLE bonus_groups (
 id uuid PRIMARY KEY, description text, level smallint, chance double precision
) ON COMMIT DROP;
INSERT INTO bonus_groups
SELECT ('19b00500-0000-4000-8000-' || lpad(n::text,12,'0'))::uuid,
 'Small bonus 5%: ' || label,n,0.05::double precision/19
FROM (VALUES (0,'Box of Luck'),(1,'Star of Sacred Birth'),(2,'Firecracker'),
 (3,'Heart of Love'),(4,'Olive of Love'),(5,'Silver Medal'),(6,'Gold Medal'),(7,'Box of Heaven')) v(n,label);
INSERT INTO bonus_groups VALUES
 ('19b00500-0000-4000-8000-000000000008','Small bonus 5%: seasonal gifts',0,11*0.05::double precision/19);

CREATE TEMP TABLE bonus_items ON COMMIT DROP AS
SELECT g.id AS group_id,i."Id" AS item_id
FROM bonus_groups g JOIN config."ItemDefinition" i
 ON i."GameConfigurationId"='00000001-0001-0000-0000-000000000000'
 AND ((g.id <> '19b00500-0000-4000-8000-000000000008' AND i."Group"=14 AND i."Number"=11)
 OR (g.id = '19b00500-0000-4000-8000-000000000008'
 AND ((i."Group"=12 AND i."Number" IN (32,33,34))
 OR (i."Group"=14 AND i."Number" IN (32,33,34,45,51,63,84,99)))));

DO $$ BEGIN
 IF (SELECT count(*) FROM bonus_items) <> 19
 OR EXISTS (SELECT FROM bonus_items b JOIN bonus_groups g ON g.id=b.group_id
 JOIN config."ItemDefinition" i ON i."Id"=b.item_id
 WHERE i."DropLevel"<>0 OR i."MaximumDropLevel" IS NOT NULL OR i."MaximumItemLevel"<g.level
 OR i."DropsFromMonsters") THEN
   RAISE EXCEPTION 'Expected 19 supported gift variants outside ordinary monster loot'; END IF;
 IF NOT EXISTS (SELECT FROM config."GameMapDefinition"
 WHERE "GameConfigurationId"='00000001-0001-0000-0000-000000000000') THEN
   RAISE EXCEPTION 'Target configuration has no maps'; END IF;
 IF EXISTS (SELECT FROM config."DropItemGroup" d JOIN bonus_groups g ON g.id=d."Id"
 WHERE (d."GameConfigurationId",d."Description",d."Chance",d."ItemLevel",d."ItemType")
 IS DISTINCT FROM ('00000001-0001-0000-0000-000000000000'::uuid,g.description,g.chance,g.level,7)
 OR d."MonsterId" IS NOT NULL OR d."MinimumMonsterLevel" IS NOT NULL OR d."MaximumMonsterLevel" IS NOT NULL)
 OR EXISTS (SELECT FROM config."ItemDropItemGroup" d JOIN bonus_groups g ON g.id=d."Id") THEN
   RAISE EXCEPTION 'Existing bonus group differs; manual review required'; END IF;
 IF EXISTS (SELECT FROM config."DropItemGroupItemDefinition" j JOIN bonus_groups g ON g.id=j."DropItemGroupId"
 WHERE NOT EXISTS (SELECT FROM bonus_items b WHERE b.group_id=j."DropItemGroupId" AND b.item_id=j."ItemDefinitionId"))
 OR EXISTS (SELECT FROM config."MonsterDefinitionDropItemGroup" j JOIN bonus_groups g ON g.id=j."DropItemGroupId")
 OR EXISTS (SELECT FROM config."GameMapDefinitionDropItemGroup" j JOIN bonus_groups g ON g.id=j."DropItemGroupId"
 JOIN config."GameMapDefinition" m ON m."Id"=j."GameMapDefinitionId"
 WHERE m."GameConfigurationId"<>'00000001-0001-0000-0000-000000000000') THEN
   RAISE EXCEPTION 'Existing bonus links differ; manual review required'; END IF;
 IF EXISTS (SELECT FROM config."DropItemGroup" d
 WHERE d."GameConfigurationId"='00000001-0001-0000-0000-000000000000' AND d."ItemType"=7
 AND d."Id" NOT IN (SELECT id FROM bonus_groups)) THEN
   RAISE EXCEPTION 'Other small bonus groups already exist; combined chance needs review'; END IF;
END $$;

INSERT INTO config."DropItemGroup"
 ("Id","GameConfigurationId","Description","Chance","ItemLevel","ItemType")
SELECT id,'00000001-0001-0000-0000-000000000000',description,chance,level,7 FROM bonus_groups
ON CONFLICT ("Id") DO NOTHING;
INSERT INTO config."DropItemGroupItemDefinition" ("DropItemGroupId","ItemDefinitionId")
SELECT group_id,item_id FROM bonus_items ON CONFLICT DO NOTHING;
INSERT INTO config."GameMapDefinitionDropItemGroup" ("GameMapDefinitionId","DropItemGroupId")
SELECT m."Id",g.id FROM config."GameMapDefinition" m CROSS JOIN bonus_groups g
WHERE m."GameConfigurationId"='00000001-0001-0000-0000-000000000000'
ON CONFLICT DO NOTHING;

SELECT count(*) AS groups,sum("Chance") AS combined_chance
FROM config."DropItemGroup" WHERE "Id" IN (SELECT id FROM bonus_groups);
SELECT count(*) AS gift_variants FROM bonus_items;
SELECT count(*) AS map_links FROM config."GameMapDefinitionDropItemGroup"
WHERE "DropItemGroupId" IN (SELECT id FROM bonus_groups);
\if :apply
COMMIT;
\else
ROLLBACK;
\endif
