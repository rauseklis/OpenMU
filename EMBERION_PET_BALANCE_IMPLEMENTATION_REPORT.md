# EMBERION Pet Balance — Implementation Report

Date: 29 October 2026. Branch: `codex/pet-durability-balance`. Base: `6abd816e8edc87fea6b3254645ac6c3215adf8d4`.

**Implemented locally for x5 and x50. No production deployment or production data mutation.** This follows the newly attached approved 3/6/24/72-hour targets, not the earlier research report's provisional caps/repair/rental recommendations. No x500 world is introduced.

## 1. Implemented behavior

Ordinary pets still wear from counted incoming `HitInfo.HealthDamage`; moving, attacking, missing and standing idle add no new ordinary pet fee. Per-pet divisors replace the generic equipment divisor only for the audited pets. No fixed hourly drain or arbitrary wear cap was added. Fractional double-precision durability is retained.

Angel, Imp, Uniria, Unicorn, Panda, Skeleton, Rudolf, Dinorant, Demon and Spirit remain disposable at zero and cannot be normally repaired. All four Fenrir colors, Horse and Raven remain at zero. Their active attributes are refreshed off; mount appearance and mount skill checks enforce unusability. Repair refreshes attributes and appearance. Horse/Raven level and experience are never reduced merely by exhaustion.

Raven preserves the existing random offensive-item selector and owner/Raven successful callback mechanism. It does **not** switch to damage-based wear or an activation/hourly fee. Its command entry, single-hit/ranged-hit execution and offline handling reject zero/expired pets, including the fractional final decrement. Commands can resume after a valid Zen repair. A pending Horse/Fenrir skill cannot continue applying damage or special effects after its required mount becomes unusable.

## 2. Exact coefficients and expected lifespan

All full-Life values remain 255. No pet definition/bonus table was edited. Damage coefficients are computed without premature rounding:

```text
K = 50 × 3600 × approvedTargetHours / 255
ordinary/Horse base loss = counted HP damage / K
Raven K = (2 + 1.28) × (1/3) × 3600 × 72 / 255
        = 1111.341176470588 selected successful callbacks per Life
Raven base loss, only when selected = 1 / K
final loss = base loss × clamp(1 − displayedProtectionPercent / 100, 0, 1)
```

The main table is for **ordinary/drop/crafted versions** without master protection, repairs or Talisman. Cash Shop rentals are the separate exception in section 5.

| Pet | ID | Divisor | Normal Life/h | Light life h | Normal life h | Heavy life h |
| --- | --- | --- | --- | --- | --- | --- |
| Guardian Angel | 13,0 | 2,117.647058824 HP/Life | 85.000000 | 15 | 3 | 0.75 |
| Imp / Satan | 13,1 | 2,117.647058824 HP/Life | 85.000000 | 15 | 3 | 0.75 |
| Uniria | 13,2 | 2,117.647058824 HP/Life | 85.000000 | 15 | 3 | 0.75 |
| Unicorn | 13,106 | 2,117.647058824 HP/Life | 85.000000 | 15 | 3 | 0.75 |
| Panda | 13,80 | 4,235.294117647 HP/Life | 42.500000 | 30 | 6 | 1.5 |
| Skeleton | 13,123 | 4,235.294117647 HP/Life | 42.500000 | 30 | 6 | 1.5 |
| Rudolf | 13,67 | 4,235.294117647 HP/Life | 42.500000 | 30 | 6 | 1.5 |
| Dinorant | 13,3 | 4,235.294117647 HP/Life | 42.500000 | 30 | 6 | 1.5 |
| Demon | 13,64 | 4,235.294117647 HP/Life | 42.500000 | 30 | 6 | 1.5 |
| Spirit of Guardian | 13,65 | 4,235.294117647 HP/Life | 42.500000 | 30 | 6 | 1.5 |
| Fenrir red | 13,37 | 16,941.176470588 HP/Life | 10.625000 | 120 | 24 | 6.0 |
| Fenrir black | 13,37 | 16,941.176470588 HP/Life | 10.625000 | 120 | 24 | 6.0 |
| Fenrir blue | 13,37 | 16,941.176470588 HP/Life | 10.625000 | 120 | 24 | 6.0 |
| Fenrir gold | 13,37 | 16,941.176470588 HP/Life | 10.625000 | 120 | 24 | 6.0 |
| Dark Horse | 13,4 | 50,823.529411765 HP/Life | 3.541667 | 360 | 72 | 18.0 |
| Dark Raven | 13,5 | 1,111.341176471 selected hits/Life | 3.541667 | 144 | 72 | 36.0 |

Incoming profiles match the research: light 10 counted HP/s, normal 50, heavy 200. These values are after mitigation/HP-SD splitting and refer to the existing durability hook, not all HP changes. Same counted damage gives the same formula on x5 and x50. Defensive bonuses, SD, misses and actual monster/character builds change realized life.

Raven assumptions are owner successful callbacks 1/2/4 per second, Raven activations 0.5/1/2 per second, 70% single/30% triple hits, 80% individual success, and 1/3 selection probability with the three eligible equipment choices. Thus Raven's light/heavy lifetime is 144/36 hours, unlike Horse's damage-based 360/18. Different equipment changes selection odds: Raven as the only eligible offensive item wears roughly three times faster. The 72-hour value is an expected normal-profile calibration, not a universal expiration time.

At normal farming ordinary 3-hour/6-hour pets lose 85/42.5 Life/hour; Fenrir 10.625 and DL pets 3.541667. A 3-hour normal pet will not survive an 8-hour normal-damage session. That follows the approved targets; the implementation does not secretly stretch them to the free/Silver/Gold session lengths. At no incoming counted damage, ordinary damage pets do not wear at all. No session length, reset cap, EXP, VIP, drop or event setting was changed.

## 3. Exhaustion, repair and destruction

Fractional zero handling is independent of integer packet rounding. The final `0.0000001 → 0` transition executes retention/destruction and state refresh even when both values display as zero. It does not rely on the durability packet's integer-change boolean. Retained items preserve their original object, color/options, level and accumulated XP.

Fenrir repair is routed through the existing Bless handler. A valid damaged Fenrir must be in the owner's normal inventory (unequipped, as before); the source must be a real owned Bless with at least one charge, and a real Creation must also be present. All conditions are checked before either charge changes, under the normal player persistence lock. Exactly one Bless and one Creation are consumed; repair is guaranteed and restores 255. Full-Life, missing-jewel, wrong-state, stale-slot or unowned targets consume neither. The old single-Bless Fenrir branch is explicitly rejected. Other Bless upgrading/repair-target behavior and Creation uses remain unchanged.

Horse/Raven continue using the existing Zen repair action and the **unchanged** `ItemPriceCalculator.CalculateRepairPrice` formula. A successful repair additionally refreshes pet functionality. Existing NPC/slot restrictions and Repair All/offline repair scope remain unchanged; Horse is not newly added to offline auto-repair. Ordinary pets and Fenrir remain ineligible for ordinary Zen repair.

## 4. Master percentage correction

Only durability skills **300, 305, 312, 578, 583, 590 and 535** are excluded from attaching the erroneous raw duration multiplier. Their existing formulas and displayed values are not rewritten. At wear time the current learned skill entries supply `CalculateDisplayValue()` as a percentage:

* 300/578: ordinary equipment category.
* 305/583: accessories.
* 312/590: ordinary pet category.
* 535: trainable Horse/Raven category.

The loss is multiplied by `1 − percent/100`, clamped to [0,1]. +10%/+20% means 10%/20% less consumption, not a 10×/20× divisor. A 3-hour pet at +20% therefore lasts 3.75 normal-profile hours. Level changes and resets are read directly from current learned entries; rebuilding the player on login cannot accumulate raw duration wrappers. Actual `ReplacedSkill` predecessors are excluded from active evaluation, rather than multiplied together. Category prerequisites do not multiply into each other's protection. Existing separate PvP `DurabilityReductionFactor` handling is preserved.

**Research correction:** fresh source/live inspection found that the three category skills have prerequisite links; their live `ReplacedSkillId` values are null. The earlier report's suggestion that these specific definitions replaced each other was inaccurate. The real error was their simultaneous raw multiplication into the same duration attribute. The implementation separates their categories and also handles actual replacement links if configured.

No unrelated master formula (including other users of Formula1204), pet bonus, damage or EXP coefficient was altered. Unboosted ordinary equipment still uses the original 2000-damage / 10000-hit global settings. Its intended master durability protection is corrected by category; changing the pet balance never changes those global defaults.

## 5. Cash Shop origin and expiration

The current product files contain actual property-10 duration values in seconds. Existing account storage records preserve package/product/price identifiers and purchase time, but the inspected item entity and live schema contain no persisted origin/rental deadline. The catalog previously discarded duration when delivering the pet. The client contains existing period-item timestamp handlers, which were inspected rather than assumed absent.

New purchases now freeze their selected duration in nullable `CashShopStorage.RentalDurationSeconds`. Missing duration on a new pet offer is refused before debit. Existing item/currency/price resolution and account/world isolation remain unchanged. Withdrawing the purchased item activates `Item.RentalExpiresAt = UTC now + stored duration`. **The purchased term starts on withdrawal/activation, not while waiting unused in the Cash Shop storage.** That activation decision is explicit and preserves the storage-use workflow. 1, 7 and 30-day products use their own actual values; there is no universal seven-day term.

A non-null deadline is the minimal persistent identification of a new rental. Such pets lose no combat Life; ordinary versions of the same definition still wear. The nullable deadline is preserved by item assignment/cloning paths and EF persistence. Calendar time continues during logout and server downtime. Expired items are excluded from power-up construction, equipment insertion/requirements, attack/mount guards and Raven execution. A server-owned expiry task removes expired equipped/inventory items and sends normal inventory/appearance updates. It runs for offline-helper players as well, independently of combat. The scheduler uses the normal persistence lock without inheriting a packet handler's reentrant AsyncLocal, and is cancelled/joined before disposing the player's context.

Fully logged-out characters have no active actor: their rows are cleaned when loaded, using the stored absolute timestamp, before inventory/bonuses are sent. The term is never restarted on login. This is lazy physical deletion while fully offline, with immediate logical expiration; the persisted deadline prevents later use. No production-wide cleanup job was added.

Existing S6 D2:11/12 period-item packets are emitted after delivery, move and inventory listing. The legacy S6 item serializer sets the existing rental/expired bits. No packet XML, client package version or new protocol is introduced. Packet bytes are tested. **The open-source extended item format currently lacks the legacy expiration flag**, and the client tooltip path needs visual/integration verification; sending its existing deadline packet alone does not prove that every client UI displays a countdown. Server removal/eligibility is independent of countdown display. No client source/build/installation was changed.

### Legacy migration issue — intentionally not guessed

Existing permanent and previously purchased item instances cannot reliably be separated from the audited item fields. Existing storage rows also did not freeze the original duration/version. The additive migration leaves both new columns null for every existing row. No origin or expiry is guessed, no permanent item becomes a rental, and no old player item is deleted by this migration.

Consequently **unidentified legacy shop pets do not automatically receive rental immunity/expiry**; they retain ordinary legacy-null treatment until provenance and an explicit migration policy are approved. Old storage entries likewise stay legacy-null on withdrawal. This specific legacy-data conversion is withheld, as requested when reliable identification is unavailable. It must be resolved/reviewed before production deployment if old purchases are to receive the new rental behavior. New purchases have complete metadata and work independently. No backfill SQL is supplied or executed against production.

## 6. Plasma Storm and mount restrictions

The original random roll and equipment selection remain. If the selected item is a supported pet, direct halving is skipped; it is not rerolled onto somebody else's armor. Fenrir/Horse/Raven cannot lose half their Life through this path. Ordinary equipment still halves exactly as before when selected, verified independently of randomness. The required mount is also checked when delayed Plasma Storm/Earthshake damage and effects resolve. This prevents an already exhausted mount from continuing its active skill while leaving valid combat effects intact.

## 7. Database migration

`20261029140000_AddPetRentalLifetime` adds only:

| Table | New column | Type / default |
| --- | --- | --- |
| data.Item | RentalExpiresAt | nullable timestamp with time zone; existing rows null |
| data.CashShopStorage | RentalDurationSeconds | nullable integer; existing rows null |

The Item model snapshot is updated. The SQL-managed storage DTO/repository reads, inserts, takes and restores duration, including failed delivery restoration. The migration was applied to a **fresh disposable PostgreSQL 17 cluster at 127.0.0.1:55432**, database `cash_shop_ci_pet_balance`. Tests confirmed timestamps/progression survive new contexts, null legacy items remain null, and rental duration survives storage reload/restore under world IDs 0 and 1. No real local or live database was used for writes.

Rollback after selling new rentals needs a reviewed plan that preserves deadline metadata; blindly dropping the new columns would discard those deadlines. No rollback, wipe, reset, reseed or production migration was executed.

## 8. Build and automated validation

The full `src/Startup/MUnique.OpenMU.Startup.csproj` builds successfully on .NET SDK 10.0.401. The first cold test build required building the repository's persistence source generator first, following its prebuild contract. Build logs contain warnings; this is not represented as a warning-free build. Final incremental build result is recorded in `output/pet-research/build-server-final.log` when present, with the complete earlier build in `build-server.log`.

| Check | Result |
| --- | --- |
| Core/affected regression suite, latest verified | 422 passed; 0 failed; 1 skipped; 423 total |
| Final catalog/duration checks | 23 passed; 0 failed; 0 skipped; 23 total |
| Real disposable PostgreSQL migration/storage tests | 13 passed; 0 failed; 0 skipped; 13 total |
| Earlier broad run including all socket fixtures | 420 passed; 2 failed; 1 skipped; 423 total |
| Whitespace / preservation checks | git diff --check; unchanged HP recoil method and protected config files |

The final affected suite explicitly excludes only the two known `Sockets` cases after running and recording them in the earlier broad suite. Those existing fixture cases fail with `Sequence contains no elements` at `ItemSerializerTests.cs:218`, looking up Brave Helm socket options **before invoking the serializer**. Their setup/configuration files are unchanged. They remain unresolved; a separate baseline execution was not performed, so this report does not claim a demonstrated pre-existing failure. No unrelated socket configuration was changed to hide the failures. The one skipped generic fixture is reported, not counted as passed.

Coverage includes every pet type, all Fenrir colors, light/normal/heavy/no-damage calibration, fractional final zero, no EXP loss, command/offline guards, actual jewel and Zen actions, unchanged Zen price, bonus removal/restoration, display percentages/category separation/replacements, delayed mount rejection, pet immunity versus ordinary equipment halving, actual product durations, normal-versus-purchased pet wear, active offline expiry, relog reconstruction, packet flags/deadlines and real PostgreSQL persistence/migration.

Evidence is retained outside the server repository in `C:/Users/ausek/Documents/ChatGPT/openmu/output/pet-research/`: TRX results, build/test logs, `live-before-implementation.txt`, preservation checks and calculation artifacts. The initial migration harness used `Username` whereas the repository's old role-name parser expects `User Id`; correcting only that disposable connection string resolved the setup failure. No production connection setting was edited.

## 9. Remaining issues before deployment

1. Resolve the legacy purchase/origin migration policy from reliable evidence. Existing item dates/origins remain untouched.
2. Resolve or explicitly accept the two recorded socket fixture failures; this branch does not fix unrelated socket configuration.
3. Verify actual client unmount/remount, inventory synchronization and countdown presentation on a throwaway server. The extended-format countdown flag limitation and existing Windows tooltip timestamp handling were not game-tested.
4. Verify a real purchase/withdrawal, server restart, repair and offline expiry end to end. Automated player/context/DB tests passed; no game login or production combat was performed.

No merge/deployment or client installation was performed. No long-lived local server was started; only the owned disposable database was run for tests. The local database is stopped after verification; its test files are retained rather than deleting user data.

## 10. Changed files

| Changed file | Purpose |
| --- | --- |
| src/DataModel/Entities/Item.cs | Nullable persisted deadline and preservation in AssignValues. |
| src/DataModel/ItemExtensions.cs | UTC expiry predicate. |
| src/GameLogic/AttackableExtensions.cs | Reject zero/expired Raven attacks and delayed mount damage. |
| src/GameLogic/InventoryStorage.cs | Refresh pet wrappers and reschedule expiry on insertion; refuse expired items. |
| src/GameLogic/ItemPowerUpFactory.cs | Exclude expired rental items and set contributions. |
| src/GameLogic/Offline/PetHandler.cs | Offline Raven checks the right-hand slot and refuses zero-Life initialization. |
| src/GameLogic/Pet/DurabilityProtection.cs | Current displayed master percentages, separate categories, actual replacement filtering. |
| src/GameLogic/Pet/PetDurabilityPolicy.cs | Approved damage/hit coefficients; rental immunity; exhaustion, retention and repair refresh. |
| src/GameLogic/Pet/PetRentalLifetime.cs | UTC expiry scheduler, login/offline cleanup and safe timer teardown. |
| src/GameLogic/Pet/RavenCommandManager.cs | Command/attack/ranged-loop guards at zero Life. |
| src/GameLogic/Player.cs | Pet wear integration; no exhaustion XP penalty; mount-use and lifecycle guards. |
| src/GameLogic/PlayerActions/ItemConsumeActions/BlessJewelConsumeHandlerPlugIn.cs | Atomic validation and two-jewel Fenrir repair; old one-Bless branch blocked. |
| src/GameLogic/PlayerActions/Items/ItemRepairAction.cs | Restore equipped pet functionality after Zen repair; pricing unchanged. |
| src/GameLogic/PlayerActions/Items/MoveItemAction.cs | Refuse expired rental movement/equipping. |
| src/GameLogic/PlayerActions/Skills/EarthShakeSkillPlugIn.cs | Reject delayed effects from an exhausted/missing Horse. |
| src/GameLogic/PlayerActions/Skills/PlasmaStormSkillPlugIn.cs | Preserve equipment halving but exclude pets and reject exhausted mount effects. |
| src/GameLogic/PlayerAppearanceData.cs | Hide unusable mounts/pets from appearance. |
| src/GameLogic/PlayerItemExtensions.cs | Expired items fail equipment requirements. |
| src/GameLogic/SkillList.cs | Do not attach raw 12–90 duration multipliers for the seven targeted durability skills. |
| src/GameServer/MessageHandler/CashShop/CashShopCatalog.cs | Read actual duration seconds; reject new pet offers with missing duration. |
| src/GameServer/MessageHandler/CashShop/CashShopItemDelivery.cs | Activate new rental expiry from persisted purchased duration. |
| src/GameServer/MessageHandler/CashShop/CashShopRequests.cs | Freeze chosen product duration in the purchased storage entry. |
| src/GameServer/RemoteView/Inventory/ItemAppearPlugIn.cs | Send deadline after item delivery. |
| src/GameServer/RemoteView/Inventory/ItemMovedPlugIn.cs | Resend deadline after a slot change. |
| src/GameServer/RemoteView/Inventory/PetRentalPackets.cs | Existing D2:11/12 UTC period-item packets. |
| src/GameServer/RemoteView/Inventory/UpdateInventoryListPlugIn.cs | Send deadline after login/full inventory synchronization. |
| src/GameServer/RemoteView/ItemSerializer.cs | Legacy S6 rental and expired flags, preserving other item bits. |
| src/GameServer/RemoteView/World/AppearanceChangedPlugIn.cs | Send removal appearance for unusable equipped pets. |
| src/Persistence/CashShop/ICashShopRepository.cs | Optional rental-duration metadata on the storage record. |
| src/Persistence/EntityFramework/CashShop/CashShopRepository.cs | Persist/read/restore rental duration without changing price or account/world isolation. |
| src/Persistence/EntityFramework/Migrations/20261029140000_AddPetRentalLifetime.cs | Additive, nullable columns only; no legacy backfill. |
| src/Persistence/EntityFramework/Migrations/EntityDataContextModelSnapshot.cs | Item deadline in the EF model snapshot. |
| tests/MUnique.OpenMU.Tests/CashShop/CashShopRepositoryTests.cs | Real PostgreSQL migration, duration/timestamp reload, legacy nulls, both world IDs. |
| tests/MUnique.OpenMU.Tests/CashShop/PetRentalTests.cs | Actual catalog terms, origin distinction, expiry/relogin/offline, packets and flags. |
| tests/MUnique.OpenMU.Tests/Offline/PetHandlerTests.cs | Correct the old zero-Life fixture to Raven hand slot 1. |
| tests/MUnique.OpenMU.Tests/PetDurabilityBalanceTests.cs | Calibration, fractional zero, all colors, jewel/Zen repair, master, bonuses and PvP regressions. |

`EMBERION_PET_BALANCE_IMPLEMENTATION_REPORT.md` itself is the additional documentation deliverable. A copy is saved in the shared research workspace and in this implementation worktree.

## 11. Preserved gameplay and scope

The x5/x50 worlds still share their existing GameConfiguration. No EXP, reset, event, VIP, drop rate, recipe, item availability, pet damage/defense/EXP bonus or currency-price file was modified. The existing player HP recoil method `AfterHitTargetAsync` was compared with the base revision and is identical; no Imp/Demon/Wings HP setting is changed. Existing pet level, EXP, color and options are preserved by exhaustion and repair. No arbitrary pet wear cap or fixed hourly drain exists. The only timed removal is an explicitly identified purchased rental deadline.
