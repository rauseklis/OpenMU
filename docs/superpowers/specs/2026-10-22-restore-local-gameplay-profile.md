# Restore the old local gameplay profile

The October 22 live database was initialized independently of the brother's old
local database. Earlier code changes are present on master/main, including the
Arena assets, buff display, king maces, slower Agility speed formulas, the 500
speed caps, Dark Side cooldown and two-handed damage bonus. A client update does
not transfer settings or custom monster spawns from another database.

The read-only live audit found resets disabled (1500 points, no configured limit),
recovery stacks of three, ammunition consumption enabled, and none of the fifteen
custom spawn definitions. The repair restores the final old policy: enabled
resets at 500 times reset count, maximum 50 resets; eight recovery items stacking
to 50; unlimited ranged ammunition; and nine farming spots. The owner explicitly
excluded Arena monster spawns on October 22; leave all existing Arena spawns
unchanged and add none. SD/complex stacks remain three. Two live Arena
arrival gates point entirely into walls; restore the old installer's correction
by moving only blocked arrivals to the nearest clear 3x3 floor area.

This is an explicit operator maintenance operation, never an automatic migration
or deployment hook. It requires the named database, defaults to rollback, checks
map/monster references and all spawn terrain cells, and refuses conflicting
existing spawn definitions. All changes commit together. Rerunning is safe.

EXP is excluded: the user deferred it in the other October 22 chat. The audit found
50x/1x/1x rather than the historical 50x/500x/9999x; any EXP change is a separate
decision. Player accounts, characters, inventories, current items, other settings,
terrain and already restored speed formulas are outside this operation. Gate
identities and destinations remain intact; only blocked Arena arrival coordinates
change.

Before production use: reproduce the missing profile on a seeded disposable
database, verify rollback and wrong-target refusal, apply and verify the profile,
verify all player data and unrelated configuration are unchanged, rerun to check
idempotence, and test refusal of a conflicting spawn. Verify unaffected gates are
identical and corrected arrivals have a clear 3x3 area. Start a disposable server
on shifted ports and check readiness. Gameplay remains for the user to verify.

The owner approved this repair on October 22, excluding Arena monster spawns.
Apply only that approved scope. Take a live backup with the existing approved backup mechanism,
apply the script during a maintenance window, restart the game server to reload
configuration (players disconnect), and check readiness and database values.
