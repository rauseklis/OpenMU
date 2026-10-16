# Staged king maces

| Item | Group/number | Base damage | Weapon speed |
| --- | --- | --- | --- |
| Mace of King | 2/22 | 110-125 | 30 |
| Royal Mace of King | 2/23 | 150-170 | 40 |

Both are one-handed physical weapons for Dark Knight and its advancements,
Magic Gladiator/Duel Master, and Dark Lord/Lord Emperor. They occupy 1x3
inventory cells and use existing Sword of Destruction requirements, durability
and ordinary physical item options as provisional testing defaults. They do not
grant a weapon skill, Raven/scepter bonuses or the two-handed +15% bonus.
Hand-slot rules and class dual-wield mechanics remain unchanged.

Only these weapons use an isolated +4 minimum and maximum damage bonus per
upgrade level. At +13: 162-177 and 202-222. Normal item level limits are retained
(+15, with the same +4 progression). No existing weapon upgrade table changes.

Configuration update 123 adds the definitions to existing Season 6 databases;
fresh Season 6 configurations include them too. Reapplying the initializer
preserves existing matching definitions and rejects differently named occupied
slots. Drops remain disabled pending client model/texture and item-data setup.
No bank/inventory grants or changes to existing items are part of this update.

The client has orange tooltip names and ground labels for these item IDs.
That styling does not register models or supply textures. Do not distribute
the weapons until client rendering and displayed statistics are validated.
