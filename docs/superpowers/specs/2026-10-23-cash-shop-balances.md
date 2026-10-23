# Cash Shop account balances

## Goal and scope

Persist WC (WCoinC), WP (WCoinP) and GP (Goblin Points), show them in the
existing MuMain Cash Shop window, and grant account test1 10,000 of each on
Server 1 after verified deployment. No client source/build changes are needed.
The client Actions setting remains untouched.

This change supports balances, opening/closing the window and an empty shop
storage response. It does not introduce a purchasable catalog or deliver items:
purchase/gift/use requests must return failure without changing balances or inventory.

## Persistence and isolation

Use separate SQL-managed balance and credit-operation tables, following Jewel
Bank's migration pattern. The key is the real account UUID plus game-server ID.
All characters of that account on that server share the balance; other accounts
and servers are isolated. Missing rows read as zero. Values are integer units,
nonnegative, capped at 2,000,000,000 per currency for existing client displays.

An administrative credit has an operation UUID and a reason. In one transaction,
lock/claim the operation, credit all three currencies with overflow guards and
record the resulting balances. Repeating an identical operation does not credit
twice; reusing the UUID with different amounts is rejected. Ordinary player
packets can only read balances, never credit them. Read from the database for
each refresh so account autosave cannot overwrite an administrative top-up.

## Existing client protocol

Do not change packet XML or the shared package version. MuMain already sends
C1 D2 01 (balance request), C1 D2 02 (open/close) and C1 D2 05 (storage).
Its packed C1 D2 01 response is 45 bytes: four-byte header, one view byte,
then five little-endian doubles at offsets 5, 13, 21, 29 and 37.
Send total cash = WC + WP, credit = WC, prepaid = WP, total point = 0 and
mileage = GP. These are the fields displayed by NewUIInGameShop.cpp.

Validate header, exact declared size and request subcode before any processing.
Only authenticated players with an entered character can read/open; use the
authenticated account and current game-server identity, never client IDs.
Send standard open success/failure, zero storage count and explicit failures for
unsupported mutations. Native responses use the connection's output lock.

## Validation and rollout

Unit tests cover response bytes/offsets, malformed requests, unauthenticated
access and immutable failure responses. Isolated PostgreSQL tests migrate an
empty throwaway database, verify zeros, all three credits, account/server
isolation, reconnect/reload persistence, concurrent credits, overflow rollback
and repeated/mismatched operation IDs. CI must require these tests, not skip
them. Start a second test server on shifted ports against that same throwaway
database and verify readiness. Never use real local or live data for tests.

Push/open a PR, verify CI, merge through protected master and watch deployment.
Show the exact live data scope before granting only test1/Server 1 +10,000 each,
using a fixed operation UUID for retry safety and a remote before/after backup.
The user has requested implementation and this top-up; do not broaden it to
other servers/accounts. Verify committed balances, then ask the user to reopen
Cash Shop to check the display. Do not automate game input or login.
