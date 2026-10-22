-- Manual maintenance; defaults to a complete rollback. Never run automatically.
-- psql -X -v expected_database=<database> [-v apply=true] -f add-devias-farming-spots.sql
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
   RAISE EXCEPTION 'Database does not match explicitly selected maintenance target'; END IF;
END $$;

CREATE TEMP TABLE devias_spots (
 id uuid PRIMARY KEY, label text, monster_number integer,
 x smallint, y smallint, quantity smallint
) ON COMMIT DROP;
INSERT INTO devias_spots VALUES
 ('3be31bf7-2d69-50f4-a4aa-7d79c8a00a5a','Devias 1',24,200,84,8),
 ('11c0cd30-9e1b-5aa6-94bb-a28e3f9bbbf6','Devias 2',22,50,42,8),
 ('40a097b1-69f3-5539-bcc4-39c782efeeaa','Devias 3',20,225,200,8);

CREATE TEMP TABLE resolved_devias_spots ON COMMIT DROP AS
SELECT s.*,g."Id" AS map_id,m."Id" AS monster_id,g."TerrainData" AS terrain
FROM devias_spots s JOIN config."GameMapDefinition" g ON g."Number" = 2
 AND g."GameConfigurationId" = '00000001-0001-0000-0000-000000000000'
JOIN config."MonsterDefinition" m ON m."GameConfigurationId" = g."GameConfigurationId"
 AND m."Number" = s.monster_number AND m."ObjectKind" = 0;

DO $$
BEGIN
 IF (SELECT count(*) FROM resolved_devias_spots) <> 3 THEN
   RAISE EXCEPTION 'Expected exactly three resolved Devias spots'; END IF;
 IF EXISTS (SELECT FROM resolved_devias_spots WHERE octet_length(terrain) <> 65539) THEN
   RAISE EXCEPTION 'Unexpected Devias terrain format'; END IF;
 IF EXISTS (SELECT FROM config."GameServerDefinition" server,
   (SELECT DISTINCT map_id FROM resolved_devias_spots) map
   WHERE server."GameConfigurationId" = '00000001-0001-0000-0000-000000000000'
     AND NOT EXISTS (SELECT FROM config."GameServerConfigurationGameMapDefinition" j
       WHERE j."GameServerConfigurationId" = server."ServerConfigurationId" AND j."GameMapDefinitionId" = map.map_id)) THEN
   RAISE EXCEPTION 'Devias is not assigned to every server using this configuration'; END IF;
 IF EXISTS (SELECT FROM resolved_devias_spots s,
   LATERAL generate_series(-4,4) dx,LATERAL generate_series(-4,4) dy
   WHERE get_byte(s.terrain,3+(s.y+dy)*256+s.x+dx) <> 0) THEN
   RAISE EXCEPTION 'Devias spot requires a clear 9x9 hunting area'; END IF;
 IF EXISTS (SELECT FROM resolved_devias_spots s JOIN config."MonsterSpawnArea" a ON a."GameMapId" = s.map_id
   JOIN config."MonsterDefinition" m ON m."Id" = a."MonsterDefinitionId"
   WHERE a."Id" NOT IN (SELECT id FROM devias_spots)
     AND (m."ObjectKind" = 1 OR (m."ObjectKind" = 0 AND a."X1" = a."X2" AND a."Y1" = a."Y2"))
     AND power(greatest(a."X1"-s.x,0,s.x-a."X2"),2)
       +power(greatest(a."Y1"-s.y,0,s.y-a."Y2"),2) < 144) THEN
   RAISE EXCEPTION 'Devias spot is too close to an NPC or existing concentrated spawn'; END IF;
 IF EXISTS (SELECT FROM resolved_devias_spots s JOIN config."ExitGate" g ON g."MapId" = s.map_id
   WHERE power(greatest(g."X1"-s.x,0,s.x-g."X2"),2)
     +power(greatest(g."Y1"-s.y,0,s.y-g."Y2"),2) < 100) THEN
   RAISE EXCEPTION 'Devias spot is too close to an arrival gate'; END IF;
 IF EXISTS (SELECT FROM resolved_devias_spots s JOIN config."MonsterSpawnArea" a ON a."Id" = s.id
   WHERE (a."MonsterDefinitionId",a."GameMapId",a."X1",a."Y1",a."X2",a."Y2",a."Quantity",a."Direction",a."SpawnTrigger",a."WaveNumber")
     IS DISTINCT FROM (s.monster_id,s.map_id,s.x,s.y,s.x,s.y,s.quantity,0,0,0)
     OR a."MaximumHealthOverride" IS NOT NULL) THEN
   RAISE EXCEPTION 'An existing Devias spot differs; manual review required'; END IF;
END $$;


-- Change only the nine explicitly identified farming spots restored by PR #14.
CREATE TEMP TABLE previous_farming_spots (
 id uuid PRIMARY KEY, map_number integer, monster_number integer, x smallint, y smallint
) ON COMMIT DROP;
INSERT INTO previous_farming_spots VALUES
 ('98b255d9-72d5-5438-ac69-ec0e97115142',4,36,193,108),
 ('9a20d0f9-cf0c-56b5-92f8-38bf1d4ce9e2',4,41,83,213),
 ('89a3df7e-c0ac-5f06-b351-c0c8853bb229',4,40,30,17),
 ('6340180e-4c46-5aaf-9019-c30795a326a3',4,40,54,88),
 ('d06a3c06-7cc5-50e7-8137-b9742b471935',7,51,221,88),
 ('45c1a50f-21d3-58de-8e7b-82332d5520d8',7,48,28,170),
 ('4b62b3d5-5392-5b24-b83c-d597f653bbee',8,60,134,72),
 ('0cb045a1-e83b-5a7c-9b4f-36520dd81877',33,308,115,17),
 ('442e5518-aedd-5204-ad14-97520d28b50b',37,351,45,230);
DO $$
BEGIN
 IF (SELECT count(*) FROM previous_farming_spots p
   JOIN config."MonsterSpawnArea" a ON a."Id"=p.id
   JOIN config."GameMapDefinition" g ON g."Id"=a."GameMapId"
   JOIN config."MonsterDefinition" m ON m."Id"=a."MonsterDefinitionId"
   WHERE g."GameConfigurationId"='00000001-0001-0000-0000-000000000000'
     AND m."GameConfigurationId"=g."GameConfigurationId"
     AND (g."Number",m."Number",a."X1",a."Y1",a."X2",a."Y2",a."Direction",a."SpawnTrigger",a."WaveNumber")
       = (p.map_number,p.monster_number,p.x,p.y,p.x,p.y,0,0,0)
     AND a."Quantity" IN (8,10) AND a."MaximumHealthOverride" IS NULL) <> 9 THEN
   RAISE EXCEPTION 'Expected nine unchanged previous farming spots with quantity eight or ten'; END IF;
END $$;
UPDATE config."MonsterSpawnArea" a SET "Quantity"=8
FROM previous_farming_spots p WHERE a."Id"=p.id;

INSERT INTO config."MonsterSpawnArea"
 ("Id","MonsterDefinitionId","GameMapId","X1","Y1","X2","Y2","Direction","Quantity","SpawnTrigger","WaveNumber","MaximumHealthOverride")
SELECT id,monster_id,map_id,x,y,x,y,0,quantity,0,0,NULL FROM resolved_devias_spots
ON CONFLICT ("Id") DO NOTHING;
SELECT label,monster_number,x,y,quantity FROM devias_spots ORDER BY label;
SELECT 'Nine previous custom farming spots now have eight monsters each' AS previous_spots;
\if :apply
COMMIT;
\else
ROLLBACK;
\endif
