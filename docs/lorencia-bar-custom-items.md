# Lorencia bar: Invisible set and Mace of King

Lumen the Barmaid (NPC 255, Lorencia bar at 123/135) sells the custom items at level +0, without a fixed store price,
so the price is the game's normal calculated price for the item (drop level formula):

| Item | Group/number | Size | Price (Zen) | Shop slot |
| --- | --- | --- | --- | --- |
| Invisible Armor | 8/74 | 2x3 | 175,100 | 96 (rows 13-15, left) |
| Mace of King | 2/22 | 1x3 | 82,100 | 98 (rows 13-15, next to the armor) |
| Invisible Helm | 7/74 | 2x2 | 175,100 | 99 |
| Invisible Pants | 9/74 | 2x2 | 175,100 | 101 |
| Invisible Gloves | 10/74 | 2x2 | 175,100 | 19 (rows 3-4) |
| Invisible Boots | 11/74 | 2x2 | 175,100 | 21 (rows 3-4) |

The shop grid is 8 columns x 15 rows (as in the client). Rows 5 and 6 stay empty. If a preferred place is taken on an
existing database, the item goes to the first free place below the existing stock; other items are not moved.
A fixed price can be set per listing in the admin panel (store price of the item in Lumen's merchant store).

Update 178 adds the listings to existing databases once; new databases get the same listings from the initializer.
Items which are already listed are not added again. The items themselves (definitions) already exist
(Invisible set update, Mace of King update 123) and exist in the client's item data.
