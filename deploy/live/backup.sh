#!/usr/bin/env bash
# Dumps the live database to /opt/openmu/backups/openmu-<date>.sql.gz and deletes dumps older than 14 days.
# Run by the deploy workflow before every update, and nightly by cron (see README.md).
set -euo pipefail

cd "$(dirname "$0")"
set -a
# shellcheck disable=SC1091
source .env
set +a

mkdir -p backups
file="backups/${DB_NAME:-openmu}-$(date +%Y-%m-%d-%H%M%S).sql.gz"

# A dump that is empty or fails must never look like a backup.
if docker compose exec -T database pg_dump -U "${DB_ADMIN_USER:-postgres}" "${DB_NAME:-openmu}" | gzip > "$file.partial"; then
    if [ "$(stat -c %s "$file.partial")" -gt 1000 ]; then
        mv "$file.partial" "$file"
        echo "backup written: $file"
    else
        rm -f "$file.partial"
        echo "backup skipped: the database is empty or does not exist yet"
    fi
else
    rm -f "$file.partial"
    echo "backup FAILED" >&2
    exit 1
fi

find backups -name '*.sql.gz' -mtime +14 -delete
