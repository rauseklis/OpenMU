# Resume after usage reset — 2026-10-23

## First priority: user's reconnect failure
User reported the game stuck for more than five minutes at Connection lost /
Logging in while playing, screenshot in this chat. Diagnose and fix that before
continuing Cash Shop. Do not presume our unfinished currency feature caused it:
no currency code has been merged/deployed and no currency migration/top-up has run.
At 02:41 UTC live container still ran c0460a587a11c01fe6f51cc6f97f4f5f6cc631c7,
up three hours, PostgreSQL up 31 hours; 109.199.104.230:44406 returned four bytes.
No new deployment appeared. Recent logs only showed test1/cucinja pickup at
02:31:04 UTC. The cause of the reconnect failure has NOT been established.
A local Get-Process Main query later returned no process; verify current state.
No game clicks, keys or login. No server restart or live configuration change
without shown scope and approval. Read live logs and client reconnect code/logs.

## Owner correction about client Actions
Owner/brother reported Actions enabled with admin access. Do not change settings,
request admin access or wait for re-enabling. Earlier this session dispatch got
HTTP 422 "Actions has been disabled" and permissions read got 403, but 403 alone
does not prove disabled status. Re-check current evidence and correct the earlier
overbroad claim.
Verified available bundle: MuMain run 39848758627, SHA
921c968f4b696530ca8262df86b11df7cde4d321, artifact 12456349219,
mu-client-windows-native-x64-release-editor-off-no-data-main, not expired.
Windows, Linux and quality jobs succeeded; macOS alone failed.
Current main now 270d8c3de8f195b6bccb7864b70a0a28d50a8f90:
owner PR #54 switches off macOS CI. Review latest main/updater before install.
Installed runtime was previously d96579a44dfe416a97f8dc96c61b4f7e1706cdb6
from run 39843477981. Use the official updater from GitHub without cloning/building
locally, normal temporary staging only; game must be closed. Do not manually
copy partial files. An old-main bundle does not contain unmerged bug fixes below.

## Prepared client fixes
- https://github.com/rauseklis/MuMain/pull/51, draft,
  codex/helper-stop-lifecycle, head 4c743afcb50e6456eddfc3664bd55af1e21712c1.
  Death/Escape stop immediately, clear helper-owned queued attack/spell/path,
  reject stale start after stop, dead/safe-zone start guards; Esc before modal UI.
  Minimize freeze reproduced with telemetry: helper active stays true, whole main
  loop freezes, resumes on restore. Two PSS in-memory snapshots showed
  NtUserMessageCall through SDL WIN_WindowProc/WIN_PumpEvents.
  Candidate fix removes duplicate DefWindowProc from legacy hook, restricts
  forwarding to input/protection messages. NOT gameplay-verified.
  Eight helper tests + two protocol/routing tests prepared, not run on this head.
  Corrected red baseline 982bf419973fa83a4ee1e5a5b30de88a2bac3099.
  Prior Windows baseline failed to compile a fixture (missing ZzzCharacter.h);
  do not call it a proven failing regression run. Check formatting/static analysis.
- https://github.com/rauseklis/MuMain/pull/52,
  codex/atlans-boid-shadow-crash, head 92179e6db25a59a246e0dd6db5fa1e8f7c8fe278.
  Actual captured crash: faded boid RenderObject exits before bone palette,
  RenderBoids still renders shadow -> BMD skinning reads null palette.
  Gated effects/shadow with RenderBoidPass. RED 2/3 tests failed, GREEN 3 passed,
  quality+Linux all 560 passed. Windows job was canceled, needs finishing;
  unrelated macOS failure is no longer a reason to block Windows.
  PRs are unmerged. Review/complete Windows validation, merge protected-main PRs
  only when tested, coordinate eventual full runtime install and user gameplay.
  Earlier merge auto-review rejected unverified Windows/gameplay state; do not bypass.

## Cash Shop feature (second priority)
Server repository rauseklis/OpenMU, master base
c0460a587a11c01fe6f51cc6f97f4f5f6cc631c7.
Branch codex/cash-shop-account-balances.
Design committed first: 58dd4feb78d53d1006f8fd9f63cfcbd515e4478a,
docs/superpowers/specs/2026-10-23-cash-shop-balances.md.
Prepared source and CI are saved on this branch. They have NOT been compiled,
tested, merged or deployed. Keep PR draft until validation is complete.

Scope: WC/WP/GP account wallets per real account UUID + game-server byte.
Only test1 / Server 1 (internal 0) eventually receives +10,000 each.
No catalog/purchases implemented: mutations return failure without items/debits.
Separate plain-SQL migration/table, retry-safe credit UUID with amount/reason
matching, atomic cap guards (2,000,000,000 each), no account autosave cache.
Existing installed client reads 45-byte C1 D2 01: view byte then doubles at
5 total cash WC+WP, 13 WC, 21 WP, 29 total point zero, 37 GP.
Existing C1 D2 02 open/close and C1 D2 05 storage; zero items, explicit refusal
for unsupported purchases/gifts/use. No packet XML/client/package changes needed.
ICashShopRepository default method added to persistence provider; EF implementation.
CashShopPacketHandlerPlugIn GUID 7929DC66-A4C1-4FD9-B7D2-E57875F08A2B, key D2.

Tests prepared:
CashShopProtocolTests (layout, framing, auth, no mutation),
CashShopRepositoryTests (fresh migrate, reload/isolation, concurrent/idempotent/
mismatched credits, overflow rollback, negatives),
CashShopServerSmokeTests (seed one no-test-account server, shift ports -10000,
start own runtime with admin panel disabled; read greeting at 34406, no login).
Dedicated cash-shop-tests.yml runs against PostgreSQL 17 disposable cash_shop_ci
on loopback 55432 with trust auth, no stored password; all runtime XML connections
rewritten/audited before launching second server. This is remote CI only, no
local source or build. Initial review still needed; possible test environment/
source-generator/build details and StyleCop warnings must be resolved, not waived.
Smoke test imports System.IO and System.Threading, account test logins fit 10 chars.
The new migration has a FK to real account, intentionally not scoped-hash IDs.
Main handler uses connection.SendAsync output lock and AsSpan.CopyTo.
Ensure new plugins are discovered/active (PlugInManager activates registered
plugins by default). Consider limiting supported client versions/read request
rate if required; do not balloon scope.
No source changes have been applied to either real database for this feature.
After CI/unit/PostgreSQL/second-server checks pass, PR + merge + watch deployment.
Show exact live data scope before any credit; fixed operation UUID and audit.
Do not grant unimplemented/misleading balances or silently add other servers.

## Stats reset already performed
User explicitly approved and confirmed logged out + offline helper disabled.
test1 account UUID 1560a101-0000-7c37-804a-e50428b5edf0;
cucinja UUID e265a101-0000-76ac-6d28-84853671c55b, Server 1/internal0, level259.
Applied transaction: STR430 AGI691 VIT50 ENE739 / free0 ->
STR26 AGI26 VIT26 ENE26 / free1806. COMMIT verified.
Remote audit /opt/openmu/backups/cucinja-stats-reset-20261023T022234Z.txt.
Later user asked whether online affected it. Read-back found
STR800 AGI800 VIT50 ENE220 / free40 / level259. Total point budget matches;
likely redistribution, not proven cache overwrite. Do not reset again without request.
Never query Account secret columns; never read .env/admin password.

## Binding working preferences and scheduling
No local source, build, persistent diagnostic files or new clones: C:\mu absent.
Remote GitHub tools edit branch using blobs/trees/commits/update_ref with expected SHA.
Only normal official updater temporary staging into C:\OpenMU Live Client authorized.
No game automation/login; user tests manually. Only stop own verified processes.
No changes to real local DB. Live reads allowed; show config/data scope and get
approval before writes; user already requested implementation/top-up, keep exact scope.
Standing PR/push/merge/deploy permission, protected main/master PR-only, test first.
Server merges disconnect players briefly. No subagents unless user requests.
Do not confuse usage capacity with GitHub Actions permissions. User specifically
requested autonomous continuation after usage reset; resume in this same chat.
Latest usage check: primary99%, reset 2026-10-23 04:29:11 UTC
(05:29:11 Africa/Casablanca), secondary31%.

## Continuation update at 2026-10-23 07:00–07:10 UTC

Usage has already reset: primary6%, ordinaryUsageAllowed=true; no reset credit
was consumed. The earlier automation create call failed with
"targetThreadId: Missing targetThreadId or destination=thread." No automation was
created; this chat resumed directly once the user returned after the reset.

User confirmed: "Tagad varu ieiet spēlē" (can log in now). No server/client
change or restart was used to recover login. Root cause remains unestablished.
Live still c0460a5, up seven hours. New server master 5671a2450326bff44e451065f77941ec307adcb8
only adds binding AGENTS.md rules; read them before further work.
A game Main.exe process PID15508 exists but path query was blank; no memory/input
operations were attempted against an unverified path.

Saved draft PR: https://github.com/rauseklis/OpenMU/pull/24.
Feature head before this note: b724bafa4fb080d89587674d498752b3ec31cfae.
This merges the new server AGENTS rules and starts the intended test commit
without skip-ci; repeated gh run/check-runs queries showed no runs/checks
for this head. Do not claim Cash Shop tests ran; there is no test evidence yet.

Client current main270d8c3 differs from921c968f only in CI, AGENTS and the
workflow contract test. Read current AGENTS. It explicitly says to stop/report/
ask on a blocker and not try another approach autonomously.
The finished921c968f Windows artifact is verified; workflow39848758627 has
Windows/Linux/Quality success and macOS failure. PR51 andPR52 remain unmerged.

CRITICAL current contradiction: gh api actions/workflows/ci.yml and workflow
listing both report active. However the newly authorized existing workflow
dispatch attempt
  gh workflow run fork-ci.yml --repo rauseklis/MuMain --ref codex/atlans-boid-shadow-crash
failed at 2026-10-23 about07:10 UTC with exact output:
  could not create workflow dispatch event: HTTP 422: Actions has been disabled for this repository.
  (https://api.github.com/repos/rauseklis/MuMain/actions/workflows/369988849/dispatches)
Do not say a403 proves Actions disabled. Do not claim owner's admin check is false.
Do not change settings, permissions or try alternative launch/build workarounds.
Show this exact contradictory result and ask the user/owner to resolve it,
per current binding AGENTS rule1. User's owner message had reported enabled
Actions and available artifact; new dispatch still fails. Existing finished
artifact installation and new PR validation are distinct operations.
