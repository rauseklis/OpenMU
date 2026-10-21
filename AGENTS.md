# Agent Instructions

This repository is the **server** of a private MU Online project (a fork of `MUnique/OpenMU`); the game client is in
[`rauseklis/MuMain`](https://github.com/rauseklis/MuMain). This file is the entry point for AI coding assistants (Claude Code, Codex, ...) and
applies to people too.

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
