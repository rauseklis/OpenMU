# Crywolf event (design, draft for approval)

Status: draft, nothing implemented. Branch `feat/crywolf` (from `origin/master` 332dfc38).

## Goal

The full original Crywolf Fortress event on our server, built in stages. Each stage ships disabled by default, so a
merge never changes ordinary play. Ordinary hunting on map 34, the Balgass barracks quest and all other events keep
working.

## Source of the logic

Upstream OpenMU PR #1005 (MIT licence), reviewed snapshot `7cd2928682777f9156caa90d7413d94b1a8c1a7f`, changes against
base `a0cfc01438bd52c013532c5654978203a0b49ac1`. Keep the licence notice and attribution. Adapt it to our fork by
porting files; do not merge the later upstream branch. The brother's partial local import (94 files, uncommitted, on his
PC) is a convenience only: either he pushes a checkpoint branch or we redo the import from the comparison. The
Aetherion pack is reference for rules and numbers only; none of its files or code is copied.

## What is verified (2026-10-26)

- `origin/master` has map 34, NPC and altar spawns and three client-to-server packets (`BD 00`, `BD 03`, `BD 09`). It has
  no server-to-client `BD` definitions and no event logic.
- The client already handles the Crywolf messages (info, altar state, contract, timer) and has the cinematic code.
- `UpdateVersion` 142 is taken (`CherryBlossomRewards`); the new update needs the next free number at merge time.
- Packet package version is 0.9.23 (was 0.9.22; the donor adds only server-to-client packets).

## Stages (each its own PR, server first when packets change)

1. **Foundation, off by default.** Port the data model (`CrywolfData`, terrain variants), persistence, migration and
   configuration update. The plug-in is disabled by default for fresh and existing databases. Tests: build, schema upgrade
   on a throwaway database, update applied twice is idempotent, nothing changes while disabled.
2. **Waves and bosses.** Event definition, state machine, monster groups and AI, Balgass and Selupan, Wolf Statue HP.
   Failing tests first for: Balgass never spawned must not count as victory; disable during any phase fully stops the
   event (timers, army, modifiers, terrain).
3. **Contracts and rewards.** Altar contracts, hero ranking, rewards, chaos-rate benefit, reward idempotence across
   disconnect and restart, full inventory.
4. **Client and packets.** Check server-to-client layouts against the client receive code; bump packet versions in the three
   projects with the matching client change, merged together. Port the cinematic fixes (MuMain PR #655) if still missing.
5. **Defeat penalties.** Configurable and off by default: EXP retained and +10% maximum HP on affected monsters, chosen by an
   explicit list of hunting maps. Applied once, on top of channel rates. Beginner mobs, NPCs, summons and fixed-HP objectives
   excluded.
6. **Staging test.** Both players run a deliberate loss and a win on a throwaway server (shifted ports, disposable
   database). Only then enable on live, by an explicit decision of the owner.

## Open decisions (needed before the stage that uses them)

| Decision | Needed by |
|---|---|
| Battle host channel (Emberion 50x or 5x) | stage 2 |
| Schedule and time zone (reference: weekly, 20:30; `Europe/Riga`), or manual start | stage 2 |
| Participation level (minimum level, resets) and entry rules | stage 2 |
| Battle length (guides say 30 min; reference state durations total about 21 min) | stage 2 |
| Rewards and victory benefits | stage 3 |
| EXP retained percent, jewel-only drop penalty and percent, map list, penalty duration | stage 5 |
| Previous result after disable and re-enable | stage 2 |

## Safety

No live database or configuration change, no merge, no deploy without the owner's explicit yes. Live changes go through a
configuration update with a backup first. Tests use a throwaway PostgreSQL database only.

## Decisions made

- 2026-10-26, owner: rewards stay as the donor designs them. Every elf with a valid contract at the end of the battle
  gets a reward item, and every top-5 hero gets one at the end of the event, so one player can get two.
- 2026-10-26: a battle in which Balgass was due but could not be spawned has no result (the previous one stays), it is
  neither a victory nor a defeat.
