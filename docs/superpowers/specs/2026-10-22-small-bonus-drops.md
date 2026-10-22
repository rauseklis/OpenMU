# Combined small-bonus monster drops

The selected small gift items currently have no ordinary monster drops. Add one
independent 5% roll per eligible monster kill, with at most one extra gift. Normal
loot rolls and guaranteed boss rewards keep their existing probabilities.

## Scope and probability

The pool contains 19 equally likely variants: Box of Luck, Star of Sacred Birth,
Firecracker, Heart of Love, Olive of Love, Silver Medal, Gold Medal, Box of Heaven,
three Ribbons, three Chocolates, Pumpkin of Luck, Christmas Star, the separate
Firecracker item, Cherry Blossom Play Box, and Christmas Firecracker.

Eight groups select levels 0–7 of item 14/11; a ninth group selects the other
11 definitions. The first eight each have chance 0.05/19; the ninth has chance
11*0.05/19. Thus a particular variant has probability 0.05/19 per eligible kill.
Box of Kundun and rewards inside any box are outside this change.

## Runtime behavior

Append `SpecialItemType.SmallBonus` without renumbering existing values. Separate
these groups from ordinary weighted loot. After ordinary loot, draw once over
their absolute chances, without premium multipliers or normalization against
ordinary loot. Preserve explicit item levels and normal eligibility filters.
Only monsters with a positive drop count and no applicable guaranteed reward
groups qualify; destructibles and other NPC kinds do not. Multiple normal loot
slots do not produce multiple bonus rolls. Empty pools consume no random draw.

## Configuration and rollout

Use a guarded, transactional SQL script with explicit game-configuration ID and
stable group IDs to link the nine groups to its maps. Preview and validate the
exact pool, total chance and existing configuration before applying. Reapplying
the script must be harmless. Existing item flags, loot tables, characters,
experience rates and spawns stay unchanged. This opt-in configuration does not
change a fresh installation automatically.

Test boundaries, crowded ordinary tables, multiple drop slots, guaranteed
rewards, NPC exclusions, level filters and reuse. Apply the configuration twice
to a disposable database, start the new server on shifted ports, inspect the
persisted groups and verify handshakes. Back up live without pruning backups,
then deploy code and the selected configuration. No client update is required.

## Approved server scope

The owner selected only Server 1. Add an opt-in boolean to GameServerDefinition,
default false through an additive migration. Each server passes its flag to the
drop generator. The maintenance transaction enables only internal server ID 0;
shared map tables stay common. No other server is enabled and no EXP rate changes.
