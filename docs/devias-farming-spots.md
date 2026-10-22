# Custom farming spots: eight monsters per group

The owner requested eight monsters at every custom farming spot on October 22.
Add the three Devias groups below and reduce the nine groups previously restored
by PR #14 from ten to eight. These twelve stable IDs are the complete scope;
standard spawns, including existing ten-monster Elite Yeti groups, stay intact.

| Area | Coordinates | Monster | Quantity |
| --- | --- | --- | --- |
| Devias 1 | 200,84 | Worm | 8 |
| Devias 2 | 50,42 | Ice Monster | 8 |
| Devias 3 | 225,200 | Elite Yeti | 8 |
| Lost Tower 1 | 193,108 | Shadow | 8 |
| Lost Tower 3 | 83,213 | Death Cow | 8 |
| Lost Tower 6 | 30,17 | Death Knight | 8 |
| Lost Tower 7 | 54,88 | Death Knight | 8 |
| Atlans 2 | 221,88 | Great Bahamut | 8 |
| Atlans 3 | 28,170 | Lizard King | 8 |
| Tarkan | 134,72 | Bloody Wolf | 8 |
| Aida | 115,17 | Death Tree | 8 |
| Kanturu | 45,230 | Splinter Wolf | 8 |

Devias 1, 2 and 3 are locations on the same map (number 2). Each new center has
a clear 9x9 hunting area. Read-only live terrain checks found walkable routes
from the corresponding warp areas: 75, 42 and 29 cardinal steps. Centers are at
least twelve tiles from NPCs or existing point spawns and ten from arrival gates.
Existing random monsters can wander through these areas. Movement and respawn
rules are unchanged. Arena, EXP, drops, resets and item settings stay unchanged.

`deploy/live/add-devias-farming-spots.sql` applies all twelve changes in one
transaction. It requires `expected_database`, defaults to rollback, checks the
nine previous definitions (including their identities, coordinates and old counts
of eight or ten), checks Devias clearance, and refuses conflicting definitions.
Rerunning is safe. The older restoration tool now creates the nine previous
spots with eight monsters on fresh fixtures too; it does not convert existing
ten-monster rows. Use the new tool for that conversion.

The owner explicitly requested this scope. Take a backup before live application
and reload the server configuration afterward; players briefly disconnect.
No client update is required. Nothing applies automatically during deployment.

The focused tests use fresh disposable `mu_devias_spots_test` and
`mu_profile_restore_test` databases at `127.0.0.1:55432`, role `muvalidation`, with
psql on PATH. They cover ten-to-eight conversion, exactly three insertions,
preservation of every other spawn and table, rollback, wrong-target refusal,
blocked terrain, idempotence and conflicting new/previous definitions. Actual
in-game routes, farming feel and monster placement remain manual user checks.
