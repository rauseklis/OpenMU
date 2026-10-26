# King maces

| Item | Group/number | Base damage | Weapon speed |
| --- | --- | --- | --- |
| Mace of King | 2/22 | 150-165 | 30 |
| Royal Mace of Emberion | 2/23 | 200-220 | 40 |

Both are one-handed physical weapons for Dark Knight and its advancements,
Magic Gladiator/Duel Master, and Dark Lord/Lord Emperor. They occupy 1x3
inventory cells and retain Sword of Destruction durability
and ordinary physical item options. They do not
grant a weapon skill, Raven/scepter bonuses or the two-handed +15% bonus.
Hand-slot rules and class dual-wield mechanics remain unchanged.

Rare weapon requirements use STR/AGI coefficients 480/334 for Mace of King
and 968/675 for Royal. Plain +0 requires 1200 STR / 841 AGI and
2401 STR / 1680 AGI respectively (approximately 70% AGI relative to STR).
Each requirement is `20 + floor(coefficient * (82 + upgrade * 3) * 3 / 100)`.
Excellent/ancient variants retain the ordinary increased effective item level.
Additional damage +4 adds 5 STR, +8 adds 10 STR, and so on; AGI is unchanged.
Both definitions have no weapon skill, including normal/excellent/ancient drops.
Update 124 applies these requirements to existing Season 6 definitions;
it does not enable drops or modify player inventories.

Only these weapons use an isolated +4 minimum and maximum damage bonus per
upgrade level. At +13: 202-217 and 252-272. Normal item level limits are retained
(+15, with the same +4 progression). No existing weapon upgrade table changes.

Configuration update 123 adds the definitions to existing Season 6 databases;
fresh Season 6 configurations include them too. Reapplying the initializer
preserves existing matching definitions and rejects differently named occupied
slots. The original definition-only update did not enable loot drops; the staged Chocolate update below adds its reviewed box sources.
No bank/inventory grants or changes to existing items are part of this update.

Deploy with the paired client update, which includes both models, their
textures, matching item data, orange names and Royal upgrade flames. The server
does not grant these weapons to accounts or alter existing inventory contents.
Final appearance and equipped-weapon effects should be reviewed in game.

Chocolate update 148 enables the normal mace in Blue at 5% overall and Royal in Red at 100%, both +0. These are staged settings requiring paired client data.
