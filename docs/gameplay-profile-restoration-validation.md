# Gameplay profile restoration validation — 2026-10-22

## Cause and live audit

The old local client/server commits are ancestors of current main/master. Their
code was retained. The separate live database does not contain all old local
profile settings and spawns. A read-only transaction found:

| Setting | Live before restoration | Proposed restored value |
| --- | --- | --- |
| Reset feature | Disabled; 1500 points; no explicit limit | Enabled; 500 times reset count; limit 50 |
| Recovery stack capacity (eight items) | 3 | 50 |
| Unlimited ammunition | 0 of 23 ranged weapons | All 23 |
| Custom farming definitions | 0 of 9 | 9 of 9 |
| Custom Arena definitions | 0 of 6 | Excluded by owner; no changes |
| Completely blocked Arena arrivals | 2 | 0; nearest clear 3x3 floor |

Existing slower Agility formulas and 500 speed caps are present. Arena terrain
SHA256 is `4351d4d7bf90365ebbf9e8c1fce620604ccce38b497b7d2722e13ff738535a53`,
matching the old full-Arena installation. All 268 installed World7/Object7 files
match the old client source. Installed client bundle marker identifies
`2899f87a7ae5a595ae6842579a9b3d3210480448`.

EXP was deferred by the user in the other chat and remains outside this repair.
The live audit found 50x/1x/1x. No real local database was accessed.

## Executed checks

- Reproduced the missing-profile failure on a seeded disposable fixture.
- Built the current server Release executable and its prerequisite source
  generator in a new worktree from origin/master: zero errors, existing warnings.
- Integration regression passed on `mu_profile_restore_test` at
  `127.0.0.1:55432`: wrong-target refusal, complete dry-run rollback, expected
  restored values, unchanged existing spawns, unchanged unaffected gate fields,
  clear corrected arrivals, repeat-application idempotence, and collision refusal.
  The revised test first failed against the fifteen-spawn script, then passed
  after removal of all six Arena definitions. All Arena spawn rows remain identical.
- Compared fingerprints of every `config` and `data` table before and after.
  Only the five intended configuration tables changed. All player data and other
  configuration tables remained identical.
- Started the freshly built server with every runtime connection context pointing
  to this disposable database and verified shifted listeners before launch.
  Admin HTTP on 18091 returned 200; connect 46406 returned four greeting bytes;
  game port 57901 returned twelve greeting bytes. Database settings were still
  correct after startup.

## Limits and application status

No live database/configuration changes have been applied. The live server passed
its deployment and returned greeting data on 44406 and 55901 during diagnosis.
The repair is a manual transaction, not a deployment hook or startup migration.

Game login, resets, actual potion stacking/pickup, ammunition use, monster
placement and teleport behavior have not been tested in gameplay. The user plays
and reports. The broader existing server suite was not rerun for this SQL-only
maintenance change; the focused database regression and server startup were run.

The owner approved the live repair excluding Arena monster spawns. Take a
backup, apply the reviewed SQL, and restart the server to reload configuration.
The restart disconnects players. Do not claim gameplay verification from greeting
or HTTP checks.
