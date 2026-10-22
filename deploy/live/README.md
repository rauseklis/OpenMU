# Live server on a VPS (OVHcloud)

One small Linux VPS runs the server in two Docker containers (OpenMU and PostgreSQL). Every merge to `master` builds a new image
and updates the server by itself (`.github/workflows/deploy-live.yml`); the database is backed up before every update.

```
you / your brother merge a PR to master
        └─ GitHub Actions builds the image  →  ghcr.io/<owner>/openmu:<commit>
              └─ copies docker-compose.yml + scripts to the VPS, backs up the database, pulls the image, restarts openmu
```

Players restart-reconnect for about a minute during an update. The database is a Docker volume on the VPS and survives updates.

## 1. Create the VPS (once)

- OVHcloud → **VPS**, Ubuntu 24.04 (or 22.04), at least **2 vCPU / 4 GB RAM / 40 GB disk** for a few players. Note its public IPv4.
- Log in as the user OVH gives you and run:

```bash
# Docker (official convenience script) and a deploy user that may run it
curl -fsSL https://get.docker.com | sudo sh
sudo adduser --disabled-password --gecos "" deploy
sudo usermod -aG docker deploy
sudo mkdir -p /opt/openmu/backups && sudo chown -R deploy:deploy /opt/openmu

# Firewall: SSH and the game ports only (the database and the admin panel are not published)
sudo ufw allow OpenSSH
sudo ufw allow 44405:44406/tcp   # connect server
sudo ufw allow 55901:55906/tcp   # game servers
sudo ufw allow 55980/tcp         # chat server
sudo ufw enable
```

- In the OVH control panel, make sure its own firewall (Network → Edge Network Firewall, if enabled for the VPS) allows the same ports.
- Safer for a start: restrict the game ports to your own two IP addresses (`sudo ufw allow from <your ip> to any port 44405:44406 proto tcp`, and so on) instead of opening them to everybody.

## 2. A key for GitHub to log in with (once)

On your own PC:

```bash
ssh-keygen -t ed25519 -f deploy_key -N "" -C "github-deploy"
```

Put `deploy_key.pub` into `/home/deploy/.ssh/authorized_keys` on the VPS (`sudo -u deploy mkdir -p ~deploy/.ssh`, append the line, `chmod 600`).
Keep `deploy_key` (the private half) for the next step, then delete it from your PC.

## 3. The settings file on the VPS (once)

```bash
sudo -u deploy cp .env.example /opt/openmu/.env     # or paste deploy/live/.env.example
sudo -u deploy nano /opt/openmu/.env                # set PUBLIC_IP, DB_ADMIN_PW and OPENMU_ADMIN_PASSWORD
chmod 600 /opt/openmu/.env
```

Generate the passwords with `openssl rand -base64 24`. Never commit this file.

## 4. The GitHub secrets (once)

Repository → Settings → Secrets and variables → Actions → **New repository secret**:

| Secret | Value |
| --- | --- |
| `DEPLOY_HOST` | the VPS's public IP |
| `DEPLOY_USER` | `deploy` |
| `DEPLOY_SSH_KEY` | the whole content of `deploy_key` (the private key) |
| `DEPLOY_KNOWN_HOSTS` | the output of `ssh-keyscan -H <the VPS IP>`, run on your own PC (so the workflow only talks to your server) |

Until `DEPLOY_HOST` exists the workflow only builds and pushes the image and deploys nothing, so it is safe to merge this first.

Also: GitHub → **Packages** → `openmu` → Package settings → make sure the repository has access, or set the package to public, so the VPS can pull it.

## 5. First deploy

Run the workflow by hand (Actions → **Deploy live server** → Run workflow) or merge anything to `master`. The first start creates and
fills the database, which takes a few minutes. Then check on the VPS:

```bash
cd /opt/openmu && docker compose ps && docker compose logs --tail 50 openmu
```

## Using it

- **Game client**: set `ServerIP` in its `config.ini` to the VPS IP and `ServerPort` to `44406` for the open-source client definition
  (`44405` is the GMO one). The client reads these from `config.ini`, not from `/p`.
- **Admin panel** (accounts, configuration): it is not exposed to the internet. Open a tunnel from your PC,
  `ssh -L 8080:127.0.0.1:8080 deploy@<the VPS IP>`, then browse to `http://localhost:8080` and log in with `OPENMU_ADMIN_USER`/`OPENMU_ADMIN_PASSWORD`.
- **Update**: merge to `master`. Watch it under Actions → Deploy live server.
- **Roll back**: Actions → Deploy live server → run it from an older commit, or on the VPS `./deploy.sh ghcr.io/<owner>/openmu:<older commit>`.
  Database changes of a newer version are not undone by that; restore a backup for that (below).

## Backups

`backup.sh` writes `/opt/openmu/backups/openmu-<date>.sql.gz` before every update and keeps 14 days. Add a nightly one:

```bash
sudo -u deploy crontab -e
# 03:30 every night
30 3 * * * /opt/openmu/backup.sh >> /opt/openmu/backups/backup.log 2>&1
```

Copy the folder off the VPS now and then (`scp`/`rsync` to your PC): a backup on the same disk does not survive losing the VPS.

Restore a dump (stops the game server while it runs):

```bash
cd /opt/openmu
docker compose stop openmu
gunzip -c backups/openmu-<date>.sql.gz | docker compose exec -T database psql -U postgres -d postgres -c 'DROP DATABASE IF EXISTS openmu' -c 'CREATE DATABASE openmu'
gunzip -c backups/openmu-<date>.sql.gz | docker compose exec -T database psql -U postgres -d openmu
docker compose start openmu
```

## Restoring the brother's old gameplay settings

A fresh VPS database does not inherit the old local database's settings or custom
monster spawns. Updating the client or merging the earlier code fixes does not
transfer that configuration. Do not import an old player database to recover it.

`restore-gameplay-profile.sql` is an explicit maintenance tool for the final old
profile: resets enabled with 500 times reset count and a limit of 50; eight
recovery items stacking to 50; unlimited ranged ammunition; nine farming spots of eight monsters each
with no Arena monster spawns added. It preserves
SD/complex stacks of three. Blocked Arena arrivals move to the nearest clear 3x3
floor area, as in the old local installer. EXP, player data, speed formulas and
terrain stay unchanged. Configuration changes affect all players using this profile;
existing potion items are not rewritten. The user must verify reset behavior,
pickup/stack behavior, ammunition and spawn placement in the game.

Show the proposed change to the owner and obtain approval before live use. Back
up the database first with the existing backup procedure. Use `psql -X` with
`ON_ERROR_STOP` (also set by the script), passing the exact connected database:

```bash
# Dry run: validates the complete operation and rolls it back.
psql -X -d openmu -v expected_database=openmu -f restore-gameplay-profile.sql
# Only after the owner's approval and a backup:
psql -X -d openmu -v expected_database=openmu -v apply=true -f restore-gameplay-profile.sql
```

Use the existing private database connection mechanism; never put credentials in
this script. The target name is a typo guard, not permission to modify live data.
The tool refuses missing definitions, blocked/safe spawn locations and conflicting
existing spawn IDs; it never overwrites those spawns. After a successful apply,
restart the game server during the agreed maintenance window to reload settings;
this disconnects players. Check both connect/game greeting ports and verify the
settings and custom spawn counts. The tool is not run by deployments or migrations.

The integration regression is `tests/maintenance/test_restore_gameplay_profile.py`.
It requires a freshly seeded disposable Season 6 database named
`mu_profile_restore_test` on `127.0.0.1:55432`, with test role `muvalidation` and
`psql` on PATH. It never targets the default PostgreSQL port or production.

## Notes

- The live database starts empty. It is not your local `openmu` database; copy one over later with `pg_dump` / `psql` if you want existing characters.
- The server image is built from `src/Startup/Dockerfile`, like the upstream all-in-one image, for linux/amd64.
- Only GitHub's own actions are used by the workflows; the deploy talks to the VPS with plain `ssh` and `scp`.
- Anyone who can push a workflow change to this repository can use the deploy secrets. Keep `master` protected (merges through pull requests only).
