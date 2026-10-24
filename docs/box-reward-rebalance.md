# Box reward rebalance

This document describes the owner's approved reward plan. The JSON manifests and
transactional maintenance script implement it for a populated Season 6 database.

## Monster bonus drops

- Keep the existing combined 5% small-bonus chance for now.
- A monster may qualify for several bonus types, but a kill awards at most one
  small bonus. Ordinary loot remains separate.
- Use the exact monsters and maps in the owner's bonus-location list, rather
  than selecting every monster in a level range.
- Remove the other bonus variants from this bonus pool for now. Preserve their
  item definitions, existing inventory items and opening behavior.
- Split the combined 5% equally among each monster's eligible bonus types,
  confirmed by the owner. Each eligible type has probability 0.05 / N per kill,
  where N is that monster's eligible bonus count. Two types each get 2.5%; three
  each get approximately 1.6667%. Select at most one bonus per kill. Removing old
  pool entries must not silently reduce the combined chance below 5%.

## Box opening rewards

These probabilities are per opening, separate from the monster bonus chance.

| Box | Armor | Weapons | Shields | Jewels | Accessories | Fruits | Zen |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Kundun +1 through +5 | 45% | 45% | 10% | 0% | 0% | 0% | 0% |
| Silver Medal | 30% | 20% | 5% | 20% | 5% | 0% | 20% |
| Gold Medal | 35% | 30% | 5% | 15% | 5% | 0% | 10% |
| Heart of Love | 0% | 50% | 10% | 30% | 0% | 0% | 10% |
| Firecracker | 20% | 20% | 0% | 35% | 0% | 0% | 25% |

Silver Medal and Gold Medal accessory rewards are standard ordinary rings only,
not pendants or transformation/event rings. Their item levels are +0 through +4,
with HP recovery randomly selected as 1%, 2% or 3% on every eligible ring reward.
Ring of Magic (13/24) is included with its normal maximum-mana option randomly
selected as +1%, +2% or +3%, instead of HP recovery, confirmed by the owner.
Both medals use the same six-ring pool: Ice (13/8), Poison (13/9), Fire (13/21),
Earth (13/22), Wind (13/23) and Magic (13/24). Each ring is equally likely within
the 5% ring category (probability 0.05 / 6 per opening), confirmed by the owner.

Box of Heaven's revised reward split is confirmed: 20% Zen, 40% jewels,
10% fruits, 25% Satan/Angel/Horn and 5% Demon/Guardian/Dinorant, totaling 100%.
Accessory rewards are removed from Box of Heaven. Guardian in the 5% group
is Spirit of Guardian (13/65), confirmed by the owner, not Guardian Angel.
The owner confirmed the 25% pet group mappings: Satan is Imp (13/1), Angel is
Guardian Angel (13/0), and Horn is Horn of Uniria (13/2).
Each pet is equally likely within its group, confirmed by the owner: Imp,
Guardian Angel and Horn of Uniria each have probability 0.25 / 3 per opening
(approximately 8.3333%); Demon, Spirit of Guardian and Horn of Dinorant each have
probability 0.05 / 3 (approximately 1.6667%). Use exact group fractions rather
than rounded percentages when configuring these rewards.

Kundun equipment is Excellent +0. Other listed equipment is ordinary.
All Kundun boxes contain non-socket Excellent equipment only, confirmed by the
owner. No socket items may enter their reward pools. Verify by actual item ID
and socket eligibility rather than relying on display names.
Skull Shield belongs to Heart of Love's shield category, separate from weapons.
Heart of Love weapons and Skull Shield both use item levels +9 through +11.
Item levels are equally likely within each agreed range, confirmed by the owner:
ordinary bonus equipment uses +7, +8 or +9; Heart of Love weapons and Skull
Shield use +9, +10 or +11; medal rings use +0, +1, +2, +3 or +4. Kundun rewards
remain fixed at +0.
The Box of Heaven fruit category may award any of the five fruit types.
All five fruit types are equally likely: Strength, Agility, Vitality, Energy and
Command each have a 2% chance per opening (one fifth of the 10% fruit category),
confirmed by the owner.
Equal selection within item lists and overlaps between box tiers are acceptable.
Indigo is removed from the proposed Kundun +5 armor list, with no replacement.
Lionheart Quiver is removed from the proposed Firecracker weapon list, with no replacement.
Firecracker's Holy Storm Claw maps to Storm Hard Glove (group 0, number 33),
confirmed by the owner. Selecting this reward gives one glove item, not a pair.
The Firecracker receiving these rewards is the reward-opening variant (14/11,
source level 2), confirmed by the owner. Remove fireworks-only variants from
monster drop sources; preserve their definitions, existing items and effects.

Zen rewards are fixed amounts when the Zen category is selected:

| Box | Zen amount |
| --- | ---: |
| Silver Medal | 10,000 |
| Gold Medal | 20,000 |
| Heart of Love | 30,000 |
| Firecracker | 50,000 |
| Box of Heaven | 100,000 |

Jewel reward pools:

| Bonus | Allowed jewels |
| --- | --- |
| Silver Medal | Bless, Soul, Chaos |
| Gold Medal | Bless, Soul, Chaos, Life |
| Heart of Love | Bless, Soul, Life, Creation |
| Box of Heaven | Bless, Soul |
| Firecracker | Bless, Soul, Life, Creation, Gemstone |

Every jewel is equally likely within its bonus's jewel category, confirmed by
the owner. Divide that category's probability by its jewel count; do not round
individual chances before generating the reward.

The medals' ring option is separate from the item's +0 through +4 upgrade level.
The reward must not omit its agreed HP recovery or maximum-mana option.
The 1%, 2% and 3% option values are equally likely (one third each), confirmed
by the owner, for both HP recovery rings and Ring of Magic's maximum mana.

| Kundun tier | Exactly one Excellent option | Exactly two Excellent options | Luck |
| --- | ---: | ---: | ---: |
| +1 | 70% | 30% | Deferred |
| +2 | 75% | 25% | Deferred |
| +3 | 80% | 20% | Deferred |
| +4 | 85% | 15% | Deferred |
| +5 | 90% | 10% | Deferred |

These settings need box-specific overrides so other drops retain their existing
Luck and Excellent rules. Items may receive only options they support.
Luck and ADD changes are deferred by the owner. The proposed weighted ADD table is not approved for this
implementation stage.

Ordinary shields from bonus rewards have a 50% chance to receive their supported
skill. Every Excellent shield and Excellent weapon receives its supported skill
with 100% probability, including Kundun rewards. Items without a supported skill
never receive one. Ordinary monster equipment drops retain their existing skill
rules.

Ordinary weapons from bonus rewards have a 50% chance to receive their supported
skill, except bows and crossbows, which receive their supported skill with 90%
probability. This does not grant a skill to an item without skill support.

## Confirmed item mappings and configuration

### Item names checked by the owner in-game

| In-game name | Server source name | Item number |
| --- | --- | ---: |
| Demonic | Black Rose | 42 |
| Storm Blitz | Aura | 43 |
| Storm Zahard | Storm Hard | 60 |
| Piercing Grove | Piercing | 61 |
| Violent Wind armor | Mistery | 39 |
| Sacred Fire armor | Sacred | 59 |

The Demonic Summoner set requested for Kundun +4 maps to Black Rose (42).
The Storm Zahard Rage Fighter armor set requested for Kundun +4 maps to
Storm Hard (60), confirmed by the owner. Armor rewards are individual pieces.
The Piercing Grove Rage Fighter armor set requested for Kundun +5 maps to
Piercing (61), confirmed by the owner.
Violent Wind armor in Kundun +1 and Silver Medal maps to the first Summoner
armor set, Mistery (39), confirmed by the owner. This mapping is for armor only;
the separately defined Violent Wind Stick retains its own identity.
Sacred Fire armor in Kundun +3 and Gold Medal maps to the Rage Fighter Sacred
armor set (59), confirmed by the owner.
Book of Samut in every proposed reward list maps to Book of Sahamutt (5/21),
confirmed by the owner.
The initialized catalog test and database validation verify that these Kundun
item pools have no sockets and support at least two Excellent options. The live
maintenance preview also refuses any socket item in a Kundun pool.

### Lorencia bar test stock

Lumen the Barmaid sells Kundun +1 through +5, Silver Medal, Gold Medal, Heart of
Love, Firecracker and Box of Heaven for 1,000 Zen each before any castle tax.
The maintenance script finds ten free cells in her existing shop and preserves
all other stock. It refuses missing shops, insufficient space or conflicting IDs.

These are unlimited merchant listings available to everyone for owner-requested
testing. Opening rewards can be worth much more than the purchase price. The
explicit price is stored on the merchant template in `StorePrice`; purchased
items do not inherit it. Ordinary item sale prices remain unchanged. The normal
NPC item-list packet does not transmit a purchase price, so the client's tooltip
may show its usual calculated price. Verify actual Zen deducted in game.

### Later shared Luck and ADD changes

Opening groups do not override Luck or equipment ADD. They use the same item
option definitions as other sources, so later changes to those shared definitions
also affect these rewards. The medal jewelry option is intentionally guaranteed
separately; it does not replace equipment ADD rules.

## Deployment and validation

Implement and test on branch codex/box-reward-settings. Use a disposable server
and database with isolated ports. Test category boundaries, independent Luck and
Excellent rolls, valid unique options, unchanged ordinary loot, at most one bonus
per kill, and exact map/monster restrictions.

Existing configuration lives in the database. Changing seed defaults alone does
not rebalance a populated server. Deploy code through a tested PR first so the
schema migration adds the optional reward fields. Then generate the maintenance
SQL using `python deploy/live/prepare-box-rewards.py`. The generator does not
connect to any database. Use the existing private database connection mechanism
with `psql -X -v expected_database=<exact-name>` for a preview; it rolls back.
After owner approval and a backup, add `-v apply=true` to commit the same plan.
Restart the game server afterward to reload the reward groups and merchant stock.
This script is not run automatically by migrations or deployment.

The maintenance test targets only the disposable loopback cluster on port 55443,
database `mu_box_rewards_20261024_final`, role `boxvalidation`. It checks preview
rollback, the database-name guard, the exact monster/map restrictions, the latest
Kundun probabilities, preservation of unrelated records and repeated application.
The explicit fixture seeds with the same JSON converters as normal startup and
reloads every reward group through production persistence for generation checks.
