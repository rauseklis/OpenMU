> Current Season 6 progression uses the exact monster/map assignments in [Monster bonuses and equipment progression](box-reward-rebalance.md). The older 19-gift pool below is historical and is replaced by that one-time startup update. Do not reapply the historical small-bonus SQL to the updated server.

# Server 1: one combined 5% small-gift monster roll

Opt-in configuration for the owner's selected gift pool, enabled only on Server 1
(internal ID 0). `GameServerDefinition.SmallBonusDropsEnabled` defaults to false;
the generator receives each server's own flag, so shared map groups do not enable
bonuses on other servers. Changing this flag requires a server reload. Each ordinary monster
with at least one loot slot and no applicable guaranteed drop group gets one
additional roll. Its chance is 5% for **the whole pool**, with at most one extra
gift. Multiple normal loot slots and premium drop bonuses do not multiply it.
Normal loot and the contents of gifts retain their existing rules. Guaranteed
boss/event rewards and non-monster objects do not get this extra roll.

| Definition | Level | Gift variants |
| --- | --- | --- |
| 14/11 | 0–7 | Box of Luck, Star of Sacred Birth, Firecracker, Heart of Love, Olive of Love, Silver Medal, Gold Medal, Box of Heaven |
| 12/32–34 | 0 | Red, Green, Blue Ribbon Box |
| 14/32–34 | 0 | Pink, Red, Blue Chocolate Box |
| 14/45 | 0 | Pumpkin of Luck |
| 14/51 | 0 | Christmas Star |
| 14/63 | 0 | Firecracker |
| 14/84 | 0 | Cherry Blossom Play Box |
| 14/99 | 0 | Christmas Firecracker |

All 19 variants have equal shares: `0.05 / 19`, approximately 0.26316% per
eligible kill for a specific variant. This is a probability, not a guarantee of
one item every twenty kills. The two Firecracker definitions are separate items.
Kundun boxes (14/11 levels 8–12) are excluded.

`SpecialItemType.SmallBonus` (stored value 7) partitions these groups from normal
weighted loot. The extra roll uses absolute group probabilities. Its item-level
and monster filters still apply. Configuration editors must keep the applicable
bonus chances summing to 0.05; do not also enable `DropsFromMonsters` on these
definitions, which would add another ordinary drop source.

## Manual configuration

`deploy/live/small-bonus-drops.sql` links nine stable-ID groups to all maps of
configuration `00000001-0001-0000-0000-000000000000`. It checks all 19 variants,
refuses conflicting groups or links and preserves existing loot, spawns, rates
and player data. It additionally enables only Server 1; other servers keep their flags. Reapplying is idempotent. It defaults to rollback:

```sh
psql -X -v expected_database=<selected-database> -f deploy/live/small-bonus-drops.sql
# After inspection, approval and a backup:
psql -X -v expected_database=<selected-database> -v apply=true -f deploy/live/small-bonus-drops.sql
```

Deploy SmallBonus-aware code before applying live configuration; reload the
server afterward. Keep a pre-change backup without pruning existing backups.
Deployment does not apply this script automatically. To roll back, unlink and
remove only the nine groups and disable the opted-in server after approval, then reload. No client update or
packet change is required.

## Validation

`DropGeneratorTest` covers the 5% boundary, 19 shares on a deterministic 10,000
roll grid, crowded normal tables, multiple normal slots, explicit gift levels,
guaranteed rewards, object kinds, monster levels and reusable pool cleanup.

The maintenance test requires a fresh disposable `mu_small_bonus_test` on
`127.0.0.1:55432`, role `muvalidation`, and `psql` on PATH:

```sh
python -m unittest discover -s tests/maintenance -p test_small_bonus_drops.py -v
```

It hashes every configuration/player table, checks preview rollback, rejects a
wrong database, verifies the exact four changed tables, 19 variants, nine groups,
5% total, map coverage, idempotence and conflict refusal. Manual gameplay should
check gift appearance and pickup; automated tests do not log in to the game.

Validated on October 22, 2026: 57 focused loot/premium/jewel tests and all 1,574
non-PostgreSQL tests passed before the per-server opt-in change. All 58 focused tests passed for the final server-scoped implementation, including
default-disabled behavior and isolated opt-in with shared map groups. The maintenance integration passed on the disposable
fixture. Startup built with zero errors; the isolated server migrated the fixture,
loaded its configuration, returned 4/12-byte connect/game greetings and HTTP 200
on the admin endpoint. Persisted configuration contained nine groups and 19
variants, with a floating-point sum of 0.05000000000000001. An initial unrestricted
suite had empty-connection errors in unrelated PostgreSQL fixtures and was stopped;
it is not counted as passing. Full auction/database integration and gameplay were
not exercised by this task.
