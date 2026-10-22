# Devias farming spots — proposal

Add one concentrated group of ten monsters near each of the Devias 1, 2 and 3
warp areas. These are three locations on the same Devias map (map number 2).

| Area | Coordinates | Monster | Added monsters |
| --- | --- | --- | --- |
| Devias 1 | 200,84 | Worm | 10 |
| Devias 2 | 50,42 | Ice Monster | 10 |
| Devias 3 | 225,200 | Elite Yeti | 10 |

Each center has a clear 9x9 hunting area. Read-only checks against the live map
found a walkable route from its warp area: 75, 42 and 29 cardinal steps,
respectively. Locations are at least twelve tiles from NPCs or existing point
spawns and ten tiles from arrival gates. Existing random spawns can still wander
through these areas. Monsters retain their normal movement and respawn rules.

The two existing concentrated Elite Yeti spots remain intact. All existing spawns,
including Arena, stay unchanged. No EXP, drop, reset or item changes are included.

`deploy/live/add-devias-farming-spots.sql` is a manual maintenance tool, not a
startup update. It requires `expected_database`, defaults to rollback, rejects
blocked terrain and conflicting IDs, and can be rerun without duplicating mobs.
Show this table to the owner and obtain live-data approval before applying.
Take a backup and reload the server configuration after application; players
briefly disconnect. No client update is required.

Before release, run `tests/maintenance/test_devias_farming_spots.py` against a
fresh Season 6 disposable database named `mu_devias_spots_test` at
`127.0.0.1:55432`, role `muvalidation`, with psql on PATH. Verify rollback,
wrong-target refusal, safe-zone refusal, preservation of every existing spawn and
all other tables, idempotence and conflict refusal. Actual in-game reachability,
farming feel and monster placement remain manual checks for the owner.
