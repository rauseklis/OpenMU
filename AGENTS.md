# Agent Instructions

This repository is the **server** of a private MU Online project (a fork of `MUnique/OpenMU`); the game client is in
[`rauseklis/MuMain`](https://github.com/rauseklis/MuMain). This file is the entry point for AI coding assistants (Claude Code, Codex, ...) and
applies to people too.

## Binding rules for every session (apply even if nobody pasted a prompt)

This is a private MU Online project of two brothers: this server repository (`rauseklis/OpenMU`, branch `master`) and the client repository
(`rauseklis/MuMain`, branch `main`). The server runs live on a VPS and a merge to `master` deploys it within minutes.

1. **If something is blocked, fails or is unclear: stop, say exactly what happened (the real error text) and ask the user. Do not try a different
   approach on your own.** Never state something as fact that you did not verify (for example "Actions is disabled"): say what you checked.
2. **Do only what the user asked.** No extra builds, branches, worktrees, cleanups or "improvements". Never delete, discard or overwrite anything
   (files, branches, worktrees, databases, backups) without asking first.
3. **Never change GitHub settings, Actions, secrets or permissions, and never change the live server's configuration** (firewall, SSH, accounts, `.env`,
   compose files). You may read the live server (logs, read-only database queries). Show the user first, and wait for a yes.
4. **Never touch a real local server or database of the user's.** Test with a throwaway database and a second server on shifted ports.
5. **Do not control the game window** (no automated clicks or keys) and do not log in to the game. The user plays and reports.
6. **Never put passwords, keys or tokens in chat, commits or files.** Do not read or print the server's `.env` or the admin password.
7. **Never work directly on `main` or `master`.** Use a branch and a pull request. You may merge your own pull request (that deploys a server change),
   but only after testing it, and say honestly what you did not test. Merge server and client changes that share packets together, server first.
8. **Install the client only with `scripts/update-live-client.ps1`; never copy game files by hand.** If the finished GitHub build is not there yet,
   wait for it (the script does) or ask. Build locally only for your own code changes, as described in that document, never as a workaround
   for a slow install.
9. **At the start of a session:** `git fetch origin`, read `docs/OPERATIONS.md` (short) from `origin/main` of the client repository, do not change anything, then ask the user
   "What are we working on today?" and wait. The full guide is `docs/CODEX_GUIDE.md` there.
10. **At the end of every task** report in a few lines: what changed, what you tested, what you did not test, what the user should check.
11. **The game client is for Windows only.** Linux and macOS clients are NOT needed and are not released (their CI builds are switched off in the client
    repository). Do not build, fix, test or write code or docs for them. This does not concern this server, which runs on Linux in Docker on the VPS.

**Read the project guide first:**
[`docs/OPERATIONS.md` in the client repository](https://github.com/rauseklis/MuMain/blob/main/docs/OPERATIONS.md). It explains how the two
repositories fit together (the packets in `src/Network/Packets`), the live server and how a merge to `master` deploys to it, how to test
safely, and the rules the owner set.

## The essentials for this repository

- The default branch is **`master`**. **A merge to `master` deploys to the live server within minutes** (`.github/workflows/deploy-live.yml`);
  documentation-only changes do not. Test before you merge.
- The live server's setup, secrets and roll-back are described in [`deploy/live/README.md`](deploy/live/README.md).
- A change to the packet XML needs a new package version in `src/PlugIns`, `src/Network` and `src/Network/Packets` (the three project
  files), a matching change in the client repository (`nuget-feed/` and `OpenMuPacketsVersion`), and both merged together.
- Never use the owner's real database or server for tests; use a throwaway PostgreSQL database
  (`AUCTION_TEST_CONNECTION_STRING` for the PostgreSQL tests).
- Work on a branch in a git worktree under `.worktrees/`; keep the existing line endings of a file when editing it.
- The coding conventions of the project are in [`CONTRIBUTING.md`](CONTRIBUTING.md).
