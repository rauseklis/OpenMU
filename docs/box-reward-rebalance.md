> World scope: the 50x and 5x worlds use the same gameplay, equipment and reward configuration. Ordinary monster bonus progression is enabled on both worlds with the same absolute chances. Only their EXP multipliers differ; characters, warehouses, Jewel Bank, Auction House and Cash Shop balances remain isolated by world. Update 158 enables the existing progression on previously opted-out worlds without changing the reward tables or player items.
# Monster bonuses and equipment progression

The monster-drop and Box of Heaven rules below define the configured progression. Other reward systems retain their existing configuration.

Each eligible kill uses one bonus roll with these absolute chances. Overlapping bonuses keep their own chances; at most **one bonus** can drop per kill. The remaining chance gives no bonus. Ordinary loot rolls separately. This progression is enabled on Server 1; other servers retain their own settings.

| Bonus | Eligibility (monster level, inclusive) | Monster-drop chance |
|---|---|---:|
| Silver Medal | Monster levels 1–44 | 1.00% |
| Gold Medal | Monster levels 45–59 | 0.35% |
| Heart of Love | Monster levels 50–60 (overlaps Gold) | 0.30% |
| Firecracker | Monster levels 60–74 | 0.15% |
| Box of Heaven | Monster levels 20–74 | 0.05% |
| Star | Monster levels 72–99 | 0.05% |

Every bonus is derived from the monster level and normal spawn data, rather than from a manually maintained monster list. It applies on every map, including Kalima, Blood Castle and Devil Square. A monster qualifies when it spawns automatically or during an event (`Automatic`, `AutomaticDuringEvent`, `OnceAtEventStart`, `AutomaticDuringWave`, which covers Blood Castle, Devil Square and the Imperial Guardian waves), is a real monster that can drop items, and has no guaranteed-reward group. NPCs, gates/traps, boss-wave starts (`OnceAtWaveStart`), manually spawned event monsters (Chaos Castle), battle-zone maps and special guaranteed-reward bosses are excluded. Summoned monsters already do not generate loot. Update 170 rebuilds these sources once on an existing database; chances per kill and the opening contents are unchanged.

Average eligible kills per bonus are approximately 100 for Silver, 286 for Gold, 333 for Heart, 2,000 for Heaven, 667 for Firecracker and 2,000 for Star. These are statistical averages, not guaranteed drop counts.

An opening gives one item or one Zen outcome, never a complete armor set. Monster drop chances and box opening chances are separate. Equipment is ordinary unless the box is explicitly a Kundun or Ancient reward.

## Monster locations (by level)

| Bonus | Level | Example monsters |
|---|---|---|
| Silver Medal | 1–44 | Goblin (3), Beetle Monster (10), Worm (20), Ice Monster (22), Ghost (32), Hell Hound (38), Hell Spider (40), Elite Skeleton (42), Thunder Lich (44); Kalima: Aegis 1 (17), Death Angel 1 (24), Blood Soldier 2 (37) |
| Gold Medal | 45–59 | Vepar (45), Poison Bull (46), Valkyrie (46), Shadow (47), Dark Knight (48), Poison Shadow (50), Gorgon (55), Death Cow (57); Kalima: Death Centurion 2 (48), Blood Soldier 3 (50), Death Angel 3 (53), Necron 3 (58) |
| Heart of Love | 50–60 | Poison Shadow (50), Cursed Wizard (54), Gorgon (55), Death Cow (57), Necron 3 (58), Devil (60) |
| Firecracker | 60–74 | Devil (60), Death Knight (62), Death Gorgon (64), Balrog (66), Great Bahamut (66), Silver Valkyrie (68), Lizard King (70), Hydra (74); Kalima: Aegis 4 (62), Blood Soldier 4 (70), Death Angel 4 (74); Blood Castle: Chief Skeleton Warrior 2 (64), Dark Skull Soldier 1 (66), Giant Ogre 1 (70) |
| Box of Heaven | 20–74 | Everything from level 20 to 74, e.g. Worm (20), Cyclops (28), Yeti (30), Hell Hound (38), Gorgon (55), Devil (60), Hydra (74) |
| Star | 72–99 | Mutant (72), Death Tree (72), Alquamos (75), Bloody Wolf (76), Iron Wheel (80), Queen Rainer (82), Beam Knight (84), Drakan (86), Zaikan (90), Phantom Knight (96); Blood Castle: Giant Ogre 3 (85), Red Skeleton Knight 3 (87) |

Overlapping bands keep their own chances; one bonus roll per kill means a level 55 monster has Gold 0.35%, Heart of Love 0.30% and Heaven 0.05% (0.70% in total), a level 60 monster Heart of Love 0.30%, Firecracker 0.15% and Heaven 0.05%.

Examples of the single bonus roll:

| Eligible bonuses | Absolute chances | No bonus |
|---|---|---:|
| Silver only | Silver 1.00% | 99.00% |
| Silver + Heaven, e.g. a level 30 monster | Silver 1.00%, Heaven 0.05% | 98.95% |
| Gold + Heart + Heaven, e.g. a level 55 monster | Gold 0.35%, Heart 0.30%, Heaven 0.05% | 99.30% |
| Firecracker + Heaven | Firecracker 0.15%, Heaven 0.05% | 99.80% |

## Ordinary opening probabilities

| Bonus | Armor | Weapons | Shields | Jewels | Rings | Pets | Higher pet group | Zen |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Silver Medal | 42% | 20% | 10% | 3% | 5% | 0% | 0% | 20% |
| Gold Medal | 51% | 20% | 10% | 4% | 5% | 0% | 0% | 10% |
| Heart Of Love | 0% | 77.4% | 8.6% | 4% | 0% | 0% | 0% | 10% |
| Firecracker | 56% | 14% | 0% | 5% | 0% | 0% | 0% | 25% |
| Box Of Heaven | 0% | 0% | 0% | 45% | 0% | 32% | 3% | 20% |
| Star | 75% | 20% | 5% | 0% | 0% | 0% | 0% | 0% |

Every listed jewel is **1%** per opening (Silver 3 jewels, Gold 4, Heart of Love 4, Firecracker 5). Rings and Zen keep their previous chances. Silver and Gold give weapons 20% and shields 10% of the whole opening; armor receives the remainder. Heart of Love splits what remains after jewels and Zen 90% weapons / 10% shields, Firecracker 80% armor / 20% weapons. Boxes without an armor or shield category do not gain one. Box of Heaven keeps its jewels, Zen and category contents; each higher pet is 1% and the ordinary pets share the rest of the old pet chance.

Silver, Gold and Firecracker select one unique armor set equally, then one of its included pieces equally. Silver's six sets are 7.00% each per opening; Gold's six sets are 8.50% each; Firecracker's seven sets are 8.00% each. Each Silver weapon is approximately 1.82%, each Gold weapon 1.67%, each Heart of Love weapon 5.16% and each Firecracker weapon 1.75%; each Silver and Gold shield is 2.5%. Shared equip eligibility does not add probability. All item pools and non-equipment probabilities stay unchanged.

## Equipment levels and options

- Silver Medal, Gold Medal and Firecracker: **+7 through +9**.
- Heart of Love weapons and Skull Shield: **+8 through +10**.
- Star armor, weapons and books: **+6 through +9**.
- Each allowed upgrade level is equally likely.
- Medal rings: **+0 through +4**. Ice, Poison, Fire, Earth and Wind receive 1-3% HP recovery; Ring of Magic receives 1-3% maximum mana instead. Each of the six rings is equally likely within the ring category.
- Ordinary equipment Luck and ADD use shared server settings. Supported ordinary weapon/shield skills use the existing bonus rules: 50%, or 90% for bows/crossbows. A weapon without a supported skill never gets one.
- **All Summoner book rewards always have their supported skill.**
- Star shields are exceptions: fixed levels below, with **no Luck, no ADD and no skill**.
- The existing 15% two-handed damage bonus is unchanged.

## Star class pools

| Class | Armor sets | Weapons |
|---|---|---|
| DW | Grand Soul, Eclipse | Dragon Soul Staff, Staff of Destruction |
| DK | Black Dragon, Ashcrow | Knight Blade, Dragon Spear |
| Elf | Divine, Iris | Saint Crossbow, Celestial Bow, Elemental Mace |
| MG | Thunder Hawk, Valiant | Thunder Blade, Rune Blade, Staff of Destruction |
| DL | Dark Steel, Glorious | Lord Scepter |
| Summoner | Demonic (server name Black Rose) | Demonic Stick, Book of Lagle |
| RF | Piercing Groove | Piercing Blade Glove |

Star uses **75% armor / 20% weapons/books / 5% shields**. Relative armor-set weights are unchanged: Grand Soul, Eclipse, Black Dragon, Ashcrow, Divine, Iris, Thunder Hawk, Valiant, Dark Steel and Glorious are approximately **5.36% each** per opening; Demonic and Piercing Groove are approximately **10.71% each**. One piece is selected uniformly within the selected set. These armor sets already had unique routes.

Shared weapons and shields now count once, without extra chance from multiple class references. Staff of Destruction is approximately **1.19%** per opening. Existing weapon/book variant weights are otherwise retained proportionally, including the EE Elemental Mace and Summoner book. Each of the four unique Star shields is **1.25%**. The opening character's class does not filter rewards. No equal overall class share is promised by these global loot pools.

| Star shield | Equip families | Fixed level | Options |
|---|---|---:|---|
| Grand Soul Shield | DW | +7 | No Luck, ADD or skill |
| Dragon Shield | DK and MG | +7 | No Luck, ADD or skill |
| Elemental Shield | Elf / EE | +8 | No Luck, ADD or skill |
| Serpent Shield | DL | +8 | No Luck, ADD or skill |

Thunder Hawk and Valiant have four armor pieces and no helm; Piercing Groove has helm, armor, pants and boots. Demonic and Black Rose are the same selected set. Star contains no old rewards, jewels, Zen, Cross Shield or unselected level-380 equipment. Dragon Spear replaces Dark Breaker. None of its selected equipment overlaps Firecracker.

## Existing ordinary bonus contents

### Silver Medal

- **Armor:** Bone (5 pieces), Scale (5 pieces), Brass (5 pieces), Silk (5 pieces), Wind (5 pieces), Violent Wind (5 pieces).
- **Weapons:** Serpent Sword, Light Saber, Battle Axe, Great Hammer, Battle Scepter, Battle Bow, Light Crossbow, Serpent Staff, Thunder Staff, Violent Wind Stick, Book of Samut.
- **Shields:** Elven Shield, Skull Shield, Plate Shield, Large Round Shield.
- **Jewels:** Jewel of Bless, Jewel of Soul, Jewel of Chaos.
- **Rings:** Ice, Poison, Fire, Earth, Wind and Magic.
- **Zen outcome:** 10,000 Zen.

### Gold Medal

- **Armor:** Sphinx (5 pieces), Plate (5 pieces), Spirit (5 pieces), Light Plate (5 pieces), Red Wing (5 pieces), Sacred Fire (4 pieces).
- **Weapons:** Heliacal Sword, Double Blade, Lighting Sword, Sacred Glove, Master Scepter, Tiger Bow, Silver Bow, Serpent Crossbow, Gorgon Staff, Legendary Staff, Red Wing Stick, Book of Neil.
- **Shields:** Dragon Slayer Shield, Spiked Shield, Tower Shield, Legendary Shield.
- **Jewels:** Jewel of Bless, Jewel of Soul, Jewel of Chaos, Jewel of Life.
- **Rings:** Ice, Poison, Fire, Earth, Wind and Magic.
- **Zen outcome:** 20,000 Zen.

### Heart Of Love

- **Weapons:** Heliacal Sword, Double Blade, Lighting Sword, Sacred Glove, Battle Scepter, Master Scepter, Tiger Bow, Silver Bow, Serpent Crossbow, Thunder Staff, Gorgon Staff, Legendary Staff, Red Wing Stick, Book of Samut, Book of Neil.
- **Shields:** Skull Shield.
- **Jewels:** Jewel of Bless, Jewel of Soul, Jewel of Life, Jewel of Creation.
- **Zen outcome:** 30,000 Zen.

### Firecracker

- **Armor:** Dragon (5 pieces), Legendary (5 pieces), Guardian (5 pieces), Adamantine (5 pieces), Ancient (5 pieces), Storm Jahad (4 pieces), Storm Crow (4 pieces).
- **Weapons:** Sword of Destruction, Holy Storm Claw, Great Scepter, Bill of Balrog, Bluewing Crossbow, Staff of Resurrection, Ancient Stick, Book of Neil.
- **Jewels:** Jewel of Bless, Jewel of Soul, Jewel of Life, Jewel of Creation, Gemstone.
- **Zen outcome:** 50,000 Zen.

### Box Of Heaven

- **Jewels:** Jewel of Bless, Jewel of Soul.
- **Pets:** Imp, Guardian Angel, Horn of Uniria.
- **Higher Pet Group:** Demon, Spirit of Guardian, Horn of Dinorant.
- Bless and Soul are **22.50% each** per opening; normal pets are **10.67% each**; each higher pet is **1.00%**. Categories are selected first, then equally among their members. Box of Heaven does not award Fruits.
- **Zen outcome:** 100,000 Zen.

## Kundun +1 through +5

Every Kundun tier gives **70% armor, 20% weapons/books and 10% shields**. The Kundun update rebuilds the pools as pre-Pink **Excellent +0** equipment with no sockets. Shared Luck/ADD and supported Skill rules remain. See [exact Kundun pools, unique-set weights and item mappings](kundun-progression.md).

| Kundun tier | One Excellent option | Two distinct Excellent options |
|---|---:|---:|
| +1 | 50% | 50% |
| +2 | 60% | 40% |
| +3 | 70% | 30% |
| +4 | 80% | 20% |
| +5 | 90% | 10% |

## What stays unchanged

Devil Square completion rewards, Ribbon box contents and probabilities, Ancient reward boxes, Cherry Blossom exchanges, shared Luck/ADD settings, other game servers and the owner-requested Lorencia test shop stock remain unchanged. Existing items in inventories and warehouses are not converted. Blood Castle fixed EXP and the repair of empty BC +5 through +7 Ribbon payouts are described in [Blood Castle rewards](blood-castle-rewards.md).

The tested startup update installs the approved plan once before loading the game configuration. Later restarts preserve administrator changes. No client code, packet or asset change is needed for these reward rules. Players reconnect after the server deployment; no new client rebuild is required.
