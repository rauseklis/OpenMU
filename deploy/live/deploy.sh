#!/usr/bin/env bash
# Run on the server by .github/workflows/deploy-live.yml: backs up the database, then starts the given image.
# Usage: ./deploy.sh ghcr.io/rauseklis/openmu:<commit>
set -euo pipefail

cd "$(dirname "$0")"
image="${1:?usage: deploy.sh <image>}"

# Keep the image in .env so a plain `docker compose up -d` (after a reboot) starts the same version.
if grep -q '^OPENMU_IMAGE=' .env; then
    sed -i "s|^OPENMU_IMAGE=.*|OPENMU_IMAGE=${image}|" .env
else
    echo "OPENMU_IMAGE=${image}" >> .env
fi

./backup.sh
docker compose pull openmu
docker compose up -d
docker image prune -f > /dev/null

# Wait for the game server: the connect server listens when the server is up.
for _ in $(seq 1 60); do
    if (exec 3<>/dev/tcp/127.0.0.1/44405) 2>/dev/null; then
        echo "deployed ${image}: the connect server is listening"
        exit 0
    fi
    sleep 2
done

echo "deployed ${image}, but the connect server is not listening after 2 minutes; check: docker compose logs openmu" >&2
exit 1
