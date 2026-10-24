# Ancient reward boxes

Each opening gives exactly one Ancient item at **+0**. Every eligible Ancient
piece in that box has the same chance; shared sets are listed once rather than
once per class. The item's Ancient stat bonus is **+5 with 70% probability** or
**+10 with 30% probability**. Luck and ADD use the server's shared item-option
settings, so later shared changes also affect these boxes. Supported skills are
guaranteed, as with other Ancient rewards. Rewards have no Excellent options or
sockets.

| Class | Box of Luck: low | Silver Box: mid | Golden Box: high |
| --- | --- | --- | --- |
| Dark Knight | Hyperion | Garuda | Hyon |
| Dark Wizard | Barnake | Heras | Anubis |
| Elf | Fase | Gaia | Aruan |
| Magic Gladiator | Hyperion | Garuda | Gaion |
| Dark Lord | Hyperion | Eplete | Agnis |
| Summoner | Unassigned | Semeden | Chrono |
| Rage Fighter | Cloud | Chamer | Trite |

The low pool contains 11 pieces, the mid pool 29, and the high pool 28.
Weapons, shields and jewelry required by these sets are included, with the
matching Ancient variant. A box gives a single piece, not an entire set.

Chamer requires the Sacred Glove weapon, Sacred Fire Armor, Pants and Boots.
Trite requires Storm Jahad Helm, Armor, Pants and Boots at item number 72, rather
than the ordinary Storm Hard items at number 60. Trite is permanent equipment:
it can be traded, dropped, stored, repaired and sold in a personal shop. Its
existing client base stats and level 150 requirement are retained.

The existing set bonuses are unchanged. Rave is excluded from these three
reward pools. Summoner's low tier and the proposed all-stat bonuses remain
unassigned or deferred.

These are the unlocked Silver and Golden Boxes. Keys and sealed boxes are not
used by these opening rules. Box of Luck is item **14/11, level 0**; Silver Box
is **14/124, level 0**; Golden Box is **14/123, level 0**. Other levels of item
14/11 (medals, Kundun boxes and other bonuses) retain their existing rewards.
This update changes no monster drop rates or ribbon-box rewards.

All three boxes are sold by **Lumen the Barmaid in the Lorencia bar**, at a
base purchase price of **1,000 Zen each**. Any active castle shop tax is
additional. Existing shop stock is kept and the boxes occupy free cells.

For manual testing, a game master can stage each box with `/item 14 11 0`,
`/item 14 124 0`, and `/item 14 123 0`, then open it by dropping it from the
inventory. Check the item name, Ancient variant, +0 level, Luck/ADD, stat bonus
and skill, and equip complete sets to verify bonuses and tooltips.

Both players need the matching client data update for Trite's permanent-item
rules. Close Main.exe and run `scripts/update-live-client.ps1 -Target
"C:\OpenMU Live Client"` from the current client clone to install the merged
GitHub build, then start Main.exe. The developer can install the local x64
Release build using the same script's `-LocalBuild` option.
