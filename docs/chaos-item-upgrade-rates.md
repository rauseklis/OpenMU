# Approved item upgrade rates — 29 October 2026

Both Emberion x5 and x50 use this shared Season 6 configuration. These are owner-selected balance values; original Webzen Season 6 authenticity is not asserted.

| Target | Normal | Normal + Luck | Excellent or Ancient | Excellent or Ancient + Luck |
| --- | ---: | ---: | ---: | ---: |
| +10 | 60% | 80% | 50% | 70% |
| +11 | 60% | 80% | 50% | 70% |
| +12 | 55% | 75% | 45% | 65% |
| +13 | 55% | 75% | 45% | 65% |
| +14 | 20% | 40% | 10% | 30% |
| +15 | 20% | 40% | 10% | 30% |

Luck adds 20 percentage points. Existing Guardian-capable item (-10) and genuine Socket item (-20) modifiers remain additive, including overlap with Excellent/Ancient. Signed calculations clamp at zero instead of wrapping negative values through byte into a high success rate. This change does not resolve the separate legacy material-value byte-overflow paths.

Recipe IDs 3, 4, 22, 23, 49 and 50 are updated for an existing database once by configuration update 161 before world loading. New Season 6 configurations use the same values. Subsequent administrator edits survive ordinary restarts. There is no database schema migration.

Zen costs, required jewels, target levels, destruction on failure, other Chaos recipes, wings crafting rates, pet durability and player HP costs are unchanged. No client packet change is introduced; existing client rate previews and manual crafting still require verification in the game.

From a +9 Excellent/Ancient item with Luck, chance to survive all steps to +11 is 49%, to +13 is 20.7025%, and to +15 is 1.863225%. These full-chain probabilities assume ordinary category equipment with no additional Guardian/Socket deductions and independent rolls. They do not describe a single final-step chance or guarantee success after a particular number of attempts.
