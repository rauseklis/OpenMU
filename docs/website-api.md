# Website API (v1)

A small HTTPS JSON API inside the game server process, for the public website (muemberion.com). The site shows rankings, character
and guild pages, and registers players and checks logins through it, so the site never touches the game database.
**It is off unless configured**: without `OPENMU_WEBSITE_API_PORT` nothing listens and nothing changes.

Code: `src/WebsiteApi` (tests: `tests/MUnique.OpenMU.WebsiteApi.Tests`), started from `src/Startup/Program.cs` (`AddWebsiteApi`).
The design and the reasons are in the client repository: `docs/superpowers/specs/2026-11-07-website-server-link-design.md`.

## How it runs

- Its own Kestrel listener on its own port. The admin panel (`127.0.0.1:8080`) is not touched. A failure to start (port in use,
  unreadable certificate) is logged and the game server runs on without the API.
- TLS with a PFX certificate. Without a certificate the API refuses to start unless `OPENMU_WEBSITE_API_ALLOW_HTTP=true` (tests only).
- Reads are plain `SELECT`s over Npgsql on a connection that can be a **read-only PostgreSQL role** (see below). The only writes are
  creating an account and replacing a password hash, through the normal Entity Framework context of the server.

## Environment variables

| Variable | Meaning |
|---|---|
| `OPENMU_WEBSITE_API_PORT` | Enables the API; the TCP port, e.g. `8443`. |
| `OPENMU_WEBSITE_API_KEYS` | `keyId:secret`, several separated by commas. Each secret at least 32 characters. Never commit it. |
| `OPENMU_WEBSITE_API_CERT_PATH` | Path of the `.pfx` file inside the container. |
| `OPENMU_WEBSITE_API_CERT_PASSWORD` | Password of the `.pfx` (optional). |
| `OPENMU_WEBSITE_API_ADDRESS` | Address to bind, default all interfaces (the firewall decides who may connect). |
| `OPENMU_WEBSITE_API_ALLOWED_IPS` | Optional comma separated list of caller IPs, checked in the application on top of the firewall. |
| `OPENMU_WEBSITE_API_READ_CONNECTION` | Optional Npgsql connection string of a read-only role; without it reads use the server's own connection. |
| `OPENMU_WEBSITE_API_ALLOW_HTTP` | `true` allows plain HTTP. Tests and local use only. |

Misconfiguration (bad port, short secret, no TLS) is logged and leaves the API off.

### Optional read-only role

```sql
CREATE ROLE website_ro LOGIN PASSWORD '<random>';
GRANT CONNECT ON DATABASE openmu TO website_ro;
GRANT USAGE ON SCHEMA data, config, guild TO website_ro;
GRANT SELECT ON data."Account", data."Character", data."StatAttribute", data."CastleSiegeData", data."CastleSiegeGuildRegistration", config."CharacterClass", guild."Guild", guild."GuildMember" TO website_ro;
```

Then `OPENMU_WEBSITE_API_READ_CONNECTION=Host=database;Database=openmu;Username=website_ro;Password=<random>`.
The reads then cannot change anything, even by a bug. (Registration and password change still use the server's own connection.)

## Request signing

Every request carries `X-Key-Id`, `X-Timestamp` (unix seconds, within 60 s of the server clock) and `X-Signature`:

```
signature = lowercase hex HMAC-SHA256(secret, METHOD + "\n" + PATH?QUERY + "\n" + TIMESTAMP + "\n" + SHA256hex(body))
```

`PATH?QUERY` is the request target exactly as sent (including `/api/v1`, query not re-ordered or re-encoded). An empty body hashes as
the empty string. Optional `X-Client-Ip`: the address of the visitor, used for per-visitor limits (trusted only because the request is signed).

- **Replay protection:** a request that changes something (POST) is accepted once; the same signature again within the window gets
  `bad_signature`. Consequence for the site: a retry needs a new timestamp, and two identical POSTs in the same second count as a replay.
  Repeated GETs are allowed (a page that asks for the same list twice in one second is normal and harmless).
- **Limits:** 120 requests/min per client address; verify and password change: 10 attempts per 10 min per account name (and 50 per client
  address), cleared by a success; 5 registrations per hour per client address; 20 failed signatures per 10 min per connection;
  `429 rate_limited` otherwise. Body at most 8 KB.
- Errors are `{ "error": code, "message": text }`: `bad_signature`, `stale_timestamp` (401), `forbidden` (403, not in the allow list),
  `not_found`, `invalid_name`/`invalid_password`/`invalid_email` (422), `name_taken`/`email_taken` (409), `invalid_credentials` (401),
  `rate_limited` (429), `bad_request` (400/413), `server_error` (500). Unsigned requests get the same answer for every path.

## Endpoints (`/api/v1`)

| Request | Answer |
|---|---|
| `GET /status` | `{ online, players, maxPlayers, uptimeSince }` (started game servers of this process) |
| `GET /rankings/players?limit=10..100` | `[{ rank, name, class, level, resets, masterLevel, guild }]`, ordered by resets, master level, level, experience |
| `GET /rankings/guilds?limit=` | `[{ rank, name, score, members, master }]`, ordered by score |
| `GET /characters/{name}` | `{ name, class, level, resets, masterLevel, guild, online, lastSeen }`; `lastSeen` is always `null` (the database stores no such time) |
| `GET /guilds/{name}` | `{ name, score, master, members:[{name,level}], logoPng }`; `logoPng` is base64 of a 64x64 PNG (the 8x8 game logo, enlarged) or `null` |
| `POST /accounts` | body `{ loginName, password, email }` -> `201 { id, loginName }` |
| `POST /auth/verify` | body `{ loginName, password }` -> `{ ok, accountId, loginName, state }` or `401 invalid_credentials` |
| `POST /accounts/password` | body `{ loginName, currentPassword, newPassword }` -> `204` |
| `GET /accounts/{loginName}/characters` | `[{ name, class, level, resets, masterLevel, stats:{str,agi,vit,ene,cmd}, points, hidden, online, pk, pkState }]` (`points` = unspent level-up points, `pk` = killed players, `pkState` = Normal/PlayerKillWarning/…, `hidden` is always `false`: OpenMU has no hide flag); `404` for unknown, banned, template and bot accounts |
| `GET /rankings/killers?limit=` | `[{ rank, name, class, value, guild }]`, `value` = player kills (only characters with kills) |
| `GET /rankings/{online-time,gens,bc,ds,cc,duel}` | `404 not_supported` (the database has no such counters); any other kind `404 not_found` |
| `GET /online` | `[{ name, class, level, guild }]`, at most 200, from the running game servers (visible characters only) |
| `GET /bans` | `[{ name, reason, until }]`: character names with banned character status or of banned accounts; `reason` and `until` are always `null` (not stored), never an account name |
| `GET /search?q=` | `{ characters:[name…10], guilds:[name…10] }`, contains, ignoring the case, `q` 2 to 20 characters (`422 invalid_input` otherwise) |
| `GET /castle-siege` | `{ owner:{guild,master}\|null, nextSiege:null, registered:[{guild,master}] }`; `nextSiege` is always `null` (the schedule is game configuration, not stored) |
| `GET /events` | `404 not_supported` (no schedule is stored) |
| `GET /accounts/{loginName}` | `{ loginName, email, emailVerified, registeredAt }` (ISO UTC); `404` for unknown, banned, template and bot accounts |
| `POST /accounts/email` | body `{ loginName, password, newEmail }` -> `204`; `401 invalid_credentials`, `422 invalid_email`, `409 email_taken`. Sets `emailVerified=false`, voids the account's open tokens. The same address (any case) is a `204` without changes. |
| `POST /accounts/verify-email/request` | body `{ loginName }` -> `{ email, token }`; `404` unknown/banned/template/bot, `409 already_verified`, `422 invalid_email` (no address), `429` after 3 per hour per account |
| `POST /accounts/verify-email/confirm` | body `{ token }` -> `{ loginName }`; `422 invalid_token`. Sets `emailVerified=true` |
| `POST /accounts/reset/request` | body `{ email }` -> `{ found:false }` or `{ found:true, loginName, email, token }`; only normal accounts (not GM, spectator, banned, template, bot) and only if exactly one account has the address (verified or not); `429` after 5 per hour per client address or 3 per hour per address |
| `POST /accounts/reset/confirm` | body `{ token, newPassword }` -> `204`; `422 invalid_token` or `invalid_password` (a wrong new password does not use the token up). Voids all tokens of the account |

**Mail tokens.** The website sends the e-mails; the server only creates and checks tokens. A token is 32 random bytes as 64 lowercase hex
characters, valid 24 h (e-mail verification) or 1 h (password reset), single use, one active token per account and purpose (a new one replaces the
old one). Only its SHA-256 is stored; the token appears only in the response of the two `request` endpoints and is never logged.
A verified e-mail address is **not** required to log in or play. Tokens and the verified flag live in two tables of their own (schema `website`:
`account_email`, `token`, with foreign keys to `data."Account"` that delete their rows with the account). They are created on first use by the
server's own database connection (not by the optional read-only role); OpenMU's own tables are not changed and there is no migration. Existing
accounts start with `emailVerified=false`.

### Rules that were verified in the code

- Login name: the login packet has 10 bytes for it, the client has `MAX_USERNAME_SIZE 10`, and `data."Account"."LoginName"` is `varchar(10)`.
  OpenMU has no character rule for login names; the API accepts letters and digits, **3 to 10**, and treats names that differ only in case
  as the same (the game itself compares exactly, so `Bob` and `bob` would be two accounts).
- Password: the login packet has 20 bytes (`LoginLongPassword`; the older short packet only 10), client `MAX_PASSWORD_SIZE 20`, game
  configuration `MaximumPasswordLength = 20`. The API accepts **8 to 20** printable ASCII characters without spaces. The site's "8-64" must become 8-20.
- E-mail: valid address, at most 100 characters, unique ignoring case.
- A new account is a normal OpenMU `Account`: state `Normal`, `RegistrationDate` = now (so the Auction House's 24 h rule applies), BCrypt
  hash from `BCrypt.Net.BCrypt.HashPassword` (what the admin panel creates; the game checks it with `BCrypt.Verify`), no vault yet (created when first used).
- Hidden from rankings and character pages: accounts that are GM (any GM state), spectator, banned, temporarily banned, template or
  **bot** accounts, and characters with the GM or banned character status. *Bot accounts are hidden by default; whether bots should
  appear as players is an owner decision.*
- `POST /auth/verify` answers identically for unknown name, wrong password, banned and template accounts, and does the same amount of work.
- Never returned: password hashes, security codes, any e-mail address.

## Deploying it (owner steps; nothing here is done by the code or the PR)

1. A name and a certificate: the site calls `https://api.muemberion.com`. Create a DNS `A` record to the VPS and a certificate (for example
   Let's Encrypt with `certbot certonly --standalone`, converted: `openssl pkcs12 -export -out api.pfx -inkey privkey.pem -in fullchain.pem`).
   Put `api.pfx` on the VPS (for example `/opt/openmu/certs/api.pfx`, readable by the container user, uid 1000). Renewal needs a cron job.
2. `.env` on the VPS (`/opt/openmu/.env`, never in git):
   `OPENMU_WEBSITE_API_PORT=8443`, `OPENMU_WEBSITE_API_KEYS=site:<64 random characters>`, `OPENMU_WEBSITE_API_CERT_PATH=/app/certs/api.pfx`,
   `OPENMU_WEBSITE_API_CERT_PASSWORD=...`, optionally `OPENMU_WEBSITE_API_ALLOWED_IPS=<Sovahost egress IP>`.
   The same key id and secret go into the site's configuration (also not in git).
3. `deploy/live/docker-compose.yml` passes the variables, publishes `8443:8443` and mounts `./certs` (done; the compose file is overwritten by every deploy).
   A self-signed certificate works if the site pins it (`CURLOPT_CAINFO` = the public `api.crt`, `CURLOPT_SSL_VERIFYPEER` on); it is valid 10 years.
4. **Who may connect:** Docker publishes ports *past* `ufw`, so a `ufw allow from` rule does **not** restrict 8443. The lock is
   `OPENMU_WEBSITE_API_ALLOWED_IPS=<Sovahost IP>` (checked by the application: others get 403) plus the signature. For a network-level lock add a rule in
   the `DOCKER-USER` iptables chain (needs root).
5. Check: from the host (or `curl` from an allowed IP) a signed `GET /api/v1/status`; without the headers the answer must be `401 bad_signature`.
6. Roll back: remove the variables (the API is off again) or the firewall rule.

## Tests

`dotnet test tests/MUnique.OpenMU.WebsiteApi.Tests`. Unit and HTTP tests (real Kestrel on a loopback port, in-memory store) always run.
The PostgreSQL tests are skipped unless `WEBSITE_API_TEST_CONNECTION_STRING` names a **throwaway** database whose name contains `test`;
it is dropped, created and filled with the seeded game data. Never point it at a real database.
