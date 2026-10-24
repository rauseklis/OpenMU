# Updating and testing the box rewards

These reward settings run on the shared live server. Players who already connect
to that server do not need PowerShell commands, a client download or file copying.
After the owner confirms deployment is finished:

1. Close the game if it was connected during the update.
2. Start `Main.exe` from the usual game folder and reconnect to the same server.
3. Visit Lumen the Barmaid inside the Lorencia bar.
4. Buy Kundun +1 through +5, Silver Medal, Gold Medal, Heart of Love, Firecracker
   or Box of Heaven from the test stock. Each listing costs 1,000 Zen before any
   castle tax. The client tooltip may show its usual price; check the actual Zen
   deducted after a purchase.
5. Open the rewards and report the box name, reward, item level, Excellent option
   count, skill flag and any unexpected behavior to the owner.

Lumen also sells Red Ribbon Box, Green Ribbon Box and Blue Ribbon Box at +0
for 1,000 Zen each before castle tax. These listings are added to existing
servers automatically without replacing other stock.

Opening a ribbon box gives either Zen or one item reward, never both:

| Box | Zen outcome | Item outcome |
| --- | --- | --- |
| Red | 90%: 10,000,000 Zen | 10%: Archangel weapon |
| Blue | 85%: 7,500,000 Zen | 15%: Excellent jewelry |
| Green | 80%: 5,000,000 Zen | 20%: pack of ten jewels |

Item outcomes keep these rules:

- Red: one of the five Archangel weapons at +0, 25% luck, and a guaranteed skill
  on weapons that support a skill. Shared equipment ADD odds still apply.
- Blue: an Excellent ring or pendant at +0 with one option 85% of the time or
  two distinct options 15% of the time, plus 1–3% HP recovery. Only HP-recovery
  jewelry is eligible; Ring of Magic and Pendant of Ability are excluded.
- Green: one random pack of ten Chaos, Bless, Soul or Life jewels.

Blood Castle completion rewards for the surviving winner and winning party:

| Level | Reward |
| --- | --- |
| +1 | 1,000,000 Zen and 1 random jewel |
| +2 | 2,000,000 Zen and 2 random jewels |
| +3 | 3,000,000 Zen and 3 random jewels |
| +4 | 4,000,000 Zen and 4 random jewels |
| +5 | Always one Green Ribbon Box |
| +6 | Always one Blue Ribbon Box |
| +7 | Always one Red Ribbon Box |

Each lower-level jewel is selected independently from Bless, Soul, Life and
Chaos with equal chances, so repeats are possible. Items go to inventory, with
the existing ground-drop fallback if inventory is full. BC +8, experience,
scoring and Devil Square rewards are unchanged by this update.

Kundun one-option/two-option probabilities are +1: 70/30%, +2: 75/25%,
+3: 80/20%, +4: 85/15%, +5: 90/10%. All Kundun equipment remains Excellent +0
without sockets. Luck and equipment ADD changes are deferred.

The test shop stock is available to all players. The monster bonus pool keeps its
combined 5% chance and awards at most one eligible bonus per kill. Opening odds
and the chance for a monster to drop a bonus are separate settings.
