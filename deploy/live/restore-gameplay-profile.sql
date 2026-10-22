-- Explicit maintenance tool; never run by deployments or migrations.
-- psql -v expected_database=... [-v apply=true] -f restore-gameplay-profile.sql
-- Without apply=true the complete operation is checked, then rolled back.
\set ON_ERROR_STOP on
\if :{?apply}
\else
\set apply false
\endif
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
SELECT set_config('mu.expected_database', :'expected_database', true);
DO $$
BEGIN
    IF current_database() <> current_setting('mu.expected_database') THEN
        RAISE EXCEPTION 'Database does not match explicitly selected maintenance target';
    END IF;
    PERFORM FROM config."GameConfiguration"
      WHERE "Id" = '00000001-0001-0000-0000-000000000000' FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'Expected Season 6 configuration is missing'; END IF;
END $$;

-- Match the old full-Arena installer: relocate only completely blocked arrivals
-- to the nearest clear 3x3 floor, preserving gate identities and destinations.
DO $$
DECLARE arena_id uuid; terrain bytea; gate record; target_x integer; target_y integer;
BEGIN
    SELECT "Id","TerrainData" INTO STRICT arena_id,terrain
    FROM config."GameMapDefinition" WHERE "Number" = 6
      AND "GameConfigurationId" = '00000001-0001-0000-0000-000000000000';
    IF octet_length(terrain) <> 65539 THEN RAISE EXCEPTION 'Unexpected Arena terrain format'; END IF;
    FOR gate IN SELECT * FROM config."ExitGate" g WHERE g."MapId" = arena_id
      AND NOT EXISTS (SELECT FROM generate_series(g."X1"::integer,g."X2"::integer) x,
        generate_series(g."Y1"::integer,g."Y2"::integer) y
        WHERE get_byte(terrain,3+y*256+x) IN (0,1)) ORDER BY g."Id"
    LOOP
      SELECT x,y INTO STRICT target_x,target_y FROM generate_series(1,254) x,
        generate_series(1,254) y
      WHERE get_byte(terrain,3+y*256+x) IN (0,1)
        AND NOT EXISTS (SELECT FROM generate_series(-1,1) dx,generate_series(-1,1) dy
          WHERE get_byte(terrain,3+(y+dy)*256+x+dx) NOT IN (0,1))
      ORDER BY (x-gate."X1")*(x-gate."X1")+(y-gate."Y1")*(y-gate."Y1"),y,x LIMIT 1;
      UPDATE config."ExitGate" SET "X1" = target_x,"X2" = target_x,"Y1" = target_y,"Y2" = target_y
      WHERE "Id" = gate."Id" AND "MapId" = arena_id;
      RAISE NOTICE 'Arena arrival % moved from (%,%) to (%,%)',gate."Id",gate."X1",gate."Y1",target_x,target_y;
    END LOOP;
    IF EXISTS (SELECT FROM (
      SELECT "X1","Y1","X2","Y2" FROM config."ExitGate" WHERE "MapId" = arena_id
      UNION ALL SELECT "X1","Y1","X2","Y2" FROM config."EnterGate" WHERE "GameMapDefinitionId" = arena_id
    ) g WHERE NOT EXISTS (SELECT FROM generate_series(g."X1"::integer,g."X2"::integer) x,
      generate_series(g."Y1"::integer,g."Y2"::integer) y
      WHERE get_byte(terrain,3+y*256+x) IN (0,1))) THEN
      RAISE EXCEPTION 'Arena still contains a blocked gate'; END IF;
END $$;

-- Restore the final October 16 policy; preserve every other reset option.
DO $$
DECLARE changed integer;
BEGIN
    UPDATE config."PlugInConfiguration"
    SET "IsActive" = true,
        "CustomConfiguration" = jsonb_set(jsonb_set("CustomConfiguration"::jsonb,
          '{PointsPerReset}', '500'), '{ResetLimit}', '50')::text
    WHERE "GameConfigurationId" = '00000001-0001-0000-0000-000000000000'
      AND "TypeId" = '6a9d585d-79d7-4674-b6ea-7e87392fa501'
      AND "CustomConfiguration"::jsonb @>
        '{"ResetStats":true,"MultiplyPointsByResetCount":true,"ReplacePointsPerReset":true}';
    GET DIAGNOSTICS changed = ROW_COUNT;
    IF changed <> 1 THEN RAISE EXCEPTION 'Expected one compatible reset configuration'; END IF;
    IF (SELECT count(*) FROM config."PlugInConfiguration"
        WHERE "GameConfigurationId" = '00000001-0001-0000-0000-000000000000'
          AND "TypeId" IN ('90b35404-aade-4f22-b5d2-4cd59b8bb4c8','79f2c2c2-2e4c-4f4b-8a74-4227d1209d27')
          AND "IsActive") <> 2 THEN RAISE EXCEPTION 'Reset commands must already be enabled'; END IF;

    UPDATE config."ItemDefinition" SET "Durability" = 50
    WHERE "GameConfigurationId" = '00000001-0001-0000-0000-000000000000'
      AND "Group" = 14 AND "Number" IN (0,1,2,3,4,5,6,8);
    GET DIAGNOSTICS changed = ROW_COUNT;
    IF changed <> 8 THEN RAISE EXCEPTION 'Expected eight recovery item definitions'; END IF;
    IF (SELECT count(*) FROM config."ItemDefinition"
        WHERE "GameConfigurationId" = '00000001-0001-0000-0000-000000000000'
          AND "Group" = 14 AND "Number" BETWEEN 35 AND 40 AND "Durability" = 3) <> 6
      THEN RAISE EXCEPTION 'Expected SD/complex stacks of three'; END IF;

    UPDATE config."ItemBasePowerUpDefinition" p SET "BaseValue" = 0
    FROM config."ItemDefinition" i
    WHERE i."Id" = p."ItemDefinitionId"
      AND i."GameConfigurationId" = '00000001-0001-0000-0000-000000000000'
      AND i."Group" = 4 AND NOT i."IsAmmunition"
      AND p."TargetAttributeId" = '4cd0b1ae-3fe0-499b-b421-4e4078182090';
    GET DIAGNOSTICS changed = ROW_COUNT;
    IF changed = 0 OR EXISTS (
      SELECT FROM config."ItemDefinition" i
      WHERE i."GameConfigurationId" = '00000001-0001-0000-0000-000000000000'
        AND i."Group" = 4 AND NOT i."IsAmmunition"
        AND NOT EXISTS (SELECT FROM config."ItemBasePowerUpDefinition" p
          WHERE p."ItemDefinitionId" = i."Id"
            AND p."TargetAttributeId" = '4cd0b1ae-3fe0-499b-b421-4e4078182090' AND p."BaseValue" = 0))
      THEN RAISE EXCEPTION 'Every ranged weapon needs zero ammunition consumption'; END IF;
END $$;

CREATE TEMP TABLE restored_spots (
    id uuid PRIMARY KEY, map_number integer, monster_number integer,
    x1 smallint, y1 smallint, x2 smallint, y2 smallint, quantity smallint
) ON COMMIT DROP;
INSERT INTO restored_spots VALUES
('98b255d9-72d5-5438-ac69-ec0e97115142',4,36,193,108,193,108,8),
('9a20d0f9-cf0c-56b5-92f8-38bf1d4ce9e2',4,41,83,213,83,213,8),
('89a3df7e-c0ac-5f06-b351-c0c8853bb229',4,40,30,17,30,17,8),
('6340180e-4c46-5aaf-9019-c30795a326a3',4,40,54,88,54,88,8),
('d06a3c06-7cc5-50e7-8137-b9742b471935',7,51,221,88,221,88,8),
('45c1a50f-21d3-58de-8e7b-82332d5520d8',7,48,28,170,28,170,8),
('4b62b3d5-5392-5b24-b83c-d597f653bbee',8,60,134,72,134,72,8),
('0cb045a1-e83b-5a7c-9b4f-36520dd81877',33,308,115,17,115,17,8),
('442e5518-aedd-5204-ad14-97520d28b50b',37,351,45,230,45,230,8);

CREATE TEMP TABLE resolved_spots ON COMMIT DROP AS
SELECT s.*,g."Id" AS map_id,m."Id" AS monster_id,g."TerrainData" AS terrain
FROM restored_spots s JOIN config."GameMapDefinition" g ON g."Number" = s.map_number
  AND g."GameConfigurationId" = '00000001-0001-0000-0000-000000000000'
JOIN config."MonsterDefinition" m ON m."Number" = s.monster_number
  AND m."GameConfigurationId" = g."GameConfigurationId" AND m."ObjectKind" = 0;
DO $$
BEGIN
    IF (SELECT count(*) FROM resolved_spots) <> 9 THEN
      RAISE EXCEPTION 'Expected exactly nine resolved map/monster pairs'; END IF;
    IF EXISTS (SELECT FROM resolved_spots s
      WHERE octet_length(s.terrain) <> 65539 OR NOT EXISTS (
        SELECT FROM config."GameServerConfigurationGameMapDefinition" j
        WHERE j."GameMapDefinitionId" = s.map_id)) THEN
      RAISE EXCEPTION 'Spawn map terrain or server assignment is missing'; END IF;
    IF EXISTS (SELECT FROM resolved_spots s,
      LATERAL generate_series(s.x1::integer,s.x2::integer) x,
      LATERAL generate_series(s.y1::integer,s.y2::integer) y
      WHERE get_byte(s.terrain,3+y*256+x) <> 0) THEN
      RAISE EXCEPTION 'Restored spawn location contains blocked terrain or safe zone'; END IF;
    -- Refuse collisions rather than overwrite someone else's edited spawn.
    IF EXISTS (SELECT FROM resolved_spots s JOIN config."MonsterSpawnArea" a ON a."Id" = s.id
      WHERE (a."MonsterDefinitionId",a."GameMapId",a."X1",a."Y1",a."X2",a."Y2",a."Quantity",a."Direction",a."SpawnTrigger",a."WaveNumber")
        IS DISTINCT FROM (s.monster_id,s.map_id,s.x1,s.y1,s.x2,s.y2,s.quantity,0,0,0)
        OR a."MaximumHealthOverride" IS NOT NULL) THEN
      RAISE EXCEPTION 'An existing custom spawn differs; manual review required'; END IF;
END $$;
INSERT INTO config."MonsterSpawnArea"
  ("Id","MonsterDefinitionId","GameMapId","X1","Y1","X2","Y2","Direction","Quantity","SpawnTrigger","WaveNumber","MaximumHealthOverride")
SELECT id,monster_id,map_id,x1,y1,x2,y2,0,quantity,0,0,NULL FROM resolved_spots
ON CONFLICT ("Id") DO NOTHING;

SELECT 'Resets: enabled, 500 x reset count, limit 50; recovery stacks: 50; ranged ammunition: unlimited; farming spawn definitions: 9; Arena monster spawns: unchanged; Arena arrivals: clear' AS proposed_profile;
\if :apply
COMMIT;
\else
ROLLBACK;
\endif
